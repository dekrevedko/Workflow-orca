using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Core.Tests;

public sealed partial class RepositoryGuardTests
{
    private static readonly IReadOnlyDictionary<string, string> AcceptanceCriterionWaivers =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AC-018"] = "The exact package-tier graph is implemented and verified by reshape-developer-facing-interfaces tasks 3.1-3.7.",
        ["AC-116"] = "Typed facade routing outcomes are implemented by reshape-developer-facing-interfaces tasks 6.8 and 7.1-7.3.",
        ["AC-317"] = "In-memory durable hosting diagnostics are implemented by reshape-developer-facing-interfaces tasks 8.4 and 9.2.",
        ["AC-318"] = "Explicit typed definition handles are implemented by reshape-developer-facing-interfaces tasks 6.1-6.2.",
        ["AC-319"] = "Split-host continuation is implemented and tested by reshape-developer-facing-interfaces tasks 2.7 and 6.6.",
        ["AC-320"] = "Worker-reported external-job failure is implemented by reshape-developer-facing-interfaces tasks 6.3-6.5.",
        ["AC-523"] = "Application-safe remediation and runtime time ownership are implemented by reshape-developer-facing-interfaces tasks 6.7 and 7.8.",
        ["AC-524"] = "The three concurrency lifetimes are implemented across the structured-fiber and interface-reshape resource slices.",
        ["DR-AC-007"] = "DR-P3 saga driving is still open.",
        ["DR-AC-008"] = "DR-P3 full DAG driving without manual pumping is still open.",
        ["DR-AC-012"] = "DR-P4 ephemeral/durable parity is still open.",
        ["DR-AC-026"] = "All continuation dispositions are not yet covered end to end.",
    };

    [Fact]
    public void ActiveWorkspace_UsesRepositoryRootProjectsAndCommands()
    {
        var repoRoot = FindRepoRoot();
        var allowedRoots = new HashSet<string>(StringComparer.Ordinal)
        {
            "benchmarks",
            "samples",
            "src",
            "tests"
        };

        Directory.Exists(Path.Combine(repoRoot, "v3-gpt")).Should().BeFalse();

        var solution = XDocument.Load(Path.Combine(repoRoot, "OrcaCore.slnx"));
        var projectPaths = solution
            .Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!.Replace('\\', '/'))
            .ToArray();

        projectPaths.Should().NotBeEmpty();
        projectPaths.Should().OnlyContain(path =>
            allowedRoots.Contains(path.Split('/', StringSplitOptions.RemoveEmptyEntries)[0]));
        projectPaths.Should().OnlyContain(path =>
            File.Exists(Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar))));
        projectPaths.Should().NotContain(path =>
            path.Contains("v3-gpt", StringComparison.OrdinalIgnoreCase));

        var staleCommands = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "docs", "implementation"), "*.md", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Line = index + 1,
                Text = line.Trim()
            }))
            .Where(candidate => IsWorkspaceCommand(candidate.Text))
            .Where(candidate => candidate.Text.Contains("v3-gpt", StringComparison.OrdinalIgnoreCase))
            .Select(candidate => $"{candidate.File}:{candidate.Line}: {candidate.Text}")
            .ToArray();

        staleCommands.Should().BeEmpty();
    }

    [Fact]
    public void AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver()
    {
        var repoRoot = FindRepoRoot();
        var catalogIds = CatalogedAcceptanceCriteria(repoRoot);
        var taggedIds = TaggedAcceptanceCriteria(repoRoot);

        catalogIds
            .Except(taggedIds, StringComparer.Ordinal)
            .Except(AcceptanceCriterionWaivers.Keys, StringComparer.Ordinal)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void AcceptanceCriterionWaivers_AreCatalogedReasonedAndNotAlreadyCovered()
    {
        var repoRoot = FindRepoRoot();
        var catalogIds = CatalogedAcceptanceCriteria(repoRoot);
        var taggedIds = TaggedAcceptanceCriteria(repoRoot);

        AcceptanceCriterionWaivers.Keys
            .Except(catalogIds, StringComparer.Ordinal)
            .Should().BeEmpty("waivers must reference a real catalog criterion");
        AcceptanceCriterionWaivers.Keys
            .Intersect(taggedIds, StringComparer.Ordinal)
            .Should().BeEmpty("a waiver must be removed when a real trait-tagged test is added");
        AcceptanceCriterionWaivers.Values
            .Should().OnlyContain(reason => !string.IsNullOrWhiteSpace(reason));
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
    public void TestcontainerBackedTestClasses_HaveContainerTrait()
    {
        var repoRoot = FindRepoRoot();
        var testcontainersUsing = string.Concat("using Testcontainers", ".");
        var untaggedContainerTests = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Select(file => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Source = File.ReadAllText(file)
            })
            .Where(candidate => candidate.Source.Contains(testcontainersUsing, StringComparison.Ordinal))
            .Where(candidate => TestClassRegex().IsMatch(candidate.Source))
            .Where(candidate =>
                !candidate.Source.Contains($"Trait({nameof(Traits)}.{nameof(Traits.Container)},", StringComparison.Ordinal) &&
                !candidate.Source.Contains("Trait(\"Container\",", StringComparison.Ordinal))
            .Select(candidate => candidate.File)
            .ToArray();

        untaggedContainerTests.Should().BeEmpty();
    }

    [Fact]
    public void TestSources_DoNotUseWallClockTaskDelay()
    {
        var repoRoot = FindRepoRoot();
        var taskDelayCall = string.Concat("Task", ".Delay(");
        var bannedDelays = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
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
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
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
    public void ProductionSources_DoNotUseWallClockStatics()
    {
        var repoRoot = FindRepoRoot();
        var bannedTokens = new[]
        {
            string.Concat("DateTimeOffset", ".UtcNow"),
            string.Concat("DateTimeOffset", ".Now"),
            string.Concat("DateTime", ".UtcNow"),
            string.Concat("DateTime", ".Now")
        };
        var bannedCalls = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Line = index + 1,
                Text = line.Trim()
            }))
            .Where(candidate => bannedTokens.Any(token => candidate.Text.Contains(token, StringComparison.Ordinal)))
            .Select(candidate => $"{candidate.File}:{candidate.Line}: {candidate.Text}")
            .ToArray();

        bannedCalls.Should().BeEmpty();
    }

    [Fact]
    public void ProductionSources_DoNotReferenceRetiredCursorExecution()
    {
        var repoRoot = FindRepoRoot();
        var retiredTokens = new[]
        {
            string.Concat("DurableDriver", "Cursor"),
            string.Concat("class DurableDriver", "Executor"),
            string.Concat("new DurableDriver", "Executor"),
            string.Concat("DurableDriver", "SegmentRun"),
            string.Concat("MergeCompleted", "Cursors"),
            string.Concat("DurableExecution", "Envelope") + ".ContentType"
        };
        var references = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Line = index + 1,
                Text = line.Trim()
            }))
            .Where(candidate => retiredTokens.Any(token =>
                candidate.Text.Contains(token, StringComparison.Ordinal)))
            .Select(candidate => $"{candidate.File}:{candidate.Line}: {candidate.Text}")
            .ToArray();

        references.Should().BeEmpty();
    }

    [Fact]
    public void DurableRuntime_RequiresCompiledCapabilityValidation()
    {
        var repoRoot = FindRepoRoot();
        var catalog = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "OrcaCore.Engine.Durable",
            "Driver",
            "DurableDriverCatalog.cs"));
        var fiberExecutor = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "OrcaCore.Engine.Durable",
            "Driver",
            "DurableFiberDriverExecutor.cs"));

        catalog.Should().Contain("definition.CompiledPlan.Instructions.Count == 0");
        catalog.Should().Contain("definition.CompiledPlan.Mode != WorkflowExecutionMode.Durable");
        fiberExecutor.Should().NotContain(string.Concat("Unsupported", "Instruction"));
        fiberExecutor.Should().NotContain("DR-P3");
    }

    [Fact]
    public void SqlServerProjectionQueries_DoNotFilterOrCountByMaterializingAllSnapshots()
    {
        var repoRoot = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(
            repoRoot,

            "src",
            "OrcaCore.Providers.SqlServer",
            "SqlServerWorkflowStore.Projections.cs"));

        source.Should().Contain("SqlServerProjectionQueryBuilder.SummaryWhereClause");
        source.Should().NotContain(".Where(snapshot => Matches(snapshot, query))");
        source.Should().NotContain("var snapshots = await ListCoreAsync(query");
    }

    [Fact]
    public void ProductionImplementationFiles_AboveThousandLines_HaveExplicitReviewWaiver()
    {
        var repoRoot = FindRepoRoot();
        var waiverDocument = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "review",
            "code-quality-remediation-summary-2026-07-04.md"));
        var oversizedFiles = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Select(file => new
            {
                File = Path.GetRelativePath(repoRoot, file),
                Lines = File.ReadLines(file).Count()
            })
            .Where(candidate => candidate.Lines > 1000)
            .Where(candidate => !waiverDocument.Contains(
                candidate.File.Replace('\\', '/'),
                StringComparison.Ordinal))
            .Select(candidate => $"{candidate.File}: {candidate.Lines} lines")
            .ToArray();

        oversizedFiles.Should().BeEmpty();
    }

    [Fact]
    public void PostgreSqlStringConstructors_ValidateConnectionStringBeforeCreatingDataSource()
    {
        var repoRoot = FindRepoRoot();
        var directDataSourceConstruction = Directory
            .EnumerateFiles(
                Path.Combine(repoRoot, "src", "OrcaCore.Providers.PostgreSql"),
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
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
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

            "benchmarks",
            "OrcaCore.Benchmarks",
            "OrcaCore.Benchmarks.csproj");
        var benchmarkProgram = Path.Combine(
            repoRoot,

            "benchmarks",
            "OrcaCore.Benchmarks",
            "Program.cs");
        var scenariosPath = Path.Combine(
            repoRoot,

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
            Path.Combine(repoRoot, "global.json")
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
        return Directory
            .EnumerateFiles(Path.Combine(repoRoot, "docs", "specs"), "*.md", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .SelectMany(source => AcceptanceCatalogRegex().Matches(source))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> TaggedAcceptanceCriteria(string repoRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
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
            .EnumerateFiles(Path.Combine(repoRoot, "tests", "OrcaCore.Integration.Tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
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

    private static bool IsBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin", StringComparer.OrdinalIgnoreCase) ||
            parts.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsWorkspaceCommand(string line)
    {
        var command = line.TrimStart(' ', '\t', '-', '>');
        return command.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("Set-Location ", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("Push-Location ", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\*\*((?:AC|JS-AC|DR-AC)-\d{3})\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex AcceptanceCatalogRegex();

    [GeneratedRegex(@"Trait\(""AC"",\s*""((?:AC|JS-AC|DR-AC)-\d{3})""\)", RegexOptions.CultureInvariant)]
    private static partial Regex AcceptanceTraitRegex();

    [GeneratedRegex(@"INT-[A-Z0-9]+-\d{3}", RegexOptions.CultureInvariant)]
    private static partial Regex IntegrationScenarioIdRegex();

    [GeneratedRegex(@"Trait\(Traits\.Scenario,\s*""(INT-[A-Z0-9]+-\d{3})""\)", RegexOptions.CultureInvariant)]
    private static partial Regex IntegrationScenarioTraitRegex();

    [GeneratedRegex(@"\bclass\s+\w+Tests\b", RegexOptions.CultureInvariant)]
    private static partial Regex TestClassRegex();
}
