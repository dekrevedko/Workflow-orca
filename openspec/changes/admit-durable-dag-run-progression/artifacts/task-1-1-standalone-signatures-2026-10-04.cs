// Signature-only proof; NOT product implementation or an additional exported port.
// Compile with only the shipped OrcaCore.dll reference; net10.0, nullable/implicit usings.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrcaCore;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers.Dag;
namespace OrcaCore.Abstractions.Durable.Dag
{
public sealed record DagRunValueV1(
    bool IsPresent, string ContractKey, string CodecFormat,
    byte[] Bytes, PayloadFingerprint Fingerprint);
public sealed record DagRunBindingV1(
    DefinitionId DefinitionId, DefinitionVersion DefinitionVersion,
    DefinitionFingerprint Fingerprint);
public sealed record DagRunNodeV1(
    int Ordinal, string NodeId, DagRunBindingV1 Workflow,
    string InputContractKey, string? OutputContractKey,
    IReadOnlyList<int> DirectDependencies);
public sealed record DagRunGraphV1(
    DagRunBindingV1 Definition, string RunInputContractKey,
    IReadOnlyList<DagRunNodeV1> Nodes);
public sealed record DagRunStartKeyV1(string Value);
public enum DagRunStateV1 { Running, CancellationRequested, Succeeded, Failed, Cancelled, IntegrityFault }
public enum DagNodeStateV1 { Pending, Ready, StartAuthorized, Running, CancellationRequested, Succeeded, Failed, Cancelled, DependencyBlocked }
public sealed record DagRunNodeStateV1(
    int Ordinal, DagNodeStateV1 State, InstanceId? ChildInstanceId,
    DagRunValueV1? Input, DagRunValueV1? Output,
    string? FailureCode, DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
public sealed record DagRunSnapshotV1(
    Guid RunId, StreamVersion StreamVersion, DagRunStartKeyV1 StartKey,
    DagRunGraphV1 Graph, DagRunValueV1 RunInput,
    DagRunStateV1 State, IReadOnlyList<DagRunNodeStateV1> Nodes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
public enum DagRunRecordKindV1 { Created, InputAdmitted, MappingFailed, ChildJoined, CancellationRequested, Terminal, IntegrityFaulted, PoisonReconciled }
public sealed record DagRunRecordV1(
    int Version, DagRunRecordKindV1 Kind, Guid RunId, Guid OperationId,
    StreamVersion StreamVersion, DagRunSnapshotV1 Snapshot);
public sealed record DagRunCheckpointV1(
    int Version, Guid RunId, StreamVersion StreamVersion,
    string ContentType, byte[] Payload, string Sha256);

}
namespace OrcaCore.Abstractions.Providers.Dag
{
public sealed record DagRunFenceV1(Guid RunId, Guid OwnerId, long Token);
public sealed record DagRunAppendV1(
    Guid OperationId, string OperationFingerprint,
    DagRunFenceV1? Fence, IReadOnlyList<DagRunRecordV1> Records,
    DagRunCheckpointV1 Checkpoint);
public sealed record DagRunLeaseRequestV1(
    Guid RunId, Guid OwnerId, long? ExpectedFence, TimeSpan LeaseDuration);
public enum DagRunLeaseOutcomeV1 { Granted, HeldByOther, StaleFence, NotFound, Terminal }
public sealed record DagRunLeaseResultV1(
    DagRunLeaseOutcomeV1 Outcome, DagRunFenceV1? Fence,
    DateTimeOffset? ExpiresAt, StreamVersion? CurrentVersion);
public sealed record DagRunDiscoveryRequestV1(long AfterCreationSequence, int MaxCount);
public sealed record DagRunDiscoveryV1(
    long CreationSequence, Guid RunId, StreamVersion StreamVersion,
    DagRunBindingV1 Definition, DagRunStateV1 State);
public sealed record DagRunSubscriptionV1(
    Guid SubscriptionId, Guid RecipientHostId, Guid RunId,
    StreamVersion AfterVersion, TimeSpan LeaseDuration);
public enum DagRunSubscribeOutcomeV1 { Subscribed, AlreadyChanged, NotFound }
public sealed record DagRunSubscribeResultV1(
    DagRunSubscribeOutcomeV1 Outcome, StreamVersion? CurrentVersion);
}
namespace ContractSignatureProof
{
// Wrapper exists only to compile the nine proposed method declarations.
internal interface ProposedExistingPortAdditions
{
Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(
    WorkflowStreamId streamId, CancellationToken cancellationToken);
Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(
    DagRunStartKeyV1 startKey, CancellationToken cancellationToken);
Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(
    WorkflowStreamId streamId, StreamVersion afterVersion, CancellationToken cancellationToken);
Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(
    DagRunLeaseRequestV1 request, CancellationToken cancellationToken);
Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(
    DagRunLeaseRequestV1 request, CancellationToken cancellationToken);
Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(
    DagRunFenceV1 fence, CancellationToken cancellationToken);
Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(
    DagRunDiscoveryRequestV1 request, CancellationToken cancellationToken);
Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(
    DagRunSubscriptionV1 subscription, CancellationToken cancellationToken);
Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId,
    CancellationToken cancellationToken);
}
}
namespace OrcaCore.Durable.Hosting.Dag
{
internal readonly record struct DurableDagNodeIdentityV1(Guid RunId, int AuthoredOrdinal);

internal sealed record DurableDagWorkflowBindingV1(
    DefinitionId DefinitionId, DefinitionVersion DefinitionVersion,
    DefinitionFingerprint DefinitionFingerprint, Type InputType, Type? OutputType);

internal sealed record DurableDagDependencyV1(
    DurableDagNodeIdentityV1 Identity, DurableDagWorkflowBindingV1 Workflow);

internal sealed record DurableDagNodeContractV1(
    DurableDagNodeIdentityV1 Identity, DurableDagWorkflowBindingV1 Workflow,
    IReadOnlyList<DurableDagDependencyV1> DirectDependencies);

internal sealed record DurableDagDecodedOutputV1(
    DurableDagNodeIdentityV1 Identity, Type DeclaredType, object? Value);

internal abstract record DurableDagMappingInputsV1
{
    private protected DurableDagMappingInputsV1() { }
    internal sealed record Available(
        object? RunInput, Type RunInputType,
        IReadOnlyList<DurableDagDecodedOutputV1> Outputs) : DurableDagMappingInputsV1;
    internal sealed record MappingInvalid : DurableDagMappingInputsV1;
}

internal sealed record DurableDagMappedInputV1(object? Value, Type DeclaredType);

internal sealed record DurableDagInputReceiptV1(
    DurableDagNodeIdentityV1 Identity, InstanceId ChildInstanceId,
    PayloadFingerprint MappedInputFingerprint);

internal abstract record DurableDagInputCommitResultV1
{
    private protected DurableDagInputCommitResultV1() { }
    internal sealed record Committed(
        DurableDagInputReceiptV1 Receipt, bool WasExisting) : DurableDagInputCommitResultV1;
    internal sealed record MappingInvalid : DurableDagInputCommitResultV1;
    internal sealed record AdmissionStopped : DurableDagInputCommitResultV1;
    internal sealed record CapacityDeferred : DurableDagInputCommitResultV1;
}

internal abstract record DurableDagChildStartResultV1
{
    private protected DurableDagChildStartResultV1() { }
    internal sealed record Started(
        InstanceId ChildInstanceId, bool WasExisting) : DurableDagChildStartResultV1;
    internal sealed record CancelledBeforeStart : DurableDagChildStartResultV1;
}

internal sealed record DurableDagChildSnapshotV1(
    InstanceId ChildInstanceId, InstanceId RootInstanceId, InstanceId ParentInstanceId,
    WorkflowInstanceStatus Status, bool HasOutput, WorkflowFailure? Failure);

internal interface IDurableDagChildBridgeV1
{
    ValueTask<DurableDagMappingInputsV1> ReadMappingInputsAsync(
        DurableDagNodeContractV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagInputCommitResultV1> CommitMappedInputAsync(
        DurableDagNodeContractV1 node, DurableDagMappedInputV1 input,
        CancellationToken cancellationToken);
    ValueTask<DurableDagChildStartResultV1> StartOrReattachAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagChildSnapshotV1?> GetChildAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagChildSnapshotV1> WaitForChildTerminalAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<WorkflowCancellationRequestStatus> RequestChildCancellationAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
}
internal sealed record DurableDagRunCreateV1(
    Guid RunId, DagRunStartKeyV1 StartKey, DagRunGraphV1 Graph, object? Input, Type InputType);
internal enum DurableDagCreateOutcomeV1 { Created, Existing, BindingConflict }
internal sealed record DurableDagCreateResultV1(
    DurableDagCreateOutcomeV1 Outcome, DagRunSnapshotV1? Snapshot);
internal enum DurableDagMutationOutcomeV1 { Committed, Existing, CapacityDeferred, AdmissionStopped, StaleFence, VersionConflict, MappingInvalid, IntegrityConflict, NotFound }
internal sealed record DurableDagMutationResultV1(
    DurableDagMutationOutcomeV1 Outcome, StreamVersion? Version,
    DagRunSnapshotV1? Snapshot);
internal enum DurableDagCancelOutcomeV1 { Requested, AlreadyRequested, AlreadyTerminal, NotFound }
internal sealed record DurableDagCancelResultV1(
    DurableDagCancelOutcomeV1 Outcome, StreamVersion? Version);
internal sealed record DurableDagReadyNoticeV1(
    Guid RunId, StreamVersion Version, Guid? SubscriptionId);
internal interface IDurableDagReadySinkV1
{
    ValueTask HandleAsync(DurableDagReadyNoticeV1 notice, CancellationToken cancellationToken);
}
internal interface IDurableDagRunBridgeV1
{
    ValueTask<DurableDagCreateResultV1> CreateOrReopenAsync(
        DurableDagRunCreateV1 run, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1?> ReadAsync(Guid runId, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> AdmitMappedInputAsync(
        DurableDagNodeContractV1 node, DurableDagMappedInputV1 input,
        DagRunFenceV1 fence, StreamVersion expectedVersion, int maxConcurrentNodes,
        CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> CommitMappingFailureAsync(
        Guid runId, int ordinal, DagRunFenceV1 fence, StreamVersion expectedVersion,
        CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> ApplyChildTerminalAsync(
        Guid runId, int ordinal, CancellationToken cancellationToken);
    ValueTask<DurableDagCancelResultV1> RequestRunCancellationAsync(
        Guid runId, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1> WaitForChangeAsync(
        Guid runId, StreamVersion afterVersion, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1> WaitForTerminalAsync(
        Guid runId, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> RecordIntegrityFaultAsync(
        Guid runId, string code, DagRunFenceV1 fence, StreamVersion expectedVersion,
        CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> ReconcilePoisonAsync(
        Guid runId, Guid operationId, DagRunFenceV1 fence, StreamVersion expectedVersion,
        CancellationToken cancellationToken);
}
}
