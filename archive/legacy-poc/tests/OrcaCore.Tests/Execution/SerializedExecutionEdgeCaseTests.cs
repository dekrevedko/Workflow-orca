
namespace OrcaCore.Tests;

public class SerializedExecutionEdgeCaseTests
{
    private sealed class ParallelState
    {
        public string Id { get; set; } = "corr-1";
        public int BranchACount { get; set; }
        public int BranchBCount { get; set; }
        public int JoinCount { get; set; }
    }

    private sealed class MarkAOnceStep : IStep<ParallelState>
    {
        public string StepId => "MarkAOnce";

        public Task<StepResult> ExecuteAsync(StepContext<ParallelState> context)
        {
            context.State.BranchACount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class MarkBOnceStep : IStep<ParallelState>
    {
        public string StepId => "MarkBOnce";

        public Task<StepResult> ExecuteAsync(StepContext<ParallelState> context)
        {
            context.State.BranchBCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class JoinOnceStep : IStep<ParallelState>
    {
        public string StepId => "JoinOnce";

        public Task<StepResult> ExecuteAsync(StepContext<ParallelState> context)
        {
            context.State.JoinCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class BufferedState
    {
        public string Id { get; set; } = "corr-1";
        public int StepAfterA { get; set; }
        public int StepAfterB { get; set; }
    }

    private sealed class AfterAStep : IStep<BufferedState>
    {
        public string StepId => "AfterA";

        public Task<StepResult> ExecuteAsync(StepContext<BufferedState> context)
        {
            context.State.StepAfterA++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class AfterBStep : IStep<BufferedState>
    {
        public string StepId => "AfterB";

        public Task<StepResult> ExecuteAsync(StepContext<BufferedState> context)
        {
            context.State.StepAfterB++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ResumeState
    {
        public string Id { get; set; } = "corr-1";
        public int ResumeCount { get; set; }
    }

    private sealed class IncrementResumeStep : IStep<ResumeState>
    {
        public string StepId => "IncrementResume";

        public Task<StepResult> ExecuteAsync(StepContext<ResumeState> context)
        {
            context.State.ResumeCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CorrelationState
    {
        public string RequestId { get; set; } = "";
        public int ResumeCount { get; set; }
    }

    private sealed class MarkCorrelationResumedStep : IStep<CorrelationState>
    {
        public string StepId => "MarkCorrelationResumed";

        public Task<StepResult> ExecuteAsync(StepContext<CorrelationState> context)
        {
            context.State.ResumeCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Concurrent_parallel_branch_resumes_produce_one_join()
    {
        var definition = new WorkflowBuilder<ParallelState>("ConcurrentParallel")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Wait("EventA", s => s.Id).Then<MarkAOnceStep>())
                .Branch("B", b => b.Wait("EventB", s => s.Id).Then<MarkBOnceStep>()))
            .Then<JoinOnceStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var scope = engine.Instance((await engine.ForDefinition(definition).Start(new ParallelState())).InstanceId);
        var barrier = new Barrier(2);

        var taskA = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await scope.RaiseEvent(new EventEnvelope("EventA", "corr-1", null, "evt-a"));
        });

        var taskB = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await scope.RaiseEvent(new EventEnvelope("EventB", "corr-1", null, "evt-b"));
        });

        await Task.WhenAll(taskA, taskB);

        var state = scope.GetState<ParallelState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(1, state.BranchACount);
        Assert.Equal(1, state.BranchBCount);
        Assert.Equal(1, state.JoinCount);
    }

    [Fact]
    public async Task Concurrent_current_and_future_events_still_produce_one_valid_outcome()
    {
        var definition = new WorkflowBuilder<BufferedState>("BufferedRace")
            .Init()
            .Wait("EventA", s => s.Id)
            .Then<AfterAStep>()
            .Wait("EventB", s => s.Id)
            .Then<AfterBStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var scope = engine.Instance((await engine.ForDefinition(definition).Start(new BufferedState())).InstanceId);
        var barrier = new Barrier(2);

        var currentTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await scope.RaiseEvent(new EventEnvelope("EventA", "corr-1", null, "evt-a"));
        });

        var futureTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await scope.RaiseEvent(new EventEnvelope("EventB", "corr-1", null, "evt-b"));
        });

        await Task.WhenAll(currentTask, futureTask);

        var state = scope.GetState<BufferedState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(1, state.StepAfterA);
        Assert.Equal(1, state.StepAfterB);
    }

    [Fact]
    public async Task Concurrent_distinct_resumes_on_same_instance_allow_only_one_commit()
    {
        var definition = new WorkflowBuilder<ResumeState>("ConflictingResume")
            .Init()
            .Wait("Approval", s => s.Id)
            .Then<IncrementResumeStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var scope = engine.Instance((await engine.ForDefinition(definition).Start(new ResumeState())).InstanceId);
        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            try
            {
                await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-1"));
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        var task2 = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            try
            {
                await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-2"));
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var state = scope.GetState<ResumeState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(1, state.ResumeCount);
        Assert.Single(results, x => x is InvalidOperationException);
    }

    [Fact]
    public async Task Concurrent_correlation_targeted_routing_across_instances_is_stable()
    {
        var definition = new WorkflowBuilder<CorrelationState>("CorrelationConcurrent")
            .Init()
            .Wait("Response", s => s.RequestId)
            .Then<MarkCorrelationResumedStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        var snapshots = new List<WorkflowInstanceSnapshot>();
        for (var i = 0; i < 10; i++)
        {
            snapshots.Add(await typed.Start(new CorrelationState { RequestId = $"req-{i}" }));
        }

        var tasks = snapshots.Select((snapshot, index) =>
            Task.Run(() => engine.RaiseEvent(
                new EventEnvelope("Response", $"req-{index}", null, $"evt-{index}"))));

        await Task.WhenAll(tasks);

        foreach (var snapshot in snapshots)
        {
            var scope = engine.Instance(snapshot.InstanceId);
            var state = scope.GetState<CorrelationState>();
            Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
            Assert.Equal(1, state.ResumeCount);
        }
    }
}

