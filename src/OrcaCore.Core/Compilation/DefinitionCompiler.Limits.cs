using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Building;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static void ValidateOptions(DefinitionCompilerOptions options, List<ValidationError> errors)
    {
        if (options.MaxInternalInstructionsPerQuantum <= 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MaxInternalInstructionsNotPositive,
                "MaxInternalInstructionsPerQuantum must be positive.",
                "options/maxInternalInstructionsPerQuantum"));
        }

        if (options.MaxScopeDepth <= 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MaxScopeDepthNotPositive,
                "MaxScopeDepth must be positive.",
                "options/maxScopeDepth"));
        }

        if (options.MaxSerializedResultBytes <= 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MaxSerializedResultBytesNotPositive,
                "MaxSerializedResultBytes must be positive.",
                "options/maxSerializedResultBytes"));
        }

        if (options.MaxSerializedEnvelopeBytes <= 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MaxSerializedEnvelopeBytesNotPositive,
                "MaxSerializedEnvelopeBytes must be positive.",
                "options/maxSerializedEnvelopeBytes"));
        }
    }

    private static bool ContainsQuantumEndingOperation<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes)
    {
        return AllPathsContainQuantumEndingOperation(nodes, continuationEndsQuantum: false);
    }

    private static bool AllPathsContainQuantumEndingOperation<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        bool continuationEndsQuantum)
    {
        var allPathsEndQuantum = continuationEndsQuantum;
        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            allPathsEndQuantum = nodes[index] switch
            {
                SelectedStepAuthoringNode<TState> => true,
                SelectedWaitAuthoringNode<TState> => true,
                SelectedPublishAuthoringNode<TState> => true,
                SelectedDelayAuthoringNode<TState> => true,
                SelectedEndAuthoringNode<TState> => true,
                SelectedContinueAsNewAuthoringNode<TState> => true,
                SelectedStructuredScopeAuthoringNode<TState> => true,
                SelectedForEachAuthoringNode<TState> => true,
                SelectedResourceLeaseAuthoringNode<TState> lease =>
                    AllPathsContainQuantumEndingOperation(lease.Body, allPathsEndQuantum),
                SelectedIfAuthoringNode<TState> conditional =>
                    AllPathsContainQuantumEndingOperation(conditional.Then, allPathsEndQuantum) &&
                    AllPathsContainQuantumEndingOperation(conditional.Else, allPathsEndQuantum),
                SelectedWhileAuthoringNode<TState> loop =>
                    allPathsEndQuantum && ContainsQuantumEndingOperation(loop.Body),
                _ => allPathsEndQuantum
            };
        }

        return allPathsEndQuantum;
    }

    private static void ValidateConfiguredLimits<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        DefinitionCompilerOptions options,
        List<ValidationError> errors)
    {
        if (options.MaxScopeDepth > 0)
        {
            var requiredDepth = MaximumScopeDepth(nodes, currentDepth: 0);
            if (requiredDepth > options.MaxScopeDepth)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.MaxScopeDepthExceeded,
                    $"The workflow requires scope depth {requiredDepth}, exceeding configured maximum {options.MaxScopeDepth}.",
                    "root"));
            }
        }

    }

    private static int MaximumScopeDepth<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        int currentDepth)
    {
        var maximum = currentDepth;
        foreach (var node in nodes)
        {
            maximum = node switch
            {
                SelectedIfAuthoringNode<TState> conditional => Math.Max(
                    maximum,
                    Math.Max(
                        MaximumScopeDepth(conditional.Then, currentDepth),
                        MaximumScopeDepth(conditional.Else, currentDepth))),
                SelectedWhileAuthoringNode<TState> loop =>
                    Math.Max(maximum, MaximumScopeDepth(loop.Body, currentDepth)),
                SelectedResourceLeaseAuthoringNode<TState> lease =>
                    Math.Max(maximum, MaximumScopeDepth(lease.Body, checked(currentDepth + 1))),
                SelectedStructuredScopeAuthoringNode<TState> scope => Math.Max(
                    maximum,
                    MaximumBranchScopeDepth(scope.Branches, checked(currentDepth + 1))),
                SelectedForEachAuthoringNode<TState> forEach => Math.Max(
                    maximum,
                    MaximumBranchInstructionScopeDepth(forEach.Body, checked(currentDepth + 1))),
                _ => maximum
            };
        }

        return maximum;
    }

    private static int MaximumBranchScopeDepth(
        IReadOnlyList<StructuredBranchAuthoring> branches,
        int scopeDepth)
    {
        var maximum = scopeDepth;
        foreach (var branch in branches)
        {
            maximum = Math.Max(
                maximum,
                MaximumBranchInstructionScopeDepth(branch.Instructions, scopeDepth));
        }

        return maximum;
    }

    private static int MaximumBranchInstructionScopeDepth(
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        int currentDepth)
    {
        var maximum = currentDepth;
        foreach (var nested in instructions.OfType<BranchStructuredScopeAuthoringInstruction>())
        {
            maximum = Math.Max(
                maximum,
                MaximumBranchScopeDepth(nested.Branches, checked(currentDepth + 1)));
        }

        foreach (var conditional in instructions.OfType<BranchIfAuthoringInstruction>())
        {
            maximum = Math.Max(maximum, MaximumBranchInstructionScopeDepth(conditional.Then, currentDepth));
            maximum = Math.Max(maximum, MaximumBranchInstructionScopeDepth(conditional.Else, currentDepth));
        }

        foreach (var lease in instructions.OfType<BranchResourceLeaseAuthoringInstruction>())
        {
            maximum = Math.Max(
                maximum,
                MaximumBranchInstructionScopeDepth(lease.Body, checked(currentDepth + 1)));
        }

        return maximum;
    }

}
