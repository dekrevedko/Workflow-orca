using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

internal static class StateMapper
{
    public static PersistedInstance ToPersistedState<TState>(
        WorkflowInstance<TState> instance,
        string? definitionVersion = null,
        int concurrencyToken = 0,
        JsonSerializerOptions? serializerOptions = null,
        IDurablePayloadTypeResolver? payloadTypeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return new PersistedInstance(
            instance.InstanceId,
            instance.DefinitionId,
            definitionVersion,
            concurrencyToken,
            JsonSerializer.SerializeToElement(instance.BusinessState, serializerOptions),
            ToPersistedRuntimeState(instance.RuntimeState, serializerOptions, payloadTypeResolver ?? DurablePayloadTypeRegistry.Default));
    }

    public static WorkflowInstance<TState> FromPersistedState<TState>(
        PersistedInstance persisted,
        WorkflowDefinition<TState> definition,
        JsonSerializerOptions? serializerOptions = null,
        IDurablePayloadTypeResolver? payloadTypeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(persisted);
        ArgumentNullException.ThrowIfNull(definition);

        if (!string.Equals(persisted.DefinitionId, definition.DefinitionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Persisted instance '{persisted.InstanceId}' targets definition '{persisted.DefinitionId}', " +
                $"but '{definition.DefinitionId}' was provided.");
        }

        var businessState = persisted.BusinessState.Deserialize<TState>(serializerOptions)
            ?? throw new InvalidOperationException(
                $"Persisted instance '{persisted.InstanceId}' could not deserialize business state.");

        var runtimeState = FromPersistedRuntimeState(
            persisted.InstanceId,
            persisted.DefinitionId,
            persisted.DefinitionVersion,
            persisted.RuntimeState,
            definition,
            serializerOptions,
            payloadTypeResolver ?? DurablePayloadTypeRegistry.Default);
        return new WorkflowInstance<TState>(
            persisted.InstanceId,
            persisted.DefinitionId,
            businessState,
            runtimeState);
    }

    private static PersistedRuntimeState ToPersistedRuntimeState(
        RuntimeState runtimeState,
        JsonSerializerOptions? serializerOptions,
        IDurablePayloadTypeResolver payloadTypeResolver)
    {
        return new PersistedRuntimeState(
            runtimeState.Status,
            runtimeState.CreatedAt,
            runtimeState.LastTransitionAt,
            runtimeState.ActiveWaits.Select(wait => ToPersistedWaitRecord(wait, runtimeState)).ToArray(),
            runtimeState.PendingEvents.Select(e => ToPersistedPendingEvent(e, serializerOptions, payloadTypeResolver)).ToArray(),
            runtimeState.ConsumedEventIds.ToArray(),
            runtimeState.Error is null ? null : ToPersistedError(runtimeState.Error),
            ToPersistedExecutionPath(runtimeState.MainPath),
            runtimeState.ActiveParallel is null ? null : ToPersistedParallelFrameGroup(runtimeState.ActiveParallel));
    }

    private static RuntimeState FromPersistedRuntimeState<TState>(
        string instanceId,
        string definitionId,
        string? definitionVersion,
        PersistedRuntimeState persisted,
        WorkflowDefinition<TState> definition,
        JsonSerializerOptions? serializerOptions,
        IDurablePayloadTypeResolver payloadTypeResolver)
    {
        var runtimeState = new RuntimeState(persisted.CreatedAt, persisted.LastTransitionAt)
        {
            Status = persisted.Status,
            Error = persisted.Error is null ? null : FromPersistedError(persisted.Error)
        };

        runtimeState.ActiveWaits.AddRange(persisted.ActiveWaits.Select(FromPersistedWaitRecord));
        runtimeState.PendingEvents.AddRange(
            persisted.PendingEvents.Select(e => FromPersistedPendingEvent(e, serializerOptions, payloadTypeResolver)));
        runtimeState.ConsumedEventIds.UnionWith(persisted.ConsumedEventIds);
        CopyExecutionPath(
            instanceId: instanceId,
            definitionId: definitionId,
            definitionVersion: definitionVersion,
            runtimeState.MainPath,
            persisted.MainPath,
            definition);

        if (persisted.ActiveParallel is not null)
        {
            var parallel = new ParallelFrameGroup();
            foreach (var entry in persisted.ActiveParallel.BranchPaths)
            {
                var path = new ExecutionPath(entry.Value.BranchId);
                CopyExecutionPath(
                    instanceId: instanceId,
                    definitionId: definitionId,
                    definitionVersion: definitionVersion,
                    path,
                    entry.Value,
                    definition);
                parallel.BranchPaths[entry.Key] = path;
            }

            runtimeState.ActiveParallel = parallel;
        }

        return runtimeState;
    }

    private static PersistedError ToPersistedError(WorkflowError error) =>
        new(error.ExceptionType, error.Message, error.StepId, error.Timestamp);

    private static WorkflowError FromPersistedError(PersistedError error) =>
        WorkflowError.FromMetadata(error.Message, error.ExceptionType, error.StepId, error.Timestamp);

    private static PersistedWaitRecord ToPersistedWaitRecord(WaitRecord wait, RuntimeState runtimeState) =>
        new(wait.WaitId, wait.EventName, wait.CorrelationId, wait.BranchId, wait.RegisteredAt, wait.Status, wait.Mode);

