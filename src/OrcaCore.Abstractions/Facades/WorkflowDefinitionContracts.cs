namespace OrcaCore;

internal interface IWorkflowDefinitionRuntimeMetadata
{
    object RuntimeDefinition { get; }

    Type RuntimeStateType { get; }
}

/// <summary>An immutable resultless ephemeral workflow definition.</summary>
public sealed class EphemeralWorkflowDefinition<TInput> : IWorkflowDefinitionRuntimeMetadata
{
    internal EphemeralWorkflowDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        RuntimeDefinition = runtimeDefinition;
        RuntimeStateType = runtimeStateType;
        Reference = new EphemeralWorkflowRef<TInput>(this);
    }

    public WorkflowMode Mode => WorkflowMode.Ephemeral;

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }

    public EphemeralWorkflowRef<TInput> Reference { get; }

    internal object RuntimeDefinition { get; }

    internal Type RuntimeStateType { get; }

    object IWorkflowDefinitionRuntimeMetadata.RuntimeDefinition => RuntimeDefinition;

    Type IWorkflowDefinitionRuntimeMetadata.RuntimeStateType => RuntimeStateType;
}

/// <summary>An immutable resultful ephemeral workflow definition.</summary>
public sealed class EphemeralWorkflowDefinition<TInput, TOutput> : IWorkflowDefinitionRuntimeMetadata
{
    internal EphemeralWorkflowDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        RuntimeDefinition = runtimeDefinition;
        RuntimeStateType = runtimeStateType;
        Reference = new EphemeralWorkflowRef<TInput, TOutput>(this);
    }

    public WorkflowMode Mode => WorkflowMode.Ephemeral;

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }

    public EphemeralWorkflowRef<TInput, TOutput> Reference { get; }

    internal object RuntimeDefinition { get; }

    internal Type RuntimeStateType { get; }

    object IWorkflowDefinitionRuntimeMetadata.RuntimeDefinition => RuntimeDefinition;

    Type IWorkflowDefinitionRuntimeMetadata.RuntimeStateType => RuntimeStateType;
}

/// <summary>A state-opaque resultless ephemeral workflow reference.</summary>
public sealed class EphemeralWorkflowRef<TInput>
{
    internal EphemeralWorkflowRef(EphemeralWorkflowDefinition<TInput> definition)
    {
        Mode = definition.Mode;
        DefinitionId = definition.DefinitionId;
        DefinitionVersion = definition.DefinitionVersion;
        DefinitionFingerprint = definition.DefinitionFingerprint;
    }

    public WorkflowMode Mode { get; }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }
}

/// <summary>A state-opaque resultful ephemeral workflow reference.</summary>
public sealed class EphemeralWorkflowRef<TInput, TOutput>
{
    internal EphemeralWorkflowRef(EphemeralWorkflowDefinition<TInput, TOutput> definition)
    {
        Mode = definition.Mode;
        DefinitionId = definition.DefinitionId;
        DefinitionVersion = definition.DefinitionVersion;
        DefinitionFingerprint = definition.DefinitionFingerprint;
    }

    public WorkflowMode Mode { get; }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }
}

/// <summary>An immutable resultless durable workflow definition.</summary>
public sealed class DurableWorkflowDefinition<TInput> : IWorkflowDefinitionRuntimeMetadata
{
    internal DurableWorkflowDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        RuntimeDefinition = runtimeDefinition;
        RuntimeStateType = runtimeStateType;
        Reference = new DurableWorkflowRef<TInput>(this);
    }

    public WorkflowMode Mode => WorkflowMode.Durable;

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }

    public DurableWorkflowRef<TInput> Reference { get; }

    internal object RuntimeDefinition { get; }

    internal Type RuntimeStateType { get; }

    object IWorkflowDefinitionRuntimeMetadata.RuntimeDefinition => RuntimeDefinition;

    Type IWorkflowDefinitionRuntimeMetadata.RuntimeStateType => RuntimeStateType;
}

/// <summary>An immutable resultful durable workflow definition.</summary>
public sealed class DurableWorkflowDefinition<TInput, TOutput> : IWorkflowDefinitionRuntimeMetadata
{
    internal DurableWorkflowDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        RuntimeDefinition = runtimeDefinition;
        RuntimeStateType = runtimeStateType;
        Reference = new DurableWorkflowRef<TInput, TOutput>(this);
    }

    public WorkflowMode Mode => WorkflowMode.Durable;

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }

    public DurableWorkflowRef<TInput, TOutput> Reference { get; }

    internal object RuntimeDefinition { get; }

    internal Type RuntimeStateType { get; }

    object IWorkflowDefinitionRuntimeMetadata.RuntimeDefinition => RuntimeDefinition;

    Type IWorkflowDefinitionRuntimeMetadata.RuntimeStateType => RuntimeStateType;
}

/// <summary>A state-opaque resultless durable workflow reference.</summary>
public sealed class DurableWorkflowRef<TInput>
{
    internal DurableWorkflowRef(DurableWorkflowDefinition<TInput> definition)
    {
        Mode = definition.Mode;
        DefinitionId = definition.DefinitionId;
        DefinitionVersion = definition.DefinitionVersion;
        DefinitionFingerprint = definition.DefinitionFingerprint;
    }

    public WorkflowMode Mode { get; }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }
}

/// <summary>A state-opaque resultful durable workflow reference.</summary>
public sealed class DurableWorkflowRef<TInput, TOutput>
{
    internal DurableWorkflowRef(DurableWorkflowDefinition<TInput, TOutput> definition)
    {
        Mode = definition.Mode;
        DefinitionId = definition.DefinitionId;
        DefinitionVersion = definition.DefinitionVersion;
        DefinitionFingerprint = definition.DefinitionFingerprint;
    }

    public WorkflowMode Mode { get; }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public DefinitionFingerprint DefinitionFingerprint { get; }
}
