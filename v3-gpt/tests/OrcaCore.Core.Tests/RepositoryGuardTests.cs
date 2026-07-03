using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests;

public sealed partial class RepositoryGuardTests
{
    private static readonly HashSet<string> AcceptanceCriterionWaivers = new(StringComparer.Ordinal)
    {
        "AC-315",
        "JS-AC-008"
    };

    [Fact]
    public void AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver()
    {
        var repoRoot = FindRepoRoot();
        var catalogIds = CatalogedAcceptanceCriteria(repoRoot);
        var taggedIds = TaggedAcceptanceCriteria(repoRoot);

        catalogIds
            .Except(taggedIds, StringComparer.Ordinal)
            .Except(AcceptanceCriterionWaivers, StringComparer.Ordinal)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void IntegrationScenarioCatalog_HasTraitCoverage()
    {
        var repoRoot = FindRepoRoot();
        var catalogIds = CatalogedIntegrationScenarios(repoRoot);
        var taggedIds = TaggedIntegrationScenarios(repoRoot);

        catalogIds
            .Except(taggedIds, StringComparer.Ordinal)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void TestSources_DoNotUseWallClockTaskDelay()
    {
        var repoRoot = FindRepoRoot();
        var taskDelayCall = string.Concat("Task", ".Delay(");
        var bannedDelays = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "v3-gpt", "tests"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Line = index + 1,
                Text = line.Trim()
            }))
            .Where(candidate => candidate.Text.Contains(taskDelayCall, StringComparison.Ordinal))
            .Where(candidate => !candidate.Text.Contains("TimeProvider", StringComparison.Ordinal))
            .Where(candidate => !candidate.Text.Contains("Timeout.Infinite", StringComparison.Ordinal))
            .Select(candidate => $"{candidate.File}:{candidate.Line}: {candidate.Text}")
            .ToArray();

