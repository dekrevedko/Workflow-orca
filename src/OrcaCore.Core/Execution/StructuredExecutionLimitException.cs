namespace OrcaCore.Core.Execution;

internal sealed class StructuredExecutionLimitException(
    string code,
    string message) : Exception(message)
{
    internal string Code { get; } = code;
}

internal static class StructuredExecutionLimitCodes
{
    internal const string SerializedResultExceeded = "SFE-LIMIT-009";

    internal const string SerializedEnvelopeExceeded = "SFE-LIMIT-010";
}
