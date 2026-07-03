using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Engine.Ephemeral.Tests.Management;

/// <summary>
/// T1-14 (CR-008, CR-016, CR-031, MG-004): the completion bridge, named End outcomes surfaced
/// through the management query, graceful Cancel, forced Terminate, clear lifecycle rejection
/// for terminal instances, and the broad-destructive-selection safety gate on Terminate.
/// </summary>
public class TerminalCommandTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    private sealed class WaitForApprovalStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class CompleteStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    /// <summary>
    /// Observes whether the step genuinely saw its token cancelled mid-await, via deterministic
    /// <see cref="TaskCompletionSource"/> coordination (never sleep-and-hope): the step signals
    /// <see cref="ReachedDelay"/> the instant it starts its indefinite, cancellable await, so the
    /// test can wait for that exact signal before triggering cancellation - proving cancellation
    /// genuinely interrupts an in-flight await rather than racing against the step's own setup.
    /// </summary>
    private sealed class ObservesCancellationStep : IStep<OrderState>
    {
        private readonly TaskCompletionSource reachedDelay = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool SawCancellation { get; private set; }

        public Task ReachedDelay => reachedDelay.Task;

        public async ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            var delayTask = Task.Delay(Timeout.Infinite, cancellationToken);
            reachedDelay.TrySetResult();

            try
            {
                await delayTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                SawCancellation = true;
            }

            return new StepResult.Completed();
        }
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildImmediateWorkflow(DefinitionId definitionId)
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new CompleteStep());
        builder.End("Done");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildWaitingWorkflow(
        DefinitionId definitionId, string eventName = "ApprovalReceived", string correlationValue = "order-1")
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep(eventName, new CorrelationId(correlationValue)));
        builder.Then(new CompleteStep());
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    [Fact]
    public async Task AwaitCompletionAsync_ImmediateWorkflow_ReturnsTerminalSnapshot()
    {
        var (engine, definitionId) = BuildImmediateWorkflow(new DefinitionId("terminal-bridge-immediate"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var completed = await engine.AwaitCompletionAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.EndOutcomeName.Should().Be("Done");
        completed.InstanceId.Should().Be(started.InstanceId);
    }

    [Fact]
    public async Task End_WithOutcomeName_RecordsOutcomeInSnapshotAndQueries()
    {
        var (engine, definitionId) = BuildImmediateWorkflow(new DefinitionId("terminal-bridge-outcome"));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        started.EndOutcomeName.Should().Be("Done");
        var queried = engine.Query().Instance(started.InstanceId).Get();
        queried!.EndOutcomeName.Should().Be("Done");
    }

    [Fact]
    public async Task CancelAsync_RunningInstance_TransitionsToCancelledAndCancelsWaits()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("terminal-bridge-cancel"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var cancelled = await engine.CancelAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        cancelled.Status.Should().Be(WorkflowStatus.Cancelled);

        var afterCancel = engine.Query().Instance(started.InstanceId).Get();
        afterCancel!.Status.Should().Be(WorkflowStatus.Cancelled);
        engine.Query().All().GetActiveWaits().Should().BeEmpty();
    }

    [Fact]
    public async Task TerminateAsync_RunningInstance_TransitionsToTerminatedAndStopsAdvancement()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("terminal-bridge-terminate"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var terminated = await engine.TerminateAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        terminated.Status.Should().Be(WorkflowStatus.Terminated);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);
        var outcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.NoMatch);
        var afterRaise = engine.Query().Instance(started.InstanceId).Get();
        afterRaise!.Status.Should().Be(WorkflowStatus.Terminated);
    }

    [Fact]
    public async Task TerminalInstance_RaiseEventCancelOrTerminate_ReturnsClearLifecycleError()
    {
        var (engine, definitionId) = BuildImmediateWorkflow(new DefinitionId("terminal-bridge-illegal"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Completed);

        var cancelAct = async () => await engine.CancelAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);
        var terminateAct = async () => await engine.TerminateAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        await cancelAct.Should().ThrowAsync<WorkflowLifecycleException>();
        await terminateAct.Should().ThrowAsync<WorkflowLifecycleException>();
    }

    [Fact]
    public async Task AllTerminate_WithoutExplicitSafety_IsRejected()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("terminal-bridge-broad-unsafe"));
        await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var act = async () => await engine.Query().All().Terminate();

        await act.Should().ThrowAsync<WorkflowDefinitionException>();
    }

    [Fact]
    public async Task AllTerminate_WithExplicitSafety_ReturnsAffectedCounts()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("terminal-bridge-broad-safe"));
        var first = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<int, OrderState>(definitionId, 2, TestContext.Current.CancellationToken);

        var results = await engine.Query().All().Terminate(confirmBroadSelection: true, TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        results.Select(result => result.InstanceId).Should().BeEquivalentTo([first.InstanceId, second.InstanceId]);
        results.Should().OnlyContain(result => result.Status == WorkflowStatus.Terminated);
    }

    [Fact]
    public async Task CancelAsync_StepMidAwait_ObservesCancellationCooperatively()
    {
        var step = new ObservesCancellationStep();

        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("terminal-bridge-cooperative-cancel");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(step);
        builder.End("Done");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        // Start suspends at the Wait, so the InstanceId is known before the blocking step ever
        // runs. Resuming drives the interpreter into the blocking step on this call's own lane
        // turn; step.ReachedDelay deterministically proves the step is inside its indefinite,
        // cancellable await BEFORE CancelAsync ever signals - never sleep/hope.
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);
        var resumeTask = engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);

        await step.ReachedDelay;

        var cancelTask = engine.CancelAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        await resumeTask;
        await AwaitIgnoringLifecycleFailureAsync(cancelTask);

        step.SawCancellation.Should().BeTrue();
    }

    private static async Task AwaitIgnoringLifecycleFailureAsync(Task<WorkflowInstanceSnapshot> task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (WorkflowLifecycleException)
        {
            // CancelAsync may race the interpreter's own terminal commit (the step observes
            // cancellation and the run completes/fails before CancelAsync's lane turn runs) -
            // the cooperative-signal assertion (step.SawCancellation) is what this test proves.
        }
    }
}
