using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class WaitMatchingTests
{
    private static readonly CorrelationId Correlation = CorrelationId.Create("order-123");

    [Fact]
    public async Task Run_WaitResult_RegistersActiveWaitAndSetsWaiting()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Then(() => new WaitResultStep())
            .Then(() => new AppendPayloadStep())
            .End());

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle()
            .Which.Should().Match<ActiveWaitSnapshot>(wait =>
                wait.EventName == "Approved" &&
                wait.CorrelationId == Correlation &&
                wait.Status == "Active" &&
                wait.Mode == "Resident");
        state.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_WaitCorrelationSelectorThrows_FailsWorkflowWithoutRegisteringWait()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait("Approved", _ => throw new InvalidOperationException("selector boom"))
            .Then(() => new AppendPayloadStep())
            .End());
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("selector boom");
        snapshot.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    public async Task RaiseEventAsync_MatchingEvent_ResumesAndClearsWait()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var resumed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "ok"),
            TestContext.Current.CancellationToken);

        resumed.Status.Should().Be(WorkflowStatus.Completed);
        resumed.ActiveWaits.Should().BeEmpty();
        state.Values.Should().Equal(["ok"]);
    }

    [Fact]
    public async Task RaiseEventAsync_MatchingEvent_ProvidesPayloadToNextStepOnly()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Then(() => new WaitResultStep())
            .Then(() => new AppendPayloadStep())
            .Then(() => new RecordSecondResumedEventStep())
            .End());
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "first"),
            TestContext.Current.CancellationToken);

        state.Values.Should().Equal(["first"]);
        state.SecondStepSawResumedEvent.Should().BeFalse();
    }

    [Fact]
    public async Task RaiseEventAsync_MatchingEvent_ThenYieldingStep_DrainsYieldContinuation()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Wait("Approved", _ => Correlation)
            .Then(() => new YieldOnceStep())
            .End());
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var resumed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "ok"),
            TestContext.Current.CancellationToken);

        resumed.Status.Should().Be(WorkflowStatus.Completed);
        state.YieldAttempts.Should().Be(2);
    }

    [Fact]
    public async Task RaiseEventAsync_WrongNameOrCorrelation_LeavesInstanceWaiting()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var wrongName = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Rejected", Correlation, "wrong"),
            TestContext.Current.CancellationToken);
        var wrongCorrelation = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", CorrelationId.Create("other"), "wrong"),
            TestContext.Current.CancellationToken);

        wrongName.Status.Should().Be(WorkflowStatus.Waiting);
        wrongCorrelation.Status.Should().Be(WorkflowStatus.Waiting);
        wrongCorrelation.ActiveWaits.Should().ContainSingle();
        state.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task RaiseEventAsync_TwoConcurrentMatches_OnlyOneContinuationCommits()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var first = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "first"),
            TestContext.Current.CancellationToken);
        var second = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "second"),
            TestContext.Current.CancellationToken);

        var snapshots = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        snapshots.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Completed);
        state.Values.Should().HaveCount(1);
    }

    [Fact]
    public async Task RaiseEventAsync_ThreeSequentialWaits_CompleteInOrder()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Wait("A", _ => Correlation)
            .Then(() => new IncrementStep())
            .Wait("B", _ => Correlation)
            .Then(() => new IncrementStep())
            .Wait("C", _ => Correlation)
            .Then(() => new IncrementStep())
            .End());
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var afterA = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", Correlation, "a"),
            TestContext.Current.CancellationToken);
        afterA.Status.Should().Be(WorkflowStatus.Waiting);
        afterA.ActiveWaits.Should().ContainSingle().Which.EventName.Should().Be("B");
        state.Count.Should().Be(1);

        var afterB = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("B", Correlation, "b"),
            TestContext.Current.CancellationToken);
        afterB.Status.Should().Be(WorkflowStatus.Waiting);
        afterB.ActiveWaits.Should().ContainSingle().Which.EventName.Should().Be("C");
        state.Count.Should().Be(2);

        var completed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("C", Correlation, "c"),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        state.Count.Should().Be(3);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition(TestState state)
    {
        return Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Wait("Approved", _ => Correlation)
            .Then(() => new AppendPayloadStep())
            .End());
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(
        EphemeralWorkflowBuilder<TestState> builder)
    {
        return builder.Build();
    }

    private static EventEnvelope Event(string name, CorrelationId correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            EventName = name,
            CorrelationId = correlationId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public List<string> Values { get; } = [];

        public bool SecondStepSawResumedEvent { get; set; }

        public int Count { get; set; }

        public int YieldAttempts { get; set; }
    }

    private sealed class WaitResultStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent(EventName.Create("Approved"), Correlation));
        }
    }

    private sealed class AppendPayloadStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Values.Add(payload);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class RecordSecondResumedEventStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.SecondStepSawResumedEvent = context.ResumedEvent is not null;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class IncrementStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Count++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class YieldOnceStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.YieldAttempts++;
            return ValueTask.FromResult<StepResult>(
                context.State.YieldAttempts == 1
                    ? global::OrcaCore.TestSupport.LegacyStepResults.Yield()
                    : new StepResult.Completed());
        }
    }
}
