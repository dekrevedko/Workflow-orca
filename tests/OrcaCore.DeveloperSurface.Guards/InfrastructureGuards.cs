using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
[Collection(CompileFixtureCollection.Name)]
public sealed class InfrastructureGuards
{
    private static readonly Regex OwnershipCompatibilityAlter = new(
        """
        \balter\s+table\s+
        (?:if\s+exists\s+)?
        (?:only\s+)?
        (?:
            (?:"[^"]+"|\[[^\]]+\]|[a-z_][a-z0-9_$]*)\s*\.\s*
        )?
        (?:
            "orcacore_resource_(?:tickets|waiters)"
            |\[orcacore_resource_(?:tickets|waiters)\]
            |orcacore_resource_(?:tickets|waiters)
        )
        (?:\s*\*)?
        \s+add\s+
        (?:column\s+)?
        (?:if\s+not\s+exists\s+)?
        (?:
            "(?:fiber_id|scope_id)"
            |\[(?:fiber_id|scope_id)\]
            |(?:fiber_id|scope_id)
        )
        (?=\s|[;,]|$)
        """,
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnorePatternWhitespace);

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
    public void DefinitionIrScanner_ReportsNoCompilerIrOnTheRuntimeDefinition()
    {
        var runtimeDefinition = Assembly.Load("OrcaCore.Core")
            .GetType("OrcaCore.Core.Definitions.WorkflowDefinition`1", throwOnError: true)!;
        PublicSurfaceCatalog.FindCompilerIrSignatureTypes(runtimeDefinition)
            .Should().BeEmpty("compiler IR must be implementation-only");
    }

    [Fact]
    public void RelationalResourcePoolOwnership_UsesGreenfieldFirstCreateSchemasOnly()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var postgreSqlRoot = Path.Combine(root, "src", "OrcaCore.Providers.PostgreSql");
        var sqlServerRoot = Path.Combine(root, "src", "OrcaCore.Providers.SqlServer");

        File.Exists(Path.Combine(postgreSqlRoot, "Migrations", "007_resource_ownership.sql"))
            .Should().BeFalse("the greenfield PostgreSQL schema must not retain an ownership upgrade migration");
        File.Exists(Path.Combine(sqlServerRoot, "Migrations", "008_resource_ownership.sql"))
            .Should().BeFalse("the greenfield SQL Server schema must not retain an ownership upgrade migration");

        var postgreSqlInitializer = File.ReadAllText(
            Path.Combine(postgreSqlRoot, "PostgreSqlResourcePoolStore.cs"));

        foreach (var firstCreateSchema in new[] { "001_initial.sql", "003_resource_pools.sql" })
        {
            var sql = File.ReadAllText(Path.Combine(sqlServerRoot, "Migrations", firstCreateSchema));
            sql.Split("fiber_id nvarchar(256) null", StringSplitOptions.None)
                .Should().HaveCount(3, "tickets and waiters must both own fiber identity from first creation");
            sql.Split("scope_id nvarchar(256) null", StringSplitOptions.None)
                .Should().HaveCount(3, "tickets and waiters must both own scope identity from first creation");
        }

        var compatibilityAlters = FindOwnershipCompatibilityAlters(
            Path.Combine(postgreSqlRoot, "PostgreSqlResourcePoolStore.cs"),
            postgreSqlInitializer);
        foreach (var migrationRoot in new[]
                 {
                     Path.Combine(postgreSqlRoot, "Migrations"),
                     Path.Combine(sqlServerRoot, "Migrations")
                 })
        {
            foreach (var migration in Directory.EnumerateFiles(migrationRoot, "*.sql"))
            {
                compatibilityAlters.AddRange(FindOwnershipCompatibilityAlters(
                    migration,
                    File.ReadAllText(migration)));
            }
        }

        compatibilityAlters.Should().BeEmpty(
            "ownership columns belong in greenfield first-create schemas, but compatibility DDL was found:{0}{1}",
            Environment.NewLine,
            string.Join(Environment.NewLine, compatibilityAlters));
    }

    [Fact]
    public void RelationalResourcePoolOwnership_ScannerRejectsRenamedFormerPostgreSqlMigration()
    {
        const string renamedMigration = "Migrations/999_renamed_resource_ownership.sql";
        const string formerPostgreSqlMigration =
            """
            alter table if exists orcacore_resource_tickets
                add column if not exists fiber_id text null;

            alter table if exists orcacore_resource_tickets
                add column if not exists scope_id text null;

            alter table if exists orcacore_resource_waiters
                add column if not exists fiber_id text null;

            alter table if exists orcacore_resource_waiters
                add column if not exists scope_id text null;
            """;

        var formerMigrationFindings = FindOwnershipCompatibilityAlters(
            renamedMigration,
            formerPostgreSqlMigration);

        formerMigrationFindings.Should().HaveCount(
            4,
            "the exact deleted PostgreSQL ownership migration must remain rejected under any filename");
        formerMigrationFindings.Should().OnlyContain(
            finding => finding.StartsWith(renamedMigration, StringComparison.Ordinal));

        var qualifiedVariants = new[]
        {
            """alter table if exists public.orcacore_resource_tickets add column if not exists fiber_id text null;""",
            """alter table if exists only "public"."orcacore_resource_waiters" add column if not exists "scope_id" text null;""",
            """alter table [dbo].[orcacore_resource_tickets] add [fiber_id] nvarchar(256) null;"""
        };

        qualifiedVariants.Should().OnlyContain(
            sql => FindOwnershipCompatibilityAlters(renamedMigration, sql).Count == 1,
            "optional PostgreSQL clauses, schema qualification, and quoted identifiers must not bypass the scanner");
    }

    private static List<string> FindOwnershipCompatibilityAlters(string sourcePath, string source)
    {
        return OwnershipCompatibilityAlter
            .Matches(source)
            .Select(match => $"{sourcePath}: {match.Value}")
            .ToList();
    }

    [Fact]
    public void ExactAuthoringPositiveFixture_Compiles()
    {
        var project = Path.Combine(
            FixtureDefinitions.RepositoryRoot(),
            "tests",
            "OrcaCore.DeveloperSurface.Guards",
            "CompileFixtures",
            "ExactAuthoring",
            "ExactAuthoring.csproj");
        var start = new ProcessStartInfo(
            "dotnet",
            $"build \"{project}\" --configuration Release --nologo --verbosity quiet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FixtureDefinitions.RepositoryRoot()
        };

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.Should().Be(
            0,
            "the exact companion declarations and their positive consumer usage must compile; output: {0}",
            output);
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

}
