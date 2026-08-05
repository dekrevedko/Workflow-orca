using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using Microsoft.Extensions.DependencyInjection;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class DeadlineRetryScenarioHost
{
    [Phase0Scenario("retry-classification-exclusions", "3.9")]
    public static async Task RetryClassificationExclusions(Phase0ScenarioContext context)
    {
        var probe = new ExecutionProbe { FailureCode = "SFE-TYPE-SCENARIO" };
        var builder = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ScenarioState());
        var observed = context.Observe(_ => builder.Then<ProbeStep>().WithRetry(3));
        DurableWorkflowBuilder<string, ScenarioState>? selected = null;
        Phase0Assert.Satisfies(
            observed,
            successor =>
            {
                selected = successor;
                return successor is not null;
            },
            "WithRetry did not return a new authoring epoch.");
        var definition = selected!.End().Build();
        var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);

        var started = await runtime.StartOrGetAsync<string, ScenarioState>(
            "deadline-retry-exclusion",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var snapshot = await SnapshotAsync(store, started.InstanceId);
        if (snapshot.Status != WorkflowInstanceStatus.Failed || probe.Executions.Count != 1)
        {
            throw new InvalidOperationException(
                "A non-retryable SFE-TYPE failure consumed retry budget.");
        }
    }

    [Phase0Scenario("duplicate-completewithin-eager", "3.9")]
    public static void DuplicateCompleteWithinIsEager(Phase0ScenarioContext context)
    {
        var root = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ScenarioState());
        var first = root.CompleteWithin(TimeSpan.FromMinutes(1));
        var observed = context.ObserveThrows<WorkflowDefinitionException, DurableWorkflowBuilder<string, ScenarioState>>(
            _ => first.CompleteWithin(TimeSpan.FromMinutes(2)));
        Phase0Assert.Satisfies(
            observed,
            exception =>
                exception.Diagnostics.Count == 1 &&
                exception.Diagnostics[0].Code == "SFE-AUTH-DEADLINE-001" &&
                exception.Diagnostics[0].RelatedLocations.Count == 1,
            "The duplicate deadline did not report one primary and one related location.");

        var selected = first.End().Build();
        var independentlyAuthored = Workflow.Durable<ScenarioState>(
                selected.DefinitionId,
                selected.DefinitionVersion)
            .Init<string>(_ => new ScenarioState())
            .CompleteWithin(TimeSpan.FromMinutes(1))
            .End()
            .Build();
        if (!selected.DefinitionFingerprint.Equals(independentlyAuthored.DefinitionFingerprint))
        {
            throw new InvalidOperationException("The rejected second deadline replaced the first selection.");
        }
    }

    [Phase0Scenario("deadline-persistence-inheritance", "3.9")]
    public static async Task DeadlinePersistsAcrossReplacement(Phase0ScenarioContext context)
    {
        const string barrier = "deadline-authoring";
        context.ReleaseBarrier(barrier);
        var root = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new ScenarioState();
            });
        var observedDeadline = context.Observe(_ => root.CompleteWithin(TimeSpan.FromMinutes(5)));
        DurableWorkflowBuilder<string, ScenarioState>? deadlineBuilder = null;
        Phase0Assert.Satisfies(
            observedDeadline,
            successor =>
            {
                deadlineBuilder = successor;
                return successor is not null;
            },
            "CompleteWithin did not create a deadline-bearing successor.");
        var definition = deadlineBuilder!
            .Wait(EventName.Create("Resume"), _ => CorrelationId.Create("deadline"))
            .End()
            .Build();
        var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var first = CreateRuntime(store, definition, new ExecutionProbe(), context.Services.TimeProvider);
        var started = await first.StartOrGetAsync<string, ScenarioState>(
            "deadline-replacement",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var originalDeadline = (await EnvelopeAsync(store, started.InstanceId)).WorkflowDeadline;

        context.AdvanceTimeBy(TimeSpan.FromMinutes(6));
        var replacement = CreateRuntime(store, definition, new ExecutionProbe(), context.Services.TimeProvider);
        await replacement.RaiseEventAsync(
            started.InstanceId,
            "Resume",
            CorrelationId.Create("deadline"),
            cancellationToken: CancellationToken.None);
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            replacement,
            definition);
        var instanceHandle = await definitionHandle.GetInstanceAsync(
            started.InstanceId,
            CancellationToken.None);
        var observedSnapshot = await context.ObserveAsync(
            _ => instanceHandle.GetSnapshotAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            observedSnapshot,
            snapshot => snapshot.Status == global::OrcaCore.WorkflowInstanceStatus.TimedOut,
            "Replacement-host advancement did not honor the original absolute deadline.");
        var finalDeadline = (await EnvelopeAsync(store, started.InstanceId)).WorkflowDeadline;
        if (originalDeadline is null || finalDeadline != originalDeadline)
        {
            throw new InvalidOperationException("The absolute workflow deadline reset across host replacement.");
        }
    }

    [Phase0Scenario("attempt-copy-fencing-overlap", "3.9")]
    public static async Task AttemptCopiesFenceLateResults(Phase0ScenarioContext context)
    {
        const string barrier = "attempt-copy";
        context.ReleaseBarrier(barrier);
        var probe = new ExecutionProbe
        {
            BlockFirst = true,
            ReplaceStatePerAttempt = true
        };
        var root = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new ScenarioState();
            })
            .Then<ProbeStep>()
            .WithRetry(2);
        var observedTimeout = context.Observe(_ => root.WithStepTimeout(TimeSpan.FromMilliseconds(30)));
        DurableWorkflowBuilder<string, ScenarioState>? timeoutBuilder = null;
        Phase0Assert.Satisfies(
            observedTimeout,
            successor =>
            {
                timeoutBuilder = successor;
                return successor is not null;
            },
            "WithStepTimeout did not bind to the preceding business step.");
        var definition = timeoutBuilder!.End().Build();
        var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);
        var running = runtime.StartOrGetAsync<string, ScenarioState>(
            "attempt-copy-fence",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None).AsTask();
        await probe.FirstStarted.Task;
        context.AdvanceTimeBy(TimeSpan.FromMilliseconds(31));
        await probe.SecondStarted.Task;
        probe.ReleaseFirst.TrySetResult();
        var started = await running;
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            definition);
        var instanceHandle = await definitionHandle.GetInstanceAsync(
            started.InstanceId,
            CancellationToken.None);
        var stateObservation = await context.ObserveAsync(
            _ => instanceHandle.GetStateAsync<ScenarioState>(CancellationToken.None));
        ScenarioState? committedState = null;
        Phase0Assert.Satisfies(
            stateObservation,
            state =>
            {
                committedState = state;
                return committedState is not null;
            },
            "The fenced terminal state was not available through the instance handle.");
        if (probe.Executions.Count != 2 || committedState!.Value != "attempt-2")
        {
            throw new InvalidOperationException(
                "The late first attempt committed over the winning detached attempt.");
        }
    }

    [Phase0Scenario("coordinate-persisted-before-dispatch", "3.9")]
    public static async Task CoordinatePersistsBeforeDispatch(Phase0ScenarioContext context)
    {
        var probe = new ExecutionProbe();
        var fixture = CreateCoordinateFixture(context, probe, "coordinate-before-dispatch");
        var first = CreateRuntime(
            fixture.Store,
            fixture.Definition,
            probe,
            context.Services.TimeProvider);
        var instanceId = await FreezeAfterAttemptAdmissionAsync(first, fixture);
        var beforeDispatch = await EnvelopeAsync(fixture.Store, instanceId);
        var admittedFiber = beforeDispatch.Fibers.Single();
        if (probe.Executions.Count != 0 ||
            admittedFiber.LogicalOperationKey is null ||
            !admittedFiber.AttemptInFlight)
        {
            throw new InvalidOperationException("The complete attempt coordinate was not committed before dispatch.");
        }

        var replacement = CreateRuntime(
            fixture.Store,
            fixture.Definition,
            probe,
            context.Services.TimeProvider);
        await replacement.StartOrGetAsync<string, ScenarioState>(
            fixture.Key,
            fixture.Definition.DefinitionId,
            fixture.Definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var execution = probe.Executions.Single();
        if (execution.OperationId.Value != admittedFiber.LogicalOperationKey)
        {
            throw new InvalidOperationException(
                "The replacement dispatch did not use the persisted attempt coordinate.");
        }
        var operation = context.Observe(_ => execution.OperationId);
        Phase0Assert.Satisfies(operation, value => !string.IsNullOrWhiteSpace(value.Value), "OperationId was empty.");
        var attempt = context.Observe(_ => execution.AttemptNumber);
        Phase0Assert.Equal(1, attempt, "The first persisted attempt ordinal was not one.");
    }

    [Phase0Scenario("same-coordinate-redispatch", "3.9")]
    public static async Task RedispatchReusesWholeCoordinate(Phase0ScenarioContext context)
    {
        var probe = new ExecutionProbe();
        var fixture = CreateCoordinateFixture(context, probe, "same-coordinate-redispatch");
        var first = CreateRuntime(
            fixture.Store,
            fixture.Definition,
            probe,
            context.Services.TimeProvider);
        var instanceId = await FreezeAfterAttemptAdmissionAsync(first, fixture);
        var committed = await EnvelopeAsync(fixture.Store, instanceId);
        var committedCoordinate = committed.Fibers.Single().LogicalOperationKey;
        var committedAttempt = committed.Fibers.Single().RetryAttempt;

        var replacement = CreateRuntime(fixture.Store, fixture.Definition, probe, context.Services.TimeProvider);
        await replacement.StartOrGetAsync<string, ScenarioState>(
            fixture.Key,
            fixture.Definition.DefinitionId,
            fixture.Definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        var execution = probe.Executions.Single();
        var operation = context.Observe(_ => execution.OperationId);
        Phase0Assert.Satisfies(
            operation,
            value => value.Value == committedCoordinate,
            "Replacement dispatch minted another operation ID.");
        var attempt = context.Observe(_ => execution.AttemptNumber);
        Phase0Assert.Equal(
            committedAttempt > 0 ? committedAttempt : 1,
            attempt,
            "Replacement dispatch consumed retry budget.");
    }

    private static async Task<InstanceId> FreezeAfterAttemptAdmissionAsync(
        DurableScenarioRuntime runtime,
        CoordinateFixture fixture)
    {
        using var hostShutdown = new CancellationTokenSource();
        fixture.Store.AfterSuccessfulAppend = batch =>
        {
            if (batch.Checkpoint is not { } checkpoint)
            {
                return;
            }

            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
            if (envelope.Fibers.Any(fiber => fiber.AttemptInFlight))
            {
                hostShutdown.Cancel();
            }
        };

        try
        {
            _ = await runtime.StartOrGetAsync<string, ScenarioState>(
                fixture.Key,
                fixture.Definition.DefinitionId,
                fixture.Definition.DefinitionVersion,
                "input",
                hostShutdown.Token);
            throw new InvalidOperationException(
                "The first host did not stop after committing attempt admission.");
        }
        catch (OperationCanceledException) when (hostShutdown.IsCancellationRequested)
        {
        }
        finally
        {
            fixture.Store.AfterSuccessfulAppend = null;
        }

        var binding = await fixture.Store.GetStartedAsync(fixture.Key, CancellationToken.None);
        return binding.HasValue
            ? binding.Value.InstanceId
            : throw new InvalidOperationException("Attempt admission did not retain the durable start binding.");
    }

    [Phase0Scenario("committed-retry-increment", "3.9")]
    public static async Task RetryOrdinalIncrementsOnlyAfterCommittedFailure(Phase0ScenarioContext context)
    {
        const string barrier = "retry-increment";
        context.ReleaseBarrier(barrier);
        var probe = new ExecutionProbe { FailFirst = true };
        var builder = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new ScenarioState();
            })
            .Then<ProbeStep>();
        var observedRetry = context.Observe(_ => builder.WithRetry(2));
        DurableWorkflowBuilder<string, ScenarioState>? retryBuilder = null;
        Phase0Assert.Satisfies(
            observedRetry,
            successor =>
            {
                retryBuilder = successor;
                return successor is not null;
            },
            "WithRetry did not retain the approved maxAttempts policy.");
        var definition = retryBuilder!.End().Build();
        var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);
        await runtime.StartOrGetAsync<string, ScenarioState>(
            "committed-retry-increment",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        var second = probe.Executions[1];
        var attempt = context.Observe(_ => second.AttemptNumber);
        Phase0Assert.Equal(2, attempt, "The eligible committed failure did not produce attempt two.");
        if (probe.Executions.Count != 2 ||
            probe.Executions[0].AttemptNumber != 1 ||
            !probe.Executions[0].OperationId.Equals(second.OperationId))
        {
            throw new InvalidOperationException("Retry changed logical identity or skipped an ordinal.");
        }
    }

    [Phase0Scenario("maxattempts-one-two-expired-replay", "3.9")]
    public static async Task ExpiredReplayDoesNotRedispatchExpiredAttempt(Phase0ScenarioContext context)
    {
        const string barrier = "expired-attempt";
        context.ReleaseBarrier(barrier);
        var probe = new ExecutionProbe { BlockFirst = true };
        var builder = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new ScenarioState();
            })
            .Then<ProbeStep>()
            .WithRetry(2);
        var observedTimeout = context.Observe(_ => builder.WithStepTimeout(TimeSpan.FromMilliseconds(30)));
        DurableWorkflowBuilder<string, ScenarioState>? timeoutBuilder = null;
        Phase0Assert.Satisfies(
            observedTimeout,
            successor =>
            {
                timeoutBuilder = successor;
                return successor is not null;
            },
            "WithStepTimeout did not bind the absolute attempt deadline.");
        var definition = timeoutBuilder!.End().Build();
        var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);
        var running = runtime.StartOrGetAsync<string, ScenarioState>(
            "expired-attempt-two",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None).AsTask();
        await probe.FirstStarted.Task;
        context.AdvanceTimeBy(TimeSpan.FromMilliseconds(31));
        await probe.SecondStarted.Task;
        probe.ReleaseFirst.TrySetResult();
        _ = await running;

        var execution = probe.Executions.Single(candidate => candidate.AttemptNumber == 2);
        var observedAttempt = context.Observe(_ => execution.AttemptNumber);
        Phase0Assert.Equal(2, observedAttempt, "Expired replay redispatched attempt one.");

        var singleAttemptProbe = new ExecutionProbe { BlockFirst = true };
        var singleDefinition = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ScenarioState())
            .Then<ProbeStep>()
            .WithRetry(1)
            .WithStepTimeout(TimeSpan.FromMilliseconds(30))
            .End(_ => "unreachable")
            .Build();
        var singleStore = new DurableScenarioProvider(context.Services.TimeProvider);
        var singleRuntime = DurableScenarioRuntime.Create(
            singleStore,
            context.Services.TimeProvider,
            configureServices: services =>
                services.AddTransient<ProbeStep>(_ => new ProbeStep(singleAttemptProbe)));
        var singleDefinitionHandle = singleRuntime.Register(singleDefinition);
        var singleRunning = singleDefinitionHandle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create("expired-attempt-one"),
            CancellationToken.None).AsTask();
        await singleAttemptProbe.FirstStarted.Task;
        var singleBinding = await singleStore.GetStartedAsync(
            "expired-attempt-one",
            CancellationToken.None);
        if (!singleBinding.HasValue)
        {
            throw new InvalidOperationException(
                "The maxAttempts-one occurrence did not retain its durable start binding.");
        }

        var singleInstance = await singleDefinitionHandle.GetInstanceAsync(
            singleBinding.Value.InstanceId,
            CancellationToken.None);
        var singleTerminal = singleInstance.WaitForOutputAsync(CancellationToken.None).AsTask();
        context.AdvanceTimeBy(TimeSpan.FromMilliseconds(31));
        try
        {
            await AwaitTerminalWithoutOutputAsync(singleTerminal);
        }
        finally
        {
            singleAttemptProbe.ReleaseFirst.TrySetResult();
        }

        var singleStarted = (await singleRunning).GetHandleOrThrow();
        var singleSnapshot = await SnapshotAsync(singleStore, singleStarted.InstanceId);
        if (singleStarted.InstanceId != singleBinding.Value.InstanceId ||
            singleAttemptProbe.Executions.Count != 1 ||
            singleSnapshot.Status != WorkflowInstanceStatus.Failed)
        {
            throw new InvalidOperationException(
                "maxAttempts one redispatched expired physical work. " +
                $"Executions={singleAttemptProbe.Executions.Count}; Status={singleSnapshot.Status}; " +
                $"ExpectedInstance={singleBinding.Value.InstanceId}; ActualInstance={singleStarted.InstanceId}.");
        }
    }

    [Phase0Scenario("distinct-logical-occurrences", "3.9")]
    public static async Task LogicalOccurrencesMintDistinctIds(Phase0ScenarioContext context)
    {
        const string barrier = "distinct-occurrences";
        context.ReleaseBarrier(barrier);
        var probe = new ExecutionProbe();
        var definition = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new ScenarioState();
            })
            .Then<ProbeStep>()
            .Then<ProbeStep>()
            .End()
            .Build();
        var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);
        await runtime.StartOrGetAsync<string, ScenarioState>(
            "distinct-occurrences",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        var first = probe.Executions[0];
        var observed = context.Observe(_ => first.OperationId);
        Phase0Assert.Satisfies(observed, id => !string.IsNullOrWhiteSpace(id.Value), "OperationId was empty.");
        if (probe.Executions.Count != 2 ||
            first.OperationId == probe.Executions[1].OperationId ||
            probe.Executions.Any(execution => execution.AttemptNumber != 1))
        {
            throw new InvalidOperationException("Distinct authored occurrences reused a logical operation ID.");
        }
    }

    private static CoordinateFixture CreateCoordinateFixture(
        Phase0ScenarioContext context,
        ExecutionProbe probe,
        string key)
    {
        var barrier = $"{key}-authoring";
        context.ReleaseBarrier(barrier);
        var definition = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new ScenarioState();
            })
            .Then<ProbeStep>()
            .End()
            .Build();
        return new CoordinateFixture(
            new DurableScenarioProvider(context.Services.TimeProvider),
            definition,
            key);
    }

    private static DurableScenarioRuntime CreateRuntime(
        DurableScenarioProvider store,
        DurableWorkflowDefinition<string> definition,
        ExecutionProbe probe,
        TimeProvider timeProvider)
    {
        var runtime = DurableScenarioRuntime.Create(
            store,
            timeProvider,
            configureServices: services =>
                services.AddTransient<ProbeStep>(_ => new ProbeStep(probe)));
        runtime.Register(definition);
        return runtime;
    }

    private static async Task<WorkflowProjectionSnapshot> SnapshotAsync(
        DurableScenarioProvider store,
        InstanceId instanceId) =>
        (await store.GetAsync(instanceId, CancellationToken.None)).Value;

    private static async Task AwaitTerminalWithoutOutputAsync(Task<string> output)
    {
        try
        {
            _ = await output;
            throw new InvalidOperationException(
                "The expired-attempt workflow produced output instead of terminating without one.");
        }
        catch (WorkflowOutputUnavailableException)
        {
        }
    }

    private static async Task<DurableExecutionEnvelopeV2> EnvelopeAsync(
        DurableScenarioProvider store,
        InstanceId instanceId)
    {
        var checkpoint = await store.LoadCheckpointAsync(instanceId, CancellationToken.None);
        return DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
    }

    private static void ConsumeBarrier(Phase0ScenarioContext context, string name) =>
        context.Services.Barrier.ReachAsync(name).GetAwaiter().GetResult();

    private sealed record CoordinateFixture(
        DurableScenarioProvider Store,
        DurableWorkflowDefinition<string> Definition,
        string Key);

    public sealed record ScenarioState(string? Value = null);

    private sealed class ExecutionProbe
    {
        internal List<StepExecutionContext> Executions { get; } = [];
        internal string? FailureCode { get; init; }
        internal bool FailFirst { get; init; }
        internal bool BlockFirst { get; init; }
        internal bool ReplaceStatePerAttempt { get; init; }
        internal TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ProbeStep(ExecutionProbe probe) : IStep<ScenarioState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<ScenarioState> context,
            CancellationToken cancellationToken)
        {
            lock (probe.Executions)
            {
                probe.Executions.Add(context.Execution);
            }

            if (context.Execution.AttemptNumber == 1)
            {
                probe.FirstStarted.TrySetResult();
                if (probe.BlockFirst)
                {
                    await probe.ReleaseFirst.Task;
                }
            }
            else if (context.Execution.AttemptNumber == 2)
            {
                probe.SecondStarted.TrySetResult();
            }

            if (probe.ReplaceStatePerAttempt)
            {
                context.ReplaceState(new ScenarioState($"attempt-{context.Execution.AttemptNumber}"));
            }

            var failureCode = probe.FailureCode;
            if (failureCode is not null ||
                probe.FailFirst && context.Execution.AttemptNumber == 1)
            {
                return new StepResult.Failed(
                    new ScenarioFailure(failureCode ?? "SCENARIO-TRANSIENT"));
            }

            return new StepResult.Completed();
        }
    }

    private sealed class ScenarioFailure(string code) : OrcaCoreException(code, code);

    private static class FixedWorkflowValueCodecProxy
    {
        internal static T Deserialize<T>(byte[] payload) =>
            System.Text.Json.JsonSerializer.Deserialize<T>(payload)
            ?? throw new InvalidOperationException("Scenario state could not be decoded.");
    }
}
