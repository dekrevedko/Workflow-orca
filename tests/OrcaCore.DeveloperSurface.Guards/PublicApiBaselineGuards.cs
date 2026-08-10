using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
[Collection(CompileFixtureCollection.Name)]
public sealed class PublicApiBaselineInfrastructureGuards
{
    private static readonly string[] ForbiddenPublicSymbols =
    {
        "OrcaCore::OrcaCore.Internal.WorkflowRuntimeBridge",
        "OrcaCore::OrcaCore.Abstractions.Events.EventEnvelope",
        "OrcaCore::OrcaCore.Abstractions.Steps.ForEachItemContext",
        "OrcaCore::OrcaCore.Abstractions.Durable.RunChildFailurePolicy",
        "OrcaCore::OrcaCore.Abstractions.Durable.RunChildrenJoinPolicy",
        "OrcaCore::OrcaCore.Abstractions.Durable.RunChildrenResidualPolicy",
        "OrcaCore::OrcaCore.Abstractions.Instances.ActiveStepSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.ActiveWaitSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.CompositionBranchOutcomeSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.ForEachGroupSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.ForEachWorkItemSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.ForEachWorkItemStatus",
        "OrcaCore::OrcaCore.Abstractions.Instances.LifecycleEventSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.SagaAuditScopeSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.SagaAuditSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.SagaCompensationActionSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.SagaCompensationActionStatus",
        "OrcaCore::OrcaCore.Abstractions.Instances.SagaForwardActionSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.SagaRecoveryInterventionSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.WaitMode",
        "OrcaCore::OrcaCore.Abstractions.Instances.WorkflowInstanceSnapshot",
        "OrcaCore::OrcaCore.Abstractions.Instances.WorkflowPressureMetrics",
        "OrcaCore::OrcaCore.Abstractions.Instances.WorkflowStatistics",
        "OrcaCore::OrcaCore.Abstractions.Instances.WorkflowStatisticsGroup",
        "OrcaCore::OrcaCore.Abstractions.Instances.WorkflowStatus",
        "OrcaCore.Core::OrcaCore.Core.Execution.IStructuredValueCodec",
        "OrcaCore.Core::OrcaCore.Core.Authoring.PublicAuthoringContracts",
        "OrcaCore.Core::OrcaCore.Core.Execution.FiberBlockedReason::ChildGroup",
        "OrcaCore.Core::OrcaCore.Core.Execution.FiberBlockedReason::ExternalJob",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Execution.DurableWorkflowRuntime::Management",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Execution.DurableWorkflowRuntime::RaiseEventToDefinitionAsync",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Aggregates.DurableYieldCommand",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Management.DagNodeRunSnapshot",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Management.DagRunSnapshot",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Management.DestructiveCommandSafety",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Management.DurableManagement",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Management.DurableManagementQuery",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Management.WorkflowInstanceQueryModel",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngine::Management",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngine::RaiseEventByDefinitionAsync",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.ActiveWaitStatistics",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.DestructiveCommandSafety",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.EphemeralInstanceManagement",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.EphemeralManagement",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.EphemeralManagementQuery",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.EphemeralStepManagement",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.TerminalCommandReport",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.WorkflowInstanceQueryModel",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.WorkflowStatistics",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Management.WorkflowStatisticsGroup",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.IWorkflowRetentionStore",
        "OrcaCore.Provider.Abstractions::OrcaCore.Provider.Abstractions.ResourceGovernance.IResourceLeaseGovernanceStore",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.ArchiveResult",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointActiveChild",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointActiveChildGroup",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointActiveExternalJob",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointSagaCompensationAction",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointSagaForwardAction",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointSagaRecoveryIntervention",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointBufferedDelivery",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.CheckpointBufferedTimer",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.IWorkflowPayloadCodec",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.IWorkflowPayloadSerializer",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.ProviderCommitPolicy",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.ProjectionCommitMode",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.PurgeResult",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.RetentionPolicy",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.SerializedPayload",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.WorkflowProjectionPressureMetrics",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.WorkflowProjectionQuery",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.WorkflowProjectionStatistics",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.WorkflowProjectionStatisticsGroup",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.ProviderJsonSerializerContext",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Execution.ContentTypeWorkflowPayloadSerializer",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Execution.JsonWorkflowPayloadSerializer",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngineOptions::StateSnapshotter",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.IEphemeralStateSnapshotter",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.SystemTextJsonEphemeralStateSnapshotter",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.CompensateChildGroupCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.DurableFiberBlockedReason::ChildGroup",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.DurableFiberBlockedReason::ExternalJob",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.DurableOwnedObligationKind::ChildGroup",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.DurableOwnedObligationKind::ExternalJob",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.CompleteExternalJobCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.CompleteSagaCompensationCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.ConsumeParentResumeTokenCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.FailSagaCompensationCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.RecordSagaForwardActionCompletedCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.RecordSagaManualRecoveryCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.RequestSagaCompensationCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.RunExternalJobCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaCompensationCompletedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaCompensationFailedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaCompensationRequestedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaCompensationStartedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaForwardActionCompletedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaForwardActionTimedOutCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaForwardActionTimedOutEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaForwardActionsTransferredEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.SagaManualRecoveryRecordedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.TimeoutExternalJobCommand",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildCompensationMaterialization",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildCompensationScheduledEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildCompletedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildMaterialization",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildResidualIntentRecordedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildScheduledEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildrenDispatchedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowChildrenScheduledEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowDeliveryBufferedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowDeliveryDiscardedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowExternalJobCompletedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowExternalJobStartedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowExternalJobStopRequestedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowExternalJobTimedOutEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowParentResumeTokenConsumedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowParentResumeTokenRecordedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowPausedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowResumedEvent",
        "OrcaCore.Runtime.Protocol::OrcaCore.Abstractions.Durable.WorkflowTimerBufferedEvent"
    };

