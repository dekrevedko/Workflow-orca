using System.Diagnostics;
using System.Formats.Tar;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
[Collection(CompileFixtureCollection.Name)]
public sealed class PublicApiBaselineInfrastructureGuards
{
    private static readonly string[] ForbiddenPublicSymbols =
    {
        "OrcaCore::OrcaCore.EventDeliveryResult",
        "OrcaCore::OrcaCore.EventDeliveryStatus",
        "OrcaCore::OrcaCore.IWorkflowEventClient",
        "OrcaCore::OrcaCore.WorkflowEvent",
        "OrcaCore::OrcaCore.WorkflowEvent`1",
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
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.ActiveWaitStatistics",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.DestructiveCommandSafety",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.EphemeralInstanceManagement",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.EphemeralManagement",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.EphemeralManagementQuery",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.EphemeralStepManagement",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.TerminalCommandReport",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.WorkflowInstanceQueryModel",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.WorkflowStatistics",
        "OrcaCore.Engine.Ephemeral::OrcaCore.Engine.Ephemeral.WorkflowStatisticsGroup",
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
        "OrcaCore::OrcaCore.Abstractions.Providers.IWorkflowPayloadCodec",
        "OrcaCore::OrcaCore.Abstractions.Providers.IWorkflowPayloadSerializer",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.ProviderCommitPolicy",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.ProjectionCommitMode",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.PurgeResult",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.RetentionPolicy",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.SerializedPayload",
        "OrcaCore.Provider.Abstractions::OrcaCore.Abstractions.Providers.WorkflowProjectionQuery",
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

    private static readonly string[] ForbiddenManifestWideTypeNames =
    {
        "OrcaCore.Hosting.OrcaCoreOpenTelemetryServiceCollectionExtensions",
        "OrcaCore.Hosting.OrcaCoreServiceCollectionExtensions",
        "OrcaCore.Hosting.WorkflowPayloadSerializationOptions"
    };

    private const string ForbiddenProbeMarker = "// FORBIDDEN:";

    private static readonly string[] ForbiddenPublicSymbolOwnerCommits =
    {
        "ac46d99543daf85c0fa3234272997ba40f47f96b",
        "666bc1e6ec57eb055f3fecbb8f74a64ebe2e1ea9"
    };

    private static readonly IReadOnlyDictionary<string, string> ForbiddenPublicSymbolAssemblyRoots =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OrcaCore"] = "src/OrcaCore.Abstractions/",
            ["OrcaCore.Core"] = "src/OrcaCore.Core/",
            ["OrcaCore.Engine.Durable"] = "src/OrcaCore.Engine.Durable/",
            ["OrcaCore.Engine.Ephemeral"] = "src/OrcaCore.Engine.Ephemeral/",
            ["OrcaCore.Provider.Abstractions"] = "src/OrcaCore.Provider.Abstractions/",
            ["OrcaCore.Runtime.Protocol"] = "src/OrcaCore.Runtime.Protocol/"
        };

