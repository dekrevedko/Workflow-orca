using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ChildCompensationAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-616")]
    public async Task ExplicitChildCompensation_EnqueuesOneCompensationStartPerCompletedChild()
    {
        var parentId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RunChildrenCommand(parentId), TestContext.Current.CancellationToken);
        await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        var scheduled = await ScheduledGroupAsync(store, parentId);
        await processor.ProcessAsync(ChildCompleted(parentId, scheduled.Children[0].ChildInstanceId, 3), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ChildCompleted(parentId, scheduled.Children[1].ChildInstanceId, 4), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(CompensateChildren(parentId, 5), TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        outbox.Where(record => record.Kind == "child-compensation-start").Should().HaveCount(2);
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

    private static DurableRunChildrenCommand RunChildrenCommand(InstanceId parentId)
    {
        return new DurableRunChildrenCommand(
            CommandIdValue(2),
            parentId,
            Timestamp(2),
            DefinitionIdValue(2),
            DefinitionVersion.Initial,
            ["alpha", "beta", "gamma"],
            RunChildFailurePolicy.PropagateFailure,
            3);
    }

    private static DurableChildCompletedCommand ChildCompleted(
        InstanceId parentId,
        InstanceId childId,
        int commandValue)
    {
        return new DurableChildCompletedCommand(
            CommandIdValue(commandValue),
            parentId,
            Timestamp(commandValue),
            childId,
            WorkflowStatus.Completed,
            null);
    }

    private static CompensateChildGroupCommand CompensateChildren(InstanceId parentId, int commandValue)
    {
        return new CompensateChildGroupCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = parentId,
            RequestedAt = Timestamp(commandValue),
            GroupId = CommandIdValue(2).Value.ToString("D"),
            CompensationDefinitionId = DefinitionIdValue(9),
            CompensationDefinitionVersion = DefinitionVersion.Initial
        };
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

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 18, minutes, 0, TimeSpan.Zero);
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
