using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Dag;

internal static class DurableDagValues
{
    internal static DagRunValueV1 Normalize(string key, object? value, Type declaredType)
    {
        if (value is null && declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) is null)
            throw new InvalidDataException("Null is not valid for this declared DAG value type.");
        var bytes = CoreWorkflowValueCodec.Serialize(value, declaredType);
        _ = CoreWorkflowValueCodec.Deserialize(bytes, declaredType);
        return new(true, key, CoreWorkflowValueCodec.Format, bytes, DurableDagWire.ValueFingerprint(bytes));
    }

    // Run input is immutable and must be reopenable by the type that submits it.
    // This rule deliberately does not change mapped inputs or child outputs.
    internal static DagRunValueV1 NormalizeRunInput(string key, object? value, Type declaredType)
    {
        if (value is null && declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) is null)
            throw new NotSupportedException("Null cannot round-trip through the declared DAG run-input type.");
        var bytes = CoreWorkflowValueCodec.Serialize(value, declaredType);
        if (!RoundTrips(bytes, declaredType))
            throw new NotSupportedException($"A value of '{declaredType}' does not round-trip losslessly through the fixed codec.");
        return new(true, key, CoreWorkflowValueCodec.Format, bytes, DurableDagWire.ValueFingerprint(bytes));
    }

    // Validate the recorded representation against its OWN graph, before comparing
    // it with an installed type. Malformed JSON is corruption, not local unavailability.
    internal static byte[] VerifiedRunInput(DagRunValueV1 value, string key)
    {
        var bytes = VerifiedBytes(value, key);
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(bytes);
        }
        catch (System.Text.Json.JsonException error)
        {
            throw new InvalidDataException("DAG committed run input is malformed.", error);
        }
        return bytes;
    }

    // True only when the bytes decode with the declared type and re-encode to the same bytes.
    internal static bool RoundTrips(byte[] bytes, Type declaredType)
    {
        try
        {
            var decoded = CoreWorkflowValueCodec.Deserialize(bytes, declaredType);
            if (decoded is null && declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) is null)
                return false;
            return CoreWorkflowValueCodec.Serialize(decoded, declaredType).AsSpan().SequenceEqual(bytes);
        }
        catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            return false;
        }
    }

    internal static PayloadFingerprint PublicFingerprint(string fingerprint) =>
        DurableDagChildEngine.RehydrateFingerprint(fingerprint);

    internal static WorkflowFailure NodeFailure(string code, int authoredOrdinal) =>
        OrcaCore.Engine.Durable.Internal.DurableContractAdapter.WorkflowFailure(
            code,
            code switch
            {
                "DAG_INPUT_MAPPING_INVALID" => "The node input mapping was invalid.",
                "CHILD_FAILED" => "The node child workflow failed.",
                "CHILD_TIMED_OUT" => "The node child workflow timed out.",
                "CHILD_TERMINATED" => "The node child workflow was terminated.",
                "CHILD_CANCELLED" => "The node child workflow was cancelled without a run cancellation request.",
                "DEPENDENCY_BLOCKED" => "A dependency of the node did not succeed.",
                _ => throw new InvalidDataException("Unknown committed DAG node failure code.")
            },
            OrcaCore.Engine.Durable.Internal.DurableContractAdapter.AuthoredLocation(
                "dag:$/dag-node:" + authoredOrdinal.ToString("D8", System.Globalization.CultureInfo.InvariantCulture)),
            OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RootFailureOccurrence(),
            []);

    internal static DefinitionRegistrationConflict RegistrationConflict(DefinitionId id, DefinitionVersion version,
        string existingFingerprint, DefinitionFingerprint attempted) =>
        OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionRegistrationConflict(
            id, version, new DefinitionFingerprint(existingFingerprint), attempted);

    internal static DefinitionFingerprint DefinitionFingerprint(string value) => new(value);

    internal static StartIdempotencyConflict StartConflict(StartIdempotencyKey key, DagRunBindingV1 existing,
        string existingInputFingerprint, DefinitionId id, DefinitionVersion version, DefinitionFingerprint attempted,
        string attemptedInputFingerprint) =>
        OrcaCore.Engine.Durable.Internal.DurableContractAdapter.StartIdempotencyConflict(
            key, existing.DefinitionId, existing.DefinitionVersion, existing.Fingerprint, existingInputFingerprint,
            id, version, attempted, PublicFingerprint(attemptedInputFingerprint));

    internal static byte[] VerifiedBytes(DagRunValueV1 value, string key)
    {
        var bytes = value.Bytes;
        if (!value.IsPresent || value.ContractKey != key || value.CodecFormat != CoreWorkflowValueCodec.Format ||
            value.Fingerprint != DurableDagWire.ValueFingerprint(bytes))
            throw new InvalidDataException("DAG committed value has an invalid key, format or fingerprint.");
        return bytes;
    }

    internal static object? DecodeVerified(byte[] bytes, Type declaredType) =>
        CoreWorkflowValueCodec.Deserialize(bytes, declaredType);
}
