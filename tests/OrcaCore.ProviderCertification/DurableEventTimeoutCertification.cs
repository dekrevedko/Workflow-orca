using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using Xunit;
using DurableEvent = OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.ProviderCertification;

public static class DurableEventTimeoutCertification
{
    public enum Schedule { TimeoutBeforeMatch, TimerThenReplay, ReplayThenTimer, ReplayWithPendingEvent }

    public sealed class Schedules : TheoryData<Schedule>
    {
        public Schedules() => AddRange(Enum.GetValues<Schedule>());
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch.AddYears(60);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public static async Task RunAsync(IProviderCertificationFixture fixture, ITimerScheduler scheduler, Schedule schedule)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var processor = new DurableCommandProcessor(fixture.EventStore);
        var loader = new DurableAggregateLoader(fixture.EventStore);
        var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(), clock,
            projectionStore: fixture.ProjectionStore);
        var ingress = new DurableWorkflowEventIngressCore(runtime, fixture.ProjectionStore, fixture.InboxStore);
        var contract = WorkflowEventContract.Create(EventName.Create("approval"), new EventContractVersion(2));
        var correlation = CorrelationId.Create("order-1");
        var timeout = TimeSpan.FromMinutes(1);
        var definition = Workflow.Durable<string>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => input)
            .Wait(contract, _ => correlation, timeout)
            .Wait(contract, _ => correlation, timeout)
            .End().Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, string>("order", definition.DefinitionId,
            definition.DefinitionVersion, "input", token);
        var id = started.InstanceId;
        var stream = new WorkflowStreamId(id);
        Task<IReadOnlyList<DurableEvent>> History() => fixture.EventStore.LoadTailAsync(stream, StreamVersion.Empty, token);
        WorkflowInboundEvent Envelope() => WorkflowInboundEvent.Create(contract, EventId.Create(Guid.NewGuid().ToString()),
            correlation, null, clock.Now, new WorkflowEventRoute.Direct(id));

        async Task StageReceived(WorkflowInboundEvent inbound)
        {
            // Stop between ingress's durable acceptance and its optional inline match.
            var normalized = DurableInboundEventNormalizer.Normalize(inbound);
            var result = await fixture.InboxStore.AcceptAsync(
                new InboxAcceptance(normalized.Envelope, normalized.Fingerprint, clock.Now), token);
            result.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);
            (await fixture.InboxStore.GetByEventIdAsync(inbound.EventId, token)).Value.State.Should().Be(InboxRecordState.Received);
        }

        async Task<FireTimerCommand> ClaimTimeout(WorkflowWaitRegisteredEvent wait)
        {
            var scheduled = (await History()).OfType<WorkflowTimerScheduledEvent>()
                .Single(timer => timer.TimerId == wait.TimeoutTimerId);
            clock.Now = scheduled.FireAt;
            var due = await scheduler.ClaimDueAsync(new TimerClaimRequest(clock.Now, 10, clock.Now, timeout), token);
            var fire = due.Should().ContainSingle().Which;
            fire.TimerId.Should().Be(scheduled.TimerId);
            fire.InstanceId.Should().Be(id);
            return fire;
        }

        async Task Dispatch(FireTimerCommand fire, DurableCommandOutcome expected)
        {
            var worker = new DurableCommandProcessor(fixture.EventStore);
            (await worker.ProcessAsync(fire, token)).Outcome.Should().Be(expected);
            await scheduler.CompleteAsync(fire.TimerId, token);
        }

        async Task<WorkflowProjectionSnapshot> AssertRetired(WorkflowInstanceStatus status)
        {
            var snapshot = (await fixture.ProjectionStore.GetAsync(id, token)).Value;
            snapshot.Status.Should().Be(status);
            snapshot.ActiveWaits.Should().BeEmpty();
            (await fixture.ProjectionStore.FindActiveWaitsAsync(definition.DefinitionId, contract.EventName,
                contract.Version, correlation, token)).Should().BeEmpty();
            var aggregate = await loader.LoadAsync(id, token);
            aggregate.WaitState.ActiveWaits.Should().BeEmpty();
            aggregate.TimerState.ActiveTimers.Should().BeEmpty();
            return snapshot;
        }

        async Task AssertTimeout(WorkflowWaitRegisteredEvent wait, FireTimerCommand fire)
        {
            await Dispatch(fire, DurableCommandOutcome.Committed);
            await runtime.DriveAsync(id, token);
            var journal = await History();
            var fired = journal.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle().Which;
            fired.TimerId.Should().Be(wait.TimeoutTimerId!.Value);
            var cancelled = journal.OfType<WorkflowWaitCancelledEvent>().Should().ContainSingle().Which;
            cancelled.WaitId.Should().Be(wait.WaitId);
            cancelled.CommandId.Should().Be(fired.CommandId);
            var snapshot = await AssertRetired(WorkflowInstanceStatus.Failed);
            snapshot.ErrorSummary.Should().Contain("WF-WAIT-TIMEOUT");
        }

        var a = (await History()).OfType<WorkflowWaitRegisteredEvent>().Single();
        var oldFire = await ClaimTimeout(a);
        var e1 = Envelope();
        if (schedule == Schedule.TimeoutBeforeMatch)
        {
            await StageReceived(e1);
            await AssertTimeout(a, oldFire);
            (await fixture.InboxStore.GetByEventIdAsync(e1.EventId, token)).Value.State.Should().Be(InboxRecordState.Poisoned);
            (await ingress.AcceptAsync(e1, token)).Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
            var terminal = await History();
            terminal.OfType<WorkflowWaitRegisteredEvent>().Should().ContainSingle();
            terminal.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty();
            return;
        }

        // Capture the other possible loser: a worker that already decided on version A.
        var before = await loader.LoadAsync(id, token);
        var staleBatch = new DurableCommitMaterializer().CreateBatch(id, before.StreamVersion,
            before.DecideTimerFired(oldFire), before, inboxDelivery: null);

        (await ingress.AcceptAsync(e1, token)).Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        var baseline = await History();
        var waits = baseline.OfType<WorkflowWaitRegisteredEvent>().ToArray();
        waits.Should().HaveCount(2);
        var b = waits[1];
        b.WaitId.Should().NotBe(a.WaitId);
        b.TimeoutTimerId.Should().NotBeNull().And.NotBe(a.TimeoutTimerId);
        (b.EventName, b.EventContractVersion, b.CorrelationId).Should().Be((a.EventName, a.EventContractVersion, a.CorrelationId));
        var match = baseline.OfType<WorkflowWaitMatchedEvent>().Single();
        match.WaitId.Should().Be(a.WaitId);
        match.MatchedEventId.Should().Be(e1.EventId);
        var cancelledTimer = baseline.OfType<WorkflowTimerCancelledEvent>().Single();
        cancelledTimer.TimerId.Should().Be(oldFire.TimerId);
        cancelledTimer.CommandId.Should().Be(match.CommandId);
        baseline.OfType<WorkflowResumeConsumedEvent>().Single().WaitId.Should().Be(a.WaitId);
        var e2 = Envelope();

        async Task AssertBWaiting()
        {
            (await History()).Select(e => e.EventId).Should().Equal(baseline.Select(e => e.EventId));
            var projection = (await fixture.ProjectionStore.GetAsync(id, token)).Value;
            projection.Status.Should().Be(WorkflowInstanceStatus.Waiting);
            projection.ActiveWaits.Should().ContainSingle().Which.WaitId.Should().Be(b.WaitId);
            var aggregate = await loader.LoadAsync(id, token);
            aggregate.TimerState.ActiveTimers.Should().ContainSingle().Which.TimerId.Should().Be(b.TimeoutTimerId!.Value);
            (await fixture.InboxStore.GetByEventIdAsync(e1.EventId, token)).Value.State.Should().Be(InboxRecordState.Applied);
        }

        var staleCommit = await fixture.EventStore.AppendAsync(staleBatch, token);
        staleCommit.IsFailure.Should().BeTrue();
        staleCommit.Error.Message.Should().Be(EventStoreConflict.ExpectedVersionMismatch(
            staleBatch.ExpectedVersion, new StreamVersion(baseline.Count)).Error.Message);
        await AssertBWaiting();

        if (schedule == Schedule.ReplayThenTimer)
        {
            (await ingress.AcceptAsync(e1, token)).Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
            await AssertBWaiting();
        }
        await Dispatch(oldFire, DurableCommandOutcome.NoOp);
        await AssertBWaiting();
        if (schedule == Schedule.ReplayThenTimer)
        {
            await AssertTimeout(b, await ClaimTimeout(b));
            (await History()).OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle().Which.MatchedEventId.Should().Be(e1.EventId);
            (await fixture.InboxStore.GetByEventIdAsync(e1.EventId, token)).Value.State.Should().Be(InboxRecordState.Applied);
            return;
        }

        if (schedule == Schedule.ReplayWithPendingEvent)
        {
            await StageReceived(e2);
            await AssertBWaiting();
        }
        (await ingress.AcceptAsync(e1, token)).Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
        if (schedule == Schedule.TimerThenReplay)
        {
            await AssertBWaiting();
            (await ingress.AcceptAsync(e2, token)).Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        }

        var completed = await History();
        completed.OfType<WorkflowWaitMatchedEvent>().Select(m => (m.WaitId, m.MatchedEventId))
            .Should().Equal((a.WaitId, e1.EventId), (b.WaitId, e2.EventId));
        completed.OfType<WorkflowResumeConsumedEvent>().Select(r => r.WaitId).Should().Equal(a.WaitId, b.WaitId);
        completed.OfType<WorkflowTimerCancelledEvent>().Select(t => t.TimerId)
            .Should().Equal(oldFire.TimerId, b.TimeoutTimerId!.Value);
        completed.OfType<WorkflowTimerFiredEvent>().Should().BeEmpty();
        completed.OfType<WorkflowCompletedEvent>().Should().ContainSingle();
        await AssertRetired(WorkflowInstanceStatus.Completed);
        (await fixture.InboxStore.GetByEventIdAsync(e1.EventId, token)).Value.State.Should().Be(InboxRecordState.Applied);
        (await fixture.InboxStore.GetByEventIdAsync(e2.EventId, token)).Value.State.Should().Be(InboxRecordState.Applied);
    }
}
