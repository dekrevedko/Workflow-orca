using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class OrcaCoreHostingServiceCollectionTests
{
    private static readonly TimeSpan HostedInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("AC", "PR-040")]
    public void AddOrcaCore_RegistersCoreEnginesAndInMemoryDefaults()
    {
        var services = new ServiceCollection();

        services.AddOrcaCore();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<EphemeralWorkflowEngine>().Should().NotBeNull();
        provider.GetRequiredService<DurableCommandRuntime>().Should().NotBeNull();
        provider.GetRequiredService<DurableCommandProcessor>().Should().NotBeNull();
        provider.GetRequiredService<DurableOutboxPump>().Should().NotBeNull();
        provider.GetRequiredService<DurableManagement>().Should().NotBeNull();
        provider.GetRequiredService<IWorkflowEventStore>().Should().BeOfType<InMemoryWorkflowProvider>();
        provider.GetRequiredService<IWorkflowOutboxStore>().Should().BeOfType<InMemoryWorkflowProvider>();
        provider.GetRequiredService<IMessageDispatcher>().Should().BeOfType<InMemoryWorkflowProvider>();
    }

    [Fact]
    [Trait("AC", "PR-040")]
    public void AddOrcaCoreHostedServices_RegistersPumpTimerAndSweepServices()
    {
        var services = new ServiceCollection();

        services.AddOrcaCore();
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));

        services.AddOrcaCoreHostedServices();
        services.AddOrcaCoreHostedServices();

        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        hostedServices.Should().ContainSingle(service => service is OrcaCoreOutboxPumpHostedService);
        hostedServices.Should().ContainSingle(service => service is OrcaCoreTimerHostedService);
        hostedServices.Should().ContainSingle(service => service is OrcaCoreOperationalSweepHostedService);
    }

    [Fact]
    [Trait("AC", "DU-032")]
    [Trait("AC", "PR-040")]
    public async Task HostedOutboxPump_StartsHostAndDispatchesCommittedOutboxRecords()
    {
        using var host = BuildHost(out var workflowStore, out _, out _);
        var outboxRecord = new OutboxWrite(OutboxRecordId.New(), "workflow.completed", [1, 2, 3]);
        await workflowStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(InstanceIdValue(1)),
                ExpectedVersion = StreamVersion.Empty,
                OutboxRecords = [outboxRecord]
            },
            TestContext.Current.CancellationToken);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await workflowStore.Dispatched.Task.WaitAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        var state = await workflowStore.GetStateAsync(
            outboxRecord.OutboxRecordId,
            TestContext.Current.CancellationToken);
        state.Value.Should().Be(OutboxRecordState.Dispatched);
    }

    [Fact]
    [Trait("AC", "EV-050")]
    [Trait("AC", "PR-040")]
    public async Task HostedTimerService_StartsHostAndFiresDueDurableTimers()
    {
        var clock = new FakeTimeProvider(Timestamp(0));
        using var host = BuildHost(out var workflowStore, out _, out _, clock);
        var instanceId = InstanceIdValue(2);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        await processor.ProcessAsync(Start(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new ScheduleTimerCommand
            {
                CommandId = CommandIdValue(2),
                InstanceId = instanceId,
                RequestedAt = clock.GetUtcNow(),
                TimerId = TimerIdValue(1),
                FireAt = clock.GetUtcNow(),
                WakeupName = "timeout"
            },
            TestContext.Current.CancellationToken);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await workflowStore.TimerFired.Task.WaitAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        var events = await workflowStore.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle()
            .Which.TimerId.Should().Be(TimerIdValue(1));
    }

    [Fact]
    [Trait("AC", "MG-064")]
    [Trait("AC", "PR-040")]
    public async Task HostedOperationalSweep_StartsHostAndExpiresResourcePoolTickets()
    {
        var clock = new FakeTimeProvider(Timestamp(0));
        using var host = BuildHost(out _, out var resourcePoolStore, out _, clock);

        await host.StartAsync(TestContext.Current.CancellationToken);
        var expiredAt = await resourcePoolStore.ExpiredAt.Task.WaitAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        expiredAt.Should().Be(clock.GetUtcNow());
    }

    [Fact]
    [Trait("AC", "PR-040")]
    public async Task HostedOutboxPump_WhenTransientClaimFailureOccurs_RetriesOnNextCycle()
    {
        var clock = new FakeTimeProvider(Timestamp(0));
        var workflowStore = new RecordingWorkflowProvider();
        using var service = new OrcaCoreOutboxPumpHostedService(
            new DurableOutboxPump(workflowStore, workflowStore),
            Options.Create(CreateFastRetryOptions()),
            clock,
            NullLogger<OrcaCoreOutboxPumpHostedService>.Instance);
        workflowStore.FailNextOutboxClaim(new InvalidOperationException("transient outbox failure"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await workflowStore.OutboxClaimFailed.Task.WaitAsync(TestContext.Current.CancellationToken);
        var retry = workflowStore.OutboxClaimRetried.Task;
        await AdvanceUntilObservedAsync(clock, retry);
        var attempts = await retry.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        attempts.Should().BeGreaterThan(1);
    }

    [Fact]
    [Trait("AC", "PR-040")]
    public async Task HostedTimerService_WhenTransientClaimFailureOccurs_RetriesOnNextCycle()
    {
        var clock = new FakeTimeProvider(Timestamp(0));
        var workflowStore = new RecordingWorkflowProvider();
        using var service = new OrcaCoreTimerHostedService(
            workflowStore,
            new DurableCommandProcessor(new DurableCommandRuntime(workflowStore)),
            Options.Create(CreateFastRetryOptions()),
            clock,
            NullLogger<OrcaCoreTimerHostedService>.Instance);
        workflowStore.FailNextTimerClaim(new InvalidOperationException("transient timer failure"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await workflowStore.TimerClaimFailed.Task.WaitAsync(TestContext.Current.CancellationToken);
        var retry = workflowStore.TimerClaimRetried.Task;
        await AdvanceUntilObservedAsync(clock, retry);
        var attempts = await retry.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        attempts.Should().BeGreaterThan(1);
    }

    [Fact]
    [Trait("AC", "PR-040")]
    public async Task HostedOperationalSweep_WhenTransientExpiryFailureOccurs_RetriesOnNextCycle()
    {
        var clock = new FakeTimeProvider(Timestamp(0));
        var resourcePoolStore = new RecordingResourcePoolStore();
        using var service = new OrcaCoreOperationalSweepHostedService(
            resourcePoolStore,
            Options.Create(CreateFastRetryOptions()),
            clock,
            NullLogger<OrcaCoreOperationalSweepHostedService>.Instance);
        resourcePoolStore.FailNextExpiry(new InvalidOperationException("transient sweep failure"));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await resourcePoolStore.ExpiryFailed.Task.WaitAsync(TestContext.Current.CancellationToken);
        var retry = resourcePoolStore.ExpiryRetried.Task;
        await AdvanceUntilObservedAsync(clock, retry);
        var attempts = await retry.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        attempts.Should().BeGreaterThan(1);
    }

    private static IHost BuildHost(
        out RecordingWorkflowProvider workflowStore,
        out RecordingResourcePoolStore resourcePoolStore,
        out FakeTimeProvider clock,
        FakeTimeProvider? suppliedClock = null,
        Action<OrcaCoreHostedServiceOptions>? configureHostedServices = null)
    {
        workflowStore = new RecordingWorkflowProvider();
        resourcePoolStore = new RecordingResourcePoolStore();
        clock = suppliedClock ?? new FakeTimeProvider(Timestamp(0));
        var capturedWorkflowStore = workflowStore;
        var capturedResourcePoolStore = resourcePoolStore;
        var capturedClock = clock;
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddSingleton<TimeProvider>(capturedClock);
        builder.Services.AddSingleton(capturedWorkflowStore);
        builder.Services.AddSingleton<IWorkflowEventStore>(capturedWorkflowStore);
        builder.Services.AddSingleton<IWorkflowInboxStore>(capturedWorkflowStore);
        builder.Services.AddSingleton<IWorkflowStartIdempotencyStore>(capturedWorkflowStore);
        builder.Services.AddSingleton<IWorkflowOutboxStore>(capturedWorkflowStore);
        builder.Services.AddSingleton<IWorkflowProjectionStore>(capturedWorkflowStore);
        builder.Services.AddSingleton<IWorkflowRetentionStore>(capturedWorkflowStore);
        builder.Services.AddSingleton<ITimerScheduler>(capturedWorkflowStore);
        builder.Services.AddSingleton<IMessageDispatcher>(capturedWorkflowStore);
        builder.Services.AddSingleton<IWorkflowPayloadSerializer>(capturedWorkflowStore);
        builder.Services.AddSingleton<IResourcePoolStore>(capturedResourcePoolStore);
        builder.Services
            .AddOrcaCore()
            .AddOrcaCoreHostedServices(options =>
            {
                options.OutboxPumpInterval = TimeSpan.FromMinutes(5);
                options.TimerSweepInterval = TimeSpan.FromMinutes(5);
                options.OperationalSweepInterval = TimeSpan.FromMinutes(5);
                configureHostedServices?.Invoke(options);
            });
        return builder.Build();
    }

    private static void ConfigureFastRetry(OrcaCoreHostedServiceOptions options)
    {
        options.OutboxPumpInterval = HostedInterval;
        options.TimerSweepInterval = HostedInterval;
        options.OperationalSweepInterval = HostedInterval;
        options.TransientFailureBackoff = FailureBackoff;
    }

    private static OrcaCoreHostedServiceOptions CreateFastRetryOptions()
    {
        var options = new OrcaCoreHostedServiceOptions();
        ConfigureFastRetry(options);
        return options;
    }

    private static async Task AdvanceUntilObservedAsync(FakeTimeProvider clock, Task observed)
    {
        for (var attempt = 0; attempt < 50 && !observed.IsCompleted; attempt++)
        {
            await Task.Yield();
            clock.Advance(FailureBackoff);
            await WaitBrieflyAsync(observed);
            clock.Advance(HostedInterval);
            await WaitBrieflyAsync(observed);
        }

        observed.IsCompleted.Should().BeTrue("the hosted-service retry window should have elapsed under fake time");
    }

    private static async Task WaitBrieflyAsync(Task observed)
    {
        for (var attempt = 0; attempt < 4 && !observed.IsCompleted; attempt++)
        {
            await Task.Yield();
        }
    }

    private static StartWorkflowCommand Start(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(0),
            DefinitionId = DefinitionId.New(),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class RecordingWorkflowProvider :
        IWorkflowEventStore,
        IWorkflowInboxStore,
        IWorkflowStartIdempotencyStore,
        IWorkflowOutboxStore,
        IWorkflowProjectionStore,
        IWorkflowRetentionStore,
        ITimerScheduler,
        IMessageDispatcher,
        IWorkflowPayloadSerializer
    {
        private readonly InMemoryWorkflowProvider inner = new();
        private Exception? outboxClaimFailure;
        private Exception? timerClaimFailure;
        private int outboxClaimAttempts;
        private int timerClaimAttempts;

        internal TaskCompletionSource<OutboxWrite> Dispatched { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<WorkflowTimerFiredEvent> TimerFired { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<Exception> OutboxClaimFailed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<int> OutboxClaimRetried { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<Exception> TimerClaimFailed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<int> TimerClaimRetried { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void FailNextOutboxClaim(Exception exception)
        {
            outboxClaimFailure = exception;
        }

        internal void FailNextTimerClaim(Exception exception)
        {
            timerClaimFailure = exception;
        }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            return inner.LoadCheckpointAsync(instanceId, cancellationToken);
        }

        public async Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            var result = await inner.AppendAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && batch.Events.OfType<WorkflowTimerFiredEvent>().SingleOrDefault() is { } timerFired)
            {
                TimerFired.TrySetResult(timerFired);
            }

            return result;
        }

        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            return inner.LoadTailAsync(streamId, afterVersion, cancellationToken);
        }

        public Task<Option<InboxRecordState>> GetAsync(EventId eventId, CancellationToken cancellationToken)
        {
            return inner.GetAsync(eventId, cancellationToken);
        }

        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            return inner.GetStartedAsync(idempotencyKey, cancellationToken);
        }

        public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
        {
            return inner.ClaimAsync(maxCount, cancellationToken);
        }

        public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
            OutboxClaimRequest request,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref outboxClaimAttempts);
            var failure = Interlocked.Exchange(ref outboxClaimFailure, null);
            if (failure is not null)
            {
                OutboxClaimFailed.TrySetResult(failure);
                return Task.FromException<IReadOnlyList<OutboxWrite>>(failure);
            }

            if (attempt > 1)
            {
                OutboxClaimRetried.TrySetResult(attempt);
            }

            return inner.ClaimAsync(request, cancellationToken);
        }

        public Task<Option<OutboxRecordState>> GetStateAsync(
            OutboxRecordId outboxRecordId,
            CancellationToken cancellationToken)
        {
            return inner.GetStateAsync(outboxRecordId, cancellationToken);
        }

        public Task MarkAsync(
            OutboxRecordId outboxRecordId,
            OutboxRecordState state,
            CancellationToken cancellationToken)
        {
            return inner.MarkAsync(outboxRecordId, state, cancellationToken);
        }

        public Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
        {
            return inner.ReleaseAsync(outboxRecordId, cancellationToken);
        }

        public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
        {
            return inner.ApplyAsync(operations, cancellationToken);
        }

        public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            return inner.ListAsync(query, cancellationToken);
        }

        public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
        {
            return inner.CountAsync(query, cancellationToken);
        }

        public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            return inner.ListActiveWaitsAsync(query, cancellationToken);
        }

        public Task<OrcaCore.Abstractions.Instances.WorkflowStatistics> GetStatisticsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            return inner.GetStatisticsAsync(query, cancellationToken);
        }

        public Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
        {
            return inner.ArchiveAsync(policy, cancellationToken);
        }

        public Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
        {
            return inner.PurgeAsync(policy, cancellationToken);
        }

        public Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
        {
            return inner.ScheduleAsync(request, cancellationToken);
        }

        public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
            DateTimeOffset dueAtOrBefore,
            int maxCount,
            CancellationToken cancellationToken)
        {
            return inner.ClaimDueAsync(dueAtOrBefore, maxCount, cancellationToken);
        }

        public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
            TimerClaimRequest request,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref timerClaimAttempts);
            var failure = Interlocked.Exchange(ref timerClaimFailure, null);
            if (failure is not null)
            {
                TimerClaimFailed.TrySetResult(failure);
                return Task.FromException<IReadOnlyList<FireTimerCommand>>(failure);
            }

            if (attempt > 1)
            {
                TimerClaimRetried.TrySetResult(attempt);
            }

            return inner.ClaimDueAsync(request, cancellationToken);
        }

        public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
        {
            return inner.CompleteAsync(timerId, cancellationToken);
        }

        public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
        {
            return inner.ReleaseAsync(timerId, cancellationToken);
        }

        public async Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
        {
            var result = await inner.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
            Dispatched.TrySetResult(record);
            return result;
        }

        public SerializedPayload Serialize<TPayload>(TPayload payload)
        {
            return inner.Serialize(payload);
        }

        public TPayload Deserialize<TPayload>(SerializedPayload payload)
        {
            return inner.Deserialize<TPayload>(payload);
        }
    }

    private sealed class RecordingResourcePoolStore : IResourcePoolStore
    {
        private readonly InMemoryResourcePoolStore inner = new();
        private Exception? expiryFailure;
        private int expiryAttempts;

        internal TaskCompletionSource<DateTimeOffset> ExpiredAt { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<Exception> ExpiryFailed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<int> ExpiryRetried { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void FailNextExpiry(Exception exception)
        {
            expiryFailure = exception;
        }

        public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
        {
            return inner.UpsertPoolAsync(definition, cancellationToken);
        }

        public Task<ResourcePoolAcquireResult> AcquireAsync(
            ResourcePoolAcquireRequest request,
            CancellationToken cancellationToken)
        {
            return inner.AcquireAsync(request, cancellationToken);
        }

        public Task<ResourcePoolReleaseResult> ReleaseAsync(
            ResourcePoolReleaseRequest request,
            CancellationToken cancellationToken)
        {
            return inner.ReleaseAsync(request, cancellationToken);
        }

        public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(
            string poolName,
            CancellationToken cancellationToken)
        {
            return inner.GetPoolAsync(poolName, cancellationToken);
        }

        public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
        {
            return inner.ResizePoolAsync(poolName, capacity, cancellationToken);
        }

        public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref expiryAttempts);
            var failure = Interlocked.Exchange(ref expiryFailure, null);
            if (failure is not null)
            {
                ExpiryFailed.TrySetResult(failure);
                return Task.FromException<ResourcePoolExpiryResult>(failure);
            }

            if (attempt > 1)
            {
                ExpiryRetried.TrySetResult(attempt);
            }

            ExpiredAt.TrySetResult(now);
            return inner.ExpireTicketsAsync(now, cancellationToken);
        }

        public Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
            Guid ticketId,
            string reason,
            DateTimeOffset releasedAt,
            CancellationToken cancellationToken)
        {
            return inner.ForceReleaseTicketAsync(ticketId, reason, releasedAt, cancellationToken);
        }
    }
}
