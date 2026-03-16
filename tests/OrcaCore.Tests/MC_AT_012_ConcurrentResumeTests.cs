using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_012_ConcurrentResumeTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public int ResumeCount { get; set; }
    }

    private sealed class IncrementStep : IStep<MyState>
    {
        public string StepId => "Increment";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.ResumeCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Concurrent_resume_attempts_resolve_as_one_valid_outcome()
    {
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var definition = new WorkflowBuilder<MyState>("ConcurrentWorkflow")
                .Init()
                .Wait("Approval", s => s.Id)
                .Step<IncrementStep>()
                .End()
                .Build();

            await using var engine = new WorkflowEngine();
            var typed = engine.ForDefinition(definition);
            var snapshot = await typed.Start(new MyState());

            var scope = engine.Instance(snapshot.InstanceId);
            var barrier = new Barrier(2);

            var envelope1 = new EventEnvelope("Approval", "corr-1", null, "evt-1");
            var envelope2 = new EventEnvelope("Approval", "corr-1", null, "evt-2");

            var task1 = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await scope.RaiseEvent(envelope1);
            });

            var task2 = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await scope.RaiseEvent(envelope2);
            });

            await Task.WhenAll(task1, task2);

            var finalState = scope.GetState<MyState>();
            Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
            Assert.Equal(1, finalState.ResumeCount);
        }
    }
}