    private const string Task76Artifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-6-forbidden-symbol-qualified-owner-audit-2026-09-22.md";
    private const string Task76ArtifactSha256 =
        "5fc46471770add6553016488d4203ab4400997c7ed1c243cbca1d8771a160025";
    private const string Task76ReviewRemediationArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-5-and-7-6-review-remediation-2026-09-23.md";
    private const string Task76ReviewRemediationArtifactSha256 =
        "7a228cd530d66006846b0fbbb340d1b91cf1ecd698c80b0d27cb6ec3830c15e5";
    private const string Task76CompletionDecision =
        "**Completed:** corrected ten ephemeral-management namespaces and two pre-split payload-codec " +
        "assembly owners, deleted three identities that never existed, and verified all 122 retained " +
        "negatives against exact namespace/type owners and member declarations inside the named type " +
        "at two immutable historical commits. The package probe, deletion-ledger inventory, and " +
        "exact-owner regression now share one catalog. The deletion inventory remains owned by reshape " +
        "task 7.17; this harmonization task audits and pins its qualified identities.";
    private const string Task76ReviewRemediationDecision =
        "**Review remediation:** PPP-1 restored the reshape Task 7.17 owner and bound it to that " +
        "task's removal text; QQQ-1 reconciled every Markdown accounting count and fourth inventory, " +
        "changed historical member resolution from body tokens to declarations, and corrected the " +
        "removed-lineage and historical-project wording. The rejected target remains immutable.";
    private const string Task76DesignDecision =
        "Task 7.6 audits the full catalog against source archives at the pre-reshape and pre-package-split\n" +
        "commits: ten ephemeral management identities lose a `.Management` namespace absent from the\n" +
        "promoted product lineage (though present in removed `v3/` lineages), two payload-codec identities\n" +
        "use the current `OrcaCore` successor of historical `OrcaCore.Abstractions.csproj`, three invented\n" +
        "projection-statistics identities are deleted, and every one of the 122 retained negatives resolves\n" +
        "to an exact historical namespace and type declaration under the named assembly source root;\n" +
        "member identities must resolve to declarations inside that named type's balanced body, not merely\n" +
        "to parameter, comment, or string tokens.";

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
    public void EveryManifestPackage_MatchesTheApprovedSourceRecord_AndAnyProvidedFeedMatchesCurrentAssemblies()
    {
        var packageFeed = Environment.GetEnvironmentVariable(PublicApiBaseline.PackageFeedVariable);
        var actual = PackageSourceProvenance.Capture(
            string.IsNullOrWhiteSpace(packageFeed) ? null : packageFeed);
        var candidatePath = Environment.GetEnvironmentVariable(PackageSourceProvenance.CandidatePathVariable);
        if (!string.IsNullOrWhiteSpace(candidatePath))
        {
            PackageSourceProvenance.WriteCandidate(actual, candidatePath);
            throw new InvalidOperationException(
                $"Captured unapproved package source provenance in '{Path.GetFullPath(candidatePath)}'. " +
                "Candidate capture never approves or updates the checked-in record.");
        }

        var approved = PackageSourceProvenance.ReadApproved();
        PackageSourceProvenance.Diff(approved, actual).Should().BeEmpty(
            "each reviewed package must be tied to its current source inputs and every explicitly provided feed must contain the exact current implementation assemblies");
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
        AssertHistoricalOwnerResolutionSemantics();
        FindForbiddenPublicSymbolsWithoutHistoricalOwner().Should().BeEmpty(
            "every namespace-pinned negative must name an exact type or member under its real historical assembly owner");
        AssertTask76Disposition();
        var packageFeed = Environment.GetEnvironmentVariable(PublicApiBaseline.PackageFeedVariable);
        var findings = string.IsNullOrWhiteSpace(packageFeed)
            ? PublicApiBaseline.FindForbiddenPublicSymbols(ForbiddenPublicSymbols)
            : PublicApiBaseline.FindForbiddenPublicSymbolsInPackages(packageFeed, ForbiddenPublicSymbols);
        findings.Should().BeEmpty(
            "the exact baseline must never normalize removed, deferred, broad-management, or wrong-owner surfaces as approved");
    }

