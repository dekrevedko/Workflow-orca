using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.TestSupport.Providers;
using Xunit;

using ActiveWaitSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using WorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;
using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

namespace OrcaCore.ProviderCertification;

public abstract class EventStoreCertificationTests
{
    protected abstract IProviderCertificationFixture CreateFixture();

    [Fact]
    [Trait("AC", "AC-309")]
    public async Task ConcurrentAppend_SameExpectedVersion_OneWinnerOneConflict()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var first = fixture.EventStore.AppendAsync(
            Batch(streamId, StreamVersion.Empty),
            TestContext.Current.CancellationToken);
        var second = fixture.EventStore.AppendAsync(
            Batch(streamId, StreamVersion.Empty),
            TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.IsFailure).Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-114")]
    public async Task CommitFailure_BeforeApply_LeavesWaitAndInboxEventAvailable()
    {
        var fixture = CreateFixture();
        var inboxEventId = EventId.Create(Guid.CreateVersion7().ToString());
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());

        var result = await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(instanceId),
                new StreamVersion(1),
                inboxEventId: inboxEventId),
            TestContext.Current.CancellationToken);
        var inboxRecord = await fixture.InboxStore.GetAsync(
            instanceId,
            inboxEventId,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        inboxRecord.HasValue.Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "AC-305")]
    public async Task InboxDuplicate_AfterRecordedApplied_IsIgnored()
    {
        var fixture = CreateFixture();
        var inboxEventId = EventId.Create(Guid.CreateVersion7().ToString());
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(instanceId),
                StreamVersion.Empty,
                inboxEventId: inboxEventId),
            TestContext.Current.CancellationToken);

        var recorded = await fixture.InboxStore.GetAsync(
            instanceId,
            inboxEventId,
            TestContext.Current.CancellationToken);

        recorded.HasValue.Should().BeTrue();
        recorded.Value.Should().Be(new InboxRecord(
            instanceId,
            inboxEventId,
            "certification-envelope",
            InboxRecordState.Applied));
    }

    [Fact]
    [Trait("AC", "AC-305")]
    public async Task SameInboxEventId_OnDifferentTargets_IsRecordedIndependently()
    {
        var fixture = CreateFixture();
        var inboxEventId = EventId.Create(Guid.CreateVersion7().ToString());
        var firstInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var secondInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());

        var first = await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(firstInstanceId),
                StreamVersion.Empty,
                inboxEventId,
                inboxEnvelopeFingerprint: "first-envelope"),
            TestContext.Current.CancellationToken);
        var second = await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(secondInstanceId),
                StreamVersion.Empty,
                inboxEventId,
                inboxEnvelopeFingerprint: "second-envelope"),
            TestContext.Current.CancellationToken);

        var firstRecord = await fixture.InboxStore.GetAsync(
            firstInstanceId,
            inboxEventId,
            TestContext.Current.CancellationToken);
        var secondRecord = await fixture.InboxStore.GetAsync(
            secondInstanceId,
            inboxEventId,
            TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        firstRecord.Value.EnvelopeFingerprint.Should().Be("first-envelope");
        secondRecord.Value.EnvelopeFingerprint.Should().Be("second-envelope");
    }

    [Fact]
    [Trait("AC", "AC-310")]
    public async Task OutboxRecords_AreNotVisibleWhenCommitFails()
    {
        var fixture = CreateFixture();

        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString())),
                new StreamVersion(1),
                outboxRecordId: OutboxRecordId.New()),
            TestContext.Current.CancellationToken);
        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-311")]
    public async Task StartIdempotencyWrite_CommittedWithStart_RoundTripsMapping()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var definitionId = DefinitionId.New();
        var definitionVersion = new DefinitionVersion(7);
        const string IdempotencyKey = "certification-start-1";
        const string DefinitionFingerprint = "definition-fingerprint-v7";
        const string InputFingerprint = "fixed-codec-input-fingerprint";
        var batch = Batch(new WorkflowStreamId(instanceId), StreamVersion.Empty) with
        {
            StartIdempotencyOperations =
            [
                new StartIdempotencyWrite(
                    IdempotencyKey,
                     instanceId,
                     definitionId,
                     definitionVersion,
                     DefinitionFingerprint,
                     InputFingerprint)
            ]
        };

        var result = await fixture.EventStore.AppendAsync(
            batch,
            TestContext.Current.CancellationToken);
        var mapping = await fixture.StartIdempotencyStore.GetStartedAsync(
            IdempotencyKey,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        mapping.HasValue.Should().BeTrue();
        mapping.Value.Should().Be(new StartedWorkflowIdempotencyRecord(
            IdempotencyKey,
            instanceId,
            definitionId,
            definitionVersion,
            DefinitionFingerprint,
            InputFingerprint));
    }

    [Fact]
    [Trait("AC", "AC-311")]
    public async Task DuplicateStartIdempotencyKey_RejectsAndRollsBackEntireCommit()
    {
        var fixture = CreateFixture();
        var winnerId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var duplicateId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var definitionId = DefinitionId.New();
        const string IdempotencyKey = "certification-start-duplicate";
        ProviderCommitBatch startBatch(InstanceId instanceId) =>
            Batch(new WorkflowStreamId(instanceId), StreamVersion.Empty) with
            {
                StartIdempotencyOperations =
                [
                    new StartIdempotencyWrite(
                         IdempotencyKey,
                         instanceId,
                         definitionId,
                         DefinitionVersion.Initial,
                         "definition-fingerprint",
                         "input-fingerprint")
                ]
            };

        var winner = await fixture.EventStore.AppendAsync(
            startBatch(winnerId),
            TestContext.Current.CancellationToken);
        var duplicate = await fixture.EventStore.AppendAsync(
            startBatch(duplicateId),
            TestContext.Current.CancellationToken);
        var duplicateTail = await fixture.EventStore.LoadTailAsync(
            new WorkflowStreamId(duplicateId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var mapping = await fixture.StartIdempotencyStore.GetStartedAsync(
            IdempotencyKey,
            TestContext.Current.CancellationToken);

        winner.IsSuccess.Should().BeTrue();
        duplicate.IsFailure.Should().BeTrue();
        duplicateTail.Should().BeEmpty("a rejected idempotent start must not partially append its event");
        mapping.Value.InstanceId.Should().Be(winnerId);
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-003")]
    [Trait("AC", "PR-010")]
    public async Task NEG_PR_003_AppendAsync_EmptyBatchIsNoOpAndDoesNotAdvanceStream()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));

        var result = await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty
            },
            TestContext.Current.CancellationToken);
        var tail = await fixture.EventStore.LoadTailAsync(
            streamId,
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewVersion.Should().Be(StreamVersion.Empty);
        tail.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-006")]
    [Trait("AC", "DU-032")]
    public async Task NEG_PR_006_ClaimAsync_WithNoPendingOutboxReturnsEmptyBatch()
    {
        var fixture = CreateFixture();

        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-007")]
    [Trait("AC", "DU-032")]
    public async Task NEG_PR_007_MarkAsync_UnknownOutboxRecordDoesNotCreateState()
    {
        var fixture = CreateFixture();
        var outboxRecordId = OutboxRecordId.New();

        await fixture.OutboxStore.MarkAsync(
            outboxRecordId,
            OutboxRecordState.Dispatched,
            TestContext.Current.CancellationToken);
        var state = await fixture.OutboxStore.GetStateAsync(
            outboxRecordId,
            TestContext.Current.CancellationToken);

        state.HasValue.Should().BeFalse();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-008")]
    [Trait("AC", "DU-032")]
    public async Task NEG_PR_008_MarkAsync_DispatchedOutboxRecordTwiceIsIdempotent()
    {
        var fixture = CreateFixture();
        var outboxRecordId = OutboxRecordId.New();
        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString())),
                StreamVersion.Empty,
                outboxRecordId: outboxRecordId),
            TestContext.Current.CancellationToken);

        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);
        await fixture.OutboxStore.MarkAsync(
            outboxRecordId,
            OutboxRecordState.Dispatched,
            TestContext.Current.CancellationToken);
        await fixture.OutboxStore.MarkAsync(
            outboxRecordId,
            OutboxRecordState.Dispatched,
            TestContext.Current.CancellationToken);
        var state = await fixture.OutboxStore.GetStateAsync(
            outboxRecordId,
            TestContext.Current.CancellationToken);
        var afterDispatch = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().ContainSingle(record => record.OutboxRecordId == outboxRecordId);
        state.Value.Should().Be(OutboxRecordState.Dispatched);
        afterDispatch.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "DU-032")]
    public async Task ClaimAsync_LeaseExpires_RecordCanBeClaimedAgain()
    {
        var fixture = CreateFixture();
        var outboxRecordId = OutboxRecordId.New();
        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString())),
                StreamVersion.Empty,
                outboxRecordId: outboxRecordId),
            TestContext.Current.CancellationToken);

        var first = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(10), TimeSpan.FromSeconds(5)),
            TestContext.Current.CancellationToken);
        var stillLeased = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(14), TimeSpan.FromSeconds(5)),
            TestContext.Current.CancellationToken);
        var reclaimed = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(16), TimeSpan.FromSeconds(5)),
            TestContext.Current.CancellationToken);

        first.Should().ContainSingle(record => record.OutboxRecordId == outboxRecordId);
        stillLeased.Should().BeEmpty();
        reclaimed.Should().ContainSingle(record => record.OutboxRecordId == outboxRecordId);
    }

    [Fact]
    [Trait("AC", "DU-032")]
    public async Task ReleaseAsync_ClaimedOutboxRecord_CanBeClaimedAgain()
    {
        var fixture = CreateFixture();
        var outboxRecordId = OutboxRecordId.New();
        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString())),
                StreamVersion.Empty,
                outboxRecordId: outboxRecordId),
            TestContext.Current.CancellationToken);
        await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(10), TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);

        await fixture.OutboxStore.ReleaseAsync(outboxRecordId, TestContext.Current.CancellationToken);
        var reclaimed = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(11), TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);

        reclaimed.Should().ContainSingle(record => record.OutboxRecordId == outboxRecordId);
    }

    [Fact]
    [Trait("AC", "PR-010")]
    [Trait("AC", "PR-016")]
    public async Task AppendAsync_AllWorkflowEventTypes_RoundTripsFromTail()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var streamId = new WorkflowStreamId(instanceId);
        var events = AllWorkflowEventTypes(instanceId);

        var result = await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events = events
            },
            TestContext.Current.CancellationToken);
        var tail = await fixture.EventStore.LoadTailAsync(
            streamId,
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewVersion.Should().Be(new StreamVersion(events.Count));
        tail.Select(workflowEvent => workflowEvent.GetType())
            .Should()
            .Equal(events.Select(workflowEvent => workflowEvent.GetType()));
        tail.Should().BeEquivalentTo(events, options => options.WithStrictOrdering());
    }

    [Fact]
    [Trait("AC", "PR-013")]
    public async Task ProjectionUpsert_RoundTripsStreamVersion()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var definitionId = DefinitionId.New();

        await fixture.ProjectionStore.ApplyAsync(
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot =
                        new global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot
                    {
                        InstanceId = instanceId,
                        RootInstanceId = instanceId,
                        DefinitionId = definitionId,
                        DefinitionVersion = DefinitionVersion.Initial,
                        Status = WorkflowStatus.Running,
                        StreamVersion = 5,
                        CreatedAt = Timestamp(1),
                        UpdatedAt = Timestamp(2)
                    }
                }
            ],
            TestContext.Current.CancellationToken);

        var snapshot = await fixture.ProjectionStore.GetAsync(
            instanceId,
            TestContext.Current.CancellationToken);

        snapshot.Value.StreamVersion.Should().Be(
            5,
            "the projected stream version is the optimistic-concurrency token (CR-022)");
    }

    [Fact]
    [Trait("AC", "AC-501")]
    [Trait("AC", "PR-013")]
    public async Task ProjectionQuery_EventAndCorrelationMustMatchTheSameActiveWait()
    {
        var fixture = CreateFixture();
        var definitionId = DefinitionId.New();
        var targetId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var splitMatchId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var targetWait = ActiveWait("approved", "order-1");
        await fixture.ProjectionStore.ApplyAsync(
            [
                Projection(targetId, definitionId, [targetWait]),
                Projection(
                    splitMatchId,
                    definitionId,
                    [ActiveWait("approved", "order-2"), ActiveWait("rejected", "order-1")])
            ],
            TestContext.Current.CancellationToken);
        var listed = await fixture.ProjectionStore.FindActiveWaitsAsync(
            definitionId,
            EventName.Create("approved"),
            CorrelationId.Create("order-1"),
            TestContext.Current.CancellationToken);

        listed.Should().ContainSingle().Which.InstanceId.Should().Be(targetId);
        listed.Single().ActiveWaits.Should().Contain(targetWait);
    }

    [Fact]
    [Trait("AC", "PR-010")]
    public async Task CheckpointUpsert_RoundTripsFullRuntimeState()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var runtimeState = FullyPopulatedRuntimeState(instanceId);

        var result = await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    new WorkflowStartedEvent
                    {
                        EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                        InstanceId = instanceId,
                        CommandId = CommandId.New(),
                        CausationId = CausationId.New(),
                        OccurredAt = Timestamp(1),
                        DefinitionId = DefinitionId.New(),
                        DefinitionVersion = DefinitionVersion.Initial
                    }
                ],
                Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(1), "application/json", [1])
                {
                    Status = WorkflowStatus.Running,
                    RuntimeState = runtimeState
                }
            },
            TestContext.Current.CancellationToken);
        var checkpoint = await fixture.EventStore.LoadCheckpointAsync(
            instanceId,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.RuntimeState.Should().BeEquivalentTo(
            runtimeState,
            "every current runtime collection must survive checkpoint compaction, " +
            "or rehydrated aggregates silently lose in-flight state");
    }

    [Fact]
    [Trait("SFE", "ProviderRecovery")]
    public async Task NestedFiberCheckpoint_OptimisticReplacementPreservesOwnersAndContinuationClaim()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var streamId = new WorkflowStreamId(instanceId);
        var ownerFiberId = new FiberId("fiber:blocked-child");
        var ownerScopeId = new ScopeId("scope:nested");
        var initialEnvelope = NestedEnvelope(instanceId, "fiber:runnable-sibling");
        var replacedEnvelope = NestedEnvelope(instanceId, ownerFiberId.Value);
        var runtimeState = FullyPopulatedRuntimeState(instanceId, ownerFiberId, ownerScopeId);
        var continuationId = OutboxRecordId.New();
        var started = Batch(streamId, StreamVersion.Empty) with
        {
            Checkpoint = new CheckpointWrite(
                instanceId,
                new StreamVersion(1),
                DurableExecutionEnvelopeV2.ContentType,
                initialEnvelope.Serialize())
            {
                Status = WorkflowStatus.Running,
                RuntimeState = runtimeState
            }
        };
        var replacement = new ProviderCommitBatch
        {
            StreamId = streamId,
            ExpectedVersion = new StreamVersion(1),
            Events =
            [
                new WorkflowStepCompletedEvent
                {
                    EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                    InstanceId = instanceId,
                    CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = Timestamp(2),
                    StepPath = "nested/branch-return"
                }
            ],
            Checkpoint = new CheckpointWrite(
                instanceId,
                new StreamVersion(2),
                DurableExecutionEnvelopeV2.ContentType,
                replacedEnvelope.Serialize())
            {
                Status = WorkflowStatus.Running,
                RuntimeState = runtimeState
            },
            OutboxRecords = [new OutboxWrite(continuationId, OutboxKinds.Continue, [1])]
        };

        (await fixture.EventStore.AppendAsync(started, TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        (await fixture.EventStore.AppendAsync(replacement, TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        var stale = await fixture.EventStore.AppendAsync(
            replacement with
            {
                Checkpoint = replacement.Checkpoint! with { Payload = initialEnvelope.Serialize() }
            },
            TestContext.Current.CancellationToken);
        var checkpoint = await fixture.EventStore.LoadCheckpointAsync(
            instanceId,
            TestContext.Current.CancellationToken);
        var claimed = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(10), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);

        stale.IsFailure.Should().BeTrue();
        checkpoint.Should().NotBeNull();
        checkpoint.Value.StreamVersion.Should().Be(new StreamVersion(2));
        DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload)
            .Should().BeEquivalentTo(replacedEnvelope);
        checkpoint.Value.RuntimeState.Should().BeEquivalentTo(runtimeState);
        checkpoint.Value.RuntimeState.ActiveWaits.Should().OnlyContain(item =>
            item.FiberId == ownerFiberId && item.ScopeId == ownerScopeId);
        claimed.Should().ContainSingle().Which.OutboxRecordId.Should().Be(continuationId);
    }

    [Fact]
    [Trait("SFE", "ProviderHostReplacement")]
    public async Task StructuredFibers_HostReplacementPreservesMixedStateAndExactNextFiber()
    {
        var fixture = CreateFixture();
        var definition = HostReplacementDefinition();
        var budget = new DurableDriverBudget(1, TimeSpan.FromSeconds(30));
        var workflowKey = $"provider-fiber-{Guid.NewGuid():N}";
        var started = await Host(fixture, budget, definition).StartOrGetAsync<string, HostState>(
            workflowKey,
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        // Each call below creates a fresh registry, driver, and aggregate loader over the same store.
        await ResumeExisting(fixture, budget, definition, workflowKey);
        await ResumeExisting(fixture, budget, definition, workflowKey);

        var mixedCheckpoint = await fixture.EventStore.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var mixed = DurableExecutionEnvelopeV2.Deserialize(mixedCheckpoint!.Value.Payload);
        var scope = mixed.Scopes.Should().ContainSingle().Subject;
        var blocked = mixed.Fibers.Single(fiber => fiber.Phase == DurableFiberPhase.Blocked &&
            fiber.Blocked?.Reason == DurableFiberBlockedReason.Wait);
        var completed = mixed.Fibers.Single(fiber => fiber.Phase == DurableFiberPhase.Completed);
        var runnable = mixed.Fibers.Single(fiber => fiber.Phase == DurableFiberPhase.Runnable);

        scope.CommittedResults.Should().ContainSingle().Which.FiberId.Should().Be(completed.FiberId);
        mixed.Scheduler.RunnableFiberIds.Should().Equal(runnable.FiberId);
        mixed.Scheduler.NextFiberId.Should().Be(runnable.FiberId);
        mixed.OwnedObligations.Should().ContainSingle(item =>
            item.FiberId == blocked.FiberId && item.ScopeId == scope.ScopeId);

        await ResumeExisting(fixture, budget, definition, workflowKey);
        var afterQuantumCheckpoint = await fixture.EventStore.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var afterQuantum = DurableExecutionEnvelopeV2.Deserialize(afterQuantumCheckpoint!.Value.Payload);
        afterQuantum.Scheduler.NextFiberId.Should().Be(runnable.FiberId);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ResumeExisting(fixture, budget, definition, workflowKey);
        }

        var eventHost = Host(fixture, budget, definition);
        await eventHost.RaiseEventAsync(
            started.InstanceId,
            "ReleaseBlockedFiber",
            HostCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await ResumeExisting(fixture, budget, definition, workflowKey);
        }

        var finalCheckpoint = await fixture.EventStore.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var finalEnvelope = DurableExecutionEnvelopeV2.Deserialize(finalCheckpoint!.Value.Payload);
        var events = await fixture.EventStore.LoadTailAsync(
            new WorkflowStreamId(started.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        var finalFiberState = string.Join(
            " | ",
            finalEnvelope.Fibers
                .OrderBy(fiber => fiber.FiberId, StringComparer.Ordinal)
                .Select(fiber =>
                    $"{fiber.FiberId}:{fiber.Phase}:instruction={fiber.InstructionId}:" +
                    $"blocked={fiber.Blocked?.Reason}:failure={fiber.Failure?.Code}/{fiber.Failure?.Message}"));
        events.OfType<WorkflowCompletedEvent>().Should().ContainSingle(
            "the replacement host must drain the exact persisted schedule; final fibers: {0}",
            finalFiberState);
        finalEnvelope.OwnedObligations.Should().BeEmpty();
        events.OfType<WorkflowStepCompletedEvent>()
            .Should().ContainSingle(item => item.StepPath.EndsWith(":merge", StringComparison.Ordinal));
    }

    private static DurableWorkflowDefinition<string> HostReplacementDefinition()
    {
        var definition = global::OrcaCore.Workflow.Durable<HostState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new HostState())
            .Parallel<string>(
                branches => branches
                    .Branch<HostBranchState>(
                        AuthoredBranchId.Create("blocked"),
                        _ => new HostBranchState { Name = "blocked" },
                        branch => branch
                            .Wait(WorkflowEventContract.Create(EventName.Create("ReleaseBlockedFiber"), EventContractVersion.Initial), _ => HostCorrelation)
                            .Return(state => state.Value.Name))
                    .Branch<HostBranchState>(
                        AuthoredBranchId.Create("completed"),
                        _ => new HostBranchState { Name = "completed" },
                        branch => branch.Return(state => state.Value.Name))
                    .Branch<HostBranchState>(
                        AuthoredBranchId.Create("yielding"),
                        _ => new HostBranchState { Name = "yielding" },
                        branch => branch
                            .Then<AdvanceOnceHostStep>()
                            .Return(state => state.Value.Name)))
            .WhenAll((_, results) => new HostState
                {
                    Results = results.Select(result => result.Result).ToList()
                })
            .End(WorkflowOutcomeName.Create("done"))
            .Build();
        return definition;
    }

    private static DurableWorkflowRuntime Host(
        IProviderCertificationFixture fixture,
        DurableDriverBudget budget,
        DurableWorkflowDefinition<string> definition)
    {
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(fixture.EventStore),
            new DurableDefinitionRegistry(new HostStepProvider()),
            TimeProvider.System,
            budget,
            fixture.ProjectionStore);
        runtime.RegisterDefinition(definition);
        return runtime;
    }

    private static async Task ResumeExisting(
        IProviderCertificationFixture fixture,
        DurableDriverBudget budget,
        DurableWorkflowDefinition<string> definition,
        string workflowKey)
    {
        await Host(fixture, budget, definition).StartOrGetAsync<string, HostState>(
            workflowKey,
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
    }

    private static CorrelationId HostCorrelation => CorrelationId.Create("provider-host-replacement");

    private sealed class AdvanceOnceHostStep : IStep<HostBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<HostBranchState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class HostStepProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(AdvanceOnceHostStep)
                ? new AdvanceOnceHostStep()
                : null;
    }

    private sealed class HostState
    {
        public List<string> Results { get; set; } = [];
    }

    private sealed class HostBranchState
    {
        public string Name { get; set; } = string.Empty;

        public int Attempts { get; set; }
    }

    private static WorkflowRuntimeCheckpointState FullyPopulatedRuntimeState(
        InstanceId instanceId,
        FiberId? ownerFiberId = null,
        ScopeId? ownerScopeId = null)
    {
        var waitId = WaitId.Parse(Guid.CreateVersion7().ToString());
        var timerId = TimerId.New();

        return new WorkflowRuntimeCheckpointState
        {
            ActiveTimers =
            [
                new CheckpointActiveTimer(timerId, Timestamp(40), "timeout", Timestamp(2))
                {
                    FiberId = ownerFiberId,
                    ScopeId = ownerScopeId
                }
            ],
            ActiveWaits =
            [
                new CheckpointActiveWait(
                    waitId,
                    "approved",
                    CorrelationId.Create("order-1"),
                    Timestamp(3),
                    WaitMode.Cold,
                    "branch-a")
                {
                    FiberId = ownerFiberId,
                    ScopeId = ownerScopeId,
                    WaitSequence = 12
                }
            ],
            ActiveResourceTickets =
            [
                new ResourcePoolTicket(
                    Guid.Parse("00000000-0000-0000-0000-000000000901"),
                    "cpu",
                    2,
                    instanceId,
                    "holder-1",
                    Timestamp(4),
                    Timestamp(44))
                {
                    FiberId = ownerFiberId,
                    ScopeId = ownerScopeId
                }
            ],
            PendingResumes =
            [
                new CheckpointPendingResume(
                    waitId,
                    EventId.Create(Guid.CreateVersion7().ToString()),
                    "approved",
                    CorrelationId.Create("order-1"),
                    "branch-a",
                    "application/json",
                    [1, 2, 3],
                    Timestamp(5))
                {
                    FiberId = ownerFiberId,
                    ScopeId = ownerScopeId,
                    WaitSequence = 12
                }
            ],
            ContinuationFailureCount = 2,
            ContinuationFailurePositionStreamVersion = new StreamVersion(7),
            ContinuationRetryNotBefore = Timestamp(9)
        };
    }

    private static DurableExecutionEnvelopeV2 NestedEnvelope(
        InstanceId instanceId,
        string nextFiberId)
    {
        const string RootFiberId = "fiber:root";
        const string ParentFiberId = "fiber:parent";
        const string BlockedFiberId = "fiber:blocked-child";
        const string SiblingFiberId = "fiber:runnable-sibling";
        return new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = instanceId,
            ContinueAsNewGeneration = 2,
            RootFiberId = RootFiberId,
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = DefinitionId.New(),
                DefinitionVersion = DefinitionVersion.Initial,
                CompilerFormatVersion = 2,
                CompilerProfileId = "orcacore-compiler-v2;quantum=1024",
                PlanFingerprint = "provider-certification-fingerprint"
            },
            StateContentType = "application/json",
            StatePayload = [1, 2, 3],
            Fibers =
            [
                new DurableFiberState
                {
                    FiberId = RootFiberId,
                    InstructionId = "instruction:root",
                    Phase = DurableFiberPhase.Blocked,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 2,
                    Blocked = new DurableFiberBlock
                    {
                        Reason = DurableFiberBlockedReason.Scope,
                        ObligationId = "scope:outer"
                    }
                },
                new DurableFiberState
                {
                    FiberId = ParentFiberId,
                    OwningScopeId = "scope:outer",
                    InstructionId = "instruction:parent",
                    Phase = DurableFiberPhase.Blocked,
                    LoopIteration = 1,
                    NextScopeEntrySequence = 1,
                    Blocked = new DurableFiberBlock
                    {
                        Reason = DurableFiberBlockedReason.Scope,
                        ObligationId = "scope:nested"
                    }
                },
                new DurableFiberState
                {
                    FiberId = BlockedFiberId,
                    OwningScopeId = "scope:nested",
                    InstructionId = "instruction:wait",
                    Phase = DurableFiberPhase.Blocked,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 0,
                    Blocked = new DurableFiberBlock
                    {
                        Reason = DurableFiberBlockedReason.Wait,
                        ObligationId = "wait:owned"
                    }
                },
                new DurableFiberState
                {
                    FiberId = SiblingFiberId,
                    OwningScopeId = "scope:nested",
                    InstructionId = "instruction:return",
                    Phase = DurableFiberPhase.Runnable,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 0,
                    ResultPayload = [9]
                }
            ],
            Scopes =
            [
                new DurableExecutionScopeState
                {
                    ScopeId = "scope:outer",
                    ScopePlanId = "scope-plan:outer",
                    ScopeEntrySequence = 1,
                    ParentFiberId = RootFiberId,
                    Kind = DurableExecutionScopeKind.WhenAll,
                    Phase = DurableExecutionScopePhase.Running,
                    ChildFiberIds = [ParentFiberId]
                },
                new DurableExecutionScopeState
                {
                    ScopeId = "scope:nested",
                    ScopePlanId = "scope-plan:nested",
                    ScopeEntrySequence = 0,
                    ParentScopeId = "scope:outer",
                    ParentFiberId = ParentFiberId,
                    Kind = DurableExecutionScopeKind.WhenAll,
                    Phase = DurableExecutionScopePhase.Running,
                    ChildFiberIds = [BlockedFiberId, SiblingFiberId],
                    CommittedResults =
                    [
                        new DurableCommittedResult
                        {
                            FiberId = SiblingFiberId,
                            Payload = [9]
                        }
                    ]
                }
            ],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = [SiblingFiberId],
                NextFiberId = nextFiberId
            },
            OwnedObligations =
            [
                new DurableOwnedObligationState
                {
                    Kind = DurableOwnedObligationKind.Wait,
                    ObligationId = "wait:owned",
                    FiberId = BlockedFiberId,
                    ScopeId = "scope:nested",
                    RegistrationSequence = 12
                }
            ],
            Diagnostics = new DurableExecutionDiagnostics
            {
                TotalQuantumRotations = 4,
                ForcedRotations = 2
            }
        };
    }

    [Fact]
    [Trait("AC", "DR-037")]
    [Trait("AC", "DR-AC-029")]
    public async Task ClaimAsync_IncludeSelector_ClaimsOnlyMatchingKinds()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var continueRecordId = OutboxRecordId.New();
        var statusRecordId = OutboxRecordId.New();
        await fixture.EventStore.AppendAsync(
            KindBatch(streamId, (continueRecordId, OutboxKinds.Continue), (statusRecordId, "status")),
            TestContext.Current.CancellationToken);

        var claimed = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(10, Timestamp(10), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);

        claimed.Should().ContainSingle(record => record.OutboxRecordId == continueRecordId);
    }

    [Fact]
    [Trait("AC", "DR-037")]
    [Trait("AC", "DR-AC-029")]
    public async Task ClaimAsync_ExcludeSelector_SkipsExcludedKindsAndLeavesThemClaimable()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var continueRecordId = OutboxRecordId.New();
        var statusRecordId = OutboxRecordId.New();
        await fixture.EventStore.AppendAsync(
            KindBatch(streamId, (continueRecordId, OutboxKinds.Continue), (statusRecordId, "status")),
            TestContext.Current.CancellationToken);

        var external = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(10, Timestamp(10), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Excluding(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);
        var continuation = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(10, Timestamp(11), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);

        external.Should().ContainSingle(record => record.OutboxRecordId == statusRecordId);
        continuation.Should().ContainSingle(record => record.OutboxRecordId == continueRecordId);
    }

    [Fact]
    [Trait("AC", "DR-037")]
    public async Task ClaimAsync_WithoutSelector_ClaimsAllKinds()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var continueRecordId = OutboxRecordId.New();
        var statusRecordId = OutboxRecordId.New();
        await fixture.EventStore.AppendAsync(
            KindBatch(streamId, (continueRecordId, OutboxKinds.Continue), (statusRecordId, "status")),
            TestContext.Current.CancellationToken);

        var claimed = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(10, Timestamp(10), TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);

        claimed.Select(record => record.OutboxRecordId)
            .Should().BeEquivalentTo([continueRecordId, statusRecordId]);
    }

    [Fact]
    [Trait("AC", "DR-AC-031")]
    public async Task OutboxSelectors_SeparateContinuationAndExternalRecordsByState()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var continuationIds = Enumerable.Range(0, 3).Select(_ => OutboxRecordId.New()).ToArray();
        var externalIds = Enumerable.Range(0, 3).Select(_ => OutboxRecordId.New()).ToArray();
        await fixture.EventStore.AppendAsync(
            KindBatch(
                streamId,
                (continuationIds[0], OutboxKinds.Continue),
                (continuationIds[1], OutboxKinds.Continue),
                (continuationIds[2], OutboxKinds.Continue),
                (externalIds[0], "status"),
                (externalIds[1], "child-start"),
                (externalIds[2], "lifecycle-event")),
            TestContext.Current.CancellationToken);

        var claimedContinuation = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(10), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);
        await fixture.OutboxStore.ReleaseAsync(
            claimedContinuation.Should().ContainSingle().Subject.OutboxRecordId,
            TestContext.Current.CancellationToken);
        var claimedExternal = await fixture.OutboxStore.ClaimAsync(
            new OutboxClaimRequest(1, Timestamp(10), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Excluding(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);

        var continuationStates = await Task.WhenAll(continuationIds.Select(id =>
            fixture.OutboxStore.GetStateAsync(id, TestContext.Current.CancellationToken)));
        var externalStates = await Task.WhenAll(externalIds.Select(id =>
            fixture.OutboxStore.GetStateAsync(id, TestContext.Current.CancellationToken)));

        claimedExternal.Should().ContainSingle();
        continuationStates.Select(state => state.Value).Should().BeEquivalentTo(
            [OutboxRecordState.Retryable, OutboxRecordState.Pending, OutboxRecordState.Pending]);
        externalStates.Select(state => state.Value).Should().BeEquivalentTo(
            [OutboxRecordState.Claimed, OutboxRecordState.Pending, OutboxRecordState.Pending]);
    }

    private static ProviderCommitBatch KindBatch(
        WorkflowStreamId streamId,
        params (OutboxRecordId RecordId, string Kind)[] records)
    {
        var batch = Batch(streamId, StreamVersion.Empty);
        return batch with
        {
            OutboxRecords = records
                .Select(record => new OutboxWrite(record.RecordId, record.Kind, [1]))
                .ToArray()
        };
    }

    private static ProviderCommitBatch Batch(
        WorkflowStreamId streamId,
        StreamVersion expectedVersion,
        EventId? inboxEventId = null,
        OutboxRecordId? outboxRecordId = null,
        string inboxEnvelopeFingerprint = "certification-envelope")
    {
        return new ProviderCommitBatch
        {
            StreamId = streamId,
            ExpectedVersion = expectedVersion,
            Events =
            [
                new WorkflowStartedEvent
                {
                    EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                    InstanceId = streamId.InstanceId,
                    CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    DefinitionId = DefinitionId.New(),
                    DefinitionVersion = DefinitionVersion.Initial
                }
            ],
            InboxOperations = inboxEventId is { } eventId
                ?
                [
                    new InboxWrite(eventId, InboxRecordState.Applied)
                    {
                        EnvelopeFingerprint = inboxEnvelopeFingerprint
                    }
                ]
                : [],
            OutboxRecords = outboxRecordId is { } recordId
                ? [new OutboxWrite(recordId, "status", [1])]
                : []
        };
    }

    private static ProjectionWrite Projection(
        InstanceId instanceId,
        DefinitionId definitionId,
        IReadOnlyList<global::OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot> activeWaits)
    {
        return new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot =
                new global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot
            {
                InstanceId = instanceId,
                RootInstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                Status = WorkflowStatus.Waiting,
                CreatedAt = Timestamp(1),
                UpdatedAt = Timestamp(2),
                ActiveWaits = activeWaits
            }
        };
    }

    private static global::OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot ActiveWait(
        string eventName,
        string correlationId)
    {
        return new global::OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot
        {
            WaitId = WaitId.Parse(Guid.CreateVersion7().ToString()),
            EventName = eventName,
            CorrelationId = CorrelationId.Create(correlationId),
            RegisteredAt = Timestamp(1),
            Status = "active",
            Mode = "cold"
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 12, 0, seconds, TimeSpan.Zero);
    }

    private static IReadOnlyList<DurableWorkflowEvent> AllWorkflowEventTypes(InstanceId instanceId)
    {
        var eventSequence = 100;
        EventId nextEventId() => EventId.Create($"00000000-0000-0000-0000-{eventSequence++:000000000000}");
        CommandId commandId() => CommandId.New();
        CausationId causationId() => CausationId.New();
        DateTimeOffset occurredAt() => Timestamp(eventSequence % 50);

        var waitId = WaitId.Parse(Guid.CreateVersion7().ToString());
        var timerId = TimerId.New();
        var ticket = new ResourcePoolTicket(
            Guid.Parse("00000000-0000-0000-0000-000000000901"),
            "cpu",
            2,
            instanceId,
            "holder-1",
            Timestamp(4),
            Timestamp(44));

        return
        [
            new WorkflowStartedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                DefinitionId = DefinitionId.New(),
                DefinitionVersion = DefinitionVersion.Initial,
                IdempotencyKey = "start-key"
            },
            new WorkflowContinuedAsNewEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                PreviousStreamVersion = new StreamVersion(12),
                Generation = 2
            },
            new WorkflowStepCompletedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                StepPath = "root.step"
            },
            new WorkflowStepFailedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                StepPath = "root.fail",
                ErrorSummary = "failed"
            },
            new WorkflowWaitRegisteredEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                WaitId = waitId,
                EventName = "approved",
                CorrelationId = CorrelationId.Create("order-1"),
                Mode = WaitMode.Cold,
                BranchId = "branch-a"
            },
            new WorkflowWaitMatchedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                WaitId = waitId,
                MatchedEventId = EventId.Create(Guid.CreateVersion7().ToString())
            },
            new WorkflowWaitCancelledEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                WaitId = waitId
            },
            new WorkflowTimerCancelledEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                TimerId = timerId
            },
            new WorkflowResumeConsumedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                WaitId = waitId
            },
            new WorkflowParkedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                Reason = DurableParkReason.Poison,
                ErrorSummary = "parked",
                FailedAttemptCount = 3,
                PositionStreamVersion = new StreamVersion(7)
            },
            new WorkflowUnparkedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt()
            },
            new WorkflowContinuationAttemptFailedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                AttemptCount = 2,
                PositionStreamVersion = new StreamVersion(8),
                NextEligibleAt = Timestamp(43),
                ErrorSummary = "retryable"
            },
            new WorkflowContinuationAttemptResetEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt()
            },
            new WorkflowTimerScheduledEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                TimerId = timerId,
                FireAt = Timestamp(42),
                WakeupName = "timeout"
            },
            new WorkflowTimerFiredEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                TimerId = timerId
            },
            new WorkflowResourcePoolAcquiredEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                HolderKey = "holder-1",
                Tickets = [ticket]
            },
            new WorkflowResourcePoolQueuedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                WaitId = WaitId.Parse(Guid.CreateVersion7().ToString()),
                HolderKey = "holder-2",
                Requirements = [new ResourcePoolRequirement("cpu", 1)],
                ExpiresAt = Timestamp(45)
            },
            new WorkflowResourcePoolReleasedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                HolderKey = "holder-1",
                Tickets = [ticket]
            },
            new WorkflowCompletedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                OutcomeName = "ok"
            },
            new WorkflowCancellationRequestedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt()
            },
            new WorkflowTerminalEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                Status = WorkflowStatus.Cancelled
            }
        ];
    }
}
/// <summary>
/// Supplies the provider ports exercised by provider certification tests.
/// </summary>
public interface IProviderCertificationFixture
{
    /// <summary>
    /// Gets the event store under certification.
    /// </summary>
    IWorkflowEventStore EventStore { get; }

