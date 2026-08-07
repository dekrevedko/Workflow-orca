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
            .GetRequiredService<EphemeralWorkflowEventRouter>()
            .RouteToInstanceAsync(
                instance.InstanceId,
                Event("timer-event-wins", clock.Now, "accepted"),
                TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));
        await Task.Yield();

        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
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

        var snapshot = await WaitForStatusAsync(instance, WorkflowInstanceStatus.Failed);
        var late = await provider
            .GetRequiredService<EphemeralWorkflowEventRouter>()
            .RouteToInstanceAsync(
                instance.InstanceId,
                Event("timer-late-event", clock.Now, "late"),
                TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure.Should().NotBeNull();
        snapshot.Failure!.Code.Should().Be("WF-WAIT-TIMEOUT");
        snapshot.ActiveWaits.Should().BeEmpty();
        late.Status.Should().Be(EphemeralEventRouteStatus.InstanceTerminal);
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
        var afterTimeout = await WaitForStatusAsync(timedOut, WorkflowInstanceStatus.Failed);

        var freshInstance = (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("timer-fresh-instance"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var fresh = await provider
            .GetRequiredService<EphemeralWorkflowEventRouter>()
            .RouteToInstanceAsync(
                freshInstance.InstanceId,
                PayloadlessEvent("timer-fresh-event", clock.Now),
                TestContext.Current.CancellationToken);

        afterTimeout.Status.Should().Be(WorkflowInstanceStatus.Failed);
        afterTimeout.Failure!.Code.Should().Be("WF-WAIT-TIMEOUT");
        fresh.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await freshInstance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await freshInstance.GetStateAsync<RaceState>(
                TestContext.Current.CancellationToken))
            .Outcomes.Should().Equal("event");
    }

    private static async Task<WorkflowInstanceSnapshot> WaitForStatusAsync(
        WorkflowInstanceHandle instance,
        WorkflowInstanceStatus expected)
    {
        WorkflowInstanceSnapshot? latest = null;
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            latest = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
            if (latest.Status == expected && latest.ActiveWaits.Count == 0)
            {
                return latest;
            }

            await Task.Yield();
        }

        throw new InvalidOperationException(
            $"Instance '{instance.InstanceId}' did not reach '{expected}'; latest was '{latest?.Status}'.");
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
            .Wait(WorkflowEventContract.Create(Approved, EventContractVersion.Initial), _ => Correlation, timeout)
            .Then<RecordOutcomeStep>()
            .End()
            .Build();
    }

    private static EphemeralTestEvent<string> Event(
        string eventId,
        DateTimeOffset occurredAt,
        string payload)
    {
        return EphemeralTestEvent<string>.Create(
            EventId.Create(eventId),
            Approved,
            Correlation,
            payload,
            occurredAt);
    }

    private static EphemeralTestEvent PayloadlessEvent(
        string eventId,
        DateTimeOffset occurredAt) =>
        EphemeralTestEvent.Create(
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
            var outcome = context.ResumedEvent is null
                ? "timeout"
                : PayloadOutcome(context.ResumedEvent);
            context.State.Outcomes.Add(outcome);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }

        private static string PayloadOutcome(EventEnvelope envelope)
        {
            try
            {
                return $"event:{envelope.GetPayload(WorkflowEventContract<string>.Create(
                    envelope.EventContract.EventName,
                    envelope.EventContract.Version))}";
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("does not contain a payload", StringComparison.Ordinal))
            {
                return "event";
            }
        }
    }
}
