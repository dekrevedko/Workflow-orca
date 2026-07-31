namespace OrcaCore.Core.Internal;

/// <summary>
/// Implementation-tier access to the fixed workflow value codec.
/// </summary>
public static class CoreWorkflowValueCodec
{
    public const string Format = FixedWorkflowValueCodec.Format;

    public const int MaxEncodedValueBytes = FixedWorkflowValueCodec.MaxEncodedValueBytes;

    public static bool IsSupportedDeclaredType(Type declaredType) =>
        FixedWorkflowValueCodec.IsSupportedDeclaredType(declaredType);

    public static byte[] Serialize(object? value, Type declaredType) =>
        FixedWorkflowValueCodec.Serialize(value, declaredType);

    public static object? Deserialize(ReadOnlySpan<byte> payload, Type declaredType) =>
        FixedWorkflowValueCodec.Deserialize(payload, declaredType);
}
