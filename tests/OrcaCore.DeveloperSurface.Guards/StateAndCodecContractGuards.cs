using AwesomeAssertions;
using System.Reflection;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class StateAndCodecInfrastructureGuards
{
    [Fact]
    public void ScenarioLedger_CoversEveryConstructionCodecStateAndProjectionConcern()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/state-and-codec-scenarios.json");
        scenarios.Select(x => x.Id).Should().BeEquivalentTo(
            "caller-created-text-values", "runtime-created-identities", "definition-id-nonempty",
            "fixed-codec-determinism", "structural-fingerprint-opacity", "attempt-local-replace-state",
            "readonly-snapshot-shape", "typed-completion-output", "projection-opacity");
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.5" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsTheExactCodecFingerprintAttemptAndProjectionRules()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var required in new[]
        {
            "orcacore-json-v1", "ReplaceState", "ReadOnlyStateSnapshot<TState>", "structural",
            "opaque code", "private branch/item or in-flight attempt copies", "Guid.Empty",
            "private constructor", "Create(string)"
        }) matrix.Should().Contain(required);
        matrix.Should().NotContain("replaceable codec");
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class StateAndCodecGreenGuards
{
    [Fact]
    public void Product_ContainsTheFixedCodecAndRuntimeCreatedSnapshotContract()
    {
        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(x => x.GetExportedTypes()).ToArray();
        exported.Should().Contain(x => x.FullName == "OrcaCore.ReadOnlyStateSnapshot`1");
        exported.Select(x => x.FullName).Should().NotContain(new[]
        {
            "OrcaCore.Abstractions.Providers.IWorkflowPayloadSerializer",
            "OrcaCore.Abstractions.Providers.IWorkflowPayloadCodec",
            "OrcaCore.Engine.Durable.Execution.JsonWorkflowPayloadSerializer",
            "OrcaCore.Engine.Durable.Execution.ContentTypeWorkflowPayloadSerializer",
            "OrcaCore.Hosting.WorkflowPayloadSerializationOptions",
            "OrcaCore.Engine.Ephemeral.IEphemeralStateSnapshotter",
            "OrcaCore.Engine.Ephemeral.SystemTextJsonEphemeralStateSnapshotter"
        });
        exported.Should().NotContain(type =>
            type.FullName == "OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngineOptions");
        var ephemeralOptions = Assembly.Load("OrcaCore.Engine.Ephemeral")
            .GetType("OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngineOptions", throwOnError: true)!;
        ephemeralOptions.IsNotPublic.Should().BeTrue();
        ephemeralOptions.GetProperty(
                "StateSnapshotter",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Should().BeNull("the fixed codec has no ordinary application replacement hook");
        var durableRuntime = Assembly.Load("OrcaCore.Engine.Durable")
            .GetType("OrcaCore.Engine.Durable.Execution.DurableWorkflowRuntime", throwOnError: true)!;
        durableRuntime.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Should().NotContain(parameter =>
                parameter.ParameterType.Name.Contains(
                    "WorkflowPayloadSerializer",
                    StringComparison.Ordinal));
        Directory.GetFiles(Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines).Should().Contain(line => line.Contains("orcacore-json-v1", StringComparison.Ordinal));
    }

    [Fact]
    public void Product_HasOneFixedCodecAndTheTypeRegistryCannotChoosePayloadBytes()
    {
        var coreAssembly = Assembly.Load("OrcaCore.Core");
        var applicationAssembly = typeof(global::OrcaCore.InstanceId).Assembly;
        var registry = coreAssembly.GetType(
            "OrcaCore.Core.Compilation.IWorkflowTypeSerializerRegistry",
            throwOnError: true)!;
        registry.GetMethods()
            .Select(method => method.Name)
            .Should().Equal("TryGetSchemaIdentity");

        var codec = applicationAssembly.GetType(
            "OrcaCore.Internal.FixedWorkflowValueCodec",
            throwOnError: true)!;
        var plan = coreAssembly.GetType(
            "OrcaCore.Core.Compilation.CompiledWorkflowPlan",
            throwOnError: true)!;
        var codecFormat = codec.GetField(
            "Format",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue();
        var planFormat = plan.GetField(
            "CodecFormat",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue();

        codecFormat.Should().Be("orcacore-json-v1");
        planFormat.Should().Be(codecFormat, "plan identity must bind the one fixed value codec");
    }

    [Fact]
    public void Product_RawJsonCallSitesAreLimitedToTheFixedCodecAndApprovedFramingBoundaries()
    {
        var sourceRoot = Path.Combine(FixtureDefinitions.RepositoryRoot(), "src");
        var expectedCallCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["OrcaCore.Abstractions/Serialization/FixedWorkflowValueCodec.cs"] = 2,
            ["OrcaCore.Abstractions/Serialization/WorkflowFailureJsonConverters.cs"] = 6,
            ["OrcaCore.Runtime.Protocol/Durable/DurableContinuationSignal.cs"] = 2,
            ["OrcaCore.Runtime.Protocol/Durable/DurableExecutionEnvelopeV2.cs"] = 2,
            ["OrcaCore.Runtime.Protocol/ResourceGovernanceRecordJsonConverter.cs"] = 2,
            ["OrcaCore.Runtime.Protocol/Serialization/WorkflowEventCodec.cs"] = 2,
            ["OrcaCore.Provider.Abstractions/InboxRouteKeyJsonConverter.cs"] = 2,
            ["OrcaCore.Engine.Durable/Outbox/DurableWorkflowOutboundEventCodec.cs"] = 2,
            ["OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs"] = 2,
            ["OrcaCore.Providers.PostgreSql/PostgreSqlResourcePoolStore.cs"] = 4,
            ["OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs"] = 2,
            ["OrcaCore.Providers.SqlServer/Internal/SqlServerResourceGovernanceStore.cs"] = 2,
            ["OrcaCore.Providers.SqlServer/Internal/SqlServerResourcePoolStore.cs"] = 2,
            ["OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStore.cs"] = 2
        };
        var rawJsonCall = new System.Text.RegularExpressions.Regex(
            @"JsonSerializer\.(?:Serialize|SerializeToUtf8Bytes|Deserialize)(?:<[^>]+>)?\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        var productRoots = Directory
            .GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actualCallCounts = productRoots
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path)
                .Where(line => rawJsonCall.IsMatch(line))
                .Select(_ => Path.GetRelativePath(sourceRoot, path).Replace('\\', '/')))
            .GroupBy(path => path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        actualCallCounts.Should().BeEquivalentTo(
            expectedCallCounts,
            "a new byte-producing call site requires explicit review as framing/provider persistence, " +
            "and workflow values must use FixedWorkflowValueCodec");
    }

}
