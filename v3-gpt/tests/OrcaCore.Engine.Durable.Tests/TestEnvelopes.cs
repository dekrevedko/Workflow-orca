using OrcaCore.Abstractions.Durable;

namespace OrcaCore.Engine.Durable.Tests;

/// <summary>
/// Builds minimal execution-position envelopes for kernel-level tests that issue driver
/// commands by hand.
/// </summary>
internal static class TestEnvelopes
{
    internal static DurableExecutionEnvelope Envelope(
        string stateContentType = "application/json",
        byte[]? statePayload = null,
        int rootIndex = 1,
        DurableCursorPhase phase = DurableCursorPhase.AtNode)
    {
        return new DurableExecutionEnvelope
        {
            EnvelopeVersion = DurableExecutionEnvelope.CurrentVersion,
            Position = new DurableExecutionPosition
            {
                Cursors =
                [
                    new DurableExecutionCursor
                    {
                        CursorId = "root",
                        Frames =
                        [
                            new DurableExecutionFrame
                            {
                                SequencePath = "root",
                                SequenceIndex = rootIndex
                            }
                        ],
                        Phase = phase
                    }
                ]
            },
            StateContentType = stateContentType,
            StatePayload = statePayload ?? [1, 2, 3]
        };
    }
}
