using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    internal static OrcaCore.Abstractions.Primitives.Validation<WorkflowDefinition<TState>> Compile<TState>(
        SelectedWorkflowAuthoring<TState> authoring)
    {
        ArgumentNullException.ThrowIfNull(authoring);

        var errors = new List<ValidationError>();
        ValidateOptions(authoring.CompilerOptions, errors);
        ValidateRootShape(authoring.RootNodes, errors);
        ValidateSequence(authoring.RootNodes, "root", nested: false, errors);
        ValidateConfiguredLimits(authoring.RootNodes, authoring.CompilerOptions, errors);
        ValidateCapabilities(authoring.Mode, authoring.RootNodes, "root", errors);
        var schemaIdentities = ValidateTypeContracts(authoring, errors);
        if (errors.Count > 0)
        {
            return OrcaCore.Abstractions.Primitives.Validation<WorkflowDefinition<TState>>.Invalid(errors);
        }

        var lowered = LowerPlan(authoring.RootNodes, authoring.Mode, schemaIdentities);
        var canonical = $"deadline:{authoring.WorkflowTimeout?.Ticks}|{DescribeSequence(authoring.RootNodes)}|" +
            string.Join(',', schemaIdentities.OrderBy(pair => pair.Key.AssemblyQualifiedName).Select(pair => pair.Value));
        var plan = new CompiledWorkflowPlan(
            authoring.Mode,
            authoring.DefinitionId,
            authoring.DefinitionVersion,
            canonical,
            lowered.Instructions,
            lowered.Scopes,
            lowered.AllowedInstructions,
            authoring.CompilerOptions,
            authoring.TypeSerializerRegistry,
            authoring.DetachedAttemptState,
            authoring.WorkflowTimeout);
        var root = new SequenceNode<TState>("root", BuildNodes(authoring.RootNodes, "root"));
        var definition = new WorkflowDefinition<TState>(
            authoring.DefinitionId,
            authoring.DefinitionVersion,
            root,
            plan);
        return OrcaCore.Abstractions.Primitives.Validation<WorkflowDefinition<TState>>.Valid(definition);
    }

    private static void ValidateRootShape<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        List<ValidationError> errors)
    {
        var initIndexes = nodes
            .Select((node, index) => (node, index))
            .Where(candidate => candidate.node is SelectedInitAuthoringNode<TState>)
            .Select(candidate => candidate.index)
            .ToArray();
        var endIndexes = nodes
            .Select((node, index) => (node, index))
            .Where(candidate => candidate.node is SelectedEndAuthoringNode<TState>)
            .Select(candidate => candidate.index)
            .ToArray();
        var continueAsNewIndexes = nodes
            .Select((node, index) => (node, index))
            .Where(candidate => candidate.node is SelectedContinueAsNewAuthoringNode<TState>)
            .Select(candidate => candidate.index)
            .ToArray();

        if (initIndexes.Length == 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MissingRootInit,
                "The workflow must contain exactly one root Init.",
                "root"));
        }
        else
        {
            if (initIndexes.Length > 1)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.MultipleRootInit,
                    "The workflow contains more than one root Init.",
                    "root"));
            }

            if (initIndexes[0] != 0)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.RootInitNotFirst,
                    "The root Init must be the first authored node.",
                    $"root/{initIndexes[0]}"));
            }
        }

        if (endIndexes.Length == 0 && continueAsNewIndexes.Length == 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MissingRootEnd,
                "The workflow must contain exactly one root End or terminal ContinueAsNew.",
                "root"));
        }
        else if (endIndexes.Length > 1)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MultipleRootEnd,
                "The workflow contains more than one root End.",
                "root"));
        }
    }

    private static void ValidateSequence<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        string path,
        bool nested,
        List<ValidationError> errors)
    {
        var terminalKind = string.Empty;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var nodePath = $"{path}/{index}";
            if (terminalKind.Length > 0)
            {
                var code = terminalKind == "End" && !nested
                    ? DefinitionCompilerCodes.NodeAfterRootEnd
                    : DefinitionCompilerCodes.UnreachableNode;
                errors.Add(Error(code, "The node is unreachable after an unconditional terminal.", nodePath));
                continue;
            }

            switch (node)
            {
                case SelectedInitAuthoringNode<TState> when nested:
                    errors.Add(Error(
                        DefinitionCompilerCodes.NestedRootInit,
                        "Workflow Init cannot appear in nested control flow.",
                        nodePath));
                    break;
                case SelectedEndAuthoringNode<TState> when nested:
                    errors.Add(Error(
                        DefinitionCompilerCodes.NestedRootEnd,
                        "Workflow End cannot appear in nested control flow.",
                        nodePath));
                    terminalKind = "End";
                    break;
                case SelectedEndAuthoringNode<TState>:
                    terminalKind = "End";
                    break;
                case SelectedContinueAsNewAuthoringNode<TState>:
                    terminalKind = "ContinueAsNew";
                    break;
                case SelectedIfAuthoringNode<TState> conditional:
                    ValidateSequence(conditional.Then, $"{nodePath}/then", nested: true, errors);
                    ValidateSequence(conditional.Else, $"{nodePath}/else", nested: true, errors);
                    break;
                case SelectedWhileAuthoringNode<TState> loop when nested:
                    errors.Add(Error(
                        DefinitionCompilerCodes.UnsupportedInstruction,
                        "While is available only in the root workflow sequence.",
                        nodePath));
                    ValidateSequence(loop.Body, $"{nodePath}/body", nested: true, errors);
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    ValidateSequence(loop.Body, $"{nodePath}/body", nested: true, errors);
                    if (!ContainsQuantumEndingOperation(loop.Body))
                    {
                        errors.Add(Error(
                            DefinitionCompilerCodes.NoProgressLoop,
                            "A reachable loop cycle must contain an operation that can end the fiber quantum.",
                            nodePath));
                    }

                    break;
                case SelectedResourceLeaseAuthoringNode<TState> lease:
                    ValidateSequence(lease.Body, $"{nodePath}/lease", nested: true, errors);
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope when nested:
                    errors.Add(Error(
                        DefinitionCompilerCodes.UnsupportedInstruction,
                        "Parallel is available only in the root workflow sequence.",
                        nodePath));
                    ValidateScope(scope, nodePath, errors);
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    ValidateScope(scope, nodePath, errors);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach when nested:
                    errors.Add(Error(
                        DefinitionCompilerCodes.UnsupportedInstruction,
                        "ForEach is available only in the root workflow sequence.",
                        nodePath));
                    ValidateBranchInstructions(forEach.Body, $"{nodePath}/item", errors);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    ValidateBranchInstructions(forEach.Body, $"{nodePath}/item", errors);
                    if (forEach.MaxConcurrency is <= 0)
                    {
                        errors.Add(Error(
                            DefinitionCompilerCodes.ForEachMaxConcurrencyNotPositive,
                            "ForEach maxConcurrency must be positive when provided.",
                            nodePath));
                    }

                    if (forEach.JoinPolicy == ForEachJoinPolicy.WhenAny &&
                        forEach.FailurePolicy != ForEachFailurePolicy.FailFast)
                    {
                        errors.Add(Error(
                            DefinitionCompilerCodes.ForEachWhenAnyFailurePolicy,
                            "ForEach WhenAny accepts only the FailFast failure policy.",
                            nodePath));
                    }

                    break;
            }
        }
    }

    private static void ValidateScope<TState>(
        SelectedStructuredScopeAuthoringNode<TState> scope,
        string path,
        List<ValidationError> errors)
    {
        ValidateStructuredBranches(scope.ResultType, scope.Branches, path, errors);
    }

    private static void ValidateStructuredBranches(
        Type resultType,
        IReadOnlyList<StructuredBranchAuthoring> branches,
        string path,
        List<ValidationError> errors)
    {
        if (branches.Count == 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.EmptyStructuredScope,
                "A structured scope must contain at least one branch.",
                path));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var branchIndex = 0; branchIndex < branches.Count; branchIndex++)
        {
            var branch = branches[branchIndex];
            var branchPath = $"{path}/branches/{branchIndex}";
            if (string.IsNullOrWhiteSpace(branch.BranchId))
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.BlankBranchIdentity,
                    "A structured branch identity cannot be blank.",
                    branchPath));
            }
            else if (!seen.Add(branch.BranchId))
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.DuplicateBranchIdentity,
                    $"The branch identity '{branch.BranchId}' is duplicated.",
                    branchPath));
            }

            foreach (var branchReturn in branch.Instructions.OfType<BranchReturnAuthoringInstruction>())
            {
                if (branchReturn.ResultType != resultType)
                {
                    errors.Add(Error(
                        DefinitionCompilerCodes.BranchResultMismatch,
                        $"Branch result type '{branchReturn.ResultType}' does not match scope result type '{resultType}'.",
                        branchPath));
                }
            }

            ValidateBranchInstructions(branch.Instructions, branchPath, errors);
        }
    }

    private static void ValidateBranchInstructions(
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        string path,
        List<ValidationError> errors)
    {
        var returnCount = CountBranchReturns(instructions);
        if (returnCount == 0)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MissingBranchReturn,
                "Every structured branch must have one reachable final return.",
                path));
        }
        else if (returnCount > 1)
        {
            errors.Add(Error(
                DefinitionCompilerCodes.MultipleBranchReturn,
                "A structured branch cannot contain more than one return.",
                path));
        }

        var returned = false;
        for (var instructionIndex = 0; instructionIndex < instructions.Count; instructionIndex++)
        {
            if (returned)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.UnreachableNode,
                    "The branch instruction is unreachable after Return.",
                    $"{path}/{instructionIndex}"));
                continue;
            }

            returned = instructions[instructionIndex] is BranchReturnAuthoringInstruction;
            if (instructions[instructionIndex] is BranchStructuredScopeAuthoringInstruction nested)
            {
                AddRootOnlyParallelError($"{path}/{instructionIndex}", errors);
                ValidateStructuredBranches(
                    nested.ResultType,
                    nested.Branches,
                    $"{path}/{instructionIndex}",
                    errors);
            }
            else if (instructions[instructionIndex] is BranchIfAuthoringInstruction conditional)
            {
                ValidateBranchNestedSequence(conditional.Then, $"{path}/{instructionIndex}/then", errors);
                ValidateBranchNestedSequence(conditional.Else, $"{path}/{instructionIndex}/else", errors);
            }
            else if (instructions[instructionIndex] is BranchResourceLeaseAuthoringInstruction lease)
            {
                ValidateBranchNestedSequence(lease.Body, $"{path}/{instructionIndex}/lease", errors);
            }
        }
    }

    private static int CountBranchReturns(IReadOnlyList<BranchAuthoringInstruction> instructions) =>
        instructions.Sum(instruction => instruction switch
        {
            BranchReturnAuthoringInstruction => 1,
            BranchIfAuthoringInstruction conditional =>
                CountBranchReturns(conditional.Then) + CountBranchReturns(conditional.Else),
            BranchResourceLeaseAuthoringInstruction lease => CountBranchReturns(lease.Body),
            _ => 0
        });

    private static void ValidateBranchNestedSequence(
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        string path,
        List<ValidationError> errors)
    {
        var returned = false;
        for (var index = 0; index < instructions.Count; index++)
        {
            if (returned)
            {
                errors.Add(Error(
                    DefinitionCompilerCodes.UnreachableNode,
                    "The branch instruction is unreachable after Return.",
                    $"{path}/{index}"));
                continue;
            }

            var instruction = instructions[index];
            returned = instruction is BranchReturnAuthoringInstruction;
            if (instruction is BranchIfAuthoringInstruction conditional)
            {
                ValidateBranchNestedSequence(conditional.Then, $"{path}/{index}/then", errors);
                ValidateBranchNestedSequence(conditional.Else, $"{path}/{index}/else", errors);
            }
            else if (instruction is BranchResourceLeaseAuthoringInstruction lease)
            {
                ValidateBranchNestedSequence(lease.Body, $"{path}/{index}/lease", errors);
            }
            else if (instruction is BranchStructuredScopeAuthoringInstruction nested)
            {
                AddRootOnlyParallelError($"{path}/{index}", errors);
                ValidateStructuredBranches(nested.ResultType, nested.Branches, $"{path}/{index}", errors);
            }
        }
    }

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

        return schemaIdentities;
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

    private static LoweredPlan LowerPlan<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        WorkflowExecutionMode mode,
        IReadOnlyDictionary<Type, string> schemaIdentities)
    {
        var instructions = new List<CompiledInstruction>();
        var scopes = new List<CompiledScopePlan>();
        LowerSequence(nodes, "root", instructions, scopes, schemaIdentities);
        WireSequence(nodes, "root", instructions, continuation: null);

        var allowed = new HashSet<CompiledInstructionKind>
        {
            CompiledInstructionKind.Init,
            CompiledInstructionKind.Step,
            CompiledInstructionKind.End,
            CompiledInstructionKind.If,
            CompiledInstructionKind.IfJoin,
            CompiledInstructionKind.LoopCheck,
            CompiledInstructionKind.LoopBack,
            CompiledInstructionKind.LoopExit,
            CompiledInstructionKind.StartScope,
            CompiledInstructionKind.BranchReturn,
            CompiledInstructionKind.ScopeJoin,
            CompiledInstructionKind.ScopeExit,
            CompiledInstructionKind.Wait,
            CompiledInstructionKind.Delay
        };
        if (mode == WorkflowExecutionMode.Durable)
        {
            allowed.Add(CompiledInstructionKind.ContinueAsNew);
            allowed.Add(CompiledInstructionKind.RunChild);
            allowed.Add(CompiledInstructionKind.RunChildren);
            allowed.Add(CompiledInstructionKind.AcquireResources);
            allowed.Add(CompiledInstructionKind.ReleaseResources);
        }

        return new LoweredPlan(instructions, scopes, allowed);
    }

    private static InstructionId? WireSequence<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        string path,
        List<CompiledInstruction> instructions,
        InstructionId? continuation)
    {
        var next = continuation;
        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            var node = nodes[index];
            var nodePath = $"{path}/{index}";
            var instruction = FindInstruction(instructions, nodePath);
            switch (node)
            {
                case SelectedEndAuthoringNode<TState>:
                case SelectedContinueAsNewAuthoringNode<TState>:
                    SetContinuation(instructions, instruction.Id, next: null, alternate: null);
                    break;
                case SelectedIfAuthoringNode<TState> conditional:
                {
                    var join = FindInstruction(instructions, $"{nodePath}/join");
                    SetContinuation(instructions, join.Id, next, alternate: null);
                    var thenTarget = WireSequence(
                        conditional.Then,
                        $"{nodePath}/then",
                        instructions,
                        join.Id);
                    var elseTarget = WireSequence(
                        conditional.Else,
                        $"{nodePath}/else",
                        instructions,
                        join.Id);
                    SetContinuation(instructions, instruction.Id, thenTarget, elseTarget);
                    break;
                }
                case SelectedWhileAuthoringNode<TState> loop:
                {
                    var loopBack = FindInstruction(instructions, $"{nodePath}/back");
                    var loopExit = FindInstruction(instructions, $"{nodePath}/exit");
                    SetContinuation(instructions, loopExit.Id, next, alternate: null);
                    SetContinuation(instructions, loopBack.Id, instruction.Id, alternate: null);
                    var bodyTarget = WireSequence(
                        loop.Body,
                        $"{nodePath}/body",
                        instructions,
                        loopBack.Id);
                    SetContinuation(instructions, instruction.Id, bodyTarget, loopExit.Id);
                    break;
                }
                case SelectedResourceLeaseAuthoringNode<TState> lease:
                {
                    var release = FindInstruction(instructions, $"{nodePath}/release");
                    SetContinuation(instructions, release.Id, next, alternate: null);
                    var bodyTarget = WireSequence(
                        lease.Body,
                        $"{nodePath}/lease",
                        instructions,
                        release.Id);
                    SetContinuation(instructions, instruction.Id, bodyTarget, alternate: null);
                    break;
                }
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    WireScope(scope, nodePath, instructions, instruction.Id, next);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    WireForEach(forEach, nodePath, instructions, instruction.Id, next);
                    break;
                default:
                    SetContinuation(instructions, instruction.Id, next, alternate: null);
                    break;
            }

            next = instruction.Id;
        }

        return next;
    }

    private static void WireScope<TState>(
        SelectedStructuredScopeAuthoringNode<TState> scope,
        string path,
        List<CompiledInstruction> instructions,
        InstructionId startInstructionId,
        InstructionId? continuation)
    {
        WireStructuredScope(
            scope.Branches,
            path,
            instructions,
            startInstructionId,
            continuation);
    }

    private static void WireStructuredScope(
        IReadOnlyList<StructuredBranchAuthoring> branches,
        string path,
        List<CompiledInstruction> instructions,
        InstructionId startInstructionId,
        InstructionId? continuation)
    {
        for (var ordinal = 0; ordinal < branches.Count; ordinal++)
        {
            WireBranch(branches[ordinal].Instructions, $"{path}/branches/{ordinal}", instructions);
        }

        var join = FindInstruction(instructions, $"{path}/join");
        var exit = FindInstruction(instructions, $"{path}/exit");
        SetContinuation(instructions, startInstructionId, join.Id, alternate: null);
        SetContinuation(instructions, join.Id, exit.Id, alternate: null);
        SetContinuation(instructions, exit.Id, continuation, alternate: null);
    }

    private static void WireForEach<TState>(
        SelectedForEachAuthoringNode<TState> forEach,
        string path,
        List<CompiledInstruction> instructions,
        InstructionId startInstructionId,
        InstructionId? continuation)
    {
        WireBranch(forEach.Body, $"{path}/item", instructions);
        var join = FindInstruction(instructions, $"{path}/join");
        var exit = FindInstruction(instructions, $"{path}/exit");
        SetContinuation(instructions, startInstructionId, join.Id, alternate: null);
        SetContinuation(instructions, join.Id, exit.Id, alternate: null);
        SetContinuation(instructions, exit.Id, continuation, alternate: null);
    }

    private static void WireBranch(
        IReadOnlyList<BranchAuthoringInstruction> authored,
        string path,
        List<CompiledInstruction> instructions)
    {
        _ = WireBranchSequence(authored, path, instructions, continuation: null);
    }

    private static InstructionId? WireBranchSequence(
        IReadOnlyList<BranchAuthoringInstruction> authored,
        string path,
        List<CompiledInstruction> instructions,
        InstructionId? continuation,
        bool returnsFollowContinuation = false)
    {
        var next = continuation;
        for (var index = authored.Count - 1; index >= 0; index--)
        {
            var instruction = FindInstruction(instructions, $"{path}/{index}");
            if (authored[index] is BranchStructuredScopeAuthoringInstruction nested)
            {
                WireStructuredScope(
                    nested.Branches,
                    $"{path}/{index}",
                    instructions,
                    instruction.Id,
                    next);
            }
            else if (authored[index] is BranchIfAuthoringInstruction conditional)
            {
                var join = FindInstruction(instructions, $"{path}/{index}/join");
                SetContinuation(instructions, join.Id, next, alternate: null);
                var thenTarget = WireBranchSequence(
                    conditional.Then,
                    $"{path}/{index}/then",
                    instructions,
                    join.Id,
                    returnsFollowContinuation);
                var elseTarget = WireBranchSequence(
                    conditional.Else,
                    $"{path}/{index}/else",
                    instructions,
                    join.Id,
                    returnsFollowContinuation);
                SetContinuation(instructions, instruction.Id, thenTarget, elseTarget);
            }
            else if (authored[index] is BranchResourceLeaseAuthoringInstruction lease)
            {
                var release = FindInstruction(instructions, $"{path}/{index}/release");
                SetContinuation(instructions, release.Id, next, alternate: null);
                var bodyTarget = WireBranchSequence(
                    lease.Body,
                    $"{path}/{index}/lease",
                    instructions,
                    release.Id,
                    returnsFollowContinuation: true);
                SetContinuation(instructions, instruction.Id, bodyTarget, alternate: null);
            }
            else
            {
                SetContinuation(
                    instructions,
                    instruction.Id,
                    authored[index] is BranchReturnAuthoringInstruction && !returnsFollowContinuation
                        ? null
                        : next,
                    alternate: null);
            }

            next = instruction.Id;
        }

        return next;
    }

    private static CompiledInstruction FindInstruction(
        IReadOnlyList<CompiledInstruction> instructions,
        string path)
    {
        return instructions.Single(instruction =>
            string.Equals(instruction.Path, path, StringComparison.Ordinal));
    }

    private static void SetContinuation(
        List<CompiledInstruction> instructions,
        InstructionId instructionId,
        InstructionId? next,
        InstructionId? alternate)
    {
        var index = instructions.FindIndex(instruction => instruction.Id == instructionId);
        instructions[index] = instructions[index] with
        {
            NextInstructionId = next,
            AlternateInstructionId = alternate
        };
    }

    private static void LowerSequence<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        string path,
        List<CompiledInstruction> instructions,
        List<CompiledScopePlan> scopes,
        IReadOnlyDictionary<Type, string> schemaIdentities)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var nodePath = $"{path}/{index}";
            switch (node)
            {
                case SelectedInitAuthoringNode<TState> init:
                    AddInstruction(instructions, CompiledInstructionKind.Init, nodePath, init.CreateState);
                    break;
                case SelectedStepAuthoringNode<TState> step:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.Step,
                        nodePath,
                        step.StepFactory,
                        policy: CompilePolicy(step.Policies),
                        stepType: step.StepType);
                    break;
                case SelectedWaitAuthoringNode<TState> wait:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.Wait,
                        nodePath,
                        wait.CorrelationSelector,
                        eventName: wait.EventName,
                        waitMode: wait.Mode,
                        waitTimeout: wait.Timeout);
                    break;
                case SelectedDelayAuthoringNode<TState> delay:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.Delay,
                        nodePath,
                        delayDuration: delay.Duration);
                    break;
                case SelectedRunChildAuthoringNode<TState> child:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.RunChild,
                        nodePath,
                        childDefinitionId: child.ChildDefinitionId,
                        childDefinitionVersion: child.ChildDefinitionVersion,
                        childFailurePolicy: child.FailurePolicy);
                    break;
                case SelectedRunChildrenAuthoringNode<TState> children:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.RunChildren,
                        nodePath,
                        children.ItemSnapshotSelector,
                        childDefinitionId: children.ChildDefinitionId,
                        childDefinitionVersion: children.ChildDefinitionVersion,
                        childFailurePolicy: children.FailurePolicy,
                        maxConcurrency: children.MaxConcurrency,
                        childJoinPolicy: children.JoinPolicy,
                        childResidualPolicy: children.ResidualPolicy);
                    break;
                case SelectedEndAuthoringNode<TState> end:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.End,
                        nodePath,
                        operation: end.OutcomeSelector,
                        outputType: end.OutputType,
                        outputSchemaIdentity: end.OutputType is null ? null : schemaIdentities[end.OutputType],
                        outputSelector: end.OutputSelector,
                        fixedOutcomeName: end.OutcomeName);
                    break;
                case SelectedContinueAsNewAuthoringNode<TState> continueAsNew:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.ContinueAsNew,
                        nodePath,
                        continueAsNew.StateSelector);
                    break;
                case SelectedIfAuthoringNode<TState> conditional:
                    AddInstruction(instructions, CompiledInstructionKind.If, nodePath, conditional.Condition);
                    LowerSequence(conditional.Then, $"{nodePath}/then", instructions, scopes, schemaIdentities);
                    LowerSequence(conditional.Else, $"{nodePath}/else", instructions, scopes, schemaIdentities);
                    AddInstruction(instructions, CompiledInstructionKind.IfJoin, $"{nodePath}/join");
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    AddInstruction(instructions, CompiledInstructionKind.LoopCheck, nodePath, loop.Condition);
                    LowerSequence(loop.Body, $"{nodePath}/body", instructions, scopes, schemaIdentities);
                    AddInstruction(instructions, CompiledInstructionKind.LoopBack, $"{nodePath}/back");
                    AddInstruction(instructions, CompiledInstructionKind.LoopExit, $"{nodePath}/exit");
                    break;
                case SelectedResourceLeaseAuthoringNode<TState> lease:
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.AcquireResources,
                        nodePath,
                        staticLeaseRequest: lease.StaticRequest,
                        leaseRequestSelector: lease.RequestSelector);
                    LowerSequence(lease.Body, $"{nodePath}/lease", instructions, scopes, schemaIdentities);
                    AddInstruction(
                        instructions,
                        CompiledInstructionKind.ReleaseResources,
                        $"{nodePath}/release");
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    LowerScope(scope, nodePath, instructions, scopes, schemaIdentities);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    LowerForEach(forEach, nodePath, instructions, scopes, schemaIdentities);
                    break;
            }
        }
    }

    private static void LowerScope<TState>(
        SelectedStructuredScopeAuthoringNode<TState> scope,
        string path,
        List<CompiledInstruction> instructions,
        List<CompiledScopePlan> scopes,
        IReadOnlyDictionary<Type, string> schemaIdentities)
    {
        LowerStructuredScope(
            typeof(TState),
            scope.ScopeKind,
            scope.ResultType,
            scope.Branches,
            scope.Merge,
            path,
            instructions,
            scopes,
            schemaIdentities);
    }

    private static void LowerStructuredScope(
        Type parentStateType,
        string scopeKind,
        Type resultType,
        IReadOnlyList<StructuredBranchAuthoring> authoredBranches,
        Delegate merge,
        string path,
        List<CompiledInstruction> instructions,
        List<CompiledScopePlan> scopes,
        IReadOnlyDictionary<Type, string> schemaIdentities)
    {
        AddInstruction(instructions, CompiledInstructionKind.StartScope, path);
        var branches = new List<CompiledBranchPlan>(authoredBranches.Count);
        for (var ordinal = 0; ordinal < authoredBranches.Count; ordinal++)
        {
            var branch = authoredBranches[ordinal];
            var branchPath = $"{path}/branches/{ordinal}";
            var branchInstructions = LowerBranch(
                branch.Instructions,
                branchPath,
                instructions,
                scopes,
                schemaIdentities);
            var resultProjector = FindBranchReturn(branch.Instructions).ResultProjector;
            branches.Add(new CompiledBranchPlan(
                new BranchPlanId($"branch:{path}:{ordinal}:{branch.BranchId}"),
                branch.BranchId,
                ordinal,
                new CompiledBranchInputPlan(
                    parentStateType,
                    branch.BranchStateType,
                    schemaIdentities[parentStateType],
                    schemaIdentities[branch.BranchStateType],
                    branch.InputProjector),
                new CompiledBranchResultPlan(
                    branch.BranchStateType,
                    resultType,
                    schemaIdentities[branch.BranchStateType],
                    schemaIdentities[resultType],
                    resultProjector),
                branchInstructions));
        }

        var join = AddInstruction(instructions, CompiledInstructionKind.ScopeJoin, $"{path}/join");
        var exit = AddInstruction(instructions, CompiledInstructionKind.ScopeExit, $"{path}/exit");
        scopes.Add(new CompiledScopePlan(
            new ScopePlanId($"scope:{path}"),
            scopeKind switch
            {
                "WhenFirst" => CompiledScopeKind.WhenFirst,
                "ParallelOutcomes" => CompiledScopeKind.WhenAllOutcomes,
                _ => CompiledScopeKind.WhenAll
            },
            resultType,
            branches,
            new CompiledMergePlan(
                scopeKind switch
                {
                    "WhenFirst" => CompiledMergeKind.WhenFirst,
                    "ParallelOutcomes" => CompiledMergeKind.WhenAllOutcomes,
                    _ => CompiledMergeKind.WhenAll
                },
                parentStateType,
                resultType,
                schemaIdentities[parentStateType],
                schemaIdentities[resultType],
                merge),
            join.Id,
            exit.Id));
    }

}
