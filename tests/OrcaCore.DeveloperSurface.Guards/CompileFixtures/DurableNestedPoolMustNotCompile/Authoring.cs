using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;

public static class Authoring
{
    public static object Build() => Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
        .Init<string>(_ => new State())
        .Parallel<int>(scope => scope.Branch<State>("branch", _ => new State(), branch => branch.WithPoolKey("cpu").Return(_ => 1)), (parent, _) => parent.Value)
        .End()
        .Build();

    public sealed record State;
}
