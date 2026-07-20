using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

public static class Consumer
{
    public static async Task<WorkflowInstanceSnapshot> RunAsync(CancellationToken cancellationToken)
    {
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .End("done")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);
        return await engine.StartAsync<string, State>(definition.DefinitionId, "minimal", cancellationToken);
    }

    public sealed record State(string Value);
}
