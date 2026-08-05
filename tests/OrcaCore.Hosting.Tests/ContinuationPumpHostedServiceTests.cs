using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

/// <summary>
/// DR-P2 lane-host lifecycle: graceful drain on shutdown (DR-033/DR-AC-011).
/// </summary>
public sealed class ContinuationPumpHostedServiceTests
{
    private sealed class DrainState
    {
        public string OrderId { get; set; } = "order-drain";

        public List<string> Log { get; set; } = [];
    }

    private sealed class GatedStep : IStep<DrainState>
    {
        internal static TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal static TaskCompletionSource Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal static int Executions;

        public async ValueTask<StepResult> ExecuteAsync(StepContext<DrainState> context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Executions);
            Started.TrySetResult();
            await Gate.Task.ConfigureAwait(false);
            context.State.Log.Add("gated");
            return new StepResult.Completed();
        }
    }

    private sealed class AfterGateStep : IStep<DrainState>
    {
        internal static int Executions;

        public ValueTask<StepResult> ExecuteAsync(StepContext<DrainState> context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Executions);
            context.State.Log.Add("after-gate");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed record HostHandle(
        DurableWorkflowRuntime Runtime,
        DurableCommandProcessor Processor,
        DurableContinuationPump Pump);

    private static HostHandle CreateHost(
        InMemoryWorkflowProvider store,
        FakeTimeProvider timeProvider,
        DefinitionId definitionId)
    {
        var processor = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            timeProvider);
        runtime.RegisterDefinition(global::OrcaCore.Workflow.Durable<DrainState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(orderId => new DrainState { OrderId = orderId ?? "order-drain" })
            .Then<GatedStep>()
            .Then<AfterGateStep>()
            .End("drained")
            .Build());
        var pump = new DurableContinuationPump(store, runtime, processor, timeProvider);
        return new HostHandle(runtime, processor, pump);
    }

    [Fact]
    [Trait("AC", "DR-AC-011")]
    public async Task GracefulShutdownMidSegment_CurrentCommitFinishes_SurvivingHostCompletes()
    {
        GatedStep.Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        GatedStep.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        GatedStep.Executions = 0;
        AfterGateStep.Executions = 0;

        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider(timeProvider);
        var definitionId = DefinitionId.New();
        var hostA = CreateHost(store, timeProvider, definitionId);
        var service = new OrcaCoreContinuationPumpHostedService(
            hostA.Pump,
            Options.Create(new OrcaCoreHostedServiceOptions
            {
                ContinuationPumpInterval = TimeSpan.FromSeconds(1),
                ContinuationDrainTimeout = TimeSpan.FromSeconds(5)
            }),
            timeProvider,
            NullLogger<OrcaCoreContinuationPumpHostedService>.Instance);
        await service.StartAsync(TestContext.Current.CancellationToken);

        // The start commit leaves the instance runnable; the hosted pump claims its
        // continuation record on the next tick and blocks inside the first step, holding the
        // segment mid-flight.
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        await hostA.Processor.ProcessAsync(
            new StartWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = timeProvider.GetUtcNow(),
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial
            },
            TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await GatedStep.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Stop requested mid-segment: claiming stops, the drain window elapses, then the
        // in-flight step finishes Ã¢â‚¬â€ its commit must still land, and nothing new may start.
        var stopTask = service.StopAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        GatedStep.Gate.TrySetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var tail = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId), StreamVersion.Empty, TestContext.Current.CancellationToken);
        tail.OfType<WorkflowStepCompletedEvent>().Should().ContainSingle(
            "the in-flight commit finishes during the drain");
        AfterGateStep.Executions.Should().Be(0, "nothing new starts after the stop request");

        var hostB = CreateHost(store, timeProvider, definitionId);
        await hostB.Pump.PumpOnceAsync(
            new OutboxClaimRequest(100, timeProvider.GetUtcNow(), TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);

        var snapshots = await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);
        snapshots.Should().ContainSingle().Which.Status.Should().Be(
            WorkflowStatus.Completed, "the surviving host completes the instance");
        GatedStep.Executions.Should().Be(1, "the committed step never re-runs on the surviving host");
        AfterGateStep.Executions.Should().Be(1);
    }
}
