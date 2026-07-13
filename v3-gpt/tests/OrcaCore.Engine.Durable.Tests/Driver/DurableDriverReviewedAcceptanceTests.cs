using System.Collections.Concurrent;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableDriverReviewedAcceptanceTests
{
    private static readonly DateTimeOffset TestEpoch = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private sealed record StartInput(string OrderId, IReadOnlyList<string> Labels);

    private sealed class TestState
    {
        public string OrderId { get; set; } = string.Empty;

        public List<string> Log { get; set; } = [];

        public int Generation { get; set; }
    }

    private sealed record Harness(
        DurableWorkflowRuntime Runtime,
        DurableCommandProcessor Processor,
        InMemoryWorkflowProvider Store,
        DurableContinuationPump Pump,
        Clock Clock,
        IWorkflowPayloadSerializer Serializer);

    private sealed class AppendStep(string value) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Log.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingStateWriteSerializer : IWorkflowPayloadSerializer
    {
        private readonly JsonWorkflowPayloadSerializer inner = new();

        public SerializedPayload Serialize<TPayload>(TPayload payload)
        {
            if (typeof(TPayload) == typeof(TestState))
            {
                throw new InvalidOperationException("Injected state checkpoint failure.");
            }

            return inner.Serialize(payload);
        }

        public TPayload Deserialize<TPayload>(SerializedPayload payload)
        {
            return inner.Deserialize<TPayload>(payload);
        }
    }

    private sealed class ContinueOnceStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Log.Add($"generation-{context.State.Generation}");
            if (context.State.Generation == 0)
            {
                var replacement = new TestState
                {
                    OrderId = context.State.OrderId,
                    Generation = 1,
                    Log = [.. context.State.Log]
                };
                return ValueTask.FromResult<StepResult>(
                    new StepResult.ContinueAsNew<TestState>(replacement));
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class RetryOnceStep : IStep<TestState>
    {
        private static readonly ConcurrentDictionary<string, int> Attempts = new();

        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            var attempt = Attempts.AddOrUpdate(context.State.OrderId, 1, (_, current) => current + 1);
            context.State.Log.Add($"attempt-{attempt}-mutation");
            return ValueTask.FromResult<StepResult>(attempt == 1
                ? new StepResult.Failed(new WorkflowLifecycleException("retry me"))
                : new StepResult.Completed());
        }

        internal static int Count(string orderId) => Attempts.GetValueOrDefault(orderId);
    }

    private sealed class CancellationAwareStep : IStep<TestState>
    {
        private static readonly ConcurrentDictionary<string, int> Executions = new();
        private static readonly ConcurrentDictionary<string, int> Cancellations = new();
        private static readonly ConcurrentDictionary<string, TaskCompletionSource> Started = new();

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            var key = context.State.OrderId;
            Executions.AddOrUpdate(key, 1, (_, current) => current + 1);
            Started.GetOrAdd(
                key,
                _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                .TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new StepResult.Completed();
            }
            catch (OperationCanceledException)
            {
                Cancellations.AddOrUpdate(key, 1, (_, current) => current + 1);
                throw;
            }
        }

        internal static Task WaitUntilStartedAsync(string key, CancellationToken cancellationToken) =>
            Started.GetOrAdd(
                key,
                _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                .Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        internal static int ExecutionCount(string key) => Executions.GetValueOrDefault(key);

        internal static int CancellationCount(string key) => Cancellations.GetValueOrDefault(key);
    }

    private static Harness CreateHarness(
        InMemoryWorkflowProvider? store = null,
        Clock? clock = null,
        IWorkflowPayloadSerializer? serializer = null,
        DurableDriverBudget? budget = null,
        int maxDriveAttemptsBeforePark = DurableContinuationPump.DefaultMaxDriveAttemptsBeforePark)
    {
        clock ??= new Clock(TestEpoch);
        store ??= new InMemoryWorkflowProvider(clock.TimeProvider);
        var processor = new DurableCommandProcessor(store);
        serializer ??= new JsonWorkflowPayloadSerializer();
        var management = new DurableManagement(store, eventStore: store, commandProcessor: processor);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            serializer,
            budget,
            projectionStore: store,
            management: management);
        var pump = new DurableContinuationPump(
            store,
            runtime,
            processor,
            clock.TimeProvider,
            maxDriveAttemptsBeforePark,
            initialFailureBackoff: TimeSpan.FromSeconds(1));
        return new Harness(runtime, processor, store, pump, clock, serializer);
    }

    private static WorkflowDefinition<TestState> WaitingDefinition(
        DefinitionId definitionId,
        DefinitionVersion? version = null)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .Wait("Approved", state => new CorrelationId(state.OrderId))
            .Then(new AppendStep("approved"))
            .End("done")
            .Build(definitionId, version ?? DefinitionVersion.Initial);
    }

    private static async Task<WorkflowInstanceSnapshot> SnapshotAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId)
    {
        var snapshots = await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);
        return snapshots.Should().ContainSingle().Subject;
    }

    private static async Task<TestState> FinalStateAsync(Harness harness, InstanceId instanceId)
    {
        var checkpoint = await harness.Store.LoadCheckpointAsync(
            instanceId,
            TestContext.Current.CancellationToken);
        checkpoint.HasValue.Should().BeTrue();
        var envelope = DurableExecutionEnvelope.Deserialize(checkpoint.Value.Payload);
        return harness.Serializer.Deserialize<TestState>(
            new SerializedPayload(envelope.StateContentType, envelope.StatePayload));
    }

    private static Task<int> PumpOnceAsync(Harness harness)
    {
        return harness.Pump.PumpOnceAsync(
            new OutboxClaimRequest(100, harness.Clock.Now, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
    }

    private static async Task FireDueTimersAsync(Harness harness)
    {
        var due = await harness.Store.ClaimDueAsync(
            new TimerClaimRequest(harness.Clock.Now, 10, harness.Clock.Now, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
        foreach (var command in due)
        {
            await harness.Processor.ProcessAsync(command, TestContext.Current.CancellationToken);
            await harness.Store.CompleteAsync(command.TimerId, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    [Trait("AC", "DR-AC-023")]
    public async Task CommittedTypedStartInput_SurvivesHostReplacementBeforeInit()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var firstProcessor = new DurableCommandProcessor(store);
        var serializer = new JsonWorkflowPayloadSerializer();
        var definitionId = DefinitionId.New();
        var input = new StartInput("order-input", ["priority", "fragile"]);

        var start = await new DurableStartService(firstProcessor).StartOrGetAsync(
            new StartOrGetRequest(
                "dr-ac-023",
                definitionId,
                DefinitionVersion.Initial,
                serializer.Serialize(input),
                clock.Now),
            TestContext.Current.CancellationToken);

        var replacement = CreateHarness(store, clock);
        replacement.Runtime.RegisterDefinition(
            new WorkflowBuilder<TestState>()
                .Init<StartInput>(value => new TestState
                {
                    OrderId = value.OrderId,
                    Log = [.. value.Labels]
                })
                .Then(new AppendStep("initialized"))
                .End("done")
                .Build(definitionId, DefinitionVersion.Initial));

        await replacement.Runtime.DriveAsync(
            start.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken);

        (await SnapshotAsync(store, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
        var state = await FinalStateAsync(replacement, start.InstanceId);
        state.OrderId.Should().Be("order-input");
        state.Log.Should().Equal("priority", "fragile", "initialized");

        var beforeRejectedStart = await store.CountAsync(
            WorkflowProjectionQuery.All,
            TestContext.Current.CancellationToken);
        var rejected = async () => await replacement.Runtime.StartOrGetAsync<Action, TestState>(
            "dr-ac-023-unserializable",
            definitionId,
            DefinitionVersion.Initial,
            () => { },
            TestContext.Current.CancellationToken);
        await rejected.Should().ThrowAsync<NotSupportedException>();
        (await store.CountAsync(WorkflowProjectionQuery.All, TestContext.Current.CancellationToken))
            .Should().Be(beforeRejectedStart, "serialization failure must precede the start commit");
    }

    [Fact]
    [Trait("AC", "DR-AC-009")]
    public async Task VersionBindingPark_RequiresRegistrationAndSingleExpectedVersionRearm()
    {
        var definitionId = DefinitionId.New();
        var first = CreateHarness();
        first.Runtime.RegisterDefinition(WaitingDefinition(definitionId));
        var start = await first.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-009",
            definitionId,
            DefinitionVersion.Initial,
            "order-rearm",
            TestContext.Current.CancellationToken);

        var replacement = CreateHarness(first.Store, first.Clock);
        var drive = await replacement.Runtime.DriveAsync(
            start.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken);
        drive.Outcome.Should().Be(DurableSegmentOutcome.Parked);

        var parked = await SnapshotAsync(first.Store, start.InstanceId);
        parked.Status.Should().Be(WorkflowStatus.Parked);
        var unavailable = await replacement.Runtime.RearmAsync(
            start.InstanceId,
            new DurableRearmRequest(new StreamVersion(parked.StreamVersion!.Value)),
            TestContext.Current.CancellationToken);
        unavailable.Outcome.Should().Be(DurableCommandOutcome.NoOp);

        replacement.Runtime.RegisterDefinition(WaitingDefinition(definitionId));
        (await SnapshotAsync(first.Store, start.InstanceId)).Status.Should().Be(
            WorkflowStatus.Parked,
            "registration alone must not mutate durable lifecycle state");

        var expectedVersion = new StreamVersion((await SnapshotAsync(first.Store, start.InstanceId)).StreamVersion!.Value);
        var concurrent = await Task.WhenAll(
            replacement.Runtime.RearmAsync(
                start.InstanceId,
                new DurableRearmRequest(expectedVersion),
                TestContext.Current.CancellationToken),
            replacement.Runtime.RearmAsync(
                start.InstanceId,
                new DurableRearmRequest(expectedVersion),
                TestContext.Current.CancellationToken));
        concurrent.Count(result => result.Outcome == DurableCommandOutcome.Committed).Should().Be(1);
        concurrent.Count(result => result.Outcome == DurableCommandOutcome.Conflict).Should().Be(1);

        await replacement.Runtime.RaiseEventAsync(
            start.InstanceId,
            "Approved",
            new CorrelationId("order-rearm"),
            cancellationToken: TestContext.Current.CancellationToken);
        (await SnapshotAsync(first.Store, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait("AC", "DR-AC-028")]
    public async Task PoisonPark_RejectsGenericResumeAndRequiresAcknowledgedRearm()
    {
        var definitionId = DefinitionId.New();
        var harness = CreateHarness();
        harness.Runtime.RegisterDefinition(WaitingDefinition(definitionId));
        var start = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-028",
            definitionId,
            DefinitionVersion.Initial,
            "order-poison",
            TestContext.Current.CancellationToken);
        var beforePark = await SnapshotAsync(harness.Store, start.InstanceId);

        var park = await harness.Processor.ProcessAsync(
            new DurableParkCommand(
                CommandId.New(),
                start.InstanceId,
                harness.Clock.Now,
                DurableParkReason.Poison,
                "operator repair required",
                3,
                new StreamVersion(beforePark.StreamVersion!.Value))
            {
                ExpectedStreamVersion = new StreamVersion(beforePark.StreamVersion.Value)
            },
            TestContext.Current.CancellationToken);
        park.Outcome.Should().Be(DurableCommandOutcome.Committed);

        var genericResume = await harness.Runtime.Management.ResumeAsync(
            start.InstanceId,
            harness.Clock.Now,
            replayBufferedDeliveries: true,
            TestContext.Current.CancellationToken);
        genericResume.Outcome.Should().Be(DurableCommandOutcome.NoOp);

        var parked = await SnapshotAsync(harness.Store, start.InstanceId);
        var expectedVersion = new StreamVersion(parked.StreamVersion!.Value);
        var unacknowledged = await harness.Runtime.RearmAsync(
            start.InstanceId,
            new DurableRearmRequest(expectedVersion),
            TestContext.Current.CancellationToken);
        unacknowledged.Outcome.Should().Be(DurableCommandOutcome.NoOp);
        (await SnapshotAsync(harness.Store, start.InstanceId)).Status.Should().Be(WorkflowStatus.Parked);

        var acknowledged = await harness.Runtime.RearmAsync(
            start.InstanceId,
            new DurableRearmRequest(expectedVersion, AcknowledgePoison: true),
            TestContext.Current.CancellationToken);
        acknowledged.Outcome.Should().Be(DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait("AC", "DR-AC-027")]
    public async Task ContinuationPoisonCount_SurvivesAlternatingHostsAndParksAtThreshold()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();
        WorkflowDefinition<TestState> Definition() => new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .Then(new AppendStep("committed"))
            .Then(new AppendStep("failing-checkpoint"))
            .End("done")
            .Build(definitionId, DefinitionVersion.Initial);

        var first = CreateHarness(
            store,
            clock,
            budget: new DurableDriverBudget(1, TimeSpan.FromSeconds(30)));
        first.Runtime.RegisterDefinition(Definition());
        var start = await first.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-027",
            definitionId,
            DefinitionVersion.Initial,
            "order-poison-count",
            TestContext.Current.CancellationToken);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var replacement = CreateHarness(
                store,
                clock,
                new FailingStateWriteSerializer(),
                maxDriveAttemptsBeforePark: 3);
            replacement.Runtime.RegisterDefinition(Definition());
            await PumpOnceAsync(replacement);
            clock.Advance(TimeSpan.FromSeconds(1 << attempt));
        }

        var parked = await SnapshotAsync(store, start.InstanceId);
        parked.Status.Should().Be(WorkflowStatus.Parked);
        parked.ErrorSummary.Should().Contain("failed 3 times");
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowContinuationAttemptFailedEvent>()
            .Select(failure => failure.AttemptCount)
            .Should().Equal(1, 2);
        events.OfType<WorkflowParkedEvent>().Should().ContainSingle()
            .Which.FailedAttemptCount.Should().Be(3);
    }

    [Fact]
    public async Task BudgetExhaustedBeforeFirstCommand_KeepsContinuationRetryable()
    {
        var definitionId = DefinitionId.New();
        var harness = CreateHarness(
            budget: new DurableDriverBudget(1, TimeSpan.FromTicks(1)));
        harness.Runtime.RegisterDefinition(
            new WorkflowBuilder<TestState>()
                .Init<string>(orderId => new TestState { OrderId = orderId })
                .Then(new AppendStep("never-admitted"))
                .End("done")
                .Build(definitionId, DefinitionVersion.Initial));
        var start = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "zero-progress-budget",
            definitionId,
            DefinitionVersion.Initial,
            "budget",
            TestContext.Current.CancellationToken);

        (await PumpOnceAsync(harness)).Should().Be(0);
        var retryable = await harness.Store.ClaimAsync(
            new OutboxClaimRequest(10, harness.Clock.Now, TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);

        retryable.Should().NotBeEmpty("the only continuation must remain claimable without a successor");
        var events = await harness.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowStepCompletedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task DriveFailureWithoutSuccessorCheckpoint_KeepsClaimedContinuationRetryable()
    {
        var definitionId = DefinitionId.New();
        WorkflowDefinition<TestState> Definition() => new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .Parallel(
                ("runnable", branch => branch
                    .Wait("A", _ => new CorrelationId("a"))
                    .Then(new AppendStep("a"))),
                ("blocked", branch => branch.Wait("B", _ => new CorrelationId("b"))))
            .End("done")
            .Build(definitionId, DefinitionVersion.Initial);
        var first = CreateHarness();
        first.Runtime.RegisterDefinition(Definition());
        var start = await first.Runtime.StartOrGetAsync<string, TestState>(
            "continuation-failure-no-checkpoint",
            definitionId,
            DefinitionVersion.Initial,
            "parallel",
            TestContext.Current.CancellationToken);
        await first.Processor.ProcessAsync(
            new DeliverEventCommand
            {
                CommandId = CommandId.New(),
                InstanceId = start.InstanceId,
                RequestedAt = first.Clock.Now,
                Envelope = new EventEnvelope
                {
                    EventId = EventId.New(),
                    EventName = "A",
                    CorrelationId = new CorrelationId("a"),
                    OccurredAt = first.Clock.Now
                }
            },
            TestContext.Current.CancellationToken);

        var failingHost = CreateHarness(
            first.Store,
            first.Clock,
            new FailingStateWriteSerializer());
        failingHost.Runtime.RegisterDefinition(Definition());
        (await PumpOnceAsync(failingHost)).Should().Be(0);
        first.Clock.Advance(TimeSpan.FromSeconds(1));

        var retryable = await first.Store.ClaimAsync(
            new OutboxClaimRequest(20, first.Clock.Now, TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);
        retryable.Should().NotBeEmpty(
            "a drive-failure fact without a runnable checkpoint emits no successor signal");
    }

    [Fact]
    [Trait("AC", "DR-AC-030")]
    public async Task PublicFacade_RoutesByCorrelationAndDefinitionAndExposesManagement()
    {
        var definitionId = DefinitionId.New();
        var harness = CreateHarness();
        harness.Runtime.RegisterDefinition(WaitingDefinition(definitionId));

        var unique = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-030-unique",
            definitionId,
            DefinitionVersion.Initial,
            "unique",
            TestContext.Current.CancellationToken);
        var fanoutA = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-030-fanout-a",
            definitionId,
            DefinitionVersion.Initial,
            "fanout",
            TestContext.Current.CancellationToken);
        var fanoutB = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-030-fanout-b",
            definitionId,
            DefinitionVersion.Initial,
            "fanout",
            TestContext.Current.CancellationToken);

        var routed = await harness.Runtime.RaiseEventByCorrelationAsync(
            "Approved",
            new CorrelationId("unique"),
            new { Source = "correlation" },
            cancellationToken: TestContext.Current.CancellationToken);
        routed.InstanceId.Should().Be(unique.InstanceId);
        routed.Result.Outcome.Should().Be(DurableCommandOutcome.Committed);

        var fanout = await harness.Runtime.RaiseEventToDefinitionAsync(
            definitionId,
            "Approved",
            new CorrelationId("fanout"),
            new { Source = "definition" },
            TestContext.Current.CancellationToken);
        fanout.Should().HaveCount(2);
        fanout.Should().OnlyContain(result => result.Result.Outcome == DurableCommandOutcome.Committed);

        var managed = await harness.Runtime.Management.ForDefinition(definitionId)
            .ListAsync(TestContext.Current.CancellationToken);
        managed.Should().HaveCount(3);
        managed.Should().OnlyContain(instance => instance.Status == WorkflowStatus.Completed);

        var fanoutMatchedIds = new List<EventId>();
        foreach (var instanceId in new[] { fanoutA.InstanceId, fanoutB.InstanceId })
        {
            var events = await harness.Store.LoadTailAsync(
                new WorkflowStreamId(instanceId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken);
            fanoutMatchedIds.Add(events.OfType<WorkflowWaitMatchedEvent>().Single().MatchedEventId);
        }

        fanoutMatchedIds.Distinct().Should().HaveCount(2, "each fanout target gets its own event id");
    }

    [Fact]
    public async Task RunChild_DriverCheckpointAndParentResumeSurviveHostReplacement()
    {
        var parentDefinitionId = DefinitionId.New();
        var childDefinitionId = DefinitionId.New();
        var first = CreateHarness();
        WorkflowDefinition<TestState> ParentDefinition() => new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .RunChild(childDefinitionId, DefinitionVersion.Initial)
            .Then(new AppendStep("after-child"))
            .End("done")
            .Build(parentDefinitionId, DefinitionVersion.Initial);

        first.Runtime.RegisterDefinition(ParentDefinition());
        var start = await first.Runtime.StartOrGetAsync<string, TestState>(
            "driver-child-restart",
            parentDefinitionId,
            DefinitionVersion.Initial,
            "parent",
            TestContext.Current.CancellationToken);
        var scheduledEvents = await first.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var child = scheduledEvents.OfType<WorkflowChildScheduledEvent>().Should().ContainSingle().Subject;

        var replacement = CreateHarness(first.Store, first.Clock);
        replacement.Runtime.RegisterDefinition(ParentDefinition());
        var completedChild = await replacement.Processor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandId.New(),
                start.InstanceId,
                replacement.Clock.Now,
                child.ChildInstanceId,
                WorkflowStatus.Completed,
                null),
            TestContext.Current.CancellationToken);
        completedChild.Outcome.Should().Be(DurableCommandOutcome.Committed);

        await PumpOnceAsync(replacement);

        (await SnapshotAsync(first.Store, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
        (await FinalStateAsync(replacement, start.InstanceId)).Log.Should().Equal("after-child");
        var finalEvents = await first.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        finalEvents.OfType<WorkflowParentResumeTokenConsumedEvent>().Should().ContainSingle();
        finalEvents.OfType<WorkflowResumeConsumedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "DR-AC-033")]
    public async Task DriverOwnedContinueAsNew_CommitsFreshEnvelopeAndResumesAfterReplacement()
    {
        var definitionId = DefinitionId.New();
        WorkflowDefinition<TestState> Definition() => new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .Then(new ContinueOnceStep())
            .Then(new AppendStep("after-rollover"))
            .End("done")
            .Build(definitionId, DefinitionVersion.Initial);

        var first = CreateHarness();
        first.Runtime.RegisterDefinition(Definition());
        var start = await new DurableStartService(first.Processor).StartOrGetAsync(
            new StartOrGetRequest(
                "dr-ac-033",
                definitionId,
                DefinitionVersion.Initial,
                first.Serializer.Serialize("rollover"),
                first.Clock.Now),
            TestContext.Current.CancellationToken);
        var rollover = await first.Runtime.DriveAsync(
            start.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken);
        rollover.Outcome.Should().Be(DurableSegmentOutcome.ContinuedAsNew);
        var rolledOver = await SnapshotAsync(first.Store, start.InstanceId);
        rolledOver.Status.Should().Be(WorkflowStatus.Running);
        rolledOver.ContinueAsNewGeneration.Should().Be(1);
        var checkpoint = await first.Store.LoadCheckpointAsync(
            start.InstanceId,
            TestContext.Current.CancellationToken);
        checkpoint.Value.ContentType.Should().Be(DurableExecutionEnvelope.ContentType);

        var replacement = CreateHarness(first.Store, first.Clock);
        replacement.Runtime.RegisterDefinition(Definition());
        await PumpOnceAsync(replacement);

        var completed = await SnapshotAsync(first.Store, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ContinueAsNewGeneration.Should().Be(1);
        var state = await FinalStateAsync(replacement, start.InstanceId);
        state.Generation.Should().Be(1);
        state.Log.Should().Equal("generation-0", "generation-1", "after-rollover");
        var events = await first.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowContinuedAsNewEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task FacadeOnlyStart_DrivesAcrossPolicyBoundaryWithoutHostedPump()
    {
        var definitionId = DefinitionId.New();
        var harness = CreateHarness();
        harness.Runtime.RegisterDefinition(
            new WorkflowBuilder<TestState>()
                .Init<string>(orderId => new TestState { OrderId = orderId })
                .WithTimeout(TimeSpan.FromMinutes(5))
                .Then(new AppendStep("timed-step"))
                .End("done")
                .Build(definitionId, DefinitionVersion.Initial));

        var start = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "facade-only-policy-boundary",
            definitionId,
            DefinitionVersion.Initial,
            "policy",
            TestContext.Current.CancellationToken);

        (await SnapshotAsync(harness.Store, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
        (await FinalStateAsync(harness, start.InstanceId)).Log.Should().Equal("timed-step");
    }

    [Fact]
    public async Task FacadeOnlyStart_DrivesAcrossContinueAsNewWithoutHostedPump()
    {
        var definitionId = DefinitionId.New();
        var harness = CreateHarness();
        harness.Runtime.RegisterDefinition(
            new WorkflowBuilder<TestState>()
                .Init<string>(orderId => new TestState { OrderId = orderId })
                .Then(new ContinueOnceStep())
                .Then(new AppendStep("after-rollover"))
                .End("done")
                .Build(definitionId, DefinitionVersion.Initial));

        var start = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "facade-only-continue-as-new",
            definitionId,
            DefinitionVersion.Initial,
            "rollover",
            TestContext.Current.CancellationToken);

        var completed = await SnapshotAsync(harness.Store, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ContinueAsNewGeneration.Should().Be(1);
        (await FinalStateAsync(harness, start.InstanceId)).Log
            .Should().Equal("generation-0", "generation-1", "after-rollover");
    }

    [Fact]
    [Trait("AC", "DR-AC-024")]
    public async Task DurableRetry_BackoffAttemptAndRollbackSurviveHostReplacement()
    {
        var definitionId = DefinitionId.New();
        const string orderId = "durable-retry";
        WorkflowDefinition<TestState> Definition() => new WorkflowBuilder<TestState>()
            .Init<string>(value => new TestState { OrderId = value })
            .WithRetry(maxAttempts: 2, backoff: TimeSpan.FromMinutes(5))
            .Then(new RetryOnceStep())
            .Then(new AppendStep("after-retry"))
            .End("done")
            .Build(definitionId, DefinitionVersion.Initial);

        var first = CreateHarness();
        first.Runtime.RegisterDefinition(Definition());
        var start = await first.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-024",
            definitionId,
            DefinitionVersion.Initial,
            orderId,
            TestContext.Current.CancellationToken);

        RetryOnceStep.Count(orderId).Should().Be(1);
        var checkpoint = await first.Store.LoadCheckpointAsync(
            start.InstanceId,
            TestContext.Current.CancellationToken);
        var backoffEnvelope = DurableExecutionEnvelope.Deserialize(checkpoint.Value.Payload);
        var retryCursor = backoffEnvelope.Position.Cursors.Should().ContainSingle().Subject;
        retryCursor.Phase.Should().Be(DurableCursorPhase.SuspendedOnRetryBackoff);
        retryCursor.RetryAttempt.Should().Be(2);
        retryCursor.RetryNotBefore.Should().Be(TestEpoch.AddMinutes(5));
        retryCursor.LogicalOperationKey.Should().NotBeNullOrWhiteSpace();
        var rolledBack = first.Serializer.Deserialize<TestState>(
            new SerializedPayload(backoffEnvelope.StateContentType, backoffEnvelope.StatePayload));
        rolledBack.Log.Should().BeEmpty("failed-attempt mutations must not cross the retry boundary");

        var replacement = CreateHarness(first.Store, first.Clock);
        replacement.Runtime.RegisterDefinition(Definition());
        await PumpOnceAsync(replacement);
        RetryOnceStep.Count(orderId).Should().Be(1, "the durable backoff is not yet eligible");

        first.Clock.Advance(TimeSpan.FromMinutes(5));
        await FireDueTimersAsync(replacement);
        await PumpOnceAsync(replacement);

        (await SnapshotAsync(first.Store, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
        RetryOnceStep.Count(orderId).Should().Be(2);
        (await FinalStateAsync(replacement, start.InstanceId)).Log
            .Should().Equal("attempt-2-mutation", "after-retry");
        var events = await first.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowTimerScheduledEvent>().Should().ContainSingle();
        events.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle();
        events.OfType<WorkflowStepCompletedEvent>().Should().HaveCount(2);
        events.OfType<WorkflowStepFailedEvent>().Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "DR-AC-025")]
    public async Task DurableTimeout_DeadlineSurvivesHostReplacementAndAppliesOnce()
    {
        var timeoutDefinitionId = DefinitionId.New();
        const string timeoutOrderId = "durable-timeout";
        WorkflowDefinition<TestState> TimeoutDefinition() => new WorkflowBuilder<TestState>()
            .Init<string>(value => new TestState { OrderId = value })
            .WithTimeout(TimeSpan.FromMinutes(5))
            .Then(new CancellationAwareStep())
            .End("unreachable")
            .Build(timeoutDefinitionId, DefinitionVersion.Initial);

        var first = CreateHarness();
        first.Runtime.RegisterDefinition(TimeoutDefinition());
        var timeoutStart = await new DurableStartService(first.Processor).StartOrGetAsync(
            new StartOrGetRequest(
                "dr-ac-025-timeout",
                timeoutDefinitionId,
                DefinitionVersion.Initial,
                first.Serializer.Serialize(timeoutOrderId),
                first.Clock.Now),
            TestContext.Current.CancellationToken);
        var boundary = await first.Runtime.DriveAsync(
            timeoutStart.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken);
        boundary.Outcome.Should().Be(DurableSegmentOutcome.PolicyBoundary);
        var admission = await first.Store.LoadCheckpointAsync(
            timeoutStart.InstanceId,
            TestContext.Current.CancellationToken);
        var admittedCursor = DurableExecutionEnvelope.Deserialize(admission.Value.Payload)
            .Position.Cursors.Should().ContainSingle().Subject;
        admittedCursor.TimeoutDeadline.Should().Be(TestEpoch.AddMinutes(5));
        CancellationAwareStep.ExecutionCount(timeoutOrderId).Should().Be(0);

        first.Clock.Advance(TimeSpan.FromMinutes(5));
        var replacement = CreateHarness(first.Store, first.Clock);
        replacement.Runtime.RegisterDefinition(TimeoutDefinition());
        await PumpOnceAsync(replacement);

        (await SnapshotAsync(first.Store, timeoutStart.InstanceId)).Status.Should().Be(WorkflowStatus.Failed);
        CancellationAwareStep.ExecutionCount(timeoutOrderId).Should().Be(0);
        CancellationAwareStep.CancellationCount(timeoutOrderId).Should().Be(0);
        var timeoutEvents = await first.Store.LoadTailAsync(
            new WorkflowStreamId(timeoutStart.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        timeoutEvents.OfType<WorkflowStepFailedEvent>().Should().ContainSingle();
        timeoutEvents.OfType<WorkflowTerminalEvent>()
            .Should().ContainSingle(item => item.Status == WorkflowStatus.Failed);
    }

    [Fact]
    [Trait("AC", "DR-AC-025")]
    public async Task OperatorCancellation_ReachesRunningStepAndDoesNotRetry()
    {
        var cancelDefinitionId = DefinitionId.New();
        const string cancelOrderId = "durable-operator-cancel";
        var cancelDefinition = new WorkflowBuilder<TestState>()
            .Init<string>(value => new TestState { OrderId = value })
            .WithCancellation()
            .WithRetry(maxAttempts: 2)
            .Then(new CancellationAwareStep())
            .End("unreachable")
            .Build(cancelDefinitionId, DefinitionVersion.Initial);
        var cancellationHost = CreateHarness();
        cancellationHost.Runtime.RegisterDefinition(cancelDefinition);
        var committedStart = await new DurableStartService(cancellationHost.Processor).StartOrGetAsync(
            new StartOrGetRequest(
                "dr-ac-025-cancel",
                cancelDefinitionId,
                DefinitionVersion.Initial,
                cancellationHost.Serializer.Serialize(cancelOrderId),
                cancellationHost.Clock.Now),
            TestContext.Current.CancellationToken);
        var runningDrive = cancellationHost.Runtime.DriveAsync(
            committedStart.InstanceId,
            DurableDriveMode.Required,
            TestContext.Current.CancellationToken);
        await CancellationAwareStep.WaitUntilStartedAsync(
            cancelOrderId,
            TestContext.Current.CancellationToken);
        var cancelTask = cancellationHost.Runtime.Management.CancelAsync(
            committedStart.InstanceId,
            cancellationHost.Clock.Now,
            TestContext.Current.CancellationToken);
        var cancelled = await cancelTask.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await runningDrive.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        cancelled.Outcome.Should().BeOneOf(DurableCommandOutcome.Committed, DurableCommandOutcome.NoOp);
        (await SnapshotAsync(cancellationHost.Store, committedStart.InstanceId)).Status
            .Should().Be(WorkflowStatus.Cancelled);
        CancellationAwareStep.ExecutionCount(cancelOrderId).Should().Be(1);
        CancellationAwareStep.CancellationCount(cancelOrderId).Should().Be(1);
        var cancellationEvents = await cancellationHost.Store.LoadTailAsync(
            new WorkflowStreamId(committedStart.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        cancellationEvents.OfType<WorkflowStepFailedEvent>().Should().BeEmpty();
        cancellationEvents.OfType<WorkflowTerminalEvent>()
            .Should().ContainSingle(item => item.Status == WorkflowStatus.Cancelled);
    }

    [Fact]
    [Trait("AC", "DR-AC-032")]
    public async Task DurableWhenFirst_ConcurrentMatchesAcrossHostsCommitOneWinner()
    {
        var definitionId = DefinitionId.New();
        WorkflowDefinition<TestState> Definition() => new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .WhenFirst(
                WhenFirstResidualPolicy.CancelRemaining,
                ("a", branch => branch
                    .Wait("A", _ => new CorrelationId("a"))
                    .Then(new AppendStep("winner-a"))),
                ("b", branch => branch
                    .Wait("B", _ => new CorrelationId("b"))
                    .Then(new AppendStep("winner-b"))))
            .Then(new AppendStep("after-winner"))
            .End("done")
            .Build(definitionId, DefinitionVersion.Initial);

        var first = CreateHarness();
        first.Runtime.RegisterDefinition(Definition());
        var start = await first.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-032",
            definitionId,
            DefinitionVersion.Initial,
            "race",
            TestContext.Current.CancellationToken);
        var hostA = CreateHarness(first.Store, first.Clock);
        var hostB = CreateHarness(first.Store, first.Clock);
        hostA.Runtime.RegisterDefinition(Definition());
        hostB.Runtime.RegisterDefinition(Definition());

        var deliveries = await Task.WhenAll(
            hostA.Processor.ProcessAsync(
                new DeliverEventCommand
                {
                    CommandId = CommandId.New(),
                    InstanceId = start.InstanceId,
                    RequestedAt = hostA.Clock.Now,
                    Envelope = new EventEnvelope
                    {
                        EventId = EventId.New(),
                        EventName = "A",
                        CorrelationId = new CorrelationId("a"),
                        OccurredAt = hostA.Clock.Now
                    }
                },
                TestContext.Current.CancellationToken),
            hostB.Processor.ProcessAsync(
                new DeliverEventCommand
                {
                    CommandId = CommandId.New(),
                    InstanceId = start.InstanceId,
                    RequestedAt = hostB.Clock.Now,
                    Envelope = new EventEnvelope
                    {
                        EventId = EventId.New(),
                        EventName = "B",
                        CorrelationId = new CorrelationId("b"),
                        OccurredAt = hostB.Clock.Now
                    }
                },
                TestContext.Current.CancellationToken));
        deliveries.Count(result => result.Outcome == DurableCommandOutcome.Committed).Should().Be(1);
        deliveries.Count(result => result.Outcome == DurableCommandOutcome.Conflict).Should().Be(1);
        await Task.WhenAll(PumpOnceAsync(hostA), PumpOnceAsync(hostB));

        var completed = await SnapshotAsync(first.Store, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        var state = await FinalStateAsync(hostA, start.InstanceId);
        state.Log.Should().HaveCount(2);
        state.Log[0].Should().BeOneOf("winner-a", "winner-b");
        state.Log[1].Should().Be("after-winner");

        await Task.WhenAll(PumpOnceAsync(hostA), PumpOnceAsync(hostB));
        (await FinalStateAsync(hostA, start.InstanceId)).Log.Should().Equal(state.Log);
        var events = await first.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        events.OfType<WorkflowResumeConsumedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "DR-AC-032")]
    public async Task DurableWhenFirst_ReadyJoinIsNotBlockedByDeferredParallelCandidate()
    {
        var definitionId = DefinitionId.New();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .Parallel(
                ("quick", branch => branch.Then(new AppendStep("quick"))),
                ("race", branch => branch.WhenFirst(
                    WhenFirstResidualPolicy.CancelRemaining,
                    ("winner", candidate => candidate
                        .Wait("A", _ => new CorrelationId("a"))
                        .Then(new AppendStep("winner"))),
                    ("loser", candidate => candidate
                        .Wait("B", _ => new CorrelationId("b"))))))
            .Then(new AppendStep("after-parallel"))
            .End("done")
            .Build(definitionId, DefinitionVersion.Initial);
        var harness = CreateHarness();
        harness.Runtime.RegisterDefinition(definition);
        var start = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-032-deferred-parallel-candidate",
            definitionId,
            DefinitionVersion.Initial,
            "composed-race",
            TestContext.Current.CancellationToken);

        (await SnapshotAsync(harness.Store, start.InstanceId)).ActiveWaits.Should().HaveCount(2);
        await harness.Runtime.RaiseEventAsync(
            start.InstanceId,
            "A",
            new CorrelationId("a"),
            new { Winner = true },
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = await SnapshotAsync(harness.Store, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        (await FinalStateAsync(harness, start.InstanceId)).Log
            .Should().Equal("quick", "winner", "after-parallel");
        var events = await harness.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "DR-AC-032")]
    public async Task DurableWhenFirst_NestedLosingBranchCancelsOwnedWaitAndCompletes()
    {
        var definitionId = DefinitionId.New();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(orderId => new TestState { OrderId = orderId })
            .WhenFirst(
                WhenFirstResidualPolicy.CancelRemaining,
                ("winner", branch => branch
                    .Wait("A", _ => new CorrelationId("a"))
                    .Then(new AppendStep("winner"))),
                ("nested-loser", branch => branch.If(
                    _ => true,
                    nested => nested.Wait("B", _ => new CorrelationId("b")))))
            .Then(new AppendStep("after-winner"))
            .End("done")
            .Build(definitionId, DefinitionVersion.Initial);
        var harness = CreateHarness();
        harness.Runtime.RegisterDefinition(definition);
        var start = await harness.Runtime.StartOrGetAsync<string, TestState>(
            "dr-ac-032-nested-loser",
            definitionId,
            DefinitionVersion.Initial,
            "nested-race",
            TestContext.Current.CancellationToken);

        (await SnapshotAsync(harness.Store, start.InstanceId)).ActiveWaits.Should().HaveCount(2);
        await harness.Runtime.RaiseEventAsync(
            start.InstanceId,
            "A",
            new CorrelationId("a"),
            new { Winner = true },
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = await SnapshotAsync(harness.Store, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        (await FinalStateAsync(harness, start.InstanceId)).Log
            .Should().Equal("winner", "after-winner");
        var events = await harness.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitCancelledEvent>().Should().ContainSingle();
    }
}
