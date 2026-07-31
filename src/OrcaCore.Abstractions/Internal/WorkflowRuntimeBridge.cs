using System.ComponentModel;

namespace OrcaCore.Internal;

/// <summary>
/// Narrow engine bridge for constructing application-owned values whose public
/// constructors intentionally remain closed to workflow authors.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class WorkflowRuntimeBridge
{
    public static EphemeralWorkflowDefinition<TInput> EphemeralDefinition<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    public static EphemeralWorkflowDefinition<TInput, TOutput> EphemeralDefinition<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    public static DurableWorkflowDefinition<TInput> DurableDefinition<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    public static DurableWorkflowDefinition<TInput, TOutput> DurableDefinition<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    public static object RuntimeDefinition(object definition) =>
        definition switch
        {
            EphemeralWorkflowDefinition<object> value => value.RuntimeDefinition,
            DurableWorkflowDefinition<object> value => value.RuntimeDefinition,
            _ => RuntimeDefinitionByReflection(definition)
        };

    public static Type RuntimeStateType(object definition) =>
        definition switch
        {
            EphemeralWorkflowDefinition<object> value => value.RuntimeStateType,
            DurableWorkflowDefinition<object> value => value.RuntimeStateType,
            _ => RuntimeStateTypeByReflection(definition)
        };

    public static DefinitionRegistrationConflict DefinitionRegistrationConflict(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint existingFingerprint,
        DefinitionFingerprint attemptedFingerprint) =>
        new(
            definitionId,
            definitionVersion,
            existingFingerprint,
            attemptedFingerprint);

    public static EphemeralDefinitionHandle<TInput> EphemeralDefinitionHandle<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            startOrGet,
            getInstance);

    public static EphemeralDefinitionHandle<TInput, TOutput> EphemeralDefinitionHandle<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            startOrGet,
            getInstance);

    public static DurableDefinitionHandle<TInput> DurableDefinitionHandle<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            startOrGet,
            getInstance);

    public static DurableDefinitionHandle<TInput, TOutput> DurableDefinitionHandle<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance) =>
        new(
            definitionId,
            definitionVersion,
            definitionFingerprint,
            startOrGet,
            getInstance);

    public static StartIdempotencyConflict StartIdempotencyConflict(
        StartIdempotencyKey key,
        DefinitionId existingDefinitionId,
        DefinitionVersion existingDefinitionVersion,
        DefinitionFingerprint existingDefinitionFingerprint,
        PayloadFingerprint existingInputFingerprint,
        DefinitionId attemptedDefinitionId,
        DefinitionVersion attemptedDefinitionVersion,
        DefinitionFingerprint attemptedDefinitionFingerprint,
        PayloadFingerprint attemptedInputFingerprint) =>
        new(
            key,
            existingDefinitionId,
            existingDefinitionVersion,
            existingDefinitionFingerprint,
            existingInputFingerprint,
            attemptedDefinitionId,
            attemptedDefinitionVersion,
            attemptedDefinitionFingerprint,
            attemptedInputFingerprint);

    public static StartIdempotencyConflict StartIdempotencyConflict(
        StartIdempotencyKey key,
        DefinitionId existingDefinitionId,
        DefinitionVersion existingDefinitionVersion,
        string existingDefinitionFingerprint,
        string existingInputFingerprint,
        DefinitionId attemptedDefinitionId,
        DefinitionVersion attemptedDefinitionVersion,
        DefinitionFingerprint attemptedDefinitionFingerprint,
        PayloadFingerprint attemptedInputFingerprint) =>
        new(
            key,
            existingDefinitionId,
            existingDefinitionVersion,
            new DefinitionFingerprint(existingDefinitionFingerprint),
            new PayloadFingerprint(existingInputFingerprint),
            attemptedDefinitionId,
            attemptedDefinitionVersion,
            attemptedDefinitionFingerprint,
            attemptedInputFingerprint);

    public static PayloadFingerprint PayloadFingerprint<T>(T value) =>
        WorkflowPayloadFingerprint.Create(value);

    public static WorkflowInstanceHandle InstanceHandle(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate) =>
        new(instanceId, getSnapshot, getState, requestCancellation, terminate);

    public static WorkflowInstanceHandle<TOutput> InstanceHandle<TOutput>(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate,
        Func<CancellationToken, ValueTask<WorkflowOutputResult<TOutput>>> getOutput,
        Func<CancellationToken, ValueTask<TOutput>> waitForOutput) =>
        new(
            instanceId,
            getSnapshot,
            getState,
            requestCancellation,
            terminate,
            getOutput,
            waitForOutput);

    public static AuthoredLocation AuthoredLocation(string value) => new(value);

    public static FailureOccurrence RootFailureOccurrence() => new FailureOccurrence.Root();

    public static WorkflowFailure WorkflowFailure(
        string code,
        string message,
        AuthoredLocation authoredLocation,
        FailureOccurrence occurrence,
        IReadOnlyList<WorkflowFailure> causes) =>
        new(code, message, authoredLocation, occurrence, causes);

    public static AmbiguousWaitRegistrationException AmbiguousWait(
        DefinitionId definitionId,
        EventName eventName,
        CorrelationId correlationId) =>
        new(definitionId, eventName, correlationId);

    public static WorkflowInstanceNotFoundException InstanceNotFound(InstanceId instanceId) =>
        new(instanceId);

    public static WorkflowInstanceDefinitionMismatchException InstanceDefinitionMismatch(
        InstanceId instanceId,
        DefinitionId expectedDefinitionId,
        DefinitionId actualDefinitionId) =>
        new(instanceId, expectedDefinitionId, actualDefinitionId);

    public static WorkflowStateTypeMismatchException StateTypeMismatch(
        InstanceId instanceId,
        Type actualType,
        Type requestedType) =>
        new(instanceId, actualType, requestedType);

    public static WorkflowOutputUnavailableException OutputUnavailable(
        WorkflowInstanceStatus status,
        WorkflowFailure? failure) =>
        new(status, failure);

    public static LeaseLostException LeaseLost(
        LeaseProtectionToken protectionToken,
        IReadOnlyList<ResourcePoolName> missingPools) =>
        new(protectionToken, missingPools);

    private static object RuntimeDefinitionByReflection(object definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.GetType()
            .GetProperty(
                "RuntimeDefinition",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(definition)!;
    }

    private static Type RuntimeStateTypeByReflection(object definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return (Type)definition.GetType()
            .GetProperty(
                "RuntimeStateType",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(definition)!;
    }
}
