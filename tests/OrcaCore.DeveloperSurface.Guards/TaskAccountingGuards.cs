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
        completed.Should().Be(127);
        pending.Should().Be(32);
    }

    [GeneratedRegex(@"^\s*-\s+\[[ xX]\]\s+", RegexOptions.CultureInvariant)]
    private static partial Regex CheckboxLine();

    [GeneratedRegex(
        @"^\s*-\s+\[(?<state>[ xX])\]\s+(?<id>\d+\.\d+[a-z]?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex TaskLine();
}
