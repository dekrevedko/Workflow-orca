
namespace OrcaCore.Tests.Durable;

public sealed class DurableBuilderContractTests
{
    private sealed class NoOpStep : IStep<MyState>
    {
        public string StepId => "NoOp";

        public Task<StepResult> ExecuteAsync(StepContext<MyState> context) =>
            Task.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed record MyState(string Id = "state-1");

    [Fact]
    public void Durable_builder_exposes_WaitLong_and_captures_definition_version()
    {
        var definition = new DurableWorkflowBuilder<MyState>("DurableFlow", "2026-03-16")
            .Init()
            .WaitLong("ApprovalTimeout", state => state.Id)
            .End()
            .Build();

        Assert.Equal("DurableFlow", definition.DefinitionId);
        Assert.Equal("2026-03-16", definition.DefinitionVersion);

        var nodes = definition.ResolveNodes(string.Empty);
        var waitLong = Assert.IsType<WaitLongNode<MyState>>(Assert.Single(nodes));
        Assert.Equal("ApprovalTimeout", waitLong.EventName);
        Assert.Equal("state-1", waitLong.CorrelationSelector(new MyState()));
    }

    [Fact]
    public void Durable_definition_builds_deterministic_node_paths()
    {
        var definition = new DurableWorkflowBuilder<MyState>("DurablePathFlow", "v1")
            .Init()
            .If(
                _ => true,
                then: branch => branch.WaitLong("Then", state => state.Id),
                @else: branch => branch.Then<NoOpStep>())
            .While(_ => false, body => body.WaitLong("Loop", state => state.Id))
            .Parallel(parallel => parallel
                .Branch("alpha", branch => branch.WaitLong("Alpha", state => state.Id))
                .Branch("beta", branch => branch.Then<NoOpStep>()))
            .End()
            .Build();

        Assert.Single(definition.ResolveNodes("0/then"));
        Assert.Single(definition.ResolveNodes("0/else"));
        Assert.Single(definition.ResolveNodes("1/body"));
        Assert.Single(definition.ResolveNodes("2/branch/alpha"));
        Assert.Single(definition.ResolveNodes("2/branch/beta"));
    }

    [Fact]
    public void Ephemeral_builder_still_does_not_expose_WaitLong()
    {
        var methods = typeof(WorkflowBuilder<MyState>).GetMethods();

        Assert.DoesNotContain(methods, method => method.Name == "WaitLong");
    }
}
