using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OpenSpecCorpusGuards
{
    private const string OpenTaskState = "Open";
    private const string CompleteTaskState = "Complete";
    private const string HistoricalCanonicalSourceCommit =
        "ba2478e995023b0712c44705174c2b0e3262f213";
    private const string HistoricalCanonicalRemovalCatalogSha256 =
        "81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd";
    private const int HistoricalCanonicalRemovalCatalogCount = 11;

    private static readonly string[] RequiredPostGateStages =
    [
        "amendment-approval",
        "canonical-open-spec",
        "numbered-requirements",
        "acceptance-criteria",
        "implementation-tasks",
        "executable-evidence",
        "refreeze",
        "independent-approval"
    ];

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
        var canonicalRequirements = canonicalCapabilityDirectories.ToDictionary(
            GetDirectoryName,
            capabilityDirectory => ReadCanonicalRequirements(
                root,
                Path.Combine(capabilityDirectory, "spec.md")),
            StringComparer.Ordinal);

        var activeRequirementOwners = new List<RequirementOwner>();
        var activeDeltaRequirements = new List<DeltaRequirement>();
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
                var capability = GetDirectoryName(capabilityDirectory);
                var capabilityKind = declaredCapabilities
                    .Single(declared => string.Equals(
                        declared.Name,
                        capability,
                        StringComparison.Ordinal))
                    .Kind;
                var deltaRequirements = ReadDeltaRequirements(
                    root,
                    GetDirectoryName(changeRoot),
                    capability,
                    capabilityKind,
                    Path.Combine(capabilityDirectory, "spec.md"));
                activeDeltaRequirements.AddRange(deltaRequirements);
                activeRequirementOwners.AddRange(deltaRequirements.Select(requirement =>
                    new RequirementOwner(
                        requirement.Change,
                        requirement.Capability,
                        requirement.Requirement,
                        requirement.Operation.ToString().ToUpperInvariant())));
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

        ValidateChangeToCanonicalProvenance(
            root,
            canonicalCapabilityDirectories,
            canonicalRequirements,
            activeDeltaRequirements,
            activeCapabilityDirectories);

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

    private static IReadOnlyDictionary<string, CanonicalRequirement> ReadCanonicalRequirements(
        string root,
        string specPath)
    {
        var requirements = ReadRequirementBlocks(root, specPath, isDelta: false);
        var duplicates = requirements
            .GroupBy(requirement => requirement.Requirement, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidDataException(
                $"Canonical spec '{RelativePath(root, specPath)}' contains duplicate requirement headings: " +
                string.Join(", ", duplicates));
        }

        return requirements.ToDictionary(
            requirement => requirement.Requirement,
            requirement => new CanonicalRequirement(requirement.Requirement, requirement.Block),
            StringComparer.Ordinal);
    }

    private static DeltaRequirement[] ReadDeltaRequirements(
        string root,
        string change,
        string capability,
        CapabilityKind capabilityKind,
        string specPath)
    {
        var requirements = ReadRequirementBlocks(root, specPath, isDelta: true)
            .Select(requirement => new DeltaRequirement(
                change,
                capability,
                capabilityKind,
                requirement.Requirement,
                requirement.Operation ?? throw new InvalidDataException(
                    $"Delta requirement '{requirement.Requirement}' in '{RelativePath(root, specPath)}' " +
                    "has no operation."),
                requirement.Block))
            .ToArray();
        foreach (var removed in requirements.Where(requirement =>
                     requirement.Operation == RequirementOperation.Removed))
        {
            var blockLines = removed.Block.Split('\n');
            if (!blockLines.Any(line =>
                    line.StartsWith("**Reason**:", StringComparison.Ordinal) &&
                    line.Length > "**Reason**:".Length) ||
                !blockLines.Any(line =>
                    line.StartsWith("**Migration**:", StringComparison.Ordinal) &&
                    line.Length > "**Migration**:".Length))
            {
                throw new InvalidDataException(
                    $"Removed requirement '{removed.Capability} :: {removed.Requirement}' in " +
                    $"'{RelativePath(root, specPath)}' must contain non-empty **Reason** and **Migration** lines.");
            }
        }

        if (requirements.Length == 0)
        {
            throw new InvalidDataException(
                $"Active delta '{RelativePath(root, specPath)}' must contain at least one requirement heading.");
        }

        return requirements;
    }

    private static RequirementBlock[] ReadRequirementBlocks(
        string root,
        string specPath,
        bool isDelta)
    {
        return ReadRequirementBlocks(
            File.ReadAllLines(specPath),
            RelativePath(root, specPath),
            isDelta);
    }

    private static RequirementBlock[] ReadRequirementBlocks(
        string[] lines,
        string source,
        bool isDelta)
    {
        var requirements = new List<RequirementBlock>();
        RequirementOperation? operation = null;
        for (var index = 0; index < lines.Length; index++)
        {
            if (isDelta && lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                operation = lines[index] switch
                {
                    "## ADDED Requirements" => RequirementOperation.Added,
                    "## MODIFIED Requirements" => RequirementOperation.Modified,
                    "## REMOVED Requirements" => RequirementOperation.Removed,
                    _ => null
                };
            }

            const string requirementPrefix = "### Requirement: ";
            if (!lines[index].StartsWith(requirementPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (isDelta && operation is null)
            {
                throw new InvalidDataException(
                    $"Requirement heading in '{source}' must follow an " +
                    "ADDED, MODIFIED, or REMOVED Requirements section.");
            }

            var end = index + 1;
            while (end < lines.Length &&
                   !lines[end].StartsWith(requirementPrefix, StringComparison.Ordinal) &&
                   !lines[end].StartsWith("## ", StringComparison.Ordinal))
            {
                end++;
            }

            var lastContentLine = end - 1;
            while (lastContentLine > index && lines[lastContentLine].Length == 0)
            {
                lastContentLine--;
            }

            requirements.Add(new RequirementBlock(
                lines[index][requirementPrefix.Length..],
                operation,
                string.Join('\n', lines[index..(lastContentLine + 1)])));
            index = end - 1;
        }

        return requirements.ToArray();
    }

    private static void ValidateChangeToCanonicalProvenance(
        string root,
        IEnumerable<string> canonicalCapabilityDirectories,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        IEnumerable<DeltaRequirement> activeDeltaRequirements,
        IEnumerable<string> activeCapabilityDirectories)
    {
        const string recordFormat =
            "change\\tcapability\\tcapability-kind\\toperation\\trequirement\\tstate\\t" +
            "delta-block-sha256\\tcanonical-block-sha256-or-dash";
        var rows = activeDeltaRequirements
            .Select(requirement => ClassifyProvenance(canonicalRequirements, requirement))
            .OrderBy(row => row.RecordLine, StringComparer.Ordinal)
            .ToArray();
        var record = string.Join('\n', rows.Select(row => row.RecordLine)) + "\n";
        var recordBytes = Encoding.UTF8.GetBytes(record);
        var checkpoint = FixtureDefinitions.Read<OpenSpecProvenanceCheckpoint>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json");

        checkpoint.SchemaVersion.Should().Be(5);
        checkpoint.RecordFormat.Should().Be(recordFormat);
        rows.Should().HaveCount(checkpoint.RecordCount);
        recordBytes.Should().HaveCount(checkpoint.RecordBytes);
        Sha256(record).Should().Be(checkpoint.RecordSha256);

        var pending = rows
            .Where(row => row.State is
                ProvenanceState.PendingAddition or
                ProvenanceState.PendingAddedCanonicalConflict or
                ProvenanceState.PendingModification or
                ProvenanceState.PendingRemoval)
            .ToArray();
        var actualPending = pending
            .GroupBy(row => row.Requirement.Capability, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProvenanceCapabilityCount(group.Key, group.Count()))
            .ToArray();
        actualPending.Should().Equal(
            checkpoint.PendingCanonicalOperations
                .OrderBy(item => item.Capability, StringComparer.Ordinal)
                .Select(item => new ProvenanceCapabilityCount(item.Capability, item.Count)));

        var taskLedger = File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "harmonize-downstream-capability-specs",
            "tasks.md"));
        var postGateCheckpoint = FixtureDefinitions.Read<PostGateAmendmentCheckpoint>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json");
        ValidatePostGateAmendmentPath(root, canonicalRequirements, postGateCheckpoint);
        foreach (var operation in checkpoint.PendingCanonicalOperations)
        {
            var taskBlock = ReadOpenTaskBlock(taskLedger, operation.TurnsGreenTask);
            taskBlock.Should().Contain(
                $"`{operation.Capability}` ({operation.Count})",
                "the turns-green task must explicitly own the capability and exact pending count");
        }

        var actualNewCapabilities = rows
            .Where(row => row.State == ProvenanceState.NewCapabilityOutsideCanonical)
            .GroupBy(row => row.Requirement.Capability, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProvenanceCapabilityCount(group.Key, group.Count()))
            .ToArray();
        actualNewCapabilities.Should().Equal(
            checkpoint.ActiveNewCapabilitiesOutsideCanonical
                .OrderBy(item => item.Capability, StringComparer.Ordinal)
                .Select(item => new ProvenanceCapabilityCount(item.Capability, item.Count)));
        foreach (var disposition in checkpoint.ActiveNewCapabilitiesOutsideCanonical)
        {
            disposition.Disposition.Should().NotBeNullOrWhiteSpace();
            File.Exists(Path.Combine(root, disposition.Evidence.Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue(
                    "every active new capability outside canonical must cite repository evidence");
        }

        var actualNewCapabilitiesAlreadyCanonical = rows
            .Where(row =>
                row.Requirement.CapabilityKind == CapabilityKind.New &&
                canonicalRequirements.ContainsKey(row.Requirement.Capability))
            .GroupBy(
                row => $"{row.Requirement.Change}\0{row.Requirement.Capability}",
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProvenanceDeclaredNewCapability(
                group.First().Requirement.Change,
                group.First().Requirement.Capability,
                group.Count()))
            .ToArray();
        actualNewCapabilitiesAlreadyCanonical.Should().Equal(
            checkpoint.ActiveNewCapabilitiesAlreadyCanonical
                .OrderBy(item => $"{item.Change}\0{item.Capability}", StringComparer.Ordinal)
                .Select(item => new ProvenanceDeclaredNewCapability(
                    item.Change,
                    item.Capability,
                    item.Count)));
        foreach (var disposition in checkpoint.ActiveNewCapabilitiesAlreadyCanonical)
        {
            disposition.Disposition.Should().NotBeNullOrWhiteSpace();
            File.Exists(Path.Combine(root, disposition.Evidence.Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue(
                    "every declared-new capability already in canonical must cite repository evidence");
            var amendment = postGateCheckpoint.Amendments.SingleOrDefault(item =>
                string.Equals(item.Id, disposition.PostGateAmendmentId, StringComparison.Ordinal));
            amendment.Should().NotBeNull(
                "every declared-new/already-canonical capability must link one permanent post-gate record");
            amendment!.OwningChange.Should().Be(disposition.Change);
            amendment.Capability.Should().Be(disposition.Capability);
            amendment.DeclaredRequirementCount.Should().Be(disposition.Count);
        }

        ValidateCanonicalCapabilityInventory(
            root,
            canonicalCapabilityDirectories,
            checkpoint);
        ValidateCapabilityDirectoryInventory(
            root,
            activeCapabilityDirectories,
            checkpoint);
        ValidateHistoricalCanonicalRemovals(root, rows, checkpoint);
        ValidateInfrastructureGuardCiLane(root);
        ValidateReviewArtifact(root, rows, checkpoint);

        checkpoint.SemanticApprovalEligible.Should().Be(pending.Length == 0);
        var semanticApproval = () => RequireSemanticApproval(pending);
        if (checkpoint.SemanticApprovalEligible)
        {
            semanticApproval.Should().NotThrow(
                "the checkpoint fixture declares complete change-to-canonical provenance");
        }
        else
        {
            semanticApproval.Should().Throw<InvalidDataException>()
                .WithMessage($"*{pending.Length} unresolved change-to-canonical operation(s)*")
                .WithMessage("*Structural OpenSpec validation is not semantic approval.*");
        }
    }

    private static void ValidateCapabilityDirectoryInventory(
        string root,
        IEnumerable<string> activeCapabilityDirectories,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        const string recordFormat =
            "repository-relative active openspec/changes/*/specs/*/ path with forward slashes and one trailing slash";
        var paths = activeCapabilityDirectories
            .Select(path => $"{RelativePath(root, path).TrimEnd('/')}/")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var record = string.Join('\n', paths) + "\n";
        var bytes = Encoding.UTF8.GetBytes(record);

        checkpoint.CapabilityDirectoryRecordFormat.Should().Be(recordFormat);
        paths.Should().HaveCount(checkpoint.CapabilityDirectoryCount);
        bytes.Should().HaveCount(checkpoint.CapabilityDirectoryBytes);
        Sha256(record).Should().Be(checkpoint.CapabilityDirectorySha256);
    }

    private static void ValidateCanonicalCapabilityInventory(
        string root,
        IEnumerable<string> canonicalCapabilityDirectories,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        const string recordFormat =
            "repository-relative openspec/specs/*/ path with forward slashes and one trailing slash";
        var paths = canonicalCapabilityDirectories
            .Select(path => $"{RelativePath(root, path).TrimEnd('/')}/")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var record = string.Join('\n', paths) + "\n";
        var bytes = Encoding.UTF8.GetBytes(record);

        checkpoint.CanonicalCapabilityRecordFormat.Should().Be(recordFormat);
        paths.Should().HaveCount(checkpoint.CanonicalCapabilityCount);
        bytes.Should().HaveCount(checkpoint.CanonicalCapabilityBytes);
        Sha256(record).Should().Be(checkpoint.CanonicalCapabilitySha256);
    }

    private static void ValidatePostGateAmendmentPath(
        string root,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        PostGateAmendmentCheckpoint checkpoint)
    {
        checkpoint.SchemaVersion.Should().Be(2);
        checkpoint.RequiredStages.Should().Equal(
            RequiredPostGateStages,
            "every late decision must re-enter each gate in the approved order");
        checkpoint.Amendments.Should().NotBeEmpty(
            "the known Section 7B post-gate decision must have a permanent record");
        checkpoint.Amendments.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        checkpoint.Amendments
            .Select(item => $"{item.OwningChange}\0{item.Capability}")
            .Should().OnlyHaveUniqueItems();

        foreach (var amendment in checkpoint.Amendments)
        {
            amendment.Id.Should().NotBeNullOrWhiteSpace();
            amendment.OwningChange.Should().NotBeNullOrWhiteSpace();
            amendment.Capability.Should().NotBeNullOrWhiteSpace();

            var changeRoot = ResolveChangeRecord(root, amendment.OwningChange);
            var proposalPath = Path.Combine(changeRoot, "proposal.md");
            var capabilityKind = ReadDeclaredCapabilities(root, proposalPath)
                .Single(declared => string.Equals(
                    declared.Name,
                    amendment.Capability,
                    StringComparison.Ordinal))
                .Kind;
            var specPath = Path.Combine(changeRoot, "specs", amendment.Capability, "spec.md");
            var amendmentRequirements = ReadDeltaRequirements(
                root,
                amendment.OwningChange,
                amendment.Capability,
                capabilityKind,
                specPath);
            var requirementIdentities = amendmentRequirements
                .Select(requirement => requirement.Requirement)
                .Order(StringComparer.Ordinal)
                .ToArray();
            amendment.RequirementIdentities.Should().HaveCount(amendment.DeclaredRequirementCount);
            amendment.RequirementIdentities.Should().OnlyHaveUniqueItems();
            requirementIdentities.Should().Equal(
                amendment.RequirementIdentities.Order(StringComparer.Ordinal),
                "the permanent post-gate record must bind the exact requirement identities " +
                "from either the active or dated archived owning change");
            var amendmentRows = amendmentRequirements
                .Select(requirement => ClassifyProvenance(canonicalRequirements, requirement))
                .ToArray();
            amendmentRows.Count(row => row.State is
                    ProvenanceState.PendingAddition or
                    ProvenanceState.PendingAddedCanonicalConflict or
                    ProvenanceState.PendingModification or
                    ProvenanceState.PendingRemoval)
                .Should().Be(
                    amendment.PendingCanonicalOperationCount,
                    "the post-gate record must not conceal unresolved canonical operations");

            ValidateTaskReference(root, amendment.OriginalApprovalTask);
            ValidateTaskReference(root, amendment.OriginalCanonicalSyncTask);
            ValidateTaskReference(root, amendment.AmendmentApprovalTask);
            var canonicalTask = ValidateTaskReference(root, amendment.CanonicalReconciliationTask);
            var canonicalCountClaim =
                $"`{amendment.Capability}` ({amendment.PendingCanonicalOperationCount})";
            if (string.Equals(
                    amendment.CanonicalReconciliationTask.ExpectedState,
                    CompleteTaskState,
                    StringComparison.Ordinal))
            {
                canonicalTask.Should().MatchRegex(
                    $@"(?s)\*\*Completed:\*\*.*?{Regex.Escape(canonicalCountClaim)}",
                    "a completed canonical reconciliation must record its exact resulting pending count in the completion statement");
            }
            else
            {
                canonicalTask.Should().Contain(
                    canonicalCountClaim,
                    "an open canonical reconciliation must name the exact capability and pending count");
            }

            var numberedTask = ValidateTaskReference(root, amendment.NumberedRequirementTask);
            amendment.NumberedRequirementPaths.Should().NotBeEmpty();
            ValidateOwnedPaths(root, numberedTask, amendment.NumberedRequirementPaths);

            var acceptanceTask = ValidateTaskReference(root, amendment.AcceptanceCriteriaTask);
            amendment.AcceptanceCriteriaPaths.Should().NotBeEmpty();
            ValidateOwnedPaths(root, acceptanceTask, amendment.AcceptanceCriteriaPaths);

            amendment.ImplementationTaskCount.Should().BeGreaterThan(0);
            amendment.ImplementationTasks.Should().HaveCount(
                amendment.ImplementationTaskCount,
                "every implementation task in the approved amendment range must remain pinned");
            amendment.ImplementationTasks
                .Select(reference => $"{reference.Change}\0{reference.Task}")
                .Should().OnlyHaveUniqueItems();
            foreach (var implementationTask in amendment.ImplementationTasks)
            {
                ValidateTaskReference(root, implementationTask);
            }

            ValidateTaskReference(root, amendment.RefreezeTask);
            amendment.ExecutableEvidencePaths.Should().NotBeEmpty();
            amendment.ExecutableEvidencePaths.Should().OnlyHaveUniqueItems();
            foreach (var evidencePath in amendment.ExecutableEvidencePaths)
            {
                RequireNonEmptyFile(root, evidencePath, "post-gate executable evidence");
            }

            RequireApprovalEvidence(
                root,
                amendment.AmendmentApprovalEvidencePath,
                amendment.AmendmentApprovalTask.Task);
            RequireNonEmptyFile(root, amendment.RefreezeEvidencePath, "post-gate refreeze evidence");
            RequireApprovalEvidence(
                root,
                amendment.IndependentApprovalEvidencePath,
                amendment.RefreezeTask.Task);
        }

        var artifactPath = RequireNonEmptyFile(
            root,
            checkpoint.ArtifactPath,
            "post-gate amendment artifact");
        var normalizedArtifact = NormalizeLineEndings(File.ReadAllText(artifactPath));
        Sha256(normalizedArtifact).Should().Be(
            checkpoint.ArtifactNormalizedSha256,
            "the human-readable post-gate amendment path must remain byte-accountable");
        foreach (var stage in RequiredPostGateStages)
        {
            normalizedArtifact.Should().Contain($"`{stage}`");
        }

        foreach (var amendment in checkpoint.Amendments)
        {
            normalizedArtifact.Should().Contain($"`{amendment.Id}`");
            normalizedArtifact.Should().Contain(
                $"`{amendment.Capability}` ({amendment.DeclaredRequirementCount})");
            normalizedArtifact.Should().Contain(
                $"exactly {amendment.ImplementationTaskCount} reshape tasks",
                "the human-readable record must pin the implementation-task count independently of the task array");
        }
    }

    private static void ValidateOwnedPaths(
        string root,
        string taskBlock,
        IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            RequireNonEmptyFile(root, path, "post-gate normative evidence");
            taskBlock.Should().Contain(
                $"`{path}`",
                "the assigned task must name every post-gate normative target literally");
        }
    }

    private static string ValidateTaskReference(string root, PostGateTaskReference reference)
    {
        var changeRoot = ResolveChangeRecord(root, reference.Change);
        var taskLedgerPath = Path.Combine(changeRoot, "tasks.md");
        var taskLedger = File.ReadAllText(taskLedgerPath);
        var matches = Regex.Matches(
            taskLedger,
            $@"(?ms)^- \[(?<state>[ x])\] {Regex.Escape(reference.Task)}\b.*?(?=^- \[[ x]\] \d+\.\d+\b|\z)",
            RegexOptions.CultureInvariant);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"Post-gate task reference '{reference.Change}:{reference.Task}' must resolve exactly once; " +
                $"found {matches.Count} in '{RelativePath(root, taskLedgerPath)}'.");
        }

        var actualState = string.Equals(
            matches[0].Groups["state"].Value,
            "x",
            StringComparison.Ordinal)
            ? CompleteTaskState
            : OpenTaskState;
        reference.ExpectedState.Should().BeOneOf(OpenTaskState, CompleteTaskState);
        actualState.Should().Be(
            reference.ExpectedState,
            "post-gate task-state transitions require an explicit reviewed fixture refreeze");
        return matches[0].Value;
    }

    private static string ResolveChangeRecord(string root, string change)
    {
        var changesRoot = Path.Combine(root, "openspec", "changes");
        var activeRoot = Path.Combine(changesRoot, change);
        var archiveRoot = Path.Combine(changesRoot, "archive");
        var archivedRoots = Directory.Exists(archiveRoot)
            ? Directory
                .EnumerateDirectories(archiveRoot)
                .Where(path => GetDirectoryName(path).EndsWith(
                    $"-{change}",
                    StringComparison.Ordinal))
            : [];
        var records = archivedRoots
            .Concat(Directory.Exists(activeRoot) ? [activeRoot] : [])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (records.Length != 1)
        {
            throw new InvalidDataException(
                $"Post-gate change '{change}' must have exactly one active or dated archived record; " +
                $"found {records.Length}: {string.Join(", ", records.Select(path => RelativePath(root, path)))}.");
        }

        return records[0];
    }

    private static string RequireNonEmptyFile(string root, string relativePath, string description)
    {
        relativePath.Should().NotBeNullOrWhiteSpace();
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"{description} must exist at '{relativePath}'");
        new FileInfo(path).Length.Should().BeGreaterThan(0, $"{description} must not be empty");
        return path;
    }

    private static void RequireApprovalEvidence(
        string root,
        string relativePath,
        string task)
    {
        var path = RequireNonEmptyFile(root, relativePath, "post-gate independent approval evidence");
        var evidence = NormalizeLineEndings(File.ReadAllText(path));
        evidence.Should().MatchRegex(
            $@"(?<![\d.]){Regex.Escape(task)}(?![\d.])",
            "post-gate approval evidence must identify its owning task");
        evidence.Should().MatchRegex(
            @"(?mi)^(?:\*\*Verdict:\*\*[ \t]*APPROVE|\*\*APPROVE\*\*)[ \t]*$",
            "post-gate approval evidence must carry an explicit approving verdict");
    }

    private static void ValidateHistoricalCanonicalRemovals(
        string root,
        IEnumerable<ProvenanceRow> rows,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        var synchronizedRemovals = rows
            .Where(row =>
                row.Requirement.Operation == RequirementOperation.Removed &&
                row.State == ProvenanceState.Synchronized)
            .Select(row => new HistoricalRemovalIdentity(
                row.Requirement.Capability,
                row.Requirement.Requirement))
            .OrderBy(item => $"{item.Capability}\0{item.Requirement}", StringComparer.Ordinal)
            .ToArray();
        var recordedRemovals = checkpoint.HistoricalCanonicalRemovals
            .Select(item => new HistoricalRemovalIdentity(item.Capability, item.Requirement))
            .OrderBy(item => $"{item.Capability}\0{item.Requirement}", StringComparer.Ordinal)
            .ToArray();
        ValidateHistoricalRemovalCatalogSemantics();
        RequireActiveRemovalsAreCataloged(
            new ActiveSynchronizedRemovalSet(synchronizedRemovals),
            new PermanentHistoricalRemovalCatalog(recordedRemovals));

        checkpoint.HistoricalCanonicalSourceCommit.Should().Be(
            HistoricalCanonicalSourceCommit,
            "the reviewed historical source anchor is part of the executable contract");
        checkpoint.HistoricalCanonicalRemovalCatalogSha256.Should().Be(
            HistoricalCanonicalRemovalCatalogSha256,
            "the embedded removal catalog must be pinned independently of its fixture values");
        var catalog = string.Join(
            '\n',
            checkpoint.HistoricalCanonicalRemovals
                .OrderBy(item => $"{item.Capability}\0{item.Requirement}", StringComparer.Ordinal)
                .Select(item => string.Join(
                    '\t',
                    checkpoint.HistoricalCanonicalSourceCommit,
                    item.SourcePath,
                    item.Capability,
                    item.Requirement,
                    item.CanonicalBlockSha256))) + "\n";
        Sha256(catalog).Should().Be(
            HistoricalCanonicalRemovalCatalogSha256,
            "a fixture-local block/hash pair must not be self-attesting");

        var historicalSources = TryReadHistoricalSources(
            root,
            checkpoint.HistoricalCanonicalSourceCommit,
            checkpoint.HistoricalCanonicalRemovals.Select(item => item.SourcePath));

        foreach (var evidence in checkpoint.HistoricalCanonicalRemovals)
        {
            evidence.SourcePath.Should().Be(
                $"openspec/specs/{evidence.Capability}/spec.md",
                "historical removal evidence must name its canonical capability source");
            evidence.CanonicalBlockSha256.Should().MatchRegex("^[0-9a-f]{64}$");
            evidence.CanonicalBlock.Should().NotBeNullOrWhiteSpace();
            var normalizedBlock = NormalizeLineEndings(evidence.CanonicalBlock);
            var historicalRequirements = ReadRequirementBlocks(
                normalizedBlock.Split('\n'),
                $"embedded historical evidence for {evidence.Capability}",
                isDelta: false);
            historicalRequirements.Should().ContainSingle(
                "each historical-removal fixture entry must embed exactly one canonical requirement block");
            var historicalBlock = historicalRequirements[0];
            historicalBlock.Requirement.Should().Be(
                evidence.Requirement,
                "the embedded canonical block heading must identify its removal record");
            historicalBlock.Block.Should().Be(
                normalizedBlock,
                "the embedded evidence must not contain unhashed preamble or trailing content");

            Sha256(historicalBlock.Block).Should().Be(
                evidence.CanonicalBlockSha256,
                "the fixture must retain the exact reviewed historical canonical requirement block even when Git history is rewritten");

            if (historicalSources is not null)
            {
                var sourceRequirements = ReadRequirementBlocks(
                    NormalizeLineEndings(historicalSources[evidence.SourcePath]).Split('\n'),
                    $"{checkpoint.HistoricalCanonicalSourceCommit}:{evidence.SourcePath}",
                    isDelta: false);
                var sourceRequirement = sourceRequirements.SingleOrDefault(requirement =>
                    string.Equals(
                        requirement.Requirement,
                        evidence.Requirement,
                        StringComparison.Ordinal));
                sourceRequirement.Should().NotBeNull(
                    "the recorded source commit must contain every historical canonical heading");
                sourceRequirement!.Block.Should().Be(
                    historicalBlock.Block,
                    "embedded removal evidence must reproduce the canonical block at its source commit");
            }
        }
    }

    private static void RequireActiveRemovalsAreCataloged(
        ActiveSynchronizedRemovalSet activeRemovals,
        PermanentHistoricalRemovalCatalog permanentCatalog)
    {
        permanentCatalog.Entries.Should().HaveCount(
            HistoricalCanonicalRemovalCatalogCount,
            "the permanent historical-removal catalog must retain reviewed entries after their owning deltas are archived");
        RequireActiveRemovalSubset(activeRemovals, permanentCatalog);
    }

    private static void RequireActiveRemovalSubset(
        ActiveSynchronizedRemovalSet activeRemovals,
        PermanentHistoricalRemovalCatalog permanentCatalog)
    {
        activeRemovals.Entries.Should().BeSubsetOf(
            permanentCatalog.Entries,
            "every currently synchronized removal must have exact immutable canonical-history evidence, while archived removal evidence remains permanently cataloged");
    }

    private static void ValidateHistoricalRemovalCatalogSemantics()
    {
        var active = new HistoricalRemovalIdentity("active-capability", "Active requirement");
        var archived = new HistoricalRemovalIdentity("archived-capability", "Archived requirement");
        RequireActiveRemovalSubset(
            new ActiveSynchronizedRemovalSet([active]),
            new PermanentHistoricalRemovalCatalog([active, archived]));
    }

    private static IReadOnlyDictionary<string, string>? TryReadHistoricalSources(
        string root,
        string commit,
        IEnumerable<string> sourcePaths)
    {
        commit.Should().MatchRegex("^[0-9a-f]{40}$");
        var commitProbe = RunGit(root, "cat-file", "-e", $"{commit}^{{commit}}");
        if (commitProbe.ExitCode != 0)
        {
            return null;
        }

        return sourcePaths
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                sourcePath => sourcePath,
                sourcePath =>
                {
                    var result = RunGit(root, "show", $"{commit}:{sourcePath}");
                    result.ExitCode.Should().Be(
                        0,
                        "historical canonical source '{0}:{1}' must be readable; git reported {2}",
                        commit,
                        sourcePath,
                        result.StandardError);
                    return result.StandardOutput;
                },
                StringComparer.Ordinal);
    }

    private static GitResult RunGit(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start git for OpenSpec provenance validation.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new GitResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());
    }

    private static void ValidateInfrastructureGuardCiLane(string root)
    {
        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        var workflow = NormalizeLineEndings(File.ReadAllText(workflowPath));
        var workflowSteps = Regex.Matches(
            workflow,
            @"(?ms)^[ \t]*-[ \t]+(?:name|uses):[^\n]*(?:\n.*?)(?=^[ \t]*-[ \t]+(?:name|uses):|\z)",
            RegexOptions.CultureInvariant)
            .Cast<Match>()
            .ToArray();
        var checkoutSteps = workflowSteps
            .Where(step => Regex.IsMatch(
                step.Value,
                @"(?m)^[ \t]*(?:-[ \t]+)?uses:[ \t]+actions/checkout@v4[ \t]*$",
                RegexOptions.CultureInvariant))
            .ToArray();
        checkoutSteps.Should().NotBeEmpty("CI must check out repository source before running validation");
        checkoutSteps.Should().OnlyContain(
            step => Regex.IsMatch(
                step.Value,
                @"(?m)^[ \t]+fetch-depth:[ \t]+0[ \t]*$",
                RegexOptions.CultureInvariant),
            "every CI checkout must retain full history so available historical corroboration cannot silently degrade to an object-missing skip");

        var guardSteps = Regex.Matches(
            workflow,
            @"(?ms)^[ \t]*-[ \t]+name:[ \t]+Infrastructure contract guards[ \t]*$.*?(?=^[ \t]*-[ \t]+(?:name|uses):|\z)",
            RegexOptions.CultureInvariant);
        guardSteps.Should().ContainSingle(
            "CI must execute the must-be-green infrastructure guard disposition exactly once");
        var normalizedCommand = Regex.Replace(guardSteps[0].Value, @"\s+", " ");
        normalizedCommand.Should().Contain(
            "dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj");
        normalizedCommand.Should().Contain("--no-build --no-restore -c Release");
        normalizedCommand.Should().Contain("--filter \"Disposition=Infrastructure\"");
    }

    private static void ValidateReviewArtifact(
        string root,
        IReadOnlyCollection<ProvenanceRow> rows,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        var artifactPath = Path.Combine(
            root,
            checkpoint.ArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(artifactPath).Should().BeTrue("the reviewed provenance artifact must exist");
        var normalizedArtifact = NormalizeLineEndings(File.ReadAllText(artifactPath));
        var pendingCount = rows.Count(row => row.State is not
            ProvenanceState.Synchronized and not
            ProvenanceState.NewCapabilityOutsideCanonical);
        RequireArtifactClaim(normalizedArtifact, $"- record rows: {checkpoint.RecordCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record bytes: {FormatCount(checkpoint.RecordBytes)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record SHA-256: `{checkpoint.RecordSha256}`");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- synchronized operations: {rows.Count(row => row.State == ProvenanceState.Synchronized)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- pending canonical operations: {pendingCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            "- declared new-capability requirements outside the canonical set: " +
            rows.Count(row => row.State == ProvenanceState.NewCapabilityOutsideCanonical));
        RequireArtifactClaim(
            normalizedArtifact,
            $"- semantic approval eligible: {(checkpoint.SemanticApprovalEligible ? "yes" : "no")}");
        foreach (var state in Enum.GetValues<ProvenanceState>())
        {
            RequireArtifactClaim(
                normalizedArtifact,
                $"| `{state}` | {rows.Count(row => row.State == state)} |");
        }

        RequireArtifactClaim(
            normalizedArtifact,
            $"- canonical capability directories: {checkpoint.CanonicalCapabilityCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record bytes: {FormatCount(checkpoint.CanonicalCapabilityBytes)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- SHA-256: `{checkpoint.CanonicalCapabilitySha256}`");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- capability directories: {checkpoint.CapabilityDirectoryCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record bytes: {FormatCount(checkpoint.CapabilityDirectoryBytes)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- SHA-256: `{checkpoint.CapabilityDirectorySha256}`");
        foreach (var operation in checkpoint.PendingCanonicalOperations)
        {
            RequireArtifactClaim(
                normalizedArtifact,
                $"| `{operation.Capability}` | {operation.Count} | task {operation.TurnsGreenTask} |");
        }

        Sha256(normalizedArtifact).Should().Be(
            checkpoint.ArtifactNormalizedSha256,
            "the human-readable provenance artifact must remain byte-accountable after LF normalization");
    }

    private static void RequireArtifactClaim(string artifact, string claim) =>
        artifact.Should().Contain(
            claim,
            "the human-readable provenance artifact must agree with live executable evidence");

    private static string FormatCount(int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    private static string ReadOpenTaskBlock(string taskLedger, string task)
    {
        var match = Regex.Match(
            taskLedger,
            $@"(?ms)^- \[ \] {Regex.Escape(task)}\b.*?(?=^- \[[ x]\] \d+\.\d+\b|\z)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidDataException(
                $"Every unresolved canonical operation must have one open turns-green task '{task}'.");
        }

        return match.Value;
    }

    private static ProvenanceRow ClassifyProvenance(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        DeltaRequirement requirement)
    {
        if (!canonicalRequirements.TryGetValue(requirement.Capability, out var capabilityRequirements))
        {
            if (requirement.CapabilityKind != CapabilityKind.New)
            {
                throw new InvalidDataException(
                    $"Modified capability '{requirement.Capability}' has no canonical spec for " +
                    $"'{requirement.Change} :: {requirement.Requirement}'.");
            }

            return CreateProvenanceRow(
                requirement,
                ProvenanceState.NewCapabilityOutsideCanonical,
                canonicalBlock: null);
        }

        var canonicalExists = capabilityRequirements.TryGetValue(
            requirement.Requirement,
            out var canonicalRequirement);
        var canonicalBlock = canonicalRequirement?.Block;
        var blocksMatch = canonicalExists &&
                          string.Equals(requirement.Block, canonicalBlock, StringComparison.Ordinal);
        var state = requirement.Operation switch
        {
            RequirementOperation.Added when !canonicalExists => ProvenanceState.PendingAddition,
            RequirementOperation.Added when blocksMatch => ProvenanceState.Synchronized,
            RequirementOperation.Added => ProvenanceState.PendingAddedCanonicalConflict,
            RequirementOperation.Modified when !canonicalExists => throw new InvalidDataException(
                $"Modified requirement heading '{requirement.Capability} :: {requirement.Requirement}' " +
                $"from '{requirement.Change}' does not match canonical verbatim."),
            RequirementOperation.Modified when blocksMatch => ProvenanceState.Synchronized,
            RequirementOperation.Modified => ProvenanceState.PendingModification,
            RequirementOperation.Removed when canonicalExists => ProvenanceState.PendingRemoval,
            RequirementOperation.Removed => ProvenanceState.Synchronized,
            _ => throw new ArgumentOutOfRangeException(nameof(requirement.Operation))
        };

        return CreateProvenanceRow(requirement, state, canonicalBlock);
    }

    private static ProvenanceRow CreateProvenanceRow(
        DeltaRequirement requirement,
        ProvenanceState state,
        string? canonicalBlock)
    {
        var canonicalHash = canonicalBlock is null ? "-" : Sha256(canonicalBlock);
        var line = string.Join('\t',
            requirement.Change,
            requirement.Capability,
            requirement.CapabilityKind.ToString().ToUpperInvariant(),
            requirement.Operation.ToString().ToUpperInvariant(),
            requirement.Requirement,
            state.ToString(),
            Sha256(requirement.Block),
            canonicalHash);
        return new ProvenanceRow(requirement, state, line);
    }

    private static void RequireSemanticApproval(IReadOnlyCollection<ProvenanceRow> pending)
    {
        if (pending.Count == 0)
        {
            return;
        }

        var summary = pending
            .GroupBy(row => row.Requirement.Capability, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}");
        throw new InvalidDataException(
            $"Checkpoint provenance has {pending.Count} unresolved change-to-canonical operation(s): " +
            $"{string.Join(", ", summary)}. Structural OpenSpec validation is not semantic approval.");
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static string NormalizeLineEndings(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

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

    private enum RequirementOperation
    {
        Added,
        Modified,
        Removed
    }

    private enum ProvenanceState
    {
        Synchronized,
        PendingAddition,
        PendingAddedCanonicalConflict,
        PendingModification,
        PendingRemoval,
        NewCapabilityOutsideCanonical
    }

    private sealed record DeclaredCapability(string Name, CapabilityKind Kind);

    private sealed record RequirementBlock(
        string Requirement,
        RequirementOperation? Operation,
        string Block);

    private sealed record CanonicalRequirement(string Requirement, string Block);

    private sealed record DeltaRequirement(
        string Change,
        string Capability,
        CapabilityKind CapabilityKind,
        string Requirement,
        RequirementOperation Operation,
        string Block);

    private sealed record ProvenanceRow(
        DeltaRequirement Requirement,
        ProvenanceState State,
        string RecordLine);

    private sealed record ProvenanceCapabilityCount(string Capability, int Count);

    private sealed record OpenSpecProvenanceCheckpoint(
        int SchemaVersion,
        string RecordFormat,
        int RecordCount,
        int RecordBytes,
        string RecordSha256,
        bool SemanticApprovalEligible,
        ProvenancePendingCapability[] PendingCanonicalOperations,
        ProvenanceNewCapability[] ActiveNewCapabilitiesOutsideCanonical,
        ProvenanceNewCanonicalCapability[] ActiveNewCapabilitiesAlreadyCanonical,
        string HistoricalCanonicalSourceCommit,
        string HistoricalCanonicalRemovalCatalogSha256,
        HistoricalCanonicalRemoval[] HistoricalCanonicalRemovals,
        string CanonicalCapabilityRecordFormat,
        int CanonicalCapabilityCount,
        int CanonicalCapabilityBytes,
        string CanonicalCapabilitySha256,
        string CapabilityDirectoryRecordFormat,
        int CapabilityDirectoryCount,
        int CapabilityDirectoryBytes,
        string CapabilityDirectorySha256,
        string ArtifactPath,
        string ArtifactNormalizedSha256);

    private sealed record ProvenancePendingCapability(
        string Capability,
        int Count,
        string TurnsGreenTask);

    private sealed record ProvenanceNewCapability(
        string Capability,
        int Count,
        string Disposition,
        string Evidence);

    private sealed record ProvenanceDeclaredNewCapability(
        string Change,
        string Capability,
        int Count);

    private sealed record ProvenanceNewCanonicalCapability(
        string Change,
        string Capability,
        int Count,
        string Disposition,
        string PostGateAmendmentId,
        string Evidence);

    private sealed record PostGateAmendmentCheckpoint(
        int SchemaVersion,
        string[] RequiredStages,
        PostGateAmendment[] Amendments,
        string ArtifactPath,
        string ArtifactNormalizedSha256);

    private sealed record PostGateAmendment(
        string Id,
        string OwningChange,
        string Capability,
        int DeclaredRequirementCount,
        int PendingCanonicalOperationCount,
        string[] RequirementIdentities,
        PostGateTaskReference OriginalApprovalTask,
        PostGateTaskReference OriginalCanonicalSyncTask,
        PostGateTaskReference AmendmentApprovalTask,
        PostGateTaskReference CanonicalReconciliationTask,
        PostGateTaskReference NumberedRequirementTask,
        PostGateTaskReference AcceptanceCriteriaTask,
        int ImplementationTaskCount,
        PostGateTaskReference[] ImplementationTasks,
        PostGateTaskReference RefreezeTask,
        string[] NumberedRequirementPaths,
        string[] AcceptanceCriteriaPaths,
        string[] ExecutableEvidencePaths,
        string AmendmentApprovalEvidencePath,
        string RefreezeEvidencePath,
        string IndependentApprovalEvidencePath);

    private sealed record PostGateTaskReference(
        string Change,
        string Task,
        string ExpectedState);

    private sealed record HistoricalCanonicalRemoval(
        string Capability,
        string Requirement,
        string SourcePath,
        string CanonicalBlockSha256,
        string CanonicalBlock);

    private sealed record GitResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed record HistoricalRemovalIdentity(
        string Capability,
        string Requirement);

    private sealed record ActiveSynchronizedRemovalSet(
        IReadOnlyCollection<HistoricalRemovalIdentity> Entries);

    private sealed record PermanentHistoricalRemovalCatalog(
        IReadOnlyCollection<HistoricalRemovalIdentity> Entries);

    private sealed record RequirementOwner(
        string Change,
        string Capability,
        string Requirement,
        string Operation);
}
