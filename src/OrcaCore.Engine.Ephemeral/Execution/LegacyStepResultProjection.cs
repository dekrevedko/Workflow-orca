namespace OrcaCore.Engine.Ephemeral.Execution;

internal static class LegacyStepResultProjection
{
    internal static bool IsYield(StepResult result) =>
        string.Equals(result.GetType().Name, "EngineYieldStepResult", StringComparison.Ordinal) &&
        result.GetType().Assembly == typeof(StepResult).Assembly;
}
