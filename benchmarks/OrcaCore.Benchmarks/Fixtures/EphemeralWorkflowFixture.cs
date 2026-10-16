using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Benchmarks.Fixtures;

internal static class EphemeralWorkflowFixture
{
    internal static EphemeralWorkflowDefinition<int, int> BuildDefinition(int stepCount)
    {
        var builder = Workflow.Ephemeral<EphemeralBenchmarkState>(
                DeterministicIds.Definition(50_000 + stepCount),
                DefinitionVersion.Initial)
            .Init<int>(_ => new EphemeralBenchmarkState());
        for (var index = 0; index < stepCount; index++)
        {
            builder.Then<IncrementStep>();
        }

        return builder
            .End(snapshot => snapshot.Value.Count)
            .Build();
    }
}

public sealed class EphemeralBenchmarkState
{
    public int Count { get; set; }
}

public sealed class IncrementStep : IStep<EphemeralBenchmarkState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<EphemeralBenchmarkState> context,
        CancellationToken cancellationToken)
    {
        context.State.Count++;
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
