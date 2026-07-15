using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableStructuredFiberDriverTests
{
    private static readonly CorrelationId WaitCorrelation = new("fiber-wait");

    [Fact]
    public async Task SelectedParallel_UsesConfiguredSerializerRegistryAtRuntime()
    {
        var registry = new TrackingSerializerRegistry();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithTypeSerializerRegistry(registry)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches.Branch<YieldingBranchState>(
                    "tracked",
                    _ => new YieldingBranchState { Name = "tracked" },
                    branch => branch.Return(state => state.Value.Name)),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("done")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-custom-serializer",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        registry.SerializeCalls.Should().BeGreaterThan(0);
        registry.DeserializeCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectedParallel_OversizedResultFailsOwningScopeBeforeCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions { MaxSerializedResultBytes = 8 })
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches.Branch<YieldingBranchState>(
                    "oversized",
                    _ => new YieldingBranchState { Name = new string('x', 64) },
                    branch => branch.Return(state => state.Value.Name)),
                (parent, _) => parent.Value)
            .End("unreachable")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var failed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-oversized-result",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            failed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = failed.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain(StructuredExecutionLimitCodes.SerializedResultExceeded);
        envelope.Scopes.Single().CommittedResults.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedLinearWorkflow_CompletesThroughFormat2FiberCheckpoint()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store),
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then<AppendStep>()
            .End("done")
            .Build();
        runtime.RegisterDefinition(definition);

        var started = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-linear",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.EndOutcomeName.Should().Be("done");
        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.ContentType.Should().Be(DurableExecutionEnvelopeV2.ContentType);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
        envelope.Fibers.Single(fiber => fiber.FiberId == envelope.RootFiberId).Phase
            .Should().Be(DurableFiberPhase.Completed);
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("step");
    }

    [Fact]
    public async Task SelectedLinear_OversizedEnvelopeIsRejectedBeforeFormat2CheckpointCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store);
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions { MaxSerializedEnvelopeBytes = 1 })
            .Init<string>(value => new TestState { Value = value })
            .End("unreachable")
            .Build();
        runtime.RegisterDefinition(definition);

        var start = async () => await runtime.StartOrGetAsync<string, TestState>(
            "fiber-oversized-envelope",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<StructuredExecutionLimitException>()
            .WithMessage("*exceeding the configured limit*");
        var projection = (await store.ListAsync(
            new WorkflowProjectionQuery { DefinitionId = definition.DefinitionId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            projection.InstanceId,
            TestContext.Current.CancellationToken);
        checkpoint.HasValue.Should().BeFalse(
            "an oversized format-2 transition must not be committed");
    }

    [Fact]
    public async Task SelectedStructuralChain_EnforcesInternalInstructionQuantumBudget()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store);
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions
            {
                MaxInternalInstructionsPerQuantum = 1
            })
            .Init<string>(value => new TestState { Value = value })
            .If(_ => true, _ => { })
            .End("done")
            .Build();
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-internal-budget",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        envelope.Diagnostics.ForcedRotations.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectedWait_ResumesOwningFiberFromFormat2CheckpointAfterHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then<WaitStep>()
            .Then<CaptureResumedEventStep>()
            .End("resumed")
            .Build();
        var firstRuntime = CreateRuntime(store);
        firstRuntime.RegisterDefinition(definition);

        var waiting = await firstRuntime.StartOrGetAsync<string, TestState>(
            "fiber-wait",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var waitingSnapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var waitingCheckpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);

        waitingSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
        waitingSnapshot.ActiveWaits.Should().ContainSingle();
        var waitingEnvelope = DurableExecutionEnvelopeV2.Deserialize(waitingCheckpoint!.Value.Payload);
        var rootFiberId = new FiberId(waitingEnvelope.RootFiberId);
        waitingEnvelope.OwnedObligations.Should().ContainSingle(obligation =>
            obligation.Kind == DurableOwnedObligationKind.Wait &&
            obligation.FiberId == waitingEnvelope.RootFiberId);
        waitingSnapshot.ActiveWaits.Single().FiberId.Should().Be(rootFiberId);
        waitingSnapshot.ActiveWaits.Single().ScopeId.Should().BeNull();

        var replacementRuntime = CreateRuntime(store);
        replacementRuntime.RegisterDefinition(definition);
        await replacementRuntime.RaiseEventAsync(
            waiting.InstanceId,
            "Continue",
            WaitCorrelation,
            "payload",
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var completedCheckpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var completedEnvelope = DurableExecutionEnvelopeV2.Deserialize(completedCheckpoint!.Value.Payload);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        completedEnvelope.OwnedObligations.Should().BeEmpty();
        JsonSerializer.Deserialize<TestState>(completedEnvelope.StatePayload)!.Log.Should().Equal("payload");
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitRegisteredEvent>().Single().FiberId.Should().Be(rootFiberId);
        events.OfType<WorkflowWaitMatchedEvent>().Single().FiberId.Should().Be(rootFiberId);
        events.OfType<WorkflowResumeConsumedEvent>().Single().FiberId.Should().Be(rootFiberId);
    }

    [Fact]
    public async Task SelectedWaitLong_CommitsColdOwnedWaitAndResumesAfterHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WaitLong("Approved", _ => new CorrelationId("order-42"))
            .Then<ObserveExternalCompletionStep>()
            .End("approved")
            .Build();
        var firstHost = CreateRuntime(store);
        firstHost.RegisterDefinition(definition);
        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-wait-long",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "order",
            TestContext.Current.CancellationToken);
        var waitingSnapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        waitingSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
        waitingSnapshot.ActiveWaits.Should().ContainSingle(wait =>
            wait.EventName == "Approved" &&
            wait.Mode == WaitMode.Cold.ToString() &&
            wait.FiberId.HasValue);

        var replacement = CreateRuntime(store);
        replacement.RegisterDefinition(definition);
        await replacement.RaiseEventAsync(
            waiting.InstanceId,
            "Approved",
            new CorrelationId("order-42"),
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedDelay_FiredTimerResumesOnceAfterHostReplacement()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Delay(TimeSpan.FromMinutes(5))
            .Then<AppendStep>()
            .End("delayed")
            .Build();
        var firstHost = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store),
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            new JsonWorkflowPayloadSerializer());
        firstHost.RegisterDefinition(definition);
        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-delay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "order",
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(5));
        var due = await store.ClaimDueAsync(
            new TimerClaimRequest(clock.Now, 10, clock.Now, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        var replacementProcessor = new DurableCommandProcessor(store);
        foreach (var fire in due)
        {
            await replacementProcessor.ProcessAsync(fire, TestContext.Current.CancellationToken);
            await store.CompleteAsync(fire.TimerId, TestContext.Current.CancellationToken);
        }

        var replacement = new DurableWorkflowRuntime(
            replacementProcessor,
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            new JsonWorkflowPayloadSerializer());
        replacement.RegisterDefinition(definition);
        await replacement.StartOrGetAsync<string, TestState>(
            "fiber-delay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

        due.Should().ContainSingle();
        completed.Status.Should().Be(WorkflowStatus.Completed);
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("step");
        envelope.OwnedObligations.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedLoopWait_RegistrationSequenceRemainsMonotonicAfterPriorWaitRemoval()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store);
        var definition = Workflow.Durable<WaitSequenceState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new WaitSequenceState())
            .While(
                state => state.Count < 2,
                loop => loop
                    .Wait("Continue", _ => WaitCorrelation)
                    .Then<IncrementWaitSequenceStep>())
            .End("done")
            .Build();
        runtime.RegisterDefinition(definition);

        var started = await runtime.StartOrGetAsync<string, WaitSequenceState>(
            "fiber-monotonic-wait-sequence",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var first = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single().ActiveWaits.Single();

        await runtime.RaiseEventAsync(
            started.InstanceId,
            "Continue",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);
        var second = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single().ActiveWaits.Single();
        var checkpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

        second.WaitSequence.Should().BeGreaterThan(first.WaitSequence);
        envelope.NextRegistrationSequence.Should().Be(second.WaitSequence + 1);

        await runtime.RaiseEventAsync(
            started.InstanceId,
            "Continue",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);
        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        completed.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task SelectedWaitThenDelay_PreservesResumeUntilFollowingStepAcrossHostReplacement()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Wait("Continue", _ => WaitCorrelation)
            .Delay(TimeSpan.FromMinutes(5))
            .Then<CaptureResumedEventStep>()
            .End("resumed")
            .Build();
        var firstHost = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store),
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            new JsonWorkflowPayloadSerializer());
        firstHost.RegisterDefinition(definition);
        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-wait-delay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "order",
            TestContext.Current.CancellationToken);

        await firstHost.RaiseEventAsync(
            waiting.InstanceId,
            "Continue",
            WaitCorrelation,
            "through-delay",
            cancellationToken: TestContext.Current.CancellationToken);
        var delayedCheckpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var delayedEnvelope = DurableExecutionEnvelopeV2.Deserialize(delayedCheckpoint!.Value.Payload);

        delayedEnvelope.Fibers.Single(fiber => fiber.FiberId == delayedEnvelope.RootFiberId)
            .ResumeFromWaitId.Should().NotBeNull();
        delayedEnvelope.OwnedObligations.Should().Contain(obligation =>
            obligation.Kind == DurableOwnedObligationKind.PendingResume);
        delayedEnvelope.OwnedObligations.Should().Contain(obligation =>
            obligation.Kind == DurableOwnedObligationKind.Timer);
        delayedCheckpoint.Value.RuntimeState.PendingResumes.Should().ContainSingle();

        clock.Advance(TimeSpan.FromMinutes(5));
        var due = await store.ClaimDueAsync(
            new TimerClaimRequest(clock.Now, 10, clock.Now, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        var replacementProcessor = new DurableCommandProcessor(store);
        foreach (var fire in due)
        {
            await replacementProcessor.ProcessAsync(fire, TestContext.Current.CancellationToken);
            await store.CompleteAsync(fire.TimerId, TestContext.Current.CancellationToken);
        }
        var firedAggregate = await new DurableAggregateLoader(store).LoadAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);

        firedAggregate.WaitState.PendingResumes.Should().ContainSingle();

        var replacementHost = new DurableWorkflowRuntime(
            replacementProcessor,
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            new JsonWorkflowPayloadSerializer());
        replacementHost.RegisterDefinition(definition);
        await replacementHost.StartOrGetAsync<string, TestState>(
            "fiber-wait-delay",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        envelope.OwnedObligations.Should().BeEmpty();
        events.OfType<WorkflowResumeConsumedEvent>().Should().ContainSingle();
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("through-delay");
    }

    [Fact]
    public async Task SelectedExternalJob_ResumesFollowingStepOnceAfterHostReplacement()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then(() => new DispatchExternalJobStep(trace))
            .Then<ObserveExternalCompletionStep>()
            .End("job-done")
            .Build();
        var firstHost = CreateRuntime(store);
        firstHost.RegisterDefinition(definition);

        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-external-job",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "job-1",
            TestContext.Current.CancellationToken);
        var waitingSnapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        waitingSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
        waitingSnapshot.ActiveWaits.Should().ContainSingle(wait =>
            wait.EventName == "ExternalJobCompleted" &&
            wait.FiberId.HasValue);
        trace.Should().Equal("dispatch");

        var replacementProcessor = new DurableCommandProcessor(store);
        var replacementHost = new DurableWorkflowRuntime(
            replacementProcessor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        replacementHost.RegisterDefinition(definition);
        var completionEventId = EventId.New();
        await replacementProcessor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = waiting.InstanceId,
                RequestedAt = TimeProvider.System.GetUtcNow(),
                ExternalJobId = "job-1",
                CompletionEventId = completionEventId
            },
            TestContext.Current.CancellationToken);
        await replacementHost.StartOrGetAsync<string, TestState>(
            "fiber-external-job",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

        await replacementProcessor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = waiting.InstanceId,
                RequestedAt = TimeProvider.System.GetUtcNow(),
                ExternalJobId = "job-1",
                CompletionEventId = completionEventId
            },
            TestContext.Current.CancellationToken);
        await replacementHost.StartOrGetAsync<string, TestState>(
            "fiber-external-job",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        trace.Should().Equal("dispatch");
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("completed");
        envelope.OwnedObligations.Should().BeEmpty();
        events.OfType<WorkflowExternalJobCompletedEvent>().Should().ContainSingle();
        events.OfType<WorkflowResumeConsumedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task SelectedExternalJob_DuplicateDispatchRepairsCommittedWaitInsteadOfParking()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then(() => new DispatchExternalJobStep(trace))
            .Then<ObserveExternalCompletionStep>()
            .End("job-done")
            .Build();
        var firstHost = CreateRuntime(store);
        firstHost.RegisterDefinition(definition);
        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-external-job-duplicate",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "job-1",
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var committed = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var root = committed.Fibers.Single(fiber => fiber.FiberId == committed.RootFiberId);
        var stale = committed with
        {
            Fibers = committed.Fibers
                .Select(fiber => fiber.FiberId == committed.RootFiberId
                    ? fiber with
                    {
                        Phase = DurableFiberPhase.Runnable,
                        Blocked = null
                    }
                    : fiber)
                .ToArray(),
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = [root.FiberId],
                NextFiberId = root.FiberId
            },
            OwnedObligations = []
        };
        var processor = new DurableCommandProcessor(store);
        var staleCommit = await processor.ProcessAsync(
            new DurableStepCompletedCommand(
                CommandId.New(),
                waiting.InstanceId,
                TimeProvider.System.GetUtcNow(),
                "simulate-dispatch-response-loss",
                stale)
            {
                ExpectedStreamVersion = checkpoint.Value.StreamVersion
            },
            TestContext.Current.CancellationToken);
        staleCommit.Outcome.Should().Be(DurableCommandOutcome.Committed);

        var replacement = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        replacement.RegisterDefinition(definition);
        await replacement.StartOrGetAsync<string, TestState>(
            "fiber-external-job-duplicate",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var repaired = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var repairedCheckpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var repairedEnvelope = DurableExecutionEnvelopeV2.Deserialize(repairedCheckpoint!.Value.Payload);

        repaired.Status.Should().Be(WorkflowStatus.Waiting);
        repaired.ActiveWaits.Should().ContainSingle(wait => wait.EventName == "ExternalJobCompleted");
        repairedEnvelope.OwnedObligations.Should().ContainSingle(obligation =>
            obligation.Kind == DurableOwnedObligationKind.ExternalJob);
        trace.Should().Equal("dispatch", "dispatch");

        await processor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = waiting.InstanceId,
                RequestedAt = TimeProvider.System.GetUtcNow(),
                ExternalJobId = "job-1",
                CompletionEventId = EventId.New()
            },
            TestContext.Current.CancellationToken);
        await replacement.StartOrGetAsync<string, TestState>(
            "fiber-external-job-duplicate",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        completed.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task SelectedResourceGrant_ReexecutesAcquireThenAdvancesWithoutPhantomWait()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("db", 1, null),
            TestContext.Current.CancellationToken);
        var otherInstanceId = InstanceId.New();
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                otherInstanceId,
                "other",
                [new ResourcePoolRequirement("db", 1)],
                TimeProvider.System.GetUtcNow(),
                null),
            TestContext.Current.CancellationToken);
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then(() => new AcquireResourceStep(trace))
            .Then<AppendStep>()
            .End("resource-done")
            .Build();
        var firstProcessor = new DurableCommandProcessor(store, pools);
        var firstHost = new DurableWorkflowRuntime(
            firstProcessor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        firstHost.RegisterDefinition(definition);

        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-resource",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "holder",
            TestContext.Current.CancellationToken);
        var waitingSnapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        waitingSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
        waitingSnapshot.ActiveWaits.Should().ContainSingle(wait =>
            wait.EventName == "ResourcePoolGranted" &&
            wait.FiberId.HasValue);
        trace.Should().Equal("acquire");

        await pools.ReleaseAsync(
            new ResourcePoolReleaseRequest(
                otherInstanceId,
                "other",
                TimeProvider.System.GetUtcNow()),
            TestContext.Current.CancellationToken);
        var replacementProcessor = new DurableCommandProcessor(store, pools);
        var replacementHost = new DurableWorkflowRuntime(
            replacementProcessor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        replacementHost.RegisterDefinition(definition);
        await replacementHost.RaiseEventAsync(
            waiting.InstanceId,
            "ResourcePoolGranted",
            new CorrelationId("holder"),
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        trace.Should().Equal("acquire", "acquire");
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("step");
        envelope.OwnedObligations.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedResourceImmediateGrant_OneCommandBudgetRecoversWithoutReacquiring()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("db", 1, null),
            TestContext.Current.CancellationToken);
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then(() => new AcquireResourceStep(trace))
            .Then<AppendStep>()
            .End("resource-done")
            .Build();
        var budget = new DurableDriverBudget(1, TimeSpan.FromSeconds(30));

        var starter = CreateRuntime(store, budget, pools);
        starter.RegisterDefinition(definition);
        var started = await starter.StartOrGetAsync<string, TestState>(
            "fiber-resource-immediate",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "holder",
            TestContext.Current.CancellationToken);

        var grantedCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var grantedEnvelope = DurableExecutionEnvelopeV2.Deserialize(grantedCheckpoint!.Value.Payload);
        var grantedFiber = grantedEnvelope.Fibers.Single(fiber =>
            fiber.Blocked?.Reason == DurableFiberBlockedReason.Resource);

        trace.Should().Equal("acquire");
        grantedEnvelope.OwnedObligations.Should().ContainSingle(obligation =>
            obligation.Kind == DurableOwnedObligationKind.Resource &&
            obligation.ObligationId == grantedFiber.Blocked!.ObligationId);

        for (var segment = 0; segment < 3; segment++)
        {
            var replacement = CreateRuntime(store, budget, pools);
            replacement.RegisterDefinition(definition);
            await replacement.StartOrGetAsync<string, TestState>(
                "fiber-resource-immediate",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "holder",
                TestContext.Current.CancellationToken);
        }

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var completedCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var completedEnvelope = DurableExecutionEnvelopeV2.Deserialize(completedCheckpoint!.Value.Payload);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        trace.Should().Equal("acquire");
        JsonSerializer.Deserialize<TestState>(completedEnvelope.StatePayload)!.Log.Should().Equal("step");
        completedEnvelope.OwnedObligations.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedRunChildren_ResumesParentFiberOnceAfterWhenAllAcrossHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var childDefinitionId = DefinitionId.New();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .RunChildren(
                childDefinitionId,
                DefinitionVersion.Initial,
                _ => ["first", "second"],
                maxConcurrency: 2,
                failurePolicy: RunChildFailurePolicy.PropagateFailure,
                joinPolicy: RunChildrenJoinPolicy.WhenAll,
                residualPolicy: RunChildrenResidualPolicy.CancelRemaining)
            .Then<AppendStep>()
            .End("children-done")
            .Build();
        var firstHost = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        firstHost.RegisterDefinition(definition);

        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-child-group",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "parent",
            TestContext.Current.CancellationToken);
        var scheduledEvents = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var scheduled = scheduledEvents.OfType<WorkflowChildrenScheduledEvent>()
            .Should().ContainSingle().Which;

        scheduled.FiberId.Should().NotBeNull();
        scheduled.ScopeId.Should().BeNull();
        scheduled.Children.Should().HaveCount(2);
        await processor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandId.New(),
                waiting.InstanceId,
                DateTimeOffset.UtcNow,
                scheduled.Children[0].ChildInstanceId,
                WorkflowStatus.Completed,
                null),
            TestContext.Current.CancellationToken);

        var replacementProcessor = new DurableCommandProcessor(store);
        var replacementHost = new DurableWorkflowRuntime(
            replacementProcessor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        replacementHost.RegisterDefinition(definition);
        await replacementHost.StartOrGetAsync<string, TestState>(
            "fiber-child-group",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var afterFirst = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        afterFirst.Status.Should().Be(WorkflowStatus.Waiting);
        afterFirst.ActiveWaits.Should().ContainSingle();

        await replacementProcessor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandId.New(),
                waiting.InstanceId,
                DateTimeOffset.UtcNow,
                scheduled.Children[1].ChildInstanceId,
                WorkflowStatus.Completed,
                null),
            TestContext.Current.CancellationToken);
        await replacementHost.StartOrGetAsync<string, TestState>(
            "fiber-child-group",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "ignored",
            TestContext.Current.CancellationToken);
        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        envelope.OwnedObligations.Should().BeEmpty();
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("step");
        events.OfType<WorkflowParentResumeTokenRecordedEvent>().Should().ContainSingle();
        events.OfType<WorkflowParentResumeTokenConsumedEvent>().Should().ContainSingle();
        events.OfType<WorkflowStepCompletedEvent>().Should().ContainSingle(workflowEvent =>
            workflowEvent.StepPath == "root/2");
    }

    [Fact]
    public async Task SelectedRunChildren_ParentCancellationEmitsChildCancelBeforeTerminal()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .RunChildren(
                DefinitionId.New(),
                DefinitionVersion.Initial,
                _ => ["first", "second"],
                maxConcurrency: 2)
            .End("unreachable")
            .Build();
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        runtime.RegisterDefinition(definition);
        var waiting = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-child-cancel",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "parent",
            TestContext.Current.CancellationToken);
        var commandId = CommandId.New();

        await processor.ProcessAsync(
            new CancelWorkflowCommand
            {
                CommandId = commandId,
                InstanceId = waiting.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);
        var cancelled = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var events = (await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken)).ToList();
        var outbox = await store.ClaimAsync(20, TestContext.Current.CancellationToken);
        var residualIndex = events.FindIndex(workflowEvent =>
            workflowEvent is WorkflowChildResidualIntentRecordedEvent { CommandId: var id } &&
            id == commandId);
        var terminalIndex = events.FindIndex(workflowEvent =>
            workflowEvent is WorkflowTerminalEvent
            {
                CommandId: var id,
                Status: WorkflowStatus.Cancelled
            } && id == commandId);

        cancelled.Status.Should().Be(WorkflowStatus.Cancelled);
        cancelled.ActiveWaits.Should().BeEmpty();
        residualIndex.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(terminalIndex);
        events.OfType<WorkflowChildResidualIntentRecordedEvent>()
            .Should().ContainSingle(workflowEvent => workflowEvent.CommandId == commandId)
            .Which.ResidualChildInstanceIds.Should().HaveCount(2);
        outbox.Count(record => record.Kind == "child-cancel").Should().Be(2);
    }

    [Fact]
    public async Task SelectedRunChildren_PropagatedFailureCancelsOnlyStillActiveSiblings()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .RunChildren(
                DefinitionId.New(),
                DefinitionVersion.Initial,
                _ => ["first", "second"],
                maxConcurrency: 2,
                failurePolicy: RunChildFailurePolicy.PropagateFailure)
            .End("unreachable")
            .Build();
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        runtime.RegisterDefinition(definition);
        var waiting = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-child-failure",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "parent",
            TestContext.Current.CancellationToken);
        var scheduled = (await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .Single();
        var failedChildId = scheduled.Children[0].ChildInstanceId;
        var siblingId = scheduled.Children[1].ChildInstanceId;

        await processor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandId.New(),
                waiting.InstanceId,
                DateTimeOffset.UtcNow,
                failedChildId,
                WorkflowStatus.Failed,
                "child failed"),
            TestContext.Current.CancellationToken);
        var failed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var events = (await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken)).ToList();
        var residual = events.OfType<WorkflowChildResidualIntentRecordedEvent>()
            .Should().ContainSingle().Which;
        var outbox = await store.ClaimAsync(20, TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ActiveWaits.Should().BeEmpty();
        residual.ResidualChildInstanceIds.Should().Equal(siblingId);
        residual.ResidualChildInstanceIds.Should().NotContain(failedChildId);
        outbox.Count(record => record.Kind == "child-cancel").Should().Be(1);
    }

    [Fact]
    public async Task SelectedParallel_YieldRotatesIsolatedFibersAndMergesAuthoredResults()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<YieldingBranchState>(
                        "first",
                        _ => new YieldingBranchState { Name = "first" },
                        branch => branch
                            .Then(() => new YieldOnceBranchStep(trace))
                            .Return(state => $"{state.Value.Name}:{state.Value.Attempts}"))
                    .Branch<YieldingBranchState>(
                        "second",
                        _ => new YieldingBranchState { Name = "second" },
                        branch => branch
                            .Then(() => new YieldOnceBranchStep(trace))
                            .Return(state => $"{state.Value.Name}:{state.Value.Attempts}")),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("merged")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-parallel",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        trace.Should().Equal("first:yield", "second:yield", "first:complete", "second:complete");
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log
            .Should().Equal("first:2", "second:2");
        envelope.Scopes.Should().BeEmpty();
        envelope.Fibers.Should().ContainSingle(fiber => fiber.FiberId == envelope.RootFiberId);
    }

    [Fact]
    public async Task SelectedParallel_ProjectionRemainsRunningWhileSiblingWaitsAndAnotherFiberExecutes()
    {
        var blockingStep = new BlockingDurableBranchStep();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "waiting",
                        _ => new WaitingBranchState { Name = "waiting", EventName = "Continue" },
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<YieldingBranchState>(
                        "running",
                        _ => new YieldingBranchState { Name = "running" },
                        branch => branch
                            .Then(() => blockingStep)
                            .Return(state => state.Value.Name)),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("merged")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var start = runtime.StartOrGetAsync<string, TestState>(
            "fiber-status",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        await blockingStep.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var running = (await store.ListAsync(
            new WorkflowProjectionQuery { DefinitionId = definition.DefinitionId },
            TestContext.Current.CancellationToken)).Single();

        running.Status.Should().Be(WorkflowStatus.Running);
        running.ActiveWaits.Should().ContainSingle();

        blockingStep.Release();
        var waiting = await start.WaitAsync(TestContext.Current.CancellationToken);
        await runtime.RaiseEventAsync(
            waiting.InstanceId,
            "Continue",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);
        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        completed.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task SelectedWhenFirst_ConsumesWinnerResumeAndCancelsLosingFiberWait()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WhenFirst<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "winner",
                        _ => new WaitingBranchState { Name = "winner", EventName = "FirstReady" },
                        branch => branch
                            .Wait("FirstReady", _ => WaitCorrelation)
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "loser",
                        _ => new WaitingBranchState { Name = "loser", EventName = "SecondReady" },
                        branch => branch
                            .Wait("SecondReady", _ => WaitCorrelation)
                            .Return(state => state.Value.Name)),
                (parent, winner) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = [winner.Value]
                })
            .End("winner")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var waiting = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-when-first",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var waitingSnapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        waitingSnapshot.ActiveWaits.Should().HaveCount(2);

        await runtime.RaiseEventAsync(
            waiting.InstanceId,
            "FirstReady",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        envelope.OwnedObligations.Should().BeEmpty();
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("winner");
        events.OfType<WorkflowWaitCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task SelectedWhenFirst_CancelsLosingFiberTimerInWinnerCommit()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 13, 11, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WhenFirst<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "timer-loser",
                        _ => new WaitingBranchState { Name = "loser" },
                        branch => branch
                            .Delay(TimeSpan.FromMinutes(5))
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "winner",
                        _ => new WaitingBranchState { Name = "winner" },
                        branch => branch.Return(state => state.Value.Name)),
                (parent, winner) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = [winner.Value]
                })
            .End("winner")
            .Build();
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store),
            new DurableDefinitionRegistry(),
            clock.TimeProvider,
            new JsonWorkflowPayloadSerializer());
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-when-first-timer",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(completed.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var winnerReturn = events.OfType<WorkflowStepCompletedEvent>().Single(workflowEvent =>
            workflowEvent.StepPath.Contains("branches/1", StringComparison.Ordinal) &&
            workflowEvent.StepPath.EndsWith(":branch-return", StringComparison.Ordinal));
        var cancelled = events.OfType<WorkflowTimerCancelledEvent>().Should().ContainSingle().Which;

        cancelled.CommandId.Should().Be(winnerReturn.CommandId);
        events.OfType<WorkflowTimerScheduledEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task SelectedWhenFirst_StopsLosingFiberExternalJobInWinnerCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var trace = new List<string>();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WhenFirst<string>(
                branches => branches
                    .Branch<TestState>(
                        "job-loser",
                        _ => new TestState { Value = "job-loser" },
                        branch => branch
                            .Then(() => new DispatchExternalJobStep(trace))
                            .Return(state => state.Value.Value))
                    .Branch<WaitingBranchState>(
                        "winner",
                        _ => new WaitingBranchState { Name = "winner" },
                        branch => branch.Return(state => state.Value.Name)),
                (parent, winner) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = [winner.Value]
                })
            .End("winner")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-when-first-job",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(completed.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var winnerReturn = events.OfType<WorkflowStepCompletedEvent>().Single(workflowEvent =>
            workflowEvent.StepPath.Contains("branches/1", StringComparison.Ordinal) &&
            workflowEvent.StepPath.EndsWith(":branch-return", StringComparison.Ordinal));
        var stopped = events.OfType<WorkflowExternalJobStopRequestedEvent>()
            .Should().ContainSingle().Which;

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ActiveWaits.Should().BeEmpty();
        trace.Should().Equal("dispatch");
        stopped.ExternalJobId.Should().Be("job-loser");
        stopped.CommandId.Should().Be(winnerReturn.CommandId);
    }

    [Fact]
    public async Task SelectedWhenFirst_ReleasesLosingFiberResourcesInWinnerCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("db", 1, null),
            TestContext.Current.CancellationToken);
        var trace = new List<string>();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WhenFirst<string>(
                branches => branches
                    .Branch<TestState>(
                        "resource-loser",
                        _ => new TestState { Value = "resource-loser" },
                        branch => branch
                            .Then(() => new AcquireResourceStep(trace))
                            .Wait("Never", _ => WaitCorrelation)
                            .Return(state => state.Value.Value))
                    .Branch<WaitingBranchState>(
                        "winner",
                        _ => new WaitingBranchState { Name = "winner" },
                        branch => branch.Return(state => state.Value.Name)),
                (parent, winner) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = [winner.Value]
                })
            .End("winner")
            .Build();
        var runtime = CreateRuntime(store, resourcePoolStore: pools);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-when-first-resource",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(completed.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        var winnerReturn = events.OfType<WorkflowStepCompletedEvent>().Single(workflowEvent =>
            workflowEvent.StepPath.Contains("branches/1", StringComparison.Ordinal) &&
            workflowEvent.StepPath.EndsWith(":branch-return", StringComparison.Ordinal));
        var released = events.OfType<WorkflowResourcePoolReleasedEvent>()
            .Should().ContainSingle().Which;

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.ActiveWaits.Should().BeEmpty();
        pool.Value.HeldTickets.Should().BeEmpty();
        trace.Should().Equal("acquire");
        released.HolderKey.Should().Be("resource-loser");
        released.CommandId.Should().Be(winnerReturn.CommandId);
    }

    [Fact]
    public async Task SelectedWhenFirst_UnscopedDeliveryUsesPersistedWaitSequenceAfterHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WhenFirst<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "first",
                        _ => new WaitingBranchState { Name = "first", EventName = "Ready" },
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "second",
                        _ => new WaitingBranchState { Name = "second", EventName = "Ready" },
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name)),
                (parent, winner) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = [winner.Value]
                })
            .End("winner")
            .Build();
        var firstHost = CreateRuntime(store);
        firstHost.RegisterDefinition(definition);

        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-sequenced-waits",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var waitingSnapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var selectedWait = waitingSnapshot.ActiveWaits
            .OrderBy(wait => wait.WaitSequence)
            .ThenBy(wait => wait.FiberId!.Value.Value, StringComparer.Ordinal)
            .First();

        waitingSnapshot.ActiveWaits.Should().HaveCount(2);
        waitingSnapshot.ActiveWaits.Select(wait => wait.WaitSequence).Should().OnlyHaveUniqueItems();
        selectedWait.WaitSequence.Should().BeGreaterThan(0);

        var replacementHost = CreateRuntime(store);
        replacementHost.RegisterDefinition(definition);
        await replacementHost.RaiseEventAsync(
            waiting.InstanceId,
            "Ready",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var matched = events.OfType<WorkflowWaitMatchedEvent>().Single();

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("first");
        matched.WaitSequence.Should().Be(selectedWait.WaitSequence);
        matched.FiberId.Should().Be(selectedWait.FiberId);
        events.OfType<WorkflowWaitCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task SelectedParallel_OneCommandBudgetPersistsNextFiberAcrossHostReplacement()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<YieldingBranchState>(
                        "first",
                        _ => new YieldingBranchState { Name = "first" },
                        branch => branch
                            .Then(() => new CompleteBranchStep(trace))
                            .Return(state => state.Value.Name))
                    .Branch<YieldingBranchState>(
                        "second",
                        _ => new YieldingBranchState { Name = "second" },
                        branch => branch
                            .Then(() => new CompleteBranchStep(trace))
                            .Return(state => state.Value.Name)),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("done")
            .Build();
        var budget = new DurableDriverBudget(1, TimeSpan.FromSeconds(30));
        var firstHost = CreateRuntime(store, budget);
        firstHost.RegisterDefinition(definition);

        var started = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-budget",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var firstCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var firstEnvelope = DurableExecutionEnvelopeV2.Deserialize(firstCheckpoint!.Value.Payload);

        trace.Should().BeEmpty("scope creation alone consumes the first command budget");
        firstEnvelope.Scheduler.NextFiberId.Should().Be(firstEnvelope.Scopes.Single().ChildFiberIds[0]);

        var replacement = CreateRuntime(store, budget);
        replacement.RegisterDefinition(definition);
        await replacement.StartOrGetAsync<string, TestState>(
            "fiber-budget",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var secondCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var secondEnvelope = DurableExecutionEnvelopeV2.Deserialize(secondCheckpoint!.Value.Payload);

        trace.Should().Equal("first");
        secondEnvelope.Scheduler.NextFiberId.Should().Be(secondEnvelope.Scopes.Single().ChildFiberIds[1]);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            await replacement.StartOrGetAsync<string, TestState>(
                "fiber-budget",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
        }

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        completed.Status.Should().Be(WorkflowStatus.Completed);
        trace.Should().Equal("first", "second");
    }

    [Fact]
    public async Task SelectedNestedParallel_RepeatedYieldPreservesSiblingFairnessAcrossHostReplacement()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<OuterNestedState>(
                        "nested",
                        _ => new OuterNestedState { Name = "nested" },
                        branch => branch
                            .Parallel<string>(
                                nested => nested
                                    .Branch<YieldingBranchState>(
                                        "slow",
                                        _ => new YieldingBranchState { Name = "slow" },
                                        child => child
                                            .Then(() => new YieldTwiceBranchStep(trace))
                                            .Return(state => state.Value.Name))
                                    .Branch<YieldingBranchState>(
                                        "nested-sibling",
                                        _ => new YieldingBranchState { Name = "nested-sibling" },
                                        child => child
                                            .Then(() => new CompleteBranchStep(trace))
                                            .Return(state => state.Value.Name)),
                                (parent, results) => new OuterNestedState
                                {
                                    Name = string.Join("+", results.Select(result => result.Value))
                                })
                            .Return(state => state.Value.Name))
                    .Branch<YieldingBranchState>(
                        "outer-sibling",
                        _ => new YieldingBranchState { Name = "outer-sibling" },
                        branch => branch
                            .Then(() => new CompleteBranchStep(trace))
                            .Return(state => state.Value.Name)),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("done")
            .Build();
        var budget = new DurableDriverBudget(1, TimeSpan.FromSeconds(30));
        var starter = CreateRuntime(store, budget);
        starter.RegisterDefinition(definition);
        var started = await starter.StartOrGetAsync<string, TestState>(
            "fiber-nested-fairness",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var observedScopeIds = new HashSet<string>(StringComparer.Ordinal);

        for (var attempt = 0; attempt < 24; attempt++)
        {
            var replacement = CreateRuntime(store, budget);
            replacement.RegisterDefinition(definition);
            await replacement.StartOrGetAsync<string, TestState>(
                "fiber-nested-fairness",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
            var checkpoint = await store.LoadCheckpointAsync(
                started.InstanceId,
                TestContext.Current.CancellationToken);
            if (checkpoint.HasValue && checkpoint.Value.ContentType == DurableExecutionEnvelopeV2.ContentType)
            {
                var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
                foreach (var scope in envelope.Scopes)
                {
                    observedScopeIds.Add(scope.ScopeId);
                }
            }

            var projection = (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = started.InstanceId },
                TestContext.Current.CancellationToken)).Single();
            if (projection.Status == WorkflowStatus.Completed)
            {
                break;
            }
        }

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        completed.Status.Should().Be(WorkflowStatus.Completed);
        trace.Should().Contain("slow:yield:1");
        trace.Should().Contain("slow:yield:2");
        trace.Should().Contain("slow:complete");
        trace.IndexOf("nested-sibling").Should().BeLessThan(trace.IndexOf("slow:complete"));
        trace.IndexOf("outer-sibling").Should().BeLessThan(trace.IndexOf("slow:complete"));
        observedScopeIds.Should().HaveCount(2, "host replacement must reuse the two committed scope identities");
    }

    [Fact]
    public async Task SelectedLoopScope_ReentryAndEveryBranchBoundaryReuseCommittedIdentities()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<LoopScopeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new LoopScopeState())
            .While(
                state => state.Iteration < 2,
                loop => loop.Parallel<int>(
                    branches => branches
                        .Branch<NumberState>(
                            "one",
                            _ => new NumberState { Value = 1 },
                            branch => branch.Return(state => state.Value.Value))
                        .Branch<NumberState>(
                            "two",
                            _ => new NumberState { Value = 2 },
                            branch => branch.Return(state => state.Value.Value)),
                    (parent, results) => new LoopScopeState
                    {
                        Iteration = parent.Value.Iteration + 1,
                        Total = parent.Value.Total + results.Sum(result => result.Value)
                    }))
            .End("done")
            .Build();
        var budget = new DurableDriverBudget(1, TimeSpan.FromSeconds(30));
        var starter = CreateRuntime(store, budget);
        starter.RegisterDefinition(definition);
        var started = await starter.StartOrGetAsync<string, LoopScopeState>(
            "fiber-loop-reentry",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var committedScopes = new Dictionary<long, (string ScopeId, IReadOnlyList<string> ChildIds)>();

        for (var crashPoint = 0; crashPoint < 20; crashPoint++)
        {
            var checkpoint = await store.LoadCheckpointAsync(
                started.InstanceId,
                TestContext.Current.CancellationToken);
            if (checkpoint.HasValue && checkpoint.Value.ContentType == DurableExecutionEnvelopeV2.ContentType)
            {
                var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
                envelope.Scopes.Should().HaveCountLessThanOrEqualTo(
                    1,
                    "completed loop scope subtrees must be pruned after each merge");
                foreach (var scope in envelope.Scopes)
                {
                    if (committedScopes.TryGetValue(scope.ScopeEntrySequence, out var committed))
                    {
                        scope.ScopeId.Should().Be(committed.ScopeId);
                        scope.ChildFiberIds.Should().Equal(committed.ChildIds);
                    }
                    else
                    {
                        committedScopes.Add(
                            scope.ScopeEntrySequence,
                            (scope.ScopeId, scope.ChildFiberIds.ToArray()));
                    }
                }
            }

            var replacement = CreateRuntime(store, budget);
            replacement.RegisterDefinition(definition);
            await replacement.StartOrGetAsync<string, LoopScopeState>(
                "fiber-loop-reentry",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
            var projection = (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = started.InstanceId },
                TestContext.Current.CancellationToken)).Single();
            if (projection.Status == WorkflowStatus.Completed)
            {
                break;
            }
        }

        var finalCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var finalEnvelope = DurableExecutionEnvelopeV2.Deserialize(finalCheckpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(started.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var state = JsonSerializer.Deserialize<LoopScopeState>(finalEnvelope.StatePayload)!;

        state.Iteration.Should().Be(2);
        state.Total.Should().Be(6);
        committedScopes.Keys.Should().Equal(0, 1);
        committedScopes.Values.Select(scope => scope.ScopeId).Should().OnlyHaveUniqueItems();
        finalEnvelope.Scopes.Should().BeEmpty();
        finalEnvelope.Fibers.Should().ContainSingle(fiber => fiber.FiberId == finalEnvelope.RootFiberId);
        events.OfType<WorkflowStepCompletedEvent>()
            .Count(item => item.StepPath.EndsWith(":merge", StringComparison.Ordinal))
            .Should().Be(2, "a lost merge response must replay from the committed post-merge position");
    }

    [Fact]
    public async Task SelectedParallel_ConflictingHostsReloadCommittedSchedulerAndReuseScopeIdentities()
    {
        var gate = new TwoHostStepGate();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<YieldingBranchState>(
                        "first",
                        _ => new YieldingBranchState { Name = "first" },
                        branch => branch
                            .Then(() => new GatedCompleteBranchStep(gate))
                            .Return(state => state.Value.Name))
                    .Branch<YieldingBranchState>(
                        "second",
                        _ => new YieldingBranchState { Name = "second" },
                        branch => branch
                            .Then(() => new GatedCompleteBranchStep(gate))
                            .Return(state => state.Value.Name)),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("done")
            .Build();
        var budget = new DurableDriverBudget(1, TimeSpan.FromSeconds(30));
        var starter = CreateRuntime(store, budget);
        starter.RegisterDefinition(definition);
        var started = await starter.StartOrGetAsync<string, TestState>(
            "fiber-conflict",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var scopeCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var scopeEnvelope = DurableExecutionEnvelopeV2.Deserialize(scopeCheckpoint!.Value.Payload);
        var expectedScopeId = scopeEnvelope.Scopes.Single().ScopeId;
        var expectedChildren = scopeEnvelope.Scopes.Single().ChildFiberIds;

        var hostA = CreateRuntime(store, budget);
        var hostB = CreateRuntime(store, budget);
        hostA.RegisterDefinition(definition);
        hostB.RegisterDefinition(definition);
        await Task.WhenAll(
            hostA.StartOrGetAsync<string, TestState>(
                "fiber-conflict",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken),
            hostB.StartOrGetAsync<string, TestState>(
                "fiber-conflict",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken));

        var conflictedCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var conflictedEnvelope = DurableExecutionEnvelopeV2.Deserialize(conflictedCheckpoint!.Value.Payload);
        conflictedEnvelope.Scopes.Should().ContainSingle().Which.ScopeId.Should().Be(expectedScopeId);
        conflictedEnvelope.Scopes.Single().ChildFiberIds.Should().Equal(expectedChildren);
        gate.InvocationCount.Should().Be(3, "one first-branch invocation is at-least-once across the conflict");

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var replacement = CreateRuntime(store, budget);
            replacement.RegisterDefinition(definition);
            await replacement.StartOrGetAsync<string, TestState>(
                "fiber-conflict",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
        }

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = started.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(started.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        events.OfType<WorkflowStepCompletedEvent>()
            .Should().ContainSingle(workflowEvent => workflowEvent.StepPath.EndsWith(":merge"));
    }

    [Fact]
    public async Task SelectedNestedParallel_MergesIntoOuterFiberBeforeRootMerge()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<OuterNestedState>(
                        "nested",
                        _ => new OuterNestedState { Name = "nested" },
                        branch => branch
                            .Parallel<int>(
                                nested => nested
                                    .Branch<NumberState>(
                                        "one",
                                        _ => new NumberState { Value = 1 },
                                        child => child.Return(state => state.Value.Value))
                                    .Branch<NumberState>(
                                        "two",
                                        _ => new NumberState { Value = 2 },
                                        child => child.Return(state => state.Value.Value)),
                                (parent, results) => new OuterNestedState
                                {
                                    Name = parent.Value.Name,
                                    Total = results.Sum(result => result.Value)
                                })
                            .Return(state => $"{state.Value.Name}:{state.Value.Total}"))
                    .Branch<OuterNestedState>(
                        "plain",
                        _ => new OuterNestedState { Name = "plain" },
                        branch => branch.Return(state => state.Value.Name)),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("nested")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-nested",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log
            .Should().Equal("nested:3", "plain");
        envelope.Scopes.Should().BeEmpty();
        envelope.Fibers.Should().ContainSingle(fiber => fiber.FiberId == envelope.RootFiberId);
    }

    [Fact]
    public async Task SelectedParallel_MergeTransfersSagaEligibilityInMergeCommitAcrossHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<YieldingBranchState>(
                        "first",
                        _ => new YieldingBranchState { Name = "first" },
                        branch => branch.Return(state => state.Value.Name))
                    .Branch<YieldingBranchState>(
                        "second",
                        _ => new YieldingBranchState { Name = "second" },
                        branch => branch.Return(state => state.Value.Name)),
                (parent, results) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = results.Select(result => result.Value).ToList()
                })
            .End("done")
            .Build();
        var budget = new DurableDriverBudget(1, TimeSpan.FromSeconds(30));
        var starter = CreateRuntime(store, budget);
        starter.RegisterDefinition(definition);
        var started = await starter.StartOrGetAsync<string, TestState>(
            "fiber-saga-transfer",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var scopeCheckpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        var scopeEnvelope = DurableExecutionEnvelopeV2.Deserialize(scopeCheckpoint!.Value.Payload);
        var scope = scopeEnvelope.Scopes.Should().ContainSingle().Subject;
        var ownerFiberId = new FiberId(scope.ChildFiberIds[0]);
        var ownerScopeId = new ScopeId(scope.ScopeId);
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(new RecordSagaForwardActionCompletedCommand
        {
            CommandId = CommandId.New(),
            InstanceId = started.InstanceId,
            RequestedAt = DateTimeOffset.UtcNow,
            ScopeId = "checkout",
            ActionKey = "reserve",
            CompensationKey = "release",
            FiberId = ownerFiberId,
            OwningScopeId = ownerScopeId,
            InstructionId = "instruction:reserve",
            CommittedSequence = 1,
            CanonicalBranchOrder = 0,
            CanonicalInstructionOrder = 0
        }, TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var replacement = CreateRuntime(store, budget);
            replacement.RegisterDefinition(definition);
            await replacement.StartOrGetAsync<string, TestState>(
                "fiber-saga-transfer",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
            var projection = (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = started.InstanceId },
                TestContext.Current.CancellationToken)).Single();
            if (projection.Status == WorkflowStatus.Completed)
            {
                break;
            }
        }

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(started.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var transfer = events.OfType<SagaForwardActionsTransferredEvent>()
            .Should().ContainSingle().Subject;
        transfer.FromExecutionScopeId.Should().Be(ownerScopeId);
        transfer.ToExecutionScopeId.Should().BeNull();
        var mergeIndex = events.ToList().FindIndex(workflowEvent =>
            workflowEvent is WorkflowStepCompletedEvent completed &&
            completed.StepPath.EndsWith(":merge", StringComparison.Ordinal));
        events.ToList().IndexOf(transfer).Should().BeLessThan(mergeIndex);
    }

    [Fact]
    [Trait("AC", "DR-AC-032")]
    public async Task SelectedWhenFirst_CancelsNestedLosingScopeWaitsInWinnerCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WhenFirst<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "winner",
                        _ => new WaitingBranchState { Name = "winner", EventName = "WinnerReady" },
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "nested-loser",
                        _ => new WaitingBranchState { Name = "loser" },
                        branch => branch
                            .Parallel<string>(
                                nested => nested
                                    .Branch<WaitingBranchState>(
                                        "nested-a",
                                        _ => new WaitingBranchState
                                        {
                                            Name = "nested-a",
                                            EventName = "NestedA"
                                        },
                                        child => child
                                            .Then<BranchWaitStep>()
                                            .Return(state => state.Value.Name))
                                    .Branch<WaitingBranchState>(
                                        "nested-b",
                                        _ => new WaitingBranchState
                                        {
                                            Name = "nested-b",
                                            EventName = "NestedB"
                                        },
                                        child => child
                                            .Then<BranchWaitStep>()
                                            .Return(state => state.Value.Name)),
                                (parent, _) => parent.Value)
                            .Return(state => state.Value.Name)),
                (parent, winner) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = [winner.Value]
                })
            .End("winner")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var waiting = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-nested-race",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var waitingSnapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        waitingSnapshot.ActiveWaits.Should().HaveCount(3);
        waitingSnapshot.ActiveWaits.Select(wait => wait.FiberId).Should().OnlyHaveUniqueItems();
        waitingSnapshot.ActiveWaits.Should().OnlyContain(wait =>
            wait.FiberId.HasValue && wait.ScopeId.HasValue);

        await runtime.RaiseEventAsync(
            waiting.InstanceId,
            "WinnerReady",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(waiting.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        envelope.OwnedObligations.Should().BeEmpty();
        envelope.Scopes.Should().BeEmpty(
            "the successfully merged winner scope prunes its cancelled losing subtree");
        var cancellations = events.OfType<WorkflowWaitCancelledEvent>().ToArray();
        cancellations.Should().HaveCount(2);
        cancellations.Should().OnlyContain(workflowEvent =>
            workflowEvent.FiberId.HasValue && workflowEvent.ScopeId.HasValue);
    }

    [Fact]
    public async Task SelectedParallel_BranchFailureCancelsSiblingWaitBeforeWorkflowFails()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "waiting",
                        _ => new WaitingBranchState { Name = "waiting", EventName = "Never" },
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "failing",
                        _ => new WaitingBranchState { Name = "failing" },
                        branch => branch
                            .Then<FailingBranchStep>()
                            .Return(state => state.Value.Name)),
                (parent, _) => parent.Value)
            .End("unreachable")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var failed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-branch-failure",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = failed.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            failed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(failed.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ActiveWaits.Should().BeEmpty();
        envelope.OwnedObligations.Should().BeEmpty();
        envelope.Scopes.Should().ContainSingle(scope =>
            scope.Phase == DurableExecutionScopePhase.Failed);
        events.OfType<WorkflowWaitCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task SelectedParallel_MergeFailureCommitsFailedScopeWithoutRerunningBranches()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<YieldingBranchState>(
                        "first",
                        _ => new YieldingBranchState { Name = "first" },
                        branch => branch
                            .Then(() => new CompleteBranchStep(trace))
                            .Return(state => state.Value.Name))
                    .Branch<YieldingBranchState>(
                        "second",
                        _ => new YieldingBranchState { Name = "second" },
                        branch => branch
                            .Then(() => new CompleteBranchStep(trace))
                            .Return(state => state.Value.Name)),
                (_, _) => throw new WorkflowLifecycleException("merge failed"))
            .End("unreachable")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var failed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-merge-failure",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = failed.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            failed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(failed.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        trace.Should().Equal("first", "second");
        envelope.Scopes.Should().ContainSingle(scope =>
            scope.Phase == DurableExecutionScopePhase.Failed);
        events.OfType<WorkflowStepFailedEvent>().Single().ErrorSummary
            .Should().Contain("merge failed");
    }

    [Fact]
    public async Task SelectedContinueAsNew_QuiescentRootMintsNewGenerationAndPreservesSelectedState()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<RolloverState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new RolloverState(0))
            .If(
                state => state.Generation == 0,
                then => then.ContinueAsNew(state => state with
                {
                    Generation = state.Generation + 1
                }))
            .End(state => $"generation-{state.Generation}")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<int, RolloverState>(
            "fiber-rollover",
            definition.DefinitionId,
            definition.DefinitionVersion,
            0,
            TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(completed.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.EndOutcomeName.Should().Be("generation-1");
        envelope.ContinueAsNewGeneration.Should().Be(1);
        envelope.RootFiberId.Should().NotBe(FiberIdentityForGeneration(completed.InstanceId, 0));
        JsonSerializer.Deserialize<RolloverState>(envelope.StatePayload)!.Generation.Should().Be(1);
        events.OfType<WorkflowContinuedAsNewEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task SelectedContinueAsNew_NonQuiescentRootFailsWithoutChangingGenerationStateOrOwnership()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var definition = Workflow.Durable<RolloverState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new RolloverState(0))
            .If(
                _ => true,
                then => then.ContinueAsNew(state => state with
                {
                    Generation = state.Generation + 1
                }))
            .End("unreachable")
            .Build();
        var instanceId = InstanceId.New();
        await processor.ProcessAsync(
            new StartWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = DateTimeOffset.UtcNow,
                DefinitionId = definition.DefinitionId,
                DefinitionVersion = definition.DefinitionVersion
            },
            TestContext.Current.CancellationToken);
        var aggregate = await new DurableAggregateLoader(store).LoadAsync(
            instanceId,
            TestContext.Current.CancellationToken);
        var rollover = definition.CompiledPlan.Instructions.Single(instruction =>
            instruction.Kind == CompiledInstructionKind.ContinueAsNew);
        var root = FiberRecord.CreateRoot(instanceId, 0, rollover.Id);
        var scopeId = new ScopeId("non-quiescent-scope");
        var childId = new FiberId("non-quiescent-child");
        var waitId = WaitId.New();
        var child = new FiberRecord(
            childId,
            scopeId,
            definition.CompiledPlan.Instructions.Single(instruction =>
                instruction.Kind == CompiledInstructionKind.End).Id,
            FiberPhase.Blocked,
            LoopIteration: 0,
            NextScopeEntrySequence: 0,
            LocalStatePayload: null,
            ResultPayload: null,
            Blocked: new FiberBlock(FiberBlockedReason.Wait, waitId.ToString()),
            Failure: null,
            CancellationReason: null);
        var execution = new StructuredExecutionState(
            instanceId,
            ContinueAsNewGeneration: 0,
            root.Id,
            new FiberSchedulerState([root.Id], root.Id),
            new Dictionary<FiberId, FiberRecord>
            {
                [root.Id] = root,
                [child.Id] = child
            },
            new Dictionary<ScopeId, ExecutionScopeRecord>
            {
                [scopeId] = new ExecutionScopeRecord(
                    scopeId,
                    new ScopePlanId("scope:non-quiescent"),
                    ScopeEntrySequence: 0,
                    ParentScopeId: null,
                    root.Id,
                    CompiledScopeKind.WhenAll,
                    ExecutionScopePhase.Running,
                    [child.Id],
                    WinnerFiberId: null,
                    new Dictionary<FiberId, byte[]?>())
            });
        var serializer = new JsonWorkflowPayloadSerializer();
        var owned = new DurableOwnedObligationState
        {
            Kind = DurableOwnedObligationKind.Wait,
            ObligationId = waitId.ToString(),
            FiberId = child.Id.Value,
            ScopeId = scopeId.Value,
            RegistrationSequence = 1
        };
        var envelope = DurableFiberEnvelopeMapper.ToEnvelope(
            execution,
            definition.CompiledPlan,
            serializer.Serialize(new RolloverState(0)),
            [owned]);
        var executor = new DurableFiberDriverExecutor<RolloverState>(definition);

        var result = await executor.RunSegmentAsync(
            new DurableDriverContext(
                instanceId,
                aggregate,
                envelope,
                processor,
                serializer,
                TimeProvider.System,
                DurableDriverBudget.Default),
            TestContext.Current.CancellationToken);
        var failed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken)).Single();
        var checkpoint = await store.LoadCheckpointAsync(
            instanceId,
            TestContext.Current.CancellationToken);
        var failedEnvelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

        result.Outcome.Should().Be(DurableSegmentOutcome.Terminal);
        failed.Status.Should().Be(WorkflowStatus.Failed);
        checkpoint.Value.ErrorSummary.Should().Contain("SFE-RUN-001");
        failedEnvelope.ContinueAsNewGeneration.Should().Be(0);
        failedEnvelope.RootFiberId.Should().Be(root.Id.Value);
        JsonSerializer.Deserialize<RolloverState>(failedEnvelope.StatePayload)!.Generation.Should().Be(0);
        failedEnvelope.OwnedObligations.Should().ContainSingle().Which.Should().BeEquivalentTo(owned);
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        DurableDriverBudget? budget = null,
        IResourcePoolStore? resourcePoolStore = null)
    {
        return new DurableWorkflowRuntime(
            new DurableCommandProcessor(store, resourcePoolStore),
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer(),
            budget);
    }

    private static string FiberIdentityForGeneration(InstanceId instanceId, long generation)
    {
        var canonical = $"root|{instanceId}|{generation}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed class AppendStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Log.Add("step");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class WaitStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent("Continue", WaitCorrelation));
        }
    }

    private sealed class CaptureResumedEventStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is JsonElement { ValueKind: JsonValueKind.String } payload)
            {
                context.State.Log.Add(payload.GetString()!);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class DispatchExternalJobStep(List<string> trace) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            trace.Add("dispatch");
            return ValueTask.FromResult<StepResult>(new StepResult.RunExternalJob(
                context.State.Value,
                [1, 2, 3]));
        }
    }

    private sealed class ObserveExternalCompletionStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Log.Add(context.ResumedEvent?.EventName == "ExternalJobCompleted"
                ? "completed"
                : "missing");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class AcquireResourceStep(List<string> trace) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            trace.Add("acquire");
            return ValueTask.FromResult<StepResult>(new StepResult.AcquireResources(
                context.State.Value,
                [new ResourcePoolRequirement("db", 1)]));
        }
    }

    private sealed class YieldOnceBranchStep(List<string> trace) : IStep<YieldingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            var suffix = context.State.Attempts == 1 ? "yield" : "complete";
            trace.Add($"{context.State.Name}:{suffix}");
            return ValueTask.FromResult<StepResult>(context.State.Attempts == 1
                ? new StepResult.Yield()
                : new StepResult.Completed());
        }
    }

    private sealed class YieldTwiceBranchStep(List<string> trace) : IStep<YieldingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            if (context.State.Attempts <= 2)
            {
                trace.Add($"{context.State.Name}:yield:{context.State.Attempts}");
                return ValueTask.FromResult<StepResult>(new StepResult.Yield());
            }

            trace.Add($"{context.State.Name}:complete");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class BranchWaitStep : IStep<WaitingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingBranchState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(
                context.State.EventName,
                WaitCorrelation));
        }
    }

    private sealed class BlockingDurableBranchStep : IStep<YieldingBranchState>
    {
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Release() => release.TrySetResult();

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    private sealed class FailingBranchStep : IStep<WaitingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingBranchState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Failed(
                new WorkflowLifecycleException("branch failed")));
        }
    }

    private sealed class CompleteBranchStep(List<string> trace) : IStep<YieldingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            trace.Add(context.State.Name);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class GatedCompleteBranchStep(TwoHostStepGate gate) : IStep<YieldingBranchState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            await gate.ArriveAsync(cancellationToken);
            return new StepResult.Completed();
        }
    }

    private sealed class TwoHostStepGate
    {
        private readonly TaskCompletionSource<bool> bothArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int invocationCount;

        internal int InvocationCount => Volatile.Read(ref invocationCount);

        internal async Task ArriveAsync(CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref invocationCount);
            if (current == 2)
            {
                bothArrived.TrySetResult(true);
            }

            if (current <= 2)
            {
                await bothArrived.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class TestState
    {
        public string Value { get; set; } = string.Empty;

        public List<string> Log { get; set; } = [];
    }

    private sealed class WaitSequenceState
    {
        public int Count { get; set; }
    }

    private sealed class IncrementWaitSequenceStep : IStep<WaitSequenceState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitSequenceState> context,
            CancellationToken cancellationToken)
        {
            context.State.Count++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class TrackingSerializerRegistry : IWorkflowTypeSerializerRegistry
    {
        private int serializeCalls;
        private int deserializeCalls;

        internal int SerializeCalls => Volatile.Read(ref serializeCalls);

        internal int DeserializeCalls => Volatile.Read(ref deserializeCalls);

        public bool TryGetSchemaIdentity(Type type, out string schemaIdentity)
        {
            schemaIdentity = $"tracking:{type.AssemblyQualifiedName}";
            return true;
        }

        public byte[] Serialize(object? value, Type declaredType)
        {
            Interlocked.Increment(ref serializeCalls);
            return JsonSerializer.SerializeToUtf8Bytes(value, declaredType);
        }

        public object? Deserialize(ReadOnlySpan<byte> payload, Type declaredType)
        {
            Interlocked.Increment(ref deserializeCalls);
            return JsonSerializer.Deserialize(payload, declaredType);
        }
    }

    private sealed class YieldingBranchState
    {
        public string Name { get; set; } = string.Empty;

        public int Attempts { get; set; }
    }

    private sealed class WaitingBranchState
    {
        public string Name { get; set; } = string.Empty;

        public string EventName { get; set; } = string.Empty;
    }

    private sealed class OuterNestedState
    {
        public string Name { get; set; } = string.Empty;

        public int Total { get; set; }
    }

    private sealed class NumberState
    {
        public int Value { get; set; }
    }

    private sealed record RolloverState(int Generation);

    private sealed class LoopScopeState
    {
        public int Iteration { get; set; }

        public int Total { get; set; }
    }
}
