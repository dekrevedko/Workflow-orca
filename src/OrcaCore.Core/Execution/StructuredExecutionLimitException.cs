namespace OrcaCore.Core.Execution;

public sealed class StructuredExecutionLimitException(
    string code,
    string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class StructuredExecutionLimitCodes
{
    public const string SerializedResultExceeded = "SFE-LIMIT-009";

    public const string SerializedEnvelopeExceeded = "SFE-LIMIT-010";

    public const string EncodedValueExceeded = "SFE-LIMIT-011";
}
