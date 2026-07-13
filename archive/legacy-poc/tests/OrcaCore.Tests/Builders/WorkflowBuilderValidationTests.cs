using OrcaCore.Abstractions.Primitives;
using OrcaCore.Runtime.Builders;

namespace OrcaCore.Tests.Builders;

public sealed class WorkflowBuilderValidationTests
{
    private sealed class NoOpStep : IStep<object>
    {
        public string StepId => "NoOp";

        public Task<StepResult> ExecuteAsync(StepContext<object> context) =>
            Task.FromResult<StepResult>(new StepResult.Completed());
    }

    [Fact]
    public void TryValidate_empty_builder_collects_init_and_end_errors()
    {
        var builder = new WorkflowBuilder<object>("Test");
        var v = builder.TryValidate();

        Assert.False(v.IsValid);
        Assert.Equal(2, v.Errors.Count);
        Assert.Contains(v.Errors, e => e.Code == "BUILD_INIT_REQUIRED");
        Assert.Contains(v.Errors, e => e.Code == "BUILD_END_REQUIRED");
    }

    [Fact]
    public void TryValidate_after_Init_only_reports_end_required()
    {
        var builder = new WorkflowBuilder<object>("Test").Init();
        var v = builder.TryValidate();

        Assert.False(v.IsValid);
        Assert.Single(v.Errors);
        Assert.Equal("BUILD_END_REQUIRED", v.Errors[0].Code);
    }

    [Fact]
    public void TryValidate_valid_builder_returns_definition()
    {
        var builder = new WorkflowBuilder<object>("Id")
            .Init()
            .Then<NoOpStep>()
            .End();

        var v = builder.TryValidate();

        Assert.True(v.IsValid);
        Assert.NotNull(v.Value);
        Assert.Equal("Id", v.Value!.DefinitionId);
    }
}
