using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// Builds immutable workflow definitions for the Slice 1 authoring surface.
/// </summary>
public sealed class WorkflowBuilder<TState>
{
    private readonly List<BuilderNode> nodes = [];

    /// <summary>
    /// Adds the Init node that converts start input into business state.
    /// </summary>
    public WorkflowBuilder<TState> Init<TInput>(Func<TInput, TState>? createState)
    {
        nodes.Add(new InitBuilderNode(input => createState!((TInput)input!), createState is null));
        return this;
    }

    /// <summary>
    /// Adds a business step constructed with a public parameterless constructor.
    /// </summary>
    public WorkflowBuilder<TState> Then<TStep>()
        where TStep : IStep<TState>, new()
    {
        return Then(() => new TStep());
    }

    /// <summary>
    /// Adds a business step using a deterministic step factory.
    /// </summary>
    public WorkflowBuilder<TState> Then(Func<IStep<TState>>? stepFactory)
    {
        nodes.Add(new StepBuilderNode(stepFactory, stepFactory is null));
        return this;
    }

    /// <summary>
    /// Adds an If node with required then sequence and optional else sequence.
    /// </summary>
    public WorkflowBuilder<TState> If(
        Func<TState, bool>? condition,
        Action<WorkflowBuilder<TState>> then,
        Action<WorkflowBuilder<TState>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(then);

        var thenBuilder = new WorkflowBuilder<TState>();
        then(thenBuilder);
        var elseBuilder = new WorkflowBuilder<TState>();
        otherwise?.Invoke(elseBuilder);

        nodes.Add(new IfBuilderNode(condition, condition is null, thenBuilder.nodes, elseBuilder.nodes));
        return this;
    }

    /// <summary>
    /// Adds a While node with a nested body sequence.
    /// </summary>
    public WorkflowBuilder<TState> While(Func<TState, bool>? condition, Action<WorkflowBuilder<TState>> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var bodyBuilder = new WorkflowBuilder<TState>();
        body(bodyBuilder);

        nodes.Add(new WhileBuilderNode(condition, condition is null, bodyBuilder.nodes));
        return this;
    }

    /// <summary>
    /// Adds a Parallel node with named branch builders.
    /// </summary>
    public WorkflowBuilder<TState> Parallel(
        params (string Name, Action<WorkflowBuilder<TState>> Build)[] branches)
    {
        ArgumentNullException.ThrowIfNull(branches);

        var branchNodes = new List<ParallelBranchBuilderNode>(branches.Length);
        foreach (var (name, build) in branches)
        {
            var branchBuilder = new WorkflowBuilder<TState>();
            build?.Invoke(branchBuilder);
            branchNodes.Add(new ParallelBranchBuilderNode(name, build is null, branchBuilder.nodes));
        }

        nodes.Add(new ParallelBuilderNode(branchNodes));
        return this;
    }

    /// <summary>
    /// Adds a wait for an event name and correlation selector.
    /// </summary>
    public WorkflowBuilder<TState> Wait(string eventName, Func<TState, CorrelationId>? correlationSelector)
    {
        nodes.Add(new WaitBuilderNode(eventName, correlationSelector, correlationSelector is null));
        return this;
    }

    /// <summary>
    /// Adds an unnamed End node.
    /// </summary>
    public WorkflowBuilder<TState> End()
    {
        return End(null);
    }

    /// <summary>
    /// Adds an End node with an optional outcome name.
    /// </summary>
    public WorkflowBuilder<TState> End(string? outcomeName)
    {
        nodes.Add(new EndBuilderNode(outcomeName));
        return this;
    }

