using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Outbox;

/// <summary>
/// Dispatches durable outbox records to a dispatcher selected by <see cref="OutboxWrite.Kind" />.
/// </summary>
public sealed class OutboxKindMessageDispatcher : IMessageDispatcher
{
    private readonly IReadOnlyDictionary<string, IMessageDispatcher> routes;
    private readonly IMessageDispatcher? fallbackDispatcher;

    public OutboxKindMessageDispatcher(
        IEnumerable<OutboxKindDispatcherRoute> routes,
        IMessageDispatcher? fallbackDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var routeMap = new Dictionary<string, IMessageDispatcher>(StringComparer.Ordinal);
        foreach (var route in routes)
        {
            if (!routeMap.TryAdd(route.Kind, route.Dispatcher))
            {
                throw new ArgumentException(
                    $"A dispatcher route for outbox kind '{route.Kind}' is already registered.",
                    nameof(routes));
            }
        }

        this.routes = routeMap;
        this.fallbackDispatcher = fallbackDispatcher;
    }

    public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (routes.TryGetValue(record.Kind, out var dispatcher))
        {
            return dispatcher.DispatchAsync(record, cancellationToken);
        }

        return fallbackDispatcher is null
            ? Task.FromResult(DispatchResult.PermanentFailure)
            : fallbackDispatcher.DispatchAsync(record, cancellationToken);
    }
}

public sealed record OutboxKindDispatcherRoute
{
    public OutboxKindDispatcherRoute(string kind, IMessageDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(dispatcher);

        Kind = kind;
        Dispatcher = dispatcher;
    }

    public string Kind { get; }

    public IMessageDispatcher Dispatcher { get; }
}
