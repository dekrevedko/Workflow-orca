using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Facade;

public sealed class EphemeralWorkflowFacadeTests
{
    [Fact]
    public async Task Registry_StartsReopensAndReturnsDetachedTypedOutput()
    {
        using var provider = CreateServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(
                snapshot => new Output(snapshot.Value.Value + 1),
                WorkflowOutcomeName.Create("completed"))
            .Build();

        var registration = registry.Register(definition);
        var definitionHandle = registration.GetHandleOrThrow();
        var start = await definitionHandle.StartOrGetAsync(
            new Input(41),
            StartIdempotencyKey.Create("facade-start"),
            TestContext.Current.CancellationToken);

        start.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<Output>>.Accepted>()
            .Which.WasExisting.Should().BeFalse();
        var output = await start.WaitForOutputAsync(TestContext.Current.CancellationToken);
        output.Should().Be(new Output(42));

        var instance = start.GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Outcome.Should().Be(WorkflowOutcomeName.Create("completed"));
        (await instance.GetStateAsync<State>(TestContext.Current.CancellationToken))
            .Should().Be(new State(41));

        var reopened = await definitionHandle.GetInstanceAsync(
            instance.InstanceId,
            TestContext.Current.CancellationToken);
        (await reopened.GetOutputAsync(TestContext.Current.CancellationToken))
            .Should().BeOfType<WorkflowOutputResult<Output>.Available>()
            .Which.Output.Should().Be(new Output(42));

        var replay = await definitionHandle.StartOrGetAsync(
            new Input(41),
            StartIdempotencyKey.Create("facade-start"),
            TestContext.Current.CancellationToken);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<Output>>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        replay.GetHandleOrThrow().InstanceId.Should().Be(instance.InstanceId);
    }

    [Fact]
    public async Task StartKeyConflictAndHostModeMismatchUseClosedResults()
    {
        using var provider = CreateServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definitionId = DefinitionId.New();
        var ephemeral = Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
        var handle = registry.Register(ephemeral).GetHandleOrThrow();
        _ = await handle.StartOrGetAsync(
            new Input(1),
            StartIdempotencyKey.Create("conflict-key"),
            TestContext.Current.CancellationToken);

        var conflict = await handle.StartOrGetAsync(
            new Input(2),
            StartIdempotencyKey.Create("conflict-key"),
            TestContext.Current.CancellationToken);
        conflict.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Conflict>();
        var exception = () => conflict.GetHandleOrThrow();
        exception.Should().Throw<WorkflowStartIdempotencyConflictException>()
            .Which.Conflict.Should().BeSameAs(
                ((WorkflowStartResult<WorkflowInstanceHandle>.Conflict)conflict).Error);

        var durable = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
        registry.Register(durable)
            .Should().BeOfType<WorkflowRegistrationResult<DurableDefinitionHandle<Input>>.HostIncompatible>()
            .Which.Error.Should().BeOfType<DefinitionHostCompatibilityFailure.EngineModeMismatch>();
    }

    [Fact]
    public async Task CancellationRequest_IsObservableAndIdempotentWhileStepRemainsInFlight()
    {
        var gate = new CancellationGate();
        using var provider = CreateServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var eventName = EventName.Create("continue");
        var correlation = CorrelationId.Create("cancellation-request");
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .Then((_, cancellationToken) => gate.RunAsync(cancellationToken))
            .End()
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            new Input(1),
            StartIdempotencyKey.Create("cancellation-request"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var delivery = events.DeliverToInstanceAsync(
            instance.InstanceId,
            WorkflowEvent.Create(
                EventId.Create("start-in-flight-step"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken).AsTask();

        await gate.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var firstRequestTask = instance.RequestCancellationAsync(
            TestContext.Current.CancellationToken).AsTask();
        await gate.CancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        var observedStatus = (await instance.GetSnapshotAsync(
            TestContext.Current.CancellationToken)).Status;
        WorkflowCancellationRequestStatus? repeatedStatus = null;
        Exception? repeatedFailure = null;
        try
        {
            repeatedStatus = await instance.RequestCancellationAsync(
                    TestContext.Current.CancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }
        catch (Exception exception)
        {
            repeatedFailure = exception;
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        WorkflowCancellationRequestStatus? firstStatus = null;
        Exception? firstFailure = null;
        try
        {
            firstStatus = await firstRequestTask;
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }

        try
        {
            _ = await delivery;
        }
        catch (OperationCanceledException)
        {
            // The event-delivery caller may observe the cancellation that won the workflow race.
        }

        observedStatus.Should().Be(WorkflowInstanceStatus.CancellationRequested);
        repeatedFailure.Should().BeNull();
        repeatedStatus.Should().Be(WorkflowCancellationRequestStatus.AlreadyRequested);
        firstFailure.Should().BeNull();
        firstStatus.Should().Be(WorkflowCancellationRequestStatus.Requested);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Cancelled);
        (await instance.RequestCancellationAsync(TestContext.Current.CancellationToken))
            .Should().Be(WorkflowCancellationRequestStatus.AlreadyTerminal);
    }

    [Fact]
    public async Task ConcurrentTerminationRequests_AreClassifiedFromTheSerializedWinner()
    {
        var gate = new CancellationGate();
        using var provider = CreateServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var eventName = EventName.Create("continue");
        var correlation = CorrelationId.Create("termination-race");
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .Then((_, cancellationToken) => gate.RunAsync(cancellationToken))
            .End()
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            new Input(1),
            StartIdempotencyKey.Create("termination-race"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var delivery = events.DeliverToInstanceAsync(
            instance.InstanceId,
            WorkflowEvent.Create(
                EventId.Create("start-termination-race"),
                eventName,
                correlation,
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken).AsTask();

        await gate.Started.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var first = instance.TerminateAsync(TestContext.Current.CancellationToken).AsTask();
        await gate.CancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var second = instance.TerminateAsync(TestContext.Current.CancellationToken).AsTask();
        gate.Release.TrySetResult();

        var results = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            _ = await delivery;
        }
        catch (OperationCanceledException)
        {
            // The event-delivery caller observes the termination fence that won the lane.
        }

        results.Should().BeEquivalentTo(
            [WorkflowTerminationStatus.Terminated, WorkflowTerminationStatus.AlreadyTerminal]);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Terminated);
        (await instance.RequestCancellationAsync(TestContext.Current.CancellationToken))
            .Should().Be(WorkflowCancellationRequestStatus.AlreadyTerminal);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services;
    }

    private sealed record Input(int Value);
    private sealed record State(int Value);
    private sealed record Output(int Value);

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
