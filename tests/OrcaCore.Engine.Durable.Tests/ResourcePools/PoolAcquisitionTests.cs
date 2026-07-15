using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.ResourcePools;

public sealed class PoolAcquisitionTests
{
    [Fact]
    public async Task AcquirePool_WhenCapacityAvailable_RecordsHeldTicketsBeforeGuardedWorkStarts()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);

        var result = await processor.ProcessAsync(
            Acquire(1, "node-1", Requirement("db")),
            TestContext.Current.CancellationToken);
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        var acquired = events.OfType<WorkflowResourcePoolAcquiredEvent>().Should().ContainSingle().Which;
        acquired.FiberId.Should().Be(new FiberId("resource-fiber"));
        acquired.ScopeId.Should().Be(new ScopeId("resource-scope"));
        var ticket = acquired.Tickets.Should().ContainSingle().Which;
        ticket.PoolName.Should().Be("db");
        ticket.FiberId.Should().Be(new FiberId("resource-fiber"));
        ticket.ScopeId.Should().Be(new ScopeId("resource-scope"));
        events.OfType<WorkflowStepCompletedEvent>().Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-519")]
    [Trait("AC", "JS-AC-013")]
    public async Task AcquirePool_WhenExhausted_RecordsColdWaitWithoutOutboxWork()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(InstanceIdValue(99), "other-node", [Requirement("db")], Timestamp(1), Timestamp(31)),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await provider.ClaimAsync(10, TestContext.Current.CancellationToken);

        await processor.ProcessAsync(Acquire(1, "node-1", Requirement("db")), TestContext.Current.CancellationToken);
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var snapshot = (await provider.ListAsync(
            new WorkflowProjectionQuery { InstanceId = InstanceIdValue(1) },
            TestContext.Current.CancellationToken)).Should().ContainSingle().Subject;
        var outbox = await provider.ClaimAsync(10, TestContext.Current.CancellationToken);

        events.OfType<WorkflowResourcePoolQueuedEvent>().Should().ContainSingle();
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle()
            .Which.Mode.Should().Be(WaitMode.Cold.ToString());
        snapshot.ActiveWaits.Single().FiberId.Should().Be(new FiberId("resource-fiber"));
        snapshot.ActiveWaits.Single().ScopeId.Should().Be(new ScopeId("resource-scope"));
        outbox.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-520")]
    public async Task CompleteGuardedScope_ReleasesTicketsExactlyOnce()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(Acquire(1, "node-1", Requirement("db")), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            new DurableCompleteCommand(CommandIdValue(3), InstanceIdValue(1), Timestamp(3), null),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableCompleteCommand(CommandIdValue(4), InstanceIdValue(1), Timestamp(4), null),
            TestContext.Current.CancellationToken);
        var snapshot = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        snapshot.Value.HeldTickets.Should().BeEmpty();
        events.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle()
            .Which.Tickets.Should().ContainSingle()
            .Which.HolderKey.Should().Be("node-1");
    }

    [Fact]
    [Trait("AC", "AC-520")]
    public async Task FailGuardedScope_ReleasesTicketsExactlyOnce()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(Acquire(1, "node-1", Requirement("db")), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            new DurableStepFailedCommand(CommandIdValue(3), InstanceIdValue(1), Timestamp(3), "guarded", "boom"),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableStepFailedCommand(CommandIdValue(4), InstanceIdValue(1), Timestamp(4), "guarded", "boom"),
            TestContext.Current.CancellationToken);
        var snapshot = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        snapshot.Value.HeldTickets.Should().BeEmpty();
        events.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle()
            .Which.Tickets.Should().ContainSingle()
            .Which.HolderKey.Should().Be("node-1");
    }

    [Fact]
    [Trait("AC", "AC-520")]
    public async Task TerminateGuardedScope_ReleasesTicketsExactlyOnce()
    {
        var provider = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(provider, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(Acquire(1, "node-1", Requirement("db")), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandIdValue(3),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(3)
            },
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandIdValue(4),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(4)
            },
            TestContext.Current.CancellationToken);
        var snapshot = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        var events = await provider.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        snapshot.Value.HeldTickets.Should().BeEmpty();
        events.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle();
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

    private static AcquireResourcePoolCommand Acquire(
        int instance,
        string holderKey,
        params ResourcePoolRequirement[] requirements)
    {
        return new AcquireResourcePoolCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(2),
            HolderKey = holderKey,
            Requirements = requirements,
            ExpiresAt = Timestamp(32),
            FiberId = new FiberId("resource-fiber"),
            ScopeId = new ScopeId("resource-scope")
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
        return new DateTimeOffset(2026, 7, 2, 17, minutes, 0, TimeSpan.Zero);
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
