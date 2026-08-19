using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OpenSpecCorpusGuards
{
    [Fact]
    public void CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var openSpecRoot = Path.Combine(root, "openspec");
        var canonicalRoot = Path.Combine(openSpecRoot, "specs");
        var changesRoot = Path.Combine(openSpecRoot, "changes");

        var canonicalCapabilityDirectories = ReadCapabilityDirectories(canonicalRoot);
        canonicalCapabilityDirectories.Should().NotBeEmpty(
            "the canonical OpenSpec corpus must contain capability specifications");
        RequireSpecFiles(root, canonicalCapabilityDirectories, "canonical");
        var canonicalCapabilities = canonicalCapabilityDirectories
            .Select(GetDirectoryName)
            .ToHashSet(StringComparer.Ordinal);

        var activeRequirementOwners = new List<RequirementOwner>();
        var activeCapabilityDirectories = new List<string>();
        foreach (var changeRoot in Directory
                     .EnumerateDirectories(changesRoot)
                     .Where(path => !string.Equals(
                         GetDirectoryName(path),
                         "archive",
                         StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var proposalPath = Path.Combine(changeRoot, "proposal.md");
            if (!File.Exists(proposalPath))
            {
                throw new InvalidDataException(
                    $"Active change '{RelativePath(root, changeRoot)}' must contain proposal.md.");
            }

            var declaredCapabilities = ReadDeclaredCapabilities(root, proposalPath);
            var capabilityDirectories = ReadCapabilityDirectories(Path.Combine(changeRoot, "specs"));
            RequireSpecFiles(root, capabilityDirectories, "active");
            ReconcileDeclaredCapabilities(root, changeRoot, declaredCapabilities, capabilityDirectories);

            var unexplainedMissingCapabilities = declaredCapabilities
                .Where(capability =>
                    capability.Kind == CapabilityKind.Modified &&
                    !canonicalCapabilities.Contains(capability.Name))
                .Select(capability => capability.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (unexplainedMissingCapabilities.Length > 0)
            {
                throw new InvalidDataException(
                    $"Active change '{RelativePath(root, changeRoot)}' modifies canonical capabilities " +
                    $"that do not exist: {string.Join(", ", unexplainedMissingCapabilities)}.");
            }

            activeCapabilityDirectories.AddRange(capabilityDirectories);
            foreach (var capabilityDirectory in capabilityDirectories)
            {
                activeRequirementOwners.AddRange(ReadRequirementOwners(
                    root,
                    GetDirectoryName(changeRoot),
                    GetDirectoryName(capabilityDirectory),
                    Path.Combine(capabilityDirectory, "spec.md")));
            }
        }

        activeCapabilityDirectories.Should().NotBeEmpty(
            "the active OpenSpec corpus must contain capability deltas");
        activeRequirementOwners.Should().NotBeEmpty(
            "every active delta must expose at least one requirement heading to the ownership scan");

        var duplicateOwners = activeRequirementOwners
            .GroupBy(
                owner => $"{owner.Capability}\0{owner.Requirement}",
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
                $"{group.First().Capability} :: {group.First().Requirement} => " +
                string.Join(", ", group
                    .Select(owner => $"{owner.Change} [{owner.Operation}]")
                    .Order(StringComparer.Ordinal)))
            .ToArray();
        duplicateOwners.Should().BeEmpty(
            "one active change may own each capability requirement heading; duplicate owners were: {0}",
            string.Join("; ", duplicateOwners));

        PreserveRuntimeConcurrencyStrayDisposition(root, changesRoot);
    }

    private static void PreserveRuntimeConcurrencyStrayDisposition(string root, string changesRoot)
    {
        const string changeName = "add-runtime-concurrency-limits";
        var activeRoot = Path.Combine(changesRoot, changeName);
        var archiveRoot = Path.Combine(changesRoot, "archive");
        var archivedRoots = Directory.Exists(archiveRoot)
            ? Directory
                .EnumerateDirectories(archiveRoot)
                .Where(path => GetDirectoryName(path).EndsWith(
                    $"-{changeName}",
                    StringComparison.Ordinal))
                .ToArray()
            : [];
        var recordedRoots = archivedRoots
            .Concat(Directory.Exists(activeRoot) ? [activeRoot] : [])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (recordedRoots.Length != 1)
        {
            throw new InvalidDataException(
                $"Expected exactly one active or archived record for '{changeName}', found " +
                $"{recordedRoots.Length}: {string.Join(", ", recordedRoots.Select(path => RelativePath(root, path)))}.");
        }

        var runtimeConcurrencyRoot = recordedRoots[0];
        var runtimeConcurrencyCapabilities = ReadCapabilityDirectories(
                Path.Combine(runtimeConcurrencyRoot, "specs"))
            .Select(GetDirectoryName)
            .ToArray();
        var declaredRuntimeConcurrencyCapabilities = ReadDeclaredCapabilities(
                root,
                Path.Combine(runtimeConcurrencyRoot, "proposal.md"))
            .Select(capability => capability.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        runtimeConcurrencyCapabilities.Should().Equal("runtime-resource-governance");
        declaredRuntimeConcurrencyCapabilities.Should().Equal(runtimeConcurrencyCapabilities);
        Directory.Exists(Path.Combine(
                runtimeConcurrencyRoot,
                "specs",
                "state-driven-runtime"))
            .Should().BeFalse(
                "the former state-driven-runtime directory was a verified stray, not a declared delta");
    }

    private static string[] ReadCapabilityDirectories(string specsRoot) =>
        Directory.Exists(specsRoot)
            ? Directory.EnumerateDirectories(specsRoot).Order(StringComparer.Ordinal).ToArray()
            : [];

    private static void RequireSpecFiles(
        string root,
        IEnumerable<string> capabilityDirectories,
        string corpus)
    {
        var missingSpecs = capabilityDirectories
            .Where(path => !File.Exists(Path.Combine(path, "spec.md")))
            .Select(path => RelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missingSpecs.Length > 0)
        {
            throw new InvalidDataException(
                $"Every {corpus} capability directory must contain spec.md; missing: " +
                string.Join(", ", missingSpecs));
        }
    }

    private static DeclaredCapability[] ReadDeclaredCapabilities(string root, string proposalPath)
    {
        if (!File.Exists(proposalPath))
        {
            throw new InvalidDataException(
                $"OpenSpec change record '{RelativePath(root, proposalPath)}' must contain proposal.md.");
        }

        var lines = File.ReadAllLines(proposalPath);
        var capabilitiesStart = Array.FindIndex(
            lines,
            line => string.Equals(line, "## Capabilities", StringComparison.Ordinal));
        if (capabilitiesStart < 0)
        {
            throw new InvalidDataException(
                $"Proposal '{RelativePath(root, proposalPath)}' must contain an exact '## Capabilities' heading.");
        }

        var impactStart = Array.FindIndex(
            lines,
            capabilitiesStart + 1,
            line => string.Equals(line, "## Impact", StringComparison.Ordinal));
        if (impactStart < 0)
        {
            throw new InvalidDataException(
                $"Proposal '{RelativePath(root, proposalPath)}' must contain '## Impact' after '## Capabilities'.");
        }

        var declaredCapabilities = new List<DeclaredCapability>();
        CapabilityKind? currentKind = null;
        for (var index = capabilitiesStart + 1; index < impactStart; index++)
        {
            currentKind = lines[index] switch
            {
                "### New Capabilities" => CapabilityKind.New,
                "### Modified Capabilities" => CapabilityKind.Modified,
                _ => currentKind
            };

            var match = Regex.Match(lines[index], @"^- `(?<name>[^`]+)`: ");
            if (!match.Success)
            {
                continue;
            }

            if (currentKind is null)
            {
                throw new InvalidDataException(
                    $"Capability '{match.Groups["name"].Value}' in '{RelativePath(root, proposalPath)}' " +
                    "must follow '### New Capabilities' or '### Modified Capabilities'.");
            }

            declaredCapabilities.Add(new DeclaredCapability(
                match.Groups["name"].Value,
                currentKind.Value));
        }

        var duplicates = declaredCapabilities
            .GroupBy(capability => capability.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidDataException(
                $"Proposal '{RelativePath(root, proposalPath)}' declares duplicate capabilities: " +
                string.Join(", ", duplicates));
        }

        return declaredCapabilities
            .OrderBy(capability => capability.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ReconcileDeclaredCapabilities(
        string root,
        string changeRoot,
        IReadOnlyCollection<DeclaredCapability> declaredCapabilities,
        IEnumerable<string> capabilityDirectories)
    {
        var declaredNames = declaredCapabilities
            .Select(capability => capability.Name)
            .ToHashSet(StringComparer.Ordinal);
        var actualNames = capabilityDirectories
            .Select(GetDirectoryName)
            .ToHashSet(StringComparer.Ordinal);
        var undeclaredDirectories = actualNames
            .Except(declaredNames, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingDirectories = declaredNames
            .Except(actualNames, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (undeclaredDirectories.Length == 0 && missingDirectories.Length == 0)
        {
            return;
        }

        throw new InvalidDataException(
            $"Change '{RelativePath(root, changeRoot)}' proposal/capability-directory mismatch. " +
            $"Undeclared directories: [{string.Join(", ", undeclaredDirectories)}]. " +
            $"Declared capabilities without directories: [{string.Join(", ", missingDirectories)}].");
    }

    private static RequirementOwner[] ReadRequirementOwners(
        string root,
        string change,
        string capability,
        string specPath)
    {
        var owners = new List<RequirementOwner>();
        string? operation = null;
        foreach (var line in File.ReadLines(specPath))
        {
            operation = line switch
            {
                "## ADDED Requirements" => "ADDED",
                "## MODIFIED Requirements" => "MODIFIED",
                "## REMOVED Requirements" => "REMOVED",
                _ => operation
            };

            const string requirementPrefix = "### Requirement: ";
            if (!line.StartsWith(requirementPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (operation is null)
            {
                throw new InvalidDataException(
                    $"Requirement heading in '{RelativePath(root, specPath)}' must follow an " +
                    "ADDED, MODIFIED, or REMOVED Requirements section.");
            }

            owners.Add(new RequirementOwner(
                change,
                capability,
                line[requirementPrefix.Length..],
                operation));
        }

        if (owners.Count == 0)
        {
            throw new InvalidDataException(
                $"Active delta '{RelativePath(root, specPath)}' must contain at least one requirement heading.");
        }

        return owners.ToArray();
    }

    private static string GetDirectoryName(string path) =>
        Path.GetFileName(path) ??
        throw new InvalidDataException($"Path '{path}' has no directory name.");

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private enum CapabilityKind
    {
        New,
        Modified
    }

    private sealed record DeclaredCapability(string Name, CapabilityKind Kind);

    private sealed record RequirementOwner(
        string Change,
        string Capability,
        string Requirement,
        string Operation);
}
