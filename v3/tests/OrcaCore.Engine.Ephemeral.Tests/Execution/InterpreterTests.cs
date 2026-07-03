using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport.Time;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public class InterpreterTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    private sealed class RecordingStep(string tag) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Executed.Add(tag);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingStep(OrcaCoreException error) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Failed(error));
    }

    private sealed class ThrowingStep(Exception exception) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            throw exception;
    }

    private sealed class UnsupportedResultStep(StepResult result) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(result);
    }

    private static WorkflowInstance<OrderState> NewInstance(Clock clock) =>
        new(
            InstanceId.New(),
            new DefinitionId("order-workflow"),
            new DefinitionVersion(1),
            new OrderState(),
            clock.Now);

    [Fact]
    public async Task Run_InitStepEnd_CompletesAndMutatesState()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new RecordingStep("only"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);
        instance.State = new OrderState { Total = 5 };

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("only");
    }

    [Fact]
    public async Task Run_TwoSteps_ExecuteInOrder_SharedState()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new RecordingStep("first"));
        builder.Then(new RecordingStep("second"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("first", "second");
    }

    [Fact]
    public async Task Run_StepReturnsFailed_InstanceFailed_LaterStepsSkipped()
    {
        var error = new WorkflowDefinitionException("boom");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new FailingStep(error));
        builder.Then(new RecordingStep("never"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.State.Executed.Should().BeEmpty();
        instance.ErrorSummary.Should().NotBeNull();
        instance.ErrorSummary!.Should().Contain("boom");
    }

    [Fact]
    public async Task Run_StepThrows_InstanceFailed_ErrorDetailsCaptured()
    {
        var exception = new InvalidOperationException("kaboom");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new ThrowingStep(exception));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.ErrorSummary.Should().NotBeNull();
        instance.ErrorSummary!.Should().Contain("kaboom");
        instance.ErrorSummary.Should().Contain(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task Run_EndWithOutcome_RecordsOutcomeName()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.End("Approved");
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.EndOutcomeName.Should().Be("Approved");
    }

    [Fact]
    public async Task Run_WaitForEventResult_SuspendsInstanceWithoutAdvancing()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new UnsupportedResultStep(new StepResult.WaitForEvent("ApprovalReceived", new CorrelationId("order-1"))));
        builder.Then(new RecordingStep("never"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Waiting);
        instance.State.Executed.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_YieldResult_StopsRunLoopWithoutAdvancing_InstanceStillRunning()
    {
        // T1-15 (CR-017): Interpreter.RunAsync itself only drives ONE lane-protected cycle - a
        // yielding step stops the run loop with YieldPending set and the pointer parked on the
        // SAME step, without throwing and without reaching a terminal status. Multi-cycle
        // continuation across separate lane calls is exercised at the engine facade level
        // (YieldTests.cs / YieldAcceptanceTests.cs); this interpreter-level test proves the
        // single-cycle stopping behavior the engine facade's continuation loop depends on.
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new UnsupportedResultStep(new StepResult.Yield()));
        builder.Then(new RecordingStep("after-yield"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Running);
        instance.YieldPending.Should().BeTrue();
        instance.State.Executed.Should().BeEmpty();
    }
}
