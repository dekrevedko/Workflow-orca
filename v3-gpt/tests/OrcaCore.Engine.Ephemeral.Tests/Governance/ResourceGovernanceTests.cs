using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Governance;

public sealed class ResourceGovernanceTests
{
    [Fact]
    public async Task StepConcurrencyLimit_AllowsOnlyConfiguredConcurrentSteps()
    {
        var gate = new StepGate();
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions
            {
                MaxConcurrentSteps = 1
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

        var secondEnteredBeforeRelease = await gate.WaitForEnteredCountAsync(
            2,
            TimeSpan.FromMilliseconds(75),
            TestContext.Current.CancellationToken);
        gate.ReleaseOne();
        await gate.WaitForEnteredCountAsync(2, TestContext.Current.CancellationToken);
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
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions
            {
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

        var secondEnteredBeforeRelease = await gate.WaitForEnteredCountAsync(
            2,
            TimeSpan.FromMilliseconds(75),
            TestContext.Current.CancellationToken);
        gate.ReleaseOne();
        await gate.WaitForEnteredCountAsync(2, TestContext.Current.CancellationToken);
        gate.ReleaseOne();
        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        secondEnteredBeforeRelease.Should().BeFalse();
        gate.MaxObservedConcurrent.Should().Be(1);
    }

    [Fact]
    public async Task Governance_DoesNotBreakPerInstanceSerialization()
    {
        var gate = new StepGate();
        var engine = new EphemeralWorkflowEngine(
            TimeProvider.System,
            new EphemeralWorkflowEngineOptions
            {
                MaxConcurrentSteps = 4
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

        var secondCompletedBeforeRelease = await CompletesWithinAsync(
            second,
            TimeSpan.FromMilliseconds(75),
            TestContext.Current.CancellationToken);
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

    private static async Task<bool> CompletesWithinAsync(
        Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
        return ReferenceEquals(winner, task);
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
        private readonly Queue<TaskCompletionSource> releases = [];
        private int activeCount;
        private int enteredCount;

        internal int EnteredCount => enteredCount;

        internal int MaxObservedConcurrent { get; private set; }

        internal Task EnterAndWaitAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource release;
            lock (releases)
            {
                enteredCount++;
                activeCount++;
                MaxObservedConcurrent = Math.Max(MaxObservedConcurrent, activeCount);
                release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                releases.Enqueue(release);
                Monitor.PulseAll(releases);
            }

            return WaitForReleaseAsync(release, cancellationToken);
        }

        internal void ReleaseOne()
        {
            TaskCompletionSource release;
            lock (releases)
            {
                release = releases.Dequeue();
                activeCount--;
            }

            release.SetResult();
        }

        internal async Task WaitForEnteredCountAsync(int count, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (releases)
                {
                    if (enteredCount >= count)
                    {
                        return;
                    }
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }

        internal async Task<bool> WaitForEnteredCountAsync(
            int count,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            try
            {
                await WaitForEnteredCountAsync(count, timeoutCancellation.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        private async Task WaitForReleaseAsync(
            TaskCompletionSource release,
            CancellationToken cancellationToken)
        {
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
