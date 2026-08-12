using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class SqlServerProviderExpectedRedGuards
{
    [Fact]
    public void Product_ContainsTheApprovedSqlServerProviderProject()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        File.Exists(Path.Combine(
                root,
                "src",
                "OrcaCore.Providers.SqlServer",
                "OrcaCore.Providers.SqlServer.csproj"))
            .Should().BeTrue("task 7.17d must add a new provider project rather than revive the deleted shape");
    }

    [Fact]
    public void ExactManifest_ContainsTheSqlServerPackageAndRegistrationOwner()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.DeveloperSurface.Guards",
            "Fixtures",
            "v1-public-contract.json"));

        contract.Should().Contain("\"id\": \"OrcaCore.Providers.SqlServer\"");
        contract.Should().Contain("\"assembly\": \"OrcaCore.Providers.SqlServer\"");
        PublicSurfaceCatalog.TargetAssemblies.Select(assembly => assembly.Name)
            .Should().Contain("OrcaCore.Providers.SqlServer");
    }

    [Fact]
    public void DependencyCatalog_ContainsOnlyTheOwnedSqlServerPackages()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        packages.Should().Contain("Microsoft.Data.SqlClient");
        packages.Should().Contain("Testcontainers.MsSql");

        var providerProject = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OrcaCore.Providers.SqlServer",
            "OrcaCore.Providers.SqlServer.csproj"));
        providerProject.Should().Contain("Microsoft.Data.SqlClient");
        providerProject.Should().NotContain("Npgsql");
        providerProject.Should().NotContain("Dapper");

        var testProject = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.Providers.SqlServer.Tests",
            "OrcaCore.Providers.SqlServer.Tests.csproj"));
        testProject.Should().Contain("Testcontainers.MsSql");
        testProject.Should().NotContain("Testcontainers.PostgreSql");
    }

    [Fact]
    public void Certification_ContainsTheSqlServerProjectAndGreenfieldSchema()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        File.Exists(Path.Combine(
                root,
                "tests",
                "OrcaCore.Providers.SqlServer.Tests",
                "OrcaCore.Providers.SqlServer.Tests.csproj"))
            .Should().BeTrue("task 7.17d requires a real-storage owning certification project");
        File.Exists(Path.Combine(
                root,
                "src",
                "OrcaCore.Providers.SqlServer",
                "Migrations",
                "001_initial.sql"))
            .Should().BeTrue("the SQL Server provider must own one complete greenfield first-create schema");
    }

    [Fact]
    public void ContinuousIntegration_RunsTheSqlServerRealStorageLane()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        workflow.Should().Contain(
            "tests/OrcaCore.Providers.SqlServer.Tests/OrcaCore.Providers.SqlServer.Tests.csproj",
            "task 7.17d must restore real SQL Server certification to CI with the provider implementation");
    }
}
