using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Management;

public sealed class TerminalCommandTests
{
    [Fact]
    public async Task AwaitCompletionAsync_ImmediateWorkflow_ReturnsTerminalSnapshot()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.AwaitCompletionAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.InstanceId.Should().NotBe(default);
    }

    [Fact]
    public async Task End_WithOutcomeName_RecordsOutcomeInSnapshotAndQueries()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .End("Approved")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        snapshot.EndOutcomeName.Should().Be("Approved");
        engine.Management.All()
            .Where(instance => instance.EndOutcomeName == "Approved")
            .List()
            .Should().ContainSingle()
            .Which.InstanceId.Should().Be(snapshot.InstanceId);
    }

    [Fact]
    public async Task CancelAsync_RunningInstance_TransitionsToCancelledAndCancelsWaits()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var cancelled = await engine.Management.Instance(started.InstanceId)
            .CancelAsync(TestContext.Current.CancellationToken);

        cancelled.Status.Should().Be(WorkflowStatus.Cancelled);
        cancelled.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-014")]
    public async Task CancelAsync_InFlightStep_SignalsStepCancellationToken()
    {
        var step = new CancellableStep();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Wait("Ready", state => new CorrelationId(state.Name))
            .Then(() => step)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var delivery = engine.RaiseEventAsync<TestState>(
            started.InstanceId,
            Event("Ready", "wait"),
            TestContext.Current.CancellationToken);
        await step.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var cancelled = await engine.Management.Instance(started.InstanceId)
            .CancelAsync(TestContext.Current.CancellationToken);

        cancelled.Status.Should().Be(WorkflowStatus.Cancelled);
        step.ObservedCancellation.Task.IsCompletedSuccessfully.Should().BeTrue();
        await delivery.Invoking(task => task.WaitAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TerminateAsync_RunningInstance_TransitionsToTerminatedAndStopsAdvancement()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var terminated = await engine.Management.Instance(started.InstanceId)
            .TerminateAsync(TestContext.Current.CancellationToken);
        var act = () => engine.RaiseEventAsync<TestState>(
            started.InstanceId,
            Event("Ready", "wait"),
            TestContext.Current.CancellationToken);

        terminated.Status.Should().Be(WorkflowStatus.Terminated);
        terminated.ActiveWaits.Should().BeEmpty();
        await act.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*terminal*");
    }

    [Fact]
    public async Task TerminalInstance_RaiseEventCancelOrTerminate_ReturnsClearLifecycleError()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var completed = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        var raise = () => engine.RaiseEventAsync<TestState>(
            completed.InstanceId,
            Event("Ready", "done"),
            TestContext.Current.CancellationToken);
        var cancel = () => engine.Management.Instance(completed.InstanceId)
            .CancelAsync(TestContext.Current.CancellationToken);
        var terminate = () => engine.Management.Instance(completed.InstanceId)
            .TerminateAsync(TestContext.Current.CancellationToken);

        await raise.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*terminal*");
        await cancel.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*Cancel*Completed*");
        await terminate.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*Terminate*Completed*");
    }

    [Fact]
    public async Task AllTerminate_WithoutExplicitSafety_IsRejected()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var act = () => engine.Management.All().TerminateAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*explicit safety*");
    }

    [Fact]
    public async Task AllTerminate_WithExplicitSafety_ReturnsAffectedCounts()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);

        var report = await engine.Management.All()
            .TerminateAsync(DestructiveCommandSafety.Confirmed, TestContext.Current.CancellationToken);

        report.AffectedCount.Should().Be(2);
        report.Results.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Terminated);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> CompletedDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Then(() => new CaptureStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Wait("Ready", state => new CorrelationId(state.Name))
            .Then(() => new CaptureStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(string eventName, string correlationId)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = eventName,
            CorrelationId = new CorrelationId(correlationId),
            Payload = null,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CaptureStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CancellableStep : IStep<TestState>
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ObservedCancellation { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation.TrySetResult();
                throw;
            }

            return new StepResult.Completed();
        }
    }
}
