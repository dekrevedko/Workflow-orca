using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Core.Tests.Compilation;

public sealed class DefinitionCompilerTests
{
    [Fact]
    public void Recompilation_ProducesStableInstructionScopeAndBranchPlanIdentities()
    {
        var definitionId = DefinitionId.New();

        var first = StructuredDefinition(definitionId).Build().CompiledPlan;
        var second = StructuredDefinition(definitionId).Build().CompiledPlan;

        first.Instructions.Select(instruction => (instruction.Id, instruction.Kind, instruction.Path))
            .Should().Equal(second.Instructions.Select(instruction => (instruction.Id, instruction.Kind, instruction.Path)));
        first.Scopes.Select(scope => scope.Id).Should().Equal(second.Scopes.Select(scope => scope.Id));
        first.Scopes.SelectMany(scope => scope.Branches).Select(branch => branch.Id)
            .Should().Equal(second.Scopes.SelectMany(scope => scope.Branches).Select(branch => branch.Id));
    }

    [Fact]
    public void CompiledPlan_IndexesInstructionsAndScopesByIdentity()
    {
        var plan = StructuredDefinition(DefinitionId.New()).Build().CompiledPlan;

        foreach (var instruction in plan.Instructions)
        {
            plan.GetInstruction(instruction.Id).Should().BeSameAs(instruction);
        }

        foreach (var scope in plan.Scopes)
        {
            plan.GetScope(scope.Id).Should().BeSameAs(scope);
        }
    }

    [Fact]
    public void SelectedModePlan_ContainsOnlyPositivelyAllowedInstructions()
    {
        var plan = StructuredDefinition(DefinitionId.New()).Build().CompiledPlan;

        plan.Instructions.Should().OnlyContain(instruction => plan.AllowedInstructions.Contains(instruction.Kind));
        var kinds = plan.Instructions.Select(instruction => instruction.Kind);
        kinds.Should().Contain(CompiledInstructionKind.StartScope);
        kinds.Should().Contain(CompiledInstructionKind.BranchReturn);
        kinds.Should().Contain(CompiledInstructionKind.ScopeJoin);
        kinds.Should().Contain(CompiledInstructionKind.ScopeExit);
    }

    [Fact]
    public void EphemeralForEach_IsAcceptedAndDurableSurfaceOmitsIt()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([1, 2, 3]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Items.Single()),
                body => body.Return(item => item.Value.Item.ToString()),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                maxConcurrency: 2,
                merge: (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeTrue();
        validation.Value.CompiledPlan.Scopes.Should().ContainSingle(scope => scope.Kind == CompiledScopeKind.ForEach);
        typeof(DurableWorkflowBuilder<TestState>).GetMethods()
            .Should().NotContain(method => method.Name == "ForEach");
    }

