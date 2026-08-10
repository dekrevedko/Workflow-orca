using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests;

public sealed class DurableOperationalProjectionTests
{
    [Fact]
    public void CommittedProgress_ProjectsTheLastActivityTimestamp()
    {
        var occurredAt = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        var instanceId = InstanceId.Parse("00000000-0000-0000-0000-000000000001");
        var aggregate = DurableWorkflowAggregate.Empty(instanceId);
        var decision = aggregate.DecideStart(new StartWorkflowCommand
        {
            CommandId = new CommandId(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            InstanceId = instanceId,
            RequestedAt = occurredAt,
            DefinitionId = DefinitionId.Parse("00000000-0000-0000-0000-000000000003"),
            DefinitionVersion = DefinitionVersion.Initial
        });

        var projection = aggregate.CreateProjectionWrites(decision.Events)
            .Should().ContainSingle().Which.InstanceSnapshot;

        projection.Should().NotBeNull();
        projection!.LastActiveAt.Should().Be(occurredAt,
            "durable stuck detection must be driven by engine-authored progress, not seeded provider flags");
    }
}
