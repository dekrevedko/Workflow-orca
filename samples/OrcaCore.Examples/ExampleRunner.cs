using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;

namespace OrcaCore.Examples;

/// <summary>
/// Runs a small application-facing workflow through the selected ephemeral engine.
/// </summary>
public static class ExampleRunner
{
    public static async Task RunAllAsync(CancellationToken cancellationToken)
    {
        var definition = Workflow.Ephemeral<CounterState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<CounterInput>(input => new CounterState(input.Value))
            .Then(context =>
            {
                context.ReplaceState(context.State with { Value = context.State.Value + 1 });
                return ValueTask.CompletedTask;
            })
            .End(
                snapshot => new CounterOutput(snapshot.Value.Value),
                WorkflowOutcomeName.Create("completed"))
            .Build();

        var services = new ServiceCollection();
        var catalog = services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        });
        catalog.AddWorkflow(definition);

        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var handle = registry.GetRequiredHandle(definition.Reference);
        var start = await handle.StartOrGetAsync(
            new CounterInput(41),
            StartIdempotencyKey.Create("examples-counter"),
            cancellationToken).ConfigureAwait(false);
        var output = await start.WaitForOutputAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine("OrcaCore typed ephemeral workflow");
        Console.WriteLine($"  definition: {definition.DefinitionId} v{definition.DefinitionVersion.Value}");
        Console.WriteLine($"  result: {output.Value}");
        Console.WriteLine("Run samples/OrcaCore.SampleHost for durable host registration.");
        Console.WriteLine("Run samples/OrcaCore.Dashboard for process-local BCL telemetry.");
    }

    private sealed record CounterInput(int Value);
    private sealed record CounterState(int Value);
    private sealed record CounterOutput(int Value);
}
