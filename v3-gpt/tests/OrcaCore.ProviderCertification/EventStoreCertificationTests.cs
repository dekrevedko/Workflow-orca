using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class EventStoreCertificationTests
{
    protected abstract IProviderCertificationFixture CreateFixture();

    [Fact]
    [Trait("AC", "AC-309")]
    public async Task ConcurrentAppend_SameExpectedVersion_OneWinnerOneConflict()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.New());
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
        var inboxEventId = EventId.New();

        var result = await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                new StreamVersion(1),
                inboxEventId: inboxEventId),
            TestContext.Current.CancellationToken);
        var inboxRecord = await fixture.InboxStore.GetAsync(
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
        var inboxEventId = EventId.New();
        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                StreamVersion.Empty,
                inboxEventId: inboxEventId),
            TestContext.Current.CancellationToken);

        var recorded = await fixture.InboxStore.GetAsync(inboxEventId, TestContext.Current.CancellationToken);

        recorded.HasValue.Should().BeTrue();
        recorded.Value.Should().Be(InboxRecordState.Applied);
    }

    [Fact]
    [Trait("AC", "AC-310")]
    public async Task OutboxRecords_AreNotVisibleWhenCommitFails()
    {
        var fixture = CreateFixture();

        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                new StreamVersion(1),
                outboxRecordId: OutboxRecordId.New()),
            TestContext.Current.CancellationToken);
        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-003")]
    [Trait("AC", "PR-010")]
    public async Task NEG_PR_003_AppendAsync_EmptyBatchIsNoOpAndDoesNotAdvanceStream()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.New());

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
                new WorkflowStreamId(InstanceId.New()),
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
                new WorkflowStreamId(InstanceId.New()),
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
                new WorkflowStreamId(InstanceId.New()),
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
        var instanceId = InstanceId.New();
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
        var instanceId = InstanceId.New();
        var definitionId = DefinitionId.New();

        await fixture.ProjectionStore.ApplyAsync(
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot = new WorkflowInstanceSnapshot
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

        var snapshots = await fixture.ProjectionStore.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle()
            .Which.StreamVersion.Should().Be(5, "the projected stream version is the optimistic-concurrency token (CR-022)");
    }

    [Fact]
    [Trait("AC", "PR-010")]
    public async Task CheckpointUpsert_RoundTripsFullRuntimeState()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceId.New();
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
                        EventId = EventId.New(),
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
            "every runtime collection — including saga records and resume-token facts — must survive " +
            "checkpoint compaction, or rehydrated aggregates silently lose in-flight state");
    }

    private static WorkflowRuntimeCheckpointState FullyPopulatedRuntimeState(InstanceId instanceId)
    {
        var waitId = WaitId.New();
        var timerId = TimerId.New();
        var childInstanceId = InstanceId.New();
        var childDefinitionId = DefinitionId.New();

        return new WorkflowRuntimeCheckpointState
        {
            ActiveTimers = [new CheckpointActiveTimer(timerId, Timestamp(40), "timeout", Timestamp(2))],
            ActiveWaits =
            [
                new CheckpointActiveWait(
                    waitId,
                    "approved",
                    new CorrelationId("order-1"),
                    Timestamp(3),
                    WaitMode.Cold,
                    "branch-a")
            ],
            BufferedDeliveries =
            [
                new CheckpointBufferedDelivery(EventId.New(), "approved", new CorrelationId("order-2"), "branch-b")
            ],
            BufferedTimers = [new CheckpointBufferedTimer(TimerId.New(), "paused-timeout", Timestamp(5))],
            ActiveChildren =
            [
                new CheckpointActiveChild(
                    "group-1",
                    childInstanceId,
                    WaitId.New(),
                    RunChildFailurePolicy.PropagateFailure,
                    RunChildrenJoinPolicy.WhenAll,
                    RunChildrenResidualPolicy.CancelRemaining,
                    """{"id":1}""")
            ],
            ActiveChildGroups =
            [
                new CheckpointActiveChildGroup(
                    "group-1",
                    RunChildFailurePolicy.PropagateFailure,
                    RunChildrenJoinPolicy.WhenAll,
                    RunChildrenResidualPolicy.CancelRemaining,
                    2,
                    1,
                    [
                        new WorkflowChildMaterialization
                        {
                            Index = 0,
                            ChildInstanceId = childInstanceId,
                            ChildDefinitionId = childDefinitionId,
                            ChildDefinitionVersion = DefinitionVersion.Initial,
                            ItemSnapshot = """{"id":1}"""
                        }
                    ])
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
            ],
            ActiveExternalJobs = [new CheckpointActiveExternalJob("job-1", WaitId.New(), TimerId.New())],
            CompletedSagaForwardActions =
            [
                new CheckpointSagaForwardAction("scope-1", "reserve-stock", "release-stock", Timestamp(6))
            ],
            SagaCompensationActions =
            [
                new CheckpointSagaCompensationAction(
                    "scope-1",
                    "release-stock",
                    0,
                    Timestamp(7),
                    Timestamp(8),
                    null,
                    null,
                    SagaCompensationActionStatus.Completed)
            ],
            SagaRecoveryInterventions =
            [
                new CheckpointSagaRecoveryIntervention(
                    "scope-1",
                    "refund",
                    "operator-1",
                    "mark-complete",
                    "resolved",
                    Timestamp(9),
                    WorkflowStatus.Completed)
            ],
            RequestedSagaCompensationScopes = ["scope-1"],
            RecordedParentResumeTokens = [EventId.New()],
            ConsumedParentResumeTokens = [EventId.New()]
        };
    }

    private static ProviderCommitBatch Batch(
        WorkflowStreamId streamId,
        StreamVersion expectedVersion,
        EventId? inboxEventId = null,
        OutboxRecordId? outboxRecordId = null)
    {
        return new ProviderCommitBatch
        {
            StreamId = streamId,
            ExpectedVersion = expectedVersion,
            Events =
            [
                new WorkflowStartedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = streamId.InstanceId,
                    CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    DefinitionId = DefinitionId.New(),
                    DefinitionVersion = DefinitionVersion.Initial
                }
            ],
            InboxOperations = inboxEventId is { } eventId
                ? [new InboxWrite(eventId, InboxRecordState.Applied)]
                : [],
            OutboxRecords = outboxRecordId is { } recordId
                ? [new OutboxWrite(recordId, "status", [1])]
                : []
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 12, 0, seconds, TimeSpan.Zero);
    }

    private static IReadOnlyList<WorkflowEvent> AllWorkflowEventTypes(InstanceId instanceId)
    {
        var eventSequence = 100;
        EventId nextEventId() => new(Guid.Parse($"00000000-0000-0000-0000-{eventSequence++:000000000000}"));
        CommandId commandId() => CommandId.New();
        CausationId causationId() => CausationId.New();
        DateTimeOffset occurredAt() => Timestamp(eventSequence % 50);

        var waitId = WaitId.New();
        var timerId = TimerId.New();
        var childInstanceId = InstanceId.New();
        var childDefinitionId = DefinitionId.New();
        var childDefinitionVersion = DefinitionVersion.Initial;
        var completionDefinitionId = DefinitionId.New();
        var completionDefinitionVersion = new DefinitionVersion(2);
        var groupId = "children-group";
        var resumeTokenId = nextEventId();
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
                CorrelationId = new CorrelationId("order-1"),
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
                MatchedEventId = EventId.New()
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
            new WorkflowChildScheduledEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ChildInstanceId = childInstanceId,
                ChildDefinitionId = childDefinitionId,
                ChildDefinitionVersion = childDefinitionVersion,
                WaitId = WaitId.New(),
                FailurePolicy = RunChildFailurePolicy.PropagateFailure
            },
            new WorkflowChildrenScheduledEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                GroupId = groupId,
                ChildDefinitionId = childDefinitionId,
                ChildDefinitionVersion = childDefinitionVersion,
                FailurePolicy = RunChildFailurePolicy.ContinueParent,
                JoinPolicy = RunChildrenJoinPolicy.WhenAny,
                ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining,
                TotalItemCount = 2,
                InitialDispatchCount = 1,
                NextDispatchIndex = 1,
                MaxConcurrency = 1,
                Children =
                [
                    new WorkflowChildMaterialization
                    {
                        Index = 0,
                        ChildInstanceId = childInstanceId,
                        ChildDefinitionId = childDefinitionId,
                        ChildDefinitionVersion = childDefinitionVersion,
                        ItemSnapshot = """{"id":1}"""
                    }
                ]
            },
            new WorkflowChildrenDispatchedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                GroupId = groupId,
                PreviousDispatchIndex = 1,
                NextDispatchIndex = 2,
                Children =
                [
                    new WorkflowChildMaterialization
                    {
                        Index = 1,
                        ChildInstanceId = InstanceId.New(),
                        ChildDefinitionId = childDefinitionId,
                        ChildDefinitionVersion = childDefinitionVersion,
                        ItemSnapshot = """{"id":2}"""
                    }
                ]
            },
            new WorkflowChildCompletedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ChildInstanceId = childInstanceId,
                ChildStatus = WorkflowStatus.Completed,
                ErrorSummary = null
            },
            new WorkflowParentResumeTokenRecordedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                GroupId = groupId,
                ResumeTokenId = resumeTokenId
            },
            new WorkflowParentResumeTokenConsumedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                GroupId = groupId,
                ResumeTokenId = resumeTokenId
            },
            new WorkflowChildResidualIntentRecordedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                GroupId = groupId,
                ResidualPolicy = RunChildrenResidualPolicy.DetachRemaining,
                ResidualChildInstanceIds = [childInstanceId]
            },
            new WorkflowChildCompensationScheduledEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                GroupId = groupId,
                CompensationDefinitionId = completionDefinitionId,
                CompensationDefinitionVersion = completionDefinitionVersion,
                Compensations =
                [
                    new WorkflowChildCompensationMaterialization
                    {
                        Index = 0,
                        SourceChildInstanceId = childInstanceId,
                        CompensationInstanceId = InstanceId.New(),
                        ItemSnapshot = """{"compensate":true}"""
                    }
                ]
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
                WaitId = WaitId.New(),
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
            new WorkflowExternalJobStartedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ExternalJobId = "job-1",
                Payload = [1, 2, 3],
                WaitId = WaitId.New(),
                TimeoutTimerId = TimerId.New(),
                TimeoutAt = Timestamp(46)
            },
            new WorkflowExternalJobCompletedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ExternalJobId = "job-1",
                CompletionEventId = EventId.New()
            },
            new WorkflowExternalJobTimedOutEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ExternalJobId = "job-1"
            },
            new WorkflowExternalJobStopRequestedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ExternalJobId = "job-1"
            },
            new WorkflowTimerBufferedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                TimerId = TimerId.New(),
                WakeupName = "paused-timeout"
            },
            new WorkflowPausedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt()
            },
            new WorkflowResumedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                BufferHandling = "replay"
            },
            new WorkflowDeliveryBufferedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                BufferedEventId = EventId.New(),
                EventName = "approved",
                CorrelationId = new CorrelationId("order-2"),
                BranchId = "branch-b"
            },
            new WorkflowDeliveryDiscardedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                DiscardedEventId = EventId.New()
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
            new WorkflowTerminalEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                Status = WorkflowStatus.Cancelled
            },
            new SagaForwardActionCompletedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ScopeId = "scope-1",
                ActionKey = "reserve",
                CompensationKey = "release"
            },
            new SagaForwardActionTimedOutEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ScopeId = "scope-1",
                ActionKey = "charge",
                CompensateScope = true
            },
            new SagaCompensationRequestedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ScopeId = "scope-1",
                Reason = "timeout"
            },
            new SagaCompensationStartedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ScopeId = "scope-1",
                ActionKey = "release",
                Order = 1
            },
            new SagaCompensationCompletedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ScopeId = "scope-1",
                ActionKey = "release"
            },
            new SagaCompensationFailedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ScopeId = "scope-1",
                ActionKey = "refund",
                ErrorSummary = "manual review"
            },
            new SagaManualRecoveryRecordedEvent
            {
                EventId = nextEventId(),
                InstanceId = instanceId,
                CommandId = commandId(),
                CausationId = causationId(),
                OccurredAt = occurredAt(),
                ScopeId = "scope-1",
                ActionKey = "refund",
                OperatorId = "operator-1",
                RecoveryAction = "mark-complete",
                Reason = "resolved",
                TargetStatus = WorkflowStatus.Completed
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

        public IWorkflowOutboxStore OutboxStore => provider;

        public IWorkflowProjectionStore ProjectionStore => provider;
    }
}
