using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Hosting.ResourceLeases;

/// <summary>Provides idempotent management of the serialized durable resource aggregate.</summary>
public interface IDurableResourcePoolManagement
{
    ValueTask<IReadOnlyList<DurableResourcePoolSnapshot>> ListAsync(
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourcePoolSnapshot> GetAsync(
        ResourcePoolName pool,
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourcePoolResizeResult> ResizeAsync(
        ResourcePoolName pool,
        int capacity,
        ResourcePoolOperationId operationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Reconciles one exact quarantined durable lease after protected work is proven stopped.</summary>
public interface IDurableResourceLeaseRecovery
{
    ValueTask<ProtectedWorkStopConfirmationStatus> ConfirmProtectedWorkStoppedAsync(
        LeaseProtectionToken protectionToken,
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Advanced detached diagnostics for outstanding durable lease obligations.</summary>
public interface IDurableResourceLeaseDiagnostics
{
    IAsyncEnumerable<DurableResourceLeaseObligationSnapshot> EnumerateOutstandingAsync(
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourceLeaseObligationSnapshot?> GetAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default);
}
