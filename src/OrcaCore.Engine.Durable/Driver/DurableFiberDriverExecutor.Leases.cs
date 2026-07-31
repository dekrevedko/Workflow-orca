using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private global::OrcaCore.ResourceLeaseRequest ResolveLeaseRequest(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState rootState)
    {
        if (instruction.StaticLeaseRequest is { } staticRequest)
        {
            return staticRequest;
        }

        var selector = instruction.LeaseRequestSelector ??
            throw global::OrcaCore.Core.Authoring.PublicAuthoringContracts.DefinitionException(
                $"Compiled lease '{instruction.Path}' has no request.");
        var state = ResolveFiberState(execution, fiber, rootState);
        try
        {
            return StructuredInvocationCache.Invoke(selector, state) as global::OrcaCore.ResourceLeaseRequest ??
                throw global::OrcaCore.Core.Authoring.PublicAuthoringContracts.DefinitionException(
                    $"Lease selector '{instruction.Path}' returned null.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw global::OrcaCore.Core.Authoring.PublicAuthoringContracts.DefinitionException(
                $"Lease selector '{instruction.Path}' failed.",
                exception.InnerException);
        }
    }

    private static IReadOnlyList<ResourcePoolRequirement> NormalizeLeaseRequest(
        global::OrcaCore.ResourceLeaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Requirements
            .Select(requirement => new ResourcePoolRequirement(
                requirement.Pool.Value,
                requirement.Units))
            .ToArray();
    }

    private static DurableOwnedObligationState? FindScopedLease(
        FiberRecord fiber,
        IEnumerable<DurableOwnedObligationState> obligations) =>
        obligations.SingleOrDefault(obligation =>
            obligation.Kind == DurableOwnedObligationKind.Resource &&
            obligation.FiberId == fiber.Id.Value &&
            obligation.ProtectionToken is not null);

    private static ResourceLeaseExecutionContext? ResolveLeaseExecutionContext(
        FiberRecord fiber,
        IEnumerable<DurableOwnedObligationState> obligations)
    {
        var lease = FindScopedLease(fiber, obligations);
        return lease is null ||
               lease.LeasePhase is not (
                   nameof(DurableLeaseObligationPhase.Held) or
                   nameof(DurableLeaseObligationPhase.ReviewMarked) or
                   nameof(DurableLeaseObligationPhase.AmbiguousHeld))
            ? null
            : RuntimeStepContextFactory.CreateLease(
                LeaseProtectionToken.Parse(lease.ProtectionToken!));
    }

    private static string LeaseOccurrenceKey(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction) =>
        $"{execution.InstanceId.Value:N}|{execution.ContinueAsNewGeneration}|" +
        $"{fiber.Id.Value}|{instruction.Id.Value}|{fiber.LoopIteration}";

    private static string LeaseHolderKey(string occurrenceKey) =>
        $"scope-{Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"holder|{occurrenceKey}")))}";

    private static string LeaseProtectionKey(string occurrenceKey) =>
        $"lease-{Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"protection|{occurrenceKey}")))}";

    private static WaitId LeaseWaitId(string occurrenceKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"wait|{occurrenceKey}"));
        return WaitId.Parse(new Guid(bytes.AsSpan(0, 16)).ToString());
    }

    private static bool HasCapacityReservingLeaseAncestor(
        StructuredExecutionState execution,
        FiberRecord fiber,
        IEnumerable<DurableOwnedObligationState> obligations)
    {
        var ownerFiberIds = new HashSet<string>(StringComparer.Ordinal) { fiber.Id.Value };
        var scopeId = fiber.OwningScopeId;
        while (scopeId is { } currentScopeId &&
               execution.Scopes.TryGetValue(currentScopeId, out var scope))
        {
            ownerFiberIds.Add(scope.ParentFiberId.Value);
            scopeId = execution.Fibers.TryGetValue(scope.ParentFiberId, out var parent)
                ? parent.OwningScopeId
                : null;
        }

        return obligations.Any(obligation =>
            obligation.Kind == DurableOwnedObligationKind.Resource &&
            obligation.ProtectionToken is not null &&
            ownerFiberIds.Contains(obligation.FiberId) &&
            obligation.LeasePhase is
                nameof(DurableLeaseObligationPhase.PendingCommit) or
                nameof(DurableLeaseObligationPhase.Held) or
                nameof(DurableLeaseObligationPhase.ReviewMarked) or
                nameof(DurableLeaseObligationPhase.AmbiguousHeld));
    }
}
