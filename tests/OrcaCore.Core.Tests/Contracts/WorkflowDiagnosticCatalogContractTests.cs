using System.Collections;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class WorkflowDiagnosticCatalogContractTests
{
    [Fact]
    public void WorkflowCatalog_MatchesEveryFrozenCodeNameMeaningAndSeverityExactly()
    {
        var catalogType = typeof(WorkflowDiagnostic).Assembly.GetType(
            "OrcaCore.WorkflowDiagnosticCatalog",
            throwOnError: true)!;
        var catalog = (IEnumerable)catalogType.GetProperty(
            "All",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var actual = catalog.Cast<object>().ToDictionary(
            item => (string)item.GetType().GetProperty("Key")!.GetValue(item)!,
            item => item.GetType().GetProperty("Value")!.GetValue(item)!);
        using var contract = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        var expected = contract.RootElement.GetProperty("diagnostics").EnumerateArray().ToArray();

        actual.Keys.Should().Equal(expected.Select(item => item.GetProperty("code").GetString()));
        foreach (var item in expected)
        {
            var descriptor = actual[item.GetProperty("code").GetString()!];
            descriptor.GetType().GetProperty("Name")!.GetValue(descriptor)
                .Should().Be(item.GetProperty("name").GetString());
            descriptor.GetType().GetProperty("Meaning")!.GetValue(descriptor)
                .Should().Be(item.GetProperty("meaning").GetString());
            descriptor.GetType().GetProperty("Severity")!.GetValue(descriptor)!.ToString()
                .Should().Be(item.GetProperty("severity").GetString());
        }
    }

    private static string ContractPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "tests",
                "OrcaCore.DeveloperSurface.Guards",
                "Fixtures",
                "v1-public-contract.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not locate the frozen v1 public contract.");
    }
}
