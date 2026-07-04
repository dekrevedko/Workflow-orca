using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Governance;

public sealed class ResourceGovernanceTests
{
    [Fact]
    public async Task StepConcurrencyLimit_AllowsOnlyConfiguredConcurrentSteps()
    {
        var gate = new StepGate();
        var governanceAttempts = new AsyncSignalCounter();
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions
            {
                MaxConcurrentSteps = 1,
                GovernanceWaitStarting = governanceAttempts.Signal
            });
        var definition = BlockingDefinition(gate);
        engine.RegisterDefinition(definition);

        var first = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        await gate.WaitForEnteredCountAsync(1, TestContext.Current.CancellationToken);
        var second = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);

        var secondEntry = gate.WaitForEnteredCountAsync(2, TestContext.Current.CancellationToken);
        await governanceAttempts.WaitForCountAsync(2, TestContext.Current.CancellationToken);
        var secondEnteredBeforeRelease = secondEntry.IsCompleted;
        gate.ReleaseOne();
        await secondEntry;
        gate.ReleaseOne();
        var snapshots = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        secondEnteredBeforeRelease.Should().BeFalse();
        gate.MaxObservedConcurrent.Should().Be(1);
        snapshots.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Completed);
    }

    [Fact]
    public async Task NamedPoolLimit_SharedAcrossDefinitions_BoundsConcurrentExecution()
    {
        var gate = new StepGate();
        var governanceAttempts = new AsyncSignalCounter();
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions
            {
                NamedPools = { ["db"] = 1 },
                GovernanceWaitStarting = governanceAttempts.Signal
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
        await governanceAttempts.WaitForCountAsync(2, TestContext.Current.CancellationToken);
        var secondEnteredBeforeRelease = secondEntry.IsCompleted;
        gate.ReleaseOne();
        await secondEntry;
        gate.ReleaseOne();
        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        secondEnteredBeforeRelease.Should().BeFalse();
        gate.MaxObservedConcurrent.Should().Be(1);
    }

    [Fact]
    public async Task Governance_DoesNotBreakPerInstanceSerialization()
    {
        var gate = new StepGate();
        var laneEnqueues = new AsyncSignalCounter();
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions
            {
                MaxConcurrentSteps = 4,
                LaneWorkItemEnqueued = _ => laneEnqueues.Signal()
            });
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState(gate))
            .Wait("Ready", _ => new CorrelationId("same"))
            .Then(() => new BlockingStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var first = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New()),
            TestContext.Current.CancellationToken);
        await gate.WaitForEnteredCountAsync(1, TestContext.Current.CancellationToken);
        var second = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New()),
            TestContext.Current.CancellationToken);

        await laneEnqueues.WaitForCountAsync(3, TestContext.Current.CancellationToken);
        var secondCompletedBeforeRelease = second.IsCompleted;
        gate.ReleaseOne();
        await first.WaitAsync(TestContext.Current.CancellationToken);
        await second.WaitAsync(TestContext.Current.CancellationToken);

        secondCompletedBeforeRelease.Should().BeFalse();
        gate.MaxObservedConcurrent.Should().Be(1);
        gate.EnteredCount.Should().Be(1);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> BlockingDefinition(
        StepGate gate,
        string? poolKey = null)
    {
        var builder = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState(gate));
        if (poolKey is not null)
        {
            builder.WithPoolKey(poolKey);
        }

        return builder
            .Then(() => new BlockingStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(EventId eventId)
    {
        return new EventEnvelope
        {
            EventId = eventId,
            EventName = "Ready",
            CorrelationId = new CorrelationId("same"),
            Payload = null,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed record TestState(StepGate Gate);

    private sealed class BlockingStep : IStep<TestState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            await context.State.Gate.EnterAndWaitAsync(cancellationToken).ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    private sealed class StepGate
    {
        private readonly object gate = new();
        private readonly Queue<TaskCompletionSource> releases = [];
        private readonly Dictionary<int, TaskCompletionSource> enteredWaiters = [];
        private int activeCount;
        private int enteredCount;

        internal int EnteredCount => enteredCount;

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

        private async Task WaitForReleaseAsync(
            TaskCompletionSource release,
            CancellationToken cancellationToken)
        {
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
