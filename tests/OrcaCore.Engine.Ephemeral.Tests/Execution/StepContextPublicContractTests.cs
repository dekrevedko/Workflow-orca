using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class StepContextPublicContractTests
{
    private static readonly EventName Resume = EventName.Create("step-context-resume");
    private static readonly CorrelationId ResumeCorrelation = CorrelationId.Create("step-context-correlation");

    [Fact]
    public async Task ResumedEventPayload_IsDetachedThroughTheFixedCodec()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ResumeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ResumeState([]))
            .Wait(Resume, _ => ResumeCorrelation)
            .Then(context =>
            {
                var first = context.ResumedEvent!.GetPayload<ResumePayload>();
                var second = context.ResumedEvent.GetPayload<ResumePayload>();
                first.Values.Add("changed-in-step");
                context.State.Observations.Add($"{first.Name}:{string.Join(',', first.Values)}");
                context.State.Observations.Add($"detached:{!ReferenceEquals(first, second)}");
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "detached-resume");
        var callerPayload = new ResumePayload("authored", ["original"]);

        var delivery = await DeliverAsync(
            provider,
            instance,
            "detached-resume-event",
            callerPayload);
        var state = await instance.GetStateAsync<ResumeState>(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        state.Observations.Should().Equal(
            "authored:original,changed-in-step",
            "detached:True");
        callerPayload.Values.Should().Equal("original");
    }

    [Fact]
    public async Task ResumedEvent_IsVisibleOnlyToTheFirstResumedStep()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ResumeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ResumeState([]))
            .Wait(Resume, _ => ResumeCorrelation)
            .Then(context =>
            {
                context.State.Observations.Add(
                    $"first:{context.ResumedEvent?.GetPayload<string>() ?? "missing"}");
                return ValueTask.CompletedTask;
            })
            .Then(context =>
            {
                context.State.Observations.Add(
                    context.ResumedEvent is null ? "second:null" : "second:present");
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "single-resume-step");

        _ = await DeliverAsync(provider, instance, "single-resume-event", "payload");
        var state = await instance.GetStateAsync<ResumeState>(TestContext.Current.CancellationToken);

        state.Observations.Should().Equal("first:payload", "second:null");
    }

    [Fact]
    public async Task ForEachItemIndex_RemainsStableAcrossInterleavedConcurrentResumes()
    {
        using var provider = CreateProvider(maxConcurrentPaths: 4);
        var definition = Workflow.Ephemeral<ForEachRootState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ForEachRootState([]))
            .ForEach<string, ForEachItemState, string>(
                _ => ["zero", "one", "two", "three"],
                ForEachOptions.Create(maxItems: 4, maxConcurrency: 4),
                item => new ForEachItemState(item.Index, item.Item, []),
                body => body
                    .Then(context =>
                    {
                        context.State.SeenIndices.Add(context.ForEachItem?.Index ?? -1);
                        return ValueTask.CompletedTask;
                    })
                    .Wait(
                        Resume,
                        state => CorrelationId.Create($"item-{state.Value.ExpectedIndex}"))
                    .Then(context =>
                    {
                        context.State.SeenIndices.Add(context.ForEachItem?.Index ?? -1);
                        return ValueTask.CompletedTask;
                    })
                    .Return(state =>
                        $"{state.Value.ExpectedIndex}:{state.Value.Value}:" +
                        string.Join(',', state.Value.SeenIndices)))
            .WhenAll((parent, outcomes) => parent.Value with
            {
                Results = outcomes.Select(outcome => outcome.Result).ToList()
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "foreach-interleaved-index");
        var waiting = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        waiting.ActiveWaits.Select(wait => wait.CorrelationId.Value).Should().BeEquivalentTo(
            "item-0",
            "item-1",
            "item-2",
            "item-3");
        foreach (var index in new[] { 2, 0, 3, 1 })
        {
            var delivery = await DeliverAsync(
                provider,
                instance,
                $"foreach-index-{index}",
                $"payload-{index}",
                CorrelationId.Create($"item-{index}"));
            delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        }

        var completed = await WaitForStatusAsync(instance, WorkflowInstanceStatus.Completed);
        var state = await instance.GetStateAsync<ForEachRootState>(TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Results.Should().Equal(
            "0:zero:0,0",
            "1:one:1,1",
            "2:two:2,2",
            "3:three:3,3");
    }

    [Fact]
    public async Task ForEachItem_IsNullOutsideAnItemBody()
    {
        using var provider = CreateProvider(maxConcurrentPaths: 2);
        var definition = Workflow.Ephemeral<ForEachRootState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ForEachRootState([]))
            .Then(context =>
            {
                context.State.Results.Add(context.ForEachItem is null ? "root-before:null" : "root-before:set");
                return ValueTask.CompletedTask;
            })
            .ForEach<string, ForEachItemState, string>(
                _ => ["zero", "one"],
                ForEachOptions.Create(maxItems: 2, maxConcurrency: 2),
                item => new ForEachItemState(item.Index, item.Item, []),
                body => body
                    .Then(context =>
                    {
                        context.State.SeenIndices.Add(context.ForEachItem?.Index ?? -1);
                        return ValueTask.CompletedTask;
                    })
                    .Return(state => $"item-{state.Value.ExpectedIndex}:{state.Value.SeenIndices.Single()}"))
            .WhenAll((parent, outcomes) => parent.Value with
            {
                Results = parent.Value.Results.Concat(outcomes.Select(outcome => outcome.Result)).ToList()
            })
            .Then(context =>
            {
                context.State.Results.Add(context.ForEachItem is null ? "root-after:null" : "root-after:set");
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "foreach-root-context");
        var state = await instance.GetStateAsync<ForEachRootState>(TestContext.Current.CancellationToken);

        state.Results.Should().Equal(
            "root-before:null",
            "item-0:0",
            "item-1:1",
            "root-after:null");
    }

    private static ServiceProvider CreateProvider(int maxConcurrentPaths = 4)
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = maxConcurrentPaths,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services.BuildServiceProvider();
    }

    private static async Task<WorkflowInstanceHandle> StartAsync(
        ServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition,
        string key)
    {
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        return (await handle.StartOrGetAsync(
            key,
            StartIdempotencyKey.Create(key),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
    }

    private static ValueTask<EventDeliveryResult> DeliverAsync<TPayload>(
        ServiceProvider provider,
        WorkflowInstanceHandle instance,
        string eventId,
        TPayload payload,
        CorrelationId? correlationId = null) =>
        provider.GetRequiredService<IWorkflowEventClient>().DeliverToInstanceAsync(
            instance.InstanceId,
            WorkflowEvent<TPayload>.Create(
                EventId.Create(eventId),
                Resume,
                correlationId ?? ResumeCorrelation,
                payload,
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

    private static async Task<WorkflowInstanceSnapshot> WaitForStatusAsync(
        WorkflowInstanceHandle instance,
        WorkflowInstanceStatus status)
    {
        while (true)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
            if (snapshot.Status == status)
            {
                return snapshot;
            }

            await Task.Yield();
        }
    }

    private sealed record ResumeState(List<string> Observations);

    private sealed record ResumePayload(string Name, List<string> Values);

    private sealed record ForEachRootState(List<string> Results);

    private sealed record ForEachItemState(int ExpectedIndex, string Value, List<int> SeenIndices);
}
