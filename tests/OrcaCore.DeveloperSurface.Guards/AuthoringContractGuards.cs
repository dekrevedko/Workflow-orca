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
        fixture.Should().Contain("<PackageReference Include=\"OrcaCore\"");
        fixture.Should().Contain("../ExactAuthoring/PositiveUsage.cs");
        fixture.Should().Contain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        fixture.Should().Contain("<RestorePackagesPath>$(MSBuildThisFileDirectory)obj\\package-cache\\$(Phase0PackageVersion)</RestorePackagesPath>");
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
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class AuthoringContractExpectedRedGuards
{
    [Fact]
    public void Product_CompilesEveryPositiveSignatureAndRejectsEveryForbiddenMember()
    {
        var positive = Build("ProductAuthoring/ProductAuthoring.csproj");
        positive.ExitCode.Should().Be(0,
            "the package consumer invokes every staged family, all root/nested/branch/item/leased members, every join, all four End overloads, Build/TryBuild, and definition/reference metadata; output: {0}",
            positive.Output);

        var forbidden = Build("ProductForbiddenAuthoring/ProductForbiddenAuthoring.csproj");
        forbidden.ExitCode.Should().NotBe(0, "forbidden capabilities must not compile");
        Regex.Matches(forbidden.Output, @"Forbidden\.cs\((\d+),(\d+)\): error CS1061")
            .Select(x => x.Groups[1].Value + ":" + x.Groups[2].Value).Distinct().Should().HaveCount(26,
            "all 26 forbidden mode/location/root-only/leased/deferred calls must be rejected; output: {0}",
            forbidden.Output);
    }

    private static (int ExitCode, string Output) Build(string relativeProject)
    {
        var project = Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests", "OrcaCore.DeveloperSurface.Guards",
            "CompileFixtures", relativeProject.Replace('/', Path.DirectorySeparatorChar));
        if (relativeProject is "ProductAuthoring/ProductAuthoring.csproj" or
            "ProductForbiddenAuthoring/ProductForbiddenAuthoring.csproj")
        {
            var packageCache = Path.Combine(Path.GetDirectoryName(project)!, "obj", "package-cache");
            if (Directory.Exists(packageCache)) Directory.Delete(packageCache, recursive: true);
        }
        var start = new ProcessStartInfo("dotnet", $"build \"{project}\" -c Release --nologo -v quiet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FixtureDefinitions.RepositoryRoot()
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
