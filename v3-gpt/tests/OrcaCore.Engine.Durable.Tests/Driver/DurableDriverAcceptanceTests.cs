using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

/// <summary>
/// DR-P1 gate: the durable driver executes registered definitions end to end on the
/// in-memory provider without any kernel commands issued by the tests.
/// </summary>
public sealed class DurableDriverAcceptanceTests
{
    private sealed class OrderState
    {
        public string OrderId { get; set; } = string.Empty;

        public int Value { get; set; }

        public List<string> Log { get; set; } = [];

        public List<int> CompletedChunks { get; set; } = [];
    }

    private sealed record HostHandle(
        DurableWorkflowRuntime Runtime,
        InMemoryWorkflowProvider Store,
        DurableCommandProcessor Processor);

    private static HostHandle CreateHost(InMemoryWorkflowProvider? store = null)
    {
        store ??= new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        return new HostHandle(runtime, store, processor);
    }

    private static async Task<WorkflowInstanceSnapshot> SnapshotAsync(HostHandle host, InstanceId instanceId)
    {
        var snapshots = await host.Store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);
        return snapshots.Should().ContainSingle().Subject;
    }

    private static async Task<OrderState> FinalStateAsync(HostHandle host, InstanceId instanceId)
    {
        var checkpoint = await host.Store.LoadCheckpointAsync(instanceId, TestContext.Current.CancellationToken);
        checkpoint.HasValue.Should().BeTrue("a driver commit must have produced a checkpoint");
        checkpoint.Value.ContentType.Should().Be(DurableExecutionEnvelope.ContentType);
        var envelope = DurableExecutionEnvelope.Deserialize(checkpoint.Value.Payload);
        return JsonSerializer.Deserialize<OrderState>(envelope.StatePayload)!;
    }

    private sealed class LogStep(string name) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Log.Add(name);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static WorkflowDefinition<OrderState> StepWaitStepEndDefinition(
        DefinitionId definitionId,
        DefinitionVersion version)
    {
        return new WorkflowBuilder<OrderState>()
            .Init<string>(orderId => new OrderState { OrderId = orderId })
            .Then(new LogStep("prepare"))
            .Wait("Approved", state => new CorrelationId(state.OrderId))
            .Then(new LogStep("ship"))
            .End("shipped")
            .Build(definitionId, version);
    }

    [Fact]
    [Trait("AC", "DR-AC-001")]
    public async Task RegisteredDefinition_StartDeliverComplete_WithoutKernelCommands()
    {
        var host = CreateHost();
        var definition = StepWaitStepEndDefinition(DefinitionId.New(), DefinitionVersion.Initial);
        host.Runtime.RegisterDefinition(definition);

        var start = await host.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-001",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "order-1",
            TestContext.Current.CancellationToken);
        var suspended = await SnapshotAsync(host, start.InstanceId);
        suspended.Status.Should().Be(WorkflowStatus.Waiting);

        await host.Runtime.RaiseEventAsync(
            start.InstanceId,
            "Approved",
            new CorrelationId("order-1"),
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = await SnapshotAsync(host, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.EndOutcomeName.Should().Be("shipped");
        var state = await FinalStateAsync(host, start.InstanceId);
        state.Log.Should().Equal("prepare", "ship");
    }

    [Fact]
    [Trait("AC", "DR-AC-002")]
    public async Task HostReplacedBetweenWaitAndEvent_CompletesIdentically()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();

        var hostA = CreateHost(store);
        hostA.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-002",
            definitionId,
            DefinitionVersion.Initial,
            "order-2",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        // Host process replaced: a fresh processor and runtime against the same store, with no
        // in-memory references carried over (DU-013).
        var hostB = CreateHost(store);
        hostB.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        await hostB.Runtime.RaiseEventAsync(
            start.InstanceId,
            "Approved",
            new CorrelationId("order-2"),
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = await SnapshotAsync(hostB, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.EndOutcomeName.Should().Be("shipped");
        var state = await FinalStateAsync(hostB, start.InstanceId);
        state.Log.Should().Equal("prepare", "ship");
    }

    private sealed class ChunkedStep : IStep<OrderState>
    {
        internal static readonly Dictionary<string, int> Executions = [];
        internal static bool CrashOnChunk3 { get; set; }

        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            var chunk = context.State.CompletedChunks.Count + 1;
            var key = $"{context.State.OrderId}:{chunk}";
            Executions[key] = Executions.GetValueOrDefault(key) + 1;
            if (chunk == 3 && CrashOnChunk3)
            {
                CrashOnChunk3 = false;
                throw new OperationCanceledException("Simulated host crash mid-chunk.");
            }

            context.State.CompletedChunks.Add(chunk);
            return ValueTask.FromResult<StepResult>(
                chunk < 3 ? new StepResult.Yield() : new StepResult.Completed());
        }
    }

    [Fact]
    [Trait("AC", "DR-AC-005")]
    public async Task YieldChunkedStep_CrashMidChunk_ResumesFromLastCommittedChunk()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var definition = new WorkflowBuilder<OrderState>()
            .Init<string>(orderId => new OrderState { OrderId = orderId })
            .Then(() => new ChunkedStep())
            .End("chunked")
            .Build(definitionId, DefinitionVersion.Initial);

        ChunkedStep.CrashOnChunk3 = true;
        var hostA = CreateHost(store);
        hostA.Runtime.RegisterDefinition(definition);
        var crash = async () => await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-005",
            definitionId,
            DefinitionVersion.Initial,
            "order-5",
            TestContext.Current.CancellationToken);
        await crash.Should().ThrowAsync<OperationCanceledException>();

        var hostB = CreateHost(store);
        hostB.Runtime.RegisterDefinition(definition);
        var start = await hostB.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-005",
            definitionId,
            DefinitionVersion.Initial,
            "order-5",
            TestContext.Current.CancellationToken);

        start.Created.Should().BeFalse();
        var completed = await SnapshotAsync(hostB, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        var state = await FinalStateAsync(hostB, start.InstanceId);
        state.CompletedChunks.Should().Equal(1, 2, 3);
        ChunkedStep.Executions["order-5:1"].Should().Be(1, "committed chunks are never re-executed");
        ChunkedStep.Executions["order-5:2"].Should().Be(1, "committed chunks are never re-executed");
        ChunkedStep.Executions["order-5:3"].Should().Be(2, "the uncommitted chunk re-runs after the crash");
    }

    [Fact]
    [Trait("AC", "DR-AC-006")]
    public async Task NestedParallelInsideIf_RestartResumesCorrectBranchOnly()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return new WorkflowBuilder<OrderState>()
                .Init<string>(orderId => new OrderState { OrderId = orderId })
                .If(
                    state => true,
                    then => then.Parallel(
                        ("approval", branch => branch
                            .Then(new LogStep("a1"))
                            .Wait("EventA", state => new CorrelationId(state.OrderId))
                            .Then(new LogStep("a2"))),
                        ("audit", branch => branch
                            .Then(new LogStep("b1")))))
                .Then(new LogStep("after"))
                .End("joined")
                .Build(definitionId, DefinitionVersion.Initial);
        }

        var hostA = CreateHost(store);
        hostA.Runtime.RegisterDefinition(Definition());
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-006",
            definitionId,
            DefinitionVersion.Initial,
            "order-6",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        var hostB = CreateHost(store);
        hostB.Runtime.RegisterDefinition(Definition());
        await hostB.Runtime.RaiseEventAsync(
            start.InstanceId,
            "EventA",
            new CorrelationId("order-6"),
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = await SnapshotAsync(hostB, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        var state = await FinalStateAsync(hostB, start.InstanceId);
        state.Log.Count(entry => entry == "a1").Should().Be(1);
        state.Log.Count(entry => entry == "b1").Should().Be(1);
        state.Log.Count(entry => entry == "a2").Should().Be(1);
        state.Log.Count(entry => entry == "after").Should().Be(1);
        state.Log.IndexOf("a2").Should().BeGreaterThan(state.Log.IndexOf("b1"),
            "the resumed instance continues the waiting branch only; the completed branch never re-runs");
        state.Log.Last().Should().Be("after");
    }

    private sealed class MutateAndWaitStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Value = 42;
            context.State.Log.Add("mutated");
            return ValueTask.FromResult<StepResult>(
                new StepResult.WaitForEvent("Go", new CorrelationId(context.State.OrderId)));
        }
    }

    private sealed class ObserveMutationStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Log.Add($"observed:{context.State.Value}");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    [Trait("AC", "DR-AC-013")]
    public async Task StepWaitPersistsState_MutationObservedExactlyOnceAfterRestart()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();

        WorkflowDefinition<OrderState> Definition()
        {
            return new WorkflowBuilder<OrderState>()
                .Init<string>(orderId => new OrderState { OrderId = orderId })
                .Then(new MutateAndWaitStep())
                .Then(new ObserveMutationStep())
                .End("observed")
                .Build(definitionId, DefinitionVersion.Initial);
        }

        var hostA = CreateHost(store);
        hostA.Runtime.RegisterDefinition(Definition());
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-013",
            definitionId,
            DefinitionVersion.Initial,
            "order-13",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(hostA, start.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        var hostB = CreateHost(store);
        hostB.Runtime.RegisterDefinition(Definition());
        await hostB.Runtime.RaiseEventAsync(
            start.InstanceId,
            "Go",
            new CorrelationId("order-13"),
            cancellationToken: TestContext.Current.CancellationToken);

        var completed = await SnapshotAsync(hostB, start.InstanceId);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        var state = await FinalStateAsync(hostB, start.InstanceId);
        state.Value.Should().Be(42);
        state.Log.Should().Equal("mutated", "observed:42");
    }

    [Fact]
    [Trait("AC", "DR-AC-016")]
    public void LightweightForEach_IsRejectedAtDurableRegistration()
    {
        var host = CreateHost();
        var definition = new WorkflowBuilder<OrderState>()
            .Init<string>(orderId => new OrderState { OrderId = orderId })
            .ForEach(
                state => (IReadOnlyList<string>)state.Log,
                WorkflowPartitioner<string>.Items(),
                body => body.Then(new LogStep("item")))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var register = () => host.Runtime.RegisterDefinition(definition);

        register.Should().Throw<WorkflowDefinitionException>()
            .WithMessage("*ForEach*")
            .WithMessage("*RunChild*");
    }

    [Fact]
    [Trait("AC", "DR-AC-018")]
    public async Task CheckpointWithoutPositionEnvelope_ParksWithDiagnostic_NeverGuessesPosition()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var host = CreateHost(store);
        host.Runtime.RegisterDefinition(StepWaitStepEndDefinition(definitionId, DefinitionVersion.Initial));
        var start = await host.Runtime.StartOrGetAsync<string, OrderState>(
            "order-dr-ac-018",
            definitionId,
            DefinitionVersion.Initial,
            "order-18",
            TestContext.Current.CancellationToken);
        (await SnapshotAsync(host, start.InstanceId)).Status.Should().Be(WorkflowStatus.Waiting);

        // Continue-as-new writes a checkpoint whose payload is a raw state baseline, not an
        // execution-position envelope — the legacy checkpoint shape DR-AC-018 targets.
        await host.Processor.ProcessAsync(
            new ContinueAsNewCommand
            {
                CommandId = CommandId.New(),
                InstanceId = start.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow,
                StateContentType = "application/json",
                StatePayload = JsonSerializer.SerializeToUtf8Bytes(new OrderState { OrderId = "order-18" })
            },
            TestContext.Current.CancellationToken);

        await host.Runtime.RaiseEventAsync(
            start.InstanceId,
            "Approved",
            new CorrelationId("order-18"),
            cancellationToken: TestContext.Current.CancellationToken);

        var snapshot = await SnapshotAsync(host, start.InstanceId);
        snapshot.Status.Should().Be(WorkflowStatus.Parked, "a checkpoint without the position envelope must never resume under a guessed position");
        snapshot.ErrorSummary.Should().Contain("envelope");
        var state = await SnapshotAsync(host, start.InstanceId);
        state.EndOutcomeName.Should().BeNull("no step may have executed under a guessed position");
    }
}
