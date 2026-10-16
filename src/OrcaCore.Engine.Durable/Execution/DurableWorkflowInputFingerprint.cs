using System.Security.Cryptography;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableWorkflowInputFingerprint
{
    internal static string Create(ReadOnlySpan<byte> fixedCodecPayload) =>
        Convert.ToHexString(SHA256.HashData(fixedCodecPayload));

    internal static string Create<TInput>(TInput input)
    {
        var fixedCodecPayload = CoreWorkflowValueCodec.Serialize(input, typeof(TInput));
        return Create(fixedCodecPayload.AsSpan());
    }
}
