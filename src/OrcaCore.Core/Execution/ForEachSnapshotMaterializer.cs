using System.Collections;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Internal;

namespace OrcaCore.Core.Execution;

/// <summary>
/// Creates the one detached, size-bounded value snapshot consumed by a dynamic item scope.
/// </summary>
internal static class ForEachSnapshotMaterializer
{
    public static object Materialize(object? selectedItems, Type itemType)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        ArgumentNullException.ThrowIfNull(itemType);
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
