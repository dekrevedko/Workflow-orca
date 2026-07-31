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
using OrcaCore.Core.Internal;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using OrcaCore.TestSupport.StructuredExecution;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableStructuredFiberDriverTests
{
    private static readonly CorrelationId WaitCorrelation = CorrelationId.Create("fiber-wait");

    [Fact]
    public async Task ResultfulEnd_AfterHostReplacementPersistsOutputStatusAndOutcomeInOneTerminalCheckpoint()
    {
        var store = new InMemoryWorkflowProvider();
        var firstRuntime = CreateRuntime(store);
        var outcome = WorkflowOutcomeName.Create("approved");
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Wait(EventName.Create("Complete"), _ => WaitCorrelation)
            .End(snapshot => new TerminalOutput(snapshot.Value.Value), outcome)
            .Build();
        var runtimeDefinition = (WorkflowDefinition<TestState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;
        firstRuntime.RegisterDefinition(runtimeDefinition);

        var waiting = await firstRuntime.StartOrGetAsync<string, TestState>(
            "typed-terminal-output",
            publicDefinition.DefinitionId,
            publicDefinition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
                TestContext.Current.CancellationToken))
            .Single().Status.Should().Be(WorkflowStatus.Waiting);

        var replacementRuntime = CreateRuntime(store);
        replacementRuntime.RegisterDefinition(runtimeDefinition);
        await replacementRuntime.RaiseEventAsync(
            waiting.InstanceId,
            "Complete",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.EndOutcomeName.Should().Be(outcome.Value);
        envelope.Output.Should().NotBeNull();
        envelope.Output!.TypeName.Should().Contain(nameof(TerminalOutput));
        JsonSerializer.Deserialize<TerminalOutput>(envelope.Output.Payload)
            .Should().Be(new TerminalOutput("input"));
    }

    [Fact]
    public async Task ResultfulEnd_RejectsUnapprovedPolymorphicOutputBeforeTerminalCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store);
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .End<PolymorphicOutputBase>(
                snapshot => new PolymorphicOutputDerived(snapshot.Value.Value, "must-not-be-dropped"))
            .Build();
        var runtimeDefinition = (WorkflowDefinition<TestState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;
        runtime.RegisterDefinition(runtimeDefinition);
        const string idempotencyKey = "polymorphic-terminal-output";

        Func<Task> act = async () =>
        {
            _ = await runtime.StartOrGetAsync<string, TestState>(
                idempotencyKey,
                publicDefinition.DefinitionId,
                publicDefinition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
        };

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*polymorphic*not supported*");
        var started = await store.GetStartedAsync(
            idempotencyKey,
            TestContext.Current.CancellationToken);
        started.HasValue.Should().BeTrue();
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(started.Value.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowCompletedEvent>().Should().BeEmpty();
        events.OfType<WorkflowTerminalEvent>()
            .Should().NotContain(workflowEvent => workflowEvent.Status == WorkflowStatus.Completed);
        var checkpoint = await store.LoadCheckpointAsync(
            started.Value.InstanceId,
            TestContext.Current.CancellationToken);
        checkpoint.HasValue.Should().BeFalse(
            "the rejected output must not create a terminal checkpoint");
    }

    [Fact]
    public async Task PublicDurableNamedStep_IsActivatedFromHostServicesWithConstructorDependencies()
    {
        var store = new InMemoryWorkflowProvider();
        var dependency = new DurableProbeDependency();
        var step = new ConstructorInjectedDurableStep(dependency);
        var services = new DurableStepServiceProvider(step);
        var registry = new DurableDefinitionRegistry(services);
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store),
            registry,
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then<ConstructorInjectedDurableStep>()
            .End()
            .Build();
        var runtimeDefinition = (WorkflowDefinition<TestState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;
        runtime.RegisterDefinition(runtimeDefinition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "durable-host-di",
            publicDefinition.DefinitionId,
            publicDefinition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
                TestContext.Current.CancellationToken))
            .Single().Status.Should().Be(WorkflowStatus.Completed);
        dependency.ExecutionCount.Should().Be(1);
        services.RequestedTypes.Should().Equal(typeof(ConstructorInjectedDurableStep));
    }

    [Fact]
    public async Task SelectedParallel_UsesRegistryOnlyForSchemaIdentityAtBuild()
    {
        var registry = new TrackingSerializerRegistry();
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        registry.SchemaResolutionCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectedParallel_OversizedResultFailsOwningScopeBeforeCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .AddColdWait("Approved", _ => CorrelationId.Create("order-42"))
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
            CorrelationId.Create("order-42"),
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<WaitSequenceState>(
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var completionEventId = EventId.Create(Guid.CreateVersion7().ToString());
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
                CompletionEventId = EventId.Create(Guid.CreateVersion7().ToString())
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
        var otherInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                otherInstanceId,
                "other",
                [new ResourcePoolRequirement("db", 1)],
                TimeProvider.System.GetUtcNow(),
                null),
            TestContext.Current.CancellationToken);
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
            CorrelationId.Create("holder"),
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Then(() => new AcquireResourceStep(trace))
            .Then<AppendStep>()
            .End("resource-done")
            .Build();
        var budget = new DurableDriverBudget(2, TimeSpan.FromSeconds(30));

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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .WhenFirst<string>(
                branches => branches
                    .Branch<TestState>(
                        "job-loser",
                        _ => new TestState { Value = "job-loser" },
                        branch => branch
                            .Then(() => new DispatchExternalJobStep(trace))
                            .Return(state => state.Value.Value))
                    .Branch<YieldingBranchState>(
                        "winner",
                        _ => new YieldingBranchState { Name = "winner" },
                        branch => branch
                            .Then(() => new YieldOnceBranchStep([]))
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
                    .Branch<YieldingBranchState>(
                        "winner",
                        _ => new YieldingBranchState { Name = "winner" },
                        branch => branch
                            .Then(() => new YieldTwiceBranchStep([]))
                            .Return(state => state.Value.Name)),
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var budget = new DurableDriverBudget(2, TimeSpan.FromSeconds(30));
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
        firstEnvelope.Scheduler.NextFiberId.Should().Be(
            firstEnvelope.Scopes.Single().ChildFiberIds[1],
            "the first branch's operation-coordinate commit consumes the second command budget");

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
    public void SelectedLoopNestedScope_IsRejectedBeforeRegistration()
    {
        Action build = () => global::OrcaCore.Workflow.Durable<LoopScopeState>(DefinitionId.New(), DefinitionVersion.Initial)
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

        build.Should().Throw<WorkflowDefinitionException>()
            .Which.Diagnostics.Should().ContainSingle(diagnostic =>
                diagnostic.Code == "SFE-AUTH-CAP-001" &&
                diagnostic.Message.Contains("Parallel is available only", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SelectedParallel_ConflictingHostsReloadCommittedSchedulerAndReuseScopeIdentities()
    {
        var gate = new TwoHostStepGate();
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var budget = new DurableDriverBudget(2, TimeSpan.FromSeconds(30));
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
        gate.InvocationCount.Should().Be(
            3,
            "the starter and both competing hosts may physically dispatch the same persisted ordinal");

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
    public async Task SelectedParallel_MergeTransfersSagaEligibilityInMergeCommitAcrossHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
    public async Task SelectedParallel_CeilingOnePreservesAuthoredWaitAndLaterFailureUntilSiblingCompletes()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var runtime = CreateRuntime(store, maxConcurrentExecutionPathsPerInstance: 1);
        runtime.RegisterDefinition(definition);

        var waiting = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-branch-failure",
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
        var waitingEnvelope = DurableExecutionEnvelopeV2.Deserialize(waitingCheckpoint!.Value.Payload);

        waitingSnapshot.ErrorSummary.Should().BeNull();
        waitingSnapshot.Status.Should().BeOneOf(
            [WorkflowStatus.Running, WorkflowStatus.Waiting],
            because: waitingSnapshot.ErrorSummary);
        waitingSnapshot.ActiveWaits.Should().ContainSingle();
        waitingEnvelope.OwnedObligations.Should().ContainSingle();
        waitingEnvelope.Scopes.Should().ContainSingle(scope =>
            scope.Phase == DurableExecutionScopePhase.Running);

        var replacement = CreateRuntime(store, maxConcurrentExecutionPathsPerInstance: 1);
        replacement.RegisterDefinition(definition);
        await replacement.RaiseEventAsync(
            waiting.InstanceId,
            "Never",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
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

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ActiveWaits.Should().BeEmpty();
        envelope.OwnedObligations.Should().BeEmpty();
        envelope.Scopes.Should().ContainSingle(scope =>
            scope.Phase == DurableExecutionScopePhase.Failed);
        events.OfType<WorkflowWaitCancelledEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedParallel_WhenAllOutcomesMergesOrderedSuccessAndFailureData()
    {
        var store = new InMemoryWorkflowProvider();
        global::OrcaCore.WorkflowFailure? observedFailure = null;
        var definition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .ParallelOutcomes<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "failing",
                        _ => new WaitingBranchState { Name = "failing" },
                        branch => branch
                            .Then<FailingBranchStep>()
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "succeeding",
                        _ => new WaitingBranchState { Name = "succeeding" },
                        branch => branch.Return(state => state.Value.Name)),
                (parent, outcomes) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = outcomes.Select(outcome =>
                    {
                        if (outcome is global::OrcaCore.BranchOutcome<string>.Failed failed)
                        {
                            observedFailure = failed.Failure;
                        }

                        return outcome switch
                        {
                            global::OrcaCore.BranchOutcome<string>.Succeeded success =>
                                $"{success.BranchId.Value}:success:{success.Result}",
                            global::OrcaCore.BranchOutcome<string>.Failed failure =>
                                $"{failure.BranchId.Value}:failure:{failure.Failure.Code}",
                            _ => throw new InvalidOperationException("Unknown branch outcome.")
                        };
                    }).ToList()
                })
            .End("outcomes")
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-branch-outcomes",
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
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal(
            "failing:failure:WF-LEGACY-LIFECYCLE",
            "succeeding:success:succeeding");
        observedFailure.Should().NotBeNull();
        observedFailure!.AuthoredLocation.Value.Should().Be(
            "workflow:$/n:00000001/parallel:00000000/n:00000000");
        observedFailure.Occurrence.Should().BeOfType<global::OrcaCore.FailureOccurrence.Branch>()
            .Which.BranchId.Should().Be(global::OrcaCore.AuthoredBranchId.Create("failing"));
    }

    [Fact]
    public async Task WideFixedParallel_CompletesWithoutACompilerOwnedFiberCeiling()
    {
        var store = new InMemoryWorkflowProvider();
        var root = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value });
        var successor = root.Parallel<int>(branches =>
        {
            foreach (var index in Enumerable.Range(0, 257))
            {
                var branchId = global::OrcaCore.AuthoredBranchId.Create($"branch-{index:D4}");
                branches.Branch(
                    branchId,
                    parent => parent.Value,
                    branch => branch.Return(_ => index));
            }
        }).WhenAll((parent, results) => new TestState
        {
            Value = parent.Value.Value,
            Log = [results.Count.ToString()]
        });
        var definition = successor.End().Build();
        var runtime = CreateRuntime(store, maxConcurrentExecutionPathsPerInstance: 1);
        runtime.RegisterDefinition(
            definition.RuntimeDefinition.Should()
                .BeOfType<WorkflowDefinition<TestState>>().Subject);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "wide-fixed-parallel",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var finalStatus = WorkflowStatus.Running;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var snapshot = (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
                TestContext.Current.CancellationToken)).Single();
            finalStatus = snapshot.Status;
            if (snapshot.Status == WorkflowStatus.Completed)
            {
                break;
            }

            await runtime.StartOrGetAsync<string, TestState>(
                "wide-fixed-parallel",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
        }

        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var state = JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!;

        finalStatus.Should().Be(WorkflowStatus.Completed);
        state.Log.Should().Equal("257");
    }

    [Fact]
    public async Task SelectedForEach_MaterializesBoundedItemsAndMergesResultsByIndex()
    {
        var store = new InMemoryWorkflowProvider();
        var builder = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value });
        builder.AddForEach<string, WaitingBranchState, string>(
            _ => ["zero", "one", "two"],
            WorkflowPartitioner<string>.Items(),
            item => new WaitingBranchState
            {
                Name = item.Items.Single()
            },
            body => body.Return(state => state.Value.Name),
            ForEachJoinPolicy.WhenAll,
            ForEachFailurePolicy.WaitAllThenFail,
            maxConcurrency: 2,
            merge: (parent, outcomes) => new TestState
            {
                Value = parent.Value.Value,
                Log = outcomes.Select(outcome => $"{outcome.Index}:{outcome.Result}").ToList()
            });
        var definition = builder.End("foreach").Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-foreach",
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
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal(
            "0:zero",
            "1:one",
            "2:two");
    }

    [Fact]
    public async Task PublicForEach_MaxItemsRejectsBeforeProjectorOrScopeAdmission()
    {
        var store = new InMemoryWorkflowProvider();
        var itemStateCalls = 0;
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .ForEach<string, WaitingBranchState, string>(
                _ => ["zero", "one"],
                global::OrcaCore.ForEachOptions.Create(1),
                item =>
                {
                    itemStateCalls++;
                    return new WaitingBranchState { Name = item.Item };
                },
                body => body.Return(state => state.Value.Name))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        var definition = publicDefinition.RuntimeDefinition.Should()
            .BeOfType<WorkflowDefinition<TestState>>().Subject;
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        Func<Task> act = () => runtime.StartOrGetAsync<string, TestState>(
            "foreach-max-items-preadmission",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        var exception = await act.Should().ThrowAsync<Exception>();
        exception.Which.ToString().Should().Contain("SFE-LIMIT-001");
        itemStateCalls.Should().Be(0);
        var checkpoint = await store.LoadCheckpointAsync(
            (await store.ListAsync(
                new WorkflowProjectionQuery { DefinitionId = definition.DefinitionId },
                TestContext.Current.CancellationToken)).Single().InstanceId,
            TestContext.Current.CancellationToken);
        checkpoint.HasValue.Should().BeFalse("no dynamic item scope may be admitted or committed");
    }

    [Fact]
    public async Task PublicForEach_EncodedValueLimitRejectsBeforeProjectorOrScopeAdmission()
    {
        var store = new InMemoryWorkflowProvider();
        var itemStateCalls = 0;
        var oversized = new string('x', FixedWorkflowValueCodec.MaxEncodedValueBytes);
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .ForEach<string, WaitingBranchState, string>(
                _ => [oversized],
                global::OrcaCore.ForEachOptions.Create(int.MaxValue),
                item =>
                {
                    itemStateCalls++;
                    return new WaitingBranchState { Name = item.Item };
                },
                body => body.Return(state => state.Value.Name))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        var definition = publicDefinition.RuntimeDefinition.Should()
            .BeOfType<WorkflowDefinition<TestState>>().Subject;
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        Func<Task> act = () => runtime.StartOrGetAsync<string, TestState>(
            "foreach-encoded-value-preadmission",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        var exception = await act.Should().ThrowAsync<StructuredExecutionLimitException>();
        exception.Which.Code.Should().Be(StructuredExecutionLimitCodes.EncodedValueExceeded);
        itemStateCalls.Should().Be(0);
        var checkpoint = await store.LoadCheckpointAsync(
            (await store.ListAsync(
                new WorkflowProjectionQuery { DefinitionId = definition.DefinitionId },
                TestContext.Current.CancellationToken)).Single().InstanceId,
            TestContext.Current.CancellationToken);
        checkpoint.HasValue.Should().BeFalse("no dynamic item scope may be admitted or committed");
    }

    [Fact]
    public async Task PublicForEach_TaggedFlatteningPreservesGroupIdentityAndFlatAggregationOrder()
    {
        var store = new InMemoryWorkflowProvider();
        var items = new[]
        {
            new TaggedWorkItem("group-a", "deploy", "unit-1"),
            new TaggedWorkItem("group-a", "verify", "unit-1"),
            new TaggedWorkItem("group-b", "deploy", "unit-2"),
            new TaggedWorkItem("group-b", "verify", "unit-2")
        };
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .ForEach<TaggedWorkItem, TaggedItemState, TaggedItemResult>(
                _ => items,
                global::OrcaCore.ForEachOptions.Create(16, maxConcurrency: 2),
                item => new TaggedItemState
                {
                    Index = item.Index,
                    Item = item.Item
                },
                body => body
                    .If(
                        state => state.Value.Item.UnitKind == "deploy",
                        then => then.Then<TaggedDeployStep>(),
                        otherwise => otherwise.Then<TaggedVerifyStep>())
                    .Return(state => new TaggedItemResult(
                        state.Value.Item.GroupId,
                        state.Value.Item.UnitKind,
                        state.Value.Item.UnitId,
                        state.Value.Observation)))
            .WhenAll((parent, results) => new TestState
            {
                Value = parent.Value.Value,
                Log = results.Select(result =>
                    $"{result.Index}:{result.Result.GroupId}:{result.Result.UnitKind}:" +
                    $"{result.Result.UnitId}:{result.Result.Observation}").ToList()
            })
            .End()
            .Build();
        var definition = publicDefinition.RuntimeDefinition.Should()
            .BeOfType<WorkflowDefinition<TestState>>().Subject;
        var runtime = CreateRuntime(store, serviceProvider: new TaggedStepServiceProvider());
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "foreach-tagged-flattening",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var snapshot = (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
                TestContext.Current.CancellationToken)).Single();
            if (snapshot.Status == WorkflowStatus.Completed)
            {
                break;
            }

            await runtime.StartOrGetAsync<string, TestState>(
                "foreach-tagged-flattening",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
        }

        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var finalProjection = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = completed.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        finalProjection.Status.Should().Be(
            WorkflowStatus.Completed,
            $"scope phases were {string.Join(",", envelope.Scopes.Select(scope => scope.Phase))}; " +
            $"error was {finalProjection.ErrorSummary}; item failures were " +
            string.Join(" | ", envelope.Scopes.SelectMany(scope => scope.ForEach?.Outcomes ?? [])
                .Select(outcome => outcome.Failure?.Message ?? outcome.Status)));
        var state = JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!;

        state.Log.Should().Equal(
            "0:group-a:deploy:unit-1:deployed",
            "1:group-a:verify:unit-1:verified",
            "2:group-b:deploy:unit-2:deployed",
            "3:group-b:verify:unit-2:verified");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedForEach_EmptySnapshotMergesExactlyOnceUnderBothJoins(bool collectOutcomes)
    {
        var store = new InMemoryWorkflowProvider();
        var mergeCalls = 0;
        var builder = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value });
        builder.AddForEach<string, WaitingBranchState, string>(
            _ => [],
            WorkflowPartitioner<string>.Items(),
            item => new WaitingBranchState { Name = item.Items.Single() },
            body => body.Return(state => state.Value.Name),
            ForEachJoinPolicy.WhenAll,
            collectOutcomes
                ? ForEachFailurePolicy.ContinueWithPartialFailures
                : ForEachFailurePolicy.WaitAllThenFail,
            maxConcurrency: null,
            merge: (parent, outcomes) =>
            {
                mergeCalls++;
                outcomes.Should().BeEmpty();
                return new TestState { Value = parent.Value.Value, Log = ["empty"] };
            });
        var definition = builder.End("empty").Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            $"fiber-foreach-empty-{collectOutcomes}",
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
        mergeCalls.Should().Be(1);
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal("empty");
    }

    [Fact]
    public async Task SelectedForEach_WhenAllOutcomesPreservesStableItemFailureCodes()
    {
        var store = new InMemoryWorkflowProvider();
        var builder = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value });
        builder.AddForEach<string, WaitingBranchState, string>(
            _ => ["zero", "one", "two"],
            WorkflowPartitioner<string>.Items(),
            item => new WaitingBranchState { Name = item.Items.Single() },
            body => body
                .Then<MaybeFailForEachStep>()
                .Return(state => state.Value.Name),
            ForEachJoinPolicy.WhenAll,
            ForEachFailurePolicy.ContinueWithPartialFailures,
            maxConcurrency: null,
            merge: (parent, outcomes) => new TestState
            {
                Value = parent.Value.Value,
                Log = outcomes.Select(outcome =>
                    $"{outcome.Index}:{outcome.Status}:{outcome.Result}:{outcome.Failure?.Code}").ToList()
            });
        var definition = builder.End("outcomes").Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "fiber-foreach-outcomes",
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
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal(
            "0:Succeeded:zero:",
            "1:Failed::WF-LEGACY-LIFECYCLE",
            "2:Succeeded:two:");
    }

    [Fact]
    public async Task SelectedForEach_ReplacementHostUsesCommittedItemSnapshotWithoutReselectingItems()
    {
        var store = new InMemoryWorkflowProvider();
        var selectorCalls = 0;
        var selectedItems = new List<string> { "zero", "one", "two" };
        var builder = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value });
        builder.AddForEach<string, WaitingBranchState, string>(
            _ =>
            {
                selectorCalls++;
                if (selectorCalls > 1)
                {
                    throw new InvalidOperationException("ForEach selector was replayed.");
                }

                return selectedItems;
            },
            WorkflowPartitioner<string>.Items(),
            item => new WaitingBranchState
            {
                Name = item.Items.Single(),
                EventName = $"Item-{item.Index}"
            },
            body => body
                .Then<BranchWaitStep>()
                .Return(state => state.Value.Name),
            ForEachJoinPolicy.WhenAll,
            ForEachFailurePolicy.WaitAllThenFail,
            maxConcurrency: 3,
            merge: (parent, outcomes) => new TestState
            {
                Value = parent.Value.Value,
                Log = outcomes.Select(outcome => $"{outcome.Index}:{outcome.Result}").ToList()
            });
        var definition = builder.End("foreach-restart").Build();
        var firstHost = CreateRuntime(store, maxConcurrentExecutionPathsPerInstance: 1);
        firstHost.RegisterDefinition(definition);

        var waiting = await firstHost.StartOrGetAsync<string, TestState>(
            "fiber-foreach-restart",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var firstCheckpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var firstEnvelope = DurableExecutionEnvelopeV2.Deserialize(firstCheckpoint!.Value.Payload);

        selectorCalls.Should().Be(1);
        firstEnvelope.Scopes.Single().ForEach!.Descriptors.Should().HaveCount(3);
        firstEnvelope.Scopes.Single().ForEach!.NextAdmissionOffset.Should().Be(1);
        firstEnvelope.Scopes.Single().ForEach!.MaxConcurrency.Should().Be(3);
        selectedItems[1] = "mutated";

        var replacementHost = CreateRuntime(store, maxConcurrentExecutionPathsPerInstance: 2);
        replacementHost.RegisterDefinition(definition);
        await replacementHost.RaiseEventAsync(
            waiting.InstanceId,
            "Item-0",
            WaitCorrelation,
            cancellationToken: TestContext.Current.CancellationToken);
        var readmittedCheckpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var readmittedEnvelope = DurableExecutionEnvelopeV2.Deserialize(readmittedCheckpoint!.Value.Payload);
        readmittedEnvelope.Scopes.Single().ForEach!.NextAdmissionOffset.Should().Be(3);
        readmittedEnvelope.Scopes.Single().ChildFiberIds.Should().HaveCount(3);

        for (var index = 1; index < 3; index++)
        {
            await replacementHost.RaiseEventAsync(
                waiting.InstanceId,
                $"Item-{index}",
                WaitCorrelation,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var completed = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();
        var finalCheckpoint = await store.LoadCheckpointAsync(
            waiting.InstanceId,
            TestContext.Current.CancellationToken);
        var finalEnvelope = DurableExecutionEnvelopeV2.Deserialize(finalCheckpoint!.Value.Payload);

        selectorCalls.Should().Be(1);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        JsonSerializer.Deserialize<TestState>(finalEnvelope.StatePayload)!.Log.Should().Equal(
            "0:zero",
            "1:one",
            "2:two");
    }

    [Fact]
    public async Task PublicParallel_WhenAllTerminationSuppressesMergeAndClearsChildWaits()
    {
        var mergeCalls = 0;
        var store = new InMemoryWorkflowProvider();
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(branches => branches
                .Branch<WaitingBranchState>(
                    global::OrcaCore.AuthoredBranchId.Create("first"),
                    _ => new WaitingBranchState { Name = "first" },
                    branch => branch
                        .Wait(EventName.Create("First"), _ => CorrelationId.Create("first"))
                        .Return(state => state.Value.Name))
                .Branch<WaitingBranchState>(
                    global::OrcaCore.AuthoredBranchId.Create("second"),
                    _ => new WaitingBranchState { Name = "second" },
                    branch => branch
                        .Wait(EventName.Create("Second"), _ => CorrelationId.Create("second"))
                        .Return(state => state.Value.Name)))
            .WhenAll((parent, _) =>
            {
                mergeCalls++;
                return new TestState { Value = parent.Value.Value, Log = ["merged"] };
            })
            .End()
            .Build();
        var definition = (WorkflowDefinition<TestState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var waiting = await runtime.StartOrGetAsync<string, TestState>(
            "parallel-terminate-suppresses-merge",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
                TestContext.Current.CancellationToken))
            .Single().ActiveWaits.Should().HaveCount(2);
        await runtime.Management.TerminateAsync(
            waiting.InstanceId,
            DateTimeOffset.UtcNow,
            global::OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);
        var terminated = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        terminated.Status.Should().Be(WorkflowStatus.Terminated);
        terminated.ActiveWaits.Should().BeEmpty();
        mergeCalls.Should().Be(0);
    }

    [Fact]
    public async Task PublicForEach_WhenAllOutcomesCancellationSuppressesMergeAndClearsItemWaits()
    {
        var mergeCalls = 0;
        var store = new InMemoryWorkflowProvider();
        var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .ForEach<string, WaitingBranchState, string>(
                _ => ["zero", "one"],
                global::OrcaCore.ForEachOptions.Create(2),
                item => new WaitingBranchState { Name = item.Item, EventName = $"Item-{item.Index}" },
                body => body
                    .Wait(EventName.Create("Resume"), state => CorrelationId.Create(state.Value.Name))
                    .Return(state => state.Value.Name))
            .WhenAllOutcomes((parent, _) =>
            {
                mergeCalls++;
                return new TestState { Value = parent.Value.Value, Log = ["merged"] };
            })
            .End()
            .Build();
        var definition = (WorkflowDefinition<TestState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var waiting = await runtime.StartOrGetAsync<string, TestState>(
            "foreach-cancel-suppresses-merge",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
                TestContext.Current.CancellationToken))
            .Single().ActiveWaits.Should().HaveCount(2);
        await runtime.Management.CancelAsync(
            waiting.InstanceId,
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        var cancelled = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waiting.InstanceId },
            TestContext.Current.CancellationToken)).Single();

        cancelled.Status.Should().Be(WorkflowStatus.Cancelled);
        cancelled.ActiveWaits.Should().BeEmpty();
        mergeCalls.Should().Be(0);
    }

    [Fact]
    public async Task PublicParallel_ReferenceModelCompletionAndRestartPermutationsProduceOneOrderedMerge()
    {
        foreach (var seed in new[] { 307, 311, 313 })
        {
            var scenario = StructuredScopeScenarioGenerator.GenerateNested(seed, branchCount: 4);
            var mergeCalls = 0;
            var store = new InMemoryWorkflowProvider();
            var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(value => new TestState { Value = value })
                .Parallel<string>(branches =>
                {
                    for (var index = 0; index < scenario.WorkItemCount; index++)
                    {
                        var captured = index;
                        branches.Branch<WaitingBranchState>(
                            global::OrcaCore.AuthoredBranchId.Create($"branch-{captured}"),
                            _ => new WaitingBranchState { Name = captured.ToString() },
                            branch => branch
                                .Wait(
                                    EventName.Create("Ready"),
                                    _ => CorrelationId.Create($"branch-{captured}"))
                                .Return(state => state.Value.Name));
                    }
                })
                .WhenAll((parent, results) =>
                {
                    mergeCalls++;
                    return new TestState
                    {
                        Value = parent.Value.Value,
                        Log = results.Select(result => result.Result).ToList()
                    };
                })
                .End()
                .Build();
            var definition = (WorkflowDefinition<TestState>)publicDefinition.GetType()
                .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(publicDefinition)!;
            var runtime = CreateRuntime(store);
            runtime.RegisterDefinition(definition);
            var started = await runtime.StartOrGetAsync<string, TestState>(
                $"parallel-reference-{seed}",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);

            foreach (var index in scenario.CompletionOrder)
            {
                runtime = CreateRuntime(store);
                runtime.RegisterDefinition(definition);
                await runtime.RaiseEventAsync(
                    started.InstanceId,
                    "Ready",
                    CorrelationId.Create($"branch-{index}"),
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            var completed = (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = started.InstanceId },
                TestContext.Current.CancellationToken)).Single();
            var checkpoint = await store.LoadCheckpointAsync(
                started.InstanceId,
                TestContext.Current.CancellationToken);
            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

            completed.Status.Should().Be(WorkflowStatus.Completed);
            completed.ActiveWaits.Should().BeEmpty();
            envelope.OwnedObligations.Should().BeEmpty();
            mergeCalls.Should().Be(1);
            JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log
                .Should().Equal("0", "1", "2", "3");
        }
    }

    [Fact]
    public async Task PublicForEach_ReferenceModelCompletionAndRestartPermutationsProduceOneIndexedMerge()
    {
        foreach (var seed in new[] { 401, 409, 419 })
        {
            var scenario = StructuredScopeScenarioGenerator.GenerateForEach(
                seed,
                itemCount: 6,
                maxConcurrency: 6);
            var mergeCalls = 0;
            var store = new InMemoryWorkflowProvider();
            var publicDefinition = global::OrcaCore.Workflow.Durable<TestState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(value => new TestState { Value = value })
                .ForEach<int, WaitingBranchState, string>(
                    _ => Enumerable.Range(0, scenario.WorkItemCount).ToArray(),
                    global::OrcaCore.ForEachOptions.Create(
                        scenario.WorkItemCount,
                        scenario.MaxConcurrency),
                    item => new WaitingBranchState
                    {
                        Name = item.Item.ToString(),
                        EventName = $"item-{item.Index}"
                    },
                    body => body
                        .Wait(
                            EventName.Create("Ready"),
                            state => CorrelationId.Create(state.Value.EventName))
                        .Return(state => state.Value.Name))
                .WhenAll((parent, results) =>
                {
                    mergeCalls++;
                    return new TestState
                    {
                        Value = parent.Value.Value,
                        Log = results.Select(result => $"{result.Index}:{result.Result}").ToList()
                    };
                })
                .End()
                .Build();
            var definition = (WorkflowDefinition<TestState>)publicDefinition.GetType()
                .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(publicDefinition)!;
            var runtime = CreateRuntime(store);
            runtime.RegisterDefinition(definition);
            var started = await runtime.StartOrGetAsync<string, TestState>(
                $"foreach-reference-{seed}",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);

            foreach (var index in scenario.CompletionOrder)
            {
                runtime = CreateRuntime(store);
                runtime.RegisterDefinition(definition);
                await runtime.RaiseEventAsync(
                    started.InstanceId,
                    "Ready",
                    CorrelationId.Create($"item-{index}"),
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            var completed = (await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = started.InstanceId },
                TestContext.Current.CancellationToken)).Single();
            var checkpoint = await store.LoadCheckpointAsync(
                started.InstanceId,
                TestContext.Current.CancellationToken);
            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);

            completed.Status.Should().Be(WorkflowStatus.Completed);
            completed.ActiveWaits.Should().BeEmpty();
            envelope.OwnedObligations.Should().BeEmpty();
            mergeCalls.Should().Be(1);
            JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log
                .Should().Equal("0:0", "1:1", "2:2", "3:3", "4:4", "5:5");
        }
    }

    [Fact]
    public async Task SelectedParallel_MergeFailureCommitsFailedScopeWithoutRerunningBranches()
    {
        var trace = new List<string>();
        var store = new InMemoryWorkflowProvider();
        var definition = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
    public async Task SelectedContinueAsNew_NonQuiescentRootFailsWithoutChangingGenerationStateOrOwnership()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var definition = global::OrcaCore.Workflow.Durable<RolloverState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new RolloverState(0))
            .ContinueAsNew(state => state with
            {
                Generation = state.Generation + 1
            })
            .Build();
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
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
        var waitId = WaitId.Parse(Guid.CreateVersion7().ToString());
        var child = new FiberRecord(
            childId,
            scopeId,
            definition.CompiledPlan.Instructions.Single(instruction =>
                instruction.Kind == CompiledInstructionKind.Init).Id,
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
        IResourcePoolStore? resourcePoolStore = null,
        int maxConcurrentExecutionPathsPerInstance = int.MaxValue,
        IServiceProvider? serviceProvider = null)
    {
        var processor = new DurableCommandProcessor(store, resourcePoolStore);
        var management = new global::OrcaCore.Engine.Durable.Management.DurableManagement(
            store,
            resourcePoolStore,
            store,
            processor);
        return new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(maxConcurrentExecutionPathsPerInstance, serviceProvider),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer(),
            budget,
            projectionStore: store,
            management: management);
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
                new StepResult.WaitForEvent(EventName.Create("Continue"), WaitCorrelation));
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
            return ValueTask.FromResult<StepResult>(global::OrcaCore.TestSupport.LegacyStepResults.RunExternalJob(
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
            return ValueTask.FromResult<StepResult>(global::OrcaCore.TestSupport.LegacyStepResults.AcquireResources(
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
                ? global::OrcaCore.TestSupport.LegacyStepResults.Yield()
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
                return ValueTask.FromResult<StepResult>(global::OrcaCore.TestSupport.LegacyStepResults.Yield());
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
            return ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(EventName.Create(context.State.EventName),
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

    private sealed record TerminalOutput(string Value);

    private sealed record TaggedWorkItem(string GroupId, string UnitKind, string UnitId);

    private sealed record TaggedItemResult(
        string GroupId,
        string UnitKind,
        string UnitId,
        string Observation);

    private sealed class TaggedItemState
    {
        public int Index { get; set; }

        public TaggedWorkItem Item { get; set; } = null!;

        public string Observation { get; set; } = string.Empty;
    }

    private sealed class TaggedDeployStep : IStep<TaggedItemState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TaggedItemState> context,
            CancellationToken cancellationToken)
        {
            context.State.Observation = "deployed";
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class TaggedVerifyStep : IStep<TaggedItemState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TaggedItemState> context,
            CancellationToken cancellationToken)
        {
            context.State.Observation = "verified";
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class TaggedStepServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(TaggedDeployStep)
                ? new TaggedDeployStep()
                : serviceType == typeof(TaggedVerifyStep)
                    ? new TaggedVerifyStep()
                    : null;
    }

    private record PolymorphicOutputBase(string Value);

    private sealed record PolymorphicOutputDerived(string Value, string Detail)
        : PolymorphicOutputBase(Value);

    private sealed class DurableProbeDependency
    {
        internal int ExecutionCount { get; set; }
    }

    private sealed class MaybeFailForEachStep : IStep<WaitingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingBranchState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                context.State.Name == "one"
                    ? new StepResult.Failed(new WorkflowLifecycleException("item failed"))
                    : new StepResult.Completed());
        }
    }

    private sealed class ConstructorInjectedDurableStep(DurableProbeDependency dependency) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            dependency.ExecutionCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class DurableStepServiceProvider(ConstructorInjectedDurableStep step) : IServiceProvider
    {
        internal List<Type> RequestedTypes { get; } = [];

        public object? GetService(Type serviceType)
        {
            RequestedTypes.Add(serviceType);
            return serviceType == typeof(ConstructorInjectedDurableStep) ? step : null;
        }
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
        private int schemaResolutionCalls;

        internal int SchemaResolutionCalls => Volatile.Read(ref schemaResolutionCalls);

        public bool TryGetSchemaIdentity(Type type, out string schemaIdentity)
        {
            Interlocked.Increment(ref schemaResolutionCalls);
            schemaIdentity = $"tracking:{type.AssemblyQualifiedName}";
            return true;
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
