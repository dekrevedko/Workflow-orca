using System.Text.Json;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// Versioned checkpoint payload envelope committing the durable execution position and the
/// serialized business state atomically (DR-012). The envelope is the checkpoint payload for
/// every driver-issued commit; a checkpoint whose content type is not
/// <see cref="ContentType"/> is not resumable by the durable driver and parks the instance.
/// </summary>
public sealed record DurableExecutionEnvelope
{
    /// <summary>
    /// The checkpoint content type identifying an envelope-carrying payload.
    /// </summary>
    public const string ContentType = "application/vnd.orcacore.durable-envelope.v1+json";

    /// <summary>
    /// The envelope format version this build writes and can read.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Gets the envelope format version.
    /// </summary>
    public required int EnvelopeVersion { get; init; }

    /// <summary>
    /// Gets the durable execution position committed with the state.
    /// </summary>
    public required DurableExecutionPosition Position { get; init; }

    /// <summary>
    /// Gets the content type of the serialized business state.
    /// </summary>
    public required string StateContentType { get; init; }

    /// <summary>
    /// Gets the serialized business state.
    /// </summary>
    public required byte[] StatePayload { get; init; }

    /// <summary>
    /// Serializes this envelope to the checkpoint payload representation.
    /// </summary>
    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            this,
            OrcaCoreJsonSerializerContext.Default.DurableExecutionEnvelope);
    }

    /// <summary>
    /// Deserializes a checkpoint payload written under <see cref="ContentType"/>.
    /// </summary>
    public static DurableExecutionEnvelope Deserialize(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return JsonSerializer.Deserialize(
                payload,
                OrcaCoreJsonSerializerContext.Default.DurableExecutionEnvelope)
            ?? throw new JsonException("Durable execution envelope payload could not be deserialized.");
    }
}

/// <summary>
/// Describes where durable execution resumes: one cursor per concurrently advancing branch.
/// </summary>
public sealed record DurableExecutionPosition
{
    /// <summary>
    /// Gets the active execution cursors ordered deterministically by cursor id.
    /// </summary>
    public required IReadOnlyList<DurableExecutionCursor> Cursors { get; init; }
}

/// <summary>
/// Describes one durable execution cursor: a frame stack from the root sequence to the
/// current node plus the suspension phase that tells the driver how to resume it.
/// </summary>
public sealed record DurableExecutionCursor
{
    /// <summary>
    /// Gets the stable cursor identity ("root", or the branch sequence node path for
    /// cursors created by a Parallel/WhenFirst split).
    /// </summary>
    public required string CursorId { get; init; }

    /// <summary>
    /// Gets the frame stack from the root sequence down to the current node.
    /// </summary>
    public required IReadOnlyList<DurableExecutionFrame> Frames { get; init; }

    /// <summary>
    /// Gets the cursor phase describing whether the current node runs next or the cursor
    /// is suspended on runtime-owned work.
    /// </summary>
    public required DurableCursorPhase Phase { get; init; }

    /// <summary>
    /// Gets the wait the cursor is suspended on, when <see cref="Phase"/> is
    /// <see cref="DurableCursorPhase.SuspendedOnWait"/>.
    /// </summary>
    public WaitId? WaitId { get; init; }

    /// <summary>
    /// Gets the timer the cursor is suspended on, when <see cref="Phase"/> is
    /// <see cref="DurableCursorPhase.SuspendedOnTimer"/> or the wait has a timeout race.
    /// </summary>
    public TimerId? TimerId { get; init; }

    /// <summary>
    /// Gets the child group the cursor is suspended on, when <see cref="Phase"/> is
    /// <see cref="DurableCursorPhase.SuspendedOnChildren"/>.
    /// </summary>
    public string? ChildGroupId { get; init; }

    /// <summary>
    /// Gets the synthetic child-completion waits owned by this cursor. The driver consumes
    /// their matched resumes atomically with the parent resume token.
    /// </summary>
    public IReadOnlyList<WaitId> ChildWaitIds { get; init; } = [];

