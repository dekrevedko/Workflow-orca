using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;

public static class Authoring
{
    public static object EphemeralRootOnlyMethods() =>
        Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Parallel<int>(scope => scope.Branch<State>("branch", _ => new State(), branch =>
            {
                branch.Init<string>(_ => new State());
                branch.End();
                branch.If(_ => true, _ => { });
                branch.While(_ => false, _ => { });
                branch.ForEach<int, State, int>();
                branch.WaitLong("approval", _ => new CorrelationId("order"));
                branch.RunChild(DefinitionId.New(), DefinitionVersion.Initial);
                branch.RunChildren(DefinitionId.New(), DefinitionVersion.Initial, _ => []);
                branch.ContinueAsNew(state => state);
                branch.RunExternalJob();
                branch.AcquireResources();
                branch.Return(_ => 1);
            }), (parent, _) => parent.Value)
            .End()
            .Build();

    public static object DurableRootAndEphemeralOnlyMethods() =>
        Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Parallel<int>(scope => scope.Branch<State>("branch", _ => new State(), branch =>
            {
                branch.Init<string>(_ => new State());
                branch.End();
                branch.If(_ => true, _ => { });
                branch.While(_ => false, _ => { });
                branch.ForEach<int, State, int>();
                branch.WaitLong("approval", _ => new CorrelationId("order"));
                branch.RunChild(DefinitionId.New(), DefinitionVersion.Initial);
                branch.RunChildren(DefinitionId.New(), DefinitionVersion.Initial, _ => []);
                branch.ContinueAsNew(state => state);
                branch.RunExternalJob();
                branch.AcquireResources();
                branch.WithPoolKey("cpu");
                branch.Return(_ => 1);
            }), (parent, _) => parent.Value)
            .End()
            .Build();

    public sealed record State;
}
