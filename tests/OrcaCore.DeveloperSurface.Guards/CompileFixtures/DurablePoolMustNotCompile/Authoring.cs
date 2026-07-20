using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;

public static class Authoring
{
    public static object Build() => Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
        .Init<string>(_ => new State())
        .WithPoolKey("cpu")
        .End()
        .Build();

    public sealed record State;
}
