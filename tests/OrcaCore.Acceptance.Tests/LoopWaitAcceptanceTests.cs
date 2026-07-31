using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class LoopWaitAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-109")]
    public async Task WaitInLoop_PreviousIterationEvent_CannotResumeLaterIteration()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = global::OrcaCore.Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .While(
                snapshot => snapshot.Value.Iteration < 2,
                body => body
                    .Wait(
                        EventName.Create("Tick"),
                        snapshot => IterationCorrelation(snapshot.Value.Iteration))
                    .Then(context =>
                    {
                        context.State.Payloads.Add(ReadPayload(context.ResumedEvent));
                        context.State.Iteration++;
                        return ValueTask.CompletedTask;
                    }))
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
                "start",
                StartIdempotencyKey.Create("loop-wait"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var events = provider.GetRequiredService<IWorkflowEventClient>();

        var first = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event(IterationCorrelation(0), "first"),
            TestContext.Current.CancellationToken);
        var stale = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event(IterationCorrelation(0), "stale"),
            TestContext.Current.CancellationToken);
        var second = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event(IterationCorrelation(1), "second"),
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        first.Status.Should().Be(EventDeliveryStatus.Accepted);
        stale.Status.Should().Be(EventDeliveryStatus.NoActiveWait);
        second.Status.Should().Be(EventDeliveryStatus.Accepted);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Payloads.Should().Equal(["first", "second"]);
    }

    private static CorrelationId IterationCorrelation(int iteration) =>
        CorrelationId.Create($"iteration-{iteration}");

    private static WorkflowEvent<string> Event(CorrelationId correlationId, string payload) =>
        WorkflowEvent<string>.Create(
            EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create("Tick"),
            correlationId,
            payload,
            DateTimeOffset.UtcNow);

    private static string ReadPayload(EventEnvelope? resumedEvent)
    {
        resumedEvent.Should().NotBeNull();
        resumedEvent!.PayloadContentType.Should().BeNull();
        return resumedEvent.Payload.Should().BeOfType<string>().Subject;
    }

    public sealed class TestState
    {
        public int Iteration { get; set; }

        public List<string> Payloads { get; init; } = [];
    }
}
