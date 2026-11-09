using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class EphemeralFailureSettlementSourceGuards
{
    [Fact]
    public void SourcePreparation_BindsTheActualApprovedCanonicalChainWithoutSourceApproval()
    {
        _ = EphemeralFailureSettlementSourceReview.AuthorizedPaths(FixtureDefinitions.RepositoryRoot());
    }
}

internal static class EphemeralFailureSettlementSourceReview
{
    private const string Prefix = "openspec/changes/expose-ephemeral-failures-and-settlement/";
    internal const string Base = "5960f0b5e920e2a53b34e38290d20c6e545fbf2a";
    private const string RegistryHash = "d62cd0d28712b18550216f5b487a42a85b557726a7fe06eccb7d1c7a53e2e282";

    internal static string[] AuthorizedPaths(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, Prefix + "artifacts/task-3-source-authorization-2026-11-02.json"));
        Hash(bytes).Should().Be(RegistryHash);
        using var document = JsonDocument.Parse(bytes);
        var record = document.RootElement;
        record.GetProperty("stage").GetString().Should().Be("CanonicalApprovedSourceInProgress");
        record.GetProperty("approvalAuthority").GetString().Should().Be("DirectUserApproval");
        record.GetProperty("sourceImplementationAuthorized").GetBoolean().Should().BeTrue();
        record.GetProperty("sourceApprovalEvidencePath").ValueKind.Should().Be(JsonValueKind.Null);
        record.GetProperty("sourceReviewPending").GetBoolean().Should().BeTrue();
        record.GetProperty("nativeQualificationComplete").GetBoolean().Should().BeFalse();
        record.GetProperty("pushAuthorized").GetBoolean().Should().BeFalse();
        const string checkpoint = "ecd188a4b4ee4d45ecdaddc7112523e20f0cc1b1";
        record.GetProperty("canonicalCheckpoint").GetString().Should().Be(checkpoint);
        record.GetProperty("canonicalParent").GetString().Should().Be("7697c7981c02d03408cbccf9a2e0b9bade33ca14");
        record.GetProperty("canonicalTree").GetString().Should().Be("ae97c26e5c088c3c5430d9fd6644379984d30922");
        record.GetProperty("canonicalVerdictCommit").GetString().Should().Be(Base);
        Git(root, "rev-parse", checkpoint + "^").Trim().Should().Be("7697c7981c02d03408cbccf9a2e0b9bade33ca14");
        Git(root, "rev-parse", checkpoint + "^{tree}").Trim().Should().Be("ae97c26e5c088c3c5430d9fd6644379984d30922");
        Git(root, "rev-parse", Base + "^").Trim().Should().Be(checkpoint);
        var verdictPath = Prefix + "artifacts/task-2-1-independent-review-verdict-2026-11-01.md";
        record.GetProperty("canonicalVerdictPath").GetString().Should().Be(verdictPath);
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", Base).Trim().Should().Be(verdictPath);
        var verdict = File.ReadAllBytes(Path.Combine(root, verdictPath));
        Hash(verdict).Should().Be("96ce82184702d169620da0810dff1c093970375148c4cc531c4daf5259ce0f22");
        Hash(verdict).Should().Be(record.GetProperty("canonicalVerdictSha256").GetString());
        Encoding.UTF8.GetBytes(Git(root, "show", Base + ":" + verdictPath)).Should().Equal(verdict);
        var files = record.GetProperty("sourceFiles").EnumerateObject().ToArray();
        files.Select(file => file.Name).Should().OnlyContain(path => path.StartsWith("src/OrcaCore.Abstractions/", StringComparison.Ordinal) ||
            path.StartsWith("src/OrcaCore.Core/", StringComparison.Ordinal) || path.StartsWith("src/OrcaCore.Engine.Ephemeral/", StringComparison.Ordinal));
        foreach (var file in files)
        {
            Hash(Encoding.UTF8.GetBytes(Git(root, "show", Base + ":" + file.Name))).Should().Be(file.Value.GetProperty("beforeSha256").GetString());
            Hash(File.ReadAllBytes(Path.Combine(root, file.Name))).Should().Be(file.Value.GetProperty("afterSha256").GetString());
        }
        Git(root, "diff", "--name-only", Base, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(files.Select(file => file.Name));
        Git(root, "ls-files", "--others", "--exclude-standard", "--", "src").Should().BeEmpty();
        var tasks = File.ReadAllText(Path.Combine(root, Prefix + "tasks.md"));
        foreach (var task in new[] { "2.1", "2.2" }) tasks.Should().Contain("- [x] " + task + " ");
        foreach (var task in new[] { "3.1", "3.2", "3.3", "3.4", "3.5", "3.6" }) tasks.Should().Contain("- [ ] " + task + " ");
        return files.Select(file => file.Name).ToArray();
    }

    internal static string PriorApi(string value)
    {
        const string property = "  property [System.Private.CoreLib]System.String? ExceptionType { get:public } attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]\n";
        const string method = "  method public WaitForSettlementAsync([System.Private.CoreLib]System.Threading.CancellationToken cancellationToken = null) -> [System.Private.CoreLib]System.Threading.Tasks.ValueTask\n";
        value.Split(property).Should().HaveCount(2);
        value.Split(method).Should().HaveCount(2);
        return value.Replace(property, "", StringComparison.Ordinal).Replace(method, "", StringComparison.Ordinal);
    }

    internal static byte[] HistoricalBytes(string root, string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, Prefix + "artifacts/task-3-source-authorization-2026-11-02.json"));
        Hash(bytes).Should().Be(RegistryHash);
        using var document = JsonDocument.Parse(bytes);
        var current = File.ReadAllBytes(Path.Combine(root, path));
        if (!document.RootElement.GetProperty("sourceFiles").TryGetProperty(path, out var pin)) return current;
        Hash(current).Should().Be(pin.GetProperty("afterSha256").GetString());
        var before = Encoding.UTF8.GetBytes(Git(root, "show", Base + ":" + path));
        Hash(before).Should().Be(pin.GetProperty("beforeSha256").GetString());
        return before;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot verify source authority through Git.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
}
