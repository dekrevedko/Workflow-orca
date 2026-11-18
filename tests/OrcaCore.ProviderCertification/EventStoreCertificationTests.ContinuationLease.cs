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
    // Concrete fixtures opt into the ID-only disposition behavior of the current stores.
    public sealed class ExpiredOwnerDispositions : TheoryData<bool, bool, OutboxRecordState>
    {
        public ExpiredOwnerDispositions()
        {
            Add(true, true, OutboxRecordState.Retryable);
            Add(false, true, OutboxRecordState.Dispatched);
            Add(true, false, OutboxRecordState.Retryable);
            Add(false, false, OutboxRecordState.Retryable);
        }
    }

    protected async Task CheckExpiredOwnerDisposition(
        bool retry, bool afterDispatch, OutboxRecordState expectedState)
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
        ProviderCommitBatch CompletePosition() => new()
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
            (await fixture.EventStore.AppendAsync(CompletePosition(), token)).IsSuccess.Should().BeTrue();
            await store.MarkAsync(second.OutboxRecordId, OutboxRecordState.Dispatched, token);
        }

        if (afterDispatch)
        {
            await FinishSecondClaim();
        }
        if (retry)
        {
            await store.MarkAsync(first.OutboxRecordId, OutboxRecordState.Retryable, token);
        }
        else
        {
            await store.ReleaseAsync(first.OutboxRecordId, token);
        }

        (await store.GetStateAsync(id, token)).Value.Should().Be(expectedState);
        var probe = await store.ClaimAsync(LeaseRequest(17), token);
        if (expectedState == OutboxRecordState.Retryable)
        {
            var third = probe.Should().ContainSingle().Which;
            third.OutboxRecordId.Should().Be(id);
            third.DispatchAttempt.Should().Be(3, "the poll is inside the replacement lease");
        }
        else
        {
            probe.Should().BeEmpty();
        }

        if (!afterDispatch)
        {
            // Attempt 3 can read the same position before attempt 2 appends.
            var competingAppend = CompletePosition();
            await FinishSecondClaim();
            var stale = await fixture.EventStore.AppendAsync(competingAppend, token);
            stale.IsFailure.Should().BeTrue();
            stale.Error.Message.Should().Contain("expected version");
        }
        else if (probe.Count > 0)
        {
            await store.MarkAsync(probe.Single().OutboxRecordId, OutboxRecordState.Dispatched, token);
        }
        (await store.GetStateAsync(id, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        var tail = await fixture.EventStore.LoadTailAsync(stream, startVersion, token);
        tail.Should().ContainSingle().Which.Should().BeOfType<WorkflowStepCompletedEvent>();
    }

    protected async Task CheckReopenedSignalIsDrainedByReplacementPump()
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

        var processor = new DurableCommandProcessor(fixture.EventStore);
        var replacement = Host(fixture, new DurableDriverBudget(20, TimeSpan.FromSeconds(30)), definition);
        var pump = new DurableContinuationPump(fixture.OutboxStore, replacement, processor);
        var request = LeaseRequest(15) with { MaxCount = 100 };
        (await pump.PumpOnceAsync(request, token)).Should().Be(expiredClaims.Count);
        var completed = await fixture.EventStore.LoadTailAsync(stream, StreamVersion.Empty, token);
        completed.OfType<WorkflowCompletedEvent>().Should().ContainSingle();

        // Drain signals created by the drive to isolate the subsequent poll of A's record.
        await pump.PumpOnceAsync(request, token);
        (await pump.PumpOnceAsync(request, token)).Should().Be(0);
        var staleId = expiredClaims[0].OutboxRecordId;
        (await fixture.OutboxStore.GetStateAsync(staleId, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        await fixture.OutboxStore.MarkAsync(staleId, OutboxRecordState.Retryable, token);
        (await pump.PumpOnceAsync(request, token)).Should().Be(1);
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
