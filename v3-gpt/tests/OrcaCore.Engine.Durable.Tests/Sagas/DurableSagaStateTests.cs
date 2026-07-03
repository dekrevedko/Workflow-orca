using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class DurableSagaStateTests
{
    [Fact]
    public void PlanCompensation_IgnoresForwardActionsWithoutCompensationKey()
    {
        var state = DurableSagaState.FromSnapshot(
            [
                new DurableSagaForwardAction("checkout", "reserve", "release", Timestamp(1)),
                new DurableSagaForwardAction("checkout", "notify", string.Empty, Timestamp(2))
            ],
            [],
            [],
            []);

        var events = state.PlanCompensation(
            SagaEventContext(CommandIdValue(3), Timestamp(3)),
            "checkout",
            "failure");

        events.OfType<SagaCompensationRequestedEvent>().Should().ContainSingle();
        events.OfType<SagaCompensationStartedEvent>().Should().ContainSingle()
            .Which.ActionKey.Should().Be("release");
    }

    [Fact]
    public void ApplyForwardCompleted_ReplacesExistingActionForSameScopeAndKey()
    {
        var state = DurableSagaState.FromSnapshot(
            [new DurableSagaForwardAction("checkout", "reserve", "old-release", Timestamp(1))],
            [],
            [],
            []);

        state.Apply(new SagaForwardActionCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            ScopeId = "checkout",
            ActionKey = "reserve",
            CompensationKey = "new-release"
        });

        state.CompletedForwardActions.Should().ContainSingle().Which.Should().Be(
            new DurableSagaForwardAction("checkout", "reserve", "new-release", Timestamp(2)));
    }

    [Fact]
    public void CreateAuditScopes_OrdersScopesAndActionsDeterministically()
    {
        var state = DurableSagaState.FromSnapshot(
            [
                new DurableSagaForwardAction("z", "b", "undo-b", Timestamp(2)),
                new DurableSagaForwardAction("z", "a", "undo-a", Timestamp(2)),
                new DurableSagaForwardAction("a", "first", "undo-first", Timestamp(1))
            ],
            [
                new DurableSagaCompensationAction(
                    "z",
                    "undo-b",
                    1,
                    Timestamp(3),
                    Timestamp(5),
                    null,
                    null,
                    SagaCompensationActionStatus.Completed),
                new DurableSagaCompensationAction(
                    "z",
                    "undo-a",
                    0,
                    Timestamp(3),
                    Timestamp(4),
                    null,
                    null,
                    SagaCompensationActionStatus.Completed)
            ],
            [],
            ["z"]);

        var audits = state.CreateAuditScopes(WorkflowStatus.Compensated);

        audits.Select(audit => audit.ScopeId).Should().Equal("a", "z");
        audits.Single(audit => audit.ScopeId == "z").ForwardActions
            .Select(action => action.ActionKey)
            .Should().Equal("a", "b");
        audits.Single(audit => audit.ScopeId == "z").CompensationActions
            .Select(action => action.ActionKey)
            .Should().Equal("undo-a", "undo-b");
        audits.Single(audit => audit.ScopeId == "z").Outcome.Should().Be(WorkflowStatus.Compensated);
    }

    private static DurableSagaEventContext SagaEventContext(CommandId commandId, DateTimeOffset requestedAt)
    {
        return new DurableSagaEventContext(
            commandId,
            InstanceIdValue(1),
            requestedAt,
            ParentInstanceId: null,
            InstanceIdValue(1));
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 12, 0, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
