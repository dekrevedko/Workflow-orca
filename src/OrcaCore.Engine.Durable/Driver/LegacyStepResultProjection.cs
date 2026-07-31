using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Driver;

internal static class LegacyStepResultProjection
{
    internal static bool IsYield(StepResult result) => IsEngineResult(result, "EngineYieldStepResult");

    internal static bool TryExternalJob(StepResult result, out LegacyExternalJob value)
    {
        if (!IsEngineResult(result, "EngineExternalJobStepResult"))
        {
            value = default!;
            return false;
        }

        value = new LegacyExternalJob(
            Get<string>(result, "ExternalJobId"),
            Get<byte[]>(result, "Payload"),
            Get<IReadOnlyList<ResourcePoolRequirement>?>(result, "Requirements"),
            Get<TimeSpan?>(result, "Timeout"));
        return true;
    }

    internal static bool TryAcquireResources(StepResult result, out LegacyAcquireResources value)
    {
        if (!IsEngineResult(result, "EngineAcquireResourcesStepResult"))
        {
            value = default!;
            return false;
        }

        value = new LegacyAcquireResources(
            Get<string>(result, "HolderKey"),
            Get<IReadOnlyList<ResourcePoolRequirement>>(result, "Requirements"),
            Get<TimeSpan?>(result, "LeaseDuration"));
        return true;
    }

    private static bool IsEngineResult(StepResult result, string name) =>
        string.Equals(result.GetType().Name, name, StringComparison.Ordinal) &&
        result.GetType().Assembly == typeof(StepResult).Assembly;

    private static T Get<T>(StepResult result, string property)
    {
        var propertyInfo = result.GetType().GetProperty(property) ??
            throw new InvalidOperationException(
                $"Legacy step result '{result.GetType().FullName}' has no '{property}' property.");
        return (T)propertyInfo.GetValue(result)!;
    }
}

internal sealed record LegacyExternalJob(
    string ExternalJobId,
    byte[] Payload,
    IReadOnlyList<ResourcePoolRequirement>? Requirements,
    TimeSpan? Timeout);

internal sealed record LegacyAcquireResources(
    string HolderKey,
    IReadOnlyList<ResourcePoolRequirement> Requirements,
    TimeSpan? LeaseDuration);
