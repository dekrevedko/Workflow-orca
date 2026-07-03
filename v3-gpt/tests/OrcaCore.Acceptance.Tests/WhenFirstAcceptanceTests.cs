using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class WhenFirstAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-204")]
    public async Task WhenFirst_WinnerIsDeterministic()
    {
        var first = await RunImmediateAsync();
        var second = await RunImmediateAsync();

        first.State.Values.Should().Equal(second.State.Values);
        first.Snapshot.CompositionOutcomes.Should().ContainSingle(outcome =>
            outcome.BranchId == "0:a" && outcome.Status == "Winner");
    }

    [Fact]
    [Trait("AC", "AC-205")]
    public async Task WhenFirst_LosingBranchPolicyIsObservable()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .WhenFirst(
                WhenFirstResidualPolicy.CancelRemaining,
                ("a", branch => branch.Wait("A", _ => new CorrelationId("a")).Then(() => new CaptureStep())),
                ("b", branch => branch.Wait("B", _ => new CorrelationId("b")).Then(() => new CaptureStep())))
            .Then(() => new AppendStep("after"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", new CorrelationId("a"), "a"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ActiveWaits.Should().BeEmpty();
        snapshot.CompositionOutcomes.Should().Contain(outcome =>
            outcome.BranchId == "1:b" && outcome.Status == "Cancelled");
    }

    private static async Task<(TestState State, WorkflowInstanceSnapshot Snapshot)> RunImmediateAsync()
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

        return (state, snapshot);
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
