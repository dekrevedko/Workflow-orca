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

        if (options.MaxActiveFibers <= 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MaxActiveFibersNotPositive,
                "MaxActiveFibers must be positive.",
                "options/maxActiveFibers"));
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
                SelectedDelayAuthoringNode<TState> => true,
                SelectedRunChildAuthoringNode<TState> => true,
                SelectedRunChildrenAuthoringNode<TState> => true,
                SelectedEndAuthoringNode<TState> => true,
                SelectedContinueAsNewAuthoringNode<TState> => true,
                SelectedStructuredScopeAuthoringNode<TState> => true,
                SelectedForEachAuthoringNode<TState> => true,
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

        if (options.MaxActiveFibers > 0)
        {
            var requiredFibers = MaximumActiveFibers(nodes, currentActiveFibers: 1);
            if (requiredFibers > options.MaxActiveFibers)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.MaxActiveFibersExceeded,
                    $"The workflow can require {requiredFibers} active fibers, exceeding configured maximum {options.MaxActiveFibers}.",
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

        return maximum;
    }

    private static int MaximumActiveFibers<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        int currentActiveFibers)
    {
        var maximum = currentActiveFibers;
        foreach (var node in nodes)
        {
            maximum = node switch
            {
                SelectedIfAuthoringNode<TState> conditional => Math.Max(
                    maximum,
                    Math.Max(
                        MaximumActiveFibers(conditional.Then, currentActiveFibers),
                        MaximumActiveFibers(conditional.Else, currentActiveFibers))),
                SelectedWhileAuthoringNode<TState> loop =>
                    Math.Max(maximum, MaximumActiveFibers(loop.Body, currentActiveFibers)),
                SelectedStructuredScopeAuthoringNode<TState> scope => Math.Max(
                    maximum,
                    MaximumBranchActiveFibers(
                        scope.Branches,
                        checked(currentActiveFibers + scope.Branches.Count))),
                SelectedForEachAuthoringNode<TState> forEach when forEach.MaxConcurrency is { } admitted => Math.Max(
                    maximum,
                    MaximumBranchInstructionActiveFibers(
                        forEach.Body,
                        checked(currentActiveFibers + admitted))),
                SelectedForEachAuthoringNode<TState> forEach => Math.Max(
                    maximum,
                    MaximumBranchInstructionActiveFibers(
                        forEach.Body,
                        checked(currentActiveFibers + 1))),
                _ => maximum
            };
        }

        return maximum;
    }

    private static int MaximumBranchActiveFibers(
        IReadOnlyList<StructuredBranchAuthoring> branches,
        int currentActiveFibers)
    {
        var maximum = currentActiveFibers;
        foreach (var branch in branches)
        {
            maximum = Math.Max(
                maximum,
                MaximumBranchInstructionActiveFibers(branch.Instructions, currentActiveFibers));
        }

        return maximum;
    }

    private static int MaximumBranchInstructionActiveFibers(
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        int currentActiveFibers)
    {
        var maximum = currentActiveFibers;
        foreach (var nested in instructions.OfType<BranchStructuredScopeAuthoringInstruction>())
        {
            maximum = Math.Max(
                maximum,
                MaximumBranchActiveFibers(
                    nested.Branches,
                    checked(currentActiveFibers + nested.Branches.Count)));
        }

        return maximum;
    }
}
