using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private static string StepThrottleObligation(
        FiberRecord fiber,
        CompiledInstruction instruction) =>
        $"step-throttle:{instruction.Id.Value}:{fiber.Id.Value}";

    private bool TryFindBlockedStepThrottle(
        StructuredExecutionState execution,
        out FiberRecord fiber,
        out CompiledInstruction instruction)
    {
        foreach (var candidate in execution.Fibers.Values
                     .Where(candidate =>
                         candidate.Phase == FiberPhase.Blocked &&
                         candidate.Blocked?.Reason == FiberBlockedReason.Resource &&
                         candidate.Blocked.ObligationId.StartsWith(
                             "step-throttle:",
                             StringComparison.Ordinal))
                     .OrderBy(candidate => candidate.Id.Value, StringComparer.Ordinal))
        {
            var candidateInstruction = plan.GetInstruction(candidate.InstructionId);
            if (candidateInstruction.Kind == CompiledInstructionKind.Step &&
                candidateInstruction.StepType is { } stepType &&
                stepThrottles.IsConfigured(stepType))
            {
                fiber = candidate;
                instruction = candidateInstruction;
                return true;
            }
        }

        fiber = null!;
        instruction = null!;
        return false;
    }

    private async Task<NoRunnableResolution> ReadmitBlockedStepThrottleAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        int commands,
        FiberRecord fiber,
        CompiledInstruction instruction,
        CancellationToken cancellationToken)
    {
        var stepType = instruction.StepType ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Blocked step throttle '{instruction.Path}' has no exact step type.");
        var owner = new StepThrottleOwner(context.InstanceId, fiber.Id);
        using var admissionCancellation = context.Processor.EnterStep(
            context.InstanceId,
            cancellationToken);
        var lease = await stepThrottles.EnterAsync(stepType, admissionCancellation.Token)
            .ConfigureAwait(false);
        var retained = false;
        try
        {
            var resumed = FiberReducer.Resume(fiber);
            execution = execution with
            {
                Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                {
                    [fiber.Id] = resumed
                },
                Scheduler = FiberScheduler.EnqueueResumed(
                    execution.Scheduler,
                    [fiber.Id])
            };
            var committed = await context.Processor.ProcessAsync(
                new DurableYieldCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    $"{instruction.Path}:step-throttle-granted",
                    BuildEnvelope(context, execution, state, ownedObligations))
                {
                    ExpectedStreamVersion = currentVersion
                },
                cancellationToken).ConfigureAwait(false);
            if (committed.Outcome != DurableCommandOutcome.Committed)
            {
                return new NoRunnableResolution(
                    execution,
                    state,
                    currentVersion,
                    commands,
                    Conflict(committed));
            }

            if (!grantedStepThrottles.TryAdd(owner, lease))
            {
                throw new InvalidOperationException(
                    $"Step-throttle admission for '{owner}' was granted more than once.");
            }

            retained = true;
            return new NoRunnableResolution(
                execution,
                state,
                committed.StreamVersion,
                checked(commands + 1));
        }
        finally
        {
            if (!retained)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
