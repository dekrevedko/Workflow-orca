using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal static class FiberIdentity
{
    internal static FiberId CreateRoot(InstanceId instanceId, long generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generation);

        return new FiberId(Hash(
            "root",
            instanceId.ToString(),
            generation.ToString(CultureInfo.InvariantCulture)));
    }

    internal static ScopeId CreateScope(
        FiberId parentFiberId,
        ScopePlanId scopePlanId,
        long scopeEntrySequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(scopeEntrySequence);

        return new ScopeId(Hash(
            "scope",
            parentFiberId.Value,
            scopePlanId.Value,
            scopeEntrySequence.ToString(CultureInfo.InvariantCulture)));
    }

    internal static FiberId CreateChild(ScopeId scopeId, BranchPlanId branchPlanId)
    {
        return new FiberId(Hash("child", scopeId.Value, branchPlanId.Value));
    }

    internal static FiberId CreateItem(ScopeId scopeId, int itemIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemIndex);

        return new FiberId(Hash(
            "item",
            scopeId.Value,
            itemIndex.ToString(CultureInfo.InvariantCulture)));
    }

    private static string Hash(params string[] parts)
    {
        var canonical = string.Join('|', parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
