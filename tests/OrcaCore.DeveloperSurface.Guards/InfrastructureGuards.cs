using AwesomeAssertions;
using OrcaCore.Core.Compilation;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class InfrastructureGuards
{
    [Fact]
    public void FrozenTargetCatalog_ModelsEveryAssemblyAndRequiredAudienceTier()
    {
        PublicSurfaceCatalog.TargetAssemblies.Select(x => x.Name).Should().Equal(
            "OrcaCore", "OrcaCore.Core", "OrcaCore.Engine.Ephemeral", "OrcaCore.Runtime.Protocol",
            "OrcaCore.Provider.Abstractions", "OrcaCore.Engine.Durable", "OrcaCore.Durable.Hosting",
            "OrcaCore.Providers.InMemory", "OrcaCore.Providers.PostgreSql", "OrcaCore.Dag", "OrcaCore.Dag.Hosting");
        PublicSurfaceCatalog.TargetAssemblies.Select(x => x.Tier).Distinct().Should().BeEquivalentTo(new[]
        {
            InterfaceTier.Application, InterfaceTier.Internal, InterfaceTier.Engine, InterfaceTier.RuntimeProtocol,
            InterfaceTier.ProviderAuthoring, InterfaceTier.DurableHosting, InterfaceTier.Dag, InterfaceTier.DagHosting
        });
        PublicSurfaceCatalog.TargetAudienceTiers.Should().Equal(Enum.GetValues<InterfaceTier>());
        PublicSurfaceCatalog.TargetCompanionFixtures.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new KeyValuePair<string, InterfaceTier>(
                "kubernetes-companion", InterfaceTier.Companion));
    }

    [Fact]
    public void DefinitionIrScanner_FollowsInheritedAndNestedPublicSignatures()
    {
        PublicSurfaceCatalog.FindCompilerIrSignatureTypes(typeof(SyntheticModeDefinition))
            .Should().Contain(typeof(CompiledWorkflowPlan),
                "an empty derived definition must not hide a compiled plan inherited through a nested signature");
    }

    [Fact]
    public void ExpectedRedLedger_HasEveryPhaseZeroBehaviorScenario()
    {
        var scenarios = FixtureDefinitions.Read<ExpectedRedScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/expected-red-scenarios.json");

        scenarios.Select(x => x.Id).Should().BeEquivalentTo(
            "exact-v1-package-manifest", "packed-package-consumers", "provider-author-store-contract",
            "exact-authoring-surface", "strong-values-codec-state", "structured-joins-foreach-paths",
            "registry-events-hosting-management", "typed-application-journeys",
            "deadline-retry-operation-coordinate", "typed-dag-contract", "lease-authoring-admission",
            "lease-retry-exit-quarantine", "lease-discovery-confirmation", "governance-provider-accounting");
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Select(x => x.TaskId).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x =>
            !string.IsNullOrWhiteSpace(x.TaskId) &&
            !string.IsNullOrWhiteSpace(x.Contract) &&
            !string.IsNullOrWhiteSpace(x.ExpectedFailure) &&
            !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
        scenarios.Select(x => x.TaskId).Should().BeEquivalentTo(
            "3.1", "3.2", "3.3", "3.4", "3.5", "3.6", "3.7", "3.8", "3.9", "3.10",
            "3.11a", "3.11b", "3.11c", "3.11d");
        scenarios.Should().NotContain(x =>
            x.Id.Contains("paused", StringComparison.OrdinalIgnoreCase) ||
            x.Id.Contains("expiry", StringComparison.OrdinalIgnoreCase) ||
            x.Id.Contains("statistics", StringComparison.OrdinalIgnoreCase) ||
            x.Contract.Contains("public job", StringComparison.OrdinalIgnoreCase));
    }

    private abstract class SyntheticDefinitionBase
    {
        public IReadOnlyList<CompiledWorkflowPlan> CompiledPlans => [];
    }

    private sealed class SyntheticModeDefinition : SyntheticDefinitionBase
    {
    }
}
