using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Hosting;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class StructuredFanoutScenarioHost
{
    private static readonly CorrelationId FanoutCorrelation = CorrelationId.Create("phase0-fanout");

    [Phase0Scenario("empty-parallel-diagnostic-parity", "3.6")]
    public static void EmptyParallelDiagnosticParity(Phase0ScenarioContext context)
    {
        var completion = Workflow.Ephemeral<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []))
            .Parallel<string>(_ => { })
            .WhenAll((parent, _) => parent.Value)
            .End();
        string[]? tryBuildCodes = null;

        var validation = context.Observe(_ => completion.TryBuild());
        Phase0Assert.Satisfies(
            validation,
            result =>
            {
                tryBuildCodes = result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();
                return !result.IsValid &&
                       tryBuildCodes.SequenceEqual(["SFE-AUTH-BRANCH-004"]) &&
                       result.Diagnostics[0].Location.Value.StartsWith("workflow:$/n:", StringComparison.Ordinal);
            },
            "TryBuild did not report the one catalogued empty-Parallel diagnostic.");

        var exception = context.ObserveThrows<WorkflowDefinitionException, EphemeralWorkflowDefinition<string>>(
            _ => completion.Build());
        Phase0Assert.Satisfies(
            exception,
            error => error.Diagnostics.Select(diagnostic => diagnostic.Code).SequenceEqual(tryBuildCodes!),
            "Build and TryBuild did not preserve identical ordered empty-Parallel diagnostics.");
    }

    [Phase0Scenario("success-failure-only-outcomes", "3.6")]
    public static async Task SuccessFailureOnlyOutcomes(Phase0ScenarioContext context)
    {
        var root = Workflow.Ephemeral<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []));
        var join = root.Parallel<string>(branches => branches
            .Branch<BranchState>(
                AuthoredBranchId.Create("succeeded"),
                _ => new BranchState("succeeded", "unused"),
                branch => branch.Return(state => state.Value.Name))
            .Branch<BranchState>(
                AuthoredBranchId.Create("failed"),
                _ => new BranchState("failed", "unused"),
                branch => branch
                    .Then(_ => ValueTask.FromException(new ScenarioFailureException("SCENARIO-FAILED")))
                    .Return(state => state.Value.Name)));
        var mergeCalls = 0;
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<BranchOutcome<string>>, FanoutState> merge =
            (parent, outcomes) =>
        {
            mergeCalls++;
            return parent.Value with
            {
                Results = outcomes.Select(outcome => outcome switch
                {
                    BranchOutcome<string>.Succeeded success =>
                        $"{success.BranchId.Value}:success:{success.Result}",
                    BranchOutcome<string>.Failed failure =>
                        $"{failure.BranchId.Value}:failure:{failure.Failure.Code}",
                    _ => throw new InvalidOperationException("Unexpected outcome variant.")
                }).ToList()
            };
        };
        EphemeralWorkflowBuilder<string, FanoutState>? successor = null;
        var observed = context.Observe(_ => join.WhenAllOutcomes(merge));
        Phase0Assert.Satisfies(
            observed,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "WhenAllOutcomes did not return a new successor-epoch facade of the exact root-builder type.");

        var definition = successor!.End().Build();
        using var provider = CreateEphemeralHost();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create("success-failure-only-outcomes"),
            CancellationToken.None)).GetHandleOrThrow();
        var completed = await instance.GetSnapshotAsync(CancellationToken.None);
        var state = await instance.GetStateAsync<FanoutState>(CancellationToken.None);
        var variants = typeof(BranchOutcome<string>).GetNestedTypes();

        if (completed.Status != WorkflowInstanceStatus.Completed ||
            mergeCalls != 1 ||
            !state.Results.SequenceEqual([
                "succeeded:success:succeeded",
                "failed:failure:SCENARIO-FAILED"
            ]) ||
            !variants.Select(type => type.Name).Order().SequenceEqual(["Failed", "Succeeded"]) ||
            variants.Any(type => !type.IsSealed || type.GetConstructors().Length != 0))
        {
            throw new InvalidOperationException(
                "The runtime did not expose one ordered, closed success/failure-only outcome family. " +
                $"status={completed.Status}; error={completed.Failure}; mergeCalls={mergeCalls}; " +
                $"results=[{string.Join(",", state.Results)}]; " +
                $"variants=[{string.Join(",", variants.Select(type => $"{type.Name}:{type.IsSealed}:{type.GetConstructors().Length}"))}].");
        }
    }

    [Phase0Scenario("ordered-join-failure", "3.6")]
    public static async Task OrderedJoinFailure(Phase0ScenarioContext context)
    {
        context.ReleaseBarrier("ordered-join-authoring");
        var root = Workflow.Durable<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []));
        var join = root.Parallel<string>(branches =>
        {
            ConsumeBarrier(context, "ordered-join-authoring");
            branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first", "Fail-First"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Fail-First"), EventContractVersion.Initial), _ => FanoutCorrelation)
                        .Then<FailByNameStep>()
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second", "Fail-Second"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Fail-Second"), EventContractVersion.Initial), _ => FanoutCorrelation)
                        .Then<FailByNameStep>()
                        .Return(state => state.Value.Name));
        });
        var mergeCalls = 0;
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<BranchResult<string>>, FanoutState> merge =
            (parent, _) =>
        {
            mergeCalls++;
            return parent.Value;
        };
        DurableWorkflowBuilder<string, FanoutState>? successor = null;
        var observed = context.Observe(_ => join.WhenAll(merge));
        Phase0Assert.Satisfies(
            observed,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "WhenAll did not return a new successor-epoch facade of the exact durable root-builder type.");

        var definition = successor!.End().Build();
        using var store = new DurableScenarioProvider();
        using var runtime = CreateDurableRuntime(
            store,
            configureServices: services => services.AddTransient<FailByNameStep>());
        runtime.Register(definition);
        var started = await runtime.StartOrGetAsync<string, FanoutState>(
            "phase0-ordered-join-failure",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        await runtime.RaiseEventAsync(
            started.InstanceId,
            "Fail-Second",
            FanoutCorrelation,
            cancellationToken: CancellationToken.None);
        await runtime.RaiseEventAsync(
            started.InstanceId,
            "Fail-First",
            FanoutCorrelation,
            cancellationToken: CancellationToken.None);

        var snapshot = await SnapshotAsync(store, started.InstanceId);
        var envelope = await EnvelopeAsync(store, started.InstanceId);
        var rootFailure = envelope.Fibers.Single(fiber => fiber.FiberId == envelope.RootFiberId).Failure;
        if (snapshot.Status != WorkflowInstanceStatus.Failed ||
            mergeCalls != 0 ||
            rootFailure?.Code != "SFE-JOIN-FAILED" ||
            !rootFailure.Causes.Select(cause => cause.Code).SequenceEqual([
                "SCENARIO-FIRST",
                "SCENARIO-SECOND"
            ]))
        {
            throw new InvalidOperationException(
                "Reverse completion did not preserve authored-order causes or suppress the success merge. " +
                $"Status={snapshot.Status}; MergeCalls={mergeCalls}; RootCode={rootFailure?.Code}; " +
                $"Causes={string.Join(",", rootFailure?.Causes.Select(cause => $"{cause.Code}:{cause.Message}") ?? [])}.");
        }
    }

    [Phase0Scenario("ancestor-terminal-suppresses-merge", "3.6")]
    public static async Task AncestorTerminalSuppressesMerge(Phase0ScenarioContext context)
    {
        var observedMergeCalls = 0;
        var root = Workflow.Durable<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []))
            .CompleteWithin(TimeSpan.FromMinutes(1));
        var join = root.Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first", "Deadline-Branch-0"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second", "Deadline-Branch-1"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                        .Return(state => state.Value.Name)));
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<BranchOutcome<string>>, FanoutState> observedMerge =
            (parent, _) =>
            {
                observedMergeCalls++;
                return parent.Value with { Results = ["merged"] };
            };
        DurableWorkflowBuilder<string, FanoutState>? successor = null;
        var observed = context.Observe(_ => join.WhenAllOutcomes(observedMerge));
        Phase0Assert.Satisfies(
            observed,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "Durable Parallel.WhenAllOutcomes did not return the exact successor-epoch root facade.");

        await AssertDurableDeadlineSuppressesMergeAsync(
            context,
            successor!.End(_ => "unreachable").Build(),
            "durable-parallel-outcomes",
            () => observedMergeCalls);

        foreach (var shape in new[] { FanoutShape.Parallel, FanoutShape.ForEach })
        {
            foreach (var collectOutcomes in new[] { false, true })
            {
                if (shape == FanoutShape.Parallel && collectOutcomes)
                {
                    continue;
                }

                var durableMergeCalls = 0;
                var durableDefinition = BuildDurableDeadlineDefinition(
                    shape,
                    collectOutcomes,
                    () => durableMergeCalls++);
                await AssertDurableDeadlineSuppressesMergeAsync(
                    context,
                    durableDefinition,
                    $"durable-{shape}-{(collectOutcomes ? "outcomes" : "all")}".ToLowerInvariant(),
                    () => durableMergeCalls);
            }
        }

        foreach (var shape in new[] { FanoutShape.Parallel, FanoutShape.ForEach })
        {
            foreach (var collectOutcomes in new[] { false, true })
            {
                var ephemeralMergeCalls = 0;
                var ephemeralDefinition = BuildEphemeralDeadlineDefinition(
                    shape,
                    collectOutcomes,
                    () => ephemeralMergeCalls++);
                await AssertEphemeralDeadlineSuppressesMergeAsync(
                    context,
                    ephemeralDefinition,
                    () => ephemeralMergeCalls);
            }
        }
    }

    [Phase0Scenario("empty-foreach-valid", "3.6")]
    public static async Task EmptyForEachIsValid(Phase0ScenarioContext context)
    {
        var root = Workflow.Ephemeral<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []));
        var join = root.ForEach<string, ItemState, string>(
            _ => [],
            ForEachOptions.Create(1),
            item => new ItemState(item.Index, item.Item, $"Item-{item.Index}"),
            body => body.Return(state => state.Value.Value));
        var mergeCalls = 0;
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<ForEachItemResult<string>>, FanoutState> merge =
            (parent, results) =>
        {
            mergeCalls++;
            if (results.Count != 0)
            {
                throw new InvalidOperationException("An empty item snapshot produced item results.");
            }

            return parent.Value with { Results = ["empty-merge"] };
        };
        EphemeralWorkflowBuilder<string, FanoutState>? successor = null;
        var observed = context.Observe(_ => join.WhenAll(merge));
        Phase0Assert.Satisfies(
            observed,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "The empty ForEach join did not return a new successor-epoch facade of the exact root-builder type.");

        var definition = successor!.End().Build();
        using var provider = CreateEphemeralHost();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create("empty-foreach-merge"),
            CancellationToken.None)).GetHandleOrThrow();
        var completed = await instance.GetSnapshotAsync(CancellationToken.None);
        var state = await instance.GetStateAsync<FanoutState>(CancellationToken.None);
        if (completed.Status != WorkflowInstanceStatus.Completed ||
            mergeCalls != 1 ||
            !state.Results.SequenceEqual(["empty-merge"]))
        {
            throw new InvalidOperationException("Empty ForEach did not merge one empty ordered list exactly once.");
        }
    }

    [Phase0Scenario("foreach-bound-and-snapshot-replay", "3.6")]
    public static async Task ForEachBoundAndSnapshotReplay(Phase0ScenarioContext context)
    {
        var optionsObservation = context.Observe(_ => ForEachOptions.Create(3, 1));
        Phase0Assert.Satisfies(
            optionsObservation,
            options => options.MaxItems == 3 && options.MaxConcurrency == 1,
            "ForEachOptions did not retain the validated item and concurrency bounds.");

        context.ReleaseBarrier("foreach-snapshot-authoring");
        var selected = new List<string> { "zero", "one", "two" };
        var selectorCalls = 0;
        var itemStateCalls = 0;
        var root = Workflow.Durable<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []));
        var join = root.ForEach<string, ItemState, string>(
            _ =>
            {
                selectorCalls++;
                if (selectorCalls > 1)
                {
                    throw new InvalidOperationException("The durable item selector was replayed.");
                }

                return selected;
            },
            ForEachOptions.Create(3, 1),
            item =>
            {
                itemStateCalls++;
                return new ItemState(item.Index, item.Item, $"Item-{item.Index}");
            },
            body =>
            {
                ConsumeBarrier(context, "foreach-snapshot-authoring");
                body.Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                    .Return(state => state.Value.Value);
            });
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<ForEachItemResult<string>>, FanoutState> merge =
            (parent, results) =>
            parent.Value with
            {
                Results = results.Select(result => $"{result.Index}:{result.Result}").ToList()
            };
        DurableWorkflowBuilder<string, FanoutState>? successor = null;
        var observed = context.Observe(_ => join.WhenAll(merge));
        Phase0Assert.Satisfies(
            observed,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "Durable ForEach.WhenAll did not return a new successor-epoch facade of the exact root-builder type.");

        var definition = successor!.End().Build();
        using var store = new DurableScenarioProvider();
        using var firstHost = CreateDurableRuntime(store);
        firstHost.Register(definition);
        var started = await firstHost.StartOrGetAsync<string, FanoutState>(
            "phase0-foreach-snapshot",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        selected[1] = "mutated";

        using var replacement = CreateDurableRuntime(store);
        replacement.Register(definition);
        for (var index = 0; index < 3; index++)
        {
            await replacement.RaiseEventAsync(
                started.InstanceId,
                "Resume",
                CorrelationId.Create($"Item-{index}"),
                cancellationToken: CancellationToken.None);
        }

        var snapshot = await SnapshotAsync(store, started.InstanceId);
        var state = JsonSerializer.Deserialize<FanoutState>(
            (await EnvelopeAsync(store, started.InstanceId)).StatePayload)!;
        if (snapshot.Status != WorkflowInstanceStatus.Completed ||
            selectorCalls != 1 ||
            itemStateCalls != 3 ||
            !state.Results.SequenceEqual(["0:zero", "1:one", "2:two"]))
        {
            throw new InvalidOperationException(
                "The committed bounded item snapshot was reselected, aliased, misindexed, or partially admitted.");
        }

        await AssertOversizedEphemeralSelectionRejectsBeforeItemStateAsync();
    }

    [Phase0Scenario("parent-release-child-queue-merge-reacquire", "3.6")]
    public static async Task ParentReleaseChildQueueMergeReacquire(Phase0ScenarioContext context)
    {
        context.ReleaseBarrier("parallel-ceiling-authoring");
        var root = Workflow.Durable<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []));
        var join = root.Parallel<string>(branches =>
        {
            ConsumeBarrier(context, "parallel-ceiling-authoring");
            branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first", "Branch-0"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Branch-0"), EventContractVersion.Initial), _ => FanoutCorrelation)
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second", "Branch-1"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Branch-1"), EventContractVersion.Initial), _ => FanoutCorrelation)
                        .Return(state => state.Value.Name));
        });
        var mergeCalls = 0;
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<BranchResult<string>>, FanoutState> merge =
            (parent, results) =>
        {
            mergeCalls++;
            return parent.Value with
            {
                Results = results.Select(result => result.Result).ToList()
            };
        };
        DurableWorkflowBuilder<string, FanoutState>? successor = null;
        var observed = context.Observe(_ => join.WhenAll(merge));
        Phase0Assert.Satisfies(
            observed,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "Parallel.WhenAll did not return a new successor-epoch facade of the exact root-builder type.");

        var definition = successor!.End().Build();
        using var store = new DurableScenarioProvider();
        using var runtime = CreateDurableRuntime(store, maxConcurrentExecutionPaths: 1);
        runtime.Register(definition);
        var started = await runtime.StartOrGetAsync<string, FanoutState>(
            "phase0-parallel-ceiling-one",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var first = await SnapshotAsync(store, started.InstanceId);
        if (!first.ActiveWaits
                .OrderBy(wait => wait.WaitSequence)
                .Select(wait => wait.EventName)
                .SequenceEqual(["Branch-0", "Branch-1"]))
        {
            throw new InvalidOperationException(
                "Ceiling one did not admit parked branches in authored order. " +
                $"Status={first.Status}; Error={first.ErrorSummary}; " +
                $"Waits={string.Join(",", first.ActiveWaits.Select(wait => wait.EventName))}.");
        }

        await runtime.RaiseEventAsync(
            started.InstanceId,
            "Branch-1",
            FanoutCorrelation,
            cancellationToken: CancellationToken.None);
        var second = await SnapshotAsync(store, started.InstanceId);
        if (!second.ActiveWaits.Select(wait => wait.EventName).SequenceEqual(["Branch-0"]))
        {
            throw new InvalidOperationException("Reverse resumption did not preserve the remaining parked branch.");
        }

        await runtime.RaiseEventAsync(
            started.InstanceId,
            "Branch-0",
            FanoutCorrelation,
            cancellationToken: CancellationToken.None);
        var terminal = await SnapshotAsync(store, started.InstanceId);
        var state = JsonSerializer.Deserialize<FanoutState>(
            (await EnvelopeAsync(store, started.InstanceId)).StatePayload)!;
        if (terminal.Status != WorkflowInstanceStatus.Completed ||
            mergeCalls != 1 ||
            !state.Results.SequenceEqual(["first", "second"]))
        {
            throw new InvalidOperationException(
                "The parent did not reacquire one path token for one authored-order merge and continuation.");
        }
    }

    [Phase0Scenario("foreach-lower-limit-and-admitted-slots", "3.6")]
    public static async Task ForEachLowerLimitAndAdmittedSlots(Phase0ScenarioContext context)
    {
        var optionsObservation = context.Observe(_ => ForEachOptions.Create(3, 2));
        Phase0Assert.Satisfies(
            optionsObservation,
            options => options.MaxItems == 3 && options.MaxConcurrency == 2,
            "ForEachOptions did not retain its node concurrency limit.");

        context.ReleaseBarrier("foreach-lower-authoring");
        var mergeCalls = 0;
        var root = Workflow.Durable<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []));
        var join = root.ForEach<string, ItemState, string>(
            _ => ["zero", "one", "two"],
            ForEachOptions.Create(3, 2),
            item => new ItemState(item.Index, item.Item, $"Item-{item.Index}"),
            body =>
            {
                ConsumeBarrier(context, "foreach-lower-authoring");
                body.Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                    .Return(state => state.Value.Value);
            });
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<ForEachItemOutcome<string>>, FanoutState> merge =
            (parent, outcomes) =>
        {
            mergeCalls++;
            return parent.Value with
            {
                Results = outcomes.Select(outcome => outcome switch
                {
                    ForEachItemOutcome<string>.Succeeded success =>
                        $"{success.Index}:{success.Result}",
                    ForEachItemOutcome<string>.Failed failure =>
                        $"{failure.Index}:{failure.Failure.Code}",
                    _ => throw new InvalidOperationException("Unexpected item outcome.")
                }).ToList()
            };
        };
        DurableWorkflowBuilder<string, FanoutState>? successor = null;
        var observed = context.Observe(_ => join.WhenAllOutcomes(merge));
        Phase0Assert.Satisfies(
            observed,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "Durable ForEach.WhenAllOutcomes did not return a new successor-epoch facade of the exact root-builder type.");

        var definition = successor!.End().Build();
        await AssertHostCeilingIsLowerLimitAsync(definition);
        await AssertParkedItemsRetainNodeSlotsAsync(definition);
        if (mergeCalls != 2)
        {
            throw new InvalidOperationException("Each completed occurrence must merge outcomes exactly once.");
        }
    }

    [Phase0Scenario("restart-readmits-unfinished-items", "3.6")]
    public static async Task RestartReadmitsUnfinishedItems(Phase0ScenarioContext context)
    {
        context.ReleaseBarrier("foreach-restart-snapshot");
        var selectorCalls = 0;
        var selectedItems = new List<string> { "zero", "one", "two" };
        var mergeCalls = 0;
        var root = Workflow.Durable<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []));
        var join = root.ForEach<string, ItemState, string>(
            _ =>
            {
                ConsumeBarrier(context, "foreach-restart-snapshot");
                selectorCalls++;
                if (selectorCalls > 1)
                {
                    throw new InvalidOperationException("A replacement host reselected committed ForEach items.");
                }

                return selectedItems;
            },
            ForEachOptions.Create(3, 3),
            item => new ItemState(item.Index, item.Item, $"Item-{item.Index}"),
            body =>
            {
                ConsumeBarrier(context, "foreach-restart-snapshot");
                body.Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                    .Return(state => state.Value.Value);
            });
        Func<ReadOnlyStateSnapshot<FanoutState>, IReadOnlyList<ForEachItemOutcome<string>>, FanoutState> merge =
            (parent, outcomes) =>
            {
                mergeCalls++;
                return parent.Value with
                {
                    Results = outcomes.Select(outcome => outcome switch
                    {
                        ForEachItemOutcome<string>.Succeeded success =>
                            $"{success.Index}:{success.Result}",
                        ForEachItemOutcome<string>.Failed failure =>
                            $"{failure.Index}:{failure.Failure.Code}",
                        _ => throw new InvalidOperationException("Unexpected item outcome.")
                    }).ToList()
                };
            };
        DurableWorkflowBuilder<string, FanoutState>? successor = null;
        var joinObservation = context.Observe(_ => join.WhenAllOutcomes(merge));
        Phase0Assert.Satisfies(
            joinObservation,
            builder =>
            {
                successor = builder;
                return !ReferenceEquals(builder, root) && builder.GetType() == root.GetType();
            },
            "Durable ForEach.WhenAllOutcomes did not return the successor-epoch root façade.");

        var definition = successor!.End().Build();
        using var store = new DurableScenarioProvider();
        using var firstHost = CreateDurableRuntime(store, maxConcurrentExecutionPaths: 1);
        firstHost.Register(definition);
        var started = await firstHost.StartOrGetAsync<string, FanoutState>(
            "phase0-foreach-restart-readmission",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var initial = await SnapshotAsync(store, started.InstanceId);
        var initialEnvelope = await EnvelopeAsync(store, started.InstanceId);
        if (selectorCalls != 1 ||
            initialEnvelope.Scopes.Single().ForEach!.Descriptors.Count != 3 ||
            initialEnvelope.Scopes.Single().ForEach!.NextAdmissionOffset != 1 ||
            !initial.ActiveWaits.Select(wait => wait.CorrelationId.Value).SequenceEqual(["Item-0"]))
        {
            throw new InvalidOperationException(
                "The first host did not commit one detached snapshot and admit only item zero.");
        }

        selectedItems[1] = "mutated-after-commit";
        using var replacementHost = CreateDurableRuntime(store, maxConcurrentExecutionPaths: 2);
        replacementHost.Register(definition);
        await replacementHost.RaiseEventAsync(
            started.InstanceId,
            "Resume",
            CorrelationId.Create("Item-0"),
            cancellationToken: CancellationToken.None);
        var readmitted = await SnapshotAsync(store, started.InstanceId);
        var readmittedEnvelope = await EnvelopeAsync(store, started.InstanceId);
        var readmission = readmittedEnvelope.Scopes.Single().ForEach!;
        if (selectorCalls != 1 ||
            readmission.NextAdmissionOffset != 3 ||
            !readmission.ItemFibers.Select(binding => binding.ItemIndex).Order().SequenceEqual([0, 1, 2]) ||
            !readmission.Outcomes.Select(outcome => outcome.Index).SequenceEqual([0]) ||
            !readmitted.ActiveWaits.OrderBy(wait => wait.WaitSequence)
                .Select(wait => wait.CorrelationId.Value)
                .SequenceEqual(["Item-1", "Item-2"]))
        {
            throw new InvalidOperationException(
                "The replacement host did not preserve terminal item zero and re-admit unfinished items in index order.");
        }

        using var finalHost = CreateDurableRuntime(store, maxConcurrentExecutionPaths: 1);
        finalHost.Register(definition);
        var finalDeliveries = new List<ProcessLocalEventRouteStatus>();
        foreach (var index in new[] { 1, 2 })
        {
            var delivered = await finalHost.RaiseEventAsync(
                started.InstanceId,
                "Resume",
                CorrelationId.Create($"Item-{index}"),
                cancellationToken: CancellationToken.None);
            finalDeliveries.Add(delivered.Status);
        }

        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            finalHost,
            definition);
        var instanceHandle = await definitionHandle.GetInstanceAsync(
            started.InstanceId,
            CancellationToken.None);
        var projectedTerminal = await SnapshotAsync(store, started.InstanceId);
        var snapshotObservation = await context.ObserveAsync(
            _ => instanceHandle.GetSnapshotAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            snapshotObservation,
            snapshot => snapshot.Status == global::OrcaCore.WorkflowInstanceStatus.Completed,
            "The replacement-host occurrence did not reach a committed terminal snapshot. " +
            $"Deliveries={string.Join(',', finalDeliveries)}; " +
            $"Status={projectedTerminal.Status}; " +
            $"Waits={string.Join(',', projectedTerminal.ActiveWaits.Select(wait => wait.CorrelationId.Value))}.");
        var finalEnvelope = await EnvelopeAsync(store, started.InstanceId);
        var finalState = JsonSerializer.Deserialize<FanoutState>(finalEnvelope.StatePayload)!;
        if (selectorCalls != 1 ||
            mergeCalls != 1 ||
            !finalState.Results.SequenceEqual(["0:zero", "1:one", "2:two"]))
        {
            throw new InvalidOperationException(
                "Restart reselected source values, lost a terminal item, or changed deterministic indexed aggregation.");
        }
    }

    private static async Task AssertHostCeilingIsLowerLimitAsync(DurableWorkflowDefinition<string> definition)
    {
        using var store = new DurableScenarioProvider();
        using var runtime = CreateDurableRuntime(store, maxConcurrentExecutionPaths: 1);
        runtime.Register(definition);
        var started = await runtime.StartOrGetAsync<string, FanoutState>(
            "phase0-foreach-host-lower",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        for (var index = 0; index < 3; index++)
        {
            var snapshot = await SnapshotAsync(store, started.InstanceId);
            if (!snapshot.ActiveWaits.Select(wait => wait.CorrelationId.Value)
                    .SequenceEqual([$"Item-{index}"]))
            {
                throw new InvalidOperationException("The host ceiling did not tighten node admission to one path.");
            }

            await runtime.RaiseEventAsync(
                started.InstanceId,
                "Resume",
                CorrelationId.Create($"Item-{index}"),
                cancellationToken: CancellationToken.None);
        }

        if ((await SnapshotAsync(store, started.InstanceId)).Status != WorkflowInstanceStatus.Completed)
        {
            throw new InvalidOperationException("Ceiling-one item execution deadlocked before merge.");
        }
    }

    private static async Task AssertParkedItemsRetainNodeSlotsAsync(DurableWorkflowDefinition<string> definition)
    {
        using var store = new DurableScenarioProvider();
        using var runtime = CreateDurableRuntime(store, maxConcurrentExecutionPaths: 3);
        runtime.Register(definition);
        var started = await runtime.StartOrGetAsync<string, FanoutState>(
            "phase0-foreach-node-lower",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var initial = await SnapshotAsync(store, started.InstanceId);
        if (!initial.ActiveWaits.Select(wait => wait.CorrelationId.Value)
                .Order()
                .SequenceEqual(["Item-0", "Item-1"]))
        {
            throw new InvalidOperationException("The node limit did not admit exactly two items.");
        }

        await runtime.RaiseEventAsync(
            started.InstanceId,
            "Resume",
            CorrelationId.Create("Item-0"),
            cancellationToken: CancellationToken.None);
        var readmitted = await SnapshotAsync(store, started.InstanceId);
        if (!readmitted.ActiveWaits.Select(wait => wait.CorrelationId.Value)
                .Order()
                .SequenceEqual(["Item-1", "Item-2"]))
        {
            throw new InvalidOperationException(
                "A parked item released its admitted-item slot before becoming terminal.");
        }

        foreach (var index in new[] { 1, 2 })
        {
            await runtime.RaiseEventAsync(
                started.InstanceId,
                "Resume",
                CorrelationId.Create($"Item-{index}"),
                cancellationToken: CancellationToken.None);
        }

        if ((await SnapshotAsync(store, started.InstanceId)).Status != WorkflowInstanceStatus.Completed)
        {
            throw new InvalidOperationException("Node-limited item execution did not complete.");
        }
    }

    private static async Task AssertOversizedEphemeralSelectionRejectsBeforeItemStateAsync()
    {
        var itemStateCalls = 0;
        var definition = Workflow.Ephemeral<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []))
            .ForEach<string, ItemState, string>(
                _ => ["zero", "one"],
                ForEachOptions.Create(1),
                item =>
                {
                    itemStateCalls++;
                    return new ItemState(item.Index, item.Item, $"Item-{item.Index}");
                },
                body => body.Return(state => state.Value.Value))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        using var provider = CreateEphemeralHost();
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create("oversized-ephemeral-selection"),
            CancellationToken.None)).GetHandleOrThrow();
        var failed = await instance.GetSnapshotAsync(CancellationToken.None);
        if (failed.Status != WorkflowInstanceStatus.Failed ||
            failed.Failure?.Code != "SFE-LIMIT-001" ||
            itemStateCalls != 0)
        {
            throw new InvalidOperationException(
                $"An oversized selection admitted or materialized an item before bound rejection " +
                $"(status: {failed.Status}, code: {failed.Failure?.Code ?? "<null>"}, " +
                $"item-state calls: {itemStateCalls}).");
        }
    }

    private static DurableWorkflowDefinition<string, string> BuildDurableDeadlineDefinition(
        FanoutShape shape,
        bool collectOutcomes,
        Action onMerge)
    {
        var root = Workflow.Durable<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []))
            .CompleteWithin(TimeSpan.FromMinutes(1));
        if (shape == FanoutShape.Parallel)
        {
            var join = root.Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first", "Deadline-Branch-0"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second", "Deadline-Branch-1"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                        .Return(state => state.Value.Name)));
            return collectOutcomes
                ? join.WhenAllOutcomes((parent, _) => RecordMerge(parent, onMerge))
                    .End(_ => "unreachable").Build()
                : join.WhenAll((parent, _) => RecordMerge(parent, onMerge))
                    .End(_ => "unreachable").Build();
        }

        var forEach = root.ForEach<string, ItemState, string>(
            _ => ["first", "second"],
            ForEachOptions.Create(2, 2),
            item => new ItemState(item.Index, item.Item, $"Deadline-Item-{item.Index}"),
            item => item
                .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                .Return(state => state.Value.Value));
        return collectOutcomes
            ? forEach.WhenAllOutcomes((parent, _) => RecordMerge(parent, onMerge))
                .End(_ => "unreachable").Build()
            : forEach.WhenAll((parent, _) => RecordMerge(parent, onMerge))
                .End(_ => "unreachable").Build();
    }

    private static EphemeralWorkflowDefinition<string, string> BuildEphemeralDeadlineDefinition(
        FanoutShape shape,
        bool collectOutcomes,
        Action onMerge)
    {
        var root = Workflow.Ephemeral<FanoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new FanoutState(value, []))
            .CompleteWithin(TimeSpan.FromMinutes(1));
        if (shape == FanoutShape.Parallel)
        {
            var join = root.Parallel<string>(branches => branches
                .Branch<BranchState>(
                    AuthoredBranchId.Create("first"),
                    _ => new BranchState("first", "Deadline-Branch-0"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                        .Return(state => state.Value.Name))
                .Branch<BranchState>(
                    AuthoredBranchId.Create("second"),
                    _ => new BranchState("second", "Deadline-Branch-1"),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                        .Return(state => state.Value.Name)));
            return collectOutcomes
                ? join.WhenAllOutcomes((parent, _) => RecordMerge(parent, onMerge))
                    .End(_ => "unreachable").Build()
                : join.WhenAll((parent, _) => RecordMerge(parent, onMerge))
                    .End(_ => "unreachable").Build();
        }

        var forEach = root.ForEach<string, ItemState, string>(
            _ => ["first", "second"],
            ForEachOptions.Create(2, 2),
            item => new ItemState(item.Index, item.Item, $"Deadline-Item-{item.Index}"),
            item => item
                .Wait(WorkflowEventContract.Create(EventName.Create("Resume"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.EventName))
                .Return(state => state.Value.Value));
        return collectOutcomes
            ? forEach.WhenAllOutcomes((parent, _) => RecordMerge(parent, onMerge))
                .End(_ => "unreachable").Build()
            : forEach.WhenAll((parent, _) => RecordMerge(parent, onMerge))
                .End(_ => "unreachable").Build();
    }

    private static FanoutState RecordMerge(ReadOnlyStateSnapshot<FanoutState> parent, Action onMerge)
    {
        onMerge();
        return parent.Value with { Results = ["merged"] };
    }

    private static async Task AssertEphemeralDeadlineSuppressesMergeAsync(
        Phase0ScenarioContext context,
        EphemeralWorkflowDefinition<string, string> definition,
        Func<int> mergeCalls)
    {
        using var provider = CreateEphemeralHost(context.Services.TimeProvider);
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create("ephemeral-deadline-suppresses-merge"),
            CancellationToken.None)).GetHandleOrThrow();
        var waiting = await instance.GetSnapshotAsync(CancellationToken.None);
        if (waiting.Status != WorkflowInstanceStatus.Waiting || waiting.ActiveWaits.Count != 2)
        {
            throw new InvalidOperationException(
                "The ephemeral deadline did not begin with two active fan-out children.");
        }

        var terminalSignal = instance.WaitForOutputAsync(CancellationToken.None).AsTask();
        context.AdvanceTimeBy(TimeSpan.FromMinutes(1));
        await AwaitTerminalWithoutOutputAsync(terminalSignal);
        var terminal = await instance.GetSnapshotAsync(CancellationToken.None);
        var state = await instance.GetStateAsync<FanoutState>(CancellationToken.None);
        if (terminal.Status != WorkflowInstanceStatus.TimedOut ||
            terminal.ActiveWaits.Count != 0 ||
            mergeCalls() != 0 ||
            state.Results.Count != 0)
        {
            throw new InvalidOperationException(
                "The ephemeral workflow deadline did not fence fan-out and suppress its merge.");
        }
    }

    private static ServiceProvider CreateEphemeralHost(TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services.BuildServiceProvider();
    }

    private static async Task AssertDurableDeadlineSuppressesMergeAsync(
        Phase0ScenarioContext context,
        DurableWorkflowDefinition<string, string> definition,
        string key,
        Func<int> mergeCalls)
    {
        using var store = new DurableScenarioProvider(context.Services.TimeProvider);
        using var runtime = CreateDurableRuntime(store, timeProvider: context.Services.TimeProvider);
        var definitionHandle = runtime.Register(definition);
        var startKey = $"phase0-{key}";
        var started = (await definitionHandle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create(startKey),
            CancellationToken.None)).GetHandleOrThrow();
        var waiting = await SnapshotAsync(store, started.InstanceId);
        if (waiting.Status != WorkflowInstanceStatus.Waiting || waiting.ActiveWaits.Count != 2)
        {
            throw new InvalidOperationException(
                "The durable deadline did not begin with two active fan-out children.");
        }

        var terminalSignal = started.WaitForOutputAsync(CancellationToken.None).AsTask();
        context.AdvanceTimeBy(TimeSpan.FromMinutes(1));
        var hostedServices = runtime.Services.GetServices<IHostedService>().ToArray();
        try
        {
            foreach (var hostedService in hostedServices)
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            context.AdvanceTimeBy(TimeSpan.FromSeconds(2));
            await AwaitTerminalWithoutOutputAsync(terminalSignal);
        }
        finally
        {
            foreach (var hostedService in hostedServices.Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
        }

        using var replacement = CreateDurableRuntime(store, timeProvider: context.Services.TimeProvider);
        var replacementHandle = replacement.Register(definition);
        _ = await replacementHandle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create(startKey),
            CancellationToken.None);
        var terminal = await SnapshotAsync(store, started.InstanceId);
        var state = JsonSerializer.Deserialize<FanoutState>(
            (await EnvelopeAsync(store, started.InstanceId)).StatePayload)!;
        if (terminal.Status != WorkflowInstanceStatus.TimedOut ||
            terminal.ActiveWaits.Count != 0 ||
            mergeCalls() != 0 ||
            state.Results.Count != 0)
        {
            throw new InvalidOperationException(
                "The durable workflow deadline did not fence fan-out and suppress its merge. " +
                $"Status={terminal.Status}; Waits={terminal.ActiveWaits.Count}; " +
                $"MergeCalls={mergeCalls()}; StateResults={string.Join(",", state.Results)}.");
        }
    }

    private static void ConsumeBarrier(Phase0ScenarioContext context, string name) =>
        context.Services.Barrier.ReachAsync(name).GetAwaiter().GetResult();

    private static DurableScenarioRuntime CreateDurableRuntime(
        DurableScenarioProvider store,
        int maxConcurrentExecutionPaths = int.MaxValue,
        Action<IServiceCollection>? configureServices = null,
        TimeProvider? timeProvider = null,
        string? partition = null) =>
        DurableScenarioRuntime.Create(
            store,
            timeProvider,
            maxConcurrentExecutionPaths,
            configureServices: configureServices,
            partition: partition);

    private static async Task<WorkflowProjectionSnapshot> SnapshotAsync(
        DurableScenarioProvider store,
        InstanceId instanceId)
    {
        var projected = await store.GetAsync(instanceId, CancellationToken.None);
        return projected.HasValue
            ? projected.Value
            : throw new InvalidOperationException($"Missing projection for '{instanceId}'.");
    }

    private static async Task<DurableExecutionEnvelopeV2> EnvelopeAsync(
        IWorkflowEventStore store,
        InstanceId instanceId)
    {
        var checkpoint = await store.LoadCheckpointAsync(instanceId, CancellationToken.None);
        return DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
    }

    private static async Task AwaitTerminalWithoutOutputAsync(Task<string> output)
    {
        try
        {
            _ = await output;
            throw new InvalidOperationException(
                "The deadline workflow produced output instead of terminating without one.");
        }
        catch (WorkflowOutputUnavailableException)
        {
        }
    }

    public sealed record FanoutState(string Value, List<string> Results);

    public sealed record BranchState(string Name, string EventName);

    public sealed record ItemState(int Index, string Value, string EventName);

    private enum FanoutShape
    {
        Parallel,
        ForEach
    }

    public sealed class ScenarioFailureException(string code)
        : OrcaCoreException(code, $"Scenario failure {code}.");

    public sealed class FailByNameStep : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(
                new StepResult.Failed(
                    new ScenarioFailureException($"SCENARIO-{context.State.Name.ToUpperInvariant()}")));
    }

}