    /// <summary>
    /// Gets the wait whose matched resume envelope feeds the next executed business step
    /// (the durable analogue of the ephemeral resume-event slot). Cleared when a step
    /// consumes it or a newer wait replaces it.
    /// </summary>
    public WaitId? ResumeFromWaitId { get; init; }

    /// <summary>
    /// Gets how many yield continuations this cursor's current step has committed (diagnostic;
    /// chunk progress itself lives in business state per CR-017).
    /// </summary>
    public int YieldCount { get; init; }

    /// <summary>
    /// Gets the one-based retry attempt that runs when this cursor next reaches its current
    /// business step. Zero means the first attempt has not crossed a durable boundary.
    /// </summary>
    public int RetryAttempt { get; init; }

    /// <summary>
    /// Gets the durable eligibility time for a pending retry backoff.
    /// </summary>
    public DateTimeOffset? RetryNotBefore { get; init; }

    /// <summary>
    /// Gets the stable logical-operation key shared by every retry attempt at this position.
    /// </summary>
    public string? LogicalOperationKey { get; init; }

    /// <summary>
    /// Gets the absolute timeout deadline committed before this cursor starts the decorated
    /// business step. Restarts use the remaining interval instead of resetting the timeout.
    /// </summary>
    public DateTimeOffset? TimeoutDeadline { get; init; }
}

/// <summary>
/// Describes one frame of a durable cursor: a sequence node and the child index the cursor
/// points at inside it.
/// </summary>
public sealed record DurableExecutionFrame
{
    /// <summary>
    /// Gets the sequence node path this frame executes ("root", "root/2/then",
    /// "root/1/branches/0", "root/4/body").
    /// </summary>
    public required string SequencePath { get; init; }

    /// <summary>
    /// Gets the child index inside the sequence the cursor points at.
    /// </summary>
    public required int SequenceIndex { get; init; }

    /// <summary>
    /// Gets the zero-based loop iteration when this frame is a While body.
    /// </summary>
    public int? LoopIteration { get; init; }

    /// <summary>
    /// Gets the branch key when this frame is a Parallel/WhenFirst branch sequence.
    /// </summary>
    public string? BranchKey { get; init; }
}

/// <summary>
/// Describes how a durable execution cursor resumes.
/// </summary>
public enum DurableCursorPhase
{
    /// <summary>
    /// The node at the top frame runs next.
    /// </summary>
    AtNode,

    /// <summary>
    /// The cursor is suspended on a registered durable wait.
    /// </summary>
    SuspendedOnWait,

    /// <summary>
    /// The cursor is suspended on a scheduled durable timer.
    /// </summary>
    SuspendedOnTimer,

    /// <summary>
    /// The cursor is suspended on a durable retry-backoff timer and re-runs the same step when
    /// that timer commits its firing.
    /// </summary>
    SuspendedOnRetryBackoff,

    /// <summary>
    /// The cursor is suspended on a dispatched child workflow or child group join.
    /// </summary>
    SuspendedOnChildren,

    /// <summary>
    /// The cursor is suspended on a dispatched external job.
    /// </summary>
    SuspendedOnExternalJob,

    /// <summary>
    /// The cursor is suspended on a queued durable resource-pool acquisition.
    /// </summary>
    SuspendedOnResourcePool,

    /// <summary>
    /// The current step committed a yield and resumes at the same node.
    /// </summary>
    Yielded,

    /// <summary>
    /// The cursor finished its branch sequence and waits for sibling cursors to join.
    /// </summary>
    Completed
}

/// <summary>
/// Categorizes why a durable instance was parked (DR-017).
/// </summary>
public enum DurableParkReason
{
    /// <summary>
    /// The persisted checkpoint payload does not carry a readable execution-position envelope.
    /// </summary>
    RuntimeStateVersion,

    /// <summary>
    /// The definition version bound at start is not registered or is incompatible.
    /// </summary>
    VersionBinding,

    /// <summary>
    /// Advancement failed repeatedly and retrying automatically would loop hot.
    /// </summary>
    Poison
}
