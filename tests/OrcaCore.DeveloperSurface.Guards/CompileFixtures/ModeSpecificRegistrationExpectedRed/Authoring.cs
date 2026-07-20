using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Ephemeral;

public static class Authoring
{
    public static EphemeralWorkflowDefinition<State> Ephemeral() =>
        Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .End()
            .Build();

    public static DurableWorkflowDefinition<State> Durable() =>
        Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .End()
            .Build();

    public static void Register(
        EphemeralWorkflowEngine ephemeral,
        DurableWorkflowRuntime durable,
        EphemeralWorkflowDefinition<State> ephemeralDefinition,
        DurableWorkflowDefinition<State> durableDefinition)
    {
        ephemeral.RegisterDefinition(ephemeralDefinition);
        durable.Definitions.Register(durableDefinition);
    }

    public sealed record State;
}
