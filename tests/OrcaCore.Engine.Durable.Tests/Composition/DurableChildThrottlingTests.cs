using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Composition;

public sealed class DurableChildThrottlingTests
{
    [Fact]
    [Trait("AC", "AC-609")]
    public async Task RunChildren_MaxConcurrency_DispatchesNextChildAfterCompletion()
    {
        var parentId = InstanceIdValue(1);
        var command = new DurableRunChildrenCommand(
            CommandIdValue(2),
            parentId,
            Timestamp(2),
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
        var childStarts = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        scheduled.TotalItemCount.Should().Be(4);
        scheduled.Children.Should().HaveCount(4);
        scheduled.InitialDispatchCount.Should().Be(2);
        scheduled.NextDispatchIndex.Should().Be(2);
        var initialStarts = childStarts.Where(record => record.Kind == "child-start").ToArray();
        initialStarts.Should().HaveCount(2);

        await new DurableCommandProcessor(store).ProcessAsync(
            new DurableChildCompletedCommand(
                CommandIdValue(3),
                parentId,
                Timestamp(3),
                scheduled.Children[0].ChildInstanceId,
                WorkflowStatus.Completed,
                null),
            TestContext.Current.CancellationToken);

        var nextStarts = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        var dispatch = await DispatchedAsync(store, parentId);

        nextStarts.Where(record => record.Kind == "child-start").Should().ContainSingle();
        dispatch.PreviousDispatchIndex.Should().Be(2);
        dispatch.NextDispatchIndex.Should().Be(3);
        dispatch.Children.Should().ContainSingle()
            .Which.ChildInstanceId.Should().Be(scheduled.Children[2].ChildInstanceId);
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

    private static async Task<WorkflowChildrenDispatchedEvent> DispatchedAsync(
        InMemoryWorkflowProvider store,
        InstanceId parentId)
    {
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(parentId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        return events.OfType<WorkflowChildrenDispatchedEvent>().Single();
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
