using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Provider.Abstractions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.ProviderCertification;

public static class Section7GovernanceScenarioHost
{
    [Phase0Scenario("four-post-commit-barriers", "3.11d")]
    public static async Task WorkflowReportsTheFourExactImmutablePostCommitFacts(
        Phase0ScenarioContext context)
    {
        using var fixture = GovernanceFixture.Create("four-barriers", ("database", 1));
        var gate = new RecordingCertificationGate(context.Services.Barrier, "four-barriers");
        foreach (var barrier in Enum.GetValues<DurableResourceLeaseCommitBarrier>())
        {
            context.ReleaseBarrier($"four-barriers:{barrier}");
        }

        var facts = await RunOneLeaseAsync(fixture.Pools, gate, "four-barriers");
        var observed = await context.ObserveAsync(_ =>
            ((IDurableResourceLeaseCertificationGate)gate).OnPostCommitAsync(
                facts[0],
                CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            _ => true,
            "The certification gate did not accept the immutable post-commit fact.");

        var expected = new[]
        {
            DurableResourceLeaseCommitBarrier.WorkflowPendingObligationCommitted,
            DurableResourceLeaseCommitBarrier.GovernanceReservationCommitted,
            DurableResourceLeaseCommitBarrier.WorkflowActivationCommitted,
            DurableResourceLeaseCommitBarrier.GovernanceOwnershipConfirmed
        };
        if (!facts.Select(fact => fact.Barrier).SequenceEqual(expected) ||
            facts.Select(fact => fact.PartitionId).Distinct().Count() != 1 ||
            facts.Select(fact => fact.ObligationId).Distinct(StringComparer.Ordinal).Count() != 1 ||
            facts.Select(fact => fact.InstanceId).Distinct().Count() != 1 ||
            facts.Select(fact => fact.LeaseProtectionToken).Distinct().Count() != 1 ||
            facts.Skip(1).Any(fact =>
                fact.Tickets.Count != 1 ||
                fact.Tickets[0].Pool.Value != "database" ||
                fact.Tickets[0].Units != 1 ||
                fact.Tickets[0].ProviderGeneration <= 0))
        {
            throw new InvalidOperationException(
                "The durable lease handoff did not report the exact four correlated immutable facts.");
        }
    }

    [Phase0Scenario("creation-versus-current-capacity", "3.11d")]
    public static async Task ReplayedCurrentCapacityDoesNotRewriteCreationAgreement(
        Phase0ScenarioContext context)
    {
        const string scenario = "creation-current-capacity";
        await ConsumeProductBarrierAsync(context, scenario, observeExactGate: true);
        using var provider = InMemoryProviderPorts.Create();
        var store = provider.ResourceGovernanceStore;
        var options = Options(scenario, ("database", 3));
        using var firstHost = CreateManagementProvider(store, options);
        var first = firstHost.GetRequiredService<IDurableResourcePoolManagement>();
        var resized = await first.ResizeAsync(
            ResourcePoolName.Create("database"),
            1,
            ResourcePoolOperationId.Create("resize-to-one"));
        if (resized is not DurableResourcePoolResizeResult.Applied)
        {
            throw new InvalidOperationException("Initial resize did not commit.");
        }

        using var replacementHost = CreateManagementProvider(store, options);
        var replacement = replacementHost.GetRequiredService<IDurableResourcePoolManagement>();
        var current = await replacement.GetAsync(ResourcePoolName.Create("database"));
        if (current.ConfiguredCapacity != 1)
        {
            throw new InvalidOperationException(
                "Replacement startup overwrote replayed current capacity with creation capacity.");
        }

        var loaded = await context.ObserveAsync(_ =>
            ((IDurableResourceGovernanceStore)store).LoadAsync(
                ResourceGovernancePartitionId.Create(scenario),
                CancellationToken.None));
        Phase0Assert.Satisfies(
            loaded,
            stream => stream.Version == 2 && stream.Records.Count == 2,
            "Creation and resize did not occupy one validated serialized stream.");

        using var incompatibleHost = CreateManagementProvider(
            store,
            Options(scenario, ("database", 4)));
        var incompatible = incompatibleHost.GetRequiredService<IDurableResourcePoolManagement>();
        var rejected = false;
        try
        {
            _ = await incompatible.GetAsync(ResourcePoolName.Create("database"));
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("creation definition", StringComparison.Ordinal))
        {
            rejected = true;
        }

        if (!rejected)
        {
            throw new InvalidOperationException(
                "Replacement startup accepted a changed immutable pool catalog.");
        }
    }

    [Phase0Scenario("stream-and-whole-batch-validation", "3.11d")]
    public static async Task GovernanceStoreCommitsOnlyCopiedConsecutiveWholeBatches(
        Phase0ScenarioContext context)
    {
        const string scenario = "whole-batch";
        await ConsumeProductBarrierAsync(context, scenario);
        using var provider = InMemoryProviderPorts.Create();
        var store = provider.ResourceGovernanceStore;
        var partition = ResourceGovernancePartitionId.Create(scenario);
        var callerPayload = Encoding.UTF8.GetBytes("first");
        var first = Record(1, callerPayload);
        var second = Record(2, Encoding.UTF8.GetBytes("second"));
        var observed = await context.ObserveAsync(_ =>
            ((IDurableResourceGovernanceStore)store).AppendAsync(
                partition,
                0,
                new[] { first, second },
                CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            result => result is ResourceGovernanceAppendResult.Committed { Version: 2 },
            "A valid consecutive whole batch did not commit.");
        callerPayload[0] ^= 0xff;

        var loaded = await store.LoadAsync(partition);
        if (loaded.Version != 2 ||
            Encoding.UTF8.GetString(loaded.Records[0].Payload.Span) != "first")
        {
            throw new InvalidOperationException("The provider retained caller-owned record bytes.");
        }

        try
        {
            _ = await store.AppendAsync(partition, 2, [Record(4, Encoding.UTF8.GetBytes("gap"))]);
            throw new InvalidOperationException("A non-consecutive batch was accepted.");
        }
        catch (ArgumentException)
        {
        }

        var conflict = await store.AppendAsync(
            partition,
            0,
            [Record(1, Encoding.UTF8.GetBytes("stale"))]);
        if (conflict is not ResourceGovernanceAppendResult.Conflict { ActualVersion: 2 } ||
            (await store.LoadAsync(partition)).Version != 2)
        {
            throw new InvalidOperationException("A stale batch partially changed the governance stream.");
        }
    }

    [Phase0Scenario("fifo-atomic-grants", "3.11d")]
    public static async Task QueueHeadReceivesOneAtomicMultiPoolGrantWithoutBypass(
        Phase0ScenarioContext context)
    {
        const string scenario = "fifo-atomic";
        using var fixture = GovernanceFixture.Create(scenario, ("alpha", 1), ("beta", 1));
        var owner = Holder("fifo-owner");
        var head = Holder("fifo-head");
        var tail = Holder("fifo-tail");
        var held = await fixture.Pools.AcquireAsync(
            Acquire(owner, "owner", ("alpha", 1)),
            CancellationToken.None);
        var queuedHead = await fixture.Pools.AcquireAsync(
            Acquire(head, "head", ("alpha", 1), ("beta", 1)),
            CancellationToken.None);
        var queuedTail = await fixture.Pools.AcquireAsync(
            Acquire(tail, "tail", ("beta", 1)),
            CancellationToken.None);
        if (held.Status != ResourcePoolAcquireStatus.Granted ||
            queuedHead.Status != ResourcePoolAcquireStatus.Queued ||
            queuedTail.Status != ResourcePoolAcquireStatus.Queued)
        {
            throw new InvalidOperationException("The contention setup did not produce one FIFO queue.");
        }

        var released = await fixture.Pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(owner, "owner", DateTimeOffset.UtcNow),
            CancellationToken.None);
        var headReplay = await fixture.Pools.AcquireAsync(
            Acquire(head, "head", ("alpha", 1), ("beta", 1)),
            CancellationToken.None);
        var tailReplay = await fixture.Pools.AcquireAsync(
            Acquire(tail, "tail", ("beta", 1)),
            CancellationToken.None);
        if (released.GrantedWaiters.Count != 1 ||
            !released.GrantedWaiters[0].HolderInstanceId.Equals(head) ||
            headReplay.Status != ResourcePoolAcquireStatus.Granted ||
            headReplay.Tickets.Count != 2 ||
            tailReplay.Status != ResourcePoolAcquireStatus.Queued)
        {
            throw new InvalidOperationException(
                $"The provider bypassed FIFO order or partially granted a multi-pool request: " +
                $"released={released.GrantedWaiters.Count}, " +
                $"released-holder={released.GrantedWaiters.FirstOrDefault()?.HolderInstanceId}, " +
                $"head={headReplay.Status}/{headReplay.Tickets.Count}, tail={tailReplay.Status}.");
        }

        await ObserveGateAsync(context, scenario, Fact(
            DurableResourceLeaseCommitBarrier.GovernanceReservationCommitted,
            head,
            headReplay.Tickets));
    }

    [Phase0Scenario("cancel-every-barrier", "3.11d")]
    public static async Task CancellationAtEveryHandoffKeepsOneClosedOwnershipOutcome(
        Phase0ScenarioContext context)
    {
        const string scenario = "cancel-barriers";
        foreach (var barrier in Enum.GetValues<DurableResourceLeaseCommitBarrier>())
        {
            var fact = Fact(barrier, Holder($"cancel-{barrier}"), []);
            await ObserveGateAsync(context, $"{scenario}:{barrier}", fact);
        }

        using var fixture = GovernanceFixture.Create(scenario, ("database", 1));
        var owner = Holder("cancel-owner");
        var waiter = Holder("cancel-waiter");
        _ = await fixture.Pools.AcquireAsync(
            Acquire(owner, "owner", ("database", 1)),
            CancellationToken.None);
        var queued = await fixture.Pools.AcquireAsync(
            Acquire(waiter, "waiter", ("database", 1)),
            CancellationToken.None);
        var cancelled = await fixture.Pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(waiter, "waiter", DateTimeOffset.UtcNow),
            CancellationToken.None);
        var snapshot = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (queued.Status != ResourcePoolAcquireStatus.Queued ||
            cancelled.ReleasedTickets.Count != 0 ||
            snapshot.HeldTickets.Count != 1 ||
            snapshot.QueuedWaiters.Count != 0)
        {
            throw new InvalidOperationException(
                "Queued cancellation created a ghost ticket or removed another holder's ownership.");
        }
    }

