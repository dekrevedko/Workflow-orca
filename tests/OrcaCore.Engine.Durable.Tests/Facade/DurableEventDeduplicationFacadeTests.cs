using AwesomeAssertions;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableEventDeduplicationFacadeTests
{
    [Fact]
    public async Task DefinitionFanout_EmptySnapshotIsOwnedAndStableOnRedelivery()
    {
        var store = new InMemoryWorkflowProvider();
        var facade = CreateFacade(store, driveAfterDelivery: false);
        var definitionId = DefinitionId.New();
        var eventId = EventId.Create("empty-fanout-event");
        var inbound = WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(EventName.Create("empty-fanout"), EventContractVersion.Initial),
            eventId,
            CorrelationId.Create("empty-fanout"),
            causationEventId: null,
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"),
            new WorkflowEventRoute.DefinitionFanout(definitionId));

        var accepted = await facade.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);
        var duplicate = await facade.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        duplicate.Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
        (await store.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken))
            .HasValue.Should().BeTrue();
        (await store.ListDefinitionFanoutTargetsAsync(eventId, TestContext.Current.CancellationToken))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DefinitionFanout_OverAtomicLimitReturnsClosedRejectionWithoutOwnership()
    {
        const int Limit = 1024;
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        await store.ApplyAsync(
            Enumerable.Range(0, Limit + 1)
                .Select(_ =>
                {
                    var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
                    return new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = new WorkflowProjectionSnapshot
                        {
                            InstanceId = instanceId,
                            RootInstanceId = instanceId,
                            DefinitionId = definitionId,
                            DefinitionVersion = DefinitionVersion.Initial,
                            Status = WorkflowInstanceStatus.Running,
                            CreatedAt = DateTimeOffset.UnixEpoch,
                            UpdatedAt = DateTimeOffset.UnixEpoch
                        }
                    };
                })
                .ToArray(),
            TestContext.Current.CancellationToken);
        var facade = CreateFacade(store, driveAfterDelivery: false);
        var eventId = EventId.Create("fanout-limit-event");
        var inbound = WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(EventName.Create("fanout-limit"), EventContractVersion.Initial),
            eventId,
            CorrelationId.Create("fanout-limit"),
            causationEventId: null,
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"),
            new WorkflowEventRoute.DefinitionFanout(definitionId));

        var result = await facade.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);

        result.Should().BeOfType<WorkflowEventAcceptanceResult.Rejected>()
            .Which.Reason.Should().BeOfType<WorkflowEventAcceptanceRejection.FanoutLimitExceeded>();
        (await store.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken))
            .HasValue.Should().BeFalse();
        (await store.ListDefinitionFanoutTargetsAsync(eventId, TestContext.Current.CancellationToken))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task MissingDirectTarget_IsRejectedWithoutRecordingEventOwnership()
    {
        var store = new InMemoryWorkflowProvider();
        var facade = CreateFacade(store, driveAfterDelivery: true);
        var eventId = EventId.Create("missing-target-event");

        var result = await facade.Events.AcceptAsync(
            Inbound(
                InstanceId.Parse(Guid.CreateVersion7().ToString()),
                eventId,
                EventName.Create("resume"),
                CorrelationId.Create("missing-target"),
                DateTimeOffset.Parse("2026-07-30T12:00:00Z")),
            TestContext.Current.CancellationToken);

        result.Should().BeOfType<WorkflowEventAcceptanceResult.Rejected>()
            .Which.Reason.Should().BeOfType<WorkflowEventAcceptanceRejection.DirectInstanceNotFound>();
        (await store.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken))
            .HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task TerminalDirectTarget_IsRejectedWithoutRecordingEventOwnership()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("resume");
        var correlation = CorrelationId.Create("terminal-rejection");
        var facade = CreateFacade(store, driveAfterDelivery: true);
        var handle = facade.Registry.Register(WaitingDefinition(DefinitionId.New(), eventName))
            .GetHandleOrThrow();
        var instanceId = (await handle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("terminal-rejection-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        _ = await facade.Events.AcceptAsync(
            Inbound(
                instanceId,
                EventId.Create("terminal-rejection-complete"),
                eventName,
                correlation,
                DateTimeOffset.Parse("2026-07-30T12:00:00Z")),
            TestContext.Current.CancellationToken);
        var rejectedEventId = EventId.Create("terminal-rejection-after-complete");

        var result = await facade.Events.AcceptAsync(
            Inbound(
                instanceId,
                rejectedEventId,
                eventName,
                correlation,
                DateTimeOffset.Parse("2026-07-30T12:00:01Z")),
            TestContext.Current.CancellationToken);

        result.Should().BeOfType<WorkflowEventAcceptanceResult.Rejected>()
            .Which.Reason.Should().BeOfType<WorkflowEventAcceptanceRejection.DirectInstanceTerminal>();
        (await store.GetByEventIdAsync(rejectedEventId, TestContext.Current.CancellationToken))
            .HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task ReplacementRuntime_IdenticalAcceptedReplayToSameTarget_ReturnsDuplicate()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("resume");
        var correlation = CorrelationId.Create("same-target");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var first = CreateFacade(store, driveAfterDelivery: false);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var started = await firstHandle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("same-target-start"),
            TestContext.Current.CancellationToken);
        var instanceId = started.GetHandleOrThrow().InstanceId;
        var envelope = Inbound(
            instanceId,
            EventId.Create("same-target-event"),
            eventName,
            correlation,
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"));

        var accepted = await first.Events.AcceptAsync(
            envelope,
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var replay = await replacement.Events.AcceptAsync(
            envelope,
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        replay.Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
    }

    [Fact]
    public async Task ReplacementRuntime_ChangedEnvelopeWithSameEventIdAndTarget_ReturnsEventConflict()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("resume");
        var correlation = CorrelationId.Create("changed-envelope");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var first = CreateFacade(store, driveAfterDelivery: false);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var started = await firstHandle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("changed-envelope-start"),
            TestContext.Current.CancellationToken);
        var instanceId = started.GetHandleOrThrow().InstanceId;
        var eventId = EventId.Create("changed-envelope-event");

        var accepted = await first.Events.AcceptAsync(
            Inbound(
                instanceId,
                eventId,
                eventName,
                correlation,
                DateTimeOffset.Parse("2026-07-30T12:00:00Z")),
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var replay = await replacement.Events.AcceptAsync(
            Inbound(
                instanceId,
                eventId,
                eventName,
                correlation,
                DateTimeOffset.Parse("2026-07-30T12:00:01Z")),
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        replay.Should().BeOfType<WorkflowEventAcceptanceResult.Rejected>()
            .Which.Reason.Should().BeOfType<WorkflowEventAcceptanceRejection.EventConflict>();
    }

    [Fact]
    public async Task SameEventId_DifferentTargets_ConflictsGloballyBeforeSecondTargetState()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("resume");
        var firstCorrelation = CorrelationId.Create("first-target");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var facade = CreateFacade(store, driveAfterDelivery: false);
        var handle = facade.Registry.Register(definition).GetHandleOrThrow();
        var firstInstance = (await handle.StartOrGetAsync(
            new Input(firstCorrelation.Value),
            StartIdempotencyKey.Create("first-target-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var missingSecondInstance = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var eventId = EventId.Create("shared-event-id");
        var occurredAt = DateTimeOffset.Parse("2026-07-30T12:00:00Z");

        var firstDelivery = await facade.Events.AcceptAsync(
            Inbound(
                firstInstance,
                eventId,
                eventName,
                firstCorrelation,
                occurredAt),
            TestContext.Current.CancellationToken);
        var secondDelivery = await facade.Events.AcceptAsync(
            Inbound(
                missingSecondInstance,
                eventId,
                eventName,
                CorrelationId.Create("second-target"),
                occurredAt),
            TestContext.Current.CancellationToken);

        firstDelivery.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        secondDelivery.Should().BeOfType<WorkflowEventAcceptanceResult.Rejected>()
            .Which.Reason.Should().BeOfType<WorkflowEventAcceptanceRejection.EventConflict>();
    }

    [Fact]
    public async Task ReplacementRuntime_ReplayClassification_PrecedesTerminalStatus()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("resume");
        var correlation = CorrelationId.Create("terminal-target");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var first = CreateFacade(store, driveAfterDelivery: true);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var instanceId = (await firstHandle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("terminal-target-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var eventId = EventId.Create("terminal-target-event");
        var occurredAt = DateTimeOffset.Parse("2026-07-30T12:00:00Z");

        var accepted = await first.Events.AcceptAsync(
            Inbound(instanceId, eventId, eventName, correlation, occurredAt),
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, driveAfterDelivery: true);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var duplicate = await replacement.Events.AcceptAsync(
            Inbound(instanceId, eventId, eventName, correlation, occurredAt),
            TestContext.Current.CancellationToken);
        var conflict = await replacement.Events.AcceptAsync(
            Inbound(
                instanceId,
                eventId,
                eventName,
                correlation,
                occurredAt.AddSeconds(1)),
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        duplicate.Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
        conflict.Should().BeOfType<WorkflowEventAcceptanceResult.Rejected>()
            .Which.Reason.Should().BeOfType<WorkflowEventAcceptanceRejection.EventConflict>();
    }

    private static DurableWorkflowDefinition<Input> WaitingDefinition(
        DefinitionId definitionId,
        EventName eventName)
    {
        return Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Correlation))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), state => CorrelationId.Create(state.Value.Correlation))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
    }

    private static FacadeServices CreateFacade(
        InMemoryWorkflowProvider store,
        bool driveAfterDelivery)
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
        return new FacadeServices(
            new DurableWorkflowDefinitionRegistry(
                runtime,
                store,
                store,
                processor,
                notifications,
                TimeProvider.System),
            new DurableWorkflowEventIngressCore(runtime, store, store, driveAfterDelivery));
    }

    private static WorkflowInboundEvent Inbound(
        InstanceId instanceId,
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt) =>
        WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            eventId,
            correlationId,
            causationEventId: null,
            occurredAt,
            new WorkflowEventRoute.Direct(instanceId));

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        DurableWorkflowEventIngressCore Events);

    private sealed record Input(string Correlation);
    private sealed record State(string Correlation);
}
