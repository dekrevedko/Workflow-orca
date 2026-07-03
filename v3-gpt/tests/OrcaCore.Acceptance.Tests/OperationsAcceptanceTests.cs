using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class OperationsAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-507")]
    public async Task StuckStep_IsSignalledAndQueryable()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(
            clock.TimeProvider,
            new EphemeralWorkflowEngineOptions
            {
                StuckStepThreshold = TimeSpan.FromSeconds(5)
            });
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .Then(() => new SlowStep(clock, TimeSpan.FromSeconds(6)))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.HasStuckStep.Should().BeTrue();
        engine.Management.All().Where(instance => instance.HasStuckStep).List()
            .Should().ContainSingle(instance => instance.InstanceId == snapshot.InstanceId);
    }

    [Fact]
    [Trait("AC", "AC-508")]
    public async Task StuckInstance_IsSignalledAndQueryable()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .Wait("Ready", _ => new CorrelationId("item-1"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(6));

        engine.Management.All().DetectStuck(TimeSpan.FromSeconds(5));

        engine.Management.All().Where(instance => instance.IsStuck).List()
            .Should().ContainSingle(instance => instance.InstanceId == waiting.InstanceId);
    }

    [Fact]
    [Trait("AC", "AC-511")]
    public async Task ConcurrencyLimitsAndNamedPoolsAreHonored()
    {
        var gate = new StepGate();
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions
            {
                MaxConcurrentSteps = 2,
                NamedPools = { ["db"] = 1 }
            });
        var firstDefinition = BlockingDefinition(gate, "db");
        var secondDefinition = BlockingDefinition(gate, "db");
        engine.RegisterDefinition(firstDefinition);
        engine.RegisterDefinition(secondDefinition);

        var first = engine.StartAsync<string, TestState>(
            firstDefinition.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        await gate.WaitForEnteredCountAsync(1, TestContext.Current.CancellationToken);
        var second = engine.StartAsync<string, TestState>(
            secondDefinition.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);

        var secondEntry = gate.WaitForEnteredCountAsync(2, TestContext.Current.CancellationToken);
        await Task.Yield();
        var secondEnteredBeforeRelease = secondEntry.IsCompleted;
        gate.ReleaseOne();
        await secondEntry;
        gate.ReleaseOne();
        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        secondEnteredBeforeRelease.Should().BeFalse();
        gate.MaxObservedConcurrent.Should().Be(1);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> BlockingDefinition(
        StepGate gate,
        string poolKey)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState { Gate = gate })
            .WithPoolKey(poolKey)
            .Then(() => new BlockingStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private sealed class TestState
    {
        public StepGate? Gate { get; set; }
    }

    private sealed class BlockingStep : IStep<TestState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            await context.State.Gate!.EnterAndWaitAsync(cancellationToken).ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    private sealed class SlowStep(Clock clock, TimeSpan duration) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            clock.Advance(duration);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class StepGate
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
                release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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

        internal async Task WaitForEnteredCountAsync(int count, CancellationToken cancellationToken)
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
                    waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
