using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class ApplicationJourneyScenarioHost
{
    [Phase0Scenario("four-event-overloads", "3.8")]
    public static async Task AllFourEventRoutesReturnClosedStatuses(Phase0ScenarioContext context)
    {
        using var provider = EphemeralServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var instancePayloadless = await StartWaitingAsync(registry, "instance-plain");
        var instancePayload = await StartWaitingAsync(registry, "instance-payload");
        var correlationPayloadless = await StartWaitingAsync(registry, "correlation-plain");
        var correlationPayload = await StartWaitingAsync(registry, "correlation-payload");

        var first = await context.ObserveAsync(_ => events.DeliverToInstanceAsync(
            instancePayloadless.Instance.InstanceId,
            Event("route-instance-plain", instancePayloadless)));
        Phase0Assert.Satisfies(
            first,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == instancePayloadless.Instance.InstanceId,
            "The payloadless instance route did not return its exact closed delivery result.");

        var callerPayload = new JourneyPayload { Value = 2 };
        var detachedPayloadEvent = WorkflowEvent<JourneyPayload>.Create(
            EventId.Create("route-instance-payload"),
            instancePayload.EventName,
            instancePayload.Correlation,
            callerPayload,
            DateTimeOffset.UtcNow);
        callerPayload.Value = 99;
        if (detachedPayloadEvent.Payload.Value != 2 ||
            ReferenceEquals(detachedPayloadEvent.Payload, callerPayload))
        {
            throw new InvalidOperationException("WorkflowEvent<TPayload> retained caller-owned payload state.");
        }

        var second = await context.ObserveAsync(_ => events.DeliverToInstanceAsync(
            instancePayload.Instance.InstanceId,
            detachedPayloadEvent));
        Phase0Assert.Satisfies(
            second,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == instancePayload.Instance.InstanceId,
            "The payload instance route did not return its exact closed delivery result.");

        var third = await context.ObserveAsync(_ => events.DeliverByCorrelationAsync(
            correlationPayloadless.DefinitionId,
            Event("route-correlation-plain", correlationPayloadless)));
        Phase0Assert.Satisfies(
            third,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == correlationPayloadless.Instance.InstanceId,
            "The payloadless correlation route did not return its exact closed delivery result.");

        var fourth = await context.ObserveAsync(_ => events.DeliverByCorrelationAsync(
            correlationPayload.DefinitionId,
            PayloadEvent("route-correlation-payload", correlationPayload, 4)));
        Phase0Assert.Satisfies(
            fourth,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == correlationPayload.Instance.InstanceId,
            "The payload correlation route did not return its exact closed delivery result.");
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
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var definition = Workflow.Ephemeral<JourneyState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(unlockName, _ => unlockCorrelation)
            .Wait(targetName, _ => targetCorrelation)
            .Then<NamedBarrierStep>()
            .End()
            .Build();
        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("dedup-start"))).GetHandleOrThrow();
        var target = WorkflowEvent.Create(
            EventId.Create("dedup-target"),
            targetName,
            targetCorrelation,
            DateTimeOffset.UtcNow);

        var beforeWait = await context.ObserveAsync(_ =>
            events.DeliverToInstanceAsync(instance.InstanceId, target));
        Phase0Assert.Satisfies(
            beforeWait,
            result => result.Status == EventDeliveryStatus.NoActiveWait,
            "A pre-wait event was consumed instead of returning NoActiveWait.");

        var unlocked = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            WorkflowEvent.Create(
                EventId.Create("dedup-unlock"),
                unlockName,
                unlockCorrelation,
                DateTimeOffset.UtcNow));
        if (unlocked.Status != EventDeliveryStatus.Accepted)
        {
            throw new InvalidOperationException("The workflow did not progress to its target wait.");
        }

        var acceptedTask = context.ObserveAsync(_ =>
            events.DeliverToInstanceAsync(instance.InstanceId, target)).AsTask();
        await context.WaitUntilBarrierReachedAsync("dedup");
        context.ReleaseBarrier("dedup");
        var accepted = await acceptedTask;
        Phase0Assert.Satisfies(
            accepted,
            result => result.Status == EventDeliveryStatus.Accepted,
            "The same event envelope was not accepted after the matching wait became active.");

        var duplicate = await events.DeliverToInstanceAsync(instance.InstanceId, target);
        if (duplicate.Status != EventDeliveryStatus.Duplicate)
        {
            throw new InvalidOperationException("An identical accepted replay was not classified as Duplicate.");
        }

        var conflict = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            WorkflowEvent.Create(
                target.EventId,
                EventName.Create("changed-dedup"),
                targetCorrelation,
                target.OccurredAt));
        if (conflict.Status != EventDeliveryStatus.EventConflict)
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
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var ambiguous = Workflow.Ephemeral<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Parallel<string>(branches => branches
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("first"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(eventName, _ => correlation)
                        .Return(state => state.Value.Value.ToString()))
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("second"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(eventName, _ => correlation)
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
                  exception.EventName.Value == eventName.Value &&
                  exception.CorrelationId.Value == correlation.Value)
        {
        }

        var noTarget = await context.ObserveAsync(_ => events.DeliverByCorrelationAsync(
            definitionId,
            WorkflowEvent.Create(
                EventId.Create("ambiguous-no-target"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow)));
        Phase0Assert.Satisfies(
            noTarget,
            result => result.Status == EventDeliveryStatus.NoActiveWait && result.InstanceId is null,
            "A rejected ambiguous candidate remained available as an event target.");

        var crossDefinitionId = DefinitionId.New();
        var cross = Workflow.Ephemeral<JourneyState>(crossDefinitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(eventName, _ => correlation)
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
        var services = EphemeralServices();
        services.AddSingleton(new NamedBarrierStep(context.Services.Barrier, "signal-reuse"));
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var definitionId = DefinitionId.New();
        var definition = Workflow.Ephemeral<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(eventName, _ => correlation)
            .Wait(eventName, _ => correlation)
            .Then<NamedBarrierStep>()
            .End()
            .Build();
        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("signal-reuse"))).GetHandleOrThrow();

        var first = await events.DeliverByCorrelationAsync(
            definitionId,
            WorkflowEvent.Create(
                EventId.Create("signal-reuse-first"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow));
        if (first.Status != EventDeliveryStatus.Accepted)
        {
            throw new InvalidOperationException("The first signal occurrence was not consumed.");
        }

        var secondTask = context.ObserveAsync(_ => events.DeliverByCorrelationAsync(
            definitionId,
            WorkflowEvent.Create(
                EventId.Create("signal-reuse-second"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow))).AsTask();
        await context.WaitUntilBarrierReachedAsync("signal-reuse");
        context.ReleaseBarrier("signal-reuse");
        var second = await secondTask;
        Phase0Assert.Satisfies(
            second,
            result => result.Status == EventDeliveryStatus.Accepted &&
                      result.InstanceId == instance.InstanceId,
            "The later occurrence did not consume a later event with the same authored signal pair.");

        var snapshot = await instance.GetSnapshotAsync();
        if (snapshot.Status != WorkflowInstanceStatus.Completed)
        {
            throw new InvalidOperationException("The workflow did not complete after both signal occurrences.");
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
            .Wait(eventName, _ => correlation)
            .Then<HandoffBarrierStep>()
            .End()
            .Build();
        var sharedWorkflowStore = new InMemoryWorkflowProvider();
        var sharedPoolStore = new InMemoryResourcePoolStore();
        var sharedGovernanceStore = new InMemoryResourceGovernanceStore();
        InstanceId instanceId;

        using (var originalOwner = DurableServices(
                   sharedWorkflowStore,
                   sharedPoolStore,
                   sharedGovernanceStore,
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
                   sharedWorkflowStore,
                   sharedPoolStore,
                   sharedGovernanceStore).BuildServiceProvider())
        {
            var ingress = ingressOwner.GetRequiredService<IWorkflowEventClient>();
            var accepted = await context.ObserveAsync(_ => ingress.DeliverByCorrelationAsync(
                definitionId,
                WorkflowEvent.Create(
                    EventId.Create("handoff-event"),
                    eventName,
                    correlation,
                    DateTimeOffset.UtcNow)));
            Phase0Assert.Satisfies(
                accepted,
                result => result.Status == EventDeliveryStatus.Accepted &&
                          result.InstanceId == instanceId,
                "Definition-less ingress did not commit the inbox and continuation handoff.");
        }

        using var firstOwner = DurableServices(
                sharedWorkflowStore,
                sharedPoolStore,
                sharedGovernanceStore,
                step)
            .BuildServiceProvider();
        using var secondOwner = DurableServices(
                sharedWorkflowStore,
                sharedPoolStore,
                sharedGovernanceStore,
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

        var firstPump = firstOwner.GetRequiredService<DurableContinuationPump>();
        var secondPump = secondOwner.GetRequiredService<DurableContinuationPump>();
        var claimedAt = DateTimeOffset.UtcNow;
        var firstTask = firstPump.PumpOnceAsync(
            new OutboxClaimRequest(16, claimedAt, TimeSpan.FromMinutes(1)),
            CancellationToken.None);
        var secondTask = secondPump.PumpOnceAsync(
            new OutboxClaimRequest(16, claimedAt, TimeSpan.FromMinutes(1)),
            CancellationToken.None);
        await context.WaitUntilBarrierReachedAsync("handoff");
        context.ReleaseBarrier("handoff");
        _ = await Task.WhenAll(firstTask, secondTask);

        var completed = await context.ObserveAsync(_ => reopened.GetSnapshotAsync());
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
        return services;
    }

    private static ServiceCollection DurableServices(
        InMemoryWorkflowProvider workflowStore,
        InMemoryResourcePoolStore poolStore,
        InMemoryResourceGovernanceStore governanceStore,
        HandoffBarrierStep step)
    {
        var services = DurableProviderServices(workflowStore, poolStore, governanceStore);
        services.AddSingleton(step);
        services.AddOrcaCoreDurableEngine(DurableOptions());
        return services;
    }

    private static ServiceCollection DurableIngressServices(
        InMemoryWorkflowProvider workflowStore,
        InMemoryResourcePoolStore poolStore,
        InMemoryResourceGovernanceStore governanceStore)
    {
        var services = DurableProviderServices(workflowStore, poolStore, governanceStore);
        services.AddOrcaCoreDurableEventIngress();
        return services;
    }

    private static ServiceCollection DurableProviderServices(
        InMemoryWorkflowProvider workflowStore,
        InMemoryResourcePoolStore poolStore,
        InMemoryResourceGovernanceStore governanceStore)
    {
        var services = new ServiceCollection();
        services.AddSingleton(workflowStore);
        services.AddSingleton(poolStore);
        services.AddSingleton(governanceStore);
        services.AddOrcaCoreInMemoryDurableProvider();
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
            .Wait(eventName, _ => correlation)
            .End()
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create($"route-{suffix}"))).GetHandleOrThrow();
        return new WaitingJourney(definitionId, eventName, correlation, instance);
    }

    private static WorkflowEvent Event(string id, WaitingJourney waiting) =>
        WorkflowEvent.Create(
            EventId.Create(id),
            waiting.EventName,
            waiting.Correlation,
            DateTimeOffset.UtcNow);

    private static WorkflowEvent<JourneyPayload> PayloadEvent(
        string id,
        WaitingJourney waiting,
        int value) =>
        WorkflowEvent<JourneyPayload>.Create(
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
