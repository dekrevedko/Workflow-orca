using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ChildWorkflowAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-606")]
    public async Task RunChild_WaitJoinCompletesParent()
    {
        var parentId = InstanceIdValue(10);
        var childId = InstanceIdValue(11);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            new DurableRunChildCommand(
                CommandIdValue(2),
                parentId,
                Timestamp(2),
                childId,
                DefinitionIdValue(2),
                DefinitionVersion.Initial,
                RunChildFailurePolicy.PropagateFailure),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandIdValue(3),
                parentId,
                Timestamp(3),
                childId,
                WorkflowStatus.Completed,
                null),
            TestContext.Current.CancellationToken);

        var snapshot = await new DurableManagement(store)
            .Instance(parentId)
            .GetAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Running);
        snapshot.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-615")]
    public async Task RunChild_CompletionFailurePolicyIsDeterministic()
    {
        var propagated = await RunFailedChildAsync(InstanceIdValue(20), InstanceIdValue(21), RunChildFailurePolicy.PropagateFailure);
        var continued = await RunFailedChildAsync(InstanceIdValue(30), InstanceIdValue(31), RunChildFailurePolicy.ContinueParent);

        propagated.Status.Should().Be(WorkflowStatus.Failed);
        continued.Status.Should().Be(WorkflowStatus.Running);
    }

    [Fact]
    [Trait("AC", "AC-607")]
    public async Task RunChildren_ChildIdsAreDeterministic()
    {
        var parentId = InstanceIdValue(40);
        var command = RunChildrenCommand(parentId);
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);
        var before = await ScheduledGroupAsync(store, parentId);

        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);
        var after = await ScheduledGroupAsync(store, parentId);

        after.Children.Select(child => child.ChildInstanceId).Should()
            .Equal(before.Children.Select(child => child.ChildInstanceId));
    }

    [Fact]
    [Trait("AC", "AC-608")]
    public async Task RunChildren_ItemSnapshotsAreStable()
    {
        var parentId = InstanceIdValue(50);
        var command = RunChildrenCommand(parentId);
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);

        var scheduled = await ScheduledGroupAsync(store, parentId);

        scheduled.Children.Select(child => child.ItemSnapshot).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    [Trait("AC", "AC-609")]
    public async Task RunChildren_DurableThrottlingSurvivesRestart()
    {
        var parentId = InstanceIdValue(60);
        var command = new DurableRunChildrenCommand(
            CommandIdValue(5),
            parentId,
            Timestamp(5),
            DefinitionIdValue(2),
            DefinitionVersion.Initial,
            ["a", "b", "c", "d"],
            RunChildFailurePolicy.PropagateFailure,
            2);
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);

        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);

        var scheduled = await ScheduledGroupAsync(store, parentId);

        scheduled.TotalItemCount.Should().Be(4);
        scheduled.InitialDispatchCount.Should().Be(2);
        scheduled.NextDispatchIndex.Should().Be(2);
    }

    [Fact]
    [Trait("AC", "AC-610")]
    public async Task RunChildren_BarrierFiresExactlyOnce()
    {
        var parentId = InstanceIdValue(70);
        var store = await SeedRunChildrenAsync(parentId);
        var scheduled = await ScheduledGroupAsync(store, parentId);
        var processor = new DurableCommandProcessor(store);

        foreach (var child in scheduled.Children)
        {
            await processor.ProcessAsync(ChildCompleted(parentId, child.ChildInstanceId), TestContext.Current.CancellationToken);
        }

        (await ResumeTokensAsync(store, parentId)).Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-611")]
    public async Task RunChildren_ResumeTokenIsReusedAfterRestart()
    {
        var parentId = InstanceIdValue(80);
        var store = await SeedRunChildrenAsync(parentId);
        var scheduled = await ScheduledGroupAsync(store, parentId);
        foreach (var child in scheduled.Children)
        {
            await new DurableCommandProcessor(store)
                .ProcessAsync(ChildCompleted(parentId, child.ChildInstanceId), TestContext.Current.CancellationToken);
        }
        var before = (await ResumeTokensAsync(store, parentId)).Single();

        await new DurableCommandProcessor(store)
            .ProcessAsync(ChildCompleted(parentId, scheduled.Children.Last().ChildInstanceId), TestContext.Current.CancellationToken);
        var after = (await ResumeTokensAsync(store, parentId)).Single();

        after.ResumeTokenId.Should().Be(before.ResumeTokenId);
    }

    [Fact]
    [Trait("AC", "AC-612")]
    public async Task RunChildren_WhenAnyResidualIsDurableBeforeParentResume()
    {
        var parentId = InstanceIdValue(90);
        var store = await SeedWhenAnyAsync(parentId);
        var scheduled = await ScheduledGroupAsync(store, parentId);

        await new DurableCommandProcessor(store)
            .ProcessAsync(ChildCompleted(parentId, scheduled.Children[0].ChildInstanceId), TestContext.Current.CancellationToken);
        var events = await EventsAsync(store, parentId);

        events.FindIndex(workflowEvent => workflowEvent is WorkflowChildResidualIntentRecordedEvent)
            .Should().BeLessThan(events.FindIndex(workflowEvent => workflowEvent is WorkflowParentResumeTokenRecordedEvent));
    }

    [Fact]
    [Trait("AC", "AC-613")]
    public async Task RunChildren_OutboxSupportsMixedKinds()
    {
        var parentId = InstanceIdValue(100);
        var store = await SeedWhenAnyAsync(parentId);
        var scheduled = await ScheduledGroupAsync(store, parentId);

        await new DurableCommandProcessor(store)
            .ProcessAsync(ChildCompleted(parentId, scheduled.Children[0].ChildInstanceId), TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        outbox.Select(record => record.Kind).Should().Contain(["child-start", "child-cancel"]);
    }

    [Fact]
    [Trait("AC", "AC-614")]
    public async Task ChildWorkflow_LineageQueriesTraverseTree()
    {
        var rootId = InstanceIdValue(1);
        var childId = InstanceIdValue(2);
        var grandchildId = InstanceIdValue(3);
        var unrelatedId = InstanceIdValue(4);
        var store = new InMemoryWorkflowProvider();
        await SeedProjectionAsync(store, Snapshot(rootId, null, rootId));
        await SeedProjectionAsync(store, Snapshot(childId, rootId, rootId));
        await SeedProjectionAsync(store, Snapshot(grandchildId, childId, rootId));
        await SeedProjectionAsync(store, Snapshot(unrelatedId, null, unrelatedId));

        var tree = await new DurableManagement(store)
            .All()
            .Where(instance => instance.RootInstanceId == rootId)
            .ListAsync(TestContext.Current.CancellationToken);

        tree.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo([rootId, childId, grandchildId]);
        tree.Should().OnlyContain(snapshot => snapshot.RootInstanceId == rootId);
    }

    private static async Task SeedProjectionAsync(
        InMemoryWorkflowProvider store,
        WorkflowInstanceSnapshot snapshot)
    {
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(snapshot.InstanceId),
                ExpectedVersion = StreamVersion.Empty,
                ProjectionOperations =
                [
                    new ProjectionWrite(snapshot.InstanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = snapshot
                    }
                ]
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<WorkflowInstanceSnapshot> RunFailedChildAsync(
        InstanceId parentId,
        InstanceId childId,
        RunChildFailurePolicy failurePolicy)
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableRunChildCommand(
                CommandIdValue(2),
                parentId,
                Timestamp(2),
                childId,
                DefinitionIdValue(2),
                DefinitionVersion.Initial,
                failurePolicy),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandIdValue(3),
                parentId,
                Timestamp(3),
                childId,
                WorkflowStatus.Failed,
                "child failed"),
            TestContext.Current.CancellationToken);

        return await new DurableManagement(store)
            .Instance(parentId)
            .GetAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<WorkflowChildrenScheduledEvent> ScheduledGroupAsync(
        InMemoryWorkflowProvider store,
        InstanceId parentId)
    {
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(parentId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        return events.OfType<WorkflowChildrenScheduledEvent>().Single();
    }

    private static DurableRunChildrenCommand RunChildrenCommand(InstanceId parentId)
    {
        return new DurableRunChildrenCommand(
            CommandIdValue(4),
            parentId,
            Timestamp(4),
            DefinitionIdValue(2),
            DefinitionVersion.Initial,
            ["alpha", "beta", "gamma"],
            RunChildFailurePolicy.PropagateFailure,
            3);
    }

    private static async Task<InMemoryWorkflowProvider> SeedRunChildrenAsync(InstanceId parentId)
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableRunChildrenCommand(
                CommandIdValue(6),
                parentId,
                Timestamp(6),
                DefinitionIdValue(2),
                DefinitionVersion.Initial,
                ["a", "b"],
                RunChildFailurePolicy.PropagateFailure,
                2),
            TestContext.Current.CancellationToken);
        return store;
    }

    private static async Task<InMemoryWorkflowProvider> SeedWhenAnyAsync(InstanceId parentId)
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableRunChildrenCommand(
                CommandIdValue(8),
                parentId,
                Timestamp(8),
                DefinitionIdValue(2),
                DefinitionVersion.Initial,
                ["a", "b", "c"],
                RunChildFailurePolicy.PropagateFailure,
                3,
                RunChildrenJoinPolicy.WhenAny,
                RunChildrenResidualPolicy.CancelRemaining),
            TestContext.Current.CancellationToken);
        return store;
    }

    private static DurableChildCompletedCommand ChildCompleted(InstanceId parentId, InstanceId childId)
    {
        return new DurableChildCompletedCommand(
            CommandId.New(),
            parentId,
            Timestamp(7),
            childId,
            WorkflowStatus.Completed,
            null);
    }

    private static async Task<IReadOnlyList<WorkflowParentResumeTokenRecordedEvent>> ResumeTokensAsync(
        InMemoryWorkflowProvider store,
        InstanceId parentId)
    {
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(parentId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        return events.OfType<WorkflowParentResumeTokenRecordedEvent>().ToArray();
    }

    private static async Task<List<WorkflowEvent>> EventsAsync(
        InMemoryWorkflowProvider store,
        InstanceId parentId)
    {
        return (await store.LoadTailAsync(
                new WorkflowStreamId(parentId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .ToList();
    }

    private static StartWorkflowCommand StartCommand(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowInstanceSnapshot Snapshot(
        InstanceId instanceId,
        InstanceId? parentInstanceId,
        InstanceId rootInstanceId)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            ParentInstanceId = parentInstanceId,
            RootInstanceId = rootInstanceId,
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = WorkflowStatus.Running,
            CreatedAt = Timestamp(1),
            UpdatedAt = Timestamp(1)
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
