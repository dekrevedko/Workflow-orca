using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Building;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableWaitTests
{
    [Fact]
    [Trait("AC", "AC-303")]
    public async Task WaitLong_DurableSurfaceRegistersColdWait()
    {
        var wait = new DurableWorkflowBuilder<TestState>()
            .WaitLong("Approved", state => new CorrelationId(state.CorrelationId))
            .Waits
            .Should().ContainSingle().Subject;
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(InstanceIdValue(1)), TestContext.Current.CancellationToken);

        var result = await processor.ProcessAsync(
            WaitRegisteredCommand(InstanceIdValue(1), WaitIdValue(1), 2, wait.Mode),
            TestContext.Current.CancellationToken);

        wait.Mode.Should().Be(WaitMode.Cold);
        result.Evicted.Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "AC-304")]
    public async Task WaitLong_AfterCommit_EvictsAndLazyResumes()
    {
        var instanceId = InstanceIdValue(1);
        var waitId = WaitIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);

        var wait = await processor.ProcessAsync(
            WaitRegisteredCommand(instanceId, waitId, 2, WaitMode.Cold),
            TestContext.Current.CancellationToken);
        var resumed = await new DurableCommandProcessor(store).ProcessAsync(
            WaitMatchedCommand(instanceId, waitId, 3),
            TestContext.Current.CancellationToken);

        wait.Evicted.Should().BeTrue();
        resumed.Outcome.Should().Be(DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait("AC", "AC-504")]
    public async Task IdleWaitingInstance_EvictsSafelyAndResumes()
    {
        var instanceId = InstanceIdValue(1);
        var waitId = WaitIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            WaitRegisteredCommand(instanceId, waitId, 2, WaitMode.Resident),
            TestContext.Current.CancellationToken);

        var evicted = await processor.EvictIdleAsync(instanceId, TestContext.Current.CancellationToken);
        var resumed = await new DurableCommandProcessor(store).ProcessAsync(
            WaitMatchedCommand(instanceId, waitId, 3),
            TestContext.Current.CancellationToken);

        evicted.Outcome.Should().Be(DurableCommandOutcome.Evicted);
        resumed.Outcome.Should().Be(DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait("AC", "AC-505")]
    public async Task TerminalInstance_EvictsAfterTerminalHandlingAndRemainsQueryable()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);

        var completed = await processor.ProcessAsync(
            new DurableCompleteCommand(CommandIdValue(2), instanceId, Timestamp(2), "Done"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        completed.Evicted.Should().BeTrue();
        events.OfType<WorkflowCompletedEvent>().Should().ContainSingle();
        events.OfType<WorkflowTerminalEvent>().Single().Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait("AC", "AC-506")]
    public async Task ConcurrentEvictAndResume_ProducesSingleMutator()
    {
        var instanceId = InstanceIdValue(1);
        var waitId = WaitIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            WaitRegisteredCommand(instanceId, waitId, 2, WaitMode.Resident),
            TestContext.Current.CancellationToken);

        var evict = processor.EvictIdleAsync(instanceId, TestContext.Current.CancellationToken);
        var resume = processor.ProcessAsync(
            WaitMatchedCommand(instanceId, waitId, 3),
            TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(evict, resume).WaitAsync(TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        results.Should().Contain(result => result.Outcome == DurableCommandOutcome.Committed);
        results.Should().Contain(result => result.Outcome == DurableCommandOutcome.Evicted);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-110")]
    public async Task BranchScopedWaits_RequireMatchingBranchId()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            WaitRegisteredCommand(instanceId, WaitIdValue(1), 2, WaitMode.Resident, "0:a"),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            WaitRegisteredCommand(instanceId, WaitIdValue(2), 3, WaitMode.Resident, "1:b"),
            TestContext.Current.CancellationToken);

        await processor.ProcessAsync(Deliver(instanceId, 4, null), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(Deliver(instanceId, 5, "0:a"), TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<WorkflowDeliveryBufferedEvent>().Should().ContainSingle()
            .Which.BranchId.Should().BeNull();
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle()
            .Which.WaitId.Should().Be(WaitIdValue(1));
    }

    [Fact]
    public void EphemeralBuilder_DoesNotExposeWaitLong()
    {
        typeof(WorkflowBuilder<TestState>).GetMethods()
            .Should().NotContain(method => method.Name == "WaitLong");
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

    private static DurableWaitRegisteredCommand WaitRegisteredCommand(
        InstanceId instanceId,
        WaitId waitId,
        int commandValue,
        WaitMode waitMode,
        string? branchId = null)
    {
        return new DurableWaitRegisteredCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            waitId,
            "Approved",
            new CorrelationId("order-1"),
            waitMode,
            branchId);
    }

    private static DeliverEventCommand Deliver(InstanceId instanceId, int commandValue, string? branchId)
    {
        return new DeliverEventCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = instanceId,
            RequestedAt = Timestamp(commandValue),
            Envelope = new EventEnvelope
            {
                EventId = EventIdValue(commandValue + 100),
                EventName = "Approved",
                CorrelationId = new CorrelationId("order-1"),
                BranchId = branchId,
                OccurredAt = Timestamp(commandValue)
            }
        };
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

    private sealed record TestState(string CorrelationId);
}
