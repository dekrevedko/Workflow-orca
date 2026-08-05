namespace OrcaCore.Core.Execution;

internal sealed class StructuredExecutionLimitException(
    string code,
    string message) : global::OrcaCore.OrcaCoreException(code, message);

internal static class StructuredExecutionLimitCodes
{
    public const string ForEachItemsExceeded = "SFE-LIMIT-001";

    public const string SerializedResultExceeded = "SFE-LIMIT-009";

    public const string SerializedEnvelopeExceeded = "SFE-LIMIT-010";

    public const string EncodedValueExceeded = "SFE-LIMIT-011";
}
