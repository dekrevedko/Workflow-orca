using OrcaCore.Abstractions.Ids;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Engine.Durable.ResourceGovernance;

internal enum DurableResourceLeaseCommitBarrier
{
    WorkflowPendingObligationCommitted,
    GovernanceReservationCommitted,
    WorkflowActivationCommitted,
    GovernanceOwnershipConfirmed
}

internal sealed record DurableResourceLeaseCommitBarrierFact(
    DurableResourceLeaseCommitBarrier Barrier,
    ResourceGovernancePartitionId PartitionId,
    string ObligationId,
    InstanceId InstanceId,
    int Generation,
    string FiberOccurrence,
    string ScopeOccurrence,
    LeaseProtectionToken LeaseProtectionToken,
    long WorkflowVersion,
    long GovernanceVersion,
    IReadOnlyList<DurableResourceLeaseTicketSnapshot> Tickets);

internal interface IDurableResourceLeaseCertificationGate
{
    ValueTask OnPostCommitAsync(
        DurableResourceLeaseCommitBarrierFact fact,
        CancellationToken cancellationToken = default);
}

internal sealed class NullDurableResourceLeaseCertificationGate
    : IDurableResourceLeaseCertificationGate
{
    internal static NullDurableResourceLeaseCertificationGate Instance { get; } = new();

    private NullDurableResourceLeaseCertificationGate()
    {
    }

    public ValueTask OnPostCommitAsync(
        DurableResourceLeaseCommitBarrierFact fact,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
