using System.Diagnostics;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
[Collection(CompileFixtureCollection.Name)]
public sealed class NativeMetadataApiGuards
{
    private static readonly Lazy<Task<string>> Consumer = new(BuildConsumer);

    [Theory]
    [InlineData("registration-parity")]
    [InlineData("closed-typed-dispatch")]
    [InlineData("closed-typed-ownership")]
    [InlineData("foreach-array-missing")]
    [InlineData("graph-members-generated")]
    [InlineData("graph-members-legacy")]
    [InlineData("write-only-generated")]
    [InlineData("write-only-legacy")]
    [InlineData("write-only-default")]
    [InlineData("included-members-generated")]
    [InlineData("included-members-legacy")]
    [InlineData("included-members-default")]
    [InlineData("baked-enum-rejected")]
    [InlineData("handwritten-rejected")]
    [InlineData("configuration-failures")]
    [InlineData("authoring-seals")]
    [InlineData("pending-registration-conflict")]
    [InlineData("f1-shape-rejection")]
    [InlineData("product-contracts")]
    [InlineData("idempotence-and-provider-ownership")]
    [InlineData("legacy-success-seals")]
    [InlineData("legacy-failure-seals")]
    [InlineData("missing-metadata-seals")]
    [InlineData("first-use-wins-race")]
    [InlineData("same-context-race")]
    [InlineData("reentrant-registration")]
    [InlineData("native-missing-install")]
    [InlineData("static-bootstrap")]
    [InlineData("f1-positive-generated")]
    [InlineData("f1-positive-legacy")]
    [InlineData("f1-member-metadata")]
    [InlineData("unsupported-before-missing")]
    [InlineData("codec-negative-boundaries")]
    [InlineData("completed-constructor-context")]
    [InlineData("failure-and-size-boundaries")]
    [InlineData("failure-and-size-legacy")]
    [InlineData("explicit-empty-converters")]
    [InlineData("product-converter-options-rejected")]
    [InlineData("public-factory-no-seal")]
    public async Task MetadataContract_IsObservedInAFreshProcess(string scenario)
    {
        var dll = await Consumer.Value;
        var (exit, output) = await Run("dotnet", [dll, scenario]);
        exit.Should().Be(0, "fresh process {0} must satisfy the public metadata contract: {1}", scenario, output);
        output.Should().Contain("PASS " + scenario);
    }

    [Fact]
    public async Task GeneratedAndLegacy_ProduceTheSameGoldenBytesAndDefinitionFingerprints()
    {
        var dll = await Consumer.Value;
        var (legacyExit, legacy) = await Run("dotnet", [dll, "golden-legacy"]);
        var (generatedExit, generated) = await Run("dotnet", [dll, "golden-generated"]);
        legacyExit.Should().Be(0, legacy);
        generatedExit.Should().Be(0, generated);
        var legacyGolden = legacy.Split('\n').Single(line => line.StartsWith("GOLDEN ", StringComparison.Ordinal));
        var generatedGolden = generated.Split('\n').Single(line => line.StartsWith("GOLDEN ", StringComparison.Ordinal));
        generatedGolden.Should().Be(legacyGolden, "mode/context identity does not enter bytes or definition/input fingerprints");
    }

    [Fact]
    public async Task EveryStrongValue_PreservesRootNestedAndCollectionBytesInBothGenerationModes()
    {
        var dll = await Consumer.Value;
        string? expected = null;
        foreach (var mode in new[] { "strong-legacy", "strong-metadata", "strong-default" })
        {
            var (exit, output) = await Run("dotnet", [dll, mode]);
            exit.Should().Be(0, "complete strong-value corpus {0}: {1}", mode, output);
            var fingerprints = output.Split('\n').Single(line => line.StartsWith("STRONG ", StringComparison.Ordinal));
            if (expected is null) expected = fingerprints;
            else fingerprints.Should().Be(expected, "factory accessibility and generation mode preserve exact bytes and fingerprints");
        }
    }

    private static async Task<string> BuildConsumer()
    {
        var project = Path.Combine(FixtureDefinitions.RepositoryRoot(),
            "tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/NativeMetadata/NativeMetadata.csproj");
        var (exit, output) = await Run("dotnet", ["build", project, "-c", "Release", "--nologo", "-v", "quiet"]);
        exit.Should().Be(0, "the public consumer must build before behavior assertions: {0}", output);
        return Path.Combine(Path.GetDirectoryName(project)!, "bin/Release/net10.0/NativeMetadata.dll");
    }

    private static async Task<(int Exit, string Output)> Run(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not launch metadata consumer.");
        var token = TestContext.Current.CancellationToken;
        var output = process.StandardOutput.ReadToEndAsync(token);
        var error = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromMinutes(2), token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output + await error);
    }
}
