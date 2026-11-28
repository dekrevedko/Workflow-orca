using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed class InMemoryProviderCertificationTests : ContinueAsNewCertificationTests
{
    [Theory]
    [ClassData<DurableEventTimeoutCertification.Schedules>]
    public async Task DurableEventTimeout_ReusedKey(DurableEventTimeoutCertification.Schedule schedule)
    {
        using var provider = InMemoryProviderPorts.Create();
        await DurableEventTimeoutCertification.RunAsync(new InMemoryProviderCertificationFixture(provider), provider.TimerScheduler, schedule);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DagScheduler_CachedNoReadyRunRecoversRemoteJoinAfterLostWake(bool restartScheduler)
    {
        await using var storage = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
        var store = storage.GetRequiredService<IWorkflowEventStore>();
        await DagRunCertification.RemoteJoinAfterLostWakeAsync(
            () => new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().AddSingleton(store.GetType(), store),
            restartScheduler, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DagScheduler_ProgressesTwoRunsAndCancelsBlockedMapper() =>
        DagRunCertification.ShippedScheduler_ProgressesTwoRunsAndCancelsTheBlockedMapperAsync(
            new ServiceCollection().AddOrcaCoreInMemoryDurableProvider(), TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public Task DagRetainedChild_DoesNotStarveHealthyRun(int batchSize) =>
        DagRunCertification.RetainedChildDoesNotStarveHealthyRunAsync(
            new ServiceCollection().AddOrcaCoreInMemoryDurableProvider(), batchSize, TestContext.Current.CancellationToken);

    [Fact]
    public async Task DagCreationAndReplay_PersistAnIndependentAggregate()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.CreationAndReplayAsync(provider.EventStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagFencedAppend_AtomicallyPersistsInputsAndAddressedNotices()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.FencedAppendAndObserversAsync(provider.EventStore, provider.OutboxStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagLeaseQueue_UsesFifoAndAnAbsoluteTurnDeadline()
    {
        var clock = new DagCertificationTimeProvider();
        using var provider = InMemoryProviderPorts.Create(clock);
        await DagRunCertification.LeaseQueueAsync(provider.EventStore, clock.GetUtcNow, clock.Advance, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagDeadWaiter_ExpiresWithoutBlockingTheLiveQueue()
    {
        var clock = new DagCertificationTimeProvider();
        using var provider = InMemoryProviderPorts.Create(clock);
        await DagRunCertification.DeadWaiterExpiresAsync(provider.EventStore, clock.Advance, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagTurnPurpose_RejectsForeignRecordsBeforeStorage()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.TurnPurposeRejectsForeignRecordsAsync(provider.EventStore, provider.OutboxStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagLastNode_RequiresAtomicRunClosure()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.LastNodeRequiresAtomicRunClosureAsync(provider.EventStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagFailureClosure_PreservesIndependentWorkAndDerivesTerminalOutcome()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.FailureClosureAndTerminalAsync(provider.EventStore, TestContext.Current.CancellationToken);
    }

    private sealed class DagCertificationTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        internal Action? BeforeNextRead;
        public override DateTimeOffset GetUtcNow()
        {
            Interlocked.Exchange(ref BeforeNextRead, null)?.Invoke();
            return now;
        }
        internal void Advance(TimeSpan duration) => now += duration;
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DagFirstUsableWake_CursorAndAppendSerialize(bool cursorFirst)
    {
        var clock = new DagCertificationTimeProvider();
        using var provider = InMemoryProviderPorts.Create(clock);
        await DagRunCertification.WakeCursorRaceAsync(provider.EventStore, provider.EventStore, cursorFirst,
            clock.GetUtcNow, clock.Advance, async (_, first, second, token) =>
            {
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var release = new ManualResetEventSlim();
                // Both synchronous operations sample this clock inside the same provider monitor.
                clock.BeforeNextRead = () => { entered.TrySetResult(); release.Wait(token); };
                var held = Task.Run(first, token);
                Task? contender = null;
                try
                {
                    await entered.Task.WaitAsync(token);
                    var attempting = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
                    contender = Task.Factory.StartNew(() =>
                    {
                        attempting.SetResult(Thread.CurrentThread);
                        return second();
                    }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
                    var thread = await attempting.Task.WaitAsync(token);
                    while (!contender.IsCompleted &&
                           (thread.ThreadState & ThreadState.WaitSleepJoin) == 0)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                    contender.IsCompleted.Should().BeFalse("the contender must wait for the provider lock");
                }
                finally
                {
                    release.Set();
                    await Task.WhenAll(held, contender ?? Task.CompletedTask);
                }
            }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagFirstUsableWake_DeliveryRetainsPayloadClaimLeaseAndHistory()
    {
        var clock = new DagCertificationTimeProvider();
        using var provider = InMemoryProviderPorts.Create(clock);
        await DagRunCertification.WakeDeliveryAsync(provider.EventStore, clock.GetUtcNow, clock.Advance, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DagFirstUsableWake_CollidingCandidateRollsBack(bool previouslyStored)
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.WakeCollisionAsync(provider.EventStore, previouslyStored, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagObservers_CleanExpiredNoticesButRetainClaimsPoisonAndBusinessWork()
    {
        var clock = new DagCertificationTimeProvider();
        using var provider = InMemoryProviderPorts.Create(clock);
        await DagRunCertification.ObserverCleanupAsync(provider.EventStore, provider.OutboxStore, clock.GetUtcNow, clock.Advance, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagConcurrentLeaseRequests_AreSerialized()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.ConcurrentLeasesAsync(provider.EventStore, provider.EventStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagCancellationAndIntegrity_PreserveAdmissionsAndDeriveCancelledTerminal()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.CancellationAndIntegrityAsync(provider.EventStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagAndWorkflowIdentities_CannotOwnTheSameGuid()
    {
        using var provider = InMemoryProviderPorts.Create();
        await DagRunCertification.CrossAggregateIdentityAsync(provider.EventStore, provider.OutboxStore, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("AC", "AC-108")]
    public Task DefinitionFanoutInbox_UsesAtomicStablePerTargetOwnership() =>
        DefinitionFanoutInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public Task StartOrDeliverInbox_UsesAtomicInputBoundIntentOwnership() =>
        StartOrDeliverInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public async Task OperationalStatisticsAndMaintenance_AreProviderAuthoritativeAndReferenceSafe()
    {
        using var provider = InMemoryProviderPorts.Create();
        await OperationalMaintenanceCertification.RunAsync(
            provider.EventStore,
            provider.InboxStore,
            provider.OutboxStore,
            provider.OperationalStore,
            provider.ProjectionStore,
            provider.MaintenanceStore,
            OrcaCore.Abstractions.Diagnostics.OrcaCoreDiagnostics.InMemoryProviderName,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ActiveWaitQuery_ImplementsTheVersionAwareProviderOverload()
    {
        var projectionStore = InMemoryProviderPorts.Create().ProjectionStore;

        projectionStore.GetType().GetMethod(
                nameof(IWorkflowProjectionStore.FindActiveWaitsAsync),
                [
                    typeof(global::OrcaCore.DefinitionId),
                    typeof(global::OrcaCore.EventName),
                    typeof(global::OrcaCore.EventContractVersion),
                    typeof(global::OrcaCore.CorrelationId),
                    typeof(CancellationToken)
                ])
            .Should().NotBeNull(
                "the selected in-memory provider must filter version identity without the interface fallback");
    }

    protected override IProviderCertificationFixture CreateFixture()
    {
        return new InMemoryProviderCertificationFixture(InMemoryProviderPorts.Create());
    }

    private sealed class InMemoryProviderCertificationFixture(InMemoryProviderPorts provider)
        : IProviderCertificationFixture
    {
        public IWorkflowEventStore EventStore => provider.EventStore;

        public IWorkflowInboxStore InboxStore => provider.InboxStore;

        public IWorkflowStartIdempotencyStore StartIdempotencyStore => provider.StartIdempotencyStore;

        public IWorkflowOutboxStore OutboxStore => provider.OutboxStore;

        public IWorkflowProjectionStore ProjectionStore => provider.ProjectionStore;
    }
}
