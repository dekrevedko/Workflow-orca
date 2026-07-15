using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Driver;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableFiberEnvelopeMapperTests
{
    [Fact]
    public void BindingValidation_ReportsEachIncompatibleEnvelopeDimension()
    {
        var plan = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .End()
            .Build()
            .CompiledPlan;
        var instanceId = InstanceId.New();
        var state = StructuredExecutionState.Create(instanceId, 0, plan.Instructions[0].Id);
        var envelope = DurableFiberEnvelopeMapper.ToEnvelope(
            state,
            plan,
            new SerializedPayload("application/json", [1]));

        DurableFiberEnvelopeValidator.Validate(
                envelope with { EnvelopeVersion = 99 },
                plan,
                instanceId)
            .Code.Should().Be("SFE-BIND-001");
        DurableFiberEnvelopeValidator.Validate(
                envelope with
                {
                    PlanBinding = envelope.PlanBinding with { CompilerFormatVersion = 99 }
                },
                plan,
                instanceId)
            .Code.Should().Be("SFE-BIND-002");
        DurableFiberEnvelopeValidator.Validate(
                envelope with
                {
                    PlanBinding = envelope.PlanBinding with { DefinitionId = DefinitionId.New() }
                },
                plan,
                instanceId)
            .Code.Should().Be("SFE-BIND-003");
        DurableFiberEnvelopeValidator.Validate(
                envelope with
                {
                    PlanBinding = envelope.PlanBinding with { PlanFingerprint = "drift" }
                },
                plan,
                instanceId)
            .Code.Should().Be("SFE-BIND-004");
        DurableFiberEnvelopeValidator.Validate(envelope, plan, instanceId)
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_PreservesNestedFibersScopesSchedulerResultsRetryAndYield()
    {
        var plan = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .End()
            .Build()
            .CompiledPlan;
        var instanceId = InstanceId.New();
        var rootId = new FiberId("root");
        var childId = new FiberId("child");
        var siblingId = new FiberId("sibling");
        var grandchildId = new FiberId("grandchild");
        var outerScopeId = new ScopeId("outer");
        var innerScopeId = new ScopeId("inner");
        var root = new FiberRecord(
            rootId,
            null,
            new InstructionId("root/1"),
            FiberPhase.Blocked,
            4,
            7,
            [1],
            null,
            new FiberBlock(FiberBlockedReason.Scope, outerScopeId.Value),
            null,
            null);
        var child = new FiberRecord(
            childId,
            outerScopeId,
            new InstructionId("branch/2"),
            FiberPhase.Blocked,
            2,
            3,
            [2],
            null,
            new FiberBlock(FiberBlockedReason.Scope, innerScopeId.Value),
            null,
            null);
        var sibling = new FiberRecord(
            siblingId,
            outerScopeId,
            new InstructionId("branch/3"),
            FiberPhase.Runnable,
            0,
            0,
            [3],
            [4],
            null,
            null,
            null)
        {
            YieldCount = 5,
            ForcedRotationCount = 2,
            RetryAttempt = 3,
            RetryNotBefore = DateTimeOffset.Parse("2026-07-13T11:00:00Z"),
            LogicalOperationKey = "retry-op",
            TimeoutDeadline = DateTimeOffset.Parse("2026-07-13T11:05:00Z"),
            ResumeFromWaitId = "wait-7"
        };
        var grandchild = new FiberRecord(
            grandchildId,
            innerScopeId,
            new InstructionId("nested/1"),
            FiberPhase.Blocked,
            0,
            0,
            [5],
            null,
            new FiberBlock(FiberBlockedReason.Wait, "wait-7"),
            null,
            null);
        var state = new StructuredExecutionState(
            instanceId,
            2,
            rootId,
            new FiberSchedulerState([siblingId], siblingId),
            new Dictionary<FiberId, FiberRecord>
            {
                [rootId] = root,
                [childId] = child,
                [siblingId] = sibling,
                [grandchildId] = grandchild
            },
            new Dictionary<ScopeId, ExecutionScopeRecord>
            {
                [outerScopeId] = new ExecutionScopeRecord(
                    outerScopeId,
                    new ScopePlanId("outer-plan"),
                    6,
                    null,
                    rootId,
                    CompiledScopeKind.WhenAll,
                    ExecutionScopePhase.Merging,
                    [childId, siblingId],
                    null,
                    new Dictionary<FiberId, byte[]?> { [siblingId] = [4] }),
                [innerScopeId] = new ExecutionScopeRecord(
                    innerScopeId,
                    new ScopePlanId("inner-plan"),
                    2,
                    outerScopeId,
                    childId,
                    CompiledScopeKind.WhenFirst,
                    ExecutionScopePhase.Running,
                    [grandchildId],
                    grandchildId,
                    new Dictionary<FiberId, byte[]?>())
            });

        var envelope = DurableFiberEnvelopeMapper.ToEnvelope(
            state,
            plan,
            new SerializedPayload("application/json", [9, 8]),
            [
                new DurableOwnedObligationState
                {
                    Kind = DurableOwnedObligationKind.Wait,
                    ObligationId = "wait-7",
                    FiberId = grandchildId.Value,
                    ScopeId = innerScopeId.Value,
                    RegistrationSequence = 11
                }
            ]);
        var rehydrated = DurableFiberEnvelopeMapper.FromEnvelope(envelope);

        envelope.EnvelopeVersion.Should().Be(2);
        envelope.PlanBinding.PlanFingerprint.Should().Be(plan.Fingerprint);
        envelope.OwnedObligations.Single().FiberId.Should().Be(grandchildId.Value);
        rehydrated.Should().BeEquivalentTo(state);
        rehydrated.Scheduler.RunnableFiberIds.Should().Equal(state.Scheduler.RunnableFiberIds);
        rehydrated.Scopes[outerScopeId].ChildFiberIds
            .Should().Equal(state.Scopes[outerScopeId].ChildFiberIds);
    }

    private sealed class TestState;
}
