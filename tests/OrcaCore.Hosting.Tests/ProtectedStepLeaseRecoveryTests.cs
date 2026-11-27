using System.Text.Json;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class ProtectedStepLeaseRecoveryTests
{
    private static readonly ResourcePoolName Pool = ResourcePoolName.Create("database");
    private static readonly ResourceGovernancePartitionId Partition = ResourceGovernancePartitionId.Create("late-protected-step");
    private static readonly StopConfirmationId Confirmation = StopConfirmationId.Create("trusted-stop");

    [Theory(Explicit = true)]
    [InlineData("Stopped")]
    [InlineData("Fenced")]
    [InlineData("EmptyLookup")]
    [Trait(Traits.Container, "PostgreSql")]
    public Task PostgreSqlLateProtectedStep(string schedule) => RunAsync("PostgreSql", schedule);

    [Theory(Explicit = true)]
    [InlineData("Stopped")]
    [InlineData("Fenced")]
    [InlineData("EmptyLookup")]
    [Trait(Traits.Container, "SqlServer")]
    public Task SqlServerLateProtectedStep(string schedule) => RunAsync("SqlServer", schedule);

    private static async Task RunAsync(string provider, string schedule)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        await using IDatabaseContainer database = provider == "PostgreSql" ? new PostgreSqlBuilder("postgres:17-alpine").Build()
            : new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await database.StartAsync(token);
        var resource = new JobResource(provider, database.GetConnectionString());
        await resource.InitializeAsync(token);
        var directory = Directory.CreateTempSubdirectory("orca-protected-step-");
        try
        {
            await using var worker = new OwnerProcess(provider, database.GetConnectionString(), directory.FullName);
            var a = await worker.AtGateAsync(token);
            await RunOverlapAsync(provider, database.GetConnectionString(), schedule, resource, worker, a, token);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task RunOverlapAsync(string provider, string connection, string schedule, JobResource resource,
        OwnerProcess worker, Admission a, CancellationToken token)
    {
        var joinedBeforeConfirmation = schedule == "Stopped";
        var fenced = schedule == "Fenced";
        var unsafeCaller = schedule == "EmptyLookup";
        Assert.True(joinedBeforeConfirmation || fenced || unsafeCaller);
        var activeB = new TaskCompletionSource<Admission>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var step = new ProtectedStep(async b =>
        {
            Assert.NotEqual(a.Instance, b.Instance);
            await resource.SubmitAsync(b, token);
            activeB.SetResult(b);
            await finishB.Task.WaitAsync(token);
            await resource.FinishAsync(b, token);
        });
        var definition = Definition();
        await using var host = await HostAsync(provider, connection, step, definition, token);
        var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference);
        var pools = host.GetRequiredService<IResourcePoolStore>();
        var recovery = host.GetRequiredService<IDurableResourceLeaseRecovery>();
        var diagnostics = host.GetRequiredService<IDurableResourceLeaseDiagnostics>();
        var governance = host.GetRequiredService<IDurableResourceGovernanceStore>();
        var owner = InstanceId.Parse(a.Instance.ToString());
        var occurrence = LeaseProtectionToken.Parse(a.Lease);
        var admitted = await CheckpointAsync(host, owner, token);
        var fiber = Assert.Single(DurableExecutionEnvelopeV2.Deserialize(admitted.Payload).Fibers, f => f.AttemptInFlight);
        Assert.EndsWith(":attempt-admission", admitted.LastStepPath, StringComparison.Ordinal);
        Assert.Equal(a.Operation, fiber.LogicalOperationKey);
        Assert.Equal(1, a.Attempt);
        Assert.Equal(a.Attempt, fiber.RetryAttempt);
        var held = Lease(admitted);
        Assert.Equal("Held", held.LeasePhase);
        Assert.Equal(a.Lease, held.ProtectionToken);
        var original = Assert.Single((await pools.GetPoolAsync(Pool.Value, token)).Value.HeldTickets);
        Assert.Equal(owner, original.HolderInstanceId);
        Assert.Equal(held.HolderKey, original.HolderKey);
        Assert.Equal(Guid.Parse(Assert.Single(held.LeaseTickets).TicketId), original.TicketId);
        Assert.Equal(1, original.ProviderGeneration);
        Assert.Empty(await resource.ReadAsync(token));

        // First-call resolution on a fresh facade re-drives SQL. A second lookup on the
        // same facade returns its cached binding without running another execution segment.
        async Task<InstanceId> StartWaiterAsync()
        {
            await using var executor = await HostAsync(provider, connection, step, definition, token);
            var fresh = executor.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference);
            return (await fresh.StartOrGetAsync(2, StartIdempotencyKey.Create("waiter"), token)).GetHandleOrThrow().InstanceId;
        }
        var waiter = await StartWaiterAsync();
        var queuedCheckpoint = await CheckpointAsync(host, waiter, token);
        while (queuedCheckpoint.LastStepPath?.EndsWith(":lease-queued", StringComparison.Ordinal) != true)
        {
            Assert.Equal(WorkflowInstanceStatus.Running, queuedCheckpoint.Status);
            Assert.Equal(waiter, await StartWaiterAsync());
            queuedCheckpoint = await CheckpointAsync(host, waiter, token);
        }
        var queued = Lease(queuedCheckpoint);
        Assert.Equal("Queued", queued.LeasePhase);
        Assert.NotEqual(a.Lease, queued.ProtectionToken);
        var waiting = Assert.Single((await pools.GetPoolAsync(Pool.Value, token)).Value.QueuedWaiters);
        Assert.Equal(waiter, waiting.HolderInstanceId);
        Assert.Equal(queued.HolderKey, waiting.HolderKey);

        var instance = await handle.GetInstanceAsync(owner, token);
        Assert.Equal(WorkflowCancellationRequestStatus.Requested, await instance.RequestCancellationAsync(token));
        var cancelled = await CheckpointAsync(host, owner, token);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, cancelled.Status);
        var journal = await JournalAsync(host, owner, token);
        Assert.Single(journal.OfType<WorkflowCancellationRequestedEvent>());
        Assert.Equal(WorkflowInstanceStatus.Cancelled, Assert.Single(journal.OfType<WorkflowTerminalEvent>()).Status);
        var beforeConfirm = (await governance.LoadLatestAsync(Partition, token)).Version;

        async Task CheckQuarantineAsync()
        {
            var pool = (await pools.GetPoolAsync(Pool.Value, token)).Value;
            Assert.Equal(1, pool.Capacity);
            Assert.Equal(0, pool.AvailableCapacity);
            Assert.Equal(original, Assert.Single(pool.HeldTickets));
            Assert.Equal(waiting.WaiterId, Assert.Single(pool.QueuedWaiters).WaiterId);
            Assert.Equal("Queued", Lease(await CheckpointAsync(host, waiter, token)).LeasePhase);
            var snapshot = await diagnostics.GetAsync(occurrence, token);
            Assert.Equal(DurableResourceLeaseObligationStatus.Quarantined, snapshot!.Status);
            Assert.Null(snapshot.AcceptedConfirmationId);
            Assert.Equal(original.TicketId, Guid.Parse(Assert.Single(snapshot.Tickets).TicketId));
            Assert.False((await pools.GetReleaseEvidenceAsync(occurrence, token)).HasValue);
            Assert.Equal(beforeConfirm, (await governance.LoadLatestAsync(Partition, token)).Version);
            Assert.Equal(cancelled.Payload, (await CheckpointAsync(host, owner, token)).Payload);
            Assert.Equal(journal.Select(e => e.EventId), (await JournalAsync(host, owner, token)).Select(e => e.EventId));
            Assert.False(activeB.Task.IsCompleted);
        }

        await CheckQuarantineAsync();
        Assert.Empty(await resource.ReadAsync(token)); // Both workers are live, A can still submit.
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine($"A={JsonSerializer.Serialize(a)} controller={Environment.ProcessId} admissionVersion={admitted.StreamVersion.Value} " +
            $"A.ticket={original.TicketId} B={waiter} B.waiter={waiting.WaiterId} B.token={queued.ProtectionToken} " +
            $"cancelVersion={cancelled.StreamVersion.Value} quarantineVersion={beforeConfirm} submissions=0 capacity=0");
        if (fenced)
        {
            var fence = await resource.FenceAsync(a.Lease, token);
            Assert.Equal(1, fence);
            Assert.Empty(await resource.ReadAsync(token));
            await CheckQuarantineAsync();
            output.WriteLine($"Resource fence committed at revision {fence} for {a.Lease} with zero active jobs; A is still gated.");
        }
        else if (joinedBeforeConfirmation)
        {
            await worker.ResumeAndJoinAsync(a, token);
            var submitted = Assert.Single(await resource.ReadAsync(token));
            AssertJob(a, submitted, accepted: true, active: true);
            await CheckQuarantineAsync();
            // Only A's process ever submits for this occurrence. Its exit joins that submitter,
            // but the resource job outlives its caller and must independently finish.
            await resource.FinishAsync(a, token);
            AssertJob(a, Assert.Single(await resource.ReadAsync(token)), accepted: true, active: false);
            await CheckQuarantineAsync();
            output.WriteLine($"Late A submission accepted at revision {submitted.Revision}; owner returned and joined; " +
                "active job observed after exit, then finished; exact A ticket retained and B still queued.");
        }

        if (unsafeCaller)
            output.WriteLine("Unsafe caller control: confirming from an empty lookup while A is still gated and can submit. No fence installed.");

        Assert.Equal(ProtectedWorkStopConfirmationStatus.Released,
            await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, Confirmation, token));
        var confirmed = await CheckpointAsync(host, owner, token);
        var confirmedJournal = await JournalAsync(host, owner, token);
        Assert.Equal(journal.Select(e => e.EventId), confirmedJournal.Take(journal.Count).Select(e => e.EventId));
        Assert.IsType<WorkflowExecutionPositionChangedEvent>(Assert.Single(confirmedJournal.Skip(journal.Count)));
        Assert.Equal(WorkflowInstanceStatus.Cancelled, confirmed.Status);
        var cancelledState = DurableExecutionEnvelopeV2.Deserialize(confirmed.Payload);
        Assert.Null(cancelledState.Output);
        Assert.Equal(1, JsonSerializer.Deserialize<int>(cancelledState.StatePayload));
        Assert.DoesNotContain(confirmedJournal.OfType<WorkflowStepCompletedEvent>(), e => e.StepPath == "root/1/lease/0");
        Assert.Equal("Released", Lease(confirmed).LeasePhase);
        Assert.Equal(Confirmation.Value, Lease(confirmed).AcceptedConfirmationId);
        var receipt = (await pools.GetReleaseEvidenceAsync(occurrence, token)).Value;
        Assert.Equal(Confirmation, receipt.ConfirmationId);
        Assert.Equal(original, Assert.Single(receipt.ReleasedTickets));
        var transferred = (await pools.GetPoolAsync(Pool.Value, token)).Value;
        Assert.Empty(transferred.QueuedWaiters);
        Assert.Equal(0, transferred.AvailableCapacity);
        var successor = Assert.Single(transferred.HeldTickets);
        Assert.NotEqual(original.TicketId, successor.TicketId);
        Assert.Equal(waiter, successor.HolderInstanceId);
        Assert.Equal(queued.HolderKey, successor.HolderKey);
        Assert.Equal(2, successor.ProviderGeneration);
        Assert.Equal(1, successor.Count);

        // A normal re-drive reconciles B's exact transferred ticket and runs its protected body.
        async Task WakeWaiterAsync()
        {
            do
            {
                Assert.Equal(waiter, await StartWaiterAsync());
                if (!activeB.Task.IsCompleted)
                {
                    var current = await CheckpointAsync(host, waiter, token);
                    Assert.True(current.Status is WorkflowInstanceStatus.Running or WorkflowInstanceStatus.Waiting,
                        $"Waiter stopped at {current.LastStepPath}: {current.Status} {current.ErrorSummary}");
                }
            } while (!activeB.Task.IsCompleted);
        }
        var wake = WakeWaiterAsync();
        try
        {
            if (await Task.WhenAny(activeB.Task, wake).WaitAsync(token) == wake)
                await wake;
            var b = await activeB.Task.WaitAsync(token);
            Assert.Equal(waiter.Value, b.Instance);
            Assert.Equal(queued.ProtectionToken, b.Lease);
            AssertJob(b, Assert.Single(await resource.ReadAsync(token), job => job.Lease == b.Lease), true, true);
            var bHeld = Lease(await CheckpointAsync(host, waiter, token));
            Assert.Equal("Held", bHeld.LeasePhase);
            Assert.Equal(b.Lease, bHeld.ProtectionToken);
            var bJournal = await JournalAsync(host, waiter, token);
            var acquired = Assert.Single(Assert.Single(bJournal.OfType<WorkflowResourcePoolAcquiredEvent>()).Tickets);
            Assert.Equal(successor, acquired);
            var head = (await governance.LoadLatestAsync(Partition, token)).Version;
            Assert.Equal(successor, Assert.Single((await pools.GetPoolAsync(Pool.Value, token)).Value.HeldTickets));
            if (!joinedBeforeConfirmation)
                await worker.ResumeAndJoinAsync(a, token);
            var jobs = await resource.ReadAsync(token);
            Assert.Equal(2, jobs.Count);
            var stale = Assert.Single(jobs, job => job.Lease == a.Lease);
            AssertJob(a, stale, accepted: !fenced, active: unsafeCaller);
            var live = Assert.Single(jobs, job => job.Lease == b.Lease);
            AssertJob(b, live, true, true);
            Assert.Equal(!joinedBeforeConfirmation, live.Revision < stale.Revision);
            var activeTokens = jobs.Where(job => job.Active).Select(job => job.Lease).ToArray();
            Assert.Equal(unsafeCaller ? new[] { b.Lease, a.Lease } : [b.Lease], activeTokens);

            Assert.Equal(owner, (await handle.StartOrGetAsync(1, StartIdempotencyKey.Create("owner"), token)).GetHandleOrThrow().InstanceId);
            Assert.Equal(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed,
                await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, Confirmation, token));
            Assert.Equal(confirmed.Payload, (await CheckpointAsync(host, owner, token)).Payload);
            Assert.Equal(confirmedJournal.Select(e => e.EventId), (await JournalAsync(host, owner, token)).Select(e => e.EventId));
            Assert.Equal(bJournal.Select(e => e.EventId), (await JournalAsync(host, waiter, token)).Select(e => e.EventId));
            Assert.Equal(head, (await governance.LoadLatestAsync(Partition, token)).Version);
            var livePool = (await pools.GetPoolAsync(Pool.Value, token)).Value;
            Assert.Equal(1, livePool.Capacity);
            Assert.Equal(0, livePool.AvailableCapacity);
            Assert.Empty(livePool.QueuedWaiters);
            Assert.Equal(successor, Assert.Single(livePool.HeldTickets));
            Assert.Equal(jobs, await resource.ReadAsync(token));
            var operations = await governance.LoadAsync(Partition, token);
            var releases = operations.Records.Where(record =>
            {
                using var document = JsonDocument.Parse(record.Payload);
                return document.RootElement.GetProperty("Operation").GetString() == "confirmed-holder-released";
            });
            Assert.Single(releases);
            output.WriteLine($"confirmation={Confirmation.Value} ownerTombstone={confirmed.StreamVersion.Value} grants=1 " +
                $"B.ticket={successor.TicketId} generation={successor.ProviderGeneration} jobs={JsonSerializer.Serialize(jobs)} " +
                $"activeExternalJobs={activeTokens.Length} livePoolTickets={livePool.HeldTickets.Count} " +
                $"B.heldCheckpointTicketCount={bHeld.LeaseTickets.Count} B.acquiredEventTicketCount=1 B.liveTicketCount=1 " +
                $"A.userStepCompletionCount=0 A.state=1 A.output=null replay=AlreadyConfirmed governanceHead={head}");
            if (unsafeCaller)
                await resource.FinishAsync(a, token);
        }
        finally
        {
            finishB.TrySetResult();
            await wake;
        }
    }

    private static void AssertJob(Admission admission, Job job, bool accepted, bool active) =>
        Assert.Equal(new Job(job.Revision, admission.Lease, admission.Operation, admission.Attempt, admission.Pid, accepted, active), job);

    private static async Task<CheckpointWrite> CheckpointAsync(ServiceProvider host, InstanceId id, CancellationToken token) =>
        (await host.GetRequiredService<IWorkflowEventStore>().LoadCheckpointAsync(id, token)).Value;

    private static Task<IReadOnlyList<WorkflowEvent>> JournalAsync(ServiceProvider host, InstanceId id, CancellationToken token) =>
        host.GetRequiredService<IWorkflowEventStore>().LoadTailAsync(new WorkflowStreamId(id), StreamVersion.Empty, token);

    private static DurableOwnedObligationState Lease(CheckpointWrite checkpoint) =>
        Assert.Single(DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations,
            obligation => obligation.Kind == DurableOwnedObligationKind.Resource);
}
