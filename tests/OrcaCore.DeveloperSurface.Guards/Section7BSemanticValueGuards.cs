using AwesomeAssertions;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class Section7BSemanticValueGuards
{
    private static readonly string[] RouteKinds =
    [
        InboxRouteKinds.Direct,
        InboxRouteKinds.Correlation,
        InboxRouteKinds.DefinitionFanout,
        InboxRouteKinds.DefinitionFanoutTarget,
        InboxRouteKinds.StartOrDeliver
    ];

    private static readonly string[] PoisonCodes =
    [
        "definition-binding-unavailable",
        "inbox-continuation-failed",
        "inbox-envelope-missing",
        "start-intent-unresolvable",
        "direct-target-missing",
        "target-terminal",
        "ambiguous-active-wait",
        "fanout-target-missing",
        "fanout-target-terminal",
        "start-target-missing",
        "start-target-terminal",
        "start-intent-invalid",
        "start-definition-version-unavailable",
        "start-binding-incompatible"
    ];

    private static readonly string[] OtherSemanticRepresentations =
    [
        OutboxKinds.Continue,
        OutboxKinds.WorkflowEvent,
        "workflow-event-materialization-failed",
        "pending-definition:"
    ];

    [Fact]
    public void Section7BSemanticValues_HaveOneNamedProductOwnerAndPermanentPolicy()
    {
        var productText = ProductSourceText();
        foreach (var routeKind in RouteKinds)
        {
            CountRepresentations(productText, routeKind).Should().Be(1,
                $"route discriminator '{routeKind}' is declared once by InboxRouteKinds");
        }

        foreach (var poisonCode in PoisonCodes)
        {
            CountRepresentations(productText, poisonCode).Should().Be(1,
                $"poison code '{poisonCode}' is declared once by DurableInboxPoisonCodes");
        }

        foreach (var representation in OtherSemanticRepresentations)
        {
            CountRepresentations(productText, representation).Should().Be(1,
                $"Section 7B semantic representation '{representation}' has one named product owner");
        }

        foreach (var storagePrefix in new[]
                 {
                     "direct|", "correlation|", "definition-fanout-target|", "direct-target|"
                 })
        {
            CountRepresentations(productText, storagePrefix).Should().Be(1,
                $"storage prefix '{storagePrefix}' has one provider-owned constant");
        }

        var conventions = File.ReadAllText(Path.Combine(
            FixtureDefinitions.RepositoryRoot(),
            "docs",
            "implementation",
            "02-engineering-conventions.md"));
        conventions.Should().Contain("exactly one named owner");
        conventions.Should().Contain("Intrinsic language and algorithmic values remain local");
    }

    [Fact]
    public void Section7BHardening_RemainsBoundedTypedAndStructurallyConsolidated()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var postgreSqlStore = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OrcaCore.Providers.PostgreSql",
            "PostgreSqlWorkflowStore.cs"));
        postgreSqlStore.Should().Contain("attempt >= options.StartIntentConflictRetryLimit");
        postgreSqlStore.Should().Contain("AcceptStartOrDeliverCoreAsync(request, attempt + 1");
        postgreSqlStore.Should().NotContain("return await AcceptStartOrDeliverAsync(request");
        postgreSqlStore.Should().Contain("ResolveInboxIdentityAsync(recordIdentity");

        var postgreSqlOptions = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OrcaCore.Providers.PostgreSql",
            "PostgreSqlWorkflowStoreOptions.cs"));
        postgreSqlOptions.Should().Contain("DefaultStartIntentConflictRetryLimit = 3");

        var eventIngress = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OrcaCore.Engine.Durable",
            "Facade",
            "DurableWorkflowEventIngressCore.cs"));
        eventIngress.Should().Contain("MaximumDefinitionFanoutTargets = 1024");

        var inMemoryStore = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OrcaCore.Providers.InMemory",
            "InMemoryWorkflowProvider.cs"));
        inMemoryStore.Should().Contain("ResolveInboxIdentityKind(recordIdentity)");

        var durableSource = string.Join('\n', Directory.EnumerateFiles(
                Path.Combine(root, "src", "OrcaCore.Engine.Durable"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(IsProductSource)
            .Select(File.ReadAllText));
        durableSource.Should().NotContain("DurableWorkflowValueFingerprint");
        durableSource.Should().Contain("DurableWorkflowInputFingerprint.Create(value)");
        durableSource.Should().Contain("DurableWorkflowInputFingerprint.Create(inputPayload)");
        durableSource.Should().Contain("catch (NotSupportedException)");
    }

    private static string ProductSourceText()
    {
        var root = Path.Combine(FixtureDefinitions.RepositoryRoot(), "src");
        return string.Join('\n', Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".sql")
            .Where(IsProductSource)
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }

    private static bool IsProductSource(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) &&
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static int CountRepresentations(string source, string value) =>
        Count(source, $"\"{value}\"") + Count(source, $"'{value}'");

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
        {
            count++;
        }

        return count;
    }
}
