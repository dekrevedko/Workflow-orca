using System.Runtime.CompilerServices;
using System.Reflection;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Execution;

internal abstract class ClosedForEachBindings
{
    private static readonly ConditionalWeakTable<object, ClosedForEachBindings> Bindings = new();

    internal static void Bind<TItem>(WorkflowPartitioner<TItem> partitioner)
    {
        Bindings.GetValue(partitioner, _ => new Typed<TItem>(partitioner));
        ForEachSnapshotMaterializer.Bind<TItem>();
    }

    internal static ClosedForEachBindings For(object partitioner) =>
        Bindings.TryGetValue(partitioner, out var binding) ? binding :
            throw new InvalidOperationException("The ForEach partitioner is missing its closed typed binding.");

    internal abstract Type SnapshotType { get; }
    internal abstract IReadOnlyList<ClosedForEachInput> Partition(object items);

    private sealed class Typed<TItem>(WorkflowPartitioner<TItem> partitioner) : ClosedForEachBindings
    {
        internal override Type SnapshotType => typeof(TItem[]);
        internal override IReadOnlyList<ClosedForEachInput> Partition(object items)
        {
            try
            {
                return partitioner.Partition((IReadOnlyList<TItem>)items)
                    .Select(partition => new ClosedForEachInput(partition.Index,
                        new global::OrcaCore.Core.Building.ForEachItemInput<TItem>(partition.Index, partition.Items)))
                    .ToArray();
            }
            catch (TargetInvocationException) { throw; }
            catch (Exception error) { throw new TargetInvocationException(error); }
        }
    }
}

internal sealed record ClosedForEachInput(int Index, object Input);
