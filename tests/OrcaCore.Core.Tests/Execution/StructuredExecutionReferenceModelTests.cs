using AwesomeAssertions;
using OrcaCore.TestSupport.StructuredExecution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class StructuredExecutionReferenceModelTests
{
    [Fact]
    public void GeneratedSchedules_MatchReferenceModelAcrossSeededCrashPermutations()
    {
        var observed = new HashSet<StructuredExecutionOperationKind>();
        foreach (var seed in new[] { 17, 31, 53, 97 })
        {
            var scenario = StructuredExecutionScenarioGenerator.Generate(
                seed,
                authoredFiberCount: 5,
                operationCount: 80);

            scenario.Operations.Should().Contain(operation =>
                operation.Kind == StructuredExecutionOperationKind.CrashReload);
            observed.UnionWith(scenario.Operations.Select(operation => operation.Kind));
            var comparison = StructuredExecutionComparisonHarness.Compare(scenario);

            comparison.Mismatches.Should().BeEmpty($"seed {seed} must be reproducible");
        }

        observed.Should().Contain([
            StructuredExecutionOperationKind.Yield,
            StructuredExecutionOperationKind.ResumeTogether,
            StructuredExecutionOperationKind.Complete,
            StructuredExecutionOperationKind.DuplicateDelivery,
            StructuredExecutionOperationKind.CrashReload]);
    }

    [Fact]
    public void GeneratedTerminalSchedules_MatchFailuresCancellationAndDuplicateDeliveries()
    {
        foreach (var terminal in new[]
                 {
                     StructuredExecutionOperationKind.Fail,
                     StructuredExecutionOperationKind.Cancel
                 })
        {
            var scenario = StructuredExecutionScenarioGenerator.GenerateTerminal(
                seed: 211 + (int)terminal,
                authoredFiberCount: 5,
                terminal);

            scenario.Operations.Should().Contain(operation => operation.Kind == terminal);
            scenario.Operations.Should().Contain(operation =>
                operation.Kind == StructuredExecutionOperationKind.DuplicateDelivery);
            scenario.Operations.Should().Contain(operation =>
                operation.Kind == StructuredExecutionOperationKind.CrashReload);
            StructuredExecutionComparisonHarness.Compare(scenario).Mismatches.Should().BeEmpty();
        }
    }

    [Fact]
    public void GeneratedNestedAndDynamicForEachScopes_MatchAcrossCompletionOrdersAndCrashes()
    {
        var scenarios = new[]
        {
            StructuredScopeScenarioGenerator.GenerateNested(seed: 307, branchCount: 4),
            StructuredScopeScenarioGenerator.GenerateForEach(
                seed: 401,
                itemCount: 9,
                maxConcurrency: 3)
        };

        foreach (var scenario in scenarios)
        {
            var comparison = StructuredExecutionComparisonHarness.Compare(scenario);

            comparison.Mismatches.Should().BeEmpty($"seed {scenario.Seed} must be reproducible");
        }
    }
}
