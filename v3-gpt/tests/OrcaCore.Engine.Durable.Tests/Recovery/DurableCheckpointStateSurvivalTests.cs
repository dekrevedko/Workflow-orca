using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Recovery;

/// <summary>
/// Regression tests for the R14 finding: checkpoints written by step completion truncate tail
/// replay, so any aggregate state missing from the checkpoint schema is silently lost — the
/// aggregate reloads from checkpoint+tail on every command, no restart required.
/// </summary>
public sealed class DurableCheckpointStateSurvivalTests
{
    [Fact]
    public async Task SagaForwardAction_SurvivesStepCheckpoint_CompensationStillPlanned()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            ForwardActionCompleted(instanceId, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            StepCompletedCommand(instanceId, 3),
            TestContext.Current.CancellationToken);

        var result = await processor.ProcessAsync(new RequestSagaCompensationCommand
        {
            CommandId = CommandIdValue(4),
            InstanceId = instanceId,
            RequestedAt = Timestamp(4),
            ScopeId = "scope-1",
            Reason = "business failure"
        }, TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        events.OfType<SagaCompensationStartedEvent>()
            .Should().ContainSingle(e => e.ActionKey == "release-stock",
                "the forward action completed before the checkpoint must still be compensated");
    }

    [Fact]
    public async Task SagaForwardActionRecord_AfterStepCheckpoint_IsStillDeduplicated()
    {
        var instanceId = InstanceIdValue(2);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            ForwardActionCompleted(instanceId, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            StepCompletedCommand(instanceId, 3),
            TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            ForwardActionCompleted(instanceId, 5),
            TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<SagaForwardActionCompletedEvent>()
            .Should().ContainSingle("re-delivered forward-action records must be deduplicated across checkpoints");
    }

    [Fact]
    public async Task RequestedCompensationScope_SurvivesStepCheckpoint_SecondRequestIsNoOp()
    {
        var instanceId = InstanceIdValue(3);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            ForwardActionCompleted(instanceId, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(new RequestSagaCompensationCommand
        {
            CommandId = CommandIdValue(3),
            InstanceId = instanceId,
            RequestedAt = Timestamp(3),
            ScopeId = "scope-1",
            Reason = "first"
        }, TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            StepCompletedCommand(instanceId, 4),
            TestContext.Current.CancellationToken);

        await processor.ProcessAsync(new RequestSagaCompensationCommand
        {
            CommandId = CommandIdValue(5),
            InstanceId = instanceId,
            RequestedAt = Timestamp(5),
            ScopeId = "scope-1",
            Reason = "duplicate"
        }, TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<SagaCompensationRequestedEvent>()
            .Should().ContainSingle("compensation-requested scopes must stay deduplicated across checkpoints");
    }

    [Fact]
    public async Task ParentResumeToken_SurvivesStepCheckpoint_ConsumedExactlyOnce()
    {
        var instanceId = InstanceIdValue(4);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);

        var runChildrenCommandId = CommandIdValue(2);
        await processor.ProcessAsync(
            new DurableRunChildrenCommand(
                runChildrenCommandId,
                instanceId,
                Timestamp(2),
                DefinitionIdValue(2),
                DefinitionVersion.Initial,
                ["""{"id":1}"""],
                RunChildFailurePolicy.ContinueParent),
            TestContext.Current.CancellationToken);
        var childInstanceId = DurableChildWorkflowState.DeterministicChildId(instanceId, runChildrenCommandId, 0);
        await processor.ProcessAsync(
            new DurableChildCompletedCommand(
                CommandIdValue(3),
                instanceId,
                Timestamp(3),
                childInstanceId,
                WorkflowStatus.Completed,
                null),
            TestContext.Current.CancellationToken);

        var groupId = runChildrenCommandId.Value.ToString("D");
        var recordedToken = (await LoadEventsAsync(store, instanceId))
            .OfType<WorkflowParentResumeTokenRecordedEvent>()
            .Single();

        // A later step checkpoint compacts the stream below the token-recorded event.
        await processor.ProcessAsync(
            StepCompletedCommand(instanceId, 4),
            TestContext.Current.CancellationToken);

        var firstConsume = await processor.ProcessAsync(new ConsumeParentResumeTokenCommand
        {
            CommandId = CommandIdValue(5),
            InstanceId = instanceId,
            RequestedAt = Timestamp(5),
            GroupId = groupId,
            ResumeTokenId = recordedToken.ResumeTokenId
        }, TestContext.Current.CancellationToken);
        await processor.ProcessAsync(new ConsumeParentResumeTokenCommand
        {
            CommandId = CommandIdValue(6),
            InstanceId = instanceId,
            RequestedAt = Timestamp(6),
            GroupId = groupId,
            ResumeTokenId = recordedToken.ResumeTokenId
        }, TestContext.Current.CancellationToken);

        var events = await LoadEventsAsync(store, instanceId);

        firstConsume.Outcome.Should().Be(
            DurableCommandOutcome.Committed,
            "a resume token recorded before the checkpoint must still be consumable");
        events.OfType<WorkflowParentResumeTokenConsumedEvent>()
            .Should().ContainSingle("resume tokens must be consumed exactly once across checkpoints");
    }

    private static async Task<IReadOnlyList<WorkflowEvent>> LoadEventsAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId)
    {
        return await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
    }

    private static RecordSagaForwardActionCompletedCommand ForwardActionCompleted(
        InstanceId instanceId,
        int commandValue)
    {
        return new RecordSagaForwardActionCompletedCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = instanceId,
            RequestedAt = Timestamp(commandValue),
            ScopeId = "scope-1",
            ActionKey = "reserve-stock",
            CompensationKey = "release-stock"
        };
    }

    private static StartWorkflowCommand StartCommand(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DurableStepCompletedCommand StepCompletedCommand(
        InstanceId instanceId,
        int commandValue,
        string stepPath = "root/1")
    {
        return new DurableStepCompletedCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            stepPath,
            "application/octet-stream",
            [(byte)commandValue]);
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 5, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return new Guid(value, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
    }
}
