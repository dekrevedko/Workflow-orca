using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Engine.Durable.Driver;

internal static class DurableFiberEnvelopeValidator
{
    internal static DurableFiberEnvelopeValidation Validate(
        DurableExecutionEnvelopeV2 envelope,
        CompiledWorkflowPlan plan,
        InstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(plan);
        if (envelope.EnvelopeVersion != DurableExecutionEnvelopeV2.CurrentVersion)
        {
            return Invalid(
                "SFE-BIND-001",
                $"Envelope format '{envelope.EnvelopeVersion}' is not supported; expected " +
                $"'{DurableExecutionEnvelopeV2.CurrentVersion}'.");
        }

        if (envelope.PlanBinding.CompilerFormatVersion != plan.FormatVersion)
        {
            return Invalid(
                "SFE-BIND-002",
                $"Compiler format '{envelope.PlanBinding.CompilerFormatVersion}' does not match " +
                $"registered format '{plan.FormatVersion}'.");
        }

        if (envelope.InstanceId != instanceId ||
            envelope.PlanBinding.DefinitionId != plan.DefinitionId ||
            envelope.PlanBinding.DefinitionVersion != plan.DefinitionVersion)
        {
            return Invalid(
                "SFE-BIND-003",
                "Envelope instance or definition/version binding does not match the registered execution plan.");
        }

        if (!string.Equals(
                envelope.PlanBinding.PlanFingerprint,
                plan.Fingerprint,
                StringComparison.Ordinal))
        {
            return Invalid(
                "SFE-BIND-004",
                $"Envelope plan fingerprint '{envelope.PlanBinding.PlanFingerprint}' does not match " +
                $"registered fingerprint '{plan.Fingerprint}'.");
        }

        return new DurableFiberEnvelopeValidation(true, null, null);
    }

    private static DurableFiberEnvelopeValidation Invalid(string code, string diagnostic)
    {
        return new DurableFiberEnvelopeValidation(false, code, diagnostic);
    }
}

internal sealed record DurableFiberEnvelopeValidation(
    bool IsValid,
    string? Code,
    string? Diagnostic);
