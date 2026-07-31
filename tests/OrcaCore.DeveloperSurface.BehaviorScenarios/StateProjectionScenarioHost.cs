using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Dag;
using OrcaCore.Hosting;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class StateProjectionScenarioHost
{
    [Phase0Scenario("caller-created-text-values", "3.5")]
    public static void CallerCreatedTextValuesHaveOneValidatedConstructionPath(
        Phase0ScenarioContext context)
    {
        var observed = context.Observe(_ => EventName.Create("observed-event"));
        Phase0Assert.Satisfies(
            observed,
            value => value.Value == "observed-event",
            "EventName.Create did not preserve the validated caller value.");

        Type[] types =
        [
            typeof(EventName),
            typeof(WorkflowOutcomeName),
            typeof(AuthoredBranchId),
            typeof(DagNodeId),
            typeof(ResourcePoolName),
            typeof(TransientPoolName),
            typeof(StartIdempotencyKey),
            typeof(CorrelationId),
            typeof(EventId),
            typeof(StopConfirmationId),
            typeof(ResourcePoolOperationId),
            typeof(ResourceGovernancePartitionId)
        ];
        foreach (var type in types)
        {
            if (type.GetConstructors().Length != 0)
            {
                throw new InvalidOperationException($"{type.Name} exposes public construction.");
            }

            var factories = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (factories.Length != 1 ||
                factories[0].Name != "Create" ||
                factories[0].ReturnType != type ||
                !factories[0].GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual([typeof(string)]))
            {
                throw new InvalidOperationException($"{type.Name} does not expose exactly Create(string).");
            }

            var create = factories[0];
            var created = create.Invoke(null, [$"valid-{type.Name}"]);
            if (created?.GetType() != type)
            {
                throw new InvalidOperationException($"{type.Name}.Create returned a sibling strong-value role.");
            }

            foreach (var invalid in new string?[] { null, string.Empty, " ", " padded" })
            {
                try
                {
                    _ = create.Invoke(null, [invalid]);
                    throw new InvalidOperationException($"{type.Name}.Create accepted invalid caller text.");
                }
                catch (TargetInvocationException exception)
                    when (exception.InnerException is ArgumentException)
                {
                }
            }
        }

        if (types.SelectMany(type => types.Where(other => other != type)
                .Select(other => type.IsAssignableFrom(other)))
            .Any(assignable => assignable))
        {
            throw new InvalidOperationException("Sibling strong-value roles became interchangeable.");
        }
    }

    [Phase0Scenario("runtime-created-identities", "3.5")]
    public static void RuntimeCreatedIdentitiesExposeOnlyCanonicalParsing(
        Phase0ScenarioContext context)
    {
        var canonical = Guid.CreateVersion7().ToString("D");
        var observed = context.Observe(_ => InstanceId.Parse(canonical));
        Phase0Assert.Satisfies(
            observed,
            value => value.Value != Guid.Empty && value.ToString() == canonical,
            "InstanceId.Parse did not round-trip one nonempty canonical identity.");

        Type[] types =
        [
            typeof(InstanceId),
            typeof(WaitId),
            typeof(StepOperationId),
            typeof(LeaseProtectionToken),
            typeof(DagRunId)
        ];
        foreach (var type in types)
        {
            if (type.GetConstructors().Length != 0 ||
                type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static) is not null)
            {
                throw new InvalidOperationException($"{type.Name} exposed caller-selected construction.");
            }

            var parse = type.GetMethod(
                "Parse",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                [typeof(string)],
                modifiers: null) ??
                throw new InvalidOperationException($"{type.Name} has no canonical parser.");
            var text = type == typeof(StepOperationId)
                ? "step-runtime"
                : type == typeof(LeaseProtectionToken)
                    ? "lease-runtime"
                    : Guid.CreateVersion7().ToString("D");
            var identity = parse.Invoke(null, [text]) ??
                throw new InvalidOperationException($"{type.Name}.Parse returned null.");
            var json = JsonSerializer.Serialize(identity, type);
            var roundTrip = JsonSerializer.Deserialize(json, type);
            if (roundTrip is null || roundTrip.ToString() != identity.ToString())
            {
                throw new InvalidOperationException($"{type.Name} did not survive its product converter.");
            }

            var tryParse = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static) ??
                throw new InvalidOperationException($"{type.Name} has no TryParse contract.");
            var invalid = type == typeof(StepOperationId) || type == typeof(LeaseProtectionToken)
                ? " "
                : Guid.Empty.ToString("D");
            object?[] arguments = [invalid, null];
            if (tryParse.Invoke(null, arguments) is not false || arguments[1] is not null)
            {
                throw new InvalidOperationException($"{type.Name}.TryParse admitted an invalid runtime identity.");
            }
        }
    }

    [Phase0Scenario("readonly-snapshot-shape", "3.5")]
    public static async Task ReadOnlyStateSnapshotsAreRuntimeCreatedAndDetached(
        Phase0ScenarioContext context)
    {
        ReadOnlyStateSnapshot<ProjectionState>? captured = null;
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = Workflow.Ephemeral<ProjectionState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<ProjectionInput>(input => new ProjectionState(input.Value, [.. input.Items]))
            .If(
                snapshot =>
                {
                    captured = snapshot;
                    return false;
                },
                nested => nested.Delay(TimeSpan.FromMilliseconds(1)))
            .End()
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            new ProjectionInput(7, ["first"]),
            StartIdempotencyKey.Create("readonly-snapshot"))).GetHandleOrThrow();
        if (captured is null)
        {
            throw new InvalidOperationException("The runtime did not provide the authored selector snapshot.");
        }

        var value = context.Observe(_ => captured.Value);
        Phase0Assert.Satisfies(
            value,
            state => state.Value == 7 && state.Items.SequenceEqual(["first"]),
            "The runtime-created snapshot did not expose its detached typed value.");

        captured.Value.Items[0] = "mutated";
        var committed = await instance.GetStateAsync<ProjectionState>();
        if (!committed.Items.SequenceEqual(["first"]) ||
            typeof(ReadOnlyStateSnapshot<ProjectionState>).IsRecord() ||
            typeof(ReadOnlyStateSnapshot<ProjectionState>).GetConstructors().Length != 0 ||
            typeof(ReadOnlyStateSnapshot<ProjectionState>).GetMethod("Deconstruct") is not null)
        {
            throw new InvalidOperationException(
                "ReadOnlyStateSnapshot exposed construction/copy surface or retained committed state.");
        }
    }

    [Phase0Scenario("typed-completion-output", "3.5")]
    public static async Task TypedCompletionKeepsOutcomeAndOutputDistinct(
        Phase0ScenarioContext context)
    {
        var eventName = EventName.Create("typed-completion");
        var correlation = CorrelationId.Create("typed-completion");
        var outcome = WorkflowOutcomeName.Create("accepted");
        var services = EphemeralServices();
        services.AddSingleton(new CompletionBarrierStep(context.Services.Barrier));
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<IWorkflowEventClient>();

        var resultlessDefinition = Workflow.Ephemeral<ProjectionState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<ProjectionInput>(input => new ProjectionState(input.Value, [.. input.Items]))
            .End(outcome)
            .Build();
        var resultlessHandle = registry.Register(resultlessDefinition).GetHandleOrThrow();
        var resultless = (await resultlessHandle.StartOrGetAsync(
            new ProjectionInput(1, []),
            StartIdempotencyKey.Create("typed-resultless"))).GetHandleOrThrow();
        var reopenedResultless = await resultlessHandle.GetInstanceAsync(resultless.InstanceId);
        var resultlessSnapshot = await reopenedResultless.GetSnapshotAsync();
        if (resultlessSnapshot.Status != WorkflowInstanceStatus.Completed ||
            resultlessSnapshot.Outcome?.Value != outcome.Value)
        {
            throw new InvalidOperationException("Resultless typed completion lost its fixed outcome.");
        }

        var resultfulDefinition = Workflow.Ephemeral<ProjectionState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<ProjectionInput>(input => new ProjectionState(input.Value, [.. input.Items]))
            .Wait(eventName, _ => correlation)
            .Then<CompletionBarrierStep>()
            .End(snapshot => new ProjectionOutput(snapshot.Value.Value + 1), outcome)
            .Build();
        var resultfulHandle = registry.Register(resultfulDefinition).GetHandleOrThrow();
        var resultful = (await resultfulHandle.StartOrGetAsync(
            new ProjectionInput(41, []),
            StartIdempotencyKey.Create("typed-resultful"))).GetHandleOrThrow();
        var outputTask = context.ObserveAsync(_ => resultful.WaitForOutputAsync()).AsTask();
        var deliveryTask = events.DeliverToInstanceAsync(
            resultful.InstanceId,
            WorkflowEvent.Create(
                EventId.Create("typed-completion-event"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow)).AsTask();
        await context.WaitUntilBarrierReachedAsync("typed-completion");
        context.ReleaseBarrier("typed-completion");
        _ = await deliveryTask;
        var output = await outputTask;
        Phase0Assert.Satisfies(
            output,
            value => value.Value == 42,
            "Typed completion required a cast or lost its output.");

        var reopened = await resultfulHandle.GetInstanceAsync(resultful.InstanceId);
        var reopenedOutput = await reopened.GetOutputAsync();
        var snapshot = await reopened.GetSnapshotAsync();
        if (reopenedOutput is not WorkflowOutputResult<ProjectionOutput>.Available
            {
                Output.Value: 42
            } ||
            snapshot.Outcome?.Value != outcome.Value)
        {
            throw new InvalidOperationException("Typed reopen lost output or conflated it with outcome metadata.");
        }
    }

    [Phase0Scenario("projection-opacity", "3.5")]
    public static async Task ApplicationProjectionsExposeOnlyCommittedRootState(
        Phase0ScenarioContext context)
    {
        var services = EphemeralServices();
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = Workflow.Ephemeral<ProjectionState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<ProjectionInput>(input => new ProjectionState(input.Value, [.. input.Items]))
            .Parallel<string>(branches => branches
                .Branch<ProjectionBranchState>(
                    AuthoredBranchId.Create("projection-first"),
                    parent => new ProjectionBranchState(parent.Value.Value + 10),
                    branch => branch
                        .Wait(
                            EventName.Create("projection-first"),
                            _ => CorrelationId.Create("projection-first"))
                        .Return(state => state.Value.PrivateValue.ToString()))
                .Branch<ProjectionBranchState>(
                    AuthoredBranchId.Create("projection-second"),
                    parent => new ProjectionBranchState(parent.Value.Value + 20),
                    branch => branch
                        .Wait(
                            EventName.Create("projection-second"),
                            _ => CorrelationId.Create("projection-second"))
                        .Return(state => state.Value.PrivateValue.ToString())))
            .WhenAll((parent, _) => parent.Value)
            .End(snapshot => new ProjectionOutput(snapshot.Value.Value))
            .Build();
        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            new ProjectionInput(5, ["root"]),
            StartIdempotencyKey.Create("projection-opacity"))).GetHandleOrThrow();

        var snapshotObservation = await context.ObserveAsync(_ => instance.GetSnapshotAsync());
        Phase0Assert.Satisfies(
            snapshotObservation,
            snapshot => snapshot.Status == WorkflowInstanceStatus.Waiting &&
                        snapshot.ActiveWaits.Count == 2,
            "The application snapshot did not retain the two authored active waits.");

        var firstState = await instance.GetStateAsync<ProjectionState>();
        firstState.Value = 99;
        firstState.Items[0] = "mutated";
        var secondState = await instance.GetStateAsync<ProjectionState>();
        var output = await instance.GetOutputAsync();
        if (secondState.Value != 5 ||
            !secondState.Items.SequenceEqual(["root"]) ||
            output is not WorkflowOutputResult<ProjectionOutput>.Pending)
        {
            throw new InvalidOperationException(
                "Application state/output projections were mutable or exposed uncommitted branch state.");
        }

        var snapshotProperties = typeof(WorkflowInstanceSnapshot)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] exactSnapshotProperties =
        [
            "ActiveWaits", "CompletedAt", "DefinitionFingerprint", "DefinitionId",
            "DefinitionVersion", "Failure", "InstanceId", "Mode", "Outcome", "StartedAt", "Status"
        ];
        if (!snapshotProperties.SequenceEqual(exactSnapshotProperties.Order(StringComparer.Ordinal)) ||
            snapshotProperties.Any(name =>
                name.Contains("Fiber", StringComparison.Ordinal) ||
                name.Contains("Scope", StringComparison.Ordinal) ||
                name.Contains("Attempt", StringComparison.Ordinal) ||
                name.Contains("Plan", StringComparison.Ordinal) ||
                name.Contains("Deadline", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The application snapshot exposed advanced runtime coordinates.");
        }
    }

    private static ServiceCollection EphemeralServices()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services;
    }

    public sealed record ProjectionInput(int Value, List<string> Items);
    public sealed record ProjectionOutput(int Value);

    public sealed class ProjectionState
    {
        public ProjectionState(int value, List<string> items)
        {
            Value = value;
            Items = items;
        }

        public int Value { get; set; }
        public List<string> Items { get; set; }
    }

    public sealed class ProjectionBranchState
    {
        public ProjectionBranchState(int privateValue) => PrivateValue = privateValue;
        public int PrivateValue { get; set; }
    }

    public sealed class CompletionBarrierStep(
        IPhase0DeterministicBarrier barrier) : IStep<ProjectionState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<ProjectionState> context,
            CancellationToken cancellationToken)
        {
            await barrier.ReachAsync("typed-completion", cancellationToken);
            return new StepResult.Completed();
        }
    }

    private static bool IsRecord(this Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.NonPublic) is not null;
}
