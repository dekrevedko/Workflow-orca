using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Durable.Tests;

/// <summary>
/// Builds minimal execution-position envelopes for kernel-level tests that issue driver
/// commands by hand.
/// </summary>
internal static class TestEnvelopes
{
    internal static DurableExecutionEnvelopeV2 Envelope(
        string stateContentType = "application/json",
        byte[]? statePayload = null,
        int rootIndex = 1,
        InstanceId? instanceId = null)
    {
        var resolvedInstanceId = instanceId ?? new InstanceId(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        return new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = resolvedInstanceId,
            ContinueAsNewGeneration = 0,
            RootFiberId = "root",
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = new DefinitionId(
                    Guid.Parse("00000000-0000-0000-0000-000000000001")),
                DefinitionVersion = DefinitionVersion.Initial,
                CompilerFormatVersion = 1,
                PlanFingerprint = "test-plan"
            },
            StateContentType = stateContentType,
            StatePayload = statePayload ?? [1, 2, 3],
            Fibers =
            [
                new DurableFiberState
                {
                    FiberId = "root",
                    InstructionId = $"root/{rootIndex}",
                    Phase = DurableFiberPhase.Runnable,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 0
                }
            ],
            Scopes = [],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = ["root"],
                NextFiberId = "root"
            }
        };
    }
}
