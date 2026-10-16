namespace OrcaCore;

/// <summary>The common explicit registry implemented by exactly one selected engine role.</summary>
public interface IWorkflowDefinitionRegistry
{
    WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput>> Register<TInput>(
        EphemeralWorkflowDefinition<TInput> definition);

    WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        EphemeralWorkflowDefinition<TInput, TOutput> definition);

    WorkflowRegistrationResult<DurableDefinitionHandle<TInput>> Register<TInput>(
        DurableWorkflowDefinition<TInput> definition);

    WorkflowRegistrationResult<DurableDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        DurableWorkflowDefinition<TInput, TOutput> definition);

    EphemeralDefinitionHandle<TInput> GetRequiredHandle<TInput>(
        EphemeralWorkflowRef<TInput> reference);

    EphemeralDefinitionHandle<TInput, TOutput> GetRequiredHandle<TInput, TOutput>(
        EphemeralWorkflowRef<TInput, TOutput> reference);

    DurableDefinitionHandle<TInput> GetRequiredHandle<TInput>(
        DurableWorkflowRef<TInput> reference);

    DurableDefinitionHandle<TInput, TOutput> GetRequiredHandle<TInput, TOutput>(
        DurableWorkflowRef<TInput, TOutput> reference);
}

public sealed class EphemeralDefinitionHandle<TInput>
{
    private readonly Func<TInput, StartIdempotencyKey, CancellationToken,
        ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet;
    private readonly Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance;

    internal EphemeralDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        this.startOrGet = startOrGet;
        this.getInstance = getInstance;
    }

    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }

    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default) =>
        startOrGet(input, idempotencyKey, cancellationToken);

    public ValueTask<WorkflowInstanceHandle> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) =>
        getInstance(instanceId, cancellationToken);
}

public sealed class EphemeralDefinitionHandle<TInput, TOutput>
{
    private readonly Func<TInput, StartIdempotencyKey, CancellationToken,
        ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet;
    private readonly Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance;

    internal EphemeralDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        this.startOrGet = startOrGet;
        this.getInstance = getInstance;
    }

    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }

    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default) =>
        startOrGet(input, idempotencyKey, cancellationToken);

    public ValueTask<WorkflowInstanceHandle<TOutput>> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) =>
        getInstance(instanceId, cancellationToken);
}

public sealed class DurableDefinitionHandle<TInput>
{
    private readonly Func<TInput, StartIdempotencyKey, CancellationToken,
        ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet;
    private readonly Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance;

    internal DurableDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        this.startOrGet = startOrGet;
        this.getInstance = getInstance;
    }

    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }

    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default) =>
        startOrGet(input, idempotencyKey, cancellationToken);

    public ValueTask<WorkflowInstanceHandle> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) =>
        getInstance(instanceId, cancellationToken);
}

public sealed class DurableDefinitionHandle<TInput, TOutput>
{
    private readonly Func<TInput, StartIdempotencyKey, CancellationToken,
        ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet;
    private readonly Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance;

    internal DurableDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        DefinitionFingerprint = definitionFingerprint;
        this.startOrGet = startOrGet;
        this.getInstance = getInstance;
    }

    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }

    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default) =>
        startOrGet(input, idempotencyKey, cancellationToken);

    public ValueTask<WorkflowInstanceHandle<TOutput>> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) =>
        getInstance(instanceId, cancellationToken);
}
