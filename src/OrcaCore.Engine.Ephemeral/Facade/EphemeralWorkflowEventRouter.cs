using System.Reflection;
using OrcaCore.Core.Internal;
using LegacyStatus = OrcaCore.WorkflowInstanceStatus;
using System.Security.Cryptography;
using System.Text;
using OrcaCore.Core.Execution;
using OrcaCore.Internal;

namespace OrcaCore.Engine.Ephemeral;

internal sealed class EphemeralWorkflowEventRouter(
    EphemeralWorkflowEngine engine,
    EphemeralWorkflowDefinitionRegistry registry)
{
    private readonly object gate = new();
    private readonly Dictionary<(InstanceId InstanceId, string EventId), string> accepted = [];

    internal async ValueTask<EphemeralEventRouteResult> RouteAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default)
    {
        using var admission = Admit(inboundEvent, cancellationToken);
        return await RouteCoreAsync(inboundEvent, payloadBytes: null, cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<EphemeralEventRouteResult> RouteAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default)
    {
        using var admission = Admit(inboundEvent, cancellationToken);
        return await RouteCoreAsync(
            inboundEvent,
            CoreWorkflowValueCodec.Serialize(inboundEvent.Payload, typeof(TPayload)),
            cancellationToken).ConfigureAwait(false);
    }

    private global::OrcaCore.Hosting.EphemeralHostLifetime.Lease Admit(WorkflowInboundEvent inboundEvent, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);
        token.ThrowIfCancellationRequested();
        lock (engine.VisibilityGate)
        {
            if (inboundEvent.Route is WorkflowEventRoute.Direct direct &&
                (!registry.TryGetInstance(direct.InstanceId, out _) || !engine.TryGetFacadeInstance(direct.InstanceId, out var instance) || instance!.Lifetime.IsRetired))
                throw FacadeValueFactory.InstanceNotFound(direct.InstanceId);
            engine.HostLifetime.RequireRunning();
            return engine.HostLifetime.Own();
        }
    }

    private ValueTask<EphemeralEventRouteResult> RouteCoreAsync(
        WorkflowInboundEvent inboundEvent,
        byte[]? payloadBytes,
        CancellationToken cancellationToken) =>
        inboundEvent.Route switch
        {
            WorkflowEventRoute.Direct direct => RouteToInstanceCoreAsync(
                direct.InstanceId,
                inboundEvent,
                payloadBytes,
                cancellationToken),
            WorkflowEventRoute.Correlation correlation => RouteByCorrelationCoreAsync(
                correlation.DefinitionId,
                inboundEvent,
                payloadBytes,
                cancellationToken),
            _ => throw new InvalidOperationException(
                "Ephemeral routing supports only direct and correlation routes without durable ownership.")
        };

    private async ValueTask<EphemeralEventRouteResult> RouteByCorrelationCoreAsync(
        DefinitionId definitionId,
        WorkflowInboundEvent inboundEvent,
        byte[]? payloadBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        var matches = registry.ListInstances()
            .Where(binding => binding.DefinitionId.Equals(definitionId))
            .Where(binding => engine.TryGetFacadeInstance(binding.InstanceId, out var instance) &&
                instance!.GetPublishedSnapshot().ActiveWaits.Any(wait =>
                    wait.EventContract.Equals(inboundEvent.EventContract) &&
                    wait.CorrelationId.Equals(inboundEvent.CorrelationId)))
            .ToArray();
        if (matches.Length == 0)
        {
            return new EphemeralEventRouteResult(EphemeralEventRouteStatus.NoActiveWait, null);
        }

        if (matches.Length > 1)
        {
            throw FacadeValueFactory.AmbiguousWait(
                definitionId,
                inboundEvent.EventContract,
                inboundEvent.CorrelationId);
        }

        return await RouteToInstanceCoreAsync(
            matches[0].InstanceId,
            inboundEvent,
            payloadBytes,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<EphemeralEventRouteResult> RouteToInstanceCoreAsync(
        InstanceId instanceId,
        WorkflowInboundEvent inboundEvent,
        byte[]? payloadBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!registry.TryGetInstance(instanceId, out var binding) ||
            !engine.TryGetFacadeInstance(instanceId, out var instance))
        {
            throw FacadeValueFactory.InstanceNotFound(instanceId);
        }

        if (!instance!.Lifetime.TryOwn(out var routingOwnership))
            throw FacadeValueFactory.InstanceNotFound(instanceId);
        using var ownedRoute = routingOwnership;
        instance.Lifetime.RegisterCleanup(this, () => ForgetAcceptedInstance(instanceId));

        var fingerprint = EventFingerprint(inboundEvent, payloadBytes);
        lock (gate)
        {
            if (accepted.TryGetValue((instanceId, inboundEvent.EventId.Value), out var existing))
            {
                return new EphemeralEventRouteResult(
                    string.Equals(existing, fingerprint, StringComparison.Ordinal)
                        ? EphemeralEventRouteStatus.Duplicate
                        : EphemeralEventRouteStatus.EventConflict,
                    instanceId);
            }
        }

        var snapshot = instance!.GetPublishedSnapshot();
        if (snapshot.Status is LegacyStatus.Completed or
            LegacyStatus.Failed or
            LegacyStatus.TimedOut or
            LegacyStatus.Cancelled or
            LegacyStatus.Terminated)
        {
            return new EphemeralEventRouteResult(EphemeralEventRouteStatus.InstanceTerminal, instanceId);
        }

        var hasWait = snapshot.ActiveWaits.Any(wait =>
            wait.EventContract.Equals(inboundEvent.EventContract) &&
            wait.CorrelationId.Equals(inboundEvent.CorrelationId));
        if (!hasWait)
        {
            return new EphemeralEventRouteResult(EphemeralEventRouteStatus.NoActiveWait, instanceId);
        }

        var envelope = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.EventEnvelope(
            inboundEvent.EventId,
            inboundEvent.EventContract,
            inboundEvent.CorrelationId,
            inboundEvent.OccurredAt,
            payloadBytes ?? []);
        await RaiseEventAsync(binding!.StateType, instanceId, envelope, cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            accepted[(instanceId, inboundEvent.EventId.Value)] = fingerprint;
        }

        return new EphemeralEventRouteResult(EphemeralEventRouteStatus.Accepted, instanceId);
    }

    internal int AcceptedEntryCount(InstanceId instanceId)
    {
        lock (gate) return accepted.Keys.Count(key => key.InstanceId.Equals(instanceId));
    }

    private void ForgetAcceptedInstance(InstanceId instanceId)
    {
        lock (gate)
        {
            foreach (var key in accepted.Keys.Where(key => key.InstanceId.Equals(instanceId)).ToArray())
                accepted.Remove(key);
        }
    }

    private async Task RaiseEventAsync(
        Type stateType,
        InstanceId instanceId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var method = typeof(EphemeralWorkflowEngine)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == nameof(EphemeralWorkflowEngine.RaiseEventCoreAsync) &&
                candidate.IsGenericMethodDefinition);
        var task = (Task)method.MakeGenericMethod(stateType)
            .Invoke(engine, [instanceId, envelope, cancellationToken])!;
        await task.ConfigureAwait(false);
    }

    private static string EventFingerprint(
        WorkflowInboundEvent inboundEvent,
        byte[]? payload)
    {
        var prefix = Encoding.UTF8.GetBytes(
            $"{inboundEvent.EventContract.EventName.Value}\n{inboundEvent.EventContract.Version.Value}\n" +
            $"{inboundEvent.CorrelationId.Value}\n{inboundEvent.OccurredAt.ToUniversalTime():O}\n");
        var buffer = new byte[prefix.Length + (payload?.Length ?? 0)];
        prefix.CopyTo(buffer, 0);
        payload?.CopyTo(buffer, prefix.Length);
        return Convert.ToHexString(SHA256.HashData(buffer));
    }
}
