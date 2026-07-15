using System.Diagnostics;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
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
        nodes.Add(new InitBuilderNode(
            input => createState!((TInput)input!),
            createState is null,
            (payload, serializer) => serializer.Deserialize<TInput>(payload)));
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
    /// Adds a retry decorator with a fixed delay between attempts to the next authored step.
    /// Durable execution persists the delay as a timer so it survives host replacement.
    /// </summary>
    public WorkflowBuilder<TState> WithRetry(int maxAttempts, TimeSpan backoff)
    {
        pendingPolicies = pendingPolicies.WithRetry(maxAttempts, backoff);
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
        return End((string?)null);
    }

    /// <summary>
    /// Adds an End node with an optional outcome name.
    /// </summary>
    public WorkflowBuilder<TState> End(string? outcomeName)
    {
        nodes.Add(new EndBuilderNode(outcomeName, null, false));
        return this;
    }

    /// <summary>
    /// Adds the root End node with a deterministic outcome selector over final state.
    /// </summary>
    public WorkflowBuilder<TState> End(Func<TState, string?>? outcomeSelector)
    {
        nodes.Add(new EndBuilderNode(null, outcomeSelector, outcomeSelector is null));
        return this;
    }

    /// <summary>
    /// Returns a typed result from the current structured branch.
    /// </summary>
    public WorkflowBuilder<TState> BranchReturn<TResult>(Func<TState, TResult>? resultSelector)
    {
        nodes.Add(new BranchReturnBuilderNode(
            typeof(TResult),
            state => resultSelector!(state),
            resultSelector is null));
        return this;
    }

    /// <summary>
    /// Ends the current root generation and starts the next generation with replacement state.
    /// </summary>
    public WorkflowBuilder<TState> ContinueAsNew(Func<TState, TState>? stateSelector)
    {
        nodes.Add(new ContinueAsNewBuilderNode(stateSelector, stateSelector is null));
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
        ValidateRootShape(nodes, errors);
        ValidateNodes(nodes, "root", errors, isRoot: true);
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

    private static void ValidateRootShape(IReadOnlyList<BuilderNode> candidateNodes, List<ValidationError> errors)
    {
        var initIndexes = candidateNodes
            .Select((node, index) => (node, index))
            .Where(candidate => candidate.node is InitBuilderNode)
            .Select(candidate => candidate.index)
            .ToArray();
        var validInitCount = candidateNodes.OfType<InitBuilderNode>().Count(node => !node.HasNullDelegate);

        if (validInitCount == 0)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MissingInit,
                "Workflow definitions require one non-null root Init node.",
                "root"));
        }

        if (initIndexes.Length > 1)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MultipleInit,
                "Workflow definitions require exactly one root Init node.",
                "root"));
        }

        if (initIndexes.Length > 0 && initIndexes[0] != 0)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.InitNotFirst,
                "The root Init node must be the first authored node.",
                $"root/{initIndexes[0]}"));
        }

        var endIndexes = candidateNodes
            .Select((node, index) => (node, index))
            .Where(candidate => candidate.node is EndBuilderNode)
            .Select(candidate => candidate.index)
            .ToArray();

        if (endIndexes.Length == 0)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MissingEnd,
                "Workflow definitions require exactly one root End node.",
                "root"));
        }

        if (endIndexes.Length > 1)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.MultipleEnd,
                "Workflow definitions require exactly one root End node.",
                "root"));
        }

        if (endIndexes.Length == 1 && endIndexes[0] < candidateNodes.Count - 1)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.NodeAfterEnd,
                "Executable nodes cannot appear after the root End node.",
                $"root/{endIndexes[0] + 1}"));
        }
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
            }
        }

        return false;
    }

    private static void ValidateNodes(
        IEnumerable<BuilderNode> candidateNodes,
        string path,
        List<ValidationError> errors,
        bool isRoot = false,
        bool isBranchFiber = false,
        bool isBranchSequenceRoot = false)
    {
        var index = 0;
        var terminalSeen = false;
        foreach (var node in candidateNodes)
        {
            var nodePath = $"{path}/{index}";
            if (terminalSeen)
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.UnreachableNode,
                    "Nodes cannot follow BranchReturn or ContinueAsNew in the same sequence.",
                    nodePath));
            }

            if (!isRoot && node is InitBuilderNode)
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.NestedInit,
                    "Init is valid only as the first root node.",
                    nodePath));
            }

            if (!isRoot && node is EndBuilderNode)
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.NestedEnd,
                    "End is valid only as the final root node.",
                    nodePath));
            }

            if (node is BranchReturnBuilderNode && (!isBranchFiber || !isBranchSequenceRoot))
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.BranchReturnNotAtScopeExit,
                    "BranchReturn is valid only as the direct terminal of an owned branch sequence.",
                    nodePath));
            }

            if (node is ContinueAsNewBuilderNode && isBranchFiber)
            {
                errors.Add(new ValidationError(
                    BuilderValidationCodes.ContinueAsNewNotRoot,
                    "ContinueAsNew is valid only on the root workflow fiber.",
                    nodePath));
            }

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
                case EndBuilderNode { HasNullDelegate: true }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NullDelegate,
                        "A required workflow outcome selector was null.",
                        nodePath));
                    break;
                case BranchReturnBuilderNode { HasNullDelegate: true }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NullDelegate,
                        "A required branch result selector was null.",
                        nodePath));
                    break;
                case ContinueAsNewBuilderNode { HasNullDelegate: true }:
                    errors.Add(new ValidationError(
                        BuilderValidationCodes.NullDelegate,
                        "A required ContinueAsNew state selector was null.",
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
                    ValidateNodes(
                        ifNode.ThenNodes,
                        $"{nodePath}/then",
                        errors,
                        isBranchFiber: isBranchFiber);
                    ValidateNodes(
                        ifNode.ElseNodes,
                        $"{nodePath}/else",
                        errors,
                        isBranchFiber: isBranchFiber);
                    break;
                case WhileBuilderNode whileNode:
                    if (whileNode.BodyNodes.Count == 0)
                    {
                        errors.Add(new ValidationError(
                            BuilderValidationCodes.EmptyWhile,
                            "While nodes require a non-empty body.",
                            nodePath));
                    }

                    ValidateNodes(
                        whileNode.BodyNodes,
                        $"{nodePath}/body",
                        errors,
                        isBranchFiber: isBranchFiber);
                    break;
            }

            if (node is StepBuilderNode stepNode)
            {
                ValidatePolicies(stepNode.Policies, $"{nodePath}/policies", errors);
            }

            if (node is BranchReturnBuilderNode or ContinueAsNewBuilderNode)
            {
                terminalSeen = true;
            }

            index++;
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
                InitBuilderNode initNode => new InitNode<TState>(nodeId, initNode.CreateState, initNode.RehydrateInput),
                StepBuilderNode stepNode => new BusinessStepNode<TState>(
                    nodeId,
                    stepNode.StepFactory!,
                    stepNode.Policies),
                EndBuilderNode endNode => new EndNode<TState>(
                    nodeId,
                    endNode.OutcomeName,
                    endNode.OutcomeSelector),
                BranchReturnBuilderNode returnNode => new BranchReturnNode<TState>(
                    nodeId,
                    returnNode.ResultType,
                    returnNode.ResultSelector!),
                ContinueAsNewBuilderNode continueNode => new ContinueAsNewNode<TState>(
                    nodeId,
                    continueNode.StateSelector!),
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

    private sealed record InitBuilderNode(
        Func<object?, TState> CreateState,
        bool HasNullDelegate,
        Func<SerializedPayload, IWorkflowPayloadSerializer, object?>? RehydrateInput = null) : BuilderNode;

    private static void ValidatePolicies(WorkflowPolicySet policies, string path, List<ValidationError> errors)
    {
        if (policies.Retry is { MaxAttempts: <= 0 })
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.InvalidRetryPolicy,
                "Retry max attempts must be greater than zero.",
                path));
        }

        if (policies.Retry is { Backoff: var backoff } && backoff < TimeSpan.Zero)
        {
            errors.Add(new ValidationError(
                BuilderValidationCodes.InvalidRetryPolicy,
                "Retry backoff cannot be negative.",
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

    private sealed record EndBuilderNode(
        string? OutcomeName,
        Func<TState, string?>? OutcomeSelector,
        bool HasNullDelegate) : BuilderNode;

    private sealed record BranchReturnBuilderNode(
        Type ResultType,
        Func<TState, object?>? ResultSelector,
        bool HasNullDelegate) : BuilderNode;

    private sealed record ContinueAsNewBuilderNode(
        Func<TState, TState>? StateSelector,
        bool HasNullDelegate) : BuilderNode;

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

}
