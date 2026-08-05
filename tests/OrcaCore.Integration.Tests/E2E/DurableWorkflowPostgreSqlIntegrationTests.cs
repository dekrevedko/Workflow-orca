using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.TestSupport;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Integration.Tests.E2E;

[Collection(nameof(PostgreSqlCollection))]
[Trait(Traits.Category, Traits.Integration)]
[Trait(Traits.Container, "PostgreSql")]
public sealed class DurableWorkflowPostgreSqlIntegrationTests(PostgreSqlOrcaFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-001")]
    [Trait("AC", "AC-001")]
    public async Task INT_E2E_001_StraightLineStartAndComplete()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Complete(1, 3),
            TestContext.Current.CancellationToken);

        var snapshot = await fixture.CreateManagement(store)
            .Instance(IntegrationIds.Instance(1))
            .GetAsync(TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-002")]
    [Trait("AC", "AC-102")]
    public async Task INT_E2E_002_WaitAndEventDelivery()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-003")]
    [Trait("AC", "AC-104")]
    public async Task INT_E2E_003_MailboxBuffersEarlyEvent()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 3),
            TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowDeliveryBufferedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-004")]
    [Trait("AC", "AC-105")]
    public async Task INT_E2E_004_DuplicateEventDedupedAcrossProcessors()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);

        var restarted = await fixture.CreateProcessorAsync(store);
        var duplicate = await restarted.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4),
            TestContext.Current.CancellationToken);

        duplicate.Outcome.Should().Be(DurableCommandOutcome.NoOp);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-009")]
    [Trait("AC", "AC-512")]
    public async Task INT_E2E_009_PauseAndResumeOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(IntegrationCommands.Pause(1, 3), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(IntegrationCommands.Resume(1, 5), TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-010")]
    [Trait("AC", "AC-314")]
    public async Task INT_E2E_010_TerminalInstanceArchiveOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var management = fixture.CreateManagement(store);
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await management.TerminateAsync(
            IntegrationIds.Instance(1),
            IntegrationIds.Timestamp(2),
            DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);

        var archived = await management.ArchiveAsync(
            new RetentionPolicy
            {
                InstanceId = IntegrationIds.Instance(1),
                RequestedAt = IntegrationIds.Timestamp(3),
                Reason = "retention"
            },
            TestContext.Current.CancellationToken);

        archived.Archived.Should().BeTrue();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-016")]
    [Trait("AC", "AC-110")]
    public async Task INT_E2E_016_ParallelWaitsUseBranchIdentity()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2, "branch-a"),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 11, 3, "branch-b"),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4, "branch-b"),
            TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle()
            .Which.WaitId.Should().Be(IntegrationIds.Wait(11));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-005")]
    [Trait("AC", "AC-606")]
    public async Task INT_E2E_005_RunChildrenWhenAllOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunChildren(1, 2, "a", "b"),
            TestContext.Current.CancellationToken);
        var scheduled = (await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken)).OfType<WorkflowChildrenScheduledEvent>().Single();
        for (var i = 0; i < scheduled.Children.Count; i++)
        {
            await processor.ProcessAsync(
                IntegrationCommands.ChildCompleted(1, 10 + i, scheduled.Children[i].ChildInstanceId),
                TestContext.Current.CancellationToken);
        }

        var snapshot = await fixture.CreateManagement(store)
            .Instance(IntegrationIds.Instance(1))
            .GetAsync(TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowStatus.Running);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-006")]
    [Trait("AC", "AC-609")]
    public async Task INT_E2E_006_ChildThrottleContinuationOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunChildrenThrottled(1, 2, 2, "a", "b", "c", "d"),
            TestContext.Current.CancellationToken);

        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        outbox.Where(record => record.Kind == "child-start").Should().HaveCount(2);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-007")]
    [Trait("AC", "JS-AC-010")]
    public async Task INT_E2E_007_ExternalJobCompositeOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        await using var pools = await fixture.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = await fixture.CreateProcessorAsync(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.CompleteExternalJob(1, 3, "job-1", 90),
            TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowExternalJobCompletedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-008")]
    [Trait("AC", "AC-406")]
    public async Task INT_E2E_008_SagaDurableCompensationSurvivesRestartOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var adapter = new DurableSagaCommandAdapter<E2ESagaState>(SagaDefinition());
        var instanceId = IntegrationIds.Instance(1);
        var beforeRestart = await fixture.CreateProcessorAsync(store);
        await beforeRestart.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(
            adapter.ForwardActionCompleted(
                IntegrationIds.Command(2),
                instanceId,
                IntegrationIds.Timestamp(2),
                "root/actions/0"),
            TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(
            adapter.ForwardActionCompleted(
                IntegrationIds.Command(3),
                instanceId,
                IntegrationIds.Timestamp(3),
                "root/actions/1"),
            TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(
            adapter.RequestCompensation(
                IntegrationIds.Command(4),
                instanceId,
                IntegrationIds.Timestamp(4),
                "checkout",
                "forward failed"),
            TestContext.Current.CancellationToken);

        var afterRestart = await fixture.CreateProcessorAsync(store);
        await afterRestart.ProcessAsync(
            adapter.RequestCompensation(
                IntegrationIds.Command(5),
                instanceId,
                IntegrationIds.Timestamp(5),
                "checkout",
                "duplicate replay"),
            TestContext.Current.CancellationToken);
        await afterRestart.ProcessAsync(
            adapter.CompensationCompleted(
                IntegrationIds.Command(6),
                instanceId,
                IntegrationIds.Timestamp(6),
                "root/actions/1/compensation"),
            TestContext.Current.CancellationToken);
        await afterRestart.ProcessAsync(
            adapter.CompensationCompleted(
                IntegrationIds.Command(7),
                instanceId,
                IntegrationIds.Timestamp(7),
                "root/actions/0/compensation"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var audit = await fixture.CreateManagement(store)
            .GetSagaAuditAsync(instanceId, TestContext.Current.CancellationToken);

        events.OfType<SagaForwardActionCompletedEvent>()
            .Select(workflowEvent => workflowEvent.ActionKey)
            .Should().Equal("root/actions/0", "root/actions/1");
        events.OfType<SagaCompensationStartedEvent>()
            .Select(workflowEvent => workflowEvent.ActionKey)
            .Should().Equal("root/actions/1/compensation", "root/actions/0/compensation");
        events.OfType<SagaCompensationStartedEvent>().Should().HaveCount(2);
        events.OfType<SagaCompensationCompletedEvent>().Should().HaveCount(2);
        events.OfType<WorkflowTerminalEvent>().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Compensated);
        audit.Scopes.Should().ContainSingle()
            .Which.Outcome.Should().Be(WorkflowStatus.Compensated);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-011")]
    [Trait("AC", "AC-306")]
    public async Task INT_E2E_011_VersionBindingDeploySimulationOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var definitionId = DefinitionId.New();
        var versionOne = DeployDefinition(definitionId, DefinitionVersion.Initial);
        var versionTwo = DeployDefinition(definitionId, new DefinitionVersion(2));
        var registry = new DurableDefinitionRegistry();
        var runtime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(store),
            registry,
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());

        registry.Register(versionOne);
        var first = await runtime.StartOrGetAsync<string, DeployState>(
            "order-int-e2e-011",
            definitionId,
            DefinitionVersion.Initial,
            "input-v1",
            TestContext.Current.CancellationToken);
        registry.Register(versionTwo);
        var management = fixture.CreateManagement(store);
        var snapshot = await management.Instance(first.InstanceId)
            .GetAsync(TestContext.Current.CancellationToken);
        var bound = registry.ResolveBound<DeployState>(snapshot);

        bound.Should().BeSameAs(versionOne);
        registry.Resolve<DeployState>(definitionId, new DefinitionVersion(2))
            .Should().BeSameAs(versionTwo);

        await using var restartedStore = await fixture.CreateStoreAsync();
        var restartedRuntime = new DurableWorkflowRuntime(
            new DurableCommandProcessor(restartedStore),
            registry,
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        var second = await restartedRuntime.StartOrGetAsync<string, DeployState>(
            "order-int-e2e-011",
            definitionId,
            DefinitionVersion.Initial,
            "input-v1",
            TestContext.Current.CancellationToken);
        var incompatibleStart = async () => await restartedRuntime.StartOrGetAsync<string, DeployState>(
            "order-int-e2e-011",
            definitionId,
            new DefinitionVersion(2),
            "input-v2",
            TestContext.Current.CancellationToken);

        second.Created.Should().BeFalse();
        second.InstanceId.Should().Be(first.InstanceId);
        await incompatibleStart.Should().ThrowAsync<WorkflowVersionException>();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-012")]
    [Trait("AC", "AC-308")]
    public async Task INT_E2E_012_StatisticsAndActiveWaitsOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        var management = fixture.CreateManagement(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(IntegrationCommands.Start(2, 10), TestContext.Current.CancellationToken);

        var stats = await management.All().StatisticsAsync(TestContext.Current.CancellationToken);
        var waits = await management.Instance(IntegrationIds.Instance(1))
            .GetActiveWaitsAsync(TestContext.Current.CancellationToken);
        stats.Groups.Sum(group => group.Count).Should().BeGreaterThanOrEqualTo(2);
        waits.Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-013")]
    [Trait("AC", "JS-AC-005")]
    public async Task INT_E2E_013_DagRunnerReconstructsNodeStatusOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        var runner = new DurableDagRunner(processor);
        var rootId = IntegrationIds.Instance(1);
        var plan = new WorkflowDagBuilder()
            .Node("extract", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("load", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .BuildValidated()
            .Value;
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        var scheduledBatches = await runner.ScheduleReadyAsync(
            new DurableDagScheduleRequest(
                rootId,
                plan,
                CompletedNodeIds: [],
                FailedNodeIds: [],
                RequestedAt: IntegrationIds.Timestamp(2)),
            TestContext.Current.CancellationToken);
        var scheduled = (await store.LoadTailAsync(
                new WorkflowStreamId(rootId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .Single();
        var extract = scheduled.Children.Single(child => child.ItemSnapshot == "extract").ChildInstanceId;
        var load = scheduled.Children.Single(child => child.ItemSnapshot == "load").ChildInstanceId;
        await SeedProjectionAsync(
            store,
            DagNodeSnapshot(extract, rootId, WorkflowStatus.Completed, 11, 15));
        await SeedProjectionAsync(
            store,
            DagNodeSnapshot(load, rootId, WorkflowStatus.Failed, 12, 16, "load failed"));

        var dag = await fixture.CreateManagement(store)
            .ReconstructDagRunAsync(rootId, TestContext.Current.CancellationToken);

        scheduledBatches.Should().ContainSingle()
            .Which.Batch.ItemSnapshots.Should().Equal("extract", "load");
        dag.RootInstanceId.Should().Be(rootId);
        dag.Nodes.Select(node => node.NodeId).Should().Equal("extract", "load");
        dag.Nodes.Single(node => node.NodeId == "extract").Status.Should().Be(WorkflowStatus.Completed);
        dag.Nodes.Single(node => node.NodeId == "extract").StartedAt.Should().Be(IntegrationIds.Timestamp(11));
        dag.Nodes.Single(node => node.NodeId == "extract").UpdatedAt.Should().Be(IntegrationIds.Timestamp(15));
        dag.Nodes.Single(node => node.NodeId == "load").Status.Should().Be(WorkflowStatus.Failed);
        dag.Nodes.Single(node => node.NodeId == "load").ErrorSummary.Should().Be("load failed");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-014")]
    [Trait("AC", "AC-519")]
    public async Task INT_E2E_014_ResourcePoolFifoWaitOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        await using var pools = await fixture.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = await fixture.CreateProcessorAsync(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(IntegrationCommands.Start(2, 10), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Acquire(1, 2, "holder-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Acquire(2, 11, "holder-2", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        pool.Value.QueuedWaiters.Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-015")]
    [Trait("AC", "AC-013")]
    public async Task INT_E2E_015_YieldCrashRecoveryOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using (var firstStore = await fixture.CreateStoreAsync())
        {
            var first = await fixture.CreateProcessorAsync(firstStore);
            await first.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            var yielded = await first.ProcessAsync(
                new DurableYieldCommand(
                    IntegrationIds.Command(2),
                    IntegrationIds.Instance(1),
                    IntegrationIds.Timestamp(2),
                    "root/1",
                    IntegrationCommands.Envelope(1, "application/json", [1])),
                TestContext.Current.CancellationToken);
            var checkpoint = await firstStore.LoadCheckpointAsync(
                IntegrationIds.Instance(1),
                TestContext.Current.CancellationToken);
            var yieldedEvents = await firstStore.LoadTailAsync(
                new WorkflowStreamId(IntegrationIds.Instance(1)),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken);

            yielded.Outcome.Should().Be(DurableCommandOutcome.Committed);
            yielded.StreamVersion.Should().Be(new StreamVersion(1));
            checkpoint.HasValue.Should().BeTrue();
            checkpoint.Value.StreamVersion.Should().Be(new StreamVersion(1));
            DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload).StatePayload.Should().Equal(1);
            yieldedEvents.OfType<WorkflowStepCompletedEvent>().Should().BeEmpty();
        }

        await using var restartedStore = await fixture.CreateStoreAsync();
        var restarted = await fixture.CreateProcessorAsync(restartedStore);
        await restarted.ProcessAsync(
            new DurableStepCompletedCommand(
                IntegrationIds.Command(3),
                IntegrationIds.Instance(1),
                IntegrationIds.Timestamp(3),
                "root/1",
                IntegrationCommands.Envelope(1, "application/json", [2])),
            TestContext.Current.CancellationToken);
        await restarted.ProcessAsync(IntegrationCommands.Complete(1, 4), TestContext.Current.CancellationToken);
        var finalCheckpoint = await restartedStore.LoadCheckpointAsync(
            IntegrationIds.Instance(1),
            TestContext.Current.CancellationToken);
        var events = await restartedStore.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var snapshot = await fixture.CreateManagement(restartedStore)
            .Instance(IntegrationIds.Instance(1))
            .GetAsync(TestContext.Current.CancellationToken);

        finalCheckpoint.HasValue.Should().BeTrue();
        finalCheckpoint.Value.StreamVersion.Should().Be(new StreamVersion(2));
        DurableExecutionEnvelopeV2.Deserialize(finalCheckpoint.Value.Payload).StatePayload.Should().Equal(2);
        events.OfType<WorkflowStepCompletedEvent>().Should().ContainSingle()
            .Which.StepPath.Should().Be("root/1");
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-017")]
    [Trait("AC", "AC-610")]
    public async Task INT_E2E_017_ParentResumeTokenBarrierSurvivesProcessorRestart()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using (var firstStore = await fixture.CreateStoreAsync())
        {
            var first = await fixture.CreateProcessorAsync(firstStore);
            await first.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            await first.ProcessAsync(
                IntegrationCommands.RunChildren(1, 2, "a", "b"),
                TestContext.Current.CancellationToken);
            var scheduled = (await firstStore.LoadTailAsync(
                new WorkflowStreamId(IntegrationIds.Instance(1)),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken)).OfType<WorkflowChildrenScheduledEvent>().Single();
            await first.ProcessAsync(
                IntegrationCommands.ChildCompleted(1, 10, scheduled.Children[0].ChildInstanceId),
                TestContext.Current.CancellationToken);
        }

        await using var restartedStore = await fixture.CreateStoreAsync();
        var restarted = await fixture.CreateProcessorAsync(restartedStore);
        var restartedEvents = await restartedStore.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var restartedSchedule = restartedEvents.OfType<WorkflowChildrenScheduledEvent>().Single();
        await restarted.ProcessAsync(
            IntegrationCommands.ChildCompleted(1, 11, restartedSchedule.Children[1].ChildInstanceId),
            TestContext.Current.CancellationToken);
        var token = (await restartedStore.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken)).OfType<WorkflowParentResumeTokenRecordedEvent>().Single();

        var firstConsume = await restarted.ProcessAsync(
            new ConsumeParentResumeTokenCommand
            {
                CommandId = IntegrationIds.Command(12),
                InstanceId = IntegrationIds.Instance(1),
                RequestedAt = IntegrationIds.Timestamp(12),
                GroupId = token.GroupId,
                ResumeTokenId = token.ResumeTokenId
            },
            TestContext.Current.CancellationToken);
        var duplicateConsume = await restarted.ProcessAsync(
            new ConsumeParentResumeTokenCommand
            {
                CommandId = IntegrationIds.Command(13),
                InstanceId = IntegrationIds.Instance(1),
                RequestedAt = IntegrationIds.Timestamp(13),
                GroupId = token.GroupId,
                ResumeTokenId = token.ResumeTokenId
            },
            TestContext.Current.CancellationToken);
        var finalEvents = await restartedStore.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        firstConsume.Outcome.Should().Be(DurableCommandOutcome.Committed);
        duplicateConsume.Outcome.Should().Be(DurableCommandOutcome.NoOp);
        finalEvents.OfType<WorkflowParentResumeTokenConsumedEvent>().Should().ContainSingle()
            .Which.ResumeTokenId.Should().Be(token.ResumeTokenId);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-E2E-018")]
    [Trait("AC", "AC-613")]
    public async Task INT_E2E_018_UnifiedOutboxMixedKindsSingleCommit()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        await using var pools = await fixture.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = await fixture.CreateProcessorAsync(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunChildren(1, 2, "a"),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 3, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        outbox.Select(record => record.Kind).Should().Contain("child-start");
        outbox.Select(record => record.Kind).Should().Contain("external-job-start");
    }

    private static WorkflowDefinition<DeployState> DeployDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return global::OrcaCore.Workflow.Durable<DeployState>(definitionId, definitionVersion)
            .Init<string>(input => new DeployState(input))
            .End()
            .Build();
    }

    private static SagaDefinition<E2ESagaState> SagaDefinition()
    {
        return new SagaBuilder<E2ESagaState>()
            .Init<string>(_ => new E2ESagaState())
            .CompensationScope("checkout", scope => scope
                .Then<ReserveInventoryStep>()
                .CompensateBy<ReleaseInventoryStep>()
                .Then<AuthorizePaymentStep>()
                .CompensateBy<RefundPaymentStep>())
            .End()
            .Build(IntegrationIds.Definition(1), DefinitionVersion.Initial);
    }

    private static async Task SeedProjectionAsync(
        IWorkflowEventStore store,
        WorkflowInstanceSnapshot snapshot)
    {
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(snapshot.InstanceId),
                ExpectedVersion = StreamVersion.Empty,
                ProjectionOperations =
                [
                    new ProjectionWrite(snapshot.InstanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = snapshot
                    }
                ]
            },
            TestContext.Current.CancellationToken);
    }

    private static WorkflowInstanceSnapshot DagNodeSnapshot(
        InstanceId instanceId,
        InstanceId rootId,
        WorkflowStatus status,
        int createdAt,
        int updatedAt,
        string? errorSummary = null)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            ParentInstanceId = rootId,
            RootInstanceId = rootId,
            DefinitionId = IntegrationIds.Definition(2),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = status,
            CreatedAt = IntegrationIds.Timestamp(createdAt),
            UpdatedAt = IntegrationIds.Timestamp(updatedAt),
            ErrorSummary = errorSummary
        };
    }

    private sealed record DeployState(string Value);

    private sealed record E2ESagaState;

    private sealed class ReserveInventoryStep : IStep<E2ESagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<E2ESagaState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ReleaseInventoryStep : IStep<E2ESagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<E2ESagaState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class AuthorizePaymentStep : IStep<E2ESagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<E2ESagaState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class RefundPaymentStep : IStep<E2ESagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<E2ESagaState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
