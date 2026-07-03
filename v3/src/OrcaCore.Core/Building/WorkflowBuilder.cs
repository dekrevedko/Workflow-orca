using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// The public authoring surface for Slice 1 (CR-001): a fluent builder producing an
/// immutable, validated <see cref="WorkflowDefinition{TState}"/>. Definition identity is
/// supplied at build time, not during authoring.
/// </summary>
public sealed class WorkflowBuilder<TState>
{
    private readonly DefinitionNode? initNode;
    private readonly SequenceBuilder<TState> body = new();

    private WorkflowBuilder(DefinitionNode? initNode)
    {
        this.initNode = initNode;
    }

    /// <summary>Starts a builder whose <c>Init</c> converts start input into initial business state (CR-005).</summary>
    public static WorkflowBuilder<TState> Create<TInput>(Func<TInput, TState> createState) =>
        new(new InitNode<TState, TInput>(createState));

    /// <summary>
    /// Starts a builder with no <c>Init</c> step. Only for exercising accumulated validation
    /// (CR-002): <see cref="BuildValidated"/> always reports a missing-Init error, and
    /// <see cref="Build"/> always throws.
    /// </summary>
    internal static WorkflowBuilder<TState> CreateWithoutInit() => new(initNode: null);

    /// <summary>Adds a parameterless step constructed via its default constructor.</summary>
    public WorkflowBuilder<TState> Then<TStep>() where TStep : IStep<TState>, new()
    {
        body.Then<TStep>();
        return this;
    }

    /// <summary>Adds an explicitly configured step instance (no reflection/constructor-argument magic).</summary>
    public WorkflowBuilder<TState> Then(IStep<TState> step)
    {
        body.Then(step);
        return this;
    }

    /// <summary>Adds a step produced fresh per activation by <paramref name="stepFactory"/>.</summary>
    public WorkflowBuilder<TState> Then(Func<IStep<TState>> stepFactory)
    {
        body.Then(stepFactory);
        return this;
    }

    /// <summary>Adds a conditional branch. <paramref name="configureElse"/> is optional (defaults to an empty branch).</summary>
    public WorkflowBuilder<TState> If(
        Func<object?, bool> condition,
        Action<SequenceBuilder<TState>> configureThen,
        Action<SequenceBuilder<TState>>? configureElse = null)
    {
        body.If(condition, configureThen, configureElse);
        return this;
    }

    /// <summary>Adds a loop that repeats <paramref name="configureBody"/> while <paramref name="condition"/> holds.</summary>
    public WorkflowBuilder<TState> While(Func<object?, bool> condition, Action<SequenceBuilder<TState>> configureBody)
    {
        body.While(condition, configureBody);
        return this;
    }

    /// <summary>Adds a set of named branches executed concurrently (CR-044).</summary>
    public WorkflowBuilder<TState> Parallel(params ReadOnlySpan<Action<SequenceBuilder<TState>>> branches)
    {
        body.Parallel(branches);
        return this;
    }

    /// <summary>Adds a wait for a matching named event (EV matching rule).</summary>
    public WorkflowBuilder<TState> Wait(string eventName, Func<object?, CorrelationId> selectCorrelationId)
    {
        body.Wait(eventName, selectCorrelationId);
        return this;
    }

    /// <summary>Terminates the definition with no named outcome.</summary>
    public WorkflowBuilder<TState> End() => End(outcomeName: null);

    /// <summary>Terminates the definition with a named outcome (CR-008).</summary>
    public WorkflowBuilder<TState> End(string? outcomeName)
    {
        body.End(outcomeName);
        return this;
    }

    /// <summary>
    /// Validates and builds the definition, reporting every accumulated error together
    /// (CR-002) instead of failing on the first.
    /// </summary>
    public Validation<WorkflowDefinition<TState>> BuildValidated(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        var root = BuildRoot();
        var errors = WorkflowDefinitionValidator.Validate(initNode is not null, root);

        return errors.Count == 0
            ? Validation<WorkflowDefinition<TState>>.Valid(new WorkflowDefinition<TState>(definitionId, definitionVersion, root))
            : Validation<WorkflowDefinition<TState>>.Invalid(errors);
    }

    /// <summary>
    /// Validates and builds the definition, throwing a single <see cref="WorkflowDefinitionException"/>
    /// that aggregates every accumulated error when validation fails.
    /// </summary>
    public WorkflowDefinition<TState> Build(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        var result = BuildValidated(definitionId, definitionVersion);
        if (result.IsValid)
        {
            return result.Value!;
        }

        var message = string.Join(
            Environment.NewLine,
            result.Errors.Select(error => $"[{error.Code}] {error.Message}" + (error.Path is null ? string.Empty : $" (at {error.Path})")));

        throw new WorkflowDefinitionException($"Workflow definition build failed with {result.Errors.Count} error(s):{Environment.NewLine}{message}");
    }

    private SequenceNode BuildRoot()
    {
        List<DefinitionNode> steps = initNode is null ? [] : [initNode];
        steps.AddRange(body.Steps);
        return new SequenceNode(steps);
    }
}
