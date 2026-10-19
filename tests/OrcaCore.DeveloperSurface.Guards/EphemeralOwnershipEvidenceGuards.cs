using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class EphemeralOwnershipEvidenceGuards
{
    [Fact]
    public void ApprovedRedBaselineAndCarriedVerdict_RemainByteExactWithoutAnUnreachableGitTree()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var artifacts = Path.Combine(OpenSpecCorpusGuards.ResolveChangeRecord(root,
            "add-ephemeral-terminal-retention"), "artifacts");
        var archive = File.ReadAllBytes(Path.Combine(artifacts, "task-3-1-approved-baseline-2026-10-17.zip"));
        Hash(archive).Should().Be("4308f221951c69d6afb1304495da68691677226cd74ef82252000141f3b45fc5");
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(artifacts,
            "task-3-1-approved-baseline-inventory-2026-10-17.json")));
        var data = inventory.RootElement;
        data.GetProperty("approvedTree").GetString().Should().Be("aed8ba3fea6a66e59f0a1cc92e90291385ac5333");
        data.GetProperty("base").GetString().Should().Be("ef88957af4c41bf18def689ba81c71e1a1d18696");
        data.GetProperty("redCheckpointForbidden").GetBoolean().Should().BeTrue();
        data.GetProperty("archiveSha256").GetString().Should().Be(Hash(archive));
        data.GetProperty("archiveBytes").GetInt32().Should().Be(archive.Length);
        data.GetProperty("nextAuthorizedTask").GetString().Should().Be("3.2");
        data.GetProperty("contentRecordSha256").GetString().Should().Be(
            "7dcc22e7488e12f647ec948475da8825058d21665789844d65165944da2a2aa6");
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        zip.Entries.Should().HaveCount(14);
        var rows = data.GetProperty("files").EnumerateArray().ToArray();
        rows.Should().HaveCount(14);
        var record = new List<string>();
        foreach (var row in rows)
        {
            var path = row.GetProperty("path").GetString()!;
            var entry = zip.GetEntry(path);
            entry.Should().NotBeNull();
            using var stream = entry!.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            var bytes = copy.ToArray();
            bytes.Length.Should().Be(row.GetProperty("bytes").GetInt32());
            Hash(bytes).Should().Be(row.GetProperty("sha256").GetString());
            record.Add($"{row.GetProperty("status").GetString()}\t{path}\t{bytes.Length}\t{Hash(bytes)}");
        }
        var content = Encoding.UTF8.GetBytes(string.Join('\n', record.Order(StringComparer.Ordinal)) + "\n");
        content.Length.Should().Be(2158);
        Hash(content).Should().Be("7dcc22e7488e12f647ec948475da8825058d21665789844d65165944da2a2aa6");
        var verdict = File.ReadAllBytes(Path.Combine(artifacts,
            "task-3-1-ownership-independent-review-verdict-2026-10-17.md"));
        verdict.Length.Should().Be(11951);
        Hash(verdict).Should().Be("d10c172dc8bae4b7ae2a3d63d0d1d39f812d0fb1b4639b4490d69a9e62d452a7");
        data.GetProperty("approval").GetProperty("sha256").GetString().Should().Be(Hash(verdict));
        data.GetProperty("approval").GetProperty("bytes").GetInt32().Should().Be(verdict.Length);
        data.GetProperty("approval").GetProperty("path").GetString().Should().Be(
            "openspec/changes/add-ephemeral-terminal-retention/artifacts/task-3-1-ownership-independent-review-verdict-2026-10-17.md");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
