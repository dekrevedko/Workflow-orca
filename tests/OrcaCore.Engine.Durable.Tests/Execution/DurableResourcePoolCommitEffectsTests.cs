using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableResourcePoolCommitEffectsTests
{
    [Fact]
    public async Task RollBackAcquiresAsync_ReleasesGrantedTickets()
    {
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquire = await pools.AcquireAsync(
            Request("node-1"),
            TestContext.Current.CancellationToken);
        var effects = new DurableResourcePoolCommitEffects(pools);

        await effects.RollBackAcquiresAsync(
            [Acquired("node-1", acquire.Tickets)],
            TestContext.Current.CancellationToken);
        var snapshot = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        snapshot.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task ReleaseCommittedTicketsAsync_RetriesTransientReleaseFailure()
    {
        var pools = new FlakyReleasePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquire = await pools.AcquireAsync(
            Request("node-1"),
            TestContext.Current.CancellationToken);
        var effects = new DurableResourcePoolCommitEffects(pools);

        await effects.ReleaseCommittedTicketsAsync(
            [Released("node-1", acquire.Tickets)],
            TestContext.Current.CancellationToken);
        var snapshot = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        pools.ReleaseAttempts.Should().Be(2);
        snapshot.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task RollBackAcquiresAsync_WhenAcquireWasQueued_DoesNotRequireResourcePoolStore()
    {
        var effects = new DurableResourcePoolCommitEffects(null);

        await effects.RollBackAcquiresAsync(
            [Queued("node-1")],
            TestContext.Current.CancellationToken);
    }

    private static WorkflowResourcePoolAcquiredEvent Acquired(
        string holderKey,
        IReadOnlyList<ResourcePoolTicket> tickets)
    {
        return new WorkflowResourcePoolAcquiredEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            HolderKey = holderKey,
            Tickets = tickets
        };
    }

    private static WorkflowResourcePoolReleasedEvent Released(
        string holderKey,
        IReadOnlyList<ResourcePoolTicket> tickets)
    {
        return new WorkflowResourcePoolReleasedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            HolderKey = holderKey,
            Tickets = tickets
        };
    }

    private static WorkflowResourcePoolQueuedEvent Queued(string holderKey)
    {
        return new WorkflowResourcePoolQueuedEvent
        {
            EventId = EventIdValue(3),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3),
            WaitId = WaitIdValue(3),
            HolderKey = holderKey,
            Requirements = [Requirement("db")],
            ExpiresAt = Timestamp(33)
        };
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static ResourcePoolAcquireRequest Request(string holderKey)
    {
        return new ResourcePoolAcquireRequest(
            InstanceIdValue(1),
            holderKey,
            [Requirement("db")],
            Timestamp(1),
            Timestamp(31));
    }

    private static ResourcePoolRequirement Requirement(string poolName)
    {
        return new ResourcePoolRequirement(poolName, 1);
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 19, 0, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return EventId.Create(GuidValue(value).ToString());
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class FlakyReleasePoolStore : IResourcePoolStore
    {
        private readonly InMemoryResourcePoolStore inner = new();
        private bool failed;

        public int ReleaseAttempts { get; private set; }

        public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
        {
            return inner.UpsertPoolAsync(definition, cancellationToken);
        }

        public Task<ResourcePoolAcquireResult> AcquireAsync(
            ResourcePoolAcquireRequest request,
            CancellationToken cancellationToken)
        {
            return inner.AcquireAsync(request, cancellationToken);
        }

        public Task<ResourcePoolReleaseResult> ReleaseAsync(
            ResourcePoolReleaseRequest request,
            CancellationToken cancellationToken)
        {
            ReleaseAttempts++;
            if (!failed)
            {
                failed = true;
                throw new InvalidOperationException("transient release failure");
            }

            return inner.ReleaseAsync(request, cancellationToken);
        }

        public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(
            string poolName,
            CancellationToken cancellationToken)
        {
            return inner.GetPoolAsync(poolName, cancellationToken);
        }

        public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
        {
            return inner.ResizePoolAsync(poolName, capacity, cancellationToken);
        }

        public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            return inner.ExpireTicketsAsync(now, cancellationToken);
        }

    }
}
