using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableDeadlineExecutionTests
{
    [Fact]
    public async Task CompleteWithin_ExpiresAfterHostReplacement_AtOriginalAbsoluteDeadline()
    {
        var startedAt = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        var clock = new Clock(startedAt);
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .CompleteWithin(TimeSpan.FromMinutes(5))
            .Delay(TimeSpan.FromMinutes(10))
            .End()
            .Build();
        var runtimeDefinition = (WorkflowDefinition<State>)definition.RuntimeDefinition;
        var firstHost = CreateRuntime(store, clock);
        firstHost.RegisterDefinition(runtimeDefinition);

        var waiting = await firstHost.StartOrGetAsync<string, State>(
            "durable-global-deadline",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));

        var due = await store.ClaimDueAsync(
            new TimerClaimRequest(clock.Now, 10, clock.Now, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        var replacementProcessor = new DurableCommandProcessor(store);
        foreach (var fire in due)
        {
            await replacementProcessor.ProcessAsync(fire, TestContext.Current.CancellationToken);
            await store.CompleteAsync(fire.TimerId, TestContext.Current.CancellationToken);
        }

        var replacementHost = CreateRuntime(store, clock, replacementProcessor);
        replacementHost.RegisterDefinition(runtimeDefinition);
        await replacementHost.StartOrGetAsync<string, State>(
            "durable-global-deadline",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        due.Should().ContainSingle();
        snapshot.Status.Should().Be(WorkflowStatus.TimedOut);
        snapshot.ErrorSummary.Should().Contain("WF-DEADLINE-EXCEEDED");
    }

    [Fact]
    public async Task CompleteWithin_PreservesAbsoluteDeadlineAcrossContinueAsNew()
    {
        var startedAt = new DateTimeOffset(2026, 7, 29, 12, 30, 0, TimeSpan.Zero);
        var clock = new Clock(startedAt);
        var store = new InMemoryWorkflowProvider();
        var publicDefinition = global::OrcaCore.Workflow.Durable<GenerationState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<int>(_ => new GenerationState(0))
            .CompleteWithin(TimeSpan.FromMinutes(5))
            .ContinueAsNew(snapshot => new GenerationState(snapshot.Value.Generation + 1))
            .Build();
        var definition = (WorkflowDefinition<GenerationState>)publicDefinition.RuntimeDefinition;
        var processor = new DurableCommandProcessor(store);
        var runtime = CreateRuntime(store, clock, processor);
        runtime.RegisterDefinition(definition);
        var serializer = new JsonWorkflowPayloadSerializer();
        var start = await new DurableStartService(processor).StartOrGetAsync(
            new StartOrGetRequest(
                "durable-deadline-rollover",
                definition.DefinitionId,
                definition.DefinitionVersion,
                serializer.Serialize(0),
                clock.Now),
            TestContext.Current.CancellationToken);

        (await runtime.DriveAsync(
            start.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken)).Outcome
            .Should().Be(DurableSegmentOutcome.PolicyBoundary);
        var beforeRollover = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                start.InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload);
        (await runtime.DriveAsync(
            start.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken)).Outcome
            .Should().Be(DurableSegmentOutcome.ContinuedAsNew);
        var afterRollover = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                start.InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload);

        beforeRollover.WorkflowDeadline.Should().Be(startedAt.AddMinutes(5));
        afterRollover.WorkflowDeadline.Should().Be(beforeRollover.WorkflowDeadline);
        afterRollover.WorkflowDeadlineTimerId.Should().Be(beforeRollover.WorkflowDeadlineTimerId);
        afterRollover.ContinueAsNewGeneration.Should().Be(1);
    }

    [Fact]
    public async Task StructuralWaitTimeout_WinsAfterHostReplacement_AndFailsWithTypedCode()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Wait(
                EventName.Create("approval"),
                _ => CorrelationId.Create("order-1"),
                TimeSpan.FromMinutes(1))
            .End()
            .Build();
        var runtimeDefinition = (WorkflowDefinition<State>)definition.RuntimeDefinition;
        var firstHost = CreateRuntime(store, clock);
        firstHost.RegisterDefinition(runtimeDefinition);

        var waiting = await firstHost.StartOrGetAsync<string, State>(
            "durable-wait-timeout",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));

        var due = await store.ClaimDueAsync(
            new TimerClaimRequest(clock.Now, 10, clock.Now, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        var replacementProcessor = new DurableCommandProcessor(store);
        foreach (var fire in due)
        {
            await replacementProcessor.ProcessAsync(fire, TestContext.Current.CancellationToken);
            await store.CompleteAsync(fire.TimerId, TestContext.Current.CancellationToken);
        }

        var replacementHost = CreateRuntime(store, clock, replacementProcessor);
        replacementHost.RegisterDefinition(runtimeDefinition);
        await replacementHost.StartOrGetAsync<string, State>(
            "durable-wait-timeout",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        due.Should().ContainSingle();
        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("WF-WAIT-TIMEOUT");
        snapshot.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    public async Task StepCoordinate_IsCommittedBeforeFirstDispatch()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 14, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider();
        var gate = new DispatchGate();
        var definition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Then(() => new GatedStep(gate))
            .End()
            .Build();
        var runtime = CreateRuntime(store, clock);
        runtime.RegisterDefinition(definition);

        var run = runtime.StartOrGetAsync<string, State>(
            "durable-step-coordinate",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        await gate.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var started = await store.GetStartedAsync(
            "durable-step-coordinate",
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            started.Value.InstanceId,
            TestContext.Current.CancellationToken);
        var fiber = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload)
            .Fibers.Should().ContainSingle().Subject;

        fiber.LogicalOperationKey.Should().NotBeNullOrWhiteSpace();
        fiber.RetryAttempt.Should().Be(1);
        fiber.AttemptInFlight.Should().BeTrue();

        gate.Release.TrySetResult();
        var completed = await run;
        (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single().Status
            .Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task TimedOutTokenIgnoringAttempt_IsFencedWhileRetryCommitsDetachedWinner()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 15, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider();
        var gate = new LateAttemptGate();
        var definition = global::OrcaCore.Workflow.Durable<MutableState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new MutableState())
            .WithRetry(2)
            .WithTimeout(TimeSpan.FromMinutes(1))
            .Then(() => new TokenIgnoringRetryStep(gate))
            .End()
            .Build();
        var runtime = CreateRuntime(store, clock);
        runtime.RegisterDefinition(definition);

        var run = runtime.StartOrGetAsync<string, MutableState>(
            "durable-late-attempt-fencing",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        await gate.FirstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));

        await gate.SecondStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var completed = await run;
        gate.ReleaseFirst.TrySetResult();
        await gate.FirstFinished.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
        var state = JsonSerializer.Deserialize<MutableState>(envelope.StatePayload)!;

        gate.OperationIds.Should().HaveCount(2);
        gate.OperationIds.Distinct().Should().ContainSingle();
        gate.AttemptNumbers.Should().Equal(1, 2);
        state.Log.Should().Equal("winner");
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        Clock clock,
        DurableCommandProcessor? processor = null)
    {
        processor ??= new DurableCommandProcessor(store);
        return new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            new JsonWorkflowPayloadSerializer());
    }

    private sealed class State;

    private sealed record GenerationState(int Generation);

    private sealed class MutableState
    {
        public List<string> Log { get; set; } = [];
    }

    private sealed class DispatchGate
    {
        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedStep(DispatchGate gate) : IStep<State>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            gate.Started.TrySetResult();
            await gate.Release.Task.WaitAsync(cancellationToken);
            return new StepResult.Completed();
        }
    }

    private sealed class LateAttemptGate
    {
        internal TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource FirstFinished { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal List<string> OperationIds { get; } = [];

        internal List<int> AttemptNumbers { get; } = [];
    }

    private sealed class TokenIgnoringRetryStep(LateAttemptGate gate) : IStep<MutableState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<MutableState> context,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                gate.OperationIds.Add(context.Execution.OperationId.ToString());
                gate.AttemptNumbers.Add(context.Execution.AttemptNumber);
            }

            if (context.Execution.AttemptNumber == 1)
            {
                gate.FirstStarted.TrySetResult();
                await gate.ReleaseFirst.Task;
                context.State.Log.Add("late");
                gate.FirstFinished.TrySetResult();
                return new StepResult.Completed();
            }

            gate.SecondStarted.TrySetResult();
            context.State.Log.Add("winner");
            return new StepResult.Completed();
        }
    }
}
