using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Composition;

public sealed class ChildResidualPolicyTests
{
    [Fact]
    [Trait("AC", "AC-612")]
    public async Task WhenAny_CancelRemaining_RecordsResidualBeforeResume()
    {
        var parentId = InstanceIdValue(1);
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
    public async Task UnifiedOutbox_CarriesChildStartAndExternalRecords()
    {
        var parentId = InstanceIdValue(10);
        var store = await SeedWhenAnyAsync(parentId);
        var scheduled = await ScheduledGroupAsync(store, parentId);

        await new DurableCommandProcessor(store)
            .ProcessAsync(ChildCompleted(parentId, scheduled.Children[0].ChildInstanceId), TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        outbox.Select(record => record.Kind).Should().Contain(["child-start", "external-message"]);
    }

    private static async Task<InMemoryWorkflowProvider> SeedWhenAnyAsync(InstanceId parentId)
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableRunChildrenCommand(
                CommandIdValue(2),
                parentId,
                Timestamp(2),
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
        return new DurableChildCompletedCommand(CommandId.New(), parentId, Timestamp(3), childId, WorkflowStatus.Completed, null);
    }

    private static async Task<WorkflowChildrenScheduledEvent> ScheduledGroupAsync(InMemoryWorkflowProvider store, InstanceId parentId)
    {
        return (await EventsAsync(store, parentId)).OfType<WorkflowChildrenScheduledEvent>().Single();
    }

    private static async Task<List<WorkflowEvent>> EventsAsync(InMemoryWorkflowProvider store, InstanceId parentId)
    {
        return (await store.LoadTailAsync(new WorkflowStreamId(parentId), StreamVersion.Empty, TestContext.Current.CancellationToken))
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

    private static DateTimeOffset Timestamp(int seconds) => new(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);

    private static InstanceId InstanceIdValue(int value) => new(GuidValue(value));

    private static CommandId CommandIdValue(int value) => new(GuidValue(value));

    private static DefinitionId DefinitionIdValue(int value) => new(GuidValue(value));

    private static Guid GuidValue(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}
