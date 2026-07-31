using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class EventEnvelopeContractTests
{
    [Fact]
    public void Envelope_WithSameData_AreEqual()
    {
        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
        var correlationId = CorrelationId.Create("order-123");
        var occurredAt = new DateTimeOffset(2026, 7, 2, 10, 15, 0, TimeSpan.Zero);

        var first = new EventEnvelope
        {
            EventId = eventId,
            EventName = "OrderApproved",
            CorrelationId = correlationId,
            Payload = "approved",
            OccurredAt = occurredAt
        };
        var second = new EventEnvelope
        {
            EventId = eventId,
            EventName = "OrderApproved",
            CorrelationId = correlationId,
            Payload = "approved",
            OccurredAt = occurredAt
        };

        first.Should().Be(second);
    }
}
