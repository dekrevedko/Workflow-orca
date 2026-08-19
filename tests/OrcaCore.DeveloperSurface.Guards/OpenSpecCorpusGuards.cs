using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OpenSpecCorpusGuards
{
    [Fact]
    public void ActiveChangeCapabilityDirectories_HaveSpecsAndPreserveResolvedStrayDisposition()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var changesRoot = Path.Combine(root, "openspec", "changes");
        var capabilityDirectories = Directory
            .EnumerateDirectories(changesRoot)
            .Select(changeRoot => Path.Combine(changeRoot, "specs"))
            .Where(Directory.Exists)
            .SelectMany(specsRoot => Directory.EnumerateDirectories(specsRoot))
            .Order(StringComparer.Ordinal)
            .ToArray();

        capabilityDirectories.Should().NotBeEmpty(
            "the active OpenSpec corpus must contain capability deltas");
        capabilityDirectories
            .Where(path => !File.Exists(Path.Combine(path, "spec.md")))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Should().BeEmpty(
                "every openspec/changes/*/specs/*/ capability directory must contain spec.md");

        var runtimeConcurrencyRoot = Path.Combine(
            changesRoot,
            "add-runtime-concurrency-limits");
        var runtimeConcurrencyCapabilities = capabilityDirectories
            .Where(path => string.Equals(
                Path.GetDirectoryName(Path.GetDirectoryName(path)),
                runtimeConcurrencyRoot,
                StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();
        var declaredRuntimeConcurrencyCapabilities = ReadDeclaredCapabilities(
            Path.Combine(runtimeConcurrencyRoot, "proposal.md"));

        runtimeConcurrencyCapabilities.Should().Equal("runtime-resource-governance");
        declaredRuntimeConcurrencyCapabilities.Should().Equal(runtimeConcurrencyCapabilities);
        Directory.Exists(Path.Combine(
                runtimeConcurrencyRoot,
                "specs",
                "state-driven-runtime"))
            .Should().BeFalse(
                "the former state-driven-runtime directory was a verified stray, not a declared delta");
    }

    private static string[] ReadDeclaredCapabilities(string proposalPath)
    {
        var proposal = File.ReadAllText(proposalPath);
        var capabilitiesStart = proposal.IndexOf("## Capabilities", StringComparison.Ordinal);
        var impactStart = proposal.IndexOf("## Impact", capabilitiesStart, StringComparison.Ordinal);

        capabilitiesStart.Should().BeGreaterThanOrEqualTo(0);
        impactStart.Should().BeGreaterThan(capabilitiesStart);

        return Regex.Matches(
                proposal[capabilitiesStart..impactStart],
                @"(?m)^- `(?<name>[^`]+)`: ")
            .Select(match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
