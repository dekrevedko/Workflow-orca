using System.Collections.Concurrent;
using System.Collections;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Internal;

namespace OrcaCore.Core.Execution;

/// <summary>
/// Creates the one detached, size-bounded value snapshot consumed by a dynamic item scope.
/// </summary>
internal static class ForEachSnapshotMaterializer
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> Snapshots = [];

    internal static void Bind<TItem>() => Snapshots.TryAdd(typeof(TItem), selected =>
    {
        var array = ((IEnumerable<TItem>)selected).ToArray();
        return RoundTrip(array, typeof(TItem[]));
    });

    public static object Materialize(object? selectedItems, Type itemType)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        ArgumentNullException.ThrowIfNull(itemType);
        if (Snapshots.TryGetValue(itemType, out var materialize)) return materialize(selectedItems);
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("The compiled workflow is missing a closed typed item snapshot binding.");
        return LegacyMaterialize(selectedItems, itemType);
    }

    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Unconfigured legacy JIT item adapter only.")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT item adapter only.")]
    private static object LegacyMaterialize(object selectedItems, Type itemType)
    {
        if (selectedItems is not IEnumerable enumerable)
        {
            throw new InvalidOperationException(
                $"ForEach selector result '{selectedItems.GetType().FullName}' is not enumerable.");
        }

        var copiedItems = new List<object?>();
        foreach (var item in enumerable)
        {
            copiedItems.Add(item);
        }

        var array = Array.CreateInstance(itemType, copiedItems.Count);
        for (var index = 0; index < copiedItems.Count; index++)
        {
            array.SetValue(copiedItems[index], index);
        }

        var snapshotType = itemType.MakeArrayType();
        return RoundTrip(array, snapshotType);
    }

    private static object RoundTrip(object array, Type snapshotType)
    {
        var encoded = FixedWorkflowValueCodec.Serialize(array, snapshotType);
        if (encoded.Length > FixedWorkflowValueCodec.MaxEncodedValueBytes)
        {
            throw new StructuredExecutionLimitException(
                StructuredExecutionLimitCodes.EncodedValueExceeded,
                $"Encoded ForEach item snapshot is {encoded.Length} bytes, exceeding the fixed " +
                $"{FixedWorkflowValueCodec.Format} value limit of " +
                $"{FixedWorkflowValueCodec.MaxEncodedValueBytes} bytes.");
        }

        return FixedWorkflowValueCodec.Deserialize(encoded, snapshotType) ??
               throw new InvalidOperationException("ForEach item snapshot decoded to null.");
    }
}