    /// <summary>
    /// Builds a definition or throws one aggregated definition exception when validation fails.
    /// </summary>
    public WorkflowDefinition<TState> Build(DefinitionId definitionId, DefinitionVersion definitionVersion)
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
    /// Builds a definition, returning all validation errors instead of throwing.
    /// </summary>
    public Validation<WorkflowDefinition<TState>> BuildValidated(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        var errors = new List<ValidationError>();
        if (!nodes.OfType<InitBuilderNode>().Any(node => !node.HasNullDelegate))
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MissingInit,
                "Workflow definitions require one Init node.",
                "root"));
        }

        if (!ContainsEnd(nodes))
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MissingEnd,
                "Workflow definitions require a reachable End node.",
                "root"));
        }

        ValidateNodes(nodes, "root", errors);

        if (errors.Count > 0)
        {
            return Validation<WorkflowDefinition<TState>>.Invalid(errors);
        }

        var root = new SequenceNode<TState>("root", BuildNodes(nodes, "root"));
        return Validation<WorkflowDefinition<TState>>.Valid(
            new WorkflowDefinition<TState>(definitionId, definitionVersion, root));
    }

    private static bool ContainsEnd(IEnumerable<BuilderNode> candidateNodes)
    {
        foreach (var node in candidateNodes)
        {
            switch (node)
            {
                case EndBuilderNode:
                    return true;
                case IfBuilderNode ifNode when ContainsEnd(ifNode.ThenNodes) || ContainsEnd(ifNode.ElseNodes):
                    return true;
                case WhileBuilderNode whileNode when ContainsEnd(whileNode.BodyNodes):
                    return true;
                case ParallelBuilderNode parallelNode
                    when parallelNode.Branches.Any(branch => ContainsEnd(branch.Nodes)):
                    return true;
            }
        }

        return false;
    }

    private static void ValidateNodes(IEnumerable<BuilderNode> candidateNodes, string path, List<ValidationError> errors)
    {
        var index = 0;
        foreach (var node in candidateNodes)
        {
            var nodePath = $"{path}/{index}";
            switch (node)
            {
                case InitBuilderNode { HasNullDelegate: true }:
                case StepBuilderNode { HasNullDelegate: true }:
                case IfBuilderNode { HasNullDelegate: true }:
                case WhileBuilderNode { HasNullDelegate: true }:
                case WaitBuilderNode { HasNullDelegate: true }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NullDelegate,
                        "A required workflow builder delegate was null.",
                        nodePath));
                    break;
            }

            switch (node)
            {
                case IfBuilderNode ifNode:
                    ValidateNodes(ifNode.ThenNodes, $"{nodePath}/then", errors);
                    ValidateNodes(ifNode.ElseNodes, $"{nodePath}/else", errors);
                    break;
                case WhileBuilderNode whileNode:
                    if (whileNode.BodyNodes.Count == 0)
                    {
                        errors.Add(new ValidationError(
                            BuilderValidationCodes.EmptyWhile,
                            "While nodes require a non-empty body.",
                            nodePath));
                    }

                    ValidateNodes(whileNode.BodyNodes, $"{nodePath}/body", errors);
                    break;
                case ParallelBuilderNode parallelNode:
                    ValidateParallel(parallelNode, nodePath, errors);
                    break;
            }

            index++;
        }
    }

    private static void ValidateParallel(
        ParallelBuilderNode parallelNode,
        string path,
        List<ValidationError> errors)
    {
        if (parallelNode.Branches.Count == 0)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.EmptyParallel,
                "Parallel nodes require at least one branch.",
                path));
        }

        foreach (var duplicate in parallelNode.Branches
                     .GroupBy(branch => branch.Name, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.DuplicateBranchName,
                $"Parallel branch name '{duplicate}' is duplicated.",
                path));
        }

        for (var branchIndex = 0; branchIndex < parallelNode.Branches.Count; branchIndex++)
        {
            var branch = parallelNode.Branches[branchIndex];
            var branchPath = $"{path}/branches/{branchIndex}";
            if (branch.HasNullDelegate)
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.NullDelegate,
                    "A required workflow builder delegate was null.",
                    branchPath));
            }

            if (branch.Nodes.Count == 0)
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.EmptyBranch,
                    "Parallel branches require a non-empty body.",
                    branchPath));
            }

            ValidateNodes(branch.Nodes, branchPath, errors);
        }
    }

    private static IEnumerable<WorkflowNode<TState>> BuildNodes(IEnumerable<BuilderNode> candidateNodes, string path)
    {
        var index = 0;
        foreach (var node in candidateNodes)
        {
            var nodeId = $"{path}/{index}";
            yield return node switch
            {
                InitBuilderNode initNode => new InitNode<TState>(nodeId, initNode.CreateState),
                StepBuilderNode stepNode => new BusinessStepNode<TState>(nodeId, stepNode.StepFactory!),
                EndBuilderNode endNode => new EndNode<TState>(nodeId, endNode.OutcomeName),
                WaitBuilderNode waitNode => new WaitNode<TState>(
                    nodeId,
                    waitNode.EventName,
                    waitNode.CorrelationSelector!),
                IfBuilderNode ifNode => new IfNode<TState>(
                    nodeId,
                    ifNode.Condition!,
                    new SequenceNode<TState>($"{nodeId}/then", BuildNodes(ifNode.ThenNodes, $"{nodeId}/then")),
                    new SequenceNode<TState>($"{nodeId}/else", BuildNodes(ifNode.ElseNodes, $"{nodeId}/else"))),
                WhileBuilderNode whileNode => new WhileNode<TState>(
                    nodeId,
                    whileNode.Condition!,
                    new SequenceNode<TState>($"{nodeId}/body", BuildNodes(whileNode.BodyNodes, $"{nodeId}/body"))),
                ParallelBuilderNode parallelNode => new ParallelNode<TState>(
                    nodeId,
                    parallelNode.Branches.Select((branch, ordinal) => new ParallelBranch<TState>(
                        new BranchId(ordinal, branch.Name),
                        new SequenceNode<TState>(
                            $"{nodeId}/branches/{ordinal}",
                            BuildNodes(branch.Nodes, $"{nodeId}/branches/{ordinal}"))))),
                _ => throw new InvalidOperationException($"Unknown builder node '{node.GetType().Name}'.")
            };
            index++;
        }
    }

    private abstract record BuilderNode;

    private sealed record InitBuilderNode(Func<object?, TState> CreateState, bool HasNullDelegate) : BuilderNode;

    private sealed record StepBuilderNode(Func<IStep<TState>>? StepFactory, bool HasNullDelegate) : BuilderNode;

    private sealed record EndBuilderNode(string? OutcomeName) : BuilderNode;

    private sealed record WaitBuilderNode(
        string EventName,
        Func<TState, CorrelationId>? CorrelationSelector,
        bool HasNullDelegate) : BuilderNode;

    private sealed record IfBuilderNode(
        Func<TState, bool>? Condition,
        bool HasNullDelegate,
        IReadOnlyList<BuilderNode> ThenNodes,
        IReadOnlyList<BuilderNode> ElseNodes) : BuilderNode;

    private sealed record WhileBuilderNode(
        Func<TState, bool>? Condition,
        bool HasNullDelegate,
        IReadOnlyList<BuilderNode> BodyNodes) : BuilderNode;

    private sealed record ParallelBuilderNode(IReadOnlyList<ParallelBranchBuilderNode> Branches) : BuilderNode;

    private sealed record ParallelBranchBuilderNode(
        string Name,
        bool HasNullDelegate,
        IReadOnlyList<BuilderNode> Nodes);
}
