using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract partial class EventStoreCertificationTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ContinuationLease_ExpiredOwnerCannotDisposeReplacement(bool retry, bool afterDispatch)
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = CreateFixture();
        var store = fixture.OutboxStore;
        var stream = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var id = OutboxRecordId.New();
        (await fixture.EventStore.AppendAsync(KindBatch(stream, (id, OutboxKinds.Continue)), token))
            .IsSuccess.Should().BeTrue();

        var first = (await store.ClaimAsync(LeaseRequest(10), token)).Should().ContainSingle().Which;
        first.OutboxRecordId.Should().Be(id);
        first.DispatchAttempt.Should().Be(1);
        (await store.ClaimAsync(LeaseRequest(14), token)).Should().BeEmpty();
        var second = (await store.ClaimAsync(LeaseRequest(15), token)).Should().ContainSingle().Which;
        second.OutboxRecordId.Should().Be(id);
        second.DispatchAttempt.Should().Be(2);
        (await store.ClaimAsync(LeaseRequest(16), token)).Should().BeEmpty();

        var startVersion = new StreamVersion(1);
        var completion = new ProviderCommitBatch
        {
            StreamId = stream,
            ExpectedVersion = startVersion,
            Events = [new WorkflowStepCompletedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                CommandId = CommandId.New(),
                CausationId = CausationId.New(),
                InstanceId = stream.InstanceId,
                OccurredAt = Timestamp(16),
                StepPath = "continuation"
            }]
        };

        async Task FinishSecondClaim()
        {
            (await fixture.EventStore.AppendAsync(completion, token)).IsSuccess.Should().BeTrue();
            (await store.MarkAsync(second.OutboxRecordId, second.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeTrue();
        }

        if (afterDispatch)
        {
            await FinishSecondClaim();
        }
        var applied = retry
            ? await store.MarkAsync(first.OutboxRecordId, first.DispatchAttempt, OutboxRecordState.Retryable, token)
            : await store.ReleaseAsync(first.OutboxRecordId, first.DispatchAttempt, token);
        applied.Should().BeFalse();
        (await store.MarkAsync(id, first.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeFalse();
        (await store.MarkPoisonedAsync(id, first.DispatchAttempt, "stale", "old owner", token)).Should().BeFalse();

        var snapshot = (await store.GetDispatchSnapshotAsync(id, token)).Value;
        snapshot.State.Should().Be(afterDispatch ? OutboxRecordState.Dispatched : OutboxRecordState.Claimed);
        snapshot.PoisonCode.Should().BeNull();
        snapshot.PoisonDetail.Should().BeNull();
        (await store.ClaimAsync(LeaseRequest(17), token)).Should().BeEmpty();
        if (!afterDispatch)
        {
            await FinishSecondClaim();
        }

        // Even a replay by the last owner must leave a terminal row intact.
        (await store.MarkAsync(id, second.DispatchAttempt, OutboxRecordState.Retryable, token)).Should().BeFalse();
        (await store.ReleaseAsync(id, second.DispatchAttempt, token)).Should().BeFalse();
        (await store.GetStateAsync(id, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        var tail = await fixture.EventStore.LoadTailAsync(stream, startVersion, token);
        tail.Should().ContainSingle().Which.Should().BeOfType<WorkflowStepCompletedEvent>();
    }

    [Fact]
    public async Task ContinuationLease_CollidingAppendCannotResetAttemptOrPartiallyCommit()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = CreateFixture();
        var store = fixture.OutboxStore;
        var stream = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var id = OutboxRecordId.New();
        (await fixture.EventStore.AppendAsync(KindBatch(stream, (id, OutboxKinds.Continue)), token)).IsSuccess.Should().BeTrue();
        var a = (await store.ClaimAsync(LeaseRequest(10), token)).Single();
        var b = (await store.ClaimAsync(LeaseRequest(15), token)).Single();
        var fresh = OutboxRecordId.New();
        var destination = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var collision = KindBatch(destination, (fresh, OutboxKinds.Continue), (id, OutboxKinds.Continue));
        (await fixture.EventStore.AppendAsync(collision, token)).IsFailure.Should().BeTrue();
        if (fixture.EventStore is IWorkflowProviderMaintenanceStore maintenance)
        {
            (await maintenance.InspectForMaintenanceAsync(destination.InstanceId, token)).Exists.Should().BeFalse();
        }
        (await fixture.EventStore.LoadTailAsync(destination, StreamVersion.Empty, token)).Should().BeEmpty();
        (await store.GetStateAsync(fresh, token)).HasValue.Should().BeFalse();
        (await fixture.EventStore.LoadTailAsync(stream, new StreamVersion(1), token)).Should().BeEmpty();
        (await store.ReleaseAsync(id, a.DispatchAttempt, token)).Should().BeFalse();
        (await store.ClaimAsync(LeaseRequest(19), token)).Should().BeEmpty();
        var c = (await store.ClaimAsync(LeaseRequest(20), token)).Should().ContainSingle().Which;
        c.OutboxRecordId.Should().Be(id);
        c.DispatchAttempt.Should().Be(3);
        (await store.MarkPoisonedAsync(id, b.DispatchAttempt, "stale", null, token)).Should().BeFalse();
        (await store.MarkPoisonedAsync(id, c.DispatchAttempt, "invalid", "current owner", token)).Should().BeTrue();
        (await store.GetDispatchSnapshotAsync(id, token)).Value.PoisonDetail.Should().Be("current owner");
    }

    [Fact]
    public async Task ContinuationLease_LateRetryCannotReopenReplacementPumpDispatch()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = CreateFixture();
        var definition = Workflow.Durable<HostBranchState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new HostBranchState()).Then<AdvanceOnceHostStep>().End().Build();
        var starter = Host(fixture, new DurableDriverBudget(1, TimeSpan.FromSeconds(30)), definition);
        var started = await starter.StartOrGetAsync<string, HostBranchState>(
            Guid.NewGuid().ToString("N"), definition.DefinitionId, definition.DefinitionVersion, "input", token);
        var stream = new WorkflowStreamId(started.InstanceId);
        var before = await fixture.EventStore.LoadTailAsync(stream, StreamVersion.Empty, token);
        before.OfType<WorkflowCompletedEvent>().Should().BeEmpty();
        var expiredClaims = await fixture.OutboxStore.ClaimAsync(LeaseRequest(10) with { MaxCount = 100 }, token);
        expiredClaims.Should().NotBeEmpty().And.OnlyContain(record => record.DispatchAttempt == 1);

        // B's group contains reclaimed records at attempt 2 and a fresh record at attempt 1.
        var fresh = new OutboxWrite(OutboxRecordId.New(), OutboxKinds.Continue, expiredClaims[0].Payload);
        (await fixture.EventStore.AppendAsync(new ProviderCommitBatch
        {
            StreamId = stream,
            ExpectedVersion = new StreamVersion(before.Count),
            OutboxRecords = [fresh]
        }, token)).IsSuccess.Should().BeTrue();
        var processor = new DurableCommandProcessor(fixture.EventStore);
        var replacement = Host(fixture, new DurableDriverBudget(20, TimeSpan.FromSeconds(30)), definition);
        var pump = new DurableContinuationPump(fixture.OutboxStore, replacement, processor);
        var request = LeaseRequest(15) with { MaxCount = 100 };
        (await pump.PumpOnceAsync(request, token)).Should().Be(expiredClaims.Count + 1);
        (await fixture.OutboxStore.GetStateAsync(fresh.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        var completed = await fixture.EventStore.LoadTailAsync(stream, StreamVersion.Empty, token);
        completed.OfType<WorkflowCompletedEvent>().Should().ContainSingle();

        // Drain signals created by the drive to isolate the subsequent poll of A's record.
        await pump.PumpOnceAsync(request, token);
        (await pump.PumpOnceAsync(request, token)).Should().Be(0);
        var staleId = expiredClaims[0].OutboxRecordId;
        (await fixture.OutboxStore.GetStateAsync(staleId, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        (await fixture.OutboxStore.MarkAsync(staleId, expiredClaims[0].DispatchAttempt, OutboxRecordState.Retryable, token)).Should().BeFalse();
        (await pump.PumpOnceAsync(request, token)).Should().Be(0);
        (await fixture.OutboxStore.GetStateAsync(staleId, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        var after = await fixture.EventStore.LoadTailAsync(stream, StreamVersion.Empty, token);
        after.Select(e => e.EventId).Should().Equal(completed.Select(e => e.EventId));
    }

    private static OutboxClaimRequest LeaseRequest(int seconds) =>
        new(1, Timestamp(seconds), TimeSpan.FromSeconds(5))
        {
            KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
        };
}
