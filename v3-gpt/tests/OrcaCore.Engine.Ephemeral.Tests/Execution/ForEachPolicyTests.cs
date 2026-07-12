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

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class ForEachPolicyTests
{
    [Fact]
    [Trait("AC", "AC-604")]
    public async Task ForEach_WaitAllThenFail_FailsAfterAllItemsFinish()
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

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ForEachGroups.Single().FailedCount.Should().Be(1);
        state.ContinuationCount.Should().Be(0);

        snapshot = await RaiseItemAsync(engine, snapshot, 0);
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);

        snapshot = await RaiseItemAsync(engine, snapshot, 2);
        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ForEachGroups.Single().CompletedCount.Should().Be(2);
        snapshot.ForEachGroups.Single().FailedCount.Should().Be(1);
        state.ContinuationCount.Should().Be(0);
    }

    [Fact]
    [Trait("AC", "AC-605")]
    public async Task ForEach_WhenAny_RecordsCancellationIntentBeforeParentContinuation()
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
        state.ContinuationCount.Should().Be(1);
        snapshot.LifecycleEvents.Select(lifecycleEvent => lifecycleEvent.EventName).Should()
            .ContainInOrder("ForEachCancellationIntentRecorded", "StepCompleted", "InstanceCompleted");
    }

    [Fact]
    public async Task ForEach_ContinueWithPartialFailures_AppliesToNestedConditionFailures()
    {
        var state = new TestState([1, 2]);
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<int>.Items(),
                item => item.If(
                    _ => throw new OrcaCoreException("condition failed"),
                    then => then.Then(() => new CountContinuationStep())),
                failurePolicy: ForEachFailurePolicy.ContinueWithPartialFailures)
            .Then(() => new CountContinuationStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ForEachGroups.Single().FailedCount.Should().Be(2);
        state.ContinuationCount.Should().Be(1);
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

        public int NextWaitIndex { get; set; }

        public int NextFailureIndex { get; set; }

        public int ContinuationCount { get; set; }
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
