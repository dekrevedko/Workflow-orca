using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Composition;

public sealed class ParentResumeTokenTests
{
    [Fact]
    [Trait("AC", "AC-610")]
    public async Task ConcurrentChildCompletions_TriggerOneParentResume()
    {
        var parentId = InstanceIdValue(1);
        var store = await SeedRunChildrenAsync(parentId);
        var scheduled = await ScheduledGroupAsync(store, parentId);
        var processor = new DurableCommandProcessor(store);

        foreach (var child in scheduled.Children)
        {
            await processor.ProcessAsync(ChildCompleted(parentId, child.ChildInstanceId), TestContext.Current.CancellationToken);
        }
        await processor.ProcessAsync(ChildCompleted(parentId, scheduled.Children.Last().ChildInstanceId), TestContext.Current.CancellationToken);

        var tokens = await ResumeTokensAsync(store, parentId);

        tokens.Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-611")]
    public async Task Restart_ReplaysRecordedResumeToken()
    {
        var parentId = InstanceIdValue(10);
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

    private static async Task<InMemoryWorkflowProvider> SeedRunChildrenAsync(InstanceId parentId)
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
                ["a", "b"],
                RunChildFailurePolicy.PropagateFailure,
                2),
            TestContext.Current.CancellationToken);
        return store;
    }

    private static DurableChildCompletedCommand ChildCompleted(InstanceId parentId, InstanceId childId)
    {
        return new DurableChildCompletedCommand(
            CommandId.New(),
            parentId,
            Timestamp(3),
            childId,
            WorkflowStatus.Completed,
            null);
    }

    private static async Task<WorkflowChildrenScheduledEvent> ScheduledGroupAsync(
        InMemoryWorkflowProvider store,
        InstanceId parentId)
    {
        var events = await store.LoadTailAsync(new WorkflowStreamId(parentId), StreamVersion.Empty, TestContext.Current.CancellationToken);
        return events.OfType<WorkflowChildrenScheduledEvent>().Single();
    }

    private static async Task<IReadOnlyList<WorkflowParentResumeTokenRecordedEvent>> ResumeTokensAsync(
        InMemoryWorkflowProvider store,
        InstanceId parentId)
    {
        var events = await store.LoadTailAsync(new WorkflowStreamId(parentId), StreamVersion.Empty, TestContext.Current.CancellationToken);
        return events.OfType<WorkflowParentResumeTokenRecordedEvent>().ToArray();
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
