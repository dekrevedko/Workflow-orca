namespace OrcaCore.Engine.Ephemeral.Internal;

/// <summary>
/// Adapts ephemeral runtime values to application-owned contracts through the exact typed friend
/// boundary declared by OrcaCore.
/// </summary>
internal static class EphemeralContractAdapter
{
    internal static object RuntimeDefinition(object definition) =>
        RuntimeMetadata(definition).RuntimeDefinition;

    internal static Type RuntimeStateType(object definition) =>
        RuntimeMetadata(definition).RuntimeStateType;

    internal static EphemeralDefinitionHandle<TInput> EphemeralDefinitionHandle<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance) =>
        new(definitionId, definitionVersion, definitionFingerprint, startOrGet, getInstance);

    internal static EphemeralDefinitionHandle<TInput, TOutput> EphemeralDefinitionHandle<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance) =>
        new(definitionId, definitionVersion, definitionFingerprint, startOrGet, getInstance);

    internal static AuthoredLocation AuthoredLocation(string value) => new(value);

    internal static FailureOccurrence RootFailureOccurrence() => new FailureOccurrence.Root();

    internal static WorkflowFailure WorkflowFailure(
        string code,
        string message,
        AuthoredLocation authoredLocation,
        FailureOccurrence occurrence,
        IReadOnlyList<WorkflowFailure> causes) =>
        new(code, message, authoredLocation, occurrence, causes);

    internal static WorkflowWaitTimeoutException WaitTimeout(
        WorkflowEventContract eventContract,
        CorrelationId correlationId) =>
        new(eventContract, correlationId);

    internal static EventEnvelope EventEnvelope(
        EventId eventId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload) =>
        new(eventId, eventContract, correlationId, occurredAt, payload);

    internal static StepAttemptTimeoutException StepTimeout(
        StepOperationId operationId,
        int attemptNumber,
        TimeSpan timeout) =>
        new(operationId, attemptNumber, timeout);

    internal static WorkflowDeadlineExceededException WorkflowDeadline(DateTimeOffset deadline) =>
        new(deadline);

    internal static WorkflowDefinitionNotRegisteredException DefinitionNotRegistered(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint) =>
        new(definitionId, definitionVersion, definitionFingerprint);

    internal static WorkflowDefinitionHostCompatibilityException HostCompatibility(
        DefinitionHostCompatibilityFailure failure) =>
        new(failure);

    internal static WorkflowDefinitionRegistrationConflictException RegistrationConflict(
        DefinitionRegistrationConflict conflict) =>
        new(conflict);

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
            [],
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
}
