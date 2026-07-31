using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests;

/// <summary>
/// Preserves R4 regression declarations whose Saga, pause/resume, or broad management surfaces
/// remain explicitly deferred, plus automatic early-event buffering superseded by the v1
/// <c>NoActiveWait</c> delivery contract. This file stays outside the active compilation set.
/// </summary>
public sealed class R4DurableEngineDeferredTests
{
    [Fact]
    public async Task R4_EarlyInboundEvent_IsBufferedAndMatchedWhenWaitRegisters()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        await processor.ProcessAsync(Start(), TestContext.Current.CancellationToken);

        var delivered = await processor.ProcessAsync(
            Deliver(instanceId, eventId, 2),
            TestContext.Current.CancellationToken);
        var registered = await processor.ProcessAsync(
            WaitRegistered(instanceId, WaitIdValue(10), 3),
            TestContext.Current.CancellationToken);
        var inbox = await store.GetAsync(eventId, TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        delivered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        registered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        inbox.Value.Should().Be(InboxRecordState.Applied);
        events.OfType<WorkflowDeliveryBufferedEvent>().Should().ContainSingle();
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle()
            .Which.MatchedEventId.Should().Be(eventId);
    }

    [Fact]
    public void R4_PausedTimerFiring_IsReplayedOnResume()
    {
        var timerId = TimerIdValue(10);
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), TimerScheduled(timerId), Paused(), TimerBuffered(timerId)]);

        var decision = aggregate.DecideResume(
            new DurableResumeCommand(CommandIdValue(5), InstanceIdValue(1), Timestamp(5)));

        decision.Events.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle()
            .Which.TimerId.Should().Be(timerId);
    }

    [Fact]
    public void R4_SagaCompensation_TerminalOnlyAfterAllActionsComplete()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), CompensationStarted("release-b", 0), CompensationStarted("release-a", 1)]);

        var first = aggregate.DecideCompleteSagaCompensation(
            CompleteCompensation("release-b", 2));
        var afterFirst = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), CompensationStarted("release-b", 0), CompensationStarted("release-a", 1), .. first.Events]);
        var second = afterFirst.DecideCompleteSagaCompensation(
            CompleteCompensation("release-a", 3));

        first.Events.OfType<WorkflowTerminalEvent>().Should().BeEmpty();
        second.Events.OfType<WorkflowTerminalEvent>().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Compensated);
    }

    [Fact]
    public async Task R4_ManagementSurface_ExposesDurableOperatorCommands()
    {
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(Start(), TestContext.Current.CancellationToken);
        var management = new DurableManagement(store);

        var result = await management.PauseAsync(
            InstanceIdValue(1),
            Timestamp(2),
            TestContext.Current.CancellationToken);
        var snapshot = await management.Instance(InstanceIdValue(1))
            .GetAsync(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        snapshot.Status.Should().Be(WorkflowStatus.Paused);
    }

    [Fact]
    public async Task R4_ManagementSurface_ExposesHistoryInspection()
    {
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(Start(), TestContext.Current.CancellationToken);

        var history = await new DurableManagement(store)
            .GetHistoryAsync(InstanceIdValue(1), TestContext.Current.CancellationToken);

        history.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStartedEvent>();
    }

    private static StartWorkflowCommand Start() =>
        new()
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };

    private static DurableWaitRegisteredCommand WaitRegistered(
        InstanceId instanceId,
        WaitId waitId,
        int commandValue) =>
        new(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            waitId,
            "Approved",
            new CorrelationId("order-1"),
            WaitMode.Resident);

    private static DeliverEventCommand Deliver(
        InstanceId instanceId,
        EventId eventId,
        int commandValue) =>
        new()
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

    private static CompleteSagaCompensationCommand CompleteCompensation(string actionKey, int commandValue) =>
        new()
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "scope-1",
            ActionKey = actionKey
        };

    private static WorkflowStartedEvent Started() =>
        new()
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };

    private static WorkflowTimerScheduledEvent TimerScheduled(TimerId timerId) =>
        new()
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            TimerId = timerId,
            FireAt = Timestamp(30),
            WakeupName = "approval-timeout"
        };

    private static WorkflowPausedEvent Paused() =>
        new()
        {
            EventId = EventIdValue(3),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3)
        };

    private static WorkflowTimerBufferedEvent TimerBuffered(TimerId timerId) =>
        new()
        {
            EventId = EventIdValue(4),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(4),
            CausationId = CausationIdValue(4),
            OccurredAt = Timestamp(4),
            TimerId = timerId,
            WakeupName = "approval-timeout"
        };

    private static SagaCompensationStartedEvent CompensationStarted(string actionKey, int order) =>
        new()
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(10 + order),
            CausationId = CausationIdValue(10 + order),
            OccurredAt = Timestamp(10 + order),
            ScopeId = "scope-1",
            ActionKey = actionKey,
            Order = order
        };

    private static DateTimeOffset Timestamp(int minutes) =>
        new(2026, 7, 2, 17, minutes, 0, TimeSpan.Zero);

    private static EventId EventIdValue(int value) =>
        EventId.Create(GuidValue(value).ToString());

    private static InstanceId InstanceIdValue(int value) =>
        InstanceId.Parse(GuidValue(value).ToString());

    private static CommandId CommandIdValue(int value) =>
        new(GuidValue(value));

    private static CausationId CausationIdValue(int value) =>
        new(GuidValue(value));

    private static DefinitionId DefinitionIdValue(int value) =>
        DefinitionId.Parse(GuidValue(value).ToString());

    private static TimerId TimerIdValue(int value) =>
        new(GuidValue(value));

    private static WaitId WaitIdValue(int value) =>
        new(GuidValue(value));

    private static Guid GuidValue(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}
