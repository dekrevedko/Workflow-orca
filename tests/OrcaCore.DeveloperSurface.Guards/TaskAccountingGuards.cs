using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed partial class TaskAccountingGuards
{
    [Fact]
    public void ChangeTaskLedger_CountsLetterSuffixedIdsAndHasNoDuplicates()
    {
        var path = Path.Combine(
            FixtureDefinitions.RepositoryRoot(),
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "tasks.md");
        var checkboxLines = File.ReadLines(path)
            .Where(line => CheckboxLine().IsMatch(line))
            .ToArray();
        var entries = checkboxLines
            .Select(line => TaskLine().Match(line))
            .ToArray();

        entries.Should().OnlyContain(match => match.Success,
            "every task checkbox must begin with a parseable task ID, including letter-suffixed IDs");
        var taskIds = entries.Select(match => match.Groups["id"].Value).ToArray();
        taskIds.Should().OnlyHaveUniqueItems();
        taskIds.Should().Contain(["3.11a", "3.11b", "3.11c", "3.11d"]);

        var completed = entries.Count(match =>
            string.Equals(match.Groups["state"].Value, "x", StringComparison.OrdinalIgnoreCase));
        var pending = entries.Length - completed;

        entries.Should().HaveCount(159);
        completed.Should().Be(132);
        pending.Should().Be(27);
    }

    [Fact]
    public void Task720CompletionNote_MatchesTheExecutableCrosswalkAccounting()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.DeveloperSurface.Guards",
            "Fixtures",
            "section-07-r-declaration-crosswalk.json")));
        var accounting = fixture.RootElement.GetProperty("accounting");
        var physicalFiles = accounting.GetProperty("physicalFiles").GetInt32();
        var physicalDeclarations = accounting.GetProperty("physicalDeclarations").GetInt32();
        var taskLine = File.ReadLines(Path.Combine(
                root,
                "openspec",
                "changes",
                "reshape-developer-facing-interfaces",
                "tasks.md"))
            .Single(line => line.StartsWith("- [x] 7.20 ", StringComparison.Ordinal));

        taskLine.Should().Contain(
            $"{physicalFiles.ToString("N0", CultureInfo.InvariantCulture)} sources / " +
            $"{physicalDeclarations.ToString("N0", CultureInfo.InvariantCulture)} declarations");
        taskLine.Should().Contain(
            "harmonization task 7.2, harmonization task 7.3, harmonization task 7.4, " +
            "and harmonization task 7.5, and the pending DAG Task 8.2 authoring-guard slice at",
            "the maintained inventory provenance must name the task that produced the current counts");
    }

    [GeneratedRegex(@"^\s*-\s+\[[ xX]\]\s+", RegexOptions.CultureInvariant)]
    private static partial Regex CheckboxLine();

    [GeneratedRegex(
        @"^\s*-\s+\[(?<state>[ xX])\]\s+(?<id>\d+\.\d+[a-z]?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex TaskLine();
}
