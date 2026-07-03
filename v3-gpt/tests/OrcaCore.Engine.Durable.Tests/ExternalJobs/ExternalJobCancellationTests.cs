using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.ExternalJobs;

public sealed class ExternalJobCancellationTests
{
    [Fact]
    [Trait("AC", "JS-AC-009")]
    [Trait("AC", "AC-520")]
    public async Task CancelRun_WithMultipleInFlightJobs_RecordsStopCommandsBeforeCancelled()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 2), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RunExternalJob("job-a", 2), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RunExternalJob("job-b", 3), TestContext.Current.CancellationToken);
        await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        var result = await processor.ProcessAsync(
            new CancelWorkflowCommand
            {
                CommandId = CommandIdValue(4),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(4)
            },
            TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        var events = (await store.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken)).ToList();
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        outbox.Where(record => record.Kind == "external-job-stop").Should().HaveCount(2);
        events.OfType<WorkflowExternalJobStopRequestedEvent>()
            .Select(stop => stop.ExternalJobId)
            .Should().BeEquivalentTo(["job-a", "job-b"]);
        events.FindLastIndex(workflowEvent => workflowEvent is WorkflowExternalJobStopRequestedEvent)
            .Should().BeLessThan(events.FindIndex(workflowEvent =>
                workflowEvent is WorkflowTerminalEvent { Status: WorkflowStatus.Cancelled }));
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    private static StartWorkflowCommand Start()
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static RunExternalJobCommand RunExternalJob(string externalJobId, int commandValue)
    {
        return new RunExternalJobCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ExternalJobId = externalJobId,
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { externalJobId }),
            Requirements = [new ResourcePoolRequirement("db", 1)],
            TimeoutAt = Timestamp(30)
        };
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 22, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
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
