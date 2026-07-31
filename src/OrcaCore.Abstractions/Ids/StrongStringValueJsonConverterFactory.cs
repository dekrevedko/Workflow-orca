using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrcaCore.Abstractions.Ids;

internal sealed class StrongStringValueJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(EventName) ||
            typeToConvert == typeof(WorkflowOutcomeName) ||
            typeToConvert == typeof(AuthoredBranchId) ||
            typeToConvert == typeof(ResourcePoolName) ||
            typeToConvert == typeof(TransientPoolName) ||
            typeToConvert == typeof(StartIdempotencyKey) ||
            typeToConvert == typeof(StopConfirmationId) ||
            typeToConvert == typeof(ResourcePoolOperationId) ||
            typeToConvert == typeof(ResourceGovernancePartitionId) ||
            typeToConvert == typeof(StepOperationId) ||
            typeToConvert == typeof(LeaseProtectionToken);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(EventName))
            return new StringValueJsonConverter<EventName>(EventName.Create, value => value.Value);
        if (typeToConvert == typeof(WorkflowOutcomeName))
            return new StringValueJsonConverter<WorkflowOutcomeName>(WorkflowOutcomeName.Create, value => value.Value);
        if (typeToConvert == typeof(AuthoredBranchId))
            return new StringValueJsonConverter<AuthoredBranchId>(AuthoredBranchId.Create, value => value.Value);
        if (typeToConvert == typeof(ResourcePoolName))
            return new StringValueJsonConverter<ResourcePoolName>(ResourcePoolName.Create, value => value.Value);
        if (typeToConvert == typeof(TransientPoolName))
            return new StringValueJsonConverter<TransientPoolName>(TransientPoolName.Create, value => value.Value);
        if (typeToConvert == typeof(StartIdempotencyKey))
            return new StringValueJsonConverter<StartIdempotencyKey>(StartIdempotencyKey.Create, value => value.Value);
        if (typeToConvert == typeof(StopConfirmationId))
            return new StringValueJsonConverter<StopConfirmationId>(StopConfirmationId.Create, value => value.Value);
        if (typeToConvert == typeof(ResourcePoolOperationId))
            return new StringValueJsonConverter<ResourcePoolOperationId>(ResourcePoolOperationId.Create, value => value.Value);
        if (typeToConvert == typeof(ResourceGovernancePartitionId))
            return new StringValueJsonConverter<ResourceGovernancePartitionId>(ResourceGovernancePartitionId.Create, value => value.Value);
        if (typeToConvert == typeof(StepOperationId))
            return new StringValueJsonConverter<StepOperationId>(StepOperationId.Parse, value => value.Value);
        if (typeToConvert == typeof(LeaseProtectionToken))
            return new StringValueJsonConverter<LeaseProtectionToken>(LeaseProtectionToken.Parse, value => value.Value);
        throw new NotSupportedException($"Strong-value JSON conversion is not registered for '{typeToConvert}'.");
    }

    private sealed class StringValueJsonConverter<T>(Func<string, T> parse, Func<T, string> format)
        : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(format(value));
        }
    }
}
