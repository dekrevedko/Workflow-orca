using System.Text;

namespace OrcaCore.Tests.Durable;

public sealed class PayloadEnvelopeSerializerTests
{
    private record BasePayload(string Value);

    private sealed record DerivedPayload(string Value, string Extra) : BasePayload(Value);

    [Fact]
    public void Registered_payload_round_trips_through_envelope()
    {
        var registry = new DurablePayloadTypeRegistry().Register<string>("string");
        var serializer = new JsonPayloadEnvelopeSerializer(registry);

        var serialized = serializer.Serialize("approved", typeof(string));
        var deserialized = serializer.Deserialize(serialized.Value!.Payload, serialized.Value.TypeKey, typeof(string));

        Assert.True(serialized.IsSuccess);
        Assert.Equal("string", serialized.Value.TypeKey);
        Assert.Equal("application/json", serialized.Value.Payload.ContentType);
        Assert.Equal("approved", Assert.IsType<string>(deserialized.Value));
    }

    [Fact]
    public void Unregistered_payload_type_fails_clearly()
    {
        var serializer = new JsonPayloadEnvelopeSerializer(new DurablePayloadTypeRegistry());

        var result = serializer.Serialize(new BasePayload("x"), typeof(BasePayload));

        Assert.True(result.IsFailure);
        Assert.IsType<DurablePayloadSerializationException>(result.Error);
    }

    [Fact]
    public void Null_payload_is_serialized_explicitly()
    {
        var registry = new DurablePayloadTypeRegistry().Register<string>("string");
        var serializer = new JsonPayloadEnvelopeSerializer(registry);

        var result = serializer.Serialize(null, typeof(string));

        Assert.True(result.IsSuccess);
        Assert.Equal("null", Encoding.UTF8.GetString(result.Value!.Payload.Body.Span));
        Assert.Null(serializer.Deserialize(result.Value.Payload, result.Value.TypeKey, typeof(string)).Value);
    }

    [Fact]
    public void Declared_type_controls_schema_and_type_key()
    {
        var registry = new DurablePayloadTypeRegistry()
            .Register<BasePayload>("base", "schema-base")
            .Register<DerivedPayload>("derived", "schema-derived");
        var serializer = new JsonPayloadEnvelopeSerializer(registry);

        var result = serializer.Serialize(new DerivedPayload("v", "extra"), typeof(BasePayload));

        Assert.True(result.IsSuccess);
        Assert.Equal("base", result.Value!.TypeKey);
        Assert.Equal("schema-base", result.Value.Payload.SchemaId);
    }

    [Fact]
    public void Serializer_produces_identical_bytes_on_repeated_calls()
    {
        var registry = new DurablePayloadTypeRegistry().Register<string>("string");
        var serializer = new JsonPayloadEnvelopeSerializer(registry);

        var first = serializer.Serialize("hello", typeof(string)).Value!;
        var second = serializer.Serialize("hello", typeof(string)).Value!;

        Assert.True(first.Payload.Body.Span.SequenceEqual(second.Payload.Body.Span));
        Assert.Equal(first.TypeKey, second.TypeKey);
    }

    [Fact]
    public void Deserialize_returns_failure_when_target_type_is_incompatible()
    {
        // "string" typeKey resolves to typeof(string), which is not assignable to int
        var serializer = new JsonPayloadEnvelopeSerializer(DurablePayloadTypeRegistry.Default);
        var serialized = serializer.Serialize("hello", typeof(string)).Value!;

        var result = serializer.Deserialize(serialized.Payload, serialized.TypeKey, typeof(int));

        Assert.True(result.IsFailure);
        Assert.IsType<DurablePayloadDeserializationException>(result.Error);
    }

    [Fact]
    public void Serializer_schema_id_and_content_type_match_registry_registration()
    {
        var registry = new DurablePayloadTypeRegistry()
            .Register<string>("string", "acme-schema-v1", "application/vnd.acme+json");
        var serializer = new JsonPayloadEnvelopeSerializer(registry);

        var result = serializer.Serialize("test", typeof(string)).Value!;

        Assert.Equal("acme-schema-v1", result.Payload.SchemaId);
        Assert.Equal("application/vnd.acme+json", result.Payload.ContentType);
    }
}
