using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ForEachAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-601")]
    public async Task ForEach_PublicItemProjectionCreatesExpectedIsolatedItems()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState(Enumerable.Range(1, 23).ToArray()))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                ForEachOptions.Create(maxItems: 23),
                item => new ItemState(item.Index, item.Item),
                body => body.Return(item => item.Value.Item))
            .WhenAll((parent, results) => parent.Value with
                {
                    BodyRuns = results.Count,
                    Results = results.OrderBy(result => result.Index).Select(result => result.Result).ToArray()
                })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("foreach-isolated-items"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.BodyRuns.Should().Be(23);
        state.Results.Should().Equal(Enumerable.Range(1, 23));
    }

    [Fact]
    [Trait("AC", "AC-602")]
    public async Task ForEach_WhenAllCompletesParent()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = WaitingDefinition(maxConcurrency: null);
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("foreach-when-all"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var events = provider.GetRequiredService<IWorkflowEventClient>();

        foreach (var index in Enumerable.Range(0, 3))
        {
            await RaiseItemAsync(events, instance, index);
        }

        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal(0, 1, 2);
        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-603")]
    public async Task ForEach_HonorsMaxConcurrency()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = WaitingDefinition(maxConcurrency: 2, itemCount: 5);
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("foreach-max-concurrency"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        snapshot.ActiveWaits.Should().HaveCount(2);
    }

    [Fact]
    [Trait("AC", "AC-602")]
    public async Task ForEach_WaitAllThenFailIsObservable()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider(
            services => services.AddTransient<FailSecondItemStep>());
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([1, 2, 3]))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                ForEachOptions.Create(maxItems: 3),
                item => new ItemState(item.Index, item.Item),
                body => body
                    .Then<FailSecondItemStep>()
                    .Wait(EventName.Create("ItemDone"), item => CorrelationId.Create($"item-{item.Value.Index}"))
                    .Return(item => item.Value.Index))
            .WhenAll(MergeResults)
            .Then(context =>
            {
                context.State.ContinuationCount++;
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("foreach-wait-all-failure"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        snapshot.ActiveWaits.Should().HaveCount(2);
        await RaiseItemAsync(events, instance, 0);
        await RaiseItemAsync(events, instance, 2);

        snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.ActiveWaits.Should().BeEmpty();
        state.ContinuationCount.Should().Be(0);
    }

    [Fact]
    [Trait("AC", "AC-604")]
    public async Task ForEach_WhenAllOutcomesMergesOrderedSuccessAndFailureValues()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider(
            services => services.AddTransient<FailSecondItemStep>());
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState([1, 2, 3]))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                ForEachOptions.Create(maxItems: 3),
                item => new ItemState(item.Index, item.Item),
                body => body
                    .Then<FailSecondItemStep>()
                    .Return(item => item.Value.Index))
            .WhenAllOutcomes((parent, outcomes) => parent.Value with
            {
                Results = outcomes.Select(outcome => outcome switch
                {
                    ForEachItemOutcome<int>.Succeeded succeeded => succeeded.Result,
                    ForEachItemOutcome<int>.Failed => -1,
                    _ => throw new InvalidOperationException("Unexpected item outcome.")
                }).ToArray()
            })
            .Then(context =>
            {
                context.State.ContinuationCount++;
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("foreach-outcomes"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal(0, -1, 2);
        state.ContinuationCount.Should().Be(1);
    }

    private static EphemeralWorkflowDefinition<string> WaitingDefinition(
        int? maxConcurrency,
        int itemCount = 3)
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState(Enumerable.Range(1, itemCount).ToArray()))
            .ForEach<int, ItemState, int>(
                parent => parent.Value.Items,
                ForEachOptions.Create(itemCount, maxConcurrency),
                item => new ItemState(item.Index, item.Item),
                body => body
                    .Wait(EventName.Create("ItemDone"), item => CorrelationId.Create($"item-{item.Value.Index}"))
                    .Return(item => item.Value.Index))
            .WhenAll(MergeResults)
            .Then(context =>
            {
                context.State.ContinuationCount++;
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
    }

    private static TestState MergeResults(
        ReadOnlyStateSnapshot<TestState> parent,
        IReadOnlyList<global::OrcaCore.ForEachItemResult<int>> results)
    {
        return parent.Value with
        {
            Results = results
                .OrderBy(result => result.Index)
                .Select(result => result.Result)
                .ToArray()
        };
    }

    private static async Task RaiseItemAsync(
        IWorkflowEventClient events,
        WorkflowInstanceHandle instance,
        int index)
    {
        var delivery = await events.DeliverToInstanceAsync(
            instance.InstanceId,
            WorkflowEvent.Create(
                EventId.Create(Guid.CreateVersion7().ToString()),
                EventName.Create("ItemDone"),
                CorrelationId.Create($"item-{index}"),
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);
        delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
    }

    private sealed record TestState(int[] Items)
    {
        public int BodyRuns { get; init; }

        public IReadOnlyList<int> Results { get; init; } = [];

        public int ContinuationCount { get; set; }
    }

    public sealed record ItemState(int Index, int Item);

    public sealed class FailSecondItemStep : IStep<ItemState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ItemState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(context.State.Index == 1
                ? new StepResult.Failed(new WorkflowLifecycleException("item failed"))
                : new StepResult.Completed());
        }
    }

}