    [Phase0Scenario("fact-equality-and-ticket-uniqueness", "3.11d")]
    public static async Task CorrelatedFactsPreserveExactOwnershipAndUniqueTickets(
        Phase0ScenarioContext context)
    {
        using var fixture = GovernanceFixture.Create("fact-equality", ("database", 1));
        var gate = new RecordingCertificationGate(context.Services.Barrier, "fact-equality");
        foreach (var barrier in Enum.GetValues<DurableResourceLeaseCommitBarrier>())
        {
            context.ReleaseBarrier($"fact-equality:{barrier}");
        }

        var facts = await RunOneLeaseAsync(fixture.Pools, gate, "fact-equality");
        await ObserveGateAsync(context, "fact-equality", facts[^1]);
        var tickets = facts.SelectMany(fact => fact.Tickets).ToArray();
        if (facts.Select(fact => fact.ObligationId).Distinct(StringComparer.Ordinal).Count() != 1 ||
            facts.Select(fact => fact.InstanceId).Distinct().Count() != 1 ||
            facts.Select(fact => fact.Generation).Distinct().Count() != 1 ||
            facts.Select(fact => fact.FiberOccurrence).Distinct(StringComparer.Ordinal).Count() != 1 ||
            facts.Select(fact => fact.ScopeOccurrence).Distinct(StringComparer.Ordinal).Count() != 1 ||
            facts.Select(fact => fact.LeaseProtectionToken).Distinct().Count() != 1 ||
            tickets.Select(ticket => ticket.TicketId).Distinct(StringComparer.Ordinal).Count() != 1 ||
            tickets.Any(ticket => ticket.ProviderGeneration <= 0))
        {
            throw new InvalidOperationException(
                "Post-commit facts diverged or contained ghost/duplicate provider ticket identities.");
        }
    }

