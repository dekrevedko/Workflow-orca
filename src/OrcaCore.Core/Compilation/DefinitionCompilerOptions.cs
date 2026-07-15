using System.Text.Json;

namespace OrcaCore.Core.Compilation;

/// <summary>
/// Static limits and type-contract resolution used while compiling a definition.
/// </summary>
public sealed record DefinitionCompilerOptions
{
    /// <summary>
    /// Gets the maximum structural instructions one fiber may execute in one quantum.
    /// </summary>
    public int MaxInternalInstructionsPerQuantum { get; init; } = 1024;

    /// <summary>
    /// Gets the maximum nested structured-scope depth.
    /// </summary>
    public int MaxScopeDepth { get; init; } = 32;

    /// <summary>
    /// Gets the maximum active fibers one instance may own.
    /// </summary>
    public int MaxActiveFibers { get; init; } = 256;

    /// <summary>
    /// Gets the maximum serialized result payload size.
    /// </summary>
    public int MaxSerializedResultBytes { get; init; } = 256 * 1024;

    /// <summary>
    /// Gets the maximum serialized execution-envelope size.
    /// </summary>
    public int MaxSerializedEnvelopeBytes { get; init; } = 4 * 1024 * 1024;
}

/// <summary>
/// Resolves whether a workflow type has a configured serialization contract.
/// </summary>
public interface IWorkflowTypeSerializerRegistry
{
    /// <summary>
    /// Tries to resolve a stable schema identity for a persisted or copied type.
    /// </summary>
    bool TryGetSchemaIdentity(Type type, out string schemaIdentity);

    /// <summary>
    /// Serializes a value using the contract represented by the resolved schema identity.
    /// </summary>
    byte[] Serialize(object? value, Type declaredType)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, declaredType);
    }

    /// <summary>
    /// Deserializes a value using the contract represented by the resolved schema identity.
    /// </summary>
    object? Deserialize(ReadOnlySpan<byte> payload, Type declaredType)
    {
        return JsonSerializer.Deserialize(payload, declaredType);
    }
}

/// <summary>
/// Supplies an explicit stable identity for captured workflow configuration that cannot be
/// represented by the built-in scalar and collection fingerprint rules.
/// </summary>
public interface IWorkflowPlanFingerprintSource
{
    /// <summary>
    /// Gets the stable configuration identity included in the compiled-plan fingerprint.
    /// </summary>
    string GetWorkflowPlanFingerprint();
}

internal sealed class DefaultWorkflowTypeSerializerRegistry : IWorkflowTypeSerializerRegistry
{
    internal static DefaultWorkflowTypeSerializerRegistry Instance { get; } = new();

    public bool TryGetSchemaIdentity(Type type, out string schemaIdentity)
    {
        ArgumentNullException.ThrowIfNull(type);
        schemaIdentity = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        if (type.IsPointer ||
            type.IsByRef ||
            type.IsByRefLike ||
            type.ContainsGenericParameters ||
            typeof(Delegate).IsAssignableFrom(type))
        {
            return false;
        }

        try
        {
            _ = JsonSerializerOptions.Default.GetTypeInfo(type);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
