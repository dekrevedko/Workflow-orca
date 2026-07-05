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

    [Fact]
    [Trait("AC", "AC-601")]
    public async Task ForEach_Body_ReadsItsOwnItemAndStableIndex()
    {
        var state = new ItemCaptureState(["pick", "pack", "label"]);
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<ItemCaptureState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<string>.Items(),
                item => item.Then(() => new CaptureItemStep()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, ItemCaptureState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        // Each body saw exactly its own item at its own stable index — no shared counter needed.
        state.Captured.OrderBy(entry => entry.Index)
            .Should()
            .Equal((0, "pick"), (1, "pack"), (2, "label"));
    }

    [Fact]
    [Trait("AC", "AC-603")]
    public async Task ForEach_Body_ItemIdentitySurvivesInterleavedResume()
    {
        // Three items run concurrently and each suspends on a wait. Resuming them out of order is
        // exactly where a call-order counter misattributes items; ForEachItem must not.
        var state = new ItemCaptureState(["alpha", "beta", "gamma"]);
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<ItemCaptureState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<string>.Items(),
                item => item
                    .Then(() => new WaitByItemStep())
                    .Then(() => new CaptureOnResumeStep()),
                maxConcurrency: 3)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, ItemCaptureState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        snapshot.ActiveWaits.Should().HaveCount(3);

        // Resume in reverse-ish order to force interleaving.
        foreach (var index in new[] { 2, 0, 1 })
        {
            snapshot = await engine.RaiseEventAsync<ItemCaptureState>(
                snapshot.InstanceId,
                new EventEnvelope
                {
                    EventId = EventId.New(),
                    EventName = "ItemDone",
                    CorrelationId = new CorrelationId($"item-{index}"),
                    Payload = index,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                TestContext.Current.CancellationToken);
        }

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Captured.OrderBy(entry => entry.Index)
            .Should()
            .Equal((0, "alpha"), (1, "beta"), (2, "gamma"));
    }

    [Fact]
    [Trait("AC", "AC-601")]
    public async Task ForEach_Body_BatchPartition_ExposesEveryItemInPartition()
    {
        var state = new ItemCaptureState(["a", "b", "c", "d", "e"]);
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<ItemCaptureState>()
            .Init<string>(_ => state)
            .ForEach(
                current => current.Items,
                WorkflowPartitioner<string>.Batch(2),
                item => item.Then(() => new CapturePartitionStep()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, ItemCaptureState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.CapturedPartitions.OrderBy(entry => entry.Index)
            .Select(entry => entry.Joined)
            .Should()
            .Equal("a,b", "c,d", "e");
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

    private sealed class ItemCaptureState(string[] items)
    {
        public string[] Items { get; } = items;

        public List<(int Index, string Item)> Captured { get; } = [];

        public List<(int Index, string Joined)> CapturedPartitions { get; } = [];
    }

    private sealed class CaptureItemStep : IStep<ItemCaptureState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ItemCaptureState> context,
            CancellationToken cancellationToken)
        {
            var forEachItem = context.ForEachItem!;
            context.State.Captured.Add((forEachItem.Index, forEachItem.Item<string>()));
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class WaitByItemStep : IStep<ItemCaptureState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ItemCaptureState> context,
            CancellationToken cancellationToken)
        {
            // Suspend this branch keyed by its own stable ForEach index.
            return ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent("ItemDone", new CorrelationId($"item-{context.ForEachItem!.Index}")));
        }
    }

    private sealed class CaptureOnResumeStep : IStep<ItemCaptureState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ItemCaptureState> context,
            CancellationToken cancellationToken)
        {
            // Runs on the resumed continuation. The context still carries this branch's own item —
            // the identity a mutable call-order counter would lose when resumes interleave.
            var forEachItem = context.ForEachItem!;
            context.State.Captured.Add((forEachItem.Index, forEachItem.Item<string>()));
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CapturePartitionStep : IStep<ItemCaptureState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ItemCaptureState> context,
            CancellationToken cancellationToken)
        {
            var forEachItem = context.ForEachItem!;
            context.State.CapturedPartitions.Add(
                (forEachItem.Index, string.Join(",", forEachItem.Items<string>())));
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
