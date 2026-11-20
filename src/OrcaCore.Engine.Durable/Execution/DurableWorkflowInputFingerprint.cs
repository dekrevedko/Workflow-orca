using System.Security.Cryptography;
using OrcaCore.Core.Internal;
using OrcaCore.Engine.Durable.Internal;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableWorkflowInputFingerprint
{
    internal static string Create(ReadOnlySpan<byte> fixedCodecPayload) =>
        Convert.ToHexString(SHA256.HashData(fixedCodecPayload));
}

internal sealed record DurableStartInput(SerializedPayload Payload, PayloadFingerprint Fingerprint)
{
    internal static DurableStartInput Capture<TInput>(TInput input)
    {
        var bytes = CoreWorkflowValueCodec.Serialize(input, typeof(TInput));
        return new(new SerializedPayload(CoreWorkflowValueCodec.Format, bytes),
            DurableContractAdapter.PayloadFingerprint(bytes));
    }
}
