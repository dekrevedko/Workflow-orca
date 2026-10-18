using OrcaCore.Core.Internal;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Internal;
using LegacyStatus = OrcaCore.WorkflowInstanceStatus;

namespace OrcaCore.Engine.Ephemeral;

internal sealed partial class EphemeralWorkflowDefinitionRegistry
{
    private EphemeralInstanceLifetime.WorkLease Pin(
        InstanceId id, out IWorkflowInstance runtime, out InstanceBinding binding,
        DefinitionId? expectedId = null, DefinitionVersion? expectedVersion = null)
    {
        lock (gate)
        {
            if (!instances.TryGetValue(id, out var found) ||
                !engine.TryGetFacadeInstance(id, out var instance) || instance!.Lifetime.IsRetired)
                throw FacadeValueFactory.InstanceNotFound(id);
            if (expectedId is not null && (!found.DefinitionId.Equals(expectedId) || !found.DefinitionVersion.Equals(expectedVersion)))
                throw FacadeValueFactory.InstanceDefinitionMismatch(id, expectedId, found.DefinitionId);
            if (!instance.Lifetime.TryPin(out var pin)) throw FacadeValueFactory.InstanceNotFound(id);
            runtime = instance;
            binding = found;
            return pin!;
        }
    }

    private ValueTask<WorkflowInstanceHandle> GetInstanceAsync(DefinitionId id, DefinitionVersion version, InstanceId instanceId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(instanceId, out _, out var binding, id, version);
        return ValueTask.FromResult(CreateInstanceHandle(binding));
    }

    private ValueTask<WorkflowInstanceHandle<T>> GetInstanceAsync<T>(DefinitionId id, DefinitionVersion version, InstanceId instanceId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(instanceId, out _, out var binding, id, version);
        return ValueTask.FromResult(CreateInstanceHandle<T>(binding));
    }

    private async ValueTask ObservePin(InstanceId id)
    {
        if (engine.OperationPinAcquired is { } observer) await observer(id).ConfigureAwait(false);
    }

    private async ValueTask<global::OrcaCore.WorkflowInstanceSnapshot> GetSnapshotAsync(InstanceBinding binding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(binding.InstanceId, out var runtime, out _);
        await ObservePin(binding.InstanceId).ConfigureAwait(false);
        return MapSnapshot(binding, runtime.GetPublishedSnapshot(), runtime.IsCancellationRequested);
    }

    private async ValueTask<object?> GetStateAsync(InstanceBinding binding, Type requestedType, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(binding.InstanceId, out var runtime, out _);
        await ObservePin(binding.InstanceId).ConfigureAwait(false);
        if (runtime.StateType != requestedType)
            throw FacadeValueFactory.StateTypeMismatch(binding.InstanceId, runtime.StateType, requestedType);
        return runtime.CopyState();
    }

    private async ValueTask<WorkflowCancellationRequestStatus> RequestCancellationAsync(InstanceBinding binding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(binding.InstanceId, out _, out _);
        return await engine.RequestCancellationAsync(binding.InstanceId, token).ConfigureAwait(false);
    }

    private async ValueTask<WorkflowTerminationStatus> TerminateAsync(InstanceBinding binding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(binding.InstanceId, out _, out _);
        return await engine.RequestTerminationAsync(binding.InstanceId, token).ConfigureAwait(false);
    }

    private async ValueTask<WorkflowOutputResult<T>> GetOutputAsync<T>(InstanceBinding binding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(binding.InstanceId, out var runtime, out _);
        await ObservePin(binding.InstanceId).ConfigureAwait(false);
        return CaptureOutput<T>(runtime);
    }

    private static WorkflowOutputResult<T> CaptureOutput<T>(IWorkflowInstance runtime)
    {
        var snapshot = runtime.GetPublishedSnapshot();
        if (!IsTerminal(snapshot.Status)) return new WorkflowOutputResult<T>.Pending();
        var payload = runtime.CopyOutputPayload();
        if (snapshot.Status == LegacyStatus.Completed && payload is not null && runtime.OutputType == typeof(T))
            return new WorkflowOutputResult<T>.Available((T)CoreWorkflowValueCodec.Deserialize(payload, typeof(T))!);
        return new WorkflowOutputResult<T>.Unavailable(MapStatus(snapshot.Status), MapFailure(snapshot));
    }

    private async ValueTask<T> WaitForOutputAsync<T>(InstanceBinding binding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pin = Pin(binding.InstanceId, out var runtime, out _);
        await ObservePin(binding.InstanceId).ConfigureAwait(false);
        while (true)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnCommitted(InstanceId id) { if (id.Equals(binding.InstanceId)) signal.TrySetResult(); }
            engine.InstanceCommitted += OnCommitted;
            try
            {
                switch (CaptureOutput<T>(runtime))
                {
                    case WorkflowOutputResult<T>.Available available: return available.Output;
                    case WorkflowOutputResult<T>.Unavailable unavailable:
                        throw FacadeValueFactory.OutputUnavailable(unavailable.Status, unavailable.Failure);
                }
                await signal.Task.WaitAsync(token).ConfigureAwait(false);
            }
            finally { engine.InstanceCommitted -= OnCommitted; }
        }
    }
}
