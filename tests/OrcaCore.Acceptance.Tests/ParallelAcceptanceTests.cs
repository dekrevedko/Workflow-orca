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

        first.Values.Should().Equal(second.Values);
    }

    [Fact]
    [Trait("AC", "AC-203")]
    public async Task ParallelWhenAll_GraphShapeInsensitive()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([], 0))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        "a",
                        _ => new BranchState("a", ""),
                        branch => branch.Then<BranchNoOpStep>().Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        "b",
                        _ => new BranchState("b", ""),
                        branch => branch.Return(state => state.Value.Name)),
                MergeResults)
            .Then<CountContinuationStep>()
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal("a", "b");
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-110")]
    public async Task ParallelWaits_MatchingEventResumesOnlyItsBranchWithoutMutatingParent()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition();
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", new CorrelationId("a")),
            TestContext.Current.CancellationToken);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle(wait => wait.EventName == "B");
        state.Values.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-007")]
    public async Task RacingBranchCompletions_SerializeDeterministically()
    {
        var (state, snapshot) = await RunBothOrdersAsync("A", "B");

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.ContinuationCount.Should().Be(1);
        state.Values.Should().Equal("a", "b");
    }

    private static async Task<(TestState State, WorkflowInstanceSnapshot Snapshot)> RunBothOrdersAsync(
        params string[] eventOrder)
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition();
        engine.RegisterDefinition(definition);
        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        foreach (var eventName in eventOrder)
        {
            snapshot = await engine.RaiseEventAsync<TestState>(
                snapshot.InstanceId,
                Event(eventName, new CorrelationId(eventName.ToLowerInvariant())),
                TestContext.Current.CancellationToken);
        }

        return (engine.Management.Instance(snapshot.InstanceId).GetState<TestState>(), snapshot);
    }

    private static WorkflowDefinition<TestState> Definition()
    {
        return Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([], 0))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        "a",
                        _ => new BranchState("a", "A"),
                        branch => branch
                            .Wait("A", _ => new CorrelationId("a"))
                            .Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        "b",
                        _ => new BranchState("b", "B"),
                        branch => branch
                            .Wait("B", _ => new CorrelationId("b"))
                            .Return(state => state.Value.Name)),
                MergeResults)
            .Then<CountContinuationStep>()
            .End()
            .Build();
    }

    private static TestState MergeResults(
        ReadOnlyParentSnapshot<TestState> parent,
        IReadOnlyList<BranchResult<string>> results)
    {
        return new TestState(results.Select(result => result.Value).ToArray(), parent.Value.ContinuationCount);
    }

    private static EventEnvelope Event(string name, CorrelationId correlationId)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = name,
            CorrelationId = correlationId,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed record TestState(IReadOnlyList<string> Values, int ContinuationCount)
    {
        public int ContinuationCount { get; set; } = ContinuationCount;
    }

    private sealed record BranchState(string Name, string EventName);

    private sealed class BranchNoOpStep : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
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
