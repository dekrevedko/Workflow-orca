using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using OrcaCore.Hosting;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
[Collection(CompileFixtureCollection.Name)]
public sealed class EphemeralShutdownSurfaceGuards
{
    [Fact]
    public void SevenShutdownDeclarations_HaveTheExactOwnerConstructionAndClosedResultShape()
    {
        Type[] types = [typeof(IEphemeralWorkflowHostLifetime), typeof(EphemeralHostShutdownResult),
            typeof(EphemeralHostShutdownResult.Completed), typeof(EphemeralHostShutdownResult.Incomplete),
            typeof(EphemeralHostShutdownIncompleteReason), typeof(EphemeralHostStoppingException), typeof(EphemeralHostShutdownIncompleteException)];
        types.Should().OnlyContain(type => type.Namespace == "OrcaCore.Hosting" && type.Assembly.GetName().Name == "OrcaCore.Engine.Ephemeral");
        var root = typeof(EphemeralHostShutdownResult);
        root.IsAbstract.Should().BeTrue();
        root.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Should().ContainSingle().Which.IsFamilyAndAssembly.Should().BeTrue();
        root.GetNestedTypes(BindingFlags.Public).Select(type => type.Name).Order(StringComparer.Ordinal).Should().Equal("Completed", "Incomplete");
        foreach (var type in types.Where(type => type.IsClass && type != root))
        {
            type.IsSealed.Should().BeTrue();
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
            type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Should().ContainSingle().Which.IsAssembly.Should().BeTrue();
            type.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static)
                .Should().OnlyContain(method => method.IsSpecialName, "there are no public factories or record-generated operations");
        }
        var method = typeof(IEphemeralWorkflowHostLifetime).GetMethods().Should().ContainSingle().Subject;
        method.Name.Should().Be("StopAsync");
        method.ReturnType.Should().Be(typeof(ValueTask<EphemeralHostShutdownResult>));
        method.GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(typeof(TimeSpan), typeof(CancellationToken));
        method.GetParameters()[0].IsOptional.Should().BeFalse();
        method.GetParameters()[1].IsOptional.Should().BeTrue();
        Enum.GetNames<EphemeralHostShutdownIncompleteReason>().Should().Equal("DeadlineElapsed", "ObservationCancelled", "ShutdownFaulted");
        Enum.GetValues<EphemeralHostShutdownIncompleteReason>().Select(value => (int)value).Should().Equal(0, 1, 2);
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs/specs/17-selected-mode-capability-matrix.md"));
        matrix.Should().Contain("public EphemeralTerminalRetentionPolicy? TerminalRetention { get; init; }");
        foreach (var type in types) matrix.Should().Contain(type.Name);
    }

    [Fact]
    public async Task ExternalConsumer_CannotConstructOrSubclassRuntimeShutdownResults()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        const string relative = "tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/EphemeralShutdownConstruction/";
        var expectedLines = File.ReadAllLines(Path.Combine(root, relative + "Forbidden.cs"))
            .Select((text, index) => (text, line: index + 1)).Where(item => item.text.Contains("// DENIED:", StringComparison.Ordinal))
            .Select(item => item.line).ToArray();
        expectedLines.Should().HaveCount(5);
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "build", relative + "EphemeralShutdownConstruction.csproj", "-c", "Release", "--nologo", "-v", "quiet", "-p:BuildProjectReferences=false" })
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start shutdown construction probe");
        var outputTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try { await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var output = await outputTask + await errorTask;
        process.ExitCode.Should().NotBe(0, output);
        var errors = Regex.Matches(output, @"Forbidden\.cs\((?<line>\d+),\d+\): error (?<code>CS\d+)")
            .Select(match => (Line: int.Parse(match.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture), Code: match.Groups["code"].Value)).Distinct().ToArray();
        errors.Select(error => error.Line).Order().Should().Equal(expectedLines, output);
        errors.Should().OnlyContain(error => error.Code == "CS0122" || error.Code == "CS1729", output);
        output.Should().NotContain("error MSB").And.NotContain("error NU");
    }
}