        bannedDelays.Should().BeEmpty();
    }

    [Fact]
    public void ProductionSources_DoNotUseVersion4GuidGeneration()
    {
        var repoRoot = FindRepoRoot();
        var guidNewGuidCall = string.Concat("Guid", ".NewGuid(");
        var bannedCalls = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "v3-gpt", "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Line = index + 1,
                Text = line.Trim()
            }))
            .Where(candidate => candidate.Text.Contains(guidNewGuidCall, StringComparison.Ordinal))
            .Select(candidate => $"{candidate.File}:{candidate.Line}: {candidate.Text}")
            .ToArray();

        bannedCalls.Should().BeEmpty();
    }

    [Fact]
    public void PostgreSqlStringConstructors_ValidateConnectionStringBeforeCreatingDataSource()
    {
        var repoRoot = FindRepoRoot();
        var directDataSourceConstruction = Directory
            .EnumerateFiles(
                Path.Combine(repoRoot, "v3-gpt", "src", "OrcaCore.Providers.PostgreSql"),
                "*.cs",
                SearchOption.TopDirectoryOnly)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Line = index + 1,
                Text = line.Trim()
            }))
            .Where(candidate => candidate.Text.Contains(
                "this(NpgsqlDataSource.Create(connectionString))",
                StringComparison.Ordinal))
            .Select(candidate => $"{candidate.File}:{candidate.Line}: {candidate.Text}")
            .ToArray();

        directDataSourceConstruction.Should().BeEmpty();
    }

    [Fact]
    public void ProviderSources_DoNotDefineStronglyTypedIdJsonConverters()
    {
        var repoRoot = FindRepoRoot();
        var providerLocalConverters = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "v3-gpt", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetRelativePath(repoRoot, file).Contains("OrcaCore.Providers.", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Line = index + 1,
                Text = line.Trim()
            }))
            .Where(candidate =>
                candidate.Text.Contains("JsonConverter<", StringComparison.Ordinal) ||
                candidate.Text.Contains(".Converters.Add(", StringComparison.Ordinal))
            .Select(candidate => $"{candidate.File}:{candidate.Line}: {candidate.Text}")
            .ToArray();

        providerLocalConverters.Should().BeEmpty();
    }

    [Fact]
    public void CiWorkflow_CollectsCoverletOutputAndPublishesCoverageArtifact()
    {
        var repoRoot = FindRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));

        workflow.Should().Contain("--collect:\"XPlat Code Coverage\"");
        workflow.Should().Contain("actions/upload-artifact");
        workflow.Should().Contain("TestResults");
    }

    [Fact]
    public void CiWorkflow_EnforcesCoreEngineCoverageBaseline()
    {
        var repoRoot = FindRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));

        workflow.Should().Contain("dotnet-reportgenerator-globaltool");
        workflow.Should().Contain("CoverageReport");
        workflow.Should().Contain("-assemblyfilters:\"+OrcaCore.Engine.*\"");
        workflow.Should().Contain("MIN_CORE_ENGINE_LINE_RATE");
        workflow.Should().Contain("Cobertura.xml");
        workflow.Should().Contain("line-rate");
    }

    [Fact]
    public void BenchmarkProject_HasRunnableScenariosAndCiBuildCoverage()
    {
        var repoRoot = FindRepoRoot();
        var benchmarkProject = Path.Combine(
            repoRoot,
            "v3-gpt",
            "benchmarks",
            "OrcaCore.Benchmarks",
            "OrcaCore.Benchmarks.csproj");
        var benchmarkProgram = Path.Combine(
            repoRoot,
            "v3-gpt",
            "benchmarks",
            "OrcaCore.Benchmarks",
            "Program.cs");
        var scenariosPath = Path.Combine(
            repoRoot,
            "v3-gpt",
            "benchmarks",
            "OrcaCore.Benchmarks",
            "Scenarios");
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));

        File.Exists(benchmarkProject).Should().BeTrue();
        File.ReadAllText(benchmarkProgram).Should().Contain("BenchmarkSwitcher");
        Directory.EnumerateFiles(scenariosPath, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .Should()
            .Contain(source => source.Contains("[Benchmark]", StringComparison.Ordinal));
        workflow.Should().Contain("Build benchmarks");
        workflow.Should().Contain("OrcaCore.Benchmarks.csproj");
    }

    [Fact]
    public void HostingProject_DoesNotReferenceRabbitMqProvider()
    {
        var repoRoot = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(
            repoRoot,
            "v3-gpt",
            "src",
            "OrcaCore.Hosting",
            "OrcaCore.Hosting.csproj"));

        project.Should().NotContain("OrcaCore.Providers.RabbitMq");
        project.Should().NotContain("RabbitMQ.Client");
    }

    [Fact]
    public void GlobalJson_UsesCiSdkFeatureBandWithRollForward()
    {
        var repoRoot = FindRepoRoot();
        var globalJsonFiles = new[]
        {
            Path.Combine(repoRoot, "global.json"),
            Path.Combine(repoRoot, "v3-gpt", "global.json")
        };

        foreach (var globalJson in globalJsonFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(globalJson));
            var sdk = document.RootElement.GetProperty("sdk");
            sdk.GetProperty("version").GetString().Should().Be("10.0.301");
            sdk.GetProperty("rollForward").GetString().Should().Be("latestFeature");
        }
    }

    private static IReadOnlySet<string> CatalogedAcceptanceCriteria(string repoRoot)
    {
        var document12 = File.ReadAllText(Path.Combine(repoRoot, "docs", "specs", "12-acceptance-criteria.md"));
        var document14 = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "specs",
            "14-driving-scenario-eks-job-scheduler.md"));

        return AcceptanceCatalogRegex()
            .Matches(string.Concat(document12, Environment.NewLine, document14))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> TaggedAcceptanceCriteria(string repoRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repoRoot, "v3-gpt", "tests"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .SelectMany(source => AcceptanceTraitRegex().Matches(source))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> CatalogedIntegrationScenarios(string repoRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repoRoot, "docs", "review", "integration-tests"), "*.md", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .SelectMany(source => IntegrationScenarioIdRegex().Matches(source))
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> TaggedIntegrationScenarios(string repoRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repoRoot, "v3-gpt", "tests", "OrcaCore.Integration.Tests"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .SelectMany(source => IntegrationScenarioTraitRegex().Matches(source))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "specs", "12-acceptance-criteria.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    [GeneratedRegex(@"\*\*((?:AC|JS-AC)-\d{3})\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex AcceptanceCatalogRegex();

    [GeneratedRegex(@"Trait\(""AC"",\s*""((?:AC|JS-AC)-\d{3})""\)", RegexOptions.CultureInvariant)]
    private static partial Regex AcceptanceTraitRegex();

    [GeneratedRegex(@"INT-[A-Z0-9]+-\d{3}", RegexOptions.CultureInvariant)]
    private static partial Regex IntegrationScenarioIdRegex();

    [GeneratedRegex(@"Trait\(Traits\.Scenario,\s*""(INT-[A-Z0-9]+-\d{3})""\)", RegexOptions.CultureInvariant)]
    private static partial Regex IntegrationScenarioTraitRegex();
}
