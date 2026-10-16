namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class InterpreterRunState<TState>
{
    internal required Action<WorkflowInstance<TState>> OnInitialized { get; init; }

    internal bool Initialized { get; set; }

    internal WorkflowInstance<TState>? Instance { get; set; }

    internal Exception? DeferredFailure { get; set; }

    internal Exception? TakeDeferredFailure()
    {
        var failure = DeferredFailure;
        DeferredFailure = null;
        return failure;
    }
}
