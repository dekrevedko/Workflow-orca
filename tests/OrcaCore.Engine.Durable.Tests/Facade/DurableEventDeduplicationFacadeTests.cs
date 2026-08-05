using AwesomeAssertions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableEventDeduplicationFacadeTests
{
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
        var envelope = WorkflowEvent.Create(
            EventId.Create("same-target-event"),
            eventName,
            correlation,
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"));

        var accepted = await first.Events.DeliverToInstanceAsync(
            instanceId,
            envelope,
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var replay = await replacement.Events.DeliverToInstanceAsync(
            instanceId,
            envelope,
            TestContext.Current.CancellationToken);

        accepted.Status.Should().Be(EventDeliveryStatus.Accepted);
        replay.Status.Should().Be(EventDeliveryStatus.Duplicate);
        replay.InstanceId.Should().Be(instanceId);
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

        var accepted = await first.Events.DeliverToInstanceAsync(
            instanceId,
            WorkflowEvent.Create(
                eventId,
                eventName,
                correlation,
                DateTimeOffset.Parse("2026-07-30T12:00:00Z")),
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var replay = await replacement.Events.DeliverToInstanceAsync(
            instanceId,
            WorkflowEvent.Create(
                eventId,
                eventName,
                correlation,
                DateTimeOffset.Parse("2026-07-30T12:00:01Z")),
            TestContext.Current.CancellationToken);

        accepted.Status.Should().Be(EventDeliveryStatus.Accepted);
        replay.Status.Should().Be(EventDeliveryStatus.EventConflict);
        replay.InstanceId.Should().Be(instanceId);
    }

    [Fact]
    public async Task SameEventId_DifferentTargets_AreIndependent()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("resume");
        var firstCorrelation = CorrelationId.Create("first-target");
        var secondCorrelation = CorrelationId.Create("second-target");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var facade = CreateFacade(store, driveAfterDelivery: false);
        var handle = facade.Registry.Register(definition).GetHandleOrThrow();
        var firstInstance = (await handle.StartOrGetAsync(
            new Input(firstCorrelation.Value),
            StartIdempotencyKey.Create("first-target-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var secondInstance = (await handle.StartOrGetAsync(
            new Input(secondCorrelation.Value),
            StartIdempotencyKey.Create("second-target-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var eventId = EventId.Create("shared-event-id");
        var occurredAt = DateTimeOffset.Parse("2026-07-30T12:00:00Z");

        var firstDelivery = await facade.Events.DeliverToInstanceAsync(
            firstInstance,
            WorkflowEvent.Create(
                eventId,
                eventName,
                firstCorrelation,
                occurredAt),
            TestContext.Current.CancellationToken);
        var secondDelivery = await facade.Events.DeliverToInstanceAsync(
            secondInstance,
            WorkflowEvent.Create(
                eventId,
                eventName,
                secondCorrelation,
                occurredAt),
            TestContext.Current.CancellationToken);

        firstDelivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        secondDelivery.Status.Should().Be(EventDeliveryStatus.Accepted);
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

        var accepted = await first.Events.DeliverToInstanceAsync(
            instanceId,
            WorkflowEvent.Create(eventId, eventName, correlation, occurredAt),
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, driveAfterDelivery: true);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var duplicate = await replacement.Events.DeliverToInstanceAsync(
            instanceId,
            WorkflowEvent.Create(eventId, eventName, correlation, occurredAt),
            TestContext.Current.CancellationToken);
        var conflict = await replacement.Events.DeliverToInstanceAsync(
            instanceId,
            WorkflowEvent.Create(
                eventId,
                eventName,
                correlation,
                occurredAt.AddSeconds(1)),
            TestContext.Current.CancellationToken);

        accepted.Status.Should().Be(EventDeliveryStatus.Accepted);
        duplicate.Status.Should().Be(EventDeliveryStatus.Duplicate);
        conflict.Status.Should().Be(EventDeliveryStatus.EventConflict);
    }

    private static DurableWorkflowDefinition<Input> WaitingDefinition(
        DefinitionId definitionId,
        EventName eventName)
    {
        return Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Correlation))
            .Wait(eventName, state => CorrelationId.Create(state.Value.Correlation))
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
            new DurableWorkflowEventClient(runtime, store, store, driveAfterDelivery));
    }

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        DurableWorkflowEventClient Events);

    private sealed record Input(string Correlation);
    private sealed record State(string Correlation);
}
