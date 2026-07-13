using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class RetentionAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-314")]
    public async Task RetentionPolicy_TerminalInstance_CanBeArchivedAndNoLongerActive()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var management = new DurableManagement(store);
        var instanceId = InstanceIdValue(1);

        await processor.ProcessAsync(Start(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(Terminate(instanceId), TestContext.Current.CancellationToken);

        var result = await management.ArchiveAsync(
            new RetentionPolicy
            {
                InstanceId = instanceId,
                RequestedAt = Timestamp(3),
                Reason = "retention window elapsed"
            },
            TestContext.Current.CancellationToken);
        var snapshot = await management.Instance(instanceId).GetAsync(TestContext.Current.CancellationToken);

        result.Archived.Should().BeTrue();
        snapshot.InstanceId.Should().Be(instanceId);
        snapshot.Status.Should().Be(WorkflowStatus.Terminated);
        snapshot.ArchivedAt.Should().Be(Timestamp(3));
    }

    private static StartWorkflowCommand Start(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static TerminateWorkflowCommand Terminate(InstanceId instanceId)
    {
        return new TerminateWorkflowCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = instanceId,
            RequestedAt = Timestamp(2)
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
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
