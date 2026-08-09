using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class FacadeHostingInfrastructureGuards
{
    private static readonly string[] WaitBuilders =
    [
        "EphemeralWorkflowBuilder", "DurableWorkflowBuilder",
        "EphemeralNestedBuilder", "DurableNestedBuilder",
        "EphemeralBranchBuilder", "DurableBranchBuilder",
        "EphemeralItemBuilder", "DurableItemBuilder",
        "DurableLeaseWorkflowBuilder", "DurableLeaseNestedBuilder",
        "DurableLeaseBranchBuilder", "DurableLeaseItemBuilder"
    ];

    private static readonly string[] PublishBuilders =
    [
        "DurableWorkflowBuilder", "DurableNestedBuilder", "DurableBranchBuilder", "DurableItemBuilder",
        "DurableLeaseWorkflowBuilder", "DurableLeaseNestedBuilder",
        "DurableLeaseBranchBuilder", "DurableLeaseItemBuilder"
    ];

    [Fact]
    public void ScenarioLedger_CoversTheCompleteSection7BGuardTarget()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/section-07b-surface-scenarios.json");

        scenarios.Select(x => x.Id).Should().Equal(
            "versioned-event-descriptors", "resumed-event-envelope", "descriptor-wait-placement", "self-routing-route-union",
            "inbound-event-envelopes", "acceptance-and-rejection-unions", "durable-event-ingress",
            "durable-publish-placement", "application-event-dispatcher", "both-mode-definition-references",
            "exact-reference-lookup", "mode-specific-catalog-builders", "stable-identity-and-absence-rules");
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "7.24" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsTheApprovedEventCatalogAndAbsenceContract()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var anchor in new[]
        {
            "EventContractVersion", "WorkflowEventContract<TPayload>", "WorkflowEventRoute",
            "StartOrDeliver<TInput>", "WorkflowInboundEvent<TPayload>", "WorkflowEventAcceptanceResult",
            "WorkflowEventAcceptanceRejection", "IWorkflowEventIngress", "WorkflowOutboundEvent",
            "IWorkflowEventDispatcher", "EphemeralWorkflowRef<TInput>", "DurableWorkflowRef<TInput>",
            "GetRequiredHandle<TInput>", "OrcaCoreEphemeralEngineBuilder", "OrcaCoreDurableEngineBuilder",
            "| `Publish` | durable | durable | durable | durable |",
            "step/`If`/wait/`Publish`/delay/return"
        }) matrix.Should().Contain(anchor);

        foreach (var superseded in new[]
        {
            "IWorkflowEventClient", "EventDeliveryStatus", "EventDeliveryResult",
            "DeliverToInstanceAsync", "DeliverByCorrelationAsync"
        }) matrix.Should().NotContain(superseded);
    }

    [Fact]
    public void Companion_HasExactWaitAndPublishPlacement()
    {
        var companion = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-public-authoring-contract.cs"));

        WaitBuilders.Should().OnlyContain(builder => CountDeclaredMethods(companion, builder, "Wait") == 4);
        PublishBuilders.Should().OnlyContain(builder => CountDeclaredMethods(companion, builder, "Publish") == 2);

        var ephemeralBlocks = DeclaredTypeBlocks(companion)
            .Where(block => block.Name.StartsWith("Ephemeral", StringComparison.Ordinal)).ToArray();
        ephemeralBlocks.Should().HaveCount(14);
        ephemeralBlocks.Should().OnlyContain(block =>
            Regex.Matches(block.Body, @"\bPublish(?:<[^>]+>)?\s*\(").Count == 0);

        var nonSequentialDurableBlocks = DeclaredTypeBlocks(companion)
            .Where(block => new[]
            {
                "DurableWorkflowInitBuilder", "DurableWorkflowCompletionBuilder",
                "DurableWorkflowParallelBranchScopeBuilder", "DurableWorkflowParallelJoinBuilder",
                "DurableForEachJoinBuilder"
            }.Contains(block.Name, StringComparer.Ordinal)).ToArray();
        nonSequentialDurableBlocks.Should().HaveCount(6);
        nonSequentialDurableBlocks.Should().OnlyContain(block =>
            Regex.Matches(block.Body, @"\bPublish(?:<[^>]+>)?\s*\(").Count == 0,
            "durable init, completion, branch-scope, and join families are not sequential authoring surfaces");
    }

    [Fact]
    public void CompileAndPackageFixtures_UseOnlyTheSection7BConsumerVocabulary()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var compileFixture = File.ReadAllText(Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards",
            "CompileFixtures", "Section7BSourceSurface", "Section7BSourceSurface.cs"));
        var packageFiles = new[]
        {
            "PrimaryPackage/Program.cs", "MinimalEphemeral/Program.cs", "PostgreSqlDurable/Program.cs",
            "CallbackIngress/Program.cs", "InMemoryDurable/Program.cs", "KubernetesCompanion/Program.cs"
        }.Select(path => File.ReadAllText(Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards",
            "PackageFixtures", path.Replace('/', Path.DirectorySeparatorChar))));
        var consumers = string.Join(Environment.NewLine, packageFiles.Prepend(compileFixture));

        foreach (var required in new[]
        {
            "WorkflowEventContract", "EventContractVersion", "WorkflowEventRoute", "WorkflowInboundEvent",
            "IWorkflowEventIngress", "IWorkflowEventDispatcher", "EphemeralWorkflowRef",
            "DurableWorkflowRef", "GetRequiredHandle", "OrcaCoreEphemeralEngineBuilder",
            "OrcaCoreDurableEngineBuilder", "AddWorkflow", "Publish", "GetPayload"
        }) consumers.Should().Contain(required);
        foreach (var superseded in new[]
        {
            "IWorkflowEventClient", "EventDeliveryStatus", "EventDeliveryResult",
            "DeliverToInstanceAsync", "DeliverByCorrelationAsync", "WorkflowEvent.Create", "DefinitionId.New()"
        }) consumers.Should().NotContain(superseded);
    }

    [Fact]
    public void Product_NonSequentialDurableBuildersExposeNoPublish()
    {
        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(assembly => assembly.GetExportedTypes()).ToArray();
        foreach (var builder in new[]
        {
            "OrcaCore.DurableWorkflowInitBuilder`1",
            "OrcaCore.DurableWorkflowCompletionBuilder`1", "OrcaCore.DurableWorkflowCompletionBuilder`2",
            "OrcaCore.DurableWorkflowParallelBranchScopeBuilder`3", "OrcaCore.DurableWorkflowParallelJoinBuilder`3",
            "OrcaCore.DurableForEachJoinBuilder`3"
        })
        {
            var type = exported.Should().ContainSingle(candidate => candidate.FullName == builder).Subject;
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Should().NotContain(method => method.Name == "Publish",
                    $"{builder} is an init, completion, branch-scope, or join family");
        }
    }

    [Fact]
    public void Product_Task725DescriptorsAndResumedEnvelopeHaveTheExactShape()
    {
        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(assembly => assembly.GetExportedTypes()).ToArray();
        Type Required(string name) => exported.Should()
            .ContainSingle(type => type.FullName == name).Subject;

        var version = Required("OrcaCore.EventContractVersion");
        version.IsSealed.Should().BeTrue();
        version.GetConstructors().Should().ContainSingle()
            .Which.GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(typeof(int));
        version.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name).Should().Equal("Value");

        var descriptor = Required("OrcaCore.WorkflowEventContract");
        descriptor.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
        descriptor.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Should().ContainSingle()
            .Which.IsFamilyAndAssembly.Should().BeTrue();
        descriptor.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name).Should().Equal("EventName", "Version");

        var typedDescriptor = Required("OrcaCore.WorkflowEventContract`1");
        typedDescriptor.IsSealed.Should().BeTrue();
        typedDescriptor.BaseType.Should().Be(descriptor);
        typedDescriptor.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Should().ContainSingle()
            .Which.IsPrivate.Should().BeTrue();

        var stepResult = Required("OrcaCore.StepResult");
        stepResult.GetNestedTypes(BindingFlags.Public)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(type => type.Name).Should().Equal(
                "Completed", "Failed", "WaitForEvent", "WaitForEvent`1");
        foreach (var waitResult in stepResult.GetNestedTypes(BindingFlags.Public)
                     .Where(type => type.Name.StartsWith("WaitForEvent", StringComparison.Ordinal)))
        {
            waitResult.IsSealed.Should().BeTrue();
            var expectedDescriptorType = waitResult.IsGenericTypeDefinition
                ? typeof(WorkflowEventContract<>).MakeGenericType(waitResult.GetGenericArguments()[0])
                : typeof(WorkflowEventContract);
            waitResult.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().ContainSingle()
                .Which.GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(
                    expectedDescriptorType,
                    typeof(CorrelationId));
            waitResult.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => property.Name).Should().Equal("CorrelationId", "EventContract");
        }

        Required("OrcaCore.WorkflowWaitTimeoutException")
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => property.Name).Should().Equal("CorrelationId", "EventContract");
        Required("OrcaCore.AmbiguousWaitRegistrationException")
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => property.Name).Should().Equal(
                "CorrelationId", "DefinitionId", "EventContract");

        var envelope = Required("OrcaCore.EventEnvelope");
        envelope.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
        envelope.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Should().ContainSingle()
            .Which.GetParameters().Select(parameter => Task725Canonical(parameter.ParameterType)).Should().Equal(
                "OrcaCore.EventId", "OrcaCore.WorkflowEventContract", "OrcaCore.CorrelationId",
                "System.DateTimeOffset", "System.ReadOnlyMemory<System.Byte>");
        envelope.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => property.Name).Should().Equal(
                "CorrelationId", "EventContract", "EventId", "OccurredAt");
        envelope.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName).Should().ContainSingle(method =>
                method.Name == "GetPayload" &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() ==
                    typeof(WorkflowEventContract<>));
    }

    [Fact]
    public void Product_Task725WaitsHaveTheFourExactDescriptorOverloads()
    {
        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(assembly => assembly.GetExportedTypes()).ToArray();
        foreach (var builderName in WaitBuilders)
        {
            var builder = exported.Should().ContainSingle(type =>
                type.Namespace == "OrcaCore" &&
                type.Name.StartsWith($"{builderName}`", StringComparison.Ordinal)).Subject;
            var waits = builder.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == "Wait").ToArray();
            waits.Should().HaveCount(4);
            waits.Should().OnlyContain(method => method.ReturnType == builder);
            waits.Count(method => !method.IsGenericMethodDefinition).Should().Be(2);
            waits.Count(method => method.IsGenericMethodDefinition).Should().Be(2);
            waits.Should().OnlyContain(method =>
                method.GetParameters()[1].ParameterType.IsGenericType &&
                method.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) &&
                method.GetParameters()[1].ParameterType.GetGenericArguments()[0].IsGenericType &&
                method.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() ==
                    typeof(ReadOnlyStateSnapshot<>) &&
                method.GetParameters()[1].ParameterType.GetGenericArguments()[1] == typeof(CorrelationId));
            waits.Count(method => method.GetParameters().Length == 2).Should().Be(2);
            waits.Count(method => method.GetParameters().Length == 3 &&
                method.GetParameters()[2].ParameterType == typeof(TimeSpan)).Should().Be(2);
            waits.Should().ContainSingle(method =>
                !method.IsGenericMethodDefinition && method.GetParameters().Length == 2 &&
                method.GetParameters()[0].ParameterType == typeof(WorkflowEventContract));
            waits.Should().ContainSingle(method =>
                !method.IsGenericMethodDefinition && method.GetParameters().Length == 3 &&
                method.GetParameters()[0].ParameterType == typeof(WorkflowEventContract) &&
                method.GetParameters()[2].ParameterType == typeof(TimeSpan));
            waits.Where(method => method.IsGenericMethodDefinition).Should().OnlyContain(method =>
                method.GetParameters()[0].ParameterType.IsGenericType &&
                method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() ==
                    typeof(WorkflowEventContract<>) &&
                method.GetParameters()[0].ParameterType.GetGenericArguments()[0] ==
                    method.GetGenericArguments()[0]);
        }
    }

    private static string Task725Canonical(Type type)
    {
        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().FullName![..^2];
            return $"{name}<{string.Join(',', type.GetGenericArguments().Select(Task725Canonical))}>";
        }

        return type.FullName ?? type.Name;
    }

    [Fact]
    public void Source_DurablePublishGateAndOutboundMaterializationRemainTypedAndFailClosed()
    {
        var repositoryRoot = FixtureDefinitions.RepositoryRoot();
        var facadeSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OrcaCore.Engine.Durable",
            "Facade",
            "DurableWorkflowFacade.cs"));
        var definitionSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OrcaCore.Core",
            "Definitions",
            "WorkflowDefinition.cs"));
        var hostingPumpSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OrcaCore.Durable.Hosting",
            "Services",
            "WorkflowEventOutboxPump.cs"));
        var applicationFactorySource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OrcaCore.Engine.Durable",
            "DurableApplicationContractFactory.cs"));

        facadeSource.Should().Contain("WorkflowDefinitionRuntimeMetadata");
        definitionSource.Should().Contain("instruction.Kind == CompiledInstructionKind.Publish");
        definitionSource.Should().Contain("instruction.StaticLeaseRequest");
        facadeSource.Should().NotContain("GetProperty(\"Kind\")");
        facadeSource.Should().NotContain("GetProperty(\"StaticLeaseRequest\")");
        hostingPumpSource.Should().Contain("DurableApplicationContractFactory.WorkflowOutboundEvent(data)");
        hostingPumpSource.Should().NotContain("BindingFlags.NonPublic");
        hostingPumpSource.Should().NotContain("ConstructorInfo");
        applicationFactorySource.Should().Contain("ConstructorCache<TContract>.Get(parameterTypes)");
        applicationFactorySource.Should().Contain("TypedWorkflowEventContractFactories.GetOrAdd");
    }

    private static int CountDeclaredMethods(string source, string typeName, string methodName)
    {
        var block = DeclaredTypeBlocks(source).Single(candidate => candidate.Name == typeName);
        return Regex.Matches(block.Body, $@"\b{Regex.Escape(methodName)}(?:<[^>]+>)?\s*\(").Count;
    }

    private static IReadOnlyList<(string Name, string Body)> DeclaredTypeBlocks(string source)
    {
        var declarations = Regex.Matches(source,
            @"public\s+(?:abstract\s+)?(?:sealed\s+)?(?:class|record)\s+(?<name>[A-Za-z0-9_]+)(?:<[^\r\n{]+>)?")
            .Cast<Match>().ToArray();
        var blocks = new List<(string Name, string Body)>();
        foreach (var declaration in declarations)
        {
            var open = source.IndexOf('{', declaration.Index + declaration.Length);
            if (open < 0) continue;
            var depth = 0;
            for (var index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0)
                {
                    blocks.Add((declaration.Groups["name"].Value, source[open..(index + 1)]));
                    break;
                }
            }
        }
        return blocks;
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class FacadeHostingExpectedRedGuards
{
    private static readonly BindingFlags DeclaredPublicInstance =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void PublicApi_ExportsTheExactOwnedSection7BTypesAndNoSupersededClient()
    {
        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(x => x.GetExportedTypes()).ToArray();
        var expectedOwners = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OrcaCore.EventContractVersion"] = "OrcaCore",
            ["OrcaCore.WorkflowEventContract"] = "OrcaCore",
            ["OrcaCore.WorkflowEventContract`1"] = "OrcaCore",
            ["OrcaCore.WorkflowEventRoute"] = "OrcaCore",
            ["OrcaCore.WorkflowInboundEvent"] = "OrcaCore",
            ["OrcaCore.WorkflowInboundEvent`1"] = "OrcaCore",
            ["OrcaCore.WorkflowEventAcceptanceRejection"] = "OrcaCore",
            ["OrcaCore.WorkflowEventAcceptanceResult"] = "OrcaCore",
            ["OrcaCore.WorkflowOutboundEvent"] = "OrcaCore",
            ["OrcaCore.EphemeralWorkflowRef`1"] = "OrcaCore",
            ["OrcaCore.EphemeralWorkflowRef`2"] = "OrcaCore",
            ["OrcaCore.DurableWorkflowRef`1"] = "OrcaCore",
            ["OrcaCore.DurableWorkflowRef`2"] = "OrcaCore",
            ["OrcaCore.WorkflowDefinitionNotRegisteredException"] = "OrcaCore",
            ["OrcaCore.WorkflowEventDispatchFailure"] = "OrcaCore.Durable.Hosting",
            ["OrcaCore.WorkflowEventDispatchResult"] = "OrcaCore.Durable.Hosting",
            ["OrcaCore.Durable.Hosting.IWorkflowEventIngress"] = "OrcaCore.Durable.Hosting",
            ["OrcaCore.Durable.Hosting.IWorkflowEventDispatcher"] = "OrcaCore.Durable.Hosting",
            ["OrcaCore.Hosting.OrcaCoreEphemeralEngineBuilder"] = "OrcaCore.Engine.Ephemeral",
            ["OrcaCore.Hosting.OrcaCoreDurableEngineBuilder"] = "OrcaCore.Durable.Hosting"
        };

        foreach (var expected in expectedOwners)
        {
            var type = exported.SingleOrDefault(candidate => candidate.FullName == expected.Key);
            type.Should().NotBeNull($"{expected.Key} must be exported");
            type?.Assembly.GetName().Name.Should().Be(expected.Value, $"{expected.Key} has one package owner");
        }

        exported.Select(type => type.FullName).Should().NotContain(new[]
        {
            "OrcaCore.IWorkflowEventClient", "OrcaCore.EventDeliveryStatus", "OrcaCore.EventDeliveryResult",
            "OrcaCore.WorkflowEvent", "OrcaCore.WorkflowEvent`1"
        });
    }

    [Fact]
    public void Reflection_EventRoutesAndAcceptanceResultsAreClosedToTheExactCases()
    {
        ExactNestedTypes("OrcaCore.WorkflowEventRoute",
            "Direct", "Correlation", "DefinitionFanout", "StartOrDeliver`1");
        ExactNestedTypes("OrcaCore.WorkflowEventAcceptanceResult", "Accepted", "Duplicate", "Rejected");
        ExactNestedTypes("OrcaCore.WorkflowEventAcceptanceRejection",
            "EventConflict", "DirectInstanceNotFound", "DirectInstanceTerminal", "StartConflict",
            "FanoutLimitExceeded");
        ExactNestedTypes("OrcaCore.WorkflowEventDispatchResult",
            "Succeeded", "RetryableFailure", "PermanentFailure");
    }

    [Fact]
    public void Reflection_DescriptorsAndEnvelopesHaveTheExactApplicationShape()
    {
        RequiredType("OrcaCore.EventContractVersion").GetProperties(DeclaredPublicInstance)
            .Select(property => property.Name).Should().Equal("Value");
        RequiredType("OrcaCore.EventContractVersion").GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.Name).Should().Equal("Initial");
        RequiredType("OrcaCore.WorkflowEventContract").GetProperties(DeclaredPublicInstance)
            .Select(property => property.Name).Should().Equal("EventName", "Version");
        RequiredType("OrcaCore.WorkflowEventContract`1").BaseType.Should().Be(RequiredType("OrcaCore.WorkflowEventContract"));

        RequiredType("OrcaCore.WorkflowInboundEvent").GetProperties(DeclaredPublicInstance)
            .Select(property => property.Name).Should().Equal(
                "EventContract", "EventId", "CorrelationId", "CausationEventId", "OccurredAt", "Route");
        RequiredType("OrcaCore.WorkflowInboundEvent`1").GetProperties(DeclaredPublicInstance)
            .Select(property => property.Name).Should().Equal("EventContract", "Payload");
        RequiredType("OrcaCore.WorkflowInboundEvent`1").BaseType?.IsGenericType.Should().BeFalse();

        var outbound = RequiredType("OrcaCore.WorkflowOutboundEvent");
        outbound.GetProperties(DeclaredPublicInstance).Select(property => property.Name).Should().Equal(
            "EventContract", "EventId", "CorrelationId", "CausationEventId", "OccurredAt",
            "OriginInstanceId", "OriginDefinitionId", "OriginDefinitionVersion");
        outbound.GetMethods(DeclaredPublicInstance).Where(method => !method.IsSpecialName)
            .Select(method => method.Name).Should().Equal("GetPayload");
    }

    [Fact]
    public void Reflection_ResumedEventEnvelopeUsesTheApprovedDescriptorShape()
    {
        var envelope = RequiredType("OrcaCore.EventEnvelope");
        envelope.Assembly.GetName().Name.Should().Be("OrcaCore");
        envelope.Namespace.Should().Be("OrcaCore");
        envelope.IsSealed.Should().BeTrue();
        envelope.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();

        var constructor = envelope.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Should().ContainSingle().Subject;
        constructor.IsAssembly.Should().BeTrue();
        constructor.GetParameters().Select(parameter => Canonical(parameter.ParameterType)).Should().Equal(
            "OrcaCore.EventId",
            "OrcaCore.WorkflowEventContract",
            "OrcaCore.CorrelationId",
            "System.DateTimeOffset",
            "System.ReadOnlyMemory<System.Byte>");

        envelope.GetProperties(DeclaredPublicInstance)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}:{Canonical(property.PropertyType)}:{property.SetMethod is null}")
            .Should().Equal(
                "CorrelationId:OrcaCore.CorrelationId:True",
                "EventContract:OrcaCore.WorkflowEventContract:True",
                "EventId:OrcaCore.EventId:True",
                "OccurredAt:System.DateTimeOffset:True");

        var getPayload = envelope.GetMethods(DeclaredPublicInstance)
            .Should().ContainSingle(method => method.Name == "GetPayload").Subject;
        getPayload.IsGenericMethodDefinition.Should().BeTrue();
        getPayload.GetGenericArguments().Should().ContainSingle();
        getPayload.GetParameters().Select(parameter => Canonical(parameter.ParameterType)).Should().Equal(
            "OrcaCore.WorkflowEventContract<TPayload>");
        getPayload.ReturnType.Should().Be(getPayload.GetGenericArguments()[0]);
    }

    [Fact]
    public void Reflection_IngressAndDispatcherExposeOnlyTheApprovedMethods()
    {
        var ingress = RequiredType("OrcaCore.Durable.Hosting.IWorkflowEventIngress");
        var ingressMethods = ingress.GetMethods(DeclaredPublicInstance);
        ingressMethods.Should().HaveCount(2);
        ingressMethods.Should().OnlyContain(method => method.Name == "AcceptAsync");
        ingressMethods.Count(method => method.IsGenericMethodDefinition).Should().Be(1);
        ingressMethods.Should().OnlyContain(method =>
            Canonical(method.ReturnType) ==
            "System.Threading.Tasks.ValueTask<OrcaCore.WorkflowEventAcceptanceResult>");

        var dispatcher = RequiredType("OrcaCore.Durable.Hosting.IWorkflowEventDispatcher");
        var dispatcherMethods = dispatcher.GetMethods(DeclaredPublicInstance);
        dispatcherMethods.Should().ContainSingle();
        dispatcherMethods.Single().Name.Should().Be("DispatchAsync");
        dispatcherMethods.Single().GetParameters().Select(parameter => Canonical(parameter.ParameterType)).Should().Equal(
            "OrcaCore.WorkflowOutboundEvent", "System.Threading.CancellationToken");
        Canonical(dispatcherMethods.Single().ReturnType).Should().Be(
            "System.Threading.Tasks.ValueTask<OrcaCore.WorkflowEventDispatchResult>");
    }

    [Fact]
    public void Reflection_WaitsAndPublishesHaveExactModePlacement()
    {
        var waitBuilders = new[]
        {
            "EphemeralWorkflowBuilder`2", "DurableWorkflowBuilder`2",
            "EphemeralNestedBuilder`2", "DurableNestedBuilder`2",
            "EphemeralBranchBuilder`2", "DurableBranchBuilder`2",
            "EphemeralItemBuilder`2", "DurableItemBuilder`2",
            "DurableLeaseWorkflowBuilder`2", "DurableLeaseNestedBuilder`2",
            "DurableLeaseBranchBuilder`2", "DurableLeaseItemBuilder`2"
        };
        foreach (var builder in waitBuilders)
            RequiredType($"OrcaCore.{builder}").GetMethods(DeclaredPublicInstance)
                .Count(method => method.Name == "Wait").Should().Be(4, $"{builder} has four descriptor waits");

        var publishBuilders = waitBuilders.Where(builder => builder.StartsWith("Durable", StringComparison.Ordinal));
        foreach (var builder in publishBuilders)
            RequiredType($"OrcaCore.{builder}").GetMethods(DeclaredPublicInstance)
                .Count(method => method.Name == "Publish").Should().Be(2, $"{builder} has two durable publishes");

        PublicSurfaceCatalog.Assemblies.SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Namespace == "OrcaCore" && type.Name.StartsWith("Ephemeral", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(DeclaredPublicInstance))
            .Should().NotContain(method => method.Name == "Publish");

        foreach (var builder in new[]
        {
            "DurableWorkflowInitBuilder`1",
            "DurableWorkflowCompletionBuilder`1", "DurableWorkflowCompletionBuilder`2",
            "DurableWorkflowParallelBranchScopeBuilder`3", "DurableWorkflowParallelJoinBuilder`3",
            "DurableForEachJoinBuilder`3"
        })
            RequiredType($"OrcaCore.{builder}").GetMethods(DeclaredPublicInstance)
                .Should().NotContain(method => method.Name == "Publish",
                    $"{builder} is an init, completion, branch-scope, or join family");
    }

    [Fact]
    public void Reflection_ReferencesRegistryAndCatalogBuildersAreExact()
    {
        foreach (var reference in new[]
        {
            "OrcaCore.EphemeralWorkflowRef`1", "OrcaCore.EphemeralWorkflowRef`2",
            "OrcaCore.DurableWorkflowRef`1", "OrcaCore.DurableWorkflowRef`2"
        }) RequiredType(reference).GetProperties(DeclaredPublicInstance).Select(property => property.Name)
            .Should().BeEquivalentTo("Mode", "DefinitionId", "DefinitionVersion", "DefinitionFingerprint");

        RequiredType("OrcaCore.WorkflowDefinitionNotRegisteredException")
            .GetProperties(DeclaredPublicInstance)
            .Select(property => $"{property.Name}:{property.PropertyType.FullName}")
            .Should().Equal(
                "DefinitionId:OrcaCore.DefinitionId",
                "DefinitionVersion:OrcaCore.DefinitionVersion",
                "DefinitionFingerprint:OrcaCore.DefinitionFingerprint");

        var lookups = RequiredType("OrcaCore.IWorkflowDefinitionRegistry").GetMethods(DeclaredPublicInstance)
            .Where(method => method.Name == "GetRequiredHandle").ToArray();
        lookups.Should().HaveCount(4);
        lookups.Should().OnlyContain(method => method.GetParameters().Length == 1 &&
            method.GetParameters()[0].ParameterType.Name.Contains("WorkflowRef", StringComparison.Ordinal) &&
            method.ReturnType.Name.Contains("DefinitionHandle", StringComparison.Ordinal));

        foreach (var builder in new[]
        {
            "OrcaCore.Hosting.OrcaCoreEphemeralEngineBuilder",
            "OrcaCore.Hosting.OrcaCoreDurableEngineBuilder"
        })
        {
            var methods = RequiredType(builder).GetMethods(DeclaredPublicInstance);
            methods.Should().HaveCount(2);
            methods.Should().OnlyContain(method => method.Name == "AddWorkflow" && method.IsGenericMethodDefinition);
            var mode = builder.Contains("Ephemeral", StringComparison.Ordinal) ? "Ephemeral" : "Durable";
            methods.Should().OnlyContain(method =>
                method.GetParameters().Single().ParameterType.Name.StartsWith($"{mode}WorkflowDefinition", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Source_UsesStableCatalogIdentityAndHasNoAttributesScanningOrProviderLeaks()
    {
        var sourceRoot = Path.Combine(FixtureDefinitions.RepositoryRoot(), "src");
        var sources = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText).ToArray();
        var productSource = string.Join(Environment.NewLine, sources);

        foreach (var required in new[]
        {
            "EventContractVersion", "WorkflowEventContract", "WorkflowEventRoute", "WorkflowInboundEvent",
            "IWorkflowEventIngress", "WorkflowEventAcceptanceResult", "IWorkflowEventDispatcher",
            "EphemeralWorkflowRef", "GetRequiredHandle", "OrcaCoreEphemeralEngineBuilder",
            "OrcaCoreDurableEngineBuilder", "AddWorkflow"
        }) productSource.Should().Contain(required);
        foreach (var forbidden in new[]
        {
            "IWorkflowEventClient", "DeliverToInstanceAsync", "DeliverByCorrelationAsync",
            "WorkflowAttribute", "SignalAttribute", "Assembly.GetTypes(", "Assembly.GetExportedTypes("
        }) productSource.Should().NotContain(forbidden);

        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(assembly => assembly.GetExportedTypes()).ToArray();
        exported.Where(type => typeof(Attribute).IsAssignableFrom(type)).Should().NotContain(type =>
            type.Name.Contains("Workflow", StringComparison.Ordinal) ||
            type.Name.Contains("Signal", StringComparison.Ordinal));
        PublicSurfaceCatalog.FindForbiddenSignatureEdges().Should().BeEmpty();

        var catalogSources = sources.Where(source =>
            source.Contains("AddWorkflow", StringComparison.Ordinal) ||
            source.Contains("GetRequiredHandle", StringComparison.Ordinal)).ToArray();
        catalogSources.Should().NotBeEmpty();
        string.Join(Environment.NewLine, catalogSources).Should().NotContain("DefinitionId.New()",
            "durable catalog composition must reuse application-declared stable identity");
    }

    [Fact]
    public void Source_ContainsTheDurableWorkflowAuthoredPublishSignature()
    {
        var sourceRoot = Path.Combine(FixtureDefinitions.RepositoryRoot(), "src");
        var productSource = string.Join(Environment.NewLine,
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                               !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        Regex.IsMatch(productSource, @"\bPublish(?:<TPayload>)?\s*\(\s*WorkflowEventContract")
            .Should().BeTrue(
                "durable workflow-authored Publish must be present, not an unrelated published-state member");
    }

    private static Type RequiredType(string fullName)
    {
        var type = PublicSurfaceCatalog.Assemblies.SelectMany(assembly => assembly.GetExportedTypes())
            .SingleOrDefault(candidate => candidate.FullName == fullName);
        type.Should().NotBeNull($"{fullName} must exist before its reflection contract can turn green");
        return type!;
    }

    private static void ExactNestedTypes(string fullName, params string[] expected)
    {
        RequiredType(fullName).GetNestedTypes(BindingFlags.Public).Select(type => type.Name).Should().Equal(expected);
    }

    private static string Canonical(Type type)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var fullName = definition.FullName!;
            var name = fullName[..fullName.IndexOf('`')];
            return $"{name}<{string.Join(',', type.GetGenericArguments().Select(Canonical))}>";
        }
        return type.FullName ?? type.Name;
    }
}
