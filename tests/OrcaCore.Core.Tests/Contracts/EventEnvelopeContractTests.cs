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
            typeof(WorkflowEventContract),
            typeof(CorrelationId),
            typeof(DateTimeOffset),
            typeof(ReadOnlyMemory<byte>));

        typeof(EventEnvelope).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [nameof(EventEnvelope.EventId), nameof(EventEnvelope.EventContract),
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
        var eventContract = WorkflowEventContract<Payload>.Create(
            EventName.Create("OrderApproved"),
            EventContractVersion.Initial);
        var correlationId = CorrelationId.Create("order-123");
        var occurredAt = new DateTimeOffset(2026, 7, 2, 10, 15, 0, TimeSpan.FromHours(2));
        var workflowEvent = WorkflowInboundEvent<Payload>.Create(
            eventContract,
            eventId,
            correlationId,
            causationEventId: null,
            occurredAt,
            new WorkflowEventRoute.Direct(InstanceId.Parse(Guid.CreateVersion7().ToString())),
            authored);
        authored.Values.Add("mutated-after-create");

        var envelope = CreateEnvelope(
            eventId,
            eventContract,
            correlationId,
            occurredAt,
            ReadPayloadBytes(workflowEvent));
        var first = envelope.GetPayload(eventContract);
        first.Values.Add("mutated-return");
        var second = envelope.GetPayload(eventContract);

        envelope.EventId.Should().BeSameAs(eventId);
        envelope.EventContract.Should().BeSameAs(eventContract);
        envelope.CorrelationId.Should().BeSameAs(correlationId);
        envelope.OccurredAt.Should().Be(occurredAt.ToUniversalTime());
        first.Values.Should().Equal("authored", "mutated-return");
        second.Values.Should().Equal("authored");
        second.Should().NotBeSameAs(first);
    }

    private static EventEnvelope CreateEnvelope(
        EventId eventId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload)
    {
        var constructor = typeof(EventEnvelope).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(EventId), typeof(WorkflowEventContract), typeof(CorrelationId), typeof(DateTimeOffset), typeof(ReadOnlyMemory<byte>)],
            modifiers: null)!;
        return (EventEnvelope)constructor.Invoke([eventId, eventContract, correlationId, occurredAt, payload]);
    }

    [Fact]
    public void Payload_RequiresExactNameVersionAndSupportsAThirdPartyPayloadType()
    {
        var expected = WorkflowEventContract<ThirdPartyMessage>.Create(
            EventName.Create("ThirdPartyReceived"),
            new EventContractVersion(2));
        var bytes = ReadPayloadBytes(WorkflowInboundEvent<ThirdPartyMessage>.Create(
            expected,
            EventId.Create(Guid.CreateVersion7().ToString()),
            CorrelationId.Create("third-party-7"),
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(InstanceId.Parse(Guid.CreateVersion7().ToString())),
            new ThirdPartyMessage { Code = "external" }));
        var envelope = CreateEnvelope(
            EventId.Create(Guid.CreateVersion7().ToString()),
            expected,
            CorrelationId.Create("third-party-7"),
            DateTimeOffset.UtcNow,
            bytes);

        envelope.GetPayload(WorkflowEventContract<ThirdPartyMessage>.Create(
            EventName.Create("ThirdPartyReceived"), new EventContractVersion(2))).Code.Should().Be("external");
        var wrongVersion = WorkflowEventContract<ThirdPartyMessage>.Create(
            EventName.Create("ThirdPartyReceived"), new EventContractVersion(3));
        var wrongName = WorkflowEventContract<ThirdPartyMessage>.Create(
            EventName.Create("OtherEvent"), new EventContractVersion(2));

        envelope.Invoking(value => value.GetPayload(wrongVersion)).Should().Throw<ArgumentException>();
        envelope.Invoking(value => value.GetPayload(wrongName)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Payload_RejectsAnotherTypedDescriptorWithTheSameWireIdentity()
    {
        var expected = WorkflowEventContract<ThirdPartyMessage>.Create(
            EventName.Create("ThirdPartyReceived"),
            new EventContractVersion(2));
        var correlationId = CorrelationId.Create("third-party-8");
        var bytes = ReadPayloadBytes(WorkflowInboundEvent<ThirdPartyMessage>.Create(
            expected,
            EventId.Create(Guid.CreateVersion7().ToString()),
            correlationId,
            causationEventId: null,
            DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(InstanceId.Parse(Guid.CreateVersion7().ToString())),
            new ThirdPartyMessage { Code = "external" }));
        var envelope = CreateEnvelope(
            EventId.Create(Guid.CreateVersion7().ToString()),
            expected,
            correlationId,
            DateTimeOffset.UtcNow,
            bytes);
        var wrongPayloadType = WorkflowEventContract<CompatibleButWrongMessage>.Create(
            EventName.Create("ThirdPartyReceived"),
            new EventContractVersion(2));

        envelope.Invoking(value => value.GetPayload(wrongPayloadType))
            .Should().Throw<ArgumentException>()
            .WithParameterName("eventContract");
    }

    private static ReadOnlyMemory<byte> ReadPayloadBytes<TPayload>(WorkflowInboundEvent<TPayload> workflowEvent)
    {
        var property = typeof(WorkflowInboundEvent<TPayload>).GetProperty(
            "PayloadBytes",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ReadOnlyMemory<byte>)property.GetValue(workflowEvent)!;
    }

    private sealed record Payload(List<string> Values);

    private sealed class ThirdPartyMessage
    {
        public string Code { get; init; } = string.Empty;
    }

    private sealed class CompatibleButWrongMessage
    {
        public string Code { get; init; } = string.Empty;
    }
}
