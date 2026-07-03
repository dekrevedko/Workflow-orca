using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// Composed build-time structural validators (CR-002). Each rule inspects the tree
/// independently and contributes its own errors; all errors are accumulated, never
/// short-circuited.
/// </summary>
internal static class WorkflowDefinitionValidator
{
    public static IReadOnlyList<ValidationError> Validate(bool hasInit, SequenceNode root)
    {
        var errors = new List<ValidationError>();

        if (!hasInit)
        {
            errors.Add(new ValidationError(WorkflowBuilderValidationCodes.MissingInit, "The definition has no Init step. Call Init(...) before building.", "root"));
        }

        ValidateSequence(root, "root", errors);

        if (!ContainsReachableEnd(root))
        {
            errors.Add(new ValidationError(WorkflowBuilderValidationCodes.MissingReachableEnd, "The definition has no reachable End. Call End(...) on every path.", "root"));
        }

        return errors;
    }

    private static void ValidateSequence(SequenceNode sequence, string path, List<ValidationError> errors)
    {
        for (var i = 0; i < sequence.Steps.Count; i++)
        {
            ValidateNode(sequence.Steps[i], $"{path}[{i}]", errors);
        }
    }

    private static void ValidateNode(DefinitionNode node, string path, List<ValidationError> errors)
    {
        switch (node)
        {
            case IfNode ifNode:
                if (ifNode.Condition is null)
                {
                    errors.Add(new ValidationError(WorkflowBuilderValidationCodes.NullCondition, "If condition must not be null.", path));
                }

                ValidateSequence(ifNode.Then, $"{path}.Then", errors);
                ValidateSequence(ifNode.Else, $"{path}.Else", errors);
                break;

            case WhileNode whileNode:
                if (whileNode.Condition is null)
                {
                    errors.Add(new ValidationError(WorkflowBuilderValidationCodes.NullCondition, "While condition must not be null.", path));
                }

                if (whileNode.Body.Steps.Count == 0)
                {
                    errors.Add(new ValidationError(WorkflowBuilderValidationCodes.WhileWithoutBody, "While must have a non-empty body.", path));
                }

                ValidateSequence(whileNode.Body, $"{path}.Body", errors);
                break;

            case ParallelNode parallelNode:
                if (parallelNode.Branches.Count == 0)
                {
                    errors.Add(new ValidationError(WorkflowBuilderValidationCodes.EmptyParallel, "Parallel must declare at least one branch.", path));
                }

                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var branch in parallelNode.Branches)
                {
                    if (!seenNames.Add(branch.Id.Name))
                    {
                        errors.Add(new ValidationError(WorkflowBuilderValidationCodes.DuplicateBranchName, $"Duplicate Parallel branch name '{branch.Id.Name}'.", path));
                    }

                    ValidateSequence(branch.Body, $"{path}.{branch.Id.Name}", errors);
                }

                break;

            case WaitNode waitNode when waitNode.SelectCorrelationId is null:
                errors.Add(new ValidationError(WorkflowBuilderValidationCodes.NullDelegate, "Wait correlation selector must not be null.", path));
                break;
        }
    }

    private static bool ContainsReachableEnd(SequenceNode sequence)
    {
        foreach (var step in sequence.Steps)
        {
            switch (step)
            {
                case EndNode:
                    return true;
                case IfNode ifNode when ContainsReachableEnd(ifNode.Then) && ContainsReachableEnd(ifNode.Else):
                    return true;
                case ParallelNode parallelNode when parallelNode.Branches.Count > 0 &&
                    parallelNode.Branches.All(branch => ContainsReachableEnd(branch.Body)):
                    return true;
            }
        }

        return false;
    }
}
