using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

[Trait("AC", "AC-021")]
public sealed class StagedWorkflowBuilderTests
{
    [Fact]
    public void InitAndCompletionStages_ExposeOnlyTheirApprovedOperations()
    {
        DeclaredPublicMethods(typeof(EphemeralWorkflowInitBuilder<State>)).Should().Equal("Init");
        DeclaredPublicMethods(typeof(DurableWorkflowInitBuilder<State>)).Should().Equal("Init");

        foreach (var completion in new[]
        {
            typeof(EphemeralWorkflowCompletionBuilder<Input>),
            typeof(EphemeralWorkflowCompletionBuilder<Input, Output>),
            typeof(DurableWorkflowCompletionBuilder<Input>),
            typeof(DurableWorkflowCompletionBuilder<Input, Output>)
        })
        {
            DeclaredPublicMethods(completion).Should().BeEquivalentTo("Build", "TryBuild");
        }
    }

    [Fact]
    public void RootBuilders_ExposeExactlyFourEndOverloads()
    {
        AssertEndShape(typeof(EphemeralWorkflowBuilder<Input, State>));
        AssertEndShape(typeof(DurableWorkflowBuilder<Input, State>));
    }

    [Fact]
    public void ResultfulDefinitions_BuildWithTypedMetadataAndStateOpaqueDurableReference()
    {
        var definitionId = DefinitionId.New();
        var version = new DefinitionVersion(3);

        var ephemeral = Workflow.Ephemeral<State>(definitionId, version)
            .Init<Input>(input => new State(input.Value))
            .End(snapshot => new Output(snapshot.Value.Value))
            .Build();
        var durable = Workflow.Durable<State>(definitionId, version)
            .Init<Input>(input => new State(input.Value))
            .End(snapshot => new Output(snapshot.Value.Value))
            .Build();

        ephemeral.Mode.Should().Be(WorkflowMode.Ephemeral);
        durable.Mode.Should().Be(WorkflowMode.Durable);
        ephemeral.DefinitionId.Should().Be(definitionId);
        durable.DefinitionVersion.Should().Be(version);
        ephemeral.DefinitionFingerprint.Value.Should().NotBeNullOrWhiteSpace();
        durable.Reference.DefinitionFingerprint.Should().Be(durable.DefinitionFingerprint);
        typeof(DurableWorkflowRef<Input, Output>).GetGenericArguments().Should().NotContain(typeof(State));
        typeof(DurableWorkflowRef<Input, Output>).GetProperties().Select(property => property.PropertyType)
            .Should().NotContain(typeof(State));
    }

    [Fact]
    public void ResultfulEnd_OutputTypeAndFixedOutcomeContributeToThePublicFingerprint()
    {
        var definitionId = DefinitionId.New();
        var outcome = WorkflowOutcomeName.Create("approved");
        var same = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(snapshot => new Output(snapshot.Value.Value), outcome)
            .Build();
        var changedOutcome = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(
                snapshot => new Output(snapshot.Value.Value),
                WorkflowOutcomeName.Create("rejected"))
            .Build();
        var changedOutputType = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(snapshot => snapshot.Value.Value, outcome)
            .Build();

        same.DefinitionFingerprint.Should().NotBe(changedOutcome.DefinitionFingerprint);
        same.DefinitionFingerprint.Should().NotBe(changedOutputType.DefinitionFingerprint);
        same.Reference.Should().BeOfType<DurableWorkflowRef<Input, Output>>();
    }

