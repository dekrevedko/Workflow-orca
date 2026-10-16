using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private async Task<DurableSegmentResult> CommitForcedRotationAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        var rotated = fiber with
        {
            ForcedRotationCount = checked(fiber.ForcedRotationCount + 1)
        };
        execution = execution with
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
        var commit = await context.Processor.ProcessAsync(
            new DurableYieldCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                $"{instruction.Path}:internal-quantum",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion
            },
            cancellationToken).ConfigureAwait(false);
        return commit.Outcome == DurableCommandOutcome.Committed
            ? DurableSegmentResult.PolicyBoundary
            : Conflict(commit);
    }
}
