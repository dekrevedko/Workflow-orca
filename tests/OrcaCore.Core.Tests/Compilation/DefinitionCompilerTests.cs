using AwesomeAssertions;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
    [InlineData((int)ForEachFailurePolicy.WaitAllThenFail)]
    [InlineData((int)ForEachFailurePolicy.ContinueWithPartialFailures)]
    public void ForEachWhenAny_RejectsNonFailFastFailurePolicies(
        int failurePolicyValue)
    {
        var failurePolicy = (ForEachFailurePolicy)failurePolicyValue;
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
    public void DurableCompiler_AcceptsManuallyConstructedRootForEach()
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
            null,
            1,
            (ReadOnlyParentSnapshot<TestState> parent, IReadOnlyList<global::OrcaCore.Core.Building.ForEachItemOutcome<string>> _) => parent.Value));

        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeTrue();
        validation.Value.CompiledPlan.Scopes.Should().ContainSingle(scope =>
            scope.Kind == CompiledScopeKind.ForEach);
    }

    [Fact]
    [Trait("AC", "AC-206")]
    public void Compiler_RejectsHandBuiltNestedWhileParallelAndForEach()
    {
        var authoring = ManualDefinition(WorkflowExecutionMode.Ephemeral);
        var nested = new List<SelectedAuthoringNode<TestState>>
        {
            new SelectedWhileAuthoringNode<TestState>(
                _ => false,
                [new SelectedDelayAuthoringNode<TestState>(TimeSpan.FromMilliseconds(1))]),
            new SelectedStructuredScopeAuthoringNode<TestState>(
                "Parallel",
                typeof(string),
                [new StructuredBranchAuthoring(
                    "branch",
                    typeof(BranchState),
                    (ReadOnlyParentSnapshot<TestState> _) => new BranchState(),
                    [new BranchReturnAuthoringInstruction(
                        typeof(string),
                        (ReadOnlyBranchSnapshot<BranchState> _) => "done")])],
                (ReadOnlyParentSnapshot<TestState> parent,
                    IReadOnlyList<global::OrcaCore.Core.Building.BranchResult<string>> _) => parent.Value),
            new SelectedForEachAuthoringNode<TestState>(
                typeof(int),
                typeof(ItemState),
                typeof(string),
                (Func<ReadOnlyParentSnapshot<TestState>, IReadOnlyList<int>>)(_ => Array.Empty<int>()),
                WorkflowPartitioner<int>.Items(),
                (Func<ForEachItemInput<int>, ItemState>)(_ => new ItemState(0)),
                [new BranchReturnAuthoringInstruction(
                    typeof(string),
                    (ReadOnlyBranchSnapshot<ItemState> _) => "done")],
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                null,
                1,
                (ReadOnlyParentSnapshot<TestState> parent,
                    IReadOnlyList<global::OrcaCore.Core.Building.ForEachItemOutcome<string>> _) => parent.Value)
        };
        authoring.RootNodes.Insert(1, new SelectedIfAuthoringNode<TestState>(_ => true, nested, []));

        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Where(error => error.Code == DefinitionCompilerCodes.UnsupportedInstruction)
            .Should().HaveCount(3)
            .And.OnlyContain(error =>
                error.Message.Contains("root workflow sequence", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "AC-206")]
    public void Compiler_RejectsHandBuiltParallelInsideRootBranchAndForEachItem()
    {
        var authoring = ManualDefinition(WorkflowExecutionMode.Ephemeral);
        var nestedFanout = NestedFanoutInstruction();
        authoring.RootNodes.Insert(1, new SelectedStructuredScopeAuthoringNode<TestState>(
            "Parallel",
            typeof(string),
            [new StructuredBranchAuthoring(
                "outer",
                typeof(BranchState),
                (ReadOnlyParentSnapshot<TestState> _) => new BranchState(),
                [
                    nestedFanout,
                    new BranchReturnAuthoringInstruction(
                        typeof(string),
                        (ReadOnlyBranchSnapshot<BranchState> _) => "outer")
                ])],
            (ReadOnlyParentSnapshot<TestState> parent,
                IReadOnlyList<global::OrcaCore.Core.Building.BranchResult<string>> _) => parent.Value));
        authoring.RootNodes.Insert(2, new SelectedForEachAuthoringNode<TestState>(
            typeof(int),
            typeof(ItemState),
            typeof(string),
            (Func<ReadOnlyParentSnapshot<TestState>, IReadOnlyList<int>>)(_ => Array.Empty<int>()),
            WorkflowPartitioner<int>.Items(),
            (Func<ForEachItemInput<int>, ItemState>)(_ => new ItemState(0)),
            [
                NestedFanoutInstruction(),
                new BranchReturnAuthoringInstruction(
                    typeof(string),
                    (ReadOnlyBranchSnapshot<ItemState> _) => "item")
            ],
            ForEachJoinPolicy.WhenAll,
            ForEachFailurePolicy.FailFast,
            null,
            1,
            (ReadOnlyParentSnapshot<TestState> parent,
                IReadOnlyList<global::OrcaCore.Core.Building.ForEachItemOutcome<string>> _) => parent.Value));

        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Where(error => error.Code == DefinitionCompilerCodes.UnsupportedInstruction)
            .Should().HaveCount(2)
            .And.OnlyContain(error =>
                error.Message.Contains("root workflow sequence", StringComparison.Ordinal));

        static BranchStructuredScopeAuthoringInstruction NestedFanoutInstruction() => new(
            "Parallel",
            typeof(object),
            typeof(string),
            [new StructuredBranchAuthoring(
                "nested",
                typeof(BranchState),
                (Func<ReadOnlyParentSnapshot<object>, BranchState>)(_ => new BranchState()),
                [new BranchReturnAuthoringInstruction(
                    typeof(string),
                    (ReadOnlyBranchSnapshot<BranchState> _) => "nested")])],
            (ReadOnlyParentSnapshot<object> parent,
                IReadOnlyList<global::OrcaCore.Core.Building.BranchResult<string>> _) => parent.Value);
    }

    [Fact]
    public void DurableCompiler_RejectsManuallyConstructedTransientPoolPolicy()
    {
        var authoring = ManualDefinition(WorkflowExecutionMode.Durable);
        authoring.RootNodes.Insert(1, new SelectedStepAuthoringNode<TestState>(
            null,
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
        var validation = StructuredDefinition(
                DefinitionId.New(),
                new SelectiveTypeSerializerRegistry(typeof(TestState), typeof(BranchState)))
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
            (ReadOnlyParentSnapshot<TestState> parent, IReadOnlyList<global::OrcaCore.Core.Building.BranchResult<string>> _) => parent.Value));

        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "SFE-TYPE-002_BRANCH_RESULT_MISMATCH");
    }

    [Fact]
    public void NonPositiveCompilerLimits_AreAccumulated()
    {
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions
            {
                MaxInternalInstructionsPerQuantum = 0,
                MaxScopeDepth = 0,
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
            "SFE-LIMIT-005_MAX_SERIALIZED_RESULT_BYTES_NOT_POSITIVE",
            "SFE-LIMIT-006_MAX_SERIALIZED_ENVELOPE_BYTES_NOT_POSITIVE");
    }

    [Fact]
    public void WideFixedParallel_HasNoCompilerOwnedBranchWidthCeiling()
    {
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches =>
                {
                    foreach (var index in Enumerable.Range(0, 300))
                    {
                        var branchId = $"branch-{index:D4}";
                        branches.Branch<BranchState>(
                            branchId,
                            _ => new BranchState(),
                            branch => branch.Return(_ => branchId));
                    }
                },
                (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LoopWithNoQuantumEndingOperation_IsRejected()
    {
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var validation = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(_ => { }, (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == "SFE-AUTH-015_EMPTY_STRUCTURED_SCOPE");
    }

    [Fact]
    public void ApprovedRootControlStructures_NeedNoClosingNodes_AndLowerStableInstructions()
    {
        var plan = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .If(_ => true, then => then.Then<NoOpStep>())
            .While(_ => false, body => body.Delay(TimeSpan.FromMilliseconds(1)))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("a", _ => new BranchState(), branch => branch.Return(_ => "a"))
                    .Branch<BranchState>("b", _ => new BranchState(), branch => branch.Return(_ => "b")),
                (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;

        plan.Instructions.Select(instruction => instruction.Kind).Should().Equal(
            CompiledInstructionKind.Init,
            CompiledInstructionKind.If,
            CompiledInstructionKind.Step,
            CompiledInstructionKind.IfJoin,
            CompiledInstructionKind.LoopCheck,
            CompiledInstructionKind.Delay,
            CompiledInstructionKind.LoopBack,
            CompiledInstructionKind.LoopExit,
            CompiledInstructionKind.StartScope,
            CompiledInstructionKind.BranchReturn,
            CompiledInstructionKind.BranchReturn,
            CompiledInstructionKind.ScopeJoin,
            CompiledInstructionKind.ScopeExit,
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
        var durable = global::OrcaCore.Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
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
        ephemeral.Fingerprint.Should().Be(durable.Fingerprint);

        var rollover = global::OrcaCore.Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .ContinueAsNew(state => state)
            .Build()
            .CompiledPlan;
        rollover.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.ContinueAsNew);
        rollover.AllowedInstructions.Should().Contain(CompiledInstructionKind.ContinueAsNew);
        ephemeral.AllowedInstructions.Should().NotContain(CompiledInstructionKind.ContinueAsNew);
    }

    [Fact]
    public void Fingerprint_IsDeterministic_ChangesWithStructureAndIgnoresCompilerOptions()
    {
        var definitionId = DefinitionId.New();

        var first = FingerprintDefinition(definitionId, "first", new()).CompiledPlan.Fingerprint;
        var repeated = FingerprintDefinition(definitionId, "first", new()).CompiledPlan.Fingerprint;
        var graphDrift = global::OrcaCore.Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
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
        optionDrift.Should().Be(first);
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
    public void PublicForEachFingerprint_ChangesWithAuthoredMaxItemsInBothModes()
    {
        var definitionId = DefinitionId.New();

        var ephemeralOne = PublicEphemeralForEachFingerprint(definitionId, maxItems: 1);
        var ephemeralTwo = PublicEphemeralForEachFingerprint(definitionId, maxItems: 2);
        var durableOne = PublicDurableForEachFingerprint(definitionId, maxItems: 1);
        var durableTwo = PublicDurableForEachFingerprint(definitionId, maxItems: 2);

        ephemeralTwo.Should().NotBe(ephemeralOne);
        durableTwo.Should().NotBe(durableOne);
        durableOne.Should().Be(ephemeralOne);
        durableTwo.Should().Be(ephemeralTwo);
    }

    [Fact]
    public void Fingerprint_ChangesWithRootBranchGraphConfiguration()
    {
        var definitionId = DefinitionId.New();

        var first = RootFingerprintDefinition(definitionId, "branch-a").CompiledPlan.Fingerprint;
        var changed = RootFingerprintDefinition(definitionId, "branch-b").CompiledPlan.Fingerprint;

        changed.Should().NotBe(first);
    }

    [Fact]
    public void Fingerprint_IgnoresCapturedOpaqueConfiguration()
    {
        var definitionId = DefinitionId.New();

        var first = CapturedFingerprintDefinition(definitionId, "first").CompiledPlan.Fingerprint;
        var changed = CapturedFingerprintDefinition(definitionId, "second").CompiledPlan.Fingerprint;

        changed.Should().Be(first);
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
        var validation = global::OrcaCore.Workflow.Ephemeral<Action>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Action>(value => value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.SerializerUnavailable);
    }

    [Fact]
    public void DefaultSerializerRegistry_RejectsApplicationJsonConverterAtBuild()
    {
        var validation = global::OrcaCore.Workflow.Ephemeral<ApplicationConvertedState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<ApplicationConvertedState>(value => value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.SerializerUnavailable);
    }

    [Fact]
    public void PublicBuilder_RejectsApplicationJsonConverterAtBuild()
    {
        var validation = global::OrcaCore.Workflow.Ephemeral<ApplicationConvertedState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<ApplicationConvertedState>(value => value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().ContainSingle(error =>
            error.Code == "SFE-TYPE-002");
    }

    [Fact]
    public void PublicBuilder_RejectsApplicationJsonConverterOnNestedMemberAtBuild()
    {
        var validation = global::OrcaCore.Workflow.Durable<PropertyConvertedState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<PropertyConvertedState>(value => value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().ContainSingle(error =>
            error.Code == "SFE-TYPE-002");
    }

    [Fact]
    public void PublicBuilder_RejectsApplicationJsonConverterOnExternalInputAtBuild()
    {
        var validation = global::OrcaCore.Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<ApplicationConvertedState>(_ => new TestState([]))
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().ContainSingle(error =>
            error.Code == "SFE-TYPE-002");
    }

    private static EphemeralWorkflowBuilder<TestState> StructuredDefinition(
        DefinitionId definitionId,
        IWorkflowTypeSerializerRegistry? serializerRegistry = null)
    {
        var builder = global::OrcaCore.Workflow.Ephemeral<TestState>(
            definitionId,
            DefinitionVersion.Initial);
        if (serializerRegistry is not null)
        {
            builder.WithTypeSerializerRegistry(serializerRegistry);
        }

        return builder
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
        return global::OrcaCore.Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .WithCompilerOptions(options)
            .Init<int[]>(_ => new TestState([]))
            .End(outcome)
            .Build();
    }

    private static WorkflowDefinition<TestState> ForEachFingerprintDefinition(
        DefinitionId definitionId,
        int batchSize)
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
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

    private static DefinitionFingerprint PublicEphemeralForEachFingerprint(
        DefinitionId definitionId,
        int maxItems) =>
        global::OrcaCore.Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([1]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                global::OrcaCore.ForEachOptions.Create(maxItems, maxConcurrency: 1),
                item => new ItemState(item.Item),
                body => body.Return(item => item.Value.Item.ToString()))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build()
            .DefinitionFingerprint;

    private static DefinitionFingerprint PublicDurableForEachFingerprint(
        DefinitionId definitionId,
        int maxItems) =>
        global::OrcaCore.Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([1]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                global::OrcaCore.ForEachOptions.Create(maxItems, maxConcurrency: 1),
                item => new ItemState(item.Item),
                body => body.Return(item => item.Value.Item.ToString()))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build()
            .DefinitionFingerprint;

    [Fact]
    public void NestedResourceLease_IsRejectedWithBothAuthoredLocations()
    {
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var builder = new DurableWorkflowBuilder<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]));
        builder.AddResourceLease(
            request,
            outer => outer.AddResourceLease(request, _ => { }));
        var validation = builder.End().TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.LeaseAncestryConflict &&
            error.Path == "root/1/lease/0" &&
            error.RelatedPath == "root/1");
        var diagnostic = PublicAuthoringContracts.Diagnostic(validation.Errors.Single());
        diagnostic.Code.Should().Be("SFE-AUTH-LEASE-001");
        diagnostic.Location.Value.Should().Be("workflow:$/n:00000001/n:00000000");
        diagnostic.RelatedLocations.Select(location => location.Value).Should()
            .Equal("workflow:$/n:00000001");
    }

    [Fact]
    public void ContinueAsNewInsideResourceLease_IsRejectedAtBuild()
    {
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var authoring = ManualDefinition(WorkflowExecutionMode.Durable);
        authoring.RootNodes.Insert(
            1,
            new SelectedResourceLeaseAuthoringNode<TestState>(
                request,
                null,
                [new SelectedContinueAsNewAuthoringNode<TestState>(state => state)]));
        var validation = DefinitionCompiler.Compile(authoring);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.LeaseBlocksContinueAsNew &&
            error.Path == "root/1/lease/0" &&
            error.RelatedPath == "root/1");
        PublicAuthoringContracts.Diagnostic(validation.Errors.Single()).Code.Should()
            .Be("SFE-AUTH-LEASE-003");
    }

    private static WorkflowDefinition<TestState> RootFingerprintDefinition(
        DefinitionId definitionId,
        string branchId)
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        branchId,
                        _ => new BranchState(),
                        branch => branch.Return(_ => branchId))
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
        return global::OrcaCore.Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
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
            typeof(int[]),
            _ => new TestState([])));
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

    [JsonConverter(typeof(ApplicationConvertedStateConverter))]
    private sealed record ApplicationConvertedState(string Value);

    private sealed record PropertyConvertedState(
        [property: JsonConverter(typeof(ApplicationStringConverter))] string Value);

    private sealed class ApplicationConvertedStateConverter : JsonConverter<ApplicationConvertedState>
    {
        public override ApplicationConvertedState? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return new ApplicationConvertedState(reader.GetString()!);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ApplicationConvertedState value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }

    private sealed class ApplicationStringConverter : JsonConverter<string>
    {
        public override string? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return reader.GetString();
        }

        public override void Write(
            Utf8JsonWriter writer,
            string value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

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
