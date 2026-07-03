using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;

namespace OrcaCore.Core.Definitions;

/// <summary>
/// Immutable handle for one compensation-aware saga definition version.
/// </summary>
public sealed record SagaDefinition<TState>
{
    internal SagaDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        Func<object?, TState> createState,
        IReadOnlyList<SagaForwardAction<TState>> forwardActions,
        IReadOnlyList<SagaCompensationScope> compensationScopes)
    {
        ArgumentNullException.ThrowIfNull(createState);
        ArgumentNullException.ThrowIfNull(forwardActions);
        ArgumentNullException.ThrowIfNull(compensationScopes);

        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        CreateState = createState;
        ForwardActions = forwardActions.ToArray();
        CompensationScopes = compensationScopes.ToArray();
    }

    /// <summary>
    /// Gets the stable saga definition identity.
    /// </summary>
    public DefinitionId DefinitionId { get; }

    /// <summary>
    /// Gets the immutable version represented by this saga definition.
    /// </summary>
    public DefinitionVersion DefinitionVersion { get; }

    internal Func<object?, TState> CreateState { get; }

    /// <summary>
    /// Gets ordered forward actions declared by the saga.
    /// </summary>
    public IReadOnlyList<SagaForwardAction<TState>> ForwardActions { get; }

    /// <summary>
    /// Gets named compensation scopes declared by the saga.
    /// </summary>
    public IReadOnlyList<SagaCompensationScope> CompensationScopes { get; }
}

/// <summary>
/// Immutable metadata for one compensatable saga forward action.
/// </summary>
public sealed record SagaForwardAction<TState>(
    string ActionKey,
    string ScopeId,
    Type? StepType,
    Func<IStep<TState>> StepFactory,
    SagaCompensationAction<TState>? Compensation);

/// <summary>
/// Immutable metadata for one saga compensating action.
/// </summary>
public sealed record SagaCompensationAction<TState>(
    Type? StepType,
    Func<IStep<TState>> StepFactory);

/// <summary>
/// Immutable metadata for one saga compensation scope.
/// </summary>
public sealed record SagaCompensationScope(
    string ScopeId,
    IReadOnlyList<string> ForwardActionKeys);
