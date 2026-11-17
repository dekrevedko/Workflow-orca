using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class DagRunCreationTests
{
    [Fact]
    public async Task Create_ReopensExactGraphAndRejectsConflictingInputOrIdentity()
    {
        var token = TestContext.Current.CancellationToken;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        await using var services = Services(workflow);
        var catalog = services.GetRequiredService<DurableDagRunInputCatalog>();
        var child = new DagRunBindingV1(workflow.DefinitionId, workflow.DefinitionVersion, workflow.DefinitionFingerprint.Value);
        var plan = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var key = catalog.Install(plan, typeof(string));
        var childKey = $"workflow:{child.DefinitionId}:1";
        var graph = new DagRunGraphV1(plan, key,
            [new(0, "root", child, childKey + ":input", childKey + ":output", []),
                new(1, "dependent", child, childKey + ":input", childKey + ":output", [0])]);
        var request = new DurableDagRunCreateV1(Guid.NewGuid(), new("create-test"), graph, null, typeof(string));
        var store = services.GetRequiredService<DurableDagRunStore>();

        var created = await store.CreateOrReopenAsync(request, token);
        created.Outcome.Should().Be(DurableDagCreateOutcomeV1.Created);
        created.Snapshot!.Nodes.Select(node => node.State).Should().Equal(DagNodeStateV1.Ready, DagNodeStateV1.Pending);
        created.Snapshot.RunInput.Bytes.Should().Equal("null"u8.ToArray());
        created.Snapshot.RunInput.IsPresent.Should().BeTrue();
        var rebuilt = request with { Graph = graph with { Nodes = graph.Nodes.Select(n => n with { DirectDependencies = n.DirectDependencies.ToArray() }).ToArray() } };
        (await store.CreateOrReopenAsync(rebuilt, token)).Outcome.Should().Be(DurableDagCreateOutcomeV1.Existing);
        (await store.CreateOrReopenAsync(request with { Input = "" }, token)).Outcome.Should().Be(DurableDagCreateOutcomeV1.BindingConflict);
        var replay = await store.CreateOrReopenAsync(request with { RunId = Guid.NewGuid() }, token);
        replay.Outcome.Should().Be(DurableDagCreateOutcomeV1.Existing);
        replay.Snapshot!.RunId.Should().Be(request.RunId);
        var other = await store.CreateOrReopenAsync(request with { RunId = Guid.NewGuid(), StartKey = new("other-owner") }, token);
        other.Outcome.Should().Be(DurableDagCreateOutcomeV1.Created);
        (await store.CreateOrReopenAsync(request with { RunId = other.Snapshot!.RunId }, token)).Outcome
            .Should().Be(DurableDagCreateOutcomeV1.BindingConflict, "the explicit run-id collision is independent of replay by key");
        (await store.CreateOrReopenAsync(request with { StartKey = new("other-key") }, token)).Outcome.Should().Be(DurableDagCreateOutcomeV1.BindingConflict);
        (await services.GetRequiredService<IWorkflowEventStore>()
            .LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(request.RunId), StreamVersion.Empty, token)).Should().ContainSingle();

        var reader = services.GetRequiredService<DurableDagMappingReader>();
        var descriptor = new DurableDagNodeContractV1(new(request.RunId, 0),
            new(workflow.DefinitionId, workflow.DefinitionVersion, workflow.DefinitionFingerprint, typeof(int), typeof(int)), []);
        var available = (await reader.ReadMappingInputsAsync(descriptor, token))
            .Should().BeOfType<DurableDagMappingInputsV1.Available>().Subject;
        available.RunInputType.Should().Be(typeof(string));
        available.RunInput.Should().BeNull();
        available.Outputs.Should().BeEmpty();
        Func<Task> forged = () => reader.ReadMappingInputsAsync(descriptor with
            { Workflow = descriptor.Workflow with { InputType = typeof(long) } }, token).AsTask();
        await forged.Should().ThrowAsync<InvalidDataException>();

        var cancellation = services.GetRequiredService<DurableDagRunCancellation>();
        var cancelled = await cancellation.RequestRunCancellationAsync(request.RunId, token);
        cancelled.Outcome.Should().Be(DurableDagCancelOutcomeV1.Requested);
        var afterCancel = await store.ReadAsync(request.RunId, token);
        afterCancel!.State.Should().Be(DagRunStateV1.Cancelled);
        afterCancel.Nodes.Select(node => node.State).Should().Equal(DagNodeStateV1.Cancelled, DagNodeStateV1.Cancelled);
        (await cancellation.RequestRunCancellationAsync(request.RunId, token)).Outcome
            .Should().Be(DurableDagCancelOutcomeV1.AlreadyTerminal);
    }

    [Fact]
    public async Task Admission_RejectsCopiedReceiptAndRetainsTheOriginalAcrossOrdinalDeferral()
    {
        var token = TestContext.Current.CancellationToken;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        await using var services = Services(workflow);
        var plan = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var child = new DagRunBindingV1(workflow.DefinitionId, workflow.DefinitionVersion, workflow.DefinitionFingerprint.Value);
        var key = services.GetRequiredService<DurableDagRunInputCatalog>().Install(plan, typeof(int));
        var childKey = $"workflow:{child.DefinitionId}:1";
        var graph = new DagRunGraphV1(plan, key,
            [new(0, "first", child, childKey + ":input", childKey + ":output", []),
                new(1, "second", child, childKey + ":input", childKey + ":output", [])]);
        var store = services.GetRequiredService<DurableDagRunStore>();
        var runId = Guid.NewGuid();
        var created = await store.CreateOrReopenAsync(new(runId, new("admit-test"), graph, 5, typeof(int)), token);
        var admission = services.GetRequiredService<DurableDagAdmission>();
        var turns = services.GetRequiredService<DurableDagRunTurns>();
        var binding = new DurableDagWorkflowBindingV1(workflow.DefinitionId, workflow.DefinitionVersion,
            workflow.DefinitionFingerprint, typeof(int), typeof(int));
        var later = await admission.PrepareMappingAsync(new(new(runId, 1), binding, []), new(8, typeof(int)), false, token);
        var earlier = await admission.PrepareMappingAsync(new(new(runId, 0), binding, []), new(7, typeof(int)), false, token);

        (await Admit(later with { }, created.Snapshot!.StreamVersion, 2)).Outcome.Should().Be(DurableDagMutationOutcomeV1.IntegrityConflict);
        (await Admit(later, created.Snapshot.StreamVersion, 2)).Outcome.Should().Be(DurableDagMutationOutcomeV1.CapacityDeferred);
        var first = await Admit(earlier, created.Snapshot.StreamVersion, 2);
        first.Outcome.Should().Be(DurableDagMutationOutcomeV1.Committed);
        (await Admit(later, created.Snapshot.StreamVersion, 2)).Outcome.Should().Be(DurableDagMutationOutcomeV1.VersionConflict);
        (await Admit(later, first.Version!.Value, 1)).Outcome.Should().Be(DurableDagMutationOutcomeV1.CapacityDeferred);
        var second = await Admit(later, first.Version!.Value, 2);
        second.Outcome.Should().Be(DurableDagMutationOutcomeV1.Committed);
        second.Snapshot!.Nodes.Select(node => node.State).Should().Equal(DagNodeStateV1.StartAuthorized, DagNodeStateV1.StartAuthorized);
        second.Snapshot.Nodes[1].Input!.Bytes.Should().Equal("8"u8.ToArray());

        async Task<DurableDagMutationResultV1> Admit(DurableDagPreparedMappingV1 receipt, StreamVersion version, int limit)
        {
            DurableDagMutationResultV1? result = null;
            (await turns.ExecuteAsync(runId, DagRunLeasePurposeV1.Admission, async (fence, ct) =>
            {
                result = await admission.AdmitPreparedMappingAsync(receipt, fence, version, limit, ct);
                return true;
            }, token)).Should().BeTrue();
            return result!;
        }
    }

    [Fact]
    public async Task Create_EmptyPlanTerminatesAtomicallyAndMissingBindingWritesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<DurableDagRunStore>();
        var plan = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var request = new DurableDagRunCreateV1(Guid.NewGuid(), new("empty-test"), new(plan, "unknown", []), 3, typeof(int));
        Func<Task> unavailable = () => store.CreateOrReopenAsync(request, token).AsTask();
        await unavailable.Should().ThrowAsync<DurableDagDefinitionUnavailableException>();
        (await store.ReadAsync(request.RunId, token)).Should().BeNull();

        var key = services.GetRequiredService<DurableDagRunInputCatalog>().Install(plan, typeof(int));
        var created = await store.CreateOrReopenAsync(request with { Graph = new(plan, key, []) }, token);
        created.Outcome.Should().Be(DurableDagCreateOutcomeV1.Created);
        created.Snapshot!.State.Should().Be(DagRunStateV1.Succeeded);
        created.Snapshot.StreamVersion.Should().Be(new StreamVersion(2));
    }

    [Fact]
    public async Task IntegrityReconciliation_RebuildsOnlyOwningChildWorkAndReplaysOperation()
    {
        var token = TestContext.Current.CancellationToken;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        await using var services = Services(workflow);
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var (run, write) = await DagPumpRunFixture.CreateAsync(events, workflow, false, token);
        var bridge = services.GetRequiredService<IDurableDagRunBridgeV1>();
        var fault = new DurableDagTurnRequestV1(run.RunId, Guid.NewGuid(), DurableDagTurnPurposeV1.IntegrityReconciliation);
        var grant = (await bridge.BeginTurnAsync(fault, token)).Turn!;
        var faulted = await bridge.RecordIntegrityFaultAsync(run.RunId, "DAG_PROTOCOL_INVALID", grant.Fence, grant.Version, token);
        faulted.Outcome.Should().Be(DurableDagMutationOutcomeV1.Committed);
        await bridge.EndTurnAsync(grant, token);
        await outbox.ClaimAsync(new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30))
            { KindSelector = OutboxKindSelector.Including("dag-child-start-v1") }, token);
        await outbox.MarkPoisonedAsync(write.OutboxRecordId, "DAG_PROTOCOL_INVALID", "old diagnostic", token);

        var request = fault with { RequestId = Guid.NewGuid() };
        grant = (await bridge.BeginTurnAsync(request, token)).Turn!;
        var operation = Guid.NewGuid();
        var restored = await bridge.ReconcilePoisonAsync(run.RunId, operation, grant.Fence, grant.Version, token);
        restored.Outcome.Should().Be(DurableDagMutationOutcomeV1.Committed);
        restored.Snapshot!.State.Should().Be(DagRunStateV1.Running);
        restored.Snapshot.Nodes.Should().BeEquivalentTo(run.Nodes);
        (await bridge.ReconcilePoisonAsync(run.RunId, operation, grant.Fence, grant.Version, token)).Outcome
            .Should().Be(DurableDagMutationOutcomeV1.Existing);
        await bridge.EndTurnAsync(grant, token);
        (await outbox.GetDispatchSnapshotAsync(write.OutboxRecordId, token)).Value.PoisonDetail.Should().Be("old diagnostic");
        var pump = services.GetRequiredService<DurableDagChildPump>();
        (await pump.PumpOnceAsync(new(100, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30)), token)).Should().Be(1);
        (await events.LoadTailAsync(new(run.Nodes[0].ChildInstanceId!), StreamVersion.Empty, token))
            .OfType<OrcaCore.Abstractions.Durable.WorkflowStartedEvent>().Should().ContainSingle();
    }

    private static ServiceProvider Services(DurableWorkflowDefinition<int, int> workflow)
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions { PartitionId = ResourceGovernancePartitionId.Create("dag-create-tests"), Pools = [] }
        }).AddWorkflow(workflow);
        return services.BuildServiceProvider();
    }
}
