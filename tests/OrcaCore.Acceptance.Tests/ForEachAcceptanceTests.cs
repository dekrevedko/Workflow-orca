using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ForEachAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-601")]
    public async Task ForEach_RuntimeBatchingCreatesExpectedIsolatedItems()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState(Enumerable.Range(1, 23).ToArray()))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Batch(10),
                item => new ItemState(item.Index, item.Items),
                body => body.Return(item => item.Value.Items.Count),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                merge: (parent, outcomes) => parent.Value with
                {
                    BodyRuns = outcomes.Count,
                    PartitionSizes = outcomes.Select(outcome => outcome.Result).ToArray()
                })
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.BodyRuns.Should().Be(3);
        state.PartitionSizes.Should().Equal(10, 10, 3);
    }

    [Fact]
    [Trait("AC", "AC-602")]
    public async Task ForEach_WhenAllCompletesParent()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(maxConcurrency: null);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        foreach (var index in Enumerable.Range(0, 3))
        {
            snapshot = await RaiseItemAsync(engine, snapshot, index);
        }

        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Results.Should().Equal(0, 1, 2);
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-603")]
    public async Task ForEach_HonorsMaxConcurrency()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(maxConcurrency: 2, itemCount: 5);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().HaveCount(2);
    }

    [Fact]
    [Trait("AC", "AC-604")]
    public async Task ForEach_WaitAllThenFailIsObservable()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([1, 2, 3]))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Index, item.Items),
                body => body
                    .Then<FailSecondItemStep>()
                    .Wait("ItemDone", item => new CorrelationId($"item-{item.Index}"))
                    .Return(item => item.Value.Index),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.WaitAllThenFail,
                merge: MergeOutcomes)
            .Then<CountContinuationStep>()
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        snapshot.ActiveWaits.Should().HaveCount(2);
        snapshot = await RaiseItemAsync(engine, snapshot, 0);
        snapshot = await RaiseItemAsync(engine, snapshot, 2);

        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();
        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ActiveWaits.Should().BeEmpty();
        state.ContinuationCount.Should().Be(0);
    }

    [Fact]
    [Trait("AC", "AC-605")]
    public async Task ForEach_WhenAnyCancelsResidualItemsBeforeContinuation()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([1, 2, 3]))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Index, item.Items),
                body => body
                    .Wait("ItemDone", item => new CorrelationId($"item-{item.Index}"))
                    .Return(item => item.Value.Index),
                ForEachJoinPolicy.WhenAny,
                ForEachFailurePolicy.FailFast,
                merge: MergeOutcomes)
            .Then<CountContinuationStep>()
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        var snapshot = await RaiseItemAsync(engine, waiting, 0);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ActiveWaits.Should().BeEmpty();
        state.Results.Should().Equal(0);
        state.ContinuationCount.Should().Be(1);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition(
        int? maxConcurrency,
        int itemCount = 3)
    {
        return Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState(Enumerable.Range(1, itemCount).ToArray()))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Index, item.Items),
                body => body
                    .Wait("ItemDone", item => new CorrelationId($"item-{item.Index}"))
                    .Return(item => item.Value.Index),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                maxConcurrency,
                MergeOutcomes)
            .Then<CountContinuationStep>()
            .End()
            .Build();
    }

    private static TestState MergeOutcomes(
        ReadOnlyParentSnapshot<TestState> parent,
        IReadOnlyList<ForEachItemOutcome<int>> outcomes)
    {
        return parent.Value with
        {
            Results = outcomes
                .Where(outcome => outcome.Status == ForEachItemTerminalStatus.Succeeded)
                .Select(outcome => outcome.Result)
                .ToArray()
        };
    }

    private static Task<WorkflowInstanceSnapshot> RaiseItemAsync(
        EphemeralWorkflowEngine engine,
        WorkflowInstanceSnapshot snapshot,
        int index)
    {
        return engine.RaiseEventAsync<TestState>(
            snapshot.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = "ItemDone",
                CorrelationId = new CorrelationId($"item-{index}"),
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);
    }

    private sealed record TestState(int[] Items)
    {
        public int BodyRuns { get; init; }

        public IReadOnlyList<int> PartitionSizes { get; init; } = [];

        public IReadOnlyList<int> Results { get; init; } = [];

        public int ContinuationCount { get; set; }
    }

    private sealed record ItemState(int Index, IReadOnlyList<int> Items);

    private sealed class FailSecondItemStep : IStep<ItemState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ItemState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(context.State.Index == 1
                ? new StepResult.Failed(new OrcaCoreException("item failed"))
                : new StepResult.Completed());
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
