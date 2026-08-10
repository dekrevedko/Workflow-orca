using System.Collections.Concurrent;
using System.Reflection;
using OrcaCore.Core.Internal;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Engine.Durable.Internal;

/// <summary>
/// Adapts durable runtime values to application-owned contracts through the exact typed friend
/// boundary declared by OrcaCore.
/// </summary>
internal static class DurableContractAdapter
{
    private const string PendingDefinitionFingerprintPrefix = "pending-definition:";
    private static readonly ConcurrentDictionary<string, TypedWorkflowEventContractFactory>
        TypedWorkflowEventContractFactories = new(StringComparer.Ordinal);

    internal static object RuntimeDefinition(object definition) =>
        RuntimeMetadata(definition).RuntimeDefinition;

    internal static Type RuntimeStateType(object definition) =>
        RuntimeMetadata(definition).RuntimeStateType;

    internal static WorkflowOutboundEvent WorkflowOutboundEvent(DurableWorkflowOutboundEventData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new WorkflowOutboundEvent(
            CreateWorkflowEventContract(data),
            EventId.Create(data.EventId),
            CorrelationId.Create(data.CorrelationId),
            data.CausationEventId is null ? null : EventId.Create(data.CausationEventId),
            data.OccurredAt,
            InstanceId.Parse(data.OriginInstanceId),
            DefinitionId.Parse(data.OriginDefinitionId),
            new DefinitionVersion(data.OriginDefinitionVersion),
            new ReadOnlyMemory<byte>(data.Payload));
    }

