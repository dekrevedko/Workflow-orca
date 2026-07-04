using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableCommandPipelineTests
{
    [Fact]
    public async Task ProcessCommand_LoadsCheckpointAndTailBeforeDecision()
    {
        var instanceId = InstanceIdValue(1);
        var store = new RecordingEventStore
        {
            Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(1), "application/octet-stream", [1]),
            Tail = [StepCompleted("root/1")]
        };
        var processor = new DurableCommandProcessor(store);

        await processor.ProcessAsync(StepCompletedCommand(instanceId, 2), TestContext.Current.CancellationToken);

        store.LoadedCheckpointFor.Should().Be(instanceId);
        store.LoadedTailAfterVersion.Should().Be(new StreamVersion(1));
    }

    [Fact]
    public async Task ProcessCommand_AppendsWithExpectedVersionFromAggregate()
    {
        var instanceId = InstanceIdValue(1);
        var store = new RecordingEventStore
        {
            Tail = [Started(instanceId)]
        };
        var processor = new DurableCommandProcessor(store);

        await processor.ProcessAsync(StepCompletedCommand(instanceId, 2), TestContext.Current.CancellationToken);

        store.AppendedBatch.Should().NotBeNull();
        store.AppendedBatch!.ExpectedVersion.Should().Be(new StreamVersion(1));
        store.AppendedBatch.StreamId.Should().Be(new WorkflowStreamId(instanceId));
    }

    [Fact]
    [Trait("AC", "EV-050")]
    public async Task ProcessCommand_TimerScheduled_IncludesTimerScheduleInCommitBatch()
    {
        var instanceId = InstanceIdValue(1);
        var timerId = TimerIdValue(10);
        var store = new RecordingEventStore
        {
            Tail = [Started(instanceId)]
        };
        var processor = new DurableCommandProcessor(store);

        await processor.ProcessAsync(
            new ScheduleTimerCommand
            {
                CommandId = CommandIdValue(10),
                InstanceId = instanceId,
                RequestedAt = Timestamp(10),
                TimerId = timerId,
                FireAt = Timestamp(30),
                WakeupName = "approval-timeout"
            },
            TestContext.Current.CancellationToken);

        store.AppendedBatch.Should().NotBeNull();
        store.AppendedBatch!.TimerSchedules.Should().ContainSingle()
            .Which.TimerId.Should().Be(timerId);
    }

    [Fact]
    public async Task ProcessCommand_VersionConflict_ReturnsClearConflict()
    {
        var instanceId = InstanceIdValue(1);
        var store = new RecordingEventStore
        {
            Tail = [Started(instanceId)],
            AppendResult = Result<AppendEventsResult>.Failure(new WorkflowConcurrencyException("expected version conflict"))
        };
        var processor = new DurableCommandProcessor(store);

        var result = await processor.ProcessAsync(
            StepCompletedCommand(instanceId, 2),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Conflict);
        result.Message.Should().Contain("expected version conflict");
    }

    [Fact]
    public async Task ProcessCommand_NotifiesRuntimeObserverAfterCommit()
    {
        var instanceId = InstanceIdValue(1);
        var observer = new RecordingRuntimeObserver();
        var store = new RecordingEventStore
        {
            Tail = [Started(instanceId)]
        };
        var processor = new DurableCommandProcessor(store, runtimeObserver: observer);

        await processor.ProcessAsync(StepCompletedCommand(instanceId, 2), TestContext.Current.CancellationToken);

        observer.Observations.Should().ContainSingle().Which.Should().Be(
            new WorkflowRuntimeObservation(
                WorkflowRuntimeObservationKind.CommandCommitted,
                instanceId,
                DurableCommandOutcome.Committed,
                new StreamVersion(2),
                EventCount: 1,
                CheckpointWritten: true,
                Evicted: false,
                InboxEventId: null,
                Message: null));
    }

    [Fact]
    public async Task ProcessCommand_WhenRuntimeObserverThrows_StillReturnsCommittedResult()
    {
        var instanceId = InstanceIdValue(1);
        var store = new RecordingEventStore
        {
            Tail = [Started(instanceId)]
        };
        var processor = new DurableCommandProcessor(
            store,
            runtimeObserver: new ThrowingRuntimeObserver());

        var result = await processor.ProcessAsync(
            StepCompletedCommand(instanceId, 2),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        store.AppendedBatch.Should().NotBeNull();
        store.AppendedBatch!.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStepCompletedEvent>();
    }

    [Fact]
    [Trait("AC", "AC-309")]
    public async Task ConcurrentResumeAttempts_CommitExactlyOneOutcome()
    {
        var instanceId = InstanceIdValue(1);
        var waitId = WaitIdValue(1);
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = StreamVersion.Empty,
            Events =
            [
                Started(instanceId),
                WaitRegistered(instanceId, waitId)
            ]
        }, TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store);

        var first = processor.ProcessAsync(WaitMatchedCommand(instanceId, waitId, 2), TestContext.Current.CancellationToken);
        var second = processor.ProcessAsync(WaitMatchedCommand(instanceId, waitId, 3), TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        results.Count(result => result.Outcome == DurableCommandOutcome.Committed).Should().Be(1);
        results.Count(result => result.Outcome == DurableCommandOutcome.NoOp).Should().Be(1);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessCommand_WhenBatchDrains_EvictsIdleLane()
    {
        var instanceId = InstanceIdValue(1);
        var evicted = new AsyncSignalCounter();
        var store = new RecordingEventStore
        {
            Tail = [Started(instanceId)]
        };
        var runtime = new DurableCommandRuntime(store, resourcePoolStore: null, onLaneEvicted: _ => evicted.Signal());
        var processor = new DurableCommandProcessor(runtime);

        await processor.ProcessAsync(StepCompletedCommand(instanceId, 2), TestContext.Current.CancellationToken);

        await evicted.WaitForCountAsync(1, TestContext.Current.CancellationToken);
        processor.ActiveLaneCount.Should().Be(0);
    }

    [Fact]
    public async Task Runtime_SharedByProcessors_ReusesOneInstanceLane()
    {
        var instanceId = InstanceIdValue(1);
        var store = new BlockingAppendEventStore
        {
            Tail = [Started(instanceId)]
        };
        var evicted = new AsyncSignalCounter();
        var runtime = new DurableCommandRuntime(store, resourcePoolStore: null, onLaneEvicted: _ => evicted.Signal());
        var firstProcessor = new DurableCommandProcessor(runtime);
        var secondProcessor = new DurableCommandProcessor(runtime);
        var first = firstProcessor.ProcessAsync(
            StepCompletedCommand(instanceId, 2),
            TestContext.Current.CancellationToken);

        await store.AppendEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = secondProcessor.ProcessAsync(
            StepCompletedCommand(instanceId, 3),
            TestContext.Current.CancellationToken);

        firstProcessor.ActiveLaneCount.Should().Be(1);
        secondProcessor.ActiveLaneCount.Should().Be(1);

        store.ReleaseAppend();
        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        store.MaxConcurrentAppends.Should().Be(1);
        await evicted.WaitForCountAsync(1, TestContext.Current.CancellationToken);
        secondProcessor.ActiveLaneCount.Should().Be(0);
    }

    private static DurableStepCompletedCommand StepCompletedCommand(InstanceId instanceId, int commandValue)
    {
        return new DurableStepCompletedCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            "root/1",
            "application/octet-stream",
            [(byte)commandValue]);
    }

    private static DurableWaitMatchedCommand WaitMatchedCommand(
        InstanceId instanceId,
        WaitId waitId,
        int commandValue)
    {
        return new DurableWaitMatchedCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            waitId,
            EventIdValue(commandValue + 100));
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowStepCompletedEvent StepCompleted(string stepPath)
    {
        return new WorkflowStepCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            StepPath = stepPath
        };
    }

    private static WorkflowWaitRegisteredEvent WaitRegistered(InstanceId instanceId, WaitId waitId)
    {
        return new WorkflowWaitRegisteredEvent
        {
            EventId = EventIdValue(3),
            InstanceId = instanceId,
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3),
            WaitId = waitId,
            EventName = "Approved",
            CorrelationId = new CorrelationId("order-1")
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class RecordingEventStore : IWorkflowEventStore
    {
        public CheckpointWrite? Checkpoint { get; init; }

        public IReadOnlyList<WorkflowEvent> Tail { get; init; } = [];

        public Result<AppendEventsResult>? AppendResult { get; init; }

        public InstanceId? LoadedCheckpointFor { get; private set; }

        public StreamVersion? LoadedTailAfterVersion { get; private set; }

        public ProviderCommitBatch? AppendedBatch { get; private set; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadedCheckpointFor = instanceId;
            return Task.FromResult(Checkpoint is null
                ? Option<CheckpointWrite>.None
                : Option<CheckpointWrite>.Some(Checkpoint));
        }

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendedBatch = batch;
            return Task.FromResult(AppendResult
                ?? Result<AppendEventsResult>.Success(new AppendEventsResult(batch.ExpectedVersion.Next())));
        }

        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadedTailAfterVersion = afterVersion;
            return Task.FromResult(Tail);
        }
    }

    private sealed class RecordingRuntimeObserver : IWorkflowRuntimeObserver
    {
        public List<WorkflowRuntimeObservation> Observations { get; } = [];

        public ValueTask OnCommandCompletedAsync(
            WorkflowRuntimeObservation observation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingRuntimeObserver : IWorkflowRuntimeObserver
    {
        public ValueTask OnCommandCompletedAsync(
            WorkflowRuntimeObservation observation,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("observer failure");
        }
    }

    private sealed class BlockingAppendEventStore : IWorkflowEventStore
    {
        private readonly object gate = new();
        private readonly TaskCompletionSource releaseAppend = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int activeAppends;

        public TaskCompletionSource AppendEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<WorkflowEvent> Tail { get; init; } = [];

        public int MaxConcurrentAppends { get; private set; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Option<CheckpointWrite>.None);
        }

        public async Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = Interlocked.Increment(ref activeAppends);
            lock (gate)
            {
                MaxConcurrentAppends = Math.Max(MaxConcurrentAppends, active);
            }

            AppendEntered.TrySetResult();
            try
            {
                if (active == 1)
                {
                    await releaseAppend.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                return Result<AppendEventsResult>.Success(
                    new AppendEventsResult(batch.ExpectedVersion.Next()));
            }
            finally
            {
                Interlocked.Decrement(ref activeAppends);
            }
        }

        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Tail);
        }

        public void ReleaseAppend()
        {
            releaseAppend.TrySetResult();
        }
    }
}
