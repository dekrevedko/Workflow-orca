using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ContinueAsNewAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-313")]
    public async Task ContinueAsNew_DurableInstance_RemainsQueryableByOriginalIdentity()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var instanceId = InstanceIdValue(1);

        await processor.ProcessAsync(Start(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ContinueAsNew(instanceId), TestContext.Current.CancellationToken);

        var snapshot = await new DurableManagement(store)
            .Instance(instanceId)
            .GetAsync(TestContext.Current.CancellationToken);

        snapshot.InstanceId.Should().Be(instanceId);
        snapshot.RootInstanceId.Should().Be(instanceId);
        snapshot.Status.Should().Be(WorkflowStatus.Running);
        snapshot.ContinueAsNewGeneration.Should().Be(1);
    }

    private static StartWorkflowCommand Start(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = new DefinitionVersion(7)
        };
    }

    private static ContinueAsNewCommand ContinueAsNew(InstanceId instanceId)
    {
        return new ContinueAsNewCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = instanceId,
            RequestedAt = Timestamp(2),
            StateContentType = "application/json",
            StatePayload = [1]
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
