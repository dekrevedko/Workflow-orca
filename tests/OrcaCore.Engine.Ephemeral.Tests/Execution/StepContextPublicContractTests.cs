using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class StepContextPublicContractTests
{
    private static readonly EventName Resume = EventName.Create("step-context-resume");
    private static readonly WorkflowEventContract<ResumePayload> TypedDynamicResume =
        WorkflowEventContract<ResumePayload>.Create(
            EventName.Create("step-context-dynamic-resume"),
            EventContractVersion.Initial);
    private static readonly CorrelationId ResumeCorrelation = CorrelationId.Create("step-context-correlation");

    [Fact]
    public async Task ResumedEventPayload_IsDetachedThroughTheFixedCodec()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ResumeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ResumeState([]))
            .Wait(WorkflowEventContract.Create(Resume, EventContractVersion.Initial), _ => ResumeCorrelation)
            .Then(context =>
            {
                var descriptor = WorkflowEventContract<ResumePayload>.Create(
                    context.ResumedEvent!.EventContract.EventName,
                    context.ResumedEvent.EventContract.Version);
                var first = context.ResumedEvent.GetPayload(descriptor);
                var second = context.ResumedEvent.GetPayload(descriptor);
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

        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
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
            .Wait(WorkflowEventContract.Create(Resume, EventContractVersion.Initial), _ => ResumeCorrelation)
            .Then(context =>
            {
                context.State.Observations.Add(
                    $"first:{(context.ResumedEvent is { } resumed ? resumed.GetPayload(
                        WorkflowEventContract<string>.Create(
                            resumed.EventContract.EventName,
                            resumed.EventContract.Version)) : "missing")}");
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
    public async Task WaitMatching_RequiresTheExactEventContractVersion()
    {
        using var provider = CreateProvider();
        var expected = WorkflowEventContract.Create(Resume, new EventContractVersion(2));
        var definition = Workflow.Ephemeral<ResumeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ResumeState([]))
            .Wait(expected, _ => ResumeCorrelation)
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "versioned-wait");
        var engine = provider.GetRequiredService<EphemeralWorkflowEngine>();

        var wrongVersion = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.EventEnvelope(
            EventId.Create("versioned-wait-v1"),
            WorkflowEventContract.Create(Resume, EventContractVersion.Initial),
            ResumeCorrelation,
            DateTimeOffset.UtcNow,
            ReadOnlyMemory<byte>.Empty);
        var stillWaiting = await engine.RaiseEventAsync<ResumeState>(
            instance.InstanceId,
            wrongVersion,
            TestContext.Current.CancellationToken);

        stillWaiting.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        stillWaiting.ActiveWaits.Should().ContainSingle()
            .Which.EventContract.Should().Be(expected);

        using var matchingProvider = CreateProvider();
        var matchingInstance = await StartAsync(matchingProvider, definition, "versioned-wait-match");
        var matchingEngine = matchingProvider.GetRequiredService<EphemeralWorkflowEngine>();
        var exactVersion = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.EventEnvelope(
            EventId.Create("versioned-wait-v2"),
            WorkflowEventContract.Create(Resume, new EventContractVersion(2)),
            ResumeCorrelation,
            DateTimeOffset.UtcNow,
            ReadOnlyMemory<byte>.Empty);
        var completed = await matchingEngine.RaiseEventAsync<ResumeState>(
            matchingInstance.InstanceId,
            exactVersion,
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(
            WorkflowInstanceStatus.Completed,
            completed.Failure?.Message);
    }

    [Fact]
    public async Task TypedDynamicWait_IsRegisteredAndResumedThroughItsDescriptor()
    {
        using var provider = CreateProvider();
        var definition = Workflow.Ephemeral<ResumeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ResumeState([]))
            .Then<TypedDynamicWaitStep>()
            .Then(context =>
            {
                var payload = context.ResumedEvent!.GetPayload(TypedDynamicResume);
                context.State.Observations.Add(payload.Name);
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var instance = await StartAsync(provider, definition, "typed-dynamic-wait");

        var waiting = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        waiting.Status.Should().Be(WorkflowInstanceStatus.Waiting, waiting.Failure?.Message);
        waiting.ActiveWaits.Should().ContainSingle()
            .Which.EventContract.Should().Be(TypedDynamicResume);

        var delivery = await provider.GetRequiredService<EphemeralWorkflowEventRouter>().RouteToInstanceAsync(
            instance.InstanceId,
            EphemeralTestEvent<ResumePayload>.Create(
                EventId.Create("typed-dynamic-wait-event"),
                TypedDynamicResume.EventName,
                ResumeCorrelation,
                new ResumePayload("typed-payload", []),
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<ResumeState>(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        state.Observations.Should().Equal("typed-payload");
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
                        WorkflowEventContract.Create(Resume, EventContractVersion.Initial),
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
            delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
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
        services.AddTransient<TypedDynamicWaitStep>();
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

    private static ValueTask<EphemeralEventRouteResult> DeliverAsync<TPayload>(
        ServiceProvider provider,
        WorkflowInstanceHandle instance,
        string eventId,
        TPayload payload,
        CorrelationId? correlationId = null) =>
        provider.GetRequiredService<EphemeralWorkflowEventRouter>().RouteToInstanceAsync(
            instance.InstanceId,
            EphemeralTestEvent<TPayload>.Create(
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

    private sealed class TypedDynamicWaitStep : IStep<ResumeState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ResumeState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent<ResumePayload>(TypedDynamicResume, ResumeCorrelation));
    }

    private sealed record ForEachRootState(List<string> Results);

    private sealed record ForEachItemState(int ExpectedIndex, string Value, List<int> SeenIndices);
}
