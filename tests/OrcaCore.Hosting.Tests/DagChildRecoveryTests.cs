using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class DagChildRecoveryTests
{
    [Fact]
    public async Task ChildPump_RecoversDeterministicStartAndAtomicTerminalJoinWithoutDagOrDispatcher()
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value + 1).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var registry = services.GetRequiredService<IWorkflowDefinitionRegistry>();
        var (run, start) = await DagPumpRunFixture.CreateAsync(store, workflow, false, TestContext.Current.CancellationToken);
        var pump = services.GetRequiredService<DurableDagChildPump>();
        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(1);
        (await outbox.GetStateAsync(start.OutboxRecordId, TestContext.Current.CancellationToken)).Value.Should().Be(OutboxRecordState.Dispatched);
        var handler = services.GetRequiredService<IDurableDagChildHandler>();
        // Simulate a lost start acknowledgement. Re-reading exact persisted binding
        // must attach the same stream, not allocate a caller-key or a second child.
        (await handler.HandleAsync(start, TestContext.Current.CancellationToken)).Should().BeTrue();
        var child = run.Nodes[0].ChildInstanceId!;
        var facts = await store.LoadTailAsync(new(child), StreamVersion.Empty, TestContext.Current.CancellationToken);
        facts.OfType<WorkflowStartedEvent>().Should().ContainSingle();
        var started = facts.OfType<WorkflowStartedEvent>().Single();
        started.IdempotencyKey.Should().BeNull();
        started.ParentInstanceId.Should().Be(InstanceId.Parse(run.RunId.ToString("D")));
        started.RootInstanceId.Should().Be(started.ParentInstanceId);
        started.InputPayload.Should().Equal("41"u8.ToArray());

        var continuation = services.GetServices<IHostedService>().OfType<OrcaCoreContinuationPumpHostedService>().Single();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await continuation.StartAsync(timeout.Token);
        try
        {
            var handle = await registry.GetRequiredHandle(workflow.Reference).GetInstanceAsync(child, timeout.Token);
            (await handle.WaitForOutputAsync(timeout.Token)).Should().Be(42);
        }
        finally { await continuation.StopAsync(CancellationToken.None); }

        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(1,
            "the child terminal transaction must carry its DAG join intent atomically");
        var final = await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken);
        final.State.Should().Be(DagRunStateV1.Succeeded);
        final.Nodes[0].Output!.Bytes.Should().Equal("42"u8.ToArray());
        var join = DagPumpRunFixture.ChildWrite("dag-child-join-v1", run.RunId);
        (await handler.HandleAsync(join, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken)).StreamVersion.Should().Be(final.StreamVersion);
    }

    [Fact]
    public async Task CancellationPump_MaterializesAnAuthorizedChildBeforeCancellingAndKeepsAdmissionUntilJoin()
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value + 1).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var (run, _) = await DagPumpRunFixture.CreateAsync(store, workflow, true, TestContext.Current.CancellationToken);
        var pump = services.GetRequiredService<DurableDagChildPump>();
        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(2);
        var beforeJoin = await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken);
        beforeJoin.Nodes[0].State.Should().Be(DagNodeStateV1.CancellationRequested);
        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(1);
        var final = await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken);
        final.State.Should().Be(DagRunStateV1.Cancelled);
        final.Nodes[0].State.Should().Be(DagNodeStateV1.Cancelled);
    }

    [Fact]
    public async Task FailedJoin_BlocksDescendantsButPreservesIndependentReadyWork()
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value + 1).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var (run, _) = await DagPumpRunFixture.CreateAsync(store, workflow, false, TestContext.Current.CancellationToken, branches: true);
        var pump = services.GetRequiredService<DurableDagChildPump>();
        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(1);
        var child = await services.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(workflow.Reference)
            .GetInstanceAsync(run.Nodes[0].ChildInstanceId!, TestContext.Current.CancellationToken);
        await child.TerminateAsync(TestContext.Current.CancellationToken);
        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(1);
        var joined = await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken);
        joined.State.Should().Be(DagRunStateV1.Running);
        joined.Nodes.Select(node => node.State).Should().Equal(DagNodeStateV1.Failed, DagNodeStateV1.DependencyBlocked, DagNodeStateV1.Ready);
        joined.Nodes[0].FailureCode.Should().Be("CHILD_TERMINATED");
        joined.Nodes[1].FailureCode.Should().Be("DEPENDENCY_BLOCKED");
        var next = await store.AcquireDagRunLeaseAsync(new(run.RunId, Guid.NewGuid(), Guid.NewGuid(),
            DagRunLeasePurposeV1.Admission, null, TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);
        next.Outcome.Should().Be(DagRunLeaseOutcomeV1.Granted, "the completed join must release its turn before idle/capacity waits");
        await store.ReleaseDagRunLeaseAsync(next.Fence!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task UnknownChildSignalVersion_IsRejectedBeforeChildEffects(int version)
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var (run, write) = await DagPumpRunFixture.CreateAsync(store, workflow, false, TestContext.Current.CancellationToken);
        var text = System.Text.Encoding.UTF8.GetString(write.Payload).Replace("\"version\":1", "\"version\":" + version, StringComparison.Ordinal);
        Func<Task> handle = () => services.GetRequiredService<IDurableDagChildHandler>().HandleAsync(
            write with { Payload = System.Text.Encoding.UTF8.GetBytes(text) }, TestContext.Current.CancellationToken);
        await handle.Should().ThrowAsync<InvalidDataException>();
        (await store.LoadTailAsync(new(run.Nodes[0].ChildInstanceId!), StreamVersion.Empty, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task CancellationBeforeRunBarrier_DoesNotMaterializeOrMutateAChild()
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var (run, _) = await DagPumpRunFixture.CreateAsync(store, workflow, false, TestContext.Current.CancellationToken);
        Func<Task> cancel = () => services.GetRequiredService<IDurableDagChildHandler>()
            .HandleAsync(DagPumpRunFixture.ChildWrite("dag-child-cancel-v1", run.RunId), TestContext.Current.CancellationToken);
        await cancel.Should().ThrowAsync<InvalidDataException>().WithMessage("*precedes the run barrier*");
        (await store.LoadTailAsync(new(run.Nodes[0].ChildInstanceId!), StreamVersion.Empty, TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task DeterministicChildCollision_IsPoisonedWithoutAdoptingOrMutatingTheForeignStream()
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var (run, start) = await DagPumpRunFixture.CreateAsync(store, workflow, false, TestContext.Current.CancellationToken);
        var child = run.Nodes[0].ChildInstanceId!;
        var foreign = new WorkflowStartedEvent
        {
            EventId = EventId.Create(Guid.NewGuid().ToString()), InstanceId = child, CommandId = CommandId.New(),
            CausationId = CausationId.New(), OccurredAt = DateTimeOffset.UtcNow,
            DefinitionId = DefinitionId.New(), DefinitionVersion = DefinitionVersion.Initial
        };
        (await store.AppendAsync(new ProviderCommitBatch { StreamId = new(child), ExpectedVersion = StreamVersion.Empty, Events = [foreign] },
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        (await services.GetRequiredService<DurableDagChildPump>().PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(0);
        (await store.LoadTailAsync(new(child), StreamVersion.Empty, TestContext.Current.CancellationToken)).Should().ContainSingle();
        var transport = (await services.GetRequiredService<IWorkflowOutboxStore>().GetDispatchSnapshotAsync(start.OutboxRecordId, TestContext.Current.CancellationToken)).Value;
        transport.State.Should().Be(OutboxRecordState.Poisoned);
        transport.PoisonDetail.Should().Contain("collision");
        var fault = await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken);
        fault.State.Should().Be(DagRunStateV1.IntegrityFault);
        fault.Nodes.Should().BeEquivalentTo(run.Nodes);
    }

    [Fact]
    public async Task MissingInstalledDefinition_RetriesWithoutPoisonOrSuccessfulAcknowledgement()
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        await using var services = Services(workflow, install: false);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var (run, start) = await DagPumpRunFixture.CreateAsync(store, workflow, false, TestContext.Current.CancellationToken);
        (await services.GetRequiredService<DurableDagChildPump>().PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(0);
        (await store.LoadTailAsync(new(run.Nodes[0].ChildInstanceId!), StreamVersion.Empty, TestContext.Current.CancellationToken)).Should().BeEmpty();
        var state = (await services.GetRequiredService<IWorkflowOutboxStore>().GetStateAsync(start.OutboxRecordId, TestContext.Current.CancellationToken)).Value;
        state.Should().NotBe(OutboxRecordState.Dispatched).And.NotBe(OutboxRecordState.Poisoned);
        (await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken)).State.Should().Be(DagRunStateV1.Running);
    }

    [Fact]
    public async Task MalformedChildSignal_FaultsItsTrustedRunAndPreservesUncertainAdmissionBeforePoison()
    {
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value + 1).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var (run, start) = await DagPumpRunFixture.CreateAsync(store, workflow, false, TestContext.Current.CancellationToken);
        var handler = services.GetRequiredService<IDurableDagChildHandler>();
        Func<Task> malformed = () => handler.HandleAsync(start with { Payload = "broken"u8.ToArray() }, TestContext.Current.CancellationToken);
        await malformed.Should().ThrowAsync<System.Text.Json.JsonException>();
        (await handler.RecordFaultAsync(run.RunId, TestContext.Current.CancellationToken)).Should().BeTrue();
        var fault = await DagPumpRunFixture.ReadAsync(store, run.RunId, TestContext.Current.CancellationToken);
        fault.State.Should().Be(DagRunStateV1.IntegrityFault);
        fault.Nodes.Should().BeEquivalentTo(run.Nodes);
    }

    private static OutboxClaimRequest Request() => new(100, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));

    private static ServiceProvider Services(DurableWorkflowDefinition<int, int> workflow, bool install = true)
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        var engine = services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions { PartitionId = ResourceGovernancePartitionId.Create("dag-pump-tests"), Pools = [] }
        });
        if (install) engine.AddWorkflow(workflow);
        return services.BuildServiceProvider();
    }
}
