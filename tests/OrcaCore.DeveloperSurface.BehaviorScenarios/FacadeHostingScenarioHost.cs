using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Providers;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Dag.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;
using OrcaCore.Providers.PostgreSql;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class FacadeHostingScenarioHost
{
    [Phase0Scenario("four-typed-registration-handles", "3.7")]
    public static void FourTypedRegistrationHandlesAreClosedAndCastFree(Phase0ScenarioContext context)
    {
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var ephemeralResultless = EphemeralResultless();
        var ephemeralResultful = EphemeralResultful();
        var durableResultless = DurableResultless();
        var durableResultful = DurableResultful();

        var first = context.Observe(_ => registry.Register(ephemeralResultless));
        Phase0Assert.Satisfies(
            first,
            result => result is WorkflowRegistrationResult<EphemeralDefinitionHandle<FacadeInput>>.Registered,
            "The resultless ephemeral definition did not return its exact registered handle family.");

        var second = context.Observe(_ => registry.Register(ephemeralResultful));
        Phase0Assert.Satisfies(
            second,
            result => result is WorkflowRegistrationResult<
                EphemeralDefinitionHandle<FacadeInput, FacadeOutput>>.Registered,
            "The resultful ephemeral definition did not return its exact registered handle family.");

        var third = context.Observe(_ => registry.Register(durableResultless));
        Phase0Assert.Satisfies(
            third,
            result => result is WorkflowRegistrationResult<DurableDefinitionHandle<FacadeInput>>.HostIncompatible
            {
                Error: DefinitionHostCompatibilityFailure.EngineModeMismatch
            },
            "A durable resultless definition was not rejected by the selected ephemeral role.");

        var fourth = context.Observe(_ => registry.Register(durableResultful));
        Phase0Assert.Satisfies(
            fourth,
            result => result is WorkflowRegistrationResult<
                DurableDefinitionHandle<FacadeInput, FacadeOutput>>.HostIncompatible
            {
                Error: DefinitionHostCompatibilityFailure.EngineModeMismatch
            },
            "A durable resultful definition was not rejected by the selected ephemeral role.");
    }

    [Phase0Scenario("compatibility-order-and-copy", "3.7")]
    public static void CompatibilityPrecedesFingerprintAndCopiesMissingNames(Phase0ScenarioContext context)
    {
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();

        var modeMismatch = context.Observe(_ => registry.Register(DurableResultless()));
        Phase0Assert.Satisfies(
            modeMismatch,
            result => result is WorkflowRegistrationResult<DurableDefinitionHandle<FacadeInput>>.HostIncompatible
            {
                Error: DefinitionHostCompatibilityFailure.EngineModeMismatch
            },
            "Engine-mode compatibility did not fail before pool or fingerprint checks.");

        var definitionId = DefinitionId.New();
        var poolA = TransientPoolName.Create("zeta");
        var poolB = TransientPoolName.Create("Alpha");
        var missingDefinition = Workflow.Ephemeral<FacadeState>(definitionId, DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .Then<ImmediateFacadeStep>()
            .WithTransientPool(poolA)
            .Then<ImmediateFacadeStep>()
            .WithTransientPool(poolB)
            .End()
            .Build();
        var missing = registry.Register(missingDefinition);
        if (missing is not WorkflowRegistrationResult<
                EphemeralDefinitionHandle<FacadeInput>>.HostIncompatible
            {
                Error: DefinitionHostCompatibilityFailure.MissingTransientPools pools
            } ||
            !pools.PoolNames.SequenceEqual([poolB, poolA]))
        {
            throw new InvalidOperationException(
                "Static transient-pool compatibility did not return copied distinct ordinal-sorted names.");
        }

        var callerNames = new List<TransientPoolName> { poolA, poolB, poolA };
        var copied = new DefinitionHostCompatibilityFailure.MissingTransientPools(callerNames);
        callerNames.Clear();
        if (!copied.PoolNames.SequenceEqual([poolB, poolA]))
        {
            throw new InvalidOperationException("Missing transient-pool names retained caller-owned storage.");
        }

        var clean = Workflow.Ephemeral<FacadeState>(definitionId, DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .End()
            .Build();
        var registered = registry.Register(clean).GetHandleOrThrow();
        var changed = Workflow.Ephemeral<FacadeState>(definitionId, DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .Delay(TimeSpan.FromMilliseconds(1))
            .End()
            .Build();
        var conflict = registry.Register(changed);
        var replay = registry.Register(clean).GetHandleOrThrow();
        if (conflict is not WorkflowRegistrationResult<
                EphemeralDefinitionHandle<FacadeInput>>.Conflict ||
            !ReferenceEquals(registered, replay))
        {
            throw new InvalidOperationException(
                "A structural fingerprint conflict mutated the existing registry binding.");
        }
    }

    [Phase0Scenario("get-handle-or-throw-parity", "3.7")]
    public static async Task GetHandleOrThrowPreservesClosedFailureValues(Phase0ScenarioContext context)
    {
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var success = registry.Register(EphemeralResultless());
        var expected = ((WorkflowRegistrationResult<
            EphemeralDefinitionHandle<FacadeInput>>.Registered)success).Handle;

        var handle = context.Observe(_ => success.GetHandleOrThrow());
        Phase0Assert.Satisfies(
            handle,
            value => ReferenceEquals(value, expected),
            "GetHandleOrThrow did not return the exact registered handle.");

        var incompatible = registry.Register(DurableResultless());
        try
        {
            _ = incompatible.GetHandleOrThrow();
            throw new InvalidOperationException("Host-incompatible registration did not throw.");
        }
        catch (WorkflowDefinitionHostCompatibilityException exception)
        {
            if (!ReferenceEquals(
                    exception.Failure,
                    ((WorkflowRegistrationResult<
                        DurableDefinitionHandle<FacadeInput>>.HostIncompatible)incompatible).Error))
            {
                throw new InvalidOperationException(
                    "The compatibility exception did not preserve the closed failure value.");
            }
        }

        var definitionId = DefinitionId.New();
        var original = Workflow.Ephemeral<FacadeState>(definitionId, DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .End()
            .Build();
        var changed = Workflow.Ephemeral<FacadeState>(definitionId, DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .Delay(TimeSpan.FromMilliseconds(1))
            .End()
            .Build();
        _ = registry.Register(original);
        var conflict = registry.Register(changed);
        try
        {
            _ = conflict.GetHandleOrThrow();
            throw new InvalidOperationException("Definition registration conflict did not throw.");
        }
        catch (WorkflowDefinitionRegistrationConflictException exception)
        {
            if (!ReferenceEquals(
                    exception.Conflict,
                    ((WorkflowRegistrationResult<
                        EphemeralDefinitionHandle<FacadeInput>>.Conflict)conflict).Error))
            {
                throw new InvalidOperationException(
                    "The definition conflict exception did not preserve the closed conflict value.");
            }
        }

        var startedHandle = success.GetHandleOrThrow();
        _ = await startedHandle.StartOrGetAsync(
            new FacadeInput(1),
            StartIdempotencyKey.Create("parity-start"));
        var startConflict = await startedHandle.StartOrGetAsync(
            new FacadeInput(2),
            StartIdempotencyKey.Create("parity-start"));
        try
        {
            _ = startConflict.GetHandleOrThrow();
            throw new InvalidOperationException("Start conflict did not throw.");
        }
        catch (WorkflowStartIdempotencyConflictException exception)
        {
            if (!ReferenceEquals(
                    exception.Conflict,
                    ((WorkflowStartResult<WorkflowInstanceHandle>.Conflict)startConflict).Error))
            {
                throw new InvalidOperationException(
                    "The start conflict exception did not preserve the closed conflict value.");
            }
        }
    }

    [Phase0Scenario("typed-instance-output-wait", "3.7")]
    public static async Task TypedOutputWaitIsNotificationDrivenAndLocallyCancellable(
        Phase0ScenarioContext context)
    {
        var eventName = EventName.Create("complete");
        var correlation = CorrelationId.Create("typed-output");
        var services = EphemeralServices();
        services.AddSingleton(new BarrierFacadeStep(context.Services.Barrier));
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var definition = Workflow.Ephemeral<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .Then<BarrierFacadeStep>()
            .End(snapshot => new FacadeOutput(snapshot.Value.Value + 1))
            .Build();
        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var start = await definitionHandle.StartOrGetAsync(
            new FacadeInput(41),
            StartIdempotencyKey.Create("typed-output-wait"));
        var instance = start.GetHandleOrThrow();

        using (var localCancellation = new CancellationTokenSource())
        {
            var cancelledWait = instance.WaitForOutputAsync(localCancellation.Token).AsTask();
            localCancellation.Cancel();
            try
            {
                _ = await cancelledWait;
                throw new InvalidOperationException("The local output wait ignored cancellation.");
            }
            catch (OperationCanceledException)
            {
                var snapshot = await instance.GetSnapshotAsync();
                if (snapshot.Status != WorkflowInstanceStatus.Waiting)
                {
                    throw new InvalidOperationException(
                        "Cancelling the local output wait cancelled or progressed the workflow.");
                }
            }
        }

        var observedWait = context.ObserveAsync(_ => instance.WaitForOutputAsync()).AsTask();
        var delivery = events.DeliverToInstanceAsync(
            instance.InstanceId,
            WorkflowEvent.Create(
                EventId.Create("typed-output-event"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow)).AsTask();
        await context.WaitUntilBarrierReachedAsync("typed-output");
        if (observedWait.IsCompleted)
        {
            throw new InvalidOperationException("WaitForOutputAsync completed before terminal output committed.");
        }

        context.ReleaseBarrier("typed-output");
        var delivered = await delivery;
        if (delivered.Status != EventDeliveryStatus.Accepted)
        {
            throw new InvalidOperationException("The event did not resume the typed output workflow.");
        }

        var output = await observedWait;
        Phase0Assert.Equal(
            new FacadeOutput(42),
            output,
            "WaitForOutputAsync did not return the detached committed output.");

        var resultless = registry.Register(EphemeralResultless()).GetHandleOrThrow();
        var terminal = (await resultless.StartOrGetAsync(
            new FacadeInput(1),
            StartIdempotencyKey.Create("resultless-output"))).GetHandleOrThrow();
        if (terminal.GetType().GetMethod(nameof(WorkflowInstanceHandle<FacadeOutput>.WaitForOutputAsync)) is not null)
        {
            throw new InvalidOperationException("A resultless workflow handle exposed output waiting.");
        }
    }

    [Phase0Scenario("reduced-snapshot-management", "3.7")]
    public static async Task SnapshotAndManagementSurfaceRemainApplicationOnly(Phase0ScenarioContext context)
    {
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var handle = registry.Register(EphemeralResultless()).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            new FacadeInput(7),
            StartIdempotencyKey.Create("reduced-snapshot"))).GetHandleOrThrow();

        var snapshot = await context.ObserveAsync(_ => instance.GetSnapshotAsync());
        Phase0Assert.Satisfies(
            snapshot,
            value => value.Status == WorkflowInstanceStatus.Completed &&
                     value.ActiveWaits.Count == 0 &&
                     value.CompletedAt.HasValue,
            "The reduced application snapshot did not report committed terminal facts.");

        var snapshotProperties = typeof(WorkflowInstanceSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = new[]
        {
            "ActiveWaits", "CompletedAt", "DefinitionFingerprint", "DefinitionId",
            "DefinitionVersion", "Failure", "InstanceId", "Mode", "Outcome", "StartedAt", "Status"
        };
        if (!snapshotProperties.SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal)) ||
            typeof(WorkflowInstanceHandle).GetMethods().Any(method =>
                method.Name.Contains("List", StringComparison.Ordinal) ||
                method.Name.Contains("Query", StringComparison.Ordinal) ||
                method.Name.Contains("Statistics", StringComparison.Ordinal) ||
                method.Name.Contains("Retry", StringComparison.Ordinal) ||
                method.Name.Contains("Pause", StringComparison.Ordinal) ||
                method.Name.Contains("Resume", StringComparison.Ordinal) ||
                method.Name.Contains("Archive", StringComparison.Ordinal) ||
                method.Name.Contains("Purge", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The application snapshot/handle leaked advanced or deferred management facts.");
        }
    }

    [Phase0Scenario("two-event-routes-four-overloads", "3.7")]
    public static async Task EventClientHasExactlyTwoRoutesAndFourOverloads(Phase0ScenarioContext context)
    {
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var client = provider.GetRequiredService<IWorkflowEventClient>();
        var eventName = EventName.Create("route");

        var first = await StartWaitingAsync(registry, "instance-empty", eventName);
        var instancePayloadless = await context.ObserveAsync(_ => client.DeliverToInstanceAsync(
            first.InstanceId,
            Event("route-instance-empty", eventName, first.Correlation)));
        Phase0Assert.Satisfies(
            instancePayloadless,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == first.InstanceId,
            "Payloadless instance delivery did not select the exact waiting instance.");

        var second = await StartWaitingAsync(registry, "instance-payload", eventName);
        var instancePayload = await context.ObserveAsync(_ => client.DeliverToInstanceAsync(
            second.InstanceId,
            PayloadEvent("route-instance-payload", eventName, second.Correlation, 2)));
        Phase0Assert.Satisfies(
            instancePayload,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == second.InstanceId,
            "Payload instance delivery did not select the exact waiting instance.");

        var third = await StartWaitingAsync(registry, "correlation-empty", eventName);
        var correlationPayloadless = await context.ObserveAsync(_ => client.DeliverByCorrelationAsync(
            third.DefinitionId,
            Event("route-correlation-empty", eventName, third.Correlation)));
        Phase0Assert.Satisfies(
            correlationPayloadless,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == third.InstanceId,
            "Payloadless correlation delivery did not select the one active pair.");

        var fourth = await StartWaitingAsync(registry, "correlation-payload", eventName);
        var correlationPayload = await context.ObserveAsync(_ => client.DeliverByCorrelationAsync(
            fourth.DefinitionId,
            PayloadEvent("route-correlation-payload", eventName, fourth.Correlation, 4)));
        Phase0Assert.Satisfies(
            correlationPayload,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == fourth.InstanceId,
            "Payload correlation delivery did not select the one active pair.");

        var methods = typeof(IWorkflowEventClient).GetMethods();
        if (methods.Length != 4 ||
            methods.Select(method => method.Name).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(["DeliverByCorrelationAsync", "DeliverToInstanceAsync"]) is false ||
            !Enum.GetNames<EventDeliveryStatus>().SequenceEqual(
                ["Accepted", "Duplicate", "NoActiveWait", "InstanceTerminal", "EventConflict"]))
        {
            throw new InvalidOperationException(
                "IWorkflowEventClient did not expose exactly two route names, four overloads, and five statuses.");
        }
    }

    [Phase0Scenario("six-hosting-entry-owners", "3.7")]
    public static void SixHostingEntriesHaveOneExactAssemblyOwner(Phase0ScenarioContext context)
    {
        var ephemeral = new ServiceCollection();
        var ephemeralRegistration = context.Observe(_ =>
            ephemeral.AddOrcaCoreEphemeralEngine(EphemeralOptions()));
        Phase0Assert.Satisfies(
            ephemeralRegistration,
            builder => builder is not null,
            "Ephemeral engine registration did not return its composition builder.");

        var durable = new ServiceCollection();
        durable.AddOrcaCoreInMemoryDurableProvider();
        var durableRegistration = context.Observe(_ =>
            durable.AddOrcaCoreDurableEngine(DurableOptions()));
        Phase0Assert.Satisfies(
            durableRegistration,
            builder => builder is not null,
            "Durable engine registration did not return its composition builder.");

        var dagRegistration = context.Observe(_ =>
            durable.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 2 }));
        Phase0Assert.Satisfies(
            dagRegistration,
            services => ReferenceEquals(services, durable),
            "DAG registration did not return the supplied service collection.");

        var exact = new (Type Type, string Member, string Assembly)[]
        {
            (typeof(OrcaCoreEphemeralEngineServiceCollectionExtensions),
                "AddOrcaCoreEphemeralEngine", "OrcaCore.Engine.Ephemeral"),
            (typeof(OrcaCoreDurableEngineServiceCollectionExtensions),
                "AddOrcaCoreDurableEngine", "OrcaCore.Durable.Hosting"),
            (typeof(OrcaCoreDurableEngineServiceCollectionExtensions),
                "AddOrcaCoreDurableEventIngress", "OrcaCore.Durable.Hosting"),
            (typeof(OrcaCoreInMemoryProviderServiceCollectionExtensions),
                "AddOrcaCoreInMemoryDurableProvider", "OrcaCore.Providers.InMemory"),
            (typeof(OrcaCorePostgreSqlProviderServiceCollectionExtensions),
                "AddOrcaCorePostgreSqlDurableProvider", "OrcaCore.Providers.PostgreSql"),
            (typeof(OrcaCoreDagHostingServiceCollectionExtensions),
                "AddOrcaCoreDag", "OrcaCore.Dag.Hosting")
        };
        foreach (var entry in exact)
        {
            if (entry.Type.Assembly.GetName().Name != entry.Assembly ||
                entry.Type.GetMethods().Count(method => method.Name == entry.Member) != 1 ||
                entry.Type.IsAbstract is false ||
                entry.Type.IsSealed is false)
            {
                throw new InvalidOperationException(
                    $"Hosting entry '{entry.Member}' did not have one exact static owner in '{entry.Assembly}'.");
            }
        }
    }

    [Phase0Scenario("role-exclusivity-and-dependencies", "3.7")]
    public static void HostRolesRejectConflictsBeforePartialRegistration(Phase0ScenarioContext context)
    {
        var durable = new ServiceCollection();
        durable.AddOrcaCoreInMemoryDurableProvider();
        var registered = context.Observe(_ =>
            durable.AddOrcaCoreDurableEngine(DurableOptions()));
        Phase0Assert.Satisfies(
            registered,
            builder => builder is not null,
            "The durable engine role was not registered with one complete provider.");

        var conflicting = context.ObserveThrows<InvalidOperationException, IServiceCollection>(_ =>
            durable.AddOrcaCoreDurableEventIngress());
        Phase0Assert.Satisfies(
            conflicting,
            exception => exception.Message.Contains("cannot be combined", StringComparison.Ordinal),
            "Engine and callback-only ingress roles were silently combined.");

        var ingress = new ServiceCollection();
        ingress.AddOrcaCoreInMemoryDurableProvider();
        ingress.AddOrcaCoreDurableEventIngress();
        using (var ingressProvider = ingress.BuildServiceProvider())
        {
            if (ingressProvider.GetService<IWorkflowEventClient>() is null ||
                ingressProvider.GetService<IWorkflowDefinitionRegistry>() is not null ||
                ingressProvider.GetServices<IHostedService>().Any())
            {
                throw new InvalidOperationException(
                    "Callback-only ingress registered a definition registry or progression worker.");
            }
        }

        try
        {
            _ = new ServiceCollection().AddOrcaCoreDurableEngine(DurableOptions());
            throw new InvalidOperationException("Durable engine registration accepted a missing provider role.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("provider role", StringComparison.Ordinal))
        {
        }

        foreach (var registerProvider in new Action<IServiceCollection>[]
                 {
                     services => services.AddOrcaCoreInMemoryDurableProvider(),
                     services => services.AddOrcaCorePostgreSqlDurableProvider(
                         new PostgreSqlDurableProviderOptions(
                             "Host=localhost;Database=orca;Username=orca;Password=orca",
                             "orca_v1"))
                 })
        {
            using var partialPort = new DurableScenarioProvider();
            var partial = new ServiceCollection();
            partial.AddSingleton<IWorkflowEventStore>(partialPort);
            try
            {
                registerProvider(partial);
                throw new InvalidOperationException(
                    "A complete provider role silently retained a pre-registered partial provider port.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("pre-registered", StringComparison.Ordinal))
            {
            }
        }

        var mixed = new ServiceCollection();
        mixed.AddOrcaCoreEphemeralEngine(EphemeralOptions());
        mixed.AddOrcaCoreInMemoryDurableProvider();
        try
        {
            mixed.AddOrcaCoreDurableEngine(DurableOptions());
            throw new InvalidOperationException("Ephemeral and durable engine roles were combined.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("cannot be combined", StringComparison.Ordinal))
        {
        }

        try
        {
            _ = new ServiceCollection().AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 1 });
            throw new InvalidOperationException("DAG registration accepted a missing durable engine role.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("requires", StringComparison.Ordinal))
        {
        }
    }

    [Phase0Scenario("programmatic-options-copy-validation", "3.7")]
    public static void ProgrammaticProviderOptionsAreValidatedAndConflictSafe(Phase0ScenarioContext context)
    {
        var services = new ServiceCollection();
        var options = new PostgreSqlDurableProviderOptions(
            "Host=localhost;Database=orca;Username=orca;Password=orca",
            "orca_v1");
        var registered = context.Observe(_ =>
            services.AddOrcaCorePostgreSqlDurableProvider(options));
        Phase0Assert.Satisfies(
            registered,
            value => ReferenceEquals(value, services),
            "PostgreSQL provider registration did not return the supplied service collection.");

        var count = services.Count;
        services.AddOrcaCorePostgreSqlDurableProvider(
            new PostgreSqlDurableProviderOptions(options.ConnectionString, options.Schema));
        if (services.Count != count)
        {
            throw new InvalidOperationException("Identical provider registration was not idempotent.");
        }

        try
        {
            services.AddOrcaCorePostgreSqlDurableProvider(
                new PostgreSqlDurableProviderOptions(options.ConnectionString, "other"));
            throw new InvalidOperationException("Conflicting provider options were silently accepted.");
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("different options", StringComparison.Ordinal))
        {
        }

        foreach (var invalid in new Action[]
        {
            () => _ = new PostgreSqlDurableProviderOptions("", "schema"),
            () => _ = new PostgreSqlDurableProviderOptions(options.ConnectionString, " ")
        })
        {
            try
            {
                invalid();
                throw new InvalidOperationException("Invalid PostgreSQL options were accepted.");
            }
            catch (ArgumentException)
            {
            }
        }

        var publicMembers = typeof(PostgreSqlDurableProviderOptions).GetProperties();
        if (publicMembers.Any(property => property.SetMethod is not null) ||
            typeof(PostgreSqlDurableProviderOptions).GetMethods().Any(method =>
                method.Name.Contains("Bind", StringComparison.Ordinal) ||
                method.Name.Contains("Serializer", StringComparison.Ordinal) ||
                method.Name.Contains("Codec", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "PostgreSQL options exposed binder, mutable, or codec replacement surface.");
        }
    }

    [Phase0Scenario("exact-type-throttle", "3.7")]
    public static void StepThrottleBindsOnlyTheExactNamedStepType(Phase0ScenarioContext context)
    {
        var throttle = context.Observe(_ => StepExecutionThrottle.For<DerivedFacadeStep>(1));
        Phase0Assert.Satisfies(
            throttle,
            value => value.StepType == typeof(DerivedFacadeStep) &&
                     value.StepType != typeof(BaseFacadeStep) &&
                     value.MaxConcurrency == 1,
            "StepExecutionThrottle did not retain the exact named step type.");

        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles =
                [
                    StepExecutionThrottle.For<BaseFacadeStep>(1),
                    StepExecutionThrottle.For<DerivedFacadeStep>(2)
                ]
            },
            TransientPools = []
        });
        try
        {
            new ServiceCollection().AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 1,
                    StepThrottles =
                    [
                        StepExecutionThrottle.For<DerivedFacadeStep>(1),
                        StepExecutionThrottle.For<DerivedFacadeStep>(2)
                    ]
                },
                TransientPools = []
            });
            throw new InvalidOperationException("Duplicate exact step throttles were accepted.");
        }
        catch (ArgumentException exception)
            when (exception.Message.Contains("duplicate exact step type", StringComparison.Ordinal))
        {
        }
    }

    [Phase0Scenario("transient-decorator-binding", "3.7")]
    public static void TransientPoolDecoratorBindsOnlyItsImmediateBusinessStep(Phase0ScenarioContext context)
    {
        var poolName = TransientPoolName.Create("external-api");
        var pool = context.Observe(_ => TransientPoolDefinition.Create(poolName, 2));
        Phase0Assert.Satisfies(
            pool,
            value => value.Name == poolName && value.Capacity == 2,
            "TransientPoolDefinition did not retain its exact name and capacity.");

        _ = Workflow.Ephemeral<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .Then<ImmediateFacadeStep>()
            .WithTransientPool(poolName)
            .End()
            .Build();

        try
        {
            _ = Workflow.Ephemeral<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<FacadeInput>(input => new FacadeState(input.Value))
                .Then<ImmediateFacadeStep>()
                .WithTransientPool(poolName)
                .WithTransientPool(poolName);
            throw new InvalidOperationException("A duplicate transient-pool decorator was accepted.");
        }
        catch (WorkflowDefinitionException exception)
            when (exception.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "SFE-AUTH-DECORATOR-001"))
        {
        }

        try
        {
            _ = Workflow.Ephemeral<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<FacadeInput>(input => new FacadeState(input.Value))
                .Wait(
                    WorkflowEventContract.Create(EventName.Create("not-a-step"), EventContractVersion.Initial),
                    _ => CorrelationId.Create("not-a-step"))
                .WithTransientPool(poolName);
            throw new InvalidOperationException("A transient pool decorated a structural wait.");
        }
        catch (WorkflowDefinitionException exception)
            when (exception.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "SFE-AUTH-DECORATOR-001"))
        {
        }
    }

    private static ServiceCollection EphemeralServices()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(EphemeralOptions());
        return services;
    }

    private static EphemeralEngineHostOptions EphemeralOptions() =>
        new()
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            TransientPools = []
        };

    private static DurableEngineHostOptions DurableOptions() =>
        new()
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create("facade"),
                Pools = []
            }
        };

    private static EphemeralWorkflowDefinition<FacadeInput> EphemeralResultless() =>
        Workflow.Ephemeral<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<FacadeInput, FacadeOutput> EphemeralResultful() =>
        Workflow.Ephemeral<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .End(snapshot => new FacadeOutput(snapshot.Value.Value))
            .Build();

    private static DurableWorkflowDefinition<FacadeInput> DurableResultless() =>
        Workflow.Durable<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .End()
            .Build();

    private static DurableWorkflowDefinition<FacadeInput, FacadeOutput> DurableResultful() =>
        Workflow.Durable<FacadeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .End(snapshot => new FacadeOutput(snapshot.Value.Value))
            .Build();

    private static async Task<WaitingInstance> StartWaitingAsync(
        IWorkflowDefinitionRegistry registry,
        string suffix,
        EventName eventName)
    {
        var definitionId = DefinitionId.New();
        var correlation = CorrelationId.Create($"route-{suffix}");
        var definition = Workflow.Ephemeral<FacadeState>(definitionId, DefinitionVersion.Initial)
            .Init<FacadeInput>(input => new FacadeState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .End()
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            new FacadeInput(1),
            StartIdempotencyKey.Create($"route-{suffix}"))).GetHandleOrThrow();
        return new WaitingInstance(definitionId, instance.InstanceId, correlation);
    }

    private static WorkflowEvent Event(
        string id,
        EventName eventName,
        CorrelationId correlation) =>
        WorkflowEvent.Create(
            EventId.Create(id),
            eventName,
            correlation,
            DateTimeOffset.UtcNow);

    private static WorkflowEvent<FacadeEventPayload> PayloadEvent(
        string id,
        EventName eventName,
        CorrelationId correlation,
        int value) =>
        WorkflowEvent<FacadeEventPayload>.Create(
            EventId.Create(id),
            eventName,
            correlation,
            new FacadeEventPayload(value),
            DateTimeOffset.UtcNow);

    private sealed record WaitingInstance(
        DefinitionId DefinitionId,
        InstanceId InstanceId,
        CorrelationId Correlation);

    public sealed record FacadeInput(int Value);
    public sealed record FacadeOutput(int Value);
    public sealed record FacadeEventPayload(int Value);

    public sealed class FacadeState
    {
        public FacadeState(int value) => Value = value;
        public int Value { get; set; }
    }

    public sealed class ImmediateFacadeStep : IStep<FacadeState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<FacadeState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    public sealed class BarrierFacadeStep(IPhase0DeterministicBarrier barrier) : IStep<FacadeState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<FacadeState> context,
            CancellationToken cancellationToken)
        {
            await barrier.ReachAsync("typed-output", cancellationToken);
            return new StepResult.Completed();
        }
    }

    public class BaseFacadeStep : IStep<FacadeState>
    {
        public virtual ValueTask<StepResult> ExecuteAsync(
            StepContext<FacadeState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    public sealed class DerivedFacadeStep : BaseFacadeStep;
}
