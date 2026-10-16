
namespace OrcaCore.Runtime.Execution;

internal static class WorkflowRuntime
{
    public static async Task<WorkflowExecutionReport> ExecuteAsync<TState>(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        ICorrelationMutationSink correlationSink,
        CancellationToken cancellationToken = default,
        EventEnvelope? resumedEvent = null,
        bool durableMode = false)
    {
        var runtime = instance.RuntimeState;
        var consumedBufferedEventIds = new List<string>();

        EnsureRootFrame(runtime, definition);

        while (runtime.Status == WorkflowStatus.Running)
        {
            if (runtime.MainPath.IsComplete)
            {
                InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Completed);
                return new WorkflowExecutionReport(consumedBufferedEventIds);
            }

            var frame = runtime.MainPath.CurrentFrame;
            if (frame.Index >= frame.Nodes.Count)
            {
                CompleteCurrentFrame(runtime.MainPath);
                continue;
            }

            var node = frame.Nodes[frame.Index];
            var result = await ExecuteNodeAsync(
                instance,
                node,
                runtime.MainPath,
                definition,
                correlationSink,
                cancellationToken,
                resumedEvent,
                durableMode);

            resumedEvent = result.BufferedResumeEvent;
            consumedBufferedEventIds.AddRange(result.ConsumedBufferedEventIds);

            if (result.Outcome == NodeExecutionOutcome.Suspended || runtime.Status != WorkflowStatus.Running)
                return new WorkflowExecutionReport(consumedBufferedEventIds);
        }

