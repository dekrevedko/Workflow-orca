using System.Reflection;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.TestSupport;

/// <summary>
/// Keeps legacy-engine regression scenarios able to create removed control intents without
/// re-exposing those intents through the application contract.
/// </summary>
public static class LegacyStepResults
{
    public static StepResult Yield() => Create("EngineYieldStepResult");

    public static StepResult ContinueAsNew<TState>(TState state) =>
        CreateGeneric("EngineContinueAsNewStepResult`1", typeof(TState), state);

    public static StepResult RunExternalJob(
        string externalJobId,
        byte[] payload,
        IReadOnlyList<ResourcePoolRequirement>? requirements = null,
        TimeSpan? timeout = null) =>
        Create("EngineExternalJobStepResult", externalJobId, payload, requirements, timeout);

    public static StepResult AcquireResources(
        string holderKey,
        IReadOnlyList<ResourcePoolRequirement> requirements,
        TimeSpan? leaseDuration = null) =>
        Create("EngineAcquireResourcesStepResult", holderKey, requirements, leaseDuration);

    private static StepResult Create(string name, params object?[] arguments)
    {
        var type = typeof(StepResult).Assembly.GetType($"OrcaCore.{name}") ??
            throw new InvalidOperationException($"Legacy result '{name}' no longer exists in the runtime bridge.");
        return (StepResult)(Activator.CreateInstance(type, arguments) ??
            throw new InvalidOperationException($"Legacy result '{name}' could not be created."));
    }

    private static StepResult CreateGeneric(
        string name,
        Type argument,
        params object?[] arguments)
    {
        var openType = typeof(StepResult).Assembly.GetType($"OrcaCore.{name}") ??
            throw new InvalidOperationException($"Legacy result '{name}' no longer exists in the runtime bridge.");
        return (StepResult)(Activator.CreateInstance(openType.MakeGenericType(argument), arguments) ??
            throw new InvalidOperationException($"Legacy result '{name}' could not be created."));
    }
}
