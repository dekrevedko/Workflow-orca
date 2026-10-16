using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class OperationsAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-511")]
    public async Task TransientPoolLimit_IsHonoredAcrossWorkflowInstances()
    {
        var gate = new StepGate();
        var pool = TransientPoolName.Create("db");
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        services.AddTransient<BlockingStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            TransientPools = [TransientPoolDefinition.Create(pool, 1)]
        });
        using var provider = services.BuildServiceProvider();
        var definition = global::OrcaCore.Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then<BlockingStep>()
            .WithTransientPool(pool)
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        var first = definitionHandle.StartOrGetAsync(
            "first",
            StartIdempotencyKey.Create("pool-first"),
            TestContext.Current.CancellationToken).AsTask();
        await gate.WaitForEnteredCountAsync(1, TestContext.Current.CancellationToken);
        var second = definitionHandle.StartOrGetAsync(
            "second",
            StartIdempotencyKey.Create("pool-second"),
            TestContext.Current.CancellationToken).AsTask();

        await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, TestContext.Current.CancellationToken);
        gate.MaxObservedConcurrent.Should().Be(1);
        gate.ReleaseOne();
        await gate.WaitForEnteredCountAsync(2, TestContext.Current.CancellationToken);
        gate.MaxObservedConcurrent.Should().Be(1);
        gate.ReleaseOne();
        var instances = await Task.WhenAll(first, second)
            .WaitAsync(TestContext.Current.CancellationToken);

        foreach (var started in instances)
        {
            var snapshot = await started.GetHandleOrThrow()
                .GetSnapshotAsync(TestContext.Current.CancellationToken);
            snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        }
    }

    public sealed class TestState;

    public sealed class BlockingStep(StepGate gate) : IStep<TestState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            await gate.EnterAndWaitAsync(cancellationToken).ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    public sealed class StepGate
    {
        private readonly object gate = new();
        private readonly Queue<TaskCompletionSource> releases = [];
        private readonly Dictionary<int, TaskCompletionSource> enteredWaiters = [];
        private int activeCount;
        private int enteredCount;

        internal int MaxObservedConcurrent { get; private set; }

        internal Task EnterAndWaitAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource release;
            TaskCompletionSource[] completedWaiters;
            lock (gate)
            {
                enteredCount++;
                activeCount++;
                MaxObservedConcurrent = Math.Max(MaxObservedConcurrent, activeCount);
                release = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                releases.Enqueue(release);
                completedWaiters = enteredWaiters
                    .Where(waiter => enteredCount >= waiter.Key)
                    .Select(waiter => waiter.Value)
                    .ToArray();
            }

            foreach (var waiter in completedWaiters)
            {
                waiter.TrySetResult();
            }

            return WaitForReleaseAsync(release, cancellationToken);
        }

        internal void ReleaseOne()
        {
            TaskCompletionSource release;
            lock (gate)
            {
                release = releases.Dequeue();
                activeCount--;
            }

            release.SetResult();
        }

        internal async Task WaitForEnteredCountAsync(
            int count,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource waiter;
            lock (gate)
            {
                if (enteredCount >= count)
                {
                    return;
                }

                if (!enteredWaiters.TryGetValue(count, out waiter!))
                {
                    waiter = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    enteredWaiters.Add(count, waiter);
                }
            }

            await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task WaitForReleaseAsync(
            TaskCompletionSource release,
            CancellationToken cancellationToken)
        {
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
