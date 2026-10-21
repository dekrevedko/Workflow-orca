using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Engine.Ephemeral.Tests.Support;
using Xunit;
using Host = OrcaCore.Engine.Ephemeral.Tests.Shutdown.HostShutdownTests.Host;
using State = OrcaCore.Engine.Ephemeral.Tests.Shutdown.HostShutdownTests.State;

namespace OrcaCore.Engine.Ephemeral.Tests.Shutdown;

[Trait("Slice", "Issue2Section3")]
[Trait("Disposition", "Implemented")]
public sealed class ShutdownDrainTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly WorkflowEventContract Continue = WorkflowEventContract.Create(EventName.Create("shutdown-continue"), EventContractVersion.Initial);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("AC", "AC-545")]
    [Trait("AC", "AC-547")]
    [Trait("AC", "AC-548")]
    [Trait("AC", "AC-554")]
    public async Task Closed_AcceptsNewOutputWaits_AndLetsEventsAndDelayAttemptsDrain(bool bounded, bool delay)
    {
        using var host = new Host(bounded);
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(input => new State(input));
        if (delay) root.Delay(TimeSpan.FromSeconds(10));
        else root.Wait(Continue, _ => CorrelationId.Create("drain"));
        var definition = root.Then<HostShutdownTests.Body>().End(state => state.Value.Value, WorkflowOutcomeName.Create("done")).Build();
        var registered = host.Registry.Register(definition).GetHandleOrThrow();
        var accepted = await registered.StartOrGetAsync("output", StartIdempotencyKey.Create("drain"), Token);
        var instance = accepted.GetHandleOrThrow();
        host.Application.StopApplication();
        using var cancelledWait = new CancellationTokenSource();
        var local = instance.WaitForOutputAsync(cancelledWait.Token).AsTask();
        cancelledWait.Cancel();
        var localError = await Record.ExceptionAsync(async () => await local.WaitAsync(Guard, Token));
        localError.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(cancelledWait.Token);
        (await instance.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Waiting);
        (await instance.GetStateAsync<State>(Token)).Value.Should().Be("output");
        var output = instance.WaitForOutputAsync(Token).AsTask();
        Task? delivery = null;
        if (delay) host.Clock.Advance(TimeSpan.FromSeconds(10));
        else delivery = Deliver(host, instance.InstanceId);
        await host.Body.Entered.Task.WaitAsync(Guard, Token);
        host.Body.CancellationObserved.Task.IsCompleted.Should().BeFalse("Closed allows accepted attempts without requesting cancellation");
        host.Body.Release.TrySetResult();
        if (delivery is not null) await delivery.WaitAsync(Guard, Token);
        (await output.WaitAsync(Guard, Token)).Should().Be("output");
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Fact]
    [Trait("AC", "AC-547")]
    public async Task Closed_RetryBackoffCanAdmitAnotherAttempt_WithoutCancellation()
    {
        var retry = new RetryBody();
        using var host = new Host(configure: services => services.AddSingleton(retry));
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(input => new State(input))
            .Then<RetryBody>().WithRetry(2, TimeSpan.FromSeconds(10))
            .End(state => state.Value.Value, WorkflowOutcomeName.Create("retried")).Build();
        var handle = host.Registry.Register(definition).GetHandleOrThrow();
        var accepted = await handle.StartOrGetAsync("retry", StartIdempotencyKey.Create("retry"), Token);
        retry.Attempts.Should().Be(1);
        host.Application.StopApplication();
        var output = accepted.GetHandleOrThrow().WaitForOutputAsync(Token).AsTask();
        host.Clock.Advance(TimeSpan.FromSeconds(10));
        (await output.WaitAsync(Guard, Token)).Should().Be("retry");
        retry.Attempts.Should().Be(2);
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Fact]
    [Trait("AC", "AC-545")]
    public async Task DelayedApplicationStoppingCallback_StillClosesAdmission_WithoutStoppingAcceptedWork()
    {
        using var host = new Host();
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(input => new State(input))
            .Wait(Continue, _ => CorrelationId.Create("drain")).End().Build();
        var handle = host.Register(definition);
        var instance = (await host.Start(handle)).GetHandleOrThrow();
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var registration = host.Application.ApplicationStopping.Register(() =>
        {
            callbackEntered.TrySetResult();
            release.Wait(Guard).Should().BeTrue();
        });
        var closing = Task.Run(host.Application.StopApplication, Token);
        try
        {
            await callbackEntered.Task.WaitAsync(Guard, Token);
            (await Record.ExceptionAsync(async () => await host.Start(handle, "blocked-notification")))
                .Should().BeOfType<EphemeralHostStoppingException>();
            (await instance.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Waiting);
            await Deliver(host, instance.InstanceId).WaitAsync(Guard, Token);
            (await instance.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Completed);
        }
        finally { release.Set(); }
        await closing.WaitAsync(Guard, Token);
        await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token);
    }

    [Fact]
    [Trait("AC", "AC-545")]
    public async Task ResolvingOnAnAlreadyStoppingHost_ClosesOnlyAdmission()
    {
        using var application = new HostShutdownTests.ApplicationLifetime();
        application.StopApplication();
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(application);
        services.AddOrcaCoreEphemeralEngine(HostShutdownTests.Options());
        using var provider = services.BuildServiceProvider();
        var lifetime = provider.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value)).End().Build();
        Record.Exception(() => registry.Register(definition)).Should().BeOfType<EphemeralHostStoppingException>();
        (await lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Fact]
    [Trait("AC", "AC-552")]
    public async Task Completed_RejectsLiveOperations_PreservesMetadataPrecedenceAndPassiveIdentity()
    {
        using var host = new Host();
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
            .End(state => state.Value.Value, WorkflowOutcomeName.Create("done")).Build();
        var handle = host.Registry.Register(definition).GetHandleOrThrow();
        var accepted = await handle.StartOrGetAsync("detached", StartIdempotencyKey.Create("completed"), Token);
        var instance = accepted.GetHandleOrThrow();
        var output = await instance.WaitForOutputAsync(Token);
        var snapshot = await instance.GetSnapshotAsync(Token);
        await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token);
        var calls = new Func<Task>[]
        {
            async () => await instance.GetSnapshotAsync(Token),
            async () => await instance.GetStateAsync<State>(Token),
            async () => await instance.GetOutputAsync(Token),
            async () => await instance.WaitForOutputAsync(Token),
            async () => await instance.RequestCancellationAsync(Token),
            async () => await instance.TerminateAsync(Token),
            async () => await handle.GetInstanceAsync(instance.InstanceId, Token),
            async () => await handle.StartOrGetAsync("detached", StartIdempotencyKey.Create("completed"), Token),
            async () => await Deliver(host, instance.InstanceId)
        };
        foreach (var call in calls)
            (await Record.ExceptionAsync(call)).Should().BeOfType<EphemeralHostStoppingException>();
        (await Record.ExceptionAsync(async () => await instance.GetStateAsync<string>(Token)))
            .Should().BeOfType<WorkflowStateTypeMismatchException>();
        (await Record.ExceptionAsync(async () => await handle.GetInstanceAsync(InstanceId.Parse(Guid.NewGuid().ToString()), Token)))
            .Should().BeOfType<WorkflowInstanceNotFoundException>();
        host.Registry.GetRequiredHandle(definition.Reference).Should().BeSameAs(handle);
        output.Should().Be("detached"); snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    internal static Task Deliver(Host host, InstanceId id) => host.Services.GetRequiredService<EphemeralWorkflowEventRouter>()
        .RouteToInstanceAsync(id, EphemeralTestEvent.Create(EventId.Create(Guid.NewGuid().ToString()), Continue.EventName,
            CorrelationId.Create("drain"), host.Clock.Now), Token).AsTask();

    private sealed class RetryBody : IStep<State>
    {
        internal int Attempts;
        public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Attempts);
            return ValueTask.FromResult<StepResult>(context.Execution.AttemptNumber == 1
                ? new StepResult.Failed(new TemporaryFailure()) : new StepResult.Completed());
        }
    }

    private sealed class TemporaryFailure() : OrcaCoreException("WF-TEST-TRANSIENT", "retry during drain");
}
