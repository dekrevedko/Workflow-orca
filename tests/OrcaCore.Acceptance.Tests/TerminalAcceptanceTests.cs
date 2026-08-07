using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class TerminalAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-011")]
    public async Task CompletionBridge_ReturnsTerminalSnapshotWithoutLiveState()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = CompletedDefinition();
        var instance = await StartAsync(provider, definition, "completion-bridge",
            "done",
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    [Fact]
    [Trait("AC", "AC-012")]
    public async Task NamedEndOutcome_IsRecordedAndQueryable()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new TestState { Name = input })
            .End(WorkflowOutcomeName.Create("Approved"))
            .Build();
        var instance = await StartAsync(provider, definition, "named-outcome",
            "done",
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Outcome.Should().Be(WorkflowOutcomeName.Create("Approved"));
    }

    [Fact]
    [Trait("AC", "AC-010")]
    [Trait("AC", "AC-014")]
    public async Task GracefulCancel_CancelsInFlightWorkAndActiveWaits()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = WaitingDefinition();
        var instance = await StartAsync(provider, definition, "graceful-cancel",
            "wait",
            TestContext.Current.CancellationToken);

        var request = await instance.RequestCancellationAsync(TestContext.Current.CancellationToken);
        var cancelled = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        request.Should().Be(WorkflowCancellationRequestStatus.Requested);
        cancelled.Status.Should().Be(WorkflowInstanceStatus.Cancelled);
        cancelled.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-015")]
    public async Task ForcedTerminate_PreventsFurtherAdvancement()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = WaitingDefinition();
        var instance = await StartAsync(provider, definition, "forced-terminate",
            "wait",
            TestContext.Current.CancellationToken);
        var events = provider.GetRequiredService<ProcessLocalEventRouter>();

        var termination = await instance.TerminateAsync(TestContext.Current.CancellationToken);
        var delivery = await events.RouteToInstanceAsync(
            instance.InstanceId,
            Event("Ready", "wait"),
            TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        termination.Should().Be(WorkflowTerminationStatus.Terminated);
        delivery.Status.Should().Be(ProcessLocalEventRouteStatus.InstanceTerminal);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Terminated);
    }

    [Fact]
    [Trait("AC", "AC-005")]
    public async Task TerminalInstances_RejectIllegalTriggers()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = CompletedDefinition();
        var instance = await StartAsync(provider, definition, "terminal-trigger",
            "done",
            TestContext.Current.CancellationToken);

        var cancellation = await instance.RequestCancellationAsync(TestContext.Current.CancellationToken);
        var termination = await instance.TerminateAsync(TestContext.Current.CancellationToken);

        cancellation.Should().Be(WorkflowCancellationRequestStatus.AlreadyTerminal);
        termination.Should().Be(WorkflowTerminationStatus.AlreadyTerminal);
    }

    private static EphemeralWorkflowDefinition<string> CompletedDefinition()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new TestState { Name = input })
            .End()
            .Build();
    }

    private static EphemeralWorkflowDefinition<string> WaitingDefinition()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new TestState { Name = input })
            .Wait(WorkflowEventContract.Create(EventName.Create("Ready"), EventContractVersion.Initial), state => CorrelationId.Create(state.Value.Name))
            .Then(_ => ValueTask.CompletedTask)
            .End()
            .Build();
    }

    private static ProcessLocalInboundEvent Event(string eventName, string correlationId)
    {
        return ProcessLocalInboundEvent.Create(
            EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create(eventName),
            CorrelationId.Create(correlationId),
            DateTimeOffset.UtcNow);
    }

    private static async Task<WorkflowInstanceHandle> StartAsync(
        IServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition,
        string idempotencyKey,
        string input,
        CancellationToken cancellationToken)
    {
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        return (await definitionHandle.StartOrGetAsync(
                input,
                StartIdempotencyKey.Create(idempotencyKey),
                cancellationToken))
            .GetHandleOrThrow();
    }

    public sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
    }

}
