using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string SqlSupportChange = "scope-sqlserver-provider-support";
    private const string SqlSupportContract = "openspec/changes/" + SqlSupportChange + "/artifacts/contract.json";
    private const string SqlSupportStage = "Proposed";
    private const string SqlSupportHash = "9babd744d2f156a0c3154e950154039f66284d0e836d1975e3ea242997c391d2";
    private static bool SqlSupportSynchronized => SqlSupportStage != "Proposed";

    private static JsonElement ReadSqlSupportContract(string root)
    {
        var text = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, SqlSupportContract)));
        Sha256(text).Should().Be(SqlSupportHash);
        using var contract = JsonDocument.Parse(text);
        return contract.RootElement.Clone();
    }

    private static void ProjectSqlSupportRegistry(JsonObject registry)
    {
        var row = registry["sqlServerSupport"]!.AsObject();
        row.Count.Should().Be(5);
        row["change"]!.GetValue<string>().Should().Be(SqlSupportChange);
        row["contract"]!.GetValue<string>().Should().Be(SqlSupportContract);
        row["stage"]!.GetValue<string>().Should().Be(SqlSupportStage);
        row["reviewTask"]!.GetValue<string>().Should().Be("1.1");
        row["canonicalSyncTask"]!.GetValue<string>().Should().Be("1.2");
        registry.Remove("sqlServerSupport");
    }

    private static HashSet<string> ValidateSqlSupport(string root, List<DeltaRequirement> active, JsonElement contract)
    {
        var state = SqlSupportSynchronized ? CompleteTaskState : OpenTaskState;
        ValidateTaskReference(root, new(SqlSupportChange, "1.1", state));
        ValidateTaskReference(root, new(SqlSupportChange, "1.2", state));
        return ValidateAmendmentOwners(root, active, contract, SqlSupportChange, SqlSupportSynchronized, (_, text) => text);
    }

    internal static string BeforeSqlSupport(string root, string path, string current)
    {
        if (!SqlSupportSynchronized) return current;
        var contract = ReadSqlSupportContract(root);
        var row = contract.GetProperty("documents").EnumerateArray().SingleOrDefault(item => item.GetProperty("path").GetString() == path);
        if (row.ValueKind != JsonValueKind.Undefined)
            return ProjectAmendmentDocument(root, path, current, contract, row, true);
        return BeforeAmendmentCanonicalFile(root, path, current, contract, contract.GetProperty("base").GetString()!);
    }
}
