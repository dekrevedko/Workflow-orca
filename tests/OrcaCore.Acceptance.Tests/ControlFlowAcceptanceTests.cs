using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ControlFlowAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-002")]
    public async Task If_ExecutesExactlyOneBranch_ThenContinues()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState { Approved = true })
            .If(
                current => current.Value.Approved,
                then => then.Then(context =>
                {
                    context.State.Values.Add("approved");
                    return ValueTask.CompletedTask;
                }),
                otherwise => otherwise.Then(context =>
                {
                    context.State.Values.Add("rejected");
                    return ValueTask.CompletedTask;
                }))
            .Then(context =>
            {
                context.State.Values.Add("continued");
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("if-branch"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Values.Should().Equal(["approved", "continued"]);
    }

    [Fact]
    [Trait("AC", "AC-003")]
    public async Task While_RunsThreeIterations_CompletesAfterFourthCheck()
    {
        var conditionChecks = 0;
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .While(
                current =>
                {
                    conditionChecks++;
                    return current.Value.Iterations < 3;
                },
                body => body.Then(context =>
                {
                    context.State.Iterations++;
                    return ValueTask.CompletedTask;
                }))
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("while-loop"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Iterations.Should().Be(3);
        conditionChecks.Should().Be(4);
    }

    public sealed class TestState
    {
        public bool Approved { get; init; }

        public int Iterations { get; set; }

        public List<string> Values { get; init; } = [];
    }
}
