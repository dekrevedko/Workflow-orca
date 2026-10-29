using System.Collections;
using System.Collections.Concurrent;
using OrcaCore.Core.Building;
using OrcaCore.Core.Authoring;

namespace OrcaCore.Core.Execution;

internal abstract class ClosedResultBindings
{
    private static readonly ConcurrentDictionary<Type, ClosedResultBindings> Bindings = [];

    internal static void Bind<TResult>() => Bindings.TryAdd(typeof(TResult), new Typed<TResult>());

    internal static ClosedResultBindings For(Type resultType)
    {
        if (Bindings.TryGetValue(resultType, out var binding)) return binding;
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("The compiled workflow is missing a closed typed result binding.");
        return LegacyBinding(resultType);
    }

    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Unconfigured legacy JIT result adapter only.")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT result adapter only.")]
    private static ClosedResultBindings LegacyBinding(Type resultType)
    {
        return Bindings.GetOrAdd(resultType, type =>
            (ClosedResultBindings)Activator.CreateInstance(typeof(Typed<>).MakeGenericType(type))!);
    }

    internal abstract IList Results();
    internal abstract IList Outcomes();
    internal abstract IList Items();
    internal abstract object Result(string branch, int ordinal, object? value);
    internal abstract object Succeeded(string branch, object? value);
    internal abstract object Failed(string branch, FiberFailure failure);
    internal abstract object Item(int index, ForEachItemTerminalStatus status, object? value, FiberFailure? failure);

    private sealed class Typed<TResult> : ClosedResultBindings
    {
        internal override IList Results() => new List<global::OrcaCore.Core.Building.BranchResult<TResult>>();
        internal override IList Outcomes() => new List<global::OrcaCore.BranchOutcome<TResult>>();
        internal override IList Items() => new List<global::OrcaCore.Core.Building.ForEachItemOutcome<TResult>>();
        internal override object Result(string branch, int ordinal, object? value) =>
            new global::OrcaCore.Core.Building.BranchResult<TResult>(branch, ordinal, (TResult)value!);
        internal override object Succeeded(string branch, object? value) =>
            AuthoringContracts.BranchSucceeded(global::OrcaCore.AuthoredBranchId.Create(branch), (TResult)value!);
        internal override object Failed(string branch, FiberFailure failure) =>
            AuthoringContracts.BranchFailed<TResult>(global::OrcaCore.AuthoredBranchId.Create(branch), failure);
        internal override object Item(int index, ForEachItemTerminalStatus status, object? value, FiberFailure? failure) =>
            new global::OrcaCore.Core.Building.ForEachItemOutcome<TResult>(index, status, value is null ? default : (TResult)value, failure);
    }
}
