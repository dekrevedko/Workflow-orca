namespace OrcaCore.Core.Internal;

/// <summary>
/// Implementation-tier access to the fixed workflow value codec.
/// </summary>
internal static class CoreWorkflowValueCodec
{
    public const string Format = FixedWorkflowValueCodec.Format;

    public const int MaxEncodedValueBytes = FixedWorkflowValueCodec.MaxEncodedValueBytes;

    public static bool IsSupportedDeclaredType(Type declaredType) =>
        FixedWorkflowValueCodec.IsSupportedDeclaredType(declaredType);

    public static byte[] Serialize(object? value, Type declaredType) =>
        FixedWorkflowValueCodec.Serialize(value, declaredType);

    public static byte[] Serialize(object? value, Type declaredType, out object? detached) =>
        FixedWorkflowValueCodec.Serialize(value, declaredType, out detached);

    public static object? Detach(object? value, Type declaredType)
    {
        FixedWorkflowValueCodec.Serialize(value, declaredType, out var detached);
        return detached;
    }

    public static object? Deserialize(ReadOnlySpan<byte> payload, Type declaredType) =>
        FixedWorkflowValueCodec.Deserialize(payload, declaredType);
}
