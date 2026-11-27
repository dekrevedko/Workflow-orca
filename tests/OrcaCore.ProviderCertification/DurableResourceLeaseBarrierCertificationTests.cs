using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Tests;
using OrcaCore.Providers.InMemory;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed partial class DurableResourceLeaseBarrierCertificationTests
{
    public enum Schedule
    {
        Uninterrupted, LegacyHeld, AfterAcquired, MissingTicket, ReplacedTicket, ReleasedAndReplaced, HeldReleasedAndReplaced
    }

    [Theory]
    [InlineData(Schedule.Uninterrupted)]
    [InlineData(Schedule.LegacyHeld)]
    [InlineData(Schedule.AfterAcquired)]
    [InlineData(Schedule.MissingTicket)]
    [InlineData(Schedule.ReplacedTicket)]
    [InlineData(Schedule.ReleasedAndReplaced)]
    [InlineData(Schedule.HeldReleasedAndReplaced)]
    [InlineData(Schedule.HeldReleasedAndReplaced, true)]
    [InlineData(Schedule.HeldReleasedAndReplaced, true, true)]
    public async Task ScopedLease_PreservesExactOwnershipAcrossCommitBoundaries(
        Schedule schedule, bool reopen = false, bool replayConfirmation = false)
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = InMemoryProviderPorts.Create();
        var workflows = provider.EventStore;
        var pools = provider.ResourcePoolStore;
        var pool = new ResourcePoolDefinition("database", 1, TimeSpan.FromMinutes(5));
        await pools.UpsertPoolAsync(pool, token);
        var definition = Definition(ResourcePoolName.Create(pool.Name));
        var steps = new Services();
        var stopOnHeld = schedule is Schedule.LegacyHeld or Schedule.HeldReleasedAndReplaced;
        var gate = new RecordingGate(stopOnHeld);
        var stopOnAcquired = schedule != Schedule.Uninterrupted && !stopOnHeld;
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
            var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(steps), TimeProvider.System,
                projectionStore: provider.ProjectionStore);
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
        var releaseAndReplace = schedule is Schedule.ReleasedAndReplaced or Schedule.HeldReleasedAndReplaced;
        var confirmation = StopConfirmationId.Create("retained-release");
        if (replayConfirmation)
        {
            Assert.Equal(ResourcePoolStopConfirmationStatus.Released, await live.ConfirmAndReleaseAsync(
                new(instanceId, original.HolderKey, occurrence.LeaseProtectionToken, confirmation, DateTimeOffset.UtcNow), token));
        }
        else if (releaseAndReplace)
        {
            await live.ReleaseAsync(new ResourcePoolReleaseRequest(instanceId, original.HolderKey, DateTimeOffset.UtcNow), token);
        }
        if (schedule == Schedule.ReplacedTicket || releaseAndReplace)
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
                Assert.Equal(replayConfirmation ? confirmation : null, receipt.ConfirmationId);
            }
        }

        var admissions = new List<CheckpointWrite>();
        var observed = new DagEventStoreProbe { Inner = workflows, AfterAppend = batch =>
        {
            if (batch.Checkpoint?.LastStepPath?.EndsWith(":attempt-admission", StringComparison.Ordinal) == true)
                admissions.Add(batch.Checkpoint);
        }};
        // Reopen uses the provider's real idempotency and projection ports.
        var restarted = Runtime(reopen ? workflows : observed, live);
        if (replayConfirmation)
        {
            var recovery = new DurableResourceLeaseRecovery(new DurableCommandProcessor(workflows, live), provider.ProjectionStore);
            Assert.Equal(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed,
                await recovery.ConfirmProtectedWorkStoppedAsync(occurrence.LeaseProtectionToken, confirmation, token));
            var afterConfirmation = (await workflows.LoadCheckpointAsync(instanceId, token)).Value;
            Assert.Equal(checkpoint.StreamVersion, afterConfirmation.StreamVersion);
            AssertHeld(afterConfirmation, obligation, original);
        }
        if (schedule == Schedule.LegacyHeld)
        {
            await restarted.DriveAsync(instanceId, DurableDriveMode.Required, token);
            var repaired = (await workflows.LoadCheckpointAsync(instanceId, token)).Value;
            Assert.Equal("lease-reconciliation", repaired.LastStepPath);
            AssertHeld(repaired, obligation, original);
            Assert.Empty(admissions);
            Assert.Empty(steps.Invocations);
        }

        if (reopen)
        {
            var existing = await restarted.StartOrGetAsync<string, State>("barrier-certification", definition.DefinitionId,
                definition.DefinitionVersion, "start", token);
            Assert.False(existing.Created);
            Assert.Equal(instanceId, existing.InstanceId);
            // Each protected admission commits a yield (execution-position change).
            var resumed = await workflows.LoadTailAsync(stream, checkpoint.StreamVersion, token);
            Assert.Empty(resumed.OfType<WorkflowExecutionPositionChangedEvent>());
        }
        else
        {
            Assert.Equal(DurableSegmentOutcome.Terminal, (await restarted.DriveAsync(instanceId, token)).Outcome);
        }
        var final = (await workflows.LoadCheckpointAsync(instanceId, token)).Value;
        var history = await workflows.LoadTailAsync(stream, StreamVersion.Empty, token);
        Assert.Single(history.OfType<WorkflowResourcePoolAcquiredEvent>());
        if (missingState || releaseAndReplace)
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

    [Fact]
    public async Task CommittedScopeExit_ReplaysGovernanceCloseOnceAcrossRestart()
    {
        var token = TestContext.Current.CancellationToken;
        var pool = ResourcePoolName.Create("database");
        var partition = ResourceGovernancePartitionId.Create("scope-exit");
        var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
                ResourcePools = new DurableResourcePoolOptions
                {
                    PartitionId = partition,
                    Pools = [DurableResourcePoolDefinition.Create(pool, 1, TimeSpan.FromMinutes(5))]
                }
            });
        using var host = services.BuildServiceProvider();
        var workflows = host.GetRequiredService<IWorkflowEventStore>();
        var governance = host.GetRequiredService<IResourcePoolStore>();
        var definition = Definition(pool);
        var steps = new Services();
        var admissions = new List<CheckpointWrite>();

        DurableWorkflowRuntime Runtime(bool stopAfterWorkflowRelease)
        {
            var events = new DagEventStoreProbe
            {
                Inner = workflows,
                AfterAppend = batch =>
                {
                    if (batch.Checkpoint?.LastStepPath?.EndsWith(":attempt-admission", StringComparison.Ordinal) == true)
                        admissions.Add(batch.Checkpoint);
                    if (stopAfterWorkflowRelease && batch.Events.Any(e => e is WorkflowResourcePoolReleasedEvent))
                        throw new HostStopped();
                }
            };
            var runtime = new DurableWorkflowRuntime(new DurableCommandProcessor(events, governance),
                new DurableDefinitionRegistry(steps), TimeProvider.System);
            runtime.RegisterDefinition(definition);
            return runtime;
        }

        await Assert.ThrowsAsync<HostStopped>(() => Runtime(true).StartOrGetAsync<string, State>("scope-exit",
            definition.DefinitionId, definition.DefinitionVersion, "start", token));
        var admission = Assert.Single(admissions);
        var instanceId = admission.InstanceId;
        var stream = new WorkflowStreamId(instanceId);
        var occurrence = Assert.IsType<LeaseProtectionToken>(Assert.Single(steps.Invocations));
        var original = Assert.Single((await governance.GetPoolAsync(pool.Value, token)).Value.HeldTickets);
        var exit = (await workflows.LoadCheckpointAsync(instanceId, token)).Value;
        var obligation = Assert.Single(DurableExecutionEnvelopeV2.Deserialize(exit.Payload).OwnedObligations);
        Assert.Equal("Released", obligation.LeasePhase);
        AssertHeld(admission, obligation, original);
        var release = Assert.Single((await workflows.LoadTailAsync(stream, StreamVersion.Empty, token)).OfType<WorkflowResourcePoolReleasedEvent>());
        Assert.Equal(original, Assert.Single(release.Tickets));
        Assert.False((await governance.GetReleaseEvidenceAsync(occurrence, token)).HasValue);

        var successor = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var queued = await governance.AcquireAsync(new(successor, "successor", [new(pool.Value, 1)], original.AcquiredAt, null), token);
        Assert.Equal(ResourcePoolAcquireStatus.Queued, queued.Status);
        Assert.Equal(DurableSegmentOutcome.Terminal, (await Runtime(false).DriveAsync(instanceId, token)).Outcome);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await workflows.LoadCheckpointAsync(instanceId, token)).Value.Status);
        var receipt = (await governance.GetReleaseEvidenceAsync(occurrence, token)).Value;
        Assert.Equal(original, Assert.Single(receipt.ReleasedTickets));
        Assert.Null(receipt.ConfirmationId);
        var next = Assert.Single((await governance.GetPoolAsync(pool.Value, token)).Value.HeldTickets);
        Assert.Equal(successor, next.HolderInstanceId);
        Assert.NotEqual(original.TicketId, next.TicketId);
        Assert.Equal(original.ProviderGeneration + 1, next.ProviderGeneration);

        Assert.Equal(DurableSegmentOutcome.Terminal, (await Runtime(false).DriveAsync(instanceId, token)).Outcome);
        Assert.Equal(next, Assert.Single((await governance.GetPoolAsync(pool.Value, token)).Value.HeldTickets));
        var history = await workflows.LoadTailAsync(stream, StreamVersion.Empty, token);
        Assert.Equal(release.EventId, Assert.Single(history.OfType<WorkflowResourcePoolReleasedEvent>()).EventId);
        Assert.Equal(original, Assert.Single(Assert.Single(history.OfType<WorkflowResourcePoolAcquiredEvent>()).Tickets));
        Assert.Single(admissions);
        Assert.Equal(occurrence, Assert.Single(steps.Invocations));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmationWithDifferentReleasedTicket_PreservesWorkflowQuarantine(bool staleConfirmer)
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = InMemoryProviderPorts.Create();
        var events = provider.EventStore;
        var pool = new ResourcePoolDefinition("database", 1, TimeSpan.FromMinutes(5));
        await provider.ResourcePoolStore.UpsertPoolAsync(pool, token);
        var definition = Definition(ResourcePoolName.Create(pool.Name));
        var gate = new RecordingGate(true);
        var processor = new DurableCommandProcessor(events, provider.ResourcePoolStore) { LeaseCertificationGate = gate };
        var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(new Services()), TimeProvider.System);
        runtime.RegisterDefinition(definition);
        await Assert.ThrowsAsync<HostStopped>(() => runtime.StartOrGetAsync<string, State>("receipt-identity",
            definition.DefinitionId, definition.DefinitionVersion, "start", token));
        var activation = gate.Facts[^1];
        var instance = activation.InstanceId;
        var occurrence = activation.LeaseProtectionToken;
        Assert.Equal(DurableCommandOutcome.Committed, (await processor.ProcessAsync(new CancelWorkflowCommand
        {
            CommandId = CommandId.New(), InstanceId = instance, RequestedAt = DateTimeOffset.UtcNow
        }, token)).Outcome);
        var quarantined = (await events.LoadCheckpointAsync(instance, token)).Value;
        var lease = Assert.Single(DurableExecutionEnvelopeV2.Deserialize(quarantined.Payload).OwnedObligations);
        Assert.Equal("Quarantined", lease.LeasePhase);
        Assert.Null(lease.AcceptedConfirmationId);
        var history = await events.LoadTailAsync(new(instance), StreamVersion.Empty, token);
        var original = Assert.Single(Assert.Single(history.OfType<WorkflowResourcePoolAcquiredEvent>()).Tickets);
        Assert.Equal(Persisted(original), Assert.Single(lease.LeaseTickets));

        // A replacement provider retained the occurrence coordinates but owns a different ticket.
        using var replacementProvider = InMemoryProviderPorts.Create();
        var replacement = replacementProvider.ResourcePoolStore;
        await replacement.UpsertPoolAsync(pool, token);
        var actual = Assert.Single((await replacement.AcquireAsync(new ResourcePoolAcquireRequest(instance,
            original.HolderKey, [new(pool.Name, original.Count)], original.AcquiredAt, original.ExpiresAt)
        {
            FiberId = original.FiberId, ScopeId = original.ScopeId, ProtectionToken = occurrence
        }, token)).Tickets);
        Assert.NotEqual(original.TicketId, actual.TicketId);
        Assert.Equal(original.ProviderGeneration, actual.ProviderGeneration);

        var caller = StopConfirmationId.Create("caller");
        var winner = staleConfirmer ? StopConfirmationId.Create("winner") : caller;
        var competitorReleased = false;
        var observed = new DagEventStoreProbe
        {
            Inner = events,
            AfterCheckpointRead = async cancellation =>
            {
                if (!staleConfirmer || competitorReleased) return;
                // The stale caller has already read an empty receipt. Commit its rival at this boundary.
                competitorReleased = true;
                Assert.Equal(ResourcePoolStopConfirmationStatus.Released, await replacement.ConfirmAndReleaseAsync(
                    new(instance, original.HolderKey, occurrence, winner, DateTimeOffset.UtcNow), cancellation));
            }
        };
        Assert.False((await replacement.GetReleaseEvidenceAsync(occurrence, token)).HasValue);
        var recovery = new DurableResourceLeaseRecovery(new DurableCommandProcessor(observed, replacement),
            provider.ProjectionStore);
        Assert.Equal(staleConfirmer ? ProtectedWorkStopConfirmationStatus.AlreadyConfirmed : ProtectedWorkStopConfirmationStatus.Released,
            await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, caller, token));
        Assert.Equal(staleConfirmer, competitorReleased);
        var receipt = (await replacement.GetReleaseEvidenceAsync(occurrence, token)).Value;
        Assert.Equal(winner, receipt.ConfirmationId);
        Assert.Equal(actual, Assert.Single(receipt.ReleasedTickets));
        Assert.Empty((await replacement.GetPoolAsync(pool.Name, token)).Value.HeldTickets);
        Assert.Equal(occurrence, (await replacement.GetConfirmationBindingAsync(winner, token)).Value);
        Assert.Equal(!staleConfirmer, (await replacement.GetConfirmationBindingAsync(caller, token)).HasValue);
        var checkpoint = (await events.LoadCheckpointAsync(instance, token)).Value;
        Assert.Equal("Quarantined", Assert.Single(DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations).LeasePhase);
        Assert.Equal(quarantined.Payload, checkpoint.Payload);
        Assert.Equal(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed,
            await recovery.ConfirmProtectedWorkStoppedAsync(occurrence, caller, token));
        Assert.Equal(quarantined.Payload, (await events.LoadCheckpointAsync(instance, token)).Value.Payload);
        Assert.Empty(await events.LoadTailAsync(new(instance), quarantined.StreamVersion, token));
    }

    private static DurableWorkflowDefinition<string> Definition(ResourcePoolName pool) =>
        Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(pool)), lease => lease.Then<LeasedStep>())
            .End().Build();

    private static void AssertHeld(CheckpointWrite checkpoint, DurableOwnedObligationState expected, params ResourcePoolTicket[] tickets)
    {
        var lease = Assert.Single(DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations);
        Assert.Equal("Held", lease.LeasePhase);
        Assert.Equal(expected.ObligationId, lease.ObligationId);
        Assert.Equal(expected.ProtectionToken, lease.ProtectionToken);
        Assert.All(tickets, ticket => Assert.Equal(ticket.HolderKey, lease.HolderKey));
        Assert.Equal(tickets.Select(Persisted), lease.LeaseTickets);
    }

    private static DurableLeaseTicketState Persisted(ResourcePoolTicket ticket) => new()
    {
        TicketId = ticket.TicketId.ToString("N"), PoolName = ticket.PoolName,
        Units = ticket.Count, ProviderGeneration = ticket.ProviderGeneration,
        ReviewDeadline = ticket.ReviewDeadline, ReviewMarked = ticket.ReviewMarked
    };

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
