using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class InterpreterTests
{
    private static readonly DefinitionId DefinitionId = new(Guid.Parse("11111111-1111-7111-8111-111111111111"));
    private static readonly DefinitionVersion Version = new(1);

    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Trace { get; } = [];
    }

    private sealed class RecordingStep(string tag, Action<OrderState>? mutate = null) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Trace.Add(tag);
            mutate?.Invoke(context.State);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingStep(string errorMessage) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Failed(new OrcaCoreException(errorMessage)));
    }

    private sealed class ThrowingStep(string exceptionMessage) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(exceptionMessage);
    }

    private sealed class UnsupportedResultStep(StepResult result) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    private static Interpreter<OrderState> CreateInterpreter(Clock clock) => new(clock.Provider);

    [Fact]
    public async Task Run_InitStepEnd_CompletesAndMutatesState()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new RecordingStep("charge", state => state.Total += 5))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 10, InstanceId.New(), CancellationToken.None);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Total.Should().Be(15);
        instance.State.Trace.Should().Equal("charge");
    }

    [Fact]
    public async Task Run_TwoSteps_ExecuteInOrder_SharedState()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new RecordingStep("first", state => state.Total += 1))
            .Then(new RecordingStep("second", state => state.Total *= 2))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 10, InstanceId.New(), CancellationToken.None);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Trace.Should().Equal("first", "second");
        instance.State.Total.Should().Be(22);
    }

    [Fact]
    public async Task Run_StepReturnsFailed_InstanceFailed_LaterStepsSkipped()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new FailingStep("payment declined"))
            .Then(new RecordingStep("never-runs"))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 10, InstanceId.New(), CancellationToken.None);

        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.State.Trace.Should().BeEmpty();
        instance.Error.Should().NotBeNull();
        instance.Error!.Message.Should().Be("payment declined");
    }

    [Fact]
    public async Task Run_StepThrows_InstanceFailed_ErrorDetailsCaptured()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new ThrowingStep("boom"))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 10, InstanceId.New(), CancellationToken.None);

        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.Error.Should().NotBeNull();
        instance.Error!.ErrorType.Should().Be(nameof(InvalidOperationException));
        instance.Error!.Message.Should().Be("boom");
        instance.Error!.StepPath.Should().Be("root[1]");
    }

    [Fact]
    public async Task Run_EndWithOutcome_RecordsOutcomeName()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .End("Approved")
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 10, InstanceId.New(), CancellationToken.None);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.EndOutcomeName.Should().Be("Approved");
    }

    [Fact]
    public async Task Run_YieldResult_ThrowsNamingOwnerTask()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        StepResult unsupported = new StepResult.Yield();

        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new UnsupportedResultStep(unsupported))
            .End()
            .Build(DefinitionId, Version);

        var act = () => CreateInterpreter(clock).RunAsync(definition, 10, InstanceId.New(), CancellationToken.None).AsTask();

        var exception = await act.Should().ThrowAsync<NotSupportedException>();
        exception.Which.Message.Should().Contain("T1-15");
    }
}