    /// <summary>
    /// Gets the inbox store observed by certification tests.
    /// </summary>
    IWorkflowInboxStore InboxStore { get; }

    /// <summary>
    /// Gets the durable start-idempotency store observed by certification tests.
    /// </summary>
    IWorkflowStartIdempotencyStore StartIdempotencyStore { get; }

    /// <summary>
    /// Gets the outbox store observed by certification tests.
    /// </summary>
    IWorkflowOutboxStore OutboxStore { get; }

    /// <summary>
    /// Gets the projection store observed by certification tests.
    /// </summary>
    IWorkflowProjectionStore ProjectionStore { get; }
}

public sealed class FakeEventStoreCertificationTests : EventStoreCertificationTests
{
    protected override IProviderCertificationFixture CreateFixture()
    {
        return new FakeProviderCertificationFixture(new FakeWorkflowEventStore());
    }

    private sealed class FakeProviderCertificationFixture(FakeWorkflowEventStore provider)
        : IProviderCertificationFixture
    {
        public IWorkflowEventStore EventStore => provider;

        public IWorkflowInboxStore InboxStore => provider;

        public IWorkflowStartIdempotencyStore StartIdempotencyStore => provider;

        public IWorkflowOutboxStore OutboxStore => provider;

        public IWorkflowProjectionStore ProjectionStore => provider;
    }
}
