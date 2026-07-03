using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Explicit interim adapter for hosts that execute SagaDefinition steps and commit their durable audit facts.
/// </summary>
public sealed class DurableSagaCommandAdapter<TState>(SagaDefinition<TState> definition)
{
    /// <summary>
    /// Gets the durable forward-action command for a successfully executed saga action.
    /// </summary>
    public RecordSagaForwardActionCompletedCommand ForwardActionCompleted(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        string actionKey)
    {
        var action = FindAction(actionKey);
        return new RecordSagaForwardActionCompletedCommand
        {
            CommandId = commandId,
            InstanceId = instanceId,
            RequestedAt = requestedAt,
            ScopeId = action.ScopeId,
            ActionKey = action.ActionKey,
            CompensationKey = action.Compensation is null
                ? string.Empty
                : $"{action.ActionKey}/compensation"
        };
    }

    /// <summary>
    /// Gets the durable compensation request command for a saga scope.
    /// </summary>
    public RequestSagaCompensationCommand RequestCompensation(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        string scopeId,
        string? reason = null)
    {
        if (!definition.CompensationScopes.Any(scope =>
            string.Equals(scope.ScopeId, scopeId, StringComparison.Ordinal)) &&
            !definition.ForwardActions.Any(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Saga scope '{scopeId}' is not declared by this definition.", nameof(scopeId));
        }

        return new RequestSagaCompensationCommand
        {
            CommandId = commandId,
            InstanceId = instanceId,
            RequestedAt = requestedAt,
            ScopeId = scopeId,
            Reason = reason
        };
    }

    /// <summary>
    /// Gets the durable compensation completion command for a compensating action.
    /// </summary>
    public CompleteSagaCompensationCommand CompensationCompleted(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        string compensationActionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compensationActionKey);
        return new CompleteSagaCompensationCommand
        {
            CommandId = commandId,
            InstanceId = instanceId,
            RequestedAt = requestedAt,
            ScopeId = FindScopeForCompensation(compensationActionKey),
            ActionKey = compensationActionKey
        };
    }

    private SagaForwardAction<TState> FindAction(string actionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionKey);
        return definition.ForwardActions.SingleOrDefault(action =>
            string.Equals(action.ActionKey, actionKey, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Saga action '{actionKey}' is not declared by this definition.", nameof(actionKey));
    }

    private string FindScopeForCompensation(string compensationActionKey)
    {
        var actionKey = compensationActionKey.EndsWith("/compensation", StringComparison.Ordinal)
            ? compensationActionKey[..^"/compensation".Length]
            : compensationActionKey;
        return FindAction(actionKey).ScopeId;
    }
}
