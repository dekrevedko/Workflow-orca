using OrcaCore.Core.Building;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static void LowerForEach<TState>(
        SelectedForEachAuthoringNode<TState> forEach,
        string path,
        List<CompiledInstruction> instructions,
        List<CompiledScopePlan> scopes,
        IReadOnlyDictionary<Type, string> schemaIdentities)
    {
        AddInstruction(instructions, CompiledInstructionKind.StartScope, path, forEach.ItemSelector);
        var branchInstructions = LowerBranch(
            forEach.Body,
            $"{path}/item",
            instructions,
            scopes,
            schemaIdentities);
        var branch = new CompiledBranchPlan(
            new BranchPlanId($"branch:{path}:item"),
            "item",
            0,
            new CompiledBranchInputPlan(
                forEach.ItemType,
                forEach.ItemStateType,
                schemaIdentities[forEach.ItemType],
                schemaIdentities[forEach.ItemStateType],
                forEach.ItemStateProjector),
            new CompiledBranchResultPlan(
                forEach.ItemStateType,
                forEach.ResultType,
                schemaIdentities[forEach.ItemStateType],
                schemaIdentities[forEach.ResultType],
                FindBranchReturn(forEach.Body).ResultProjector),
            branchInstructions);
        var join = AddInstruction(instructions, CompiledInstructionKind.ScopeJoin, $"{path}/join");
        var exit = AddInstruction(instructions, CompiledInstructionKind.ScopeExit, $"{path}/exit");
        scopes.Add(new CompiledScopePlan(
            new ScopePlanId($"scope:{path}"),
            CompiledScopeKind.ForEach,
            forEach.ResultType,
            [branch],
            new CompiledMergePlan(
                CompiledMergeKind.ForEach,
                typeof(TState),
                forEach.ResultType,
                schemaIdentities[typeof(TState)],
                schemaIdentities[forEach.ResultType],
                forEach.Merge),
            join.Id,
            exit.Id)
        {
            ForEach = new CompiledForEachPlan(
                forEach.ItemType,
                schemaIdentities[forEach.ItemType],
                forEach.ItemSelector,
                forEach.Partitioner,
                forEach.ItemStateProjector,
                forEach.JoinPolicy,
                forEach.FailurePolicy,
                forEach.MaxItems,
                forEach.MaxConcurrency)
        });
    }

    private static BranchReturnAuthoringInstruction FindBranchReturn(
        IReadOnlyList<BranchAuthoringInstruction> instructions) =>
        FindBranchReturns(instructions).Single();

    private static IEnumerable<BranchReturnAuthoringInstruction> FindBranchReturns(
        IReadOnlyList<BranchAuthoringInstruction> instructions) =>
        instructions.SelectMany(instruction => instruction switch
        {
            BranchReturnAuthoringInstruction branchReturn => [branchReturn],
            BranchIfAuthoringInstruction conditional =>
                FindBranchReturns(conditional.Then).Concat(FindBranchReturns(conditional.Else)),
            BranchResourceLeaseAuthoringInstruction lease => FindBranchReturns(lease.Body),
            _ => []
        });
}
