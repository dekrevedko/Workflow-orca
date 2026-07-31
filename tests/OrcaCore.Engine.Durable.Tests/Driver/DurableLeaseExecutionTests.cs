using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableLeaseExecutionTests
{
    [Fact]
    public async Task ScopedLease_ExposesStableProtectionToken_AndReleasesBeforeParentContinues()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var observation = new LeaseObservation();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var definition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(
                request,
                lease => lease.Then<ObserveLeaseStep>())
            .Then<ObserveReleasedCapacityStep>()
            .End()
            .Build();
        var processor = new DurableCommandProcessor(store, pools);
        var runtimeDefinition = (WorkflowDefinition<State>)definition.RuntimeDefinition;
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(new LeaseStepServices(pools, observation)),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        runtime.RegisterDefinition(runtimeDefinition);

        var started = await runtime.StartOrGetAsync<string, State>(
            "scoped-lease-release-order",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var pool = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        observation.ProtectionToken.Should().NotBeNullOrWhiteSpace();
        observation.AvailableCapacitySeenByParent.Should().Be(1);
        pool.AvailableCapacity.Should().Be(1);
        pool.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectorRequest_IsCommittedOnce_AndReusedAfterHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var selectorCalls = 0;
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(
                _ =>
                {
                    selectorCalls++;
                    return request;
                },
                lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var definition = (WorkflowDefinition<State>)publicDefinition.RuntimeDefinition;
        var budget = new DurableDriverBudget(1, TimeSpan.FromMinutes(1));
        var firstHost = CreateRuntime(store, pools, definition, budget);

        var started = await firstHost.StartOrGetAsync<string, State>(
            "selector-commit-replay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        (await firstHost.DriveAsync(
            started.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken)).Outcome
            .Should().Be(DurableSegmentOutcome.BudgetExhausted);
        var committedRequest = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                started.InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload);
        var persistedLease = committedRequest.OwnedObligations.Should().ContainSingle(
            obligation => obligation.ProtectionToken != null).Subject;

        var replacement = CreateRuntime(store, pools, definition, DurableDriverBudget.Default);
        (await replacement.DriveAsync(
            started.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken)).Outcome
            .Should().Be(DurableSegmentOutcome.Terminal);
        var completed = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                started.InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload);

        selectorCalls.Should().Be(1);
        persistedLease.LeaseRequirements.Should().ContainSingle()
            .Which.PoolName.Should().Be("database");
        persistedLease.ProtectionToken.Should().NotBeNullOrWhiteSpace();
        completed.OwnedObligations.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownPools_FailWithCompleteSortedTypedSet_BeforeLeaseMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("zeta")),
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("alpha")));
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var definition = (WorkflowDefinition<State>)publicDefinition.RuntimeDefinition;
        var runtime = CreateRuntime(store, pools, definition, DurableDriverBudget.Default);

        Func<Task> act = () => runtime.StartOrGetAsync<string, State>(
            "unknown-lease-pools",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<ResourcePoolNotConfiguredException>())
            .Which;
        exception.Code.Should().Be("WF-RESOURCE-POOL-NOT-CONFIGURED");
        exception.MissingPools.Select(pool => pool.Value).Should().Equal("alpha", "zeta");
        (await pools.ListPoolsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        var started = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            TestContext.Current.CancellationToken)).Should().ContainSingle().Subject;
        (await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken)).HasValue.Should().BeFalse(
            "the invalid request must fail before its lease obligation is committed");
    }

    [Fact]
    public async Task DirectTicketTransfer_ResumesOnlyQueuedFiber_AfterHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var requirements = new[] { new ResourcePoolRequirement("database", 1) };
        (await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                blocker,
                "external-blocker",
                requirements,
                DateTimeOffset.UtcNow,
                ExpiresAt: null),
            TestContext.Current.CancellationToken)).Status.Should().Be(ResourcePoolAcquireStatus.Granted);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var definition = (WorkflowDefinition<State>)publicDefinition.RuntimeDefinition;
        var firstHost = CreateRuntime(store, pools, definition, DurableDriverBudget.Default);

        var started = await firstHost.StartOrGetAsync<string, State>(
            "queued-lease-transfer",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        var queuedPool = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;
        queuedPool.QueuedWaiters.Should().ContainSingle(
            waiter => waiter.HolderInstanceId == started.InstanceId);

        var released = await pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(blocker, "external-blocker", DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);
        released.GrantedWaiters.Should().ContainSingle(
            waiter => waiter.HolderInstanceId == started.InstanceId);

        var replacement = CreateRuntime(store, pools, definition, DurableDriverBudget.Default);
        (await replacement.DriveAsync(
            started.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken)).Outcome.Should()
            .Be(DurableSegmentOutcome.Terminal);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var finalPool = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        finalPool.AvailableCapacity.Should().Be(1);
        finalPool.HeldTickets.Should().BeEmpty();
        finalPool.QueuedWaiters.Should().BeEmpty();
    }

    [Fact]
    public async Task QueuedBranchLease_DoesNotParkRunnableSibling()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        (await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                blocker,
                "external-blocker",
                [new ResourcePoolRequirement("database", 1)],
                DateTimeOffset.UtcNow,
                ExpiresAt: null),
            TestContext.Current.CancellationToken)).Status.Should().Be(
                ResourcePoolAcquireStatus.Granted);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var sibling = new SiblingProgress();
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Parallel<int>(branches => branches
                .Branch(
                    AuthoredBranchId.Create("leased"),
                    snapshot => snapshot.Value,
                    branch => branch.AcquireResources(
                        request,
                        lease => lease.Return(_ => 1)))
                .Branch(
                    AuthoredBranchId.Create("sibling"),
                    snapshot => snapshot.Value,
                    branch => branch
                        .Then<RecordSiblingStep>()
                        .Return(_ => 2)))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var definition = (WorkflowDefinition<State>)publicDefinition.RuntimeDefinition;
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store, pools),
            new DurableDefinitionRegistry(new LeaseStepServices(
                pools,
                new LeaseObservation(),
                siblingProgress: sibling)),
            TimeProvider.System,
            DurableDriverBudget.Default);
        runtime.RegisterDefinition(definition);

        var started = await runtime.StartOrGetAsync<string, State>(
            "queued-branch-sibling",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        var pending = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        pending.Status.Should().Be(WorkflowStatus.Waiting);
        sibling.Executions.Should().Be(1);
        (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value.QueuedWaiters.Should()
            .ContainSingle(waiter => waiter.HolderInstanceId.Equals(started.InstanceId));
    }

    [Fact]
    public async Task Cancellation_RemovesExactQueuedHolder_AndRetainsCancelledBeforeGrantTombstone()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        (await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                blocker,
                "external-blocker",
                [new ResourcePoolRequirement("database", 1)],
                DateTimeOffset.UtcNow,
                ExpiresAt: null),
            TestContext.Current.CancellationToken)).Status.Should().Be(ResourcePoolAcquireStatus.Granted);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var definition = (WorkflowDefinition<State>)global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<NoOpStep>())
            .End()
            .Build()
            .RuntimeDefinition;
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(new LeaseStepServices(pools, new LeaseObservation())),
            TimeProvider.System,
            DurableDriverBudget.Default);
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, State>(
            "cancel-queued-holder",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        var queued = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;
        queued.QueuedWaiters.Should().ContainSingle(
            waiter => waiter.HolderInstanceId.Equals(started.InstanceId));

        (await processor.ProcessAsync(
            new CancelWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = started.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken)).Outcome.Should().Be(DurableCommandOutcome.Committed);

        var projection = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                started.InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload);
        var after = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;
        projection.Status.Should().Be(WorkflowStatus.Cancelled);
        checkpoint.OwnedObligations.Should().ContainSingle()
            .Which.LeasePhase.Should().Be(nameof(DurableLeaseObligationPhase.CancelledBeforeGrant));
        after.QueuedWaiters.Should().BeEmpty();
        after.HeldTickets.Should().ContainSingle(ticket =>
            ticket.HolderInstanceId.Equals(blocker));
        after.AvailableCapacity.Should().Be(0);
    }

    [Fact]
    public async Task Termination_QuarantinesProtectedCapacity_UntilPhysicalReturnIsConfirmed()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var gate = new LeasedRetryGate();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var definition = (WorkflowDefinition<State>)global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<LeasedRetryStep>())
            .End()
            .Build()
            .RuntimeDefinition;
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(new LeaseStepServices(
                pools,
                new LeaseObservation(),
                gate)),
            TimeProvider.System,
            DurableDriverBudget.Default);
        runtime.RegisterDefinition(definition);
        var running = runtime.StartOrGetAsync<string, State>(
            "terminate-active-lease",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        await gate.FirstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            TestContext.Current.CancellationToken)).Single().InstanceId;

        (await processor.ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken)).Outcome.Should().Be(DurableCommandOutcome.Committed);

        var checkpoint = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                instanceId,
                TestContext.Current.CancellationToken)).Value.Payload);
        var lease = checkpoint.OwnedObligations.Should().ContainSingle().Subject;
        var pool = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;
        lease.LeasePhase.Should().Be(nameof(DurableLeaseObligationPhase.Quarantined));
        pool.AvailableCapacity.Should().Be(0);
        pool.HeldTickets.Should().ContainSingle();

        gate.ReleaseFirst.TrySetResult();
        await running;
        var confirmation = await new DurableResourceLeaseRecovery(processor, store)
            .ConfirmProtectedWorkStoppedAsync(
                LeaseProtectionToken.Parse(lease.ProtectionToken!),
                StopConfirmationId.Create("terminated-step-returned"),
                TestContext.Current.CancellationToken);
        confirmation.Should().Be(ProtectedWorkStopConfirmationStatus.Released);
        (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value.AvailableCapacity.Should().Be(1);
    }

    [Fact]
    public async Task BranchLease_ReleasesInExplicitScopeExitCommit_BeforeMerge()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("leased"),
                snapshot => snapshot.Value,
                branch => branch.AcquireResources(
                    request,
                    lease => lease.Return(_ => 1))))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var definition = (WorkflowDefinition<State>)publicDefinition.RuntimeDefinition;
        var runtime = CreateRuntime(store, pools, definition, DurableDriverBudget.Default);

        var started = await runtime.StartOrGetAsync<string, State>(
            "branch-lease-release",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(started.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var releaseStep = events.OfType<WorkflowStepCompletedEvent>().Should()
            .ContainSingle(workflowEvent =>
                workflowEvent.StepPath.EndsWith("/release", StringComparison.Ordinal))
            .Subject;
        var release = events.OfType<WorkflowResourcePoolReleasedEvent>().Should()
            .ContainSingle()
            .Subject;
        var mergeIndex = events.ToList().FindIndex(workflowEvent =>
            workflowEvent is WorkflowStepCompletedEvent completed &&
            completed.StepPath.EndsWith(":merge", StringComparison.Ordinal));

        release.CommandId.Should().Be(releaseStep.CommandId);
        events.ToList().IndexOf(release).Should().BeLessThan(mergeIndex);
    }

    [Fact]
    public async Task MixedPoolReviewDeadline_MarksOnlyDueTicket_WithoutReclaimingCapacity()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("fast", 1, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("slow", 1, TimeSpan.FromMinutes(2)),
            TestContext.Current.CancellationToken);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("fast")),
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("slow")));
        var definition = (WorkflowDefinition<State>)global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<LeasedRetryStep>())
            .End()
            .Build()
            .RuntimeDefinition;
        var gate = new LeasedRetryGate();
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 19, 0, 0, TimeSpan.Zero));
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(new LeaseStepServices(
                pools,
                new LeaseObservation(),
                gate)),
            clock.TimeProvider,
            DurableDriverBudget.Default);
        runtime.RegisterDefinition(definition);

        var running = runtime.StartOrGetAsync<string, State>(
            "mixed-review-marks",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        var firstAttemptOrTerminal = await Task.WhenAny(
            gate.FirstStarted.Task,
            running,
            Task.Delay(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken));
        if (firstAttemptOrTerminal == running)
        {
            var early = (await store.ListAsync(
                new WorkflowProjectionQuery(),
                TestContext.Current.CancellationToken)).Single();
            throw new InvalidOperationException(
                $"Leased attempt terminalized before dispatch: {early.Status}: {early.ErrorSummary}");
        }

        firstAttemptOrTerminal.Should().Be(gate.FirstStarted.Task);
        await pools.ExpireTicketsAsync(
            clock.TimeProvider.GetUtcNow() + TimeSpan.FromSeconds(90),
            TestContext.Current.CancellationToken);
        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            TestContext.Current.CancellationToken)).Single().InstanceId;
        var envelope = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                instanceId,
                TestContext.Current.CancellationToken)).Value.Payload);
        var token = LeaseProtectionToken.Parse(
            envelope.OwnedObligations.Single(candidate => candidate.ProtectionToken != null)
                .ProtectionToken!);
        var snapshot = await new DurableResourceLeaseDiagnostics(processor, store)
            .GetAsync(token, TestContext.Current.CancellationToken);

        snapshot.Should().NotBeNull();
        snapshot!.Tickets.Should().HaveCount(2);
        snapshot.Tickets.Single(ticket => ticket.Pool.Value == "fast")
            .ReviewMarked.Should().BeTrue();
        snapshot.Tickets.Single(ticket => ticket.Pool.Value == "slow")
            .ReviewMarked.Should().BeFalse();
        (await pools.GetPoolAsync("fast", TestContext.Current.CancellationToken))
            .Value.AvailableCapacity.Should().Be(0);
        (await pools.GetPoolAsync("slow", TestContext.Current.CancellationToken))
            .Value.AvailableCapacity.Should().Be(0);

        gate.ReleaseFirst.TrySetResult();
        (await running).InstanceId.Should().Be(instanceId);
        (await pools.GetPoolAsync("fast", TestContext.Current.CancellationToken))
            .Value.AvailableCapacity.Should().Be(1);
        (await pools.GetPoolAsync("slow", TestContext.Current.CancellationToken))
            .Value.AvailableCapacity.Should().Be(1);
    }

    [Fact]
    public async Task MissingExactProviderTicket_CommitsLeaseLostBeforeRedispatch()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new TicketHidingResourcePoolStore(new InMemoryResourcePoolStore());
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var definition = (WorkflowDefinition<State>)global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<LeasedRetryStep>())
            .End()
            .Build()
            .RuntimeDefinition;
        var lostHostGate = new LeasedRetryGate();
        var firstHost = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store, pools),
            new DurableDefinitionRegistry(new LeaseStepServices(
                pools,
                new LeaseObservation(),
                lostHostGate)),
            TimeProvider.System,
            DurableDriverBudget.Default);
        firstHost.RegisterDefinition(definition);

        var abandoned = firstHost.StartOrGetAsync<string, State>(
            "missing-ticket-lease-lost",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        await lostHostGate.FirstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            TestContext.Current.CancellationToken)).Single().InstanceId;
        var exactTicket = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value.HeldTickets.Single();
        pools.HideTicket(exactTicket.TicketId);

        var replacementGate = new LeasedRetryGate();
        var replacement = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store, pools),
            new DurableDefinitionRegistry(new LeaseStepServices(
                pools,
                new LeaseObservation(),
                replacementGate)),
            TimeProvider.System,
            DurableDriverBudget.Default);
        replacement.RegisterDefinition(definition);
        (await replacement.DriveAsync(
            instanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken)).Outcome.Should()
            .Be(DurableSegmentOutcome.Terminal);
        var failed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken)).Single();

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ErrorSummary.Should().Contain("WF-LEASE-LOST");
        replacementGate.FirstStarted.Task.IsCompleted.Should().BeFalse();

        lostHostGate.ReleaseFirst.TrySetResult();
        await abandoned;
    }

    [Fact]
    public async Task TimedOutLeasedAttempt_WaitsForPhysicalReturn_AndQuarantinesAfterSuccessfulRetry()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 18, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var gate = new LeasedRetryGate();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(
                request,
                lease => lease
                    .Then<LeasedRetryStep>()
                    .WithRetry(2)
                    .WithStepTimeout(TimeSpan.FromMinutes(1)))
            .End()
            .Build();
        var definition = (WorkflowDefinition<State>)publicDefinition.RuntimeDefinition;
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store, pools),
            new DurableDefinitionRegistry(new LeaseStepServices(
                pools,
                new LeaseObservation(),
                gate)),
            clock.TimeProvider,
            DurableDriverBudget.Default);
        runtime.RegisterDefinition(definition);

        var run = runtime.StartOrGetAsync<string, State>(
            "leased-ambiguous-retry",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);
        var firstAttemptOrTerminal = await Task.WhenAny(
            gate.FirstStarted.Task,
            run,
            Task.Delay(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken));
        if (firstAttemptOrTerminal == run)
        {
            var early = (await store.ListAsync(
                new WorkflowProjectionQuery(),
                TestContext.Current.CancellationToken)).Single();
            throw new InvalidOperationException(
                $"Leased attempt terminalized before dispatch: {early.Status}: {early.ErrorSummary}");
        }

        firstAttemptOrTerminal.Should().Be(gate.FirstStarted.Task);
        var activeEnvelope = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                (await store.ListAsync(
                    new WorkflowProjectionQuery(),
                    TestContext.Current.CancellationToken)).Single().InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload);
        var activeLease = activeEnvelope.OwnedObligations.Should()
            .ContainSingle(obligation => obligation.ProtectionToken != null).Subject;
        var activeProcessor = new DurableCommandProcessor(store, pools);
        var activeRecovery = new DurableResourceLeaseRecovery(activeProcessor, store);
        (await activeRecovery.ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken.Parse(activeLease.ProtectionToken!),
            StopConfirmationId.Create("premature-proof"),
            TestContext.Current.CancellationToken)).Should()
            .Be(ProtectedWorkStopConfirmationStatus.NotConfirmable);
        var diagnostics = new DurableResourceLeaseDiagnostics(activeProcessor, store);
        var activeSnapshot = await diagnostics.GetAsync(
            LeaseProtectionToken.Parse(activeLease.ProtectionToken!),
            TestContext.Current.CancellationToken);
        activeSnapshot.Should().NotBeNull();
        activeSnapshot!.Status.Should().Be(DurableResourceLeaseObligationStatus.Held);
        activeSnapshot.Tickets.Should().ContainSingle()
            .Which.ProviderGeneration.Should().BePositive();
        clock.Advance(TimeSpan.FromMinutes(1));
        await Task.Yield();

        gate.SecondStarted.Task.IsCompleted.Should().BeFalse(
            "a token-ignoring leased body must physically return before its retry starts");
        gate.ReleaseFirst.TrySetResult();
        await gate.SecondStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var completed = await run;
        var checkpoint = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                completed.InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload);
        var lease = checkpoint.OwnedObligations.Should().ContainSingle(
            obligation => obligation.ProtectionToken != null).Subject;
        var pool = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;

        lease.LeasePhase.Should().Be("Quarantined");
        gate.Attempts.Should().Be(2);
        pool.AvailableCapacity.Should().Be(0);
        pool.HeldTickets.Should().ContainSingle();

        var recovery = new DurableResourceLeaseRecovery(
            new DurableCommandProcessor(store, pools),
            store);
        var confirmation = StopConfirmationId.Create("stop-proof-1");
        (await recovery.ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken.Parse(lease.ProtectionToken!),
            confirmation,
            TestContext.Current.CancellationToken)).Should()
            .Be(ProtectedWorkStopConfirmationStatus.Released);
        (await recovery.ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken.Parse(lease.ProtectionToken!),
            confirmation,
            TestContext.Current.CancellationToken)).Should()
            .Be(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed);
        (await recovery.ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken.Parse(lease.ProtectionToken!),
            StopConfirmationId.Create("stop-proof-2"),
            TestContext.Current.CancellationToken)).Should()
            .Be(ProtectedWorkStopConfirmationStatus.AlreadyConfirmed);
        (await recovery.ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken.Parse("lease-unknown"),
            confirmation,
            TestContext.Current.CancellationToken)).Should()
            .Be(ProtectedWorkStopConfirmationStatus.ConfirmationConflict);
        (await recovery.ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken.Parse("lease-unknown"),
            StopConfirmationId.Create("stop-proof-3"),
            TestContext.Current.CancellationToken)).Should()
            .Be(ProtectedWorkStopConfirmationStatus.TokenNotFound);
        (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value.AvailableCapacity.Should().Be(1);
        var tombstone = DurableExecutionEnvelopeV2.Deserialize(
            (await store.LoadCheckpointAsync(
                completed.InstanceId,
                TestContext.Current.CancellationToken)).Value.Payload)
            .OwnedObligations.Should().ContainSingle().Subject;
        tombstone.LeasePhase.Should().Be("Released");
        tombstone.AcceptedConfirmationId.Should().Be("stop-proof-1");
    }

    [Fact]
    public async Task ForgedLiveAncestorLease_FailsBeforePoolMutationWithRuntimeDiagnostic()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("database", 1, LeaseDuration: null),
            TestContext.Current.CancellationToken);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("forged-ancestry"),
                snapshot => snapshot.Value,
                branch => branch.AcquireResources(
                    request,
                    lease => lease.Return(_ => 1))))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var definition = (WorkflowDefinition<State>)publicDefinition.RuntimeDefinition;
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(
            new StartWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = TimeProvider.System.GetUtcNow(),
                DefinitionId = definition.DefinitionId,
                DefinitionVersion = definition.DefinitionVersion
            },
            TestContext.Current.CancellationToken);
        var aggregate = await new DurableAggregateLoader(store).LoadAsync(
            instanceId,
            TestContext.Current.CancellationToken);

        var scopePlan = definition.CompiledPlan.Scopes.Single();
        var scopeEntry = StructuredExecutionState.Create(
            instanceId,
            0,
            scopePlan.JoinInstructionId);
        var startedScope = ScopeReducer.StartScope(
            scopeEntry,
            scopeEntry.RootFiberId,
            scopePlan);
        var child = startedScope.State.Fibers[startedScope.ChildFiberIds.Single()];
        definition.CompiledPlan.GetInstruction(child.InstructionId).Kind
            .Should().Be(CompiledInstructionKind.AcquireResources);
        var forgedAncestor = new DurableOwnedObligationState
        {
            Kind = DurableOwnedObligationKind.Resource,
            ObligationId = WaitId.Parse(Guid.CreateVersion7().ToString()).ToString(),
            FiberId = startedScope.State.RootFiberId.Value,
            InstructionId = definition.CompiledPlan.Instructions[0].Id.Value,
            AuthoredPath = "workflow:$",
            LeasePhase = nameof(DurableLeaseObligationPhase.Held),
            HolderKey = "forged-holder",
            ProtectionToken = "lease-forged-ancestor",
            LeaseRequirements = [new ResourcePoolRequirement("database", 1)],
            RegistrationSequence = 1
        };
        var serializer = new JsonWorkflowPayloadSerializer();
        var envelope = DurableFiberEnvelopeMapper.ToEnvelope(
            startedScope.State,
            definition.CompiledPlan,
            serializer.Serialize(new State()),
            [forgedAncestor]);
        var executor = new DurableFiberDriverExecutor<State>(definition);

        var result = await executor.RunSegmentAsync(
            new DurableDriverContext(
                instanceId,
                aggregate,
                envelope,
                processor,
                serializer,
                TimeProvider.System,
                DurableDriverBudget.Default),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableSegmentOutcome.Terminal);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken)).Single();
        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("SFE-RUN-002");
        var pool = (await pools.GetPoolAsync(
            "database",
            TestContext.Current.CancellationToken)).Value;
        pool.AvailableCapacity.Should().Be(1);
        pool.HeldTickets.Should().BeEmpty();
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        InMemoryResourcePoolStore pools,
        WorkflowDefinition<State> definition,
        DurableDriverBudget budget)
    {
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store, pools),
            new DurableDefinitionRegistry(new LeaseStepServices(pools, new LeaseObservation())),
            TimeProvider.System,
            budget);
        runtime.RegisterDefinition(definition);
        return runtime;
    }

    private sealed class State;

    private sealed class LeaseObservation
    {
        internal string? ProtectionToken { get; set; }

        internal int AvailableCapacitySeenByParent { get; set; }
    }

    private sealed class SiblingProgress
    {
        internal int Executions;
    }

    private sealed class ObserveLeaseStep(LeaseObservation observation) : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            observation.ProtectionToken = context.ResourceLease?.ProtectionToken.Value;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ObserveReleasedCapacityStep(
        IResourcePoolStore pools,
        LeaseObservation observation) : IStep<State>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            observation.AvailableCapacitySeenByParent =
                (await pools.GetPoolAsync("database", cancellationToken)).Value.AvailableCapacity;
            return new StepResult.Completed();
        }
    }

    private sealed class NoOpStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class RecordSiblingStep(SiblingProgress progress) : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref progress.Executions);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class LeasedRetryGate
    {
        internal int Attempts;
        internal TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class LeasedRetryStep(LeasedRetryGate gate) : IStep<State>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref gate.Attempts);
            if (attempt == 1)
            {
                gate.FirstStarted.TrySetResult();
                await gate.ReleaseFirst.Task;
            }
            else
            {
                gate.SecondStarted.TrySetResult();
            }

            return new StepResult.Completed();
        }
    }

    private sealed class LeaseStepServices(
        IResourcePoolStore pools,
        LeaseObservation observation,
        LeasedRetryGate? retryGate = null,
        SiblingProgress? siblingProgress = null) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ObserveLeaseStep))
            {
                return new ObserveLeaseStep(observation);
            }

            if (serviceType == typeof(ObserveReleasedCapacityStep))
            {
                return new ObserveReleasedCapacityStep(pools, observation);
            }

            if (serviceType == typeof(NoOpStep))
            {
                return new NoOpStep();
            }

            if (serviceType == typeof(LeasedRetryStep) && retryGate is not null)
            {
                return new LeasedRetryStep(retryGate);
            }

            if (serviceType == typeof(RecordSiblingStep) && siblingProgress is not null)
            {
                return new RecordSiblingStep(siblingProgress);
            }

            return null;
        }
    }

    private sealed class TicketHidingResourcePoolStore(IResourcePoolStore inner) : IResourcePoolStore
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

        public async Task<OrcaCore.Abstractions.Primitives.Option<ResourcePoolSnapshot>> GetPoolAsync(
            string poolName,
            CancellationToken cancellationToken)
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
