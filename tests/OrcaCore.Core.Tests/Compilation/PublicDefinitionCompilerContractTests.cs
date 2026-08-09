using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Compilation;

/// <summary>
/// Public compiler-contract coverage recovered from the retired mixed-tier
/// <c>DefinitionCompilerTests</c>. This suite intentionally uses no compiled IR,
/// implementation authoring nodes, friend-only registries, or runtime definitions.
/// </summary>
public sealed class PublicDefinitionCompilerContractTests
{
    [Fact]
    public void WideFixedParallel_HasNoCompilerOwnedBranchWidthCeiling()
    {
        var validation = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches =>
                {
                    foreach (var index in Enumerable.Range(0, 300))
                    {
                        var branchId = $"branch-{index:D4}";
                        branches.Branch(
                            AuthoredBranchId.Create(branchId),
                            _ => new BranchState(),
                            branch => branch.Return(_ => branchId));
                    }
                })
            .WhenAll((parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeTrue();
        validation.TryGetValue(out var definition).Should().BeTrue();
        definition!.DefinitionFingerprint.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LoopWithNoQuantumEndingOperation_IsRejected()
    {
        var validation = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .While(_ => true, _ => { })
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == "SFE-AUTH-LOOP-001");
    }

    [Fact]
    public void LoopWithQuantumEndingOperationOnOnlyOneConditionalPath_IsRejected()
    {
        var validation = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .While(
                _ => true,
                body => body.If(_ => false, then => then.Then<NoOpStep>()))
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == "SFE-AUTH-LOOP-001");
    }

    [Fact]
    public void PortableStructuredGraph_HasTheSameFingerprintAcrossModes()
    {
        var definitionId = DefinitionId.New();

        var ephemeral = BuildPortableEphemeral(definitionId);
        var durable = BuildPortableDurable(definitionId);

        ephemeral.DefinitionFingerprint.Should().Be(durable.DefinitionFingerprint);
        ephemeral.Mode.Should().Be(WorkflowMode.Ephemeral);
        durable.Mode.Should().Be(WorkflowMode.Durable);
        DeclaredMethods(typeof(DurableWorkflowBuilder<int[], TestState>))
            .Should().Contain("ContinueAsNew");
        DeclaredMethods(typeof(EphemeralWorkflowBuilder<int[], TestState>))
            .Should().NotContain("ContinueAsNew");
    }

    [Fact]
    public void Fingerprint_IsDeterministicAndChangesWithStructureAndOutcome()
    {
        var definitionId = DefinitionId.New();
        var first = BuildOutcome(definitionId, "first");
        var repeated = BuildOutcome(definitionId, "first");
        var graphDrift = Workflow.Ephemeral<TestState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .If(_ => true, _ => { })
            .End(WorkflowOutcomeName.Create("first"))
            .Build();
        var outcomeDrift = BuildOutcome(definitionId, "second");

        repeated.DefinitionFingerprint.Should().Be(first.DefinitionFingerprint);
        graphDrift.DefinitionFingerprint.Should().NotBe(first.DefinitionFingerprint);
        outcomeDrift.DefinitionFingerprint.Should().NotBe(first.DefinitionFingerprint);
    }

    [Fact]
    public void Fingerprint_ChangesWithAuthoredBranchIdentity()
    {
        var definitionId = DefinitionId.New();

        BuildBranch(definitionId, "branch-a").DefinitionFingerprint.Should()
            .NotBe(BuildBranch(definitionId, "branch-b").DefinitionFingerprint);
    }

    [Fact]
    public void Fingerprint_UsesOnlyTheExplicitEventNameAndVersionForContractIdentity()
    {
        var definitionId = DefinitionId.New();
        var name = EventName.Create("OrderApproved");
        var versionOne = BuildWait(
            definitionId,
            WorkflowEventContract.Create(name, EventContractVersion.Initial));
        var versionTwo = BuildWait(
            definitionId,
            WorkflowEventContract.Create(name, new EventContractVersion(2)));
        var typed = BuildTypedWait(
            definitionId,
            WorkflowEventContract<ThirdPartyEvent>.Create(name, EventContractVersion.Initial));

        versionTwo.DefinitionFingerprint.Should().NotBe(versionOne.DefinitionFingerprint);
        typed.DefinitionFingerprint.Should().Be(versionOne.DefinitionFingerprint);
        BuildWait(
            definitionId,
            WorkflowEventContract.Create(EventName.Create("OrderApproved"), EventContractVersion.Initial))
            .DefinitionFingerprint.Should().Be(versionOne.DefinitionFingerprint);
    }

