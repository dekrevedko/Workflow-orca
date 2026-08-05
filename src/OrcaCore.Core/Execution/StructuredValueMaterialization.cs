using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Internal;

namespace OrcaCore.Core.Execution;

internal sealed record StructuredSerializedValue
{
    public StructuredSerializedValue(Type declaredType, string schemaIdentity, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaIdentity);
        ArgumentNullException.ThrowIfNull(payload);

        DeclaredType = declaredType;
        SchemaIdentity = schemaIdentity;
        Payload = payload.ToArray();
    }

    public Type DeclaredType { get; }

    public string SchemaIdentity { get; }

    public byte[] Payload { get; }
}

internal static class BranchInputMaterializer
{
    public static StructuredSerializedValue Materialize(
        CompiledBranchInputPlan inputPlan,
        object parentState)
    {
        ArgumentNullException.ThrowIfNull(inputPlan);
        ArgumentNullException.ThrowIfNull(parentState);
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

        return new StructuredSerializedValue(
            inputPlan.BranchStateType,
            inputPlan.BranchStateSchemaIdentity,
            CoreWorkflowValueCodec.Serialize(
            projected,
            inputPlan.BranchStateType));
    }
}
