using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Hosting;

public static class Consumer
{
    public static async Task<DurableWorkflowStartResult> StartAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddOrcaCore();
        await using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<DurableWorkflowRuntime>();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .End("done")
            .Build();
        return await runtime.StartOrGetAsync<string, State>("in-memory", definition, "value", cancellationToken);
    }

    public sealed record State(string Value);
}
