using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class LoopWaitTests
{
    [Fact]
    public async Task Run_WhileRegistersWaitEachIteration_CreatesFreshWaitIds()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var firstWait = await StartAsync(engine, definition);
        var firstWaitId = firstWait.ActiveWaits.Should().ContainSingle().Which.WaitId;

        var secondWait = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "first"),
            TestContext.Current.CancellationToken);

        secondWait.Status.Should().Be(WorkflowStatus.Waiting);
        secondWait.ActiveWaits.Should().ContainSingle()
            .Which.WaitId.Should().NotBe(firstWaitId);
    }

    [Fact]
    public async Task RaiseEventAsync_EventForPreviousIteration_DoesNotResumeLaterIteration()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var firstWait = await StartAsync(engine, definition);
        var secondWait = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "first"),
            TestContext.Current.CancellationToken);

        var stale = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "stale"),
            TestContext.Current.CancellationToken);

        stale.Status.Should().Be(WorkflowStatus.Waiting);
        stale.ActiveWaits.Should().ContainSingle()
            .Which.CorrelationId.Should().Be(IterationCorrelation(1));
        state.Payloads.Should().Equal(["first"]);
    }

    [Fact]
    public async Task RaiseEventAsync_CurrentIterationEvent_ResumesCurrentWait()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var firstWait = await StartAsync(engine, definition);
        await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "first"),
            TestContext.Current.CancellationToken);

        var completed = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(1), "second"),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["first", "second"]);
    }

    [Fact]
    public async Task Mailbox_PreviousIterationEvent_RemainsStaleForLaterWait()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var firstWait = await StartAsync(engine, definition);
        await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "first"),
            TestContext.Current.CancellationToken);
        await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "stale"),
            TestContext.Current.CancellationToken);

        var completed = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(1), "second"),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ErrorSummary.Should().BeNull();
        state.Payloads.Should().Equal(["first", "second"]);
    }

    [Fact]
    public async Task RaiseEventAsync_WaitInsideWhile_DoesNotReExecuteStepsBeforeWaitOnResume()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = NoDoubleExecutionDefinition(state);
        engine.RegisterDefinition(definition);

        var firstWait = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        var firstWaitId = engine.Management.Instance(firstWait.InstanceId)
            .GetActiveWaits()
            .Should().ContainSingle().Which.WaitId;

        state.PreWaitCount.Should().Be(1);
        state.PostWaitCount.Should().Be(0);

        var secondWait = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Approval", new CorrelationId("corr-1"), "first"),
            TestContext.Current.CancellationToken);
        var secondWaitId = engine.Management.Instance(firstWait.InstanceId)
            .GetActiveWaits()
            .Should().ContainSingle().Which.WaitId;

        secondWait.Status.Should().Be(WorkflowStatus.Waiting);
        secondWaitId.Should().NotBe(firstWaitId);
        state.PreWaitCount.Should().Be(2);
        state.PostWaitCount.Should().Be(1);

        var completed = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Approval", new CorrelationId("corr-1"), "second"),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(firstWait.InstanceId).GetActiveWaits().Should().BeEmpty();
        state.PreWaitCount.Should().Be(2);
        state.PostWaitCount.Should().Be(2);
    }

    private static Task<WorkflowInstanceSnapshot> StartAsync(
        EphemeralWorkflowEngine engine,
        OrcaCore.Core.Definitions.WorkflowDefinition<TestState> definition)
    {
        return engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .While(
                current => current.Iteration < 2,
                body => body
                    .Wait("Tick", current => IterationCorrelation(current.Iteration))
                    .Then(() => new CaptureAndIncrementStep()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> NoDoubleExecutionDefinition(TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .While(
                current => current.PostWaitCount < 2,
                body => body
                    .Then(() => new CountPreWaitStep())
                    .Wait("Approval", _ => new CorrelationId("corr-1"))
                    .Then(() => new CountPostWaitStep()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static CorrelationId IterationCorrelation(int iteration)
    {
        return new CorrelationId($"iteration-{iteration}");
    }

    private static EventEnvelope Event(string name, CorrelationId correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = name,
            CorrelationId = correlationId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public int Iteration { get; set; }

        public List<string> Payloads { get; } = [];

        public int PreWaitCount { get; set; }

        public int PostWaitCount { get; set; }
    }

    private sealed class CaptureAndIncrementStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Payloads.Add(payload);
            }

            context.State.Iteration++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CountPreWaitStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.PreWaitCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CountPostWaitStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.PostWaitCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
