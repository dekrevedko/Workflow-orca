using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class StructuredSagaOwnershipTests
{
    [Fact]
    public void ForwardCompletion_PersistsFiberScopeInstructionAndOrderingIdentity()
    {
        var ownerFiberId = new FiberId("fiber:reserve");
        var ownerScopeId = new ScopeId("scope:branch-a");
        var state = DurableSagaState.Empty();

        state.Apply(ForwardCompleted(
            ownerFiberId,
            ownerScopeId,
            instructionId: "instruction:reserve",
            committedSequence: 7,
            canonicalBranchOrder: 1,
            canonicalInstructionOrder: 2));

        var action = state.CompletedForwardActions.Should().ContainSingle().Subject;
        action.FiberId.Should().Be(ownerFiberId);
        action.OwningScopeId.Should().Be(ownerScopeId);
        action.EligibleScopeId.Should().Be(ownerScopeId);
        action.InstructionId.Should().Be("instruction:reserve");
        action.CommittedSequence.Should().Be(7);
        action.CanonicalBranchOrder.Should().Be(1);
        action.CanonicalInstructionOrder.Should().Be(2);

        var restored = DurableSagaState.FromSnapshot(
            state.CreateCheckpointForwardActions().Select(DurableSagaForwardAction.FromCheckpoint),
            [],
            [],
            []);

        restored.CompletedForwardActions.Should().BeEquivalentTo(state.CompletedForwardActions);
    }

    [Fact]
    public void ScopeTransfer_ChangesEligibilityWithoutChangingStableActionIdentity()
    {
        var childScopeId = new ScopeId("scope:child");
        var parentScopeId = new ScopeId("scope:parent");
        var state = DurableSagaState.Empty();
        state.Apply(ForwardCompleted(
            new FiberId("fiber:child"),
            childScopeId,
            "instruction:charge",
            committedSequence: 3,
            canonicalBranchOrder: 0,
            canonicalInstructionOrder: 1));
        var before = state.CompletedForwardActions.Single();

        state.Apply(new SagaForwardActionsTransferredEvent
        {
            EventId = EventId.New(),
            InstanceId = InstanceId.New(),
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(4),
            FromExecutionScopeId = childScopeId,
            ToExecutionScopeId = parentScopeId
        });

        var after = state.CompletedForwardActions.Should().ContainSingle().Subject;
        after.Should().Be(before with { EligibleScopeId = parentScopeId });
        after.FiberId.Should().Be(before.FiberId);
        after.OwningScopeId.Should().Be(childScopeId);
        after.InstructionId.Should().Be(before.InstructionId);
        after.CommittedSequence.Should().Be(before.CommittedSequence);
    }

    [Fact]
    public void PlanCompensation_CoversDescendantsAndIgnoresWallClockCompletionOrder()
    {
        var outerScopeId = new ScopeId("scope:outer");
        var branchAScopeId = new ScopeId("scope:branch-a");
        var branchBScopeId = new ScopeId("scope:branch-b");
        var first = StateWithSiblingActions(branchAScopeId, branchBScopeId, reverseCompletionTimes: false);
        var second = StateWithSiblingActions(branchAScopeId, branchBScopeId, reverseCompletionTimes: true);

        var firstPlan = first.PlanCompensation(
            Context(),
            "checkout",
            "scope failure",
            [outerScopeId, branchAScopeId, branchBScopeId]);
        var secondPlan = second.PlanCompensation(
            Context(),
            "checkout",
            "scope failure",
            [outerScopeId, branchAScopeId, branchBScopeId]);

        firstPlan.OfType<SagaCompensationStartedEvent>().Select(item => item.ActionKey)
            .Should().Equal("undo-branch-b", "undo-branch-a-second", "undo-branch-a-first");
        secondPlan.OfType<SagaCompensationStartedEvent>().Select(item => item.ActionKey)
            .Should().Equal("undo-branch-b", "undo-branch-a-second", "undo-branch-a-first");
    }

    [Fact]
    public void PlanCompensation_HonorsPlanBoundScopeOrderingOverride()
    {
        var scopeId = new ScopeId("scope:override");
        var state = DurableSagaState.FromSnapshot(
            [
                Action("first", "undo-first", scopeId, branchOrder: 0, instructionOrder: 0) with
                {
                    ScopeOrderOverride = 2
                },
                Action("second", "undo-second", scopeId, branchOrder: 1, instructionOrder: 0) with
                {
                    ScopeOrderOverride = 1
                }
            ],
            [],
            [],
            []);

        var plan = state.PlanCompensation(Context(), "checkout", "override", [scopeId]);

        plan.OfType<SagaCompensationStartedEvent>().Select(item => item.ActionKey)
            .Should().Equal("undo-first", "undo-second");
    }

    [Fact]
    public void StepFailure_PlansCompensationForCommittedActionsBeforeTerminalFact()
    {
        var ownerFiberId = new FiberId("fiber:failed-branch");
        var ownerScopeId = new ScopeId("scope:failed-branch");
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                ForwardCompleted(
                    ownerFiberId,
                    ownerScopeId,
                    "instruction:reserve",
                    committedSequence: 1,
                    canonicalBranchOrder: 0,
                    canonicalInstructionOrder: 0)
            ]);

        var decision = aggregate.DecideStepFailed(new DurableStepFailedCommand(
            CommandId.New(),
            aggregate.InstanceId,
            Timestamp(5),
            "branch/fail",
            "boom")
        {
            TerminalFiberIds = [ownerFiberId]
        });

        decision.Events.OfType<SagaCompensationRequestedEvent>()
            .Should().ContainSingle().Which.ScopeId.Should().Be("checkout");
        decision.Events.OfType<SagaCompensationStartedEvent>()
            .Should().ContainSingle().Which.ActionKey.Should().Be("release");
        var events = decision.Events.ToList();
        events.FindIndex(item => item is SagaCompensationStartedEvent)
            .Should().BeLessThan(events.FindIndex(item => item is WorkflowTerminalEvent));
    }

    private static DurableSagaState StateWithSiblingActions(
        ScopeId branchAScopeId,
        ScopeId branchBScopeId,
        bool reverseCompletionTimes)
    {
        return DurableSagaState.FromSnapshot(
            [
                Action("branch-a-first", "undo-branch-a-first", branchAScopeId, 0, 0) with
                {
                    CompletedAt = Timestamp(reverseCompletionTimes ? 5 : 1),
                    CommittedSequence = 1
                },
                Action("branch-b", "undo-branch-b", branchBScopeId, 1, 0) with
                {
                    CompletedAt = Timestamp(reverseCompletionTimes ? 1 : 5),
                    CommittedSequence = 2
                },
                Action("branch-a-second", "undo-branch-a-second", branchAScopeId, 0, 1) with
                {
                    CompletedAt = Timestamp(3),
                    CommittedSequence = 3
                }
            ],
            [],
            [],
            []);
    }

    private static DurableSagaForwardAction Action(
        string actionKey,
        string compensationKey,
        ScopeId scopeId,
        int branchOrder,
        int instructionOrder)
    {
        return new DurableSagaForwardAction("checkout", actionKey, compensationKey, Timestamp(1))
        {
            FiberId = new FiberId($"fiber:{actionKey}"),
            OwningScopeId = scopeId,
            EligibleScopeId = scopeId,
            InstructionId = $"instruction:{actionKey}",
            CommittedSequence = 1,
            CanonicalBranchOrder = branchOrder,
            CanonicalInstructionOrder = instructionOrder
        };
    }

    private static SagaForwardActionCompletedEvent ForwardCompleted(
        FiberId fiberId,
        ScopeId scopeId,
        string instructionId,
        long committedSequence,
        int canonicalBranchOrder,
        int canonicalInstructionOrder)
    {
        return new SagaForwardActionCompletedEvent
        {
            EventId = EventId.New(),
            InstanceId = InstanceId.New(),
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(1),
            ScopeId = "checkout",
            ActionKey = "reserve",
            CompensationKey = "release",
            FiberId = fiberId,
            OwningScopeId = scopeId,
            InstructionId = instructionId,
            CommittedSequence = committedSequence,
            CanonicalBranchOrder = canonicalBranchOrder,
            CanonicalInstructionOrder = canonicalInstructionOrder
        };
    }

    private static WorkflowStartedEvent Started()
    {
        var instanceId = InstanceId.New();
        return new WorkflowStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(0),
            DefinitionId = DefinitionId.New(),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DurableSagaEventContext Context()
    {
        var instanceId = InstanceId.New();
        return new DurableSagaEventContext(
            CommandId.New(),
            instanceId,
            Timestamp(10),
            ParentInstanceId: null,
            instanceId);
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 13, 12, 0, seconds, TimeSpan.Zero);
    }
}