    private static void AssertHistoricalOwnerResolutionSemantics()
    {
        const string syntheticSource = """"
            namespace Historical.Owner;

            internal sealed class WrongType
            {
                private const string Braces = "{ not a body }";
                internal void ExpectedMember() { }
            }

            internal sealed class ExpectedType
            {
                private const string RawBraces = """{ still not a body }""";
                private const string Decoy = "StringOnly";
                // CommentOnly() { }
                internal void ActualMember(CancellationToken cancellationToken = default) { }
            }

            internal enum ExpectedEnum
            {
                ActualValue = 1,
            }
            """";
        const string expectedType = @"\bclass\s+ExpectedType\b";
        var maskedSource = MaskNonCode(syntheticSource);

        SourceContainsHistoricalOwner(
                maskedSource,
                "Historical.Owner",
                expectedType,
                "ActualMember")
            .Should().BeTrue("the member belongs to the exact named historical type");
        SourceContainsHistoricalOwner(
                maskedSource,
                "Historical.Owner",
                expectedType,
                "ExpectedMember")
            .Should().BeFalse("a member on a sibling type must not satisfy the qualified owner");
        SourceContainsHistoricalOwner(
                maskedSource,
                "Historical.Other",
                expectedType,
                "ActualMember")
            .Should().BeFalse("a declaration in another namespace must not satisfy the qualified owner");
        SourceContainsHistoricalOwner(maskedSource, "Historical.Owner", expectedType, "cancellationToken")
            .Should().BeFalse("a parameter token is not a declaration on the named type");
        SourceContainsHistoricalOwner(maskedSource, "Historical.Owner", expectedType, "RawBraces")
            .Should().BeTrue("real field declarations remain visible after literal masking");
        SourceContainsHistoricalOwner(maskedSource, "Historical.Owner", expectedType, "CommentOnly")
            .Should().BeFalse("comments cannot satisfy a historical member identity");
        SourceContainsHistoricalOwner(maskedSource, "Historical.Owner", expectedType, "StringOnly")
            .Should().BeFalse("string contents cannot satisfy a historical member identity");
        SourceContainsHistoricalOwner(maskedSource, "Historical.Owner", @"\benum\s+ExpectedEnum\b", "ActualValue")
            .Should().BeTrue("historical enum values are declarations too");
    }