        return new WorkflowExecutionReport(consumedBufferedEventIds);
    }

    public static async Task<WorkflowExecutionReport> ResumeParallelBranchAsync<TState>(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        ICorrelationMutationSink correlationSink,
        string branchId,
        EventEnvelope resumedEvent,
        CancellationToken cancellationToken,
        bool durableMode = false)
    {
        var runtime = instance.RuntimeState;
        var consumedBufferedEventIds = new List<string>();
        var parallel = runtime.ActiveParallel
            ?? throw new InvalidOperationException("No active parallel execution to resume.");

        if (!parallel.BranchPaths.TryGetValue(branchId, out var branchPath))
            throw new InvalidOperationException($"Parallel branch '{branchId}' not found.");

        var result = await ExecutePathAsync(
            instance,
            branchPath,
            definition,
            correlationSink,
            cancellationToken,
            resumedEvent,
            durableMode);
        consumedBufferedEventIds.AddRange(result.ConsumedBufferedEventIds);

        if (runtime.Status == WorkflowStatus.Failed)
        {
            runtime.ActiveParallel = null;
            return new WorkflowExecutionReport(consumedBufferedEventIds);
        }

        if (result.Outcome == NodeExecutionOutcome.Completed)
        {
            parallel.BranchPaths.Remove(branchId);
        }

        if (parallel.BranchPaths.Count == 0)
        {
            runtime.ActiveParallel = null;
            var continuation = await ExecuteAsync(instance, definition, correlationSink, cancellationToken, durableMode: durableMode);
            consumedBufferedEventIds.AddRange(continuation.ConsumedBufferedEventIds);
            return new WorkflowExecutionReport(consumedBufferedEventIds);
        }

        if (parallel.BranchPaths.Values.Any(p => !p.IsComplete))
        {
            if (runtime.Status == WorkflowStatus.Running)
                InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Waiting);
        }

        return new WorkflowExecutionReport(consumedBufferedEventIds);
    }

    private static void EnsureRootFrame<TState>(RuntimeState runtime, WorkflowDefinition<TState> definition)
    {
        if (runtime.MainPath.Frames.Count == 0)
            runtime.MainPath.Frames.Add(new ExecutionFrame(FrameKind.Root, string.Empty, definition.Nodes));
    }

    private static async Task<NodeExecutionResult> ExecutePathAsync<TState>(
        WorkflowInstance<TState> instance,
        ExecutionPath path,
        WorkflowDefinition<TState> definition,
        ICorrelationMutationSink correlationSink,
        CancellationToken cancellationToken,
        EventEnvelope? resumedEvent,
        bool durableMode)
    {
        var consumedBufferedEventIds = new List<string>();

        while (instance.RuntimeState.Status == WorkflowStatus.Running)
        {
            if (path.IsComplete)
                return NodeExecutionResult.Completed(consumedBufferedEventIds);

            var frame = path.CurrentFrame;
            if (frame.Index >= frame.Nodes.Count)
            {
                CompleteCurrentFrame(path);
                continue;
            }

            var node = frame.Nodes[frame.Index];
            var result = await ExecuteNodeAsync(
                instance,
                node,
                path,
                definition,
                correlationSink,
                cancellationToken,
                resumedEvent,
                durableMode);

            resumedEvent = result.BufferedResumeEvent;
            consumedBufferedEventIds.AddRange(result.ConsumedBufferedEventIds);

            if (result.Outcome is NodeExecutionOutcome.Suspended or NodeExecutionOutcome.Completed
                || instance.RuntimeState.Status != WorkflowStatus.Running)
                return result.WithConsumedBufferedEventIds(consumedBufferedEventIds);
        }

        return NodeExecutionResult.Completed(consumedBufferedEventIds);
    }

    private static void CompleteCurrentFrame(ExecutionPath path)
    {
        var completed = path.CurrentFrame;
        path.Frames.RemoveAt(path.Frames.Count - 1);

        if (path.IsComplete)
            return;

        if (completed.Kind == FrameKind.WhileBody)
            // Don't advance parent here. Re-entering the While node causes
            // the condition to be evaluated again for the next iteration.
            return;
    }

    private static async Task<NodeExecutionResult> ExecuteNodeAsync<TState>(
        WorkflowInstance<TState> instance,
        IWorkflowNode node,
        ExecutionPath path,
        WorkflowDefinition<TState> definition,
        ICorrelationMutationSink correlationSink,
        CancellationToken cancellationToken,
        EventEnvelope? resumedEvent,
        bool durableMode)
    {
        var runtime = instance.RuntimeState;
        var frame = path.CurrentFrame;

        switch (node)
        {
            case BusinessStepNode<TState> businessNode:
            {
                var context = new StepContext<TState>(
                    instance.BusinessState,
                    instance.InstanceId,
                    instance.DefinitionId,
                    ResolveCurrentNodePath(frame),
                    cancellationToken,
                    resumedEvent);

                StepResult result;
                try
                {
                    result = await businessNode.Step.ExecuteAsync(context);
                }
                catch (Exception ex)
                {
                    runtime.Error = WorkflowError.FromException(ex, businessNode.Step.StepId, DateTimeOffset.UtcNow);
                    InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
                    return NodeExecutionResult.Completed();
                }

                if (result is StepResult.Completed)
                {
                    frame.Index++;
                    return NodeExecutionResult.Continued();
                }

                if (result is StepResult.Failed failed)
                {
                    runtime.Error = WorkflowError.FromException(failed.Error, businessNode.Step.StepId, DateTimeOffset.UtcNow);
                    InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
                    return NodeExecutionResult.Completed();
                }

                if (result is StepResult.WaitForEvent waitResult)
                {
                    frame.Index++;
                    return HandleWait(instance, waitResult, correlationSink, path.BranchId, updateWorkflowStatusOnSuspend: path.BranchId is null);
                }

                if (result is StepResult.Yield)
                    return NodeExecutionResult.Suspended();

                throw new InvalidOperationException(
                    $"Unexpected step result type: {result.GetType().Name}");
            }

            case WaitNode<TState> waitNode:
            {
                var waitResult = new StepResult.WaitForEvent(
                    waitNode.EventName,
                    waitNode.CorrelationSelector(instance.BusinessState));
                frame.Index++;
                return HandleWait(instance, waitResult, correlationSink, path.BranchId, updateWorkflowStatusOnSuspend: path.BranchId is null);
            }

            case WaitLongNode<TState> waitLongNode:
                if (!durableMode)
                {
                    runtime.Error = WorkflowError.FromException(
                        new InvalidOperationException(
                            "WaitLong requires durable mode. The current engine is ephemeral-only. Use Wait for ephemeral workflows or switch to a durable engine configuration."),
                        node.NodeId,
                        DateTimeOffset.UtcNow);
                    InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
                    return NodeExecutionResult.Completed();
                }

                var waitLongResult = new StepResult.WaitForEvent(
                    waitLongNode.EventName,
                    waitLongNode.CorrelationSelector(instance.BusinessState));
                frame.Index++;
                return HandleWait(instance, waitLongResult, correlationSink, path.BranchId, updateWorkflowStatusOnSuspend: path.BranchId is null, isLongWait: true);

            case IfNode<TState> ifNode:
            {
                frame.Index++;
                var useThenBranch = ifNode.Condition(instance.BusinessState);
                var branchNodes = useThenBranch
                    ? ifNode.ThenNodes
                    : ifNode.ElseNodes;

                if (branchNodes.Count > 0)
                {
                    var branchPath = $"{frame.NodePath}/{frame.Index - 1}/{(useThenBranch ? "then" : "else")}".TrimStart('/');
                    path.Frames.Add(new ExecutionFrame(FrameKind.IfBranch, branchPath, branchNodes, CreateScopeId()));
                }

                return NodeExecutionResult.Continued();
            }

            case WhileNode<TState> whileNode:
            {
                if (!whileNode.Condition(instance.BusinessState))
                {
                    frame.Index++;
                    return NodeExecutionResult.Continued();
                }

                var bodyPath = $"{frame.NodePath}/{frame.Index}/body".TrimStart('/');
                path.Frames.Add(new ExecutionFrame(FrameKind.WhileBody, bodyPath, whileNode.BodyNodes, CreateScopeId()));
                return NodeExecutionResult.Continued();
            }

            case ParallelNode<TState> parallelNode:
                frame.Index++;
                return await ExecuteParallelAsync(
                    instance,
                    parallelNode,
                    definition,
                    correlationSink,
                    cancellationToken,
                    durableMode);

            default:
                throw new InvalidOperationException(
                    $"Unexpected workflow node type: {node.GetType().Name}");
        }
    }

    private static NodeExecutionResult HandleWait<TState>(
        WorkflowInstance<TState> instance,
        StepResult.WaitForEvent wait,
        ICorrelationMutationSink correlationSink,
        string? branchId,
        bool updateWorkflowStatusOnSuspend,
        bool isLongWait = false)
    {
        var runtime = instance.RuntimeState;
        var waitRecord = new WaitRecord(
            WaitId: Guid.NewGuid().ToString("N"),
            EventName: wait.EventName,
            CorrelationId: wait.CorrelationId,
            BranchId: branchId,
            RegisteredAt: DateTimeOffset.UtcNow,
            Status: WaitStatus.Active,
            Mode: isLongWait ? WaitMode.Cold : WaitMode.Resident);

        var buffered = runtime.PendingEvents.FirstOrDefault(p =>
            !p.Consumed
            && p.Envelope.EventName == wait.EventName
            && p.Envelope.CorrelationId == wait.CorrelationId);

        if (buffered is not null)
        {
            var idx = runtime.PendingEvents.IndexOf(buffered);
            runtime.PendingEvents[idx] = buffered with { Consumed = true };
            runtime.ConsumedEventIds.Add(buffered.Envelope.EventId);
            runtime.ActiveWaits.Add(waitRecord with { Status = WaitStatus.Matched });
            return NodeExecutionResult.Continued(buffered.Envelope, [buffered.Envelope.EventId]);
        }

        runtime.ActiveWaits.Add(waitRecord);
        correlationSink.Add(wait.EventName, wait.CorrelationId, instance.InstanceId);

        if (updateWorkflowStatusOnSuspend && runtime.Status == WorkflowStatus.Running)
            InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Waiting);

        return NodeExecutionResult.Suspended();
    }

    private static async Task<NodeExecutionResult> ExecuteParallelAsync<TState>(
        WorkflowInstance<TState> instance,
        ParallelNode<TState> parallelNode,
        WorkflowDefinition<TState> definition,
        ICorrelationMutationSink correlationSink,
        CancellationToken cancellationToken,
        bool durableMode)
    {
        var runtime = instance.RuntimeState;
        var group = new ParallelFrameGroup();
        runtime.ActiveParallel = group;

        foreach (var branch in parallelNode.Branches)
        {
            var path = new ExecutionPath(branch.BranchId);
            var currentFrame = runtime.MainPath.CurrentFrame;
            var branchPath = $"{currentFrame.NodePath}/{currentFrame.Index - 1}/branch/{branch.BranchId}".TrimStart('/');
            path.Frames.Add(new ExecutionFrame(FrameKind.ParallelBranch, branchPath, branch.Nodes, CreateScopeId()));
            group.BranchPaths[branch.BranchId] = path;
        }

        foreach (var branch in parallelNode.Branches.ToList())
        {
            if (!group.BranchPaths.TryGetValue(branch.BranchId, out var path))
                continue;

            var result = await ExecutePathAsync(
                instance,
                path,
                definition,
                correlationSink,
                cancellationToken,
                resumedEvent: null,
                durableMode);

            if (runtime.Status == WorkflowStatus.Failed)
            {
                runtime.ActiveParallel = null;
                return NodeExecutionResult.Completed();
            }

            if (result.Outcome == NodeExecutionOutcome.Completed)
                group.BranchPaths.Remove(branch.BranchId);
        }

        if (group.BranchPaths.Count == 0)
        {
            runtime.ActiveParallel = null;
            return NodeExecutionResult.Continued();
        }

        if (runtime.Status == WorkflowStatus.Running)
            InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Waiting);

        return NodeExecutionResult.Suspended();
    }

    private readonly record struct NodeExecutionResult(NodeExecutionOutcome Outcome, EventEnvelope? BufferedResumeEvent = null)
    {
        public IReadOnlyList<string> ConsumedBufferedEventIds { get; init; } = [];

        public static NodeExecutionResult Continued(
            EventEnvelope? bufferedResumeEvent = null,
            IReadOnlyList<string>? consumedBufferedEventIds = null) =>
            new(NodeExecutionOutcome.Continued, bufferedResumeEvent)
            {
                ConsumedBufferedEventIds = consumedBufferedEventIds ?? []
            };

        public static NodeExecutionResult Completed(IReadOnlyList<string>? consumedBufferedEventIds = null) =>
            new(NodeExecutionOutcome.Completed)
            {
                ConsumedBufferedEventIds = consumedBufferedEventIds ?? []
            };

        public static NodeExecutionResult Suspended() => new(NodeExecutionOutcome.Suspended);

        public NodeExecutionResult WithConsumedBufferedEventIds(IReadOnlyList<string> consumedBufferedEventIds) =>
            this with
            {
                ConsumedBufferedEventIds = ConsumedBufferedEventIds.Count == 0
                    ? consumedBufferedEventIds
                    : ConsumedBufferedEventIds.Concat(consumedBufferedEventIds).ToArray()
            };
    }

    private enum NodeExecutionOutcome
    {
        Continued,
        Completed,
        Suspended
    }

    private static string ResolveCurrentNodePath(ExecutionFrame frame)
    {
        var segment = frame.Index.ToString();
        return string.IsNullOrEmpty(frame.NodePath) ? segment : $"{frame.NodePath}/{segment}";
    }

    private static string CreateScopeId() => Guid.NewGuid().ToString("N");
}

internal sealed record WorkflowExecutionReport(IReadOnlyList<string> ConsumedBufferedEventIds);
