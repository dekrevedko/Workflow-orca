using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string AddressedWakeChange = "retain-first-usable-dag-wake";
    private const string AddressedWakeProposal = "20203c48db381b7e995f0dd07a1c4122eff82b36";
    private const string AddressedWakeImplementation = "bc4ab313a124874481312d2b738a796f36ceca6c";
    private const string AddressedWakeCanonical = "openspec/specs/durable-persistence-and-outbox/spec.md";
    private const string AddressedWakeNumbered = "docs/specs/10-provider-model-and-extensibility.md";
    private const string PrereleaseNotes = "docs/prerelease-notes.md";
    private const string AddressedWakeRoot = "openspec/changes/" + AddressedWakeChange + "/";

    private static void ValidateAddressedWakeApproval(string root)
    {
        const string delta = AddressedWakeRoot + "specs/durable-persistence-and-outbox/spec.md";
        var approved = DagFacadeApprovedHistory.TextAt(root, AddressedWakeProposal, delta);
        NormalizeLineEndings(File.ReadAllText(Path.Combine(root, delta))).Should().Be(approved);
        var verdict = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, AddressedWakeRoot, "artifacts/contract-approval.md")));
        Sha256(verdict).Should().Be("22b364e9745174f70e64be08e2eff74f491de0983af53c938e84cbd054a047f3");
        RunGit(root, "merge-base", "--is-ancestor", AddressedWakeProposal, "HEAD").ExitCode.Should().Be(0);
        RunGit(root, "merge-base", "--is-ancestor", AddressedWakeImplementation, "HEAD").ExitCode.Should().Be(0);
        var evidence = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, AddressedWakeRoot, "artifacts/qualification.md")));
        Sha256(evidence).Should().Be("f0907a72346661ba07be6b7d7b34206ab5bef6cc465d99857a2079469614cba6");
        foreach (var task in new[] { "1.1", "2.1", "3.1", "3.2" })
            ValidateTaskReference(root, new(AddressedWakeChange, task, CompleteTaskState));
        foreach (var path in new[] { AddressedWakeCanonical, AddressedWakeNumbered, PrereleaseNotes })
        {
            var current = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, path)));
            var beforeWake = BeforeAddressedWake(root, path, current);
            BeforeSqlSupport(root, path, beforeWake).Should().Be(DagFacadeApprovedHistory.TextAt(root, AddressedWakeProposal, path));
        }
    }

    private static void ProjectAddressedWakeRegistry(JsonObject registry)
    {
        var row = registry["firstUsableAddressedWake"]!.AsObject();
        Sha256(row.ToJsonString()).Should().Be("214e0e86c7f6a5f2c23b5de8fa58285f505618afe5df31f77bdf53bf6c6953d5",
            "only the approved addressed-wake contract row may be removed for historical checks");
        registry.Remove("firstUsableAddressedWake");
    }

    internal static string BeforeAddressedWake(string root, string path, string current)
    {
        if (path is not (AddressedWakeCanonical or AddressedWakeNumbered or PrereleaseNotes)) return current;
        if (path == AddressedWakeCanonical)
        {
            const string header = "## ADDED Requirements\n\n";
            var delta = DagFacadeApprovedHistory.TextAt(root, AddressedWakeProposal,
                AddressedWakeRoot + "specs/durable-persistence-and-outbox/spec.md");
            delta.Should().StartWith(header);
            var suffix = "\n" + delta[header.Length..];
            current.Should().EndWith(suffix, "the addressed-wake requirement must be synchronized verbatim");
            return current[..^suffix.Length];
        }

        var previous = DagFacadeApprovedHistory.TextAt(root, AddressedWakeProposal, path);
        var approved = DagFacadeApprovedHistory.TextAt(root, AddressedWakeImplementation, path);
        if (path == PrereleaseNotes)
        {
            approved.Should().EndWith(previous);
            var prefix = approved[..^previous.Length];
            current.Should().StartWith(prefix);
            return current[prefix.Length..];
        }

        approved.Should().StartWith(previous);
        var numberedSuffix = approved[previous.Length..];
        Sha256(numberedSuffix).Should().Be("96bf75b4bb9d649b240b057a4abe4054a6b98f2b14fb12a358bb45fbddcb249a");
        current.Should().EndWith(numberedSuffix, "only the approved PR-012a suffix may be projected");
        return current[..^numberedSuffix.Length];
    }
}
