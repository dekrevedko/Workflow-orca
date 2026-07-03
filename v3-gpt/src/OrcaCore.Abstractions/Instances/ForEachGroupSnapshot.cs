namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Immutable view of one in-instance ForEach group.
/// </summary>
public sealed record ForEachGroupSnapshot
{
    public required string GroupId { get; init; }

    public required int TotalItems { get; init; }

    public required int WorkItemCount { get; init; }

    public int? MaxConcurrency { get; init; }

    public required int ActiveCount { get; init; }

    public required int CompletedCount { get; init; }

    public required int FailedCount { get; init; }

    public required int CancelledCount { get; init; }

    public required IReadOnlyList<ForEachWorkItemSnapshot> WorkItems { get; init; }
}
