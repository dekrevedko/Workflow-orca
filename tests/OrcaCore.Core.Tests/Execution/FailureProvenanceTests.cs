using System.Reflection;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Internal;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

[Trait("AC", "AC-022")]
public sealed class FailureProvenanceTests
{
    [Fact]
    public void FailureOccurrence_IsTheExactClosedRuntimeCreatedUnion()
    {
        var type = typeof(FailureOccurrence);
        type.IsAbstract.Should().BeTrue();
        type.IsClass.Should().BeTrue();
        type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters().Length == 0)
            .Should().ContainSingle()
            .Which.IsFamilyAndAssembly.Should().BeTrue("the base constructor is private protected");

        var variants = type.GetNestedTypes(BindingFlags.Public)
            .OrderBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToArray();
        variants.Select(candidate => candidate.Name).Should().Equal("Branch", "Item", "Root");
        variants.Should().OnlyContain(candidate => candidate.IsSealed);
        variants.SelectMany(candidate => candidate.GetConstructors()).Should().BeEmpty();

        var branchConstructor = typeof(FailureOccurrence.Branch)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is
                [{ ParameterType: var parameterType }] &&
                parameterType == typeof(AuthoredBranchId));
        var itemConstructor = typeof(FailureOccurrence.Item)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is
                [{ ParameterType: var parameterType }] &&
                parameterType == typeof(int));
        branchConstructor.IsAssembly.Should().BeTrue();
        itemConstructor.IsAssembly.Should().BeTrue();

        Action nullBranch = () => branchConstructor.Invoke([null]);
        Action negativeItem = () => itemConstructor.Invoke([-1]);
        nullBranch.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentNullException>();
        negativeItem.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentOutOfRangeException>();

        typeof(WorkflowFailure).IsSealed.Should().BeTrue();
        typeof(WorkflowFailure).IsAssignableTo(typeof(IEquatable<WorkflowFailure>)).Should().BeFalse();
        typeof(WorkflowFailure).GetMethod(
            nameof(object.Equals),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Should().BeNull("workflow failures retain ordinary reference equality");
        typeof(WorkflowFailure).GetProperty(nameof(WorkflowFailure.AuthoredLocation))!.PropertyType
            .Should().Be(typeof(AuthoredLocation));
        typeof(WorkflowFailure).GetProperty(nameof(WorkflowFailure.Occurrence))!.PropertyType
            .Should().Be(typeof(FailureOccurrence));
    }

    [Fact]
    public void DetachedFailureGraph_PreservesProvenanceByValueWithoutSharingReferences()
    {
        var child = new FiberFailure(
            "CHILD",
            "Child failed.",
            authoredLocation: FailureProvenance.Location(
                "workflow:$/n:00000001/parallel:00000000/n:00000000"),
            occurrence: FailureProvenance.BranchOccurrence("left"));
        var source = new FiberFailure(
            "SFE-JOIN-FAILED",
            "Join failed.",
            [child],
            FailureProvenance.Location("workflow:$/n:00000001"),
            FailureProvenance.RootOccurrence());

        var detached = global::OrcaCore.Core.Authoring.PublicAuthoringContracts.ItemOutcome(
            new global::OrcaCore.Core.Building.ForEachItemOutcome<int>(
                0,
                ForEachItemTerminalStatus.Failed,
                0,
                source));
        var publicFailure = detached.Should()
            .BeOfType<ForEachItemOutcome<int>.Failed>().Subject.Failure;

        publicFailure.Code.Should().Be(source.Code);
        publicFailure.AuthoredLocation.Should().Be(source.AuthoredLocation);
        publicFailure.AuthoredLocation.Should().NotBeSameAs(source.AuthoredLocation);
        publicFailure.Occurrence.Should().Be(source.Occurrence);
        publicFailure.Occurrence.Should().NotBeSameAs(source.Occurrence);
        publicFailure.Causes.Should().ContainSingle();
        publicFailure.Causes[0].AuthoredLocation.Should().Be(child.AuthoredLocation);
        publicFailure.Causes[0].AuthoredLocation.Should().NotBeSameAs(child.AuthoredLocation);
        publicFailure.Causes[0].Occurrence.Should().Be(child.Occurrence);
        publicFailure.Causes[0].Occurrence.Should().NotBeSameAs(child.Occurrence);

        var second = global::OrcaCore.Core.Authoring.PublicAuthoringContracts.ItemOutcome(
            new global::OrcaCore.Core.Building.ForEachItemOutcome<int>(
                0,
                ForEachItemTerminalStatus.Failed,
                0,
                source));
        second.Should().BeOfType<ForEachItemOutcome<int>.Failed>().Subject.Failure
            .Should().NotBe(publicFailure);
    }

    [Fact]
    public void AggregateFailures_PreservesSingleIdentityAndUsesOwningProvenanceForMany()
    {
        var first = new FiberFailure(
            "FIRST",
            "First.",
            authoredLocation: FailureProvenance.Location(
                "workflow:$/n:00000002/parallel:00000000/n:00000000"),
            occurrence: FailureProvenance.BranchOccurrence("first"));
        var second = new FiberFailure(
            "SECOND",
            "Second.",
            authoredLocation: FailureProvenance.Location(
                "workflow:$/n:00000002/parallel:00000001/n:00000000"),
            occurrence: FailureProvenance.BranchOccurrence("second"));
        var owningLocation = FailureProvenance.Location("workflow:$/n:00000002");
        var owningOccurrence = FailureProvenance.RootOccurrence();

        ScopeReducer.AggregateFailures([first], owningLocation, owningOccurrence)
            .Should().BeSameAs(first);
        var aggregate = ScopeReducer.AggregateFailures(
            [first, second],
            owningLocation,
            owningOccurrence);

        aggregate.Code.Should().Be("SFE-JOIN-FAILED");
        aggregate.AuthoredLocation.Should().BeSameAs(owningLocation);
        aggregate.Occurrence.Should().BeSameAs(owningOccurrence);
        aggregate.Causes.Should().Equal(first, second);
    }

    [Fact]
    public void FixedCodec_RoundTripsTheVersionedClosedOccurrenceAllowlist()
    {
        foreach (var occurrence in new FailureOccurrence[]
                 {
                     FailureProvenance.RootOccurrence(),
                     FailureProvenance.BranchOccurrence("branch-a"),
                     FailureProvenance.ItemOccurrence(7)
                 })
        {
            var source = CreateFailure(
                "CODE",
                "Message.",
                FailureProvenance.Location("workflow:$/n:00000003"),
                occurrence,
                []);
            var payload = CoreWorkflowValueCodec.Serialize(source, typeof(WorkflowFailure));
            var roundTrip = CoreWorkflowValueCodec.Deserialize(payload, typeof(WorkflowFailure))
                .Should().BeOfType<WorkflowFailure>().Subject;

            roundTrip.Should().NotBeSameAs(source);
            roundTrip.Code.Should().Be(source.Code);
            roundTrip.Message.Should().Be(source.Message);
            roundTrip.AuthoredLocation.Should().Be(source.AuthoredLocation);
            roundTrip.Occurrence.Should().Be(source.Occurrence);
            roundTrip.Occurrence.Should().NotBeSameAs(source.Occurrence);
        }
    }

    [Theory]
    [InlineData("""{"version":1,"kind":"future"}""")]
    [InlineData("""{"version":2,"kind":"root"}""")]
    [InlineData("""{"version":1,"kind":"branch"}""")]
    [InlineData("""{"version":1,"kind":"item","index":-1}""")]
    public void FixedCodec_RejectsUnknownOrMalformedOccurrence(string occurrence)
    {
        var json =
            $$"""{"code":"CODE","message":"Message.","authoredLocation":"workflow:$","occurrence":{{occurrence}},"causes":[]}""";

        Action deserialize = () => CoreWorkflowValueCodec.Deserialize(
            Encoding.UTF8.GetBytes(json),
            typeof(WorkflowFailure));

        deserialize.Should().Throw<JsonException>();
    }

    private static WorkflowFailure CreateFailure(
        string code,
        string message,
        AuthoredLocation location,
        FailureOccurrence occurrence,
        IReadOnlyList<WorkflowFailure> causes) =>
        (WorkflowFailure)typeof(WorkflowFailure)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single()
            .Invoke([code, message, location, occurrence, causes]);
}
