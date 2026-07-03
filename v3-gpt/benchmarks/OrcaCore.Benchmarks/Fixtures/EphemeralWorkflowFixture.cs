using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Benchmarks.Fixtures;

internal static class EphemeralWorkflowFixture
{
    internal static WorkflowDefinition<EphemeralBenchmarkState> BuildDefinition(int stepCount)
    {
        var builder = new WorkflowBuilder<EphemeralBenchmarkState>()
            .Init<int>(_ => new EphemeralBenchmarkState());
        for (var index = 0; index < stepCount; index++)
        {
            builder.Then<IncrementStep>();
        }

        return builder
            .End()
            .Build(DeterministicIds.Definition(50_000 + stepCount), DefinitionVersion.Initial);
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
