using System.Collections;
using System.Reflection;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Internal;

namespace OrcaCore.Core.Execution;

internal sealed record MaterializedBranchResult(
    BranchPlanId BranchPlanId,
    StructuredSerializedValue Result);

internal sealed record MaterializedBranchOutcome(
    BranchPlanId BranchPlanId,
    StructuredSerializedValue? Result,
    FiberFailure? Failure);

internal sealed class StructuredMergeException : global::OrcaCore.OrcaCoreException
{
    public StructuredMergeException(string code, string message, Exception innerException)
        : base(code, message, innerException)
    {
    }
}

internal static class ScopeMergeAdapter
{
    public static StructuredSerializedValue ExecuteOutcomes(
        CompiledScopePlan scopePlan,
        object parentState,
        IReadOnlyList<MaterializedBranchOutcome> committedOutcomes)
    {
        ArgumentNullException.ThrowIfNull(scopePlan);
        ArgumentNullException.ThrowIfNull(parentState);
        ArgumentNullException.ThrowIfNull(committedOutcomes);
        if (scopePlan.Kind != CompiledScopeKind.WhenAllOutcomes)
        {
            throw new InvalidOperationException($"Scope plan '{scopePlan.Id}' is not an outcome-preserving join.");
        }

        if (scopePlan.Merge.Merge is null)
        {
            throw new InvalidOperationException($"Scope plan '{scopePlan.Id}' has no merge contract.");
        }

        if (committedOutcomes.Count != scopePlan.Branches.Count)
        {
            throw new InvalidOperationException(
                $"Scope plan '{scopePlan.Id}' requires {scopePlan.Branches.Count} merge outcome(s), " +
                $"but {committedOutcomes.Count} were supplied.");
        }

        try
        {
            var parentCopyPayload = Serialize(
                parentState,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
            var parentCopy = Deserialize(parentCopyPayload) ??
                throw new InvalidOperationException("Parent state copy deserialized as null.");
            var parentSnapshot = StructuredInvocationCache.CreateParentSnapshot(
                scopePlan.Merge.ParentStateType,
                parentCopy);
            var orderedOutcomes = BuildOrderedOutcomes(scopePlan, committedOutcomes);
            var replacement = StructuredInvocationCache.Invoke(
                scopePlan.Merge.Merge,
                parentSnapshot,
                orderedOutcomes);
            if (replacement is not null &&
                !scopePlan.Merge.ParentStateType.IsInstanceOfType(replacement))
            {
                throw new InvalidOperationException(
                    $"Merge returned '{replacement.GetType().FullName}', expected " +
                    $"'{scopePlan.Merge.ParentStateType.FullName}'.");
            }

            return Serialize(
                replacement,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
        }
        catch (StructuredMergeException)
        {
            throw;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new StructuredMergeException(
                "SFE-MERGE-002",
                $"Outcome merge materialization for scope plan '{scopePlan.Id}' failed: " +
                exception.InnerException.Message,
                exception.InnerException);
        }
        catch (Exception exception)
        {
            throw new StructuredMergeException(
                "SFE-MERGE-002",
                $"Outcome merge materialization for scope plan '{scopePlan.Id}' failed: {exception.Message}",
                exception);
        }
    }

    public static StructuredSerializedValue Execute(
        CompiledScopePlan scopePlan,
        object parentState,
        IReadOnlyList<MaterializedBranchResult> committedResults)
    {
        ArgumentNullException.ThrowIfNull(scopePlan);
        ArgumentNullException.ThrowIfNull(parentState);
        ArgumentNullException.ThrowIfNull(committedResults);
        if (scopePlan.Merge.Merge is null)
        {
            throw new InvalidOperationException($"Scope plan '{scopePlan.Id}' has no merge contract.");
        }

        if (scopePlan.Kind == CompiledScopeKind.ForEach)
        {
            throw new NotSupportedException("ForEach outcome merge is implemented with dynamic item scopes.");
        }

        var expectedCount = scopePlan.Kind == CompiledScopeKind.WhenFirst
            ? 1
            : scopePlan.Branches.Count;
        if (committedResults.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Scope plan '{scopePlan.Id}' requires {expectedCount} merge result(s), " +
                $"but {committedResults.Count} were supplied.");
        }

        try
        {
            var parentCopyPayload = Serialize(
                parentState,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
            var parentCopy = Deserialize(parentCopyPayload) ??
                throw new InvalidOperationException("Parent state copy deserialized as null.");
            var parentSnapshot = StructuredInvocationCache.CreateParentSnapshot(
                scopePlan.Merge.ParentStateType,
                parentCopy);
            var orderedResults = BuildOrderedResults(scopePlan, committedResults);
            var mergeArgument = scopePlan.Kind == CompiledScopeKind.WhenFirst
                ? ((IList)orderedResults)[0]
                : orderedResults;
            var replacement = StructuredInvocationCache.Invoke(
                scopePlan.Merge.Merge,
                parentSnapshot,
                mergeArgument);
            if (replacement is not null && !scopePlan.Merge.ParentStateType.IsInstanceOfType(replacement))
            {
                throw new InvalidOperationException(
                    $"Merge returned '{replacement.GetType().FullName}', expected " +
                    $"'{scopePlan.Merge.ParentStateType.FullName}'.");
            }

            return Serialize(
                replacement,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new StructuredMergeException(
                "SFE-MERGE-001",
                $"Merge for scope plan '{scopePlan.Id}' failed: {exception.InnerException.Message}",
                exception.InnerException);
        }
        catch (StructuredMergeException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StructuredMergeException(
                "SFE-MERGE-002",
                $"Merge materialization for scope plan '{scopePlan.Id}' failed: {exception.Message}",
                exception);
        }
    }

    public static StructuredSerializedValue ExecuteForEach(
        CompiledScopePlan scopePlan,
        object parentState,
        IReadOnlyList<ForEachTerminalOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(scopePlan);
        ArgumentNullException.ThrowIfNull(parentState);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (scopePlan.Kind != CompiledScopeKind.ForEach)
        {
            throw new InvalidOperationException($"Scope plan '{scopePlan.Id}' is not a ForEach scope.");
        }

        try
        {
            var parentCopyPayload = Serialize(
                parentState,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
            if (scopePlan.Merge.Merge is null)
            {
                return parentCopyPayload;
            }

            var parentCopy = Deserialize(parentCopyPayload) ??
                throw new InvalidOperationException("Parent state copy deserialized as null.");
            var parentSnapshot = StructuredInvocationCache.CreateParentSnapshot(
                scopePlan.Merge.ParentStateType,
                parentCopy);
            var bindings = ClosedResultBindings.For(scopePlan.ResultType);
            var typedOutcomes = bindings.Items();
            var resultSchema = scopePlan.Branches.Single().Result.ResultSchemaIdentity;
            foreach (var outcome in outcomes.OrderBy(outcome => outcome.Index))
            {
                object? result = null;
                if (outcome.Status == ForEachItemTerminalStatus.Succeeded)
                {
                    result = Deserialize(new StructuredSerializedValue(
                        scopePlan.ResultType,
                        resultSchema,
                        outcome.ResultPayload ??
                            throw new InvalidOperationException(
                                $"ForEach item '{outcome.Index}' has no committed result payload.")));
                }

                typedOutcomes.Add(bindings.Item(outcome.Index, outcome.Status, result, outcome.Failure));
            }

            var replacement = StructuredInvocationCache.Invoke(
                scopePlan.Merge.Merge,
                parentSnapshot,
                typedOutcomes);
            if (replacement is not null && !scopePlan.Merge.ParentStateType.IsInstanceOfType(replacement))
            {
                throw new InvalidOperationException(
                    $"ForEach merge returned '{replacement.GetType().FullName}', expected " +
                    $"'{scopePlan.Merge.ParentStateType.FullName}'.");
            }

            return Serialize(
                replacement,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new StructuredMergeException(
                "SFE-MERGE-001",
                $"Merge for scope plan '{scopePlan.Id}' failed: {exception.InnerException.Message}",
                exception.InnerException);
        }
        catch (StructuredMergeException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StructuredMergeException(
                "SFE-MERGE-002",
                $"Merge materialization for scope plan '{scopePlan.Id}' failed: {exception.Message}",
                exception);
        }
    }

    private static object BuildOrderedResults(
        CompiledScopePlan scopePlan,
        IReadOnlyList<MaterializedBranchResult> committedResults)
    {
        var byBranch = committedResults.ToDictionary(result => result.BranchPlanId);
        var bindings = ClosedResultBindings.For(scopePlan.ResultType);
        var list = bindings.Results();
        foreach (var branch in scopePlan.Branches.OrderBy(branch => branch.Ordinal))
        {
            if (!byBranch.TryGetValue(branch.Id, out var materialized))
            {
                if (scopePlan.Kind == CompiledScopeKind.WhenFirst)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Committed result for branch plan '{branch.Id}' is missing.");
            }

            if (materialized.Result.DeclaredType != scopePlan.ResultType ||
                !string.Equals(
                    materialized.Result.SchemaIdentity,
                    branch.Result.ResultSchemaIdentity,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Committed result for branch plan '{branch.Id}' does not match its compiled contract.");
            }

            var value = Deserialize(materialized.Result);
            list.Add(bindings.Result(branch.BranchId, branch.Ordinal, value));
        }

        if (list.Count != committedResults.Count)
        {
            throw new InvalidOperationException("One or more committed results reference an unknown branch plan.");
        }

        return list;
    }

    private static object BuildOrderedOutcomes(
        CompiledScopePlan scopePlan,
        IReadOnlyList<MaterializedBranchOutcome> committedOutcomes)
    {
        var byBranch = committedOutcomes.ToDictionary(outcome => outcome.BranchPlanId);
        var bindings = ClosedResultBindings.For(scopePlan.ResultType);
        var list = bindings.Outcomes();

        foreach (var branch in scopePlan.Branches.OrderBy(branch => branch.Ordinal))
        {
            if (!byBranch.TryGetValue(branch.Id, out var materialized))
            {
                throw new InvalidOperationException(
                    $"Committed outcome for branch plan '{branch.Id}' is missing.");
            }

            object publicOutcome;
            if (materialized.Failure is { } failure)
            {
                if (materialized.Result is not null)
                {
                    throw new InvalidOperationException(
                        $"Failed branch plan '{branch.Id}' also supplied a result.");
                }

                publicOutcome = bindings.Failed(branch.BranchId, failure);
            }
            else
            {
                var result = materialized.Result ??
                    throw new InvalidOperationException(
                        $"Successful branch plan '{branch.Id}' did not supply a result.");
                if (result.DeclaredType != scopePlan.ResultType ||
                    !string.Equals(
                        result.SchemaIdentity,
                        branch.Result.ResultSchemaIdentity,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Committed outcome for branch plan '{branch.Id}' does not match its compiled contract.");
                }

                publicOutcome = bindings.Succeeded(branch.BranchId, Deserialize(result));
            }

            list.Add(publicOutcome);
        }

        if (list.Count != committedOutcomes.Count)
        {
            throw new InvalidOperationException("One or more committed outcomes reference an unknown branch plan.");
        }

        return list;
    }

    private static StructuredSerializedValue Serialize(
        object? value,
        Type declaredType,
        string schemaIdentity) =>
        new(
            declaredType,
            schemaIdentity,
            CoreWorkflowValueCodec.Serialize(value, declaredType));

    private static object? Deserialize(StructuredSerializedValue value) =>
        CoreWorkflowValueCodec.Deserialize(value.Payload, value.DeclaredType);
}
