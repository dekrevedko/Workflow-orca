using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

public static class Authoring
{
    public static object Build() => Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
        .ForEach<int, State, int>(_ => [1], WorkflowPartitioner<int>.Items(), _ => new State(), branch => branch.Return(_ => 1), ForEachJoinPolicy.WhenAll, ForEachFailurePolicy.FailFast)
        .End()
        .Build();

    public sealed record State;
}
