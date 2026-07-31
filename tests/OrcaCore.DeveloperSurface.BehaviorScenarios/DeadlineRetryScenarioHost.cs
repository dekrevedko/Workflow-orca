using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;

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
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);

        await runtime.StartOrGetAsync<string, ScenarioState>(
            "deadline-retry-exclusion",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var snapshot = await SnapshotAsync(store);
        if (snapshot.Status != LegacyWorkflowStatus.Failed || probe.Executions.Count != 1)
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
        var store = new InMemoryWorkflowProvider();
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
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
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
            FirstDelay = TimeSpan.FromMilliseconds(120),
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
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);
        var started = await runtime.StartOrGetAsync<string, ScenarioState>(
            "attempt-copy-fence",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
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
            context.Services.TimeProvider,
            new DurableDriverBudget(1, TimeSpan.FromMinutes(1)));
        var started = await first.StartOrGetAsync<string, ScenarioState>(
            fixture.Key,
            fixture.Definition.DefinitionId,
            fixture.Definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var beforeDispatch = await EnvelopeAsync(fixture.Store, started.InstanceId);
        if (probe.Executions.Count != 0 ||
            beforeDispatch.Fibers.Single().LogicalOperationKey is null ||
            !beforeDispatch.Fibers.Single().AttemptInFlight)
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
            context.Services.TimeProvider,
            new DurableDriverBudget(1, TimeSpan.FromMinutes(1)));
        var started = await first.StartOrGetAsync<string, ScenarioState>(
            fixture.Key,
            fixture.Definition.DefinitionId,
            fixture.Definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        var committed = await EnvelopeAsync(fixture.Store, started.InstanceId);
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
        var store = new InMemoryWorkflowProvider();
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
        var probe = new ExecutionProbe { FirstDelay = TimeSpan.FromMilliseconds(120) };
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
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store, definition, probe, context.Services.TimeProvider);
        await runtime.StartOrGetAsync<string, ScenarioState>(
            "expired-attempt-two",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        var execution = probe.Executions.Single(candidate => candidate.AttemptNumber == 2);
        var observedAttempt = context.Observe(_ => execution.AttemptNumber);
        Phase0Assert.Equal(2, observedAttempt, "Expired replay redispatched attempt one.");

        var singleAttemptProbe = new ExecutionProbe { FirstDelay = TimeSpan.FromMilliseconds(120) };
        var singleDefinition = Workflow.Durable<ScenarioState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ScenarioState())
            .Then<ProbeStep>()
            .WithRetry(1)
            .WithStepTimeout(TimeSpan.FromMilliseconds(30))
            .End()
            .Build();
        var singleStore = new InMemoryWorkflowProvider();
        var singleRuntime = CreateRuntime(
            singleStore,
            singleDefinition,
            singleAttemptProbe,
            context.Services.TimeProvider);
        await singleRuntime.StartOrGetAsync<string, ScenarioState>(
            "expired-attempt-one",
            singleDefinition.DefinitionId,
            singleDefinition.DefinitionVersion,
            "input",
            CancellationToken.None);
        if (singleAttemptProbe.Executions.Count != 1 ||
            (await SnapshotAsync(singleStore)).Status != LegacyWorkflowStatus.Failed)
        {
            throw new InvalidOperationException("maxAttempts one redispatched expired physical work.");
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
        var store = new InMemoryWorkflowProvider();
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
        return new CoordinateFixture(new InMemoryWorkflowProvider(), definition, key);
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        DurableWorkflowDefinition<string> definition,
        ExecutionProbe probe,
        TimeProvider timeProvider,
        DurableDriverBudget? budget = null)
    {
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store),
            new DurableDefinitionRegistry(new ProbeServices(probe)),
            timeProvider,
            budget ?? DurableDriverBudget.Default);
        runtime.RegisterDefinition(RuntimeDefinition<ScenarioState>(definition));
        return runtime;
    }

    private static WorkflowDefinition<TState> RuntimeDefinition<TState>(object publicDefinition) =>
        (WorkflowDefinition<TState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;

    private static async Task<LegacyWorkflowInstanceSnapshot> SnapshotAsync(InMemoryWorkflowProvider store) =>
        (await store.ListAsync(new WorkflowProjectionQuery(), CancellationToken.None)).Single();

    private static async Task<DurableExecutionEnvelopeV2> EnvelopeAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId)
    {
        var checkpoint = await store.LoadCheckpointAsync(instanceId, CancellationToken.None);
        return DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
    }

    private static void ConsumeBarrier(Phase0ScenarioContext context, string name) =>
        context.Services.Barrier.ReachAsync(name).GetAwaiter().GetResult();

    private sealed record CoordinateFixture(
        InMemoryWorkflowProvider Store,
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
        internal TimeSpan FirstDelay { get; init; }
        internal TaskCompletionSource FirstStarted { get; } =
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
                else if (probe.FirstDelay > TimeSpan.Zero)
                {
                    await Task.Delay(probe.FirstDelay, TimeProvider.System, cancellationToken);
                }
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

    private sealed class ProbeServices(ExecutionProbe probe) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ProbeStep) ? new ProbeStep(probe) : null;
    }

    private static class FixedWorkflowValueCodecProxy
    {
        internal static T Deserialize<T>(byte[] payload) =>
            System.Text.Json.JsonSerializer.Deserialize<T>(payload)
            ?? throw new InvalidOperationException("Scenario state could not be decoded.");
    }
}
