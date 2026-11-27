using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Tests;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed partial class DurableResourceLeaseBarrierCertificationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProtectedChildStepRestart_RequiresItsOwnLease(bool admittedItem, bool removeObligation)
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = InMemoryProviderPorts.Create();
        var events = provider.EventStore;
        var pools = provider.ResourcePoolStore;
        var pool = ResourcePoolName.Create("database");
        await pools.UpsertPoolAsync(new ResourcePoolDefinition(pool.Value, 1, TimeSpan.FromMinutes(5)), token);
        var request = ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(pool));
        var root = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, _ => { });
        var joined = admittedItem
            ? root.ForEach<int, State, int>(_ => [1], ForEachOptions.Create(1), _ => new State(),
                item => item.AcquireResources(request, lease => lease.If(_ => true, nested => nested.Then<LeasedStep>()))
                    .Return(_ => 1)).WhenAll((state, _) => state.Value)
            : root.Parallel<int>(branches => branches.Branch(AuthoredBranchId.Create("lease"), state => state.Value,
                branch => branch.AcquireResources(request, lease => lease.Then<LeasedStep>())
                    .Return(_ => 1))).WhenAll((state, _) => state.Value);
        var definition = joined.Then<LeasedStep>().End().Build();
        var steps = new Services();
        DurableWorkflowRuntime Runtime(IWorkflowEventStore store)
        {
            var runtime = new DurableWorkflowRuntime(new DurableCommandProcessor(store, pools),
                new DurableDefinitionRegistry(steps), TimeProvider.System);
            runtime.RegisterDefinition(definition);
            return runtime;
        }

        var acquirePath = admittedItem ? "root/2/item/0" : "root/2/branches/0/0";
        var stepPath = acquirePath + (admittedItem ? "/lease/0/then/0" : "/lease/0");
        var boundary = admittedItem ? stepPath + ":attempt-admission" : acquirePath + ":lease-activated";
        CheckpointWrite? stopped = null;
        var interrupted = new DagEventStoreProbe { Inner = events, AfterAppend = batch =>
        {
            if (batch.Checkpoint?.LastStepPath != boundary) return;
            stopped = batch.Checkpoint;
            throw new HostStopped();
        }};
        await Assert.ThrowsAsync<HostStopped>(() => Runtime(interrupted).StartOrGetAsync<string, State>(
            "protected-child", definition.DefinitionId, definition.DefinitionVersion, "start", token));
        Assert.Empty(steps.Invocations);
        var checkpoint = Assert.IsType<CheckpointWrite>(stopped);
        var instance = checkpoint.InstanceId;
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        var lease = Assert.Single(envelope.OwnedObligations, o => o.LeasePhase == "Held");
        var previous = Assert.Single(envelope.OwnedObligations, o => o.LeasePhase == "Released");
        Assert.NotEqual(previous.FiberId, lease.FiberId);
        Assert.Equal(acquirePath, lease.AuthoredPath);
        var fiber = Assert.Single(envelope.Fibers, f => f.FiberId == lease.FiberId);
        Assert.Equal($"instruction:{stepPath}:Step", fiber.InstructionId);
        Assert.Equal(admittedItem, fiber.AttemptInFlight);
        var original = Assert.Single((await pools.GetPoolAsync(pool.Value, token)).Value.HeldTickets);
        Assert.Equal(Persisted(original), Assert.Single(lease.LeaseTickets));
        var stream = new WorkflowStreamId(instance);
        var occurrence = LeaseProtectionToken.Parse(lease.ProtectionToken!);
        if (removeObligation)
        {
            var damaged = envelope with { OwnedObligations = envelope.OwnedObligations.Where(o => o != lease).ToArray() };
            checkpoint = checkpoint with { Payload = damaged.Serialize() };
            Assert.True((await events.AppendAsync(new ProviderCommitBatch
            {
                StreamId = stream, ExpectedVersion = checkpoint.StreamVersion, Checkpoint = checkpoint
            }, token)).IsSuccess);
        }

        var result = await Runtime(events).DriveAsync(instance, token);
        var tail = await events.LoadTailAsync(stream, checkpoint.StreamVersion, token);
        if (removeObligation)
        {
            Assert.Empty(steps.Invocations);
            Assert.Equal(DurableSegmentOutcome.Parked, result.Outcome);
            Assert.Equal(DurableParkReason.Poison, Assert.IsType<WorkflowParkedEvent>(Assert.Single(tail)).Reason);
            Assert.Equal(checkpoint.Payload, (await events.LoadCheckpointAsync(instance, token)).Value.Payload);
            Assert.Equal(original, Assert.Single((await pools.GetPoolAsync(pool.Value, token)).Value.HeldTickets));
            Assert.False((await pools.GetReleaseEvidenceAsync(occurrence, token)).HasValue);
            return;
        }

        Assert.Equal(DurableSegmentOutcome.Terminal, result.Outcome);
        Assert.Collection(steps.Invocations, scoped => Assert.Equal(occurrence, scoped), outside => Assert.Null(outside));
        Assert.Single(tail.OfType<WorkflowStepCompletedEvent>(), step => step.StepPath == stepPath);
        Assert.Equal(original, Assert.Single(Assert.Single(tail.OfType<WorkflowResourcePoolReleasedEvent>()).Tickets));
        Assert.Equal(WorkflowInstanceStatus.Completed, Assert.Single(tail.OfType<WorkflowTerminalEvent>()).Status);
    }
}
