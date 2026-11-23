using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableStartIdempotencyFacadeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("AC", "AC-328")]
    public async Task FailedInitIsTerminalAcrossHostsWithoutCommittingState(bool throws, bool withDeadline)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryWorkflowProvider();
        var calls = 0;
        var projected = false;
        var builder = Workflow.Durable<LossyInput>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value =>
            {
                calls++;
                return throws ? throw new InvalidOperationException("init callback failed") : LossyInput.Of(value);
            });
        if (withDeadline)
        {
            builder.CompleteWithin(TimeSpan.FromMinutes(1));
        }

        var definition = builder.End(snapshot =>
        {
            projected = true;
            return snapshot.Value.Value;
        }).Build();
        var handle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("failed-init");
        var started = Assert.IsType<WorkflowStartResult<WorkflowInstanceHandle<int>>.Accepted>(
            await handle.StartOrGetAsync(17, key, token));
        Assert.False(started.WasExisting);
        var instance = started.Handle;
        var snapshot = await instance.GetSnapshotAsync(token);
        Assert.Equal(WorkflowInstanceStatus.Failed, snapshot.Status);
        var failure = Assert.IsType<WorkflowFailure>(snapshot.Failure);
        Assert.Contains("Init", failure.Message);
        Assert.Contains(throws ? "init callback failed" : nameof(LossyInput), failure.Message);
        var unavailable = Assert.IsType<WorkflowOutputResult<int>.Unavailable>(await instance.GetOutputAsync(token));
        Assert.Equal(WorkflowInstanceStatus.Failed, unavailable.Status);
        Assert.Equal(failure.Message, unavailable.Failure?.Message);
        var waitError = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(() => instance.WaitForOutputAsync(token).AsTask());
        Assert.Equal(failure.Message, waitError.Failure?.Message);

        foreach (var registry in new[] { handle, CreateRegistry(store).Register(definition).GetHandleOrThrow() })
        {
            var replay = Assert.IsType<WorkflowStartResult<WorkflowInstanceHandle<int>>.Accepted>(
                await registry.StartOrGetAsync(17, key, token));
            Assert.True(replay.WasExisting);
            Assert.Equal(instance.InstanceId, replay.Handle.InstanceId);
            Assert.Equal(failure.Message, (await replay.Handle.GetSnapshotAsync(token)).Failure?.Message);
        }

        Assert.Equal(1, calls);
        Assert.False(projected);
        Assert.False((await store.LoadCheckpointAsync(instance.InstanceId, token)).HasValue);
        var events = await store.LoadTailAsync(new global::OrcaCore.Abstractions.Ids.WorkflowStreamId(instance.InstanceId),
            global::OrcaCore.Abstractions.Ids.StreamVersion.Empty, token);
        Assert.Collection(events,
            startedEvent => Assert.IsType<global::OrcaCore.Abstractions.Durable.WorkflowStartedEvent>(startedEvent),
            terminal => Assert.IsType<global::OrcaCore.Abstractions.Durable.WorkflowTerminalEvent>(terminal));
    }

    [Theory]
    [InlineData("queued", 0)]
    [InlineData("input decode", 0)]
    [InlineData("Init returns", 1)]
    [InlineData("Init throws", 1)]
    [Trait("AC", "AC-328")]
    public async Task InitDeadlineDiscardsLateWorkWithoutACheckpoint(string expiry, int expectedCalls)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new global::OrcaCore.TestSupport.Clock(DateTimeOffset.UnixEpoch);
        var budget = TimeSpan.FromMinutes(1);
        var store = new InMemoryWorkflowProvider();
        var calls = 0;
        var authored = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<DeadlineInput>(input =>
            {
                calls++;
                clock.Advance(budget);
                return expiry == "Init throws" ? throw new InvalidOperationException("too late") : new State(input.Value);
            }).CompleteWithin(budget).End().Build();
        var processor = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(), clock.TimeProvider);
        runtime.RegisterDefinition(authored);
        var input = DurableStartInput.Capture(new DeadlineInput(17));
        var start = await new DurableStartService(processor).StartOrGetAsync(
            new StartOrGetRequest("late-init", authored.DefinitionId, authored.DefinitionVersion,
                authored.DefinitionFingerprint.Value, input.Fingerprint.Value, input.Payload, clock.Now), token);
        if (expiry == "queued")
        {
            clock.Advance(budget);
        }

        try
        {
            if (expiry == "input decode")
            {
                DeadlineInput.OnConstruction.Value = () => clock.Advance(budget);
            }
            Assert.Equal(DurableSegmentOutcome.Terminal,
                (await runtime.DriveAsync(start.InstanceId!, DurableDriveMode.Required, token)).Outcome);
        }
        finally
        {
            DeadlineInput.OnConstruction.Value = null;
        }

        Assert.Equal(expectedCalls, calls);
        var snapshot = (await store.GetAsync(start.InstanceId!, token)).Value;
        Assert.Equal(WorkflowInstanceStatus.TimedOut, snapshot.Status);
        Assert.Contains("WF-DEADLINE-EXCEEDED", snapshot.ErrorSummary);
        Assert.False((await store.LoadCheckpointAsync(start.InstanceId!, token)).HasValue);
    }

    private sealed class DeadlineInput
    {
        internal static AsyncLocal<Action?> OnConstruction { get; } = new();
        public int Value { get; }

        public DeadlineInput(int value)
        {
            OnConstruction.Value?.Invoke();
            Value = value;
        }
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task StaleInitFailureCannotOverwriteAnotherHostsWaitingCheckpoint()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryWorkflowProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    entered.SetResult();
                    release.Wait(token);
                    throw new InvalidOperationException("stale initialization");
                }
                return new State(value);
            })
            .Wait(WorkflowEventContract.Create(EventName.Create("resume"), EventContractVersion.Initial),
                _ => CorrelationId.Create("init-race"))
            .End().Build();
        var firstHost = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var secondHost = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("init-race");
        var firstStart = Task.Run(async () => await firstHost.StartOrGetAsync(17, key, token), token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            var existing = Assert.IsType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>(
                await secondHost.StartOrGetAsync(17, key, token));
            Assert.True(existing.WasExisting);
            Assert.Equal(WorkflowInstanceStatus.Waiting, (await existing.Handle.GetSnapshotAsync(token)).Status);
        }
        finally
        {
            release.Set();
        }

        var owner = (await firstStart).GetHandleOrThrow();
        Assert.Equal(2, calls);
        Assert.Equal(WorkflowInstanceStatus.Waiting, (await owner.GetSnapshotAsync(token)).Status);
        Assert.Equal(17, (await owner.GetStateAsync<State>(token)).Value);
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task LossyInputIsRejectedBeforeBindingANewOrExistingStartKey()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<LossyInput>(input => new State(input.Value)).End().Build();
        var handle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("lossy-input");
        await Assert.ThrowsAsync<NotSupportedException>(() => handle.StartOrGetAsync(LossyInput.Of(17), key, token).AsTask());
        Assert.False((await store.GetStartedAsync(key.Value, token)).HasValue);

        var owner = (await handle.StartOrGetAsync(LossyInput.Of(0), key, token)).GetHandleOrThrow();
        await Assert.ThrowsAsync<NotSupportedException>(() => handle.StartOrGetAsync(LossyInput.Of(17), key, token).AsTask());
        Assert.Equal(owner.InstanceId, (await store.GetStartedAsync(key.Value, token)).Value.InstanceId);
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task StartKeyAndInitUseTheSameCapturedInputBytesAcrossHosts()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<ReadCountingInput>(input => new State(input.Value)).End().Build();
        var handle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("single-capture");
        var input = new ReadCountingInput { AdvanceOnRead = true };
        var instance = (await handle.StartOrGetAsync(input, key, token)).GetHandleOrThrow();
        var committed = await instance.GetStateAsync<State>(token);
        var replay = new ReadCountingInput { Value = committed.Value };

        foreach (var registry in new[] { handle, CreateRegistry(store).Register(definition).GetHandleOrThrow() })
        {
            var existing = Assert.IsType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>(
                await registry.StartOrGetAsync(replay, key, token));
            Assert.True(existing.WasExisting);
            Assert.Equal(instance.InstanceId, existing.Handle.InstanceId);
        }
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task ExplicitNullInputPersistsTheBytesThatBoundTheStartKey()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int?>(input => new State(input ?? 17)).End().Build();
        var handle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(null, StartIdempotencyKey.Create("present-null"), token)).GetHandleOrThrow();
        var events = await store.LoadTailAsync(new global::OrcaCore.Abstractions.Ids.WorkflowStreamId(instance.InstanceId),
            global::OrcaCore.Abstractions.Ids.StreamVersion.Empty, token);
        var started = Assert.Single(events.OfType<global::OrcaCore.Abstractions.Durable.WorkflowStartedEvent>());
        Assert.Equal("orcacore-json-v1", started.InputContentType);
        Assert.Equal("null"u8.ToArray(), started.InputPayload);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("null"u8)), started.InputFingerprint);
        Assert.Equal(17, (await instance.GetStateAsync<State>(token)).Value);
    }

    private sealed class LossyInput
    {
        public int Value { get; private set; }
        public int Revision { get; init; }
        internal static LossyInput Of(int value) => new() { Value = value, Revision = value };
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task LossyDetachedReplacementFailsItsDurableStepAndPreservesTheCheckpoint()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<LossyInput>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => LossyInput.Of(0)).Then<ReplaceWithLossyState>().End().Build();
        using var services = new ServiceCollection().AddSingleton(new ReplaceWithLossyState()).BuildServiceProvider();
        var owner = (await CreateRegistry(store, new DurableDefinitionRegistry(services)).Register(definition).GetHandleOrThrow()
            .StartOrGetAsync(0, StartIdempotencyKey.Create("lossy-state"), token)).GetHandleOrThrow();
        var snapshot = await owner.GetSnapshotAsync(token);
        Assert.Equal(WorkflowInstanceStatus.Failed, snapshot.Status);
        Assert.Contains(nameof(LossyInput), Assert.IsType<WorkflowFailure>(snapshot.Failure).Message);
        var committed = await owner.GetStateAsync<LossyInput>(token);
        Assert.Equal(0, committed.Value);
        Assert.Equal(0, committed.Revision);
    }

    private sealed class ReplaceWithLossyState : IStep<LossyInput>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<LossyInput> context, CancellationToken token)
        {
            context.ReplaceState(LossyInput.Of(17));
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ReadCountingInput
    {
        private int value;
        internal bool AdvanceOnRead { get; init; }
        public int Value { get => AdvanceOnRead ? ++value : value; set => this.value = value; }
    }

    [Fact]
    public async Task ReplacementHost_SameKeyWithChangedInput_ReturnsConflictWithoutAnIncompatibleHandle()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
        var key = StartIdempotencyKey.Create("replacement-host-input-conflict");

        var firstHandle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var firstStart = await firstHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);
        var firstInstance = firstStart.GetHandleOrThrow().InstanceId;

        var replacementHandle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var conflict = await replacementHandle.StartOrGetAsync(
            new Input(2),
            key,
            TestContext.Current.CancellationToken);

        conflict.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Conflict>();
        var getIncompatibleHandle = () => conflict.GetHandleOrThrow();
        getIncompatibleHandle.Should().Throw<WorkflowStartIdempotencyConflictException>();

        var compatibleReplay = await replacementHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);
        compatibleReplay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        compatibleReplay.GetHandleOrThrow().InstanceId.Should().Be(firstInstance);
    }

    [Fact]
    public async Task ReplacementHost_SameKeyWithChangedDefinitionFingerprint_ReturnsConflict()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var original = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("original-outcome"))
            .Build();
        var changed = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("changed-outcome"))
            .Build();
        original.DefinitionFingerprint.Should().NotBe(changed.DefinitionFingerprint);
        var key = StartIdempotencyKey.Create("replacement-host-definition-conflict");

        var firstHandle = CreateRegistry(store).Register(original).GetHandleOrThrow();
        _ = await firstHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);

        var replacementHandle = CreateRegistry(store).Register(changed).GetHandleOrThrow();
        var conflict = await replacementHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);

        conflict.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Conflict>();
        var getIncompatibleHandle = () => conflict.GetHandleOrThrow();
        getIncompatibleHandle.Should().Throw<WorkflowStartIdempotencyConflictException>();
    }

    private static DurableWorkflowDefinitionRegistry CreateRegistry(InMemoryWorkflowProvider store,
        DurableDefinitionRegistry? definitions = null)
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            definitions ?? new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
        return new DurableWorkflowDefinitionRegistry(
            runtime,
            store,
            store,
            processor,
            notifications,
            TimeProvider.System);
    }

    private sealed record Input(int Value);
    private sealed record State(int Value);
}
