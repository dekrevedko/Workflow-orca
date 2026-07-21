using System.Xml.Linq;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class ProviderAuthorInfrastructureGuards
{
    [Fact]
    public void CustomHostFixture_UsesOnlyTheApprovedProviderAuthoringClosure()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var contract = FixtureDefinitions.Read<V1PublicContract>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json");
        var projectPath = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards", "PackageFixtures",
            "ProviderCustomHost", "ProviderCustomHost.csproj");
        var project = XDocument.Load(projectPath);

        project.Descendants("ProjectReference").Should().BeEmpty();
        project.Descendants("PackageReference").Select(x => x.Attribute("Include")!.Value).Should().Equal(
            "OrcaCore", "OrcaCore.Runtime.Protocol", "OrcaCore.Provider.Abstractions");
        project.Descendants("PackageReference").Should().NotContain(x =>
            x.Attribute("Include")!.Value.Contains("Engine", StringComparison.Ordinal) ||
            x.Attribute("Include")!.Value.Contains("Hosting", StringComparison.Ordinal) ||
            x.Attribute("Include")!.Value.Contains("Dag", StringComparison.Ordinal));
        contract.Packages.Single(x => x.Id == "OrcaCore.Provider.Abstractions").Dependencies.Should().Equal(
            "OrcaCore", "OrcaCore.Runtime.Protocol");
        contract.Packages.Single(x => x.Id == "OrcaCore.Runtime.Protocol").Dependencies.Should().Equal("OrcaCore");
        contract.Packages.Where(x => x.Id != "OrcaCore.Provider.Abstractions")
            .SelectMany(x => x.Dependencies.Select(dependency => (Owner: x.Id, Dependency: dependency)))
            .Should().NotContain(edge => edge.Dependency == "OrcaCore.Provider.Abstractions" &&
                edge.Owner == "OrcaCore.Runtime.Protocol", "the protocol-to-provider reverse edge is forbidden");
    }

    [Fact]
    public void CustomHostFixture_DefinesTheCompleteCopyingAtomicStoreContract()
    {
        var source = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests",
            "OrcaCore.DeveloperSurface.Guards", "PackageFixtures", "ProviderCustomHost", "Program.cs"));

        source.Should().Contain("IDurableResourceGovernanceStore");
        source.Should().Contain("ValueTask<ResourceGovernanceStream> LoadAsync(");
        source.Should().Contain("ValueTask<ResourceGovernanceAppendResult> AppendAsync(");
        source.Should().Contain("long expectedVersion");
        source.Should().Contain("IReadOnlyList<ResourceGovernanceRecord> records");
        source.Should().Contain("ResourceGovernanceRecord.FromPersisted(");
        source.Should().Contain("record.Payload.ToArray()");
        source.Should().Contain("record.FormatId");
        source.Should().Contain("record.Checksum");
        source.Should().Contain("ResourceGovernanceStream.Create(records.Count, records)");
        source.Should().Contain("if (expectedVersion != persisted.Count)");
        source.Should().Contain("if (copiedBatch.Count == 0)");
        source.Should().Contain("copiedBatch[index].Sequence != expectedVersion + index + 1");
        source.Should().Contain("ResourceGovernanceStream.Create(prospective.Count, prospective)");
        source.IndexOf("ResourceGovernanceStream.Create(prospective.Count, prospective)", StringComparison.Ordinal)
            .Should().BeLessThan(source.IndexOf("persisted.AddRange(copiedBatch)", StringComparison.Ordinal),
                "the complete copied stream must validate before the whole batch is committed");
        source.Should().Contain("new ResourceGovernanceAppendResult.Conflict(persisted.Count)");
        source.Should().Contain("new ResourceGovernanceAppendResult.Committed(persisted.Count)");
        source.Should().Contain("lock (gate)");
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class ProviderAuthorExpectedRedGuards
{
    [Fact]
    public void ExactProviderAuthoringProjects_ExistForTheFixtureToCompileAgainst()
    {
        var src = Path.Combine(FixtureDefinitions.RepositoryRoot(), "src");
        File.Exists(Path.Combine(src, "OrcaCore.Runtime.Protocol", "OrcaCore.Runtime.Protocol.csproj"))
            .Should().BeTrue("task 7.1 must create the exact runtime-protocol package");
        File.Exists(Path.Combine(src, "OrcaCore.Provider.Abstractions", "OrcaCore.Provider.Abstractions.csproj"))
            .Should().BeTrue("task 7.1 must create the exact provider-authoring package");
    }
}
