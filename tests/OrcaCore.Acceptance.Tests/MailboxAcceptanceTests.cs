using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class MailboxAcceptanceTests
{
    private static readonly CorrelationId FirstCorrelation = CorrelationId.Create("first");
    private static readonly CorrelationId SecondCorrelation = CorrelationId.Create("second");

    [Fact]
    public async Task OutOfOrderEvent_IsNotConsumedAndTheSameEnvelopeCanBeRedelivered()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definitionHandle = Register(provider, TwoWaitDefinition());
        var instance = (await definitionHandle.StartOrGetAsync(
                "start",
                StartIdempotencyKey.Create("out-of-order-redelivery"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();
        var second = Event("Second", SecondCorrelation, "early");

        var early = await events.RouteToInstanceAsync(
            instance.InstanceId,
            second,
            TestContext.Current.CancellationToken);
        var first = await events.RouteToInstanceAsync(
            instance.InstanceId,
            Event("First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);
        var redelivery = await events.RouteToInstanceAsync(
            instance.InstanceId,
            second,
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        early.Status.Should().Be(ProcessLocalEventRouteStatus.NoActiveWait);
        first.Status.Should().Be(ProcessLocalEventRouteStatus.Accepted);
        redelivery.Status.Should().Be(ProcessLocalEventRouteStatus.Accepted);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Payloads.Should().Equal(["first", "early"]);
    }

    [Fact]
    [Trait("AC", "AC-105")]
    public async Task DuplicateAcceptedEvent_ProducesOneConsumptionAndContinuation()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definitionHandle = Register(provider, OneWaitDefinition());
        var instance = (await definitionHandle.StartOrGetAsync(
                "start",
                StartIdempotencyKey.Create("duplicate-event"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();
        var acceptedEvent = Event("First", FirstCorrelation, "accepted");

        var accepted = await events.RouteToInstanceAsync(
            instance.InstanceId,
            acceptedEvent,
            TestContext.Current.CancellationToken);
        var duplicate = await events.RouteToInstanceAsync(
            instance.InstanceId,
            acceptedEvent,
            TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        accepted.Status.Should().Be(ProcessLocalEventRouteStatus.Accepted);
        duplicate.Status.Should().Be(ProcessLocalEventRouteStatus.Duplicate);
        state.Payloads.Should().Equal(["accepted"]);
    }

    private static EphemeralDefinitionHandle<string> Register(
        IServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition) =>
        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

    private static EphemeralWorkflowDefinition<string> OneWaitDefinition() =>
        global::OrcaCore.Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait(WorkflowEventContract.Create(EventName.Create("First"), EventContractVersion.Initial), _ => FirstCorrelation)
            .Then(CapturePayload)
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<string> TwoWaitDefinition() =>
        global::OrcaCore.Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait(WorkflowEventContract.Create(EventName.Create("First"), EventContractVersion.Initial), _ => FirstCorrelation)
            .Then(CapturePayload)
            .Wait(WorkflowEventContract.Create(EventName.Create("Second"), EventContractVersion.Initial), _ => SecondCorrelation)
            .Then(CapturePayload)
            .End()
            .Build();

    private static ValueTask CapturePayload(StepContext<TestState> context)
    {
        context.State.Payloads.Add(ReadPayload(context.ResumedEvent));
        return ValueTask.CompletedTask;
    }

    private static ProcessLocalInboundEvent<string> Event(
        string name,
        CorrelationId correlationId,
        string payload) =>
        ProcessLocalInboundEvent<string>.Create(
            EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create(name),
            correlationId,
            payload,
            DateTimeOffset.UtcNow);

    private static string ReadPayload(EventEnvelope? resumedEvent)
    {
        resumedEvent.Should().NotBeNull();
        return resumedEvent!.GetPayload(WorkflowEventContract<string>.Create(
            resumedEvent.EventContract.EventName,
            resumedEvent.EventContract.Version));
    }

    public sealed class TestState
    {
        public List<string> Payloads { get; init; } = [];
    }
}
