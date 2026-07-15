using System.Collections;
using System.Reflection;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal sealed record MaterializedBranchResult(
    BranchPlanId BranchPlanId,
    StructuredSerializedValue Result);

internal sealed class StructuredMergeException : Exception
{
    internal StructuredMergeException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    internal string Code { get; }
}

internal static class ScopeMergeAdapter
{
    internal static StructuredSerializedValue Execute(
        CompiledScopePlan scopePlan,
        object parentState,
        IReadOnlyList<MaterializedBranchResult> committedResults,
        IStructuredValueCodec codec)
    {
        ArgumentNullException.ThrowIfNull(scopePlan);
        ArgumentNullException.ThrowIfNull(parentState);
        ArgumentNullException.ThrowIfNull(committedResults);
        ArgumentNullException.ThrowIfNull(codec);
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
            var parentCopyPayload = codec.Serialize(
                parentState,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
            var parentCopy = codec.Deserialize(parentCopyPayload) ??
                throw new InvalidOperationException("Parent state copy deserialized as null.");
            var parentSnapshot = StructuredInvocationCache.CreateParentSnapshot(
                scopePlan.Merge.ParentStateType,
                parentCopy);
            var orderedResults = BuildOrderedResults(scopePlan, committedResults, codec);
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

            return codec.Serialize(
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

    internal static StructuredSerializedValue ExecuteForEach(
        CompiledScopePlan scopePlan,
        object parentState,
        IReadOnlyList<ForEachTerminalOutcome> outcomes,
        IStructuredValueCodec codec)
    {
        ArgumentNullException.ThrowIfNull(scopePlan);
        ArgumentNullException.ThrowIfNull(parentState);
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(codec);
        if (scopePlan.Kind != CompiledScopeKind.ForEach)
        {
            throw new InvalidOperationException($"Scope plan '{scopePlan.Id}' is not a ForEach scope.");
        }

        try
        {
            var parentCopyPayload = codec.Serialize(
                parentState,
                scopePlan.Merge.ParentStateType,
                scopePlan.Merge.ParentStateSchemaIdentity);
            if (scopePlan.Merge.Merge is null)
            {
                return parentCopyPayload;
            }

            var parentCopy = codec.Deserialize(parentCopyPayload) ??
                throw new InvalidOperationException("Parent state copy deserialized as null.");
            var parentSnapshot = StructuredInvocationCache.CreateParentSnapshot(
                scopePlan.Merge.ParentStateType,
                parentCopy);
            var outcomeType = typeof(ForEachItemOutcome<>).MakeGenericType(scopePlan.ResultType);
            var outcomeListType = typeof(List<>).MakeGenericType(outcomeType);
            var typedOutcomes = (IList)(Activator.CreateInstance(outcomeListType) ??
                throw new InvalidOperationException("Could not create the typed ForEach outcome list."));
            var resultSchema = scopePlan.Branches.Single().Result.ResultSchemaIdentity;
            foreach (var outcome in outcomes.OrderBy(outcome => outcome.Index))
            {
                object? result = null;
                if (outcome.Status == ForEachItemTerminalStatus.Succeeded)
                {
                    result = codec.Deserialize(new StructuredSerializedValue(
                        scopePlan.ResultType,
                        resultSchema,
                        outcome.ResultPayload ??
                            throw new InvalidOperationException(
                                $"ForEach item '{outcome.Index}' has no committed result payload.")));
                }

                typedOutcomes.Add(Activator.CreateInstance(
                    outcomeType,
                    outcome.Index,
                    outcome.Status,
                    result,
                    outcome.Failure?.Message) ??
                    throw new InvalidOperationException(
                        $"Could not create outcome for ForEach item '{outcome.Index}'."));
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

            return codec.Serialize(
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
        IReadOnlyList<MaterializedBranchResult> committedResults,
        IStructuredValueCodec codec)
    {
        var byBranch = committedResults.ToDictionary(result => result.BranchPlanId);
        var resultRecordType = typeof(BranchResult<>).MakeGenericType(scopePlan.ResultType);
        var listType = typeof(List<>).MakeGenericType(resultRecordType);
        var list = (IList)(Activator.CreateInstance(listType) ??
            throw new InvalidOperationException("Could not create the typed branch-result list."));
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

            var value = codec.Deserialize(materialized.Result);
            var resultRecord = Activator.CreateInstance(
                resultRecordType,
                branch.BranchId,
                branch.Ordinal,
                value) ?? throw new InvalidOperationException(
                    $"Could not create the typed result for branch plan '{branch.Id}'.");
            list.Add(resultRecord);
        }

        if (list.Count != committedResults.Count)
        {
            throw new InvalidOperationException("One or more committed results reference an unknown branch plan.");
        }

        return list;
    }
}
