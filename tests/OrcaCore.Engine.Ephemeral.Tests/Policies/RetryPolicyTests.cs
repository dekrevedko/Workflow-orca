using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Policies;

public sealed class RetryPolicyTests
{

    [Fact]
    public async Task StepFactoryFailure_IsCapturedAndRetriedByStepPolicy()
    {
        var factoryAttempts = 0;
        var definition = global::OrcaCore.Workflow.Ephemeral<RetryState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new RetryState())
            .WithRetry(maxAttempts: 2)
            .Then(() => Interlocked.Increment(ref factoryAttempts) == 1
                ? throw new InvalidOperationException("factory unavailable")
                : new FlakyStep(new AttemptCounter(), failuresBeforeSuccess: 0))
            .End()
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, RetryState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        factoryAttempts.Should().Be(2);
    }

    [Fact]
    public async Task RetryPolicy_TransientFailures_RetriesUntilSuccess()
    {
        var attempts = new AttemptCounter();
        var engine = new EphemeralWorkflowEngine();
        var authoring = global::OrcaCore.Workflow.Ephemeral<RetryState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new RetryState());
        EnableDetachedAttemptState(authoring);
        var definition = authoring
            .WithRetry(maxAttempts: 3)
            .Then(() => new FlakyStep(attempts, failuresBeforeSuccess: 2))
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, RetryState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        attempts.Value.Should().Be(3);
        var committed = engine.Management.Instance(snapshot.InstanceId).GetState<RetryState>();
        committed.Attempts.Should().Be(3);
        committed.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task RetryPolicy_ExhaustedAttempts_FailsOnceWithoutDuplicateCommit()
    {
        var state = new RetryState();
        var attempts = new AttemptCounter();
        var engine = new EphemeralWorkflowEngine();
        var authoring = global::OrcaCore.Workflow.Ephemeral<RetryState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state);
        EnableDetachedAttemptState(authoring);
        var definition = authoring
            .WithRetry(maxAttempts: 2)
            .Then(() => new AlwaysFailsStep(attempts))
            .Then(() => new ShouldNotRunStep())
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, RetryState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        attempts.Value.Should().Be(2);
        state.Attempts.Should().Be(0, "failed attempt-local mutations must be discarded");
        state.Completed.Should().BeFalse();
        state.AfterFailureStepRan.Should().BeFalse();
    }

    private sealed class RetryState
    {
        public int Attempts { get; set; }

        public bool Completed { get; set; }

        public bool AfterFailureStepRan { get; set; }
    }

    private sealed class AttemptCounter
    {
        public int Value;
    }

    private static void EnableDetachedAttemptState(object builder)
    {
        var method = builder.GetType().BaseType!.GetMethod(
            "UseDetachedAttemptState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Detached attempt-state test seam was not found.");
        method.Invoke(builder, null);
    }

    private sealed class FlakyStep(AttemptCounter attempts, int failuresBeforeSuccess) : IStep<RetryState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts.Should().Be(0, "each retry starts from detached committed state");
            var attempt = Interlocked.Increment(ref attempts.Value);
            context.State.Attempts = 99;
            if (attempt <= failuresBeforeSuccess)
            {
                return ValueTask.FromResult<StepResult>(
                    new StepResult.Failed(new WorkflowLifecycleException("transient")));
            }

            context.ReplaceState(new RetryState { Attempts = attempt, Completed = true });
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class AlwaysFailsStep(AttemptCounter? attempts = null) : IStep<RetryState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            if (attempts is not null)
            {
                Interlocked.Increment(ref attempts.Value);
            }
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowLifecycleException("permanent")));
        }
    }

    private sealed class ShouldNotRunStep : IStep<RetryState>
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
