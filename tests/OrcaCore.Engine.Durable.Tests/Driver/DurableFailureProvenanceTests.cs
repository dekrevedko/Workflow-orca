using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableFailureProvenanceTests
{
    [Fact]
    [Trait("Requirement", "CR-014a")]
    [Trait("AC", "AC-029")]
    public async Task SelectedParallel_WhenAllOutcomesPreservesOrderedFailureProvenance()
    {
        var store = new InMemoryWorkflowProvider();
        WorkflowFailure? observedFailure = null;
        var definition = Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(value => new TestState { Value = value })
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        AuthoredBranchId.Create("failing"),
                        _ => new BranchState { Name = "failing" },
                        branch => branch
                            .Then<FailingBranchStep>()
                            .Return(state => state.Value.Name))
                    .Branch<BranchState>(
                        AuthoredBranchId.Create("succeeding"),
                        _ => new BranchState { Name = "succeeding" },
                        branch => branch.Return(state => state.Value.Name)))
            .WhenAllOutcomes((parent, outcomes) => new TestState
                {
                    Value = parent.Value.Value,
                    Log = outcomes.Select(outcome =>
                    {
                        if (outcome is BranchOutcome<string>.Failed failed)
                        {
                            observedFailure = failed.Failure;
                        }

                        return outcome switch
                        {
                            BranchOutcome<string>.Succeeded success =>
                                $"{success.BranchId.Value}:success:{success.Result}",
                            BranchOutcome<string>.Failed failure =>
                                $"{failure.BranchId.Value}:failure:{failure.Failure.Code}",
                            _ => throw new InvalidOperationException("Unknown branch outcome.")
                        };
                    }).ToList()
                })
            .End()
            .Build();
        var runtime = CreateRuntime(store);
        runtime.RegisterDefinition(definition);

        var completed = await runtime.StartOrGetAsync<string, TestState>(
            "failure-provenance",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint!.Value.Payload);
        var snapshot = (await store.GetAsync(
            completed.InstanceId,
            TestContext.Current.CancellationToken)).Value;

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        JsonSerializer.Deserialize<TestState>(envelope.StatePayload)!.Log.Should().Equal(
            "failing:failure:WF-LEGACY-LIFECYCLE",
            "succeeding:success:succeeding");
        observedFailure.Should().NotBeNull();
        observedFailure!.AuthoredLocation.Value.Should().Be(
            "workflow:$/n:00000001/parallel:00000000/n:00000000");
        observedFailure.Occurrence.Should().BeOfType<FailureOccurrence.Branch>()
            .Which.BranchId.Should().Be(AuthoredBranchId.Create("failing"));
    }

    private static DurableWorkflowRuntime CreateRuntime(InMemoryWorkflowProvider store)
    {
        var processor = new DurableCommandProcessor(store);
        return new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
    }

    private sealed class FailingBranchStep : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Failed(
                new WorkflowLifecycleException("branch failed")));
    }

    private sealed class TestState
    {
        public string Value { get; set; } = string.Empty;

        public List<string> Log { get; set; } = [];
    }

    private sealed class BranchState
    {
        public string Name { get; set; } = string.Empty;
    }
}
