using System.Diagnostics;
using System.Reflection;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
[Collection(CompileFixtureCollection.Name)]
public sealed class InfrastructureGuards
{
    private static readonly string[] RetiredProviderRoots =
    [
        "OrcaCore.Providers.RabbitMq",
        "OrcaCore.Providers.Redis",
        "OrcaCore.Providers.Relational",
        "OrcaCore.Providers.SqlServer",
        "OrcaCore.Providers.ZeroMq"
    ];

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
    public void ExactV1Manifest_HasNoOrphanedProviderSourcesTestsOrSdkVersions()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        foreach (var provider in RetiredProviderRoots)
        {
            EnumerateNonBuildFiles(Path.Combine(root, "src", provider))
                .Should().BeEmpty($"{provider} is outside the exact v1 manifest and has no approved source owner");
            EnumerateNonBuildFiles(Path.Combine(root, "tests", $"{provider}.Tests"))
                .Should().BeEmpty($"{provider} has no active certification project in v1");
        }

        var packageVersions = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
        packageVersions.Should().NotContainAny(
            "Microsoft.Data.SqlClient",
            "NetMQ",
            "RabbitMQ.Client",
            "StackExchange.Redis",
            "Testcontainers.MsSql",
            "Testcontainers.RabbitMq",
            "Testcontainers.Redis");

