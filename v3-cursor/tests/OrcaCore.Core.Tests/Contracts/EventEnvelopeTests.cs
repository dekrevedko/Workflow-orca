using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void Envelope_WithSameData_AreEqual()
    {
        var eventId = EventId.New();
        var correlationId = new CorrelationId("order-42");
        var occurredAt = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
        var payload = new { Amount = 100m };

        var first = new EventEnvelope(
            eventId,
            "PaymentReceived",
            correlationId,
            payload,
            occurredAt);

        var second = new EventEnvelope(
            eventId,
            "PaymentReceived",
            correlationId,
            payload,
            occurredAt);

        first.Should().Be(second);
    }
}
