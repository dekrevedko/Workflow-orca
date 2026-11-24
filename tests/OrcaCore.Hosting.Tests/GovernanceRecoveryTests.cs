using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class GovernanceRecoveryTests
{
    private static readonly ResourcePoolName Pool = ResourcePoolName.Create("jobs");
    private static readonly DurableResourcePoolOptions Options = new()
    {
        PartitionId = ResourceGovernancePartitionId.Create("governance-recovery"),
        Pools = [DurableResourcePoolDefinition.Create(Pool, 3, TimeSpan.FromSeconds(10))]
    };
    private readonly CancellationToken token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RestartUsesLatestStateAndRetainsResizeIdentityDespiteAnUnreadableEarlierEnvelope()
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var original = new DurableResourcePoolManagement(store, Options);
        var id = ResourcePoolOperationId.Create("reduce");
        await original.ResizeAsync(Pool, 1, id, token);
        var valid = (await store.LoadAsync(Options.PartitionId, token)).Records[^1];
        var old = Record(3, """{"Kind":"unknown"}"""u8.ToArray());
        var current = ResourceGovernanceRecord.FromPersisted(4, valid.FormatId, valid.Payload, valid.Checksum);
        await store.AppendAsync(Options.PartitionId, 2, [old, current], token);

        var restarted = new DurableResourcePoolManagement(store, Options);
        Assert.Equal(1, (await restarted.GetAsync(Pool, token)).ConfiguredCapacity);
        Assert.IsType<DurableResourcePoolResizeResult.Applied>(await restarted.ResizeAsync(Pool, 1, id, token));
        Assert.IsType<DurableResourcePoolResizeResult.Conflict>(await restarted.ResizeAsync(Pool, 2, id, token));
        var history = await store.LoadAsync(Options.PartitionId, token);
        Assert.Equal(4, history.Version);
        Assert.Equal(old.Checksum, history.Records[2].Checksum);
    }

    [Theory]
    [InlineData("unknown-kind")]
    [InlineData("duplicate-pool")]
    [InlineData("missing-history-field")]
    [InlineData("empty-pools")]
    public async Task InvalidLatestStatePreventsStartupAndMutation(string fault)
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var original = new DurableResourcePoolManagement(store, Options);
        await original.InitializeAsync(token);
        var seed = (await store.LoadAsync(Options.PartitionId, token)).Records.Single();
        var envelope = JsonNode.Parse(seed.Payload.Span)!.AsObject();
        var state = envelope["State"]!.AsObject();
        var pools = state["Pools"]!.AsArray();
        switch (fault)
        {
            case "unknown-kind": envelope["Kind"] = "unknown"; break;
            case "duplicate-pool": pools.Add(pools[0]!.DeepClone()); break;
            case "missing-history-field": state.Remove("ExpiredTickets"); break;
            case "empty-pools": pools.Clear(); break;
        }

        var corrupt = Record(2, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope));
        await store.AppendAsync(Options.PartitionId, 1, [corrupt], token);
        var restarted = new DurableResourcePoolManagement(store, Options);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => restarted.InitializeAsync(token).AsTask());
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => original.ResizeAsync(Pool, 2, ResourcePoolOperationId.Create("r"), token).AsTask());
        Assert.Equal(2, (await store.LoadAsync(Options.PartitionId, token)).Version);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public async Task RestartPreservesMultiPoolOwnershipAndFifo(int thirdRequestedAtSeconds)
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var options = new DurableResourcePoolOptions
        {
            PartitionId = Options.PartitionId,
            Pools = new[] { "db", "io" }.Select(name => DurableResourcePoolDefinition.Create(
                ResourcePoolName.Create(name), 1, TimeSpan.FromMinutes(1))).ToArray()
        };
        SerializedResourceGovernanceAggregate Restart() => new(store, options);
        var time = DateTimeOffset.UnixEpoch;
        ResourcePoolAcquireRequest Request(string key, int seconds) =>
            new(InstanceId.Parse(Guid.CreateVersion7().ToString("D")), key, [new("db", 1), new("io", 1)], time.AddSeconds(seconds), null)
            {
                ProtectionToken = LeaseProtectionToken.Parse(key)
            };
        var first = Request("first", 0);
        var second = Request("second", 1);
        var third = Request("third", thirdRequestedAtSeconds);
        var active = await Restart().AcquireAsync(first, token);
        Assert.Equal(2, active.Tickets.Count);
        Assert.Equal(ResourcePoolAcquireStatus.Queued, (await Restart().AcquireAsync(second, token)).Status);
        Assert.Equal(ResourcePoolAcquireStatus.Queued, (await Restart().AcquireAsync(third, token)).Status);
        var due = await Restart().ExpireTicketsAsync(time.AddMinutes(2), token);
        Assert.Equal(2, due.ExpiredTickets.Count);

        var recovered = Restart();
        foreach (var pool in await recovered.ListPoolsAsync(token))
        {
            var expected = active.Tickets.Single(ticket => ticket.PoolName == pool.Name);
            Assert.Equal(expected with { ReviewMarked = true }, Assert.Single(pool.HeldTickets));
            Assert.Equal(0, pool.AvailableCapacity);
            Assert.Equal(new[] { "second", "third" }, pool.QueuedWaiters.Select(waiter => waiter.HolderKey));
        }

        var confirmed = new ResourcePoolStopConfirmationRequest(first.HolderInstanceId, first.HolderKey,
            first.ProtectionToken!, StopConfirmationId.Create("stopped"), time.AddMinutes(3));
        Assert.Equal(ResourcePoolStopConfirmationStatus.Released, await recovered.ConfirmAndReleaseAsync(confirmed, token));
        var replacement = Restart();
        Assert.Equal(ResourcePoolStopConfirmationStatus.AlreadyConfirmed, await replacement.ConfirmAndReleaseAsync(confirmed, token));
        Assert.Equal(first.ProtectionToken, (await replacement.GetConfirmationBindingAsync(confirmed.ConfirmationId, token)).Value);
        var evidence = (await replacement.GetReleaseEvidenceAsync(first.ProtectionToken!, token)).Value;
        Assert.Equal(2, evidence.ReleasedTickets.Count);
        var duplicate = await replacement.AcquireAsync(second, token);
        Assert.Equal(ResourcePoolAcquireStatus.Granted, duplicate.Status);
        Assert.All(duplicate.Tickets, ticket => Assert.Equal(2, ticket.ProviderGeneration));
        var transferred = await replacement.ReleaseAsync(
            new ResourcePoolReleaseRequest(second.HolderInstanceId, second.HolderKey, time.AddMinutes(4)), token);
        Assert.Equal(third.HolderInstanceId, Assert.Single(transferred.GrantedWaiters).HolderInstanceId);
        foreach (var pool in await Restart().ListPoolsAsync(token))
        {
            Assert.Equal(third.HolderInstanceId, Assert.Single(pool.HeldTickets).HolderInstanceId);
            Assert.Empty(pool.QueuedWaiters);
            Assert.Equal(0, pool.AvailableCapacity);
        }
    }

    [Fact]
    public async Task DuplicatePoolRequirementsCannotReserveCapacityFromAnIncomingOrPersistedRequest()
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var aggregate = new SerializedResourceGovernanceAggregate(store, Options);
        var time = DateTimeOffset.UnixEpoch;
        var waiter = new ResourcePoolAcquireRequest(InstanceId.Parse(Guid.NewGuid().ToString("D")), "waiter",
            [new(Pool.Value, 2)], time, null);
        var repeated = waiter with { Requirements = [new(Pool.Value, 2), new(Pool.Value, 2)] };
        await Assert.ThrowsAsync<ArgumentException>(() => aggregate.AcquireAsync(repeated, token));
        Assert.Equal(0, (await store.LoadLatestAsync(Options.PartitionId, token)).Version);

        var submitted = new List<ResourcePoolRequirement> { new(Pool.Value, 2) };
        var mutating = new HeadOnlyStore(store)
        {
            BeforeAppend = () =>
            {
                submitted.Add(new(Pool.Value, 2));
                return Task.CompletedTask;
            }
        };
        var instance = new SerializedResourceGovernanceAggregate(mutating, Options);
        var accepted = await instance.AcquireAsync(waiter with { Requirements = submitted }, token);
        Assert.Equal(2, submitted.Count);
        Assert.Equal(2, accepted.Tickets.Sum(ticket => ticket.Count));
        await instance.ReleaseAsync(new ResourcePoolReleaseRequest(waiter.HolderInstanceId, waiter.HolderKey, time), token);

        var holder = new ResourcePoolAcquireRequest(InstanceId.Parse(Guid.NewGuid().ToString("D")), "holder",
            [new(Pool.Value, 3)], time, null);
        Assert.Equal(ResourcePoolAcquireStatus.Granted, (await aggregate.AcquireAsync(holder, token)).Status);
        Assert.Equal(ResourcePoolAcquireStatus.Queued, (await aggregate.AcquireAsync(waiter, token)).Status);
        var head = await store.LoadLatestAsync(Options.PartitionId, token);
        var envelope = JsonNode.Parse(head.Record!.Payload.Span)!;
        var requirements = envelope["State"]!["Waiters"]![0]!["Requirements"]!.AsArray();
        requirements.Add(requirements[0]!.DeepClone());
        var damaged = Record(head.Version + 1, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope));
        await store.AppendAsync(Options.PartitionId, head.Version, [damaged], token);

        var release = new ResourcePoolReleaseRequest(holder.HolderInstanceId, holder.HolderKey, time);
        await Assert.ThrowsAsync<InvalidOperationException>(() => aggregate.ReleaseAsync(release, token));
        Assert.Equal(damaged.Sequence, (await store.LoadLatestAsync(Options.PartitionId, token)).Version);
    }

    [Theory]
    [InlineData("Tickets")]
    [InlineData("Waiters")]
    [InlineData("ReleaseEvidence")]
    public async Task MalformedOwnerInCurrentSnapshotCannotBeServedOrRecommitted(string collection)
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var original = new SerializedResourceGovernanceAggregate(store, Options);
        ResourcePoolAcquireRequest Request(string key) => new(InstanceId.Parse(Guid.CreateVersion7().ToString("D")),
            key, [new("jobs", 3)], DateTimeOffset.UnixEpoch, null);
        var first = Request("first");
        await original.AcquireAsync(first, token);
        await original.AcquireAsync(Request("second"), token);
        await original.ReleaseAsync(new ResourcePoolReleaseRequest(first.HolderInstanceId, first.HolderKey, first.RequestedAt), token);
        await original.AcquireAsync(Request("third"), token);
        var head = await store.LoadLatestAsync(Options.PartitionId, token);
        var envelope = JsonNode.Parse(head.Record!.Payload.Span)!.AsObject();
        envelope["State"]![collection]![0]!["InstanceId"] = "bad-guid";
        var corrupt = Record(head.Version + 1, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope));
        await store.AppendAsync(Options.PartitionId, head.Version, [corrupt], token);

        var restarted = new DurableResourcePoolManagement(store, Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.GetAsync(Pool, token).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            original.ResizeManagementAsync(Pool, 2, ResourcePoolOperationId.Create("r"), token).AsTask());
        Assert.Equal(corrupt.Sequence, (await store.LoadLatestAsync(Options.PartitionId, token)).Version);
    }

    [Fact]
    public async Task ConflictingResizeRecoversWinningIdentitiesBeforeRetry()
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var fenced = new HeadOnlyStore(store);
        var local = new DurableResourcePoolManagement(fenced, Options);
        var remote = new DurableResourcePoolManagement(store, Options);
        await local.InitializeAsync(token);
        var winner = ResourcePoolOperationId.Create("winner");
        fenced.BeforeAppend = async () => { await remote.ResizeAsync(Pool, 2, winner, token); };
        var loser = ResourcePoolOperationId.Create("retry");
        await local.ResizeAsync(Pool, 1, loser, token);
        Assert.Equal(new long[] { 0, 1, 2 }, fenced.ExpectedVersions);
        Assert.Equal(1, (await local.GetAsync(Pool, token)).ConfiguredCapacity);
        Assert.IsType<DurableResourcePoolResizeResult.Conflict>(await local.ResizeAsync(Pool, 3, winner, token));
        Assert.IsType<DurableResourcePoolResizeResult.Applied>(await local.ResizeAsync(Pool, 1, loser, token));
        Assert.Equal(3, (await store.LoadAsync(Options.PartitionId, token)).Version);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("empty-without-token")]
    [InlineData("empty-with-confirmation")]
    [InlineData("padded-token")]
    [InlineData("foreign-ticket-owner")]
    [InlineData("foreign-ticket-token")]
    public async Task RestartValidatesScopedClosureReceiptsFromTheLatestRecord(string shape)
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var original = new SerializedResourceGovernanceAggregate(store, Options);
        var owner = InstanceId.Parse(Guid.CreateVersion7().ToString("D"));
        var occurrence = LeaseProtectionToken.Parse("occurrence");
        var request = new ResourcePoolAcquireRequest(owner, "holder", [new(Pool.Value, 1)], DateTimeOffset.UnixEpoch, null)
            { ProtectionToken = occurrence };
        await original.AcquireAsync(request, token);
        await original.ReleaseAsync(new ResourcePoolReleaseRequest(owner, request.HolderKey, request.RequestedAt), token);

        var head = await store.LoadLatestAsync(Options.PartitionId, token);
        var envelope = JsonNode.Parse(head.Record!.Payload.Span)!;
        var receipt = envelope["State"]!["ReleaseEvidence"]![0]!;
        var tickets = receipt["ReleasedTickets"]!.AsArray();
        switch (shape)
        {
            case "empty": tickets.Clear(); break;
            case "empty-without-token": tickets.Clear(); receipt["ProtectionToken"] = null; break;
            case "empty-with-confirmation": tickets.Clear(); receipt["ConfirmationId"] = "confirmed"; break;
            case "padded-token": tickets.Clear(); receipt["ProtectionToken"] = " occurrence"; break;
            case "foreign-ticket-owner": tickets[0]!["InstanceId"] = Guid.CreateVersion7().ToString("D"); break;
            case "foreign-ticket-token": tickets[0]!["ProtectionToken"] = "other-occurrence"; break;
            default: throw new ArgumentOutOfRangeException(nameof(shape));
        }

        var latest = Record(head.Version + 1, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope));
        await store.AppendAsync(Options.PartitionId, head.Version, [latest], token);
        var restarted = new SerializedResourceGovernanceAggregate(new HeadOnlyStore(store), Options);
        if (shape == "empty")
        {
            var closed = (await restarted.GetReleaseEvidenceAsync(occurrence, token)).Value;
            Assert.Equal(owner, closed.HolderInstanceId);
            Assert.Equal(request.HolderKey, closed.HolderKey);
            Assert.Equal(occurrence, closed.ProtectionToken);
            Assert.Null(closed.ConfirmationId);
            Assert.Empty(closed.ReleasedTickets);
            Assert.Equal(ResourcePoolAcquireStatus.Rejected, (await restarted.AcquireAsync(request, token)).Status);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.AcquireAsync(request, token));
        }
        Assert.Equal(latest.Sequence, (await store.LoadLatestAsync(Options.PartitionId, token)).Version);
    }

    [Fact]
    public async Task WrongPartitionCannotInitializeOrMutate()
    {
        using var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = services.GetRequiredService<IDurableResourceGovernanceStore>();
        var wrong = new HeadOnlyStore(store) { ReturnedPartition = ResourceGovernancePartitionId.Create("other") };
        var local = new DurableResourcePoolManagement(wrong, Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.InitializeAsync(token).AsTask());
        Assert.Empty(wrong.ExpectedVersions);
    }

    private sealed class HeadOnlyStore(IDurableResourceGovernanceStore inner) : IDurableResourceGovernanceStore
    {
        internal List<long> ExpectedVersions { get; } = [];
        internal Func<Task>? BeforeAppend { get; set; }
        internal ResourceGovernancePartitionId? ReturnedPartition { get; init; }

        public async ValueTask<ResourceGovernanceHead> LoadLatestAsync(
            ResourceGovernancePartitionId partitionId, CancellationToken cancellationToken = default)
        {
            var head = await inner.LoadLatestAsync(partitionId, cancellationToken);
            return ReturnedPartition is null ? head : ResourceGovernanceHead.Create(ReturnedPartition, head.Version, head.Record);
        }

        public ValueTask<ResourceGovernanceStream> LoadAsync(
            ResourceGovernancePartitionId partitionId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Recovery attempted a history read.");

        public async ValueTask<ResourceGovernanceAppendResult> AppendAsync(ResourceGovernancePartitionId partitionId,
            long expectedVersion, IReadOnlyList<ResourceGovernanceRecord> records, CancellationToken cancellationToken = default)
        {
            ExpectedVersions.Add(expectedVersion);
            if (BeforeAppend is { } commit)
            {
                BeforeAppend = null;
                await commit();
            }
            return await inner.AppendAsync(partitionId, expectedVersion, records, cancellationToken);
        }
    }

    private static ResourceGovernanceRecord Record(long sequence, byte[] bytes) =>
        ResourceGovernanceRecord.FromPersisted(sequence, ResourceGovernanceRecord.V1Format, bytes,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
}
