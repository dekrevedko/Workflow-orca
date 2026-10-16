using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.ProviderCertification;

public static class OperationalMaintenanceCertification
{
    public static async Task RunAsync(
        IWorkflowEventStore eventStore,
        IWorkflowInboxStore inboxStore,
        IWorkflowOutboxStore outboxStore,
        IWorkflowOperationalStore operationalStore,
        IWorkflowProjectionStore projectionStore,
        IWorkflowProviderMaintenanceStore maintenanceStore,
        string expectedProviderName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProviderName);
        var definitionId = global::OrcaCore.DefinitionId.New();
        var runningId = global::OrcaCore.InstanceId.Parse(Guid.CreateVersion7().ToString());
        var waitingId = global::OrcaCore.InstanceId.Parse(Guid.CreateVersion7().ToString());
        var pressureId = global::OrcaCore.InstanceId.Parse(Guid.CreateVersion7().ToString());
        var continuationId = new OutboxRecordId(Guid.CreateVersion7());
        var externalId = new OutboxRecordId(Guid.CreateVersion7());
        var statisticsRequest = new WorkflowOperatorStatisticsRequest(
            Timestamp(10),
            TimeSpan.FromSeconds(5));
        await eventStore.AppendAsync(
            SeedBatch(
                runningId,
                definitionId,
                global::OrcaCore.DefinitionVersion.Initial,
                global::OrcaCore.WorkflowInstanceStatus.Running,
                checkpoint: true),
            cancellationToken).ConfigureAwait(false);
        await eventStore.AppendAsync(
            SeedBatch(
                waitingId,
                definitionId,
                global::OrcaCore.DefinitionVersion.Initial,
                global::OrcaCore.WorkflowInstanceStatus.Waiting,
                checkpoint: false),
            cancellationToken).ConfigureAwait(false);
        await eventStore.AppendAsync(
            SeedBatch(
                pressureId,
                definitionId,
                new global::OrcaCore.DefinitionVersion(2),
                global::OrcaCore.WorkflowInstanceStatus.Completed,
                checkpoint: false,
                outbox:
                [
                    new OutboxWrite(continuationId, OutboxKinds.Continue, [1]),
                    new OutboxWrite(externalId, "operator-certification", [2])
                ]),
            cancellationToken).ConfigureAwait(false);

        var beforeStuckRefresh = await operationalStore
            .GetOperatorStatisticsAsync(cancellationToken)
            .ConfigureAwait(false);
        beforeStuckRefresh.ProviderName.Should().Be(expectedProviderName);
        beforeStuckRefresh.Pressure.StuckInstanceCount.Should().Be(0,
            "reading operator statistics must not mutate provider state");
        (await projectionStore.GetAsync(runningId, cancellationToken).ConfigureAwait(false))
            .Value.IsStuck.Should().BeFalse();

        await operationalStore
            .RefreshStuckStateAsync(statisticsRequest, cancellationToken)
            .ConfigureAwait(false);
        var initial = await operationalStore
            .GetOperatorStatisticsAsync(cancellationToken)
            .ConfigureAwait(false);
        initial.ProviderName.Should().Be(expectedProviderName);
        initial.Groups.Should().Contain(group =>
            group.DefinitionId.Equals(definitionId) &&
            group.DefinitionVersion.Equals(global::OrcaCore.DefinitionVersion.Initial) &&
            group.Status == global::OrcaCore.WorkflowInstanceStatus.Running &&
            group.Count == 1);
        initial.Groups.Should().Contain(group =>
            group.DefinitionId.Equals(definitionId) &&
            group.DefinitionVersion.Equals(new global::OrcaCore.DefinitionVersion(2)) &&
            group.Status == global::OrcaCore.WorkflowInstanceStatus.Completed &&
            group.Count == 1);
        initial.Pressure.ActiveInstanceCount.Should().Be(2);
        initial.Pressure.StuckInstanceCount.Should().Be(1);
        initial.Pressure.ActiveWaitCount.Should().Be(1);
        initial.StuckGroups.Should().ContainSingle().Which.Should().Be(
            new WorkflowOperatorStuckGroup(definitionId, 1));
        var markedRunning = await projectionStore.GetAsync(runningId, cancellationToken).ConfigureAwait(false);
        markedRunning.HasValue.Should().BeTrue();
        markedRunning.Value.LastActiveAt.Should().Be(Timestamp(1));
        markedRunning.Value.IsStuck.Should().BeTrue();
        markedRunning.Value.StuckDetectedAt.Should().Be(Timestamp(10));
        var healthyWaiting = await projectionStore.GetAsync(waitingId, cancellationToken).ConfigureAwait(false);
        healthyWaiting.HasValue.Should().BeTrue();
        healthyWaiting.Value.IsStuck.Should().BeFalse(
            "an ordinary external wait is not stuck solely because its last transition is old");
        initial.ActiveWaitGroups.Should().ContainSingle().Which.Should().Be(
            new WorkflowOperatorActiveWaitGroup(
                definitionId,
                global::OrcaCore.EventName.Create("operator-certification"),
                1));
        initial.Pressure.StreamEventCount.Should().BeGreaterThanOrEqualTo(2);
        initial.Pressure.CheckpointCount.Should().BeGreaterThanOrEqualTo(1);
        initial.Pressure.CheckpointLag.Should().BeGreaterThanOrEqualTo(1);
        initial.Pressure.ContinuationPendingCount.Should().Be(1);
        initial.Pressure.ExternalOutboxPendingCount.Should().Be(1);

