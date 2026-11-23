using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Hosting.Tests;
using Xunit;

namespace OrcaCore.ProviderCertification;

public static partial class DurableResourceGrantRaceCertification
{
    public enum RecoveryCase { ResumeReserved, CloseReserved, CloseEmpty, CloseJournalled, ResumePromoted, CloseQueued, CancelEmpty, ReceiptReplay }

    public static async Task RecoverAsync(Func<IServiceCollection> services, Type providerType, RecoveryCase schedule)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(1));
        var ct = deadline.Token;
        var partition = ResourceGovernancePartitionId.Create($"orphan-{Guid.CreateVersion7():N}");
        var definition = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(v => v)
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(ResourcePoolName.Create("slot"))),
                scope => scope.Then<CountedStep>()).End().Build();
        var rival = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var stopAt = schedule is RecoveryCase.ResumeReserved or RecoveryCase.CloseReserved or RecoveryCase.ReceiptReplay
            ? DurableResourceLeaseCommitBarrier.GovernanceReservationCommitted
            : DurableResourceLeaseCommitBarrier.WorkflowPendingObligationCommitted;
        var gate = new InterruptedGrant(stopAt);
        await using (var seed = BuildHost(services(), partition))
        {
            await seed.GetServices<IHostedService>().Single(s => s.GetType().Assembly == providerType.Assembly).StartAsync(ct);
            if (schedule is RecoveryCase.ResumePromoted or RecoveryCase.CloseQueued)
                (await seed.GetRequiredService<IResourcePoolStore>().AcquireAsync(Request(blocker, "blocker"), ct))
                    .Status.Should().Be(ResourcePoolAcquireStatus.Granted);
            var runtime = CreateRuntime(seed, gate);
            runtime.RegisterDefinition(definition);
            await Assert.ThrowsAsync<WorkerStopped>(() => runtime.StartOrGetAsync<int, int>($"start-{partition}",
                definition.DefinitionId, definition.DefinitionVersion, 0, ct));
            seed.GetRequiredService<CountedStep>().Calls.Should().Be(0);
        }

        var id = gate.Fact!.InstanceId;
        var token = gate.Fact.LeaseProtectionToken;
        string holder;
        ResourcePoolTicket? reserved;
        await using (var interrupted = BuildHost(services(), partition))
        {
            var events = interrupted.GetRequiredService<IWorkflowEventStore>();
            var pools = interrupted.GetRequiredService<IResourcePoolStore>();
            var pending = await ObligationAsync(events, id, ct);
            holder = pending.HolderKey!;
            if (schedule is RecoveryCase.CloseJournalled or RecoveryCase.ResumePromoted or RecoveryCase.CloseQueued)
            {
                var probe = new DagEventStoreProbe { Inner = events, AfterAppend = batch =>
                {
                    if (batch.Events.Any(e => e is WorkflowResourcePoolAcquiredEvent or WorkflowResourcePoolQueuedEvent))
                        throw new WorkerStopped();
                }};
                var runtime = CreateRuntime(interrupted, NullDurableResourceLeaseCertificationGate.Instance, events: probe);
                runtime.RegisterDefinition(definition);
                await Assert.ThrowsAsync<WorkerStopped>(() => runtime.DriveAsync(id, ct));
                (await ObligationAsync(events, id, ct)).LeasePhase.Should().Be("PendingCommit");
                if (schedule == RecoveryCase.ResumePromoted)
                {
                    (await pools.GetPoolAsync("slot", ct)).Value.QueuedWaiters.Should().ContainSingle().Which.HolderInstanceId.Should().Be(id);
                    await pools.ReleaseAsync(new ResourcePoolReleaseRequest(blocker, "blocker", DateTimeOffset.UtcNow), ct);
                }
            }
            reserved = (await pools.GetPoolAsync("slot", ct)).Value.HeldTickets.SingleOrDefault(t => t.HolderInstanceId.Equals(id));
            if (schedule is RecoveryCase.CloseEmpty or RecoveryCase.CloseQueued or RecoveryCase.CancelEmpty) reserved.Should().BeNull();
            else
            {
                reserved!.HolderInstanceId.Should().Be(id);
                (await pools.AcquireAsync(Request(rival, "rival"), ct)).Status.Should().Be(ResourcePoolAcquireStatus.Queued);
            }

            if (schedule is RecoveryCase.CloseEmpty or RecoveryCase.CloseReserved or RecoveryCase.CloseJournalled or RecoveryCase.CloseQueued or RecoveryCase.CancelEmpty or RecoveryCase.ReceiptReplay)
            {
                // Read a live checkpoint, then hold the direct acquire (or queue promotion) before its governance write.
                var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var staleProbe = new DagEventStoreProbe { Inner = events, AfterCheckpointRead = async token =>
                {
                    admitted.TrySetResult();
                    await proceed.Task.WaitAsync(token);
                }};
                await using var competing = BuildHost(services().AddSingleton<IWorkflowEventStore>(staleProbe), partition);
                var competingPools = competing.GetRequiredService<IResourcePoolStore>();
                var lateAcquire = schedule is RecoveryCase.CloseEmpty or RecoveryCase.CancelEmpty
                    ? competingPools.AcquireAsync(Request(id, holder) with { ProtectionToken = token }, ct) : null;
                var latePromotion = schedule == RecoveryCase.CloseQueued
                    ? competingPools.ReleaseAsync(new ResourcePoolReleaseRequest(blocker, "blocker", DateTimeOffset.UtcNow), ct) : null;
                if (lateAcquire is not null || latePromotion is not null) await admitted.Task.WaitAsync(ct);

                // The database committed termination. Losing its reply stops this host before any governance close.
                var probe = new DagEventStoreProbe { Inner = events, AfterAppend = batch =>
                {
                    if (batch.Events.Any(e => e is WorkflowTerminalEvent ||
                        schedule == RecoveryCase.CancelEmpty && e is WorkflowCancellationRequestedEvent)) throw new WorkerStopped();
                }};
                var terminator = new DurableCommandProcessor(probe, pools);
                if (schedule == RecoveryCase.CancelEmpty)
                    await Assert.ThrowsAsync<WorkerStopped>(() => terminator.ProcessAsync(new CancelWorkflowCommand
                    {
                        InstanceId = id, CommandId = CommandId.New(), RequestedAt = DateTimeOffset.UtcNow
                    }, ct));
                else await Assert.ThrowsAsync<WorkerStopped>(() => terminator.ProcessAsync(new TerminateWorkflowCommand
                {
                    InstanceId = id, CommandId = CommandId.New(), RequestedAt = DateTimeOffset.UtcNow
                }, ct));
                (await events.LoadCheckpointAsync(id, ct)).Value.Status.Should().Be(schedule == RecoveryCase.CancelEmpty
                    ? WorkflowInstanceStatus.CancellationRequested : WorkflowInstanceStatus.Terminated);
                proceed.TrySetResult();
                if (lateAcquire is not null) (await lateAcquire).Status.Should().Be(ResourcePoolAcquireStatus.Rejected);
                if (latePromotion is not null) await latePromotion;
                if (schedule == RecoveryCase.CancelEmpty)
                    await Assert.ThrowsAsync<WorkerStopped>(() => terminator.FinalizeCancellationAsync(id, DateTimeOffset.UtcNow, ct));
                (await pools.GetReleaseEvidenceAsync(token, ct)).HasValue.Should().BeFalse();
                (await pools.GetPoolAsync("slot", ct)).Value.HeldTickets.Select(t => t.TicketId)
                    .Should().Equal(reserved is null ? [] : new[] { reserved.TicketId });
            }
        }

        ResourcePoolTicket? transferred = null;
        if (schedule == RecoveryCase.ReceiptReplay)
        {
            await using var closing = BuildHost(services(), partition);
            var events = closing.GetRequiredService<IWorkflowEventStore>();
            var pools = closing.GetRequiredService<IResourcePoolStore>();
            var probe = new DagEventStoreProbe { Inner = events, BeforeAppend = batch =>
            {
                if (batch.Events.Any(e => e is WorkflowResourcePoolReleasedEvent)) throw new WorkerStopped();
            }};
            var runtime = CreateRuntime(closing, NullDurableResourceLeaseCertificationGate.Instance, events: probe);
            runtime.RegisterDefinition(definition);
            await Assert.ThrowsAsync<WorkerStopped>(() => runtime.DriveAsync(id, ct));
            (await pools.GetReleaseEvidenceAsync(token, ct)).Value.ReleasedTickets.Single().TicketId.Should().Be(reserved!.TicketId);
            (await events.LoadTailAsync(new WorkflowStreamId(id), StreamVersion.Empty, ct)).OfType<WorkflowResourcePoolReleasedEvent>().Should().BeEmpty();
            transferred = (await pools.GetPoolAsync("slot", ct)).Value.HeldTickets.Single();
            transferred.HolderInstanceId.Should().Be(rival);
        }

        await using var replacement = BuildHost(services(), partition);
        var store = replacement.GetRequiredService<IWorkflowEventStore>();
        var governance = replacement.GetRequiredService<IResourcePoolStore>();
        var resume = schedule is RecoveryCase.ResumeReserved or RecoveryCase.ResumePromoted;
        if (resume)
        {
            var runtime = CreateRuntime(replacement, NullDurableResourceLeaseCertificationGate.Instance);
            runtime.RegisterDefinition(definition);
            (await runtime.DriveAsync(id, ct)).Outcome.Should().Be(DurableSegmentOutcome.Terminal);
            var history = await store.LoadTailAsync(new WorkflowStreamId(id), StreamVersion.Empty, ct);
            history.OfType<WorkflowResourcePoolAcquiredEvent>().Should().ContainSingle().Which.Tickets.Single().TicketId.Should().Be(reserved!.TicketId);
            history.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle().Which.Tickets.Single().TicketId.Should().Be(reserved.TicketId);
            history.OfType<WorkflowCompletedEvent>().Should().ContainSingle();
        }
        else
        {
            // Only the production operational sweep runs. No client reopens or drives this terminal workflow.
            var sweep = replacement.GetServices<IHostedService>().Single(s => s.GetType().Name == "OrcaCoreOperationalSweepHostedService");
            await sweep.StartAsync(ct);
            try
            {
                var expected = reserved is null ? "CancelledBeforeGrant" : "Released";
                while ((await ObligationAsync(store, id, ct)).LeasePhase != expected)
                    await Task.Delay(TimeSpan.FromMilliseconds(20), TimeProvider.System, ct);
            }
            finally { await sweep.StopAsync(ct); }
            var history = await store.LoadTailAsync(new WorkflowStreamId(id), StreamVersion.Empty, ct);
            var release = history.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle().Which;
            release.HolderKey.Should().Be(holder);
            release.Tickets.Select(t => (t.TicketId, t.ProviderGeneration)).Should().Equal(
                reserved is null ? [] : new[] { (reserved.TicketId, reserved.ProviderGeneration) });
            var closed = await ObligationAsync(store, id, ct);
            closed.LeaseTickets.Select(t => t.TicketId).Should().Equal(reserved is null ? [] : new[] { reserved.TicketId.ToString("N") });
            (await store.LoadCheckpointAsync(id, ct)).Value.Status.Should().Be(schedule == RecoveryCase.CancelEmpty
                ? WorkflowInstanceStatus.Cancelled : WorkflowInstanceStatus.Terminated);
        }

        replacement.GetRequiredService<CountedStep>().Calls.Should().Be(resume ? 1 : 0);
        var receipt = (await governance.GetReleaseEvidenceAsync(token, ct)).Value;
        receipt.ReleasedTickets.Select(t => t.TicketId).Should().Equal(reserved is null ? [] : new[] { reserved.TicketId });
        var stale = await governance.AcquireAsync(Request(id, holder) with { ProtectionToken = token }, ct);
        stale.Status.Should().Be(ResourcePoolAcquireStatus.Rejected);
        var after = (await governance.GetPoolAsync("slot", ct)).Value;
        after.QueuedWaiters.Should().BeEmpty();
        if (reserved is null) after.HeldTickets.Should().BeEmpty();
        else
        {
            var next = after.HeldTickets.Should().ContainSingle().Which;
            next.HolderInstanceId.Should().Be(rival);
            next.ProviderGeneration.Should().BeGreaterThan(reserved.ProviderGeneration);
            next.TicketId.Should().NotBe(reserved.TicketId);
            if (transferred is not null) next.TicketId.Should().Be(transferred.TicketId);
        }
    }

    private static ResourcePoolAcquireRequest Request(InstanceId id, string holder) =>
        new(id, holder, [new ResourcePoolRequirement("slot", 1)], DateTimeOffset.UtcNow, null);

    private static async Task<DurableOwnedObligationState> ObligationAsync(IWorkflowEventStore events, InstanceId id, CancellationToken ct) =>
        DurableExecutionEnvelopeV2.Deserialize((await events.LoadCheckpointAsync(id, ct)).Value.Payload).OwnedObligations.Single();

    private sealed class WorkerStopped : Exception;

    private sealed class InterruptedGrant(DurableResourceLeaseCommitBarrier stopAt) : IDurableResourceLeaseCertificationGate
    {
        internal DurableResourceLeaseCommitBarrierFact? Fact { get; private set; }
        public ValueTask OnPostCommitAsync(DurableResourceLeaseCommitBarrierFact fact, CancellationToken cancellationToken = default)
        {
            if (fact.Barrier == stopAt) { Fact = fact; throw new WorkerStopped(); }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CountedStep : IStep<int>
    {
        internal int Calls { get; private set; }
        public ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
