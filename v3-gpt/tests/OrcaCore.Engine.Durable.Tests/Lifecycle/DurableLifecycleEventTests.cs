using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Lifecycle;

public sealed class DurableLifecycleEventTests
{
    [Fact]
    public async Task DurableTerminalTransition_CommitsLifecycleOutboxRecordWithState()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            new DurableCompleteCommand(CommandIdValue(2), instanceId, Timestamp(2), "Done"),
            TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        var lifecyclePayload = JsonSerializer.Deserialize<LifecycleEventSnapshot>(
            outbox.Single(record => record.Kind == "lifecycle-event").Payload);

        events.OfType<WorkflowTerminalEvent>().Single().Status.Should().Be(WorkflowStatus.Completed);
        lifecyclePayload.Should().BeEquivalentTo(new
        {
            InstanceId = instanceId,
            EventName = "InstanceCompleted",
            Status = WorkflowStatus.Completed,
            Durable = true
        });
    }

    private static StartWorkflowCommand StartCommand(InstanceId instanceId)
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
