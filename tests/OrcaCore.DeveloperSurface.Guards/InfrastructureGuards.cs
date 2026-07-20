using AwesomeAssertions;
using OrcaCore.Core.Compilation;
using System.Xml.Linq;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class InfrastructureGuards
{
    [Fact]
    public void ExportedTypeCatalog_ClassifiesEveryLoadedOrcaCoreType()
    {
        PublicSurfaceCatalog.ExportedTypes.Should().NotBeEmpty();
        PublicSurfaceCatalog.ExportedTypes.Select(entry => entry.Type).Should().OnlyHaveUniqueItems();
        PublicSurfaceCatalog.ExportedTypes.GroupBy(entry => entry.Tier)
            .Select(group => group.Key)
            .Should().BeEquivalentTo(Enum.GetValues<InterfaceTier>());
    }

    [Fact]
    public void DefinitionIrScanner_FollowsInheritedAndNestedPublicSignatures()
    {
        PublicSurfaceCatalog.FindCompilerIrSignatureTypes(typeof(SyntheticModeDefinition))
            .Should().Contain(typeof(CompiledWorkflowPlan),
                "an empty derived definition must not hide a compiled plan inherited through a nested signature");
    }

    [Fact]
    public void ConsumerFixtures_DefineAllApprovedJourneysWithoutInventingPackages()
    {
        var fixtures = FixtureDefinitions.Read<ConsumerFixture[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/consumer-fixtures.json");

        fixtures.Select(x => x.Id).Should().BeEquivalentTo(
            "minimal-ephemeral", "in-memory-durable", "provider-backed-durable", "meta-package");
        fixtures.Should().OnlyContain(x => x.CurrentProjectReferences.Length > 0 && x.Assertions.Length > 0);
        fixtures.Should().OnlyContain(x => File.Exists(Path.Combine(FixtureDefinitions.RepositoryRoot(), x.CompileProject)));
        fixtures.SelectMany(x => x.CurrentProjectReferences)
            .Should().OnlyContain(path => File.Exists(Path.Combine(FixtureDefinitions.RepositoryRoot(), path)));
        fixtures.SelectMany(x => x.FuturePackages).Should().OnlyContain(name => name.StartsWith("OrcaCore", StringComparison.Ordinal));
        foreach (var fixture in fixtures)
        {
            ProjectReferences(fixture.CompileProject).Should().BeEquivalentTo(
                fixture.CurrentProjectReferences,
                $"{fixture.Id} must compile against exactly its declared current project closure");
        }
    }

    [Fact]
    public void ProviderAuthorFixture_DeclaresOnlyApprovedAdvancedEdge()
    {
        var fixture = FixtureDefinitions.Read<ConsumerFixture>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/provider-author-fixture.json");

        fixture.FuturePackages.Should().Equal("OrcaCore.Provider.Abstractions", "OrcaCore.Runtime.Protocol");
        fixture.Assertions.Should().Contain(x => x.Contains("no engine", StringComparison.OrdinalIgnoreCase));
        fixture.CurrentProjectReferences.Should().OnlyContain(path => File.Exists(Path.Combine(FixtureDefinitions.RepositoryRoot(), path)));
        ProjectReferences(fixture.CompileProject).Should().Equal(fixture.CurrentProjectReferences);
        ProjectReferences(fixture.CompileProject).Should().NotContain(reference =>
            reference.Contains("Engine", StringComparison.Ordinal));
    }

    [Fact]
    public void ExpectedRedLedger_HasEveryPhaseZeroBehaviorScenario()
    {
        var scenarios = FixtureDefinitions.Read<ExpectedRedScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/expected-red-scenarios.json");

        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().HaveCount(22);
        var futurePhases = new[] { "Phase 1", "Phase 2", "Phase 3", "Phase 5" };
        scenarios.Should().OnlyContain(x => futurePhases.Contains(x.FuturePhase));
    }

    private static string[] ProjectReferences(string relativeProject)
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var projectPath = Path.Combine(root, relativeProject.Replace('/', Path.DirectorySeparatorChar));
        return XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include!)))
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private abstract class SyntheticDefinitionBase
    {
        public IReadOnlyList<CompiledWorkflowPlan> CompiledPlans => [];
    }

    private sealed class SyntheticModeDefinition : SyntheticDefinitionBase
    {
    }
}
