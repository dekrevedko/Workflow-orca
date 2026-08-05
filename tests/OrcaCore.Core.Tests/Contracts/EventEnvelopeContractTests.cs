using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class EventEnvelopeContractTests
{
    [Fact]
    public void Envelope_HasTheExactRuntimeCreatedSurface()
    {
        typeof(EventEnvelope).GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Should().BeEmpty();

        var constructor = typeof(EventEnvelope).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Should().ContainSingle().Subject;
        constructor.GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(
            typeof(EventId),
            typeof(EventName),
            typeof(CorrelationId),
            typeof(DateTimeOffset),
            typeof(ReadOnlyMemory<byte>));

        typeof(EventEnvelope).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [nameof(EventEnvelope.EventId), nameof(EventEnvelope.EventName),
                    nameof(EventEnvelope.CorrelationId), nameof(EventEnvelope.OccurredAt)],
                options => options.WithStrictOrdering());
        typeof(EventEnvelope).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Should().ContainSingle(method =>
                method.Name == nameof(EventEnvelope.GetPayload) && method.IsGenericMethodDefinition);
    }

    [Fact]
    public void Payload_RoundTripsThroughTheFixedCodecAndRemainsDetached()
    {
        var authored = new Payload(["authored"]);
        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
        var eventName = EventName.Create("OrderApproved");
        var correlationId = CorrelationId.Create("order-123");
        var occurredAt = new DateTimeOffset(2026, 7, 2, 10, 15, 0, TimeSpan.FromHours(2));
        var workflowEvent = WorkflowEvent<Payload>.Create(
            eventId,
            eventName,
            correlationId,
            authored,
            occurredAt);
        authored.Values.Add("mutated-after-create");

        var envelope = CreateEnvelope(
            eventId,
            eventName,
            correlationId,
            occurredAt,
            ReadPayloadBytes(workflowEvent));
        var first = envelope.GetPayload<Payload>();
        first.Values.Add("mutated-return");
        var second = envelope.GetPayload<Payload>();

        envelope.EventId.Should().BeSameAs(eventId);
        envelope.EventName.Should().BeSameAs(eventName);
        envelope.CorrelationId.Should().BeSameAs(correlationId);
        envelope.OccurredAt.Should().Be(occurredAt.ToUniversalTime());
        first.Values.Should().Equal("authored", "mutated-return");
        second.Values.Should().Equal("authored");
        second.Should().NotBeSameAs(first);
    }

    private static EventEnvelope CreateEnvelope(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload)
    {
        var constructor = typeof(EventEnvelope).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(EventId), typeof(EventName), typeof(CorrelationId), typeof(DateTimeOffset), typeof(ReadOnlyMemory<byte>)],
            modifiers: null)!;
        return (EventEnvelope)constructor.Invoke([eventId, eventName, correlationId, occurredAt, payload]);
    }

    private static ReadOnlyMemory<byte> ReadPayloadBytes<TPayload>(WorkflowEvent<TPayload> workflowEvent)
    {
        var property = typeof(WorkflowEvent<TPayload>).GetProperty(
            "PayloadBytes",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ReadOnlyMemory<byte>)property.GetValue(workflowEvent)!;
    }

    private sealed record Payload(List<string> Values);
}