    private static readonly string[] RequiredRemovedPlaceholders =
    {
        "OrcaCore::OrcaCore.EngineAcquireResourcesStepResult",
        "OrcaCore::OrcaCore.EngineContinueAsNewStepResult`1",
        "OrcaCore::OrcaCore.EngineExternalJobStepResult",
        "OrcaCore::OrcaCore.EngineYieldStepResult"
    };

    private static readonly string[] ForbiddenMetadataTypes =
    {
        "OrcaCore::OrcaCore.Internal.WorkflowRuntimeBridge",
        "OrcaCore::OrcaCore.Internal.AuthoringKernelProxy",
        "OrcaCore.Core::OrcaCore.Core.Authoring.AuthoringContractFactory",
        "OrcaCore.Core::OrcaCore.Core.Execution.IStructuredValueCodec",
        "OrcaCore.Core::OrcaCore.Core.Authoring.PublicAuthoringContracts",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Driver.RuntimeStepContextFactory",
        "OrcaCore.Engine.Durable::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Execution.RuntimeStepContextFactory",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory"
    };

    [Fact]
    public void EveryTargetAssembly_MatchesTheApprovedExactPublicApiBaseline()
    {
        var packageFeed = Environment.GetEnvironmentVariable(PublicApiBaseline.PackageFeedVariable);
        var actual = string.IsNullOrWhiteSpace(packageFeed)
            ? PublicApiBaseline.CaptureCurrent()
            : PublicApiBaseline.CapturePackages(packageFeed);
        var candidateDirectory = Environment.GetEnvironmentVariable(PublicApiBaseline.CandidateDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(candidateDirectory))
        {
            PublicApiBaseline.WriteCandidates(actual, candidateDirectory);
            throw new InvalidOperationException(
                $"Captured candidate public API files in '{Path.GetFullPath(candidateDirectory)}'. " +
                "Candidate capture never approves or updates the checked-in baseline.");
        }

        var approved = PublicApiBaseline.ReadApproved();
        PublicApiBaseline.Diff(approved, actual).Should().BeEmpty(
            "every public type and declared externally visible member must match the independently reviewed baseline");
    }

    [Fact]
    public void RemovedInternalStepResultPlaceholderInventory_IsExactAndAbsentFromProductMetadata()
    {
        RemovedInternalPlaceholderCatalog.All
            .Select(item => $"{item.Assembly}::{item.MetadataName}")
            .Order(StringComparer.Ordinal)
            .Should().Equal(RequiredRemovedPlaceholders);
        RemovedInternalPlaceholderCatalog.All.Should().OnlyContain(item => item.Task == "7.18");

        var packageFeed = Environment.GetEnvironmentVariable(PublicApiBaseline.PackageFeedVariable);
        var findings = string.IsNullOrWhiteSpace(packageFeed)
            ? PublicApiBaseline.FindForbiddenInternalPlaceholders()
            : PublicApiBaseline.FindForbiddenInternalPlaceholdersInPackages(packageFeed);
        findings.Should().BeEmpty(
            "removed StepResult control-intent bridges must not survive merely by becoming internal");
    }

    [Fact]
    public void RemovedDeferredAndWrongOwnerPublicSymbols_AreAbsentBeforeBaselineApproval()
    {
        ForbiddenPublicSymbols.Should().OnlyHaveUniqueItems();
        var packageFeed = Environment.GetEnvironmentVariable(PublicApiBaseline.PackageFeedVariable);
        var findings = string.IsNullOrWhiteSpace(packageFeed)
            ? PublicApiBaseline.FindForbiddenPublicSymbols(ForbiddenPublicSymbols)
            : PublicApiBaseline.FindForbiddenPublicSymbolsInPackages(packageFeed, ForbiddenPublicSymbols);
        findings.Should().BeEmpty(
            "the exact baseline must never normalize removed, deferred, broad-management, or wrong-owner surfaces as approved");
    }

