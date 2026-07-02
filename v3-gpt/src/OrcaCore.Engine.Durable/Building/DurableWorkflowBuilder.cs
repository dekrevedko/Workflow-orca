using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Durable.Building;

internal sealed class DurableWorkflowBuilder<TState>
{
    private readonly List<DurableWaitDefinition<TState>> waits = [];

    internal IReadOnlyList<DurableWaitDefinition<TState>> Waits => waits;

    internal DurableWorkflowBuilder<TState> WaitLong(
        string eventName,
        Func<TState, CorrelationId> correlationSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationSelector);

        waits.Add(new DurableWaitDefinition<TState>(
            eventName,
            correlationSelector,
            WaitMode.Cold));
        return this;
    }
}

internal sealed record DurableWaitDefinition<TState>(
    string EventName,
    Func<TState, CorrelationId> CorrelationSelector,
    WaitMode Mode);
