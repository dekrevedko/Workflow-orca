
namespace OrcaCore.Tests;

public class MC_AT_009_FailingStepTests
{
    private sealed class MyState
    {
        public bool StepCExecuted { get; set; }
    }

    private sealed class FailingStep : IStep<MyState>
    {
        public string StepId => "Failing";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            return Task.FromResult<StepResult>(
                new StepResult.Failed(new InvalidOperationException("Business error")));
        }
    }

    private sealed class ThrowingStep : IStep<MyState>
    {
        public string StepId => "Throwing";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            throw new InvalidOperationException("Unhandled exception");
        }
    }

    private sealed class StepC : IStep<MyState>
    {
        public string StepId => "StepC";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.StepCExecuted = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Explicit_failure_moves_workflow_to_failed_state()
    {
        var definition = new WorkflowBuilder<MyState>("FailWorkflow")
            .Init()
            .Then<FailingStep>()
            .Then<StepC>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.NotNull(snapshot.Error);
        Assert.Equal("Failing", snapshot.Error.StepId);
        Assert.Contains("Business error", snapshot.Error.Exception.Message);
        // RWI-AT-016: error details are inspectable – verify all fields populated
        Assert.NotEmpty(snapshot.Error.ExceptionType);
        Assert.NotEmpty(snapshot.Error.Message);
        Assert.True(snapshot.Error.Timestamp > DateTimeOffset.MinValue);
        Assert.False(engine.Instance(snapshot.InstanceId).GetState<MyState>().StepCExecuted);
    }

    [Fact]
    public async Task Unhandled_exception_moves_workflow_to_failed_state()
    {
        var definition = new WorkflowBuilder<MyState>("ThrowWorkflow")
            .Init()
            .Then<ThrowingStep>()
            .Then<StepC>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.NotNull(snapshot.Error);
        Assert.Equal("Throwing", snapshot.Error.StepId);
        Assert.NotEmpty(snapshot.Error.ExceptionType);
        Assert.NotEmpty(snapshot.Error.Message);
        Assert.True(snapshot.Error.Timestamp > DateTimeOffset.MinValue);
        Assert.False(engine.Instance(snapshot.InstanceId).GetState<MyState>().StepCExecuted);
    }

    [Fact]
    public async Task RaiseEvent_on_failed_workflow_is_rejected()
    {
        var definition = new WorkflowBuilder<MyState>("FailWorkflow")
            .Init()
            .Then<FailingStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.Instance(snapshot.InstanceId).RaiseEvent(
                new EventEnvelope("Anything", "corr", null, "evt-1")));

        Assert.Contains("terminal state", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkflowStatus.Failed, engine.Instance(snapshot.InstanceId).Get().Status);
    }

    [Fact]
    public async Task RaiseEvent_on_completed_workflow_is_rejected()
    {
        var definition = new WorkflowBuilder<MyState>("SimpleWorkflow")
            .Init()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.Instance(snapshot.InstanceId).RaiseEvent(
                new EventEnvelope("Anything", "corr", null, "evt-1")));

        Assert.Contains("terminal state", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snapshot.InstanceId).Get().Status);
    }
}