    private static WaitRecord FromPersistedWaitRecord(PersistedWaitRecord wait) =>
        new(wait.WaitId, wait.EventName, wait.CorrelationId, wait.BranchId, wait.RegisteredAt, wait.Status, wait.Mode);

    private static PersistedPendingEvent ToPersistedPendingEvent(
        PendingEvent pendingEvent,
        JsonSerializerOptions? serializerOptions,
        IDurablePayloadTypeResolver payloadTypeResolver) =>
        new(ToPersistedEventEnvelope(pendingEvent.Envelope, serializerOptions, payloadTypeResolver), pendingEvent.ReceivedAt, pendingEvent.Consumed);

    private static PendingEvent FromPersistedPendingEvent(
        PersistedPendingEvent pendingEvent,
        JsonSerializerOptions? serializerOptions,
        IDurablePayloadTypeResolver payloadTypeResolver) =>
        new(FromPersistedEventEnvelope(pendingEvent.Envelope, serializerOptions, payloadTypeResolver), pendingEvent.ReceivedAt, pendingEvent.Consumed);

    private static PersistedEventEnvelope ToPersistedEventEnvelope(
        EventEnvelope envelope,
        JsonSerializerOptions? serializerOptions,
        IDurablePayloadTypeResolver payloadTypeResolver)
    {
        JsonElement? payload = null;
        string? payloadTypeKey = null;
        if (envelope.Payload is not null)
        {
            if (!payloadTypeResolver.TryGetTypeKey(envelope.Payload.GetType(), out payloadTypeKey))
            {
                throw new DurablePayloadSerializationException(
                    $"Payload type '{envelope.Payload.GetType().FullName}' is not registered for durable serialization.");
            }

            payload = JsonSerializer.SerializeToElement(envelope.Payload, envelope.Payload.GetType(), serializerOptions);
        }

        return new PersistedEventEnvelope(
            envelope.EventName,
            envelope.CorrelationId,
            payload,
            payloadTypeKey,
            envelope.EventId);
    }

    private static EventEnvelope FromPersistedEventEnvelope(
        PersistedEventEnvelope envelope,
        JsonSerializerOptions? serializerOptions,
        IDurablePayloadTypeResolver payloadTypeResolver)
    {
        object? payload = null;
        if (envelope.Payload is not null)
        {
            if (envelope.PayloadTypeKey is null)
            {
                throw new DurablePayloadDeserializationException(
                    $"Persisted event '{envelope.EventId}' is missing a payload type key.");
            }

            if (!payloadTypeResolver.TryResolveType(envelope.PayloadTypeKey, out var payloadType))
            {
                throw new DurablePayloadDeserializationException(
                    $"Persisted event '{envelope.EventId}' references unknown payload type key '{envelope.PayloadTypeKey}'.");
            }

            payload = envelope.Payload.Value.Deserialize(payloadType, serializerOptions);
        }

        return new EventEnvelope(
            envelope.EventName,
            envelope.CorrelationId,
            payload,
            envelope.EventId);
    }

    private static PersistedExecutionPath ToPersistedExecutionPath(ExecutionPath path) =>
        new(
            path.BranchId,
            path.Frames.Select(frame => new PersistedFrame(ToPersistedFrameKind(frame.Kind), frame.NodePath, frame.Index, frame.ScopeId)).ToArray());

    private static PersistedParallelFrameGroup ToPersistedParallelFrameGroup(ParallelFrameGroup group) =>
        new(group.BranchPaths.ToDictionary(
            entry => entry.Key,
            entry => ToPersistedExecutionPath(entry.Value),
            StringComparer.Ordinal));

    private static void CopyExecutionPath<TState>(
        string instanceId,
        string definitionId,
        string? definitionVersion,
        ExecutionPath target,
        PersistedExecutionPath persisted,
        WorkflowDefinition<TState> definition)
    {
        foreach (var frame in persisted.Frames)
        {
            IReadOnlyList<IWorkflowNode> nodes;
            try
            {
                nodes = definition.ResolveNodes(frame.NodePath);
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
            {
                throw new DurableDefinitionRehydrationException(
                    $"Persisted durable instance '{instanceId}' for definition '{definitionId}' version '{definitionVersion ?? "<missing>"}' " +
                    $"references unknown node path '{frame.NodePath}'.",
                    ex);
            }

            target.Frames.Add(new ExecutionFrame(ToRuntimeFrameKind(frame.Kind), frame.NodePath, nodes, frame.ScopeId)
            {
                Index = frame.Index
            });
        }
    }

    private static PersistedFrameKind ToPersistedFrameKind(FrameKind kind) => kind switch
    {
        FrameKind.Root => PersistedFrameKind.Root,
        FrameKind.IfBranch => PersistedFrameKind.IfBranch,
        FrameKind.WhileBody => PersistedFrameKind.WhileBody,
        FrameKind.ParallelBranch => PersistedFrameKind.ParallelBranch,
        _ => throw new InvalidOperationException($"Unsupported runtime frame kind '{kind}'.")
    };

    private static FrameKind ToRuntimeFrameKind(PersistedFrameKind kind) => kind switch
    {
        PersistedFrameKind.Root => FrameKind.Root,
        PersistedFrameKind.IfBranch => FrameKind.IfBranch,
        PersistedFrameKind.WhileBody => FrameKind.WhileBody,
        PersistedFrameKind.ParallelBranch => FrameKind.ParallelBranch,
        _ => throw new InvalidOperationException($"Unsupported persisted frame kind '{kind}'.")
    };
}
