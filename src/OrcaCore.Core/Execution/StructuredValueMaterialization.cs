using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal sealed record StructuredSerializedValue
{
    internal StructuredSerializedValue(Type declaredType, string schemaIdentity, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaIdentity);
        ArgumentNullException.ThrowIfNull(payload);

        DeclaredType = declaredType;
        SchemaIdentity = schemaIdentity;
        Payload = payload.ToArray();
    }

    internal Type DeclaredType { get; }

    internal string SchemaIdentity { get; }

    internal byte[] Payload { get; }
}

internal interface IStructuredValueCodec
{
    StructuredSerializedValue Serialize(object? value, Type declaredType, string schemaIdentity);

    object? Deserialize(StructuredSerializedValue value);
}

internal static class BranchInputMaterializer
{
    internal static StructuredSerializedValue Materialize(
        CompiledBranchInputPlan inputPlan,
        object parentState,
        IStructuredValueCodec codec)
    {
        ArgumentNullException.ThrowIfNull(inputPlan);
        ArgumentNullException.ThrowIfNull(parentState);
        ArgumentNullException.ThrowIfNull(codec);
        if (!inputPlan.ParentStateType.IsInstanceOfType(parentState))
        {
            throw new ArgumentException(
                $"Parent state must be assignable to '{inputPlan.ParentStateType.FullName}'.",
                nameof(parentState));
        }

        var snapshot = StructuredInvocationCache.CreateParentSnapshot(
            inputPlan.ParentStateType,
            parentState);
        var projected = StructuredInvocationCache.Invoke(inputPlan.Projector, snapshot);
        if (projected is not null && !inputPlan.BranchStateType.IsInstanceOfType(projected))
        {
            throw new InvalidOperationException(
                $"Branch input projector returned '{projected.GetType().FullName}', expected " +
                $"'{inputPlan.BranchStateType.FullName}'.");
        }

        return codec.Serialize(
            projected,
            inputPlan.BranchStateType,
            inputPlan.BranchStateSchemaIdentity);
    }
}
