using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class WhenFirstTests
{
    [Fact]
    public async Task WhenFirst_ConcurrentCompletions_SelectsDeterministicWinner()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .WhenFirst(
                ("a", branch => branch.Then(() => new AppendStep("a"))),
                ("b", branch => branch.Then(() => new AppendStep("b"))))
            .Then(() => new AppendStep("after"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal(["a", "after"]);
        snapshot.CompositionOutcomes.Should().ContainSingle(outcome =>
            outcome.BranchId == "0:a" && outcome.Status == "Winner");
    }

    [Fact]
    public async Task WhenFirst_CancelRemaining_CancelsLosingBranchWork()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state, WhenFirstResidualPolicy.CancelRemaining);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        waiting.ActiveWaits.Should().HaveCount(2);

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", new CorrelationId("a"), "a"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ActiveWaits.Should().BeEmpty();
        state.Values.Should().Equal(["a", "after"]);
        snapshot.CompositionOutcomes.Should().Contain(outcome =>
            outcome.BranchId == "1:b" && outcome.Status == "Cancelled");
    }

    [Fact]
    public async Task WhenFirst_LetRemainingComplete_RecordsLoserOutcome()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state, WhenFirstResidualPolicy.LetRemainingComplete);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var first = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", new CorrelationId("a"), "a"),
            TestContext.Current.CancellationToken);
        first.Status.Should().Be(WorkflowStatus.Waiting);
        first.CompositionOutcomes.Should().Contain(outcome =>
            outcome.BranchId == "0:a" && outcome.Status == "Winner");

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("B", new CorrelationId("b"), "b"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal(["a", "b", "after"]);
        snapshot.CompositionOutcomes.Should().Contain(outcome =>
            outcome.BranchId == "1:b" && outcome.Status == "Completed");
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition(
        TestState state,
        WhenFirstResidualPolicy residualPolicy)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .WhenFirst(
                residualPolicy,
                ("a", branch => branch.Wait("A", _ => new CorrelationId("a")).Then(() => new CaptureStep())),
                ("b", branch => branch.Wait("B", _ => new CorrelationId("b")).Then(() => new CaptureStep())))
            .Then(() => new AppendStep("after"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
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
        public List<string> Values { get; } = [];
    }

    private sealed class AppendStep(string value) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CaptureStep : IStep<TestState>
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
}
