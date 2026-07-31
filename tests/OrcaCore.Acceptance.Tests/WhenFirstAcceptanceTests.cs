using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
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
        first.State.Values.Should().Equal("a");
        first.Snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait("AC", "AC-205")]
    public async Task WhenFirst_LosingBranchWaitIsCancelledBeforeContinuation()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([]))
            .WhenFirst<string>(
                branches => branches
                    .Branch<BranchState>(
                        "a",
                        _ => new BranchState("a"),
                        branch => branch
                            .Wait("A", _ => CorrelationId.Create("a"))
                            .Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        "b",
                        _ => new BranchState("b"),
                        branch => branch
                            .Wait("B", _ => CorrelationId.Create("b"))
                            .Return(state => state.Value.Name)),
                MergeWinner)
            .End()
            .Build();
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", CorrelationId.Create("a")),
            TestContext.Current.CancellationToken);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ActiveWaits.Should().BeEmpty();
        state.Values.Should().Equal("a");
    }

    private static async Task<(TestState State, WorkflowInstanceSnapshot Snapshot)> RunImmediateAsync()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([]))
            .WhenFirst<string>(
                branches => branches
                    .Branch<BranchState>(
                        "a",
                        _ => new BranchState("a"),
                        branch => branch.Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        "b",
                        _ => new BranchState("b"),
                        branch => branch.Return(state => state.Value.Name)),
                MergeWinner)
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        return (engine.Management.Instance(snapshot.InstanceId).GetState<TestState>(), snapshot);
    }

    private static TestState MergeWinner(
        ReadOnlyParentSnapshot<TestState> parent,
        global::OrcaCore.Core.Building.BranchResult<string> winner)
    {
        return new TestState([.. parent.Value.Values, winner.Value]);
    }

    private static EventEnvelope Event(string name, CorrelationId correlationId)
    {
        return new EventEnvelope
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            EventName = name,
            CorrelationId = correlationId,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed record TestState(IReadOnlyList<string> Values);

    private sealed record BranchState(string Name);
}
