using System.Diagnostics;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed partial class RecoveryCrosswalkInfrastructureGuards
{
    private const string CrosswalkPath =
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/section-07-r-declaration-crosswalk.json";
    private const string ActiveCorpusGuardPath =
        "tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs";
    private const string DocumentationRoot = "docs/";
    private const string ArchiveDocumentationDirectory = "archive/";
    private const string ReviewDocumentationDirectory = "review/";
    private static readonly string ApprovedImmutableDocumentationPrefixDeclaration =
        $"    private static readonly string[] ImmutableDocumentationPrefixes = [\"{DocumentationRoot}{ArchiveDocumentationDirectory}\", \"{DocumentationRoot}{ReviewDocumentationDirectory}\"];";
    private static readonly (string Name, string Path)[] ApprovedHistoricalArchiveCoordinates =
    [
        ("Task77ArchiveRecord", DocumentationRoot + ArchiveDocumentationDirectory +
            "plans/developer-facing-interface-phase-00-kickoff-archive-provenance-2026-09-23.md"),
        ("Task77ArchivedPrompt", DocumentationRoot + ArchiveDocumentationDirectory +
            "plans/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md")
    ];
    private const string SourceBaseline = "d76192f089dd07f68e310c21fe4e5a38dd93cf7f";
    private const string TargetBase = "287fbde74681c015235d76f6498c031addfd9ac1";
    private const string ApprovedDispositionRecordSha256 =
        "29ec81bb9725a49a73c4b76091411925d2d6acf8b82a756e7c47d3a0079dfeee";

    private const string ActiveStatus = "active";
    private const string CompileExcludedStatus = "compile-excluded";
    private const string OutOfBandFixtureStatus = "out-of-band-fixture";
    private const string InactiveProjectStatus = "inactive-project";

    private const string SupersededTestShape = "SupersededTestShape";
    private const string RemovedConcept = "RemovedConcept";
    private const string FutureNonV1 = "FutureNonV1";
    private const string Section8 = "Section8";

    private static readonly string[] CrosswalkProperties =
    {
        "schemaVersion", "sourceBaseline", "targetBase", "accounting", "classificationPolicy", "rule",
        "sourceInventory", "historicalSourceInventory", "relocations", "retiredDeclarations"
    };

    private static readonly string[] SourceInventoryProperties =
    {
        "source", "ownerProject", "status", "declarations"
    };

    private static readonly string[] RetiredDeclarationProperties =
    {
        "source", "member", "lifecycle", "disposition", "positiveCredit", "owner", "recovery", "reason"
    };

    private static readonly string[] HistoricalSourceProperties =
    {
        "source", "lifecycle", "baselineDeclarations", "recovery"
    };

    private static readonly string[] RelocationProperties =
    {
        "fromSource", "toSource", "member", "targetStatus", "recovery"
    };

    private static readonly string[] AuthoredYieldRetirements =
    {
        "tests/OrcaCore.Acceptance.Tests/YieldAcceptanceTests.cs::Yield_CommitsProgressAndCompletesExactlyOnce",
        "tests/OrcaCore.Engine.Durable.Tests/Aggregates/DurableAggregateTests.cs::DecideYield_WritesCheckpointWithoutCompletingStep",
        "tests/OrcaCore.Engine.Durable.Tests/Driver/DurableDriverAcceptanceTests.cs::YieldChunkedStep_CrashMidChunk_ResumesFromLastCommittedChunk",
        "tests/OrcaCore.Engine.Durable.Tests/Driver/DurableStructuredFiberDriverTests.cs::SelectedParallel_YieldRotatesIsolatedFibersAndMergesAuthoredResults",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/StructuredFiberExecutionTests.cs::SelectedParallel_YieldPersistsPrivateStateAndRotatesBranches",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/WaitMatchingTests.cs::RaiseEventAsync_MatchingEvent_ThenYieldingStep_DrainsYieldContinuation",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/YieldTests.cs::Run_YieldingStep_CommitsProgressForEachYield",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/YieldTests.cs::Run_YieldingStep_DoesNotDuplicateCompletedEffects",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/YieldTests.cs::Run_YieldingStep_ReentersSameStepUntilCompleted",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/YieldTests.cs::Run_YieldingStep_ReleasesLaneBetweenContinuations",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/YieldTests.cs::Run_YieldingStep_RemainsRunningBetweenContinuations",
        "tests/OrcaCore.Integration.Tests/E2E/DurableWorkflowPostgreSqlIntegrationTests.cs::INT_E2E_015_YieldCrashRecoveryOnPostgreSql"
    };

    private static readonly string[] KeywordFalsePositiveCorrections =
    {
        "tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs::WorkflowBuilder_DoesNotExposeCompensationMethods",
        "tests/OrcaCore.Core.Tests/Execution/LinearFiberInterpreterTests.cs::Yield_PreservesInstruction_AndRotatesToRunnableSibling",
        "tests/OrcaCore.Core.Tests/Lifecycle/LifecycleMachineTests.cs::TerminalStatuses_AreExactly_RegularAndSagaTerminalStates",
        "tests/OrcaCore.Engine.Durable.Tests/Driver/DurableFiberEnvelopeMapperTests.cs::RoundTrip_PreservesNestedFibersScopesSchedulerResultsRetryAndYield",
        "tests/OrcaCore.Engine.Ephemeral.Tests/Timers/EphemeralTimerTests.cs::FireDueTimersAsync_TimerContinuationYields_DrainsYieldContinuation"
    };

    [Fact]
    public void GuardDocumentationInputs_ExcludeHistoricalArchiveAndRecursiveDocsScans()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var guardRoot = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards");
        var findings = Directory.EnumerateFiles(guardRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsBuildOutput(path))
            .Select(path => (
                Path: Path.GetRelativePath(root, path).Replace('\\', '/'),
                Source: File.ReadAllText(path)))
            .Select(item => (
                item.Path,
                Source: StripApprovedImmutableDocumentationExclusion(item.Path, item.Source)))
            .SelectMany(item => new[]
            {
                ArchivedDocumentationPathRegex().IsMatch(item.Source)
                    ? $"{item.Path} -> archived documentation path"
                    : null,
                RecursiveDocumentationScanRegex().IsMatch(item.Source)
                    ? $"{item.Path} -> recursive documentation scan"
                    : null
            })
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        findings.Should().BeEmpty(
            "guards must read explicit active normative/binding paths; historical documents are provenance, not current guidance");
    }

    private static string StripApprovedImmutableDocumentationExclusion(string path, string source)
    {
        if (!path.Equals(ActiveCorpusGuardPath, StringComparison.Ordinal))
        {
            return source;
        }

        var occurrences = Regex.Matches(
            source,
            Regex.Escape(ApprovedImmutableDocumentationPrefixDeclaration),
            RegexOptions.CultureInvariant);
        if (occurrences.Count != 1)
        {
            throw new InvalidDataException(
                $"The active corpus guard must declare the exact immutable documentation exclusions once; found {occurrences.Count}.");
        }

        source = source.Replace(
            ApprovedImmutableDocumentationPrefixDeclaration,
            string.Empty,
            StringComparison.Ordinal);

        // Task 7.7 verifies historical Git objects, not current normative guidance.
        // Permit only its two named, exact archive coordinates in this guard source.
        foreach (var (name, coordinate) in ApprovedHistoricalArchiveCoordinates)
        {
            var pattern = $@"private const string {name}\s*=\s*""{Regex.Escape(coordinate)}"";";
            var matches = Regex.Matches(source, pattern, RegexOptions.CultureInvariant);
            if (matches.Count != 1)
            {
                throw new InvalidDataException(
                    $"The active corpus guard must declare historical archive coordinate {name} exactly once; found {matches.Count}.");
            }

            source = source.Remove(matches[0].Index, matches[0].Length);
        }

        return source;
    }

    private static void ValidateRelocationDiscoverySemantics()
    {
        var baseline = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tests/Old.cs"] = new[] { "Moved" },
            ["tests/AlreadyThere.cs"] = new[] { "Duplicate" },
            ["tests/OtherOld.cs"] = new[] { "Duplicate" }
        };
        var current = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tests/New.cs"] = new[] { "Moved" },
            ["tests/AlreadyThere.cs"] = new[] { "Duplicate" }
        };
        var statuses = current.Keys.ToDictionary(key => key, _ => CompileExcludedStatus, StringComparer.Ordinal);

        DiscoverRelocations(baseline, current, statuses).Select(row => row.FromCoordinate).Should().Equal(
            "tests/Old.cs::Moved");
    }

    private static void ValidateCompileRemoveDiscoverySemantics()
    {
        GlobMatches("Legacy/**/*.cs", "legacy/Nested/Test.cs").Should().BeTrue(
            "MSBuild path matching is case-insensitive on the supported Windows repository");

        var project = Path.GetTempFileName();
        try
        {
            File.WriteAllText(project, """
                <Project>
                  <ItemGroup Condition="'$(Configuration)' == 'Debug'">
                    <Compile Remove="Legacy/**/*.cs" />
                  </ItemGroup>
                </Project>
                """);

            var action = () => ReadCompileRemoves(project);
            action.Should().Throw<InvalidOperationException>().WithMessage("*conditional Compile Remove*");
        }
        finally
        {
            File.Delete(project);
        }
    }

    [Fact]
    public void TestSourceInventory_AndEveryRetiredDeclarationHaveExactNonCreditedDispositions()
    {
        ValidateRelocationDiscoverySemantics();
        ValidateCompileRemoveDiscoverySemantics();

        var root = FixtureDefinitions.RepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, CrosswalkPath)));
        var crosswalk = document.RootElement;

        crosswalk.EnumerateObject().Select(property => property.Name).Should().Equal(CrosswalkProperties);
        crosswalk.GetProperty("schemaVersion").GetInt32().Should().Be(6);
        crosswalk.GetProperty("sourceBaseline").GetString().Should().Be(SourceBaseline);
        crosswalk.GetProperty("targetBase").GetString().Should().Be(TargetBase);
        crosswalk.GetProperty("classificationPolicy").GetString().Should().Be(
            "Disposition is reviewed row data pinned by the guard digest; path/member keywords do not classify rows. " +
            "Owner, recovery, and reason are normalized consequences of that reviewed disposition. " +
            "Relocation records physical same-member coordinate continuity only and grants no semantic replacement credit.");
        GitObjectExists(root, SourceBaseline).Should().BeTrue("the recovery checkpoint must remain readable");
        GitObjectExists(root, TargetBase).Should().BeTrue("the task-7.20 base must remain readable");

        var discoveredSources = DiscoverSourceInventory(root);
        var recordedSources = crosswalk.GetProperty("sourceInventory").EnumerateArray().ToArray();
        recordedSources.Should().HaveCount(discoveredSources.Length);

        var recordedSourceRows = recordedSources.Select(ReadSourceInventoryRow).ToArray();
        recordedSourceRows.Should().Equal(discoveredSources);
        recordedSourceRows.Select(row => row.Source).Should().OnlyHaveUniqueItems();
        var allowedStatuses = new[]
        {
            ActiveStatus, CompileExcludedStatus, OutOfBandFixtureStatus, InactiveProjectStatus
        };
        recordedSourceRows.Select(row => row.Status).Should().OnlyContain(status => allowedStatuses.Contains(status));

        var currentDeclarations = discoveredSources.ToDictionary(
            source => source.Source,
            source => ReadTestDeclarations(File.ReadAllText(Path.Combine(
                root,
                source.Source.Replace('/', Path.DirectorySeparatorChar)))),
            StringComparer.Ordinal);
        var currentCoordinates = currentDeclarations
            .SelectMany(pair => pair.Value.Select(member => $"{pair.Key}::{member}"))
            .ToHashSet(StringComparer.Ordinal);
        currentDeclarations.Should().OnlyContain(pair => pair.Value.Distinct(StringComparer.Ordinal).Count() == pair.Value.Length,
            "a source/member coordinate must identify exactly one current declaration");
        var activeCoordinates = discoveredSources
            .Where(source => source.Status == ActiveStatus)
            .SelectMany(source => currentDeclarations[source.Source].Select(member => $"{source.Source}::{member}"))
            .ToHashSet(StringComparer.Ordinal);
        var compileExcludedCoordinates = discoveredSources
            .Where(source => source.Status == CompileExcludedStatus)
            .SelectMany(source => currentDeclarations[source.Source].Select(member => $"{source.Source}::{member}"))
            .ToHashSet(StringComparer.Ordinal);

        var baselineSources = ReadGitArchiveSources(root, SourceBaseline);
        var baselineDeclarations = baselineSources.ToDictionary(
            pair => pair.Key,
            pair => ReadTestDeclarations(pair.Value),
            StringComparer.Ordinal);
        baselineDeclarations.Should().OnlyContain(pair => pair.Value.Distinct(StringComparer.Ordinal).Count() == pair.Value.Length,
            "a source/member coordinate must identify exactly one recovery-baseline declaration");
        var baselineCoordinates = baselineSources
            .SelectMany(pair => baselineDeclarations[pair.Key].Select(member => $"{pair.Key}::{member}"))
            .ToHashSet(StringComparer.Ordinal);
        var historicalSources = crosswalk.GetProperty("historicalSourceInventory").EnumerateArray().ToArray();
        var expectedHistoricalSources = baselineSources
            .Where(pair => discoveredSources.All(current => current.Source != pair.Key))
            .Select(pair => new HistoricalSourceRow(
                pair.Key,
                "physically-deleted",
                ReadTestDeclarations(pair.Value).Length,
                $"git:{SourceBaseline}:{pair.Key}"))
            .OrderBy(row => row.Source, StringComparer.Ordinal)
            .ToArray();
        historicalSources.Should().HaveCount(expectedHistoricalSources.Length);
        historicalSources.Select(ReadHistoricalSourceRow).Should().Equal(expectedHistoricalSources);
        var expectedRelocations = DiscoverRelocations(
            baselineDeclarations,
            currentDeclarations,
            discoveredSources.ToDictionary(source => source.Source, source => source.Status, StringComparer.Ordinal));
        var recordedRelocations = crosswalk.GetProperty("relocations").EnumerateArray().ToArray();
        recordedRelocations.Should().HaveCount(expectedRelocations.Length);
        recordedRelocations.Select(ReadRelocationRow).Should().Equal(expectedRelocations);
        expectedRelocations.Select(row => row.FromCoordinate).Should().OnlyHaveUniqueItems();
        expectedRelocations.Select(row => row.ToCoordinate).Should().OnlyHaveUniqueItems();
        expectedRelocations.Select(row => row.TargetStatus).Should().OnlyContain(status => status == CompileExcludedStatus,
            "physical relocation alone must never convert an old declaration into passing evidence");
        var expectedRetired = BuildExpectedRetiredCoordinates(
            compileExcludedCoordinates,
            baselineCoordinates,
            currentCoordinates,
            discoveredSources.Select(source => source.Source).ToHashSet(StringComparer.Ordinal),
            expectedRelocations);

        var retired = crosswalk.GetProperty("retiredDeclarations").EnumerateArray().ToArray();
        retired.Should().HaveCount(expectedRetired.Length);
        var recordedCoordinates = retired
            .Select(item => $"{item.GetProperty("source").GetString()}::{item.GetProperty("member").GetString()}")
            .ToArray();
        recordedCoordinates.Should().OnlyHaveUniqueItems();
        recordedCoordinates.Order(StringComparer.Ordinal).Should()
            .Equal(expectedRetired.Select(row => row.Coordinate).Order(StringComparer.Ordinal));

        foreach (var item in retired)
        {
            item.EnumerateObject().Select(property => property.Name).Should().Equal(RetiredDeclarationProperties);
            var source = item.GetProperty("source").GetString()!;
            var member = item.GetProperty("member").GetString()!;
            var coordinate = $"{source}::{member}";
            var expected = expectedRetired.Single(row => row.Coordinate == coordinate);
            item.GetProperty("lifecycle").GetString().Should().Be(expected.Lifecycle);

            var disposition = item.GetProperty("disposition").GetString()!;
            disposition.Should().BeOneOf(SupersededTestShape, RemovedConcept, FutureNonV1, Section8);
            item.GetProperty("positiveCredit").GetBoolean().Should().BeFalse(
                "retired, deferred, and removed declarations are not passing evidence");
            item.GetProperty("owner").GetString().Should().Be(ExpectedOwner(disposition));
            item.GetProperty("recovery").GetString().Should().Be(
                expected.Lifecycle is CompileExcludedStatus or "relocated"
                    ? $"current:{source}"
                    : $"git:{SourceBaseline}:{source}");
            item.GetProperty("reason").GetString().Should().Be(ExpectedReason(disposition));
        }

        ComputeDispositionRecordSha256(retired).Should().Be(ApprovedDispositionRecordSha256,
            "the method-level disposition review is curated fixture evidence, not a keyword classifier");

        retired.Where(item => item.GetProperty("disposition").GetString() == FutureNonV1)
            .Select(item => item.GetProperty("positiveCredit").GetBoolean()).Should().OnlyContain(value => !value);
        retired.Where(item => item.GetProperty("disposition").GetString() == Section8)
            .Select(item => item.GetProperty("positiveCredit").GetBoolean()).Should().OnlyContain(value => !value);
        retired.Where(item => item.GetProperty("disposition").GetString() == RemovedConcept)
            .Select(item => $"{item.GetProperty("source").GetString()}::{item.GetProperty("member").GetString()}")
            .Order(StringComparer.Ordinal).Should().Equal(AuthoredYieldRetirements.Order(StringComparer.Ordinal));
        retired.Where(item => KeywordFalsePositiveCorrections.Contains(
                $"{item.GetProperty("source").GetString()}::{item.GetProperty("member").GetString()}",
                StringComparer.Ordinal))
            .Select(item => item.GetProperty("disposition").GetString()).Should()
            .OnlyContain(value => value == SupersededTestShape,
                "names mentioning Saga, compensation, or Yield do not make a negative, serialization, timer, or scheduler test that capability");
        retired.SelectMany(item => item.EnumerateObject().Select(property => property.Name)).Should()
            .NotContain(new[] { "activeTarget", "equivalence" },
                "task 7.20 intentionally removes every unproven replacement mapping instead of preserving coordinate-only credit");

        ValidateAccounting(
            crosswalk.GetProperty("accounting"),
            discoveredSources,
            baselineSources,
            baselineCoordinates,
            activeCoordinates,
            compileExcludedCoordinates,
            expectedRetired,
            retired,
            expectedRelocations);
        ValidateAcceptanceLedger(root, discoveredSources);

        crosswalk.GetProperty("rule").GetString().Should().Be(
            "Every physical test source and every logical retired Fact/Theory declaration is enumerated exactly. " +
            "A baseline coordinate absent from its original file but present under the same member name in one other current file is recorded once as a physical relocation; " +
            "this is not semantic-equivalence evidence and grants no passing credit. In-place removal means only that the baseline coordinate is absent while its source file remains, " +
            "so it may include a rename or rewritten test. Future, Section 8, authored-Yield, and superseded-shape rows are excluded from passing evidence. " +
            "SQL Server resource-pool certification remains active through its shared inherited certification facts even though its derived wrapper declares no local Fact/Theory member.");
    }

    private static void ValidateAccounting(
        JsonElement accounting,
        SourceInventoryRow[] sources,
        IReadOnlyDictionary<string, string> baselineSources,
        IReadOnlySet<string> baselineCoordinates,
        IReadOnlySet<string> activeCoordinates,
        IReadOnlySet<string> compileExcludedCoordinates,
        RetiredCoordinate[] retired,
        JsonElement[] recordedRetired,
        RelocationRow[] relocations)
    {
        var relocatedTargets = relocations.Select(row => row.ToCoordinate).ToHashSet(StringComparer.Ordinal);
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["physicalFiles"] = sources.Length,
            ["physicalDeclarations"] = sources.Sum(source => source.Declarations),
            ["activeFiles"] = sources.Count(source => source.Status == ActiveStatus),
            ["activeDeclarations"] = sources.Where(source => source.Status == ActiveStatus).Sum(source => source.Declarations),
            ["compileExcludedFiles"] = sources.Count(source => source.Status == CompileExcludedStatus),
            ["compileExcludedDeclarations"] = sources.Where(source => source.Status == CompileExcludedStatus).Sum(source => source.Declarations),
            ["outOfBandFixtureFiles"] = sources.Count(source => source.Status == OutOfBandFixtureStatus),
            ["outOfBandFixtureDeclarations"] = sources.Where(source => source.Status == OutOfBandFixtureStatus).Sum(source => source.Declarations),
            ["inactiveProjectFiles"] = sources.Count(source => source.Status == InactiveProjectStatus),
            ["inactiveProjectDeclarations"] = sources.Where(source => source.Status == InactiveProjectStatus).Sum(source => source.Declarations),
            ["baselineFiles"] = baselineSources.Count,
            ["baselineDeclarations"] = baselineCoordinates.Count,
            ["baselineRetainedActiveDeclarations"] = baselineCoordinates.Count(activeCoordinates.Contains),
            ["baselineRetainedCompileExcludedDeclarations"] = baselineCoordinates.Count(compileExcludedCoordinates.Contains),
            ["baselineRetainedRelocatedDeclarations"] = relocations.Length,
            ["newlyExcludedDeclarations"] = compileExcludedCoordinates.Count(coordinate =>
                !baselineCoordinates.Contains(coordinate) && !relocatedTargets.Contains(coordinate)),
            ["retiredDeclarations"] = retired.Length,
            ["physicallyDeletedFiles"] = baselineSources.Keys.Count(source => sources.All(current => current.Source != source)),
            ["physicallyDeletedDeclarations"] = retired.Count(row => row.Lifecycle == "physically-deleted"),
            ["inPlaceRemovedFiles"] = retired.Where(row => row.Lifecycle == "in-place-removed").Select(row => row.Source).Distinct(StringComparer.Ordinal).Count(),
            ["inPlaceRemovedDeclarations"] = retired.Count(row => row.Lifecycle == "in-place-removed"),
            ["relocatedFiles"] = relocations.Select(row => row.ToSource).Distinct(StringComparer.Ordinal).Count(),
            ["relocatedDeclarations"] = relocations.Length,
            ["supersededTestShapeDeclarations"] = recordedRetired.Count(item => item.GetProperty("disposition").GetString() == SupersededTestShape),
            ["removedConceptDeclarations"] = recordedRetired.Count(item => item.GetProperty("disposition").GetString() == RemovedConcept),
            ["futureNonV1Declarations"] = recordedRetired.Count(item => item.GetProperty("disposition").GetString() == FutureNonV1),
            ["section8Declarations"] = recordedRetired.Count(item => item.GetProperty("disposition").GetString() == Section8),
            ["replacementCreditDeclarations"] = 0
        };

        accounting.EnumerateObject().Select(property => property.Name).Should().Equal(expected.Keys);
        foreach (var pair in expected)
        {
            accounting.GetProperty(pair.Key).GetInt32().Should().Be(pair.Value, pair.Key);
        }

        expected["activeFiles"].Should().Be(191);
        expected["activeDeclarations"].Should().Be(714);
        expected["compileExcludedFiles"].Should().Be(131);
        expected["compileExcludedDeclarations"].Should().Be(688);
        expected["outOfBandFixtureFiles"].Should().Be(17);
        expected["outOfBandFixtureDeclarations"].Should().Be(0);
        expected["inactiveProjectFiles"].Should().Be(0);
        expected["inactiveProjectDeclarations"].Should().Be(0);
        expected["physicallyDeletedFiles"].Should().Be(9);
        expected["physicallyDeletedDeclarations"].Should().Be(28);
        expected["inPlaceRemovedFiles"].Should().Be(53);
        expected["inPlaceRemovedDeclarations"].Should().Be(150);
        expected["relocatedFiles"].Should().Be(2);
        expected["relocatedDeclarations"].Should().Be(6);

        (expected["baselineRetainedActiveDeclarations"] +
         expected["baselineRetainedCompileExcludedDeclarations"] +
         expected["baselineRetainedRelocatedDeclarations"] +
         expected["inPlaceRemovedDeclarations"] +
         expected["physicallyDeletedDeclarations"]).Should().Be(expected["baselineDeclarations"],
            "every baseline declaration must have one and only one current or retired lifecycle");
        (expected["baselineRetainedCompileExcludedDeclarations"] +
         expected["baselineRetainedRelocatedDeclarations"] +
         expected["newlyExcludedDeclarations"]).Should().Be(expected["compileExcludedDeclarations"],
            "a relocated excluded declaration is retained old evidence, not newly excluded evidence");
        (expected["compileExcludedDeclarations"] +
         expected["inPlaceRemovedDeclarations"] +
         expected["physicallyDeletedDeclarations"]).Should().Be(expected["retiredDeclarations"],
            "relocations are represented by their current excluded coordinate and must not be double-counted at the old path");
    }

    private static void ValidateAcceptanceLedger(string root, SourceInventoryRow[] sources)
    {
        var acceptanceSources = sources
            .Where(source => source.Source.StartsWith("tests/OrcaCore.Acceptance.Tests/", StringComparison.Ordinal))
            .ToArray();
        acceptanceSources.Where(source => source.Status == ActiveStatus).Sum(source => source.Declarations).Should().Be(37);
        acceptanceSources.Where(source => source.Status == CompileExcludedStatus).Sum(source => source.Declarations).Should().Be(27);

        var expectedExcluded = new[]
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
        acceptanceSources.Where(source => source.Status == CompileExcludedStatus)
            .Select(source => Path.GetFileName(source.Source)).Should().Equal(expectedExcluded);

        var project = File.ReadAllText(Path.Combine(root, "tests", "OrcaCore.Acceptance.Tests", "OrcaCore.Acceptance.Tests.csproj"));
        CompileRemoveRegex().Matches(project).Select(match => Path.GetFileName(match.Groups[1].Value))
            .Should().Equal(expectedExcluded);
    }

    private static SourceInventoryRow[] DiscoverSourceInventory(string root)
    {
        var testsRoot = Path.Combine(root, "tests");
        var solutionProjects = XDocument.Load(Path.Combine(root, "OrcaCore.slnx"))
            .Descendants("Project")
            .Select(element => NormalizePath(element.Attribute("Path")?.Value ?? string.Empty))
            .Where(path => path.StartsWith("tests/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var projects = Directory.EnumerateFiles(testsRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(path => new ProjectOwner(
                Path.GetDirectoryName(path)!,
                NormalizePath(Path.GetRelativePath(root, path)),
                ReadCompileRemoves(path)))
            .OrderByDescending(project => project.Directory.Length)
            .ToArray();

        return Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(path =>
            {
                var owner = projects.FirstOrDefault(project => IsUnderDirectory(path, project.Directory))
                    ?? throw new InvalidOperationException($"Test source '{path}' has no owning project.");
                var source = NormalizePath(Path.GetRelativePath(root, path));
                var projectRelativeSource = NormalizePath(Path.GetRelativePath(owner.Directory, path));
                string status;
                if (solutionProjects.Contains(owner.Project))
                {
                    status = owner.CompileRemoves.Any(pattern => GlobMatches(pattern, projectRelativeSource))
                        ? CompileExcludedStatus
                        : ActiveStatus;
                }
                else if (source.Contains("/CompileFixtures/", StringComparison.Ordinal) ||
                         source.Contains("/PackageFixtures/", StringComparison.Ordinal))
                {
                    status = OutOfBandFixtureStatus;
                }
                else
                {
                    status = InactiveProjectStatus;
                }

                return new SourceInventoryRow(
                    source,
                    owner.Project,
                    status,
                    ReadTestDeclarations(File.ReadAllText(path)).Length);
            })
            .OrderBy(source => source.Source, StringComparer.Ordinal)
            .ToArray();
    }

    private static RetiredCoordinate[] BuildExpectedRetiredCoordinates(
        IReadOnlySet<string> compileExcludedCoordinates,
        IReadOnlySet<string> baselineCoordinates,
        IReadOnlySet<string> currentCoordinates,
        IReadOnlySet<string> currentSources,
        RelocationRow[] relocations)
    {
        var relocationByTarget = relocations.ToDictionary(row => row.ToCoordinate, StringComparer.Ordinal);
        var relocatedBaselineCoordinates = relocations.Select(row => row.FromCoordinate).ToHashSet(StringComparer.Ordinal);
        var rows = compileExcludedCoordinates
            .Select(coordinate => ParseCoordinate(
                coordinate,
                relocationByTarget.ContainsKey(coordinate) ? "relocated" : CompileExcludedStatus))
            .ToDictionary(row => row.Coordinate, StringComparer.Ordinal);

        foreach (var coordinate in baselineCoordinates.Where(coordinate =>
                     !currentCoordinates.Contains(coordinate) && !relocatedBaselineCoordinates.Contains(coordinate)))
        {
            var source = coordinate[..coordinate.LastIndexOf("::", StringComparison.Ordinal)];
            rows.Add(
                coordinate,
                ParseCoordinate(coordinate, currentSources.Contains(source) ? "in-place-removed" : "physically-deleted"));
        }

        return rows.Values
            .OrderBy(row => row.Source, StringComparer.Ordinal)
            .ThenBy(row => row.Member, StringComparer.Ordinal)
            .ToArray();
    }

    private static RelocationRow[] DiscoverRelocations(
        IReadOnlyDictionary<string, string[]> baselineDeclarations,
        IReadOnlyDictionary<string, string[]> currentDeclarations,
        IReadOnlyDictionary<string, string> currentStatuses)
    {
        var currentCoordinatesByMember = currentDeclarations
            .SelectMany(pair => pair.Value.Select(member => new
            {
                Source = pair.Key,
                Member = member,
                Coordinate = $"{pair.Key}::{member}"
            }))
            .GroupBy(item => item.Member, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        return baselineDeclarations
            .SelectMany(pair => pair.Value.Select(member => new { Source = pair.Key, Member = member }))
            .Where(item => !currentDeclarations.TryGetValue(item.Source, out var members) ||
                           !members.Contains(item.Member, StringComparer.Ordinal))
            .Select(item =>
            {
                if (!currentCoordinatesByMember.TryGetValue(item.Member, out var candidates))
                {
                    return null;
                }

                var otherSources = candidates
                    .Where(candidate => candidate.Source != item.Source)
                    .Where(candidate => !baselineDeclarations.TryGetValue(candidate.Source, out var baselineMembers) ||
                                        !baselineMembers.Contains(item.Member, StringComparer.Ordinal))
                    .ToArray();
                if (otherSources.Length != 1)
                {
                    return null;
                }

                var target = otherSources[0];
                return new RelocationRow(
                    item.Source,
                    target.Source,
                    item.Member,
                    currentStatuses[target.Source],
                    $"git:{SourceBaseline}:{item.Source}::{item.Member} -> current:{target.Coordinate}");
            })
            .OfType<RelocationRow>()
            .OrderBy(row => row.FromSource, StringComparer.Ordinal)
            .ThenBy(row => row.Member, StringComparer.Ordinal)
            .ThenBy(row => row.ToSource, StringComparer.Ordinal)
            .ToArray();
    }

    private static RetiredCoordinate ParseCoordinate(string coordinate, string lifecycle)
    {
        var separator = coordinate.LastIndexOf("::", StringComparison.Ordinal);
        return new RetiredCoordinate(coordinate[..separator], coordinate[(separator + 2)..], lifecycle);
    }

    private static string ExpectedOwner(string disposition) => disposition switch
    {
        RemovedConcept => "requirement:developer-facing-surface/authored-yield-absent",
        FutureNonV1 => "active:docs/specs/13-phasing-and-open-questions.md#13.4",
        Section8 => "task:8.0",
        SupersededTestShape => "task:7.20",
        _ => throw new InvalidOperationException($"Unknown retired declaration disposition '{disposition}'.")
    };

    private static string ExpectedReason(string disposition) => disposition switch
    {
        RemovedConcept => "Authored Yield is removed from v1; this retired declaration receives no replacement credit.",
        FutureNonV1 => "The tested capability is future or non-v1 and receives no completed or passing evidence credit.",
        Section8 => "The tested DAG or child-composition capability belongs to Section 8 and receives no Section 7 passing credit.",
        SupersededTestShape => "The declaration targets a superseded pre-Section-7 test shape. It receives no replacement credit; current requirements are certified independently.",
        _ => throw new InvalidOperationException($"Unknown retired declaration disposition '{disposition}'.")
    };

    private static SourceInventoryRow ReadSourceInventoryRow(JsonElement item)
    {
        item.EnumerateObject().Select(property => property.Name).Should().Equal(SourceInventoryProperties);
        return new SourceInventoryRow(
            item.GetProperty("source").GetString()!,
            item.GetProperty("ownerProject").GetString()!,
            item.GetProperty("status").GetString()!,
            item.GetProperty("declarations").GetInt32());
    }

    private static HistoricalSourceRow ReadHistoricalSourceRow(JsonElement item)
    {
        item.EnumerateObject().Select(property => property.Name).Should().Equal(HistoricalSourceProperties);
        return new HistoricalSourceRow(
            item.GetProperty("source").GetString()!,
            item.GetProperty("lifecycle").GetString()!,
            item.GetProperty("baselineDeclarations").GetInt32(),
            item.GetProperty("recovery").GetString()!);
    }

    private static RelocationRow ReadRelocationRow(JsonElement item)
    {
        item.EnumerateObject().Select(property => property.Name).Should().Equal(RelocationProperties);
        return new RelocationRow(
            item.GetProperty("fromSource").GetString()!,
            item.GetProperty("toSource").GetString()!,
            item.GetProperty("member").GetString()!,
            item.GetProperty("targetStatus").GetString()!,
            item.GetProperty("recovery").GetString()!);
    }

    private static string ComputeDispositionRecordSha256(JsonElement[] retired)
    {
        var record = string.Join(
            '\n',
            retired
                .Select(item => new
                {
                    Source = item.GetProperty("source").GetString()!,
                    Member = item.GetProperty("member").GetString()!,
                    Disposition = item.GetProperty("disposition").GetString()!
                })
                .OrderBy(item => item.Source, StringComparer.Ordinal)
                .ThenBy(item => item.Member, StringComparer.Ordinal)
                .Select(item => $"{item.Source}\t{item.Member}\t{item.Disposition}")) + "\n";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record))).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, string> ReadGitArchiveSources(string root, string commit)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("archive");
        startInfo.ArgumentList.Add("--format=tar");
        startInfo.ArgumentList.Add(commit);
        startInfo.ArgumentList.Add("tests");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git archive.");
        using var archive = new MemoryStream();
        var copyOutput = process.StandardOutput.BaseStream.CopyToAsync(archive);
        var readError = process.StandardError.ReadToEndAsync();
        Task.WhenAll(copyOutput, readError, process.WaitForExitAsync()).GetAwaiter().GetResult();
        process.ExitCode.Should().Be(
            0,
            "git archive must read recovery checkpoint {0}: {1}",
            commit,
            readError.GetAwaiter().GetResult());

        archive.Position = 0;
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var reader = new TarReader(archive))
        {
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) ||
                    !entry.Name.EndsWith(".cs", StringComparison.Ordinal) ||
                    entry.DataStream is null)
                {
                    continue;
                }

                using var text = new StreamReader(entry.DataStream);
                sources.Add(NormalizePath(entry.Name), text.ReadToEnd());
            }
        }

        return sources;
    }

    private static string[] ReadTestDeclarations(string source) => NamedTestDeclarationRegex().Matches(source)
        .Select(match => match.Groups["name"].Value)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string[] ReadCompileRemoves(string project)
    {
        var removes = XDocument.Load(project)
            .Descendants("Compile")
            .Where(element => element.Attribute("Remove") is not null)
            .ToArray();
        var conditional = removes
            .Where(element => element.Attribute("Condition") is not null ||
                              element.Ancestors("ItemGroup").Any(group => group.Attribute("Condition") is not null))
            .Select(element => element.Attribute("Remove")!.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (conditional.Length > 0)
        {
            throw new InvalidOperationException(
                $"Test inventory discovery cannot evaluate conditional Compile Remove items in '{project}': " +
                string.Join(", ", conditional));
        }
        return removes
            .Select(element => NormalizePath(element.Attribute("Remove")!.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool GlobMatches(string pattern, string path)
    {
        var expression = Regex.Escape(NormalizePath(pattern))
            .Replace(@"\*\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", "[^/]", StringComparison.Ordinal);
        return Regex.IsMatch(path, $"^{expression}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsUnderDirectory(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static bool GitObjectExists(string root, string objectName)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("cat-file");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(objectName);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git cat-file.");
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private sealed record ProjectOwner(string Directory, string Project, string[] CompileRemoves);

    private sealed record SourceInventoryRow(string Source, string OwnerProject, string Status, int Declarations);

    private sealed record HistoricalSourceRow(
        string Source,
        string Lifecycle,
        int BaselineDeclarations,
        string Recovery);

    private sealed record RelocationRow(
        string FromSource,
        string ToSource,
        string Member,
        string TargetStatus,
        string Recovery)
    {
        public string FromCoordinate => $"{FromSource}::{Member}";

        public string ToCoordinate => $"{ToSource}::{Member}";
    }

    private sealed record RetiredCoordinate(string Source, string Member, string Lifecycle)
    {
        public string Coordinate => $"{Source}::{Member}";
    }

    [GeneratedRegex(
        @"(?ms)^\s*\[(?:Fact|Theory)(?:Attribute)?(?:\([^\]]*\))?\](?:\s*\[[^\]]+\])*\s*(?:(?:public|private|internal|protected|static|async|virtual|override|sealed|new)\s+)+[^\r\n({;=]+\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex NamedTestDeclarationRegex();

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
