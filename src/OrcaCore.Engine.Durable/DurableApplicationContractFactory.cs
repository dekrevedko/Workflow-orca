using System.Reflection;
using System.Security.Cryptography;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Internal;

/// <summary>
/// Constructs application contracts owned by OrcaCore without widening their constructors
/// or introducing a cross-assembly friend relationship.
/// </summary>
internal static class DurableApplicationContractFactory
{
    private const string PendingDefinitionFingerprintPrefix = "pending-definition:";
    internal static object RuntimeDefinition(object definition) =>
        ReadNonPublicProperty<object>(definition, "RuntimeDefinition");

    internal static Type RuntimeStateType(object definition) =>
        ReadNonPublicProperty<Type>(definition, "RuntimeStateType");

    internal static DefinitionRegistrationConflict DefinitionRegistrationConflict(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint existingFingerprint,
        DefinitionFingerprint attemptedFingerprint) =>
        Construct<DefinitionRegistrationConflict>(
            [
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(DefinitionFingerprint)
            ],
            definitionId,
            definitionVersion,
            existingFingerprint,
            attemptedFingerprint);

    internal static DurableDefinitionHandle<TInput> DurableDefinitionHandle<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance) =>
        Construct<DurableDefinitionHandle<TInput>>(
            [
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(Func<TInput, StartIdempotencyKey, CancellationToken,
                    ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>>),
                typeof(Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>>)
            ],
            definitionId,
            definitionVersion,
            definitionFingerprint,
            startOrGet,
            getInstance);

    internal static DurableDefinitionHandle<TInput, TOutput> DurableDefinitionHandle<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance) =>
        Construct<DurableDefinitionHandle<TInput, TOutput>>(
            [
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(Func<TInput, StartIdempotencyKey, CancellationToken,
                    ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>>),
                typeof(Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>>)
            ],
            definitionId,
            definitionVersion,
            definitionFingerprint,
            startOrGet,
            getInstance);

    internal static StartIdempotencyConflict StartIdempotencyConflict(
        StartIdempotencyKey key,
        DefinitionId existingDefinitionId,
        DefinitionVersion existingDefinitionVersion,
        DefinitionFingerprint existingDefinitionFingerprint,
        PayloadFingerprint existingInputFingerprint,
        DefinitionId attemptedDefinitionId,
        DefinitionVersion attemptedDefinitionVersion,
        DefinitionFingerprint attemptedDefinitionFingerprint,
        PayloadFingerprint attemptedInputFingerprint) =>
        Construct<StartIdempotencyConflict>(
            [
                typeof(StartIdempotencyKey),
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(PayloadFingerprint),
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(PayloadFingerprint)
            ],
            key,
            existingDefinitionId,
            existingDefinitionVersion,
            existingDefinitionFingerprint,
            existingInputFingerprint,
            attemptedDefinitionId,
            attemptedDefinitionVersion,
            attemptedDefinitionFingerprint,
            attemptedInputFingerprint);

    internal static StartIdempotencyConflict PendingStartIdempotencyConflict(
        StartIdempotencyKey key,
        Abstractions.Providers.InboxStartBindingConflict conflict) =>
        StartIdempotencyConflict(
            key,
            conflict.ExistingDefinitionId,
            conflict.ExistingDefinitionVersion,
            conflict.ExistingDefinitionFingerprint ?? PendingDefinitionFingerprint(
                conflict.ExistingDefinitionId,
                conflict.ExistingDefinitionVersion),
            conflict.ExistingInputFingerprint,
            conflict.AttemptedDefinitionId,
            conflict.AttemptedDefinitionVersion,
            Construct<DefinitionFingerprint>(
                [typeof(string)],
                PendingDefinitionFingerprint(
                    conflict.AttemptedDefinitionId,
                    conflict.AttemptedDefinitionVersion)),
            Construct<PayloadFingerprint>([typeof(string)], conflict.AttemptedInputFingerprint));

    internal static StartIdempotencyConflict PendingStartIdempotencyConflict(
        StartIdempotencyKey key,
        Abstractions.Providers.InboxStartIntentRecord conflict,
        DefinitionId attemptedDefinitionId,
        DefinitionVersion attemptedDefinitionVersion,
        DefinitionFingerprint attemptedDefinitionFingerprint,
        PayloadFingerprint attemptedInputFingerprint) =>
        StartIdempotencyConflict(
            key,
            conflict.DefinitionId,
            conflict.DefinitionVersion,
            conflict.DefinitionFingerprint ?? PendingDefinitionFingerprint(
                conflict.DefinitionId,
                conflict.DefinitionVersion),
            conflict.WorkflowInputFingerprint,
            attemptedDefinitionId,
            attemptedDefinitionVersion,
            attemptedDefinitionFingerprint,
            attemptedInputFingerprint);

    // A callback-only route binds identity/version but has no structural definition to hash.
    // Use one deterministic provisional value until the owning host materializes the exact binding.
    private static string PendingDefinitionFingerprint(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion) =>
        $"{PendingDefinitionFingerprintPrefix}{definitionId.Value:N}:{definitionVersion.Value}";

    internal static StartIdempotencyConflict StartIdempotencyConflict(
        StartIdempotencyKey key,
        DefinitionId existingDefinitionId,
        DefinitionVersion existingDefinitionVersion,
        string existingDefinitionFingerprint,
        string existingInputFingerprint,
        DefinitionId attemptedDefinitionId,
        DefinitionVersion attemptedDefinitionVersion,
        DefinitionFingerprint attemptedDefinitionFingerprint,
        PayloadFingerprint attemptedInputFingerprint) =>
        StartIdempotencyConflict(
            key,
            existingDefinitionId,
            existingDefinitionVersion,
            Construct<DefinitionFingerprint>([typeof(string)], existingDefinitionFingerprint),
            Construct<PayloadFingerprint>([typeof(string)], existingInputFingerprint),
            attemptedDefinitionId,
            attemptedDefinitionVersion,
            attemptedDefinitionFingerprint,
            attemptedInputFingerprint);

    internal static PayloadFingerprint PayloadFingerprint<T>(T value)
    {
        var bytes = CoreWorkflowValueCodec.Serialize(value, typeof(T));
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        return Construct<PayloadFingerprint>([typeof(string)], digest);
    }

    internal static WorkflowInstanceHandle InstanceHandle(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate) =>
        Construct<WorkflowInstanceHandle>(
            [
                typeof(InstanceId),
                typeof(Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>>),
                typeof(Func<Type, CancellationToken, ValueTask<object?>>),
                typeof(Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>>),
                typeof(Func<CancellationToken, ValueTask<WorkflowTerminationStatus>>)
            ],
            instanceId,
            getSnapshot,
            getState,
            requestCancellation,
            terminate);

    internal static WorkflowInstanceHandle<TOutput> InstanceHandle<TOutput>(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate,
        Func<CancellationToken, ValueTask<WorkflowOutputResult<TOutput>>> getOutput,
        Func<CancellationToken, ValueTask<TOutput>> waitForOutput) =>
        Construct<WorkflowInstanceHandle<TOutput>>(
            [
                typeof(InstanceId),
                typeof(Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>>),
                typeof(Func<Type, CancellationToken, ValueTask<object?>>),
                typeof(Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>>),
                typeof(Func<CancellationToken, ValueTask<WorkflowTerminationStatus>>),
                typeof(Func<CancellationToken, ValueTask<WorkflowOutputResult<TOutput>>>),
                typeof(Func<CancellationToken, ValueTask<TOutput>>)
            ],
            instanceId,
            getSnapshot,
            getState,
            requestCancellation,
            terminate,
            getOutput,
            waitForOutput);

    internal static AuthoredLocation AuthoredLocation(string value) =>
        Construct<AuthoredLocation>([typeof(string)], value);

    internal static FailureOccurrence RootFailureOccurrence() =>
        Construct<FailureOccurrence.Root>(Type.EmptyTypes);

    internal static WorkflowFailure WorkflowFailure(
        string code,
        string message,
        AuthoredLocation authoredLocation,
        FailureOccurrence occurrence,
        IReadOnlyList<WorkflowFailure> causes) =>
        Construct<WorkflowFailure>(
            [
                typeof(string),
                typeof(string),
                typeof(AuthoredLocation),
                typeof(FailureOccurrence),
                typeof(IReadOnlyList<WorkflowFailure>)
            ],
            code,
            message,
            authoredLocation,
            occurrence,
            causes);

    internal static AmbiguousWaitRegistrationException AmbiguousWait(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId) =>
        Construct<AmbiguousWaitRegistrationException>(
            [typeof(DefinitionId), typeof(WorkflowEventContract), typeof(CorrelationId)],
            definitionId,
            eventContract,
            correlationId);

    internal static WorkflowInstanceNotFoundException InstanceNotFound(InstanceId instanceId) =>
        Construct<WorkflowInstanceNotFoundException>([typeof(InstanceId)], instanceId);

    internal static WorkflowDefinitionNotRegisteredException DefinitionNotRegistered(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint) =>
        Construct<WorkflowDefinitionNotRegisteredException>(
            [typeof(DefinitionId), typeof(DefinitionVersion), typeof(DefinitionFingerprint)],
            definitionId,
            definitionVersion,
            definitionFingerprint);

    internal static WorkflowDefinitionHostCompatibilityException HostCompatibility(
        DefinitionHostCompatibilityFailure failure) =>
        Construct<WorkflowDefinitionHostCompatibilityException>(
            [typeof(DefinitionHostCompatibilityFailure)],
            failure);

    internal static WorkflowDefinitionRegistrationConflictException RegistrationConflict(
        DefinitionRegistrationConflict conflict) =>
        Construct<WorkflowDefinitionRegistrationConflictException>(
            [typeof(DefinitionRegistrationConflict)],
            conflict);

    internal static WorkflowInstanceDefinitionMismatchException InstanceDefinitionMismatch(
        InstanceId instanceId,
        DefinitionId expectedDefinitionId,
        DefinitionId actualDefinitionId) =>
        Construct<WorkflowInstanceDefinitionMismatchException>(
            [typeof(InstanceId), typeof(DefinitionId), typeof(DefinitionId)],
            instanceId,
            expectedDefinitionId,
            actualDefinitionId);

    internal static WorkflowStateTypeMismatchException StateTypeMismatch(
        InstanceId instanceId,
        Type actualType,
        Type requestedType) =>
        Construct<WorkflowStateTypeMismatchException>(
            [typeof(InstanceId), typeof(Type), typeof(Type)],
            instanceId,
            actualType,
            requestedType);

    internal static WorkflowOutputUnavailableException OutputUnavailable(
        WorkflowInstanceStatus status,
        WorkflowFailure? failure) =>
        Construct<WorkflowOutputUnavailableException>(
            [typeof(WorkflowInstanceStatus), typeof(WorkflowFailure)],
            status,
            failure);

    internal static LeaseLostException LeaseLost(
        LeaseProtectionToken protectionToken,
        IReadOnlyList<ResourcePoolName> missingPools) =>
        Construct<LeaseLostException>(
            [typeof(LeaseProtectionToken), typeof(IReadOnlyList<ResourcePoolName>)],
            protectionToken,
            missingPools);

    internal static ResourcePoolNotConfiguredException ResourcePoolsNotConfigured(
        IReadOnlyList<ResourcePoolName> missingPools) =>
        Construct<ResourcePoolNotConfiguredException>(
            [typeof(IReadOnlyList<ResourcePoolName>)],
            missingPools);

    internal static WorkflowWaitTimeoutException WaitTimeout(
        WorkflowEventContract eventContract,
        CorrelationId correlationId) =>
        Construct<WorkflowWaitTimeoutException>(
            [typeof(WorkflowEventContract), typeof(CorrelationId)],
            eventContract,
            correlationId);

    internal static StepAttemptTimeoutException StepTimeout(
        StepOperationId operationId,
        int attemptNumber,
        TimeSpan timeout) =>
        Construct<StepAttemptTimeoutException>(
            [typeof(StepOperationId), typeof(int), typeof(TimeSpan)],
            operationId,
            attemptNumber,
            timeout);

    internal static WorkflowDeadlineExceededException WorkflowDeadline(DateTimeOffset deadline) =>
        Construct<WorkflowDeadlineExceededException>([typeof(DateTimeOffset)], deadline);

    internal static WorkflowDefinitionException DefinitionException(
        string message,
        Exception? innerException = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var detail = innerException is null ? message : $"{message} {innerException.Message}";
        var location = AuthoredLocation("workflow:$");
        var diagnostic = Construct<WorkflowDiagnostic>(
            [
                typeof(string),
                typeof(WorkflowDiagnosticSeverity),
                typeof(AuthoredLocation),
                typeof(IReadOnlyList<AuthoredLocation>),
                typeof(string)
            ],
            "SFE-AUTH-CAP-001",
            WorkflowDiagnosticSeverity.Error,
            location,
            Array.Empty<AuthoredLocation>(),
            detail);
        return Construct<WorkflowDefinitionException>(
            [typeof(IReadOnlyList<WorkflowDiagnostic>)],
            (IReadOnlyList<WorkflowDiagnostic>)[diagnostic]);
    }

    private static TProperty ReadNonPublicProperty<TProperty>(object instance, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            returnType: typeof(TProperty),
            types: Type.EmptyTypes,
            modifiers: null) ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
        return (TProperty)(property.GetValue(instance) ?? throw new InvalidOperationException(
            $"Property '{instance.GetType().FullName}.{propertyName}' returned null."));
    }

    private static TContract Construct<TContract>(Type[] parameterTypes, params object?[] arguments)
    {
        var constructor = typeof(TContract).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            parameterTypes,
            modifiers: null) ?? throw new MissingMethodException(
                typeof(TContract).FullName,
                $".ctor({string.Join(", ", parameterTypes.Select(type => type.FullName))})");
        return (TContract)constructor.Invoke(arguments);
    }
}
