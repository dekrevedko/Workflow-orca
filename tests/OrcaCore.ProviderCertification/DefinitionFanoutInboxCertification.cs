using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public static class DefinitionFanoutInboxCertification
{
    public static async Task RunAsync(IProviderCertificationFixture fixture)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var definitionId = DefinitionId.New();
        var otherDefinitionId = DefinitionId.New();
        var firstInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var secondInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var terminalInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var unrelatedInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var laterInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var eventName = EventName.Create("definition-fanout-certification");
        var eventVersion = new EventContractVersion(2);
        var correlationId = CorrelationId.Create("definition-fanout-certification");
        await fixture.ProjectionStore.ApplyAsync(
            [
                Projection(firstInstanceId, definitionId, DefinitionVersion.Initial, WorkflowInstanceStatus.Running),
                Projection(secondInstanceId, definitionId, new DefinitionVersion(2), WorkflowInstanceStatus.Waiting),
                Projection(terminalInstanceId, definitionId, DefinitionVersion.Initial, WorkflowInstanceStatus.Completed),
                Projection(unrelatedInstanceId, otherDefinitionId, DefinitionVersion.Initial, WorkflowInstanceStatus.Running)
            ],
            cancellationToken);

        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
        var envelope = Envelope(eventId, definitionId, eventName, eventVersion, correlationId);
        var acceptance = new InboxDefinitionFanoutAcceptance(
            new InboxAcceptance(envelope, "definition-fanout-envelope", envelope.OccurredAt),
            definitionId,
            MaximumTargetCount: 2);

        var accepted = await fixture.InboxStore.AcceptDefinitionFanoutAsync(acceptance, cancellationToken);
        var targets = await fixture.InboxStore.ListDefinitionFanoutTargetsAsync(eventId, cancellationToken);

        accepted.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);
        accepted.DefinitionFanoutTargets.Should().BeEquivalentTo([firstInstanceId, secondInstanceId]);
        targets.Select(target => target.InstanceId).Should().BeEquivalentTo([firstInstanceId, secondInstanceId]);
        targets.Should().OnlyContain(target =>
            target.State == InboxRecordState.Received &&
            target.Envelope!.Route.Kind == "definition-fanout" &&
            target.Route!.Kind == "definition-fanout-target");
        var firstPage = await fixture.InboxStore.ListReceivedAsync(
            accepted.Record!.AcceptanceSequence,
            maxCount: 1,
            cancellationToken);
        var secondPage = await fixture.InboxStore.ListReceivedAsync(
            firstPage.Single().AcceptanceSequence,
            maxCount: 1,
            cancellationToken);
        firstPage.Concat(secondPage).Select(record => record.InstanceId)
            .Should().BeEquivalentTo([firstInstanceId, secondInstanceId],
                "the fanout parent is ownership-only and each target has its own cursor position");

        var firstMatch = await fixture.InboxStore.GetMatchSnapshotAsync(
            new InboxMatchRequest(firstInstanceId, definitionId, eventName, eventVersion, correlationId),
            cancellationToken);
        var applied = await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(firstInstanceId),
                ExpectedVersion = StreamVersion.Empty,
                InboxRouteMutations = firstMatch.RouteRevisions
                    .Select(revision => new InboxRouteMutation(revision.Route, revision.Revision))
                    .ToArray(),
                InboxOperations =
                [
                    new InboxWrite(eventId, InboxRecordState.Applied)
                    {
                        ExpectedState = InboxRecordState.Received,
                        TargetInstanceId = firstInstanceId
                    }
                ]
            },
            cancellationToken);
        applied.IsSuccess.Should().BeTrue();
        (await fixture.InboxStore.GetAsync(firstInstanceId, eventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Applied);
        (await fixture.InboxStore.GetAsync(secondInstanceId, eventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Received);
        await fixture.InboxStore.MarkPoisonedAsync(
            new InboxRecordIdentity(eventId, firstInstanceId),
            InboxRecordState.Received,
            "stale-fanout-observation",
            "A stale target observation must not poison the global ownership root.",
            cancellationToken);
        (await fixture.InboxStore.GetByEventIdAsync(eventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Received);
        (await fixture.InboxStore.GetAsync(firstInstanceId, eventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Applied);
        await fixture.InboxStore.RecordHandoffFailureAsync(
            new InboxRecordIdentity(eventId, secondInstanceId),
            InboxRecordState.Received,
            expectedFailureCount: 0,
            maxFailureCount: 2,
            retryNotBefore: DateTimeOffset.UnixEpoch.AddMinutes(1),
            code: "fanout-handoff-failed",
            detail: "certification retry",
            cancellationToken);
        var retryableSecond = await fixture.InboxStore.GetAsync(secondInstanceId, eventId, cancellationToken);
        retryableSecond.Value.HandoffFailureCount.Should().Be(1);
        retryableSecond.Value.State.Should().Be(InboxRecordState.Received);
        (await fixture.InboxStore.GetAsync(firstInstanceId, eventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Applied);
        (await fixture.InboxStore.ListHandoffRetriesAsync(
                DateTimeOffset.UnixEpoch.AddMinutes(1),
                maxCount: 10,
                cancellationToken))
            .Should().Contain(record =>
                record.EventId.Equals(eventId) && record.InstanceId!.Equals(secondInstanceId));

        await fixture.ProjectionStore.ApplyAsync(
            [Projection(laterInstanceId, definitionId, new DefinitionVersion(3), WorkflowInstanceStatus.Running)],
            cancellationToken);
        var excludedTargetIdentity = new InboxRecordIdentity(eventId, laterInstanceId);
        await fixture.InboxStore.RecordHandoffFailureAsync(
            excludedTargetIdentity,
            InboxRecordState.Received,
            expectedFailureCount: 0,
            maxFailureCount: 2,
            retryNotBefore: DateTimeOffset.UnixEpoch.AddMinutes(1),
            code: "fanout-handoff-failed",
            detail: "certification retry",
            cancellationToken);
        var rootAfterExcludedTargetFailure = await fixture.InboxStore.GetByEventIdAsync(
            eventId,
            cancellationToken);
        rootAfterExcludedTargetFailure.Value.State.Should().Be(InboxRecordState.Received);
        rootAfterExcludedTargetFailure.Value.HandoffFailureCount.Should().Be(0,
            "a non-member target identity must not update the global ownership root");
        await fixture.InboxStore.MarkPoisonedAsync(
            excludedTargetIdentity,
            InboxRecordState.Received,
            "stale-fanout-observation",
            "A stale target observation must not poison the global ownership root.",
            cancellationToken);
        (await fixture.InboxStore.GetByEventIdAsync(eventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Received);
        (await fixture.InboxStore.GetAsync(laterInstanceId, eventId, cancellationToken))
            .HasValue.Should().BeFalse();
        var excludedTargetTransition = async () => await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(laterInstanceId),
                ExpectedVersion = StreamVersion.Empty,
                InboxOperations =
                [
                    new InboxWrite(eventId, InboxRecordState.Applied)
                    {
                        ExpectedState = InboxRecordState.Received,
                        TargetInstanceId = laterInstanceId
                    }
                ]
            },
            cancellationToken);
        await excludedTargetTransition.Should().ThrowAsync<InvalidOperationException>(
            "an instance outside the committed snapshot must never transition the global envelope root");
        (await fixture.InboxStore.GetByEventIdAsync(eventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Received);
        (await fixture.InboxStore.GetAsync(laterInstanceId, eventId, cancellationToken))
            .HasValue.Should().BeFalse();

        var duplicate = await fixture.InboxStore.AcceptDefinitionFanoutAsync(acceptance, cancellationToken);
        duplicate.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Duplicate);
        duplicate.DefinitionFanoutTargets.Should().BeEquivalentTo([firstInstanceId, secondInstanceId]);
        (await fixture.InboxStore.ListDefinitionFanoutTargetsAsync(eventId, cancellationToken))
            .Select(target => target.InstanceId)
            .Should().BeEquivalentTo([firstInstanceId, secondInstanceId]);

        var conflict = await fixture.InboxStore.AcceptDefinitionFanoutAsync(
            acceptance with
            {
                Acceptance = acceptance.Acceptance with { EnvelopeFingerprint = "changed-envelope" }
            },
            cancellationToken);
        conflict.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Conflict);

        var rejectedEventId = EventId.Create(Guid.CreateVersion7().ToString());
        var rejectedEnvelope = Envelope(rejectedEventId, definitionId, eventName, eventVersion, correlationId);
        var rejected = await fixture.InboxStore.AcceptDefinitionFanoutAsync(
            new InboxDefinitionFanoutAcceptance(
                new InboxAcceptance(rejectedEnvelope, "fanout-limit", rejectedEnvelope.OccurredAt),
                definitionId,
                MaximumTargetCount: 2),
            cancellationToken);
        rejected.Disposition.Should().Be(InboxAcceptanceCommitDisposition.FanoutLimitExceeded);
        (await fixture.InboxStore.GetByEventIdAsync(rejectedEventId, cancellationToken)).HasValue.Should().BeFalse();
        (await fixture.InboxStore.ListDefinitionFanoutTargetsAsync(rejectedEventId, cancellationToken)).Should().BeEmpty();

        var emptyEventId = EventId.Create(Guid.CreateVersion7().ToString());
        var emptyEnvelope = Envelope(emptyEventId, DefinitionId.New(), eventName, eventVersion, correlationId);
        var empty = await fixture.InboxStore.AcceptDefinitionFanoutAsync(
            new InboxDefinitionFanoutAcceptance(
                new InboxAcceptance(emptyEnvelope, "empty-fanout", emptyEnvelope.OccurredAt),
                emptyEnvelope.Route.DefinitionId!,
                MaximumTargetCount: 2),
            cancellationToken);
        empty.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);
        empty.DefinitionFanoutTargets.Should().BeEmpty();
        (await fixture.InboxStore.ListDefinitionFanoutTargetsAsync(emptyEventId, cancellationToken)).Should().BeEmpty();
    }

    private static ProjectionWrite Projection(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        WorkflowInstanceStatus status) =>
        new(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot = new WorkflowProjectionSnapshot
            {
                InstanceId = instanceId,
                RootInstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion,
                Status = status,
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch
            }
        };

    private static DurableEventEnvelope Envelope(
        EventId eventId,
        DefinitionId definitionId,
        EventName eventName,
        EventContractVersion eventVersion,
        CorrelationId correlationId) =>
        new()
        {
            EventId = eventId,
            EventName = eventName.Value,
            EventContractVersion = eventVersion.Value,
            CorrelationId = correlationId,
            OccurredAt = DateTimeOffset.UnixEpoch,
            Route = new DurableEventRouteEnvelope
            {
                Kind = "definition-fanout",
                DefinitionId = definitionId
            }
        };
}
