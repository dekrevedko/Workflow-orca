using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class StateAndCodecInfrastructureGuards
{
    [Fact]
    public void ScenarioLedger_CoversEveryConstructionCodecStateAndProjectionConcern()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/state-and-codec-scenarios.json");
        scenarios.Select(x => x.Id).Should().BeEquivalentTo(
            "caller-created-text-values", "runtime-created-identities", "definition-id-nonempty",
            "fixed-codec-determinism", "structural-fingerprint-opacity", "attempt-local-replace-state",
            "readonly-snapshot-shape", "typed-completion-output", "projection-opacity");
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.5" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsTheExactCodecFingerprintAttemptAndProjectionRules()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var required in new[]
        {
            "orcacore-json-v1", "ReplaceState", "ReadOnlyStateSnapshot<TState>", "structural",
            "opaque code", "private branch/item or in-flight attempt copies", "Guid.Empty",
            "private constructor", "Create(string)"
        }) matrix.Should().Contain(required);
        matrix.Should().NotContain("replaceable codec");
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class StateAndCodecExpectedRedGuards
{
    [Fact]
    public void Product_ContainsTheFixedCodecAndRuntimeCreatedSnapshotContract()
    {
        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(x => x.GetExportedTypes()).ToArray();
        exported.Should().Contain(x => x.FullName == "OrcaCore.ReadOnlyStateSnapshot`1");
        Directory.GetFiles(Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines).Should().Contain(line => line.Contains("orcacore-json-v1", StringComparison.Ordinal));
    }
}
