namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Observable execution status for an in-instance ForEach work item.
/// </summary>
public enum ForEachWorkItemStatus
{
    Pending,
    Active,
    Completed,
    Failed,
    Cancelled
}
