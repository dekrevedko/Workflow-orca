using System.Reflection;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal static class FailureProvenance
{
    public static FiberFailure Create(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        string code,
        string message,
        IReadOnlyList<FiberFailure>? causes = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fiber);
        ArgumentNullException.ThrowIfNull(instruction);
        var occurrence = OccurrenceForFiber(plan, state, fiber);
        return new FiberFailure(
            code,
            message,
            causes,
            LocationFromCompilerPath(instruction.Path),
            occurrence);
    }

    public static (AuthoredLocation Location, FailureOccurrence Occurrence) ForScope(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        ExecutionScopeRecord scope)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scope);
        var parent = state.Fibers[scope.ParentFiberId];
        var path = scope.ScopePlanId.Value.StartsWith("scope:", StringComparison.Ordinal)
            ? scope.ScopePlanId.Value["scope:".Length..]
            : scope.ScopePlanId.Value;
        return (LocationFromCompilerPath(path), OccurrenceForFiber(plan, state, parent));
    }

    public static AuthoredLocation LocationFromCompilerPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            string.Equals(path, "root", StringComparison.Ordinal))
        {
            return Location("workflow:$");
        }

        var tokens = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var canonical = new List<string> { "workflow:$" };
        for (var index = 1; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (int.TryParse(token, out var ordinal))
            {
                canonical.Add($"n:{ordinal:D8}");
                continue;
            }

            if (string.Equals(token, "then", StringComparison.Ordinal))
            {
                canonical.Add("if:true");
            }
            else if (string.Equals(token, "else", StringComparison.Ordinal))
            {
                canonical.Add("if:false");
            }
            else if (string.Equals(token, "body", StringComparison.Ordinal))
            {
                canonical.Add("while:body");
            }
            else if (string.Equals(token, "lease", StringComparison.Ordinal))
            {
                canonical.Add("lease:body");
            }
            else if (string.Equals(token, "item", StringComparison.Ordinal))
            {
                canonical.Add("foreach:body");
            }
            else if (string.Equals(token, "branches", StringComparison.Ordinal) &&
                     index + 1 < tokens.Length &&
                     int.TryParse(tokens[index + 1], out var branchOrdinal))
            {
                canonical.Add($"parallel:{branchOrdinal:D8}");
                index++;
            }
        }

        return Location(string.Join('/', canonical));
    }

    public static FailureOccurrence RootOccurrence() =>
        (FailureOccurrence)typeof(FailureOccurrence.Root)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 0)
            .Invoke([]);

    public static FailureOccurrence BranchOccurrence(string branchId) =>
        (FailureOccurrence)typeof(FailureOccurrence.Branch)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is
                [{ ParameterType: var parameterType }] &&
                parameterType == typeof(AuthoredBranchId))
            .Invoke([AuthoredBranchId.Create(branchId)]);

    public static FailureOccurrence ItemOccurrence(int index) =>
        (FailureOccurrence)typeof(FailureOccurrence.Item)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is
                [{ ParameterType: var parameterType }] &&
                parameterType == typeof(int))
            .Invoke([index]);

    public static AuthoredLocation Clone(AuthoredLocation location) =>
        Location(location.Value);

    public static FailureOccurrence Clone(FailureOccurrence occurrence) => occurrence switch
    {
        FailureOccurrence.Root => RootOccurrence(),
        FailureOccurrence.Branch branch => BranchOccurrence(branch.BranchId.Value),
        FailureOccurrence.Item item => ItemOccurrence(item.Index),
        _ => throw new InvalidOperationException(
            $"Unknown failure occurrence '{occurrence.GetType().FullName}'.")
    };

    public static AuthoredLocation Location(string value) =>
        (AuthoredLocation)typeof(AuthoredLocation)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is
                [{ ParameterType: var parameterType }] &&
                parameterType == typeof(string))
            .Invoke([value]);

    private static FailureOccurrence OccurrenceForFiber(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber)
    {
        if (fiber.OwningScopeId is not { } scopeId)
        {
            return RootOccurrence();
        }

        var scope = state.Scopes[scopeId];
        if (scope.Kind == CompiledScopeKind.ForEach)
        {
            var index = scope.ForEach?.ItemIndexByFiber.TryGetValue(fiber.Id, out var itemIndex) == true
                ? itemIndex
                : throw new InvalidOperationException(
                    $"ForEach scope '{scope.Id}' has no item index for fiber '{fiber.Id}'.");
            return ItemOccurrence(index);
        }

        var ordinal = scope.ChildFiberIds
            .Select((candidate, index) => (candidate, index))
            .Where(pair => pair.candidate == fiber.Id)
            .Select(pair => pair.index)
            .DefaultIfEmpty(-1)
            .Single();
        if (ordinal < 0)
        {
            throw new InvalidOperationException(
                $"Scope '{scope.Id}' does not own fiber '{fiber.Id}'.");
        }

        var branch = plan.GetScope(scope.ScopePlanId).Branches
            .Single(candidate => candidate.Ordinal == ordinal);
        return BranchOccurrence(branch.BranchId);
    }
}
