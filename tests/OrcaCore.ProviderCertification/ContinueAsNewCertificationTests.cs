using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class ContinueAsNewCertificationTests : EventStoreCertificationTests
{
    [Fact]
    [Trait("AC", "AC-313")]
    public async Task ContinueAsNewProjection_PreservesLogicalIdentityAcrossRollover()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(1);
        var definitionId = DefinitionIdValue(1);

        var result = await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    Started(instanceId, definitionId),
                    ContinuedAsNew(instanceId)
                ],
                ProjectionOperations =
                [
                    new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = new WorkflowInstanceSnapshot
                        {
                            InstanceId = instanceId,
                            RootInstanceId = instanceId,
                            DefinitionId = definitionId,
                            DefinitionVersion = new DefinitionVersion(7),
                            Status = WorkflowStatus.Running,
                            CreatedAt = Timestamp(1),
                            UpdatedAt = Timestamp(2),
                            ContinueAsNewGeneration = 1
                        }
                    }
                ],
                Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(2), "application/json", [1])
                {
                    DefinitionId = definitionId,
                    RootInstanceId = instanceId,
                    DefinitionVersion = new DefinitionVersion(7),
                    Status = WorkflowStatus.Running,
                    ContinueAsNewGeneration = 1
                }
            },
            TestContext.Current.CancellationToken);

        var snapshots = await fixture.ProjectionStore.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);
        var checkpoint = await fixture.EventStore.LoadCheckpointAsync(instanceId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        snapshots.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new
                {
                    InstanceId = instanceId,
                    RootInstanceId = instanceId,
                    ContinueAsNewGeneration = 1
                });
        checkpoint.Value.ContinueAsNewGeneration.Should().Be(1);
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId, DefinitionId definitionId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            RootInstanceId = instanceId,
            DefinitionId = definitionId,
            DefinitionVersion = new DefinitionVersion(7)
        };
    }

    private static WorkflowContinuedAsNewEvent ContinuedAsNew(InstanceId instanceId)
    {
        return new WorkflowContinuedAsNewEvent
        {
            EventId = EventIdValue(2),
            InstanceId = instanceId,
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            RootInstanceId = instanceId,
            PreviousStreamVersion = new StreamVersion(1),
            Generation = 1
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

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
