using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Hosting.ResourceLeases;

internal sealed class HostedDurableResourceLeaseRecovery(
    DurableResourceLeaseRecovery runtime) : IDurableResourceLeaseRecovery
{
    public ValueTask<ProtectedWorkStopConfirmationStatus> ConfirmProtectedWorkStoppedAsync(
        LeaseProtectionToken protectionToken,
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default) =>
        runtime.ConfirmProtectedWorkStoppedAsync(
            protectionToken,
            confirmationId,
            cancellationToken);
}

internal sealed class HostedDurableResourceLeaseDiagnostics(
    DurableResourceLeaseDiagnostics runtime) : IDurableResourceLeaseDiagnostics
{
    public IAsyncEnumerable<DurableResourceLeaseObligationSnapshot> EnumerateOutstandingAsync(
        CancellationToken cancellationToken = default) =>
        runtime.EnumerateOutstandingAsync(cancellationToken);

    public ValueTask<DurableResourceLeaseObligationSnapshot?> GetAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default) =>
        runtime.GetAsync(protectionToken, cancellationToken);
}
