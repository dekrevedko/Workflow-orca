using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore
{
    internal static class StrongValueValidation
    {
        internal static string CallerCreated(string value, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);
            if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException("Value must be non-empty and have no leading or trailing whitespace.", parameterName);
            }

            return value;
        }

        internal static string RuntimeCreated(string value, string parameterName)
        {
            return CallerCreated(value, parameterName);
        }
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class EventName : IEquatable<EventName>
    {
        private EventName(string value) => Value = value;
        public string Value { get; }
        public static EventName Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(EventName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is EventName other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class WorkflowOutcomeName : IEquatable<WorkflowOutcomeName>
    {
        private WorkflowOutcomeName(string value) => Value = value;
        public string Value { get; }
        public static WorkflowOutcomeName Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(WorkflowOutcomeName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is WorkflowOutcomeName other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class AuthoredBranchId : IEquatable<AuthoredBranchId>
    {
        private AuthoredBranchId(string value) => Value = value;
        public string Value { get; }
        public static AuthoredBranchId Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(AuthoredBranchId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is AuthoredBranchId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class ResourcePoolName : IEquatable<ResourcePoolName>
    {
        private ResourcePoolName(string value) => Value = value;
        public string Value { get; }
        public static ResourcePoolName Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(ResourcePoolName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ResourcePoolName other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class TransientPoolName : IEquatable<TransientPoolName>
    {
        private TransientPoolName(string value) => Value = value;
        public string Value { get; }
        public static TransientPoolName Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(TransientPoolName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is TransientPoolName other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class StartIdempotencyKey : IEquatable<StartIdempotencyKey>
    {
        private StartIdempotencyKey(string value) => Value = value;
        public string Value { get; }
        public static StartIdempotencyKey Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(StartIdempotencyKey? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is StartIdempotencyKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class StopConfirmationId : IEquatable<StopConfirmationId>
    {
        private StopConfirmationId(string value) => Value = value;
        public string Value { get; }
        public static StopConfirmationId Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(StopConfirmationId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is StopConfirmationId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class ResourcePoolOperationId : IEquatable<ResourcePoolOperationId>
    {
        private ResourcePoolOperationId(string value) => Value = value;
        public string Value { get; }
        public static ResourcePoolOperationId Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(ResourcePoolOperationId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ResourcePoolOperationId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class ResourceGovernancePartitionId : IEquatable<ResourceGovernancePartitionId>
    {
        private ResourceGovernancePartitionId(string value) => Value = value;
        public string Value { get; }
        public static ResourceGovernancePartitionId Create(string value) => new(StrongValueValidation.CallerCreated(value, nameof(value)));
        public bool Equals(ResourceGovernancePartitionId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ResourceGovernancePartitionId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class StepOperationId : IEquatable<StepOperationId>
    {
        private StepOperationId(string value) => Value = value;
        public string Value { get; }
        public static StepOperationId Parse(string value) => new(StrongValueValidation.RuntimeCreated(value, nameof(value)));
        public static bool TryParse(string? value, out StepOperationId? operationId) => TryParseRuntime(value, out operationId, static parsed => new StepOperationId(parsed));
        public bool Equals(StepOperationId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is StepOperationId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        private static bool TryParseRuntime<T>(string? value, out T? result, Func<string, T> factory) where T : class
        {
            if (value is not null && !string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                result = factory(value);
                return true;
            }

            result = null;
            return false;
        }
    }

    [JsonConverter(typeof(StrongStringValueJsonConverterFactory))]
    public sealed class LeaseProtectionToken : IEquatable<LeaseProtectionToken>
    {
        private LeaseProtectionToken(string value) => Value = value;
        public string Value { get; }
        public static LeaseProtectionToken Parse(string value) => new(StrongValueValidation.RuntimeCreated(value, nameof(value)));
        public static bool TryParse(string? value, out LeaseProtectionToken? token)
        {
            if (value is not null && !string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                token = new LeaseProtectionToken(value);
                return true;
            }

            token = null;
            return false;
        }
        public bool Equals(LeaseProtectionToken? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is LeaseProtectionToken other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }
}
