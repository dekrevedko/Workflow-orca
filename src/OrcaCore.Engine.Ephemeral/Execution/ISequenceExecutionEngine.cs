namespace OrcaCore.Engine.Ephemeral.Execution;

internal interface ISequenceExecutionEngine<TState>
{
    Task<bool> RunSequenceAsync<TInput>(
        SequenceExecutionContext<TState, TInput> context,
        int startIndex,
        CancellationToken cancellationToken,
        bool deferStepFailures = false);

    Task ContinueSequenceAsync<TInput>(
        SequenceExecutionContext<TState, TInput> context,
        int startIndex,
        CancellationToken cancellationToken);

    void Fail(
        WorkflowInstance<TState> instance,
        Exception exception,
        string stepPath);
}
