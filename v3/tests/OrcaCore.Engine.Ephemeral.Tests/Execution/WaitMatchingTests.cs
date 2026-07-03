using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport.Concurrency;
using OrcaCore.TestSupport.Time;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public class WaitMatchingTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
        public EventEnvelope? ObservedResumedEvent { get; set; }
        public List<EventEnvelope?> ObservedResumedEventPerStep { get; } = [];
    }

    private sealed class RecordingStep(string tag) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Executed.Add(tag);
            context.State.ObservedResumedEventPerStep.Add(context.ResumedEvent);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class WaitForApprovalStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class CaptureResumedEventStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.ObservedResumedEvent = context.ResumedEvent;
            context.State.Executed.Add("after-wait");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static WorkflowInstance<OrderState> NewInstance(Clock clock) =>
        new(
            InstanceId.New(),
            new DefinitionId("order-workflow"),
            new DefinitionVersion(1),
            new OrderState(),
            clock.Now);

    [Fact]
    public async Task Run_WaitResult_RegistersActiveWaitAndSetsWaiting()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Waiting);
        instance.ActiveWait.Should().NotBeNull();
        instance.ActiveWait!.EventName.Should().Be("ApprovalReceived");
        instance.ActiveWait!.CorrelationId.Should().Be(new CorrelationId("order-1"));
    }

    [Fact]
    public async Task RaiseEventAsync_MatchingEvent_ResumesAndClearsWait()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new CaptureResumedEventStep());
        builder.End("Approved");
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(
            EventId.New(),
            "ApprovalReceived",
            new CorrelationId("order-1"),
            Payload: "approved-payload",
            clock.Now);

        var outcome = await interpreter.TryResumeAsync(
            instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed);
        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.ActiveWait.Should().BeNull();
        instance.State.Executed.Should().Equal("after-wait");
    }

    [Fact]
    public async Task RaiseEventAsync_MatchingEvent_ProvidesPayloadToNextStepOnly()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new CaptureResumedEventStep());
        builder.Then(new RecordingStep("second-after-wait"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(
            EventId.New(),
            "ApprovalReceived",
            new CorrelationId("order-1"),
            Payload: "approved-payload",
            clock.Now);

        await interpreter.TryResumeAsync(
            instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.State.ObservedResumedEvent.Should().Be(envelope);
        instance.State.ObservedResumedEventPerStep.Should().HaveCount(1);
        instance.State.ObservedResumedEventPerStep[0].Should().BeNull();
    }

    [Fact]
    public async Task RaiseEventAsync_WrongNameOrCorrelation_LeavesInstanceWaiting()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new RecordingStep("after-wait"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        var wrongName = new EventEnvelope(EventId.New(), "SomethingElse", new CorrelationId("order-1"), null, clock.Now);
        var wrongCorrelation = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-2"), null, clock.Now);

        var outcomeWrongName = await interpreter.TryResumeAsync(
            instance, definition, wrongName, clock.TimeProvider, TestContext.Current.CancellationToken);
        var outcomeWrongCorrelation = await interpreter.TryResumeAsync(
            instance, definition, wrongCorrelation, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcomeWrongName.Should().Be(RaiseEventOutcome.NoMatch);
        outcomeWrongCorrelation.Should().Be(RaiseEventOutcome.NoMatch);
        instance.Status.Should().Be(WorkflowStatus.Waiting);
        instance.ActiveWait.Should().NotBeNull();
        instance.State.Executed.Should().BeEmpty();
    }

    [Fact]
    public async Task RaiseEventAsync_TwoConcurrentMatches_OnlyOneContinuationCommits()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("concurrent-wait-workflow");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new RecordingStep("resumed"));
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);

        var coordinator = new RaceCoordinator();
        var firstEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "first", DateTimeOffset.UnixEpoch);
        var secondEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "second", DateTimeOffset.UnixEpoch);

        async Task<RaiseEventOutcome> RaiseAsync(EventEnvelope envelope)
        {
            await coordinator.ArriveAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            return await engine.RaiseEventAsync<OrderState>(snapshot.InstanceId, envelope, TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
        }

        var firstTask = RaiseAsync(firstEnvelope);
        var secondTask = RaiseAsync(secondEnvelope);

        var outcomes = await Task.WhenAll(firstTask, secondTask);

        outcomes.Count(outcome => outcome == RaiseEventOutcome.Resumed).Should().Be(1);
        outcomes.Count(outcome => outcome == RaiseEventOutcome.NoMatch).Should().Be(1);
    }
}
