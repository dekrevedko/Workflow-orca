using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// Builds compensation-aware saga definitions as a separate semantic kind.
/// </summary>
public sealed class SagaBuilder<TState>
{
    private const string RootScopeId = "root";

    private readonly List<SagaActionBuilderNode> actions = [];
    private readonly List<string> declaredScopes = [];
    private Func<object?, TState>? createState;
    private bool hasNullInit;
    private bool hasEnd;
    private string currentScopeId = RootScopeId;

    /// <summary>
    /// Adds the Init node that converts start input into saga business state.
    /// </summary>
    public SagaBuilder<TState> Init<TInput>(Func<TInput, TState>? createState)
    {
        this.createState = input => createState!((TInput)input!);
        hasNullInit = createState is null;
        return this;
    }

    /// <summary>
    /// Adds a forward action constructed with a public parameterless constructor.
    /// </summary>
    public SagaBuilder<TState> Then<TStep>()
        where TStep : IStep<TState>, new()
    {
        return Then(() => new TStep(), typeof(TStep));
    }

    /// <summary>
    /// Adds an explicitly configured forward action instance.
    /// </summary>
    public SagaBuilder<TState> Then(IStep<TState>? step)
    {
        return Then(step is null ? null : () => step, step?.GetType(), step is null);
    }

    /// <summary>
    /// Adds a forward action using a deterministic step factory.
    /// </summary>
    public SagaBuilder<TState> Then(Func<IStep<TState>>? stepFactory)
    {
        return Then(stepFactory, null, stepFactory is null);
    }

    /// <summary>
    /// Binds the most recently declared forward action to a compensation step.
    /// </summary>
    public SagaBuilder<TState> CompensateBy<TStep>()
        where TStep : IStep<TState>, new()
    {
        return CompensateBy(() => new TStep(), typeof(TStep));
    }

    /// <summary>
    /// Binds the most recently declared forward action to an explicitly configured compensation step.
    /// </summary>
    public SagaBuilder<TState> CompensateBy(IStep<TState>? step)
    {
        return CompensateBy(step is null ? null : () => step, step?.GetType(), step is null);
    }

    /// <summary>
    /// Binds the most recently declared forward action to a deterministic compensation step factory.
    /// </summary>
    public SagaBuilder<TState> CompensateBy(Func<IStep<TState>>? stepFactory)
    {
        return CompensateBy(stepFactory, null, stepFactory is null);
    }

    /// <summary>
    /// Adds a named compensation scope.
    /// </summary>
    public SagaBuilder<TState> CompensationScope(string scopeId, Action<SagaBuilder<TState>> build)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentNullException.ThrowIfNull(build);

        declaredScopes.Add(scopeId);
        var previousScopeId = currentScopeId;
        currentScopeId = scopeId;
        try
        {
            build(this);
        }
        finally
        {
            currentScopeId = previousScopeId;
        }

        return this;
    }

    /// <summary>
    /// Marks the saga definition as having a successful forward end.
    /// </summary>
    public SagaBuilder<TState> End()
    {
        hasEnd = true;
        return this;
    }

    /// <summary>
    /// Builds a saga definition or throws one aggregated definition exception when validation fails.
    /// </summary>
    public SagaDefinition<TState> Build(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        var validation = BuildValidated(definitionId, definitionVersion);
        if (validation.IsValid)
        {
            return validation.Value;
        }

        var message = string.Join(
            Environment.NewLine,
            validation.Errors.Select(error => $"{error.Code}: {error.Message} ({error.Path ?? "root"})"));
        throw new WorkflowDefinitionException(message);
    }

    /// <summary>
    /// Builds a saga definition, returning all validation errors instead of throwing.
    /// </summary>
    public Validation<SagaDefinition<TState>> BuildValidated(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        var errors = new List<ValidationError>();
        if (createState is null || hasNullInit)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MissingInit,
                "Saga definitions require one Init node.",
                "root"));
        }

        if (!hasEnd)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MissingEnd,
                "Saga definitions require an End marker.",
                "root"));
        }

        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index];
            if (action.HasNullStep)
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.NullDelegate,
                    "A required saga forward action delegate was null.",
                    $"root/actions/{index}"));
            }

            if (action.Compensation is { HasNullStep: true })
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.NullDelegate,
                    "A required saga compensation delegate was null.",
                    $"root/actions/{index}/compensation"));
            }
        }

        var duplicateScopes = declaredScopes
            .GroupBy(scope => scope, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var duplicateScope in duplicateScopes)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.SagaDuplicateScope,
                $"Saga compensation scope '{duplicateScope}' is duplicated.",
                duplicateScope));
        }

        if (errors.Count > 0)
        {
            return Validation<SagaDefinition<TState>>.Invalid(errors);
        }

        var forwardActions = actions
            .Select((action, index) => new SagaForwardAction<TState>(
                $"root/actions/{index}",
                action.ScopeId,
                action.StepType,
                action.StepFactory!,
                action.Compensation is null
                    ? null
                    : new SagaCompensationAction<TState>(
                        action.Compensation.StepType,
                        action.Compensation.StepFactory!)))
            .ToArray();
        var scopes = declaredScopes
            .Distinct(StringComparer.Ordinal)
            .Select(scopeId => new SagaCompensationScope(
                scopeId,
                forwardActions
                    .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
                    .Select(action => action.ActionKey)
                    .ToArray()))
            .ToArray();

        return Validation<SagaDefinition<TState>>.Valid(
            new SagaDefinition<TState>(
                definitionId,
                definitionVersion,
                createState!,
                forwardActions,
                scopes));
    }

    private SagaBuilder<TState> Then(
        Func<IStep<TState>>? stepFactory,
        Type? stepType,
        bool hasNullStep = false)
    {
        actions.Add(new SagaActionBuilderNode(currentScopeId, stepType, stepFactory, hasNullStep));
        return this;
    }

    private SagaBuilder<TState> CompensateBy(
        Func<IStep<TState>>? stepFactory,
        Type? stepType,
        bool hasNullStep = false)
    {
        if (actions.Count == 0)
        {
            actions.Add(new SagaActionBuilderNode(
                currentScopeId,
                null,
                null,
                true,
                new SagaCompensationBuilderNode(stepType, stepFactory, hasNullStep)));
            return this;
        }

        var lastIndex = actions.Count - 1;
        actions[lastIndex] = actions[lastIndex] with
        {
            Compensation = new SagaCompensationBuilderNode(stepType, stepFactory, hasNullStep)
        };
        return this;
    }

    private sealed record SagaActionBuilderNode(
        string ScopeId,
        Type? StepType,
        Func<IStep<TState>>? StepFactory,
        bool HasNullStep,
        SagaCompensationBuilderNode? Compensation = null);

    private sealed record SagaCompensationBuilderNode(
        Type? StepType,
        Func<IStep<TState>>? StepFactory,
        bool HasNullStep);
}