        var registry = File.ReadAllText(Path.Combine(root, "docs", "specs", "13-phasing-and-open-questions.md"));
        registry.Should().Contain("Additional durable storage providers");
        registry.Should().Contain("SQL Server");
    }

    [Fact]
    public void RelationalSchemas_ContainNoCompatibilityAlterTableStatements()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var findings = Directory
            .EnumerateDirectories(Path.Combine(root, "src"), "OrcaCore.Providers.*")
            .SelectMany(providerRoot => Directory.Exists(Path.Combine(providerRoot, "Migrations"))
                ? Directory.EnumerateFiles(Path.Combine(providerRoot, "Migrations"), "*.sql")
                : [])
            .SelectMany(path => FindCompatibilityAlterTables(path, File.ReadAllText(path)))
            .ToArray();

        findings.Should().BeEmpty(
            "greenfield provider schemas must create the complete current shape and retain no upgrade ALTER TABLE DDL");
    }

    [Fact]
    public void RelationalSchemaScanner_ParsesQualifiedIdentifiersAndIgnoresCommentsAndLiterals()
    {
        const string sql =
            """
            alter table if exists public.orcacore_inbox add column payload bytea;
            ALTER TABLE [dbo].[orcacore_outbox] ADD [poison_code] nvarchar(256) null;
            alter table only "custom"."orcacore_checkpoints" add column "runtime_state" jsonb;
            -- alter table orcacore_commented add column ignored integer;
            select 'alter table orcacore_literal add column ignored integer;';
            select $$alter table orcacore_dollar_literal add column ignored integer;$$;
            select $body$alter table orcacore_tagged_literal add column ignored integer;$body$;
            /* alter table orcacore_block_comment add column ignored integer; */
            alter table application_table add column allowed integer;
            """;

        FindCompatibilityAlterTables("renamed.sql", sql)
            .Should().Equal(
                "renamed.sql: public.orcacore_inbox",
                "renamed.sql: dbo.orcacore_outbox",
                "renamed.sql: custom.orcacore_checkpoints");
    }

    private static string[] EnumerateNonBuildFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> FindCompatibilityAlterTables(string sourcePath, string source)
    {
        var tokens = TokenizeSql(source);
        var findings = new List<string>();
        for (var index = 0; index + 2 < tokens.Count; index++)
        {
            if (!tokens[index].Is("alter") || !tokens[index + 1].Is("table"))
            {
                continue;
            }

            var cursor = index + 2;
            if (tokens[cursor].Is("if") && cursor + 1 < tokens.Count && tokens[cursor + 1].Is("exists"))
            {
                cursor += 2;
            }

            if (cursor < tokens.Count && tokens[cursor].Is("only"))
            {
                cursor++;
            }

            if (cursor >= tokens.Count || !tokens[cursor].Identifier)
            {
                continue;
            }

            var qualifiedName = tokens[cursor].Text;
            var tableName = tokens[cursor].Text;
            while (cursor + 2 < tokens.Count && tokens[cursor + 1].Text == "." && tokens[cursor + 2].Identifier)
            {
                cursor += 2;
                tableName = tokens[cursor].Text;
                qualifiedName = $"{qualifiedName}.{tokens[cursor].Text}";
            }

            if (tableName.StartsWith("orcacore_", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add($"{sourcePath}: {qualifiedName}");
            }
        }

        return findings;
    }

    private static IReadOnlyList<SqlToken> TokenizeSql(string source)
    {
        var tokens = new List<SqlToken>();
        for (var index = 0; index < source.Length;)
        {
            if (char.IsWhiteSpace(source[index]))
            {
                index++;
                continue;
            }

            if (index + 1 < source.Length && source[index] == '-' && source[index + 1] == '-')
            {
                index += 2;
                while (index < source.Length && source[index] is not '\r' and not '\n')
                {
                    index++;
                }
                continue;
            }

            if (index + 1 < source.Length && source[index] == '/' && source[index + 1] == '*')
            {
                var depth = 1;
                index += 2;
                while (index < source.Length && depth > 0)
                {
                    if (index + 1 < source.Length && source[index] == '/' && source[index + 1] == '*')
                    {
                        depth++;
                        index += 2;
                    }
                    else if (index + 1 < source.Length && source[index] == '*' && source[index + 1] == '/')
                    {
                        depth--;
                        index += 2;
                    }
                    else
                    {
                        index++;
                    }
                }
                continue;
            }

            if (source[index] == '$')
            {
                var delimiterEnd = index + 1;
                while (delimiterEnd < source.Length &&
                       (char.IsLetterOrDigit(source[delimiterEnd]) || source[delimiterEnd] == '_'))
                {
                    delimiterEnd++;
                }

                if (delimiterEnd < source.Length && source[delimiterEnd] == '$')
                {
                    var delimiter = source[index..(delimiterEnd + 1)];
                    var bodyEnd = source.IndexOf(delimiter, delimiterEnd + 1, StringComparison.Ordinal);
                    index = bodyEnd < 0 ? source.Length : bodyEnd + delimiter.Length;
                    continue;
                }
            }

            if (source[index] == '\'')
            {
                index++;
                while (index < source.Length)
                {
                    if (source[index] != '\'')
                    {
                        index++;
                        continue;
                    }

                    if (index + 1 < source.Length && source[index + 1] == '\'')
                    {
                        index += 2;
                        continue;
                    }

                    index++;
                    break;
                }
                continue;
            }

            if (source[index] is '"' or '[')
            {
                var opening = source[index++];
                var closing = opening == '"' ? '"' : ']';
                var value = new System.Text.StringBuilder();
                while (index < source.Length)
                {
                    if (source[index] != closing)
                    {
                        value.Append(source[index++]);
                        continue;
                    }

                    if (index + 1 < source.Length && source[index + 1] == closing)
                    {
                        value.Append(closing);
                        index += 2;
                        continue;
                    }

                    index++;
                    break;
                }
                tokens.Add(new SqlToken(value.ToString(), true));
                continue;
            }

            if (char.IsLetter(source[index]) || source[index] == '_')
            {
                var start = index++;
                while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] is '_' or '$'))
                {
                    index++;
                }
                tokens.Add(new SqlToken(source[start..index], true));
                continue;
            }

            tokens.Add(new SqlToken(source[index++].ToString(), false));
        }

        return tokens;
    }

    private readonly record struct SqlToken(string Text, bool Identifier)
    {
        public bool Is(string value) => Identifier && Text.Equals(value, StringComparison.OrdinalIgnoreCase);
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
