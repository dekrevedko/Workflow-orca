using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// A mutable working copy of one persisted execution cursor. Frames always point at the
/// current node; the segment run advances or suspends it and serializes it back into the
/// envelope on every commit.
/// </summary>
internal sealed class DurableDriverCursor
{
    internal required string CursorId { get; set; }

    internal required List<DurableExecutionFrame> Frames { get; init; }

    internal DurableCursorPhase Phase { get; set; } = DurableCursorPhase.AtNode;

    internal WaitId? WaitId { get; set; }

    internal TimerId? TimerId { get; set; }

    internal string? ChildGroupId { get; set; }

    internal List<WaitId> ChildWaitIds { get; set; } = [];

    internal WaitId? ResumeFromWaitId { get; set; }

    internal int YieldCount { get; set; }

    internal int RetryAttempt { get; set; }

    internal DateTimeOffset? RetryNotBefore { get; set; }

    internal string? LogicalOperationKey { get; set; }

    internal DateTimeOffset? TimeoutDeadline { get; set; }

    internal DurableExecutionFrame Top => Frames[^1];

    internal void ReplaceTop(DurableExecutionFrame frame)
    {
        Frames[^1] = frame;
    }

    internal void AdvanceIndex()
    {
        var top = Top;
        ReplaceTop(top with { SequenceIndex = top.SequenceIndex + 1 });
    }

    internal DurableExecutionCursor ToRecord()
    {
        return new DurableExecutionCursor
        {
            CursorId = CursorId,
            Frames = [.. Frames],
            Phase = Phase,
            WaitId = WaitId,
            TimerId = TimerId,
            ChildGroupId = ChildGroupId,
            ChildWaitIds = [.. ChildWaitIds],
            ResumeFromWaitId = ResumeFromWaitId,
            YieldCount = YieldCount,
            RetryAttempt = RetryAttempt,
            RetryNotBefore = RetryNotBefore,
            LogicalOperationKey = LogicalOperationKey,
            TimeoutDeadline = TimeoutDeadline
        };
    }

    internal static DurableDriverCursor FromRecord(DurableExecutionCursor record)
    {
        return new DurableDriverCursor
        {
            CursorId = record.CursorId,
            Frames = [.. record.Frames],
            Phase = record.Phase,
            WaitId = record.WaitId,
            TimerId = record.TimerId,
            ChildGroupId = record.ChildGroupId,
            ChildWaitIds = [.. record.ChildWaitIds],
            ResumeFromWaitId = record.ResumeFromWaitId,
            YieldCount = record.YieldCount,
            RetryAttempt = record.RetryAttempt,
            RetryNotBefore = record.RetryNotBefore,
            LogicalOperationKey = record.LogicalOperationKey,
            TimeoutDeadline = record.TimeoutDeadline
        };
    }
}
