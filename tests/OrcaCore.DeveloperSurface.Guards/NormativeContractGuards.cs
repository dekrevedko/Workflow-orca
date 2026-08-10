using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using AwesomeAssertions;
using AwesomeAssertions.Execution;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed record V1Package(string Id, string Tier, string[] Dependencies);
public sealed record V1Diagnostic(string Code, string Name, string Meaning, string Severity);
public sealed record V1Failure(string Code, string Owner);
public sealed record V1Ownership(string Family, string Namespace, string Assembly);

public sealed record V1PublicContract(
    string PackageVersion,
    string PackageFeed,
    string[] AudienceTiers,
    string[] CompanionFixtures,
    string CompanionSha256,
    V1Package[] Packages,
    string[] CallerCreatedValues,
    string[] RuntimeCreatedValues,
    string[] WorkflowDiagnostics,
    string[] DagDiagnostics,
    string[] FailureCodes,
    string[] AllowedFriends,
    string[] AuthoredLocationRoots,
    string[] AuthoredLocationTokens,
    V1Diagnostic[] Diagnostics,
    V1Failure[] Failures,
    V1Ownership[] Ownership);

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed partial class NormativeContractInfrastructureGuards
{
    private static V1PublicContract Contract => FixtureDefinitions.Read<V1PublicContract>(
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json");

    [Fact]
    public void FrozenCatalog_IsCompleteUniqueAndDeclaresExactPackageInputs()
    {
        Contract.PackageVersion.Should().Be("0.0.0-phase0");
        Contract.PackageFeed.Should().Be("artifacts/phase0-packages");
        Contract.AudienceTiers.Should().Equal(Enum.GetNames<InterfaceTier>());
        Contract.CompanionFixtures.Should().Equal("kubernetes-companion");
        Contract.CompanionSha256.Should().MatchRegex("^[A-F0-9]{64}$");
        Contract.Packages.Should().HaveCount(11);
        Contract.Packages.Select(package => package.Id).Should().OnlyHaveUniqueItems();
        Contract.Packages.Select(package => package.Tier).Distinct().Should().BeEquivalentTo(
            "Application", "Internal", "Engine", "RuntimeProtocol", "ProviderAuthoring",
            "DurableHosting", "Dag", "DagHosting");
        Contract.Packages.Should().OnlyContain(package =>
            package.Id.StartsWith("OrcaCore", StringComparison.Ordinal));
        Contract.CallerCreatedValues.Should().HaveCount(12).And.OnlyHaveUniqueItems();
        Contract.RuntimeCreatedValues.Should().HaveCount(5).And.OnlyHaveUniqueItems();
        Contract.WorkflowDiagnostics.Should().HaveCount(27).And.OnlyHaveUniqueItems();
        Contract.DagDiagnostics.Should().HaveCount(7).And.OnlyHaveUniqueItems();
        Contract.FailureCodes.Should().HaveCount(26).And.OnlyHaveUniqueItems();
        Contract.AllowedFriends.Should().Equal(
            "OrcaCore->OrcaCore.Core",
            "OrcaCore->OrcaCore.Engine.Durable",
            "OrcaCore->OrcaCore.Engine.Ephemeral",
            "OrcaCore.Core->OrcaCore.Core.Tests",
            "OrcaCore.Core->OrcaCore.Engine.Durable",
            "OrcaCore.Core->OrcaCore.Engine.Ephemeral",
            "OrcaCore.Durable.Hosting->OrcaCore.Dag.Hosting",
            "OrcaCore.Durable.Hosting->OrcaCore.Hosting.Tests",
            "OrcaCore.Engine.Durable->OrcaCore.Durable.Hosting",
            "OrcaCore.Engine.Durable->OrcaCore.Engine.Durable.Tests",
            "OrcaCore.Engine.Durable->OrcaCore.ProviderCertification",
            "OrcaCore.Engine.Ephemeral->OrcaCore.Engine.Ephemeral.Tests",
            "OrcaCore.Providers.PostgreSql->OrcaCore.Providers.PostgreSql.Tests");
        Contract.Diagnostics.Select(x => x.Code).Should().Equal(
            Contract.WorkflowDiagnostics.Concat(Contract.DagDiagnostics));
        Contract.Diagnostics.Should().OnlyContain(x => x.Severity == "Error");
        Contract.Diagnostics.Select(x => x.Name).Should().OnlyHaveUniqueItems();
        Contract.Diagnostics.Select(x => x.Meaning).Should().OnlyHaveUniqueItems();
        Contract.Failures.Select(x => x.Code).Should().Equal(Contract.FailureCodes);
        Contract.Ownership.Should().HaveCount(12);
    }

    [Fact]
    public void FrozenCatalog_AgreesWithOwningMatrixRows()
    {
        var matrix = Read("docs/specs/17-selected-mode-capability-matrix.md");
        foreach (var package in Contract.Packages)
        {
            var row = matrix.Split('\n').Single(line => line.StartsWith($"| `{package.Id}` |", StringComparison.Ordinal));
            var dependencyCell = row.Split('|')[3];
            var actual = Regex.Matches(dependencyCell, "`(?<id>OrcaCore[^`]*)`")
                .Select(match => match.Groups["id"].Value)
                .ToArray();
            actual.Should().Equal(package.Dependencies, $"{package.Id} direct dependencies are row-scoped");
        }

        foreach (var value in Contract.CallerCreatedValues) matrix.Should().Contain($"class {value}");
        foreach (var value in Contract.RuntimeCreatedValues) matrix.Should().Contain($"class {value}");
        foreach (var code in Contract.WorkflowDiagnostics.Concat(Contract.DagDiagnostics).Concat(Contract.FailureCodes))
        {
            matrix.Should().Contain($"`{code}`");
        }

        matrix.Should().Contain($"`{Contract.PackageVersion}`");
        matrix.Should().Contain($"feed `{Contract.PackageFeed}`");
    }

    [Fact]
    public void CompanionBaseline_ContainsRequiredBuilderFamiliesAndNoDeferredMember()
    {
        var companion = Read("docs/specs/17-public-authoring-contract.cs");
        var required = new[]
        {
            "EphemeralWorkflowInitBuilder", "DurableWorkflowInitBuilder",
            "EphemeralWorkflowBuilder", "DurableWorkflowBuilder",
            "EphemeralNestedBuilder", "DurableNestedBuilder",
            "EphemeralBranchBuilder", "DurableBranchBuilder",
            "EphemeralItemBuilder", "DurableItemBuilder",
            "DurableLeaseWorkflowBuilder", "DurableLeaseNestedBuilder",
            "DurableLeaseBranchBuilder", "DurableLeaseItemBuilder",
            "EphemeralWorkflowParallelBranchScopeBuilder", "DurableWorkflowParallelBranchScopeBuilder",
            "EphemeralWorkflowParallelJoinBuilder", "DurableWorkflowParallelJoinBuilder",
            "EphemeralWorkflowCompletionBuilder", "DurableWorkflowCompletionBuilder",
            "EphemeralWorkflowDefinition", "DurableWorkflowDefinition", "DurableWorkflowRef"
        };
        foreach (var type in required) companion.Should().Contain($"class {type}");
        foreach (var declaration in new[]
        {
            "class EventContractVersion", "class WorkflowEventContract", "class WorkflowEventContract<TPayload>",
            "record WorkflowEventRoute", "class WorkflowInboundEvent", "class WorkflowInboundEvent<TPayload>",
            "record WorkflowEventAcceptanceResult", "record WorkflowEventAcceptanceRejection",
            "class WorkflowOutboundEvent", "class WorkflowEventDispatchFailure", "record WorkflowEventDispatchResult",
            "class EphemeralWorkflowRef", "class DurableWorkflowRef",
            "class OrcaCoreEphemeralEngineBuilder", "class OrcaCoreDurableEngineBuilder",
            "interface IWorkflowEventIngress", "interface IWorkflowEventDispatcher"
        }) companion.Should().Contain(declaration);

        foreach (var forbidden in new[] { "WaitLong(", "Yield(", "WhenFirst(", "RunChild(", "RunChildren(", "RunExternalJob(", "Saga(" })
        {
            companion.Should().NotContain(forbidden);
        }
    }

    [Fact]
    public void CompanionBaseline_HasExactReviewedNamespaceArityAndSignatures()
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            FixtureDefinitions.RepositoryRoot(), "docs/specs/17-public-authoring-contract.cs"));
        Convert.ToHexString(SHA256.HashData(bytes)).Should().Be(Contract.CompanionSha256,
            "any namespace, arity, signature, overload, or placement change requires baseline review");
    }

    [Fact]
    public void DiagnosticAndFailureCatalogs_AgreeWithExactMatrixRows()
    {
        var matrix = Read("docs/specs/17-selected-mode-capability-matrix.md");
        foreach (var diagnostic in Contract.Diagnostics)
        {
            matrix.Should().Contain($"| `{diagnostic.Code}` | `{diagnostic.Name}`: {diagnostic.Meaning} |");
        }
        foreach (var failure in Contract.Failures)
        {
            if (failure.Code.StartsWith("CHILD_", StringComparison.Ordinal))
                matrix.Should().Contain("| `CHILD_FAILED`, `CHILD_TIMED_OUT`, `CHILD_TERMINATED`, `CHILD_CANCELLED` | normalized DAG child terminal failure |");
            else
                matrix.Should().Contain($"| `{failure.Code}` | {failure.Owner} |");
        }
    }

    [Fact]
    public void OwnershipCatalog_AgreesWithExactMatrixRows()
    {
        var matrix = Read("docs/specs/17-selected-mode-capability-matrix.md");
        foreach (var owner in Contract.Ownership)
        {
            var row = matrix.Split('\n').Single(line => line.StartsWith($"| {owner.Family} |", StringComparison.Ordinal));
            row.Should().Contain($"| `{owner.Namespace}` | `{owner.Assembly}`");
        }
    }

    [Fact]
    public void AuthoredLocationGrammar_IsCanonicalAndClosed()
    {
        Contract.AuthoredLocationRoots.Should().Equal("workflow:$", "dag:$");
        Contract.AuthoredLocationTokens.Should().Equal(
            "n:dddddddd", "if:true", "if:false", "while:body", "parallel:dddddddd",
            "foreach:body", "lease:body", "dag-node:dddddddd");
        var valid = AuthoredLocationRegex();
        new[]
        {
            "workflow:$", "dag:$", "workflow:$/n:00000000/if:true/lease:body",
            "dag:$/dag-node:00000012"
        }.Should().OnlyContain(value => valid.IsMatch(value));
        new[]
        {
            "workflow:/", "workflow:$/n:1", "workflow:$/if:True", "dag:$/node:00000001",
            "workflow:$/message:localized"
        }.Should().NotContain(value => valid.IsMatch(value));
    }

    [Fact]
    public void ProductStrongValues_UseExactConstructionFamilies()
    {
        using var scope = new AssertionScope();
        foreach (var name in Contract.CallerCreatedValues)
        {
            var type = PublicSurfaceCatalog.ExportedTypes.Select(x => x.Type)
                .Where(x => x.Name == name).Should().ContainSingle($"{name} has one canonical declaration").Subject;
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
            type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == "Create")
                .Should().ContainSingle().Which.GetParameters().Select(x => x.ParameterType).Should().Equal(typeof(string));
            type.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(x => x.Name)
                .Should().NotContain(new[] { "New", "Parse", "TryParse" });
        }

        foreach (var name in Contract.RuntimeCreatedValues)
        {
            var type = PublicSurfaceCatalog.ExportedTypes.Select(x => x.Type)
                .Where(x => x.Name == name).Should().ContainSingle($"{name} has one canonical declaration").Subject;
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
            type.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(x => x.Name).Should().NotContain("Create");
            type.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(x => x.Name)
                .Should().Contain(new[] { "Parse", "TryParse" });
        }
    }

    [Fact]
    public void StepContextAndItemValues_HaveTheExactRootOwnedSurface()
    {
        var itemContext = typeof(global::OrcaCore.ForEachItemContext);
        itemContext.Assembly.GetName().Name.Should().Be("OrcaCore");
        itemContext.Namespace.Should().Be("OrcaCore");
        itemContext.IsSealed.Should().BeTrue();
        itemContext.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Should().ContainSingle().Which.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(typeof(int));
        itemContext.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().ContainSingle(property => property.Name == "Index" && property.PropertyType == typeof(int));

        var stepContext = typeof(global::OrcaCore.StepContext<>);
        stepContext.Assembly.GetName().Name.Should().Be("OrcaCore");
        stepContext.Namespace.Should().Be("OrcaCore");
        stepContext.IsSealed.Should().BeTrue();
        stepContext.GetGenericArguments().Should().ContainSingle();
        stepContext.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
        stepContext.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => property.Name)
            .Should().Equal("Execution", "ForEachItem", "ResourceLease", "ResumedEvent", "State", "TimeProvider");
        stepContext.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().OnlyContain(property => property.SetMethod == null);
        var replaceState = stepContext.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().ContainSingle(method => method.Name == "ReplaceState").Subject;
        replaceState.ReturnType.Should().Be(typeof(void));
        replaceState.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(stepContext.GetGenericArguments()[0]);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(
        FixtureDefinitions.RepositoryRoot(), path.Replace('/', Path.DirectorySeparatorChar)));

    [GeneratedRegex("^(?:workflow|dag):\\$(?:/(?:n:[0-9]{8}|if:(?:true|false)|while:body|parallel:[0-9]{8}|foreach:body|lease:body|dag-node:[0-9]{8}))*$", RegexOptions.CultureInvariant)]
    private static partial Regex AuthoredLocationRegex();
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class NormativeContractProductGuards
{
    private static V1PublicContract Contract => FixtureDefinitions.Read<V1PublicContract>(
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json");

    [Fact]
    public void ProductAssemblies_MatchExactV1Manifest()
    {
        var productProjects = Directory.GetFiles(
                Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        productProjects.Should().Equal(Contract.Packages.Select(package => package.Id).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void ProductProjects_DeclareExactPackageIdAssemblyNameAndRowScopedEdges()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        foreach (var package in Contract.Packages)
        {
            var projects = Directory.GetFiles(Path.Combine(root, "src"), $"{package.Id}.csproj", SearchOption.AllDirectories);
            projects.Should().ContainSingle($"{package.Id} must have exactly one owning project");
            var project = System.Xml.Linq.XDocument.Load(projects.Single());
            Value(project, "PackageId", package.Id).Should().Be(package.Id);
            Value(project, "AssemblyName", package.Id).Should().Be(package.Id);
            var references = project.Descendants("ProjectReference")
                .Select(x => Path.GetFileNameWithoutExtension(x.Attribute("Include")!.Value))
                .ToArray();
            references.Should().Equal(package.Dependencies);
        }
    }

    [Fact]
    public void ProductFriendAssemblies_AreExactlyApproved()
    {
        var actual = PublicSurfaceCatalog.Assemblies
            .SelectMany(assembly => assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => $"{assembly.GetName().Name}->{attribute.AssemblyName.Split(',')[0]}"))
            .OrderBy(edge => edge, StringComparer.Ordinal)
            .ToArray();
        actual.Should().Equal(Contract.AllowedFriends.OrderBy(edge => edge, StringComparer.Ordinal));
    }

    [Fact]
    public void ProductPublicSignatures_HaveNoForbiddenRecursiveTierEdges()
    {
        PublicSurfaceCatalog.Assemblies.Select(x => x.GetName().Name).Should().BeEquivalentTo(
            PublicSurfaceCatalog.TargetAssemblyNames,
            "cross-tier certification must inspect every exact future assembly, not a legacy subset");
        PublicSurfaceCatalog.FindForbiddenSignatureEdges().Should().BeEmpty();
    }

    [Fact]
    public void ProductBuiltInFailures_HaveOneCanonicalDeclarationInTheirNormativeOwner()
    {
        var expectedTypes = Contract.Failures
            .Select(x => Regex.Match(x.Owner, "^`(?<type>[^`]+)`$").Groups["type"].Value)
            .Where(x => x.Length > 0)
            .Concat(new[] { "OrcaCoreException", "WorkflowDiagnostic", "Validation`1", "WorkflowDefinitionException" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var exported = PublicSurfaceCatalog.ExportedTypes.Select(x => x.Type).ToArray();
        var workflowOwner = Contract.Ownership.Single(x =>
            x.Family.StartsWith("Workflow strong values", StringComparison.Ordinal));
        var dagOwner = Contract.Ownership.Single(x =>
            x.Family.StartsWith("`DagNodeId`", StringComparison.Ordinal));
        using var scope = new AssertionScope();
        foreach (var name in expectedTypes)
        {
            var matches = exported.Where(x => x.Name == name).ToArray();
            matches.Should().ContainSingle($"{name} owns exactly one fixed failure code");
            if (matches.Length == 1)
            {
                var failure = Contract.Failures.SingleOrDefault(x =>
                    Regex.Match(x.Owner, "^`(?<type>[^`]+)`$").Groups["type"].Value == name);
                var expectedOwner = failure?.Code.StartsWith("DAG-", StringComparison.Ordinal) == true
                    ? dagOwner
                    : workflowOwner;
                matches[0].Namespace.Should().Be(expectedOwner.Namespace);
                matches[0].Assembly.GetName().Name.Should().Be(expectedOwner.Assembly);
            }
        }
    }

    [Fact]
    public void EveryExternallyVisibleProductException_UsesTheStableCodedFailureBase()
    {
        PublicSurfaceCatalog.ExportedTypes
            .Select(entry => entry.Type)
            .Where(type => type != typeof(global::OrcaCore.OrcaCoreException) &&
                           typeof(Exception).IsAssignableFrom(type))
            .Should().OnlyContain(type => typeof(global::OrcaCore.OrcaCoreException).IsAssignableFrom(type),
                "every public product failure must expose the stable OrcaCoreException.Code contract");
    }

    private static string Value(System.Xml.Linq.XDocument project, string name, string fallback) =>
        project.Descendants(name).Select(x => x.Value).SingleOrDefault() ?? fallback;
}
