using OrcaCore.Abstractions.Primitives;
using OrcaCore.Runtime.Durable.Builders;

namespace OrcaCore.Tests.Durable;

public sealed class DurableWorkflowBuilderValidationTests
{
    private sealed class NoOpStep : IStep<MyState>
    {
        public string StepId => "NoOp";

        public Task<StepResult> ExecuteAsync(StepContext<MyState> context) =>
            Task.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed record MyState(string Id = "state-1");

    [Fact]
    public void TryValidate_empty_builder_collects_init_and_end_errors()
    {
        var builder = new DurableWorkflowBuilder<MyState>("D", "v1");
        var v = builder.TryValidate();

        Assert.False(v.IsValid);
        Assert.Equal(2, v.Errors.Count);
        Assert.Contains(v.Errors, e => e.Code == "BUILD_INIT_REQUIRED");
        Assert.Contains(v.Errors, e => e.Code == "BUILD_END_REQUIRED");
    }

    [Fact]
    public void TryValidate_after_Init_only_reports_end_required()
    {
        var builder = new DurableWorkflowBuilder<MyState>("D", "v1").Init();
        var v = builder.TryValidate();

        Assert.False(v.IsValid);
        Assert.Single(v.Errors);
        Assert.Equal("BUILD_END_REQUIRED", v.Errors[0].Code);
    }

    [Fact]
    public void TryValidate_valid_builder_returns_definition()
    {
        var builder = new DurableWorkflowBuilder<MyState>("Flow", "2026-01-01")
            .Init()
            .Then<NoOpStep>()
            .End();

        var v = builder.TryValidate();

        Assert.True(v.IsValid);
        Assert.NotNull(v.Value);
        Assert.Equal("Flow", v.Value!.DefinitionId);
        Assert.Equal("2026-01-01", v.Value.DefinitionVersion);
    }
}
