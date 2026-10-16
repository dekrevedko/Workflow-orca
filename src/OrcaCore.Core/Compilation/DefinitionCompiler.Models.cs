using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private sealed record LoweredPlan(
        IReadOnlyList<CompiledInstruction> Instructions,
        IReadOnlyList<CompiledScopePlan> Scopes,
        IReadOnlySet<CompiledInstructionKind> AllowedInstructions);

    private static CompiledPolicyPlan CompilePolicy(WorkflowPolicySet policy)
    {
        return new CompiledPolicyPlan
        {
            Retry = policy.Retry is null
                ? null
                : new CompiledRetryPolicy(policy.Retry.MaxAttempts, policy.Retry.Backoff),
            Timeout = policy.Timeout?.Duration,
            CancellationEnabled = policy.Cancellation,
            TransientPoolKey = policy.PoolKey
        };
    }

    private static ValidationError Error(string code, string message, string path) =>
        new(code, message, path);
}
