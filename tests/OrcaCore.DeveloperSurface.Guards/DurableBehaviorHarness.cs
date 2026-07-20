using System.Collections.Concurrent;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.DeveloperSurface.Guards;

internal sealed record DurableGuardHost(
    DurableWorkflowRuntime Runtime,
    DurableCommandProcessor Processor,
    DurableContinuationPump Pump,
    DurableDefinitionRegistry Registry,
    InMemoryWorkflowProvider Store,
    InMemoryResourcePoolStore Pools,
    ManualTimeProvider Clock);

internal static class DurableBehaviorHarness
{
    internal static readonly DateTimeOffset Epoch = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    internal static DurableGuardHost CreateHost(
        InMemoryWorkflowProvider? store = null,
        InMemoryResourcePoolStore? pools = null,
        ManualTimeProvider? clock = null,
        WorkflowDefinition<GuardState>? definition = null)
    {
        clock ??= new ManualTimeProvider(Epoch);
        store ??= new InMemoryWorkflowProvider(clock);
        pools ??= new InMemoryResourcePoolStore();
        var processor = new DurableCommandProcessor(store, pools);
        var management = new DurableManagement(store, pools, store, processor);
        var registry = new DurableDefinitionRegistry();
        var runtime = new DurableWorkflowRuntime(
            processor,
            registry,
            clock,
            new JsonWorkflowPayloadSerializer(),
            projectionStore: store,
            management: management);
        if (definition is not null)
        {
            runtime.RegisterDefinition(definition);
        }

        return new DurableGuardHost(
            runtime,
            processor,
            new DurableContinuationPump(store, runtime, processor, clock),
            registry,
            store,
            pools,
            clock);
    }

    internal static async Task<InstanceId> StartAndPumpAsync(
        DurableGuardHost host,
        DefinitionId definitionId,
        CancellationToken cancellationToken)
    {
        var instanceId = InstanceId.New();
        var result = await host.Processor.ProcessAsync(
            new StartWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = host.Clock.GetUtcNow(),
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial
            },
            cancellationToken);
        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        (await PumpOnceAsync(host, cancellationToken)).Should().BeGreaterThan(0);
        return instanceId;
    }

    internal static Task<int> PumpOnceAsync(DurableGuardHost host, CancellationToken cancellationToken) =>
        host.Pump.PumpOnceAsync(
            new OutboxClaimRequest(100, host.Clock.GetUtcNow(), TimeSpan.FromMinutes(5)),
            cancellationToken);

    internal static async Task<WorkflowInstanceSnapshot> SnapshotAsync(
        DurableGuardHost host,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var snapshots = await host.Store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            cancellationToken);
        return snapshots.Should().ContainSingle().Subject;
    }

    internal static Task<IReadOnlyList<WorkflowEvent>> TailAsync(
        DurableGuardHost host,
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        host.Store.LoadTailAsync(new WorkflowStreamId(instanceId), StreamVersion.Empty, cancellationToken);

    internal static WorkflowDefinition<GuardState> ExternalJobDefinition(DefinitionId definitionId, string key) =>
        Workflow.Durable<GuardState>(definitionId, DefinitionVersion.Initial)
            .Init<object?>(_ => new GuardState { Key = key })
            .Then<DispatchExternalJobStep>()
            .Then<ObserveExternalJobCompletionStep>()
            .End("job-complete")
            .Build();

    internal static WorkflowDefinition<GuardState> WaitDefinition(
        DefinitionId definitionId,
        string key,
        string eventName) =>
        Workflow.Durable<GuardState>(definitionId, DefinitionVersion.Initial)
            .Init<object?>(_ => new GuardState { Key = key })
            .Wait(eventName, state => new CorrelationId(state.Key))
            .End("matched")
            .Build();

    internal sealed class GuardState
    {
        public string Key { get; set; } = string.Empty;

        public List<string> Log { get; set; } = [];
    }

    internal sealed class DispatchExternalJobStep : IStep<GuardState>
    {
        internal static readonly ConcurrentDictionary<string, int> Executions = new();

        public ValueTask<StepResult> ExecuteAsync(
            StepContext<GuardState> context,
            CancellationToken cancellationToken)
        {
            Executions.AddOrUpdate(context.State.Key, 1, (_, count) => count + 1);
            return ValueTask.FromResult<StepResult>(
                new StepResult.RunExternalJob(context.State.Key, [1, 2, 3]));
        }
    }

    internal sealed class ObserveExternalJobCompletionStep : IStep<GuardState>
    {
        internal static readonly ConcurrentDictionary<string, int> Executions = new();

        public ValueTask<StepResult> ExecuteAsync(
            StepContext<GuardState> context,
            CancellationToken cancellationToken)
        {
            Executions.AddOrUpdate(context.State.Key, 1, (_, count) => count + 1);
            context.State.Log.Add(context.ResumedEvent?.EventName ?? "missing-completion-event");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset current = start;

    public override DateTimeOffset GetUtcNow() => current;

    internal void Advance(TimeSpan duration) => current += duration;
}