    [Fact]
    public void PublishFingerprint_BindsTheEventContractAndPayloadTypeButNotSelectorCaptures()
    {
        var definitionId = DefinitionId.New();
        var name = EventName.Create("OrderPublished");
        var versionOne = BuildPublish(
            definitionId,
            WorkflowEventContract.Create(name, EventContractVersion.Initial),
            "order-1");
        var versionTwo = BuildPublish(
            definitionId,
            WorkflowEventContract.Create(name, new EventContractVersion(2)),
            "order-1");
        var typed = BuildTypedPublish(
            definitionId,
            WorkflowEventContract<ThirdPartyEvent>.Create(name, EventContractVersion.Initial));

        versionTwo.DefinitionFingerprint.Should().NotBe(versionOne.DefinitionFingerprint);
        typed.DefinitionFingerprint.Should().NotBe(versionOne.DefinitionFingerprint);
        BuildPublish(
                definitionId,
                WorkflowEventContract.Create(name, EventContractVersion.Initial),
                "another-captured-correlation")
            .DefinitionFingerprint.Should().Be(versionOne.DefinitionFingerprint);
    }

    [Fact]
    public void Fingerprint_IgnoresCapturedOpaqueSelectorConfiguration()
    {
        var definitionId = DefinitionId.New();

        BuildCapturedOutput(definitionId, "first").DefinitionFingerprint.Should()
            .Be(BuildCapturedOutput(definitionId, "second").DefinitionFingerprint);
    }

    [Fact]
    public void UnsupportedDelegateState_IsRejectedThroughPublicDiagnostics()
    {
        var validation = Workflow.Ephemeral<Action>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<Action>(value => value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == "SFE-TYPE-002");
    }

    [Theory]
    [InlineData("state-root")]
    [InlineData("state-member")]
    [InlineData("external-input")]
    public void ApplicationJsonConverters_AreRejectedAtEveryPublicBuildPosition(string position)
    {
        IReadOnlyList<WorkflowDiagnostic> diagnostics = position switch
        {
            "state-root" => Workflow.Ephemeral<ApplicationConvertedState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<ApplicationConvertedState>(value => value)
                .End()
                .TryBuild()
                .Diagnostics,
            "state-member" => Workflow.Durable<PropertyConvertedState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<PropertyConvertedState>(value => value)
                .End()
                .TryBuild()
                .Diagnostics,
            "external-input" => Workflow.Durable<TestState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<ApplicationConvertedState>(_ => new TestState([]))
                .End()
                .TryBuild()
                .Diagnostics,
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null)
        };

        diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == "SFE-TYPE-002");
    }

    [Fact]
    public void NestedFanoutAndLeaseRecursion_AreUnrepresentableThroughPublicBuilders()
    {
        Type[] nestedAndFiberBuilders =
        [
            typeof(EphemeralNestedBuilder<int[], TestState>),
            typeof(DurableNestedBuilder<int[], TestState>),
            typeof(EphemeralBranchBuilder<BranchState, string>),
            typeof(DurableBranchBuilder<BranchState, string>),
            typeof(EphemeralItemBuilder<BranchState, string>),
            typeof(DurableItemBuilder<BranchState, string>)
        ];
        Type[] leaseBuilders =
        [
            typeof(DurableLeaseWorkflowBuilder<int[], TestState>),
            typeof(DurableLeaseNestedBuilder<int[], TestState>),
            typeof(DurableLeaseBranchBuilder<BranchState, string>),
            typeof(DurableLeaseItemBuilder<BranchState, string>)
        ];

        nestedAndFiberBuilders.Should().OnlyContain(type =>
            !DeclaredMethods(type).Contains("Parallel", StringComparer.Ordinal) &&
            !DeclaredMethods(type).Contains("ForEach", StringComparer.Ordinal));
        leaseBuilders.Should().OnlyContain(type =>
            !DeclaredMethods(type).Contains("AcquireResources", StringComparer.Ordinal) &&
            !DeclaredMethods(type).Contains("ContinueAsNew", StringComparer.Ordinal));
    }

    [Fact]
    public void CompilerOptionsAndSerializerReplacement_AreAbsentFromTheConsumerSurface()
    {
        Type[] authoringStages =
        [
            typeof(EphemeralWorkflowInitBuilder<TestState>),
            typeof(DurableWorkflowInitBuilder<TestState>),
            typeof(EphemeralWorkflowBuilder<int[], TestState>),
            typeof(DurableWorkflowBuilder<int[], TestState>)
        ];

        authoringStages.Should().OnlyContain(type =>
            !DeclaredMethods(type).Contains("WithCompilerOptions", StringComparer.Ordinal) &&
            !DeclaredMethods(type).Contains("WithTypeSerializerRegistry", StringComparer.Ordinal));
    }

    private static EphemeralWorkflowDefinition<int[]> BuildPortableEphemeral(
        DefinitionId definitionId) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .If(_ => true, then => then.Then<NoOpStep>())
            .While(_ => false, body => body.Delay(TimeSpan.FromMilliseconds(1)))
            .Parallel<string>(
                branches => branches
                    .Branch(
                        AuthoredBranchId.Create("a"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "a"))
                    .Branch(
                        AuthoredBranchId.Create("b"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "b")))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

    private static DurableWorkflowDefinition<int[]> BuildPortableDurable(
        DefinitionId definitionId) =>
        Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .If(_ => true, then => then.Then<NoOpStep>())
            .While(_ => false, body => body.Delay(TimeSpan.FromMilliseconds(1)))
            .Parallel<string>(
                branches => branches
                    .Branch(
                        AuthoredBranchId.Create("a"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "a"))
                    .Branch(
                        AuthoredBranchId.Create("b"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "b")))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<int[]> BuildOutcome(
        DefinitionId definitionId,
        string outcome) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .End(WorkflowOutcomeName.Create(outcome))
            .Build();

    private static EphemeralWorkflowDefinition<int[]> BuildBranch(
        DefinitionId definitionId,
        string branchId) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Parallel<string>(
                branches => branches
                    .Branch(
                        AuthoredBranchId.Create(branchId),
                        _ => new BranchState(),
                        branch => branch.Return(_ => branchId))
                    .Branch(
                        AuthoredBranchId.Create("plain"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "plain")))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<int[], string> BuildCapturedOutput(
        DefinitionId definitionId,
        string output) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .End(_ => output)
            .Build();

    private static EphemeralWorkflowDefinition<int[]> BuildWait(
        DefinitionId definitionId,
        WorkflowEventContract eventContract) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Wait(eventContract, _ => CorrelationId.Create("order-1"))
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<int[]> BuildTypedWait<TPayload>(
        DefinitionId definitionId,
        WorkflowEventContract<TPayload> eventContract) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Wait(eventContract, _ => CorrelationId.Create("order-1"))
            .End()
            .Build();

    private static DurableWorkflowDefinition<int[]> BuildPublish(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        string correlationId) =>
        Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Publish(eventContract, _ => CorrelationId.Create(correlationId))
            .End()
            .Build();

    private static DurableWorkflowDefinition<int[]> BuildTypedPublish(
        DefinitionId definitionId,
        WorkflowEventContract<ThirdPartyEvent> eventContract) =>
        Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<int[]>(_ => new TestState([]))
            .Publish(eventContract, _ => CorrelationId.Create("order-1"), _ => new ThirdPartyEvent())
            .End()
            .Build();

    private static string[] DeclaredMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private sealed record TestState(IReadOnlyList<int> Items);

    private sealed record BranchState;

    private sealed class ThirdPartyEvent
    {
        public string Value { get; init; } = string.Empty;
    }

    [JsonConverter(typeof(ApplicationConvertedStateConverter))]
    private sealed record ApplicationConvertedState(string Value);

    private sealed record PropertyConvertedState(
        [property: JsonConverter(typeof(ApplicationStringConverter))] string Value);

    private sealed class ApplicationConvertedStateConverter : JsonConverter<ApplicationConvertedState>
    {
        public override ApplicationConvertedState? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            new(reader.GetString()!);

        public override void Write(
            Utf8JsonWriter writer,
            ApplicationConvertedState value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class ApplicationStringConverter : JsonConverter<string>
    {
        public override string? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            reader.GetString();

        public override void Write(
            Utf8JsonWriter writer,
            string value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private sealed class NoOpStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
