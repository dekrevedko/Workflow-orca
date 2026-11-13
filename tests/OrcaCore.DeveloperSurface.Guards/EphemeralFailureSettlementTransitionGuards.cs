using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class EphemeralFailureSettlementTransitionGuards
{
    private const string Prefix = "openspec/changes/expose-ephemeral-failures-and-settlement/";
    private const string ContractVerdictCommit = "7697c7981c02d03408cbccf9a2e0b9bade33ca14";

    [Fact]
    public void CanonicalPreparation_DoesNotExposeOrImplementThePendingMembers()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        _ = EphemeralFailureSettlementSourceReview.AuthorizedPaths(root);
        typeof(WorkflowFailure).GetProperty("ExceptionType")!.PropertyType.Should().Be(typeof(string));
        typeof(WorkflowFailure).GetProperty("ExceptionType")!.SetMethod.Should().BeNull();
        typeof(WorkflowInstanceHandle).GetMethod("WaitForSettlementAsync")!.IsVirtual.Should().BeFalse();
        Git(root, ["diff", "--name-only", ContractVerdictCommit, "--",
            "Directory.Build.props", "Directory.Packages.props", "global.json",
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json"]).Trim().Should().BeEmpty();
    }

    [Fact]
    public void CurrentContract_RequiresDurableBytesNativeNamesAndPassiveOwnership()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        string Read(string path) => File.ReadAllText(Path.Combine(root, path)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var structured = Read("openspec/specs/structured-fiber-execution/spec.md");
        structured.Should().Contain("Ephemeral fiber records MAY carry detached type text");
        structured.Should().Contain("Durable engine-produced fiber, checkpoint, outbox, projection");
        structured.Should().Contain("failure bytes SHALL remain byte-identical to base 0a8e375");
        var quality = Read("openspec/specs/quality-and-verification/spec.md");
        quality.Should().Contain("actual stored\nfailure bytes").And.Contain("real PostgreSQL and SQL Server lanes");
        quality.Should().Contain("captured ExceptionType text equals its JIT");
        var surface = Read("openspec/specs/developer-facing-surface/spec.md");
        surface.Should().Contain("normative declaration companion SHALL change in the reviewed canonical");
        surface.Should().Contain("Executable public-API/compile/package baselines");
        var matrix = Read("docs/specs/17-selected-mode-capability-matrix.md");
        matrix.Should().Contain("public string? ExceptionType { get; }");
        matrix.Should().Contain("public ValueTask WaitForSettlementAsync(\n        CancellationToken cancellationToken = default);");
        var companion = Read("docs/specs/17-public-authoring-contract.cs");
        companion.Should().Contain("public string? ExceptionType => throw new NotSupportedException();");
        companion.Should().Contain("public ValueTask WaitForSettlementAsync(\n        CancellationToken cancellationToken = default) => throw new NotSupportedException();");
        var guide = Read("docs/ephemeral-engine-developer-guide.md");
        guide.Should().Contain("## Issue #16 resource-release recipe").And.Contain("The issue #16 checkpoint implements this recipe.");
        guide.Should().Contain("ExceptionDispatchInfo.Capture(error.InnerException).Throw()");
        guide.Should().Contain("A start that fails/cancels before Accepted supplies no handle or receipt");
        Read(Prefix + "tasks.md").Should().Contain("2.1/2.2 remain open until transition approval and checkpoint");
    }

    private static string Git(string root, string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start Git.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
}

// Historical approval tests validate their original layer. Before returning that
// layer, this adapter proves the current file differs only by the twelve pinned
// issue-16 operations. It is not a general current-document exemption.
internal static class EphemeralFailureSettlementReviewHistory
{
    private const string RegistryPath = "openspec/changes/expose-ephemeral-failures-and-settlement/artifacts/task-2-1-amendment-registry-2026-11-01.json";
    private const string RegistrySha256 = "37330c37abfb606af69fef227172dd1bc7a76220ca22173ca6d576080c2836aa";
    private const string BeforeCommit = "7697c7981c02d03408cbccf9a2e0b9bade33ca14";

    private static JsonDocument ReadRegistry(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, RegistryPath));
        Hash(bytes).Should().Be(RegistrySha256, "the transition registry is source-pinned");
        return JsonDocument.Parse(bytes);
    }

    internal static string CanonicalBefore(string root, string path)
    {
        using var registry = ReadRegistry(root);
        var record = registry.RootElement;
        var pin = record.GetProperty("canonicalFiles").EnumerateArray()
            .Single(row => row.GetProperty("path").GetString() == path);
        var before = Normalize(Git(root, "show", BeforeCommit + ":" + path));
        Hash(before).Should().Be(pin.GetProperty("beforeSha256").GetString());
        var expected = before;
        var capability = path.Split('/')[2];
        var deltaPath = "openspec/changes/expose-ephemeral-failures-and-settlement/specs/" + capability + "/spec.md";
        foreach (var operation in record.GetProperty("synchronizedOperations").EnumerateArray()
                     .Where(row => row.GetProperty("capability").GetString() == capability))
        {
            var title = operation.GetProperty("requirement").GetString()!;
            var block = Block(Normalize(File.ReadAllText(Path.Combine(root, deltaPath))), title);
            Hash(block).Should().Be(operation.GetProperty("blockSha256").GetString());
            if (operation.GetProperty("operation").GetString() == "MODIFIED")
            {
                var previous = Block(expected, title);
                expected.Split(previous).Should().HaveCount(2);
                expected = expected.Replace(previous, block, StringComparison.Ordinal);
            }
            else
            {
                operation.GetProperty("operation").GetString().Should().Be("ADDED");
                expected.Should().NotContain("### Requirement: " + title + "\n");
                expected = expected.TrimEnd('\n') + "\n\n" + block + "\n";
            }
        }
        var current = Normalize(File.ReadAllText(Path.Combine(root, path)));
        current.Should().Be(expected, "unowned text, order and preambles cannot change through the historical adapter");
        Hash(current).Should().Be(pin.GetProperty("afterSha256").GetString());
        return before;
    }

    internal static string CurrentDocumentationHash(string root, string path, string historicalHash)
    {
        using var registry = ReadRegistry(root);
        var rows = registry.RootElement.GetProperty("normativeDocumentationSuccessors").EnumerateArray()
            .Where(row => row.GetProperty("path").GetString() == path).ToArray();
        if (rows.Length == 0) return historicalHash;
        rows.Should().ContainSingle();
        var row = rows[0];
        var before = Normalize(Git(root, "show", BeforeCommit + ":" + path));
        Hash(before).Should().Be(historicalHash, "the original documentation approval pin remains intact");
        historicalHash.Should().Be(row.GetProperty("beforeSha256").GetString());
        return EphemeralFailureSettlementSourceReview.PublicationDocumentationHash(
            root, path, row.GetProperty("afterSha256").GetString()!);
    }

    private static string Block(string text, string title)
    {
        var match = Regex.Match(text,
            "(?ms)^### Requirement: " + Regex.Escape(title) + @"\n.*?(?=^### Requirement: |^## |\z)",
            RegexOptions.CultureInvariant);
        match.Success.Should().BeTrue("the exact named requirement must exist");
        return match.Value.TrimEnd('\n');
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);
    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot read historical Git evidence.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
}
