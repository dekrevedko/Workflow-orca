using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ExternalJobAcceptanceTests
{
    [Fact]
    [Trait("AC", "JS-AC-004")]
    public async Task ExternalJobCompletion_RedeliveryResumesExactlyOnce()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(StartCommand(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RunExternalJob(1, "job-1"), TestContext.Current.CancellationToken);
        var eventId = EventIdValue(50);

        var first = await processor.ProcessAsync(
            CompleteExternalJob(1, "job-1", eventId, 3),
            TestContext.Current.CancellationToken);
        var duplicate = await processor.ProcessAsync(
            CompleteExternalJob(1, "job-1", eventId, 4),
            TestContext.Current.CancellationToken);

        first.Outcome.Should().Be(DurableCommandOutcome.Committed);
        duplicate.Outcome.Should().Be(DurableCommandOutcome.NoOp);
    }

    [Fact]
    [Trait("AC", "JS-AC-013")]
    public async Task ExternalJobQueuedForTickets_DoesNotDispatchStart()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                InstanceIdValue(99),
                "other",
                [new ResourcePoolRequirement("db", 1)],
                Timestamp(1),
                Timestamp(30)),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(StartCommand(1), TestContext.Current.CancellationToken);
        await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        await processor.ProcessAsync(RunExternalJob(1, "job-1"), TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        outbox.Should().NotContain(record => record.Kind == "external-job-start");
    }

    private static StartWorkflowCommand StartCommand(int instance)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static RunExternalJobCommand RunExternalJob(int instance, string externalJobId)
    {
        return new RunExternalJobCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(2),
            ExternalJobId = externalJobId,
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { externalJobId }),
            Requirements = [new ResourcePoolRequirement("db", 1)],
            TimeoutAt = Timestamp(30)
        };
    }

    private static CompleteExternalJobCommand CompleteExternalJob(
        int instance,
        string externalJobId,
        EventId eventId,
        int commandValue)
    {
        return new CompleteExternalJobCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(commandValue),
            ExternalJobId = externalJobId,
            CompletionEventId = eventId
        };
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 20, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }
}
