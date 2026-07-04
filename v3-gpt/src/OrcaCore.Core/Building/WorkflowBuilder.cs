using System.Diagnostics;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Durable;
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
    private WorkflowPolicySet definitionPolicies = WorkflowPolicySet.Empty;
    private WorkflowPolicySet pendingPolicies = WorkflowPolicySet.Empty;

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
    /// Adds an explicitly configured business step instance. The instance is shared across all
    /// workflow instances and executions, so it must be stateless or thread-safe; use the
    /// factory overload for steps that carry per-execution state.
    /// </summary>
    public WorkflowBuilder<TState> Then(IStep<TState>? step)
    {
        nodes.Add(new StepBuilderNode(() => step!, step is null, ConsumePendingPolicies()));
        return this;
    }

    /// <summary>
    /// Adds a business step using a deterministic step factory.
    /// </summary>
    public WorkflowBuilder<TState> Then(Func<IStep<TState>>? stepFactory)
    {
        nodes.Add(new StepBuilderNode(stepFactory, stepFactory is null, ConsumePendingPolicies()));
        return this;
    }

    /// <summary>
    /// Adds a retry decorator to the next authored step.
    /// </summary>
    public WorkflowBuilder<TState> WithRetry(int maxAttempts)
    {
        pendingPolicies = pendingPolicies.WithRetry(maxAttempts);
        return this;
    }

    /// <summary>
    /// Adds a timeout decorator to the next authored step.
    /// </summary>
    public WorkflowBuilder<TState> WithTimeout(TimeSpan duration)
    {
        pendingPolicies = pendingPolicies.WithTimeout(duration);
        return this;
    }

    /// <summary>
    /// Adds a cancellation decorator to the next authored step.
    /// </summary>
    public WorkflowBuilder<TState> WithCancellation()
    {
        pendingPolicies = pendingPolicies.WithCancellation();
        return this;
    }

    /// <summary>
    /// Adds an in-process pool hint decorator to the next authored step.
    /// </summary>
    public WorkflowBuilder<TState> WithPoolKey(string poolKey)
    {
        pendingPolicies = pendingPolicies.WithPoolKey(poolKey);
        return this;
    }

    /// <summary>
    /// Adds a definition-level retry decorator.
    /// </summary>
    public WorkflowBuilder<TState> WithDefinitionRetry(int maxAttempts)
    {
        definitionPolicies = definitionPolicies.WithRetry(maxAttempts);
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
    /// Adds a WhenFirst node that cancels losing branch work after the first branch completes.
    /// </summary>
    public WorkflowBuilder<TState> WhenFirst(
        params (string Name, Action<WorkflowBuilder<TState>> Build)[] branches)
    {
        return WhenFirst(WhenFirstResidualPolicy.CancelRemaining, branches);
    }

    /// <summary>
    /// Adds a WhenFirst node with an explicit losing-branch residual policy.
    /// </summary>
    public WorkflowBuilder<TState> WhenFirst(
        WhenFirstResidualPolicy residualPolicy,
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

        nodes.Add(new WhenFirstBuilderNode(residualPolicy, branchNodes));
        return this;
    }

    /// <summary>
    /// Adds an in-instance ForEach node with deterministic partitioning and WhenAll join behavior.
    /// </summary>
    public WorkflowBuilder<TState> ForEach<TItem>(
        Func<TState, IReadOnlyList<TItem>>? itemSelector,
        WorkflowPartitioner<TItem>? partitioner,
        Action<WorkflowBuilder<TState>> body,
        int? maxConcurrency = null,
        ForEachJoinPolicy joinPolicy = ForEachJoinPolicy.WhenAll,
        ForEachFailurePolicy failurePolicy = ForEachFailurePolicy.FailFast,
        ForEachResidualPolicy residualPolicy = ForEachResidualPolicy.CancelRemaining)
    {
        ArgumentNullException.ThrowIfNull(body);

        var bodyBuilder = new WorkflowBuilder<TState>();
        body(bodyBuilder);

        nodes.Add(new ForEachBuilderNode<TItem>(
            itemSelector,
            itemSelector is null,
            partitioner,
            partitioner is null,
            bodyBuilder.nodes,
            maxConcurrency,
            joinPolicy,
            failurePolicy,
            residualPolicy));
        return this;
    }

    /// <summary>
    /// Adds a durable child workflow node.
    /// </summary>
    public WorkflowBuilder<TState> RunChild(
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        RunChildFailurePolicy failurePolicy = RunChildFailurePolicy.PropagateFailure)
    {
        nodes.Add(new RunChildBuilderNode(childDefinitionId, childDefinitionVersion, failurePolicy));
        return this;
    }

    /// <summary>
    /// Adds a durable child workflow fan-out node.
    /// </summary>
    public WorkflowBuilder<TState> RunChildren(
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        Func<TState, IReadOnlyList<string>>? itemSnapshotSelector,
        RunChildFailurePolicy failurePolicy = RunChildFailurePolicy.PropagateFailure,
        int? maxConcurrency = null,
        RunChildrenJoinPolicy joinPolicy = RunChildrenJoinPolicy.WhenAll,
        RunChildrenResidualPolicy residualPolicy = RunChildrenResidualPolicy.CancelRemaining)
    {
        nodes.Add(new RunChildrenBuilderNode(
            childDefinitionId,
            childDefinitionVersion,
            itemSnapshotSelector,
            itemSnapshotSelector is null,
            failurePolicy,
            maxConcurrency,
            joinPolicy,
            residualPolicy));
        return this;
    }

    /// <summary>
    /// Adds a wait for an event name and correlation selector.
    /// </summary>
    public WorkflowBuilder<TState> Wait(string eventName, Func<TState, CorrelationId>? correlationSelector)
    {
        nodes.Add(new WaitBuilderNode(eventName, correlationSelector, correlationSelector is null, null));
        return this;
    }

    /// <summary>
    /// Adds a wait that races an event against a timeout.
    /// </summary>
    public WorkflowBuilder<TState> Wait(
        string eventName,
        Func<TState, CorrelationId>? correlationSelector,
        TimeSpan timeout)
    {
        nodes.Add(new WaitBuilderNode(eventName, correlationSelector, correlationSelector is null, timeout));
        return this;
    }

    /// <summary>
    /// Adds a first-class delay node.
    /// </summary>
    public WorkflowBuilder<TState> Delay(TimeSpan duration)
    {
        nodes.Add(new DelayBuilderNode(duration));
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
        ValidatePolicies(definitionPolicies, "root/policies", errors);

        if (errors.Count > 0)
        {
            return Validation<WorkflowDefinition<TState>>.Invalid(errors);
        }

        var root = new SequenceNode<TState>("root", BuildNodes(nodes, "root"));
        return Validation<WorkflowDefinition<TState>>.Valid(
            new WorkflowDefinition<TState>(
                definitionId,
                definitionVersion,
                root,
                definitionPolicies,
                ContainsDurableOnlyNodes(nodes)));
    }

    private static bool ContainsDurableOnlyNodes(IEnumerable<BuilderNode> candidateNodes)
    {
        foreach (var node in candidateNodes)
        {
            switch (node)
            {
                case RunChildBuilderNode:
                case RunChildrenBuilderNode:
                    return true;
                case IfBuilderNode ifNode
                    when ContainsDurableOnlyNodes(ifNode.ThenNodes) || ContainsDurableOnlyNodes(ifNode.ElseNodes):
                    return true;
                case WhileBuilderNode whileNode when ContainsDurableOnlyNodes(whileNode.BodyNodes):
                    return true;
                case ParallelBuilderNode parallelNode
                    when parallelNode.Branches.Any(branch => ContainsDurableOnlyNodes(branch.Nodes)):
                    return true;
                case WhenFirstBuilderNode whenFirstNode
                    when whenFirstNode.Branches.Any(branch => ContainsDurableOnlyNodes(branch.Nodes)):
                    return true;
                case IForEachBuilderNode forEachNode when ContainsDurableOnlyNodes(forEachNode.BodyNodes):
                    return true;
            }
        }

        return false;
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
                case WhenFirstBuilderNode whenFirstNode
                    when whenFirstNode.Branches.Any(branch => ContainsEnd(branch.Nodes)):
                    return true;
                case IForEachBuilderNode forEachNode when ContainsEnd(forEachNode.BodyNodes):
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
                case RunChildrenBuilderNode { HasNullDelegate: true }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NullDelegate,
                        "A required workflow builder delegate was null.",
                        nodePath));
                    break;
                case DelayBuilderNode delayNode when delayNode.Duration <= TimeSpan.Zero:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NonPositiveDelay,
                        "Delay duration must be greater than zero.",
                        nodePath));
                    break;
                case WaitBuilderNode { Timeout: { } timeout } when timeout <= TimeSpan.Zero:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NonPositiveTimeout,
                        "Wait timeout must be greater than zero.",
                        nodePath));
                    break;
                case IForEachBuilderNode { HasNullDelegate: true }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NullDelegate,
                        "A required workflow builder delegate was null.",
                        nodePath));
                    break;
                case IForEachBuilderNode { HasNullPartitioner: true }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NullDelegate,
                        "A required workflow partitioner was null.",
                        nodePath));
                    break;
                case IForEachBuilderNode { MaxConcurrency: <= 0 }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NonPositiveMaxConcurrency,
                        "ForEach max concurrency must be greater than zero.",
                        nodePath));
                    break;
                case RunChildrenBuilderNode { MaxConcurrency: <= 0 }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NonPositiveMaxConcurrency,
                        "RunChildren max concurrency must be greater than zero.",
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
                    ValidateBranches(parallelNode.Branches, nodePath, "Parallel", errors);
                    break;
                case WhenFirstBuilderNode whenFirstNode:
                    ValidateBranches(whenFirstNode.Branches, nodePath, "WhenFirst", errors);
                    break;
                case IForEachBuilderNode forEachNode:
                    if (forEachNode.BodyNodes.Count == 0)
                    {
                        errors.Add(new ValidationError(
                            BuilderValidationCodes.EmptyForEach,
                            "ForEach nodes require a non-empty body.",
                            nodePath));
                    }

                    ValidateNodes(forEachNode.BodyNodes, $"{nodePath}/body", errors);
                    break;
            }

            if (node is StepBuilderNode stepNode)
            {
                ValidatePolicies(stepNode.Policies, $"{nodePath}/policies", errors);
            }

            index++;
        }
    }

    private static void ValidateBranches(
        IReadOnlyList<ParallelBranchBuilderNode> branches,
        string path,
        string nodeName,
        List<ValidationError> errors)
    {
        if (branches.Count == 0)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.EmptyParallel,
                $"{nodeName} nodes require at least one branch.",
                path));
        }

        foreach (var duplicate in branches
                     .GroupBy(branch => branch.Name, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.DuplicateBranchName,
                $"{nodeName} branch name '{duplicate}' is duplicated.",
                path));
        }

        for (var branchIndex = 0; branchIndex < branches.Count; branchIndex++)
        {
            var branch = branches[branchIndex];
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
                    $"{nodeName} branches require a non-empty body.",
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
                StepBuilderNode stepNode => new BusinessStepNode<TState>(
                    nodeId,
                    stepNode.StepFactory!,
                    stepNode.Policies),
                EndBuilderNode endNode => new EndNode<TState>(nodeId, endNode.OutcomeName),
                WaitBuilderNode waitNode => new WaitNode<TState>(
                    nodeId,
                    waitNode.EventName,
                    waitNode.CorrelationSelector!,
                    waitNode.Timeout),
                DelayBuilderNode delayNode => new DelayNode<TState>(
                    nodeId,
                    delayNode.Duration),
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
                WhenFirstBuilderNode whenFirstNode => new WhenFirstNode<TState>(
                    nodeId,
                    whenFirstNode.ResidualPolicy,
                    whenFirstNode.Branches.Select((branch, ordinal) => new ParallelBranch<TState>(
                        new BranchId(ordinal, branch.Name),
                        new SequenceNode<TState>(
                            $"{nodeId}/branches/{ordinal}",
                            BuildNodes(branch.Nodes, $"{nodeId}/branches/{ordinal}"))))),
                IForEachBuilderNode forEachNode => forEachNode.Build(
                    nodeId,
                    new SequenceNode<TState>(
                        $"{nodeId}/body",
                        BuildNodes(forEachNode.BodyNodes, $"{nodeId}/body"))),
                RunChildBuilderNode runChildNode => new RunChildNode<TState>(
                    nodeId,
                    runChildNode.ChildDefinitionId,
                    runChildNode.ChildDefinitionVersion,
                    runChildNode.FailurePolicy),
                RunChildrenBuilderNode runChildrenNode => new RunChildrenNode<TState>(
                    nodeId,
                    runChildrenNode.ChildDefinitionId,
                    runChildrenNode.ChildDefinitionVersion,
                    runChildrenNode.ItemSnapshotSelector!,
                    runChildrenNode.FailurePolicy,
                    runChildrenNode.MaxConcurrency,
                    runChildrenNode.JoinPolicy,
                    runChildrenNode.ResidualPolicy),
                _ => throw new UnreachableException()
            };
            index++;
        }
    }

    private abstract record BuilderNode;

    private sealed record InitBuilderNode(Func<object?, TState> CreateState, bool HasNullDelegate) : BuilderNode;

    private static void ValidatePolicies(WorkflowPolicySet policies, string path, List<ValidationError> errors)
    {
        if (policies.Retry is { MaxAttempts: <= 0 })
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.InvalidRetryPolicy,
                "Retry max attempts must be greater than zero.",
                path));
        }
    }

    private WorkflowPolicySet ConsumePendingPolicies()
    {
        var policies = pendingPolicies;
        pendingPolicies = WorkflowPolicySet.Empty;
        return policies;
    }

    private sealed record StepBuilderNode(
        Func<IStep<TState>>? StepFactory,
        bool HasNullDelegate,
        WorkflowPolicySet Policies) : BuilderNode;

    private sealed record EndBuilderNode(string? OutcomeName) : BuilderNode;

    private sealed record WaitBuilderNode(
        string EventName,
        Func<TState, CorrelationId>? CorrelationSelector,
        bool HasNullDelegate,
        TimeSpan? Timeout) : BuilderNode;

    private sealed record DelayBuilderNode(TimeSpan Duration) : BuilderNode;

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

    private sealed record WhenFirstBuilderNode(
        WhenFirstResidualPolicy ResidualPolicy,
        IReadOnlyList<ParallelBranchBuilderNode> Branches) : BuilderNode;

    private sealed record RunChildBuilderNode(
        DefinitionId ChildDefinitionId,
        DefinitionVersion ChildDefinitionVersion,
        RunChildFailurePolicy FailurePolicy) : BuilderNode;

    private sealed record RunChildrenBuilderNode(
        DefinitionId ChildDefinitionId,
        DefinitionVersion ChildDefinitionVersion,
        Func<TState, IReadOnlyList<string>>? ItemSnapshotSelector,
        bool HasNullDelegate,
        RunChildFailurePolicy FailurePolicy,
        int? MaxConcurrency,
        RunChildrenJoinPolicy JoinPolicy,
        RunChildrenResidualPolicy ResidualPolicy) : BuilderNode;

    private interface IForEachBuilderNode
    {
        bool HasNullDelegate { get; }

        bool HasNullPartitioner { get; }

        IReadOnlyList<BuilderNode> BodyNodes { get; }

        int? MaxConcurrency { get; }

        ForEachJoinPolicy JoinPolicy { get; }

        ForEachFailurePolicy FailurePolicy { get; }

        ForEachResidualPolicy ResidualPolicy { get; }

        WorkflowNode<TState> Build(string nodeId, SequenceNode<TState> body);
    }

    private sealed record ForEachBuilderNode<TItem>(
        Func<TState, IReadOnlyList<TItem>>? ItemSelector,
        bool HasNullDelegate,
        WorkflowPartitioner<TItem>? Partitioner,
        bool HasNullPartitioner,
        IReadOnlyList<BuilderNode> BodyNodes,
        int? MaxConcurrency,
        ForEachJoinPolicy JoinPolicy,
        ForEachFailurePolicy FailurePolicy,
        ForEachResidualPolicy ResidualPolicy) : BuilderNode, IForEachBuilderNode
    {
        public WorkflowNode<TState> Build(string nodeId, SequenceNode<TState> body)
        {
            return new ForEachNode<TState, TItem>(
                nodeId,
                ItemSelector!,
                Partitioner!,
                body,
                MaxConcurrency,
                JoinPolicy,
                FailurePolicy,
                ResidualPolicy);
        }
    }

    private sealed record ParallelBranchBuilderNode(
        string Name,
        bool HasNullDelegate,
        IReadOnlyList<BuilderNode> Nodes);
}
