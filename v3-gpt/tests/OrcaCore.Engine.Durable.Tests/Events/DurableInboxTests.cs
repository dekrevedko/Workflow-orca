using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Events;

public sealed class DurableInboxTests
{
    [Fact]
    [Trait("AC", "AC-305")]
    public async Task DuplicateEvent_BeforeAndAfterRestart_ProducesOneOutcome()
    {
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        var store = new InMemoryWorkflowProvider();
        await SeedWaitAsync(store, instanceId, WaitIdValue(1));
        var processor = new DurableCommandProcessor(store);

        var first = await processor.ProcessAsync(
            DeliverCommand(instanceId, eventId, 2),
            TestContext.Current.CancellationToken);
        var second = await new DurableCommandProcessor(store).ProcessAsync(
            DeliverCommand(instanceId, eventId, 3),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var inbox = await store.GetAsync(eventId, TestContext.Current.CancellationToken);

        first.Outcome.Should().Be(DurableCommandOutcome.Committed);
        second.Outcome.Should().Be(DurableCommandOutcome.NoOp);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        inbox.Value.Should().Be(InboxRecordState.Applied);
    }

    [Fact]
    [Trait("AC", "AC-114")]
    public async Task CrashAfterMatchBeforeCommit_LeavesWaitActiveAndEventAvailable()
    {
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        var store = new FakeWorkflowEventStore();
        await SeedWaitAsync(store, instanceId, WaitIdValue(1));
        store.FailNextCommitBeforeApply();
        var processor = new DurableCommandProcessor(store);

        var failed = await processor.ProcessAsync(
            DeliverCommand(instanceId, eventId, 2),
            TestContext.Current.CancellationToken);
        var retried = await new DurableCommandProcessor(store).ProcessAsync(
            DeliverCommand(instanceId, eventId, 3),
            TestContext.Current.CancellationToken);
        var inbox = await store.GetAsync(eventId, TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        failed.Outcome.Should().Be(DurableCommandOutcome.Conflict);
        retried.Outcome.Should().Be(DurableCommandOutcome.Committed);
        inbox.Value.Should().Be(InboxRecordState.Applied);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task InboxRecord_AppliedOnlyAfterStateCommitSucceeds()
    {
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        var store = new FakeWorkflowEventStore();
        await SeedWaitAsync(store, instanceId, WaitIdValue(1));
        store.FailNextCommitBeforeApply();
        var processor = new DurableCommandProcessor(store);

        await processor.ProcessAsync(
            DeliverCommand(instanceId, eventId, 2),
            TestContext.Current.CancellationToken);
        var inbox = await store.GetAsync(eventId, TestContext.Current.CancellationToken);

        inbox.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task PoisonedDelivery_IsRecordedWithClearFailureMetadata()
    {
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(
            StartCommand(instanceId),
            TestContext.Current.CancellationToken);

        var result = await new DurableCommandProcessor(store).ProcessAsync(
            DeliverCommand(instanceId, eventId, 2),
            TestContext.Current.CancellationToken);
        var inbox = await store.GetAsync(eventId, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Poisoned);
        result.Message.Should().Contain("No active wait");
        inbox.Value.Should().Be(InboxRecordState.Poisoned);
    }

    private static async Task SeedWaitAsync(
        IWorkflowEventStore store,
        InstanceId instanceId,
        WaitId waitId)
    {
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            WaitRegisteredCommand(instanceId, waitId, 2),
            TestContext.Current.CancellationToken);
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

    private static DurableWaitRegisteredCommand WaitRegisteredCommand(
        InstanceId instanceId,
        WaitId waitId,
        int commandValue)
    {
        return new DurableWaitRegisteredCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            waitId,
            "Approved",
            new CorrelationId("order-1"),
            WaitMode.Resident);
    }

    private static DeliverEventCommand DeliverCommand(
        InstanceId instanceId,
        EventId eventId,
        int commandValue)
    {
        return new DeliverEventCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = instanceId,
            RequestedAt = Timestamp(commandValue),
            Envelope = new EventEnvelope
            {
                EventId = eventId,
                EventName = "Approved",
                CorrelationId = new CorrelationId("order-1"),
                OccurredAt = Timestamp(commandValue)
            }
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
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

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
