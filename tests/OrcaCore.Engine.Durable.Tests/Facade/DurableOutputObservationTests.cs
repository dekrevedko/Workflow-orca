using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableOutputObservationTests : IDisposable
{
    // Only used to bound a deadlock. All workflow and observation time advances explicitly.
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private readonly ObservationClock clock;
    private readonly ServiceProvider storage = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider().BuildServiceProvider();
    private readonly WorkflowEventContract approval = WorkflowEventContract.Create(EventName.Create("approval"), EventContractVersion.Initial);
    private readonly CorrelationId correlation = CorrelationId.Create("remote-approval");
    private readonly ObservationReads reads;
    private readonly ServiceProvider observer;
    private readonly ServiceProvider writer;
    private readonly CancellationToken token = TestContext.Current.CancellationToken;

    public DurableOutputObservationTests()
    {
        var projections = storage.GetRequiredService<IWorkflowProjectionStore>();
        reads = new ObservationReads(projections);
        clock = new ObservationClock(() => reads.Count);
        observer = CreateHost(reads);
        writer = CreateHost(projections);
    }

    [Fact]
    public async Task WaitForOutputObservesCompletionCommittedOnlyOnAnotherHostWithinOneSecond()
    {
        var (local, remote) = await StartWaitingAsync();
        using var cleanup = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            var readsBeforeWait = reads.Count;
            var waiting = local.WaitForOutputAsync(cleanup.Token).AsTask();
            // The in-memory projection completes synchronously. The public waiter has read Waiting
            // and yielded before the other host is allowed to commit.
            Assert.Equal(readsBeforeWait + 1, reads.Count);
            Assert.Equal(WorkflowInstanceStatus.Waiting, reads.LastStatus);
            Assert.False(waiting.IsCompleted);
            await ApproveAsync(writer, remote.InstanceId);
            var committed = Assert.IsType<WorkflowOutputResult<int>.Available>(await remote.GetOutputAsync(token));
            Assert.Equal(42, committed.Output);

            clock.Advance(Interval);
            Assert.Equal(42, await waiting.WaitAsync(Watchdog, token));
        }
        finally
        {
            await cleanup.CancelAsync();
        }
    }

    [Fact]
    public async Task LocalCommitDuringStaleReadWakesImmediately()
    {
        var (local, _) = await StartWaitingAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Capture the first Waiting snapshot and return it only after local ingress commits.
        reads.OnNextRead = ct => release.Task.WaitAsync(ct);
        var waiting = local.WaitForOutputAsync(token).AsTask();
        Assert.Null(reads.OnNextRead);
        Assert.False(waiting.IsCompleted);
        try
        {
            await ApproveAsync(observer, local.InstanceId);
            var beforeRelease = reads.Count;
            release.SetResult();
            Assert.Equal(42, await waiting.WaitAsync(Watchdog, token));
            Assert.Equal(beforeRelease + 1, reads.Count);
            Assert.Equal(DateTimeOffset.UnixEpoch, clock.GetUtcNow());
            // A second, already-terminal wait needs only the authoritative query.
            Assert.Equal(42, await local.WaitForOutputAsync(token));
            Assert.Equal(beforeRelease + 2, reads.Count);
            clock.Advance(Interval);
            Assert.Equal(0, clock.Fired);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task SeveralLocalCommitsDuringOnePendingReadCauseOnlyOneEarlyRecheck()
    {
        var (local, remote) = await StartWaitingAsync(approvals: 3);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reads.OnNextRead = ct => release.Task.WaitAsync(ct);
        var waiting = local.WaitForOutputAsync(token).AsTask();
        Assert.Null(reads.OnNextRead);
        try
        {
            await ApproveAsync(observer, local.InstanceId, "first");
            await ApproveAsync(observer, local.InstanceId, "second");
            Assert.Equal(WorkflowInstanceStatus.Waiting, (await remote.GetSnapshotAsync(token)).Status);
            var beforeRelease = reads.Count;
            release.SetResult();
            await clock.ExpectArmAsync(token, afterReads: beforeRelease + 1);
            Assert.Equal(beforeRelease + 1, reads.Count);
            Assert.Equal(0, clock.Fired);

            await ApproveAsync(writer, remote.InstanceId, "third");
            clock.Advance(Interval);
            Assert.Equal(42, await waiting.WaitAsync(Watchdog, token));
            Assert.Equal(beforeRelease + 2, reads.Count);
            Assert.Equal(1, clock.Fired);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task SlowPendingReadAccruesNoTicksAndRearmsFromReadCompletion()
    {
        var (local, remote) = await StartWaitingAsync();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var waiting = local.WaitForOutputAsync(caller.Token).AsTask();
        await clock.ExpectArmAsync(token);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reads.OnNextRead = ct =>
        {
            captured.SetResult();
            return release.Task.WaitAsync(ct);
        };

        try
        {
            var initialReads = reads.Count;
            clock.Advance(Interval - TimeSpan.FromTicks(1));
            Assert.Equal(initialReads, reads.Count);
            clock.Advance(TimeSpan.FromTicks(1));
            await captured.Task.WaitAsync(Watchdog, token);
            clock.Advance(Interval * 8);
            Assert.Equal(initialReads + 1, reads.Count);
            Assert.Equal(1, clock.Fired);

            release.SetResult();
            await clock.ExpectArmAsync(token);
            Assert.Equal(initialReads + 1, reads.Count);
            clock.Advance(Interval - TimeSpan.FromTicks(1));
            Assert.Equal(initialReads + 1, reads.Count);
            clock.Advance(TimeSpan.FromTicks(1));
            await clock.ExpectArmAsync(token);
            Assert.Equal(initialReads + 2, reads.Count);

            await caller.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Watchdog, token));
            clock.Advance(Interval * 8);
            Assert.Equal(initialReads + 2, reads.Count);
            Assert.Equal(2, clock.Fired);
            Assert.Equal(WorkflowInstanceStatus.Waiting, (await remote.GetSnapshotAsync(token)).Status);
        }
        finally
        {
            release.TrySetResult();
            await caller.CancelAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteTerminationAndSubsequentPurgeSurfaceTypedOutcomes(bool purge)
    {
        var (local, remote) = await StartWaitingAsync();
        var waiting = local.WaitForOutputAsync(token).AsTask();
        Assert.False(waiting.IsCompleted);
        Assert.Equal(WorkflowTerminationStatus.Terminated, await remote.TerminateAsync(token));
        if (purge)
        {
            // Retention requires the writer to settle its durable start continuation first.
            await writer.GetRequiredService<DurableContinuationPump>()
                .PumpOnceAsync(new OutboxClaimRequest(16, clock.GetUtcNow(), Interval), token);
            var result = await storage.GetRequiredService<IWorkflowProviderMaintenanceStore>()
                .PurgeForMaintenanceAsync(new WorkflowProviderMaintenanceRequest(remote.InstanceId, clock.GetUtcNow()), token);
            Assert.Equal(new WorkflowProviderMaintenanceResult(WorkflowProviderMaintenanceDisposition.Purged), result);
        }

        clock.Advance(Interval);
        if (purge)
        {
            var error = await Assert.ThrowsAsync<WorkflowInstanceNotFoundException>(() => waiting.WaitAsync(Watchdog, token));
            Assert.Equal(local.InstanceId, error.InstanceId);
        }
        else
        {
            var error = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(() => waiting.WaitAsync(Watchdog, token));
            Assert.Equal(WorkflowInstanceStatus.Terminated, error.Status);
        }

        var finalReads = reads.Count;
        clock.Advance(Interval * 3);
        Assert.Equal(finalReads, reads.Count);
        Assert.Equal(1, clock.Fired);
    }

    [Fact]
    public async Task EachCallerRechecksOncePerIntervalAndCancellationOnlyRemovesThatWaiter()
    {
        var (local, remote) = await StartWaitingAsync();
        var initialReads = reads.Count;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var first = local.WaitForOutputAsync(caller.Token).AsTask();
        var second = local.WaitForOutputAsync(token).AsTask();
        await clock.ExpectArmAsync(token);
        await clock.ExpectArmAsync(token);
        Assert.Equal(initialReads + 2, reads.Count);
        clock.Advance(Interval);
        await clock.ExpectArmAsync(token);
        await clock.ExpectArmAsync(token);
        Assert.Equal(initialReads + 4, reads.Count);

        await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Watchdog, token));
        Assert.False(second.IsCompleted);
        Assert.Equal(WorkflowInstanceStatus.Waiting, (await remote.GetSnapshotAsync(token)).Status);
        await ApproveAsync(writer, remote.InstanceId);
        clock.Advance(Interval);
        Assert.Equal(42, await second.WaitAsync(Watchdog, token));
        clock.Advance(Interval * 3);
        Assert.Equal(initialReads + 5, reads.Count);
        Assert.Equal(3, clock.Fired);
    }

    [Fact]
    public async Task QueryFailureEndsTheWaitAndCallerCanRetry()
    {
        var (local, remote) = await StartWaitingAsync();
        var failure = new IOException("Projection read interrupted.");
        var waiting = local.WaitForOutputAsync(token).AsTask();
        reads.OnNextRead = _ => Task.FromException(failure);
        clock.Advance(Interval);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => waiting.WaitAsync(Watchdog, token)));
        var finalReads = reads.Count;
        clock.Advance(Interval * 3);
        Assert.Equal(finalReads, reads.Count);
        Assert.Equal(1, clock.Fired);
        await ApproveAsync(writer, remote.InstanceId);
        Assert.Equal(42, await local.WaitForOutputAsync(token));
    }

    public void Dispose()
    {
        observer.Dispose();
        writer.Dispose();
        storage.Dispose();
    }

    private async Task<(WorkflowInstanceHandle<int> Local, WorkflowInstanceHandle<int> Remote)> StartWaitingAsync(int approvals = 1)
    {
        var builder = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(input => input);
        for (var index = 0; index < approvals; index++)
        {
            builder.Wait(approval, _ => correlation);
        }
        var definition = builder.End(state => state.Value + 1).Build();
        var remoteDefinition = writer.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition).GetHandleOrThrow();
        var remote = (await remoteDefinition.StartOrGetAsync(41, StartIdempotencyKey.Create("remote"), token))
            .GetHandleOrThrow();
        var localDefinition = observer.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition).GetHandleOrThrow();
        return (await localDefinition.GetInstanceAsync(remote.InstanceId, token), remote);
    }

    private async Task ApproveAsync(ServiceProvider host, InstanceId id, string eventId = "approved")
    {
        var message = WorkflowInboundEvent.Create(approval, EventId.Create(eventId), correlation,
            null, clock.GetUtcNow(), new WorkflowEventRoute.Direct(id));
        Assert.IsType<WorkflowEventAcceptanceResult.Accepted>(
            await host.GetRequiredService<IWorkflowEventIngress>().AcceptAsync(message, token));
    }

    private ServiceProvider CreateHost(IWorkflowProjectionStore projections)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddSingleton(storage.GetRequiredService<IWorkflowEventStore>());
        services.AddSingleton(storage.GetRequiredService<IWorkflowInboxStore>());
        services.AddSingleton(storage.GetRequiredService<IWorkflowOutboxStore>());
        services.AddSingleton(storage.GetRequiredService<IWorkflowStartIdempotencyStore>());
        services.AddSingleton(projections);
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
                { MaxConcurrentExecutionPathsPerInstance = 1, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions
                { PartitionId = ResourceGovernancePartitionId.Create("observation"), Pools = [] }
        });
        return services.BuildServiceProvider();
    }

    private sealed class ObservationClock(Func<int> readCount) : TimeProvider
    {
        private readonly Clock clock = new(DateTimeOffset.UnixEpoch);
        private readonly Channel<(TimeSpan Due, TimeSpan Period, int Reads)> armed =
            Channel.CreateUnbounded<(TimeSpan, TimeSpan, int)>();
        private int fired;
        internal int Fired => Volatile.Read(ref fired);
        internal void Advance(TimeSpan by) => clock.Advance(by);
        public override DateTimeOffset GetUtcNow() => clock.Now;
        public override long GetTimestamp() => clock.TimeProvider.GetTimestamp();
        public override long TimestampFrequency => clock.TimeProvider.TimestampFrequency;

        internal async Task ExpectArmAsync(CancellationToken token, int afterReads = 0)
        {
            while (true)
            {
                var timer = await armed.Reader.ReadAsync(token).AsTask().WaitAsync(Watchdog, token);
                Assert.Equal((Interval, Timeout.InfiniteTimeSpan), (timer.Due, timer.Period));
                if (timer.Reads >= afterReads) return;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = clock.TimeProvider.CreateTimer(timerState =>
            {
                Interlocked.Increment(ref fired);
                callback(timerState);
            }, state, dueTime, period);
            armed.Writer.TryWrite((dueTime, period, readCount()));
            return timer;
        }
    }

    private sealed class ObservationReads(IWorkflowProjectionStore store) : IWorkflowProjectionStore
    {
        private int count;
        private Func<CancellationToken, Task>? onNextRead;
        internal int Count => Volatile.Read(ref count);
        internal WorkflowInstanceStatus? LastStatus { get; private set; }
        internal Func<CancellationToken, Task>? OnNextRead
        {
            get => Volatile.Read(ref onNextRead);
            set => Volatile.Write(ref onNextRead, value);
        }

        public async Task<Option<WorkflowProjectionSnapshot>> GetAsync(InstanceId id, CancellationToken token)
        {
            var result = await store.GetAsync(id, token);
            Interlocked.Increment(ref count);
            LastStatus = result.HasValue ? result.Value.Status : null;
            if (Interlocked.Exchange(ref onNextRead, null) is { } hook)
            {
                await hook(token);
            }
            return result;
        }

        public Task ApplyAsync(IReadOnlyList<ProjectionWrite> writes, CancellationToken token)
            => store.ApplyAsync(writes, token);

        public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
            DefinitionId? definitionId, EventName name, CorrelationId correlation, CancellationToken token)
            => store.FindActiveWaitsAsync(definitionId, name, correlation, token);

        public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(CancellationToken token)
            => store.ListLeaseRecoveryCandidatesAsync(token);
    }
}
