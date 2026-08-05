using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed partial class RecoveryCrosswalkInfrastructureGuards
{
    private const string CrosswalkPath =
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/section-07-r-declaration-crosswalk.json";

    private static readonly string[] RequiredEvidenceKeys =
    {
        "E-AUTH", "E-CODEC", "E-DEADLINE", "E-EVENT", "E-FACADE", "E-FANOUT",
        "E-HOST", "E-LEASE", "E-LIFE", "E-OBS", "E-RECOVERY", "E-STRONG"
    };

    private static readonly string[] RequiredExcludedAcceptanceSources =
    {
        "ChildCompensationAcceptanceTests.cs",
        "ChildWorkflowAcceptanceTests.cs",
        "DagAcceptanceTests.cs",
        "DagObservabilityAcceptanceTests.cs",
        "ExternalJobAcceptanceTests.cs",
        "LegacyManagementAcceptanceTests.cs",
        "LegacyOperationsAcceptanceTests.cs",
        "RetentionAcceptanceTests.cs",
        "SagaAcceptanceTests.cs",
        "WhenFirstAcceptanceTests.cs",
        "YieldAcceptanceTests.cs"
    };

    private static readonly string[] FiveNewlyExcludedLegacyAcceptanceSources =
    {
        "ChildCompensationAcceptanceTests.cs",
        "ExternalJobAcceptanceTests.cs",
        "LegacyManagementAcceptanceTests.cs",
        "RetentionAcceptanceTests.cs"
    };

    private static readonly IReadOnlyDictionary<string, int> MixedRFiles = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["tests/OrcaCore.Core.Tests/Compilation/DefinitionCompilerTests.cs"] = 30,
        ["tests/OrcaCore.Core.Tests/Contracts/StrongValueContractTests.cs"] = 5,
        ["tests/OrcaCore.Engine.Durable.Tests/Driver/DurableDriverReviewedAcceptanceTests.cs"] = 11,
        ["tests/OrcaCore.Engine.Ephemeral.Tests/Execution/RoutingTests.cs"] = 5,
        ["tests/OrcaCore.Engine.Ephemeral.Tests/Execution/StructuredFiberExecutionTests.cs"] = 33
    };

    private static readonly IReadOnlyDictionary<string, string> RequiredReclassifiedDeclarations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tests/OrcaCore.Engine.Ephemeral.Tests/Execution/RoutingTests.cs::RaiseByCorrelationAsync_MultipleActiveWaits_ReturnsAmbiguousWithoutDelivery"] = "L",
            ["tests/OrcaCore.Engine.Ephemeral.Tests/Execution/RoutingTests.cs::RaiseByDefinitionAsync_MultipleDefinitions_DeliversOnlyTargetDefinition"] = "L",
            ["tests/OrcaCore.Engine.Ephemeral.Tests/Execution/RoutingTests.cs::WaitRegistration_DuplicateCorrelationAcrossInstances_Succeeds"] = "L",
            ["tests/OrcaCore.Engine.Durable.Tests/Driver/DurableDriverReviewedAcceptanceTests.cs::DurableWhenFirst_ConcurrentMatchesAcrossHostsCommitOneWinner"] = "L",
            ["tests/OrcaCore.Engine.Durable.Tests/Driver/DurableDriverReviewedAcceptanceTests.cs::PublicFacade_RoutesByCorrelationAndDefinitionAndExposesManagement"] = "L",
            ["tests/OrcaCore.Engine.Durable.Tests/Driver/DurableDriverReviewedAcceptanceTests.cs::RunChild_DriverCheckpointAndParentResumeSurviveHostReplacement"] = "D8"
        };

    [Fact]
    public void GuardDocumentationInputs_ExcludeHistoricalArchiveAndRecursiveDocsScans()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var guardRoot = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards");
        var findings = Directory.EnumerateFiles(guardRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .SelectMany(item => new[]
            {
                ArchivedDocumentationPathRegex().IsMatch(item.Source)
                    ? $"{Path.GetRelativePath(root, item.Path)} -> archived documentation path"
                    : null,
                RecursiveDocumentationScanRegex().IsMatch(item.Source)
                    ? $"{Path.GetRelativePath(root, item.Path)} -> recursive documentation scan"
                    : null
            })
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        findings.Should().BeEmpty(
            "guards must read explicit active normative/binding paths; historical documents are provenance, not current guidance");
    }

    [Fact]
    public void RDeclarationCrosswalk_MapsExactly373DeclarationsAndSixExplicitNonRRows()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, CrosswalkPath)));
        var crosswalk = document.RootElement;

        crosswalk.GetProperty("schemaVersion").GetInt32().Should().Be(4);
        crosswalk.GetProperty("disposition").GetString().Should().Be("R");
        crosswalk.GetProperty("sourceBaseline").GetString().Should().Be("d76192f089dd07f68e310c21fe4e5a38dd93cf7f");
        crosswalk.GetProperty("section7Checkpoint").GetString().Should().Be("50254d08175431896d580ecfcc93d8e49e1c2ec7");
        crosswalk.GetProperty("expectedDeclarationCount").GetInt32().Should().Be(373);

        var evidenceCatalog = crosswalk.GetProperty("evidenceCatalog").EnumerateArray().ToArray();
        var definedEvidenceKeys = evidenceCatalog
            .Select(entry => entry.GetProperty("key").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        evidenceCatalog.Select(entry => entry.GetProperty("key").GetString()!).Should().Equal(RequiredEvidenceKeys);
        evidenceCatalog.Select(entry => entry.GetProperty("meaning").GetString())
            .Should().OnlyContain(meaning => !string.IsNullOrWhiteSpace(meaning));

        var accounting = crosswalk.GetProperty("accounting");
        accounting.GetProperty("excludedFiles").GetInt32().Should().Be(102);
        accounting.GetProperty("excludedDeclarations").GetInt32().Should().Be(554);
        accounting.GetProperty("rDeclarations").GetInt32().Should().Be(373);
        accounting.GetProperty("section8Declarations").GetInt32().Should().Be(39);
        accounting.GetProperty("laterOrLegacyDeclarations").GetInt32().Should().Be(135);
        accounting.GetProperty("removedConceptDeclarations").GetInt32().Should().Be(7);
        accounting.GetProperty("unsupportedBlockers").GetInt32().Should().Be(0);
        accounting.GetProperty("activeAcceptanceDeclarations").GetInt32().Should().Be(37);
        accounting.GetProperty("excludedLegacyAcceptanceDeclarations").GetInt32().Should().Be(5);
        accounting.EnumerateObject().Select(property => property.Name).Should().OnlyHaveUniqueItems();
        (accounting.GetProperty("rDeclarations").GetInt32() +
         accounting.GetProperty("section8Declarations").GetInt32() +
         accounting.GetProperty("laterOrLegacyDeclarations").GetInt32() +
         accounting.GetProperty("removedConceptDeclarations").GetInt32() +
         accounting.GetProperty("unsupportedBlockers").GetInt32())
            .Should().Be(accounting.GetProperty("excludedDeclarations").GetInt32());
        ValidateAcceptanceAccounting(root, accounting);

        var reclassified = crosswalk.GetProperty("reclassifiedDeclarations").EnumerateArray().ToArray();
        reclassified.Should().HaveCount(6);
        var reclassifiedCoordinates = reclassified.ToDictionary(
            item => $"{item.GetProperty("source").GetString()}::{item.GetProperty("member").GetString()}",
            item => item.GetProperty("disposition").GetString()!,
            StringComparer.Ordinal);
        reclassifiedCoordinates.Should().BeEquivalentTo(RequiredReclassifiedDeclarations);
        reclassified.Select(item => item.GetProperty("reason").GetString())
            .Should().OnlyContain(reason => !string.IsNullOrWhiteSpace(reason));
        reclassified.Count(item => item.GetProperty("disposition").GetString() == "L").Should().Be(5);
        reclassified.Count(item => item.GetProperty("disposition").GetString() == "D8").Should().Be(1);
        foreach (var item in reclassified)
        {
            var source = item.GetProperty("source").GetString()!;
            var member = item.GetProperty("member").GetString()!;
            var absoluteSource = Path.Combine(root, source.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(absoluteSource).Should().BeTrue("reclassified source '{0}' must remain recoverable", source);
            ActiveTestDeclarationRegex(member).Matches(File.ReadAllText(absoluteSource)).Should().ContainSingle(
                "reclassified declaration '{0}::{1}' must remain exact and recoverable",
                source,
                member);
        }

        var groups = crosswalk.GetProperty("groups").EnumerateArray().ToArray();
        groups.Should().HaveCount(62, "the ledger contains 57 pure-R files and five mixed-disposition files");
        var sourcePaths = groups.Select(group => group.GetProperty("source").GetString()!).ToArray();
        sourcePaths.Should().OnlyHaveUniqueItems();
        MixedRFiles.Keys.Should().BeSubsetOf(sourcePaths);

        var rows = new List<string>();
        var usedEvidenceKeys = new HashSet<string>(StringComparer.Ordinal);
        var usedActiveTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var source = group.GetProperty("source").GetString()!;
            source.Should().StartWith("tests/", "R evidence must identify a repository-relative test source");
            source.Should().NotContain("..", "crosswalk paths must not escape the repository");
            var absoluteSource = Path.Combine(root, source.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(absoluteSource).Should().BeTrue("crosswalk source '{0}' must exist", source);
            ValidateExcludedSource(root, source);
            var sourceText = File.ReadAllText(absoluteSource);
            var members = group.GetProperty("members").EnumerateArray().Select(member => member.GetString()!).ToArray();
            members.Should().NotBeEmpty();
            members.Should().OnlyHaveUniqueItems();
            foreach (var member in members)
            {
                ActiveTestDeclarationRegex(member).Matches(sourceText).Should().ContainSingle(
                    "crosswalk row '{0}::{1}' must name exactly one Fact/Theory declaration", source, member);
                rows.Add($"{source}::{member}");
            }

            string[] groupEvidenceKeys = ReadEvidenceKeys(group.GetProperty("evidenceKeys")).ToArray();
            groupEvidenceKeys.Should().NotBeEmpty();
            groupEvidenceKeys.Should().OnlyHaveUniqueItems();
            string[] groupActiveTargets = ReadEvidenceKeys(group.GetProperty("activeTargets")).ToArray();
            groupActiveTargets.Should().NotBeEmpty();
            groupActiveTargets.Should().OnlyHaveUniqueItems();
            var memberEvidence = group.GetProperty("memberEvidence").EnumerateArray().ToArray();
            memberEvidence.Should().HaveCount(members.Length,
                "each R declaration in '{0}' must have per-method evidence", source);
            var memberEvidenceNames = memberEvidence
                .Select(item => item.GetProperty("member").GetString()!).ToArray();
            memberEvidenceNames.Should().BeEquivalentTo(members);
            memberEvidenceNames.Should().OnlyHaveUniqueItems();
            var memberEvidenceUnion = new HashSet<string>(StringComparer.Ordinal);
            var memberActiveTargetUnion = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in memberEvidence)
            {
                var memberName = item.GetProperty("member").GetString()!;
                var rowEvidenceKeys = ReadEvidenceKeys(item.GetProperty("evidenceKeys")).ToArray();
                rowEvidenceKeys.Should().NotBeEmpty("R declaration '{0}::{1}' requires executable evidence", source, memberName);
                rowEvidenceKeys.Should().OnlyHaveUniqueItems();
                foreach (var evidenceKey in rowEvidenceKeys)
                {
                    definedEvidenceKeys.Should().Contain(evidenceKey,
                        "crosswalk evidence key '{0}' must be defined in the recovery record", evidenceKey);
                    usedEvidenceKeys.Add(evidenceKey);
                    memberEvidenceUnion.Add(evidenceKey);
                }

                var activeTargets = ReadEvidenceKeys(item.GetProperty("activeTargets")).ToArray();
                activeTargets.Should().NotBeEmpty(
                    "R declaration '{0}::{1}' requires exact active test or scenario evidence",
                    source,
                    memberName);
                activeTargets.Should().OnlyHaveUniqueItems();
                foreach (var activeTarget in activeTargets)
                {
                    activeTarget.Should().MatchRegex(
                        @"^(?:scenario:[a-z0-9-]+|test:tests/.+\.cs#[A-Za-z_][A-Za-z0-9_]*)$",
                        "active evidence coordinates use one closed grammar");
                    usedActiveTargets.Add(activeTarget);
                    memberActiveTargetUnion.Add(activeTarget);
                }
            }
            memberEvidenceUnion.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    groupEvidenceKeys.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                .Should().BeTrue(
                "group evidence for '{0}' must equal the exact union of its per-method evidence", source);
            memberActiveTargetUnion.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    groupActiveTargets.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                .Should().BeTrue(
                "group active targets for '{0}' must equal the exact union of its per-method targets", source);

            var declarationCount = TestDeclarationRegex().Matches(sourceText).Count;
            if (MixedRFiles.TryGetValue(source, out var expectedMixedCount))
            {
                members.Should().HaveCount(expectedMixedCount,
                    "mixed file '{0}' must map its exact R subset", source);
                declarationCount.Should().BeGreaterThan(expectedMixedCount,
                    "mixed file '{0}' must retain at least one explicitly non-R declaration", source);
            }
            else
            {
                members.Should().HaveCount(declarationCount,
                    "pure-R file '{0}' must map every Fact/Theory declaration", source);
            }
        }

        rows.Should().HaveCount(373);
        rows.Should().OnlyHaveUniqueItems();
        (rows.Count + reclassified.Length).Should().Be(
            379,
            "the six semantic corrections reclassify, rather than erase, declarations from the original R ledger");
        rows.Should().NotIntersectWith(
            RequiredReclassifiedDeclarations.Keys,
            "deferred, superseded, and Section-8 declarations must not receive R replacement credit");
        usedEvidenceKeys.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(
                definedEvidenceKeys.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            .Should().BeTrue(
            "the finite named evidence catalog and the declaration crosswalk must stay in lockstep");
        ValidateActiveTargets(root, usedActiveTargets);
        crosswalk.GetProperty("rule").GetString().Should().Contain("exact compiled test methods or executable scenario drivers");
    }

    private static void ValidateAcceptanceAccounting(string root, JsonElement accounting)
    {
        var acceptanceRoot = Path.Combine(root, "tests", "OrcaCore.Acceptance.Tests");
        var project = File.ReadAllText(Path.Combine(acceptanceRoot, "OrcaCore.Acceptance.Tests.csproj"));
        var compileRemoves = CompileRemoveRegex().Matches(project)
            .Select(match => match.Groups[1].Value.Replace('\\', '/'))
            .ToArray();
        compileRemoves.Should().BeEquivalentTo(RequiredExcludedAcceptanceSources);

        var declarationCounts = Directory.EnumerateFiles(acceptanceRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                path => Path.GetFileName(path)!,
                path => TestDeclarationRegex().Matches(File.ReadAllText(path)).Count,
                StringComparer.Ordinal);
        var activeDeclarations = declarationCounts
            .Where(pair => !compileRemoves.Contains(pair.Key, StringComparer.Ordinal))
            .Sum(pair => pair.Value);
        activeDeclarations.Should().Be(accounting.GetProperty("activeAcceptanceDeclarations").GetInt32());

        FiveNewlyExcludedLegacyAcceptanceSources.Should().BeSubsetOf(compileRemoves);
        FiveNewlyExcludedLegacyAcceptanceSources.Sum(source => declarationCounts[source]).Should()
            .Be(accounting.GetProperty("excludedLegacyAcceptanceDeclarations").GetInt32());
    }

    private static void ValidateActiveTargets(string root, IReadOnlySet<string> activeTargets)
    {
        activeTargets.Should().NotBeEmpty();
        var scenarioSources = Directory
            .EnumerateFiles(
                Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.BehaviorScenarios"),
                "*.cs",
                SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToArray();

        foreach (var target in activeTargets)
        {
            if (target.StartsWith("scenario:", StringComparison.Ordinal))
            {
                var scenarioId = target["scenario:".Length..];
                var matches = scenarioSources
                    .SelectMany(source => ScenarioDriverRegex().Matches(source.Text)
                        .Where(match => match.Groups[1].Value == scenarioId)
                        .Select(_ => source.Path))
                    .ToArray();
                matches.Should().ContainSingle(
                    "scenario target '{0}' must have exactly one executable Phase0Scenario driver",
                    scenarioId);
                continue;
            }

            target.Should().StartWith("test:");
            var coordinate = target["test:".Length..];
            var separator = coordinate.LastIndexOf('#');
            separator.Should().BeGreaterThan(0, "test target '{0}' must identify path#member", target);
            var source = coordinate[..separator];
            var member = coordinate[(separator + 1)..];
            source.Should().StartWith("tests/");
            source.Should().NotContain("..");
            var absoluteSource = Path.Combine(root, source.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(absoluteSource).Should().BeTrue("active test source '{0}' must exist", source);
            var sourceText = File.ReadAllText(absoluteSource);
            ActiveTestDeclarationRegex(member).Matches(sourceText).Should().ContainSingle(
                "active test target '{0}' must name exactly one Fact/Theory declaration",
                target);

            var sourceParts = source.Split('/');
            sourceParts.Should().HaveCountGreaterThan(2);
            var projectDirectory = Path.Combine(root, sourceParts[0], sourceParts[1]);
            var project = Directory.EnumerateFiles(projectDirectory, "*.csproj").Single();
            var projectRelativeSource = string.Join('/', sourceParts.Skip(2));
            var compileRemoves = CompileRemoveRegex().Matches(File.ReadAllText(project))
                .Select(match => match.Groups[1].Value.Replace('\\', '/'))
                .ToArray();
            compileRemoves.Should().NotContain(
                projectRelativeSource,
                "active test target '{0}' must be compiled by its project",
                target);
        }
    }

    private static void ValidateExcludedSource(string root, string source)
    {
        var sourceParts = source.Split('/');
        sourceParts.Should().HaveCountGreaterThan(2);
        var projectDirectory = Path.Combine(root, sourceParts[0], sourceParts[1]);
        var project = Directory.EnumerateFiles(projectDirectory, "*.csproj").Single();
        var projectRelativeSource = string.Join('/', sourceParts.Skip(2));
        var compileRemoves = CompileRemoveRegex().Matches(File.ReadAllText(project))
            .Select(match => match.Groups[1].Value.Replace('\\', '/'))
            .ToArray();
        compileRemoves.Should().Contain(
            projectRelativeSource,
            "retired source '{0}' must remain recoverable but excluded from passing-test credit",
            source);
    }

    private static IEnumerable<string> ReadEvidenceKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            yield return element.GetString()!;
            yield break;
        }
        element.ValueKind.Should().Be(JsonValueKind.Array);
        foreach (var value in element.EnumerateArray()) yield return value.GetString()!;
    }

    private static Regex ActiveTestDeclarationRegex(string member) => new(
        $@"(?s)\[(?:Fact|Theory)(?:Attribute)?(?:\([^\]]*\))?\]" +
        $@"(?:(?!\[(?:Fact|Theory)).)*?\b{Regex.Escape(member)}\s*\(",
        RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\[(?:Fact|Theory)(?:Attribute)?(?:\([^\]]*\))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TestDeclarationRegex();

    [GeneratedRegex(@"\[Phase0Scenario\(""([^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ScenarioDriverRegex();

    [GeneratedRegex(@"<Compile\s+Remove=""([^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex CompileRemoveRegex();

    [GeneratedRegex(
        """(?:docs[\\/]+archive|Path\.Combine\([^;\r\n]*["']docs["']\s*,\s*["']archive["'])""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArchivedDocumentationPathRegex();

    [GeneratedRegex(
        """Directory\.(?:GetFiles|EnumerateFiles)\s*\(\s*Path\.Combine\([^;\r\n]*["']docs["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecursiveDocumentationScanRegex();
}