    internal static DefinitionRegistrationConflict DefinitionRegistrationConflict(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint existingFingerprint,
        DefinitionFingerprint attemptedFingerprint) =>
        new DefinitionRegistrationConflict(
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
        new DurableDefinitionHandle<TInput>(
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
        new DurableDefinitionHandle<TInput, TOutput>(
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
        new StartIdempotencyConflict(
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
            new DefinitionFingerprint(
                PendingDefinitionFingerprint(
                    conflict.AttemptedDefinitionId,
                    conflict.AttemptedDefinitionVersion)),
            new PayloadFingerprint(conflict.AttemptedInputFingerprint));

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
            new DefinitionFingerprint(existingDefinitionFingerprint),
            new PayloadFingerprint(existingInputFingerprint),
            attemptedDefinitionId,
            attemptedDefinitionVersion,
            attemptedDefinitionFingerprint,
            attemptedInputFingerprint);

    internal static PayloadFingerprint PayloadFingerprint<T>(T value)
    {
        var digest = DurableWorkflowInputFingerprint.Create(value);
        return new PayloadFingerprint(digest);
    }

    internal static WorkflowInstanceHandle InstanceHandle(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate) =>
        new WorkflowInstanceHandle(
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
        new WorkflowInstanceHandle<TOutput>(
            instanceId,
            getSnapshot,
            getState,
            requestCancellation,
            terminate,
            getOutput,
            waitForOutput);

    internal static AuthoredLocation AuthoredLocation(string value) => new(value);

    internal static FailureOccurrence RootFailureOccurrence() => new FailureOccurrence.Root();

    internal static WorkflowFailure WorkflowFailure(
        string code,
        string message,
        AuthoredLocation authoredLocation,
        FailureOccurrence occurrence,
        IReadOnlyList<WorkflowFailure> causes) =>
        new WorkflowFailure(
            code,
            message,
            authoredLocation,
            occurrence,
            causes);

    internal static AmbiguousWaitRegistrationException AmbiguousWait(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId) =>
        new AmbiguousWaitRegistrationException(
            definitionId,
            eventContract,
            correlationId);

    internal static WorkflowInstanceNotFoundException InstanceNotFound(InstanceId instanceId) =>
        new(instanceId);

    internal static WorkflowDefinitionNotRegisteredException DefinitionNotRegistered(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint) =>
        new WorkflowDefinitionNotRegisteredException(
            definitionId,
            definitionVersion,
            definitionFingerprint);

    internal static WorkflowDefinitionHostCompatibilityException HostCompatibility(
        DefinitionHostCompatibilityFailure failure) =>
        new WorkflowDefinitionHostCompatibilityException(failure);

    internal static WorkflowDefinitionRegistrationConflictException RegistrationConflict(
        DefinitionRegistrationConflict conflict) =>
        new WorkflowDefinitionRegistrationConflictException(conflict);

    internal static WorkflowInstanceDefinitionMismatchException InstanceDefinitionMismatch(
        InstanceId instanceId,
        DefinitionId expectedDefinitionId,
        DefinitionId actualDefinitionId) =>
        new WorkflowInstanceDefinitionMismatchException(
            instanceId,
            expectedDefinitionId,
            actualDefinitionId);

    internal static WorkflowStateTypeMismatchException StateTypeMismatch(
        InstanceId instanceId,
        Type actualType,
        Type requestedType) =>
        new WorkflowStateTypeMismatchException(
            instanceId,
            actualType,
            requestedType);

    internal static WorkflowOutputUnavailableException OutputUnavailable(
        WorkflowInstanceStatus status,
        WorkflowFailure? failure) =>
        new WorkflowOutputUnavailableException(
            status,
            failure);

    internal static LeaseLostException LeaseLost(
        LeaseProtectionToken protectionToken,
        IReadOnlyList<ResourcePoolName> missingPools) =>
        new LeaseLostException(
            protectionToken,
            missingPools);

    internal static ResourcePoolNotConfiguredException ResourcePoolsNotConfigured(
        IReadOnlyList<ResourcePoolName> missingPools) =>
        new ResourcePoolNotConfiguredException(missingPools);

    internal static WorkflowWaitTimeoutException WaitTimeout(
        WorkflowEventContract eventContract,
        CorrelationId correlationId) =>
        new WorkflowWaitTimeoutException(
            eventContract,
            correlationId);

    internal static StepAttemptTimeoutException StepTimeout(
        StepOperationId operationId,
        int attemptNumber,
        TimeSpan timeout) =>
        new StepAttemptTimeoutException(
            operationId,
            attemptNumber,
            timeout);

    internal static WorkflowDeadlineExceededException WorkflowDeadline(DateTimeOffset deadline) =>
        new(deadline);

    internal static WorkflowDefinitionException DefinitionException(
        string message,
        Exception? innerException = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var detail = innerException is null ? message : $"{message} {innerException.Message}";
        var location = AuthoredLocation("workflow:$");
        var diagnostic = new WorkflowDiagnostic(
            "SFE-AUTH-CAP-001",
            WorkflowDiagnosticSeverity.Error,
            location,
            Array.Empty<AuthoredLocation>(),
            detail);
        return new WorkflowDefinitionException([diagnostic]);
    }

    private static IWorkflowDefinitionRuntimeMetadata RuntimeMetadata(object definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition as IWorkflowDefinitionRuntimeMetadata ??
            throw new ArgumentException(
                "The definition does not expose the approved runtime metadata contract.",
                nameof(definition));
    }

    private static WorkflowEventContract CreateWorkflowEventContract(DurableWorkflowOutboundEventData data)
    {
        var eventName = EventName.Create(data.EventName);
        var version = new EventContractVersion(data.EventContractVersion);
        if (data.PayloadTypeName is null)
        {
            return global::OrcaCore.WorkflowEventContract.Create(eventName, version);
        }

        var factory = TypedWorkflowEventContractFactories.GetOrAdd(
            data.PayloadTypeName,
            static payloadTypeName => CreateTypedWorkflowEventContractFactory(payloadTypeName));
        if (!string.Equals(data.PayloadSchemaIdentity, factory.SchemaIdentity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The workflow-event payload schema identity '{data.PayloadSchemaIdentity}' does not match " +
                $"the declared payload type identity '{factory.SchemaIdentity}'.");
        }

        return factory.Create(eventName, version);
    }

    private static TypedWorkflowEventContractFactory CreateTypedWorkflowEventContractFactory(
        string payloadTypeName)
    {
        var payloadType = Type.GetType(payloadTypeName, throwOnError: false) ??
            throw new InvalidOperationException(
                $"The workflow-event payload type '{payloadTypeName}' is unavailable.");
        var schemaIdentity = payloadType.AssemblyQualifiedName ?? payloadType.FullName ?? payloadType.Name;
        var descriptorType = typeof(WorkflowEventContract<>).MakeGenericType(payloadType);
        var create = descriptorType.GetMethod(
            nameof(global::OrcaCore.WorkflowEventContract.Create),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null,
            [typeof(EventName), typeof(EventContractVersion)],
            modifiers: null) ??
            throw new MissingMethodException(
                descriptorType.FullName,
                $"{nameof(global::OrcaCore.WorkflowEventContract.Create)}({typeof(EventName).FullName}, " +
                $"{typeof(EventContractVersion).FullName})");
        return new TypedWorkflowEventContractFactory(schemaIdentity, create);
    }

    private sealed record TypedWorkflowEventContractFactory(
        string SchemaIdentity,
        MethodInfo CreateMethod)
    {
        internal WorkflowEventContract Create(EventName eventName, EventContractVersion version) =>
            (WorkflowEventContract)(CreateMethod.Invoke(null, [eventName, version]) ??
                throw new InvalidOperationException(
                    "The typed workflow-event descriptor factory returned no descriptor."));
    }

}
