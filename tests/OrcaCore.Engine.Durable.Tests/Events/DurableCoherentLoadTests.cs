using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Providers.InMemory;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Abstractions.Serialization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Events;

public sealed class DurableCoherentLoadTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PositionOnlyCommitRejectsASecondHostAtTheOldVersion(bool fiberFailure)
    {
        using var store = new InMemoryWorkflowProvider();
        var first = new DurableCommandProcessor(store);
        var second = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(first, new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var start = await runtime.StartOrGetAsync<string, State>("position-version", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var checkpoint = (await store.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        var oldEnvelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        DurableCheckpointPayload envelope = oldEnvelope with
        {
            Fibers = oldEnvelope.Fibers.Select(fiber => fiber with { QuantumRotationCount = fiber.QuantumRotationCount + 1 }).ToArray()
        };
        var commandId = new CommandId(Guid.NewGuid());
        var accepted = fiberFailure
            ? await first.ProcessAsync(new DurableFiberFailedCommand(commandId, start.InstanceId, DateTimeOffset.UtcNow,
                "root/position", "failed branch", envelope) { ExpectedStreamVersion = checkpoint.StreamVersion }, TestContext.Current.CancellationToken)
            : await first.ProcessAsync(new DurableYieldCommand(commandId, start.InstanceId, DateTimeOffset.UtcNow,
                "root/position", envelope) { ExpectedStreamVersion = checkpoint.StreamVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(DurableCommandOutcome.Committed, accepted.Outcome);
        var current = (await store.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        Assert.True(current.StreamVersion.Value > checkpoint.StreamVersion.Value);
        Assert.Equal(envelope.Payload, current.Payload);
        var stale = await second.ProcessAsync(new DurableYieldCommand(new CommandId(Guid.NewGuid()),
            start.InstanceId, DateTimeOffset.UtcNow, "root/stale", oldEnvelope)
            { ExpectedStreamVersion = checkpoint.StreamVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(DurableCommandOutcome.Conflict, stale.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterleavedCheckpointLoadMustNotCommitPublishTwice(bool afterTail)
    {
        using var store = new InMemoryWorkflowProvider();
        var other = new DurableWorkflowRuntime(new DurableCommandProcessor(store), new DurableDefinitionRegistry(), TimeProvider.System);
        var wrapped = new InterleavingStore(store, async id => { await other.DriveAsync(id, TestContext.Current.CancellationToken); })
        { AdvanceAfterTail = afterTail };
        var runtime = new DurableWorkflowRuntime(new DurableCommandProcessor(wrapped), new DurableDefinitionRegistry(), TimeProvider.System);
        var outgoing = WorkflowEventContract.Create(EventName.Create("review-published"), EventContractVersion.Initial);
        var correlation = CorrelationId.Create("review-race");
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v))
            .Publish(outgoing, _ => correlation)
            .Delay(TimeSpan.FromDays(1))
            .End().Build();
        runtime.RegisterDefinition(definition);
        other.RegisterDefinition(definition);
        await runtime.StartOrGetAsync<string, State>("review-key", definition.DefinitionId, definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var published = await store.ClaimAsync(new OutboxClaimRequest(10, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1))
        { KindSelector = OutboxKindSelector.Including(OutboxKinds.WorkflowEvent) }, TestContext.Current.CancellationToken);
        Assert.Single(published);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MatchedWaitAndTimeoutPairsResumeTheCapturedBlockedPosition(bool timeoutWins)
    {
        using var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(), TimeProvider.System);
        var correlation = CorrelationId.Create("paired-wait");
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v))
            .Wait(WorkflowEventContract.Create(EventName.Create("resume"), EventContractVersion.Initial),
                _ => correlation, TimeSpan.FromSeconds(1))
            .End().Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, State>("paired-wait", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var checkpoint = (await store.LoadCheckpointAsync(started.InstanceId, TestContext.Current.CancellationToken)).Value;
        var before = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, TestContext.Current.CancellationToken);
        if (timeoutWins)
        {
            var timer = Assert.Single(before.TimerState.ActiveTimers);
            await processor.ProcessAsync(new FireTimerCommand
            {
                CommandId = CommandId.New(), InstanceId = started.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow.AddSeconds(2), TimerId = timer.TimerId
            }, TestContext.Current.CancellationToken);
        }
        else
        {
            await runtime.RaiseFacadeEventAsync(started.InstanceId, new DurableEventEnvelope
            {
                EventId = EventId.Create("paired-event"), EventName = "resume", CorrelationId = correlation,
                OccurredAt = DateTimeOffset.UtcNow,
                Route = new DurableEventRouteEnvelope { Kind = InboxRouteKinds.Direct, InstanceId = started.InstanceId }
            }, TestContext.Current.CancellationToken, driveAfterDelivery: false);
        }
        var loaded = await new DurableAggregateLoader(store).LoadExecutionStateAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(checkpoint.StreamVersion, loaded.Checkpoint.Value.StreamVersion);
        Assert.False(loaded.NeedsPositionReload);
        Assert.True(loaded.Aggregate.StreamVersion.Value > checkpoint.StreamVersion.Value);
        var result = await runtime.DriveAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(DurableSegmentOutcome.Terminal, result.Outcome);
        var final = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(timeoutWins ? WorkflowInstanceStatus.Failed : WorkflowInstanceStatus.Completed, final.Status);
    }

    [Fact]
    public async Task RolloverTailRequiresTheReplacementCheckpoint()
    {
        using var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var start = await runtime.StartOrGetAsync<string, State>("rollover", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var checkpoint = (await store.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        var result = await processor.ProcessAsync(new ContinueAsNewCommand
        {
            CommandId = CommandId.New(), InstanceId = start.InstanceId, RequestedAt = DateTimeOffset.UtcNow,
            StateContentType = checkpoint.ContentType, StatePayload = checkpoint.Payload,
            Envelope = new DurableCheckpointPayload { ContentType = checkpoint.ContentType, Payload = checkpoint.Payload },
            ExpectedStreamVersion = checkpoint.StreamVersion
        }, TestContext.Current.CancellationToken);
        Assert.Equal(DurableCommandOutcome.Committed, result.Outcome);
        var wrapped = new InterleavingStore(store, _ => Task.CompletedTask) { ForcedCheckpoint = checkpoint };
        var loaded = await new DurableAggregateLoader(wrapped).LoadExecutionStateAsync(start.InstanceId, TestContext.Current.CancellationToken);
        Assert.True(loaded.NeedsPositionReload);
        Assert.False((await new DurableAggregateLoader(store).LoadExecutionStateAsync(start.InstanceId,
            TestContext.Current.CancellationToken)).NeedsPositionReload);
    }

    [Fact]
    public async Task ContinuouslyAdvancingCheckpointsKeepTheContinuationRetryable()
    {
        using var store = new InMemoryWorkflowProvider();
        var first = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(first, new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var start = await runtime.StartOrGetAsync<string, State>("stale-load", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var checkpoint = (await store.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        await first.ProcessAsync(new DurableYieldCommand(CommandId.New(), start.InstanceId, DateTimeOffset.UtcNow,
            "root/position", new DurableCheckpointPayload { ContentType = checkpoint.ContentType, Payload = checkpoint.Payload })
            { ExpectedStreamVersion = checkpoint.StreamVersion }, TestContext.Current.CancellationToken);
        var wrapped = new InterleavingStore(store, async id =>
        {
            var latest = (await store.LoadCheckpointAsync(id, TestContext.Current.CancellationToken)).Value;
            var next = await first.ProcessAsync(new DurableYieldCommand(CommandId.New(), id, DateTimeOffset.UtcNow,
                "root/moving", new DurableCheckpointPayload { ContentType = latest.ContentType, Payload = latest.Payload })
                { ExpectedStreamVersion = latest.StreamVersion }, TestContext.Current.CancellationToken);
            Assert.Equal(DurableCommandOutcome.Committed, next.Outcome);
        }) { RepeatOnEveryCheckpointRead = true };
        var second = new DurableCommandProcessor(wrapped);
        var replacement = new DurableWorkflowRuntime(second, new DurableDefinitionRegistry(), TimeProvider.System);
        replacement.RegisterDefinition(definition);
        var result = await replacement.DriveAsync(start.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(DurableSegmentOutcome.Conflict, result.Outcome);
        Assert.Equal(6, wrapped.CheckpointReads);
        Assert.Empty(wrapped.Batches);
        var pump = new DurableContinuationPump(store, replacement, second);
        Assert.Equal(0, await pump.PumpOnceAsync(new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken));
        var retryable = await store.ClaimAsync(new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1))
            { KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue) }, TestContext.Current.CancellationToken);
        Assert.NotEmpty(retryable);
        Assert.Empty(wrapped.Batches);
    }

    [Fact]
    public async Task UnchangedUnreconciledTailParksWithTheOffendingEventInItsDiagnostic()
    {
        using var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, State>("unreconciled", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        await AppendUncheckpointedPositionAsync(store, started.InstanceId);
        var result = await runtime.DriveAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(DurableSegmentOutcome.Parked, result.Outcome);
        var parked = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(DurableParkReason.Poison, parked.ParkReason);
        Assert.Contains(nameof(WorkflowExecutionPositionChangedEvent), parked.ErrorSummary);
        Assert.Contains("No supported checkpoint repair exists", parked.ErrorSummary);
        Assert.Contains("re-arming will park again", parked.ErrorSummary);
        Assert.Contains("engine or provider defect", parked.ErrorSummary);
        var version = parked.StreamVersion;
        Assert.Equal(DurableSegmentOutcome.Parked,
            (await runtime.DriveAsync(started.InstanceId, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(version, (await new DurableAggregateLoader(store).LoadAsync(started.InstanceId,
            TestContext.Current.CancellationToken)).StreamVersion);
    }

    [Fact]
    public async Task RearmingAnUnreconciledPositionParksAgainWithoutReplacingItsCheckpoint()
    {
        using var store = new InMemoryWorkflowProvider();
        var runtime = new DurableWorkflowRuntime(new DurableCommandProcessor(store), new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, State>("repark", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var checkpoint = await AppendUncheckpointedPositionAsync(store, started.InstanceId);
        Assert.Equal(DurableSegmentOutcome.Parked,
            (await runtime.DriveAsync(started.InstanceId, TestContext.Current.CancellationToken)).Outcome);
        var parked = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, TestContext.Current.CancellationToken);

        var rearmed = await runtime.RearmAsync(started.InstanceId,
            new DurableRearmRequest(parked.StreamVersion, AcknowledgePoison: true), TestContext.Current.CancellationToken);
        Assert.Equal(DurableCommandOutcome.Committed, rearmed.Outcome);
        Assert.Null((await new DurableAggregateLoader(store).LoadAsync(started.InstanceId,
            TestContext.Current.CancellationToken)).ParkReason);
        Assert.Equal(DurableSegmentOutcome.Parked,
            (await runtime.DriveAsync(started.InstanceId, TestContext.Current.CancellationToken)).Outcome);

        var reparked = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(DurableParkReason.Poison, reparked.ParkReason);
        Assert.Contains(nameof(WorkflowExecutionPositionChangedEvent), reparked.ErrorSummary);
        var unchanged = (await store.LoadCheckpointAsync(started.InstanceId, TestContext.Current.CancellationToken)).Value;
        Assert.Equal(checkpoint.StreamVersion, unchanged.StreamVersion);
        Assert.Equal(checkpoint.ContentType, unchanged.ContentType);
        Assert.Equal(checkpoint.Payload, unchanged.Payload);
        var tail = await store.LoadTailAsync(new WorkflowStreamId(started.InstanceId), checkpoint.StreamVersion,
            TestContext.Current.CancellationToken);
        Assert.Collection(tail,
            item => Assert.IsType<WorkflowExecutionPositionChangedEvent>(item),
            item => Assert.IsType<WorkflowParkedEvent>(item),
            item => Assert.IsType<WorkflowUnparkedEvent>(item),
            item => Assert.IsType<WorkflowParkedEvent>(item));
    }

    [Fact]
    public async Task ParkAppendConflictReloadsAndHonorsTheConcurrentCommit()
    {
        using var store = new InMemoryWorkflowProvider();
        var otherHost = new DurableCommandProcessor(store);
        var wrapped = new InterleavingStore(store, _ => Task.CompletedTask)
        {
            BeforeParkAppend = async batch =>
            {
                var cancelled = await otherHost.ProcessAsync(new CancelWorkflowCommand
                {
                    CommandId = CommandId.New(), InstanceId = batch.StreamId.InstanceId, RequestedAt = DateTimeOffset.UtcNow
                }, TestContext.Current.CancellationToken);
                Assert.Equal(DurableCommandOutcome.Committed, cancelled.Outcome);
            }
        };
        var runtime = new DurableWorkflowRuntime(new DurableCommandProcessor(wrapped), new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, State>("park-conflict", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        await AppendUncheckpointedPositionAsync(store, started.InstanceId);

        var result = await runtime.DriveAsync(started.InstanceId, TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(wrapped.ParkAppendResults).IsSuccess);
        Assert.Equal(DurableSegmentOutcome.Terminal, result.Outcome);
        var current = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, current.Status);
        Assert.Null(current.ParkReason);
        var events = await store.LoadTailAsync(new WorkflowStreamId(started.InstanceId), StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(events, item => item is WorkflowParkedEvent);
        Assert.Single(events.OfType<WorkflowCancellationRequestedEvent>());
    }

    private static async Task<CheckpointWrite> AppendUncheckpointedPositionAsync(InMemoryWorkflowProvider store, InstanceId instanceId)
    {
        var checkpoint = (await store.LoadCheckpointAsync(instanceId, TestContext.Current.CancellationToken)).Value;
        // Emulate a faulty writer that commits a position fact without its checkpoint.
        var append = await store.AppendAsync(new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId), ExpectedVersion = checkpoint.StreamVersion,
            Events = [new WorkflowExecutionPositionChangedEvent
            {
                EventId = EventId.Create("orphan-position"), InstanceId = instanceId,
                CommandId = CommandId.New(), CausationId = CausationId.New(), OccurredAt = DateTimeOffset.UtcNow
            }]
        }, TestContext.Current.CancellationToken);
        Assert.True(append.IsSuccess);
        return checkpoint;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    public async Task LongLoopCountsPositionFactsWithoutAddingCommitBoundaries(int commandBudget)
    {
        const int iterations = 100;
        using var store = new InMemoryWorkflowProvider();
        var wrapped = new InterleavingStore(store, _ => Task.CompletedTask);
        using var services = new ServiceCollection().AddSingleton<Increment>().BuildServiceProvider();
        var runtime = new DurableWorkflowRuntime(new DurableCommandProcessor(wrapped), new DurableDefinitionRegistry(services),
            TimeProvider.System, new DurableDriverBudget(commandBudget, TimeSpan.FromMinutes(1)));
        var definition = Workflow.Durable<LoopState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new LoopState())
            .While(state => state.Value.Count < iterations, body => body.Then<Increment>()).End().Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<int, LoopState>("loop-cost", definition.DefinitionId,
            definition.DefinitionVersion, 0, TestContext.Current.CancellationToken);
        DurableSegmentResult result;
        var turns = 0;
        do
        {
            Assert.True(turns++ < 1000, "The loop must make bounded progress.");
            result = await runtime.DriveAsync(started.InstanceId, TestContext.Current.CancellationToken);
        } while (result.Outcome != DurableSegmentOutcome.Terminal);
        var events = await store.LoadTailAsync(new WorkflowStreamId(started.InstanceId), StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var completed = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, TestContext.Current.CancellationToken);
        Assert.True(completed.Status == WorkflowInstanceStatus.Completed, completed.ErrorSummary);
        Assert.Equal(iterations, events.OfType<WorkflowStepCompletedEvent>().Count());
        var positions = events.OfType<WorkflowExecutionPositionChangedEvent>().ToArray();
        Assert.NotEmpty(positions);
        Assert.All(wrapped.Batches.Where(batch => batch.Checkpoint is not null), batch => Assert.NotEmpty(batch.Events));
        var addedBytes = positions.Sum(item => Encoding.UTF8.GetByteCount(WorkflowEventCodec.Serialize(item)));
        output.WriteLine($"budget={commandBudget}; iterations={iterations}; commits={wrapped.Batches.Count}; " +
            $"events={events.Count}; addedPositionEvents={positions.Length}; addedEventJsonBytes={addedBytes}; addedCommits=0");
    }

    public sealed record State(string Value);
    public sealed class LoopState { public int Count { get; set; } }
    public sealed class Increment : IStep<LoopState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<LoopState> context, CancellationToken cancellationToken)
        {
            context.State.Count++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Theory]
    [InlineData("delay")]
    [InlineData("wait-match")]
    [InlineData("wait-timeout")]
    public async Task CancellationStillWinsWhenAnotherHostCommitsAWakeBeforeFinalization(string wake)
    {
        using var store = new InMemoryWorkflowProvider();
        var primary = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(primary, new DurableDefinitionRegistry(), TimeProvider.System);
        var correlation = CorrelationId.Create("cancel-wake");
        var builder = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(v => new State(v));
        var definition = (wake == "delay" ? builder.Delay(TimeSpan.FromDays(1)) : builder.Wait(
            WorkflowEventContract.Create(EventName.Create("resume"), EventContractVersion.Initial),
            _ => correlation, TimeSpan.FromDays(1))).End().Build();
        runtime.RegisterDefinition(definition);
        var start = await runtime.StartOrGetAsync<string, State>("cancel-wake", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var before = await new DurableAggregateLoader(store).LoadAsync(start.InstanceId, TestContext.Current.CancellationToken);
        var wrapped = new InterleavingStore(store, _ => Task.CompletedTask)
        {
            AfterCancellationCommitted = async () =>
            {
                if (wake == "wait-match")
                    await runtime.RaiseFacadeEventAsync(start.InstanceId, new DurableEventEnvelope
                    {
                        EventId = EventId.Create("cancel-wake-event"), EventName = "resume", CorrelationId = correlation,
                        OccurredAt = DateTimeOffset.UtcNow,
                        Route = new DurableEventRouteEnvelope { Kind = InboxRouteKinds.Direct, InstanceId = start.InstanceId }
                    }, TestContext.Current.CancellationToken, driveAfterDelivery: false);
                else
                    await primary.ProcessAsync(new FireTimerCommand
                    {
                        CommandId = CommandId.New(), InstanceId = start.InstanceId,
                        RequestedAt = DateTimeOffset.UtcNow.AddDays(2), TimerId = Assert.Single(before.TimerState.ActiveTimers).TimerId
                    }, TestContext.Current.CancellationToken);
            }
        };
        await new DurableCommandProcessor(wrapped).ProcessAsync(new CancelWorkflowCommand
        {
            CommandId = CommandId.New(), InstanceId = start.InstanceId, RequestedAt = DateTimeOffset.UtcNow
        }, TestContext.Current.CancellationToken);
        await runtime.DriveAsync(start.InstanceId, TestContext.Current.CancellationToken);
        var after = await new DurableAggregateLoader(store).LoadAsync(start.InstanceId, TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, after.Status);
        var events = await store.LoadTailAsync(new WorkflowStreamId(start.InstanceId), StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(events, item => item is WorkflowCompletedEvent);
    }

    [Fact]
    public async Task BackoffCommittedAfterThePumpCheckKeepsItsWakeRetryable()
    {
        using var store = new InMemoryWorkflowProvider();
        var primary = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(primary, new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var start = await runtime.StartOrGetAsync<string, State>("backoff-race", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var before = await new DurableAggregateLoader(store).LoadAsync(start.InstanceId, TestContext.Current.CancellationToken);
        await primary.ProcessAsync(new FireTimerCommand
        {
            CommandId = CommandId.New(), InstanceId = start.InstanceId,
            RequestedAt = DateTimeOffset.UtcNow.AddDays(2), TimerId = Assert.Single(before.TimerState.ActiveTimers).TimerId
        }, TestContext.Current.CancellationToken);
        var nextEligibleAt = DateTimeOffset.UtcNow.AddDays(1);
        var wrapped = new InterleavingStore(store, async id =>
        {
            var current = await new DurableAggregateLoader(store).LoadAsync(id, TestContext.Current.CancellationToken);
            var checkpoint = (await store.LoadCheckpointAsync(id, TestContext.Current.CancellationToken)).Value;
            var failed = await primary.ProcessAsync(new DurableContinuationAttemptFailedCommand(CommandId.New(), id,
                DateTimeOffset.UtcNow, "competing host failure", checkpoint.StreamVersion, nextEligibleAt, current.StreamVersion),
                TestContext.Current.CancellationToken);
            Assert.Equal(DurableCommandOutcome.Committed, failed.Outcome);
        }) { AdvanceOnCheckpointRead = 2 };
        var workerProcessor = new DurableCommandProcessor(wrapped);
        var worker = new DurableWorkflowRuntime(workerProcessor, new DurableDefinitionRegistry(), TimeProvider.System);
        worker.RegisterDefinition(definition);
        var pump = new DurableContinuationPump(store, worker, workerProcessor);
        Assert.Equal(0, await pump.PumpOnceAsync(new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken));
        var after = await new DurableAggregateLoader(store).LoadAsync(start.InstanceId, TestContext.Current.CancellationToken);
        Assert.False(after.IsTerminal);
        Assert.Equal(nextEligibleAt, after.ContinuationRetryNotBefore);
        Assert.NotEmpty(await store.ClaimAsync(new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1))
            { KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue) }, TestContext.Current.CancellationToken));
        Assert.Empty(wrapped.Batches);
    }

    private sealed class InterleavingStore(
        InMemoryWorkflowProvider inner,
        Func<InstanceId, Task> advanceOtherHost) : IWorkflowEventStore, IWorkflowStartIdempotencyStore
    {
        internal bool AdvanceAfterTail { get; init; }
        internal int AdvanceOnCheckpointRead { get; init; } = 1;
        internal bool RepeatOnEveryCheckpointRead { get; init; }
        internal Func<Task>? AfterCancellationCommitted { get; init; }
        internal Func<ProviderCommitBatch, Task>? BeforeParkAppend { get; init; }
        internal List<Result<AppendEventsResult>> ParkAppendResults { get; } = [];
        internal CheckpointWrite? ForcedCheckpoint { get; init; }
        internal int CheckpointReads { get; private set; }
        internal List<ProviderCommitBatch> Batches { get; } = [];
        public Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(
            WorkflowStreamId streamId, CancellationToken cancellationToken) =>
            inner.LoadDagRunCheckpointAsync(streamId, cancellationToken);

        public Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(
            DagRunStartKeyV1 startKey, CancellationToken cancellationToken) =>
            inner.GetDagRunByStartKeyAsync(startKey, cancellationToken);

        public Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(
            WorkflowStreamId streamId, StreamVersion afterVersion, CancellationToken cancellationToken) =>
            inner.LoadDagRunTailAsync(streamId, afterVersion, cancellationToken);

        public Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(
            DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
            inner.AcquireDagRunLeaseAsync(request, cancellationToken);

        public Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(
            DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
            inner.RenewDagRunLeaseAsync(request, cancellationToken);

        public Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(
            DagRunFenceV1 fence, CancellationToken cancellationToken) =>
            inner.ReleaseDagRunLeaseAsync(fence, cancellationToken);

        public Task CancelDagRunLeaseRequestAsync(
            Guid runId, Guid ownerId, Guid requestId, CancellationToken cancellationToken) =>
            inner.CancelDagRunLeaseRequestAsync(runId, ownerId, requestId, cancellationToken);

        public Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(
            DagRunDiscoveryRequestV1 request, CancellationToken cancellationToken) =>
            inner.ListRecoverableDagRunsAsync(request, cancellationToken);

        public Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(
            DagRunSubscriptionV1 subscription, CancellationToken cancellationToken) =>
            inner.SubscribeDagRunAsync(subscription, cancellationToken);

        public Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId,
            CancellationToken cancellationToken) =>
            inner.UnsubscribeDagRunAsync(subscriptionId, recipientHostId, cancellationToken);

        public Task<int> PruneExpiredDagRunObserversAsync(
            int maxCount, CancellationToken cancellationToken) =>
            inner.PruneExpiredDagRunObserversAsync(maxCount, cancellationToken);

        private int armed = 1;
        public async Task<Option<CheckpointWrite>> LoadCheckpointAsync(InstanceId instanceId, CancellationToken cancellationToken)
        {
            CheckpointReads++;
            if (ForcedCheckpoint is { } forced) return Option<CheckpointWrite>.Some(forced);
            var captured = await inner.LoadCheckpointAsync(instanceId, cancellationToken);
            var tail = await inner.LoadTailAsync(new WorkflowStreamId(instanceId), StreamVersion.Empty, cancellationToken);
            if (!AdvanceAfterTail && CheckpointReads >= AdvanceOnCheckpointRead && tail.Count > 0 &&
                (RepeatOnEveryCheckpointRead || Interlocked.Exchange(ref armed, 0) == 1))
                await advanceOtherHost(instanceId);
            return captured;
        }
        public async Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch, CancellationToken cancellationToken)
        {
            Batches.Add(batch);
            var parking = batch.Events.Any(item => item is WorkflowParkedEvent);
            if (parking && BeforeParkAppend is { } beforePark) await beforePark(batch);
            var result = await inner.AppendAsync(batch, cancellationToken);
            if (parking) ParkAppendResults.Add(result);
            if (AfterCancellationCommitted is { } after && batch.Events.Any(item => item is WorkflowCancellationRequestedEvent))
                await after();
            return result;
        }
        public async Task<IReadOnlyList<global::OrcaCore.Abstractions.Durable.WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            var tail = await inner.LoadTailAsync(streamId, afterVersion, cancellationToken);
            if (AdvanceAfterTail && tail.Count > 0 && Interlocked.Exchange(ref armed, 0) == 1)
                await advanceOtherHost(streamId.InstanceId);
            return tail;
        }

        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            inner.GetStartedAsync(idempotencyKey, cancellationToken);
    }

}
