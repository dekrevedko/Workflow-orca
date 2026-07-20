using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;

public static class Authoring
{
    public static object Build() => Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
        .Init<string>(_ => new State())
        .WaitLong("approval", _ => new CorrelationId("order"))
        .End()
        .Build();

    public sealed record State;
}
