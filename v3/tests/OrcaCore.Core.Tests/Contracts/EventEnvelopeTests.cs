using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Tests.Contracts;

public class EventEnvelopeTests
{
    [Fact]
    public void Envelope_WithSameData_AreEqual()
    {
        var eventId = EventId.New();
        var correlationId = new CorrelationId("corr-1");
        var occurredAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = new EventEnvelope(eventId, "Approved", correlationId, Payload: 42, occurredAt);
        var second = new EventEnvelope(eventId, "Approved", correlationId, Payload: 42, occurredAt);

        first.Should().Be(second);
    }
}
