using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Sagas;

public sealed class EphemeralSagaTests
{
    [Fact]
    [Trait("AC", "AC-401")]
    public async Task EphemeralSagaSuccess_CompletesWithoutCompensation()
    {
        var state = new SagaState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new SagaBuilder<SagaState>()
            .Init<string>(_ => state)
            .Then(() => new RecordingStep("reserve"))
            .CompensateBy(() => new RecordingStep("release"))
            .End());

        var snapshot = await engine.StartSagaAsync<string, SagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal("reserve");
    }

    [Fact]
    [Trait("AC", "AC-402")]
    [Trait("AC", "AC-403")]
    public async Task EphemeralSagaFailure_CompensatesCompletedActionsInReverseOrder()
    {
        var state = new SagaState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new SagaBuilder<SagaState>()
            .Init<string>(_ => state)
            .Then(() => new RecordingStep("reserve"))
            .CompensateBy(() => new RecordingStep("release"))
            .Then(() => new RecordingStep("authorize"))
            .CompensateBy(() => new RecordingStep("refund"))
            .Then(() => new FailingStep("payment failed"))
            .End());

        var snapshot = await engine.StartSagaAsync<string, SagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Compensated);
        state.Values.Should().Equal("reserve", "authorize", "refund", "release");
    }

    [Fact]
    [Trait("AC", "AC-404")]
    public async Task EphemeralSagaCompensationFailure_IsObservableInSnapshot()
    {
        var state = new SagaState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new SagaBuilder<SagaState>()
            .Init<string>(_ => state)
            .Then(() => new RecordingStep("reserve"))
            .CompensateBy(() => new FailingStep("release failed"))
            .Then(() => new FailingStep("payment failed"))
            .End());

        var snapshot = await engine.StartSagaAsync<string, SagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.CompensationFailed);
        snapshot.ErrorSummary.Should().Contain("release failed");
        engine.Management.All().Statistics().OldestActiveInstanceAge.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "AC-409")]
    public async Task EphemeralSagaRepeatedCompensation_IsIdempotentWithinProcess()
    {
        var state = new SagaState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new SagaBuilder<SagaState>()
            .Init<string>(_ => state)
            .Then(() => new RecordingStep("reserve"))
            .CompensateBy(() => new RecordingStep("release"))
            .Then(() => new FailingStep("payment failed"))
            .End());

        var snapshot = await engine.StartSagaAsync<string, SagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);
        var repeated = await engine.RequestSagaCompensationAsync<SagaState>(
            snapshot.InstanceId,
            TestContext.Current.CancellationToken);

        repeated.Status.Should().Be(WorkflowStatus.Compensated);
        state.Values.Should().Equal("reserve", "release");
    }

    [Fact]
    public async Task EphemeralSagaExplicitCompensation_AfterSuccess_CompensatesCompletedActions()
    {
        var state = new SagaState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new SagaBuilder<SagaState>()
            .Init<string>(_ => state)
            .Then(() => new RecordingStep("reserve"))
            .CompensateBy(() => new RecordingStep("release"))
            .End());
        var completed = await engine.StartSagaAsync<string, SagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);

        var compensated = await engine.RequestSagaCompensationAsync<SagaState>(
            completed.InstanceId,
            TestContext.Current.CancellationToken);

        compensated.Status.Should().Be(WorkflowStatus.Compensated);
        state.Values.Should().Equal("reserve", "release");
        engine.Management.All().Statistics().OldestActiveInstanceAge.Should().BeNull();
    }

    [Fact]
    public async Task EphemeralSagaCancelledCompensation_CanBeRetried()
    {
        var state = new SagaState();
        var compensation = new CancelOnceStep("release");
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new SagaBuilder<SagaState>()
            .Init<string>(_ => state)
            .Then(() => new RecordingStep("reserve"))
            .CompensateBy(() => compensation)
            .End());
        var completed = await engine.StartSagaAsync<string, SagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);

        var firstAttempt = () => engine.RequestSagaCompensationAsync<SagaState>(
            completed.InstanceId,
            TestContext.Current.CancellationToken);

        await firstAttempt.Should().ThrowAsync<OperationCanceledException>();
        var retried = await engine.RequestSagaCompensationAsync<SagaState>(
            completed.InstanceId,
            TestContext.Current.CancellationToken);

        retried.Status.Should().Be(WorkflowStatus.Compensated);
        state.Values.Should().Equal("reserve", "release");
    }

    [Fact]
    public async Task EphemeralSagaScopedCompensation_OnlyCompensatesRequestedScope()
    {
        var state = new SagaState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new SagaBuilder<SagaState>()
            .Init<string>(_ => state)
            .CompensationScope("inventory", scope => scope
                .Then(() => new RecordingStep("reserve"))
                .CompensateBy(() => new RecordingStep("release")))
            .CompensationScope("payment", scope => scope
                .Then(() => new RecordingStep("authorize"))
                .CompensateBy(() => new RecordingStep("refund")))
            .End());
        var completed = await engine.StartSagaAsync<string, SagaState>(
            definition,
            "start",
            TestContext.Current.CancellationToken);

        var compensated = await engine.RequestSagaCompensationAsync<SagaState>(
            completed.InstanceId,
            "payment",
            TestContext.Current.CancellationToken);

        compensated.Status.Should().Be(WorkflowStatus.Compensated);
        state.Values.Should().Equal("reserve", "authorize", "refund");
    }

    [Fact]
    public void EphemeralSagaPublicDocs_StateInProcessOnlyLimits()
    {
        var source = File.ReadAllText(FindSourceFile("OrcaCore.Engine.Ephemeral", "EphemeralWorkflowEngine.cs"));

        source.Should().Contain("in-process only");
        source.Should().Contain("no durable recovery");
        source.Should().Contain("no durable compensation audit");
        source.Should().Contain("no post-restart operator remediation");
    }

    private static SagaDefinition<SagaState> Definition(SagaBuilder<SagaState> builder)
    {
        return builder.Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static string FindSourceFile(string projectName, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", projectName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(fileName);
    }

    private sealed class SagaState
    {
        public List<string> Values { get; } = [];
    }

    private sealed class RecordingStep(string value) : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<SagaState> context,
            CancellationToken cancellationToken)
        {
            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingStep(string message) : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<SagaState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowLifecycleException(message)));
        }
    }

    private sealed class CancelOnceStep(string value) : IStep<SagaState>
    {
        private int attempts;

        public ValueTask<StepResult> ExecuteAsync(
            StepContext<SagaState> context,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
