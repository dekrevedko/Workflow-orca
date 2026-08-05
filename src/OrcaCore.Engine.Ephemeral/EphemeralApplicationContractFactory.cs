using System.Reflection;

namespace OrcaCore.Engine.Ephemeral.Internal;

/// <summary>
/// Constructs application contracts owned by OrcaCore without widening their constructors
/// or introducing a cross-assembly friend relationship.
/// </summary>
internal static class EphemeralApplicationContractFactory
{
    internal static object RuntimeDefinition(object definition) =>
        ReadNonPublicProperty<object>(definition, "RuntimeDefinition");

    internal static Type RuntimeStateType(object definition) =>
        ReadNonPublicProperty<Type>(definition, "RuntimeStateType");

    internal static EphemeralDefinitionHandle<TInput> EphemeralDefinitionHandle<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle>> getInstance) =>
        Construct<EphemeralDefinitionHandle<TInput>>(
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

    internal static EphemeralDefinitionHandle<TInput, TOutput> EphemeralDefinitionHandle<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TInput, StartIdempotencyKey, CancellationToken,
            ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>>> startOrGet,
        Func<InstanceId, CancellationToken, ValueTask<WorkflowInstanceHandle<TOutput>>> getInstance) =>
        Construct<EphemeralDefinitionHandle<TInput, TOutput>>(
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

    internal static WorkflowWaitTimeoutException WaitTimeout(
        EventName eventName,
        CorrelationId correlationId) =>
        Construct<WorkflowWaitTimeoutException>(
            [typeof(EventName), typeof(CorrelationId)],
            eventName,
            correlationId);

    internal static EventEnvelope EventEnvelope(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload) =>
        Construct<EventEnvelope>(
            [
                typeof(EventId),
                typeof(EventName),
                typeof(CorrelationId),
                typeof(DateTimeOffset),
                typeof(ReadOnlyMemory<byte>)
            ],
            eventId,
            eventName,
            correlationId,
            occurredAt,
            payload);

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
            (IReadOnlyList<WorkflowDiagnostic>)new[] { diagnostic });
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
