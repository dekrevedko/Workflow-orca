using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class PolicyAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-113")]
    public async Task StepTimeoutPolicy_TriggersConfiguredAction()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var step = new NeverCompletesStep();
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider(
            services => services.AddSingleton(step),
            clock.TimeProvider);
        var definition = global::OrcaCore.Workflow.Ephemeral<TimeoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TimeoutState())
            .Then<NeverCompletesStep>()
            .WithStepTimeout(TimeSpan.FromSeconds(30))
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var startTask = definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("step-timeout"),
            TestContext.Current.CancellationToken).AsTask();
        await step.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));

        var instance = (await startTask.WaitAsync(TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure.Should().NotBeNull();
        snapshot.Failure!.Message.Should().Contain("timed out");
    }

    [Fact]
    [Trait("AC", "AC-510")]
    public async Task RetryPolicy_IsBoundedAndIdempotent()
    {
        var failing = new AlwaysFailsStep();
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider(services =>
        {
            services.AddSingleton(failing);
            services.AddTransient<ShouldNotRunStep>();
        });
        var definition = global::OrcaCore.Workflow.Ephemeral<RetryState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new RetryState())
            .Then<AlwaysFailsStep>()
            .WithRetry(maxAttempts: 2)
            .Then<ShouldNotRunStep>()
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("bounded-retry"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<RetryState>(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        failing.InvocationCount.Should().Be(2);
        state.Attempts.Should().Be(0, "failed attempt-local state must not commit");
        state.AfterFailureStepRan.Should().BeFalse();
    }

    public sealed class TimeoutState;

    public sealed class RetryState
    {
        public int Attempts { get; set; }

        public bool AfterFailureStepRan { get; set; }
    }

    public sealed class NeverCompletesStep : IStep<TimeoutState>
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TimeoutState> context,
            CancellationToken cancellationToken)
        {
            var delay = Task.Delay(Timeout.InfiniteTimeSpan, context.TimeProvider, cancellationToken);
            Started.TrySetResult();
            await delay.ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    public sealed class AlwaysFailsStep : IStep<RetryState>
    {
        private int invocationCount;

        public int InvocationCount => Volatile.Read(ref invocationCount);

        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref invocationCount);
            context.State.Attempts++;
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowLifecycleException("permanent")));
        }
    }

    public sealed class ShouldNotRunStep : IStep<RetryState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            context.State.AfterFailureStepRan = true;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
