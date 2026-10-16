
namespace OrcaCore.Tests;

public class WorkflowBuilderContractTests
{
    private sealed class NoOpStep : IStep<object>
    {
        public string StepId => "NoOp";

        public Task<StepResult> ExecuteAsync(StepContext<object> context) =>
            Task.FromResult<StepResult>(new StepResult.Completed());
    }

    [Fact]
    public void Build_requires_Init()
    {
        var builder = new WorkflowBuilder<object>("Test");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("Init", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_requires_End()
    {
        var builder = new WorkflowBuilder<object>("Test")
            .Init();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("End", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cannot_add_steps_before_Init()
    {
        var builder = new WorkflowBuilder<object>("Test");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Then<NoOpStep>());

        Assert.Contains("Init", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cannot_add_steps_after_End()
    {
        var builder = new WorkflowBuilder<object>("Test")
            .Init()
            .End();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Then<NoOpStep>());

        Assert.Contains("after End", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_Init_is_rejected()
    {
        var builder = new WorkflowBuilder<object>("Test").Init();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Init());

        Assert.Contains("already", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_End_is_rejected()
    {
        var builder = new WorkflowBuilder<object>("Test")
            .Init()
            .End();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.End());

        Assert.Contains("already", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

