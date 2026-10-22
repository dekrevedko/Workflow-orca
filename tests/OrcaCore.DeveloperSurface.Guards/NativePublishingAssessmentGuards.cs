using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class NativePublishingAssessmentGuards
{
    [Fact]
    public void ContractAuthority_IsTheExactApprovedCheckpointAndVerdictOnlyChild()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var change = ChangeDirectory(root);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(change, "artifacts/task-2-approval-binding-2026-10-21.json")));
        var binding = document.RootElement;
        var checkpoint = binding.GetProperty("contractCheckpoint").GetString()!;
        var verdict = binding.GetProperty("contractVerdictCommit").GetString()!;
        var verdictPath = binding.GetProperty("contractVerdictPath").GetString()!;
        Git(root, "rev-list", "--parents", "-n", "1", checkpoint).Trim().Split(' ')
            .Should().Equal(checkpoint, binding.GetProperty("contractParent").GetString());
        Git(root, "rev-parse", checkpoint + "^{tree}").Trim().Should().Be(binding.GetProperty("contractTree").GetString());
        Git(root, "rev-list", "--parents", "-n", "1", verdict).Trim().Split(' ').Should().Equal(verdict, checkpoint);
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", verdict).Trim().Should().Be(verdictPath);
        var committed = Encoding.UTF8.GetBytes(Git(root, "show", verdict + ":" + verdictPath));
        Hash(committed).Should().Be(binding.GetProperty("contractVerdictSha256").GetString());
        File.ReadAllBytes(Path.Combine(change, "artifacts", Path.GetFileName(verdictPath))).Should().Equal(committed);
        binding.GetProperty("sourceApprovalEvidencePath").ValueKind.Should().Be(JsonValueKind.Null);
        binding.GetProperty("assessmentApprovalEvidencePath").ValueKind.Should().Be(JsonValueKind.Null);
        binding.GetProperty("productSourceAmendment").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void FourAppendedCanonicalBlocks_EqualTheirF1ClarifiedDeltas()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var change = ChangeDirectory(root);
        var delta = Normalize(File.ReadAllText(Path.Combine(change, "specs/quality-and-verification/spec.md")));
        var canonical = Normalize(File.ReadAllText(Path.Combine(root, "openspec/specs/quality-and-verification/spec.md")));
        var baseline = Normalize(Git(root, "show", "dbd829e2104fba70f6f678513fcaa08bf17a6045:openspec/specs/quality-and-verification/spec.md"));
        canonical.Should().StartWith(baseline);
        var additions = Blocks(delta);
        additions.Should().HaveCount(4);
        Blocks(canonical[baseline.Length..]).Should().Equal(additions);
        var warnings = additions.Single(block => block.StartsWith("### Requirement: AOT and trimming findings", StringComparison.Ordinal));
        warnings.Should().Contain("The six base cells SHALL leave JsonSerializerIsReflectionEnabledByDefault unset.");
        warnings.Should().Contain("JsonSerializerIsReflectionEnabledByDefault=true");
        warnings.Should().Contain("at most AssessedConditional");
        warnings.Should().Contain("exact\nexception/inner-exception types and messages");
    }

    [Fact]
    public void RiskInventory_PreservesTheApprovedRowsAndAddsTheMissingMetadataCallsite()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var change = ChangeDirectory(root);
        var oldRows = File.ReadAllLines(Path.Combine(change, "artifacts/contract-source-risk-inventory-2026-10-20.tsv"));
        var rows = File.ReadAllLines(Path.Combine(change, "artifacts/task-2-source-risk-inventory-2026-10-21.tsv"));
        rows.Should().HaveCount(91);
        oldRows.Should().HaveCount(90);
        rows.Should().Contain(oldRows);
        var added = rows.Except(oldRows, StringComparer.Ordinal).Should().ContainSingle().Which.Split('\t');
        added[0].Should().Be("src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs");
        added[1].Should().Be("58");
        var source = Normalize(Git(root, "show", "eea51c9c2ba14d6b3bd0c963a3b5a38873fe1416:" + added[0])).Split('\n');
        Hash(Encoding.UTF8.GetBytes(source[57])).Should().Be(added[4]);
        source[57].Should().Contain("JsonSerializerOptions.Default.GetTypeInfo(type)");
    }

    private static string ChangeDirectory(string root)
    {
        var active = Path.Combine(root, "openspec/changes/assess-ephemeral-native-aot");
        if (Directory.Exists(active)) return active;
        return Directory.GetDirectories(Path.Combine(root, "openspec/changes/archive"), "*-assess-ephemeral-native-aot").Single();
    }

    private static string[] Blocks(string text) => Regex.Matches(text, @"(?ms)^### Requirement: .*?(?=^### Requirement: |^## |\z)")
        .Select(match => match.Value.TrimEnd('\n')).ToArray();
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git lookup could not start.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, "Git authority lookups fail closed: {0}", error);
        return output;
    }
}
