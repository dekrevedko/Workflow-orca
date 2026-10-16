using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

// Preserved as source-only evidence for the deferred broad-management surface. The v1
// acceptance project excludes this file because AC-516 requires that surface to be absent.
public sealed class LegacyManagementAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-516")]
    public async Task BroadDestructiveSelection_RequiresExplicitSafety()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var engine = provider.GetRequiredService<EphemeralWorkflowEngine>();
        var definition = WaitingDefinition();
        _ = await StartAsync(provider, definition, "broad-destructive-safety",
            "wait",
            TestContext.Current.CancellationToken);

        var unsafeTerminate = () => engine.Management.All()
            .TerminateAsync(TestContext.Current.CancellationToken);
        var safeTerminate = await engine.Management.All()
            .TerminateAsync(DestructiveCommandSafety.Confirmed, TestContext.Current.CancellationToken);

        await unsafeTerminate.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*explicit safety*");
        safeTerminate.AffectedCount.Should().Be(1);
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
