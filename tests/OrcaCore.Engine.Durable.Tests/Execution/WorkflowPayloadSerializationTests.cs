using System.Text;
using AwesomeAssertions;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class WorkflowPayloadSerializationTests
{
    [Fact]
    public void Serializer_WritesWithConfiguredCodecAndReadsByPayloadContentType()
    {
        var json = new RecordingCodec("application/json");
        var binary = new RecordingCodec("application/vnd.test+binary");
        var serializer = new ContentTypeWorkflowPayloadSerializer(
            [json, binary],
            binary.ContentType);

        var written = serializer.Serialize("new-value");
        var historical = serializer.Deserialize<string>(new SerializedPayload(
            json.ContentType,
            Encoding.UTF8.GetBytes("old-value")));

        written.ContentType.Should().Be(binary.ContentType);
        Encoding.UTF8.GetString(written.Payload).Should().Be("new-value");
        historical.Should().Be("old-value");
        binary.SerializeCalls.Should().Be(1);
        json.DeserializeCalls.Should().Be(1);
    }

    [Fact]
    public void Serializer_UnknownReadContentType_ReturnsClearCompatibilityFailure()
    {
        var serializer = new ContentTypeWorkflowPayloadSerializer(
            [new RecordingCodec("application/json")],
            "application/json");

        var act = () => serializer.Deserialize<string>(new SerializedPayload(
            "application/unknown",
            []));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*application/unknown*registered*");
    }

    [Fact]
    public void Serializer_DuplicateCodecContentType_IsRejectedAtCompositionTime()
    {
        var act = () => new ContentTypeWorkflowPayloadSerializer(
            [new RecordingCodec("application/json"), new RecordingCodec("APPLICATION/JSON")],
            "application/json");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate*application/json*");
    }

    [Fact]
    public void JsonAdapter_AdvertisesStableContentTypeAndRoundTrips()
    {
        IWorkflowPayloadCodec codec = new JsonWorkflowPayloadSerializer();

        var payload = codec.Serialize(new TestPayload("value"));
        var restored = codec.Deserialize<TestPayload>(payload);

        codec.ContentType.Should().Be("application/json");
        payload.ContentType.Should().Be(codec.ContentType);
        restored.Should().Be(new TestPayload("value"));
    }

    private sealed record TestPayload(string Value);

    private sealed class RecordingCodec(string contentType) : IWorkflowPayloadCodec
    {
        public string ContentType { get; } = contentType;

        internal int DeserializeCalls { get; private set; }

        internal int SerializeCalls { get; private set; }

        public SerializedPayload Serialize<TPayload>(TPayload payload)
        {
            SerializeCalls++;
            return new SerializedPayload(ContentType, Encoding.UTF8.GetBytes(payload?.ToString() ?? string.Empty));
        }

        public TPayload Deserialize<TPayload>(SerializedPayload payload)
        {
            DeserializeCalls++;
            return (TPayload)(object)Encoding.UTF8.GetString(payload.Payload);
        }
    }
}
