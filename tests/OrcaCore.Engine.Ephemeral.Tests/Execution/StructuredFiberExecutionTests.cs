using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class StructuredFiberExecutionTests
{
    private static readonly CorrelationId WaitCorrelation = new("structured-wait");

    [Fact]
    public async Task SelectedParallel_UsesConfiguredSerializerRegistryAtRuntime()
    {
        var registry = new TrackingSerializerRegistry();
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithTypeSerializerRegistry(registry)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "tracked",
                    parent => new BranchState(parent.Value.Value, "tracked"),
                    branch => branch.Return(state => state.Value.Result)),
                (parent, results) => parent.Value with
                {
                    Results = results.Select(result => result.Value).ToList()
                })
            .End("done")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "serializer",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        registry.SerializeCalls.Should().BeGreaterThan(0);
        registry.DeserializeCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectedParallel_ZeroBackoffRetryRunsSiblingBeforeNextAttempt()
    {
        var trace = new List<string>();
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<YieldingBranchState>(
                        "retrying",
                        _ => new YieldingBranchState("retrying"),
                        branch => branch
                            .WithRetry(2)
                            .Then(() => new RetryOnceStep(trace))
                            .Return(state => state.Value.Name))
                    .Branch<YieldingBranchState>(
                        "sibling",
                        _ => new YieldingBranchState("sibling"),
                        branch => branch
                            .Then(() => new TraceStep(trace))
                            .Return(state => state.Value.Name)),
                (parent, _) => parent.Value)
            .End("done")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "retry",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        trace.Should().Equal("retrying:attempt:1", "sibling", "retrying:attempt:2");
    }

    [Fact]
    public async Task SelectedRoot_DelayedRetryBackgroundFailureFailsInstanceObservably()
    {
        var trace = new List<string>();
        var clock = new Clock(new DateTimeOffset(2026, 7, 14, 9, 0, 0, TimeSpan.Zero));
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = Workflow.Ephemeral<YieldingBranchState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new YieldingBranchState("retrying"))
            .WithRetry(2, TimeSpan.FromMilliseconds(10))
            .Then(() => new RetryOnceStep(trace))
            .End(_ =>
            {
                resumed.TrySetResult();
                throw new InvalidOperationException("retry outcome exploded");
            })
            .Build();
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, YieldingBranchState>(
            definition.DefinitionId,
            "retry",
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        await resumed.Task.WaitAsync(TestContext.Current.CancellationToken);
        var failed = await WaitForStatusAsync(
            engine,
            waiting.InstanceId,
            WorkflowStatus.Failed,
            TestContext.Current.CancellationToken);

        failed.ErrorSummary.Should().Contain("retry outcome exploded");
    }

    [Fact]
    public async Task SelectedParallel_SaturatedPoolReleasesTurnForRunnableSiblingAndResumesOwner()
    {
        var holderStep = new BlockingBranchStep();
        var trace = new List<string>();
        var pooledRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var targetDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions { NamedPools = { ["db"] = 1 } });
        var holder = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "holder",
                    parent => new BranchState(parent.Value.Value, "holder"),
                    branch => branch
                        .WithPoolKey("db")
                        .Then(() => holderStep)
                        .Return(state => state.Value.Result)),
                (parent, _) => parent.Value)
            .End("held")
            .Build();
        var target = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        "pooled",
                        parent => new BranchState(parent.Value.Value, "pooled"),
                        branch => branch
                            .WithPoolKey("db")
                            .Then(() => new SignalBranchStep(trace, pooledRan))
                            .Return(state => state.Value.Result))
                    .Branch<BranchState>(
                        "sibling",
                        parent => new BranchState(parent.Value.Value, "sibling"),
                        branch => branch
                            .Then(() => new SignalBranchStep(trace))
                            .Return(state => state.Value.Result)),
                (parent, _) => parent.Value)
            .Then(() => new SignalParentStep(targetDone))
            .End("done")
            .Build();
        engine.RegisterDefinition(holder);
        engine.RegisterDefinition(target);

        var holding = engine.StartAsync<string, ParentState>(
            holder.DefinitionId,
            "holder",
            TestContext.Current.CancellationToken);
        await holderStep.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var waiting = await engine.StartAsync<string, ParentState>(
            target.DefinitionId,
            "target",
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        trace.Should().Equal("sibling");
        pooledRan.Task.IsCompleted.Should().BeFalse();

        holderStep.Release();
        await holding.WaitAsync(TestContext.Current.CancellationToken);
        await targetDone.Task.WaitAsync(TestContext.Current.CancellationToken);

        trace.Should().Equal("sibling", "pooled");
        engine.Management.Instance(waiting.InstanceId).Get().Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task SelectedRoot_ResourceGrantBackgroundFailureFailsInstanceObservably()
    {
        var holderStep = new BlockingBranchStep();
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions { NamedPools = { ["db"] = 1 } });
        var holder = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "holder",
                    parent => new BranchState(parent.Value.Value, "holder"),
                    branch => branch
                        .WithPoolKey("db")
                        .Then(() => holderStep)
                        .Return(state => state.Value.Result)),
                (parent, _) => parent.Value)
            .End("held")
            .Build();
        var target = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .WithPoolKey("db")
            .Then(() => new AppendStep())
            .End(_ =>
            {
                resumed.TrySetResult();
                throw new InvalidOperationException("resource outcome exploded");
            })
            .Build();
        engine.RegisterDefinition(holder);
        engine.RegisterDefinition(target);

        var holding = engine.StartAsync<string, ParentState>(
            holder.DefinitionId,
            "holder",
            TestContext.Current.CancellationToken);
        await holderStep.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var waiting = await engine.StartAsync<string, ParentState>(
            target.DefinitionId,
            "target",
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        holderStep.Release();
        await holding.WaitAsync(TestContext.Current.CancellationToken);
        await resumed.Task.WaitAsync(TestContext.Current.CancellationToken);
        var failed = await WaitForStatusAsync(
            engine,
            waiting.InstanceId,
            WorkflowStatus.Failed,
            TestContext.Current.CancellationToken);

        failed.ErrorSummary.Should().Contain("resource outcome exploded");
    }

    [Fact]
    public async Task SelectedParallel_OversizedResultFailsOwningScopeBeforeMerge()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions { MaxSerializedResultBytes = 8 })
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "oversized",
                    parent => new BranchState(parent.Value.Value, new string('x', 64)),
                    branch => branch.Return(state => state.Value.Result)),
                (parent, _) => parent.Value)
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var failed = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "oversized",
            TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ErrorSummary.Should().Contain("SFE-LIMIT-009");
    }

    [Fact]
    public async Task SelectedParallel_ThrowingMergeFailsInstanceObservably()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "branch",
                    parent => new BranchState(parent.Value.Value, "result"),
                    branch => branch.Return(state => state.Value.Result)),
                (_, _) => throw new InvalidOperationException("merge exploded"))
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var failed = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "merge",
            TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ErrorSummary.Should().Contain("merge exploded");
    }

    [Fact]
    public async Task SelectedParallel_ThrowingBranchReturnProjectorFailsInstanceObservably()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "branch",
                    parent => new BranchState(parent.Value.Value, "result"),
                    branch => branch.Return(_ =>
                        throw new InvalidOperationException("branch projection exploded"))),
                (parent, _) => parent.Value)
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var failed = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "projection",
            TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ErrorSummary.Should().Contain("Branch return projection");
    }

    [Fact]
    public async Task SelectedWait_ThrowingCorrelationSelectorAfterResumeFailsInstanceObservably()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait("First", _ => WaitCorrelation)
            .Wait("Second", _ => throw new InvalidOperationException("correlation exploded"))
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "correlation",
            TestContext.Current.CancellationToken);

        var failed = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = "First",
                CorrelationId = WaitCorrelation,
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ActiveWaits.Should().BeEmpty();
        failed.ErrorSummary.Should().Contain("wait selector");
    }

    [Fact]
    public async Task SelectedStep_UserNotSupportedExceptionFailsThroughStepBoundary()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Then<ThrowNotSupportedStep>()
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var failed = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "unsupported",
            TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ErrorSummary.Should().Contain("user not supported");
    }

    [Fact]
    public async Task SelectedParallel_ExecutesIsolatedBranchesAndMergesInAuthoredOrder()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("first", parent => new BranchState(parent.Value.Value, "first"), branch =>
                        branch.Return(state => state.Value.Result))
                    .Branch<BranchState>("second", parent => new BranchState(parent.Value.Value, "second"), branch =>
                        branch.Return(state => state.Value.Result)),
                (parent, results) => parent.Value with
                {
                    Results = results.Select(result => result.Value).ToList()
                })
            .End("merged")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "order",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.EndOutcomeName.Should().Be("merged");
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("first", "second");
    }

    [Fact]
    public async Task SelectedParallel_RemainsRunningWhileSiblingWaitsAndAnotherFiberExecutes()
    {
        var blockingStep = new BlockingBranchStep();
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "waiting",
                        _ => new WaitingBranchState("waiting", "Continue"),
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        "running",
                        parent => new BranchState(parent.Value.Value, "running"),
                        branch => branch
                            .Then(() => blockingStep)
                            .Return(state => state.Value.Result)),
                (parent, results) => parent.Value with
                {
                    Results = results.Select(result => result.Value).ToList()
                })
            .End("merged")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var start = engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "status",
            TestContext.Current.CancellationToken);
        await blockingStep.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var running = engine.Management.All().Get();

        running.Status.Should().Be(WorkflowStatus.Running);
        running.ActiveWaits.Should().ContainSingle();

        blockingStep.Release();
        var waiting = await start.WaitAsync(TestContext.Current.CancellationToken);
        var completed = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = "Continue",
                CorrelationId = WaitCorrelation,
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task SelectedParallel_UnscopedDeliveryConsumesEarliestWaitSequence()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "first",
                        _ => new WaitingBranchState("first", "Ready"),
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "second",
                        _ => new WaitingBranchState("second", "Ready"),
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name)),
                (parent, results) => parent.Value with
                {
                    Results = results.Select(result => result.Value).ToList()
                })
            .End("merged")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "sequence",
            TestContext.Current.CancellationToken);
        var ordered = waiting.ActiveWaits
            .OrderBy(wait => wait.WaitSequence)
            .ThenBy(wait => wait.FiberId?.Value ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var afterFirst = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            ReadyEvent(),
            TestContext.Current.CancellationToken);

        waiting.ActiveWaits.Should().HaveCount(2);
        waiting.ActiveWaits.Select(wait => wait.WaitSequence).Should().OnlyHaveUniqueItems();
        waiting.ActiveWaits.Should().OnlyContain(wait => wait.FiberId.HasValue);
        afterFirst.ActiveWaits.Should().ContainSingle();
        afterFirst.ActiveWaits.Single().WaitId.Should().Be(ordered[1].WaitId);

        var completed = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            ReadyEvent(),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);

        static EventEnvelope ReadyEvent() => new()
        {
            EventId = EventId.New(),
            EventName = "Ready",
            CorrelationId = WaitCorrelation,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task SelectedNestedParallel_MergesIntoOuterPrivateStateBeforeRootMerge()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<OuterNestedState>(
                        "nested",
                        _ => new OuterNestedState("nested", 0),
                        branch => branch
                            .Parallel<int>(
                                nested => nested
                                    .Branch<NumberState>(
                                        "one",
                                        _ => new NumberState(1),
                                        child => child.Return(state => state.Value.Value))
                                    .Branch<NumberState>(
                                        "two",
                                        _ => new NumberState(2),
                                        child => child.Return(state => state.Value.Value)),
                                (parent, results) => parent.Value with
                                {
                                    Total = results.Sum(result => result.Value)
                                })
                            .Return(state => $"{state.Value.Name}:{state.Value.Total}"))
                    .Branch<OuterNestedState>(
                        "plain",
                        _ => new OuterNestedState("plain", 0),
                        branch => branch.Return(state => state.Value.Name)),
                (parent, results) => parent.Value with
                {
                    Results = results.Select(result => result.Value).ToList()
                })
            .End("nested")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "nested",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("nested:3", "plain");
    }

    [Fact]
    public async Task SelectedStraightLineStep_ExecutesThroughCompiledPlan()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Then<AppendStep>()
            .End("stepped")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "order",
            TestContext.Current.CancellationToken);

        completed.EndOutcomeName.Should().Be("stepped");
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("step");
    }

    [Fact]
    public async Task SelectedStructuralChain_EnforcesInternalInstructionQuantumBudget()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions
            {
                MaxInternalInstructionsPerQuantum = 1
            })
            .Init<string>(value => new ParentState(value, []))
            .If(_ => true, _ => { })
            .End("done")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "budget",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.LifecycleEvents.Should().Contain(lifecycleEvent =>
            lifecycleEvent.EventName == "FiberQuantumRotated");
    }

    [Fact]
    public async Task SelectedParallel_YieldPersistsPrivateStateAndRotatesBranches()
    {
        var trace = new List<string>();
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<YieldingBranchState>(
                        "first",
                        _ => new YieldingBranchState("first"),
                        branch => branch
                            .Then(() => new YieldOnceStep(trace))
                            .Return(state => $"{state.Value.Name}:{state.Value.Attempts}"))
                    .Branch<YieldingBranchState>(
                        "second",
                        _ => new YieldingBranchState("second"),
                        branch => branch
                            .Then(() => new YieldOnceStep(trace))
                            .Return(state => $"{state.Value.Name}:{state.Value.Attempts}")),
                (parent, results) => parent.Value with
                {
                    Results = results.Select(result => result.Value).ToList()
                })
            .End("yielded")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "order",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        trace.Should().Equal("first:yield", "second:yield", "first:complete", "second:complete");
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("first:2", "second:2");
    }

    [Fact]
    public async Task SelectedWait_ResumesOwningFiberAndProvidesEventToNextStep()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Then<WaitStep>()
            .Then<CaptureResumedEventStep>()
            .End("resumed")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "order",
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        waiting.ActiveWaits.Should().ContainSingle();

        var completed = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = "Continue",
                CorrelationId = WaitCorrelation,
                Payload = "payload",
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("payload");
    }

    [Fact]
    public async Task SelectedStructuralWait_ResumesOwningFiberAndProvidesEventToNextStep()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait("Continue", _ => WaitCorrelation)
            .Then<CaptureResumedEventStep>()
            .End("resumed")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "order",
            TestContext.Current.CancellationToken);
        var completed = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = "Continue",
                CorrelationId = WaitCorrelation,
                Payload = "structural-payload",
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("structural-payload");
    }

    [Fact]
    public async Task SelectedWhenFirst_CancelsLosingFiberWaitBeforeParentCompletes()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .WhenFirst<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "first",
                        _ => new WaitingBranchState("first", "FirstReady"),
                        branch => branch
                            .Wait("FirstReady", _ => WaitCorrelation)
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "second",
                        _ => new WaitingBranchState("second", "SecondReady"),
                        branch => branch
                            .Wait("SecondReady", _ => WaitCorrelation)
                            .Return(state => state.Value.Name)),
                (parent, winner) => parent.Value with
                {
                    Results = [winner.Value]
                })
            .End("winner")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "order",
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        waiting.ActiveWaits.Should().HaveCount(2);

        var completed = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = "FirstReady",
                CorrelationId = WaitCorrelation,
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("first");
    }

    [Fact]
    public async Task SelectedWhenFirst_CancelsLosingFiberDelayBeforeParentCompletes()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 13, 11, 0, 0, TimeSpan.Zero));
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .WhenFirst<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "timer-loser",
                        _ => new WaitingBranchState("loser", string.Empty),
                        branch => branch
                            .Delay(TimeSpan.FromMinutes(5))
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "winner",
                        _ => new WaitingBranchState("winner", string.Empty),
                        branch => branch.Return(state => state.Value.Name)),
                (parent, winner) => parent.Value with { Results = [winner.Value] })
            .End("winner")
            .Build();
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        engine.RegisterDefinition(definition);

        var completed = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "timer-race",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));
        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        fired.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedWhenFirst_CancelsNestedLosingScopeWaitsBeforeParentCompletes()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .WhenFirst<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "winner",
                        _ => new WaitingBranchState("winner", "WinnerReady"),
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<WaitingBranchState>(
                        "nested-loser",
                        _ => new WaitingBranchState("loser", string.Empty),
                        branch => branch
                            .Parallel<string>(
                                nested => nested
                                    .Branch<WaitingBranchState>(
                                        "nested-a",
                                        _ => new WaitingBranchState("nested-a", "NestedA"),
                                        child => child
                                            .Then<BranchWaitStep>()
                                            .Return(state => state.Value.Name))
                                    .Branch<WaitingBranchState>(
                                        "nested-b",
                                        _ => new WaitingBranchState("nested-b", "NestedB"),
                                        child => child
                                            .Then<BranchWaitStep>()
                                            .Return(state => state.Value.Name)),
                                (parent, _) => parent.Value)
                            .Return(state => state.Value.Name)),
                (parent, winner) => parent.Value with
                {
                    Results = [winner.Value]
                })
            .End("winner")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "nested-race",
            TestContext.Current.CancellationToken);
        waiting.ActiveWaits.Should().HaveCount(3);

        var completed = await engine.RaiseEventAsync<ParentState>(
            waiting.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = "WinnerReady",
                CorrelationId = WaitCorrelation,
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
    }

    [Theory]
    [InlineData(UnsupportedDurableResult.ExternalJob)]
    [InlineData(UnsupportedDurableResult.ResourceAcquisition)]
    public async Task SelectedParallel_DurableOnlyResultRejectsAndCleansPreviouslyOwnedWait(
        UnsupportedDurableResult result)
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(
                branches => branches
                    .Branch<WaitingBranchState>(
                        "waiting",
                        _ => new WaitingBranchState("waiting", "NeverDelivered"),
                        branch => branch
                            .Then<BranchWaitStep>()
                            .Return(state => state.Value.Name))
                    .Branch<UnsupportedResultState>(
                        "unsupported",
                        _ => new UnsupportedResultState(result),
                        branch => branch
                            .Then<UnsupportedDurableResultStep>()
                            .Return(_ => "unsupported")),
                (parent, _) => parent.Value)
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var start = async () => await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "unsupported",
            TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<NotSupportedException>();
        engine.Management.All().Get().ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedNestedScope_UsesCompiledControlFlowContinuations()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .If(
                state => state.Value == "run",
                then => then.Parallel<string>(
                    branches => branches
                        .Branch<BranchState>(
                            "nested",
                            parent => new BranchState(parent.Value.Value, "nested"),
                            branch => branch.Return(state => state.Value.Result)),
                    (parent, results) => parent.Value with
                    {
                        Results = results.Select(result => result.Value).ToList()
                    }),
                otherwise => otherwise.Then(() => new AppendStep()))
            .End("nested")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "run",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("nested");
    }

    [Fact]
    public async Task SelectedForEach_MaterializesIsolatedItemsAndMergesOutcomesByIndex()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ForEachBranchState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new ForEachBranchState(item.Index, item.Items.Single()),
                body => body.Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                merge: (parent, outcomes) => parent.Value with
                {
                    Results = outcomes.Select(outcome => $"{outcome.Index}:{outcome.Result}").ToList()
                })
            .End("foreach")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "alpha,beta,gamma",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("0:alpha", "1:beta", "2:gamma");
    }

    [Fact]
    public async Task SelectedForEach_MaxConcurrencyLimitsAdmittedNonterminalFibers()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, WaitingForEachState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new WaitingForEachState(item.Index, item.Items.Single()),
                body => body
                    .Then<ForEachWaitStep>()
                    .Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                maxConcurrency: 2,
                merge: (parent, outcomes) => parent.Value with
                {
                    Results = outcomes.Select(outcome => outcome.Result!).ToList()
                })
            .End("foreach")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "alpha,beta,gamma",
            TestContext.Current.CancellationToken);

        snapshot.ActiveWaits.Select(wait => wait.EventName)
            .Should().BeEquivalentTo("Item-0", "Item-1");

        snapshot = await RaiseItemAsync(engine, snapshot, 0);
        snapshot.ActiveWaits.Select(wait => wait.EventName)
            .Should().BeEquivalentTo("Item-1", "Item-2");

        snapshot = await RaiseItemAsync(engine, snapshot, 2);
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot = await RaiseItemAsync(engine, snapshot, 1);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(snapshot.InstanceId).GetState<ParentState>().Results
            .Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public async Task SelectedForEach_WhenAnyMergesOnlyWinnerAndCancelsResidualWaits()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, WaitingForEachState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new WaitingForEachState(item.Index, item.Items.Single()),
                body => body
                    .Then<ForEachWaitStep>()
                    .Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAny,
                ForEachFailurePolicy.FailFast,
                merge: (parent, outcomes) => parent.Value with
                {
                    Results = outcomes.Select(outcome => $"{outcome.Index}:{outcome.Result}").ToList()
                })
            .End("winner")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "alpha,beta,gamma",
            TestContext.Current.CancellationToken);
        waiting.ActiveWaits.Should().HaveCount(3);

        var completed = await RaiseItemAsync(engine, waiting, 1);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("1:beta");
    }

    [Fact]
    public async Task SelectedForEach_WhenAnyCancelsNestedResidualScopesBeforeMerge()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, WaitingForEachState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new WaitingForEachState(item.Index, item.Items.Single()),
                body => body
                    .Parallel<string>(
                        nested => nested.Branch<WaitingForEachState>(
                            "nested-wait",
                            parent => parent.Value,
                            child => child
                                .Then<ForEachWaitStep>()
                                .Return(state => state.Value.Value)),
                        (parent, _) => parent.Value)
                    .Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAny,
                ForEachFailurePolicy.FailFast,
                merge: (parent, outcomes) => parent.Value with
                {
                    Results = outcomes.Select(outcome => $"{outcome.Index}:{outcome.Result}").ToList()
                })
            .End("winner")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "alpha,beta,gamma",
            TestContext.Current.CancellationToken);
        waiting.ActiveWaits.Should().HaveCount(3);

        var completed = await RaiseItemAsync(engine, waiting, 1);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("1:beta");
    }

    [Fact]
    public async Task SelectedForEach_ContinueWithPartialFailuresMergesEveryOutcome()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, WaitingForEachState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new WaitingForEachState(item.Index, item.Items.Single()),
                body => body
                    .Then<MaybeFailForEachStep>()
                    .Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.ContinueWithPartialFailures,
                merge: (parent, outcomes) => parent.Value with
                {
                    Results = outcomes
                        .Select(outcome => $"{outcome.Index}:{outcome.Status}:{outcome.Result}")
                        .ToList()
                })
            .End("partial")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "alpha,beta,gamma",
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal(
                "0:Succeeded:alpha",
                "1:Failed:",
                "2:Succeeded:gamma");
    }

    [Fact]
    public async Task SelectedForEach_WaitAllThenFailWaitsForEveryAdmittedItem()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, WaitingForEachState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new WaitingForEachState(item.Index, item.Items.Single()),
                body => body
                    .Then<WaitOrFailForEachStep>()
                    .Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.WaitAllThenFail)
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "alpha,beta,gamma",
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        waiting.ActiveWaits.Select(wait => wait.EventName)
            .Should().BeEquivalentTo("Item-0", "Item-2");

        waiting = await RaiseItemAsync(engine, waiting, 2);
        waiting.Status.Should().Be(WorkflowStatus.Waiting);

        var failed = await RaiseItemAsync(engine, waiting, 0);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ActiveWaits.Should().BeEmpty();
        failed.ErrorSummary.Should().Contain("item failed");
    }

    [Fact]
    public async Task SelectedForEach_WhenAnyFailedWinnerFailsWithoutMerge()
    {
        var mergeCalls = 0;
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, WaitingForEachState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new WaitingForEachState(item.Index, item.Items.Single()),
                body => body
                    .Then<FailFirstForEachStep>()
                    .Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAny,
                ForEachFailurePolicy.FailFast,
                merge: (parent, _) =>
                {
                    mergeCalls++;
                    return parent.Value;
                })
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var failed = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "beta,alpha,gamma",
            TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ActiveWaits.Should().BeEmpty();
        mergeCalls.Should().Be(0);
    }

    [Fact]
    public async Task SelectedForEach_BatchPartitionCreatesOneFiberPerPartition()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ForEachBranchState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Batch(2),
                item => new ForEachBranchState(item.Index, string.Join('+', item.Items)),
                body => body.Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                merge: (parent, outcomes) => parent.Value with
                {
                    Results = outcomes.Select(outcome => outcome.Result!).ToList()
                })
            .End("batches")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<string, ParentState>(
            definition.DefinitionId,
            "a,b,c,d,e",
            TestContext.Current.CancellationToken);

        engine.Management.Instance(completed.InstanceId).GetState<ParentState>().Results
            .Should().Equal("a+b", "c+d", "e");
    }

    [Fact]
    public async Task SelectedForEach_FailFastCancelsPreviouslyRegisteredSiblingWait()
    {
        var mergeCalls = 0;
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, WaitingForEachState, string>(
                parent => parent.Value.Value.Split(','),
                WorkflowPartitioner<string>.Items(),
                item => new WaitingForEachState(item.Index, item.Items.Single()),
                body => body
                    .Then<WaitOrFailForEachStep>()
                    .Return(state => state.Value.Value),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                merge: (parent, _) =>
                {
                    mergeCalls++;
                    return parent.Value;
                })
            .End("unreachable")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var failed = await engine.StartAsync<string, ParentState>(
            definition.DefinitionId,
            "alpha,beta,gamma",
            TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowStatus.Failed);
        failed.ActiveWaits.Should().BeEmpty();
        mergeCalls.Should().Be(0);
    }

    [Fact]
    public async Task SelectedForEach_ItemStateMaterializationBreaksParentAliases()
    {
        var input = new MutableForEachParentState(
            [new MutableItem("a"), new MutableItem("b")],
            []);
        var definition = Workflow.Ephemeral<MutableForEachParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<MutableForEachParentState>(state => state)
            .ForEach<MutableItem, MutableItemState, string>(
                parent => parent.Value.Items,
                WorkflowPartitioner<MutableItem>.Items(),
                item => new MutableItemState(item.Index, item.Items.Single()),
                body => body
                    .Then<MutatePrivateItemStep>()
                    .Return(state => state.Value.Item.Value),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                merge: (parent, outcomes) => parent.Value with
                {
                    Results = outcomes.Select(outcome => outcome.Result!).ToList()
                })
            .End("isolated")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var completed = await engine.AwaitCompletionAsync<MutableForEachParentState, MutableForEachParentState>(
            definition.DefinitionId,
            input,
            TestContext.Current.CancellationToken);
        var state = engine.Management.Instance(completed.InstanceId).GetState<MutableForEachParentState>();

        state.Items.Select(item => item.Value).Should().Equal("a", "b");
        state.Results.Should().Equal("a-branch", "b-branch");
    }

    private static Task<WorkflowInstanceSnapshot> RaiseItemAsync(
        EphemeralWorkflowEngine engine,
        WorkflowInstanceSnapshot snapshot,
        int index)
    {
        return engine.RaiseEventAsync<ParentState>(
            snapshot.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.New(),
                EventName = $"Item-{index}",
                CorrelationId = new CorrelationId($"item-{index}"),
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<WorkflowInstanceSnapshot> WaitForStatusAsync(
        EphemeralWorkflowEngine engine,
        InstanceId instanceId,
        WorkflowStatus status,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 1_000; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = engine.Management.Instance(instanceId).Get();
            if (snapshot.Status == status)
            {
                return snapshot;
            }

            await Task.Yield();
        }

        return engine.Management.Instance(instanceId).Get();
    }

    private sealed class AppendStep : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken)
        {
            context.State.Results.Add("step");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ThrowNotSupportedStep : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("user not supported");
        }
    }

    private sealed class YieldOnceStep(List<string> trace) : IStep<YieldingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            var phase = context.State.Attempts == 1 ? "yield" : "complete";
            trace.Add($"{context.State.Name}:{phase}");
            return ValueTask.FromResult<StepResult>(context.State.Attempts == 1
                ? new StepResult.Yield()
                : new StepResult.Completed());
        }
    }

    private sealed class RetryOnceStep(List<string> trace) : IStep<YieldingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            trace.Add($"{context.State.Name}:attempt:{context.State.Attempts}");
            return ValueTask.FromResult<StepResult>(context.State.Attempts == 1
                ? new StepResult.Failed(new WorkflowDefinitionException("retry"))
                : new StepResult.Completed());
        }
    }

    private sealed class TraceStep(List<string> trace) : IStep<YieldingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldingBranchState> context,
            CancellationToken cancellationToken)
        {
            trace.Add(context.State.Name);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class SignalBranchStep(
        List<string> trace,
        TaskCompletionSource? signal = null) : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            trace.Add(context.State.Result);
            signal?.TrySetResult();
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class SignalParentStep(TaskCompletionSource signal) : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken)
        {
            signal.TrySetResult();
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class WaitStep : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent("Continue", WaitCorrelation));
        }
    }

    private sealed class CaptureResumedEventStep : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Results.Add(payload);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class BranchWaitStep : IStep<WaitingBranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingBranchState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent(context.State.EventName, WaitCorrelation));
        }
    }

    private sealed class BlockingBranchStep : IStep<BranchState>
    {
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Release() => release.TrySetResult();

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    private sealed class ForEachWaitStep : IStep<WaitingForEachState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingForEachState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(
                $"Item-{context.State.Index}",
                new CorrelationId($"item-{context.State.Index}")));
        }
    }

    private sealed class MaybeFailForEachStep : IStep<WaitingForEachState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingForEachState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(context.State.Index == 1
                ? new StepResult.Failed(new WorkflowDefinitionException("item failed"))
                : new StepResult.Completed());
        }
    }

    private sealed class WaitOrFailForEachStep : IStep<WaitingForEachState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingForEachState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(context.State.Index == 1
                ? new StepResult.Failed(new WorkflowDefinitionException("item failed"))
                : new StepResult.WaitForEvent(
                    $"Item-{context.State.Index}",
                    new CorrelationId($"item-{context.State.Index}")));
        }
    }

    private sealed class FailFirstForEachStep : IStep<WaitingForEachState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<WaitingForEachState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(context.State.Index == 0
                ? new StepResult.Failed(new WorkflowDefinitionException("winner failed"))
                : new StepResult.Completed());
        }
    }

    private sealed class MutatePrivateItemStep : IStep<MutableItemState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<MutableItemState> context,
            CancellationToken cancellationToken)
        {
            context.State.Item.Value += "-branch";
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class UnsupportedDurableResultStep : IStep<UnsupportedResultState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<UnsupportedResultState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(context.State.Result switch
            {
                UnsupportedDurableResult.ExternalJob => new StepResult.RunExternalJob("job", []),
                UnsupportedDurableResult.ResourceAcquisition => new StepResult.AcquireResources(
                    "holder",
                    [new ResourcePoolRequirement("pool", 1)]),
                _ => throw new InvalidOperationException("Unknown unsupported result.")
            });
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

    private sealed record ParentState(string Value, List<string> Results);

    private sealed record BranchState(string ParentValue, string Result);

    private sealed record YieldingBranchState(string Name)
    {
        public int Attempts { get; set; }
    }

    private sealed record WaitingBranchState(string Name, string EventName);

    private sealed record OuterNestedState(string Name, int Total);

    private sealed record NumberState(int Value);

    private sealed record ForEachBranchState(int Index, string Value);

    private sealed record WaitingForEachState(int Index, string Value);

    public enum UnsupportedDurableResult
    {
        ExternalJob,
        ResourceAcquisition
    }

    private sealed record UnsupportedResultState(UnsupportedDurableResult Result);

    private sealed record MutableForEachParentState(
        IReadOnlyList<MutableItem> Items,
        IReadOnlyList<string> Results);

    private sealed record MutableItemState(int Index, MutableItem Item);

    private sealed class MutableItem(string value)
    {
        public string Value { get; set; } = value;
    }
}
