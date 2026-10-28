using System.Text.Json;
using System.Text.Json.Serialization;
using OrcaCore;
using OrcaCore.Abstractions.Ids;

internal static class StrongValueCases
{
    internal static async Task<string> Run(string mode)
    {
        JsonSerializerContext context = mode == "strong-default" ? StrongDefaultContext.Default : StrongMetadataContext.Default;
        if (mode == "strong-default") WorkflowJsonMetadata.Register<StrongDefaultContext>();
        else if (mode == "strong-metadata") WorkflowJsonMetadata.Register<StrongMetadataContext>();
        var fingerprints = new List<string>();
        await Check(EventName.Create, value => value.Value, context, fingerprints);
        await Check(WorkflowOutcomeName.Create, value => value.Value, context, fingerprints);
        await Check(AuthoredBranchId.Create, value => value.Value, context, fingerprints);
        await Check(ResourcePoolName.Create, value => value.Value, context, fingerprints);
        await Check(TransientPoolName.Create, value => value.Value, context, fingerprints);
        await Check(StartIdempotencyKey.Create, value => value.Value, context, fingerprints);
        await Check(StopConfirmationId.Create, value => value.Value, context, fingerprints);
        await Check(ResourcePoolOperationId.Create, value => value.Value, context, fingerprints);
        await Check(ResourceGovernancePartitionId.Create, value => value.Value, context, fingerprints);
        await Check(StepOperationId.Parse, value => value.Value, context, fingerprints);
        await Check(LeaseProtectionToken.Parse, value => value.Value, context, fingerprints);
        return string.Join("|", fingerprints);
    }

    private static async Task Check<T>(Func<string, T> parse, Func<T, string> format,
        JsonSerializerContext context, List<string> fingerprints) where T : class
    {
        var value = parse("catalogue-value");
        var different = parse("different-value");
        foreach (var sample in new[] { "catalogue-value", "a\"b\\c" })
        {
            var json = sample == "catalogue-value" ? "\"catalogue-value\"" : "\"a\\u0022b\\\\c\"";
            var contract = context.GetTypeInfo(typeof(T)) ?? throw new InvalidOperationException("missing generated root " + typeof(T));
            Check(JsonSerializer.Serialize(parse(sample), contract) == json, "external generator emits fixed literal bytes for " + typeof(T));
            var decoded = (T)JsonSerializer.Deserialize(json, contract)!;
            Check(format(decoded) == sample, "generated converter preserves parsed value for " + typeof(T));
        }
        var root = Materialize(value);
        var envelope = new StrongEnvelope<T> { Value = value, Values = [value] };
        var copy = Materialize(envelope);
        var list = Materialize(new List<T> { value });
        Check(format(root) == "catalogue-value" && format(copy.Value) == "catalogue-value" &&
            format(copy.Values.Single()) == "catalogue-value" && format(list.Single()) == "catalogue-value" &&
            !ReferenceEquals(copy, envelope) && !ReferenceEquals(copy.Values, envelope.Values), "root/nested/collection closure detaches " + typeof(T));
        Check(JsonSerializer.Deserialize("null", context.GetTypeInfo(typeof(T))!) is null, "existing reference converter null handling stays unchanged");
        foreach (var invalid in new[] { "\"\"", "\" \"", "\" padded\"" })
        {
            try { _ = JsonSerializer.Deserialize(invalid, context.GetTypeInfo(typeof(T))!); }
            catch (ArgumentException) { continue; }
            throw new InvalidOperationException("ASSERTION: existing string validation was weakened for " + typeof(T));
        }
        fingerprints.Add(await CodecCases.CheckFingerprint(value, different, "\"catalogue-value\""));
        fingerprints.Add(await CodecCases.CheckFingerprint(envelope,
            new StrongEnvelope<T> { Value = different, Values = [different] }, "{\"Value\":\"catalogue-value\",\"Values\":[\"catalogue-value\"]}"));
        fingerprints.Add(await CodecCases.CheckFingerprint(new List<T> { value }, new List<T> { different }, "[\"catalogue-value\"]"));
    }

