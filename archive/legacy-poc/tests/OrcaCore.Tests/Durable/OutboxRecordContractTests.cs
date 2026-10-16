namespace OrcaCore.Tests.Durable;

public sealed class OutboxRecordContractTests
{
    [Fact]
    public void Deterministic_outbox_id_uses_instance_stream_version_and_sequence()
    {
        Assert.Equal("inst-1:4:2", OutboxRecord.CreateDeterministicId("inst-1", 4, 2));
    }

    [Fact]
    public void Sequence_order_is_stream_version_then_sequence()
    {
        var records = new[]
        {
            CreateRecord("inst-1:2:0", 2, 0),
            CreateRecord("inst-1:1:1", 1, 1),
            CreateRecord("inst-1:1:0", 1, 0)
        };

        var ordered = records
            .OrderBy(record => record.StreamVersion)
            .ThenBy(record => record.Sequence)
            .Select(record => record.OutboxId)
            .ToArray();

        Assert.Equal(["inst-1:1:0", "inst-1:1:1", "inst-1:2:0"], ordered);
    }

    [Fact]
    public void Idempotency_key_defaults_to_outbox_id_in_canonical_constructor()
    {
        var record = CreateRecord("inst-1:3:7", 3, 7);

        Assert.Equal(record.OutboxId, record.IdempotencyKey);
    }

    [Fact]
    public void Status_enum_has_exactly_four_states_and_no_failed_state()
    {
        var values = Enum.GetValues<OutboxStatus>();

        Assert.Equal(4, values.Length);
        Assert.Contains(OutboxStatus.Pending, values);
        Assert.Contains(OutboxStatus.Leased, values);
        Assert.Contains(OutboxStatus.Dispatched, values);
        Assert.Contains(OutboxStatus.Poisoned, values);
        Assert.DoesNotContain("Failed", values.Select(v => v.ToString()));
    }

    [Fact]
    public void Fresh_record_has_zero_attempt_count_and_null_lease_and_next_attempt()
    {
        var record = CreateRecord("inst-1:0:0", 0, 0);

        Assert.Equal(OutboxStatus.Pending, record.Status);
        Assert.Equal(0, record.AttemptCount);
        Assert.Null(record.LeaseOwner);
        Assert.Null(record.LeaseExpiresAt);
        Assert.Null(record.NextAttemptAt);
        Assert.Null(record.LastAttemptAt);
        Assert.Null(record.LastError);
    }

    private static OutboxRecord CreateRecord(string outboxId, int streamVersion, int sequence)
    {
        var payloadEnvelope = new JsonPayloadEnvelopeSerializer(DurablePayloadTypeRegistry.Default)
            .Serialize("payload", typeof(string))
            .Value!;

        return new OutboxRecord(
            outboxId,
            outboxId,
            "inst-1",
            ParentInstanceId: null,
            RootInstanceId: "inst-1",
            GroupId: null,
            StreamId: "stream-1",
            StreamVersion: streamVersion,
            Sequence: sequence,
            MessageType: "WorkflowWaiting",
            Channel: "workflow-status",
            Destination: "WorkflowWaiting",
            PayloadEnvelope: payloadEnvelope,
            CorrelationId: null,
            CausationEventId: null,
            ResumeTokenId: null,
            Status: OutboxStatus.Pending,
            AttemptCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            LastAttemptAt: null,
            NextAttemptAt: null,
            LeaseOwner: null,
            LeaseExpiresAt: null,
            LastError: null);
    }
}
