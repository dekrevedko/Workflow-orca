using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;

namespace OrcaCore.Integration.Tests.CurrentSurface;

public sealed class EphemeralApplicationJourneyTests
{
    [Fact]
    public async Task Application_CanStartAndObserveTypedOutputWithoutDurableIngress()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        });

        using var provider = services.BuildServiceProvider();
        var definitions = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = Workflow.Ephemeral<JourneyState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .End(
                state => new JourneyOutput(state.Value.Value + 1),
                WorkflowOutcomeName.Create("finished"))
            .Build();
        var handle = definitions.Register(definition).GetHandleOrThrow();

        var start = await handle.StartOrGetAsync(
            new JourneyInput(41),
            StartIdempotencyKey.Create("ephemeral-current-surface"),
            TestContext.Current.CancellationToken);
        var instance = start.GetHandleOrThrow();

        (await start.WaitForOutputAsync(TestContext.Current.CancellationToken))
            .Should().Be(new JourneyOutput(42));
        var completed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        completed.Outcome.Should().Be(WorkflowOutcomeName.Create("finished"));
    }

    private sealed record JourneyInput(int Value);

    private sealed record JourneyState(int Value);

    private sealed record JourneyOutput(int Value);
}