    [Phase0Scenario("isolated-restoration", "3.11d")]
    public static async Task ReleasingOneHolderRestoresOnlyItsExactPoolUnits(
        Phase0ScenarioContext context)
    {
        const string scenario = "isolated-restoration";
        using var fixture = GovernanceFixture.Create(scenario, ("alpha", 2), ("beta", 2));
        var first = Holder("isolated-first");
        var second = Holder("isolated-second");
        var firstGrant = await fixture.Pools.AcquireAsync(
            Acquire(first, "first", ("alpha", 1)),
            CancellationToken.None);
        _ = await fixture.Pools.AcquireAsync(
            Acquire(second, "second", ("beta", 2)),
            CancellationToken.None);
        _ = await fixture.Pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(first, "first", DateTimeOffset.UtcNow),
            CancellationToken.None);
        var alpha = (await fixture.Pools.GetPoolAsync("alpha", CancellationToken.None)).Value;
        var beta = (await fixture.Pools.GetPoolAsync("beta", CancellationToken.None)).Value;
        if (alpha.AvailableCapacity != 2 ||
            beta.AvailableCapacity != 0 ||
            !beta.HeldTickets.Single().HolderInstanceId.Equals(second))
        {
            throw new InvalidOperationException(
                $"Release restored unrelated durable capacity: " +
                $"alpha={alpha.AvailableCapacity}/{alpha.HeldTickets.Count}, " +
                $"beta={beta.AvailableCapacity}/{beta.HeldTickets.Count}, " +
                $"beta-holder={beta.HeldTickets.Single().HolderInstanceId}.");
        }

