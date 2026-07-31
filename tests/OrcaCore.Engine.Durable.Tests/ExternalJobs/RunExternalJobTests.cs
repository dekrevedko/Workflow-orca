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

public sealed class RunExternalJobTests
{
    [Fact]
    [Trait("AC", "JS-AC-010")]
    [Trait("AC", "JS-AC-012")]
    public async Task StartExternalJob_WhenTicketsGranted_DispatchesStartCommandAndRegistersColdWait()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db-a", 1), TestContext.Current.CancellationToken);
        await pools.UpsertPoolAsync(Pool("db-b", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await provider.ClaimAsync(10, TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            RunExternalJob(1, "job-1", Requirement("db-a"), Requirement("db-b")),
            TestContext.Current.CancellationToken);
        var outbox = await provider.ClaimAsync(10, TestContext.Current.CancellationToken);
        var snapshot = (await provider.ListAsync(
            new WorkflowProjectionQuery { InstanceId = InstanceIdValue(1) },
            TestContext.Current.CancellationToken)).Should().ContainSingle().Subject;
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db-a", TestContext.Current.CancellationToken);

        outbox.Should().ContainSingle(record => record.Kind == "external-job-start");
        outbox.Where(record => record.Kind == "external-job-start").Should().ContainSingle()
            .Which.Payload.Should().NotBeEmpty();
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle(wait =>
            wait.EventName == "ExternalJobCompleted" &&
            wait.CorrelationId.Equals(CorrelationId.Create("job-1")) &&
            wait.Mode == WaitMode.Cold.ToString());
        snapshot.ActiveWaits.Single().FiberId.Should().Be(new FiberId("job-fiber"));
        snapshot.ActiveWaits.Single().ScopeId.Should().Be(new ScopeId("job-scope"));
        events.OfType<WorkflowExternalJobStartedEvent>().Single().FiberId
            .Should().Be(new FiberId("job-fiber"));
        events.OfType<WorkflowTimerScheduledEvent>().Single().ScopeId
            .Should().Be(new ScopeId("job-scope"));
        pool.Value.HeldTickets.Single().FiberId.Should().Be(new FiberId("job-fiber"));
    }

    [Fact]
    [Trait("AC", "JS-AC-004")]
    public async Task CompleteExternalJob_WhenCompletionRedelivered_ResumesExactlyOnce()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RunExternalJob(1, "job-1", Requirement("db")), TestContext.Current.CancellationToken);
        var completionEventId = EventIdValue(90);

        var first = await processor.ProcessAsync(
            CompleteExternalJob(1, "job-1", completionEventId, commandValue: 3),
            TestContext.Current.CancellationToken);
        var duplicate = await processor.ProcessAsync(
            CompleteExternalJob(1, "job-1", completionEventId, commandValue: 4),
            TestContext.Current.CancellationToken);
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        first.Outcome.Should().Be(DurableCommandOutcome.Committed);
        duplicate.Outcome.Should().Be(DurableCommandOutcome.NoOp);
        events.OfType<WorkflowExternalJobCompletedEvent>().Should().ContainSingle();
        events.OfType<WorkflowExternalJobCompletedEvent>().Single().FiberId
            .Should().Be(new FiberId("job-fiber"));
        var matched = events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle().Which;
        matched.MatchedEventId.Should().Be(completionEventId);
        matched.FiberId.Should().Be(new FiberId("job-fiber"));
    }

    [Fact]
    [Trait("Scenario", "NEG-JS-005")]
    [Trait("AC", "JS-AC-004")]
    public async Task NEG_JS_005_CompleteExternalJob_WrongCorrelationDoesNotResumeWaitingJob()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RunExternalJob(1, "job-1", Requirement("db")), TestContext.Current.CancellationToken);
        var wrongCompletionEventId = EventIdValue(91);

        var result = await processor.ProcessAsync(
            CompleteExternalJob(1, "job-2", wrongCompletionEventId, commandValue: 3),
            TestContext.Current.CancellationToken);
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var snapshot = (await provider.ListAsync(
            new WorkflowProjectionQuery { InstanceId = InstanceIdValue(1) },
            TestContext.Current.CancellationToken)).Should().ContainSingle().Subject;
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        var inbox = await provider.GetAsync(
            InstanceIdValue(1),
            wrongCompletionEventId,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Poisoned);
        inbox.Value.State.Should().Be(InboxRecordState.Poisoned);
        events.OfType<WorkflowExternalJobCompletedEvent>().Should().BeEmpty();
        events.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty();
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle(wait =>
            wait.EventName == "ExternalJobCompleted" &&
            wait.CorrelationId.Equals(CorrelationId.Create("job-1")));
        pool.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderKey.Should().Be("job-1");
    }

    [Fact]
    [Trait("AC", "JS-AC-006")]
    [Trait("AC", "JS-AC-011")]
    public async Task TimeoutExternalJob_WhenTimerWins_DispatchesStopCommandAndReleasesTickets()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RunExternalJob(1, "job-1", Requirement("db")), TestContext.Current.CancellationToken);
        await provider.ClaimAsync(10, TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            new TimeoutExternalJobCommand
            {
                CommandId = CommandIdValue(3),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(3),
                ExternalJobId = "job-1"
            },
            TestContext.Current.CancellationToken);
        var outbox = await provider.ClaimAsync(10, TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        outbox.Should().ContainSingle(record => record.Kind == "external-job-stop");
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "JS-AC-013")]
    public async Task StartExternalJob_WhenTicketQueued_DoesNotDispatchStartCommand()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(InstanceIdValue(99), "other", [Requirement("db")], Timestamp(1), Timestamp(30)),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await provider.ClaimAsync(10, TestContext.Current.CancellationToken);

        await processor.ProcessAsync(RunExternalJob(1, "job-1", Requirement("db")), TestContext.Current.CancellationToken);
        var outbox = await provider.ClaimAsync(10, TestContext.Current.CancellationToken);

        outbox.Should().NotContain(record => record.Kind == "external-job-start");
    }

    private static StartWorkflowCommand Start(int instance)
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

    private static RunExternalJobCommand RunExternalJob(
        int instance,
        string externalJobId,
        params ResourcePoolRequirement[] requirements)
    {
        return new RunExternalJobCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(2),
            ExternalJobId = externalJobId,
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { externalJobId }),
            Requirements = requirements,
            TimeoutAt = Timestamp(30),
            FiberId = new FiberId("job-fiber"),
            ScopeId = new ScopeId("job-scope")
        };
    }

    private static CompleteExternalJobCommand CompleteExternalJob(
        int instance,
        string externalJobId,
        EventId completionEventId,
        int commandValue)
    {
        return new CompleteExternalJobCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(commandValue),
            ExternalJobId = externalJobId,
            CompletionEventId = completionEventId
        };
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static ResourcePoolRequirement Requirement(string poolName)
    {
        return new ResourcePoolRequirement(poolName, 1);
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 19, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static EventId EventIdValue(int value)
    {
        return EventId.Create($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
