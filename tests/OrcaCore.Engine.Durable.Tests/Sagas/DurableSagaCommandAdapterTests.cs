using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class DurableSagaCommandAdapterTests
{
    [Fact]
    [Trait("AC", "AC-406")]
    public async Task Adapter_MapsSagaDefinitionActionsToDurableCommands()
    {
        var definition = new SagaBuilder<SagaState>()
            .Init<string>(_ => new SagaState())
            .CompensationScope("checkout", scope => scope
                .Then<ReserveInventoryStep>()
                .CompensateBy<ReleaseInventoryStep>())
            .End()
            .Build(DefinitionIdValue(1), DefinitionVersion.Initial);
        var adapter = new DurableSagaCommandAdapter<SagaState>(definition);
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);

        await processor.ProcessAsync(Start(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            adapter.ForwardActionCompleted(CommandIdValue(2), instanceId, Timestamp(2), "root/actions/0"),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            adapter.RequestCompensation(CommandIdValue(3), instanceId, Timestamp(3), "checkout", "test"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<SagaForwardActionCompletedEvent>().Should().ContainSingle()
            .Which.CompensationKey.Should().Be("root/actions/0/compensation");
        events.OfType<SagaCompensationStartedEvent>().Should().ContainSingle()
            .Which.ActionKey.Should().Be("root/actions/0/compensation");
    }

    [Fact]
    [Trait("AC", "AC-406")]
    public async Task Adapter_DurableRestartDuringCompensation_DoesNotDuplicateEffectsAndCompletesSaga()
    {
        var definition = new SagaBuilder<SagaState>()
            .Init<string>(_ => new SagaState())
            .CompensationScope("checkout", scope => scope
                .Then<ReserveInventoryStep>()
                .CompensateBy<ReleaseInventoryStep>()
                .Then<AuthorizePaymentStep>()
                .CompensateBy<RefundPaymentStep>())
            .End()
            .Build(DefinitionIdValue(1), DefinitionVersion.Initial);
        var adapter = new DurableSagaCommandAdapter<SagaState>(definition);
        var instanceId = InstanceIdValue(2);
        var store = new InMemoryWorkflowProvider();
        var beforeRestart = new DurableCommandProcessor(store);
        await beforeRestart.ProcessAsync(Start(instanceId), TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(
            adapter.ForwardActionCompleted(CommandIdValue(2), instanceId, Timestamp(2), "root/actions/0"),
            TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(
            adapter.ForwardActionCompleted(CommandIdValue(3), instanceId, Timestamp(3), "root/actions/1"),
            TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(
            adapter.RequestCompensation(CommandIdValue(4), instanceId, Timestamp(4), "checkout", "forward failed"),
            TestContext.Current.CancellationToken);

        var afterRestart = new DurableCommandProcessor(store);
        await afterRestart.ProcessAsync(
            adapter.RequestCompensation(CommandIdValue(5), instanceId, Timestamp(5), "checkout", "duplicate replay"),
            TestContext.Current.CancellationToken);
        await afterRestart.ProcessAsync(
            adapter.CompensationCompleted(
                CommandIdValue(6),
                instanceId,
                Timestamp(6),
                "root/actions/1/compensation"),
            TestContext.Current.CancellationToken);
        await new DurableCommandProcessor(store).ProcessAsync(
            adapter.CompensationCompleted(
                CommandIdValue(7),
                instanceId,
                Timestamp(7),
                "root/actions/0/compensation"),
            TestContext.Current.CancellationToken);

        var events = await EventsAsync(store, instanceId);
        var audit = await new DurableManagement(store)
            .GetSagaAuditAsync(instanceId, TestContext.Current.CancellationToken);

        events.OfType<SagaForwardActionCompletedEvent>()
            .Select(workflowEvent => workflowEvent.ActionKey)
            .Should().Equal("root/actions/0", "root/actions/1");
        events.OfType<SagaCompensationStartedEvent>()
            .Select(workflowEvent => workflowEvent.ActionKey)
            .Should().Equal("root/actions/1/compensation", "root/actions/0/compensation");
        events.OfType<SagaCompensationCompletedEvent>().Should().HaveCount(2);
        events.OfType<WorkflowTerminalEvent>().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Compensated);
        audit.Scopes.Should().ContainSingle()
            .Which.Outcome.Should().Be(WorkflowStatus.Compensated);
    }

    private static async Task<IReadOnlyList<WorkflowEvent>> EventsAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId)
    {
        return await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
    }

    private sealed record SagaState;

    private sealed class ReserveInventoryStep : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<SagaState> context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ReleaseInventoryStep : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<SagaState> context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class AuthorizePaymentStep : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<SagaState> context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class RefundPaymentStep : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<SagaState> context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static StartWorkflowCommand Start(InstanceId instanceId)
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

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 16, 0, seconds, TimeSpan.Zero);
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
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
