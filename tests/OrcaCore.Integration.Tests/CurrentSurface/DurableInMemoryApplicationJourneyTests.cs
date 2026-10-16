using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting;

namespace OrcaCore.Integration.Tests.CurrentSurface;

public sealed class DurableInMemoryApplicationJourneyTests
{
    [Fact]
    public async Task CorrelationEventsAcceptedBeforeStart_ConsumeOldestWhenTheWaitRegisters()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        using var host = DurableTestHosts.BuildInMemory(stores);
        var definitionId = DefinitionId.New();
        var eventContract = WorkflowEventContract.Create(
            EventName.Create("before-wait-correlation"),
            EventContractVersion.Initial);
        var correlation = CorrelationId.Create("before-wait-correlation");
        var definition = Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(eventContract, _ => correlation)
            .End(WorkflowOutcomeName.Create("finished"))
            .Build();
        var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var ingress = host.GetRequiredService<IWorkflowEventIngress>();

        foreach (var eventId in new[] { "before-wait-first", "before-wait-second" })
        {
            var accepted = await ingress.AcceptAsync(
                WorkflowInboundEvent.Create(
                    eventContract,
                    EventId.Create(eventId),
                    correlation,
                    causationEventId: null,
                    DateTimeOffset.Parse("2026-08-06T12:00:00Z"),
                    new WorkflowEventRoute.Correlation(definitionId)),
                TestContext.Current.CancellationToken);
            accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        }