    internal static void FactoryBoundary()
    {
        var factory = new StrongStringValueJsonConverterFactory();
        Check(typeof(StrongStringValueJsonConverterFactory).IsSealed, "public factory remains sealed");
        Check(factory.CanConvert(typeof(EventName)), "closed table retains EventName");
        Check(factory.CanConvert(typeof(WorkflowOutcomeName)), "closed table retains WorkflowOutcomeName");
        Check(factory.CanConvert(typeof(AuthoredBranchId)), "closed table retains AuthoredBranchId");
        Check(factory.CanConvert(typeof(ResourcePoolName)), "closed table retains ResourcePoolName");
        Check(factory.CanConvert(typeof(TransientPoolName)), "closed table retains TransientPoolName");
        Check(factory.CanConvert(typeof(StartIdempotencyKey)), "closed table retains StartIdempotencyKey");
        Check(factory.CanConvert(typeof(StopConfirmationId)), "closed table retains StopConfirmationId");
        Check(factory.CanConvert(typeof(ResourcePoolOperationId)), "closed table retains ResourcePoolOperationId");
        Check(factory.CanConvert(typeof(ResourceGovernancePartitionId)), "closed table retains ResourceGovernancePartitionId");
        Check(factory.CanConvert(typeof(StepOperationId)), "closed table retains StepOperationId");
        Check(factory.CanConvert(typeof(LeaseProtectionToken)), "closed table retains LeaseProtectionToken");
        Check(!factory.CanConvert(typeof(string)), "unsupported public factory type is not admitted");
        try { factory.CreateConverter(typeof(string), new JsonSerializerOptions()); }
        catch (NotSupportedException)
        {
            var converter = (JsonConverter<EventName>)factory.CreateConverter(typeof(EventName), new JsonSerializerOptions());
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer)) converter.Write(writer, EventName.Create("catalogue-value"), new JsonSerializerOptions());
            Check(System.Text.Encoding.UTF8.GetString(buffer.ToArray()) == "\"catalogue-value\"", "direct converter works before metadata configuration");
            return;
        }
        throw new InvalidOperationException("ASSERTION: unsupported direct factory request did not throw");
    }

    private static T Materialize<T>(T value) => WorkflowInboundEvent<T>.Create(
        WorkflowEventContract<T>.Create(EventName.Create("strong"), EventContractVersion.Initial),
        EventId.Create("strong"), CorrelationId.Create("strong"), null, DateTimeOffset.UtcNow,
        new WorkflowEventRoute.Correlation(DefinitionId.New()), value).Payload;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ASSERTION: " + message);
    }
}

public sealed class StrongEnvelope<T>
{
    public T Value { get; set; } = default!;
    public List<T> Values { get; set; } = [];
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(EventName))]
[JsonSerializable(typeof(StrongEnvelope<EventName>))]
[JsonSerializable(typeof(WorkflowOutcomeName))]
[JsonSerializable(typeof(StrongEnvelope<WorkflowOutcomeName>))]
[JsonSerializable(typeof(AuthoredBranchId))]
[JsonSerializable(typeof(StrongEnvelope<AuthoredBranchId>))]
[JsonSerializable(typeof(ResourcePoolName))]
[JsonSerializable(typeof(StrongEnvelope<ResourcePoolName>))]
[JsonSerializable(typeof(TransientPoolName))]
[JsonSerializable(typeof(StrongEnvelope<TransientPoolName>))]
[JsonSerializable(typeof(StartIdempotencyKey))]
[JsonSerializable(typeof(StrongEnvelope<StartIdempotencyKey>))]
[JsonSerializable(typeof(StopConfirmationId))]
[JsonSerializable(typeof(StrongEnvelope<StopConfirmationId>))]
[JsonSerializable(typeof(ResourcePoolOperationId))]
[JsonSerializable(typeof(StrongEnvelope<ResourcePoolOperationId>))]
[JsonSerializable(typeof(ResourceGovernancePartitionId))]
[JsonSerializable(typeof(StrongEnvelope<ResourceGovernancePartitionId>))]
[JsonSerializable(typeof(StepOperationId))]
[JsonSerializable(typeof(StrongEnvelope<StepOperationId>))]
[JsonSerializable(typeof(LeaseProtectionToken))]
[JsonSerializable(typeof(StrongEnvelope<LeaseProtectionToken>))]
public partial class StrongMetadataContext : JsonSerializerContext;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(EventName))]
[JsonSerializable(typeof(StrongEnvelope<EventName>))]
[JsonSerializable(typeof(WorkflowOutcomeName))]
[JsonSerializable(typeof(StrongEnvelope<WorkflowOutcomeName>))]
[JsonSerializable(typeof(AuthoredBranchId))]
[JsonSerializable(typeof(StrongEnvelope<AuthoredBranchId>))]
[JsonSerializable(typeof(ResourcePoolName))]
[JsonSerializable(typeof(StrongEnvelope<ResourcePoolName>))]
[JsonSerializable(typeof(TransientPoolName))]
[JsonSerializable(typeof(StrongEnvelope<TransientPoolName>))]
[JsonSerializable(typeof(StartIdempotencyKey))]
[JsonSerializable(typeof(StrongEnvelope<StartIdempotencyKey>))]
[JsonSerializable(typeof(StopConfirmationId))]
[JsonSerializable(typeof(StrongEnvelope<StopConfirmationId>))]
[JsonSerializable(typeof(ResourcePoolOperationId))]
[JsonSerializable(typeof(StrongEnvelope<ResourcePoolOperationId>))]
[JsonSerializable(typeof(ResourceGovernancePartitionId))]
[JsonSerializable(typeof(StrongEnvelope<ResourceGovernancePartitionId>))]
[JsonSerializable(typeof(StepOperationId))]
[JsonSerializable(typeof(StrongEnvelope<StepOperationId>))]
[JsonSerializable(typeof(LeaseProtectionToken))]
[JsonSerializable(typeof(StrongEnvelope<LeaseProtectionToken>))]
public partial class StrongDefaultContext : JsonSerializerContext;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, Converters = new[] { typeof(StrongStringValueJsonConverterFactory) })]
[JsonSerializable(typeof(StrongEnvelope<EventName>))]
public partial class ExplicitProductConverterContext : JsonSerializerContext;
