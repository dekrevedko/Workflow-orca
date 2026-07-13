using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Recovery;

public sealed class DurableRecoveryTests
{
    [Fact]
    [Trait("AC", "AC-301")]
    public async Task WaitingInstance_RehydrateAfterRestart_RemainsResumable()
    {
        var instanceId = InstanceIdValue(1);
        var waitId = WaitIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(WaitRegisteredCommand(instanceId, waitId, 2), TestContext.Current.CancellationToken);

        var restarted = new DurableCommandProcessor(store);
        var result = await restarted.ProcessAsync(
            WaitMatchedCommand(instanceId, waitId, 3),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-302")]
    public async Task CrashBeforeCommit_RehydratesLastCommittedStateOnly()
    {
        var instanceId = InstanceIdValue(1);
        var store = new FakeWorkflowEventStore();
        await store.AppendAsync(new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = StreamVersion.Empty,
            Events = [Started(instanceId)]
        }, TestContext.Current.CancellationToken);
        store.FailNextCommitBeforeApply();
        var processor = new DurableCommandProcessor(store);

        var failed = await processor.ProcessAsync(
            StepCompletedCommand(instanceId, 2),
            TestContext.Current.CancellationToken);
        var recovered = await new DurableCommandProcessor(store).ProcessAsync(
            StepCompletedCommand(instanceId, 3),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        failed.Outcome.Should().Be(DurableCommandOutcome.Conflict);
        recovered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        events.OfType<WorkflowStepCompletedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-316")]
    public async Task HostKilledBeforeShutdownHook_LosesNoCommittedTransition()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(StepCompletedCommand(instanceId, 2), TestContext.Current.CancellationToken);

        var restarted = new DurableCommandProcessor(store);
        var result = await restarted.ProcessAsync(
            StepCompletedCommand(instanceId, 3, "root/2"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        events.OfType<WorkflowStepCompletedEvent>().Select(workflowEvent => workflowEvent.StepPath)
            .Should().Equal("root/1", "root/2");
    }

    [Fact]
    public async Task CheckpointPlusTail_RehydratesWithoutGenesisReplay()
    {
        var instanceId = InstanceIdValue(1);
        var waitId = WaitIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(StepCompletedCommand(instanceId, 2), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(WaitRegisteredCommand(instanceId, waitId, 3), TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(instanceId, TestContext.Current.CancellationToken);

        var result = await new DurableCommandProcessor(store).ProcessAsync(
            WaitMatchedCommand(instanceId, waitId, 4),
            TestContext.Current.CancellationToken);

        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.StreamVersion.Should().Be(new StreamVersion(2));
        checkpoint.Value.Status.Should().Be(WorkflowStatus.Running);
        checkpoint.Value.LastStepPath.Should().Be("root/1");
        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
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
            TestEnvelopes.Envelope("application/octet-stream", [(byte)commandValue]));
    }

    private static DurableWaitRegisteredCommand WaitRegisteredCommand(
        InstanceId instanceId,
        WaitId waitId,
        int commandValue)
    {
        return new DurableWaitRegisteredCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            waitId,
            "Approved",
            new CorrelationId("order-1"));
    }

    private static DurableWaitMatchedCommand WaitMatchedCommand(
        InstanceId instanceId,
        WaitId waitId,
        int commandValue)
    {
        return new DurableWaitMatchedCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            waitId,
            EventIdValue(commandValue + 100));
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
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

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