        var started = await handle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("before-wait-correlation"),
            TestContext.Current.CancellationToken);

        (await started.GetHandleOrThrow().GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        var first = await stores.InboxStore.GetByEventIdAsync(
            EventId.Create("before-wait-first"),
            TestContext.Current.CancellationToken);
        var second = await stores.InboxStore.GetByEventIdAsync(
            EventId.Create("before-wait-second"),
            TestContext.Current.CancellationToken);
        first.Value.State.Should().Be(InboxRecordState.Applied);
        first.Value.InstanceId.Should().Be(started.GetHandleOrThrow().InstanceId);
        second.Value.State.Should().Be(InboxRecordState.Received);
        second.Value.AcceptanceSequence.Should().BeGreaterThan(first.Value.AcceptanceSequence);
    }

    [Fact]
    public async Task DirectEventAcceptedBeforeItsWait_RemainsPendingAndResumesTheLaterWait()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        using var host = DurableTestHosts.BuildInMemory(stores);
        var definitionId = DefinitionId.New();
        var eventName = EventName.Create("before-wait-direct");
        var correlation = CorrelationId.Create("before-wait-direct");
        var v1 = WorkflowEventContract.Create(eventName, EventContractVersion.Initial);
        var v2 = WorkflowEventContract.Create(eventName, new EventContractVersion(2));
        var definition = Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(v1, _ => correlation)
            .Wait(v2, _ => correlation)
            .End(WorkflowOutcomeName.Create("finished"))
            .Build();
        var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var started = await handle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("before-wait-direct"),
            TestContext.Current.CancellationToken);
        var instance = started.GetHandleOrThrow();
        var ingress = host.GetRequiredService<IWorkflowEventIngress>();

        var early = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v2,
                EventId.Create("before-wait-direct-v2"),
                correlation,
                causationEventId: null,
                DateTimeOffset.Parse("2026-08-06T12:00:00Z"),
                new WorkflowEventRoute.Direct(instance.InstanceId)),
            TestContext.Current.CancellationToken);
        early.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await stores.InboxStore.GetByEventIdAsync(
                EventId.Create("before-wait-direct-v2"),
                TestContext.Current.CancellationToken))
            .Value.State.Should().Be(InboxRecordState.Received);

        var firstWait = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v1,
                EventId.Create("before-wait-direct-v1"),
                correlation,
                causationEventId: null,
                DateTimeOffset.Parse("2026-08-06T12:00:01Z"),
                new WorkflowEventRoute.Direct(instance.InstanceId)),
            TestContext.Current.CancellationToken);

        firstWait.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await stores.InboxStore.GetByEventIdAsync(
                EventId.Create("before-wait-direct-v2"),
                TestContext.Current.CancellationToken))
            .Value.State.Should().Be(InboxRecordState.Applied);
    }

    [Fact]
    public async Task UnmatchedDirectEvent_RemainsObservableAsPoisonWhenItsTargetCompletes()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        using var host = DurableTestHosts.BuildInMemory(stores);
        var eventName = EventName.Create("terminal-pending-direct");
        var correlation = CorrelationId.Create("terminal-pending-direct");
        var v1 = WorkflowEventContract.Create(eventName, EventContractVersion.Initial);
        var v2 = WorkflowEventContract.Create(eventName, new EventContractVersion(2));
        var definition = Workflow.Durable<JourneyState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(v1, _ => correlation)
            .End(WorkflowOutcomeName.Create("finished"))
            .Build();
        var definitionHandle = host.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("terminal-pending-direct"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var ingress = host.GetRequiredService<IWorkflowEventIngress>();

        await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v2,
                EventId.Create("terminal-pending-direct-v2"),
                correlation,
                causationEventId: null,
                DateTimeOffset.Parse("2026-08-06T12:00:00Z"),
                new WorkflowEventRoute.Direct(instance.InstanceId)),
            TestContext.Current.CancellationToken);
        await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v1,
                EventId.Create("terminal-pending-direct-v1"),
                correlation,
                causationEventId: null,
                DateTimeOffset.Parse("2026-08-06T12:00:01Z"),
                new WorkflowEventRoute.Direct(instance.InstanceId)),
            TestContext.Current.CancellationToken);

        var poison = await stores.InboxStore.GetByEventIdAsync(
            EventId.Create("terminal-pending-direct-v2"),
            TestContext.Current.CancellationToken);
        poison.Value.State.Should().Be(InboxRecordState.Poisoned);
        poison.Value.PoisonCode.Should().Be("target-terminal");
        poison.Value.Envelope.Should().NotBeNull();
    }

    [Fact]
    public async Task ParallelDuplicateWaits_AreRejectedBeforeEitherWaitBecomesRoutable()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        using var host = DurableTestHosts.BuildInMemory(stores);
        var definitionId = DefinitionId.New();
        var eventContract = WorkflowEventContract.Create(
            EventName.Create("ambiguous-durable-wait"),
            new EventContractVersion(2));
        var correlation = CorrelationId.Create("ambiguous-durable-wait");
        var definition = Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Parallel<string>(branches => branches
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("first"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(eventContract, _ => correlation)
                        .Return(state => state.Value.Value.ToString()))
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("second"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(eventContract, _ => correlation)
                        .Return(state => state.Value.Value.ToString())))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        var start = async () => await handle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("ambiguous-durable-wait"),
            TestContext.Current.CancellationToken);

        var exception = await start.Should().ThrowAsync<AmbiguousWaitRegistrationException>();
        exception.Which.DefinitionId.Should().Be(definitionId);
        exception.Which.EventContract.Should().Be(eventContract);
        exception.Which.CorrelationId.Should().Be(correlation);

        var delivery = await host.GetRequiredService<IWorkflowEventIngress>().AcceptAsync(
            WorkflowInboundEvent.Create(
                eventContract,
                EventId.Create("ambiguous-durable-wait-probe"),
                correlation,
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Correlation(definitionId)),
            TestContext.Current.CancellationToken);

        delivery.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        var pending = await stores.InboxStore.GetByEventIdAsync(
            EventId.Create("ambiguous-durable-wait-probe"),
            TestContext.Current.CancellationToken);
        pending.Value.State.Should().Be(InboxRecordState.Received);
        pending.Value.InstanceId.Should().BeNull();
    }

    [Fact]
    public async Task PublicV2Ingress_ResumesOnlyTheMatchingV2WaitAndNeverTheV1Wait()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        using var host = DurableTestHosts.BuildInMemory(stores);
        var eventName = EventName.Create("versioned-approval");
        var correlation = CorrelationId.Create("versioned-approval");
        var definitions = host.GetRequiredService<IWorkflowDefinitionRegistry>();
        var v1 = definitions.Register(VersionedDefinition(
            DefinitionId.New(),
            EventContractVersion.Initial,
            eventName,
            correlation)).GetHandleOrThrow();
        var v2 = definitions.Register(VersionedDefinition(
            DefinitionId.New(),
            new EventContractVersion(2),
            eventName,
            correlation)).GetHandleOrThrow();
        var v1Instance = (await v1.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("versioned-v1"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var v2Instance = (await v2.StartOrGetAsync(
            new JourneyInput(2),
            StartIdempotencyKey.Create("versioned-v2"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var ingress = host.GetRequiredService<IWorkflowEventIngress>();
        var v2Contract = WorkflowEventContract.Create(eventName, new EventContractVersion(2));

        var wrongVersion = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v2Contract,
                EventId.Create("versioned-v2-to-v1"),
                correlation,
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Direct(v1Instance.InstanceId)),
            TestContext.Current.CancellationToken);

        wrongVersion.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        var pendingWrongVersion = await stores.InboxStore.GetByEventIdAsync(
            EventId.Create("versioned-v2-to-v1"),
            TestContext.Current.CancellationToken);
        pendingWrongVersion.Value.State.Should().Be(InboxRecordState.Received);
        (await v1Instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Waiting);

        var accepted = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v2Contract,
                EventId.Create("versioned-v2-to-v2"),
                correlation,
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Direct(v2Instance.InstanceId)),
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await v2Instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await v1Instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Waiting);
    }

    [Fact]
    public async Task ReplacementHost_PreservesStartBindingAndCompletesThroughPublicFacade()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        var continueEvent = EventName.Create("continue");
        var correlation = CorrelationId.Create("durable-in-memory-replacement");
        var definition = Workflow.Durable<JourneyState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(continueEvent, EventContractVersion.Initial), _ => correlation)
            .End(
                state => new JourneyOutput(state.Value.Value + 1),
                WorkflowOutcomeName.Create("finished"))
            .Build();
        var startKey = StartIdempotencyKey.Create("durable-in-memory-replacement");
        InstanceId firstInstanceId;

        using (var firstHost = DurableTestHosts.BuildInMemory(stores))
        {
            var definitions = firstHost.GetRequiredService<IWorkflowDefinitionRegistry>();
            var handle = definitions.Register(definition).GetHandleOrThrow();
            var firstStart = await handle.StartOrGetAsync(
                new JourneyInput(41),
                startKey,
                TestContext.Current.CancellationToken);
            firstStart.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Accepted>()
                .Which.WasExisting.Should().BeFalse();
            firstInstanceId = firstStart.GetHandleOrThrow().InstanceId;
        }

        using var replacementHost = DurableTestHosts.BuildInMemory(stores);
        var replacementDefinitions =
            replacementHost.GetRequiredService<IWorkflowDefinitionRegistry>();
        var replacementHandle = replacementDefinitions.Register(definition).GetHandleOrThrow();

        var incompatibleReplay = await replacementHandle.StartOrGetAsync(
            new JourneyInput(99),
            startKey,
            TestContext.Current.CancellationToken);
        incompatibleReplay.Should()
            .BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Conflict>();

        var replay = await replacementHandle.StartOrGetAsync(
            new JourneyInput(41),
            startKey,
            TestContext.Current.CancellationToken);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        replay.GetHandleOrThrow().InstanceId.Should().Be(firstInstanceId);

        var delivery = await replacementHost
            .GetRequiredService<IWorkflowEventIngress>()
            .AcceptAsync(
                WorkflowInboundEvent.Create(
                    WorkflowEventContract.Create(continueEvent, EventContractVersion.Initial),
                    EventId.Create("durable-in-memory-continue"),
                    correlation,
                    causationEventId: null,
                    DateTimeOffset.UtcNow,
                    new WorkflowEventRoute.Direct(firstInstanceId)),
                TestContext.Current.CancellationToken);

        delivery.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await replay.WaitForOutputAsync(TestContext.Current.CancellationToken))
            .Should().Be(new JourneyOutput(42));
        (await replay.GetHandleOrThrow().GetSnapshotAsync(
                TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    private sealed record JourneyInput(int Value);

    private sealed record JourneyState(int Value);

    private sealed record JourneyBranchState(int Value);

    private sealed record JourneyOutput(int Value);

    private static DurableWorkflowDefinition<JourneyInput> VersionedDefinition(
        DefinitionId definitionId,
        EventContractVersion eventContractVersion,
        EventName eventName,
        CorrelationId correlation) =>
        Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, eventContractVersion), _ => correlation)
            .End(WorkflowOutcomeName.Create("finished"))
            .Build();
}
