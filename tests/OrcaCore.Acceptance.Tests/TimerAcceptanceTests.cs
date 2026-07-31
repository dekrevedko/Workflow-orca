using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class TimerAcceptanceTests
{
    private static readonly CorrelationId Correlation = CorrelationId.Create("order-123");

    [Fact]
    [Trait("AC", "AC-111")]
    public async Task EphemeralDelay_CompletesAfterDueTime()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider(timeProvider: clock.TimeProvider);
        var engine = provider.GetRequiredService<EphemeralWorkflowEngine>();
        var definition = global::OrcaCore.Workflow.Ephemeral<TimerState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TimerState())
            .Delay(TimeSpan.FromSeconds(30))
            .Then(context =>
            {
                context.State.Sink.Add("continued");
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("ephemeral-delay"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var started = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));

        await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);
        var completed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TimerState>(TestContext.Current.CancellationToken);

        started.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Sink.Should().Equal(["continued"]);
    }

    public sealed record TimerState
    {
        public List<string> Sink { get; init; } = [];
    }

}