        var loaded = await context.ObserveAsync(_ =>
            fixture.Store.LoadAsync(fixture.PartitionId, CancellationToken.None));
        Phase0Assert.Satisfies(
            loaded,
            stream => stream.Version >= 4,
            "Isolated acquisition and release were not serialized in governance.");
        await ObserveGateAsync(
            context,
            scenario,
            Fact(
                DurableResourceLeaseCommitBarrier.GovernanceOwnershipConfirmed,
                first,
                firstGrant.Tickets));
    }

    [Phase0Scenario("contended-conservation-transfer", "3.11d")]
    public static async Task DirectTransferConservesCapacityAndMintsDistinctSuccessorTickets(
        Phase0ScenarioContext context)
    {
        const string scenario = "conservation-transfer";
        using var fixture = GovernanceFixture.Create(scenario, ("database", 1));
        var first = Holder("transfer-first");
        var second = Holder("transfer-second");
        var firstGrant = await fixture.Pools.AcquireAsync(
            Acquire(first, "first", ("database", 1)),
            CancellationToken.None);
        _ = await fixture.Pools.AcquireAsync(
            Acquire(second, "second", ("database", 1)),
            CancellationToken.None);
        _ = await fixture.Pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(first, "first", DateTimeOffset.UtcNow),
            CancellationToken.None);
        var secondGrant = await fixture.Pools.AcquireAsync(
            Acquire(second, "second", ("database", 1)),
            CancellationToken.None);
        var snapshot = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (secondGrant.Status != ResourcePoolAcquireStatus.Granted ||
            snapshot.AvailableCapacity != 0 ||
            snapshot.HeldTickets.Count != 1 ||
            !snapshot.HeldTickets[0].HolderInstanceId.Equals(second) ||
            firstGrant.Tickets[0].TicketId == secondGrant.Tickets[0].TicketId ||
            secondGrant.Tickets[0].ProviderGeneration <= firstGrant.Tickets[0].ProviderGeneration)
        {
            throw new InvalidOperationException(
                $"Contended direct transfer leaked capacity or reused the predecessor ticket: " +
                $"grant={secondGrant.Status}/{secondGrant.Tickets.Count}, " +
                $"available={snapshot.AvailableCapacity}, held={snapshot.HeldTickets.Count}, " +
                $"holder={snapshot.HeldTickets.FirstOrDefault()?.HolderInstanceId}, " +
                $"generations={firstGrant.Tickets[0].ProviderGeneration}/" +
                $"{secondGrant.Tickets.FirstOrDefault()?.ProviderGeneration}, " +
                $"ticket-equal={firstGrant.Tickets[0].TicketId == secondGrant.Tickets.FirstOrDefault()?.TicketId}.");
        }

        var loaded = await context.ObserveAsync(_ =>
            fixture.Store.LoadAsync(fixture.PartitionId, CancellationToken.None));
        Phase0Assert.Satisfies(
            loaded,
            stream => stream.Version >= 4,
            "Contended ownership transfer was not durably serialized.");
        await ObserveGateAsync(
            context,
            scenario,
            Fact(
                DurableResourceLeaseCommitBarrier.GovernanceOwnershipConfirmed,
                second,
                secondGrant.Tickets));
    }

    [Phase0Scenario("management-input-closure", "3.11d")]
    public static async Task ManagementRejectsUnknownAndUnboundInputsBeforeMutation(
        Phase0ScenarioContext context)
    {
        using var fixture = GovernanceFixture.Create("management-closure", ("database", 2));
        var missing = await context.ObserveThrowsAsync<
            ResourcePoolNotConfiguredException,
            DurableResourcePoolSnapshot>(_ =>
            fixture.Management.GetAsync(
                ResourcePoolName.Create("missing"),
                CancellationToken.None));
        Phase0Assert.Satisfies(
            missing,
            exception => exception.MissingPools.Select(pool => pool.Value).SequenceEqual(["missing"]),
            "Unknown GetAsync did not return the typed copied pool failure.");

        var before = await fixture.Store.LoadAsync(fixture.PartitionId);
        try
        {
            _ = await fixture.Management.ResizeAsync(
                ResourcePoolName.Create("database"),
                0,
                ResourcePoolOperationId.Create("must-not-bind"));
            throw new InvalidOperationException("Zero capacity was accepted.");
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        var applied = await context.ObserveAsync(_ =>
            fixture.Management.ResizeAsync(
                ResourcePoolName.Create("database"),
                1,
                ResourcePoolOperationId.Create("management-valid"),
                CancellationToken.None));
        Phase0Assert.Satisfies(
            applied,
            result => result is DurableResourcePoolResizeResult.Applied
            {
                Capacity: 1,
                Snapshot.ConfiguredCapacity: 1
            },
            "Valid ResizeAsync did not return its closed applied result.");
        var after = await fixture.Store.LoadAsync(fixture.PartitionId);
        if (after.Version != before.Version + 1)
        {
            throw new InvalidOperationException(
                "Invalid management input bound an operation before the valid resize.");
        }
    }

    [Phase0Scenario("closed-resize-replay-and-debt", "3.11d")]
    public static async Task ResizeReplayIsClosedAndDebtTracksHeldUnits(
        Phase0ScenarioContext context)
    {
        const string scenario = "resize-debt";
        await ConsumeProductBarrierAsync(context, scenario);
        using var fixture = GovernanceFixture.Create(scenario, ("database", 2));
        var holder = Holder("resize-holder");
        _ = await fixture.Pools.AcquireAsync(
            Acquire(holder, "holder", ("database", 2)),
            CancellationToken.None);
        var operation = ResourcePoolOperationId.Create("resize-under-load");
        var applied = await context.ObserveAsync(_ =>
            fixture.Management.ResizeAsync(
                ResourcePoolName.Create("database"),
                1,
                operation,
                CancellationToken.None));
        Phase0Assert.Satisfies(
            applied,
            result => result is DurableResourcePoolResizeResult.Applied
            {
                Snapshot.ReservedUnits: 2,
                Snapshot.ResizeDebt: 1
            },
            "Resize under load did not preserve ownership and report exact debt.");

        var replay = await fixture.Management.ResizeAsync(
            ResourcePoolName.Create("database"),
            1,
            operation);
        var conflict = await fixture.Management.ResizeAsync(
            ResourcePoolName.Create("database"),
            3,
            operation);
        if (replay is not DurableResourcePoolResizeResult.Applied ||
            conflict is not DurableResourcePoolResizeResult.Conflict
            {
                RecordedCapacity: 1,
                AttemptedCapacity: 3
            })
        {
            throw new InvalidOperationException("Resize operation replay did not return the closed union.");
        }

        var loaded = await context.ObserveAsync(_ =>
            fixture.Store.LoadAsync(fixture.PartitionId, CancellationToken.None));
        Phase0Assert.Satisfies(
            loaded,
            stream => stream.Version == 3,
            "Applied replay/conflict appended duplicate resize records.");
    }

    [Phase0Scenario("confirmation-and-tombstone-accounting", "3.11d")]
    public static async Task AcceptedConfirmationRetainsItsBindingAndReleasesExactlyOnce(
        Phase0ScenarioContext context)
    {
        const string scenario = "confirmation-tombstone";
        using var fixture = GovernanceFixture.Create(scenario, ("database", 1));
        using var workflowProvider = InMemoryProviderPorts.Create();
        var workflowStore = workflowProvider.EventStore;
        var control = new BlockingLeaseControl();
        var publicDefinition = global::OrcaCore.Workflow.Durable<CertificationState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new CertificationState())
            .AcquireResources(
                ResourceLeaseRequest.Create(
                    ResourceLeaseRequirement.Require(ResourcePoolName.Create("database"))),
                lease => lease.Then<BlockingLeaseStep>())
            .End()
            .Build();
        var processor = new DurableCommandProcessor(workflowStore, fixture.Pools);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(new StepServices(control)),
            TimeProvider.System,
            DurableDriverBudget.Default,
            workflowProvider.ProjectionStore);
        runtime.RegisterDefinition(publicDefinition);
        var running = runtime.StartOrGetAsync<string, CertificationState>(
            scenario,
            publicDefinition.DefinitionId,
            publicDefinition.DefinitionVersion,
            "start",
            CancellationToken.None);
        await AwaitSignalBeforeWorkflowCompletionAsync(
            control.Started.Task,
            running,
            "The confirmation-and-tombstone workflow completed before its protected body started.");
        var projection = (await workflowProvider.ProjectionStore.ListLeaseRecoveryCandidatesAsync(
            CancellationToken.None)).Single();
        var terminated = await processor.ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = projection.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow
            },
            CancellationToken.None);
        if (terminated.Outcome != DurableCommandOutcome.Committed)
        {
            throw new InvalidOperationException("Termination did not commit quarantine intent.");
        }

        control.Release.TrySetResult();
        _ = await running;
        var recovery = new DurableResourceLeaseRecovery(
            processor,
            workflowProvider.ProjectionStore,
            TimeProvider.System);
        var adapter = new RecoveryAdapter(recovery);
        var token = LeaseProtectionToken.Parse(control.ProtectionToken!);
        var confirmation = StopConfirmationId.Create("truthful-stop");
        var first = await context.ObserveAsync(_ =>
            ((IDurableResourceLeaseRecovery)adapter).ConfirmProtectedWorkStoppedAsync(
                token,
                confirmation,
                CancellationToken.None));
        Phase0Assert.Satisfies(
            first,
            status => status == ProtectedWorkStopConfirmationStatus.Released,
            "Truthful confirmation did not release quarantined ownership.");
        var replay = await adapter.ConfirmProtectedWorkStoppedAsync(token, confirmation);
        var pool = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (replay != ProtectedWorkStopConfirmationStatus.AlreadyConfirmed ||
            pool.AvailableCapacity != 1 ||
            pool.HeldTickets.Count != 0)
        {
            throw new InvalidOperationException(
                "Confirmation replay lost its tombstone binding or released capacity twice.");
        }

        var loaded = await context.ObserveAsync(_ =>
            fixture.Store.LoadAsync(fixture.PartitionId, CancellationToken.None));
        Phase0Assert.Satisfies(
            loaded,
            stream => stream.Version >= 3,
            "Confirmation release was not reflected in serialized governance accounting.");
        await ConsumeProductBarrierAsync(context, scenario);
    }

    private static async Task<IReadOnlyList<DurableResourceLeaseCommitBarrierFact>> RunOneLeaseAsync(
        IResourcePoolStore pools,
        RecordingCertificationGate gate,
        string key)
    {
        using var workflowProvider = InMemoryProviderPorts.Create();
        var workflowStore = workflowProvider.EventStore;
        var publicDefinition = global::OrcaCore.Workflow.Durable<CertificationState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new CertificationState())
            .AcquireResources(
                ResourceLeaseRequest.Create(
                    ResourceLeaseRequirement.Require(ResourcePoolName.Create("database"))),
                lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var processor = new DurableCommandProcessor(workflowStore, pools)
        {
            LeaseCertificationGate = gate
        };
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(new StepServices()),
            TimeProvider.System,
            DurableDriverBudget.Default,
            workflowProvider.ProjectionStore);
        runtime.RegisterDefinition(publicDefinition);
        _ = await runtime.StartOrGetAsync<string, CertificationState>(
            key,
            publicDefinition.DefinitionId,
            publicDefinition.DefinitionVersion,
            "start",
            CancellationToken.None);
        return gate.Facts.ToArray();
    }

    private static async Task ConsumeProductBarrierAsync(
        Phase0ScenarioContext context,
        string scenario,
        bool observeExactGate = false)
    {
        var fact = Fact(
            DurableResourceLeaseCommitBarrier.WorkflowPendingObligationCommitted,
            Holder($"{scenario}-barrier"),
            []);
        if (observeExactGate)
        {
            var observedName = $"{scenario}:observed";
            context.ReleaseBarrier(observedName);
            var observedGate = new SingleBarrierGate(context.Services.Barrier, observedName);
            var observed = await context.ObserveAsync(_ =>
                ((IDurableResourceLeaseCertificationGate)observedGate).OnPostCommitAsync(
                    fact,
                    CancellationToken.None));
            Phase0Assert.Satisfies(
                observed,
                _ => true,
                "The exact certification gate did not accept the post-commit fact.");
        }

        var productName = $"{scenario}:product";
        context.ReleaseBarrier(productName);
        var productGate = new SingleBarrierGate(context.Services.Barrier, productName);
        using var provider = InMemoryProviderPorts.Create();
        var processor = new DurableCommandProcessor(provider.EventStore)
        {
            LeaseCertificationGate = productGate
        };
        await processor.ReportLeaseBarrierAsync(
            fact.Barrier,
            fact.InstanceId,
            fact.Generation,
            fact.ObligationId,
            fact.FiberOccurrence,
            fact.ScopeOccurrence,
            fact.LeaseProtectionToken.Value,
            fact.WorkflowVersion,
            Array.Empty<ResourcePoolTicket>(),
            CancellationToken.None);
    }

    private static async Task ObserveGateAsync(
        Phase0ScenarioContext context,
        string scenario,
        DurableResourceLeaseCommitBarrierFact fact)
    {
        var name = $"{scenario}:observed";
        context.ReleaseBarrier(name);
        var gate = new SingleBarrierGate(context.Services.Barrier, name);
        var observed = await context.ObserveAsync(_ =>
            ((IDurableResourceLeaseCertificationGate)gate).OnPostCommitAsync(
                fact,
                CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            _ => true,
            "The exact certification gate did not accept the post-commit fact.");

        var productName = $"{scenario}:product";
        context.ReleaseBarrier(productName);
        var productGate = new SingleBarrierGate(context.Services.Barrier, productName);
        using var provider = InMemoryProviderPorts.Create();
        var processor = new DurableCommandProcessor(provider.EventStore)
        {
            LeaseCertificationGate = productGate
        };
        await processor.ReportLeaseBarrierAsync(
            fact.Barrier,
            fact.InstanceId,
            fact.Generation,
            fact.ObligationId,
            fact.FiberOccurrence,
            fact.ScopeOccurrence,
            fact.LeaseProtectionToken.Value,
            fact.WorkflowVersion,
            fact.Tickets.Select(ticket => new ResourcePoolTicket(
                Guid.ParseExact(ticket.TicketId, "N"),
                ticket.Pool.Value,
                ticket.Units,
                fact.InstanceId,
                fact.ObligationId,
                DateTimeOffset.UnixEpoch,
                null)
            {
                FiberId = new FiberId(fact.FiberOccurrence),
                ScopeId = new ScopeId(fact.ScopeOccurrence),
                ProviderGeneration = ticket.ProviderGeneration,
                ReviewDeadline = ticket.ReviewDeadline,
                ReviewMarked = ticket.ReviewMarked
            }).ToArray(),
            CancellationToken.None);
    }

    private static DurableResourceLeaseCommitBarrierFact Fact(
        DurableResourceLeaseCommitBarrier barrier,
        InstanceId instanceId,
        IReadOnlyList<ResourcePoolTicket> tickets) =>
        new(
            barrier,
            ResourceGovernancePartitionId.Create("default"),
            "obligation",
            instanceId,
            0,
            "root",
            "root",
            LeaseProtectionToken.Parse("protection-token"),
            1,
            tickets.Count == 0 ? 0 : tickets.Max(ticket => ticket.ProviderGeneration),
            tickets.Select(ticket => new DurableResourceLeaseTicketSnapshot(
                    ticket.TicketId.ToString("N"),
                    ResourcePoolName.Create(ticket.PoolName),
                    ticket.Count,
                    ticket.ProviderGeneration,
                    ticket.ReviewDeadline ?? DateTimeOffset.MaxValue,
                    ticket.ReviewMarked))
                .ToArray());

    private static ResourcePoolAcquireRequest Acquire(
        InstanceId holder,
        string holderKey,
        params (string Pool, int Units)[] requirements) =>
        new(
            holder,
            holderKey,
            requirements.Select(requirement =>
                new ResourcePoolRequirement(requirement.Pool, requirement.Units)).ToArray(),
            DateTimeOffset.UtcNow,
            null)
        {
            FiberId = new FiberId($"fiber-{holderKey}"),
            ScopeId = new ScopeId($"scope-{holderKey}")
        };

    private static InstanceId Holder(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        hash[6] = (byte)((hash[6] & 0x0f) | 0x70);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return InstanceId.Parse(new Guid(hash[..16]).ToString());
    }

    private static DurableResourcePoolOptions Options(
        string partition,
        params (string Name, int Capacity)[] pools) =>
        new()
        {
            PartitionId = ResourceGovernancePartitionId.Create(partition),
            Pools = pools.Select(pool => DurableResourcePoolDefinition.Create(
                    ResourcePoolName.Create(pool.Name),
                    pool.Capacity,
                    TimeSpan.FromMinutes(5)))
                .ToArray()
        };

    private static ServiceProvider CreateManagementProvider(
        IDurableResourceGovernanceStore store,
        DurableResourcePoolOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDurableProviderRole>(CertificationProviderRole.Instance);
        services.AddSingleton(store);
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            ResourcePools = options
        });
        return services.BuildServiceProvider();
    }

    private static ResourceGovernanceRecord Record(long sequence, byte[] payload)
    {
        var checksum = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return ResourceGovernanceRecord.FromPersisted(
            sequence,
            ResourceGovernanceRecord.V1Format,
            payload,
            checksum);
    }

    private sealed class GovernanceFixture : IDisposable
    {
        private GovernanceFixture(
            ServiceProvider provider,
            ResourceGovernancePartitionId partitionId)
        {
            Provider = provider;
            PartitionId = partitionId;
            Pools = provider.GetRequiredService<IResourcePoolStore>();
            Store = provider.GetRequiredService<IDurableResourceGovernanceStore>();
            Management = provider.GetRequiredService<IDurableResourcePoolManagement>();
        }

        internal ServiceProvider Provider { get; }
        internal ResourceGovernancePartitionId PartitionId { get; }
        internal IResourcePoolStore Pools { get; }
        internal IDurableResourceGovernanceStore Store { get; }
        internal IDurableResourcePoolManagement Management { get; }

        internal static GovernanceFixture Create(
            string partition,
            params (string Name, int Capacity)[] pools)
        {
            var services = new ServiceCollection();
            services.AddOrcaCoreInMemoryDurableProvider();
            services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 4,
                    StepThrottles = []
                },
                ResourcePools = Options(partition, pools)
            });
            return new GovernanceFixture(
                services.BuildServiceProvider(),
                ResourceGovernancePartitionId.Create(partition));
        }

        public void Dispose() => Provider.Dispose();
    }

    private sealed record CertificationProviderRole : IDurableProviderRole
    {
        internal static CertificationProviderRole Instance { get; } = new();

        public string Name => "provider-certification";

        public bool IsDevelopmentOnly => true;
    }

    private sealed class RecordingCertificationGate(
        IPhase0DeterministicBarrier barrier,
        string scenario) : IDurableResourceLeaseCertificationGate
    {
        internal List<DurableResourceLeaseCommitBarrierFact> Facts { get; } = [];

        public async ValueTask OnPostCommitAsync(
            DurableResourceLeaseCommitBarrierFact fact,
            CancellationToken cancellationToken = default)
        {
            Facts.Add(fact);
            await barrier.ReachAsync($"{scenario}:{fact.Barrier}", cancellationToken);
        }
    }

    private sealed class SingleBarrierGate(
        IPhase0DeterministicBarrier barrier,
        string name) : IDurableResourceLeaseCertificationGate
    {
        public ValueTask OnPostCommitAsync(
            DurableResourceLeaseCommitBarrierFact fact,
            CancellationToken cancellationToken = default) =>
            barrier.ReachAsync(name, cancellationToken);
    }

    private sealed class RecoveryAdapter(
        DurableResourceLeaseRecovery recovery) : IDurableResourceLeaseRecovery
    {
        public ValueTask<ProtectedWorkStopConfirmationStatus> ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken protectionToken,
            StopConfirmationId confirmationId,
            CancellationToken cancellationToken = default) =>
            recovery.ConfirmProtectedWorkStoppedAsync(
                protectionToken,
                confirmationId,
                cancellationToken);
    }

    private sealed class CertificationState;

    private sealed class NoOpStep : IStep<CertificationState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<CertificationState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class BlockingLeaseControl
    {
        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string? ProtectionToken;
    }

    private static async Task AwaitSignalBeforeWorkflowCompletionAsync(
        Task signal,
        Task workflow,
        string completionMessage)
    {
        var first = await Task.WhenAny(signal, workflow);
        if (first == workflow && !signal.IsCompleted)
        {
            await workflow;
            throw new InvalidOperationException(completionMessage);
        }

        await signal;
    }

    private sealed class BlockingLeaseStep(
        BlockingLeaseControl control) : IStep<CertificationState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<CertificationState> context,
            CancellationToken cancellationToken)
        {
            control.ProtectionToken = context.ResourceLease?.ProtectionToken.Value;
            control.Started.TrySetResult();
            await control.Release.Task;
            return new StepResult.Completed();
        }
    }

    private sealed class StepServices(
        BlockingLeaseControl? blocking = null) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(NoOpStep)
                ? new NoOpStep()
                : serviceType == typeof(BlockingLeaseStep) && blocking is not null
                    ? new BlockingLeaseStep(blocking)
                    : null;
    }
}