    [Fact]
    public void RemovedCapabilitiesAndConstructionHelpers_HaveNoProductSourceBridge()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var forbiddenNames = new[]
        {
            "RaiseEventToDefinitionAsync",
            "RaiseEventByDefinitionAsync",
            "RaiseEventByDefinitionCoreAsync",
            "IStructuredValueCodec",
            "AuthoringKernelProxy",
            "AuthoringContractFactory",
            "DurableApplicationContractFactory",
            "EphemeralApplicationContractFactory",
            "RuntimeStepContextFactory",
            "WorkflowRuntimeBridge",
            "PublicAuthoringContracts"
        };
        var findings = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => forbiddenNames
                .Where(name => File.ReadAllText(path).Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(name => $"{Path.GetRelativePath(root, path)} -> {name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        findings.Should().BeEmpty(
            "obsolete raw fanout APIs and removed construction helpers must not survive as public, internal, renamed, or reflection bridges; the approved self-routing definition-fanout provider route remains allowed");
    }

    [Fact]
    public void TypedAuthoringBoundary_UsesClosedGenericOperationsAndNoReflectionNameDispatch()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var boundaryPaths = new[]
        {
            "src/OrcaCore.Abstractions/Internal/TypedAuthoringBoundary.cs",
            "src/OrcaCore.Core/Building/TypedAuthoringOperations.cs",
            "src/OrcaCore.Abstractions/Authoring/WorkflowAuthoringFacades.cs",
            "src/OrcaCore.Abstractions/Authoring/NestedAuthoringFacades.cs",
            "src/OrcaCore.Abstractions/Authoring/BranchAuthoringFacades.cs"
        };
        var sources = boundaryPaths.ToDictionary(
            path => path,
            path => File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))),
            StringComparer.Ordinal);
        var combined = string.Join('\n', sources.Values);

        sources[boundaryPaths[0]].Should().Contain("interface ITypedAuthoringOperations");
        sources[boundaryPaths[0]].Should().Contain("Assembly.Load(CoreAssemblyName)");
        sources[boundaryPaths[0]].Should().Contain("RuntimeHelpers.RunModuleConstructor(core.ManifestModule.ModuleHandle)");
        sources[boundaryPaths[1]].Should().Contain("class TypedAuthoringOperations : ITypedAuthoringOperations");
        sources[boundaryPaths[1]].Should().Contain("TypedAuthoringBoundary.Install(TypedAuthoringOperations.Instance)");
        sources.Skip(2).Select(pair => pair.Value).Should().OnlyContain(source =>
            source.Contains("AuthoringKernelHandle", StringComparison.Ordinal));

        var forbiddenTokens = new[]
        {
            "GetMethod(",
            "GetMethods(",
            "MakeGenericMethod",
            "GetConstructor(",
            "GetConstructors(",
            "Expression.",
            "TargetInvocationException",
            "BindingFlags.NonPublic",
            "string methodName",
            "private readonly object implementation",
            "dynamic "
        };
        forbiddenTokens.Should().OnlyContain(token => !combined.Contains(token, StringComparison.Ordinal),
            "the typed authoring boundary must stay compile-checked and may not regress to member-name dispatch or a non-public reflection bridge");
        File.Exists(Path.Combine(root, "src/OrcaCore.Abstractions/Internal/AuthoringKernelProxy.cs"))
            .Should().BeFalse();
    }

    [Fact]
    public void RemovedCapabilitiesAndConstructionHelpers_HaveNoProductMetadataType()
    {
        ForbiddenMetadataTypes.Should().OnlyHaveUniqueItems();
        var packageFeed = Environment.GetEnvironmentVariable(PublicApiBaseline.PackageFeedVariable);
        var findings = string.IsNullOrWhiteSpace(packageFeed)
            ? PublicApiBaseline.FindForbiddenMetadataTypes(ForbiddenMetadataTypes)
            : PublicApiBaseline.FindForbiddenMetadataTypesInPackages(packageFeed, ForbiddenMetadataTypes);
        findings.Should().BeEmpty(
            "removed codec and construction helpers must not be hidden as internal metadata compatibility bridges");
    }

    [Fact]
    public void RemovedInternalStepResultPlaceholderNames_HaveNoSourceOrReflectionStringBridge()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var scanRoots = new[] { Path.Combine(root, "src"), Path.Combine(root, "tests", "OrcaCore.TestSupport") };
        var forbiddenNames = RemovedInternalPlaceholderCatalog.All.Select(item =>
            item.MetadataName[(item.MetadataName.LastIndexOf('.') + 1)..]).ToArray();
        var findings = new List<string>();
        foreach (var scanRoot in scanRoots.Where(Directory.Exists))
        foreach (var file in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            var source = File.ReadAllText(file);
            foreach (var name in forbiddenNames.Where(name => source.Contains(name, StringComparison.Ordinal)))
                findings.Add($"{Path.GetRelativePath(root, file)} -> {name}");
        }

        findings.Should().BeEmpty(
            "qualified removed placeholders must have neither declarations nor name-based reflection fallbacks");
    }

    [Fact]
    public void CanonicalFormatter_CoversEveryRequiredPublicDeclarationCategory()
    {
        var formatted = PublicApiBaseline.CaptureAssemblyForTesting(typeof(PublicApiFormatterProbe<>).Assembly);

        formatted.Should().Contain("type public abstract class OrcaCore.DeveloperSurface.Guards.PublicApiFormatterProbe`1");
        formatted.Should().Contain("type protected class OrcaCore.DeveloperSurface.Guards.PublicApiFormatterProbe`1+Nested`1");
        formatted.Should().Contain("generic T : class & new()");
        formatted.Should().Contain("ctor protected .ctor(");
        formatted.Should().Contain("field public const [System.Private.CoreLib]System.Int32 Constant = 1");
        formatted.Should().Contain("field protected readonly !T");
        formatted.Should().Contain("property [System.Private.CoreLib]System.String");
        formatted.Should().Contain("event [System.Private.CoreLib]System.EventHandler");
        formatted.Should().Contain("operator public static op_Addition(");
        formatted.Should().Contain("method public abstract Transform<TResult>(in !T");
        formatted.Should().Contain("where [TResult : unmanaged");
    }

    [Fact]
    public void BaselineComparator_RejectsBothAddedAndRemovedSignatures()
    {
        var expected = PublicSurfaceCatalog.TargetAssemblyNames.ToDictionary(
            name => name,
            name => $"# orcacore-public-api-v1\nassembly {name}\ntype public class Approved.{name}\n",
            StringComparer.Ordinal);
        var actual = expected.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        actual["OrcaCore"] = "# orcacore-public-api-v1\nassembly OrcaCore\ntype public class Unexpected.Addition\n";

        var diff = PublicApiBaseline.Diff(expected, actual);
        diff.Should().Contain("- type public class Approved.OrcaCore");
        diff.Should().Contain("+ type public class Unexpected.Addition");
    }

    [Fact]
    public void BaselineInventoryAndCandidateCapture_FailClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), "orcacore-public-api-guard", Guid.NewGuid().ToString("N"));
        var missing = Path.Combine(root, "missing");
        var partial = Path.Combine(root, "partial");
        var nonemptyCandidate = Path.Combine(root, "candidate");

        try
        {
            Action readMissing = () => PublicApiBaseline.ReadApproved(missing);
            readMissing.Should().Throw<InvalidOperationException>()
                .WithMessage("Approved public API baseline directory is missing:*");

            Directory.CreateDirectory(partial);
            File.WriteAllText(
                Path.Combine(partial, "OrcaCore.api.txt"),
                "# orcacore-public-api-v1\nassembly OrcaCore\n");
            Action readPartial = () => PublicApiBaseline.ReadApproved(partial);
            readPartial.Should().Throw<InvalidOperationException>()
                .WithMessage("Public API baseline coverage mismatch.*");

            Directory.CreateDirectory(nonemptyCandidate);
            File.WriteAllText(Path.Combine(nonemptyCandidate, "stale.api.txt"), "stale");
            var captured = PublicSurfaceCatalog.TargetAssemblyNames.ToDictionary(
                name => name,
                name => $"# orcacore-public-api-v1\nassembly {name}\n",
                StringComparer.Ordinal);
            Action writeOverStale = () => PublicApiBaseline.WriteCandidates(captured, nonemptyCandidate);
            writeOverStale.Should().Throw<InvalidOperationException>()
                .WithMessage("Candidate capture destination must be absent or empty:*");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

public abstract class PublicApiFormatterProbe<T>
    where T : class, new()
{
    public const int Constant = 1;
    protected readonly T ProtectedField;

    protected PublicApiFormatterProbe(T value)
    {
        ProtectedField = value;
    }

    public required string Name { get; init; }

    public abstract string this[int index] { get; protected set; }

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public abstract TResult Transform<TResult>(in T input, out string output)
        where TResult : unmanaged;

    public static PublicApiFormatterProbe<T> operator +(
        PublicApiFormatterProbe<T> left,
        PublicApiFormatterProbe<T> right) => left;

    protected class Nested<TNested>
        where TNested : unmanaged
    {
    }
}
