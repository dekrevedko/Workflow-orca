
namespace OrcaCore.Tests.Durable;

public sealed class DefinitionPathIndexTests
{
    private sealed class NoOpStep : IStep<MyState>
    {
        public string StepId => "NoOp";

        public Task<StepResult> ExecuteAsync(StepContext<MyState> context) =>
            Task.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed record MyState(string Id = "state-1");

    [Fact]
    public void Definition_builds_deterministic_node_list_paths()
    {
        var definition = new WorkflowBuilder<MyState>("PathWorkflow")
            .Init()
            .If(
                _ => true,
                then: branch => branch.Then<NoOpStep>(),
                @else: branch => branch.Then<NoOpStep>())
            .While(_ => false, body => body.Then<NoOpStep>())
            .Parallel(parallel => parallel
                .Branch("alpha", branch => branch.Then<NoOpStep>())
                .Branch("beta", branch => branch.Wait("Evt", state => state.Id)))
            .End()
            .Build();

        Assert.Same(definition.Nodes, definition.ResolveNodes(string.Empty));
        Assert.Single(definition.ResolveNodes("0/then"));
        Assert.Single(definition.ResolveNodes("0/else"));
        Assert.Single(definition.ResolveNodes("1/body"));
        Assert.Single(definition.ResolveNodes("2/branch/alpha"));
        Assert.Single(definition.ResolveNodes("2/branch/beta"));
    }

    [Fact]
    public void ResolveNodes_rejects_unknown_path()
    {
        var definition = new WorkflowBuilder<MyState>("PathWorkflow")
            .Init()
            .End()
            .Build();

        Action act = () => definition.ResolveNodes("missing/path");
        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("missing/path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
