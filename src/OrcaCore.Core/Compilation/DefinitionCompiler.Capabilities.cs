using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Building;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static void ValidateCapabilities<TState>(
        WorkflowExecutionMode mode,
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        string path,
        List<ValidationError> errors)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var nodePath = $"{path}/{index}";
            if (node is SelectedForEachAuthoringNode<TState> && mode != WorkflowExecutionMode.Ephemeral)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.UnsupportedInstruction,
                    "ForEach is not supported by the durable selected-mode compiler.",
                    nodePath));
            }

            if (node is SelectedContinueAsNewAuthoringNode<TState> && mode != WorkflowExecutionMode.Durable)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.UnsupportedInstruction,
                    "ContinueAsNew is not supported by the ephemeral selected-mode compiler.",
                    nodePath));
            }

            if (node is SelectedRunChildAuthoringNode<TState> or SelectedRunChildrenAuthoringNode<TState> &&
                mode != WorkflowExecutionMode.Durable)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.UnsupportedInstruction,
                    "Child workflows are not supported by the ephemeral selected-mode compiler.",
                    nodePath));
            }

            if (node is SelectedStepAuthoringNode<TState> { Policies.PoolKey: not null } &&
                mode != WorkflowExecutionMode.Ephemeral)
            {
                AddTransientPoolError(nodePath, errors);
            }

            switch (node)
            {
                case SelectedIfAuthoringNode<TState> conditional:
                    ValidateCapabilities(mode, conditional.Then, $"{nodePath}/then", errors);
                    ValidateCapabilities(mode, conditional.Else, $"{nodePath}/else", errors);
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    ValidateCapabilities(mode, loop.Body, $"{nodePath}/body", errors);
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    ValidateBranchCapabilities(mode, scope.Branches, nodePath, errors);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    ValidateBranchInstructionCapabilities(mode, forEach.Body, $"{nodePath}/item", errors);
                    break;
            }
        }
    }

    private static void ValidateBranchCapabilities(
        WorkflowExecutionMode mode,
        IReadOnlyList<StructuredBranchAuthoring> branches,
        string path,
        List<ValidationError> errors)
    {
        for (var index = 0; index < branches.Count; index++)
        {
            ValidateBranchInstructionCapabilities(
                mode,
                branches[index].Instructions,
                $"{path}/branches/{index}",
                errors);
        }
    }

    private static void ValidateBranchInstructionCapabilities(
        WorkflowExecutionMode mode,
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        string path,
        List<ValidationError> errors)
    {
        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            var instructionPath = $"{path}/{index}";
            if (instruction is BranchStepAuthoringInstruction { Policies.PoolKey: not null } &&
                mode != WorkflowExecutionMode.Ephemeral)
            {
                AddTransientPoolError(instructionPath, errors);
            }

            if (instruction is BranchStructuredScopeAuthoringInstruction nested)
            {
                ValidateBranchCapabilities(mode, nested.Branches, instructionPath, errors);
            }
        }
    }

    private static void AddTransientPoolError(string path, List<ValidationError> errors)
    {
        errors.Add(Error(
            DefinitionCompilerCodes.UnsupportedInstruction,
            "Transient in-process pool policies are not supported by the durable selected-mode compiler.",
            path));
    }
}
