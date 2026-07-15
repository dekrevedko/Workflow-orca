using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Policies;

public sealed class RetryPolicyTests
{
    [Fact]
    public void DefinitionRetry_IsRejectedInsteadOfSilentlyIgnored()
    {
        var definition = new WorkflowBuilder<RetryState>()
            .Init<string>(_ => new RetryState())
            .WithDefinitionRetry(maxAttempts: 2)
            .Then(() => new AlwaysFailsStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        var engine = new EphemeralWorkflowEngine();

        var act = () => engine.RegisterDefinition(definition);

        act.Should().Throw<WorkflowDefinitionException>()
            .WithMessage("*Definition-level retry*individual steps*");
    }

    [Fact]
    public async Task StepFactoryFailure_IsCapturedAndRetriedByStepPolicy()
    {
        var factoryAttempts = 0;
        var definition = Workflow.Ephemeral<RetryState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new RetryState())
            .WithRetry(maxAttempts: 2)
            .Then(() => Interlocked.Increment(ref factoryAttempts) == 1
                ? throw new InvalidOperationException("factory unavailable")
                : new FlakyStep(failuresBeforeSuccess: 0))
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
        var state = new RetryState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Workflow.Ephemeral<RetryState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .WithRetry(maxAttempts: 3)
            .Then(() => new FlakyStep(failuresBeforeSuccess: 2))
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, RetryState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Attempts.Should().Be(3);
        state.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task RetryPolicy_ExhaustedAttempts_FailsOnceWithoutDuplicateCommit()
    {
        var state = new RetryState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Workflow.Ephemeral<RetryState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .WithRetry(maxAttempts: 2)
            .Then(() => new AlwaysFailsStep())
            .Then(() => new ShouldNotRunStep())
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, RetryState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        state.Attempts.Should().Be(2);
        state.Completed.Should().BeFalse();
        state.AfterFailureStepRan.Should().BeFalse();
    }

    private sealed class RetryState
    {
        public int Attempts { get; set; }

        public bool Completed { get; set; }

        public bool AfterFailureStepRan { get; set; }
    }

    private sealed class FlakyStep(int failuresBeforeSuccess) : IStep<RetryState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            if (context.State.Attempts <= failuresBeforeSuccess)
            {
                return ValueTask.FromResult<StepResult>(
                    new StepResult.Failed(new WorkflowDefinitionException("transient")));
            }

            context.State.Completed = true;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class AlwaysFailsStep : IStep<RetryState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowDefinitionException("permanent")));
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
