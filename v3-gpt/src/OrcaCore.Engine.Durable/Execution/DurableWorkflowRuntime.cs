using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Public durable workflow facade for definition registration and version-bound starts.
/// </summary>
public sealed class DurableWorkflowRuntime(
    DurableCommandProcessor commandProcessor,
    DurableDefinitionRegistry definitions,
    TimeProvider timeProvider)
{
    private readonly DurableStartService startService = new(commandProcessor);

    /// <summary>
    /// Registers one durable workflow definition version.
    /// </summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        definitions.Register(definition);
    }

    /// <summary>
    /// Starts a registered workflow definition or returns the instance previously started with the same key.
    /// </summary>
    public async Task<DurableWorkflowStartResult> StartOrGetAsync<TInput, TState>(
        string idempotencyKey,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        definitions.Resolve<TState>(definitionId, definitionVersion);

        var result = await startService
            .StartOrGetAsync(
                new StartOrGetRequest(
                    idempotencyKey,
                    definitionId,
                    definitionVersion,
                    input,
                    timeProvider.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);
        return new DurableWorkflowStartResult(
            result.InstanceId,
            definitionId,
            definitionVersion,
            result.Created);
    }

    /// <summary>
    /// Starts the supplied definition version or returns the instance previously started with the same key.
    /// </summary>
    public async Task<DurableWorkflowStartResult> StartOrGetAsync<TInput, TState>(
        string idempotencyKey,
        WorkflowDefinition<TState> definition,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RegisterDefinition(definition);

        return await StartOrGetAsync<TInput, TState>(
            idempotencyKey,
            definition.DefinitionId,
            definition.DefinitionVersion,
            input,
            cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Result of a durable start-or-get request.
/// </summary>
public sealed record DurableWorkflowStartResult(
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    bool Created);
