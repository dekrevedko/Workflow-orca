using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ParallelAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-201")]
    public async Task ParallelWhenAll_ContinuationRunsExactlyOnce()
    {
        var (state, snapshot) = await RunBothOrdersAsync("A", "B");

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-202")]
    public async Task ParallelWhenAll_OrderInsensitiveOutcome()
    {
        var (first, _) = await RunBothOrdersAsync("A", "B");
        var (second, _) = await RunBothOrdersAsync("B", "A");

        first.Values.OrderBy(value => value).Should().Equal(second.Values.OrderBy(value => value));
    }

    [Fact]
    [Trait("AC", "AC-203")]
    public async Task ParallelWhenAll_GraphShapeInsensitive()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Parallel(
                ("a", branch => branch.Then(() => new AppendStep("a")).Then(() => new AppendStep("noop"))),
                ("b", branch => branch.Then(() => new AppendStep("b"))))
            .Then(() => new CountContinuationStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-110")]
    public async Task ParallelWaits_MatchingEventResumesOnlyItsBranch()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", new CorrelationId("a"), "a"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        state.Values.Should().Equal(["a"]);
    }

    [Fact]
    [Trait("AC", "AC-007")]
    public async Task RacingBranchCompletions_SerializeDeterministically()
    {
        var (state, snapshot) = await RunBothOrdersAsync("A", "B");

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.ContinuationCount.Should().Be(1);
        state.Values.Should().BeEquivalentTo(["a", "b", "after"]);
    }

    private static async Task<(TestState State, WorkflowInstanceSnapshot Snapshot)> RunBothOrdersAsync(
        params string[] eventOrder)
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        foreach (var eventName in eventOrder)
        {
            snapshot = await engine.RaiseEventAsync<TestState>(
                snapshot.InstanceId,
                Event(eventName, new CorrelationId(eventName.ToLowerInvariant()), eventName.ToLowerInvariant()),
                TestContext.Current.CancellationToken);
        }

        return (state, snapshot);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Parallel(
                ("a", branch => branch.Wait("A", _ => new CorrelationId("a")).Then(() => new CaptureStep())),
                ("b", branch => branch.Wait("B", _ => new CorrelationId("b")).Then(() => new CaptureStep())))
            .Then(() => new CountContinuationStep())
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

        public int ContinuationCount { get; set; }
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

    private sealed class CountContinuationStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.ContinuationCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
