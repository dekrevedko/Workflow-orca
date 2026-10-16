using OrcaCore.Abstractions.Ids;

namespace OrcaCore;

/// <summary>Reports that a structural event wait reached its authored deadline.</summary>
public sealed class WorkflowWaitTimeoutException : OrcaCoreException
{
    internal WorkflowWaitTimeoutException(
        WorkflowEventContract eventContract,
        CorrelationId correlationId)
        : base(
            "WF-WAIT-TIMEOUT",
            $"Wait for event '{eventContract?.EventName}' version " +
            $"'{eventContract?.Version}' with correlation '{correlationId}' timed out.")
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlationId);
        EventContract = eventContract;
        CorrelationId = correlationId;
    }

    /// <summary>Gets the event contract whose wait timed out.</summary>
    public WorkflowEventContract EventContract { get; }

    /// <summary>Gets the correlation identity whose wait timed out.</summary>
    public CorrelationId CorrelationId { get; }
}

/// <summary>Reports that one business-step attempt reached its authored deadline.</summary>
public sealed class StepAttemptTimeoutException : OrcaCoreException
{
    internal StepAttemptTimeoutException(
        StepOperationId operationId,
        int attemptNumber,
        TimeSpan timeout)
        : base(
            "WF-STEP-TIMEOUT",
            $"Step operation '{operationId}' attempt {attemptNumber} timed out after {timeout}.")
    {
        ArgumentNullException.ThrowIfNull(operationId);
        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptNumber),
                attemptNumber,
                "Attempt number must be positive.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
        }

        OperationId = operationId;
        AttemptNumber = attemptNumber;
        Timeout = timeout;
    }

    /// <summary>Gets the stable logical step occurrence.</summary>
    public StepOperationId OperationId { get; }

    /// <summary>Gets the positive retry-policy ordinal.</summary>
    public int AttemptNumber { get; }

    /// <summary>Gets the authored per-attempt timeout.</summary>
    public TimeSpan Timeout { get; }
}

/// <summary>Reports that the workflow-wide start-relative deadline was exceeded.</summary>
public sealed class WorkflowDeadlineExceededException : OrcaCoreException
{
    internal WorkflowDeadlineExceededException(DateTimeOffset deadline)
        : base("WF-DEADLINE-EXCEEDED", $"Workflow deadline '{deadline:O}' was exceeded.")
    {
        Deadline = deadline;
    }

    /// <summary>Gets the persisted absolute workflow deadline.</summary>
    public DateTimeOffset Deadline { get; }
}
