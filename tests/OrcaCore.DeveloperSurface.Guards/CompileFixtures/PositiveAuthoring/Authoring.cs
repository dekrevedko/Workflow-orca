using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;

public static class Authoring
{
    public static object Ephemeral() => Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
        .Init<string>(_ => new State())
        .WithPoolKey("root-cpu")
        .Then<NoOpStep>()
        .Parallel<int>(
            scope => scope.Branch<State>(
                "branch",
                _ => new State(),
                branch => branch.WithPoolKey("branch-cpu").Then<NoOpStep>().Return(_ => 1)),
            (parent, _) => parent.Value)
        .End()
        .Build();

    public static object Durable() => Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
        .Init<string>(_ => new State())
        .If(_ => true, nested => nested.WaitLong("approved", _ => new CorrelationId("42")))
        .End()
        .Build();

    public sealed record State;

    public sealed class NoOpStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
