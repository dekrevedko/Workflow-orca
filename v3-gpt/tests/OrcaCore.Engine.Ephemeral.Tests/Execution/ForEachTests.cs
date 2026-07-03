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

public sealed class ForEachTests
{
    [Fact]
    [Trait("AC", "AC-601")]
    public async Task ForEach_BatchSize_CreatesExpectedWorkItems()
    {
        var state = new TestState(Enumerable.Range(1, 23).ToArray());
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<int>.Batch(10),
                item => item.Then(() => new CountBodyStep()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var group = snapshot.ForEachGroups.Should().ContainSingle().Subject;
        group.WorkItems.Should().HaveCount(3);
        group.WorkItems.Select(workItem => workItem.Items.Count).Should().Equal(10, 10, 3);
        group.CompletedCount.Should().Be(3);
        state.BodyRuns.Should().Be(3);
    }

    [Fact]
    [Trait("AC", "AC-602")]
    public async Task ForEach_WhenAll_ParentContinuesAfterAllItemsComplete()
    {
        var state = new TestState([1, 2, 3]);
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        state.ContinuationCount.Should().Be(0);

        snapshot = await RaiseItemAsync(engine, snapshot, 0);
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        state.ContinuationCount.Should().Be(0);

        snapshot = await RaiseItemAsync(engine, snapshot, 1);
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        state.ContinuationCount.Should().Be(0);

        snapshot = await RaiseItemAsync(engine, snapshot, 2);
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-603")]
    public async Task ForEach_MaxConcurrency_BoundsActiveItems()
    {
        var state = new TestState([1, 2, 3, 4, 5]);
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state, maxConcurrency: 2);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.ActiveWaits.Should().HaveCount(2);
        snapshot.ForEachGroups.Single().ActiveCount.Should().Be(2);

        snapshot = await RaiseItemAsync(engine, snapshot, 0);

        snapshot.ActiveWaits.Should().HaveCount(2);
        snapshot.ForEachGroups.Single().ActiveCount.Should().Be(2);
        state.ContinuationCount.Should().Be(0);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition(
        TestState state,
        int? maxConcurrency = null)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<int>.Items(),
                item => item.Then(() => new WaitForItemStep()),
                maxConcurrency)
            .Then(() => new CountContinuationStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static Task<WorkflowInstanceSnapshot> RaiseItemAsync(
        EphemeralWorkflowEngine engine,
        WorkflowInstanceSnapshot snapshot,
        int index)
    {
        return engine.RaiseEventAsync<TestState>(
            snapshot.InstanceId,
            Event(index),
            TestContext.Current.CancellationToken);
    }

    private static EventEnvelope Event(int index)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = "ItemDone",
            CorrelationId = new CorrelationId($"item-{index}"),
            Payload = index,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState(int[] items)
    {
        public int[] Items { get; } = items;

        public int BodyRuns { get; set; }

        public int NextWaitIndex { get; set; }

        public int ContinuationCount { get; set; }
    }

    private sealed class CountBodyStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.BodyRuns++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class WaitForItemStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent is not null)
            {
                return ValueTask.FromResult<StepResult>(new StepResult.Completed());
            }

            var index = context.State.NextWaitIndex++;
            return ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent("ItemDone", new CorrelationId($"item-{index}")));
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
