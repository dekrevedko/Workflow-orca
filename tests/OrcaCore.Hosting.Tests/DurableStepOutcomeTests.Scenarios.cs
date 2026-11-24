using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.SqlServer;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DurableStepOutcomeTests
{
    private static readonly DateTimeOffset AdmissionTime = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(1);
    private const int InitialState = 10;
    private const int FirstSuccess = 11;
    private const int RetrySuccess = 22;

    private sealed record SeedState(int WorkerPid, string StartKey, Guid InstanceId, string OperationId,
        long Version, string CheckpointHash, string[] Journal, string[] WinnerEvents, string[] RejectedEvents);

    private static async Task RunOutcomeScenarioAsync(string phase, string provider, string connection, string directory,
        bool loseAcknowledgement, int maxAttempts, CancellationToken token)
    {
        var definition = Workflow.Durable<int>(DefinitionId.Parse("60861a92-46c2-4107-b881-fac79afbbcac"), DefinitionVersion.Initial)
            .Init<int>(value => value).Then<OutcomeStep>().WithRetry(maxAttempts).WithStepTimeout(StepTimeout)
            .End(state => state.Value).Build();

        if (phase == "seed")
        {
            // Independent driver clocks straddle the same persisted absolute deadline. No wall-clock sleep
            // determines which outcome is selected locally or which SQL transaction commits first.
            var firstStep = new OutcomeStep(hold: true);
            var retryStep = new OutcomeStep(hold: true);
            var success = new OutcomeAppendGate(batch => batch.Events.Any(e => e is WorkflowStepCompletedEvent));
            var timeout = new OutcomeAppendGate(batch => batch.Events.Any(e => e is WorkflowStepFailedEvent)
                || batch.Checkpoint?.LastStepPath?.EndsWith(":retry:2", StringComparison.Ordinal) == true);
            await using var first = await CreateOutcomeHostAsync(provider, connection, definition, firstStep, AdmissionTime, success, token);
            await using var expired = await CreateOutcomeHostAsync(provider, connection, definition, retryStep, AdmissionTime + StepTimeout, timeout, token);
            var key = StartIdempotencyKey.Create($"outcome-{Guid.NewGuid():N}");
            var successDrive = first.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference)
                .StartOrGetAsync(InitialState, key, token).AsTask();
            var dispatch = await BeforeDriverExitAsync(firstStep.Entered.Task, successDrive, token);
            dispatch.AttemptNumber.Should().Be(1);
            var stream = new WorkflowStreamId(dispatch.WorkflowInstanceId);
            var sql = first.GetRequiredService<OutcomeStore>().Inner;
            var admitted = (await sql.LoadCheckpointAsync(stream.InstanceId, token)).Value;
            var firstFiber = Envelope(admitted).Fibers.Should().ContainSingle().Which;
            firstFiber.AttemptInFlight.Should().BeTrue();
            firstFiber.RetryAttempt.Should().Be(1);
            firstFiber.LogicalOperationKey.Should().Be(dispatch.OperationId.ToString());
            firstFiber.TimeoutDeadline.Should().Be(AdmissionTime + StepTimeout);

            var timeoutDrive = expired.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference)
                .StartOrGetAsync(InitialState, key, token).AsTask();
            var timeoutBatch = await BeforeDriverExitAsync(timeout.Prepared.Task, timeoutDrive, token);
            firstStep.Release.SetResult();
            var successBatch = await BeforeDriverExitAsync(success.Prepared.Task, successDrive, token);
            successBatch.StreamId.Should().Be(stream);
            timeoutBatch.StreamId.Should().Be(stream);
            successBatch.ExpectedVersion.Should().Be(admitted.StreamVersion);
            timeoutBatch.ExpectedVersion.Should().Be(admitted.StreamVersion);
            successBatch.Events.Should().ContainSingle().Which.Should().BeOfType<WorkflowStepCompletedEvent>();
            State(successBatch.Checkpoint!).Should().Be(FirstSuccess);
            State(timeoutBatch.Checkpoint!).Should().Be(InitialState);
            if (maxAttempts == 1)
            {
                timeoutBatch.Events.OfType<WorkflowStepFailedEvent>().Should().ContainSingle().Which.ErrorSummary.Should().Contain("WF-STEP-TIMEOUT");
                timeoutBatch.Events.OfType<WorkflowTerminalEvent>().Should().ContainSingle().Which.Status.Should().Be(WorkflowInstanceStatus.Failed);
            }
            else
            {
                timeoutBatch.Events.Should().ContainSingle().Which.Should().BeOfType<WorkflowExecutionPositionChangedEvent>();
                var retry = Envelope(timeoutBatch.Checkpoint!).Fibers.Should().ContainSingle().Which;
                retry.RetryAttempt.Should().Be(2);
                retry.AttemptInFlight.Should().BeTrue();
                retry.LogicalOperationKey.Should().Be(firstFiber.LogicalOperationKey);
                retry.TimeoutDeadline.Should().Be(AdmissionTime + StepTimeout + StepTimeout);
            }

            var winner = loseAcknowledgement ? success : timeout;
            var loser = loseAcknowledgement ? timeout : success;
            winner.Submit.SetResult();
            var committed = await winner.Stored.Task.WaitAsync(token);
            committed.IsSuccess.Should().BeTrue();
            loser.Submit.SetResult();
            var rejected = await loser.Stored.Task.WaitAsync(token);
            rejected.IsFailure.Should().BeTrue();
            rejected.Error.Should().BeOfType<WorkflowConcurrencyException>();
            rejected.Error.Message.Should().Be(EventStoreConflict.ExpectedVersionMismatch(
                admitted.StreamVersion, committed.Value.NewVersion).Error.Message);
            if (!loseAcknowledgement)
            {
                timeout.Acknowledge.SetResult();
                if (maxAttempts == 2)
                {
                    var retry = await BeforeDriverExitAsync(retryStep.Entered.Task, timeoutDrive, token);
                    retry.AttemptNumber.Should().Be(2);
                    retry.OperationId.Should().Be(dispatch.OperationId);
                }
                else
                {
                    (await timeoutDrive).GetHandleOrThrow().InstanceId.Should().Be(stream.InstanceId);
                }
            }
            // The losing driver has submitted its stale write. Its conflict response stays held so it cannot
            // advance the winner's position. In the lost-ack case the successful driver's response is also held.
            var checkpoint = (await sql.LoadCheckpointAsync(stream.InstanceId, token)).Value;
            checkpoint.StreamVersion.Should().Be(committed.Value.NewVersion);
            var winnerBatch = loseAcknowledgement ? successBatch : timeoutBatch;
            checkpoint.Payload.Should().Equal(winnerBatch.Checkpoint!.Payload);
            var loserBatch = loseAcknowledgement ? timeoutBatch : successBatch;
            var history = await sql.LoadTailAsync(stream, StreamVersion.Empty, token);
            var ids = EventIds(history);
            ids.Skip(checked((int)admitted.StreamVersion.Value)).Should().Equal(EventIds(winnerBatch.Events));
            ids.Intersect(EventIds(loserBatch.Events)).Should().BeEmpty();
            firstStep.Invocations.Select(call => call.AttemptNumber).Should().Equal(1);
            retryStep.Invocations.Select(call => call.AttemptNumber).Should()
                .Equal(!loseAcknowledgement && maxAttempts == 2 ? new[] { 2 } : Array.Empty<int>());
            var seed = new SeedState(Environment.ProcessId, key.Value, stream.InstanceId.Value, dispatch.OperationId.ToString(),
                checkpoint.StreamVersion.Value, Convert.ToHexString(SHA256.HashData(checkpoint.Payload)), ids,
                EventIds(winnerBatch.Events), EventIds(loserBatch.Events));
            await WriteOutcomeMarkerAsync(directory, "seed.json", seed, token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return;
        }

        if (phase != "recover") throw new ArgumentOutOfRangeException(nameof(phase));
        var original = JsonSerializer.Deserialize<SeedState>(await File.ReadAllTextAsync(Path.Combine(directory, "seed.json"), token))!;
        original.WorkerPid.Should().NotBe(Environment.ProcessId);
        var replacementStep = new OutcomeStep(hold: false);
        await using var recovered = await CreateOutcomeHostAsync(provider, connection, definition, replacementStep,
            AdmissionTime + StepTimeout + TimeSpan.FromSeconds(1), null, token);
        var store = recovered.GetRequiredService<IWorkflowEventStore>();
        var instanceId = InstanceId.Parse(original.InstanceId.ToString());
        var startStream = new WorkflowStreamId(instanceId);
        var before = (await store.LoadCheckpointAsync(instanceId, token)).Value;
        before.StreamVersion.Value.Should().Be(original.Version);
        Convert.ToHexString(SHA256.HashData(before.Payload)).Should().Be(original.CheckpointHash);
        EventIds(await store.LoadTailAsync(startStream, StreamVersion.Empty, token)).Should().Equal(original.Journal);

        var replay = await recovered.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference)
            .StartOrGetAsync(InitialState, StartIdempotencyKey.Create(original.StartKey), token);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<int>>.Accepted>().Which.WasExisting.Should().BeTrue();
        var handle = replay.GetHandleOrThrow();
        handle.InstanceId.Should().Be(instanceId);
        var failed = !loseAcknowledgement && maxAttempts == 1;
        var expectedStatus = failed ? WorkflowInstanceStatus.Failed : WorkflowInstanceStatus.Completed;
        (await handle.GetSnapshotAsync(token)).Status.Should().Be(expectedStatus);
        var expectedState = loseAcknowledgement ? FirstSuccess : failed ? InitialState : RetrySuccess;
        (await handle.GetStateAsync<int>(token)).Should().Be(expectedState);
        if (!failed) (await handle.WaitForOutputAsync(token)).Should().Be(expectedState);

        var events = await store.LoadTailAsync(startStream, StreamVersion.Empty, token);
        EventIds(events.Take(original.Journal.Length)).Should().Equal(original.Journal);
        EventIds(events).Intersect(original.RejectedEvents).Should().BeEmpty();
        EventIds(events).Intersect(original.WinnerEvents).Should().Equal(original.WinnerEvents);
        var tail = events.Skip(original.Journal.Length).Select(e => e.GetType()).ToArray();
        tail.Should().Equal(failed ? Array.Empty<Type>() : loseAcknowledgement
            ? [typeof(WorkflowCompletedEvent), typeof(WorkflowTerminalEvent)]
            : [typeof(WorkflowStepCompletedEvent), typeof(WorkflowCompletedEvent), typeof(WorkflowTerminalEvent)]);
        events.OfType<WorkflowStepCompletedEvent>().Should().HaveCount(failed ? 0 : 1);
        events.OfType<WorkflowStepFailedEvent>().Should().HaveCount(failed ? 1 : 0);
        events.OfType<WorkflowTerminalEvent>().Should().ContainSingle().Which.Status.Should().Be(expectedStatus);
        if (failed) events.OfType<WorkflowStepFailedEvent>().Single().ErrorSummary.Should().Contain("WF-STEP-TIMEOUT");
        var attempts = replacementStep.Invocations.ToArray();
        attempts.Select(call => call.AttemptNumber).Should().Equal(!loseAcknowledgement && maxAttempts == 2 ? new[] { 2 } : Array.Empty<int>());
        attempts.Should().OnlyContain(call => call.OperationId.ToString() == original.OperationId && call.WorkflowInstanceId.Equals(instanceId));
        await WriteOutcomeMarkerAsync(directory, "recovered.json", new { WorkerPid = Environment.ProcessId, original.InstanceId,
            Status = expectedStatus.ToString(), State = expectedState, RecoveryAttempts = attempts.Select(call => call.AttemptNumber) }, token);
    }

    private static DurableExecutionEnvelopeV2 Envelope(CheckpointWrite checkpoint) => DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
    private static int State(CheckpointWrite checkpoint) => JsonSerializer.Deserialize<int>(Envelope(checkpoint).StatePayload);
    private static string[] EventIds(IEnumerable<WorkflowEvent> events) => events.Select(e => e.EventId.Value).ToArray();

    private static async Task<T> BeforeDriverExitAsync<T>(Task<T> barrier, Task drive, CancellationToken token)
    {
        if (await Task.WhenAny(barrier, drive).WaitAsync(token) == drive && !barrier.IsCompleted)
        {
            await drive;
            throw new InvalidOperationException("The driver exited before reaching the scheduled test boundary.");
        }
        return await barrier.WaitAsync(token);
    }

    private static async Task WriteOutcomeMarkerAsync<T>(string directory, string name, T value, CancellationToken token)
    {
        var path = Path.Combine(directory, name);
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(value), token);
        File.Move(path + ".tmp", path);
    }

    private static async Task<ServiceProvider> CreateOutcomeHostAsync(string provider, string connection,
        DurableWorkflowDefinition<int, int> definition, OutcomeStep step, DateTimeOffset now, OutcomeAppendGate? append, CancellationToken token)
    {
        var services = new ServiceCollection();
        Type options;
        switch (provider)
        {
            case "PostgreSql":
                services.AddOrcaCorePostgreSqlDurableProvider(new(connection, "public"));
                options = typeof(PostgreSqlDurableProviderOptions);
                break;
            case "SqlServer":
                services.AddOrcaCoreSqlServerDurableProvider(new(connection, "step_outcome"));
                options = typeof(SqlServerDurableProviderOptions);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(provider));
        }
        if (append is not null)
        {
            var descriptor = services.Single(item => item.ServiceType == typeof(IWorkflowEventStore));
            services.AddSingleton(sp => new OutcomeStore((IWorkflowEventStore)descriptor.ImplementationFactory!(sp),
                sp.GetRequiredService<IWorkflowStartIdempotencyStore>(), append));
            services.AddSingleton<IWorkflowEventStore>(sp => sp.GetRequiredService<OutcomeStore>());
        }
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
        services.AddSingleton(step);
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 1, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions { PartitionId = ResourceGovernancePartitionId.Create("step-outcome"), Pools = [] }
        }).AddWorkflow(definition);
        var host = services.BuildServiceProvider();
        // Background pumps remain stopped so only the named competing drivers run.
        await host.GetServices<IHostedService>().Single(service => service.GetType().Assembly == options.Assembly).StartAsync(token);
        return host;
    }

    private sealed class OutcomeStep(bool hold) : IStep<int>
    {
        internal ConcurrentQueue<StepExecutionContext> Invocations { get; } = new();
        internal TaskCompletionSource<StepExecutionContext> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken)
        {
            Invocations.Enqueue(context.Execution);
            Entered.TrySetResult(context.Execution);
            if (hold) await Release.Task; // The business body deliberately ignores its cancellation token.
            context.ReplaceState(context.Execution.AttemptNumber == 1 ? FirstSuccess : RetrySuccess);
            return new StepResult.Completed();
        }
    }

    private sealed class OutcomeAppendGate(Func<ProviderCommitBatch, bool> match)
    {
        internal TaskCompletionSource<ProviderCommitBatch> Prepared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Submit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<Result<AppendEventsResult>> Stored { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Acknowledge { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal async Task<Result<AppendEventsResult>> AppendAsync(IWorkflowEventStore sql, ProviderCommitBatch batch, CancellationToken token)
        {
            if (!match(batch) || !Prepared.TrySetResult(batch)) return await sql.AppendAsync(batch, token);
            await Submit.Task.WaitAsync(token);
            try
            {
                var result = await sql.AppendAsync(batch, token);
                Stored.SetResult(result);
                await Acknowledge.Task.WaitAsync(token);
                return result;
            }
            catch (Exception exception)
            {
                Stored.TrySetException(exception);
                throw;
            }
        }
    }

    private sealed class OutcomeStore(IWorkflowEventStore inner, IWorkflowStartIdempotencyStore starts, OutcomeAppendGate append)
        : IWorkflowEventStore, IWorkflowStartIdempotencyStore
    {
        internal IWorkflowEventStore Inner => inner;
        public Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch, CancellationToken token) => append.AppendAsync(inner, batch, token);
        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(string key, CancellationToken token) => starts.GetStartedAsync(key, token);
        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(InstanceId id, CancellationToken token) => inner.LoadCheckpointAsync(id, token);
        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(WorkflowStreamId id, StreamVersion version, CancellationToken token) => inner.LoadTailAsync(id, version, token);
        public Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(WorkflowStreamId id, CancellationToken token) => inner.LoadDagRunCheckpointAsync(id, token);
        public Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(DagRunStartKeyV1 key, CancellationToken token) => inner.GetDagRunByStartKeyAsync(key, token);
        public Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(WorkflowStreamId id, StreamVersion version, CancellationToken token) => inner.LoadDagRunTailAsync(id, version, token);
        public Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(DagRunLeaseRequestV1 request, CancellationToken token) => inner.AcquireDagRunLeaseAsync(request, token);
        public Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(DagRunLeaseRequestV1 request, CancellationToken token) => inner.RenewDagRunLeaseAsync(request, token);
        public Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(DagRunFenceV1 fence, CancellationToken token) => inner.ReleaseDagRunLeaseAsync(fence, token);
        public Task CancelDagRunLeaseRequestAsync(Guid run, Guid owner, Guid request, CancellationToken token) => inner.CancelDagRunLeaseRequestAsync(run, owner, request, token);
        public Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(DagRunDiscoveryRequestV1 request, CancellationToken token) => inner.ListRecoverableDagRunsAsync(request, token);
        public Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(DagRunSubscriptionV1 subscription, CancellationToken token) => inner.SubscribeDagRunAsync(subscription, token);
        public Task UnsubscribeDagRunAsync(Guid subscription, Guid host, CancellationToken token) => inner.UnsubscribeDagRunAsync(subscription, host, token);
        public Task<int> PruneExpiredDagRunObserversAsync(int maxCount, CancellationToken token) => inner.PruneExpiredDagRunObserversAsync(maxCount, token);
    }
}
