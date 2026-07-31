using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static partial class LeaseExitScenarioHost
{
    [Phase0Scenario("total-confirmation-precedence", "3.11c")]
    public static async Task ConfirmationResultPrecedenceIsExhaustive(
        Phase0ScenarioContext context)
    {
        const string barrier = "confirmation-precedence";
        context.ReleaseBarrier(barrier);
        var quarantined = await RunTimedOutRetryAsync(
            context,
            barrier,
            "confirmation-precedence-quarantined");
        var recovery = CreateRecovery(quarantined.Processor, quarantined.Store);
        var confirmation = StopConfirmationId.Create("confirmation-precedence");

        var released = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                quarantined.Token,
                confirmation,
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.Released,
            released,
            "A first truthful confirmation did not release quarantined capacity.");

        var already = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                quarantined.Token,
                confirmation,
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.AlreadyConfirmed,
            already,
            "An idempotent confirmation replay was not classified AlreadyConfirmed.");

        var conflict = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                LeaseProtectionToken.Parse("lease-other"),
                confirmation,
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.ConfirmationConflict,
            conflict,
            "A confirmation ID rebound to another token did not win precedence.");

        var active = await ActiveLeaseFixture.CreateAsync(
            context,
            barrier,
            "confirmation-precedence-active");
        var activeRecovery = CreateRecovery(active.Processor, active.Store);
        var notConfirmable = await context.ObserveAsync(
            _ => activeRecovery.ConfirmProtectedWorkStoppedAsync(
                active.Token,
                StopConfirmationId.Create("active-proof"),
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.NotConfirmable,
            notConfirmable,
            "A live held obligation was incorrectly confirmable.");

        var missing = await context.ObserveAsync(
            _ => activeRecovery.ConfirmProtectedWorkStoppedAsync(
                LeaseProtectionToken.Parse("lease-missing"),
                StopConfirmationId.Create("missing-proof"),
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.TokenNotFound,
            missing,
            "An unbound unknown token did not return TokenNotFound.");

        active.Gate.Release.TrySetResult();
        await active.Running;
    }

    [Phase0Scenario("normal-release-race", "3.11c")]
    public static async Task NormalReleaseWinsBeforeLateConfirmation(
        Phase0ScenarioContext context)
    {
        const string barrier = "normal-release-race";
        context.ReleaseBarrier(barrier);
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var probe = new LeaseProbe();
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(DatabaseRequest(), lease => lease.Then<CaptureLeaseStep>())
            .End()
            .Build();
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, probe),
            processor);
        _ = await runtime.StartOrGetAsync<string, LeaseState>(
            "normal-release-race",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var token = LeaseProtectionToken.Parse(probe.ProtectionToken!);
        var recovery = CreateRecovery(processor, store);
        var result = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                token,
                StopConfirmationId.Create("late-confirmation"),
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.TokenNotFound,
            result,
            "A late confirmation overrode a committed normal release.");
        OrcaCore.Hosting.ResourceLeases.IDurableResourcePoolManagement management =
            new LeaseExitScenarioHost.ResourcePoolManagementView(pools);
        var pool = await context.ObserveAsync(
            _ => management.GetAsync(
                ResourcePoolName.Create("database"),
                CancellationToken.None));
        Phase0Assert.Satisfies(
            pool,
            snapshot =>
                snapshot.ConfiguredCapacity == 1 &&
                snapshot.ReservedUnits == 0,
            "Normal release and late confirmation changed capacity more than once.");
    }

    [Phase0Scenario("retained-bindings-and-tombstones", "3.11c")]
    public static async Task ConfirmationAndNormalReleaseTombstonesSurviveUntilRetentionPurge(
        Phase0ScenarioContext context)
    {
        const string barrier = "retained-bindings";
        context.ReleaseBarrier(barrier);
        var confirmed = await RunTimedOutRetryAsync(
            context,
            barrier,
            "retained-bindings-confirmed");
        var recovery = CreateRecovery(confirmed.Processor, confirmed.Store);
        var acceptedId = StopConfirmationId.Create("retained-confirmation");
        var released = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                confirmed.Token,
                acceptedId,
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.Released,
            released,
            "A truthful confirmation did not create its retained binding.");

        var replay = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                confirmed.Token,
                acceptedId,
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.AlreadyConfirmed,
            replay,
            "The retained confirmation binding was not idempotent.");
        var alternateReplay = await recovery.ConfirmProtectedWorkStoppedAsync(
            confirmed.Token,
            StopConfirmationId.Create("retained-confirmation-alternate"),
            CancellationToken.None);
        if (alternateReplay != ProtectedWorkStopConfirmationStatus.AlreadyConfirmed)
        {
            throw new InvalidOperationException(
                "A token released by accepted confirmation lost its retained tombstone.");
        }

        var confirmedDiagnostics = CreateDiagnostics(confirmed.Processor, confirmed.Store);
        var confirmedTombstone = await confirmedDiagnostics.GetAsync(
            confirmed.Token,
            CancellationToken.None);
        if (confirmedTombstone is not
            {
                Status: DurableResourceLeaseObligationStatus.Released,
                AcceptedConfirmationId: not null
            } ||
            !confirmedTombstone.AcceptedConfirmationId.Equals(acceptedId))
        {
            throw new InvalidOperationException(
                "Exact diagnostics did not retain the accepted confirmation tombstone.");
        }

        var normalStore = new InMemoryWorkflowProvider();
        var normalPools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var normalProbe = new LeaseProbe();
        var normalDefinition = Workflow.Durable<LeaseState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new LeaseState())
            .AcquireResources(DatabaseRequest(), lease => lease.Then<CaptureLeaseStep>())
            .End()
            .Build();
        var normalProcessor = new DurableCommandProcessor(normalStore, normalPools);
        var normalRuntime = CreateRuntime(
            normalStore,
            normalPools,
            normalDefinition,
            context.Services.TimeProvider,
            new LeaseServices(normalPools, normalProbe),
            normalProcessor);
        var normalStart = await normalRuntime.StartOrGetAsync<string, LeaseState>(
            "retained-bindings-normal",
            normalDefinition.DefinitionId,
            normalDefinition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var normalToken = LeaseProtectionToken.Parse(normalProbe.ProtectionToken!);
        var normalRecovery = CreateRecovery(normalProcessor, normalStore);
        var late = await normalRecovery.ConfirmProtectedWorkStoppedAsync(
            normalToken,
            StopConfirmationId.Create("normal-release-late"),
            CancellationToken.None);
        var normalTombstone = await CreateDiagnostics(normalProcessor, normalStore)
            .GetAsync(normalToken, CancellationToken.None);
        if (late != ProtectedWorkStopConfirmationStatus.TokenNotFound ||
            normalTombstone is not
            {
                Status: DurableResourceLeaseObligationStatus.Released,
                AcceptedConfirmationId: null
            })
        {
            throw new InvalidOperationException(
                "Normal release did not retain a non-confirmable token tombstone.");
        }

        var purge = await ((IWorkflowRetentionStore)confirmed.Store).PurgeAsync(
            new RetentionPolicy
            {
                InstanceId = confirmed.InstanceId,
                RequestedAt = context.Services.TimeProvider.GetUtcNow(),
                Reason = "provider dedup window elapsed"
            },
            CancellationToken.None);
        if (!purge.Purged)
        {
            throw new InvalidOperationException(
                $"The confirmed workflow retention purge was rejected: {purge.Reason}");
        }

        await ((IResourceLeaseGovernanceStore)confirmed.Pools).PurgeReleaseEvidenceAsync(
            confirmed.InstanceId,
            CancellationToken.None);
        var afterPurge = await recovery.ConfirmProtectedWorkStoppedAsync(
            confirmed.Token,
            acceptedId,
            CancellationToken.None);
        if (afterPurge != ProtectedWorkStopConfirmationStatus.TokenNotFound)
        {
            throw new InvalidOperationException(
                "Purged confirmation evidence returned a status other than TokenNotFound.");
        }

        _ = normalStart;
    }

    [Phase0Scenario("causal-release-gap-recovery", "3.11c")]
    public static async Task ReplacementRuntimeClosesProviderReleaseGapWithoutSecondRelease(
        Phase0ScenarioContext context)
    {
        const string barrier = "causal-release-gap";
        context.ReleaseBarrier(barrier);
        var fixture = await RunTimedOutRetryAsync(
            context,
            barrier,
            "causal-release-gap");
        var before = await EnvelopeAsync(fixture.Store, fixture.InstanceId);
        var obligation = before.OwnedObligations.Single(candidate =>
            candidate.ProtectionToken == fixture.Token.Value);
        var providerRelease = await fixture.Pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(
                fixture.InstanceId,
                obligation.HolderKey!,
                context.Services.TimeProvider.GetUtcNow()),
            CancellationToken.None);
        if (providerRelease.ReleasedTickets.Count != 1)
        {
            throw new InvalidOperationException(
                "The causal-gap setup did not durably release one exact provider ticket.");
        }

        var replacementProcessor = new DurableCommandProcessor(fixture.Store, fixture.Pools);
        var replacement = CreateRuntime(
            fixture.Store,
            fixture.Pools,
            fixture.Definition,
            context.Services.TimeProvider,
            new LeaseServices(fixture.Pools, new LeaseProbe()),
            replacementProcessor);
        _ = await replacement.StartOrGetAsync<string, LeaseState>(
            fixture.Key,
            fixture.Definition.DefinitionId,
            fixture.Definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        var diagnostics = CreateDiagnostics(replacementProcessor, fixture.Store);
        var observed = await context.ObserveAsync(
            _ => diagnostics.GetAsync(fixture.Token, CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            snapshot =>
                snapshot is not null &&
                snapshot.Status == DurableResourceLeaseObligationStatus.Released &&
                snapshot.AcceptedConfirmationId is null,
            "Replacement-host reconciliation did not close the causal provider-release gap.");

        var replay = await fixture.Pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(
                fixture.InstanceId,
                obligation.HolderKey!,
                context.Services.TimeProvider.GetUtcNow()),
            CancellationToken.None);
        var pool = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (replay.ReleasedTickets.Count != 0 ||
            pool.AvailableCapacity != 1 ||
            pool.HeldTickets.Count != 0)
        {
            throw new InvalidOperationException(
                "Release-gap recovery performed a second release or changed conserved capacity.");
        }
    }

    [Phase0Scenario("proof-validation", "3.11c")]
    public static async Task InvalidConfirmationCannotBindOrRelease(
        Phase0ScenarioContext context)
    {
        const string barrier = "proof-validation";
        context.ReleaseBarrier(barrier);
        var fixture = await ActiveLeaseFixture.CreateAsync(
            context,
            barrier,
            "proof-validation");
        var recovery = CreateRecovery(fixture.Processor, fixture.Store);
        var active = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                fixture.Token,
                StopConfirmationId.Create("premature"),
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.NotConfirmable,
            active,
            "An active protected body accepted premature stop confirmation.");
        var swapped = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                LeaseProtectionToken.Parse("lease-token-swapped"),
                StopConfirmationId.Create("swapped"),
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.TokenNotFound,
            swapped,
            "A token-swapped confirmation bound to unrelated capacity.");

        var diagnostics = CreateDiagnostics(fixture.Processor, fixture.Store);
        var snapshot = await diagnostics.GetAsync(fixture.Token, CancellationToken.None);
        var pool = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (snapshot?.AcceptedConfirmationId is not null ||
            snapshot?.Status != DurableResourceLeaseObligationStatus.Held ||
            pool.AvailableCapacity != 0)
        {
            throw new InvalidOperationException(
                "Rejected confirmation altered the obligation binding or capacity.");
        }

        fixture.Gate.Release.TrySetResult();
        await fixture.Running;
    }

    [Phase0Scenario("mixed-pool-review-marks", "3.11c")]
    public static async Task MixedPoolReviewMarksRemainPerTicket(
        Phase0ScenarioContext context)
    {
        const string barrier = "mixed-pool-review";
        context.ReleaseBarrier(barrier);
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(
            ("fast", 1, TimeSpan.FromMinutes(1)),
            ("slow", 1, TimeSpan.FromMinutes(10)));
        var gate = new LeaseGate();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("fast")),
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("slow")));
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(request, lease => lease.Then<BlockingLeaseStep>())
            .End()
            .Build();
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, new LeaseProbe(), gate),
            processor);
        var running = runtime.StartOrGetAsync<string, LeaseState>(
            "mixed-pool-review",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            CancellationToken.None)).Single().InstanceId;
        var envelope = await EnvelopeAsync(store, instanceId);
        var token = LeaseProtectionToken.Parse(
            envelope.OwnedObligations.Single(obligation => obligation.ProtectionToken is not null)
                .ProtectionToken!);

        context.AdvanceTimeBy(TimeSpan.FromMinutes(2));
        _ = await pools.ExpireTicketsAsync(
            context.Services.TimeProvider.GetUtcNow(),
            CancellationToken.None);
        var diagnostics = CreateDiagnostics(processor, store);
        var observed = await context.ObserveAsync(
            _ => diagnostics.GetAsync(token, CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            snapshot =>
                snapshot is not null &&
                snapshot.Tickets.Count == 2 &&
                snapshot.Tickets.Single(ticket => ticket.Pool.Value == "fast").ReviewMarked &&
                !snapshot.Tickets.Single(ticket => ticket.Pool.Value == "slow").ReviewMarked &&
                snapshot.Tickets.All(ticket =>
                    ticket.Units == 1 &&
                    ticket.ProviderGeneration > 0 &&
                    !string.IsNullOrWhiteSpace(ticket.TicketId)),
            "Mixed-pool reconciliation flattened review evidence across tickets.");
        if ((await pools.GetPoolAsync("fast", CancellationToken.None)).Value.AvailableCapacity != 0 ||
            (await pools.GetPoolAsync("slow", CancellationToken.None)).Value.AvailableCapacity != 0)
        {
            throw new InvalidOperationException("Review marking reclaimed protected capacity.");
        }

        gate.Release.TrySetResult();
        await running;
    }

    [Phase0Scenario("missing-ticket-lease-lost", "3.11c")]
    public static async Task MissingProviderTicketCommitsLeaseLostBeforeRedispatch(
        Phase0ScenarioContext context)
    {
        const string barrier = "missing-ticket";
        context.ReleaseBarrier(barrier);
        var store = new InMemoryWorkflowProvider();
        var underlying = await CreatePoolsAsync(("database", 1, TimeSpan.FromMinutes(5)));
        var pools = new TicketHidingResourcePoolStore(underlying);
        var firstGate = new LeaseGate();
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(DatabaseRequest(), lease => lease.Then<BlockingLeaseStep>())
            .End()
            .Build();
        var firstProcessor = new DurableCommandProcessor(store, pools);
        var first = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, new LeaseProbe(), firstGate),
            firstProcessor);
        var abandoned = first.StartOrGetAsync<string, LeaseState>(
            "missing-ticket",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        await firstGate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            CancellationToken.None)).Single().InstanceId;
        var active = await EnvelopeAsync(store, instanceId);
        var token = LeaseProtectionToken.Parse(
            active.OwnedObligations.Single(obligation => obligation.ProtectionToken is not null)
                .ProtectionToken!);
        var ticket = (await pools.GetPoolAsync(
            "database",
            CancellationToken.None)).Value.HeldTickets.Single();
        pools.HideTicket(ticket.TicketId);

        var replacementGate = new LeaseGate();
        var replacementProcessor = new DurableCommandProcessor(store, pools);
        var replacement = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, new LeaseProbe(), replacementGate),
            replacementProcessor);
        _ = await replacement.StartOrGetAsync<string, LeaseState>(
            "missing-ticket",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var diagnostics = CreateDiagnostics(replacementProcessor, store);
        var lease = await context.ObserveAsync(
            _ => diagnostics.GetAsync(token, CancellationToken.None));
        Phase0Assert.Satisfies(
            lease,
            snapshot =>
                snapshot is not null &&
                snapshot.Status == DurableResourceLeaseObligationStatus.LeaseLost,
            "A missing exact provider ticket was not preserved as LeaseLost.");
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            replacement,
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
            definition,
            [ResourcePoolName.Create("database")]);
        var instance = await definitionHandle.GetInstanceAsync(
            instanceId,
            CancellationToken.None);
        var workflow = await context.ObserveAsync(
            _ => instance.GetSnapshotAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            workflow,
            snapshot =>
                snapshot.Status == global::OrcaCore.WorkflowInstanceStatus.Failed &&
                snapshot.Failure?.Code == "WF-LEASE-LOST",
            "Missing-ticket reconciliation redispatched instead of failing with LeaseLost.");
        if (replacementGate.FirstStarted.Task.IsCompleted)
        {
            throw new InvalidOperationException(
                "Replacement execution started after provider ownership was lost.");
        }

        firstGate.Release.TrySetResult();
        await abandoned;
    }

    [Phase0Scenario("truthful-confirmation-no-time-reclaim", "3.11c")]
    public static async Task OnlyTruthfulConfirmationReclaimsReviewedCapacity(
        Phase0ScenarioContext context)
    {
        const string barrier = "no-time-reclaim";
        context.ReleaseBarrier(barrier);
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, TimeSpan.FromMinutes(1)));
        var gate = new LeaseGate();
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(DatabaseRequest(), lease => lease.Then<BlockingLeaseStep>())
            .End()
            .Build();
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, new LeaseProbe(), gate),
            processor);
        var running = runtime.StartOrGetAsync<string, LeaseState>(
            "no-time-reclaim",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            CancellationToken.None)).Single().InstanceId;
        var active = await EnvelopeAsync(store, instanceId);
        var token = LeaseProtectionToken.Parse(
            active.OwnedObligations.Single(obligation => obligation.ProtectionToken is not null)
                .ProtectionToken!);

        context.AdvanceTimeBy(TimeSpan.FromDays(1));
        _ = await pools.ExpireTicketsAsync(
            context.Services.TimeProvider.GetUtcNow(),
            CancellationToken.None);
        var recovery = CreateRecovery(processor, store);
        var elapsed = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                token,
                StopConfirmationId.Create("elapsed-only"),
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.NotConfirmable,
            elapsed,
            "Elapsed time made an active protected body confirmable.");
        if ((await pools.GetPoolAsync("database", CancellationToken.None))
            .Value.AvailableCapacity != 0)
        {
            throw new InvalidOperationException("Elapsed time reclaimed protected capacity.");
        }

        var terminal = await processor.ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = context.Services.TimeProvider.GetUtcNow()
            },
            CancellationToken.None);
        if (terminal.Outcome != DurableCommandOutcome.Committed)
        {
            throw new InvalidOperationException("Trusted recovery setup did not quarantine.");
        }

        gate.Release.TrySetResult();
        await running;
        var confirmed = await context.ObserveAsync(
            _ => recovery.ConfirmProtectedWorkStoppedAsync(
                token,
                StopConfirmationId.Create("truthful-stop"),
                CancellationToken.None));
        Phase0Assert.Equal(
            ProtectedWorkStopConfirmationStatus.Released,
            confirmed,
            "Truthful stop confirmation did not release quarantined capacity.");
        if ((await pools.GetPoolAsync("database", CancellationToken.None))
            .Value.AvailableCapacity != 1)
        {
            throw new InvalidOperationException(
                "Truthful confirmation did not restore exact capacity.");
        }
    }

    private sealed class TicketHidingResourcePoolStore(
        IResourcePoolStore inner) : IResourcePoolStore
    {
        private Guid? hiddenTicketId;

        internal void HideTicket(Guid ticketId) => hiddenTicketId = ticketId;

        public Task UpsertPoolAsync(
            ResourcePoolDefinition definition,
            CancellationToken cancellationToken) =>
            inner.UpsertPoolAsync(definition, cancellationToken);

        public Task<ResourcePoolAcquireResult> AcquireAsync(
            ResourcePoolAcquireRequest request,
            CancellationToken cancellationToken) =>
            inner.AcquireAsync(request, cancellationToken);

        public Task<ResourcePoolReleaseResult> ReleaseAsync(
            ResourcePoolReleaseRequest request,
            CancellationToken cancellationToken) =>
            inner.ReleaseAsync(request, cancellationToken);

        public async Task<OrcaCore.Abstractions.Primitives.Option<ResourcePoolSnapshot>>
            GetPoolAsync(string poolName, CancellationToken cancellationToken)
        {
            var result = await inner.GetPoolAsync(poolName, cancellationToken);
            return !result.HasValue || hiddenTicketId is not { } hidden
                ? result
                : OrcaCore.Abstractions.Primitives.Option<ResourcePoolSnapshot>.Some(
                    result.Value with
                    {
                        HeldTickets = result.Value.HeldTickets
                            .Where(ticket => ticket.TicketId != hidden)
                            .ToArray()
                    });
        }

        public async Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(
            CancellationToken cancellationToken)
        {
            var result = await inner.ListPoolsAsync(cancellationToken);
            return hiddenTicketId is not { } hidden
                ? result
                : result.Select(pool => pool with
                    {
                        HeldTickets = pool.HeldTickets
                            .Where(ticket => ticket.TicketId != hidden)
                            .ToArray()
                    })
                    .ToArray();
        }

        public Task ResizePoolAsync(
            string poolName,
            int capacity,
            CancellationToken cancellationToken) =>
            inner.ResizePoolAsync(poolName, capacity, cancellationToken);

        public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            inner.ExpireTicketsAsync(now, cancellationToken);
    }
}
