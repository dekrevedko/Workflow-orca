// AMENDMENT PROOF ONLY. Not a product codec, provider, bridge, or exported API.
// Compile together with the dated signatures, assembly name OrcaCore.Runtime.Protocol,
// referencing only the shipped OrcaCore.dll (no friend grant or product source edit).
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OrcaCore;
using OrcaCore.Abstractions.Durable.Dag;

namespace FingerprintContractProof;

internal static class Program
{
    private static void Main()
    {
#if FORBIDDEN_FINGERPRINT_CONSTRUCTORS
        _ = new DefinitionFingerprint(new string('A', 64));
        _ = new PayloadFingerprint(new string('B', 64));
#else
        var bytes = Encoding.UTF8.GetBytes("null");
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        var binding = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var value = new DagRunValueV1(true, "workflow:proof:1:input", "orcacore-json-v1", bytes, digest);
        var bindingJson = JsonSerializer.SerializeToUtf8Bytes(binding);
        var valueJson = JsonSerializer.SerializeToUtf8Bytes(value);

        // Same opaque text travels through protocol decode, provider materialization and
        // a Hosting load. None of these steps constructs an OrcaCore fingerprint object.
        foreach (var boundary in new[] { "protocol", "provider", "hosting" })
        {
            binding = ReadBinding(bindingJson);
            value = ReadValue(valueJson);
            Require(binding.Fingerprint == new string('A', 64), "definition text changed");
            Require(value.Fingerprint == digest && value.Bytes.SequenceEqual(bytes), "value text/bytes changed");
            Require(value.IsPresent && value.CodecFormat == "orcacore-json-v1", "present-null lost");
            bindingJson = JsonSerializer.SerializeToUtf8Bytes(binding);
            valueJson = JsonSerializer.SerializeToUtf8Bytes(value);
            Console.WriteLine(boundary + ": canonical fingerprint strings reconstructed without friend access");
        }

        foreach (var invalid in new string?[]
        {
            null, "", " ", digest.ToLowerInvariant(), " " + digest, digest + " ",
            digest[..63], digest + "0", "G" + digest[1..], "a" + digest[1..]
        })
        {
            var badBinding = binding with { Fingerprint = invalid! };
            var badValue = value with { Fingerprint = invalid! };
            Reject(() => ReadBinding(JsonSerializer.SerializeToUtf8Bytes(badBinding)));
            Reject(() => ReadValue(JsonSerializer.SerializeToUtf8Bytes(badValue)));
        }
        // Valid shape is not authenticity: a different valid digest must still fail
        // installed-definition binding or committed-value byte checks at runtime.
        var different = value with { Fingerprint = new string('B', 64) };
        Reject(() => ReadValue(JsonSerializer.SerializeToUtf8Bytes(different)));
        Console.WriteLine("21 negative controls rejected; no typed factory, reflection, UnsafeAccessor or converter hook");
#endif
    }

    private static DagRunBindingV1 ReadBinding(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        Fingerprint(document.RootElement.GetProperty("Fingerprint"));
        return JsonSerializer.Deserialize<DagRunBindingV1>(bytes)
            ?? throw new JsonException("Binding required.");
    }

    private static DagRunValueV1 ReadValue(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var fingerprint = Fingerprint(document.RootElement.GetProperty("Fingerprint"));
        var value = JsonSerializer.Deserialize<DagRunValueV1>(bytes)
            ?? throw new JsonException("Value required.");
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(value.Bytes)), fingerprint, StringComparison.Ordinal))
            throw new JsonException("Fingerprint differs from the exact committed bytes.");
        return value;
    }

    private static string Fingerprint(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new JsonException("Fingerprint must be a string.");
        var text = element.GetString()!;
        if (text.Length != 64 || text.Any(c => c is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new JsonException("Fingerprint must be exactly 64 uppercase ASCII hexadecimal characters.");
        return text;
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (JsonException) { return; }
        throw new InvalidOperationException("Invalid persisted fingerprint was accepted.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
