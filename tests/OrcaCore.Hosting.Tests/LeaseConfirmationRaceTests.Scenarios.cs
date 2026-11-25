using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.SqlServer;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class LeaseConfirmationRaceTests
{
    private static readonly ResourcePoolName Pool = ResourcePoolName.Create("database");
    private static readonly ResourceGovernancePartitionId Partition = ResourceGovernancePartitionId.Create("confirmation-race");
    private static readonly StopConfirmationId WinnerId = StopConfirmationId.Create("confirmation-a");
    private static readonly StopConfirmationId ContenderId = StopConfirmationId.Create("confirmation-b");
    private static readonly LeaseProtectionToken UnknownToken = LeaseProtectionToken.Parse("unknown-lease");

    private sealed record Seed(int WorkerPid, Guid Owner, string OwnerKey, string OwnerToken, Guid OwnerTicket,
        Guid Waiter, string WaiterKey, string WaiterToken, Guid WaiterId, long WaiterVersion,
        long GovernanceVersion, string[] OwnerJournal, string[] WaiterJournal);

    private sealed record Committed(int WorkerPid, Guid PromotedTicket, long Version, string Checksum);
    private sealed record Contender(int WorkerPid, string Status, string[] OwnerJournal);
    private sealed record ConfirmationReplays(string WinnerReplay, string ContenderReplay,
        string OtherTokenWithWinningId, string OtherTokenWithLosingId);

    private static async Task RunConfirmationScenarioAsync(string phase, string provider, string connection,
        string directory, bool stale, bool keepWinnerAlive, CancellationToken token)
    {
        var workflow = Workflow.Durable<int>(DefinitionId.Parse("d85c8ea3-b738-4cc2-a66d-04102a5762f5"), DefinitionVersion.Initial)
            .Init<int>(value => value)
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(Pool)),
                lease => lease.Then<ProtectedStep>())
            .End(state => state.Value).Build();
        await using var host = await CreateConfirmationHostAsync(provider, connection, workflow, token);
        var recovery = host.GetRequiredService<IDurableResourceLeaseRecovery>();
        var gate = host.GetRequiredService<ConfirmationAppendGate>();
        if (phase == "winner")
        {
            var setup = await SeedConfirmationAsync(host, workflow, token);
            await WritePhaseAsync(directory, "seed.json", setup, token);
            await AwaitSignalAsync(directory, "allow-winner", token);
            gate.BeforeWrite = (expected, checks, record) => CheckProposal(setup, expected, checks, record, WinnerId);
            gate.AfterWrite = async (result, record) =>
            {
                var version = Assert.IsType<ResourceGovernanceAppendResult.Committed>(result).Version;
                Assert.Equal(setup.GovernanceVersion + 1, version);
                var promoted = await AssertTransferAsync(host, setup, token);
                var originalVersion = await AssertGapAsync(host, setup, token);
                await WritePhaseAsync(directory, "committed.json",
                    new Committed(Environment.ProcessId, promoted.TicketId, version, record.Checksum), token);
                // The controller either kills A here or releases it only after B has published the tombstone and exited.
                await AwaitSignalAsync(directory, "allow-winner-return", token);
                Assert.True(keepWinnerAlive);
                var published = await ReadCheckpointAsync(host, setup.Owner, token);
                Assert.Equal(originalVersion.Value + 1, published.StreamVersion.Value);
                Assert.Equal("Released", Obligation(published).LeasePhase);
                Assert.Equal(WinnerId.Value, Obligation(published).AcceptedConfirmationId);
            };
            var winnerStatus = await recovery.ConfirmProtectedWorkStoppedAsync(LeaseProtectionToken.Parse(setup.OwnerToken), WinnerId, token);
            Assert.True(keepWinnerAlive);
            Assert.Equal(ProtectedWorkStopConfirmationStatus.Released, winnerStatus);
            await WritePhaseAsync(directory, "winner-returned.json", new { WorkerPid = Environment.ProcessId, Status = winnerStatus.ToString() }, token);
            return;
        }

        var seed = await ReadPhaseAsync<Seed>(directory, "seed.json", token);
        Assert.NotEqual(seed.WorkerPid, Environment.ProcessId);
        var occurrence = LeaseProtectionToken.Parse(seed.OwnerToken);
        if (phase == "contender")
        {
            await AssertGapAsync(host, seed, token);
            Assert.Equal(ProtectedWorkStopConfirmationStatus.TokenNotFound,
                await recovery.ConfirmProtectedWorkStoppedAsync(UnknownToken, ContenderId, token));
            var rejected = false;
            if (stale)
            {
                gate.BeforeWrite = async (expected, checks, record) =>
                {
                    await CheckProposal(seed, expected, checks, record, ContenderId);
                    await WritePhaseAsync(directory, "prepared.json", new { WorkerPid = Environment.ProcessId, expected }, token);
                    await AwaitSignalAsync(directory, "allow-contender", token);
                    await AssertGapAsync(host, seed, token);
                    await AssertTransferAsync(host, seed, token);
                };
                gate.AfterWrite = (result, _) =>
                {
                    Assert.Equal(seed.GovernanceVersion + 1, Assert.IsType<ResourceGovernanceAppendResult.Conflict>(result).ActualVersion);
                    rejected = true;
                    return Task.CompletedTask;
                };
            }
            var status = await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, ContenderId, token);
            Assert.Equal(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed, status);
            Assert.Equal(stale, rejected);
            await WritePhaseAsync(directory, "contender.json", new Contender(Environment.ProcessId, status.ToString(),
                EventIds(await ReadJournalAsync(host, seed.Owner, token))), token);
            return;
        }

        if (phase == "verify")
        {
            await VerifyConfirmationAsync(host, seed, directory, token);
            if (!keepWinnerAlive)
                await SeedLegacySplitTombstoneAsync(host, seed, token);
            return;
        }

        if (phase != "repair") throw new ArgumentOutOfRangeException(nameof(phase));
        await VerifyLegacySplitRepairAsync(host, seed, directory, stale ? ContenderId : WinnerId, token);
    }

    private static async Task<Seed> SeedConfirmationAsync(ServiceProvider host, DurableWorkflowDefinition<int, int> workflow,
        CancellationToken token)
    {
        var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(workflow.Reference);
        var step = host.GetRequiredService<ProtectedStep>();
        var started = handle.StartOrGetAsync(1, StartIdempotencyKey.Create("owner-a"), token).AsTask();
        if (await Task.WhenAny(step.Entered.Task, started).WaitAsync(token) == started)
        {
            await started;
            throw new InvalidOperationException("Owner returned before entering its protected step.");
        }
        var owner = await step.Entered.Task;
        var instance = await handle.GetInstanceAsync(owner, token);
        Assert.Equal(WorkflowTerminationStatus.Terminated, await instance.TerminateAsync(token));
        step.Return.TrySetResult();
        Assert.Equal(owner, (await started).GetHandleOrThrow().InstanceId);

        var waiter = (await handle.StartOrGetAsync(2, StartIdempotencyKey.Create("waiter-b"), token)).GetHandleOrThrow().InstanceId;
        Assert.NotEqual(owner, waiter);
        var ownerCheckpoint = await ReadCheckpointAsync(host, owner.Value, token);
        var held = Obligation(ownerCheckpoint);
        Assert.Equal("Quarantined", held.LeasePhase);
        Assert.Null(held.AcceptedConfirmationId);
        var waiterCheckpoint = await ReadCheckpointAsync(host, waiter.Value, token);
        var queued = Obligation(waiterCheckpoint);
        Assert.Equal("Queued", queued.LeasePhase);
        Assert.NotEqual(held.ProtectionToken, queued.ProtectionToken);

        var pool = (await host.GetRequiredService<IResourcePoolStore>().GetPoolAsync(Pool.Value, token)).Value;
        Assert.Equal(0, pool.AvailableCapacity);
        var ticket = Assert.Single(pool.HeldTickets);
        Assert.Equal(owner, ticket.HolderInstanceId);
        Assert.Equal(held.HolderKey, ticket.HolderKey);
        Assert.Equal(Guid.Parse(Assert.Single(held.LeaseTickets).TicketId), ticket.TicketId);
        Assert.Equal(1, ticket.ProviderGeneration);
        var waiting = Assert.Single(pool.QueuedWaiters);
        Assert.Equal(waiter, waiting.HolderInstanceId);
        Assert.Equal(queued.HolderKey, waiting.HolderKey);
        var ownerJournal = await ReadJournalAsync(host, owner.Value, token);
        Assert.Equal(ticket.TicketId, Assert.Single(Assert.Single(ownerJournal.OfType<WorkflowResourcePoolAcquiredEvent>()).Tickets).TicketId);
        Assert.Empty(ownerJournal.OfType<WorkflowResourcePoolReleasedEvent>());
        var waiterJournal = await ReadJournalAsync(host, waiter.Value, token);
        Assert.Equal(waiting.HolderKey, Assert.Single(waiterJournal.OfType<WorkflowResourcePoolQueuedEvent>()).HolderKey);
        Assert.Empty(waiterJournal.OfType<WorkflowResourcePoolAcquiredEvent>());
        return new Seed(Environment.ProcessId, owner.Value, ticket.HolderKey, held.ProtectionToken!, ticket.TicketId,
            waiter.Value, waiting.HolderKey, queued.ProtectionToken!, waiting.WaiterId, waiterCheckpoint.StreamVersion.Value,
            (await host.GetRequiredService<IDurableResourceGovernanceStore>().LoadLatestAsync(Partition, token)).Version,
            EventIds(ownerJournal), EventIds(waiterJournal));
    }

    private static async Task VerifyConfirmationAsync(ServiceProvider host, Seed seed, string directory, CancellationToken token)
    {
        var committed = await ReadPhaseAsync<Committed>(directory, "committed.json", token);
        var contender = await ReadPhaseAsync<Contender>(directory, "contender.json", token);
        Assert.Equal(seed.WorkerPid, committed.WorkerPid);
        Assert.NotEqual(contender.WorkerPid, Environment.ProcessId);
        var governance = host.GetRequiredService<IDurableResourceGovernanceStore>();
        var journal = await governance.LoadAsync(Partition, token);
        var appended = Assert.Single(journal.Records.Skip(checked((int)seed.GovernanceVersion)));
        Assert.Equal(committed.Version, appended.Sequence);
        Assert.Equal(committed.Checksum, appended.Checksum);
        var promoted = await AssertTransferAsync(host, seed, token);
        Assert.Equal(committed.PromotedTicket, promoted.TicketId);
        var promotedTicketIds = new HashSet<Guid>();
        foreach (var record in journal.Records)
        {
            using var document = JsonDocument.Parse(record.Payload);
            foreach (var ticket in document.RootElement.GetProperty("State").GetProperty("Tickets").EnumerateArray())
            {
                if (ticket.GetProperty("ProtectionToken").GetString() == seed.WaiterToken)
                    promotedTicketIds.Add(Guid.Parse(ticket.GetProperty("TicketId").GetString()!));
            }
        }
        Assert.Equal(committed.PromotedTicket, Assert.Single(promotedTicketIds));
        var pools = host.GetRequiredService<IResourcePoolStore>();
        var occurrence = LeaseProtectionToken.Parse(seed.OwnerToken);
        var providerId = (await pools.GetReleaseEvidenceAsync(occurrence, token)).Value.ConfirmationId!.Value;
        var checkpoint = await ReadCheckpointAsync(host, seed.Owner, token);
        var lease = Obligation(checkpoint);
        Assert.Equal("Released", lease.LeasePhase);
        Assert.Equal(seed.OwnerKey, lease.HolderKey);
        Assert.Equal(seed.OwnerTicket, Guid.Parse(Assert.Single(lease.LeaseTickets).TicketId));
        var workflowId = lease.AcceptedConfirmationId;
        Assert.Equal(WinnerId.Value, providerId);
        Assert.Equal(providerId, workflowId);
        var diagnostics = await host.GetRequiredService<IDurableResourceLeaseDiagnostics>().GetAsync(occurrence, token);
        Assert.Equal(DurableResourceLeaseObligationStatus.Released, diagnostics!.Status);
        Assert.Equal(workflowId, diagnostics.AcceptedConfirmationId!.Value);
        var ownerJournal = await ReadJournalAsync(host, seed.Owner, token);
        Assert.Equal(contender.OwnerJournal, EventIds(ownerJournal));
        Assert.Equal(seed.OwnerJournal, EventIds(ownerJournal.Take(seed.OwnerJournal.Length)));
        var tail = ownerJournal.Skip(seed.OwnerJournal.Length).ToArray();
        // Termination cleared the aggregate's active tickets. The workflow journals the tombstone
        // position, and the governance receipt identifies the physical reservation that was released.
        Assert.IsType<WorkflowExecutionPositionChangedEvent>(Assert.Single(tail));

        var replays = await AssertConfirmationReplaysAsync(host, occurrence, token);

        var replayedCheckpoint = await ReadCheckpointAsync(host, seed.Owner, token);
        Assert.Equal(checkpoint.StreamVersion, replayedCheckpoint.StreamVersion);
        Assert.Equal(checkpoint.Payload, replayedCheckpoint.Payload);
        Assert.Equal(contender.OwnerJournal, EventIds(await ReadJournalAsync(host, seed.Owner, token)));
        Assert.Equal(seed.WaiterJournal, EventIds(await ReadJournalAsync(host, seed.Waiter, token)));
        Assert.Equal(seed.WaiterVersion, (await ReadCheckpointAsync(host, seed.Waiter, token)).StreamVersion.Value);
        Assert.Equal(committed.Version, (await governance.LoadLatestAsync(Partition, token)).Version);
        Assert.Equal(promoted.TicketId, (await AssertTransferAsync(host, seed, token)).TicketId);

        await WritePhaseAsync(directory, "verified.json", new
        {
            WorkerPid = Environment.ProcessId, ProviderConfirmationId = providerId, WorkflowConfirmationId = workflowId,
            DiagnosticConfirmationId = diagnostics.AcceptedConfirmationId.Value,
            Agreement = providerId == workflowId, Outcome = "Canonical agreement",
            seed.Owner, seed.OwnerTicket, seed.OwnerToken, seed.Waiter, seed.WaiterId, seed.WaiterToken,
            PromotedTicket = promoted.TicketId, promoted.ProviderGeneration, GrantCount = promotedTicketIds.Count,
            ConfirmationGovernanceVersion = journal.Version, WorkflowVersion = checkpoint.StreamVersion.Value,
            WorkflowConfirmationEvents = EventIds(tail), contender.Status, replays.WinnerReplay, replays.ContenderReplay,
            replays.OtherTokenWithWinningId, replays.OtherTokenWithLosingId
        }, token);
    }

    private static async Task<ConfirmationReplays> AssertConfirmationReplaysAsync(ServiceProvider host,
        LeaseProtectionToken occurrence, CancellationToken token)
    {
        var recovery = host.GetRequiredService<IDurableResourceLeaseRecovery>();
        var winnerReplay = await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, WinnerId, token);
        var contenderReplay = await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, ContenderId, token);
        var otherWithWinner = await recovery.ConfirmProtectedWorkStoppedAsync(UnknownToken, WinnerId, token);
        var otherWithContender = await recovery.ConfirmProtectedWorkStoppedAsync(UnknownToken, ContenderId, token);
        Assert.Equal(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed, winnerReplay);
        Assert.Equal(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed, contenderReplay);
        Assert.Equal(ProtectedWorkStopConfirmationStatus.ConfirmationConflict, otherWithWinner);
        Assert.Equal(ProtectedWorkStopConfirmationStatus.TokenNotFound, otherWithContender);
        return new(winnerReplay.ToString(), contenderReplay.ToString(), otherWithWinner.ToString(), otherWithContender.ToString());
    }

    // The process race has already passed. Recreate a checkpoint written by the former driver
    // while retaining the exact governance receipt, transferred reservation and workflow event stream.
    private static async Task SeedLegacySplitTombstoneAsync(ServiceProvider host, Seed seed, CancellationToken token)
    {
        var checkpoint = await ReadCheckpointAsync(host, seed.Owner, token);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        var obligations = envelope.OwnedObligations.ToArray();
        var index = Array.FindIndex(obligations, item => item.ProtectionToken == seed.OwnerToken);
        var released = obligations[index];
        Assert.Equal("Released", released.LeasePhase);
        Assert.Equal(WinnerId.Value, released.AcceptedConfirmationId);
        obligations[index] = released with { AcceptedConfirmationId = ContenderId.Value };
        var legacy = envelope with { OwnedObligations = obligations };
        var written = await host.GetRequiredService<IWorkflowEventStore>().AppendAsync(new ProviderCommitBatch
        {
            StreamId = new(checkpoint.InstanceId), ExpectedVersion = checkpoint.StreamVersion,
            Checkpoint = checkpoint with { Payload = legacy.Serialize() }
        }, token);
        Assert.True(written.IsSuccess);
        Assert.Equal(checkpoint.StreamVersion, written.Value.NewVersion);
    }

    private static async Task VerifyLegacySplitRepairAsync(ServiceProvider host, Seed seed, string directory,
        StopConfirmationId repairId, CancellationToken token)
    {
        var committed = await ReadPhaseAsync<Committed>(directory, "committed.json", token);
        var contender = await ReadPhaseAsync<Contender>(directory, "contender.json", token);
        var occurrence = LeaseProtectionToken.Parse(seed.OwnerToken);
        var before = await ReadCheckpointAsync(host, seed.Owner, token);
        var legacy = Obligation(before);
        Assert.Equal(seed.OwnerToken, legacy.ProtectionToken);
        Assert.Equal("Released", legacy.LeasePhase);
        Assert.Equal(ContenderId.Value, legacy.AcceptedConfirmationId);
        Assert.Equal(committed.PromotedTicket, (await AssertTransferAsync(host, seed, token)).TicketId);

        var recovery = host.GetRequiredService<IDurableResourceLeaseRecovery>();
        var otherWithContender = await recovery.ConfirmProtectedWorkStoppedAsync(UnknownToken, ContenderId, token);
        Assert.Equal(ProtectedWorkStopConfirmationStatus.TokenNotFound, otherWithContender);
        var unchanged = await ReadCheckpointAsync(host, seed.Owner, token);
        Assert.Equal(before.StreamVersion, unchanged.StreamVersion);
        Assert.Equal(before.Payload, unchanged.Payload);
        Assert.Equal(contender.OwnerJournal, EventIds(await ReadJournalAsync(host, seed.Owner, token)));

        var status = await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, repairId, token);
        Assert.Equal(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed, status);
        var repaired = await ReadCheckpointAsync(host, seed.Owner, token);
        var lease = Obligation(repaired);
        Assert.Equal("Released", lease.LeasePhase);
        Assert.Equal(WinnerId.Value, lease.AcceptedConfirmationId);
        Assert.Equal(before.StreamVersion.Value + 1, repaired.StreamVersion.Value);
        var diagnostics = await host.GetRequiredService<IDurableResourceLeaseDiagnostics>().GetAsync(occurrence, token);
        Assert.Equal(DurableResourceLeaseObligationStatus.Released, diagnostics!.Status);
        Assert.Equal(WinnerId.Value, diagnostics.AcceptedConfirmationId!.Value);
        var journal = await ReadJournalAsync(host, seed.Owner, token);
        Assert.Equal(contender.OwnerJournal, EventIds(journal.Take(contender.OwnerJournal.Length)));
        Assert.IsType<WorkflowExecutionPositionChangedEvent>(Assert.Single(journal.Skip(contender.OwnerJournal.Length)));

        var replays = await AssertConfirmationReplaysAsync(host, occurrence, token);
        var replayed = await ReadCheckpointAsync(host, seed.Owner, token);
        Assert.Equal(repaired.StreamVersion, replayed.StreamVersion);
        Assert.Equal(repaired.Payload, replayed.Payload);
        Assert.Equal(EventIds(journal), EventIds(await ReadJournalAsync(host, seed.Owner, token)));
        Assert.Equal(seed.WaiterVersion, (await ReadCheckpointAsync(host, seed.Waiter, token)).StreamVersion.Value);
        Assert.Equal(seed.WaiterJournal, EventIds(await ReadJournalAsync(host, seed.Waiter, token)));
        var head = await host.GetRequiredService<IDurableResourceGovernanceStore>().LoadLatestAsync(Partition, token);
        Assert.Equal(committed.Version, head.Version);
        var promoted = await AssertTransferAsync(host, seed, token);
        Assert.Equal(committed.PromotedTicket, promoted.TicketId);
        await WritePhaseAsync(directory, "legacy-repair.json", new
        {
            WorkerPid = Environment.ProcessId, SeededWorkflowId = legacy.AcceptedConfirmationId,
            UnrelatedTokenBeforeRepair = otherWithContender.ToString(), RepairCaller = repairId.Value, RepairStatus = status.ToString(),
            RepairedWorkflowId = lease.AcceptedConfirmationId, DiagnosticConfirmationId = diagnostics.AcceptedConfirmationId.Value,
            CorrectiveEvents = journal.Count - contender.OwnerJournal.Length, GovernanceVersion = head.Version,
            PromotedTicket = promoted.TicketId, Replays = replays
        }, token);
    }

    private static async Task<ResourcePoolTicket> AssertTransferAsync(ServiceProvider host, Seed seed, CancellationToken token)
    {
        var pools = host.GetRequiredService<IResourcePoolStore>();
        var occurrence = LeaseProtectionToken.Parse(seed.OwnerToken);
        Assert.Equal(occurrence, (await pools.GetConfirmationBindingAsync(WinnerId, token)).Value);
        Assert.False((await pools.GetConfirmationBindingAsync(ContenderId, token)).HasValue);
        var receipt = (await pools.GetReleaseEvidenceAsync(occurrence, token)).Value;
        Assert.Equal(WinnerId, receipt.ConfirmationId);
        Assert.Equal(occurrence, receipt.ProtectionToken);
        Assert.Equal(seed.Owner, receipt.HolderInstanceId.Value);
        Assert.Equal(seed.OwnerKey, receipt.HolderKey);
        Assert.Equal(seed.OwnerTicket, Assert.Single(receipt.ReleasedTickets).TicketId);
        var pool = (await pools.GetPoolAsync(Pool.Value, token)).Value;
        Assert.Equal(0, pool.AvailableCapacity);
        Assert.Empty(pool.QueuedWaiters);
        var promoted = Assert.Single(pool.HeldTickets);
        Assert.Equal(seed.Waiter, promoted.HolderInstanceId.Value);
        Assert.Equal(seed.WaiterKey, promoted.HolderKey);
        Assert.Equal(2, promoted.ProviderGeneration);
        Assert.Equal(1, promoted.Count);
        Assert.NotEqual(seed.OwnerTicket, promoted.TicketId);
        return promoted;
    }

    private static async Task<StreamVersion> AssertGapAsync(ServiceProvider host, Seed seed, CancellationToken token)
    {
        var checkpoint = await ReadCheckpointAsync(host, seed.Owner, token);
        var lease = Obligation(checkpoint);
        Assert.Equal(seed.OwnerToken, lease.ProtectionToken);
        Assert.Equal("Quarantined", lease.LeasePhase);
        Assert.Null(lease.AcceptedConfirmationId);
        Assert.Equal(seed.OwnerJournal, EventIds(await ReadJournalAsync(host, seed.Owner, token)));
        return checkpoint.StreamVersion;
    }

    private static Task CheckProposal(Seed seed, long expected, IReadOnlyDictionary<InstanceId, long> checks,
        ResourceGovernanceRecord record, StopConfirmationId id)
    {
        Assert.Equal(seed.GovernanceVersion, expected);
        var guard = Assert.Single(checks);
        Assert.Equal(seed.Waiter, guard.Key.Value);
        Assert.Equal(seed.WaiterVersion, guard.Value);
        using var document = JsonDocument.Parse(record.Payload);
        var envelope = document.RootElement;
        Assert.Equal("confirmed-holder-released", envelope.GetProperty("Operation").GetString());
        var state = envelope.GetProperty("State");
        var binding = Assert.Single(state.GetProperty("ConfirmationBindings").EnumerateArray());
        Assert.Equal(id.Value, binding.GetProperty("ConfirmationId").GetString());
        Assert.Equal(seed.OwnerToken, binding.GetProperty("ProtectionToken").GetString());
        var ticket = Assert.Single(state.GetProperty("Tickets").EnumerateArray());
        Assert.Equal(seed.WaiterToken, ticket.GetProperty("ProtectionToken").GetString());
        Assert.Empty(state.GetProperty("Waiters").EnumerateArray());
        return Task.CompletedTask;
    }

    private sealed class ProtectedStep : IStep<int>
    {
        internal TaskCompletionSource<InstanceId> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Return { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken)
        {
            // Only owner A may enter. B remains parked while we observe the transferred reservation.
            Assert.True(Entered.TrySetResult(context.Execution.WorkflowInstanceId));
            await Return.Task;
            return new StepResult.Completed();
        }
    }

    private sealed class ConfirmationAppendGate(ICheckpointGuardedResourceGovernanceStore inner) : ICheckpointGuardedResourceGovernanceStore
    {
        internal Func<long, IReadOnlyDictionary<InstanceId, long>, ResourceGovernanceRecord, Task>? BeforeWrite { get; set; }
        internal Func<ResourceGovernanceAppendResult, ResourceGovernanceRecord, Task>? AfterWrite { get; set; }

        public async ValueTask<ResourceGovernanceAppendResult> AppendIfCurrentAsync(ResourceGovernancePartitionId id, long expected,
            IReadOnlyList<ResourceGovernanceRecord> records, IReadOnlyDictionary<InstanceId, long> checkpoints, CancellationToken token = default)
        {
            if (BeforeWrite is not null) await BeforeWrite(expected, checkpoints, Assert.Single(records));
            var result = await inner.AppendIfCurrentAsync(id, expected, records, checkpoints, token);
            if (AfterWrite is not null) await AfterWrite(result, Assert.Single(records));
            return result;
        }

        public ValueTask<ResourceGovernanceAppendResult> AppendAsync(ResourceGovernancePartitionId id, long expected,
            IReadOnlyList<ResourceGovernanceRecord> records, CancellationToken token = default) => inner.AppendAsync(id, expected, records, token);
        public ValueTask<ResourceGovernanceStream> LoadAsync(ResourceGovernancePartitionId id, CancellationToken token = default) =>
            inner.LoadAsync(id, token);
        public ValueTask<ResourceGovernanceHead> LoadLatestAsync(ResourceGovernancePartitionId id, CancellationToken token = default) =>
            inner.LoadLatestAsync(id, token);
    }

    private static async Task<ServiceProvider> CreateConfirmationHostAsync(string provider, string connection,
        DurableWorkflowDefinition<int, int> workflow, CancellationToken token)
    {
        var services = new ServiceCollection();
        Type options;
        switch (provider)
        {
            case "PostgreSql":
                services.AddOrcaCorePostgreSqlDurableProvider(new(connection, "public"));
                options = typeof(PostgreSqlDurableProviderOptions);
                break;
            case "SqlServer":
                services.AddOrcaCoreSqlServerDurableProvider(new(connection, "confirmation_race"));
                options = typeof(SqlServerDurableProviderOptions);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(provider));
        }
        var descriptor = services.Single(item => item.ServiceType == typeof(IDurableResourceGovernanceStore));
        services.AddSingleton(sp => new ConfirmationAppendGate((ICheckpointGuardedResourceGovernanceStore)descriptor.ImplementationFactory!(sp)));
        services.AddSingleton<IDurableResourceGovernanceStore>(sp => sp.GetRequiredService<ConfirmationAppendGate>());
        services.AddSingleton(new ProtectedStep());
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 1, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = Partition, Pools = [DurableResourcePoolDefinition.Create(Pool, 1, TimeSpan.FromHours(1))]
            }
        }).AddWorkflow(workflow);
        var host = services.BuildServiceProvider();
        // Only provider initialization runs. Background progression cannot consume B's promoted ticket.
        await host.GetServices<IHostedService>().Single(service => service.GetType().Assembly == options.Assembly).StartAsync(token);
        return host;
    }

    private static Task<IReadOnlyList<WorkflowEvent>> ReadJournalAsync(ServiceProvider host, Guid id, CancellationToken token) =>
        host.GetRequiredService<IWorkflowEventStore>().LoadTailAsync(new WorkflowStreamId(InstanceId.Parse(id.ToString())),
            StreamVersion.Empty, token);
    private static async Task<CheckpointWrite> ReadCheckpointAsync(ServiceProvider host, Guid id, CancellationToken token) =>
        (await host.GetRequiredService<IWorkflowEventStore>().LoadCheckpointAsync(InstanceId.Parse(id.ToString()), token)).Value;
    private static DurableOwnedObligationState Obligation(CheckpointWrite checkpoint) =>
        Assert.Single(DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations, o => o.Kind == DurableOwnedObligationKind.Resource);
    private static string[] EventIds(IEnumerable<WorkflowEvent> events) => events.Select(item => item.EventId.Value).ToArray();

    private static async Task AwaitSignalAsync(string directory, string name, CancellationToken token)
    {
        while (!File.Exists(Path.Combine(directory, name)))
            await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token);
    }
    private static async Task WritePhaseAsync<T>(string directory, string name, T value, CancellationToken token)
    {
        var path = Path.Combine(directory, name);
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(value), token);
        File.Move(path + ".tmp", path);
    }
    private static async Task<T> ReadPhaseAsync<T>(string directory, string name, CancellationToken token) =>
        JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(Path.Combine(directory, name), token))!;
}
