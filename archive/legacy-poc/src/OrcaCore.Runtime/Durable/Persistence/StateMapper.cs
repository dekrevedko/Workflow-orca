using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

internal static class StateMapper
{
    public static PersistedInstance ToPersistedState<TState>(
        WorkflowInstance<TState> instance,
        string? definitionVersion = null,
        int concurrencyToken = 0,
        JsonSerializerOptions? serializerOptions = null,
        IPayloadSchemaResolver? payloadTypeResolver = null,
        IPayloadEnvelopeSerializer? payloadEnvelopeSerializer = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var serializer = payloadEnvelopeSerializer ?? new JsonPayloadEnvelopeSerializer(payloadTypeResolver ?? DurablePayloadTypeRegistry.Default);
        return new PersistedInstance(
            instance.InstanceId,
            instance.DefinitionId,
            definitionVersion,
            concurrencyToken,
            JsonSerializer.SerializeToElement(instance.BusinessState, serializerOptions),
            ToPersistedRuntimeState(instance.RuntimeState, serializer));
    }

    public static WorkflowInstance<TState> FromPersistedState<TState>(
        PersistedInstance persisted,
        WorkflowDefinition<TState> definition,
        JsonSerializerOptions? serializerOptions = null,
        IPayloadSchemaResolver? payloadTypeResolver = null,
        IPayloadEnvelopeSerializer? payloadEnvelopeSerializer = null)
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

        var serializer = payloadEnvelopeSerializer ?? new JsonPayloadEnvelopeSerializer(payloadTypeResolver ?? DurablePayloadTypeRegistry.Default);
        var runtimeState = FromPersistedRuntimeState(
            persisted.InstanceId,
            persisted.DefinitionId,
            persisted.DefinitionVersion,
            persisted.RuntimeState,
            definition,
            serializer);
        return new WorkflowInstance<TState>(
            persisted.InstanceId,
            persisted.DefinitionId,
            businessState,
            runtimeState);
    }

    private static PersistedRuntimeState ToPersistedRuntimeState(
        RuntimeState runtimeState,
        IPayloadEnvelopeSerializer payloadEnvelopeSerializer)
    {
        return new PersistedRuntimeState(
            runtimeState.Status,
            runtimeState.CreatedAt,
            runtimeState.LastTransitionAt,
            runtimeState.ActiveWaits.Select(wait => ToPersistedWaitRecord(wait, runtimeState)).ToArray(),
            runtimeState.PendingEvents.Select(e => ToPersistedPendingEvent(e, payloadEnvelopeSerializer)).ToArray(),
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
        IPayloadEnvelopeSerializer payloadEnvelopeSerializer)
    {
        var runtimeState = new RuntimeState(persisted.CreatedAt, persisted.LastTransitionAt)
        {
            Status = persisted.Status,
            Error = persisted.Error is null ? null : FromPersistedError(persisted.Error)
        };

        runtimeState.ActiveWaits.AddRange(persisted.ActiveWaits.Select(FromPersistedWaitRecord));
        runtimeState.PendingEvents.AddRange(
            persisted.PendingEvents.Select(e => FromPersistedPendingEvent(e, payloadEnvelopeSerializer)));
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
        IPayloadEnvelopeSerializer payloadEnvelopeSerializer) =>
        new(ToPersistedEventEnvelope(pendingEvent.Envelope, payloadEnvelopeSerializer), pendingEvent.ReceivedAt, pendingEvent.Consumed);

    private static PendingEvent FromPersistedPendingEvent(
        PersistedPendingEvent pendingEvent,
        IPayloadEnvelopeSerializer payloadEnvelopeSerializer) =>
        new(FromPersistedEventEnvelope(pendingEvent.Envelope, payloadEnvelopeSerializer), pendingEvent.ReceivedAt, pendingEvent.Consumed);

    private static PersistedEventEnvelope ToPersistedEventEnvelope(
        EventEnvelope envelope,
        IPayloadEnvelopeSerializer payloadEnvelopeSerializer)
    {
        SerializedPayloadEnvelope? payloadEnvelope = null;
        var payloadType = envelope.PayloadType;
        if (payloadType is not null)
        {
            var serialized = payloadEnvelopeSerializer.Serialize(envelope.Payload, payloadType);
            if (serialized.IsFailure)
                throw serialized.Error!;

            payloadEnvelope = serialized.Value;
        }

        return new PersistedEventEnvelope(
            envelope.EventName,
            envelope.CorrelationId,
            payloadEnvelope,
            envelope.EventId);
    }

    private static EventEnvelope FromPersistedEventEnvelope(
        PersistedEventEnvelope envelope,
        IPayloadEnvelopeSerializer payloadEnvelopeSerializer)
    {
        object? payload = null;
        Type? declaredPayloadType = null;
        if (envelope.PayloadEnvelope is not null)
        {
            var deserialized = payloadEnvelopeSerializer.Deserialize(
                envelope.PayloadEnvelope.Payload,
                envelope.PayloadEnvelope.TypeKey,
                typeof(object));
            if (deserialized.IsFailure)
                throw deserialized.Error!;

            payload = deserialized.Value;
            declaredPayloadType = payload?.GetType();
        }

        return new EventEnvelope(
            envelope.EventName,
            envelope.CorrelationId,
            payload,
            envelope.EventId,
            declaredPayloadType);
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
