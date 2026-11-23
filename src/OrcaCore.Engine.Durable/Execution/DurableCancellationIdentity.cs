using System.Security.Cryptography;
using System.Text;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Durable.Execution;

internal enum DurableCancellationOrigin { Independent, OwningDag, LegacyUnknown }

internal static class DurableCancellationIdentity
{
    // Persisted recipe. One cancellation can win per instance. The two issuers use distinct
    // command IDs so the winning request remains attributable even if another host finalizes it.
    internal static CommandId For(InstanceId instance, DurableCancellationOrigin origin)
    {
        var issuer = origin switch
        {
            DurableCancellationOrigin.Independent => "independent",
            DurableCancellationOrigin.OwningDag => "owning-dag",
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        var recipe = $"orcacore-cancel-v1\n{instance.Value:D}\n{issuer}\n";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(recipe));
        digest[6] = (byte)((digest[6] & 0x0f) | 0x80); // UUIDv8 in network byte order.
        digest[8] = (byte)((digest[8] & 0x3f) | 0x80);
        return new CommandId(new Guid(digest.AsSpan(0, 16), bigEndian: true));
    }

    internal static DurableCancellationOrigin Read(InstanceId instance, CommandId winner)
    {
        if (winner == For(instance, DurableCancellationOrigin.Independent)) return DurableCancellationOrigin.Independent;
        if (winner == For(instance, DurableCancellationOrigin.OwningDag)) return DurableCancellationOrigin.OwningDag;
        if (winner.Value.Version == 7 && (winner.Value.Variant & 0xC) == 8) return DurableCancellationOrigin.LegacyUnknown;
        throw new InvalidDataException("The winning cancellation command is not bound to this child.");
    }
}
