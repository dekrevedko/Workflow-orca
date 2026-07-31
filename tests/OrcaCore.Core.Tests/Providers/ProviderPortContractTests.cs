using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.Core.Tests.Providers;

public sealed class ProviderPortContractTests
{
    [Fact]
    public void EventStoreAppendResult_RepresentsSuccessAndVersionConflictWithoutExceptions()
    {
        var success = Result<AppendEventsResult>.Success(
            new AppendEventsResult(new StreamVersion(3)));
        var conflict = EventStoreConflict.ExpectedVersionMismatch(
            new StreamVersion(2),
            new StreamVersion(3));

        success.IsSuccess.Should().BeTrue();
        success.Value.NewVersion.Should().Be(new StreamVersion(3));
        conflict.IsFailure.Should().BeTrue();
        conflict.Error.Message.Should().Contain("expected version");
    }

    [Fact]
    public void CommitBatch_CarriesEventsCheckpointInboxOutboxAndProjectionOperations()
    {
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var batch = new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = StreamVersion.Empty,
            Events =
            [
                new WorkflowStartedEvent
                {
                    EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                    InstanceId = instanceId,
                    CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    DefinitionId = DefinitionId.New(),
                    DefinitionVersion = DefinitionVersion.Initial
                }
            ],
            Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(1), "application/json", [1, 2, 3]),
            InboxOperations = [new InboxWrite(EventId.Create(Guid.CreateVersion7().ToString()), InboxRecordState.Received)],
            StartIdempotencyOperations =
            [
                new StartIdempotencyWrite(
                    "order-1",
                     instanceId,
                     DefinitionId.New(),
                     DefinitionVersion.Initial,
                     "definition-fingerprint",
                     "input-fingerprint")
            ],
            OutboxRecords = [new OutboxWrite(OutboxRecordId.New(), "status", [4, 5])],
            ProjectionOperations = [new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)]
        };

        batch.Events.Should().ContainSingle();
        batch.Checkpoint.Should().NotBeNull();
        batch.InboxOperations.Should().ContainSingle();
        batch.StartIdempotencyOperations.Should().ContainSingle();
        batch.OutboxRecords.Should().ContainSingle();
        batch.ProjectionOperations.Should().ContainSingle();
    }

    [Fact]
    public void ProjectionCommitDecision_MatchesRecordedIoq3Resolution()
    {
        ProviderCommitPolicy.ProjectionMode.Should().Be(ProjectionCommitMode.SameCommitBoundary);
    }

    [Fact]
    public void ProviderPorts_DoNotExposeProviderSpecificTypes()
    {
        var providerTypes = typeof(IWorkflowEventStore).Assembly.GetTypes()
            .Where(type => type.Namespace == "OrcaCore.Abstractions.Providers")
            .ToArray();

        providerTypes.SelectMany(type => type.GetMembers())
            .Select(member => member.ToString())
            .Should().NotContain(member =>
                member.Contains("Npgsql", StringComparison.Ordinal) ||
                member.Contains("PostgreSql", StringComparison.Ordinal) ||
                member.Contains("InMemory", StringComparison.Ordinal));
    }

    [Fact]
    public void TimerSchedulerPort_CarriesDurableWakeupCommandData()
    {
        var schedule = new TimerScheduleRequest
        {
            TimerId = TimerId.New(),
            InstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString()),
            CommandId = CommandId.New(),
            FireAt = DateTimeOffset.UtcNow.AddMinutes(5),
            WakeupName = "timeout"
        };

        schedule.TimerId.Should().NotBe(default);
        schedule.InstanceId.Should().NotBe(default);
        schedule.CommandId.Should().NotBe(default);
        schedule.WakeupName.Should().Be("timeout");
    }
}
