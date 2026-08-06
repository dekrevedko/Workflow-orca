using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class WaitAcceptanceTests
{
    private static readonly CorrelationId Correlation = CorrelationId.Create("order-123");

    [Fact]
    [Trait("AC", "AC-101")]
    public async Task Wait_EntersWaiting_WithInspectableActiveWait()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var instance = await StartWaitingAsync(provider, "wait-inspection");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        var wait = snapshot.ActiveWaits.Should().ContainSingle().Which;
        wait.EventContract.Should().Be(WorkflowEventContract.Create(
            EventName.Create("Approved"), EventContractVersion.Initial));
        wait.AuthoredLocation.Value.Should().Be("workflow:$/n:00000001");
        wait.Deadline.Should().Be(wait.RegisteredAt.AddHours(1));
    }

    [Fact]
    [Trait("AC", "AC-102")]
    public async Task MatchingEvent_ResumesExactlyOnce_WithPayload()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var instance = await StartWaitingAsync(provider, "matching-event");
        var events = provider.GetRequiredService<IWorkflowEventClient>();

        var delivery = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event("Approved", Correlation, "accepted"),
            TestContext.Current.CancellationToken);
        var resumed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        resumed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Payloads.Should().Equal(["accepted"]);
    }

    [Fact]
    [Trait("AC", "AC-103")]
    public async Task NonMatchingEvent_DoesNotResume()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var instance = await StartWaitingAsync(provider, "non-matching-event");
        var events = provider.GetRequiredService<IWorkflowEventClient>();

        var delivery = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event("Approved", CorrelationId.Create("other"), "ignored"),
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EventDeliveryStatus.NoActiveWait);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        state.Payloads.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-006")]
    public async Task ConcurrentResumeAttempts_ProduceOneSequentialOutcome()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var instance = await StartWaitingAsync(provider, "concurrent-resume");
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var first = events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event("Approved", Correlation, "first"),
            TestContext.Current.CancellationToken).AsTask();
        var second = events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event("Approved", Correlation, "second"),
            TestContext.Current.CancellationToken).AsTask();

        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        state.Payloads.Should().HaveCount(1);
    }

    private static async Task<WorkflowInstanceHandle> StartWaitingAsync(
        IServiceProvider provider,
        string idempotencyKey)
    {
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait(WorkflowEventContract.Create(EventName.Create("Approved"), EventContractVersion.Initial), _ => Correlation, TimeSpan.FromHours(1))
            .Then(context =>
            {
                if (context.ResumedEvent is { } resumedEvent)
                {
                    context.State.Payloads.Add(resumedEvent.GetPayload(
                        WorkflowEventContract<string>.Create(
                            resumedEvent.EventContract.EventName,
                            resumedEvent.EventContract.Version)));
                }

                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        return (await definitionHandle.StartOrGetAsync(
                "start",
                StartIdempotencyKey.Create(idempotencyKey),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
    }

    private static WorkflowEvent<string> Event(string name, CorrelationId correlationId, string payload)
    {
        return WorkflowEvent<string>.Create(
            EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create(name),
            correlationId,
            payload,
            DateTimeOffset.UtcNow);
    }

    public sealed class TestState
    {
        public List<string> Payloads { get; init; } = [];
    }

}
