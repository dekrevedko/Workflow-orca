using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal sealed class WaitLongStep<TState> : IStep<TState>
{
    private readonly string _eventName;

    public WaitLongStep(string eventName, Func<TState, string> correlationSelector)
    {
        _eventName = eventName;
        _ = correlationSelector; // stored for future durable mode support
    }

    public string StepId => $"WaitLong({_eventName})";

    public Task<StepResult> ExecuteAsync(StepContext<TState> context)
    {
        throw new InvalidOperationException(
            $"WaitLong requires durable mode. The current engine is ephemeral-only. " +
            $"Use Wait for ephemeral workflows or switch to a durable engine configuration.");
    }
}
