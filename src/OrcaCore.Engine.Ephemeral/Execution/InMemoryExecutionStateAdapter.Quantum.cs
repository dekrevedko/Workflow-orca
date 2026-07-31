using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private async Task<StructuredExecutionState> RotateFiberQuantumAsync(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        WorkflowInstance<TState> instance)
    {
        var rotated = fiber with
        {
            ForcedRotationCount = checked(fiber.ForcedRotationCount + 1)
        };
        var result = execution with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
            {
                [fiber.Id] = rotated
            },
            Scheduler = FiberScheduler.CompleteTurn(
                execution.Scheduler,
                fiber.Id,
                requeueSelected: true)
        };
        instance.RecordLifecycleEvent(
            "FiberQuantumRotated",
            instruction.Path,
            LegacyWorkflowStatus.Running,
            timeProvider.GetUtcNow());
        await Task.Yield();
        return result;
    }
}
