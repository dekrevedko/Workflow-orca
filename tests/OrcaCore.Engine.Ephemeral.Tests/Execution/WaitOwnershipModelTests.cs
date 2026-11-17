using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Engine.Ephemeral.Internal;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class WaitOwnershipModelTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static readonly WorkflowEventContract Ready = WorkflowEventContract.Create(
        EventName.Create("ownership-pilot"), EventContractVersion.Initial);
    private static readonly CorrelationId Key = CorrelationId.Create("same-key");
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CallerCancellationAfterSuccessorRegistration_PreservesUniqueActiveWait()
    {
        var collection = new ServiceCollection();
        collection.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = []
            },
            TransientPools = []
        });
        using var services = collection.BuildServiceProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var releaseSelector = new ManualResetEventSlim();
        var selectorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .Wait(Ready, _ => Key)
            .Wait(Ready, _ =>
            {
                selectorEntered.TrySetResult();
                if (!releaseSelector.Wait(Guard, TestContext.Current.CancellationToken))
                    throw new TimeoutException("Pilot selector barrier was not released.");
                return Key;
            })
            .End().Build();
        var handle = services.GetRequiredService<IWorkflowDefinitionRegistry>().Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("input", StartIdempotencyKey.Create("pilot"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var router = new ProcessLocalEventRouter(services);
        var delivery = Task.Run(async () => await router.RouteByCorrelationAsync(definition.DefinitionId,
            ProcessLocalInboundEvent.Create(EventId.Create("pilot-event"), Ready.EventName, Key,
                Now), cancellation.Token), TestContext.Current.CancellationToken);
        try
        {
            await selectorEntered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            cancellation.Cancel();
        }
        finally { releaseSelector.Set(); }
        Func<Task> observe = async () => await delivery.WaitAsync(Guard, TestContext.Current.CancellationToken);
        await observe.Should().ThrowAsync<OperationCanceledException>();
        services.GetRequiredService<EphemeralWorkflowEngine>()
            .TryGetFacadeInstance(instance.InstanceId, out var runtime).Should().BeTrue();
        // Inspect actual state: the published snapshot can hide the restored predecessor.
        var actual = ((WorkflowInstance<State>)runtime!).ToSnapshot();
        actual.ActiveWaits.Should().ContainSingle("WOP-002 forbids two active occurrences for the same key");
    }

    [Fact]
    public async Task MatchedReservationAndSuccessor_ExcludeOtherInstanceUntilExactRelease()
    {
        var index = new EphemeralRoutingIndex();
        var definition = DefinitionId.New();
        var first = Create(index, definition);
        var competitor = Create(index, definition);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RuntimeWaitRecord? successor = null;
        AddWait(first, async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Guard, Token);
            successor = AddWait(first);
        });
        var delivery = first.RaiseEventAsync(Envelope("one"), Now, Token);
        try
        {
            await entered.Task.WaitAsync(Guard, Token);
            Action register = () => AddWait(competitor);
            register.Should().Throw<AmbiguousWaitRegistrationException>();
        }
        finally { release.TrySetResult(); }
        await delivery.WaitAsync(Guard, Token);
        index.FindUniqueOwner(definition, Ready, Key).Should().Be(first.InstanceId);
        Action stillOwned = () => AddWait(competitor);
        stillOwned.Should().Throw<AmbiguousWaitRegistrationException>();
        first.CancelWait(successor!);
        AddWait(competitor);
        index.FindUniqueOwner(definition, Ready, Key).Should().Be(competitor.InstanceId);
    }

    [Fact]
    public async Task EscapingContinuation_RetainsReservationAndRetriesWithoutDoubleConsumption()
    {
        var index = new EphemeralRoutingIndex();
        var definition = DefinitionId.New();
        var instance = Create(index, definition);
        var competitor = Create(index, definition);
        var attempts = 0;
        var original = AddWait(instance, (_, _) => ++attempts == 1
            ? Task.FromException(new InvalidOperationException("injected escape")) : Task.CompletedTask);
        Func<Task> deliver = () => instance.RaiseEventAsync(Envelope("retry"), Now, Token);
        await deliver.Should().ThrowAsync<InvalidOperationException>();
        instance.ToSnapshot().ActiveWaits.Should().ContainSingle().Which.WaitId.Should().Be(original.WaitId);
        Action register = () => AddWait(competitor);
        register.Should().Throw<AmbiguousWaitRegistrationException>();
        await deliver();
        await deliver();
        attempts.Should().Be(2);
        index.FindUniqueOwner(definition, Ready, Key).Should().BeNull();
        AddWait(competitor);
    }

    [Fact]
    public async Task QueuedDuplicate_InvokesContinuationOnce()
    {
        var index = new EphemeralRoutingIndex();
        var instance = Create(index, DefinitionId.New());
        var lane = new InstanceExecutionLane();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocations = 0;
        AddWait(instance, async (_, _) =>
        {
            invocations++;
            entered.TrySetResult();
            await release.Task.WaitAsync(Guard, Token);
        });
        var envelope = Envelope("duplicate");
        var first = lane.RunAsync(instance.InstanceId, _ => instance.RaiseEventAsync(envelope, Now, Token), Token);
        Task? duplicate = null;
        try
        {
            await entered.Task.WaitAsync(Guard, Token);
            duplicate = lane.RunAsync(instance.InstanceId, _ => instance.RaiseEventAsync(envelope, Now, Token), Token);
            duplicate.IsCompleted.Should().BeFalse();
        }
        finally { release.TrySetResult(); }
        await first.WaitAsync(Guard, Token);
        await duplicate!.WaitAsync(Guard, Token);
        invocations.Should().Be(1);
        instance.ToSnapshot().ActiveWaits.Should().BeEmpty();
        index.FindUniqueOwner(instance.DefinitionId, Ready, Key).Should().BeNull();
    }

    [Fact]
    public async Task StaleTimerAndRepeatedCleanup_CannotReleaseSuccessorReservation()
    {
        var index = new EphemeralRoutingIndex();
        var definition = DefinitionId.New();
        var instance = Create(index, definition);
        RuntimeWaitRecord? successor = null;
        var original = AddWait(instance, (_, _) => { successor = AddWait(instance); return Task.CompletedTask; });
        await instance.RaiseEventAsync(Envelope("handover"), Now, Token);
        await instance.FireWaitTimeoutAsync(original, Now.AddMinutes(1),
            _ => throw new InvalidOperationException("stale timer ran"), Token);
        instance.CancelWait(original);
        instance.ToSnapshot().ActiveWaits.Should().ContainSingle().Which.WaitId.Should().Be(successor!.WaitId);
        index.FindUniqueOwner(definition, Ready, Key).Should().Be(instance.InstanceId);
        instance.CancelWait(successor!);
        instance.CancelWait(successor!);
        index.FindUniqueOwner(definition, Ready, Key).Should().BeNull();
    }

    [Theory]
    [InlineData("definition")]
    [InlineData("name")]
    [InlineData("version")]
    [InlineData("correlation")]
    public void FullKeyIsolation_PreservesUnrelatedOwner(string dimension)
    {
        var index = new EphemeralRoutingIndex();
        var definition = DefinitionId.New();
        var first = Create(index, definition);
        var second = Create(index, dimension == "definition" ? DefinitionId.New() : definition);
        var contract = WorkflowEventContract.Create(dimension == "name" ? EventName.Create("other") : Ready.EventName,
            dimension == "version" ? new EventContractVersion(2) : EventContractVersion.Initial);
        var correlation = dimension == "correlation" ? CorrelationId.Create("other") : Key;
        var old = AddWait(first);
        var other = second.EnterWait(contract, correlation, null, Now, "workflow:$/n:00000002", null,
            (_, _) => Task.CompletedTask);
        first.CancelWait(old);
        index.FindUniqueOwner(second.DefinitionId, contract, correlation).Should().Be(second.InstanceId);
        second.ToSnapshot().ActiveWaits.Should().ContainSingle().Which.WaitId.Should().Be(other.WaitId);
        second.CancelWait(other);
        index.FindUniqueOwner(second.DefinitionId, contract, correlation).Should().BeNull();
    }

    [Fact]
    public async Task CancellationQueuesBehindContinuationAndReleasesSuccessor()
    {
        var index = new EphemeralRoutingIndex();
        var instance = Create(index, DefinitionId.New());
        var lane = new InstanceExecutionLane();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AddWait(instance, async (_, _) =>
        {
            AddWait(instance);
            entered.TrySetResult();
            await release.Task.WaitAsync(Guard, Token);
        });
        var delivery = lane.RunAsync(instance.InstanceId,
            _ => instance.RaiseEventAsync(Envelope("cancel-race"), Now, Token), Token);
        Task? cleanup = null;
        try
        {
            await entered.Task.WaitAsync(Guard, Token);
            instance.TryRequestCancellation(Now).Should().Be(WorkflowCancellationRequestStatus.Requested);
            cleanup = lane.RunAsync(instance.InstanceId, _ => Task.FromResult(instance.Cancel(Now)), Token);
            cleanup.IsCompleted.Should().BeFalse("the instance lane is still executing the continuation");
            index.FindUniqueOwner(instance.DefinitionId, Ready, Key).Should().Be(instance.InstanceId);
        }
        finally { release.TrySetResult(); }
        await delivery.WaitAsync(Guard, Token);
        await cleanup!.WaitAsync(Guard, Token);
        instance.ToSnapshot().ActiveWaits.Should().BeEmpty();
        index.FindUniqueOwner(instance.DefinitionId, Ready, Key).Should().BeNull();
    }

    private static WorkflowInstance<State> Create(EphemeralRoutingIndex index, DefinitionId definition)
    {
        var instance = new WorkflowInstance<State>(InstanceId.Parse(Guid.NewGuid().ToString()), definition,
            DefinitionVersion.Initial, new State("input"), Now);
        instance.SetCorrelationIndex(index);
        return instance;
    }

    private static RuntimeWaitRecord AddWait(WorkflowInstance<State> instance,
        Func<EventEnvelope, CancellationToken, Task>? resume = null) =>
        instance.EnterWait(Ready, Key, null, Now, "workflow:$/n:00000001", Now.AddMinutes(1),
            resume ?? ((_, _) => Task.CompletedTask));

    private static EventEnvelope Envelope(string id) =>
        EphemeralContractAdapter.EventEnvelope(EventId.Create(id), Ready, Key, Now, ReadOnlyMemory<byte>.Empty);

    private sealed record State(string Value);
}
