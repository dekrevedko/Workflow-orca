using System.Collections.Concurrent;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

/// <summary>
/// DR-P2 gate: the lane host advances instances through the restart-safe continuation signal
/// (outbox <c>continue</c> kind + continuation pump) on the in-memory provider. Crashes are
/// simulated by never driving on the committing host; a second host's pump must finish the
/// work exactly once.
/// </summary>
public sealed class DurableDriverHostAcceptanceTests
{
    private static readonly DateTimeOffset TestEpoch = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private sealed class OrderState
    {
        public string OrderId { get; set; } = string.Empty;

        public List<string> Log { get; set; } = [];
    }

    private sealed record HostHandle(
        DurableWorkflowRuntime Runtime,
        InMemoryWorkflowProvider Store,
        DurableCommandProcessor Processor,
        DurableContinuationPump Pump,
        Clock Clock);

    private static HostHandle CreateHost(
        InMemoryWorkflowProvider? store = null,
        Clock? clock = null,
        DurableDriverBudget? budget = null,
        IResourcePoolStore? resourcePoolStore = null)
    {
        clock ??= new Clock(TestEpoch);
        store ??= new InMemoryWorkflowProvider(clock.TimeProvider);
        var processor = new DurableCommandProcessor(store, resourcePoolStore);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            new JsonWorkflowPayloadSerializer(),
            budget);
        var pump = new DurableContinuationPump(store, runtime, processor, clock.TimeProvider);
        return new HostHandle(runtime, store, processor, pump, clock);
    }

    private static Task<int> PumpOnceAsync(HostHandle host)
    {
        return host.Pump.PumpOnceAsync(
            new OutboxClaimRequest(200, host.Clock.Now, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
    }

    private static async Task<WorkflowInstanceSnapshot> SnapshotAsync(HostHandle host, InstanceId instanceId)
    {
        var snapshots = await host.Store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);
        return snapshots.Should().ContainSingle().Subject;
    }

    private static async Task<int> StepCompletionCountAsync(HostHandle host, InstanceId instanceId)
    {
        var tail = await host.Store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        return tail.OfType<WorkflowStepCompletedEvent>().Count();
    }

    private static Task<DurableCommandResult> DeliverEventWithoutDrivingAsync(
        HostHandle host,
        InstanceId instanceId,
        string eventName,
        string correlation,
        EventId eventId)
    {
        // Kernel-level delivery: the wait-matched commit (and its continuation record) lands,
        // but no local drive follows ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â the crash window DR-AC-003 targets.
        return host.Processor.ProcessAsync(
            new DeliverEventCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = host.Clock.Now,
                Envelope = new EventEnvelope
                {
                    EventId = eventId,
                    EventName = eventName,
                    CorrelationId = CorrelationId.Create(correlation),
                    OccurredAt = host.Clock.Now
                }
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task FireDueTimersAsync(HostHandle host)
    {
        var due = await host.Store.ClaimDueAsync(
            new TimerClaimRequest(host.Clock.Now, 10, host.Clock.Now, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
        foreach (var command in due)
        {
            await host.Processor.ProcessAsync(command, TestContext.Current.CancellationToken);
            await host.Store.CompleteAsync(command.TimerId, TestContext.Current.CancellationToken);
        }
    }

    private sealed class CountingStep(string name) : IStep<OrderState>
    {
        internal static readonly ConcurrentDictionary<string, int> Executions = new();

        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            Executions.AddOrUpdate($"{context.State.OrderId}:{name}", 1, (_, count) => count + 1);
            context.State.Log.Add(name);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }

        internal static int CountFor(string orderId, string name)
        {
            return Executions.GetValueOrDefault($"{orderId}:{name}");
        }
    }

    private static WorkflowDefinition<OrderState> StepWaitStepEndDefinition(
        DefinitionId definitionId,
        DefinitionVersion version)
    {
        return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, version)
            .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
            .Then(() => new CountingStep("prepare"))
            .Wait("Approved", state => CorrelationId.Create(state.OrderId))
            .Then(() => new CountingStep("ship"))
            .End("shipped")
            .Build();
    }

    [Fact]
    [Trait("AC", "DR-AC-003")]
    public async Task HostKilledAfterWaitMatchedCommit_SecondHostPumpCompletesInstance()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        var hostA = CreateHost(store, clock);
        hostA.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-003",
            definitionId,
            DefinitionVersion.Initial,
            "order-h3",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        var delivered = await DeliverEventWithoutDrivingAsync(
            hostA, start.InstanceId, "Approved", "order-h3", EventId.Create(Guid.CreateVersion7().ToString()));
        delivered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().NotBe(
            WorkflowStatus.Completed, "host A died before continuing");

        var hostB = CreateHost(store, clock);
        hostB.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        var processed = await PumpOnceAsync(hostB);

        processed.Should().BeGreaterThan(0, "the continuation pump must claim the pending signal");
        var completed = await SnapshotAsync(hostB, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.EndOutcomeName.Should().Be("shipped");
        CountingStep.CountFor("order-h3", "ship").Should().Be(1);
    }

    [Fact]
    [Trait("AC", "DR-AC-004")]
    public async Task DuplicateEventAndDuplicateClaim_ExactlyOneStepExecutionCommits()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        var hostA = CreateHost(store, clock);
        hostA.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-004",
            definitionId,
            DefinitionVersion.Initial,
            "order-h4",
            TestContext.Current.CancellationToken);

        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
        var first = await DeliverEventWithoutDrivingAsync(hostA, start.InstanceId, "Approved", "order-h4", eventId);
        first.Outcome.Should().Be(DurableCommandOutcome.Committed);

        // A first pump claims the continuation and dies before advancing; its lease expires.
        var preClaims = await store.ClaimAsync(
            new OutboxClaimRequest(10, clock.Now, TimeSpan.FromSeconds(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);
        preClaims.Should().NotBeEmpty();
        clock.Advance(TimeSpan.FromSeconds(2));

        // The same event is redelivered while the continuation is still pending.
        var duplicate = await DeliverEventWithoutDrivingAsync(hostA, start.InstanceId, "Approved", "order-h4", eventId);
        duplicate.Outcome.Should().Be(
            DurableCommandOutcome.NoOp, "durable inbox dedup rejects the redelivery");

        var hostB = CreateHost(store, clock);
        hostB.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        await PumpOnceAsync(hostB);

        (await SnapshotAsync(hostB, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
        CountingStep.CountFor("order-h4", "ship").Should().Be(1, "inbox dedup + DR-034 idempotence");
        var tail = await hostB.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId), StreamVersion.Empty, TestContext.Current.CancellationToken);
        tail.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle("the wait matched exactly once");
    }

    [Fact]
    [Trait("AC", "DR-AC-010")]
    public async Task TwoLaneHostsOneStore_HundredInstances_AllCompleteWithoutDuplicateCommits()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
                .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
                .Then(() => new CountingStep("s1"))
                .Then(() => new CountingStep("s2"))
                .Then(() => new CountingStep("s3"))
                .Then(() => new CountingStep("s4"))
                .End("done")
                .Build();
        }

        // A small budget forces every instance through multiple continuation cycles, so both
        // hosts' pumps race on claims and on stream versions (DR-030/035).
        var budget = new DurableDriverBudget(2, TimeSpan.FromSeconds(30));
        var hostA = CreateHost(store, clock, budget);
        var hostB = CreateHost(store, clock, budget);
        hostA.Runtime.RegisterDefinition(Definition());
        hostB.Runtime.RegisterDefinition(Definition());

        var instanceIds = new List<InstanceId>();
        for (var index = 0; index < 100; index++)
        {
            var host = index % 2 == 0 ? hostA : hostB;
            var start = await host.Runtime.StartOrGetAsync<string, OrderState>(
                $"order-dr-ac-010-{index}",
                definitionId,
                DefinitionVersion.Initial,
                $"order-h10-{index}",
                TestContext.Current.CancellationToken);
            instanceIds.Add(start.InstanceId);
        }

        for (var cycle = 0; cycle < 50; cycle++)
        {
            var processed = await Task.WhenAll(PumpOnceAsync(hostA), PumpOnceAsync(hostB));
            if (processed.Sum() == 0)
            {
                break;
            }
        }

        foreach (var instanceId in instanceIds)
        {
            (await SnapshotAsync(hostA, instanceId)).Status.Should().Be(WorkflowStatus.Completed);
            (await StepCompletionCountAsync(hostA, instanceId)).Should().Be(
                4, "conflicts must resolve internally without duplicated step commits");
        }

        for (var index = 0; index < 100; index++)
        {
            foreach (var step in new[] { "s1", "s2", "s3", "s4" })
            {
                CountingStep.CountFor($"order-h10-{index}", step).Should().BeGreaterThanOrEqualTo(
                    1,
                    "physical redispatch is at-least-once while the durable step transition commits once");
            }
        }
    }

    [Fact]
    [Trait("AC", "DR-AC-014")]
    public async Task CrashAfterRunnableStepCommit_NewHostClaimsContinuationAndRunsNextStepOnce()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
                .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
                .Then(() => new CountingStep("first"))
                .Then(() => new CountingStep("second"))
                .End("done")
                .Build();
        }

        // Budget 1: host A commits the first step (a runnable-leaving commit) and returns ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â
        // then "crashes" by never being used again. The continuation record is the only path
        // to the second step.
        var hostA = CreateHost(store, clock, new DurableDriverBudget(2, TimeSpan.FromSeconds(30)));
        hostA.Runtime.RegisterDefinition(Definition());
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-014",
            definitionId,
            DefinitionVersion.Initial,
            "order-h14",
            TestContext.Current.CancellationToken);

        CountingStep.CountFor("order-h14", "first").Should().Be(1);
        CountingStep.CountFor("order-h14", "second").Should().Be(0);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().NotBe(WorkflowStatus.Completed);

        var hostB = CreateHost(store, clock);
        hostB.Runtime.RegisterDefinition(Definition());
        await PumpOnceAsync(hostB);

        (await SnapshotAsync(hostB, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
        CountingStep.CountFor("order-h14", "first").Should().Be(1, "the committed step never re-executes");
        CountingStep.CountFor("order-h14", "second").Should().Be(1, "the next step runs exactly once");
    }

    [Fact]
    [Trait("AC", "DR-AC-015")]
    public async Task StaleContinuation_PumpNoOpsAndMarksProcessed()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        var hostA = CreateHost(store, clock);
        hostA.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-015",
            definitionId,
            DefinitionVersion.Initial,
            "order-h15",
            TestContext.Current.CancellationToken);
        await hostA.Runtime.RaiseEventAsync(
            start.InstanceId,
            "Approved",
            CorrelationId.Create("order-h15"),
            cancellationToken: TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);

        var eventCountBefore = (await hostA.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId), StreamVersion.Empty, TestContext.Current.CancellationToken)).Count;
        var shipCountBefore = CountingStep.CountFor("order-h15", "ship");

        // Host A advanced everything in-process, so its continuation records are stale.
        var hostB = CreateHost(store, clock);
        hostB.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        var processed = await PumpOnceAsync(hostB);
        processed.Should().BeGreaterThan(0, "stale records are claimed, no-op, and are marked processed");

        var eventCountAfter = (await hostB.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId), StreamVersion.Empty, TestContext.Current.CancellationToken)).Count;
        eventCountAfter.Should().Be(eventCountBefore, "a stale continuation performs no duplicate step execution");
        CountingStep.CountFor("order-h15", "ship").Should().Be(shipCountBefore);
        (await PumpOnceAsync(hostB)).Should().Be(0, "processed records are never claimed again");
    }

    [Fact]
    [Trait("AC", "DR-AC-017")]
    public async Task SegmentBudgetExceeded_CommitsProgressAndResumesFromPersistedPosition()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            var builder = global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
                .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" });
            for (var index = 1; index <= 6; index++)
            {
                var step = index;
                builder = builder.Then(() => new CountingStep($"step-{step}"));
            }

            return builder.End("budgeted").Build();
        }

        var host = CreateHost(store, clock, new DurableDriverBudget(2, TimeSpan.FromSeconds(30)));
        host.Runtime.RegisterDefinition(Definition());
        var start = await host.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-017",
            definitionId,
            DefinitionVersion.Initial,
            "order-h17",
            TestContext.Current.CancellationToken);

        (await SnapshotAsync(host, start.InstanceId)).Status.Should().NotBe(
            WorkflowStatus.Completed, "the segment budget ends the segment early");

        var cycles = 0;
        while ((await SnapshotAsync(host, start.InstanceId)).Status != WorkflowStatus.Completed && cycles < 20)
        {
            cycles++;
            (await PumpOnceAsync(host)).Should().BeGreaterThan(
                0, "every budget-exhausted segment leaves a fresh continuation signal");
        }

        cycles.Should().BeGreaterThan(1, "a six-step workflow with budget two needs several segments");
        (await SnapshotAsync(host, start.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
        for (var index = 1; index <= 6; index++)
        {
            CountingStep.CountFor("order-h17", $"step-{index}").Should().Be(
                1, "resuming from the persisted position never re-runs committed steps");
        }
    }

    [Fact]
    [Trait("AC", "DR-AC-019")]
    public async Task TimerResume_HostReplacedWhileTimerPending_NextStepRunsExactlyOnce()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
                .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
                .Then(() => new CountingStep("before-delay"))
                .Delay(TimeSpan.FromMinutes(5))
                .Then(() => new CountingStep("after-delay"))
                .End("timed")
                .Build();
        }

        var hostA = CreateHost(store, clock);
        hostA.Runtime.RegisterDefinition(Definition());
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-019",
            definitionId,
            DefinitionVersion.Initial,
            "order-h19",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        // Host replaced while the timer is pending; the new host's timer pump fires it.
        var hostB = CreateHost(store, clock);
        hostB.Runtime.RegisterDefinition(Definition());
        clock.Advance(TimeSpan.FromMinutes(6));
        await FireDueTimersAsync(hostB);
        await PumpOnceAsync(hostB);

        var completed = await SnapshotAsync(hostB, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        CountingStep.CountFor("order-h19", "before-delay").Should().Be(1);
        CountingStep.CountFor("order-h19", "after-delay").Should().Be(
            1, "the driver resumes the step after the fired timer exactly once");
    }

    private sealed class TimeoutObservingStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Log.Add(context.ResumedEvent is null ? "timeout" : $"event:{context.ResumedEvent.EventName}");
            CountingStep.Executions.AddOrUpdate(
                $"{context.State.OrderId}:decide", 1, (_, count) => count + 1);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed record TimeoutBranchState(string OrderId);

    [Fact]
    [Trait("AC", "DR-AC-020")]
    public async Task WaitLongTimeoutWins_ResumesTimeoutBranchOnce_LateEventIsDeduplicated()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
                .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
                .WhenFirst<string>(
                    branches => branches
                        .Branch<TimeoutBranchState>(
                            "event",
                            parent => new TimeoutBranchState(parent.Value.OrderId),
                            branch => branch
                                .Wait("Approval", state => CorrelationId.Create(state.OrderId))
                                .Return(_ => "event"))
                        .Branch<TimeoutBranchState>(
                            "timeout",
                            parent => new TimeoutBranchState(parent.Value.OrderId),
                            branch => branch
                                .Delay(TimeSpan.FromMinutes(10))
                                .Return(_ => "timeout")),
                    (parent, winner) => new OrderState
                    {
                        OrderId = parent.Value.OrderId,
                        Log = [.. parent.Value.Log, winner.Value]
                    })
                .Then(() => new CountingStep("decide"))
                .End("decided")
                .Build();
        }

        var host = CreateHost(store, clock);
        host.Runtime.RegisterDefinition(Definition());
        var start = await host.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-020",
            definitionId,
            DefinitionVersion.Initial,
            "order-h20",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(host, start.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        // The timeout fires before any event arrives: the kernel cancels the wait in the same
        // commit and the driver resumes on the timeout branch.
        clock.Advance(TimeSpan.FromMinutes(11));
        await FireDueTimersAsync(host);
        await PumpOnceAsync(host);

        var completed = await SnapshotAsync(host, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        CountingStep.Executions.GetValueOrDefault("order-h20:decide").Should().Be(1);

        // The late matching event must not double-resume anything.
        var late = await host.Runtime.RaiseEventAsync(
            start.InstanceId,
            "Approval",
            CorrelationId.Create("order-h20"),
            cancellationToken: TestContext.Current.CancellationToken);
        late.Outcome.Should().NotBe(DurableCommandOutcome.Committed, "the raced wait no longer exists");
        CountingStep.Executions.GetValueOrDefault("order-h20:decide").Should().Be(1);
        var state = await host.Store.LoadTailAsync(
            new WorkflowStreamId(start.InstanceId), StreamVersion.Empty, TestContext.Current.CancellationToken);
        state.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty("the timeout won the race");
    }

    private sealed class DispatchJobStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            CountingStep.Executions.AddOrUpdate(
                $"{context.State.OrderId}:dispatch", 1, (_, count) => count + 1);
            return ValueTask.FromResult<StepResult>(
                global::OrcaCore.TestSupport.LegacyStepResults.RunExternalJob(context.State.OrderId, [1, 2, 3]));
        }
    }

    private sealed class ObserveCompletionStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            CountingStep.Executions.AddOrUpdate(
                $"{context.State.OrderId}:observe", 1, (_, count) => count + 1);
            context.State.Log.Add(context.ResumedEvent is null ? "no-event" : "completed");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    [Trait("AC", "DR-AC-021")]
    public async Task ExternalJobCompletionAfterHostRestart_ResumesFollowingStepExactlyOnce()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
                .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
                .Then<DispatchJobStep>()
                .Then<ObserveCompletionStep>()
                .End("job-done")
                .Build();
        }

        var hostA = CreateHost(store, clock);
        hostA.Runtime.RegisterDefinition(Definition());
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-021",
            definitionId,
            DefinitionVersion.Initial,
            "order-h21",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().Be(
            WorkflowStatus.Waiting, "the instance suspends on the dispatched job's completion wait");
        CountingStep.Executions.GetValueOrDefault("order-h21:dispatch").Should().Be(1);

        // Host replaced; the completion is reported by an external watcher afterwards, and
        // the crash window between the completion commit and the continuation is bridged by
        // the second host's pump.
        var hostB = CreateHost(store, clock);
        hostB.Runtime.RegisterDefinition(Definition());
        var completion = await hostB.Processor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = start.InstanceId,
                RequestedAt = clock.Now,
                ExternalJobId = "order-h21",
                CompletionEventId = EventId.Create(Guid.CreateVersion7().ToString())
            },
            TestContext.Current.CancellationToken);
        completion.Outcome.Should().Be(DurableCommandOutcome.Committed);
        await PumpOnceAsync(hostB);

        var completed = await SnapshotAsync(hostB, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.EndOutcomeName.Should().Be("job-done");
        CountingStep.Executions.GetValueOrDefault("order-h21:dispatch").Should().Be(
            1, "the dispatching step never re-runs after its commit");
        CountingStep.Executions.GetValueOrDefault("order-h21:observe").Should().Be(
            1, "the driver resumes the following step exactly once");
    }

    private sealed class AcquirePoolStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            CountingStep.Executions.AddOrUpdate(
                $"{context.State.OrderId}:acquire", 1, (_, count) => count + 1);
            return ValueTask.FromResult<StepResult>(global::OrcaCore.TestSupport.LegacyStepResults.AcquireResources(
                context.State.OrderId,
                [new ResourcePoolRequirement("db", 1)]));
        }
    }

    [Fact]
    [Trait("AC", "DR-AC-022")]
    public async Task ResourcePoolGrantAfterRelease_ResumesGuardedHolderExactlyOnce_AcrossRestart()
    {
        var clock = new Clock(TestEpoch);
        var store = new InMemoryWorkflowProvider(clock.TimeProvider);
        var poolStore = new InMemoryResourcePoolStore();
        await poolStore.UpsertPoolAsync(
            new ResourcePoolDefinition("db", 1, null), TestContext.Current.CancellationToken);
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
                .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
                .Then<AcquirePoolStep>()
                .Then(() => new CountingStep("guarded"))
                .Wait("Release", state => CorrelationId.Create(state.OrderId))
                .End("released")
                .Build();
        }

        var hostA = CreateHost(store, clock, resourcePoolStore: poolStore);
        hostA.Runtime.RegisterDefinition(Definition());
        var holderA = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-022-a",
            definitionId,
            DefinitionVersion.Initial,
            "holder-a",
            TestContext.Current.CancellationToken);
        CountingStep.CountFor("holder-a", "guarded").Should().Be(1, "capacity one grants the first holder");

        var holderB = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-022-b",
            definitionId,
            DefinitionVersion.Initial,
            "holder-b",
            TestContext.Current.CancellationToken);
        CountingStep.CountFor("holder-b", "guarded").Should().Be(0, "the pool is exhausted, so the holder queues");
        (await SnapshotAsync(hostA, holderB.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        // The first holder completes: its tickets release and free capacity for the queued one.
        await hostA.Runtime.RaiseEventAsync(
            holderA.InstanceId,
            "Release",
            CorrelationId.Create("holder-a"),
            cancellationToken: TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, holderA.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);

        // The grant signal commits, then the host dies before continuing (restart between
        // grant and continuation).
        var grant = await DeliverEventWithoutDrivingAsync(
            hostA, holderB.InstanceId, "ResourcePoolGranted", "holder-b", EventId.Create(Guid.CreateVersion7().ToString()));
        grant.Outcome.Should().Be(DurableCommandOutcome.Committed);

        var hostB = CreateHost(store, clock, resourcePoolStore: poolStore);
        hostB.Runtime.RegisterDefinition(Definition());
        await PumpOnceAsync(hostB);

        CountingStep.Executions.GetValueOrDefault("holder-b:acquire").Should().Be(
            2, "the grant signal re-runs the guarded acquisition, which now succeeds");
        CountingStep.CountFor("holder-b", "guarded").Should().Be(
            1, "the driver resumes the guarded holder exactly once after the grant commits");
        (await SnapshotAsync(hostB, holderB.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        await hostB.Runtime.RaiseEventAsync(
            holderB.InstanceId,
            "Release",
            CorrelationId.Create("holder-b"),
            cancellationToken: TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostB, holderB.InstanceId)).Status.Should().Be(WorkflowStatus.Completed);
    }
}
