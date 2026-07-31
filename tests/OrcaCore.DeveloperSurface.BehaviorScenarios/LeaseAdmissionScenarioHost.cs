using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class LeaseAdmissionScenarioHost
{
    [Phase0Scenario("lease-factory-invariants", "3.11a")]
    public static void LeaseFactoryInvariants(Phase0ScenarioContext context)
    {
        var poolA = ResourcePoolName.Create("pool-a");
        var poolB = ResourcePoolName.Create("pool-b");
        var observedRequirement = context.Observe(
            _ => ResourceLeaseRequirement.Require(poolA, 2));
        ResourceLeaseRequirement? first = null;
        Phase0Assert.Satisfies(
            observedRequirement,
            requirement =>
            {
                first = requirement;
                return requirement.Pool.Equals(poolA) && requirement.Units == 2;
            },
            "ResourceLeaseRequirement.Require did not preserve the validated pool and units.");

        var additional = new[]
        {
            ResourceLeaseRequirement.Require(poolB, 3)
        };
        ResourceLeaseRequest? createdRequest = null;
        var observedRequest = context.Observe(
            _ => ResourceLeaseRequest.Create(first!, additional));
        Phase0Assert.Satisfies(
            observedRequest,
            request =>
            {
                createdRequest = request;
                return
                request.Requirements.Count == 2 &&
                request.Requirements[0].Pool.Equals(poolA) &&
                request.Requirements[0].Units == 2 &&
                request.Requirements[1].Pool.Equals(poolB) &&
                request.Requirements[1].Units == 3;
            },
            "ResourceLeaseRequest.Create did not preserve ordinal-stable distinct requirements.");

        additional[0] = ResourceLeaseRequirement.Require(ResourcePoolName.Create("mutated"), 9);
        if (!createdRequest!.Requirements[1].Pool.Equals(poolB) ||
            createdRequest.Requirements[1].Units != 3)
        {
            throw new InvalidOperationException("The request retained the caller's mutable array.");
        }

        AssertThrows<ArgumentNullException>(() => ResourceLeaseRequirement.Require(null!));
        AssertThrows<ArgumentOutOfRangeException>(
            () => ResourceLeaseRequirement.Require(poolA, 0));
        AssertThrows<ArgumentNullException>(
            () => ResourceLeaseRequest.Create(null!));
        AssertThrows<ArgumentException>(
            () => ResourceLeaseRequest.Create(
                first!,
                ResourceLeaseRequirement.Require(poolA)));
    }

    [Phase0Scenario("runtime-unknown-pool-before-mutation", "3.11a")]
    public static async Task RuntimeUnknownPoolFailsBeforeMutation(Phase0ScenarioContext context)
    {
        const string barrier = "unknown-pool";
        context.ReleaseBarrier(barrier);
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("zeta")),
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("alpha")));
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(request, lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var runtime = CreateRuntime(store, pools, definition, context.Services.TimeProvider);
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
            definition,
            request.Requirements.Select(requirement => requirement.Pool));

        var observed = await context.ObserveThrowsAsync<
            ResourcePoolNotConfiguredException,
            WorkflowStartResult<WorkflowInstanceHandle>>(
            _ => definitionHandle.StartOrGetAsync(
                "input",
                StartIdempotencyKey.Create("unknown-pools"),
                CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            exception =>
                exception.Code == "WF-RESOURCE-POOL-NOT-CONFIGURED" &&
                exception.MissingPools.Select(pool => pool.Value)
                    .SequenceEqual(["alpha", "zeta"]),
            "Unknown pools were not reported as one complete ordinal-sorted typed failure.");

        if ((await pools.ListPoolsAsync(CancellationToken.None)).Count != 0)
        {
            throw new InvalidOperationException("Unknown-pool admission mutated provider pools.");
        }

        var instance = (await store.ListAsync(new WorkflowProjectionQuery(), CancellationToken.None))
            .Single();
        if ((await store.LoadCheckpointAsync(instance.InstanceId, CancellationToken.None)).HasValue)
        {
            throw new InvalidOperationException(
                "Unknown-pool admission committed a lease obligation before failing.");
        }
    }

    [Phase0Scenario("selector-request-commit-replay", "3.11a")]
    public static async Task SelectorRequestCommitsOnceAcrossReplacement(Phase0ScenarioContext context)
    {
        const string barrier = "selector-request";
        context.ReleaseBarrier(barrier);
        var selectorCalls = 0;
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var root = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new LeaseState());
        Func<ReadOnlyStateSnapshot<LeaseState>, ResourceLeaseRequest> selector = _ =>
        {
            ConsumeBarrier(context, barrier);
            selectorCalls++;
            return request;
        };
        Action<DurableLeaseWorkflowBuilder<string, LeaseState>> body =
            lease => lease.Then<NoOpStep>();
        var observed = context.Observe(_ => root.AcquireResources(selector, body));
        Phase0Assert.Satisfies(
            observed,
            successor => ReferenceEquals(successor, root),
            "Selector AcquireResources did not preserve the authoring session.");
        var definition = root.End().Build();
        var first = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new DurableDriverBudget(1, TimeSpan.FromMinutes(1)));

        var started = await first.StartOrGetAsync<string, LeaseState>(
            "selector-replay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        _ = await first.StartOrGetAsync<string, LeaseState>(
            "selector-replay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var committed = await EnvelopeAsync(store, started.InstanceId);
        var original = committed.OwnedObligations.Single(
            obligation => obligation.ProtectionToken is not null);

        var replacement = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider);
        _ = await replacement.StartOrGetAsync<string, LeaseState>(
            "selector-replay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var terminal = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            CancellationToken.None)).Single();
        if (terminal.Status != LegacyWorkflowStatus.Completed ||
            selectorCalls != 1 ||
            original.LeaseRequirements.Count != 1 ||
            original.LeaseRequirements[0].PoolName != "database")
        {
            throw new InvalidOperationException(
                "Replacement-host replay reselected or changed the committed lease request.");
        }
    }

    [Phase0Scenario("atomic-multipool-grant", "3.11a")]
    public static async Task MultiPoolAdmissionIsAtomic(Phase0ScenarioContext context)
    {
        const string barrier = "atomic-multipool";
        context.ReleaseBarrier(barrier);
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(
            ("pool-a", 1, (TimeSpan?)null),
            ("pool-b", 1, (TimeSpan?)null));
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var blocked = await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                blocker,
                "external",
                [new ResourcePoolRequirement("pool-a", 1)],
                context.Services.TimeProvider.GetUtcNow(),
                ExpiresAt: null),
            CancellationToken.None);
        if (blocked.Status != ResourcePoolAcquireStatus.Granted)
        {
            throw new InvalidOperationException("The external capacity blocker was not granted.");
        }

        var first = ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-b"));
        var observedRequest = context.Observe(
            _ => ResourceLeaseRequest.Create(
                first,
                ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-a"))));
        ResourceLeaseRequest? request = null;
        Phase0Assert.Satisfies(
            observedRequest,
            created =>
            {
                request = created;
                return created.Requirements.Count == 2;
            },
            "The multi-pool request was not created as one immutable unit.");
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(request!, lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var runtime = CreateRuntime(store, pools, definition, context.Services.TimeProvider);
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
            definition,
            request!.Requirements.Select(requirement => requirement.Pool));
        var observedStart = await context.ObserveAsync(
            _ => definitionHandle.StartOrGetAsync(
                "input",
                StartIdempotencyKey.Create("atomic-multipool"),
                CancellationToken.None));
        Phase0Assert.Satisfies(
            observedStart,
            result => result.GetHandleOrThrow().InstanceId is not null,
            "The queued multi-pool workflow did not produce an instance identity.");

        var poolA = (await pools.GetPoolAsync("pool-a", CancellationToken.None)).Value;
        var poolB = (await pools.GetPoolAsync("pool-b", CancellationToken.None)).Value;
        if (poolA.QueuedWaiters.Count != 1 ||
            poolB.HeldTickets.Count != 0 ||
            poolB.AvailableCapacity != 1)
        {
            throw new InvalidOperationException(
                "A multi-pool admission partially reserved its available subset.");
        }
    }

    [Phase0Scenario("queued-cancellation-zero-ticket", "3.11a")]
    public static async Task QueuedCancellationLeavesZeroTickets(Phase0ScenarioContext context)
    {
        const string barrier = "queued-cancel";
        context.ReleaseBarrier(barrier);
        var fixture = await CreateQueuedFixtureAsync(context, barrier, "queued-cancel");
        var observed = await context.ObserveAsync(
            _ => fixture.Instance.RequestCancellationAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            result => result == WorkflowCancellationRequestStatus.Requested,
            "Queued cancellation did not win one serialized terminal transition.");

        var envelope = await EnvelopeAsync(fixture.Store, fixture.InstanceId);
        var obligation = envelope.OwnedObligations.Single();
        var pool = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (obligation.LeasePhase != "CancelledBeforeGrant" ||
            obligation.LeaseTickets.Count != 0 ||
            pool.QueuedWaiters.Any(waiter => waiter.HolderInstanceId.Equals(fixture.InstanceId)) ||
            pool.HeldTickets.Any(ticket => ticket.HolderInstanceId.Equals(fixture.InstanceId)))
        {
            throw new InvalidOperationException(
                "Queued cancellation retained tickets, capacity, or the exact waiter.");
        }
    }

    [Phase0Scenario("park-only-requesting-fiber", "3.11a")]
    public static async Task OnlyRequestingFiberParks(Phase0ScenarioContext context)
    {
        const string barrier = "park-requesting-fiber";
        context.ReleaseBarrier(barrier);
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        _ = await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                blocker,
                "external",
                [new ResourcePoolRequirement("database", 1)],
                context.Services.TimeProvider.GetUtcNow(),
                ExpiresAt: null),
            CancellationToken.None);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var probe = new LeaseProbe();
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .Parallel<int>(branches => branches
                .Branch(
                    AuthoredBranchId.Create("leased"),
                    snapshot => snapshot.Value,
                    branch => branch
                        .AcquireResources(request, lease => lease.Return(_ => 1)))
                .Branch(
                    AuthoredBranchId.Create("sibling"),
                    snapshot => snapshot.Value,
                    branch => branch
                        .Then<SiblingProbeStep>()
                        .Return(_ => 2)))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            services: new LeaseServices(probe));
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
            definition,
            [ResourcePoolName.Create("database")]);
        var observedStart = await context.ObserveAsync(
            _ => definitionHandle.StartOrGetAsync(
                "input",
                StartIdempotencyKey.Create("park-requesting-fiber"),
                CancellationToken.None));
        WorkflowInstanceHandle? instance = null;
        Phase0Assert.Satisfies(
            observedStart,
            result =>
            {
                instance = result.GetHandleOrThrow();
                return instance.InstanceId is not null;
            },
            "The parked workflow did not return its durable instance identity.");
        var observedSnapshot = await context.ObserveAsync(
            _ => instance!.GetSnapshotAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            observedSnapshot,
            snapshot => snapshot.Status == global::OrcaCore.WorkflowInstanceStatus.Waiting,
            "The workflow did not enter its waiting state after the sibling completed.");
        if (probe.SiblingExecutions != 1)
        {
            throw new InvalidOperationException(
                "A queued branch lease prevented a runnable sibling from progressing.");
        }
    }

    [Phase0Scenario("legal-placement-and-sequential-scopes", "3.11a")]
    public static async Task LegalLeasePlacementsCompileAndRun(Phase0ScenarioContext context)
    {
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var root = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new LeaseState());
        Action<DurableLeaseWorkflowBuilder<string, LeaseState>> rootBody =
            lease => lease.Then<NoOpStep>();
        var observedRoot = context.Observe(_ => root.AcquireResources(request, rootBody));
        Phase0Assert.Satisfies(
            observedRoot,
            successor => ReferenceEquals(successor, root),
            "Root lease placement did not preserve the authoring session.");

        Phase0Observation<DurableBranchBuilder<LeaseState, int>>? observedBranch = null;
        var afterParallel = root
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("leased-branch"),
                snapshot => snapshot.Value,
                branch =>
                {
                    Action<DurableLeaseBranchBuilder<LeaseState, int>> body =
                        lease => lease.Return(_ => 1);
                    observedBranch = context.Observe(
                        _ => branch.AcquireResources(request, body));
                }))
            .WhenAll((snapshot, _) => snapshot.Value);
        Phase0Assert.Satisfies(
            observedBranch ?? throw new InvalidOperationException("Branch body was not authored."),
            branch => branch is not null,
            "Root Parallel branch lease placement was rejected.");

        Phase0Observation<DurableItemBuilder<LeaseState, int>>? observedItem = null;
        var definition = afterParallel
            .ForEach<int, LeaseState, int>(
                _ => [1],
                ForEachOptions.Create(1),
                item => new LeaseState { Value = item.Item },
                item =>
                {
                    Action<DurableLeaseItemBuilder<LeaseState, int>> body =
                        lease => lease.Return(snapshot => snapshot.Value.Value);
                    observedItem = context.Observe(
                        _ => item.AcquireResources(request, body));
                })
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        Phase0Assert.Satisfies(
            observedItem ?? throw new InvalidOperationException("Item body was not authored."),
            item => item is not null,
            "Root ForEach item lease placement was rejected.");

        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var runtime = CreateRuntime(store, pools, definition, TimeProvider.System);
        var started = await runtime.StartOrGetAsync<string, LeaseState>(
            "legal-lease-placements",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            CancellationToken.None)).Single();
        if (snapshot.Status != LegacyWorkflowStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Legal sequential lease placements did not complete: {snapshot.Status}.");
        }
    }

    [Phase0Scenario("leased-body-omissions", "3.11a")]
    public static void LeasedBuildersOmitCapacityExpandingMembers(Phase0ScenarioContext context)
    {
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var root = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new LeaseState());
        Action<DurableLeaseWorkflowBuilder<string, LeaseState>> body =
            lease => lease.Then<NoOpStep>();
        var observedAcquire = context.Observe(_ => root.AcquireResources(request, body));
        Phase0Assert.Satisfies(
            observedAcquire,
            successor => ReferenceEquals(successor, root),
            "Static AcquireResources did not return the same authoring session.");

        foreach (var type in new[]
        {
            typeof(DurableLeaseWorkflowBuilder<,>),
            typeof(DurableLeaseNestedBuilder<,>),
            typeof(DurableLeaseBranchBuilder<,>),
            typeof(DurableLeaseItemBuilder<,>)
        })
        {
            var forbidden = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name is
                    "Parallel" or "ForEach" or "AcquireResources" or "ContinueAsNew")
                .Select(method => method.Name)
                .Distinct()
                .ToArray();
            if (forbidden.Length != 0)
            {
                throw new InvalidOperationException(
                    $"{type.Name} exposed forbidden members: {string.Join(", ", forbidden)}.");
            }
        }

        var completion = root.End();
        var observedBuild = context.Observe(_ => completion.TryBuild());
        Phase0Assert.Satisfies(
            observedBuild,
            result => result.TryGetValue(out _),
            "A legal leased body did not compile.");
    }

    [Phase0Scenario("ancestry-and-rollover-runtime-defense", "3.11a")]
    public static async Task RuntimeRejectsForgedAncestryAndRolloverState(Phase0ScenarioContext context)
    {
        const string barrier = "lease-rollover";
        context.ReleaseBarrier(barrier);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(request, lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            DurableDriverBudget.Default);
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
            definition,
            [ResourcePoolName.Create("database")]);
        var observedStart = await context.ObserveAsync(
            _ => definitionHandle.StartOrGetAsync(
                "input",
                StartIdempotencyKey.Create("lease-rollover"),
                CancellationToken.None));
        WorkflowInstanceHandle? instance = null;
        Phase0Assert.Satisfies(
            observedStart,
            result =>
            {
                instance = result.GetHandleOrThrow();
                return instance.InstanceId is not null;
            },
            "A fully exited lease could not advance to terminal progression.");
        var observedSnapshot = await context.ObserveAsync(
            _ => instance!.GetSnapshotAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            observedSnapshot,
            snapshot => snapshot.Status == global::OrcaCore.WorkflowInstanceStatus.Completed,
            "Terminal progression after a fully exited lease failed.");
        var envelope = await EnvelopeAsync(store, instance!.InstanceId);
        if (envelope.OwnedObligations.Any(obligation =>
                obligation.Kind == DurableOwnedObligationKind.Resource &&
                obligation.LeasePhase != "Released"))
        {
            throw new InvalidOperationException(
                "Terminal progression retained a live capacity ancestor.");
        }

        await AssertForgedLeaseAncestryFailsBeforePoolMutationAsync(context);
        await AssertForgedNonQuiescentRolloverFailsAsync(context);
    }

    private static async Task AssertForgedLeaseAncestryFailsBeforePoolMutationAsync(
        Phase0ScenarioContext context)
    {
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new LeaseState())
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("forged-ancestry"),
                snapshot => snapshot.Value,
                branch => branch.AcquireResources(
                    request,
                    lease => lease.Return(_ => 1))))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new DurableDriverBudget(1, TimeSpan.FromMinutes(1)));
        var started = await runtime.StartOrGetAsync<string, LeaseState>(
            "forged-lease-ancestry",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        DurableExecutionEnvelopeV2? candidate = null;
        CheckpointWrite? checkpoint = null;
        for (var turn = 0; turn < 12; turn++)
        {
            var checkpointOption = await store.LoadCheckpointAsync(
                started.InstanceId,
                CancellationToken.None);
            if (checkpointOption.HasValue)
            {
                checkpoint = checkpointOption.Value;
                var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
                var scope = envelope.Scopes.SingleOrDefault(scope =>
                    scope.Phase == DurableExecutionScopePhase.Running &&
                    scope.ChildFiberIds.Count == 1);
                if (scope is not null &&
                    envelope.OwnedObligations.Count == 0 &&
                    envelope.Fibers.Single(fiber =>
                        fiber.FiberId == scope.ChildFiberIds[0]).Phase == DurableFiberPhase.Runnable)
                {
                    candidate = envelope;
                    break;
                }
            }

            _ = await runtime.StartOrGetAsync<string, LeaseState>(
                "forged-lease-ancestry",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                CancellationToken.None);
        }

        if (candidate is null || checkpoint is null)
        {
            throw new InvalidOperationException(
                "Could not freeze the branch immediately before scoped lease admission.");
        }

        var activeScope = candidate.Scopes.Single(scope =>
            scope.Phase == DurableExecutionScopePhase.Running);
        var root = candidate.Fibers.Single(fiber =>
            fiber.FiberId == activeScope.ParentFiberId);
        var forged = candidate with
        {
            OwnedObligations =
            [
                new DurableOwnedObligationState
                {
                    Kind = DurableOwnedObligationKind.Resource,
                    ObligationId = WaitId.Parse(Guid.CreateVersion7().ToString()).ToString(),
                    FiberId = root.FiberId,
                    InstructionId = root.InstructionId,
                    AuthoredPath = "workflow:$",
                    LeasePhase = "Held",
                    HolderKey = "forged-holder",
                    ProtectionToken = "lease-forged-ancestor",
                    LeaseRequirements = [new ResourcePoolRequirement("database", 1)],
                    RegistrationSequence = 1
                }
            ],
            NextRegistrationSequence = 2
        };
        await ReplaceCheckpointAsync(store, checkpoint, forged);

        var replacement = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider);
        _ = await replacement.StartOrGetAsync<string, LeaseState>(
            "forged-lease-ancestry",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var failed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            CancellationToken.None)).Single();
        var pool = (await pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (failed.Status != LegacyWorkflowStatus.Failed ||
            failed.ErrorSummary is null ||
            !failed.ErrorSummary.Contains("SFE-RUN-002", StringComparison.Ordinal) ||
            pool.AvailableCapacity != 1 ||
            pool.HeldTickets.Count != 0)
        {
            throw new InvalidOperationException(
                "Forged live lease ancestry did not fail before pool mutation with SFE-RUN-002.");
        }
    }

    private static async Task AssertForgedNonQuiescentRolloverFailsAsync(
        Phase0ScenarioContext context)
    {
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new LeaseState())
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("rollover-child"),
                snapshot => snapshot.Value.Value,
                branch => branch.Return(snapshot => snapshot.Value)))
            .WhenAll((snapshot, _) => snapshot.Value)
            .ContinueAsNew(snapshot => snapshot.Value)
            .Build();
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync();
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new DurableDriverBudget(1, TimeSpan.FromMinutes(1)));
        var started = await runtime.StartOrGetAsync<string, LeaseState>(
            "forged-rollover",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        DurableExecutionEnvelopeV2? candidate = null;
        CheckpointWrite? checkpoint = null;
        DurableExecutionScopeState? scopeTemplate = null;
        DurableFiberState? childTemplate = null;
        for (var turn = 0; turn < 16; turn++)
        {
            var checkpointOption = await store.LoadCheckpointAsync(
                started.InstanceId,
                CancellationToken.None);
            if (checkpointOption.HasValue)
            {
                checkpoint = checkpointOption.Value;
                var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
                if (envelope.Scopes.Count == 1)
                {
                    scopeTemplate = envelope.Scopes[0];
                    childTemplate = envelope.Fibers.Single(fiber =>
                        fiber.FiberId == scopeTemplate.ChildFiberIds.Single());
                }

                var root = envelope.Fibers.Single(fiber =>
                    fiber.FiberId == envelope.RootFiberId);
                if (envelope.ContinueAsNewGeneration == 0 &&
                    envelope.OwnedObligations.Count == 0 &&
                    envelope.Scopes.Count == 0 &&
                    root.Phase == DurableFiberPhase.Runnable &&
                    root.InstructionId.Contains("ContinueAsNew", StringComparison.Ordinal) &&
                    scopeTemplate is not null &&
                    childTemplate is not null)
                {
                    candidate = envelope;
                    break;
                }
            }

            _ = await runtime.StartOrGetAsync<string, LeaseState>(
                "forged-rollover",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                CancellationToken.None);
        }

        if (candidate is null || checkpoint is null)
        {
            throw new InvalidOperationException(
                "Could not freeze the root immediately before ContinueAsNew.");
        }

        var completedScope = scopeTemplate!;
        var childId = completedScope.ChildFiberIds.Single();
        var waitId = WaitId.Parse(Guid.CreateVersion7().ToString());
        var forged = candidate with
        {
            Fibers =
            [
                .. candidate.Fibers,
                childTemplate! with
                {
                    Phase = DurableFiberPhase.Blocked,
                    Blocked = new DurableFiberBlock
                    {
                        Reason = DurableFiberBlockedReason.Wait,
                        ObligationId = waitId.ToString()
                    },
                    ResultPayload = null
                }
            ],
            Scopes =
            [
                completedScope with
                {
                    Phase = DurableExecutionScopePhase.Running,
                    CommittedResults = []
                }
            ],
            OwnedObligations =
            [
                new DurableOwnedObligationState
                {
                    Kind = DurableOwnedObligationKind.Wait,
                    ObligationId = waitId.ToString(),
                    FiberId = childId,
                    ScopeId = completedScope.ScopeId,
                    RegistrationSequence = 1
                }
            ],
            NextRegistrationSequence = 2
        };
        await ReplaceCheckpointAsync(store, checkpoint, forged);

        var replacement = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider);
        _ = await replacement.StartOrGetAsync<string, LeaseState>(
            "forged-rollover",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var failed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            CancellationToken.None)).Single();
        var failedCheckpoint = (await store.LoadCheckpointAsync(
            started.InstanceId,
            CancellationToken.None)).Value;
        var failedEnvelope = DurableExecutionEnvelopeV2.Deserialize(failedCheckpoint.Payload);
        if (failed.Status != LegacyWorkflowStatus.Failed ||
            failedCheckpoint.ErrorSummary is null ||
            !failedCheckpoint.ErrorSummary.Contains("SFE-RUN-001", StringComparison.Ordinal) ||
            failedEnvelope.ContinueAsNewGeneration != 0 ||
            failedEnvelope.OwnedObligations.Count != 1)
        {
            throw new InvalidOperationException(
                "Forged non-quiescent rollover did not fail without changing generation or ownership: " +
                $"status={failed.Status}; error={failedCheckpoint.ErrorSummary}; " +
                $"generation={failedEnvelope.ContinueAsNewGeneration}; " +
                $"obligations={failedEnvelope.OwnedObligations.Count}.");
        }
    }

    private static async Task ReplaceCheckpointAsync(
        InMemoryWorkflowProvider store,
        CheckpointWrite checkpoint,
        DurableExecutionEnvelopeV2 envelope)
    {
        var result = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(checkpoint.InstanceId),
                ExpectedVersion = checkpoint.StreamVersion,
                Checkpoint = checkpoint with
                {
                    ContentType = DurableExecutionEnvelopeV2.ContentType,
                    Payload = envelope.Serialize()
                }
            },
            CancellationToken.None);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Could not commit a forged runtime-defense checkpoint.");
        }
    }

    private static async Task<QueuedFixture> CreateQueuedFixtureAsync(
        Phase0ScenarioContext context,
        string barrier,
        string key)
    {
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        _ = await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                blocker,
                "external",
                [new ResourcePoolRequirement("database", 1)],
                context.Services.TimeProvider.GetUtcNow(),
                ExpiresAt: null),
            CancellationToken.None);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(request, lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            processor: processor);
        var started = await runtime.StartOrGetAsync<string, LeaseState>(
            key,
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            store,
            store,
            new DurableManagement(store, pools, store, processor),
            context.Services.TimeProvider,
            definition,
            [ResourcePoolName.Create("database")]);
        var instance = await definitionHandle.GetInstanceAsync(
            started.InstanceId,
            CancellationToken.None);
        return new QueuedFixture(store, pools, processor, started.InstanceId, instance);
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        IResourcePoolStore pools,
        DurableWorkflowDefinition<string> definition,
        TimeProvider timeProvider,
        DurableDriverBudget? budget = null,
        IServiceProvider? services = null,
        DurableCommandProcessor? processor = null)
    {
        var runtime = new DurableWorkflowRuntime(
            processor ?? new DurableCommandProcessor(store, pools),
            new DurableDefinitionRegistry(services ?? new LeaseServices(new LeaseProbe())),
            timeProvider,
            budget ?? DurableDriverBudget.Default);
        runtime.RegisterDefinition(RuntimeDefinition<LeaseState>(definition));
        return runtime;
    }

    private static async Task<InMemoryResourcePoolStore> CreatePoolsAsync(
        params (string Name, int Capacity, TimeSpan? ReviewAfter)[] definitions)
    {
        var pools = new InMemoryResourcePoolStore();
        foreach (var definition in definitions)
        {
            await pools.UpsertPoolAsync(
                new ResourcePoolDefinition(
                    definition.Name,
                    definition.Capacity,
                    definition.ReviewAfter),
                CancellationToken.None);
        }

        return pools;
    }

    private static WorkflowDefinition<TState> RuntimeDefinition<TState>(object publicDefinition) =>
        (WorkflowDefinition<TState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;

    private static async Task<DurableExecutionEnvelopeV2> EnvelopeAsync(
        IWorkflowEventStore store,
        InstanceId instanceId)
    {
        var checkpoint = await store.LoadCheckpointAsync(instanceId, CancellationToken.None);
        return DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
    }

    private static void ConsumeBarrier(Phase0ScenarioContext context, string name) =>
        context.Services.Barrier.ReachAsync(name).GetAwaiter().GetResult();

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
    }

    public sealed class LeaseState
    {
        public int Value { get; set; }
    }

    private sealed class LeaseProbe
    {
        internal int SiblingExecutions;
    }

    private sealed class NoOpStep : IStep<LeaseState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<LeaseState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class SiblingProbeStep(LeaseProbe probe) : IStep<LeaseState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<LeaseState> context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref probe.SiblingExecutions);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class LeaseServices(LeaseProbe probe) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(NoOpStep))
            {
                return new NoOpStep();
            }

            if (serviceType == typeof(SiblingProbeStep))
            {
                return new SiblingProbeStep(probe);
            }

            return null;
        }
    }

    private sealed record QueuedFixture(
        InMemoryWorkflowProvider Store,
        InMemoryResourcePoolStore Pools,
        DurableCommandProcessor Processor,
        InstanceId InstanceId,
        WorkflowInstanceHandle Instance);
}