    [Fact]
    public void End_RejectsExplicitNullSelectorsAndOutcomesEagerly()
    {
        var ephemeral = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value));
        var durable = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value));

        Action ephemeralOutcome = () => ephemeral.End(null!);
        Action ephemeralOutput = () => ephemeral.End<Output>(null!);
        Action durableOutcome = () => durable.End(null!);
        Action durableOutput = () => durable.End<Output>(null!);

        ephemeralOutcome.Should().Throw<ArgumentNullException>().WithParameterName("outcome");
        ephemeralOutput.Should().Throw<ArgumentNullException>().WithParameterName("output");
        durableOutcome.Should().Throw<ArgumentNullException>().WithParameterName("outcome");
        durableOutput.Should().Throw<ArgumentNullException>().WithParameterName("output");
    }

    [Fact]
    public void ContinueAsNew_IsATerminalCompletionStage()
    {
        var completion = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .ContinueAsNew(snapshot => snapshot.Value);

        completion.Should().BeOfType<DurableWorkflowCompletionBuilder<Input>>();
        completion.Build().Mode.Should().Be(WorkflowMode.Durable);
    }

    [Fact]
    public void Decorators_RequireOneImmediatelyPrecedingBusinessStep()
    {
        var beforeStep = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value));
        var afterStep = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Then<ProbeStep>()
            .WithRetry(2);

        Action misplaced = () => beforeStep.WithRetry(2);
        Action duplicate = () => afterStep.WithRetry(3);

        misplaced.Should().Throw<WorkflowDefinitionException>()
            .Which.Diagnostics.Should().ContainSingle(x => x.Code == "SFE-AUTH-DECORATOR-001");
        duplicate.Should().Throw<WorkflowDefinitionException>()
            .Which.Diagnostics.Should().ContainSingle(x => x.Code == "SFE-AUTH-DECORATOR-001");
    }

    [Fact]
    public void CompleteWithin_DuplicateReportsPrimaryAndFirstRelatedLocation()
    {
        var builder = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .CompleteWithin(TimeSpan.FromMinutes(1));

        Action duplicate = () => builder.CompleteWithin(TimeSpan.FromMinutes(2));

        var exception = duplicate.Should().Throw<WorkflowDefinitionException>().Which;
        var diagnostic = exception.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be("SFE-AUTH-DEADLINE-001");
        diagnostic.Location.Value.Should().MatchRegex("^workflow:\\$/n:[0-9]{8}$");
        diagnostic.RelatedLocations.Should().ContainSingle();
        diagnostic.RelatedLocations[0].Should().NotBe(diagnostic.Location);
    }

    [Fact]
    public void CompleteWithin_DuplicateUsesActualSecondAndPreservedFirstLocations()
    {
        var builder = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Then<ProbeStep>()
            .CompleteWithin(TimeSpan.FromMinutes(1))
            .Delay(TimeSpan.FromSeconds(1));

        Action duplicate = () => builder.CompleteWithin(TimeSpan.FromMinutes(2));

        var diagnostic = duplicate.Should().Throw<WorkflowDefinitionException>()
            .Which.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be("SFE-AUTH-DEADLINE-001");
        diagnostic.Location.Value.Should().Be("workflow:$/n:00000003");
        diagnostic.RelatedLocations.Should().ContainSingle()
            .Which.Value.Should().Be("workflow:$/n:00000002");
    }

    [Fact]
    public void CompleteWithin_DurationContributesToStructuralFingerprintInBothModes()
    {
        var definitionId = DefinitionId.New();

        BuildEphemeralDeadline(definitionId, TimeSpan.FromMinutes(1)).DefinitionFingerprint
            .Should().NotBe(BuildEphemeralDeadline(definitionId, TimeSpan.FromMinutes(2)).DefinitionFingerprint);
        BuildDurableDeadline(definitionId, TimeSpan.FromMinutes(1)).DefinitionFingerprint
            .Should().NotBe(BuildDurableDeadline(definitionId, TimeSpan.FromMinutes(2)).DefinitionFingerprint);
    }

    [Fact]
    public void StructuralWaitTimeout_ContributesToStructuralFingerprintInBothModes()
    {
        var definitionId = DefinitionId.New();
        var eventName = EventName.Create("resume");

        Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(eventName, _ => CorrelationId.Create("same"), TimeSpan.FromMinutes(1))
            .End()
            .Build()
            .DefinitionFingerprint
            .Should().NotBe(
                Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
                    .Init<Input>(input => new State(input.Value))
                    .Wait(eventName, _ => CorrelationId.Create("same"), TimeSpan.FromMinutes(2))
                    .End()
                    .Build()
                    .DefinitionFingerprint);

        Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(eventName, _ => CorrelationId.Create("same"), TimeSpan.FromMinutes(1))
            .End()
            .Build()
            .DefinitionFingerprint
            .Should().NotBe(
                Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
                    .Init<Input>(input => new State(input.Value))
                    .Wait(eventName, _ => CorrelationId.Create("same"), TimeSpan.FromMinutes(2))
                    .End()
                    .Build()
                    .DefinitionFingerprint);
    }

    [Fact]
    public void AcquireResources_RecordsTheLeaseBoundaryAndItsBodyInThePublicFingerprint()
    {
        var definitionId = DefinitionId.New();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database"), 2));
        var leasedStep = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .AcquireResources(request, lease => lease.Then<ProbeStep>())
            .End()
            .Build();
        var leasedEmpty = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .AcquireResources(request, _ => { })
            .End()
            .Build();
        var unleased = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Then<ProbeStep>()
            .End()
            .Build();

        leasedStep.DefinitionFingerprint.Should().NotBe(leasedEmpty.DefinitionFingerprint);
        leasedStep.DefinitionFingerprint.Should().NotBe(unleased.DefinitionFingerprint);
    }

    [Fact]
    public void BranchLease_ReturnIsAuthoredInsideThePublicLeaseBoundary()
    {
        var definitionId = DefinitionId.New();
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var leased = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("leased"),
                snapshot => snapshot.Value,
                branch => branch.AcquireResources(
                    request,
                    lease => lease.Return(snapshot => snapshot.Value.Value))))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var unleased = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("leased"),
                snapshot => snapshot.Value,
                branch => branch.Return(snapshot => snapshot.Value.Value)))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();

        leased.Mode.Should().Be(WorkflowMode.Durable);
        leased.DefinitionFingerprint.Should().NotBe(unleased.DefinitionFingerprint);
    }

    [Fact]
    public void EmptyParallel_HasTryBuildAndBuildDiagnosticParity()
    {
        var completion = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Parallel<int>(_ => { })
            .WhenAll((snapshot, _) => snapshot.Value)
            .End();

        var validation = completion.TryBuild();
        var exception = ((Action)(() => completion.Build()))
            .Should().Throw<WorkflowDefinitionException>().Which;

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Select(x => x.Code).Should().Equal("SFE-AUTH-BRANCH-004");
        exception.Diagnostics.Select(x => x.Code).Should().Equal(
            validation.Diagnostics.Select(x => x.Code));
        validation.Diagnostics[0].Location.Value.Should().MatchRegex(
            "^workflow:\\$/n:[0-9]{8}$");
    }

    [Fact]
    public void BranchNestedIf_ContributesToThePublicFingerprintAndRetainsEphemeralMode()
    {
        var definitionId = DefinitionId.New();
        var nested = Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("only"),
                snapshot => snapshot.Value,
                branch => branch
                    .If(
                        snapshot => snapshot.Value.Value > 0,
                        then => then.Then<ProbeStep>(),
                        otherwise => otherwise.Delay(TimeSpan.FromMilliseconds(1)))
                    .Return(snapshot => snapshot.Value.Value)))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var baseline = Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("only"),
                snapshot => snapshot.Value,
                branch => branch.Return(snapshot => snapshot.Value.Value)))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();

        nested.Mode.Should().Be(WorkflowMode.Ephemeral);
        nested.DefinitionFingerprint.Should().NotBe(baseline.DefinitionFingerprint);
    }

    [Fact]
    public void NamedSteps_AreAuthoredByTypeWithoutAConsumerActivationFactory()
    {
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Then<ProbeStep>()
            .End()
            .Build();

        definition.DefinitionFingerprint.Value.Should().NotBeNullOrWhiteSpace();
        var then = typeof(EphemeralWorkflowBuilder<Input, State>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "Then")
            .ToArray();
        then.Should().ContainSingle(method =>
            method.IsGenericMethodDefinition &&
            method.GetParameters().Length == 0);
        then.Should().NotContain(method =>
            method.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("Factory", StringComparison.Ordinal)));
    }

    [Fact]
    public void RootForEach_PreservesModeAndIncludesAdmissionOptionsInTheFingerprint()
    {
        var definitionId = DefinitionId.New();
        var ephemeral = Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .ForEach<int, State, int>(
                snapshot => [snapshot.Value.Value],
                ForEachOptions.Create(4, 2),
                item => new State(item.Item),
                body => body.Return(snapshot => snapshot.Value.Value))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var durable = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .ForEach<int, State, int>(
                snapshot => [snapshot.Value.Value],
                ForEachOptions.Create(4, 2),
                item => new State(item.Item),
                body => body.Return(snapshot => snapshot.Value.Value))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();
        var changedAdmission = Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .ForEach<int, State, int>(
                snapshot => [snapshot.Value.Value],
                ForEachOptions.Create(5, 2),
                item => new State(item.Item),
                body => body.Return(snapshot => snapshot.Value.Value))
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            .Build();

        ephemeral.Mode.Should().Be(WorkflowMode.Ephemeral);
        durable.Mode.Should().Be(WorkflowMode.Durable);
        ephemeral.DefinitionFingerprint.Should().NotBe(changedAdmission.DefinitionFingerprint);
    }

    [Fact]
    public void AcquireResources_RetainsStaticRequestStructureButKeepsSelectorBehaviorOpaque()
    {
        var definitionId = DefinitionId.New();
        var poolA = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-a"), 1));
        var poolB = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-b"), 2));

        var first = BuildLeased(definitionId, poolA);
        var same = BuildLeased(definitionId, poolA);
        var changedRequest = BuildLeased(definitionId, poolB);
        var unleased = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Then<ProbeStep>()
            .End()
            .Build();

        var selectorCaptureA = poolA;
        var selectedFirst = BuildSelectedLease(definitionId, _ => selectorCaptureA);
        var selectorCaptureB = poolB;
        var selectedSameStructure = BuildSelectedLease(definitionId, _ => selectorCaptureB);

        first.DefinitionFingerprint.Should().Be(same.DefinitionFingerprint);
        first.DefinitionFingerprint.Should().NotBe(changedRequest.DefinitionFingerprint);
        first.DefinitionFingerprint.Should().NotBe(unleased.DefinitionFingerprint);
        selectedFirst.DefinitionFingerprint.Should().Be(selectedSameStructure.DefinitionFingerprint);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("nested")]
    [InlineData("branch")]
    [InlineData("item")]
    public void AcquireResources_RetainsStaticRequestAtEveryApprovedLocation(string location)
    {
        var definitionId = DefinitionId.New();
        var first = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-a"), 1));
        var changed = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-b"), 2));

        FingerprintAt(location, definitionId, first).Should()
            .NotBe(FingerprintAt(location, definitionId, changed));
    }

    private static DefinitionFingerprint FingerprintAt(
        string location,
        DefinitionId definitionId,
        ResourceLeaseRequest request) => location switch
        {
            "root" => BuildLeased(definitionId, request).DefinitionFingerprint,
            "nested" => Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
                .Init<Input>(input => new State(input.Value))
                .If(_ => true, nested => nested.AcquireResources(request, leased => leased.Then<ProbeStep>()))
                .End()
                .Build()
                .DefinitionFingerprint,
            "branch" => Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
                .Init<Input>(input => new State(input.Value))
                .Parallel<int>(branches => branches.Branch(
                    AuthoredBranchId.Create("branch"),
                    snapshot => snapshot.Value,
                    branch => branch.AcquireResources(
                        request,
                        leased => leased.Return(snapshot => snapshot.Value.Value))))
                .WhenAll((snapshot, _) => snapshot.Value)
                .End()
                .Build()
                .DefinitionFingerprint,
            "item" => Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
                .Init<Input>(input => new State(input.Value))
                .ForEach<int, State, int>(
                    snapshot => [snapshot.Value.Value],
                    ForEachOptions.Create(1),
                    item => new State(item.Item),
                    item => item.AcquireResources(
                        request,
                        leased => leased.Return(snapshot => snapshot.Value.Value)))
                .WhenAll((snapshot, _) => snapshot.Value)
                .End()
                .Build()
                .DefinitionFingerprint,
            _ => throw new ArgumentOutOfRangeException(nameof(location), location, null)
        };

    private static DurableWorkflowDefinition<Input> BuildLeased(
        DefinitionId definitionId,
        ResourceLeaseRequest request) =>
        Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .AcquireResources(request, leased => leased.Then<ProbeStep>())
            .End()
            .Build();

    private static DurableWorkflowDefinition<Input> BuildSelectedLease(
        DefinitionId definitionId,
        Func<ReadOnlyStateSnapshot<State>, ResourceLeaseRequest> request) =>
        Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .AcquireResources(request, leased => leased.Then<ProbeStep>())
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<Input> BuildEphemeralDeadline(
        DefinitionId definitionId,
        TimeSpan timeout) =>
        Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .CompleteWithin(timeout)
            .End()
            .Build();

    private static DurableWorkflowDefinition<Input> BuildDurableDeadline(
        DefinitionId definitionId,
        TimeSpan timeout) =>
        Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .CompleteWithin(timeout)
            .End()
            .Build();

    private static string[] DeclaredPublicMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static void AssertEndShape(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "End")
            .ToArray();
        methods.Should().HaveCount(4);
        methods.Count(method => method.IsGenericMethodDefinition).Should().Be(2);
        methods.Count(method => !method.IsGenericMethodDefinition).Should().Be(2);
    }

    private sealed record Input(int Value);
    private sealed record State(int Value);
    private sealed record Output(int Value);
    private sealed class ProbeStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
