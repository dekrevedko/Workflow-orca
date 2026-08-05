using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class AuthoringContractInfrastructureGuards
{
    private static string Companion => File.ReadAllText(Path.Combine(
        FixtureDefinitions.RepositoryRoot(), "docs", "specs", "17-public-authoring-contract.cs"));

    [Fact]
    public void Companion_HasExactRootEndParallelCompletionAndLambdaShape()
    {
        foreach (var root in new[] { "EphemeralWorkflowBuilder", "DurableWorkflowBuilder" })
        {
            var block = TypeBlock(root);
            Count(block, " End(").Should().Be(2, $"{root} has exactly two resultless End overloads");
            Count(block, " End<TOutput>(").Should().Be(2, $"{root} has exactly two resultful End overloads");
            Count(block, " Parallel<TResult>(").Should().Be(1, $"{root} owns one root-only Parallel factory");
        }

        foreach (var completion in new[]
        {
            "EphemeralWorkflowCompletionBuilder<TInput>", "EphemeralWorkflowCompletionBuilder<TInput, TOutput>",
            "DurableWorkflowCompletionBuilder<TInput>", "DurableWorkflowCompletionBuilder<TInput, TOutput>"
        })
        {
            var block = TypeBlock(completion);
            Count(block, " Build()").Should().Be(1);
            Count(block, " TryBuild()").Should().Be(1);
        }

        TypeBlock("DurableWorkflowBuilder").Should().Contain(
            "public DurableWorkflowCompletionBuilder<TInput> ContinueAsNew(");
        Companion.Should().Contain("Func<StepContext<TState>, ValueTask>");
        Companion.Should().Contain("Func<StepContext<TState>, CancellationToken, ValueTask>");
        Companion.Should().NotContain("Action<StepContext<");
    }

    [Fact]
    [Trait("AC", "AC-605")]
    public void Companion_RestrictsParallelAndLeasedCapabilitiesToApprovedOwners()
    {
        var forbiddenOwners = new[]
        {
            "EphemeralNestedBuilder", "DurableNestedBuilder", "EphemeralBranchBuilder", "EphemeralItemBuilder",
            "DurableBranchBuilder", "DurableItemBuilder", "DurableLeaseWorkflowBuilder",
            "DurableLeaseNestedBuilder", "DurableLeaseBranchBuilder", "DurableLeaseItemBuilder"
        };
        foreach (var owner in forbiddenOwners)
        {
            var block = TypeBlock(owner);
            block.Should().NotContain(" Parallel");
            block.Should().NotContain("ParallelBranchScopeBuilder");
            block.Should().NotContain("ParallelJoinBuilder");
        }

        foreach (var leased in forbiddenOwners.Where(x => x.StartsWith("DurableLease", StringComparison.Ordinal)))
        {
            var block = TypeBlock(leased);
            block.Should().NotContain(" ForEach");
            block.Should().NotContain(" AcquireResources");
            block.Should().NotContain(" ContinueAsNew");
        }
    }

    [Fact]
    public void AuthoringBehaviorLedger_IsExactAndSemantic()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/authoring-behavior-scenarios.json");
        scenarios.Select(x => x.Id).Should().BeEquivalentTo(
            "trybuild-build-diagnostic-parity", "ephemeral-resultless-end-null-outcome",
            "ephemeral-resultful-end-null-selector", "durable-resultless-end-null-outcome",
            "durable-resultful-end-null-selector", "empty-parallel-build-parity", "continue-as-new-terminal",
            "duplicate-decorator-eager", "duplicate-deadline-eager", "leased-and-nested-omissions");
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.4" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void ProductPositiveFixture_ConsumesOnlyThePackedProductAndHasAnIncompletePackageMutationControl()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var fixture = File.ReadAllText(Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards",
            "CompileFixtures", "ProductAuthoring", "ProductAuthoring.csproj"));
        var positive = File.ReadAllText(Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards",
            "CompileFixtures", "ProductAuthoring", "ProductPositiveUsage.cs"));
        fixture.Should().Contain("<PackageReference Include=\"OrcaCore\"");
        fixture.Should().Contain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        fixture.Should().Contain("<RestorePackagesPath>$(MSBuildThisFileDirectory)obj\\package-cache\\$(Phase0PackageVersion)</RestorePackagesPath>");
        positive.Should().Contain("ExerciseCurrentNonMessagingAuthoring").And.Contain("DurableLeaseItemBuilder")
            .And.NotContain(".Wait(").And.NotContain(".Publish(");
        fixture.Should().NotContain("../ExactAuthoring/PositiveUsage.cs");
        fixture.Should().NotContain("Authoring.cs\"");
        fixture.Should().NotContain("SupportTypes.cs");

        var incomplete = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards",
            "CompileFixtures", "IncompleteProductPackage", "IncompleteProductPackage.csproj");
        File.Exists(incomplete).Should().BeTrue();
        File.ReadAllText(incomplete).Should().Contain("<PackageId>OrcaCore</PackageId>")
            .And.Contain("0.0.0-negativecontrol");
        File.ReadAllText(Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards",
            "run-compile-fixtures.ps1")).Should().Contain("unexpectedly compiled against the incomplete package");
    }

    [Fact]
    public void ProductSource_HasNoSupersededMixedModeBuilder()
    {
        var source = Directory.GetFiles(
                Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"),
                "*.cs",
                SearchOption.AllDirectories)
            .SelectMany(File.ReadLines)
            .ToArray();

        foreach (var forbidden in new[]
        {
            "SelectedWorkflowBuilder<", "LegacyWorkflowBuilder<", "CreateLegacyTestPlan",
            "ContainsDurableOnlyNodes", "requiresDurableEngine"
        })
        {
            source.Should().NotContain(line => line.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    private static string TypeBlock(string declaration)
    {
        var marker = declaration.Contains('<') ? $"class {declaration}" : $"class {declaration}<";
        var start = Companion.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0 && !declaration.Contains('<')) start = Companion.IndexOf($"class {declaration}\n", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"{declaration} must exist in the companion");
        var open = Companion.IndexOf('{', start);
        var depth = 0;
        for (var index = open; index < Companion.Length; index++)
        {
            if (Companion[index] == '{') depth++;
            if (Companion[index] != '}') continue;
            depth--;
            if (depth == 0) return Companion[start..(index + 1)];
        }
        throw new InvalidOperationException($"Unclosed declaration {declaration}.");
    }

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
[Collection(CompileFixtureCollection.Name)]
public sealed class ProductAuthoringGreenGuards
{
    [Fact]
    public void Product_DoesNotExportSupersededAuthoringEntryPointsOrDeferredFamilies()
    {
        var exported = PublicSurfaceCatalog.Assemblies
            .Where(assembly => assembly.GetName().Name == "OrcaCore.Core")
            .SelectMany(assembly => assembly.GetExportedTypes())
            .ToArray();
        var forbidden = new[]
        {
            "OrcaCore.Core.Building.Workflow",
            "OrcaCore.Core.Building.WorkflowBuilder`1",
            "OrcaCore.Core.Building.SelectedWorkflowBuilder`2",
            "OrcaCore.Core.Building.EphemeralWorkflowBuilder`1",
            "OrcaCore.Core.Building.DurableWorkflowBuilder`1",
            "OrcaCore.Core.Building.BranchBuilder`2",
            "OrcaCore.Core.Building.SagaBuilder`1",
            "OrcaCore.Core.Building.WorkflowDagBuilder",
            "OrcaCore.Core.Definitions.SagaDefinition`1"
        };

        exported.Select(type => type.FullName).Should().NotContain(forbidden);
        exported.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.Name)
            .Should().NotContain(new[]
            {
                "WhenFirst", "WaitLong", "Yield", "RunChild", "RunChildren", "RunExternalJob"
            });
        Assembly.Load("OrcaCore.Core")
            .GetType("OrcaCore.Core.Definitions.WorkflowDefinition`1", throwOnError: true)!
            .GetProperty("RequiresDurableEngine")
            .Should().BeNull();
        Directory.GetFiles(
                Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"),
                "*.cs",
                SearchOption.AllDirectories)
            .SelectMany(File.ReadLines)
            .Should().NotContain(line => line.Contains("FromLegacy", StringComparison.Ordinal));

        Assembly.Load("OrcaCore.Engine.Durable")
            .GetType("OrcaCore.Engine.Durable.Management.DurableManagement", throwOnError: false)
            .Should().BeNull("the superseded management surface must remain absent");
    }

    [Fact]
    public void Product_CompilesEveryPositiveSignatureAndRejectsEveryForbiddenMember()
    {
        var script = Path.Combine(
            FixtureDefinitions.RepositoryRoot(),
            "tests",
            "OrcaCore.DeveloperSurface.Guards",
            "run-compile-fixtures.ps1");
        var start = new ProcessStartInfo(
            "powershell",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Disposition Green")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FixtureDefinitions.RepositoryRoot()
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.Should().Be(0,
            "the self-contained package lane must freshly pack current source, compile every positive signature, reject all forbidden members, and prove its mutation control; output: {0}",
            output);
    }
}
