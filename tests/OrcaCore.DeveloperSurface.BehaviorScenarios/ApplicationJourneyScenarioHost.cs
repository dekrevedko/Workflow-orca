using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Durable.Hosting;
using OrcaCore.Hosting;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class ApplicationJourneyScenarioHost
{
    [Phase0Scenario("four-event-overloads", "3.8")]
    public static async Task AllFourEventRoutesReturnClosedStatuses(Phase0ScenarioContext context)
    {
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();
        var instancePayloadless = await StartWaitingAsync(registry, "instance-plain");
        var instancePayload = await StartWaitingAsync(registry, "instance-payload");
        var correlationPayloadless = await StartWaitingAsync(registry, "correlation-plain");
        var correlationPayload = await StartWaitingAsync(registry, "correlation-payload");

        var firstEventObservation = context.Observe(_ => WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(
                instancePayloadless.EventName,
                EventContractVersion.Initial),
            EventId.Create("route-instance-plain"),
            instancePayloadless.Correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(instancePayloadless.Instance.InstanceId)));
        Phase0Assert.Satisfies(
            firstEventObservation,
            inbound => inbound.Route is WorkflowEventRoute.Direct direct &&
                       direct.InstanceId == instancePayloadless.Instance.InstanceId,
            "The payloadless inbound envelope did not retain its exact direct route.");
        var firstEvent = WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(
                instancePayloadless.EventName,
                EventContractVersion.Initial),
            EventId.Create("route-instance-plain"),
            instancePayloadless.Correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(instancePayloadless.Instance.InstanceId));
        var first = await events.RouteAsync(firstEvent);
        if (first.Status != ProcessLocalEventRouteStatus.Accepted ||
            first.InstanceId != instancePayloadless.Instance.InstanceId)
        {
            throw new InvalidOperationException(
                "The payloadless instance route did not return its exact closed delivery result.");
        }

        var callerPayload = new JourneyPayload { Value = 2 };
        var detachedPayloadObservation = context.Observe(_ => WorkflowInboundEvent<JourneyPayload>.Create(
            WorkflowEventContract<JourneyPayload>.Create(
                instancePayload.EventName,
                EventContractVersion.Initial),
            EventId.Create("route-instance-payload"),
            instancePayload.Correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(instancePayload.Instance.InstanceId),
            callerPayload));
        Phase0Assert.Satisfies(
            detachedPayloadObservation,
            inbound => inbound.Payload.Value == 2 && !ReferenceEquals(inbound.Payload, callerPayload),
            "The typed inbound envelope retained caller-owned payload state.");
        var detachedPayloadEvent = WorkflowInboundEvent<JourneyPayload>.Create(
            WorkflowEventContract<JourneyPayload>.Create(
                instancePayload.EventName,
                EventContractVersion.Initial),
            EventId.Create("route-instance-payload"),
            instancePayload.Correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(instancePayload.Instance.InstanceId),
            callerPayload);
        callerPayload.Value = 99;
        if (detachedPayloadEvent.Payload.Value != 2 ||
            ReferenceEquals(detachedPayloadEvent.Payload, callerPayload))
        {
            throw new InvalidOperationException("ProcessLocalInboundEvent<TPayload> retained caller-owned payload state.");
        }

        var second = await events.RouteAsync(detachedPayloadEvent);
        if (second.Status != ProcessLocalEventRouteStatus.Accepted ||
            second.InstanceId != instancePayload.Instance.InstanceId)
        {
            throw new InvalidOperationException(
                "The payload instance route did not return its exact closed delivery result.");
        }

        var third = await events.RouteByCorrelationAsync(
            correlationPayloadless.DefinitionId,
            Event("route-correlation-plain", correlationPayloadless));
        if (third.Status != ProcessLocalEventRouteStatus.Accepted ||
            third.InstanceId != correlationPayloadless.Instance.InstanceId)
        {
            throw new InvalidOperationException(
                "The payloadless correlation route did not return its exact closed delivery result.");
        }

        var fourth = await events.RouteByCorrelationAsync(
            correlationPayload.DefinitionId,
            PayloadEvent("route-correlation-payload", correlationPayload, 4));
        if (fourth.Status != ProcessLocalEventRouteStatus.Accepted ||
            fourth.InstanceId != correlationPayload.Instance.InstanceId)
        {
            throw new InvalidOperationException(
                "The payload correlation route did not return its exact closed delivery result.");
        }
    }

    [Phase0Scenario("dedup-conflict-redelivery", "3.8")]
    public static async Task AcceptedReplayConflictsAndNonConsumingRedeliveryAreDistinct(
        Phase0ScenarioContext context)
    {
        var unlockName = EventName.Create("unlock-dedup");
        var unlockCorrelation = CorrelationId.Create("unlock-dedup");
        var targetName = EventName.Create("target-dedup");
        var targetCorrelation = CorrelationId.Create("target-dedup");
        var services = EphemeralServices();
        services.AddSingleton(new NamedBarrierStep(context.Services.Barrier, "dedup"));
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();
        var definition = Workflow.Ephemeral<JourneyState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(unlockName, EventContractVersion.Initial), _ => unlockCorrelation)
            .Wait(WorkflowEventContract.Create(targetName, EventContractVersion.Initial), _ => targetCorrelation)
            .Then<NamedBarrierStep>()
            .End()
            .Build();
        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("dedup-start"))).GetHandleOrThrow();
        var targetObservation = context.Observe(_ => WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(targetName, EventContractVersion.Initial),
            EventId.Create("dedup-target"),
            targetCorrelation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(instance.InstanceId)));
        Phase0Assert.Satisfies(
            targetObservation,
            inbound => inbound.EventId.Value == "dedup-target" &&
                       inbound.Route is WorkflowEventRoute.Direct,
            "The dedup target envelope did not preserve its identity and direct route.");
        var target = WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(targetName, EventContractVersion.Initial),
            EventId.Create("dedup-target"),
            targetCorrelation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(instance.InstanceId));

        var beforeWait = await events.RouteAsync(target);
        if (beforeWait.Status != ProcessLocalEventRouteStatus.NoActiveWait)
        {
            throw new InvalidOperationException(
                "A pre-wait event was consumed instead of returning NoActiveWait.");
        }

        var unlocked = await events.RouteToInstanceAsync(
            instance.InstanceId,
            ProcessLocalInboundEvent.Create(
                EventId.Create("dedup-unlock"),
                unlockName,
                unlockCorrelation,
                DateTimeOffset.UtcNow));
        if (unlocked.Status != ProcessLocalEventRouteStatus.Accepted)
        {
            throw new InvalidOperationException("The workflow did not progress to its target wait.");
        }

        var acceptedTask = events.RouteAsync(target).AsTask();
        await context.WaitUntilBarrierReachedAsync("dedup");
        context.ReleaseBarrier("dedup");
        var accepted = await acceptedTask;
        if (accepted.Status != ProcessLocalEventRouteStatus.Accepted)
        {
            throw new InvalidOperationException(
                "The same event envelope was not accepted after the matching wait became active.");
        }

        var duplicate = await events.RouteAsync(target);
        if (duplicate.Status != ProcessLocalEventRouteStatus.Duplicate)
        {
            throw new InvalidOperationException("An identical accepted replay was not classified as Duplicate.");
        }

        var conflict = await events.RouteAsync(
            WorkflowInboundEvent.Create(
                WorkflowEventContract.Create(
                    EventName.Create("changed-dedup"),
                    EventContractVersion.Initial),
                target.EventId,
                targetCorrelation,
                causationEventId: null,
                target.OccurredAt,
                new WorkflowEventRoute.Direct(instance.InstanceId)));
        if (conflict.Status != ProcessLocalEventRouteStatus.EventConflict)
        {
            throw new InvalidOperationException("A changed accepted envelope was not classified as EventConflict.");
        }
    }

    [Phase0Scenario("ambiguous-pair-rejection", "3.8")]
    public static async Task AmbiguousWaitsAreRejectedBeforeTheyBecomeRoutable(
        Phase0ScenarioContext context)
    {
        var eventName = EventName.Create("ambiguous");
        var correlation = CorrelationId.Create("ambiguous");
        var definitionId = DefinitionId.New();
        var services = EphemeralServices();
        services.AddSingleton(new NamedBarrierStep(context.Services.Barrier, "ambiguity"));
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();
        var ambiguous = Workflow.Ephemeral<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Parallel<string>(branches => branches
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("first"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
                        .Return(state => state.Value.Value.ToString()))
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("second"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
                        .Return(state => state.Value.Value.ToString())))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        var ambiguousHandle = registry.Register(ambiguous).GetHandleOrThrow();
        try
        {
            _ = await ambiguousHandle.StartOrGetAsync(
                new JourneyInput(1),
                StartIdempotencyKey.Create("ambiguous-within"));
            throw new InvalidOperationException("Two active waits inside one instance were admitted.");
        }
        catch (AmbiguousWaitRegistrationException exception)
            when (exception.DefinitionId.Value == definitionId.Value &&
                exception.EventContract.EventName.Value == eventName.Value &&
                  exception.CorrelationId.Value == correlation.Value)
        {
        }

        var noTargetObservation = context.Observe(_ => WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            EventId.Create("ambiguous-no-target"),
            correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Correlation(definitionId)));
        Phase0Assert.Satisfies(
            noTargetObservation,
            inbound => inbound.Route is WorkflowEventRoute.Correlation route &&
                       route.DefinitionId == definitionId,
            "The ambiguous-pair probe did not retain its exact correlation route.");
        var noTargetEvent = WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            EventId.Create("ambiguous-no-target"),
            correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Correlation(definitionId));
        var noTarget = await events.RouteAsync(noTargetEvent);
        if (noTarget.Status != ProcessLocalEventRouteStatus.NoActiveWait || noTarget.InstanceId is not null)
        {
            throw new InvalidOperationException(
                "A rejected ambiguous candidate remained available as an event target.");
        }

        var crossDefinitionId = DefinitionId.New();
        var cross = Workflow.Ephemeral<JourneyState>(crossDefinitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .End()
            .Build();
        var crossHandle = registry.Register(cross).GetHandleOrThrow();
        var first = (await crossHandle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("ambiguous-cross-first"))).GetHandleOrThrow();
        try
        {
            _ = await crossHandle.StartOrGetAsync(
                new JourneyInput(2),
                StartIdempotencyKey.Create("ambiguous-cross-second"));
            throw new InvalidOperationException("Two instances with the same active wait pair were admitted.");
        }
        catch (AmbiguousWaitRegistrationException)
        {
        }
        _ = await first.TerminateAsync();

        var barrierDefinition = Workflow.Ephemeral<JourneyState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Then<NamedBarrierStep>()
            .End()
            .Build();
        var barrierHandle = registry.Register(barrierDefinition).GetHandleOrThrow();
        var barrierStart = barrierHandle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("ambiguous-barrier")).AsTask();
        await context.WaitUntilBarrierReachedAsync("ambiguity");
        context.ReleaseBarrier("ambiguity");
        _ = await barrierStart;
    }

    [Phase0Scenario("signal-stream-reuse", "3.8")]
    public static async Task ASignalPairCanBeReusedByALaterOccurrence(Phase0ScenarioContext context)
    {
        var eventName = EventName.Create("reusable-signal");
        var correlation = CorrelationId.Create("reusable-signal");
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();
        var definitionId = DefinitionId.New();
        var definition = Workflow.Ephemeral<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .End(snapshot => snapshot.Value.Value)
            .Build();
        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var firstInstance = (await definitionHandle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("signal-reuse-first"))).GetHandleOrThrow();

        var first = await events.RouteByCorrelationAsync(
            definitionId,
            ProcessLocalInboundEvent.Create(
                EventId.Create("signal-reuse-first"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow));
        if (first.Status != ProcessLocalEventRouteStatus.Accepted)
        {
            throw new InvalidOperationException("The first signal occurrence was not consumed.");
        }
        _ = await firstInstance.WaitForOutputAsync();

        var secondInstance = (await definitionHandle.StartOrGetAsync(
            new JourneyInput(2),
            StartIdempotencyKey.Create("signal-reuse-second"))).GetHandleOrThrow();
        var secondEventObservation = context.Observe(_ => WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            EventId.Create("signal-reuse-second"),
            correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Correlation(definitionId)));
        Phase0Assert.Satisfies(
            secondEventObservation,
            inbound => inbound.EventId.Value == "signal-reuse-second" &&
                       inbound.Route is WorkflowEventRoute.Correlation,
            "The later signal envelope did not retain its identity and correlation route.");
        var secondEvent = WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            EventId.Create("signal-reuse-second"),
            correlation,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Correlation(definitionId));
        var second = await events.RouteAsync(secondEvent);
        if (second.Status != ProcessLocalEventRouteStatus.Accepted ||
            second.InstanceId != secondInstance.InstanceId)
        {
            throw new InvalidOperationException(
                "A later workflow occurrence did not consume a later event with the same authored signal pair.");
        }

        var output = await secondInstance.WaitForOutputAsync();
        if (output != 2)
        {
            throw new InvalidOperationException("The later workflow occurrence did not complete with its own state.");
        }
    }

    [Phase0Scenario("definitionless-continuation-handoff", "3.8")]
    public static async Task DefinitionlessIngressHandsOffToExactlyOneDefinitionOwner(
        Phase0ScenarioContext context)
    {
        var eventName = EventName.Create("handoff");
        var correlation = CorrelationId.Create("handoff");
        var definitionId = DefinitionId.New();
        var executionCount = new HandoffExecutionCount();
        var step = new HandoffBarrierStep(
            context.Services.Barrier,
            executionCount);
        var definition = Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .Then<HandoffBarrierStep>()
            .End()
            .Build();
        using var sharedProvider = new DurableScenarioProvider();
        InstanceId instanceId;

        using (var originalOwner = DurableServices(
                   sharedProvider,
                   step).BuildServiceProvider())
        {
            var registry = originalOwner.GetRequiredService<IWorkflowDefinitionRegistry>();
            var handle = registry.Register(definition).GetHandleOrThrow();
            var started = await handle.StartOrGetAsync(
                new JourneyInput(1),
                StartIdempotencyKey.Create("handoff-start"));
            var instance = started.GetHandleOrThrow();
            instanceId = instance.InstanceId;
            var parked = await instance.GetSnapshotAsync();
            if (parked.Status != WorkflowInstanceStatus.Waiting)
            {
                throw new InvalidOperationException("The definition owner did not park on the authored wait.");
            }
        }

        using (var ingressOwner = DurableIngressServices(
                   sharedProvider).BuildServiceProvider())
        {
            var ingress = ingressOwner.GetRequiredService<IWorkflowEventIngress>();
            var accepted = await context.ObserveAsync(_ => ingress.AcceptAsync(
                WorkflowInboundEvent.Create(
                    WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
                    EventId.Create("handoff-event"),
                    correlation,
                    causationEventId: null,
                    DateTimeOffset.UtcNow,
                    new WorkflowEventRoute.Correlation(definitionId))));
            Phase0Assert.Satisfies(
                accepted,
                result => result is WorkflowEventAcceptanceResult.Accepted,
                "Definition-less ingress did not commit the inbox and continuation handoff.");
        }

        using var firstOwner = DurableServices(
                sharedProvider,
                step)
            .BuildServiceProvider();
        using var secondOwner = DurableServices(
                sharedProvider,
                step)
            .BuildServiceProvider();
        var firstDefinition = firstOwner
            .GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        _ = secondOwner
            .GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var reopened = await firstDefinition.GetInstanceAsync(instanceId);
        var beforePump = await reopened.GetSnapshotAsync();
        if (beforePump.Status is WorkflowInstanceStatus.Completed or
                WorkflowInstanceStatus.Failed or
                WorkflowInstanceStatus.Cancelled or
                WorkflowInstanceStatus.Terminated ||
            executionCount.Value != 0)
        {
            throw new InvalidOperationException(
                "Callback-only ingress progressed the definition while every definition owner was offline.");
        }

        var terminalCommitted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        sharedProvider.AfterSuccessfulAppend = batch =>
        {
            if (batch.StreamId.InstanceId.Equals(instanceId) &&
                batch.ProjectionOperations.Any(operation =>
                    operation.InstanceSnapshot?.Status is
                        WorkflowInstanceStatus.Completed or
                        WorkflowInstanceStatus.Failed or
                        WorkflowInstanceStatus.Cancelled or
                        WorkflowInstanceStatus.Terminated or
                        WorkflowInstanceStatus.TimedOut))
            {
                terminalCommitted.TrySetResult(true);
            }
        };
        var firstHosted = firstOwner.GetServices<IHostedService>().ToArray();
        var secondHosted = secondOwner.GetServices<IHostedService>().ToArray();
        await Task.WhenAll(firstHosted.Concat(secondHosted)
            .Select(service => service.StartAsync(CancellationToken.None)));
        await context.WaitUntilBarrierReachedAsync("handoff");
        context.ReleaseBarrier("handoff");

        await terminalCommitted.Task;
        sharedProvider.AfterSuccessfulAppend = null;
        await Task.WhenAll(secondHosted.Reverse().Concat(firstHosted.Reverse())
            .Select(service => service.StopAsync(CancellationToken.None)));
        var completed = await context.ObserveAsync(
            _ => reopened.GetSnapshotAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            completed,
            snapshot => snapshot.Status == WorkflowInstanceStatus.Completed &&
                        executionCount.Value == 1,
            "Competing replacement owners did not progress the committed handoff exactly once.");
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
        services.AddSingleton<ProcessLocalEventRouter>();
        return services;
    }

    private static ServiceCollection DurableServices(
        DurableScenarioProvider provider,
        HandoffBarrierStep step)
    {
        var services = DurableProviderServices(provider);
        services.AddSingleton(step);
        services.AddOrcaCoreDurableEngine(DurableOptions());
        services.AddSingleton<ProcessLocalEventRouter>();
        return services;
    }

    private static ServiceCollection DurableIngressServices(
        DurableScenarioProvider provider)
    {
        var services = DurableProviderServices(provider);
        services.AddOrcaCoreDurableEventIngress();
        services.AddSingleton<ProcessLocalEventRouter>();
        return services;
    }

    private static ServiceCollection DurableProviderServices(
        DurableScenarioProvider provider)
    {
        var services = new ServiceCollection();
        provider.AddRoleTo(services);
        return services;
    }

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
                PartitionId = ResourceGovernancePartitionId.Create("handoff"),
                Pools = []
            }
        };

    private static async Task<WaitingJourney> StartWaitingAsync(
        IWorkflowDefinitionRegistry registry,
        string suffix)
    {
        var definitionId = DefinitionId.New();
        var eventName = EventName.Create($"route-{suffix}");
        var correlation = CorrelationId.Create($"route-{suffix}");
        var definition = Workflow.Ephemeral<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .End()
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create($"route-{suffix}"))).GetHandleOrThrow();
        return new WaitingJourney(definitionId, eventName, correlation, instance);
    }

    private static ProcessLocalInboundEvent Event(string id, WaitingJourney waiting) =>
        ProcessLocalInboundEvent.Create(
            EventId.Create(id),
            waiting.EventName,
            waiting.Correlation,
            DateTimeOffset.UtcNow);

    private static ProcessLocalInboundEvent<JourneyPayload> PayloadEvent(
        string id,
        WaitingJourney waiting,
        int value) =>
        ProcessLocalInboundEvent<JourneyPayload>.Create(
            EventId.Create(id),
            waiting.EventName,
            waiting.Correlation,
            new JourneyPayload { Value = value },
            DateTimeOffset.UtcNow);

    private sealed record WaitingJourney(
        DefinitionId DefinitionId,
        EventName EventName,
        CorrelationId Correlation,
        WorkflowInstanceHandle Instance);

    public sealed record JourneyInput(int Value);

    public sealed class JourneyState
    {
        public JourneyState(int value) => Value = value;
        public int Value { get; set; }
    }

    public sealed class JourneyBranchState
    {
        public JourneyBranchState(int value) => Value = value;
        public int Value { get; set; }
    }

    public sealed class JourneyPayload
    {
        public int Value { get; set; }
    }

    public sealed class NamedBarrierStep(
        IPhase0DeterministicBarrier barrier,
        string name) : IStep<JourneyState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<JourneyState> context,
            CancellationToken cancellationToken)
        {
            await barrier.ReachAsync(name, cancellationToken);
            return new StepResult.Completed();
        }
    }

    public sealed class HandoffBarrierStep(
        IPhase0DeterministicBarrier barrier,
        HandoffExecutionCount executionCount) : IStep<JourneyState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<JourneyState> context,
            CancellationToken cancellationToken)
        {
            executionCount.Increment();
            await barrier.ReachAsync("handoff", cancellationToken);
            return new StepResult.Completed();
        }
    }

    public sealed class HandoffExecutionCount
    {
        private int value;
        public int Value => Volatile.Read(ref value);
        public void Increment() => Interlocked.Increment(ref value);
    }
}
