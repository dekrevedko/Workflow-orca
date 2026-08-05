using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Building;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static void ValidateCapabilities<TState>(
        WorkflowExecutionMode mode,
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        string path,
        List<ValidationError> errors,
        string? activeLeasePath = null)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var nodePath = $"{path}/{index}";
            if (node is SelectedContinueAsNewAuthoringNode<TState> && activeLeasePath is not null)
            {
                errors.Add(new ValidationError(
                    DefinitionCompilerCodes.LeaseBlocksContinueAsNew,
                    "ContinueAsNew cannot be reached before the active resource lease exits.",
                    nodePath,
                    activeLeasePath));
            }

            if (node is SelectedContinueAsNewAuthoringNode<TState> && mode != WorkflowExecutionMode.Durable)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.UnsupportedInstruction,
                    "ContinueAsNew is not supported by the ephemeral selected-mode compiler.",
                    nodePath));
            }

            if (node is SelectedResourceLeaseAuthoringNode<TState> && mode != WorkflowExecutionMode.Durable)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.UnsupportedInstruction,
                    "AcquireResources is not supported by the ephemeral selected-mode compiler.",
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
                    ValidateCapabilities(mode, conditional.Then, $"{nodePath}/then", errors, activeLeasePath);
                    ValidateCapabilities(mode, conditional.Else, $"{nodePath}/else", errors, activeLeasePath);
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    ValidateCapabilities(mode, loop.Body, $"{nodePath}/body", errors, activeLeasePath);
                    break;
                case SelectedResourceLeaseAuthoringNode<TState> lease:
                    if (activeLeasePath is not null)
                    {
                        errors.Add(new ValidationError(
                            DefinitionCompilerCodes.LeaseAncestryConflict,
                            "A capacity-reserving resource lease cannot be acquired beneath an active resource lease.",
                            nodePath,
                            activeLeasePath));
                    }

                    ValidateCapabilities(
                        mode,
                        lease.Body,
                        $"{nodePath}/lease",
                        errors,
                        activeLeasePath ?? nodePath);
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    ValidateBranchCapabilities(mode, scope.Branches, nodePath, errors, activeLeasePath);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    ValidateBranchInstructionCapabilities(
                        mode,
                        forEach.Body,
                        $"{nodePath}/item",
                        errors,
                        activeLeasePath);
                    break;
            }
        }
    }

    private static void ValidateBranchCapabilities(
        WorkflowExecutionMode mode,
        IReadOnlyList<StructuredBranchAuthoring> branches,
        string path,
        List<ValidationError> errors,
        string? activeLeasePath = null)
    {
        for (var index = 0; index < branches.Count; index++)
        {
            ValidateBranchInstructionCapabilities(
                mode,
                branches[index].Instructions,
                $"{path}/branches/{index}",
                errors,
                activeLeasePath);
        }
    }

    private static void ValidateBranchInstructionCapabilities(
        WorkflowExecutionMode mode,
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        string path,
        List<ValidationError> errors,
        string? activeLeasePath = null)
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
                // Shape validation reports the root-only capability violation. Continue walking
                // the hand-built subtree so mode-specific violations are still aggregated.
                ValidateBranchCapabilities(mode, nested.Branches, instructionPath, errors, activeLeasePath);
            }
            else if (instruction is BranchIfAuthoringInstruction conditional)
            {
                ValidateBranchInstructionCapabilities(
                    mode,
                    conditional.Then,
                    $"{instructionPath}/then",
                    errors,
                    activeLeasePath);
                ValidateBranchInstructionCapabilities(
                    mode,
                    conditional.Else,
                    $"{instructionPath}/else",
                    errors,
                    activeLeasePath);
            }
            else if (instruction is BranchResourceLeaseAuthoringInstruction lease)
            {
                if (mode != WorkflowExecutionMode.Durable)
                {
                    errors.Add(Error(
                        DefinitionCompilerCodes.UnsupportedInstruction,
                        "AcquireResources is not supported by the ephemeral selected-mode compiler.",
                        instructionPath));
                }

                if (activeLeasePath is not null)
                {
                    errors.Add(new ValidationError(
                        DefinitionCompilerCodes.LeaseAncestryConflict,
                        "A capacity-reserving resource lease cannot be acquired beneath an active resource lease.",
                        instructionPath,
                        activeLeasePath));
                }

                ValidateBranchInstructionCapabilities(
                    mode,
                    lease.Body,
                    $"{instructionPath}/lease",
                    errors,
                    activeLeasePath ?? instructionPath);
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

    private static void AddRootOnlyParallelError(string path, List<ValidationError> errors)
    {
        errors.Add(Error(
            DefinitionCompilerCodes.UnsupportedInstruction,
            "Parallel is available only in the root workflow sequence.",
            path));
    }
}
