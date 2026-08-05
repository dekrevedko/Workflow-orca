
namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class ResumeEventSlot(EventEnvelope? envelope)
{
    private EventEnvelope? envelope = envelope;

    internal EventEnvelope? Take()
    {
        var current = envelope;
        envelope = null;
        return current;
    }
}
