using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class ProductionDeletionLedgerInfrastructureGuards
{
    private const string LedgerPath =
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/production-deletion-ledger.json";

    [Fact]
    public void Ledger_CoversTheExactRecoveryDiffCompileExclusionsOrphansAndRetiredPackages()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var document = LoadLedger(root);
        var ledger = document.RootElement;
        var expectedCounts = ledger.GetProperty("expectedCounts");
        var families = ledger.GetProperty("families").EnumerateArray().ToArray();

        families.Should().HaveCount(expectedCounts.GetProperty("families").GetInt32());
        families.Select(family => family.GetProperty("id").GetString()).Should().OnlyHaveUniqueItems();

        var recordedDeletedPaths = ReadFamilyStrings(families, "deletedPaths")
            .Concat(ReadFamilyStrings(families, "closureDeletedPaths"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        recordedDeletedPaths.Should().HaveCount(expectedCounts.GetProperty("physicallyDeletedPaths").GetInt32());
        recordedDeletedPaths.Should().OnlyHaveUniqueItems();
        recordedDeletedPaths.Should().Equal(ReadPhysicalDeletions(
            root,
            ledger.GetProperty("recoveryCheckpoint").GetString()!,
            ledger.GetProperty("productionTarget").GetString()!));

        var recordedCompileRemoves = ReadCompileRemoves(families);
        recordedCompileRemoves.Should().HaveCount(expectedCounts.GetProperty("compileExcludedEntries").GetInt32());
        recordedCompileRemoves.Should().OnlyHaveUniqueItems();
        recordedCompileRemoves.Should().Equal(ReadProductionCompileRemoves(root));

        var recordedOrphanedRoots = ReadFamilyStrings(families, "orphanedRoots");
        recordedOrphanedRoots.Should().HaveCount(expectedCounts.GetProperty("orphanedProductionRoots").GetInt32());
        recordedOrphanedRoots.Should().OnlyHaveUniqueItems();
        recordedOrphanedRoots.Should().Equal(ReadOrphanedProductionRoots(root));

        var recordedPackages = ReadFamilyStrings(families, "packageArtifacts");
        recordedPackages.Should().HaveCount(expectedCounts.GetProperty("retiredPackageArtifacts").GetInt32());
        recordedPackages.Should().OnlyHaveUniqueItems();
        recordedPackages.Should().Equal(ReadRetiredProjectPackages(root, recordedDeletedPaths));
        AssertPackagesAreOutsideTheCurrentManifest(root, recordedPackages);
    }

    [Fact]
    public void Ledger_HasClosedDispositionsNormativeOwnersRecoverySymbolInventoriesAndEvidence()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var ledgerText = File.ReadAllText(Path.Combine(root, LedgerPath));
        ledgerText.Contains("unresolved", StringComparison.OrdinalIgnoreCase).Should().BeFalse(
            "the completed ledger may not carry an unresolved disposition or placeholder");

        using var document = JsonDocument.Parse(ledgerText);
        var ledger = document.RootElement;
        ledger.GetProperty("schemaVersion").GetInt32().Should().Be(1);

        var recoveryCheckpoint = ledger.GetProperty("recoveryCheckpoint").GetString()!;
        recoveryCheckpoint.Should().MatchRegex("^[0-9a-f]{40}$");
        GitObjectExists(root, recoveryCheckpoint).Should().BeTrue("the recovery checkpoint must remain available");

        var productionTarget = ledger.GetProperty("productionTarget").GetString()!;
        productionTarget.Should().MatchRegex("^[0-9a-f]{40}$");
        GitIsAncestor(root, productionTarget).Should().BeTrue("the ledger must remain based on its reviewed production target");

        var allowedDispositions = ledger.GetProperty("allowedDispositions")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        allowedDispositions.Should().Equal("Remove", "ReplaceOrRelocate", "Defer", "DeadOrDuplicate");
        var tasks = File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "tasks.md"));

        var families = ledger.GetProperty("families").EnumerateArray().ToArray();
        foreach (var family in families)
        {
            var id = RequiredString(family, "id");
            allowedDispositions.Should().Contain(RequiredString(family, "disposition"),
                "family '{0}' must use the closed disposition set", id);

            var normativeIds = RequiredStrings(family, "normativeIds");
            normativeIds.Should().OnlyHaveUniqueItems();
            foreach (var normativeId in normativeIds)
            {
                AssertNormativeRequirementExists(root, normativeId, id);
            }

            var owner = RequiredString(family, "owner");
            (owner.StartsWith("active:", StringComparison.Ordinal) ||
             owner.StartsWith("task:", StringComparison.Ordinal)).Should().BeTrue(
                "family '{0}' must name its active owner or exact future task", id);
            AssertOwnerExists(root, tasks, owner, id);

            var recoveryPaths = RequiredStrings(family, "recoveryPaths");
            foreach (var recoveryPath in recoveryPaths)
            {
                AssertRecoveryPathExists(root, recoveryCheckpoint, productionTarget, recoveryPath, id);
            }

            foreach (var successorPath in OptionalStrings(family, "successorPaths"))
            {
                PathExists(root, successorPath).Should().BeTrue(
                    "family '{0}' successor '{1}' must exist", id, successorPath);
            }

            foreach (var resolvedRoot in OptionalStrings(family, "resolvedRoots"))
            {
                EnumerateNonBuildFiles(Path.Combine(root, resolvedRoot)).Should().BeEmpty(
                    "family '{0}' resolved root '{1}' must contain no production or test source", id, resolvedRoot);
            }

            RequiredStrings(family, "symbolFamilies").Should().OnlyHaveUniqueItems();
            foreach (var evidence in RequiredStrings(family, "executableEvidence"))
            {
                AssertExecutableEvidenceExists(root, evidence, id);
            }
        }

        var inventories = ledger.GetProperty("symbolInventories").EnumerateArray().ToArray();
        inventories.Should().HaveCount(ledger.GetProperty("expectedCounts").GetProperty("symbolInventories").GetInt32());
        inventories.Select(item => RequiredString(item, "id")).Should().OnlyHaveUniqueItems();
        foreach (var inventory in inventories)
        {
            AssertSymbolInventory(root, inventory);
            AssertExecutableEvidenceExists(root, RequiredString(inventory, "evidence"), RequiredString(inventory, "id"));
            var owner = RequiredString(inventory, "owner");
            owner.Should().StartWith("task:");
            AssertOwnerExists(root, tasks, owner, RequiredString(inventory, "id"));
        }
    }

    [Fact]
    public void Ledger_SqlServerDispositionMatchesTheHumanReadableCompanion()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var document = LoadLedger(root);
        var family = document.RootElement.GetProperty("families").EnumerateArray()
            .Single(item => RequiredString(item, "id") == "sqlserver-provider-orphan");

        RequiredString(family, "disposition").Should().Be("ReplaceOrRelocate");
        RequiredString(family, "owner").Should().Be("active:OrcaCore.Providers.SqlServer");
        var companion = File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "production-deletion-ledger.md"));
        companion.Should().Contain(
            "| Provisional SQL Server project/package and migration | Replace/relocate | `OrcaCore.Providers.SqlServer` |");
    }

    [Fact]
    public void Ledger_AllCompanionDispositionsAndScheduledOwnersMatchTheMachineReadableLedger()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var document = LoadLedger(root);
        var families = document.RootElement.GetProperty("families").EnumerateArray().ToArray();
        var companion = File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "production-deletion-ledger.md"));
        var rows = Regex.Matches(
                companion,
                @"(?m)^\| (?<inventory>[^|]+?) \| (?<disposition>Replace\s*/\s*relocate|Remove|Defer|Dead\s*/\s*duplicate) \| (?<owner>[^|]+?) \|")
            .Cast<Match>()
            .ToArray();

        rows.Should().HaveCount(families.Length,
            "the human-readable companion must retain one ordered row per machine-readable family");
        for (var index = 0; index < families.Length; index++)
        {
            var family = families[index];
            var familyId = RequiredString(family, "id");
            NormalizeCompanionDisposition(rows[index].Groups["disposition"].Value)
                .Should().Be(RequiredString(family, "disposition"),
                    "companion row '{0}' must agree with family '{1}'",
                    rows[index].Groups["inventory"].Value.Trim(),
                    familyId);

            var owner = RequiredString(family, "owner");
            var companionOwner = rows[index].Groups["owner"].Value.Replace("`", string.Empty);
            if (owner.StartsWith("task:", StringComparison.Ordinal))
            {
                companionOwner.Should().MatchRegex(
                    $@"(?i)\btask\s+{Regex.Escape(owner["task:".Length..])}\b",
                    "companion row for family '{0}' must name the same scheduled task owner",
                    familyId);
            }
            else if (Regex.Match(owner, @"^active:(?<path>.+\.md)#(?<section>\d+(?:\.\d+)*)$") is { Success: true } ownerMatch)
            {
                companionOwner.Should().Contain(ownerMatch.Groups["path"].Value);
                companionOwner.Should().Contain(ownerMatch.Groups["section"].Value);
            }
        }
    }

    [Fact]
    public void Ledger_RequiredCapabilityGapsStayDeferredToTheirLoadBearingTasks()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var document = LoadLedger(root);
        var families = document.RootElement.GetProperty("families").EnumerateArray()
            .ToDictionary(family => RequiredString(family, "id"), StringComparer.Ordinal);
        var requiredDeferrals = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dag-authoring-and-runner-relocation"] = "task:8.5"
        };

        foreach (var (familyId, owner) in requiredDeferrals)
        {
            families.Should().ContainKey(familyId);
            RequiredString(families[familyId], "disposition").Should().Be("Defer",
                "required capability gap '{0}' has no complete active equivalent yet", familyId);
            RequiredString(families[familyId], "owner").Should().Be(owner);
        }

        var restoredCapabilities = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider-retention-capability-replacement"] =
                "active:OrcaCore.Provider.Abstractions+OrcaCore.Providers.InMemory+OrcaCore.Providers.PostgreSql",
            ["management-and-operational-projection-replacement"] =
                "active:OrcaCore.Provider.Abstractions+OrcaCore.Engine.Ephemeral",
            ["bcl-telemetry-relocation"] =
                "active:OrcaCore.Engine.Durable+OrcaCore.Engine.Ephemeral+OrcaCore.Durable.Hosting"
        };
        foreach (var (familyId, owner) in restoredCapabilities)
        {
            families.Should().ContainKey(familyId);
            RequiredString(families[familyId], "disposition").Should().Be("ReplaceOrRelocate");
            RequiredString(families[familyId], "owner").Should().Be(owner);
        }

        var tasks = File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "tasks.md"));
        foreach (var taskId in new[] { "8.5" })
        {
            Regex.Matches(tasks, $@"(?m)^\s*-\s+\[ \]\s+{Regex.Escape(taskId)}\b")
                .Should().ContainSingle("load-bearing owner task '{0}' must remain explicitly pending", taskId);
        }
    }

    private static JsonDocument LoadLedger(string root) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(root, LedgerPath)));

    private static string[] ReadPhysicalDeletions(
        string root,
        string recoveryCheckpoint,
        string productionTarget)
    {
        return new[] { recoveryCheckpoint, productionTarget }
            .SelectMany(commit => RunGit(root, "ls-tree", "-r", "--name-only", commit, "--", "src", "samples"))
            .Distinct(StringComparer.Ordinal)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Where(path => !File.Exists(ToAbsolutePath(root, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadProductionCompileRemoves(string root)
    {
        return new[] { "src", "samples" }
            .SelectMany(scope => Directory.EnumerateFiles(Path.Combine(root, scope), "*.csproj", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .SelectMany(project => XDocument.Load(project).Descendants("Compile")
                .Select(element => element.Attribute("Remove")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => $"{NormalizePath(Path.GetRelativePath(root, project))}|{NormalizePath(value!)}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadOrphanedProductionRoots(string root)
    {
        return Directory.EnumerateDirectories(Path.Combine(root, "src"))
            .Where(directory => !Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Any())
            .Where(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Any(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                             path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)))
            .Select(path => NormalizePath(Path.GetRelativePath(root, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadRetiredProjectPackages(string root, IReadOnlyCollection<string> deletedPaths)
    {
        return deletedPaths
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Directory.EnumerateFiles(Path.GetDirectoryName(ToAbsolutePath(root, path))!, "*.csproj").Any())
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertPackagesAreOutsideTheCurrentManifest(string root, IEnumerable<string> retiredPackages)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json")));
        var activePackages = manifest.RootElement.GetProperty("packages").EnumerateArray()
            .Select(package => package.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        retiredPackages.Intersect(activePackages, StringComparer.Ordinal).Should().BeEmpty(
            "retired/deferred package artifacts must not leak into the exact v1 package manifest");
    }

    private static string[] ReadFamilyStrings(IEnumerable<JsonElement> families, string propertyName) =>
        families.SelectMany(family => OptionalStrings(family, propertyName))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] EnumerateNonBuildFiles(string root)
    {
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];
    }

    private static string[] ReadCompileRemoves(IEnumerable<JsonElement> families) =>
        families.SelectMany(family => family.TryGetProperty("compileRemoves", out var values)
                ? values.EnumerateArray().Select(value =>
                    $"{NormalizePath(RequiredString(value, "project"))}|{NormalizePath(RequiredString(value, "remove"))}")
                : [])
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] RequiredStrings(JsonElement element, string propertyName)
    {
        var values = element.GetProperty(propertyName).EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        values.Should().NotBeEmpty("'{0}' is required", propertyName);
        values.Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value), "'{0}' cannot contain blanks", propertyName);
        return values.Select(value => value!).ToArray();
    }

    private static string[] OptionalStrings(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var values)
            ? values.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [];

    private static string NormalizeCompanionDisposition(string disposition) =>
        Regex.Replace(disposition, @"[\s/]", string.Empty).ToLowerInvariant() switch
        {
            "replacerelocate" => "ReplaceOrRelocate",
            "deadduplicate" => "DeadOrDuplicate",
            "remove" => "Remove",
            "defer" => "Defer",
            var value => value
        };

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName).GetString();
        value.Should().NotBeNullOrWhiteSpace("'{0}' is required", propertyName);
        return value!;
    }

    private static void AssertOwnerExists(string root, string tasks, string owner, string familyId)
    {
        if (owner.StartsWith("task:", StringComparison.Ordinal))
        {
            var taskId = owner["task:".Length..];
            Regex.Matches(tasks, $@"(?m)^\s*-\s+\[[ x]\]\s+{Regex.Escape(taskId)}\b")
                .Should().ContainSingle(
                    "family or inventory '{0}' must name one real task rather than a task-shaped placeholder",
                    familyId);
            return;
        }

        const string activePrefix = "active:";
        var activeOwner = owner[activePrefix.Length..];
        var documentOwner = Regex.Match(activeOwner, @"^(?<path>.+\.md)#(?<section>\d+(?:\.\d+)*)$");
        if (!documentOwner.Success)
        {
            return;
        }

        var documentPath = documentOwner.Groups["path"].Value;
        var section = documentOwner.Groups["section"].Value;
        var absolutePath = ToAbsolutePath(root, documentPath);
        File.Exists(absolutePath).Should().BeTrue(
            "family '{0}' active document owner '{1}' must exist",
            familyId,
            documentPath);
        Regex.Matches(File.ReadAllText(absolutePath), $@"(?m)^#+\s+{Regex.Escape(section)}(?:\s|\b)")
            .Should().ContainSingle(
                "family '{0}' active document owner must name one real section '{1}'",
                familyId,
                section);
    }

    private static void AssertNormativeRequirementExists(string root, string coordinate, string familyId)
    {
        var separator = coordinate.IndexOf("::", StringComparison.Ordinal);
        separator.Should().BeGreaterThan(0, "family '{0}' normative coordinate must be capability::requirement", familyId);
        var capability = coordinate[..separator];
        var requirement = coordinate[(separator + 2)..];
        var specPath = Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "specs",
            capability,
            "spec.md");
        File.Exists(specPath).Should().BeTrue("family '{0}' capability '{1}' must exist", familyId, capability);
        File.ReadAllLines(specPath).Should().Contain($"### Requirement: {requirement}",
            "family '{0}' must cite an exact normative requirement heading", familyId);
    }

    private static void AssertRecoveryPathExists(
        string root,
        string recoveryCheckpoint,
        string productionTarget,
        string recoveryPath,
        string familyId)
    {
        if (recoveryPath.StartsWith("current:", StringComparison.Ordinal))
        {
            var currentPath = recoveryPath["current:".Length..];
            PathExists(root, currentPath).Should().BeTrue(
                "family '{0}' recovery path '{1}' must exist", familyId, currentPath);
            return;
        }

        var match = Regex.Match(recoveryPath, @"^git:(?<commit>[0-9a-f]{40}):(?<path>.+)$");
        match.Success.Should().BeTrue(
            "family '{0}' recovery path must identify an exact commit and path", familyId);
        var commit = match.Groups["commit"].Value;
        var objectPath = match.Groups["path"].Value;
        GitObjectExists(root, commit).Should().BeTrue(
            "family '{0}' recovery commit '{1}' must remain readable", familyId, commit);
        GitIsAncestor(root, recoveryCheckpoint, commit).Should().BeTrue(
            "family '{0}' recovery commit must descend from the ledger checkpoint", familyId);
        GitIsAncestor(root, commit, productionTarget).Should().BeTrue(
            "family '{0}' recovery commit must not postdate the reviewed production target", familyId);
        GitObjectExists(root, $"{commit}:{objectPath}").Should().BeTrue(
            "family '{0}' recovery object '{1}' must remain readable", familyId, recoveryPath);
    }

    private static void AssertExecutableEvidenceExists(string root, string coordinate, string familyId)
    {
        const string prefix = "test:";
        coordinate.Should().StartWith(prefix, "family '{0}' executable evidence must use test:path#member", familyId);
        var separator = coordinate.LastIndexOf('#');
        separator.Should().BeGreaterThan(prefix.Length, "family '{0}' evidence must name a member", familyId);
        var source = coordinate[prefix.Length..separator];
        var member = coordinate[(separator + 1)..];
        var absoluteSource = ToAbsolutePath(root, source);
        File.Exists(absoluteSource).Should().BeTrue("family '{0}' evidence source '{1}' must exist", familyId, source);
        Regex.Matches(File.ReadAllText(absoluteSource), $@"\b{Regex.Escape(member)}\s*\(")
            .Should().ContainSingle("family '{0}' evidence member '{1}' must be exact", familyId, coordinate);
    }

    private static void AssertSymbolInventory(string root, JsonElement inventory)
    {
        var id = RequiredString(inventory, "id");
        var source = RequiredString(inventory, "source");
        var field = RequiredString(inventory, "field");
        var sourceText = File.ReadAllText(ToAbsolutePath(root, source));
        var match = Regex.Match(
            sourceText,
            $@"(?s)\b{Regex.Escape(field)}\s*=\s*\{{(?<body>.*?)\}};");
        match.Success.Should().BeTrue("symbol inventory '{0}' field '{1}' must exist", id, field);
        var values = Regex.Matches(match.Groups["body"].Value, "\"(?<value>[^\"]+)\"")
            .Select(item => item.Groups["value"].Value)
            .ToArray();
        values.Should().HaveCount(inventory.GetProperty("expectedCount").GetInt32());
        values.Should().OnlyHaveUniqueItems();
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', values) + "\n");
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant().Should()
            .Be(RequiredString(inventory, "orderedSha256"), "symbol inventory '{0}' must remain exact", id);
    }

    private static bool PathExists(string root, string path)
    {
        var absolutePath = ToAbsolutePath(root, path);
        return File.Exists(absolutePath) || Directory.Exists(absolutePath);
    }

    private static string ToAbsolutePath(string root, string path) =>
        Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static bool GitObjectExists(string root, string objectName) =>
        RunGit(root, ["cat-file", "-e", objectName], throwOnFailure: false).ExitCode == 0;

    private static bool GitIsAncestor(string root, string commit) =>
        GitIsAncestor(root, commit, "HEAD");

    private static bool GitIsAncestor(string root, string ancestor, string descendant) =>
        RunGit(root, ["merge-base", "--is-ancestor", ancestor, descendant], throwOnFailure: false).ExitCode == 0;

    private static string[] RunGit(string root, params string[] arguments) =>
        RunGit(root, arguments, throwOnFailure: true).Output;

    private static GitResult RunGit(string root, IReadOnlyCollection<string> arguments, bool throwOnFailure)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (throwOnFailure && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }

        return new GitResult(
            process.ExitCode,
            output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record GitResult(int ExitCode, string[] Output);
}
