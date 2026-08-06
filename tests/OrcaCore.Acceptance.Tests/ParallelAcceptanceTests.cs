using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ParallelAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-201")]
    public async Task ParallelWhenAll_ContinuationRunsExactlyOnce()
    {
        var (state, snapshot) = await RunBothOrdersAsync("A", "B");

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
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
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([], 0))
            .Parallel<string>(branches => branches
                    .Branch<BranchState>(
                        AuthoredBranchId.Create("a"),
                        _ => new BranchState("a", ""),
                        branch => branch
                            .Then(_ => ValueTask.CompletedTask)
                            .Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        AuthoredBranchId.Create("b"),
                        _ => new BranchState("b", ""),
                        branch => branch.Return(state => state.Value.Name)))
            .WhenAll(MergeResults)
            .Then(context =>
            {
                context.State.ContinuationCount++;
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("parallel-graph-shape"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Values.Should().Equal("a", "b");
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-110")]
    public async Task ParallelWaits_MatchingEventResumesOnlyItsBranchWithoutMutatingParent()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = Definition();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("parallel-single-branch"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var delivery = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event("A", CorrelationId.Create("a")),
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle(wait =>
            wait.EventContract.EventName.Equals(EventName.Create("B")));
        state.Values.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-007")]
    public async Task RacingBranchCompletions_SerializeDeterministically()
    {
        var (state, snapshot) = await RunBothOrdersAsync("A", "B");

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.ContinuationCount.Should().Be(1);
        state.Values.Should().Equal("a", "b");
    }

    private static async Task<(TestState State, global::OrcaCore.WorkflowInstanceSnapshot Snapshot)> RunBothOrdersAsync(
        params string[] eventOrder)
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = Definition();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create($"parallel-{Guid.CreateVersion7():N}"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var events = provider.GetRequiredService<IWorkflowEventClient>();

        foreach (var eventName in eventOrder)
        {
            _ = await events.DeliverToInstanceAsync(
                instance.InstanceId,
                Event(eventName, CorrelationId.Create(eventName.ToLowerInvariant())),
                TestContext.Current.CancellationToken);
        }

        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);
        return (state, snapshot);
    }

    private static EphemeralWorkflowDefinition<string> Definition()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([], 0))
            .Parallel<string>(branches => branches
                    .Branch<BranchState>(
                        AuthoredBranchId.Create("a"),
                        _ => new BranchState("a", "A"),
                        branch => branch
                            .Wait(WorkflowEventContract.Create(EventName.Create("A"), EventContractVersion.Initial), _ => CorrelationId.Create("a"))
                            .Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        AuthoredBranchId.Create("b"),
                        _ => new BranchState("b", "B"),
                        branch => branch
                            .Wait(WorkflowEventContract.Create(EventName.Create("B"), EventContractVersion.Initial), _ => CorrelationId.Create("b"))
                            .Return(state => state.Value.Name)))
            .WhenAll(MergeResults)
            .Then(context =>
            {
                context.State.ContinuationCount++;
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
    }

    private static TestState MergeResults(
        ReadOnlyStateSnapshot<TestState> parent,
        IReadOnlyList<global::OrcaCore.BranchResult<string>> results)
    {
        return new TestState(results.Select(result => result.Result).ToArray(), parent.Value.ContinuationCount);
    }

    private static WorkflowEvent Event(string name, CorrelationId correlationId)
    {
        return WorkflowEvent.Create(
            EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create(name),
            correlationId,
            DateTimeOffset.UtcNow);
    }

    public sealed record TestState(IReadOnlyList<string> Values, int ContinuationCount)
    {
        public int ContinuationCount { get; set; } = ContinuationCount;
    }

    public sealed record BranchState(string Name, string EventName);
}
