namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Immutable view of one ForEach partition scheduled inside an instance.
/// </summary>
public sealed record ForEachWorkItemSnapshot
{
    public required int Index { get; init; }

    public required IReadOnlyList<object?> Items { get; init; }

    public required ForEachWorkItemStatus Status { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? ErrorSummary { get; init; }
}
