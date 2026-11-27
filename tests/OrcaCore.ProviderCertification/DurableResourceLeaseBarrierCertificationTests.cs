using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Hosting.Tests;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed class DurableResourceLeaseBarrierCertificationTests
{
    public enum Schedule { Uninterrupted, LegacyHeld, AfterAcquired, MissingTicket, ReplacedTicket, ReleasedAndReplaced }

    [Theory]
    [InlineData(Schedule.Uninterrupted)]
    [InlineData(Schedule.LegacyHeld)]
    [InlineData(Schedule.AfterAcquired)]
    [InlineData(Schedule.MissingTicket)]
    [InlineData(Schedule.ReplacedTicket)]
    [InlineData(Schedule.ReleasedAndReplaced)]
    public async Task ScopedLease_PreservesExactOwnershipAcrossCommitBoundaries(Schedule schedule)
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = InMemoryProviderPorts.Create();
        var workflows = provider.EventStore;
        var pools = provider.ResourcePoolStore;
        var pool = new ResourcePoolDefinition("database", 1, TimeSpan.FromMinutes(5));
        await pools.UpsertPoolAsync(pool, token);
        var request = ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(ResourcePoolName.Create(pool.Name)));
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<LeasedStep>())
            .End().Build();
        var steps = new Services();
        var gate = new RecordingGate(schedule == Schedule.LegacyHeld);
        var stopOnAcquired = schedule is not (Schedule.Uninterrupted or Schedule.LegacyHeld);
        var interruptedStore = new DagEventStoreProbe
        {
            Inner = workflows,
            AfterAppend = batch =>
            {
                if (batch.Events.Any(e => e is WorkflowResourcePoolAcquiredEvent)) throw new HostStopped();
            }
        };
        DurableWorkflowRuntime Runtime(IWorkflowEventStore events, IResourcePoolStore governance)
        {
            var processor = new DurableCommandProcessor(events, governance) { LeaseCertificationGate = gate };
            var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(steps), TimeProvider.System);
            runtime.RegisterDefinition(definition);
            return runtime;
        }

        var first = Runtime(stopOnAcquired ? interruptedStore : workflows, pools);
        var start = first.StartOrGetAsync<string, State>("barrier-certification", definition.DefinitionId,
            definition.DefinitionVersion, "start", token);
        if (schedule == Schedule.Uninterrupted) await start;
        else await Assert.ThrowsAsync<HostStopped>(() => start);

        var barriers = new[]
        {
            DurableResourceLeaseCommitBarrier.WorkflowPendingObligationCommitted,
            DurableResourceLeaseCommitBarrier.GovernanceReservationCommitted,
            DurableResourceLeaseCommitBarrier.WorkflowActivationCommitted,
            DurableResourceLeaseCommitBarrier.GovernanceOwnershipConfirmed
        };
        gate.Facts.Select(fact => fact.Barrier).Should().Equal(stopOnAcquired ? barriers.Take(2) : barriers);
        gate.Facts.Select(fact => fact.ObligationId).Distinct().Should().ContainSingle();
        gate.Facts.Select(fact => fact.LeaseProtectionToken).Distinct().Should().ContainSingle();
        gate.Facts.Skip(1).Should().OnlyContain(fact =>
            fact.Tickets.Count == 1 && fact.Tickets[0].Pool.Value == "database" &&
            fact.Tickets[0].Units == 1 && fact.Tickets[0].ProviderGeneration > 0);
        if (schedule == Schedule.Uninterrupted) return;

        Assert.Empty(steps.Invocations);
        var occurrence = gate.Facts[^1];
        var instanceId = occurrence.InstanceId;
        var stream = new WorkflowStreamId(instanceId);
        var checkpoint = (await workflows.LoadCheckpointAsync(instanceId, token)).Value;
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        var obligation = Assert.Single(envelope.OwnedObligations);
        var acquired = Assert.Single((await workflows.LoadTailAsync(stream, StreamVersion.Empty, token))
            .OfType<WorkflowResourcePoolAcquiredEvent>());
        var original = Assert.Single(acquired.Tickets);
        Assert.Equal(original, Assert.Single((await pools.GetPoolAsync(pool.Name, token)).Value.HeldTickets));
        Assert.Equal(instanceId, original.HolderInstanceId);
        Assert.Equal(obligation.HolderKey, original.HolderKey);
        Assert.Equal(stopOnAcquired ? "PendingCommit" : "Held", obligation.LeasePhase);
        if (schedule == Schedule.LegacyHeld)
        {
            // Older writers persisted ownership in the acquisition event and runtime snapshot
            // while omitting the ticket facts from the lexical scope's envelope.
            var legacy = envelope with { OwnedObligations = [obligation with { LeaseTickets = [] }] };
            var seeded = await workflows.AppendAsync(new ProviderCommitBatch
            {
                StreamId = stream, ExpectedVersion = checkpoint.StreamVersion,
                Checkpoint = checkpoint with { Payload = legacy.Serialize() }
            }, token);
            Assert.True(seeded.IsSuccess);
        }

        // Simulate a restarted governance provider missing its original state, without a
        // release receipt. A replacement can have the same holder, pool, units and generation.
        var missingState = schedule is Schedule.MissingTicket or Schedule.ReplacedTicket;
        using var restartedGovernance = missingState ? InMemoryProviderPorts.Create() : null;
        var live = restartedGovernance?.ResourcePoolStore ?? pools;
        ResourcePoolTicket? replacementTicket = null;
        if (missingState) await live.UpsertPoolAsync(pool, token);
        if (schedule == Schedule.ReleasedAndReplaced)
        {
            await live.ReleaseAsync(new ResourcePoolReleaseRequest(instanceId, original.HolderKey, DateTimeOffset.UtcNow), token);
        }
        if (schedule is Schedule.ReplacedTicket or Schedule.ReleasedAndReplaced)
        {
            var reacquired = await live.AcquireAsync(new ResourcePoolAcquireRequest(instanceId, original.HolderKey,
                [new(pool.Name, original.Count)], original.AcquiredAt, original.ExpiresAt)
                { FiberId = original.FiberId, ScopeId = original.ScopeId, ProtectionToken = occurrence.LeaseProtectionToken }, token);
            replacementTicket = Assert.Single(reacquired.Tickets);
            Assert.NotEqual(original.TicketId, replacementTicket.TicketId);
            if (missingState) Assert.Equal(original.ProviderGeneration, replacementTicket.ProviderGeneration);
            else
            {
                Assert.True(replacementTicket.ProviderGeneration > original.ProviderGeneration);
                var receipt = (await live.GetReleaseEvidenceAsync(occurrence.LeaseProtectionToken, token)).Value;
                Assert.Equal(original, Assert.Single(receipt.ReleasedTickets));
            }
        }

        var admissions = new List<CheckpointWrite>();
        var observed = new DagEventStoreProbe { Inner = workflows, AfterAppend = batch =>
        {
            if (batch.Checkpoint?.LastStepPath?.EndsWith(":attempt-admission", StringComparison.Ordinal) == true)
                admissions.Add(batch.Checkpoint);
        }};
        var restarted = Runtime(observed, live);
        if (schedule == Schedule.LegacyHeld)
        {
            await restarted.DriveAsync(instanceId, DurableDriveMode.Required, token);
            var repaired = (await workflows.LoadCheckpointAsync(instanceId, token)).Value;
            Assert.Equal("lease-reconciliation", repaired.LastStepPath);
            AssertHeld(repaired, obligation, original);
            Assert.Empty(admissions);
            Assert.Empty(steps.Invocations);
        }

        Assert.Equal(DurableSegmentOutcome.Terminal, (await restarted.DriveAsync(instanceId, token)).Outcome);
        var final = (await workflows.LoadCheckpointAsync(instanceId, token)).Value;
        var history = await workflows.LoadTailAsync(stream, StreamVersion.Empty, token);
        Assert.Single(history.OfType<WorkflowResourcePoolAcquiredEvent>());
        if (missingState || schedule == Schedule.ReleasedAndReplaced)
        {
            Assert.Empty(admissions);
            Assert.Empty(steps.Invocations);
            Assert.Equal(WorkflowInstanceStatus.Failed, final.Status);
            Assert.Contains("WF-LEASE-LOST", final.ErrorSummary);
            Assert.Equal("LeaseLost", Assert.Single(DurableExecutionEnvelopeV2.Deserialize(final.Payload).OwnedObligations).LeasePhase);
            Assert.Empty(history.OfType<WorkflowResourcePoolReleasedEvent>());
            Assert.Equal(replacementTicket is null ? [] : new[] { replacementTicket },
                (await live.GetPoolAsync(pool.Name, token)).Value.HeldTickets);
        }
        else
        {
            AssertHeld(Assert.Single(admissions), obligation, original);
            Assert.Equal(occurrence.LeaseProtectionToken, Assert.Single(steps.Invocations));
            Assert.Equal(WorkflowInstanceStatus.Completed, final.Status);
            Assert.Equal(original, Assert.Single(Assert.Single(history.OfType<WorkflowResourcePoolReleasedEvent>()).Tickets));
            Assert.Empty((await live.GetPoolAsync(pool.Name, token)).Value.HeldTickets);
        }
    }

    private static void AssertHeld(CheckpointWrite checkpoint, DurableOwnedObligationState expected, ResourcePoolTicket ticket)
    {
        var lease = Assert.Single(DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations);
        Assert.Equal("Held", lease.LeasePhase);
        Assert.Equal(expected.ObligationId, lease.ObligationId);
        Assert.Equal(expected.ProtectionToken, lease.ProtectionToken);
        Assert.Equal(ticket.HolderKey, lease.HolderKey);
        Assert.Equal(new DurableLeaseTicketState
        {
            TicketId = ticket.TicketId.ToString("N"), PoolName = ticket.PoolName,
            Units = ticket.Count, ProviderGeneration = ticket.ProviderGeneration,
            ReviewDeadline = ticket.ReviewDeadline, ReviewMarked = ticket.ReviewMarked
        }, Assert.Single(lease.LeaseTickets));
    }

    private sealed class HostStopped : Exception;

    private sealed class RecordingGate(bool stopAfterOwnershipConfirmed) : IDurableResourceLeaseCertificationGate
    {
        internal List<DurableResourceLeaseCommitBarrierFact> Facts { get; } = [];

        public ValueTask OnPostCommitAsync(DurableResourceLeaseCommitBarrierFact fact, CancellationToken cancellationToken = default)
        {
            Facts.Add(fact);
            if (stopAfterOwnershipConfirmed && fact.Barrier == DurableResourceLeaseCommitBarrier.GovernanceOwnershipConfirmed)
                throw new HostStopped();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class State;

    private sealed class LeasedStep(List<LeaseProtectionToken?> invocations) : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
        {
            invocations.Add(context.ResourceLease?.ProtectionToken);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class Services : IServiceProvider
    {
        internal List<LeaseProtectionToken?> Invocations { get; } = [];
        public object? GetService(Type serviceType) => serviceType == typeof(LeasedStep) ? new LeasedStep(Invocations) : null;
    }
}
