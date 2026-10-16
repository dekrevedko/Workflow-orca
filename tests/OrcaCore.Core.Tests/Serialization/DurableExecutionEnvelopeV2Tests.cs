using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Core.Tests.Serialization;

public sealed class DurableExecutionEnvelopeV2Tests
{
    [Fact]
    public void Format2_RoundTripsCompleteStructuredExecutionState()
    {
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var envelope = new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = instanceId,
            ContinueAsNewGeneration = 3,
            RootFiberId = "root",
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = DefinitionId.New(),
                DefinitionVersion = new DefinitionVersion(7),
                CompilerFormatVersion = 2,
                CompilerProfileId = "orcacore-compiler-v2;quantum=1024",
                PlanFingerprint = "FINGERPRINT"
            },
            StateContentType = "application/json",
            StatePayload = [1, 2, 3],
            Fibers =
            [
                new DurableFiberState
                {
                    FiberId = "root",
                    InstructionId = "root/4",
                    Phase = DurableFiberPhase.Blocked,
                    LoopIteration = 5,
                    NextScopeEntrySequence = 9,
                    LocalStatePayload = [4],
                    ResultPayload = [5],
                    Blocked = new DurableFiberBlock
                    {
                        Reason = DurableFiberBlockedReason.Wait,
                        ObligationId = "wait-1"
                    },
                    QuantumRotationCount = 2,
                    ForcedRotationCount = 1,
                    RetryAttempt = 3,
                    RetryNotBefore = DateTimeOffset.Parse("2026-07-13T10:00:00Z"),
                    LogicalOperationKey = "operation-1"
                },
                new DurableFiberState
                {
                    FiberId = "child",
                    OwningScopeId = "scope-1",
                    InstructionId = "branch/2",
                    Phase = DurableFiberPhase.Completed,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 0,
                    ResultPayload = [8]
                }
            ],
            Scopes =
            [
                new DurableExecutionScopeState
                {
                    ScopeId = "scope-1",
                    ScopePlanId = "scope:root/3",
                    ScopeEntrySequence = 8,
                    ParentFiberId = "root",
                    Kind = DurableExecutionScopeKind.WhenAll,
                    Phase = DurableExecutionScopePhase.Joinable,
                    ChildFiberIds = ["child"],
                    CommittedResults =
                    [
                        new DurableCommittedResult
                        {
                            FiberId = "child",
                            Payload = [8]
                        }
                    ]
                }
            ],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = ["child", "root"],
                NextFiberId = "child"
            },
            OwnedObligations =
            [
                new DurableOwnedObligationState
                {
                    Kind = DurableOwnedObligationKind.Wait,
                    ObligationId = "wait-1",
                    FiberId = "root",
                    ScopeId = "scope-1",
                    RegistrationSequence = 12
                }
            ],
            Diagnostics = new DurableExecutionDiagnostics
            {
                TotalQuantumRotations = 2,
                ForcedRotations = 1
            }
        };

        var roundTripped = DurableExecutionEnvelopeV2.Deserialize(envelope.Serialize());

        roundTripped.EnvelopeVersion.Should().Be(2);
        roundTripped.InstanceId.Should().Be(instanceId);
        roundTripped.ContinueAsNewGeneration.Should().Be(3);
        roundTripped.RootFiberId.Should().Be("root");
        roundTripped.PlanBinding.Should().Be(envelope.PlanBinding);
        roundTripped.StatePayload.Should().Equal(1, 2, 3);
        roundTripped.Fibers.Should().HaveCount(2);
        roundTripped.Fibers[0].Blocked!.ObligationId.Should().Be("wait-1");
        roundTripped.Fibers[0].RetryAttempt.Should().Be(3);
        roundTripped.Scopes.Single().CommittedResults.Single().Payload.Should().Equal(8);
        roundTripped.Scheduler.NextFiberId.Should().Be("child");
        roundTripped.OwnedObligations.Single().RegistrationSequence.Should().Be(12);
        roundTripped.Diagnostics.ForcedRotations.Should().Be(1);
    }
}