        var claimed = await outboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(10), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including("operator-certification")
            },
            cancellationToken).ConfigureAwait(false);
        claimed.Should().ContainSingle().Which.OutboxRecordId.Should().Be(externalId);
        (await operationalStore.GetOperatorStatisticsAsync(cancellationToken).ConfigureAwait(false))
            .Pressure.ExternalOutboxClaimedCount.Should().Be(1);
        await outboxStore.MarkPoisonedAsync(
            externalId,
            "operator-certification-permanent",
            "certified permanent failure",
            cancellationToken).ConfigureAwait(false);
        (await operationalStore.GetOperatorStatisticsAsync(cancellationToken).ConfigureAwait(false))
            .Pressure.ExternalOutboxPoisonedCount.Should().Be(1);

        var blocked = await maintenanceStore.PurgeForMaintenanceAsync(
            new WorkflowProviderMaintenanceRequest(pressureId, Timestamp(11)),
            cancellationToken).ConfigureAwait(false);
        blocked.Should().Be(new WorkflowProviderMaintenanceResult(
            WorkflowProviderMaintenanceDisposition.Rejected,
            WorkflowProviderMaintenanceBlocker.PendingOutboxDispatch));

        await CertifyMonotonicRouteTombstoneAsync(
            eventStore,
            inboxStore,
            maintenanceStore,
            definitionId,
            cancellationToken).ConfigureAwait(false);
        await CertifyArchiveOwnershipAndArtifactParityAsync(
            eventStore,
            operationalStore,
            maintenanceStore,
            definitionId,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task CertifyArchiveOwnershipAndArtifactParityAsync(
        IWorkflowEventStore eventStore,
        IWorkflowOperationalStore operationalStore,
        IWorkflowProviderMaintenanceStore maintenanceStore,
        global::OrcaCore.DefinitionId definitionId,
        CancellationToken cancellationToken)
    {
        var instanceId = global::OrcaCore.InstanceId.Parse(Guid.CreateVersion7().ToString());
        var seeded = await eventStore.AppendAsync(
            SeedBatch(
                instanceId,
                definitionId,
                global::OrcaCore.DefinitionVersion.Initial,
                global::OrcaCore.WorkflowInstanceStatus.Completed,
                checkpoint: false),
            cancellationToken).ConfigureAwait(false);
        seeded.IsSuccess.Should().BeTrue();

        var archived = await maintenanceStore.ArchiveForMaintenanceAsync(
            new WorkflowProviderMaintenanceRequest(instanceId, Timestamp(24)),
            cancellationToken).ConfigureAwait(false);
        archived.Disposition.Should().Be(WorkflowProviderMaintenanceDisposition.Archived);

        var laterProjection = await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = new StreamVersion(1),
                ProjectionOperations =
                [
                    Projection(
                        instanceId,
                        definitionId,
                        global::OrcaCore.DefinitionVersion.Initial,
                        global::OrcaCore.WorkflowInstanceStatus.Completed,
                        new StreamVersion(1))
                ]
            },
            cancellationToken).ConfigureAwait(false);
        laterProjection.IsSuccess.Should().BeTrue();
        (await maintenanceStore.InspectForMaintenanceAsync(instanceId, cancellationToken).ConfigureAwait(false))
            .ArchivedAt.Should().Be(Timestamp(24),
                "archive time is provider-owned metadata and must survive later aggregate projections");

        var streamOnlyId = global::OrcaCore.InstanceId.Parse(Guid.CreateVersion7().ToString());
        var pressureBeforeStreamOnly = await operationalStore
            .GetOperatorStatisticsAsync(cancellationToken)
            .ConfigureAwait(false);
        var streamOnly = await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(streamOnlyId),
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    new WorkflowStartedEvent
                    {
                        EventId = global::OrcaCore.EventId.Create(Guid.CreateVersion7().ToString()),
                        InstanceId = streamOnlyId,
                        CommandId = CommandId.New(),
                        CausationId = CausationId.New(),
                        OccurredAt = Timestamp(25),
                        RootInstanceId = streamOnlyId,
                        DefinitionId = definitionId,
                        DefinitionVersion = global::OrcaCore.DefinitionVersion.Initial
                    }
                ]
            },
            cancellationToken).ConfigureAwait(false);
        streamOnly.IsSuccess.Should().BeTrue();
        var pressureAfterStreamOnly = await operationalStore
            .GetOperatorStatisticsAsync(cancellationToken)
            .ConfigureAwait(false);
        pressureAfterStreamOnly.Pressure.StreamEventCount.Should().Be(
            pressureBeforeStreamOnly.Pressure.StreamEventCount + 1,
            "stream-only durable artifacts must participate in provider pressure without scanning the event table");
        (await maintenanceStore.ArchiveForMaintenanceAsync(
                new WorkflowProviderMaintenanceRequest(streamOnlyId, Timestamp(26)),
                cancellationToken).ConfigureAwait(false))
            .Disposition.Should().Be(WorkflowProviderMaintenanceDisposition.NotFound,
                "archive owns projection metadata and a stream without a projection is not archivable");
    }

    private static async Task CertifyMonotonicRouteTombstoneAsync(
        IWorkflowEventStore eventStore,
        IWorkflowInboxStore inboxStore,
        IWorkflowProviderMaintenanceStore maintenanceStore,
        global::OrcaCore.DefinitionId definitionId,
        CancellationToken cancellationToken)
    {
        var instanceId = global::OrcaCore.InstanceId.Parse(Guid.CreateVersion7().ToString());
        var eventId = global::OrcaCore.EventId.Create(Guid.CreateVersion7().ToString());
        var eventName = global::OrcaCore.EventName.Create("retention-route-tombstone");
        var correlationId = global::OrcaCore.CorrelationId.Create("retention-route-tombstone");
        await eventStore.AppendAsync(
            SeedBatch(
                instanceId,
                definitionId,
                global::OrcaCore.DefinitionVersion.Initial,
                global::OrcaCore.WorkflowInstanceStatus.Waiting,
                checkpoint: false),
            cancellationToken).ConfigureAwait(false);
        var envelope = new DurableEventEnvelope
        {
            EventId = eventId,
            EventName = eventName.Value,
            EventContractVersion = global::OrcaCore.EventContractVersion.Initial.Value,
            CorrelationId = correlationId,
            OccurredAt = Timestamp(20),
            Route = new DurableEventRouteEnvelope
            {
                Kind = InboxRouteKinds.Direct,
                InstanceId = instanceId
            }
        };
        var accepted = await inboxStore.AcceptAsync(
            new InboxAcceptance(envelope, "retention-route-tombstone-fingerprint", Timestamp(20)),
            cancellationToken).ConfigureAwait(false);
        accepted.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);
        var terminalized = await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = new StreamVersion(1),
                ProjectionOperations =
                [
                    Projection(
                        instanceId,
                        definitionId,
                        global::OrcaCore.DefinitionVersion.Initial,
                        global::OrcaCore.WorkflowInstanceStatus.Completed,
                        new StreamVersion(1))
                ]
            },
            cancellationToken).ConfigureAwait(false);
        terminalized.IsSuccess.Should().BeTrue();

        var pendingBlock = await maintenanceStore.PurgeForMaintenanceAsync(
            new WorkflowProviderMaintenanceRequest(instanceId, Timestamp(21)),
            cancellationToken).ConfigureAwait(false);
        pendingBlock.Blocker.Should().Be(WorkflowProviderMaintenanceBlocker.PendingInboxDelivery);

        var match = await inboxStore.GetMatchSnapshotAsync(
            new InboxMatchRequest(
                instanceId,
                definitionId,
                eventName,
                global::OrcaCore.EventContractVersion.Initial,
                correlationId),
            cancellationToken).ConfigureAwait(false);
        match.PendingEvent.Should().NotBeNull();
        var applied = await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = new StreamVersion(1),
                InboxRouteMutations = match.RouteRevisions
                    .Select(revision => new InboxRouteMutation(revision.Route, revision.Revision))
                    .ToArray(),
                InboxOperations =
                [
                    new InboxWrite(eventId, InboxRecordState.Applied)
                    {
                        ExpectedState = InboxRecordState.Received,
                        TargetInstanceId = instanceId
                    }
                ],
                ProjectionOperations =
                [
                    Projection(
                        instanceId,
                        definitionId,
                        global::OrcaCore.DefinitionVersion.Initial,
                        global::OrcaCore.WorkflowInstanceStatus.Completed,
                        new StreamVersion(1))
                ]
            },
            cancellationToken).ConfigureAwait(false);
        applied.IsSuccess.Should().BeTrue();

        var archived = await maintenanceStore.ArchiveForMaintenanceAsync(
            new WorkflowProviderMaintenanceRequest(instanceId, Timestamp(22)),
            cancellationToken).ConfigureAwait(false);
        archived.Disposition.Should().Be(WorkflowProviderMaintenanceDisposition.Archived);
        var inspection = await maintenanceStore.InspectForMaintenanceAsync(instanceId, cancellationToken)
            .ConfigureAwait(false);
        inspection.Should().Be(new WorkflowProviderMaintenanceInspection(
            Exists: true,
            ArchivedAt: Timestamp(22),
            Blocker: null));
        var purged = await maintenanceStore.PurgeForMaintenanceAsync(
            new WorkflowProviderMaintenanceRequest(instanceId, Timestamp(23)),
            cancellationToken).ConfigureAwait(false);
        purged.Disposition.Should().Be(WorkflowProviderMaintenanceDisposition.Purged);
        (await maintenanceStore.InspectForMaintenanceAsync(instanceId, cancellationToken).ConfigureAwait(false))
            .Exists.Should().BeFalse();
        (await inboxStore.GetByEventIdAsync(eventId, cancellationToken).ConfigureAwait(false))
            .Value.State.Should().Be(InboxRecordState.Applied,
                "accepted-event confirmation is retained as a tombstone after physical cleanup");

        var staleRoute = match.RouteRevisions.Single(revision => revision.Route.Kind == InboxRouteKinds.Direct).Route;
        var staleCommit = await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                InboxRouteMutations = [new InboxRouteMutation(staleRoute, 0)]
            },
            cancellationToken).ConfigureAwait(false);
        staleCommit.IsFailure.Should().BeTrue(
            "physical cleanup must retain a monotonic route-revision tombstone against delayed snapshots");
    }

    private static ProviderCommitBatch SeedBatch(
        global::OrcaCore.InstanceId instanceId,
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.DefinitionVersion definitionVersion,
        global::OrcaCore.WorkflowInstanceStatus status,
        bool checkpoint,
        bool stuck = false,
        IReadOnlyList<OutboxWrite>? outbox = null)
    {
        var occurredAt = Timestamp(1);
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = StreamVersion.Empty,
            Events =
            [
                new WorkflowStartedEvent
                {
                    EventId = global::OrcaCore.EventId.Create(Guid.CreateVersion7().ToString()),
                    InstanceId = instanceId,
                    CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = occurredAt,
                    RootInstanceId = instanceId,
                    DefinitionId = definitionId,
                    DefinitionVersion = definitionVersion
                }
            ],
            Checkpoint = checkpoint
                ? new CheckpointWrite(instanceId, new StreamVersion(1), "application/orcacore-certification", [1])
                : null,
            OutboxRecords = outbox ?? [],
            ProjectionOperations =
            [
                Projection(instanceId, definitionId, definitionVersion, status, new StreamVersion(1), stuck)
            ]
        };
    }

    private static ProjectionWrite Projection(
        global::OrcaCore.InstanceId instanceId,
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.DefinitionVersion definitionVersion,
        global::OrcaCore.WorkflowInstanceStatus status,
        StreamVersion streamVersion,
        bool stuck = false) =>
        new(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot = new WorkflowProjectionSnapshot
            {
                InstanceId = instanceId,
                RootInstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion,
                Status = status,
                StreamVersion = streamVersion.Value,
                CreatedAt = Timestamp(1),
                UpdatedAt = Timestamp(1),
                LastActiveAt = Timestamp(1),
                IsStuck = stuck,
                StuckDetectedAt = stuck ? Timestamp(2) : null,
                ActiveWaits = status == global::OrcaCore.WorkflowInstanceStatus.Running
                    ?
                    [
                        new WorkflowProjectionActiveWaitSnapshot
                        {
                            WaitId = global::OrcaCore.WaitId.Parse(Guid.CreateVersion7().ToString()),
                            EventName = "operator-certification",
                            EventContractVersion = 1,
                            CorrelationId = global::OrcaCore.CorrelationId.Create("operator-certification"),
                            RegisteredAt = Timestamp(1),
                            Status = "Active",
                            Mode = "Durable"
                        }
                    ]
                    : []
            }
        };

    private static DateTimeOffset Timestamp(int second) =>
        new(2026, 8, 9, 12, 0, second, TimeSpan.Zero);
}
