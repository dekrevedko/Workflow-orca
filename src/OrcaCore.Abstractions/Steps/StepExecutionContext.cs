namespace OrcaCore;

/// <summary>
/// Identifies one stable logical step occurrence and its current diagnostic attempt.
/// </summary>
public sealed class StepExecutionContext
{
    internal StepExecutionContext(
        InstanceId workflowInstanceId,
        StepOperationId operationId,
        int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(workflowInstanceId);
        ArgumentNullException.ThrowIfNull(operationId);
        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptNumber), attemptNumber, "Attempt number must be positive.");
        }

        WorkflowInstanceId = workflowInstanceId;
        OperationId = operationId;
        AttemptNumber = attemptNumber;
    }

    /// <summary>Gets the logical workflow instance being executed.</summary>
    public InstanceId WorkflowInstanceId { get; }

    /// <summary>Gets the stable identity of this logical step occurrence.</summary>
    public StepOperationId OperationId { get; }

    /// <summary>Gets the one-based diagnostic attempt number.</summary>
    public int AttemptNumber { get; }
}
