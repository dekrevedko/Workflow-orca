using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrcaCore.Dag;

/// <summary>Caller-created identity for one authored DAG node.</summary>
[JsonConverter(typeof(DagStrongValueJsonConverterFactory))]
public sealed class DagNodeId : IEquatable<DagNodeId>
{
    private DagNodeId(string value) => Value = value;

    public string Value { get; }

    public static DagNodeId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("A DAG node identity cannot have surrounding whitespace.", nameof(value));
        }

        return new DagNodeId(value);
    }

    public bool Equals(DagNodeId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DagNodeId other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}

/// <summary>Opaque runtime-created identity for one DAG run.</summary>
[JsonConverter(typeof(DagStrongValueJsonConverterFactory))]
public sealed class DagRunId : IEquatable<DagRunId>
{
    private DagRunId(Guid value) => Value = value;

    /// <summary>Gets the stable run value.</summary>
    public Guid Value { get; }

    /// <summary>Parses one runtime-created run identity.</summary>
    public static DagRunId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? new DagRunId(parsed)
            : throw new FormatException($"'{value}' is not a valid DAG run identity.");
    }

    /// <summary>Attempts to parse one runtime-created run identity.</summary>
    public static bool TryParse(string? value, out DagRunId? runId)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            runId = new DagRunId(parsed);
            return true;
        }

        runId = null;
        return false;
    }

    internal static DagRunId New() => new(Guid.CreateVersion7());

    public bool Equals(DagRunId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is DagRunId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString("D");
}

internal sealed class DagStrongValueJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(DagNodeId) || typeToConvert == typeof(DagRunId);

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options) =>
        typeToConvert == typeof(DagNodeId)
            ? new DagNodeIdConverter()
            : new DagRunIdConverter();

    private sealed class DagNodeIdConverter : JsonConverter<DagNodeId>
    {
        public override DagNodeId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            DagNodeId.Create(reader.GetString()!);

        public override void Write(
            Utf8JsonWriter writer,
            DagNodeId value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class DagRunIdConverter : JsonConverter<DagRunId>
    {
        public override DagRunId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            DagRunId.Parse(reader.GetString()!);

        public override void Write(
            Utf8JsonWriter writer,
            DagRunId value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}

/// <summary>Reports that one DAG run does not exist.</summary>
public sealed class DagRunNotFoundException : OrcaCoreException
{
    internal DagRunNotFoundException(DagRunId runId)
        : base("DAG-RUN-NOT-FOUND", $"DAG run '{runId}' was not found.") =>
        RunId = runId;

    public DagRunId RunId { get; }
}

/// <summary>Reports that a run belongs to another DAG definition.</summary>
public sealed class DagRunDefinitionMismatchException : OrcaCoreException
{
    internal DagRunDefinitionMismatchException(
        DagRunId runId,
        DefinitionId expectedDefinitionId,
        DefinitionId actualDefinitionId)
        : base(
            "DAG-RUN-DEFINITION-MISMATCH",
            $"DAG run '{runId}' belongs to definition '{actualDefinitionId}', not '{expectedDefinitionId}'.")
    {
        RunId = runId;
        ExpectedDefinitionId = expectedDefinitionId;
        ActualDefinitionId = actualDefinitionId;
    }

    public DagRunId RunId { get; }

    public DefinitionId ExpectedDefinitionId { get; }

    public DefinitionId ActualDefinitionId { get; }
}

/// <summary>Projects a typed DAG registration conflict through the success helper.</summary>
public sealed class DagDefinitionRegistrationConflictException : OrcaCoreException
{
    internal DagDefinitionRegistrationConflictException(DefinitionRegistrationConflict conflict)
        : base(
            "DAG-DEFINITION-REGISTRATION-CONFLICT",
            $"DAG definition registration conflicts with fingerprint '{conflict.ExistingFingerprint}'.") =>
        Conflict = conflict;

    public DefinitionRegistrationConflict Conflict { get; }
}

/// <summary>Projects a typed DAG start-key conflict through the success helper.</summary>
public sealed class DagStartIdempotencyConflictException : OrcaCoreException
{
    internal DagStartIdempotencyConflictException(StartIdempotencyConflict conflict)
        : base(
            "DAG-START-IDEMPOTENCY-CONFLICT",
            $"DAG start key conflicts with definition '{conflict.ExistingDefinitionId}'.") =>
        Conflict = conflict;

    public StartIdempotencyConflict Conflict { get; }
}