    [Theory]
    [InlineData(ForEachFailurePolicy.WaitAllThenFail)]
    [InlineData(ForEachFailurePolicy.ContinueWithPartialFailures)]
    public void ForEachWhenAny_RejectsNonFailFastFailurePolicies(
        ForEachFailurePolicy failurePolicy)
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([1]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Items.Single()),
                body => body.Return(item => item.Value.Item.ToString()),
                ForEachJoinPolicy.WhenAny,
                failurePolicy)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.ForEachWhenAnyFailurePolicy);
    }

    [Fact]
    public void ForEach_RejectsNonPositiveMaxConcurrency()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([1]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Items.Single()),
                body => body.Return(item => item.Value.Item.ToString()),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                maxConcurrency: 0)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.ForEachMaxConcurrencyNotPositive);
    }

    [Fact]
    [Trait("AC", "DR-AC-016")]
    public void DurableCompiler_RejectsManuallyConstructedForEach()
    {
        var authoring = ManualDefinition(WorkflowExecutionMode.Durable);
        authoring.RootNodes.Insert(1, new SelectedForEachAuthoringNode<TestState>(
            typeof(int),
            typeof(ItemState),
            typeof(string),
            (Func<ReadOnlyParentSnapshot<TestState>, IReadOnlyList<int>>)(_ => Array.Empty<int>()),
            WorkflowPartitioner<int>.Items(),
            (Func<ForEachItemInput<int>, ItemState>)(_ => new ItemState(0)),
            [new BranchReturnAuthoringInstruction(typeof(string), (ReadOnlyBranchSnapshot<ItemState> _) => "done")],
            ForEachJoinPolicy.WhenAll,
            ForEachFailurePolicy.FailFast,
            1,
            (ReadOnlyParentSnapshot<TestState> parent, IReadOnlyList<ForEachItemOutcome<string>> _) => parent.Value));

        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "SFE-CAP-001_UNSUPPORTED_INSTRUCTION");
    }

    [Fact]
    public void DurableCompiler_RejectsManuallyConstructedTransientPoolPolicy()
    {
        var authoring = ManualDefinition(WorkflowExecutionMode.Durable);
        authoring.RootNodes.Insert(1, new SelectedStepAuthoringNode<TestState>(
            () => new NoOpStep(),
            WorkflowPolicySet.Empty.WithPoolKey("cpu")));

        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.UnsupportedInstruction &&
            error.Message.Contains("Transient", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingSerializerForBranchResult_IsRejectedBeforeRegistration()
    {
        var validation = StructuredDefinition(DefinitionId.New())
            .WithTypeSerializerRegistry(new SelectiveTypeSerializerRegistry(typeof(TestState), typeof(BranchState)))
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == "SFE-TYPE-001_SERIALIZER_UNAVAILABLE" &&
            error.Message.Contains(typeof(string).FullName!, StringComparison.Ordinal));
    }

    [Fact]
    public void ManuallyMismatchedBranchResultAndMergeType_IsRejected()
    {
        var authoring = ManualDefinition(WorkflowExecutionMode.Ephemeral);
        authoring.RootNodes.Insert(1, new SelectedStructuredScopeAuthoringNode<TestState>(
            "Parallel",
            typeof(string),
            [new StructuredBranchAuthoring(
                "branch",
                typeof(BranchState),
                (ReadOnlyParentSnapshot<TestState> _) => new BranchState(),
                [new BranchReturnAuthoringInstruction(typeof(int), (ReadOnlyBranchSnapshot<BranchState> _) => 42)])],
            (ReadOnlyParentSnapshot<TestState> parent, IReadOnlyList<BranchResult<string>> _) => parent.Value));

        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "SFE-TYPE-002_BRANCH_RESULT_MISMATCH");
    }

    [Fact]
    public void NonPositiveCompilerLimits_AreAccumulated()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions
            {
                MaxInternalInstructionsPerQuantum = 0,
                MaxScopeDepth = 0,
                MaxActiveFibers = -1,
                MaxSerializedResultBytes = 0,
                MaxSerializedEnvelopeBytes = -1
            })
            .Init<int[]>(_ => new TestState([]))
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(error => error.Code).Should().Equal(
            "SFE-LIMIT-001_MAX_INTERNAL_INSTRUCTIONS_NOT_POSITIVE",
            "SFE-LIMIT-002_MAX_SCOPE_DEPTH_NOT_POSITIVE",
            "SFE-LIMIT-003_MAX_ACTIVE_FIBERS_NOT_POSITIVE",
            "SFE-LIMIT-005_MAX_SERIALIZED_RESULT_BYTES_NOT_POSITIVE",
            "SFE-LIMIT-006_MAX_SERIALIZED_ENVELOPE_BYTES_NOT_POSITIVE");
    }

    [Fact]
    public void StructuredScopeDepth_AboveConfiguredLimit_IsRejected()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions { MaxScopeDepth = 1 })
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "outer",
                    _ => new BranchState(),
                    branch => branch
                        .Parallel<string>(
                            nested => nested.Branch<BranchState>(
                                "inner",
                                _ => new BranchState(),
                                child => child.Return(_ => "inner")),
                            (parent, _) => parent.Value)
                        .Return(_ => "outer")),
                (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == "SFE-LIMIT-007_MAX_SCOPE_DEPTH_EXCEEDED");
    }

    [Fact]
    public void AuthoredScope_AboveConfiguredActiveFiberLimit_IsRejected()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions { MaxActiveFibers = 2 })
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("first", _ => new BranchState(), branch => branch.Return(_ => "first"))
                    .Branch<BranchState>("second", _ => new BranchState(), branch => branch.Return(_ => "second")),
                (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == "SFE-LIMIT-008_MAX_ACTIVE_FIBERS_EXCEEDED");
    }

    [Fact]
    public void LoopWithNoQuantumEndingOperation_IsRejected()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .While(_ => true, _ => { })
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "SFE-PLAN-001_NO_PROGRESS_LOOP");
    }

    [Fact]
    public void LoopWithQuantumEndingOperationOnOnlyOneConditionalPath_IsRejected()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .While(
                _ => true,
                body => body.If(_ => false, then => then.Then<NoOpStep>()))
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == DefinitionCompilerCodes.NoProgressLoop);
    }

    [Fact]
    public void StructuredScopeWithNoBranches_IsRejectedDuringCompilation()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(_ => { }, (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == "SFE-AUTH-015_EMPTY_STRUCTURED_SCOPE");
    }

    [Fact]
    public void NestedBuilders_NeedNoClosingNodes_AndLowerStableStructuralInstructions()
    {
        var plan = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .If(
                _ => true,
                then => then.While(
                    _ => true,
                    body => body.Parallel<string>(
                        branches => branches
                            .Branch<BranchState>("a", _ => new BranchState(), branch => branch.Return(_ => "a"))
                            .Branch<BranchState>("b", _ => new BranchState(), branch => branch.Return(_ => "b")),
                        (parent, _) => parent.Value)))
            .End()
            .Build()
            .CompiledPlan;

        plan.Instructions.Select(instruction => instruction.Kind).Should().Equal(
            CompiledInstructionKind.Init,
            CompiledInstructionKind.If,
            CompiledInstructionKind.LoopCheck,
            CompiledInstructionKind.StartScope,
            CompiledInstructionKind.BranchReturn,
            CompiledInstructionKind.BranchReturn,
            CompiledInstructionKind.ScopeJoin,
            CompiledInstructionKind.ScopeExit,
            CompiledInstructionKind.LoopBack,
            CompiledInstructionKind.LoopExit,
            CompiledInstructionKind.IfJoin,
            CompiledInstructionKind.End);
        plan.Instructions.Select(instruction => instruction.Id).Should().OnlyHaveUniqueItems();

        var publicMethods = typeof(EphemeralWorkflowBuilder<TestState>).GetMethods().Select(method => method.Name);
        publicMethods.Should().NotContain("EndIf");
        publicMethods.Should().NotContain("EndWhile");
        publicMethods.Should().NotContain("EndParallel");
    }

    [Fact]
    public void ScopePlan_PreservesTypedInputResultMergeAndResolvedPolicies()
    {
        var plan = StructuredDefinition(DefinitionId.New()).Build().CompiledPlan;

        var scope = plan.Scopes.Should().ContainSingle().Which;
        scope.Merge.Kind.Should().Be(CompiledMergeKind.WhenAll);
        scope.Merge.ParentStateType.Should().Be(typeof(TestState));
        scope.Merge.ResultType.Should().Be(typeof(string));
        scope.Merge.ParentStateSchemaIdentity.Should().NotBeNullOrWhiteSpace();
        scope.Merge.ResultSchemaIdentity.Should().NotBeNullOrWhiteSpace();

        var branch = scope.Branches[0];
        branch.Input.ParentStateType.Should().Be(typeof(TestState));
        branch.Input.BranchStateType.Should().Be(typeof(BranchState));
        branch.Result.BranchStateType.Should().Be(typeof(BranchState));
        branch.Result.ResultType.Should().Be(typeof(string));
        branch.Input.BranchStateSchemaIdentity.Should().NotBeNullOrWhiteSpace();
        branch.Result.ResultSchemaIdentity.Should().NotBeNullOrWhiteSpace();

        plan.Instructions.Should().OnlyContain(instruction => instruction.Policy != null);
        plan.Instructions.Select(instruction => instruction.Policy).Should().OnlyContain(policy =>
            policy.Retry == null &&
            policy.Timeout == null &&
            !policy.CancellationEnabled &&
            policy.TransientPoolKey == null &&
            policy.DurableResourceKeys.Count == 0);
    }

    [Fact]
    public void PortableStructuredGraph_LowersIdenticallyAcrossModes_AndDurableAddsRollover()
    {
        var definitionId = DefinitionId.New();
        var ephemeral = StructuredDefinition(definitionId).Build().CompiledPlan;
        var durable = Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("first", _ => new BranchState(), branch => branch.Return(_ => "first"))
                    .Branch<BranchState>("second", _ => new BranchState(), branch => branch.Return(_ => "second")),
                (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;

        ephemeral.Instructions.Select(instruction => (instruction.Id, instruction.Kind))
            .Should().Equal(durable.Instructions.Select(instruction => (instruction.Id, instruction.Kind)));
        ephemeral.Scopes.Select(scope => scope.Id).Should().Equal(durable.Scopes.Select(scope => scope.Id));
        ephemeral.Fingerprint.Should().NotBe(durable.Fingerprint);

        var rollover = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .If(_ => true, then => then.ContinueAsNew(state => state))
            .End()
            .Build()
            .CompiledPlan;
        rollover.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.ContinueAsNew);
        rollover.AllowedInstructions.Should().Contain(CompiledInstructionKind.ContinueAsNew);
        ephemeral.AllowedInstructions.Should().NotContain(CompiledInstructionKind.ContinueAsNew);
    }

    [Fact]
    public void Fingerprint_IsDeterministic_AndChangesWithGraphOrCompilerConfiguration()
    {
        var definitionId = DefinitionId.New();

        var first = FingerprintDefinition(definitionId, "first", new()).CompiledPlan.Fingerprint;
        var repeated = FingerprintDefinition(definitionId, "first", new()).CompiledPlan.Fingerprint;
        var graphDrift = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .If(_ => true, _ => { })
            .End("first")
            .Build()
            .CompiledPlan
            .Fingerprint;
        var outcomeDrift = FingerprintDefinition(definitionId, "second", new()).CompiledPlan.Fingerprint;
        var optionDrift = FingerprintDefinition(
            definitionId,
            "first",
            new DefinitionCompilerOptions { MaxInternalInstructionsPerQuantum = 2048 })
            .CompiledPlan
            .Fingerprint;

        first.Should().Be(repeated);
        graphDrift.Should().NotBe(first);
        outcomeDrift.Should().NotBe(first);
        optionDrift.Should().NotBe(first);
    }

    [Fact]
    public void Fingerprint_ChangesWithForEachPartitionerConfiguration()
    {
        var definitionId = DefinitionId.New();

        var batchTwo = ForEachFingerprintDefinition(definitionId, 2).CompiledPlan.Fingerprint;
        var batchThree = ForEachFingerprintDefinition(definitionId, 3).CompiledPlan.Fingerprint;

        batchThree.Should().NotBe(batchTwo);
    }

    [Fact]
    public void Fingerprint_ChangesWithNestedBranchGraphConfiguration()
    {
        var definitionId = DefinitionId.New();

        var first = NestedFingerprintDefinition(definitionId, "nested-a").CompiledPlan.Fingerprint;
        var changed = NestedFingerprintDefinition(definitionId, "nested-b").CompiledPlan.Fingerprint;

        changed.Should().NotBe(first);
    }

    [Fact]
    public void Fingerprint_ChangesWithCapturedDeclaredConfiguration()
    {
        var definitionId = DefinitionId.New();

        var first = CapturedFingerprintDefinition(definitionId, "first").CompiledPlan.Fingerprint;
        var changed = CapturedFingerprintDefinition(definitionId, "second").CompiledPlan.Fingerprint;

        changed.Should().NotBe(first);
    }

    [Fact]
    public void CompiledPlan_CollectionsCannotBeMutatedThroughRuntimeCasts()
    {
        var plan = StructuredDefinition(DefinitionId.New()).Build().CompiledPlan;

        AssertReadOnly(plan.Instructions);
        AssertReadOnly(plan.Scopes);
        AssertReadOnly(plan.Scopes.Single().Branches);
        AssertReadOnly(plan.Scopes.Single().Branches[0].Instructions);
        if (plan.AllowedInstructions is ICollection<CompiledInstructionKind> allowed)
        {
            allowed.IsReadOnly.Should().BeTrue();
        }
    }

    [Fact]
    public void DefaultSerializerRegistry_RejectsUnsupportedDelegateState()
    {
        var validation = Workflow.Ephemeral<Action>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Action>(value => value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.SerializerUnavailable);
    }

    private static EphemeralWorkflowBuilder<TestState> StructuredDefinition(DefinitionId definitionId)
    {
        return Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("first", _ => new BranchState(), branch => branch.Return(_ => "first"))
                    .Branch<BranchState>("second", _ => new BranchState(), branch => branch.Return(_ => "second")),
                (parent, _) => parent.Value)
            .End();
    }

    private static WorkflowDefinition<TestState> FingerprintDefinition(
        DefinitionId definitionId,
        string outcome,
        DefinitionCompilerOptions options)
    {
        return Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .WithCompilerOptions(options)
            .Init<int[]>(_ => new TestState([]))
            .End(outcome)
            .Build();
    }

    private static WorkflowDefinition<TestState> ForEachFingerprintDefinition(
        DefinitionId definitionId,
        int batchSize)
    {
        return Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([1, 2, 3]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Batch(batchSize),
                item => new ItemState(item.Items.Single()),
                body => body.Return(item => item.Value.Item.ToString()),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                merge: (parent, _) => parent.Value)
            .End()
            .Build();
    }

    private static WorkflowDefinition<TestState> NestedFingerprintDefinition(
        DefinitionId definitionId,
        string nestedBranchId)
    {
        return Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        "outer",
                        _ => new BranchState(),
                        branch => branch
                            .Parallel<string>(
                                nested => nested.Branch<BranchState>(
                                    nestedBranchId,
                                    _ => new BranchState(),
                                    child => child.Return(_ => nestedBranchId)),
                                (parent, _) => parent.Value)
                            .Return(_ => "outer"))
                    .Branch<BranchState>(
                        "plain",
                        _ => new BranchState(),
                        branch => branch.Return(_ => "plain")),
                (parent, _) => parent.Value)
            .End()
            .Build();
    }

    private static WorkflowDefinition<TestState> CapturedFingerprintDefinition(
        DefinitionId definitionId,
        string outcome)
    {
        return Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .End(_ => outcome)
            .Build();
    }

    private static void AssertReadOnly<T>(IReadOnlyCollection<T> values)
    {
        values.Should().BeAssignableTo<ICollection<T>>();
        ((ICollection<T>)values).IsReadOnly.Should().BeTrue();
    }

    private static SelectedWorkflowAuthoring<TestState> ManualDefinition(WorkflowExecutionMode mode)
    {
        var authoring = new SelectedWorkflowAuthoring<TestState>(
            DefinitionId.New(),
            DefinitionVersion.Initial,
            mode);
        authoring.RootNodes.Add(new SelectedInitAuthoringNode<TestState>(
            _ => new TestState([]),
            (_, _) => Array.Empty<int>()));
        authoring.RootNodes.Add(new SelectedEndAuthoringNode<TestState>(null, null));
        return authoring;
    }

    private sealed class SelectiveTypeSerializerRegistry(params Type[] supportedTypes)
        : IWorkflowTypeSerializerRegistry
    {
        private readonly HashSet<Type> supported = [.. supportedTypes];

        public bool TryGetSchemaIdentity(Type type, out string schemaIdentity)
        {
            schemaIdentity = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
            return supported.Contains(type);
        }
    }

    private sealed record TestState(IReadOnlyList<int> Items);

    private sealed record BranchState;

    private sealed record ItemState(int Item);

    private sealed class NoOpStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