    private static void AssertTask76Disposition()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var ledger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "openspec/changes/harmonize-downstream-capability-specs/tasks.md")));
        var task = Regex.Match(
            ledger,
            @"(?ms)^- \[x\] 7\.6 .*?(?=^- \[[ xX]\] 7\.7 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 7.6 must retain its completed exact-owner disposition");
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task76CompletionDecision);
        task.Value.Should().Contain($"`{Task76Artifact}`");
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task76ReviewRemediationDecision);
        task.Value.Should().Contain($"`{Task76ReviewRemediationArtifact}`");

        var design = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "openspec/changes/harmonize-downstream-capability-specs/design.md")));
        design.Should().Contain(Task76DesignDecision);
        design.Should().Contain(
            "The machine-readable 122-entry inventory retains reshape Task 7.17 as its removal owner.");

        var artifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task76Artifact.Replace('/', Path.DirectorySeparatorChar))));
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(artifact)))
            .ToLowerInvariant()
            .Should().Be(Task76ArtifactSha256);
        artifact.Should().ContainAll(
            "ten ephemeral management types",
            "IWorkflowPayloadCodec",
            "never existed under the recorded provider namespace",
            "122 exact identities",
            "remains owned by reshape Task 7.17");
        var remediation = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task76ReviewRemediationArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(remediation)))
            .ToLowerInvariant()
            .Should().Be(Task76ReviewRemediationArtifactSha256);
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string[] FindForbiddenPublicSymbolsWithoutHistoricalOwner()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var archives = ForbiddenPublicSymbolOwnerCommits.ToDictionary(
            commit => commit,
            commit => ReadHistoricalSourceArchive(root, commit),
            StringComparer.Ordinal);

        return ForbiddenPublicSymbols
            .Where(identity => !HasHistoricalOwner(identity, archives))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasHistoricalOwner(
        string identity,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> archives)
    {
        var parts = identity.Split("::", StringSplitOptions.None);
        if (parts.Length is < 2 or > 3 ||
            !ForbiddenPublicSymbolAssemblyRoots.TryGetValue(parts[0], out var assemblyRoot))
        {
            return false;
        }

        var lastDot = parts[1].LastIndexOf('.');
        if (lastDot <= 0 || lastDot == parts[1].Length - 1) return false;
        var @namespace = parts[1][..lastDot];
        var metadataName = Regex.Replace(parts[1][(lastDot + 1)..], @"`\d+$", string.Empty);
        var declarationPattern =
            $@"\b(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+{Regex.Escape(metadataName)}\b";
        var memberName = parts.Length == 3 ? parts[2] : null;

        return archives.Values.Any(files => files
            .Where(file => file.Key.StartsWith(assemblyRoot, StringComparison.Ordinal))
            .Select(file => file.Value)
            .Any(source => SourceContainsHistoricalOwner(
                source,
                @namespace,
                declarationPattern,
                memberName)));
    }

    private static bool SourceContainsHistoricalOwner(
        string source,
        string @namespace,
        string declarationPattern,
        string? memberName)
    {
        var escapedNamespace = Regex.Escape(@namespace);
        var candidateRegions = new List<string>();
        if (Regex.IsMatch(
                source,
                $@"(?m)^\s*namespace\s+{escapedNamespace}\s*;",
                RegexOptions.CultureInvariant))
        {
            candidateRegions.Add(source);
        }

        foreach (Match namespaceDeclaration in Regex.Matches(
                     source,
                     $@"(?m)^\s*namespace\s+{escapedNamespace}\s*\{{",
                     RegexOptions.CultureInvariant))
        {
            var namespaceBodyStart = source.IndexOf('{', namespaceDeclaration.Index);
            var namespaceBodyEnd = FindMatchingBrace(source, namespaceBodyStart);
            if (namespaceBodyEnd > namespaceBodyStart)
            {
                candidateRegions.Add(source[(namespaceBodyStart + 1)..namespaceBodyEnd]);
            }
        }

        foreach (var candidateRegion in candidateRegions)
        {
            foreach (Match declaration in Regex.Matches(
                         candidateRegion,
                         declarationPattern,
                         RegexOptions.CultureInvariant))
            {
                if (memberName is null) return true;

                var bodyStart = candidateRegion.IndexOf('{', declaration.Index + declaration.Length);
                var declarationTerminator = candidateRegion.IndexOf(';', declaration.Index + declaration.Length);
                if (bodyStart < 0 || declarationTerminator >= 0 && declarationTerminator < bodyStart) continue;

                var bodyEnd = FindMatchingBrace(candidateRegion, bodyStart);
                if (bodyEnd < 0) continue;
                if (ContainsHistoricalMemberDeclaration(
                        candidateRegion[(bodyStart + 1)..bodyEnd],
                        memberName,
                        declaration.Value.Contains("enum", StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsHistoricalMemberDeclaration(string body, string memberName, bool isEnum)
    {
        var escapedName = Regex.Escape(memberName);
        var pattern = isEnum
            ? @"(?m)^[ \t]*(?:\[[^\r\n]+\][ \t]*)*" + escapedName + @"\b[ \t]*(?:=|,|(?=\r?$))"
            : @"(?m)^[ \t]*(?:\[[^\r\n]+\][ \t]*)*\b(?:public|internal|protected|private)\b[^\r\n;{}()]*\b" +
              escapedName + @"\b(?:<[^>\r\n]+>)?[ \t]*(?:\(|\{|=>|=|;)";
        return Regex.IsMatch(body, pattern, RegexOptions.CultureInvariant);
    }

    private static string MaskNonCode(string source)
    {
        var masked = source.ToCharArray();
        static void Hide(char[] target, int index)
        {
            if (target[index] is not ('\r' or '\n')) target[index] = ' ';
        }

        for (var index = 0; index < source.Length;)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (current == '/' && next == '/')
            {
                do
                {
                    Hide(masked, index++);
                } while (index < source.Length && source[index] != '\n');
                continue;
            }

            if (current == '/' && next == '*')
            {
                Hide(masked, index++);
                Hide(masked, index++);
                while (index < source.Length)
                {
                    if (source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')
                    {
                        Hide(masked, index++);
                        Hide(masked, index++);
                        break;
                    }

                    Hide(masked, index++);
                }

                continue;
            }

            if (current == '"')
            {
                var quoteCount = 1;
                while (index + quoteCount < source.Length && source[index + quoteCount] == '"') quoteCount++;
                if (quoteCount >= 3)
                {
                    var closingCount = quoteCount;
                    for (var count = 0; count < quoteCount; count++) Hide(masked, index++);
                    while (index < source.Length)
                    {
                        if (source[index] == '"')
                        {
                            var run = 1;
                            while (index + run < source.Length && source[index + run] == '"') run++;
                            if (run >= closingCount)
                            {
                                for (var count = 0; count < closingCount; count++) Hide(masked, index++);
                                break;
                            }
                        }

                        Hide(masked, index++);
                    }

                    continue;
                }

                var verbatim = index > 0 && (source[index - 1] == '@' ||
                    index > 1 && source[index - 1] == '$' && source[index - 2] == '@');
                Hide(masked, index++);
                while (index < source.Length)
                {
                    if (verbatim && source[index] == '"' && index + 1 < source.Length && source[index + 1] == '"')
                    {
                        Hide(masked, index++);
                        Hide(masked, index++);
                        continue;
                    }

                    var closes = source[index] == '"' && (verbatim || !IsEscaped(source, index));
                    Hide(masked, index++);
                    if (closes) break;
                }

                continue;
            }

            if (current == '\'')
            {
                Hide(masked, index++);
                while (index < source.Length)
                {
                    var closes = source[index] == '\'' && !IsEscaped(source, index);
                    Hide(masked, index++);
                    if (closes) break;
                }

                continue;
            }

            index++;
        }

        return new string(masked);
    }

    private static int FindMatchingBrace(string source, int openingBrace)
    {
        var depth = 0;
        var inLineComment = false;
        var inBlockComment = false;
        var inString = false;
        var inCharacter = false;
        var verbatimString = false;
        var rawStringQuoteCount = 0;

        for (var index = openingBrace; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n') inLineComment = false;
                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    index++;
                }

                continue;
            }

            if (rawStringQuoteCount > 0)
            {
                if (current != '"') continue;
                var closingQuoteCount = 1;
                while (index + closingQuoteCount < source.Length &&
                       source[index + closingQuoteCount] == '"')
                {
                    closingQuoteCount++;
                }

                if (closingQuoteCount >= rawStringQuoteCount)
                {
                    index += rawStringQuoteCount - 1;
                    rawStringQuoteCount = 0;
                }

                continue;
            }

            if (inString)
            {
                if (verbatimString && current == '"' && next == '"')
                {
                    index++;
                    continue;
                }

                if (current == '"' && (verbatimString || !IsEscaped(source, index)))
                {
                    inString = false;
                    verbatimString = false;
                }

                continue;
            }

            if (inCharacter)
            {
                if (current == '\'' && !IsEscaped(source, index)) inCharacter = false;
                continue;
            }

            if (current == '/' && next == '/')
            {
                inLineComment = true;
                index++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                index++;
                continue;
            }

            if (current == '"')
            {
                var quoteCount = 1;
                while (index + quoteCount < source.Length && source[index + quoteCount] == '"') quoteCount++;
                if (quoteCount >= 3)
                {
                    rawStringQuoteCount = quoteCount;
                    index += quoteCount - 1;
                }
                else
                {
                    inString = true;
                    verbatimString = index > 0 &&
                                     (source[index - 1] == '@' ||
                                      index > 1 && source[index - 1] == '$' && source[index - 2] == '@');
                }

                continue;
            }

            if (current == '\'')
            {
                inCharacter = true;
                continue;
            }

            if (current == '{') depth++;
            if (current != '}') continue;
            depth--;
            if (depth == 0) return index;
        }

        return -1;
    }

    private static bool IsEscaped(string source, int index)
    {
        var slashCount = 0;
        for (var cursor = index - 1; cursor >= 0 && source[cursor] == '\\'; cursor--) slashCount++;
        return slashCount % 2 != 0;
    }

    private static IReadOnlyDictionary<string, string> ReadHistoricalSourceArchive(string root, string commit)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("archive");
        startInfo.ArgumentList.Add("--format=tar");
        startInfo.ArgumentList.Add(commit);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("src");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git archive for forbidden-symbol owner evidence.");
        using var archive = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(archive);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git archive failed for historical forbidden-symbol owner commit '{commit}': " +
                process.StandardError.ReadToEnd());
        }

        archive.Position = 0;
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var reader = new TarReader(archive))
        {
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.EntryType != TarEntryType.RegularFile ||
                    entry.DataStream is null ||
                    !entry.Name.EndsWith(".cs", StringComparison.Ordinal))
                {
                    continue;
                }

                using var sourceReader = new StreamReader(entry.DataStream, leaveOpen: true);
                files.Add(entry.Name.Replace('\\', '/'), MaskNonCode(sourceReader.ReadToEnd()));
            }
        }

        return files;
    }

    [Fact]
    public void PackedConsumerNegativeFixture_CoversEveryForbiddenSymbolExactlyOnce()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var sourcePath = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards", "CompileFixtures",
            "ProductForbiddenLegacySurface", "ForbiddenLegacySurface.cs");
        var projectPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, "ProductForbiddenLegacySurface.csproj");
        var sourceLines = File.ReadAllLines(sourcePath);
        var markers = sourceLines
            .Select(line => new { markerIndex = line.IndexOf(ForbiddenProbeMarker, StringComparison.Ordinal), line })
            .Where(item => item.markerIndex >= 0)
            .Select(item => new
            {
                item.line,
                marker = item.line[(item.markerIndex + ForbiddenProbeMarker.Length)..].Trim()
            })
            .Select(item =>
            {
                item.line.Trim().Should().Be($"{ForbiddenProbeMarker}{item.marker}",
                    "the marker must be the single source of truth for its generated compiler probe");
                return item.marker;
            })
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expected = ForbiddenPublicSymbols
            .Concat(RequiredRemovedPlaceholders)
            .Concat(ForbiddenManifestWideTypeNames.Select(name => $"*::{name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        markers.Should().OnlyHaveUniqueItems();
        markers.Should().Equal(expected,
            "fresh-package compiler evidence must cover every reflection-negative symbol and every removed result bridge");

        PublicSurfaceCatalog.ExportedTypes.Select(item => item.Type.FullName)
            .Should().NotContain(ForbiddenManifestWideTypeNames,
                "obsolete hosting and codec hooks must not move into another current package");
        PublicSurfaceCatalog.Assemblies.Should().HaveCount(PublicSurfaceCatalog.TargetAssemblyNames.Count,
            "manifest-wide absence claims must inspect every exact package assembly");
        var fixturePackageReferences = System.Xml.Linq.XDocument.Load(projectPath)
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
        fixturePackageReferences.Should().Equal(
            PublicSurfaceCatalog.TargetAssemblyNames.Order(StringComparer.Ordinal),
            "manifest-wide packed-consumer claims must reference all twelve exact packages");
    }

    [Fact]
    public void ObsoleteEventNegatives_ArePairedWithThePositiveSection7BRouteUnion()
    {
        var route = PublicSurfaceCatalog.Assemblies.SelectMany(assembly => assembly.GetExportedTypes())
            .Single(type => type.FullName == "OrcaCore.WorkflowEventRoute");
        route.GetNestedTypes(BindingFlags.Public)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .Should().Equal("Correlation", "DefinitionFanout", "Direct", "StartOrDeliver`1");
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
    public void PackageSourceComparator_RejectsSourceAndAssemblyDrift()
    {
        var expected = new PackageSourceProvenanceRecord(
            PackageSourceProvenance.FormatVersion,
            "0.0.0-phase0",
            [new PackageSourceProvenanceEntry("OrcaCore", "src/OrcaCore.Abstractions/OrcaCore.csproj", "source-a")]);
        var actual = expected with
        {
            Packages =
            [
                expected.Packages[0] with
                {
                    SourceSha256 = "source-b"
                }
            ]
        };

        PackageSourceProvenance.Diff(expected, actual).Should()
            .Contain("OrcaCore: source SHA-256 differs.");

        Action compareStalePackage = () => PackageSourceProvenance.EnsureAssemblyHashesMatch(
            "OrcaCore", "current-assembly", "stale-package-assembly");
        compareStalePackage.Should().Throw<InvalidOperationException>()
            .WithMessage("Package 'OrcaCore' is stale:*");
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
