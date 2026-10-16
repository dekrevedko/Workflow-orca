using System.Security.Cryptography;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public static class StartOrDeliverInboxCertification
{
    public static async Task RunAsync(IProviderCertificationFixture fixture)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var definitionId = DefinitionId.New();
        var definitionVersion = new DefinitionVersion(2);
        var startKey = $"pending-start-{Guid.CreateVersion7():N}";
        var input = "workflow-input"u8.ToArray();
        var inputFingerprint = Convert.ToHexString(SHA256.HashData(input));
        var firstEvent = Envelope(
            EventId.Create(Guid.CreateVersion7().ToString()),
            definitionId,
            definitionVersion,
            startKey,
            input,
            "event-payload-one"u8.ToArray());
        var firstAcceptance = Acceptance(firstEvent, "start-event-one", inputFingerprint);

        var accepted = await fixture.InboxStore.AcceptStartOrDeliverAsync(firstAcceptance, cancellationToken);
        var pending = await fixture.InboxStore.GetStartIntentAsync(startKey, cancellationToken);

        accepted.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);
        accepted.Record!.InstanceId.Should().BeNull();
        accepted.Record.Envelope!.Payload.Should().Equal("event-payload-one"u8.ToArray());
        pending.Value.State.Should().Be(InboxStartIntentState.Pending);
        pending.Value.WorkflowInputPayload.Should().Equal(input);
        pending.Value.WorkflowInputPayload.Should().NotEqual(accepted.Record.Envelope.Payload,
            "workflow input and event payload are independent fixed-codec values");

        var duplicate = await fixture.InboxStore.AcceptStartOrDeliverAsync(firstAcceptance, cancellationToken);
        duplicate.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Duplicate);

        var secondEvent = Envelope(
            EventId.Create(Guid.CreateVersion7().ToString()),
            definitionId,
            definitionVersion,
            startKey,
            input,
            "event-payload-two"u8.ToArray());
        var secondAcceptance = Acceptance(secondEvent, "start-event-two", inputFingerprint);
        (await fixture.InboxStore.AcceptStartOrDeliverAsync(secondAcceptance, cancellationToken))
            .Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted,
                "compatible events reuse one pending start intent without losing either envelope");

        var conflictingEvent = Envelope(
            EventId.Create(Guid.CreateVersion7().ToString()),
            definitionId,
            definitionVersion,
            startKey,
            "different-input"u8.ToArray(),
            "conflicting-event"u8.ToArray());
        var rejected = await fixture.InboxStore.AcceptStartOrDeliverAsync(
            Acceptance(
                conflictingEvent,
                "start-conflict",
                Convert.ToHexString(SHA256.HashData("different-input"u8))),
            cancellationToken);
        rejected.Disposition.Should().Be(InboxAcceptanceCommitDisposition.StartConflict);
        rejected.StartConflict.Should().NotBeNull();
        (await fixture.InboxStore.GetByEventIdAsync(conflictingEvent.EventId, cancellationToken))
            .HasValue.Should().BeFalse("a known incompatible binding rejects before event ownership");

        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var materialized = await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                StartIdempotencyOperations =
                [
                    new StartIdempotencyWrite(
                        startKey,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        "definition-fingerprint",
                        inputFingerprint)
                ],
                OutboxRecords =
                [
                    new OutboxWrite(OutboxRecordId.New(), "continue", "continuation"u8.ToArray())
                ],
                ProjectionOperations =
                [
                    Projection(instanceId, definitionId, definitionVersion)
                ]
            },
            cancellationToken);
        materialized.IsSuccess.Should().BeTrue();

        var materializedIntent = await fixture.InboxStore.GetStartIntentAsync(startKey, cancellationToken);
        materializedIntent.Value.State.Should().Be(InboxStartIntentState.Materialized);
        materializedIntent.Value.InstanceId.Should().Be(instanceId);
        materializedIntent.Value.DefinitionFingerprint.Should().Be("definition-fingerprint");
        foreach (var eventId in new[] { firstEvent.EventId, secondEvent.EventId })
        {
            var target = await fixture.InboxStore.GetAsync(instanceId, eventId, cancellationToken);
            target.HasValue.Should().BeTrue();
            target.Value.State.Should().Be(InboxRecordState.Received);
            target.Value.Route!.Kind.Should().Be("direct");
        }

        var postStartEvent = Envelope(
            EventId.Create(Guid.CreateVersion7().ToString()),
            definitionId,
            definitionVersion,
            startKey,
            input,
            "event-payload-after-start"u8.ToArray());
        var postStartAcceptance = await fixture.InboxStore.AcceptStartOrDeliverAsync(
            Acceptance(postStartEvent, "start-event-after-start", inputFingerprint),
            cancellationToken);
        postStartAcceptance.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);
        postStartAcceptance.Record!.InstanceId.Should().Be(instanceId);
        (await fixture.InboxStore.GetAsync(instanceId, postStartEvent.EventId, cancellationToken))
            .Value.Route!.Kind.Should().Be(InboxRouteKinds.Direct,
                "a compatible previously materialized binding attaches in the acceptance transaction");

        await fixture.InboxStore.MarkPoisonedAsync(
            new InboxRecordIdentity(secondEvent.EventId, instanceId),
            InboxRecordState.Received,
            "certified-direct-fallback",
            "The matching direct identity must still reach the materialized record.",
            cancellationToken);
        (await fixture.InboxStore.GetAsync(instanceId, secondEvent.EventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Poisoned);
        (await fixture.InboxStore.GetByEventIdAsync(firstEvent.EventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Received);

        var poisonKey = $"poison-start-{Guid.CreateVersion7():N}";
        var poisonInput = "poison-input"u8.ToArray();
        var poisonEvent = Envelope(
            EventId.Create(Guid.CreateVersion7().ToString()),
            DefinitionId.New(),
            DefinitionVersion.Initial,
            poisonKey,
            poisonInput,
            "poison-event"u8.ToArray());
        await fixture.InboxStore.AcceptStartOrDeliverAsync(
            Acceptance(
                poisonEvent,
                "poison-envelope",
                Convert.ToHexString(SHA256.HashData(poisonInput))),
            cancellationToken);
        await fixture.InboxStore.MarkPoisonedAsync(
            poisonEvent.EventId,
            InboxRecordState.Received,
            "definition-unavailable",
            "certification poison",
            cancellationToken);
        (await fixture.InboxStore.GetStartIntentAsync(poisonKey, cancellationToken))
            .Value.State.Should().Be(InboxStartIntentState.Poisoned);
        (await fixture.InboxStore.GetByEventIdAsync(poisonEvent.EventId, cancellationToken))
            .Value.State.Should().Be(InboxRecordState.Poisoned);

        await CertifyConcurrentReservationAsync(fixture, cancellationToken);
    }

    private static async Task CertifyConcurrentReservationAsync(
        IProviderCertificationFixture fixture,
        CancellationToken cancellationToken)
    {
        var definitionId = DefinitionId.New();
        var definitionVersion = DefinitionVersion.Initial;
        var compatibleKey = $"concurrent-compatible-start-{Guid.CreateVersion7():N}";
        var compatibleInput = "concurrent-compatible-input"u8.ToArray();
        var compatibleFingerprint = Convert.ToHexString(SHA256.HashData(compatibleInput));
        var compatibleEvents = new[]
        {
            Envelope(
                EventId.Create(Guid.CreateVersion7().ToString()),
                definitionId,
                definitionVersion,
                compatibleKey,
                compatibleInput,
                "compatible-event-one"u8.ToArray()),
            Envelope(
                EventId.Create(Guid.CreateVersion7().ToString()),
                definitionId,
                definitionVersion,
                compatibleKey,
                compatibleInput,
                "compatible-event-two"u8.ToArray())
        };
        var compatibleResults = await Task.WhenAll(compatibleEvents.Select((envelope, index) =>
            fixture.InboxStore.AcceptStartOrDeliverAsync(
                Acceptance(envelope, $"compatible-envelope-{index}", compatibleFingerprint),
                cancellationToken)));

        compatibleResults.Should().OnlyContain(result =>
            result.Disposition == InboxAcceptanceCommitDisposition.Accepted,
            "concurrent compatible events must reuse one atomically reserved start intent");
        (await fixture.InboxStore.GetStartIntentAsync(compatibleKey, cancellationToken))
            .Value.State.Should().Be(InboxStartIntentState.Pending);
        foreach (var envelope in compatibleEvents)
        {
            (await fixture.InboxStore.GetByEventIdAsync(envelope.EventId, cancellationToken))
                .HasValue.Should().BeTrue();
        }

        var incompatibleKey = $"concurrent-incompatible-start-{Guid.CreateVersion7():N}";
        var firstInput = "concurrent-first-input"u8.ToArray();
        var secondInput = "concurrent-second-input"u8.ToArray();
        var incompatibleEvents = new[]
        {
            Envelope(
                EventId.Create(Guid.CreateVersion7().ToString()),
                definitionId,
                definitionVersion,
                incompatibleKey,
                firstInput,
                "incompatible-event-one"u8.ToArray()),
            Envelope(
                EventId.Create(Guid.CreateVersion7().ToString()),
                definitionId,
                definitionVersion,
                incompatibleKey,
                secondInput,
                "incompatible-event-two"u8.ToArray())
        };
        var incompatibleResults = await Task.WhenAll(
            fixture.InboxStore.AcceptStartOrDeliverAsync(
                Acceptance(
                    incompatibleEvents[0],
                    "incompatible-envelope-one",
                    Convert.ToHexString(SHA256.HashData(firstInput))),
                cancellationToken),
            fixture.InboxStore.AcceptStartOrDeliverAsync(
                Acceptance(
                    incompatibleEvents[1],
                    "incompatible-envelope-two",
                    Convert.ToHexString(SHA256.HashData(secondInput))),
                cancellationToken));

        incompatibleResults.Count(result =>
                result.Disposition == InboxAcceptanceCommitDisposition.Accepted)
            .Should().Be(1);
        incompatibleResults.Count(result =>
                result.Disposition == InboxAcceptanceCommitDisposition.StartConflict)
            .Should().Be(1);
        var ownedCount = 0;
        foreach (var envelope in incompatibleEvents)
        {
            if ((await fixture.InboxStore.GetByEventIdAsync(envelope.EventId, cancellationToken)).HasValue)
            {
                ownedCount++;
            }
        }

        ownedCount.Should().Be(1,
            "the incompatible concurrent loser must reject before taking event ownership");
    }

    private static InboxStartOrDeliverAcceptance Acceptance(
        DurableEventEnvelope envelope,
        string envelopeFingerprint,
        string inputFingerprint) =>
        new(
            new InboxAcceptance(envelope, envelopeFingerprint, envelope.OccurredAt),
            envelope.Route.DefinitionId!,
            envelope.Route.DefinitionVersion!,
            envelope.Route.StartIdempotencyKey!,
            envelope.Route.WorkflowInputContentType!,
            [.. envelope.Route.WorkflowInputPayload!],
            inputFingerprint);

    private static DurableEventEnvelope Envelope(
        EventId eventId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        string startKey,
        byte[] workflowInput,
        byte[] eventPayload) =>
        new()
        {
            EventId = eventId,
            EventName = "start-or-deliver-certification",
            EventContractVersion = 2,
            CorrelationId = CorrelationId.Create("start-or-deliver-certification"),
            OccurredAt = DateTimeOffset.UnixEpoch,
            PayloadContentType = "application/vnd.orcacore.fixed+json;v=1",
            Payload = eventPayload,
            Route = new DurableEventRouteEnvelope
            {
                Kind = "start-or-deliver",
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion,
                StartIdempotencyKey = startKey,
                WorkflowInputContentType = "application/vnd.orcacore.fixed+json;v=1",
                WorkflowInputPayload = workflowInput
            }
        };

    private static ProjectionWrite Projection(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion) =>
        new(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot = new WorkflowProjectionSnapshot
            {
                InstanceId = instanceId,
                RootInstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion,
                Status = WorkflowInstanceStatus.Running,
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch
            }
        };
}
