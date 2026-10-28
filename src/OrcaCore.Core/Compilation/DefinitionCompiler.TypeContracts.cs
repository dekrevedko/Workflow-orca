using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Building;

namespace OrcaCore.Core.Compilation;

// Kept together so schema diagnostics and their complete authoring closure share one owner.
internal static partial class DefinitionCompiler
{
    private static IReadOnlyDictionary<Type, string> ValidateTypeContracts<TState>(
        SelectedWorkflowAuthoring<TState> authoring,
        List<ValidationError> errors)
    {
        var requiredTypes = new List<(Type Type, string Path)> { (typeof(TState), "root/state") };
        CollectTypes(authoring.RootNodes, "root", requiredTypes);

        var schemaIdentities = new Dictionary<Type, string>();
        var seen = new HashSet<Type>();
        foreach (var required in requiredTypes)
        {
            if (!seen.Add(required.Type))
            {
                continue;
            }

            try
            {
                if (!authoring.TypeSerializerRegistry.TryGetSchemaIdentity(required.Type, out var schemaIdentity) ||
                    string.IsNullOrWhiteSpace(schemaIdentity))
                {
                    errors.Add(Error(
                        DefinitionCompilerCodes.SerializerUnavailable,
                        $"No serializer schema is registered for '{required.Type.FullName}'.",
                        required.Path));
                    continue;
                }

                schemaIdentities.Add(required.Type, schemaIdentity);
            }
            catch (global::OrcaCore.Internal.WorkflowJsonContractException exception)
            {
                errors.Add(Error(exception.DiagnosticCode, exception.Message, required.Path));
            }
        }

        // Internal snapshot metadata is required, but does not enter the established schema/fingerprint input.
        if (errors.Count == 0 && global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.Seal() is not null)
            ValidateSnapshotContracts(authoring.RootNodes, "root", errors);
        return schemaIdentities;
    }

