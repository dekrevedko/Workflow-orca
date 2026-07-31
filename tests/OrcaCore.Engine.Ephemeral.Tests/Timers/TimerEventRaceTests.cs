using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Timers;

public sealed class TimerEventRaceTests
{
    private static readonly EventName Approved = EventName.Create("approved");
    private static readonly CorrelationId Correlation =
        CorrelationId.Create("order-123");

    [Fact]
    public async Task EventBeforeTimeout_ConsumesEventAndCancelsTimer()
    {
        var clock = new Clock(
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        using var provider = CreateProvider(clock);
        var definition = Definition(TimeSpan.FromMinutes(5));
        var handle = provider
            .GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("timer-event-wins"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

        var delivery = await provider
            .GetRequiredService<IWorkflowEventClient>()
            .DeliverToInstanceAsync(
                instance.InstanceId,
                Event("timer-event-wins", clock.Now, "accepted"),
                TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));
        var dueTimers = await provider
            .GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        dueTimers.Should().BeEmpty();
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await instance.GetStateAsync<RaceState>(
                TestContext.Current.CancellationToken))
            .Outcomes.Should().Equal("event:accepted");
    }

    [Fact]
    public async Task TimeoutBeforeEvent_FailsAndCancelsWait()
    {
        var clock = new Clock(
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        using var provider = CreateProvider(clock);
        var definition = Definition(TimeSpan.FromMinutes(5));
        var handle = provider
            .GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("timer-timeout-wins"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        clock.Advance(TimeSpan.FromMinutes(5));

        var fired = await provider
            .GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(
            TestContext.Current.CancellationToken);
        var late = await provider
            .GetRequiredService<IWorkflowEventClient>()
            .DeliverToInstanceAsync(
                instance.InstanceId,
                Event("timer-late-event", clock.Now, "late"),
                TestContext.Current.CancellationToken);

        fired.Should().ContainSingle();
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure.Should().NotBeNull();
        snapshot.Failure!.Code.Should().Be("WF-WAIT-TIMEOUT");
        snapshot.ActiveWaits.Should().BeEmpty();
        late.Status.Should().Be(EventDeliveryStatus.InstanceTerminal);
        (await instance.GetStateAsync<RaceState>(
                TestContext.Current.CancellationToken))
            .Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task TimedOutInstance_DoesNotPoisonSameWaitSignatureForFreshInstance()
    {
        var clock = new Clock(
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        using var provider = CreateProvider(clock);
        var definition = Definition(TimeSpan.FromMinutes(5));
        var handle = provider
            .GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var timedOut = (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("timer-first-instance"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        clock.Advance(TimeSpan.FromMinutes(5));
        _ = await provider
            .GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);

        var freshInstance = (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("timer-fresh-instance"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var afterTimeout = await timedOut.GetSnapshotAsync(
            TestContext.Current.CancellationToken);
        var fresh = await provider
            .GetRequiredService<IWorkflowEventClient>()
            .DeliverToInstanceAsync(
                freshInstance.InstanceId,
                PayloadlessEvent("timer-fresh-event", clock.Now),
                TestContext.Current.CancellationToken);

        afterTimeout.Status.Should().Be(WorkflowInstanceStatus.Failed);
        afterTimeout.Failure!.Code.Should().Be("WF-WAIT-TIMEOUT");
        fresh.Status.Should().Be(EventDeliveryStatus.Accepted);
        (await freshInstance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await freshInstance.GetStateAsync<RaceState>(
                TestContext.Current.CancellationToken))
            .Outcomes.Should().Equal("event");
    }

    private static ServiceProvider CreateProvider(Clock clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock.TimeProvider);
        services.AddTransient<RecordOutcomeStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services.BuildServiceProvider();
    }

    private static EphemeralWorkflowDefinition<string> Definition(TimeSpan timeout)
    {
        return Workflow.Ephemeral<RaceState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new RaceState([]))
            .Wait(Approved, _ => Correlation, timeout)
            .Then<RecordOutcomeStep>()
            .End()
            .Build();
    }

    private static WorkflowEvent<string> Event(
        string eventId,
        DateTimeOffset occurredAt,
        string payload)
    {
        return WorkflowEvent<string>.Create(
            EventId.Create(eventId),
            Approved,
            Correlation,
            payload,
            occurredAt);
    }

    private static WorkflowEvent PayloadlessEvent(
        string eventId,
        DateTimeOffset occurredAt) =>
        WorkflowEvent.Create(
            EventId.Create(eventId),
            Approved,
            Correlation,
            occurredAt);

    private sealed record RaceState(List<string> Outcomes);

    private sealed class RecordOutcomeStep : IStep<RaceState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RaceState> context,
            CancellationToken cancellationToken)
        {
            var outcome = context.ResumedEvent switch
            {
                null => "timeout",
                { Payload: string payload } => $"event:{payload}",
                { Payload: null } => "event",
                { Payload: var payload } => $"event-payload-type:{payload.GetType().Name}"
            };
            context.State.Outcomes.Add(outcome);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
