using AwesomeAssertions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableLifecycleFacadeTests
{
    [Fact]
    public async Task CancellationRequest_IsObservableAndIdempotentWhileStepRemainsInFlight()
    {
        var gate = new CancellationGate();
        var store = new InMemoryWorkflowProvider();
        var facade = CreateFacade(store, new StepProvider(gate));
        var eventName = EventName.Create("continue");
        var correlation = CorrelationId.Create("durable-cancellation-request");
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .Then<CancellationGateStep>()
            .End()
            .Build();
        var definitionHandle = facade.Registry.Register(definition).GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            new Input(1),
            StartIdempotencyKey.Create("durable-cancellation-request"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var delivery = facade.Events.AcceptAsync(
            Inbound(
                instance.InstanceId,
                EventId.Create("start-durable-cancellation-step"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken).AsTask();

        await gate.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var firstRequest = instance.RequestCancellationAsync(
            TestContext.Current.CancellationToken).AsTask();
        await gate.CancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var requested = await firstRequest.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        var observed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var repeated = await instance.RequestCancellationAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        gate.Release.TrySetResult();

        requested.Should().Be(WorkflowCancellationRequestStatus.Requested);
        try
        {
            _ = await delivery;
        }
        catch (OperationCanceledException)
        {
            // The event-delivery caller may observe the cancellation that won the workflow race.
        }

        observed.Status.Should().Be(WorkflowInstanceStatus.CancellationRequested);
        repeated.Should().Be(WorkflowCancellationRequestStatus.AlreadyRequested);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Cancelled);
        (await instance.RequestCancellationAsync(TestContext.Current.CancellationToken))
            .Should().Be(WorkflowCancellationRequestStatus.AlreadyTerminal);

        var history = await store.LoadTailAsync(
            new global::OrcaCore.Abstractions.Ids.WorkflowStreamId(instance.InstanceId),
            global::OrcaCore.Abstractions.Ids.StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        history.OfType<global::OrcaCore.Abstractions.Durable.WorkflowCancellationRequestedEvent>()
            .Should().ContainSingle();
        history.OfType<global::OrcaCore.Abstractions.Durable.WorkflowTerminalEvent>()
            .Should().ContainSingle(item =>
                item.Status == WorkflowInstanceStatus.Cancelled);
    }

    [Fact]
    public async Task ConcurrentTerminationRequests_AreClassifiedFromTheSerializedWinner()
    {
        var store = new InMemoryWorkflowProvider();
        var facade = CreateFacade(store);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(
                WorkflowEventContract.Create(EventName.Create("never"), EventContractVersion.Initial),
                _ => CorrelationId.Create("durable-termination-race"))
            .End()
            .Build();
        var instance = (await facade.Registry.Register(definition)
                .GetHandleOrThrow()
                .StartOrGetAsync(
                    new Input(1),
                    StartIdempotencyKey.Create("durable-termination-race"),
                    TestContext.Current.CancellationToken))
            .GetHandleOrThrow();

        var first = instance.TerminateAsync(TestContext.Current.CancellationToken).AsTask();
        var second = instance.TerminateAsync(TestContext.Current.CancellationToken).AsTask();
        var results = await Task.WhenAll(first, second);

        results.Should().ContainSingle(status => status == WorkflowTerminationStatus.Terminated);
        results.Should().ContainSingle(status => status == WorkflowTerminationStatus.AlreadyTerminal);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Terminated);
    }

    [Fact]
    public async Task ReplacementHost_FinalizesPersistedCancellationRequest()
    {
        var gate = new CancellationGate();
        var store = new InMemoryWorkflowProvider();
        var original = CreateFacade(store, new StepProvider(gate));
        var eventName = EventName.Create("replacement-continue");
        var correlation = CorrelationId.Create("replacement-cancellation");
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .Then<CancellationGateStep>()
            .End()
            .Build();
        var originalHandle = original.Registry.Register(definition).GetHandleOrThrow();
        var instance = (await originalHandle.StartOrGetAsync(
            new Input(1),
            StartIdempotencyKey.Create("replacement-cancellation"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var delivery = original.Events.AcceptAsync(
            Inbound(
                instance.InstanceId,
                EventId.Create("replacement-start-step"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken).AsTask();

        await gate.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        (await instance.RequestCancellationAsync(TestContext.Current.CancellationToken))
            .Should().Be(WorkflowCancellationRequestStatus.Requested);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.CancellationRequested);

        var replacement = CreateFacade(store);
        var replacementHandle = replacement.Registry.Register(definition).GetHandleOrThrow();
        var reopened = await replacementHandle.GetInstanceAsync(
            instance.InstanceId,
            TestContext.Current.CancellationToken);
        (await reopened.RequestCancellationAsync(TestContext.Current.CancellationToken))
            .Should().Be(WorkflowCancellationRequestStatus.AlreadyRequested);
        (await reopened.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Cancelled);

        gate.Release.TrySetResult();
        try
        {
            _ = await delivery;
        }
        catch (OperationCanceledException)
        {
            // The original host's physical caller may observe the fenced cancellation.
        }
    }

    private static FacadeServices CreateFacade(
        InMemoryWorkflowProvider store,
        IServiceProvider? serviceProvider = null)
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            serviceProvider is null
                ? new DurableDefinitionRegistry()
                : new DurableDefinitionRegistry(serviceProvider),
            TimeProvider.System,
            projectionStore: store);
        return new FacadeServices(
            new DurableWorkflowDefinitionRegistry(
                runtime,
                store,
                store,
                processor,
                notifications,
                TimeProvider.System),
            new DurableWorkflowEventIngressCore(runtime, store, store));
    }

    private static WorkflowInboundEvent Inbound(
        InstanceId instanceId,
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt) =>
        WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            eventId,
            correlationId,
            causationEventId: null,
            occurredAt,
            new WorkflowEventRoute.Direct(instanceId));

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        DurableWorkflowEventIngressCore Events);

    private sealed record Input(int Value);
    private sealed record State(int Value);

    private sealed class CancellationGateStep(CancellationGate gate) : IStep<State>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            await gate.RunAsync(cancellationToken).ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    private sealed class StepProvider(CancellationGate gate) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(CancellationGateStep)
                ? new CancellationGateStep(gate)
                : null;
    }

    private sealed class CancellationGate
    {
        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal async ValueTask RunAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            using var registration = cancellationToken.Register(
                () => CancellationObserved.TrySetResult());
            await Release.Task.ConfigureAwait(false);
        }
    }
}
