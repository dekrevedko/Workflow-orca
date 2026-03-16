using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_011_WaitLongUnavailableTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
    }

    [Fact]
    public async Task WaitLong_is_rejected_at_runtime_with_clear_error()
    {
        // Manually construct a definition containing WaitLongStep
        // (the builder intentionally does not expose WaitLong)
        var steps = new List<IStep<MyState>>
        {
            new WaitLongStep<MyState>("DurableEvent", s => s.Id)
        };
        var definition = new WorkflowDefinition<MyState>("WaitLongWorkflow", steps.AsReadOnly());

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.NotNull(snapshot.Error);
        Assert.Contains("WaitLong requires durable mode", snapshot.Error.Exception.Message);
    }

    [Fact]
    public void Builder_does_not_expose_WaitLong()
    {
        // Compile-time guard: verify WorkflowBuilder does not have a WaitLong method
        var methods = typeof(WorkflowBuilder<MyState>).GetMethods();
        Assert.DoesNotContain(methods, m => m.Name == "WaitLong");
    }
}
