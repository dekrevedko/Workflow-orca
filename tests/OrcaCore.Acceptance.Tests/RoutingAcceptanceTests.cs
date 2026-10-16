using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class RoutingAcceptanceTests
{
    private static readonly CorrelationId Correlation = CorrelationId.Create("shared");

    [Fact]
    [Trait("AC", "AC-106")]
    public async Task CorrelationTargetedEvent_ResumesExactlyOne()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definitionHandle = Register(provider, Definition());
        var instance = (await definitionHandle.StartOrGetAsync(
                "start",
                StartIdempotencyKey.Create("correlation-one"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();

        var delivery = await events.RouteByCorrelationAsync(
            definitionHandle.DefinitionId,
            Event("payload"),
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(ProcessLocalEventRouteStatus.Accepted);
        delivery.InstanceId.Should().Be(instance.InstanceId);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Payloads.Should().Equal(["payload"]);
    }

    [Fact]
    [Trait("AC", "AC-107")]
    public async Task CorrelationPair_RejectsSecondActiveWaitBeforeDelivery()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definitionHandle = Register(provider, Definition());
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();

        var missing = await events.RouteByCorrelationAsync(
            definitionHandle.DefinitionId,
            Event("missing"),
            TestContext.Current.CancellationToken);

        missing.Status.Should().Be(ProcessLocalEventRouteStatus.NoActiveWait);
        missing.InstanceId.Should().BeNull();

        var first = (await definitionHandle.StartOrGetAsync(
            "first",
            StartIdempotencyKey.Create("ambiguous-first"),
            TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var ambiguous = async () => await definitionHandle.StartOrGetAsync(
            "second",
            StartIdempotencyKey.Create("ambiguous-second"),
            TestContext.Current.CancellationToken);

        await ambiguous.Should().ThrowAsync<AmbiguousWaitRegistrationException>();

        var accepted = await events.RouteByCorrelationAsync(
            definitionHandle.DefinitionId,
            Event("accepted"),
            TestContext.Current.CancellationToken);

        accepted.Status.Should().Be(ProcessLocalEventRouteStatus.Accepted);
        accepted.InstanceId.Should().Be(first.InstanceId);
    }

    [Fact]
    public async Task CorrelationDelivery_IsScopedToTheTargetDefinition()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var target = Register(provider, Definition());
        var other = Register(provider, Definition());
        var targetInstance = (await target.StartOrGetAsync(
                "target",
                StartIdempotencyKey.Create("target-definition"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var otherInstance = (await other.StartOrGetAsync(
                "other",
                StartIdempotencyKey.Create("other-definition"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();

        var delivery = await events.RouteByCorrelationAsync(
            target.DefinitionId,
            Event("payload"),
            TestContext.Current.CancellationToken);
        var targetState = await targetInstance.GetStateAsync<TestState>(
            TestContext.Current.CancellationToken);
        var otherState = await otherInstance.GetStateAsync<TestState>(
            TestContext.Current.CancellationToken);
        var otherSnapshot = await otherInstance.GetSnapshotAsync(
            TestContext.Current.CancellationToken);

        delivery.InstanceId.Should().Be(targetInstance.InstanceId);
        targetState.Payloads.Should().Equal(["payload"]);
        otherState.Payloads.Should().BeEmpty();
        otherSnapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
    }

    private static EphemeralDefinitionHandle<string> Register(
        IServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition) =>
        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

    private static EphemeralWorkflowDefinition<string> Definition() =>
        global::OrcaCore.Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait(WorkflowEventContract.Create(EventName.Create("Approved"), EventContractVersion.Initial), _ => Correlation)
            .Then(context =>
            {
                context.State.Payloads.Add(ReadPayload(context.ResumedEvent));
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();

    private static ProcessLocalInboundEvent<string> Event(string payload) =>
        ProcessLocalInboundEvent<string>.Create(
            EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create("Approved"),
            Correlation,
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
