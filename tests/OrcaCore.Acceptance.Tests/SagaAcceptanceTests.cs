using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class SagaAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-403")]
    public async Task SagaFailure_CompensatesCompletedActionsInReverseOrder()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ForwardCompleted("reserve", "release", 2), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ForwardCompleted("authorize", "refund", 3), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(RequestCompensation(4), TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<SagaCompensationStartedEvent>()
            .Select(compensation => compensation.ActionKey)
            .Should().Equal("refund", "release");
    }

    [Fact]
    [Trait("AC", "AC-409")]
    public async Task RepeatedSagaCompensationRequest_DoesNotDuplicateStartedFacts()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ForwardCompleted("reserve", "release", 2), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(RequestCompensation(3), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RequestCompensation(4), TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<SagaCompensationStartedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-405")]
    public async Task SagaForwardTimeout_WithCompensatingPolicy_CompensatesCompletedActions()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ForwardCompleted("reserve", "release", 2), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(new SagaForwardActionTimedOutCommand
        {
            CommandId = CommandIdValue(3),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(3),
            ScopeId = "checkout",
            ActionKey = "authorize",
            CompensateScope = true
        }, TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<SagaForwardActionTimedOutEvent>().Should().ContainSingle();
        events.OfType<SagaCompensationStartedEvent>().Should().ContainSingle()
            .Which.ActionKey.Should().Be("release");
    }

    [Fact]
    [Trait("AC", "AC-402")]
    [Trait("AC", "AC-403")]
    public async Task EphemeralSagaFailure_CompensatesInProcessInReverseOrder()
    {
        var state = new EphemeralSagaState();
        var definition = new SagaBuilder<EphemeralSagaState>()
            .Init<string>(_ => state)
            .Then(() => new EphemeralRecordingStep("reserve"))
            .CompensateBy(() => new EphemeralRecordingStep("release"))
            .Then(() => new EphemeralRecordingStep("authorize"))
            .CompensateBy(() => new EphemeralRecordingStep("refund"))
            .Then(() => new EphemeralFailingStep())
            .End()
            .Build(DefinitionIdValue(10), DefinitionVersion.Initial);

        var snapshot = await new EphemeralWorkflowEngine().StartSagaAsync<string, EphemeralSagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(OrcaCore.Abstractions.Instances.WorkflowStatus.Compensated);
        state.Values.Should().Equal("reserve", "authorize", "refund", "release");
    }

    private static StartWorkflowCommand Start()
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static RecordSagaForwardActionCompletedCommand ForwardCompleted(
        string actionKey,
        string compensationKey,
        int commandValue)
    {
        return new RecordSagaForwardActionCompletedCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "checkout",
            ActionKey = actionKey,
            CompensationKey = compensationKey
        };
    }

    private static RequestSagaCompensationCommand RequestCompensation(int commandValue)
    {
        return new RequestSagaCompensationCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "checkout",
            Reason = "forward failure"
        };
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 16, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private sealed class EphemeralSagaState
    {
        public List<string> Values { get; } = [];
    }

    private sealed class EphemeralRecordingStep(string value) : IStep<EphemeralSagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<EphemeralSagaState> context,
            CancellationToken cancellationToken)
        {
            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class EphemeralFailingStep : IStep<EphemeralSagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<EphemeralSagaState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowDefinitionException("forward failed")));
        }
    }
}