    private static void ValidateSnapshotContracts<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes, string path, List<ValidationError> errors)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var nodePath = $"{path}/{index}";
            switch (nodes[index])
            {
                case SelectedForEachAuthoringNode<TState> forEach:
                    var snapshotType = global::OrcaCore.Core.Execution.ClosedForEachBindings.For(forEach.Partitioner).SnapshotType;
                    try { _ = global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.ResolveContract(snapshotType); }
                    catch (global::OrcaCore.Internal.WorkflowJsonContractException exception)
                    { errors.Add(Error(exception.DiagnosticCode, exception.Message, $"{nodePath}/item-snapshot")); }
                    break;
                case SelectedIfAuthoringNode<TState> conditional:
                    ValidateSnapshotContracts(conditional.Then, $"{nodePath}/then", errors);
                    ValidateSnapshotContracts(conditional.Else, $"{nodePath}/else", errors);
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    ValidateSnapshotContracts(loop.Body, $"{nodePath}/body", errors);
                    break;
                case SelectedResourceLeaseAuthoringNode<TState> lease:
                    ValidateSnapshotContracts(lease.Body, $"{nodePath}/lease", errors);
                    break;
            }
        }
    }

    private static void CollectTypes<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        string path,
        List<(Type Type, string Path)> requiredTypes)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var nodePath = $"{path}/{index}";
            switch (node)
            {
                case SelectedInitAuthoringNode<TState> init:
                    requiredTypes.Add((init.InputType, $"{nodePath}/input"));
                    break;
                case SelectedEndAuthoringNode<TState> end when end.OutputType is not null:
                    requiredTypes.Add((end.OutputType, $"{nodePath}/output"));
                    break;
                case SelectedPublishAuthoringNode<TState> publish when publish.PayloadType is not null:
                    requiredTypes.Add((publish.PayloadType, $"{nodePath}/payload"));
                    break;
                case SelectedIfAuthoringNode<TState> conditional:
                    CollectTypes(conditional.Then, $"{nodePath}/then", requiredTypes);
                    CollectTypes(conditional.Else, $"{nodePath}/else", requiredTypes);
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    CollectTypes(loop.Body, $"{nodePath}/body", requiredTypes);
                    break;
                case SelectedResourceLeaseAuthoringNode<TState> lease:
                    CollectTypes(lease.Body, $"{nodePath}/lease", requiredTypes);
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    requiredTypes.Add((scope.ResultType, $"{nodePath}/result"));
                    CollectBranchTypes(scope.Branches, nodePath, requiredTypes);

                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    requiredTypes.Add((forEach.ItemType, $"{nodePath}/item"));
                    requiredTypes.Add((forEach.ItemStateType, $"{nodePath}/item-state"));
                    requiredTypes.Add((forEach.ResultType, $"{nodePath}/result"));
                    break;
            }
        }
    }

    private static void CollectBranchTypes(
        IReadOnlyList<StructuredBranchAuthoring> branches,
        string path,
        List<(Type Type, string Path)> requiredTypes)
    {
        foreach (var branch in branches)
        {
            var branchPath = $"{path}/branches/{branch.BranchId}";
            requiredTypes.Add((branch.BranchStateType, $"{branchPath}/state"));
            for (var index = 0; index < branch.Instructions.Count; index++)
            {
                if (branch.Instructions[index] is BranchPublishAuthoringInstruction publish &&
                    publish.PayloadType is not null)
                {
                    requiredTypes.Add((publish.PayloadType, $"{branchPath}/{index}/payload"));
                    continue;
                }

                if (branch.Instructions[index] is BranchIfAuthoringInstruction conditional)
                {
                    CollectBranchInstructionTypes(
                        conditional.Then,
                        $"{branchPath}/{index}/then",
                        requiredTypes);
                    CollectBranchInstructionTypes(
                        conditional.Else,
                        $"{branchPath}/{index}/else",
                        requiredTypes);
                    continue;
                }

                if (branch.Instructions[index] is BranchResourceLeaseAuthoringInstruction lease)
                {
                    CollectBranchInstructionTypes(
                        lease.Body,
                        $"{branchPath}/{index}/lease",
                        requiredTypes);
                    continue;
                }

                if (branch.Instructions[index] is not BranchStructuredScopeAuthoringInstruction nested)
                {
                    continue;
                }

                var nestedPath = $"{branchPath}/{index}";
                requiredTypes.Add((nested.ParentStateType, $"{nestedPath}/parent-state"));
                requiredTypes.Add((nested.ResultType, $"{nestedPath}/result"));
                CollectBranchTypes(nested.Branches, nestedPath, requiredTypes);
            }
        }
    }

    private static void CollectBranchInstructionTypes(
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        string path,
        List<(Type Type, string Path)> requiredTypes)
    {
        for (var index = 0; index < instructions.Count; index++)
        {
            switch (instructions[index])
            {
                case BranchPublishAuthoringInstruction publish when publish.PayloadType is not null:
                    requiredTypes.Add((publish.PayloadType, $"{path}/{index}/payload"));
                    break;
                case BranchStructuredScopeAuthoringInstruction nested:
                {
                    var nestedPath = $"{path}/{index}";
                    requiredTypes.Add((nested.ParentStateType, $"{nestedPath}/parent-state"));
                    requiredTypes.Add((nested.ResultType, $"{nestedPath}/result"));
                    CollectBranchTypes(nested.Branches, nestedPath, requiredTypes);
                    break;
                }
                case BranchIfAuthoringInstruction conditional:
                    CollectBranchInstructionTypes(conditional.Then, $"{path}/{index}/then", requiredTypes);
                    CollectBranchInstructionTypes(conditional.Else, $"{path}/{index}/else", requiredTypes);
                    break;
                case BranchResourceLeaseAuthoringInstruction lease:
                    CollectBranchInstructionTypes(lease.Body, $"{path}/{index}/lease", requiredTypes);
                    break;
            }
        }
    }

}
