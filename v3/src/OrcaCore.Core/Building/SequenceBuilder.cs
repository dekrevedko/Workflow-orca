using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// Fluent authoring surface shared by the workflow root and every nested sequence (If/While
/// body, Parallel branch): sugar over the T1-03 tree (CR-001, CR-003). Instances are
/// intentionally mutable during authoring; <see cref="BuildSequence"/> snapshots the result
/// into the immutable <see cref="SequenceNode"/> tree.
/// </summary>
public sealed class SequenceBuilder<TState>
{
    private readonly List<DefinitionNode> steps = [];

    /// <summary>Adds a parameterless step constructed via its default constructor.</summary>
    public SequenceBuilder<TState> Then<TStep>() where TStep : IStep<TState>, new()
    {
        steps.Add(new BusinessStepNode<TState>(() => new TStep()));
        return this;
    }

    /// <summary>Adds an explicitly configured step instance (no reflection/constructor-argument magic).</summary>
    public SequenceBuilder<TState> Then(IStep<TState> step)
    {
        steps.Add(new BusinessStepNode<TState>(() => step));
        return this;
    }

    /// <summary>Adds a step produced fresh per activation by <paramref name="stepFactory"/>.</summary>
    public SequenceBuilder<TState> Then(Func<IStep<TState>> stepFactory)
    {
        steps.Add(new BusinessStepNode<TState>(stepFactory));
        return this;
    }

    /// <summary>Adds a conditional branch. <paramref name="configureElse"/> is optional (defaults to an empty branch).</summary>
    public SequenceBuilder<TState> If(
        Func<object?, bool> condition,
        Action<SequenceBuilder<TState>> configureThen,
        Action<SequenceBuilder<TState>>? configureElse = null)
    {
        var thenBuilder = new SequenceBuilder<TState>();
        configureThen?.Invoke(thenBuilder);

        var elseBuilder = new SequenceBuilder<TState>();
        configureElse?.Invoke(elseBuilder);

        steps.Add(new IfNode(condition, thenBuilder.BuildSequence(), elseBuilder.BuildSequence()));
        return this;
    }

    /// <summary>Adds a loop that repeats <paramref name="configureBody"/> while <paramref name="condition"/> holds.</summary>
    public SequenceBuilder<TState> While(Func<object?, bool> condition, Action<SequenceBuilder<TState>> configureBody)
    {
        var bodyBuilder = new SequenceBuilder<TState>();
        configureBody?.Invoke(bodyBuilder);

        steps.Add(new WhileNode(condition, bodyBuilder.BuildSequence()));
        return this;
    }

    /// <summary>Adds a set of named branches executed concurrently (CR-044).</summary>
    public SequenceBuilder<TState> Parallel(params ReadOnlySpan<Action<SequenceBuilder<TState>>> branches)
    {
        var parallelBranches = new List<ParallelBranch>(branches.Length);
        for (var i = 0; i < branches.Length; i++)
        {
            var branchBuilder = new SequenceBuilder<TState>();
            branches[i]?.Invoke(branchBuilder);
            parallelBranches.Add(new ParallelBranch(new BranchId(i, $"Branch{i}"), branchBuilder.BuildSequence()));
        }

        steps.Add(new ParallelNode(parallelBranches));
        return this;
    }

    /// <summary>Adds a wait for a matching named event (EV matching rule).</summary>
    public SequenceBuilder<TState> Wait(string eventName, Func<object?, CorrelationId> selectCorrelationId)
    {
        steps.Add(new WaitNode(eventName, selectCorrelationId));
        return this;
    }

    /// <summary>Terminates this sequence path with no named outcome.</summary>
    public SequenceBuilder<TState> End() => End(outcomeName: null);

    /// <summary>Terminates this sequence path with a named outcome (CR-008).</summary>
    public SequenceBuilder<TState> End(string? outcomeName)
    {
        steps.Add(new EndNode(outcomeName));
        return this;
    }

    internal SequenceNode BuildSequence() => new([.. steps]);

    internal IReadOnlyList<DefinitionNode> Steps => steps;
}
