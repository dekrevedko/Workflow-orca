using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Composition;

public sealed class RunChildrenTests
{
    [Fact]
    [Trait("AC", "AC-607")]
    public async Task RunChildren_RestartDoesNotDuplicateChildIds()
    {
        var parentId = InstanceIdValue(1);
        var command = RunChildrenCommand(parentId);
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);

        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);

        var scheduled = await ScheduledGroupAsync(store, parentId);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = parentId },
            TestContext.Current.CancellationToken)).Single();
        scheduled.FiberId.Should().Be(new FiberId("child-fiber"));
        scheduled.ScopeId.Should().Be(new ScopeId("child-scope"));
        snapshot.ActiveWaits.Should().OnlyContain(wait =>
            wait.FiberId == new FiberId("child-fiber") &&
            wait.ScopeId == new ScopeId("child-scope"));
        scheduled.Children.Select(child => child.ChildInstanceId).Should().OnlyHaveUniqueItems();
        scheduled.Children.Should().HaveCount(3);
        (await store.ClaimAsync(10, TestContext.Current.CancellationToken))
            .Where(record => record.Kind == "child-start")
            .Should()
            .HaveCount(3, "the duplicate command must not enqueue another child-start window");
    }

    [Fact]
    [Trait("AC", "AC-608")]
    public async Task RunChildren_ItemSnapshotsRemainStableAcrossRestart()
    {
        var parentId = InstanceIdValue(10);
        var command = RunChildrenCommand(parentId);
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(StartCommand(parentId), TestContext.Current.CancellationToken);
        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);
        var before = await ScheduledGroupAsync(store, parentId);

        await new DurableCommandProcessor(store).ProcessAsync(command, TestContext.Current.CancellationToken);
        var after = await ScheduledGroupAsync(store, parentId);

        after.Children.Select(child => child.ItemSnapshot).Should()
            .Equal(before.Children.Select(child => child.ItemSnapshot));
        after.Children.Select(child => child.ChildInstanceId).Should()
            .Equal(before.Children.Select(child => child.ChildInstanceId));
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
            CommandIdValue(2),
            parentId,
            Timestamp(2),
            DefinitionIdValue(2),
            DefinitionVersion.Initial,
            ["alpha", "beta", "gamma"],
            RunChildFailurePolicy.PropagateFailure,
            3)
        {
            FiberId = new FiberId("child-fiber"),
            ScopeId = new ScopeId("child-scope")
        };
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
