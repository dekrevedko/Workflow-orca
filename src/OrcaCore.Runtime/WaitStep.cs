using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal sealed class WaitStep<TState>(string eventName, Func<TState, string> correlationSelector) : IStep<TState>
{
    public string StepId => $"Wait({eventName})";

    public Task<StepResult> ExecuteAsync(StepContext<TState> context)
    {
        var correlationId = correlationSelector(context.State);
        return Task.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }
}
