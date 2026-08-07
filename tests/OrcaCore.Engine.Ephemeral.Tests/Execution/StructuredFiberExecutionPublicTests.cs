using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class StructuredFiberExecutionPublicTests
{
    private static readonly EventName StartWork = EventName.Create("structured-start");
    private static readonly EventName Continue = EventName.Create("structured-continue");
    private static readonly EventName Ready = EventName.Create("structured-ready");
    private static readonly CorrelationId WaitCorrelation =
        CorrelationId.Create("structured-wait");

    [Fact]
    public async Task SelectedParallel_ZeroBackoffRetryRunsSiblingBeforeNextAttempt()
    {
        var trace = new ConcurrentQueue<string>();
        var retry = new RetryOnceStep(trace);
        using var provider = CreateProvider(
            services => services.AddSingleton(retry),
            pathCeiling: 2);
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("retrying"),
                    _ => new BranchState("retrying"),
                    branch => branch
                        .Then<RetryOnceStep>()
                        .WithRetry(2)
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("sibling"),
                    _ => new BranchState("sibling"),
                    branch => branch
                        .Then(_ =>
                        {
                            trace.Enqueue("sibling");
                            return ValueTask.CompletedTask;
                        })
                        .Return(state => state.Value.Name)))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "retry-fairness");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        trace.Should().Equal("retrying:1", "sibling", "retrying:2");
    }

    [Fact]
    public async Task SelectedParallel_WhenAllFailureStillRunsEveryAuthoredSibling()
    {
        var trace = new ConcurrentQueue<string>();
        using var provider = CreateProvider(
            services => services.AddSingleton(new FailingBranchStep(trace)));
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("failing"),
                    _ => new BranchState("failing"),
                    branch => branch
                        .Then<FailingBranchStep>()
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("sibling"),
                    _ => new BranchState("sibling"),
                    branch => branch
                        .Then(_ =>
                        {
                            trace.Enqueue("sibling");
                            return ValueTask.CompletedTask;
                        })
                        .Return(state => state.Value.Name)))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "wait-all-failure");
        var snapshot = await WaitForStatusAsync(
            instance,
            WorkflowInstanceStatus.Failed);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        trace.Should().Equal("failing", "sibling");
    }

    [Fact]
    public async Task SelectedParallel_WhenAllExceptionStillRunsEveryAuthoredSibling()
    {
        var trace = new ConcurrentQueue<string>();
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("throwing"),
                    _ => new BranchState("throwing"),
                    branch => branch
                        .Then(_ =>
                        {
                            trace.Enqueue("throwing");
                            return ValueTask.FromException(
                                new InvalidOperationException("branch exploded"));
                        })
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("sibling"),
                    _ => new BranchState("sibling"),
                    branch => branch
                        .Then(_ =>
                        {
                            trace.Enqueue("sibling");
                            return ValueTask.CompletedTask;
                        })
                        .Return(state => state.Value.Name)))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "wait-all-exception");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        trace.Should().Equal("throwing", "sibling");
    }

    [Fact]
    public async Task SelectedParallel_WhenAllOutcomesMergesOrderedSuccessAndFailureData()
    {
        var trace = new ConcurrentQueue<string>();
        using var provider = CreateProvider(
            services => services.AddSingleton(new FailingBranchStep(trace)));
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("failing"),
                    _ => new BranchState("failing"),
                    branch => branch
                        .Then<FailingBranchStep>()
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("succeeding"),
                    _ => new BranchState("succeeding"),
                    branch => branch.Return(state => state.Value.Name)))
            .WhenAllOutcomes((parent, outcomes) => parent.Value with
            {
                Results = outcomes.Select(outcome => outcome switch
                {
                    BranchOutcome<string>.Succeeded success =>
                        $"{success.BranchId.Value}:success:{success.Result}",
                    BranchOutcome<string>.Failed failure =>
                        $"{failure.BranchId.Value}:failure:{failure.Failure.Code}",
                    _ => throw new InvalidOperationException("Unknown branch outcome.")
                }).ToList()
            })
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "ordered-outcomes");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Failure.Should().BeNull();
        state.Results.Should().Equal(
            "failing:failure:WF-LEGACY-LIFECYCLE",
            "succeeding:success:succeeding");
    }

    [Fact]
    public async Task WideFixedParallel_CompletesWithoutACompilerOwnedFiberCeiling()
    {
        using var provider = CreateProvider(pathCeiling: 1);
        var root = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []));
        var definition = root.Parallel<int>(branches =>
        {
            foreach (var index in Enumerable.Range(0, 257))
            {
                var captured = index;
                branches.Branch<BranchState>(
                    AuthoredBranchId.Create($"branch-{index:D4}"),
                    _ => new BranchState($"branch-{index:D4}"),
                    branch => branch.Return(_ => captured));
            }
        })
        .WhenAll((parent, results) => parent.Value with
        {
            Results = [results.Count.ToString()]
        })
        .End()
        .Build();

        var instance = await StartAsync(provider, definition, "wide-parallel");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal("257");
    }

    [Fact]
    public async Task SelectedRoot_DelayedRetryBackgroundFailureFailsInstanceObservably()
    {
        var clock = new Clock(
            new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero));
        var retry = new RetryOnceStep(new ConcurrentQueue<string>());
        using var provider = CreateProvider(
            services =>
            {
                services.AddSingleton<TimeProvider>(clock.TimeProvider);
                services.AddSingleton(retry);
            });
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Then<RetryOnceStep>()
            .WithRetry(2, TimeSpan.FromMilliseconds(10))
            .Then(_ => ValueTask.FromException(
                new InvalidOperationException("retry continuation exploded")))
            .End()
            .Build();
        var handle = Register(provider, definition);

        var start = handle.StartOrGetAsync(
            "delayed-retry",
            StartIdempotencyKey.Create("structured-delayed-retry"),
            TestContext.Current.CancellationToken).AsTask();
        await retry.FirstAttempt.Task.WaitAsync(TestContext.Current.CancellationToken);
        var instance = (await start).GetHandleOrThrow();
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Waiting);

        clock.Advance(TimeSpan.FromMilliseconds(10));
        var snapshot = await WaitForStatusAsync(
            instance,
            WorkflowInstanceStatus.Failed);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure!.Message.Should().Contain("retry continuation exploded");
    }

    [Fact]
    public async Task SelectedParallel_SaturatedPoolReleasesTurnForRunnableSiblingAndResumesOwner()
    {
        var pool = TransientPoolName.Create("structured-db");
        var trace = new ConcurrentQueue<string>();
        var holder = new AsyncGate();
        using var provider = CreateProvider(
            options: new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 2,
                    StepThrottles = []
                },
                TransientPools = [TransientPoolDefinition.Create(pool, 1)]
            });
        var holderDefinition = WaitingParallelDefinition(
            "pool-holder",
            AuthoredBranchId.Create("holder"),
            branch => branch
                .Then(async (_, cancellationToken) =>
                {
                    holder.Enter();
                    await holder.WaitForReleaseAsync(cancellationToken);
                })
                .WithTransientPool(pool)
                .Return(state => state.Value.Name));
        var targetDefinition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait(WorkflowEventContract.Create(StartWork, EventContractVersion.Initial), _ => WaitCorrelation)
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("pooled"),
                    _ => new BranchState("pooled"),
                    branch => branch
                        .Then(_ =>
                        {
                            trace.Enqueue("pooled");
                            return ValueTask.CompletedTask;
                        })
                        .WithTransientPool(pool)
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("sibling"),
                    _ => new BranchState("sibling"),
                    branch => branch
                        .Then(_ =>
                        {
                            trace.Enqueue("sibling");
                            return ValueTask.CompletedTask;
                        })
                        .Return(state => state.Value.Name)))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        var holderInstance = await StartAsync(provider, holderDefinition, "pool-holder");
        var targetInstance = await StartAsync(provider, targetDefinition, "pool-target");

        var holderDelivery = DeliverAsync(
            provider,
            holderInstance,
            StartWork,
            WaitCorrelation,
            "pool-holder-start").AsTask();
        await holder.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var targetDelivery = DeliverAsync(
            provider,
            targetInstance,
            StartWork,
            WaitCorrelation,
            "pool-target-start").AsTask();
        await WaitForTraceCountAsync(trace, 1);

        trace.Should().Equal("sibling");
        targetDelivery.IsCompleted.Should().BeFalse();

        holder.Release();
        (await holderDelivery).Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await targetDelivery).Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await WaitForStatusAsync(targetInstance, WorkflowInstanceStatus.Completed))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        trace.Should().Equal("sibling", "pooled");
    }

    [Fact]
    public async Task SelectedRoot_ResourceGrantBackgroundFailureFailsInstanceObservably()
    {
        var pool = TransientPoolName.Create("structured-failure-db");
        var holder = new AsyncGate();
        using var provider = CreateProvider(
            options: new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 2,
                    StepThrottles = []
                },
                TransientPools = [TransientPoolDefinition.Create(pool, 1)]
            });
        var holderDefinition = WaitingParallelDefinition(
            "failure-holder",
            AuthoredBranchId.Create("holder"),
            branch => branch
                .Then(async (_, cancellationToken) =>
                {
                    holder.Enter();
                    await holder.WaitForReleaseAsync(cancellationToken);
                })
                .WithTransientPool(pool)
                .Return(state => state.Value.Name));
        var targetDefinition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait(WorkflowEventContract.Create(StartWork, EventContractVersion.Initial), _ => WaitCorrelation)
            .Then(_ => ValueTask.FromException(
                new InvalidOperationException("resource continuation exploded")))
            .WithTransientPool(pool)
            .End()
            .Build();
        var holderInstance = await StartAsync(provider, holderDefinition, "failure-holder");
        var targetInstance = await StartAsync(provider, targetDefinition, "failure-target");

        var holderDelivery = DeliverAsync(
            provider,
            holderInstance,
            StartWork,
            WaitCorrelation,
            "failure-holder-start").AsTask();
        await holder.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var targetDelivery = DeliverAsync(
            provider,
            targetInstance,
            StartWork,
            WaitCorrelation,
            "failure-target-start").AsTask();
        targetDelivery.IsCompleted.Should().BeFalse();

        holder.Release();
        _ = await holderDelivery;
        _ = await targetDelivery;
        var snapshot = await WaitForStatusAsync(
            targetInstance,
            WorkflowInstanceStatus.Failed);

        snapshot.Failure!.Message.Should().Contain("resource continuation exploded");
    }

    [Fact]
    public async Task SelectedParallel_ThrowingMergeFailsInstanceObservably()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches.Branch<BranchState>(
                AuthoredBranchId.Create("branch"),
                _ => new BranchState("result"),
                branch => branch.Return(state => state.Value.Name)))
            .WhenAll((_, _) => throw new InvalidOperationException("merge exploded"))
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "throwing-merge");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure!.Message.Should().Contain("merge exploded");
    }

    [Fact]
    public async Task SelectedParallel_ThrowingBranchReturnProjectorFailsInstanceObservably()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches.Branch<BranchState>(
                AuthoredBranchId.Create("branch"),
                _ => new BranchState("result"),
                branch => branch.Return(_ =>
                    throw new InvalidOperationException("branch projection exploded"))))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "throwing-projection");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure!.Message.Should().Contain("branch projection exploded");
    }

    [Fact]
    public async Task SelectedWait_ThrowingCorrelationSelectorAfterResumeFailsInstanceObservably()
    {
        var first = EventName.Create("structured-first");
        var second = EventName.Create("structured-second");
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait(WorkflowEventContract.Create(first, EventContractVersion.Initial), _ => WaitCorrelation)
            .Wait(WorkflowEventContract.Create(second, EventContractVersion.Initial), _ => throw new InvalidOperationException("correlation exploded"))
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "throwing-correlation");

        var delivery = await DeliverAsync(
            provider,
            instance,
            first,
            WaitCorrelation,
            "throwing-correlation-first");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.ActiveWaits.Should().BeEmpty();
        snapshot.Failure!.Message.Should().Contain("correlation exploded");
    }

    [Fact]
    public async Task SelectedStep_UserNotSupportedExceptionFailsThroughStepBoundary()
    {
        using var provider = CreateProvider(
            services => services.AddTransient<ThrowNotSupportedStep>());
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Then<ThrowNotSupportedStep>()
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "not-supported");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure!.Message.Should().Contain("user not supported");
    }

    [Fact]
    public async Task SelectedParallel_ExecutesIsolatedBranchesAndMergesInAuthoredOrder()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first"),
                    branch => branch
                        .Then(context =>
                        {
                            context.ReplaceState(new BranchState(
                                context.State.Name + "-isolated"));
                            return ValueTask.CompletedTask;
                        })
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second"),
                    branch => branch.Return(state => state.Value.Name)))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = results.Select(result => result.Result).ToList()
            })
            .End(WorkflowOutcomeName.Create("merged"))
            .Build();

        var instance = await StartAsync(provider, definition, "isolated-order");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Outcome.Should().Be(WorkflowOutcomeName.Create("merged"));
        state.Results.Should().Equal("first-isolated", "second");
        state.Input.Should().Be("isolated-order");
    }

    [Fact]
    public async Task SelectedParallel_RemainsRunningWhileSiblingWaitsAndAnotherFiberExecutes()
    {
        var running = new AsyncGate();
        using var provider = CreateProvider(pathCeiling: 2);
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait(WorkflowEventContract.Create(StartWork, EventContractVersion.Initial), _ => WaitCorrelation)
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("waiting"),
                    _ => new BranchState("waiting"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(Continue, EventContractVersion.Initial), _ => WaitCorrelation)
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("running"),
                    _ => new BranchState("running"),
                    branch => branch
                        .Then(async (_, cancellationToken) =>
                        {
                            running.Enter();
                            await running.WaitForReleaseAsync(cancellationToken);
                        })
                        .Return(state => state.Value.Name)))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = results.Select(result => result.Result).ToList()
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "running-and-waiting");

        var startFanout = DeliverAsync(
            provider,
            instance,
            StartWork,
            WaitCorrelation,
            "running-and-waiting-start").AsTask();
        await running.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var active = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        active.Status.Should().Be(WorkflowInstanceStatus.Running);
        active.ActiveWaits.Should().ContainSingle()
            .Which.EventContract.EventName.Should().Be(Continue);

        running.Release();
        _ = await startFanout;
        var waiting = await WaitForStatusAsync(instance, WorkflowInstanceStatus.Waiting);
        waiting.ActiveWaits.Should().ContainSingle();

        var resume = await DeliverAsync(
            provider,
            instance,
            Continue,
            WaitCorrelation,
            "running-and-waiting-continue");
        resume.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await WaitForStatusAsync(instance, WorkflowInstanceStatus.Completed))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    [Fact]
    public async Task SelectedParallel_CeilingOneAdmitsAuthoredOrderAndUnscopedDeliveryUsesEarliestWait()
    {
        using var provider = CreateProvider(pathCeiling: 1);
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait(WorkflowEventContract.Create(StartWork, EventContractVersion.Initial), _ => WaitCorrelation)
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(Ready, EventContractVersion.Initial), _ => WaitCorrelation)
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(Ready, EventContractVersion.Initial), _ => WaitCorrelation)
                        .Return(state => state.Value.Name)))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = results.Select(result => result.Result).ToList()
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "ceiling-one");
        _ = await DeliverAsync(
            provider,
            instance,
            StartWork,
            WaitCorrelation,
            "ceiling-one-start");
        var bothWaiting = await WaitForStatusAsync(instance, WorkflowInstanceStatus.Waiting);
        var authored = bothWaiting.ActiveWaits
            .OrderBy(wait => wait.AuthoredLocation.Value, StringComparer.Ordinal)
            .ToArray();

        var first = await DeliverAsync(
            provider,
            instance,
            Ready,
            WaitCorrelation,
            "ceiling-one-first");
        var oneWaiting = await WaitForStatusAsync(instance, WorkflowInstanceStatus.Waiting);

        bothWaiting.ActiveWaits.Should().HaveCount(2);
        bothWaiting.ActiveWaits.Select(wait => wait.WaitId).Should().OnlyHaveUniqueItems();
        first.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        oneWaiting.ActiveWaits.Should().ContainSingle()
            .Which.WaitId.Should().Be(authored[1].WaitId);

        var second = await DeliverAsync(
            provider,
            instance,
            Ready,
            WaitCorrelation,
            "ceiling-one-second");
        second.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await WaitForStatusAsync(instance, WorkflowInstanceStatus.Completed))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    [Fact]
    public async Task SelectedStraightLineStep_ExecutesThroughCompiledPlan()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Then(context =>
            {
                context.State.Results.Add("step");
                return ValueTask.CompletedTask;
            })
            .End(WorkflowOutcomeName.Create("stepped"))
            .Build();

        var instance = await StartAsync(provider, definition, "straight-line");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Outcome.Should().Be(WorkflowOutcomeName.Create("stepped"));
        state.Results.Should().Equal("step");
    }

    [Fact]
    public async Task SelectedWait_ResumesOwningFiberAndProvidesEventToNextStep()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddTransient<PortableWaitStep>();
            services.AddTransient<CapturePayloadStep>();
        });
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Then<PortableWaitStep>()
            .Then<CapturePayloadStep>()
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "portable-wait");
        var waiting = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var delivery = await DeliverAsync(
            provider,
            instance,
            Continue,
            WaitCorrelation,
            "portable-wait-event",
            "portable-payload");
        var completed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        waiting.ActiveWaits.Should().ContainSingle();
        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        state.Results.Should().Equal("portable-payload");
    }

    [Fact]
    public async Task SelectedStructuralWait_ResumesOwningFiberAndProvidesEventToNextStep()
    {
        using var provider = CreateProvider(
            services => services.AddTransient<CapturePayloadStep>());
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait(WorkflowEventContract.Create(Continue, EventContractVersion.Initial), _ => WaitCorrelation)
            .Then<CapturePayloadStep>()
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "structural-wait");

        var delivery = await DeliverAsync(
            provider,
            instance,
            Continue,
            WaitCorrelation,
            "structural-wait-event",
            "structural-payload");
        var completed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal("structural-payload");
    }

    [Fact]
    public async Task SelectedRootScopeWithNestedIf_UsesCompiledControlFlowContinuations()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .If(
                state => state.Value.Input == "run",
                then => then.Then(context =>
                {
                    context.State.Results.Add("conditional");
                    return ValueTask.CompletedTask;
                }))
            .Parallel<string>(branches => branches.Branch<BranchState>(
                AuthoredBranchId.Create("nested"),
                _ => new BranchState("nested"),
                branch => branch.Return(state => state.Value.Name)))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = [.. parent.Value.Results, .. results.Select(result => result.Result)]
            })
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "run");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal("conditional", "nested");
    }

    [Fact]
    public async Task SelectedForEach_MaterializesIsolatedItemsAndMergesOutcomesByIndex()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ItemState, string>(
                parent => parent.Value.Input.Split(','),
                ForEachOptions.Create(3),
                item => new ItemState(item.Index, item.Item),
                body => body
                    .Then(context =>
                    {
                        context.ReplaceState(context.State with
                        {
                            Value = context.State.Value + "-item"
                        });
                        return ValueTask.CompletedTask;
                    })
                    .Return(state => state.Value.Value))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = results.Select(result =>
                    $"{result.Index}:{result.Result}").ToList()
            })
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "alpha,beta,gamma");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal(
            "0:alpha-item",
            "1:beta-item",
            "2:gamma-item");
        state.Input.Should().Be("alpha,beta,gamma");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedForEach_EmptySnapshotMergesExactlyOnceUnderBothJoins(
        bool collectOutcomes)
    {
        var mergeCalls = 0;
        using var provider = CreateProvider();
        var root = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []));
        var join = root.ForEach<string, ItemState, string>(
            _ => [],
            ForEachOptions.Create(1),
            item => new ItemState(item.Index, item.Item),
            body => body.Return(state => state.Value.Value));
        var successor = collectOutcomes
            ? join.WhenAllOutcomes((parent, outcomes) =>
            {
                mergeCalls++;
                outcomes.Should().BeEmpty();
                return parent.Value with { Results = ["empty"] };
            })
            : join.WhenAll((parent, results) =>
            {
                mergeCalls++;
                results.Should().BeEmpty();
                return parent.Value with { Results = ["empty"] };
            });
        var definition = successor.End().Build();

        var instance = await StartAsync(
            provider,
            definition,
            collectOutcomes ? "empty-outcomes" : "empty-results");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        mergeCalls.Should().Be(1);
        state.Results.Should().Equal("empty");
    }

    [Fact]
    public async Task PublicForEach_MaxItemsRejectsBeforeAnyItemStateIsMaterialized()
    {
        var itemStateCalls = 0;
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ItemState, string>(
                _ => ["zero", "one"],
                ForEachOptions.Create(1),
                item =>
                {
                    Interlocked.Increment(ref itemStateCalls);
                    return new ItemState(item.Index, item.Item);
                },
                body => body.Return(state => state.Value.Value))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "max-items");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure!.Code.Should().Be("SFE-LIMIT-001");
        snapshot.Failure.Message.Should().Contain("exceeding MaxItems 1");
        itemStateCalls.Should().Be(0);
    }

    [Fact]
    public async Task PublicForEach_EncodedValueLimitRejectsBeforeAnyItemStateIsMaterialized()
    {
        var itemStateCalls = 0;
        var oversized = new string('x', 512 * 1024);
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ItemState, string>(
                _ => [oversized],
                ForEachOptions.Create(1),
                item =>
                {
                    Interlocked.Increment(ref itemStateCalls);
                    return new ItemState(item.Index, item.Item);
                },
                body => body.Return(state => state.Value.Value))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "encoded-limit");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure!.Code.Should().Be("SFE-LIMIT-011");
        snapshot.Failure.Message.Should().Contain("exceeding");
        itemStateCalls.Should().Be(0);
    }

    [Fact]
    public async Task PublicForEach_TaggedFlatteningPreservesGroupIdentityAndFlatAggregationOrder()
    {
        TaggedWorkItem[] items =
        [
            new("group-a", "deploy", "unit-1"),
            new("group-a", "verify", "unit-1"),
            new("group-b", "deploy", "unit-2"),
            new("group-b", "verify", "unit-2")
        ];
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<TaggedWorkItem, TaggedItemState, TaggedItemResult>(
                _ => items,
                ForEachOptions.Create(16, maxConcurrency: 2),
                item => new TaggedItemState(item.Index, item.Item, string.Empty),
                body => body
                    .If(
                        state => state.Value.Item.UnitKind == "deploy",
                        then => then.Then(context =>
                        {
                            context.State.Observation = "deployed";
                            return ValueTask.CompletedTask;
                        }),
                        otherwise => otherwise.Then(context =>
                        {
                            context.State.Observation = "verified";
                            return ValueTask.CompletedTask;
                        }))
                    .Return(state => new TaggedItemResult(
                        state.Value.Item.GroupId,
                        state.Value.Item.UnitKind,
                        state.Value.Item.UnitId,
                        state.Value.Observation)))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = results.Select(result =>
                    $"{result.Index}:{result.Result.GroupId}:{result.Result.UnitKind}:" +
                    $"{result.Result.UnitId}:{result.Result.Observation}").ToList()
            })
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "tagged-items");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal(
            "0:group-a:deploy:unit-1:deployed",
            "1:group-a:verify:unit-1:verified",
            "2:group-b:deploy:unit-2:deployed",
            "3:group-b:verify:unit-2:verified");
    }

    [Fact]
    public async Task PublicParallel_WhenAllOutcomesCancellationSuppressesMergeAndClearsChildWaits()
    {
        var mergeCalls = 0;
        using var provider = CreateProvider(pathCeiling: 2);
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(Continue, EventContractVersion.Initial), _ => CorrelationId.Create("first"))
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(Continue, EventContractVersion.Initial), _ => CorrelationId.Create("second"))
                        .Return(state => state.Value.Name)))
            .WhenAllOutcomes((parent, _) =>
            {
                Interlocked.Increment(ref mergeCalls);
                return parent.Value with { Results = ["merged"] };
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "cancel-fanout");
        var waiting = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var cancellation = await instance.RequestCancellationAsync(
            TestContext.Current.CancellationToken);
        var cancelled = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        waiting.ActiveWaits.Should().HaveCount(2);
        cancellation.Should().Be(WorkflowCancellationRequestStatus.Requested);
        cancelled.Status.Should().Be(WorkflowInstanceStatus.Cancelled);
        cancelled.ActiveWaits.Should().BeEmpty();
        mergeCalls.Should().Be(0);
        state.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicForEach_WhenAllTerminationSuppressesMergeAndClearsItemWaits()
    {
        var mergeCalls = 0;
        using var provider = CreateProvider(pathCeiling: 2);
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ItemState, string>(
                _ => ["zero", "one"],
                ForEachOptions.Create(2, 2),
                item => new ItemState(item.Index, item.Item),
                body => body
                    .Wait(WorkflowEventContract.Create(Continue, EventContractVersion.Initial), state => CorrelationId.Create(state.Value.Value))
                    .Return(state => state.Value.Value))
            .WhenAll((parent, _) =>
            {
                Interlocked.Increment(ref mergeCalls);
                return parent.Value with { Results = ["merged"] };
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "terminate-fanout");
        var waiting = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var termination = await instance.TerminateAsync(
            TestContext.Current.CancellationToken);
        var terminated = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        waiting.ActiveWaits.Should().HaveCount(2);
        termination.Should().Be(WorkflowTerminationStatus.Terminated);
        terminated.Status.Should().Be(WorkflowInstanceStatus.Terminated);
        terminated.ActiveWaits.Should().BeEmpty();
        mergeCalls.Should().Be(0);
        state.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicParallel_ReferenceModelCompletionPermutationsProduceOneOrderedMerge()
    {
        foreach (var seed in new[] { 307, 311, 313 })
        {
            var mergeCalls = 0;
            using var provider = CreateProvider(pathCeiling: 4);
            var definition = Workflow.Ephemeral<ParentState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(value => new ParentState(value, []))
                .Parallel<string>(branches =>
                {
                    foreach (var index in Enumerable.Range(0, 4))
                    {
                        var captured = index;
                        branches.Branch<BranchState>(
                            AuthoredBranchId.Create($"branch-{captured}"),
                            _ => new BranchState(captured.ToString()),
                            branch => branch
                                .Wait(
                                    WorkflowEventContract.Create(Ready, EventContractVersion.Initial),
                                    _ => CorrelationId.Create($"branch-{captured}"))
                                .Return(state => state.Value.Name));
                    }
                })
                .WhenAll((parent, results) =>
                {
                    Interlocked.Increment(ref mergeCalls);
                    return parent.Value with
                    {
                        Results = results.Select(result => result.Result).ToList()
                    };
                })
                .End()
                .Build();
            var instance = await StartAsync(provider, definition, $"parallel-order-{seed}");

            foreach (var index in CompletionOrder(4, seed))
            {
                var result = await DeliverAsync(
                    provider,
                    instance,
                    Ready,
                    CorrelationId.Create($"branch-{index}"),
                    $"parallel-order-{seed}-{index}");
                result.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
            }

            var completed = await instance.GetSnapshotAsync(
                TestContext.Current.CancellationToken);
            var state = await instance.GetStateAsync<ParentState>(
                TestContext.Current.CancellationToken);
            completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
            completed.ActiveWaits.Should().BeEmpty();
            mergeCalls.Should().Be(1);
            state.Results.Should().Equal("0", "1", "2", "3");
        }
    }

    [Fact]
    public async Task PublicForEach_ReferenceModelCompletionPermutationsProduceOneIndexedMerge()
    {
        foreach (var seed in new[] { 401, 409, 419 })
        {
            var mergeCalls = 0;
            using var provider = CreateProvider(pathCeiling: 6);
            var definition = Workflow.Ephemeral<ParentState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(value => new ParentState(value, []))
                .ForEach<int, ItemState, string>(
                    _ => Enumerable.Range(0, 6).ToArray(),
                    ForEachOptions.Create(6, 6),
                    item => new ItemState(item.Index, item.Item.ToString()),
                    body => body
                        .Wait(
                            WorkflowEventContract.Create(Ready, EventContractVersion.Initial),
                            state => CorrelationId.Create($"item-{state.Value.Index}"))
                        .Return(state => state.Value.Value))
                .WhenAll((parent, results) =>
                {
                    Interlocked.Increment(ref mergeCalls);
                    return parent.Value with
                    {
                        Results = results.Select(result =>
                            $"{result.Index}:{result.Result}").ToList()
                    };
                })
                .End()
                .Build();
            var instance = await StartAsync(provider, definition, $"foreach-order-{seed}");

            foreach (var index in CompletionOrder(6, seed))
            {
                var result = await DeliverAsync(
                    provider,
                    instance,
                    Ready,
                    CorrelationId.Create($"item-{index}"),
                    $"foreach-order-{seed}-{index}");
                result.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
            }

            var completed = await instance.GetSnapshotAsync(
                TestContext.Current.CancellationToken);
            var state = await instance.GetStateAsync<ParentState>(
                TestContext.Current.CancellationToken);
            completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
            completed.ActiveWaits.Should().BeEmpty();
            mergeCalls.Should().Be(1);
            state.Results.Should().Equal(
                "0:0",
                "1:1",
                "2:2",
                "3:3",
                "4:4",
                "5:5");
        }
    }

    [Fact]
    public async Task SelectedForEach_MaxConcurrencyLimitsAdmittedNonterminalFibers()
    {
        using var provider = CreateProvider(pathCeiling: 4);
        var definition = WaitingForEachDefinition(
            ForEachOptions.Create(3, 2),
            "alpha,beta,gamma");
        var instance = await StartAsync(provider, definition, "max-concurrency");
        var initial = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        initial.ActiveWaits.Select(wait => wait.CorrelationId.Value)
            .Should().BeEquivalentTo("item-0", "item-1");
        _ = await DeliverAsync(
            provider,
            instance,
            Continue,
            CorrelationId.Create("item-0"),
            "max-concurrency-0");
        var admittedThird = await instance.GetSnapshotAsync(
            TestContext.Current.CancellationToken);
        admittedThird.ActiveWaits.Select(wait => wait.CorrelationId.Value)
            .Should().BeEquivalentTo("item-1", "item-2");

        _ = await DeliverAsync(
            provider,
            instance,
            Continue,
            CorrelationId.Create("item-2"),
            "max-concurrency-2");
        _ = await DeliverAsync(
            provider,
            instance,
            Continue,
            CorrelationId.Create("item-1"),
            "max-concurrency-1");
        var completed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public async Task SelectedForEach_HostPathCeilingTightensNodeAdmissionAndParkedItemKeepsSlot()
    {
        using var provider = CreateProvider(pathCeiling: 1);
        var definition = WaitingForEachDefinition(
            ForEachOptions.Create(3, 3),
            "alpha,beta,gamma");
        var instance = await StartAsync(provider, definition, "host-ceiling");

        for (var index = 0; index < 3; index++)
        {
            var waiting = await instance.GetSnapshotAsync(
                TestContext.Current.CancellationToken);
            waiting.ActiveWaits.Should().ContainSingle()
                .Which.CorrelationId.Should().Be(
                    CorrelationId.Create($"item-{index}"));
            _ = await DeliverAsync(
                provider,
                instance,
                Continue,
                CorrelationId.Create($"item-{index}"),
                $"host-ceiling-{index}");
        }

        var completed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);
        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public async Task SelectedForEach_ContinueWithPartialFailuresMergesEveryOutcome()
    {
        using var provider = CreateProvider(pathCeiling: 3);
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ItemState, string>(
                parent => parent.Value.Input.Split(','),
                ForEachOptions.Create(3, 3),
                item => new ItemState(item.Index, item.Item),
                body => body
                    .Then(context => context.State.Index == 1
                        ? ValueTask.FromException(
                            new WorkflowLifecycleException("item failed"))
                        : ValueTask.CompletedTask)
                    .Return(state => state.Value.Value))
            .WhenAllOutcomes((parent, outcomes) => parent.Value with
            {
                Results = outcomes.Select(outcome => outcome switch
                {
                    ForEachItemOutcome<string>.Succeeded success =>
                        $"{success.Index}:Succeeded:{success.Result}:",
                    ForEachItemOutcome<string>.Failed failure =>
                        $"{failure.Index}:Failed::{failure.Failure.Code}",
                    _ => throw new InvalidOperationException("Unknown item outcome.")
                }).ToList()
            })
            .End()
            .Build();

        var instance = await StartAsync(provider, definition, "alpha,beta,gamma");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal(
            "0:Succeeded:alpha:",
            "1:Failed::WF-LEGACY-LIFECYCLE",
            "2:Succeeded:gamma:");
    }

    [Fact]
    public async Task SelectedForEach_WaitAllThenFailWaitsForEveryAdmittedItem()
    {
        using var provider = CreateProvider(pathCeiling: 3);
        var definition = Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .ForEach<string, ItemState, string>(
                parent => parent.Value.Input.Split(','),
                ForEachOptions.Create(3, 3),
                item => new ItemState(item.Index, item.Item),
                body => body
                    .If(
                        state => state.Value.Index == 1,
                        then => then.Then(_ => ValueTask.FromException(
                            new WorkflowLifecycleException("item failed"))),
                        otherwise => otherwise.Wait(
                            WorkflowEventContract.Create(Continue, EventContractVersion.Initial),
                            state => CorrelationId.Create(
                                $"item-{state.Value.Index}")))
                    .Return(state => state.Value.Value))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "alpha,beta,gamma");
        var waiting = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        waiting.ActiveWaits.Select(wait => wait.CorrelationId.Value)
            .Should().BeEquivalentTo("item-0", "item-2");
        _ = await DeliverAsync(
            provider,
            instance,
            Continue,
            CorrelationId.Create("item-2"),
            "wait-all-2");
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Waiting);
        _ = await DeliverAsync(
            provider,
            instance,
            Continue,
            CorrelationId.Create("item-0"),
            "wait-all-0");
        var failed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        failed.Status.Should().Be(WorkflowInstanceStatus.Failed);
        failed.ActiveWaits.Should().BeEmpty();
        failed.Failure!.Message.Should().Contain("item failed");
    }

    [Fact]
    public async Task SelectedForEach_ItemStateMaterializationBreaksParentAliases()
    {
        var input = new MutableParentState(
            [new MutableItem("a"), new MutableItem("b")],
            []);
        using var provider = CreateProvider(pathCeiling: 2);
        var definition = Workflow.Ephemeral<MutableParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<MutableParentState>(state => state)
            .ForEach<MutableItem, MutableItemState, string>(
                parent => parent.Value.Items,
                ForEachOptions.Create(2, 2),
                item => new MutableItemState(item.Index, item.Item),
                body => body
                    .Then(context =>
                    {
                        context.State.Item.Value += "-branch";
                        return ValueTask.CompletedTask;
                    })
                    .Return(state => state.Value.Item.Value))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = results.Select(result => result.Result).ToList()
            })
            .End()
            .Build();
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            input,
            StartIdempotencyKey.Create("structured-alias-isolation"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<MutableParentState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Items.Select(item => item.Value).Should().Equal("a", "b");
        state.Results.Should().Equal("a-branch", "b-branch");
        input.Items.Select(item => item.Value).Should().Equal("a", "b");
    }

    private static ServiceProvider CreateProvider(
        Action<ServiceCollection>? configure = null,
        int pathCeiling = 4,
        EphemeralEngineHostOptions? options = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.AddOrcaCoreEphemeralEngine(options ?? new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = pathCeiling,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services.BuildServiceProvider();
    }

    private static EphemeralDefinitionHandle<string> Register(
        ServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition) =>
        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

    private static async Task<WorkflowInstanceHandle> StartAsync(
        ServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition,
        string input) =>
        (await Register(provider, definition).StartOrGetAsync(
            input,
            StartIdempotencyKey.Create($"structured-{input}"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

    private static ValueTask<EphemeralEventRouteResult> DeliverAsync(
        ServiceProvider provider,
        WorkflowInstanceHandle instance,
        EventName eventName,
        CorrelationId correlation,
        string eventId) =>
        provider.GetRequiredService<EphemeralWorkflowEventRouter>()
            .RouteToInstanceAsync(
                instance.InstanceId,
                EphemeralTestEvent.Create(
                    EventId.Create(eventId),
                    eventName,
                    correlation,
                    DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);

    private static ValueTask<EphemeralEventRouteResult> DeliverAsync<TPayload>(
        ServiceProvider provider,
        WorkflowInstanceHandle instance,
        EventName eventName,
        CorrelationId correlation,
        string eventId,
        TPayload payload) =>
        provider.GetRequiredService<EphemeralWorkflowEventRouter>()
            .RouteToInstanceAsync(
                instance.InstanceId,
                EphemeralTestEvent<TPayload>.Create(
                    EventId.Create(eventId),
                    eventName,
                    correlation,
                    payload,
                    DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);

    private static async Task<WorkflowInstanceSnapshot> WaitForStatusAsync(
        WorkflowInstanceHandle instance,
        WorkflowInstanceStatus status)
    {
        while (true)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var snapshot = await instance.GetSnapshotAsync(
                TestContext.Current.CancellationToken);
            if (snapshot.Status == status)
            {
                return snapshot;
            }

            await Task.Yield();
        }
    }

    private static async Task WaitForTraceCountAsync(
        ConcurrentQueue<string> trace,
        int count)
    {
        while (trace.Count < count)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private static EphemeralWorkflowDefinition<string> WaitingParallelDefinition(
        string name,
        AuthoredBranchId branchId,
        Action<EphemeralBranchBuilder<BranchState, string>> body) =>
        Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value, []))
            .Wait(WorkflowEventContract.Create(StartWork, EventContractVersion.Initial), _ => WaitCorrelation)
            .Parallel<string>(branches => branches.Branch(
                branchId,
                _ => new BranchState(name),
                body))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<string> WaitingForEachDefinition(
        ForEachOptions options,
        string items) =>
        Workflow.Ephemeral<ParentState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new ParentState(items, []))
            .ForEach<string, ItemState, string>(
                parent => parent.Value.Input.Split(','),
                options,
                item => new ItemState(item.Index, item.Item),
                body => body
                    .Wait(
                        WorkflowEventContract.Create(Continue, EventContractVersion.Initial),
                        state => CorrelationId.Create(
                            $"item-{state.Value.Index}"))
                    .Return(state => state.Value.Value))
            .WhenAll((parent, results) => parent.Value with
            {
                Results = results.Select(result => result.Result).ToList()
            })
            .End()
            .Build();

    private static int[] CompletionOrder(int count, int seed)
    {
        var result = Enumerable.Range(0, count).ToArray();
        var random = new Random(seed);
        for (var index = result.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (result[index], result[swap]) = (result[swap], result[index]);
        }

        return result;
    }

    private sealed record ParentState(string Input, List<string> Results);

    private sealed record BranchState(string Name);

    private sealed record ItemState(int Index, string Value);

    private sealed record TaggedWorkItem(
        string GroupId,
        string UnitKind,
        string UnitId);

    private sealed class TaggedItemState(
        int index,
        TaggedWorkItem item,
        string observation)
    {
        public int Index { get; } = index;

        public TaggedWorkItem Item { get; } = item;

        public string Observation { get; set; } = observation;
    }

    private sealed record TaggedItemResult(
        string GroupId,
        string UnitKind,
        string UnitId,
        string Observation);

    private sealed record MutableParentState(
        List<MutableItem> Items,
        List<string> Results);

    private sealed class MutableItem(string value)
    {
        public string Value { get; set; } = value;
    }

    private sealed record MutableItemState(int Index, MutableItem Item);

    private sealed class RetryOnceStep(ConcurrentQueue<string> trace) : IStep<BranchState>, IStep<ParentState>
    {
        private int attempts;

        internal TaskCompletionSource FirstAttempt { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken) =>
            ExecuteCore();

        ValueTask<StepResult> IStep<ParentState>.ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken) =>
            ExecuteCore();

        private ValueTask<StepResult> ExecuteCore()
        {
            var attempt = Interlocked.Increment(ref attempts);
            trace.Enqueue($"retrying:{attempt}");
            if (attempt == 1)
            {
                FirstAttempt.TrySetResult();
                return ValueTask.FromResult<StepResult>(
                    new StepResult.Failed(
                        new WorkflowLifecycleException("retry once")));
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingBranchStep(ConcurrentQueue<string> trace) : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            trace.Enqueue(context.State.Name);
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(
                    new WorkflowLifecycleException("branch failed")));
        }
    }

    private sealed class ThrowNotSupportedStep : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("user not supported");
    }

    private sealed class PortableWaitStep : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent(
                    WorkflowEventContract.Create(Continue, EventContractVersion.Initial),
                    WaitCorrelation));
    }

    private sealed class CapturePayloadStep : IStep<ParentState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ParentState> context,
            CancellationToken cancellationToken)
        {
            context.State.Results.Add(
                context.ResumedEvent is null
                    ? "unexpected:null"
                    : context.ResumedEvent.GetPayload(WorkflowEventContract<string>.Create(
                        context.ResumedEvent.EventContract.EventName,
                        context.ResumedEvent.EventContract.Version)));
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class AsyncGate
    {
        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource Released { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Enter() => Entered.TrySetResult();

        internal Task WaitForReleaseAsync(CancellationToken cancellationToken) =>
            Released.Task.WaitAsync(cancellationToken);

        internal void Release() => Released.TrySetResult();
    }
}
