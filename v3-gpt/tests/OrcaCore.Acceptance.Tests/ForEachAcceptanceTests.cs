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
    public async Task ForEach_RuntimeBatchingCreatesExpectedItems()
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
        group.WorkItems.Select(workItem => workItem.Items.Count).Should().Equal(10, 10, 3);
        state.BodyRuns.Should().Be(3);
    }

    [Fact]
    [Trait("AC", "AC-602")]
    public async Task ForEach_WhenAllCompletesParent()
    {
        var state = new TestState([1, 2, 3]);
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition(state);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        foreach (var index in Enumerable.Range(0, 3))
        {
            snapshot = await RaiseItemAsync(engine, snapshot, index);
        }

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-603")]
    public async Task ForEach_HonorsMaxConcurrency()
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
    }

    [Fact]
    [Trait("AC", "AC-604")]
    public async Task ForEach_WaitAllThenFailIsObservable()
    {
        var state = new TestState([1, 2, 3]);
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<int>.Items(),
                item => item.Then(() => new FailSecondItemStep()),
                failurePolicy: ForEachFailurePolicy.WaitAllThenFail)
            .Then(() => new CountContinuationStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        snapshot = await RaiseItemAsync(engine, snapshot, 0);
        snapshot = await RaiseItemAsync(engine, snapshot, 2);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ForEachGroups.Single().FailedCount.Should().Be(1);
        snapshot.ForEachGroups.Single().CompletedCount.Should().Be(2);
        state.ContinuationCount.Should().Be(0);
    }

    [Fact]
    [Trait("AC", "AC-605")]
    public async Task ForEach_WhenAnyCancellationIntentPrecedesContinuation()
    {
        var state = new TestState([1, 2, 3]);
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<int>.Items(),
                item => item.Then(() => new WaitForItemStep()),
                joinPolicy: ForEachJoinPolicy.WhenAny,
                residualPolicy: ForEachResidualPolicy.CancelRemaining)
            .Then(() => new CountContinuationStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var snapshot = await RaiseItemAsync(engine, waiting, 0);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ForEachGroups.Single().CancelledCount.Should().Be(2);
        snapshot.LifecycleEvents.Select(lifecycleEvent => lifecycleEvent.EventName).Should()
            .ContainInOrder("ForEachCancellationIntentRecorded", "StepCompleted", "InstanceCompleted");
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

        public int NextFailureIndex { get; set; }

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

    private sealed class FailSecondItemStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent is not null)
            {
                return ValueTask.FromResult<StepResult>(new StepResult.Completed());
            }

            var index = context.State.NextFailureIndex++;
            if (index == 1)
            {
                return ValueTask.FromResult<StepResult>(
                    new StepResult.Failed(new OrcaCoreException("item failed")));
            }

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
