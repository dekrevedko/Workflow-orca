using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Hosting.ResourceLeases;

internal sealed class DurableResourcePoolManagement : IDurableResourcePoolManagement
{
    private readonly SerializedResourceGovernanceAggregate aggregate;

    internal DurableResourcePoolManagement(
        IDurableResourceGovernanceStore store,
        DurableResourcePoolOptions options)
        : this(new SerializedResourceGovernanceAggregate(store, options))
    {
    }

    internal DurableResourcePoolManagement(SerializedResourceGovernanceAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        this.aggregate = aggregate;
    }

    internal ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        aggregate.InitializeAsync(cancellationToken);

    public ValueTask<IReadOnlyList<DurableResourcePoolSnapshot>> ListAsync(
        CancellationToken cancellationToken = default) =>
        aggregate.ListManagementAsync(cancellationToken);

    public ValueTask<DurableResourcePoolSnapshot> GetAsync(
        ResourcePoolName pool,
        CancellationToken cancellationToken = default) =>
        aggregate.GetManagementAsync(pool, cancellationToken);

    public ValueTask<DurableResourcePoolResizeResult> ResizeAsync(
        ResourcePoolName pool,
        int capacity,
        ResourcePoolOperationId operationId,
        CancellationToken cancellationToken = default) =>
        aggregate.ResizeManagementAsync(pool, capacity, operationId, cancellationToken);
}
