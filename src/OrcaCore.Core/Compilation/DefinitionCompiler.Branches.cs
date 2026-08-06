using OrcaCore.Core.Building;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static IReadOnlyList<InstructionId> LowerBranch(
        IReadOnlyList<BranchAuthoringInstruction> branchInstructions,
        string path,
        List<CompiledInstruction> instructions,
        List<CompiledScopePlan> scopes,
        IReadOnlyDictionary<Type, string> schemaIdentities)
    {
        var ids = new List<InstructionId>(branchInstructions.Count);
        for (var index = 0; index < branchInstructions.Count; index++)
        {
            var authored = branchInstructions[index];
            if (authored is BranchStructuredScopeAuthoringInstruction nested)
            {
                LowerStructuredScope(
                    nested.ParentStateType,
                    nested.ScopeKind,
                    nested.ResultType,
                    nested.Branches,
                    nested.Merge,
                    $"{path}/{index}",
                    instructions,
                    scopes,
                    schemaIdentities);
                ids.Add(FindInstruction(instructions, $"{path}/{index}").Id);
                continue;
            }

            if (authored is BranchIfAuthoringInstruction conditional)
            {
                ids.Add(AddInstruction(
                    instructions,
                    CompiledInstructionKind.If,
                    $"{path}/{index}",
                    conditional.Condition).Id);
                LowerBranch(conditional.Then, $"{path}/{index}/then", instructions, scopes, schemaIdentities);
                LowerBranch(conditional.Else, $"{path}/{index}/else", instructions, scopes, schemaIdentities);
                AddInstruction(instructions, CompiledInstructionKind.IfJoin, $"{path}/{index}/join");
                continue;
            }

            if (authored is BranchResourceLeaseAuthoringInstruction lease)
            {
                ids.Add(AddInstruction(
                    instructions,
                    CompiledInstructionKind.AcquireResources,
                    $"{path}/{index}",
                    staticLeaseRequest: lease.StaticRequest,
                    leaseRequestSelector: lease.RequestSelector).Id);
                LowerBranch(lease.Body, $"{path}/{index}/lease", instructions, scopes, schemaIdentities);
                AddInstruction(
                    instructions,
                    CompiledInstructionKind.ReleaseResources,
                    $"{path}/{index}/release");
                continue;
            }

            var kind = authored switch
            {
                BranchReturnAuthoringInstruction => CompiledInstructionKind.BranchReturn,
                BranchWaitAuthoringInstruction => CompiledInstructionKind.Wait,
                BranchDelayAuthoringInstruction => CompiledInstructionKind.Delay,
                _ => CompiledInstructionKind.Step
            };
            var operation = authored switch
            {
                BranchStepAuthoringInstruction step => step.StepFactory,
                BranchWaitAuthoringInstruction wait => wait.CorrelationSelector,
                BranchReturnAuthoringInstruction branchReturn => branchReturn.ResultProjector,
                _ => null
            };
            var waitInstruction = authored as BranchWaitAuthoringInstruction;
            var delayInstruction = authored as BranchDelayAuthoringInstruction;
            var stepInstruction = authored as BranchStepAuthoringInstruction;
            ids.Add(AddInstruction(
                instructions,
                kind,
                $"{path}/{index}",
                operation,
                waitInstruction?.EventContract,
                waitInstruction?.Mode,
                delayInstruction?.Duration,
                policy: stepInstruction is null ? null : CompilePolicy(stepInstruction.Policies),
                stepType: stepInstruction?.StepType,
                waitTimeout: waitInstruction?.Timeout).Id);
        }

        return ids;
    }
}
