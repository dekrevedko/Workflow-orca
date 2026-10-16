using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Composition;

public sealed class RunChildTests
{
    [Fact]
    [Trait("AC", "AC-606")]
    public async Task RunChild_WaitJoin_ParentContinuesAfterChildCompletion()
    {
        var parentId = InstanceIdValue(1);
        var childId = InstanceIdValue(2);
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
        var waiting = await RehydrateAsync(store, parentId);

        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        outbox.Should().ContainSingle(record => record.Kind == "child-start");
        waiting.Snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        waiting.Snapshot.ActiveWaits.Should().ContainSingle(wait => wait.EventName == "ChildCompleted");

        await processor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandIdValue(3),
                parentId,
                Timestamp(3),
                childId,
                WorkflowStatus.Completed,
                null),
            TestContext.Current.CancellationToken);
        var resumed = await RehydrateAsync(store, parentId);

        resumed.Snapshot.Status.Should().Be(WorkflowStatus.Running);
        resumed.Snapshot.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-615")]
    public async Task RunChild_ChildFailurePropagatesByPolicy()
    {
        var propagated = await RunFailedChildAsync(InstanceIdValue(10), InstanceIdValue(11), RunChildFailurePolicy.PropagateFailure);
        var continued = await RunFailedChildAsync(InstanceIdValue(20), InstanceIdValue(21), RunChildFailurePolicy.ContinueParent);

        propagated.Status.Should().Be(WorkflowStatus.Failed);
        propagated.ErrorSummary.Should().Be("child failed");
        continued.Status.Should().Be(WorkflowStatus.Running);
        continued.ErrorSummary.Should().BeNull();
    }

    private static async Task<DurableAggregateSnapshot> RunFailedChildAsync(
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

        return (await RehydrateAsync(store, parentId)).Snapshot;
    }

    private static async Task<DurableWorkflowAggregate> RehydrateAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId)
    {
        var tail = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        return DurableWorkflowAggregate.Rehydrate(null, tail);
    }

    private static OrcaCore.Abstractions.Durable.StartWorkflowCommand StartCommand(InstanceId instanceId)
    {
        return new OrcaCore.Abstractions.Durable.StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
