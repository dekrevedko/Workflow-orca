using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Ownership;

[Trait("Slice", "Issue1Task3.2")]
[Trait("Disposition", "Implemented")]
public sealed class ContinuationOwnershipTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);
    private static readonly WorkflowEventContract Continue = WorkflowEventContract.Create(
        EventName.Create("continuation-ownership"), EventContractVersion.Initial);

    public static TheoryData<bool, string, bool> CallbackCases
    {
        get
        {
            var cases = new TheoryData<bool, string, bool>();
            foreach (var bounded in new[] { false, true })
            foreach (var kind in new[] { "retry", "resource", "timer" })
            foreach (var bulk in new[] { false, true }) cases.Add(bounded, kind, bulk);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(CallbackCases))]
    public async Task TerminalUnit_WaitsForRetryGrantOrTimerCompletion_ThenCannotBeResurrected(
        bool bounded, string kind, bool bulk)
    {
        var workKind = kind switch
        {
            "retry" => EphemeralWorkKind.RetryContinuation,
            "resource" => EphemeralWorkKind.ResourceContinuation,
            _ => EphemeralWorkKind.TimerDispatch
        };
        var completion = new CompletionGate(workKind);
        var body = new BlockingBody();
        using var host = new Host(bounded, completion, body);
        IWorkflowInstance? runtime = null;
        Task<EphemeralEventRouteResult>? holderDelivery = null;
        var retryAttempts = 0;
        try
        {
            EphemeralWorkflowDefinition<string> definition;
            if (kind == "retry")
            {
                definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
                    .Init<string>(key => new State(key))
                    .Then(_ =>
                    {
                        Interlocked.Increment(ref retryAttempts);
                        throw new InvalidOperationException("retryable boundary");
                    })
                    .WithRetry(2, TimeSpan.FromHours(1)).End().Build();
            }
            else if (kind == "resource")
            {
                definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
                    .Init<string>(key => new State(key))
                    .Wait(Continue, state => CorrelationId.Create(state.Value.Key))
                    .Then<BlockingBody>().End().Build();
            }
            else
            {
                definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
                    .Init<string>(key => new State(key)).Delay(TimeSpan.FromSeconds(30)).End().Build();
            }

            var handle = host.Registry.Register(definition).GetHandleOrThrow();
            if (kind == "resource")
            {
                var holder = await host.Start(handle, "holder");
                holderDelivery = host.Deliver(holder, "holder");
                await body.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            }
            var instance = await host.Start(handle, "victim");
            host.Engine.TryGetFacadeInstance(instance.InstanceId, out runtime).Should().BeTrue();
            if (kind == "resource")
            {
                (await host.Deliver(instance, "victim")).Status.Should().Be(EphemeralEventRouteStatus.Accepted);
                await host.GovernanceWait.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
                host.Router.AcceptedEntryCount(instance.InstanceId).Should().Be(1);
            }
            if (kind == "timer") host.Clock.Advance(TimeSpan.FromSeconds(30));
            else await instance.TerminateAsync(TestContext.Current.CancellationToken);

            (await completion.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken))
                .Should().Be(instance.InstanceId);
            var terminal = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
            terminal.Status.Should().Be(kind == "timer" ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Terminated);
            runtime!.Lifetime.WhenSettled.IsCompleted.Should().BeFalse();
            (bulk ? host.Engine.EvictTerminalInstances() : host.Engine.EvictInstance(instance.InstanceId) ? 1 : 0)
                .Should().Be(0, "the real continuation/dispatch still owns its completion callback");

            completion.Release.TrySetResult();
            await runtime.Lifetime.WhenSettled.WaitAsync(Guard, TestContext.Current.CancellationToken);
            (bulk ? host.Engine.EvictTerminalInstances() : host.Engine.EvictInstance(instance.InstanceId) ? 1 : 0)
                .Should().Be(1);
            host.Router.AcceptedEntryCount(instance.InstanceId).Should().Be(0);
            host.Registry.TryGetInstance(instance.InstanceId, out _).Should().BeFalse();

            // A stale owner cannot reinsert the released runtime, even through the actual storage seam.
            var immutable = runtime.ToSnapshot();
            ((WorkflowInstance<State>)runtime).Complete("stale-completion", host.Clock.Now);
            runtime.ToSnapshot().Status.Should().Be(immutable.Status);
            runtime.ToSnapshot().EndOutcomeName.Should().Be(immutable.EndOutcomeName);
            host.Storage.Save((WorkflowInstance<State>)runtime);
            host.Storage.TryGet(instance.InstanceId, out _).Should().BeFalse();
            host.Clock.Advance(TimeSpan.FromHours(2));
            runtime.ToSnapshot().Status.Should().Be(terminal.Status);
            host.Storage.TryGet(instance.InstanceId, out _).Should().BeFalse();
            if (kind == "retry") retryAttempts.Should().Be(1);
            if (kind == "resource") body.Attempts.Should().Be(1, "the cancelled grant must not dispatch a late victim body");
        }
        finally
        {
            completion.Release.TrySetResult();
            body.Release.TrySetResult();
            if (runtime is not null && !runtime.Lifetime.IsTerminal)
                await host.Engine.RequestTerminationAsync(runtime.InstanceId, TestContext.Current.CancellationToken);
            if (runtime is not null)
                await runtime.Lifetime.WhenSettled.WaitAsync(Guard, TestContext.Current.CancellationToken);
            if (holderDelivery is not null)
                await holderDelivery.WaitAsync(Guard, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedInstance_CannotBeAbortedByTheProvisionalCleanupPath(bool bounded)
    {
        using var host = new Host(bounded, new CompletionGate(EphemeralWorkKind.TimerDispatch), new BlockingBody());
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(key => new State(key)).Wait(Continue, state => CorrelationId.Create(state.Value.Key)).End().Build();
        var handle = host.Registry.Register(definition).GetHandleOrThrow();
        var instance = await host.Start(handle, "accepted");
        await host.Engine.AbortProvisionalStartAsync(instance.InstanceId);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(WorkflowInstanceStatus.Waiting);
        var replay = await handle.StartOrGetAsync("accepted", StartIdempotencyKey.Create("accepted"), TestContext.Current.CancellationToken);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>().Which.WasExisting.Should().BeTrue();
        replay.GetHandleOrThrow().InstanceId.Should().Be(instance.InstanceId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ProvisionalAbort_ReportsInternalLifecycleWithoutAnOperatorTermination(
        bool bounded, bool cancel)
    {
        using var host = new Host(bounded, new CompletionGate(EphemeralWorkKind.TimerDispatch), new BlockingBody());
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(key => new State(key)).End().Build();
        var observed = new ConcurrentQueue<(string Metric, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Name is "orca.commands.processed" or "orca.commands.duration" or "orca.lifecycle.events")
                current.EnableMeasurementEvents(instrument);
        };
        void Capture(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var values = tags.ToArray();
            if (values.Any(tag => tag.Key == "orca.definition.id" && Equals(tag.Value, definition.DefinitionId.ToString())))
                observed.Enqueue((instrument.Name, values));
        }
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Capture(instrument, tags));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Capture(instrument, tags));
        listener.Start();
        using var caller = new CancellationTokenSource();
        var original = new InvalidOperationException("original post-Init failure");
        var initialized = false;
        host.Engine.InstanceCommitted += _ =>
        {
            if (initialized) return;
            initialized = true;
            if (cancel) caller.Cancel();
            else throw original;
        };
        var handle = host.Registry.Register(definition).GetHandleOrThrow();
        var error = await Record.ExceptionAsync(async () => await handle.StartOrGetAsync(
            "abort", StartIdempotencyKey.Create("abort"), caller.Token));
        if (cancel) error.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(caller.Token);
        else error.Should().BeSameAs(original);
        observed.Where(item => item.Metric is "orca.commands.processed" or "orca.commands.duration")
            .Should().NotContain(item => item.Tags.Any(tag => tag.Key == "orca.command.type" && Equals(tag.Value, "request_termination")));
        observed.Where(item => item.Metric == "orca.lifecycle.events" &&
                               item.Tags.Any(tag => tag.Key == "orca.lifecycle.origin" && Equals(tag.Value, "provisional-abort")))
            .Should().ContainSingle();
        host.Storage.List().Should().BeEmpty();
        host.Registry.ListInstances().Should().BeEmpty();
    }

    public sealed record State(string Key);

    private sealed class CompletionGate(EphemeralWorkKind selected)
    {
        internal TaskCompletionSource<InstanceId> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ValueTask Observe(InstanceId id, EphemeralWorkKind kind)
        {
            if (kind != selected) return ValueTask.CompletedTask;
            Entered.TrySetResult(id);
            return new ValueTask(Release.Task);
        }
    }

    private sealed class BlockingBody : IStep<State>
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Attempts { get; private set; }
        public async ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
        {
            Attempts++;
            Entered.TrySetResult();
            await Release.Task;
            return new StepResult.Completed();
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly ServiceProvider services;
        internal Clock Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        internal InMemoryInstanceRegistry Storage { get; } = new();
        internal EphemeralWorkflowEngine Engine { get; }
        internal EphemeralWorkflowDefinitionRegistry Registry { get; }
        internal EphemeralWorkflowEventRouter Router { get; }
        internal TaskCompletionSource GovernanceWait { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Host(bool bounded, CompletionGate completion, BlockingBody body)
        {
            services = new ServiceCollection().AddSingleton(body).BuildServiceProvider();
            Engine = new EphemeralWorkflowEngine(Clock.TimeProvider, new EphemeralWorkflowEngineOptions
            {
                TerminalRetention = bounded ? new EphemeralTerminalRetentionSettings(1, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)) : null,
                WorkCompletionPending = completion.Observe,
                StepThrottles = new Dictionary<Type, int> { [typeof(BlockingBody)] = 1 },
                GovernanceWaitStarting = () => GovernanceWait.TrySetResult()
            }, Storage, new InstanceExecutionLane(), services);
            Registry = new EphemeralWorkflowDefinitionRegistry(Engine);
            Router = new EphemeralWorkflowEventRouter(Engine, Registry);
        }

        internal async Task<WorkflowInstanceHandle> Start(EphemeralDefinitionHandle<string> handle, string key) =>
            (await handle.StartOrGetAsync(key, StartIdempotencyKey.Create(key), TestContext.Current.CancellationToken)).GetHandleOrThrow();

        internal Task<EphemeralEventRouteResult> Deliver(WorkflowInstanceHandle instance, string key) =>
            Router.RouteToInstanceAsync(instance.InstanceId,
                EphemeralTestEvent.Create(EventId.Create($"event-{key}"), Continue.EventName,
                    CorrelationId.Create(key), Clock.Now), TestContext.Current.CancellationToken).AsTask();

        public void Dispose() { Engine.Dispose(); services.Dispose(); }
    }
}
