using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableOutputObservationTests
{
    [Fact]
    public async Task WaitForOutputObservesCompletionCommittedOnlyOnAnotherHostWithinOneSecond()
    {
        var token = TestContext.Current.CancellationToken;
        using var store = new InMemoryWorkflowProvider();
        var reads = new ObservationReads(store);
        var clock = new ObservationClock();
        using var observer = CreateHost(store, reads, clock);
        using var writer = CreateHost(store, store, clock);
        var approval = WorkflowEventContract.Create(EventName.Create("approval"), EventContractVersion.Initial);
        var correlation = CorrelationId.Create("remote-approval");
        var definition = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input)
            .Wait(approval, _ => correlation)
            .End(state => state.Value + 1)
            .Build();
        var remoteDefinition = writer.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition).GetHandleOrThrow();
        var remote = (await remoteDefinition.StartOrGetAsync(41, StartIdempotencyKey.Create("remote"), token))
            .GetHandleOrThrow();
        var localDefinition = observer.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition).GetHandleOrThrow();
        var local = await localDefinition.GetInstanceAsync(remote.InstanceId, token);

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

            var message = WorkflowInboundEvent.Create(approval, EventId.Create("approved"), correlation,
                null, clock.GetUtcNow(), new WorkflowEventRoute.Direct(remote.InstanceId));
            Assert.IsType<WorkflowEventAcceptanceResult.Accepted>(
                await writer.GetRequiredService<IWorkflowEventIngress>().AcceptAsync(message, token));
            var committed = Assert.IsType<WorkflowOutputResult<int>.Available>(await remote.GetOutputAsync(token));
            Assert.Equal(42, committed.Output);

            clock.Advance(TimeSpan.FromSeconds(1));
            // The wall-clock limit only bounds a broken wait. Workflow time stays fixed at one second.
            Assert.Equal(42, await waiting.WaitAsync(TimeSpan.FromSeconds(5), token));
        }
        finally
        {
            await cleanup.CancelAsync();
        }
    }

    private static ServiceProvider CreateHost(
        InMemoryWorkflowProvider sharedStore, IWorkflowProjectionStore projections, TimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(clock);
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddSingleton<IWorkflowEventStore>(sharedStore);
        services.AddSingleton<IWorkflowInboxStore>(sharedStore);
        services.AddSingleton<IWorkflowOutboxStore>(sharedStore);
        services.AddSingleton<IWorkflowStartIdempotencyStore>(sharedStore);
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

    private sealed class ObservationClock : TimeProvider
    {
        private readonly Clock clock = new(DateTimeOffset.UnixEpoch);
        private int fired;
        internal int Fired => Volatile.Read(ref fired);
        internal void Advance(TimeSpan by) => clock.Advance(by);
        public override DateTimeOffset GetUtcNow() => clock.Now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => clock.TimeProvider.CreateTimer(timerState =>
            {
                Interlocked.Increment(ref fired);
                callback(timerState);
            }, state, dueTime, period);
    }

    private sealed class ObservationReads(IWorkflowProjectionStore store) : IWorkflowProjectionStore
    {
        internal int Count { get; private set; }
        internal WorkflowInstanceStatus? LastStatus { get; private set; }

        public async Task<Option<WorkflowProjectionSnapshot>> GetAsync(InstanceId id, CancellationToken token)
        {
            var result = await store.GetAsync(id, token);
            Count++;
            LastStatus = result.HasValue ? result.Value.Status : null;
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
