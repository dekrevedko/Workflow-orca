using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Tests.Support;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class StepContextDurabilityTests
{
    private static readonly WorkflowEventContract<ResumePayload> TypedDynamicResume =
        WorkflowEventContract<ResumePayload>.Create(
            EventName.Create("typed-dynamic-resume"),
            EventContractVersion.Initial);
    private static readonly WorkflowEventContract<ResumePayload> TypedStructuralResume =
        WorkflowEventContract<ResumePayload>.Create(
            EventName.Create("resume"),
            EventContractVersion.Initial);

    [Fact]
    public async Task ResumedEventPayload_IsDetachedAcrossCheckpointAndVisibleOnlyToFirstStep()
    {
        using var store = new DurableTestStore();
        var recorder = new ResumeRecorder();
        var definition = Workflow.Durable<ResumeState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new ResumeState())
            .Wait(
                TypedStructuralResume,
                _ => CorrelationId.Create("resume-context"))
            .Then<CaptureFirstResumeStep>()
            .Then<CaptureSecondResumeStep>()
            .End()
            .Build();

        using var firstServices = StepServices(recorder);
        var first = CreateRuntime(store, firstServices);
        first.RegisterDefinition(definition);
        var started = await first.StartOrGetAsync<string, ResumeState>(
            "step-context-resume",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        var payload = new ResumePayload { Value = "persisted" };
        var delivered = await first.RaiseFacadeEventAsync(
            started.InstanceId,
            EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create("resume"),
            CorrelationId.Create("resume-context"),
            DateTimeOffset.UtcNow,
            payload,
            TestContext.Current.CancellationToken,
            driveAfterDelivery: false);
        delivered.Outcome.Should().Be(DurableCommandOutcome.Committed);

        payload.Value = "mutated-after-commit";
        using var replacementServices = StepServices(recorder);
        var replacement = CreateRuntime(store, replacementServices);
        replacement.RegisterDefinition(definition);
        _ = await replacement.StartOrGetAsync<string, ResumeState>(
            "step-context-resume",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        recorder.Observations.Should().Equal(
            "first:persisted",
            "second:none");
    }

    [Fact]
    public async Task ForEachItemIndex_SurvivesCheckpointAndReplacementHosts_AndIsNullOutsideBody()
    {
        using var store = new DurableTestStore();
        var recorder = new ForEachRecorder();
        var definition = Workflow.Durable<ForEachRootState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new ForEachRootState())
            .Then<CaptureOutsideForEachStep>()
            .ForEach<int, ForEachItemState, int>(
                _ => [10, 20, 30],
                ForEachOptions.Create(maxItems: 3, maxConcurrency: 3),
                item => new ForEachItemState
                {
                    Value = item.Item,
                    Correlation = $"item-{item.Index}"
                },
                body => body
                    .Wait(
                        WorkflowEventContract.Create(EventName.Create("item-ready"), EventContractVersion.Initial),
                        state => CorrelationId.Create(state.Value.Correlation))
                    .Then<CaptureForEachItemStep>()
                    .Return(state => state.Value.Value))
            .WhenAll((parent, _) => parent.Value)
            .Then<CaptureOutsideForEachStep>()
            .End()
            .Build();

        using (var firstServices = StepServices(recorder))
        {
            var first = CreateRuntime(store, firstServices);
            first.RegisterDefinition(definition);
            _ = await first.StartOrGetAsync<string, ForEachRootState>(
                "step-context-foreach",
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                TestContext.Current.CancellationToken);
        }

        var start = await store.GetStartedAsync(
            "step-context-foreach",
            TestContext.Current.CancellationToken);
        start.HasValue.Should().BeTrue();
        var instanceId = start.Value.InstanceId;

        foreach (var index in new[] { 2, 0, 1 })
        {
            using var replacementServices = StepServices(recorder);
            var replacement = CreateRuntime(store, replacementServices);
            replacement.RegisterDefinition(definition);
            var delivered = await replacement.RaiseEventAsync(
                instanceId,
                "item-ready",
                CorrelationId.Create($"item-{index}"),
                cancellationToken: TestContext.Current.CancellationToken);
            delivered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        }

        recorder.ItemContexts.OrderBy(item => item.Value).Should().Equal(
            new ForEachObservation(10, 0),
            new ForEachObservation(20, 1),
            new ForEachObservation(30, 2));
        recorder.OutsideContexts.Should().Equal("none", "none");
    }

    [Fact]
    public async Task TypedDynamicWait_SurvivesDurableRegistrationAndResume()
    {
        using var store = new DurableTestStore();
        var recorder = new ResumeRecorder();
        var definition = Workflow.Durable<ResumeState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ResumeState())
            .Then<TypedDynamicWaitStep>()
            .Then<CaptureFirstResumeStep>()
            .End()
            .Build();

        using var services = StepServices(recorder);
        var runtime = CreateRuntime(store, services);
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, ResumeState>(
            "typed-dynamic-wait",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            TestContext.Current.CancellationToken);

        var checkpoint = await store.LoadCheckpointAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);
        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.RuntimeState.ActiveWaits.Should().ContainSingle(wait =>
            wait.EventName == TypedDynamicResume.EventName.Value &&
            wait.EventContractVersion == TypedDynamicResume.Version.Value);

        var delivered = await runtime.RaiseFacadeEventAsync(
            started.InstanceId,
            EventId.Create("typed-dynamic-wait-event"),
            TypedDynamicResume.EventName,
            CorrelationId.Create("typed-dynamic-correlation"),
            DateTimeOffset.UtcNow,
            new ResumePayload { Value = "typed" },
            TestContext.Current.CancellationToken);

        delivered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        recorder.Observations.Should().Equal("first:typed");
    }

    private static ServiceProvider StepServices(object recorder)
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder.GetType(), recorder);
        services.AddTransient<CaptureFirstResumeStep>();
        services.AddTransient<CaptureSecondResumeStep>();
        services.AddTransient<TypedDynamicWaitStep>();
        services.AddTransient<CaptureOutsideForEachStep>();
        services.AddTransient<CaptureForEachItemStep>();
        return services.BuildServiceProvider();
    }

    private static DurableWorkflowRuntime CreateRuntime(
        DurableTestStore store,
        IServiceProvider services) =>
        new(
            new DurableCommandProcessor(store),
            new DurableDefinitionRegistry(services),
            TimeProvider.System,
            projectionStore: store);

    private sealed class ResumeState;

    private sealed class ResumePayload
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class ResumeRecorder
    {
        internal List<string> Observations { get; } = [];
    }

    private sealed class TypedDynamicWaitStep : IStep<ResumeState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ResumeState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent<ResumePayload>(
                    TypedDynamicResume,
                    CorrelationId.Create("typed-dynamic-correlation")));
    }

    private sealed class CaptureFirstResumeStep(ResumeRecorder recorder) : IStep<ResumeState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ResumeState> context,
            CancellationToken cancellationToken)
        {
            var value = context.ResumedEvent is { } resumed
                ? resumed.GetPayload(WorkflowEventContract<ResumePayload>.Create(
                    resumed.EventContract.EventName,
                    resumed.EventContract.Version)).Value
                : "none";
            recorder.Observations.Add($"first:{value}");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CaptureSecondResumeStep(ResumeRecorder recorder) : IStep<ResumeState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ResumeState> context,
            CancellationToken cancellationToken)
        {
            recorder.Observations.Add(context.ResumedEvent is null ? "second:none" : "second:event");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ForEachRootState;

    private sealed class ForEachItemState
    {
        public int Value { get; set; }
        public string Correlation { get; set; } = string.Empty;
    }

    private sealed record ForEachObservation(int Value, int Index);

    private sealed class ForEachRecorder
    {
        internal ConcurrentQueue<ForEachObservation> ItemContexts { get; } = new();
        internal ConcurrentQueue<string> OutsideContexts { get; } = new();
    }

    private sealed class CaptureOutsideForEachStep(ForEachRecorder recorder) : IStep<ForEachRootState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ForEachRootState> context,
            CancellationToken cancellationToken)
        {
            recorder.OutsideContexts.Enqueue(
                context.ForEachItem is null ? "none" : context.ForEachItem.Index.ToString());
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CaptureForEachItemStep(ForEachRecorder recorder) : IStep<ForEachItemState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<ForEachItemState> context,
            CancellationToken cancellationToken)
        {
            context.ForEachItem.Should().NotBeNull();
            recorder.ItemContexts.Enqueue(
                new ForEachObservation(context.State.Value, context.ForEachItem!.Index));
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
