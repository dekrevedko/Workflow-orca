using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Hosting.Tests;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed partial class DurableResourceLeaseBarrierCertificationTests
{
    public enum SecondPoolState { Exact, Missing, Replacement }

    [Theory]
    [InlineData(SecondPoolState.Exact, false)]
    [InlineData(SecondPoolState.Exact, true)]
    [InlineData(SecondPoolState.Missing, false)]
    [InlineData(SecondPoolState.Missing, true)]
    [InlineData(SecondPoolState.Replacement, false)]
    [InlineData(SecondPoolState.Replacement, true)]
    public async Task TwoPoolHeldRestart_RequiresBothOriginalTickets(SecondPoolState secondPool, bool legacyHeld)
    {
        var token = TestContext.Current.CancellationToken;
        var database = ResourcePoolName.Create("database");
        var network = ResourcePoolName.Create("network");
        var partition = ResourceGovernancePartitionId.Create("two-pool-restart");
        var services = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 1, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = partition,
                Pools = [DurableResourcePoolDefinition.Create(database, 1, TimeSpan.FromHours(1)),
                    DurableResourcePoolDefinition.Create(network, 2, TimeSpan.FromHours(1))]
            }
        });
        using var host = services.BuildServiceProvider();
        var workflows = host.GetRequiredService<IWorkflowEventStore>();
        var pools = host.GetRequiredService<IResourcePoolStore>();
        var governance = host.GetRequiredService<IDurableResourceGovernanceStore>();
        var diagnostics = host.GetRequiredService<IDurableResourceLeaseDiagnostics>();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(database), ResourceLeaseRequirement.Require(network, 2)),
                lease => lease.Then<LeasedStep>())
            .End().Build();
        var steps = new Services();
        var admissions = new List<CheckpointWrite>();
        DurableWorkflowRuntime Runtime(RecordingGate? gate = null)
        {
            var observed = new DagEventStoreProbe { Inner = workflows, AfterAppend = batch =>
            {
                if (batch.Checkpoint?.LastStepPath?.EndsWith(":attempt-admission", StringComparison.Ordinal) == true)
                    admissions.Add(batch.Checkpoint);
            }};
            var processor = new DurableCommandProcessor(observed, pools) { LeaseCertificationGate = gate ?? new RecordingGate(false) };
            var runtime = new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(steps), TimeProvider.System);
            runtime.RegisterDefinition(definition);
            return runtime;
        }

        var gate = new RecordingGate(true);
        await Assert.ThrowsAsync<HostStopped>(() => Runtime(gate).StartOrGetAsync<string, State>("two-pool",
            definition.DefinitionId, definition.DefinitionVersion, "start", token));
        var activation = gate.Facts[^1];
        var owner = activation.InstanceId;
        var stream = new WorkflowStreamId(owner);
        var checkpoint = (await workflows.LoadCheckpointAsync(owner, token)).Value;
        Assert.EndsWith(":lease-activated", checkpoint.LastStepPath, StringComparison.Ordinal);
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        var obligation = Assert.Single(envelope.OwnedObligations);
        Assert.Equal(activation.LeaseProtectionToken.Value, obligation.ProtectionToken);
        var grant = Assert.Single((await workflows.LoadTailAsync(stream, StreamVersion.Empty, token)).OfType<WorkflowResourcePoolAcquiredEvent>());
        var original = grant.Tickets.OrderBy(ticket => ticket.PoolName, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { (database.Value, 1), (network.Value, 2) }, original.Select(ticket => (ticket.PoolName, ticket.Count)));
        Assert.All(original, ticket => Assert.Equal(owner, ticket.HolderInstanceId));
        AssertHeld(checkpoint, obligation, original);
        Assert.Equal(original[0], Assert.Single((await pools.GetPoolAsync(database.Value, token)).Value.HeldTickets));
        Assert.Equal(original[1], Assert.Single((await pools.GetPoolAsync(network.Value, token)).Value.HeldTickets));
        Assert.Empty(admissions);
        Assert.Empty(steps.Invocations);
        if (legacyHeld)
        {
            // Older Held writers kept both tickets in the workflow grant and omitted the lexical ticket list.
            var legacy = envelope with { OwnedObligations = [obligation with { LeaseTickets = [] }] };
            Assert.True((await workflows.AppendAsync(new ProviderCommitBatch
            {
                StreamId = stream, ExpectedVersion = checkpoint.StreamVersion,
                Checkpoint = checkpoint with { Payload = legacy.Serialize() }
            }, token)).IsSuccess);
        }

        var actual = original;
        if (secondPool != SecondPoolState.Exact)
        {
            var replacement = await ChangeSecondTicketAsync(governance, partition, original[1], secondPool, token);
            actual = replacement is null ? [original[0]] : [original[0], replacement];
        }

        async Task AssertLiveTicketsAsync(ResourcePoolTicket[] expected)
        {
            var snapshots = (await pools.ListPoolsAsync(token)).OrderBy(pool => pool.Name, StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, snapshots.SelectMany(pool => pool.HeldTickets));
            Assert.All(snapshots, pool =>
            {
                Assert.Empty(pool.QueuedWaiters);
                Assert.Equal(pool.Capacity - expected.Where(ticket => ticket.PoolName == pool.Name).Sum(ticket => ticket.Count),
                    pool.AvailableCapacity);
            });
        }

        await AssertLiveTicketsAsync(actual);
        var beforeDrive = (await governance.LoadLatestAsync(partition, token)).Version;
        Assert.Equal(DurableSegmentOutcome.Terminal, (await Runtime().DriveAsync(owner, token)).Outcome);
        var final = (await workflows.LoadCheckpointAsync(owner, token)).Value;
        var afterDrive = (await governance.LoadLatestAsync(partition, token)).Version;
        Assert.Equal(DurableSegmentOutcome.Terminal, (await Runtime().DriveAsync(owner, token)).Outcome);
        Assert.Equal(final.StreamVersion, (await workflows.LoadCheckpointAsync(owner, token)).Value.StreamVersion);
        Assert.Equal(afterDrive, (await governance.LoadLatestAsync(partition, token)).Version);
        var history = await workflows.LoadTailAsync(stream, StreamVersion.Empty, token);
        Assert.Equal(grant.EventId, Assert.Single(history.OfType<WorkflowResourcePoolAcquiredEvent>()).EventId);
        var completed = history.OfType<WorkflowStepCompletedEvent>().Where(step => step.StepPath == "root/1/lease/0");
        var receipt = await pools.GetReleaseEvidenceAsync(activation.LeaseProtectionToken, token);
        if (secondPool == SecondPoolState.Exact)
        {
            AssertHeld(Assert.Single(admissions), obligation, original);
            Assert.Equal(activation.LeaseProtectionToken, Assert.Single(steps.Invocations));
            Assert.Single(completed);
            Assert.Equal(WorkflowInstanceStatus.Completed, final.Status);
            Assert.Equal(original, Assert.Single(history.OfType<WorkflowResourcePoolReleasedEvent>()).Tickets);
            Assert.Equal(original, receipt.Value.ReleasedTickets);
            await AssertLiveTicketsAsync([]);
            return;
        }

        Assert.Empty(admissions);
        Assert.Empty(steps.Invocations);
        Assert.Empty(completed);
        Assert.Equal(WorkflowInstanceStatus.Failed, final.Status);
        var failure = Assert.Single(history.OfType<WorkflowStepFailedEvent>());
        Assert.Equal("lease-reconciliation", failure.StepPath);
        Assert.Equal(final.ErrorSummary, failure.ErrorSummary);
        Assert.StartsWith("WF-LEASE-LOST:", failure.ErrorSummary, StringComparison.Ordinal);
        static string Describe(IEnumerable<ResourcePoolTicket> tickets) => string.Join(";", tickets.Select(ticket =>
            $"{ticket.TicketId:N}/{ticket.PoolName}/{ticket.Count}/{ticket.ProviderGeneration}"));
        Assert.Contains($"Expected=[{Describe(original)}] Actual=[{Describe(actual)}]", failure.ErrorSummary, StringComparison.Ordinal);
        Assert.Empty(history.OfType<WorkflowResourcePoolReleasedEvent>());
        Assert.False(receipt.HasValue);
        Assert.Equal(beforeDrive, afterDrive);
        await AssertLiveTicketsAsync(actual);
        var lost = Assert.Single(DurableExecutionEnvelopeV2.Deserialize(final.Payload).OwnedObligations);
        Assert.Equal(obligation.ObligationId, lost.ObligationId);
        Assert.Equal(obligation.ProtectionToken, lost.ProtectionToken);
        Assert.Equal("LeaseLost", lost.LeasePhase);
        var diagnostic = await diagnostics.GetAsync(activation.LeaseProtectionToken, token);
        Assert.NotNull(diagnostic);
        Assert.Equal(DurableResourceLeaseObligationStatus.LeaseLost, diagnostic.Status);
        Assert.Multiple(
            () => Assert.Equal(original.Select(Persisted), lost.LeaseTickets),
            () => Assert.Equal(original.Select(ticket => new DurableResourceLeaseTicketSnapshot(ticket.TicketId.ToString("N"),
                ResourcePoolName.Create(ticket.PoolName), ticket.Count, ticket.ProviderGeneration, ticket.ReviewDeadline!.Value,
                ticket.ReviewMarked)), diagnostic.Tickets),
            () => Assert.Contains($"MissingPools=[{network.Value}]", failure.ErrorSummary, StringComparison.Ordinal));
    }

    private static async Task<ResourcePoolTicket?> ChangeSecondTicketAsync(IDurableResourceGovernanceStore governance,
        ResourceGovernancePartitionId partition, ResourcePoolTicket second, SecondPoolState state, CancellationToken token)
    {
        // A normal governance release is all-or-nothing. Inject one checksummed full-state record at the real
        // store port, retaining every other pool, ticket field, counter and receipt from the live grant.
        var head = await governance.LoadLatestAsync(partition, token);
        var document = JsonNode.Parse(head.Record!.Payload.Span)!;
        var tickets = document["State"]!["Tickets"]!.AsArray();
        var target = Assert.Single(tickets, node => node!["TicketId"]!.GetValue<string>() == second.TicketId.ToString("N"))!;
        ResourcePoolTicket? replacement = null;
        if (state == SecondPoolState.Missing)
            Assert.True(tickets.Remove(target));
        else
        {
            Assert.Equal(SecondPoolState.Replacement, state);
            replacement = second with { TicketId = Guid.NewGuid() };
            target["TicketId"] = replacement.TicketId.ToString("N");
        }
        var payload = JsonSerializer.SerializeToUtf8Bytes(document);
        var record = ResourceGovernanceRecord.FromPersisted(head.Version + 1, ResourceGovernanceRecord.V1Format,
            payload, Convert.ToHexStringLower(SHA256.HashData(payload)));
        var committed = Assert.IsType<ResourceGovernanceAppendResult.Committed>(
            await governance.AppendAsync(partition, head.Version, [record], token));
        Assert.Equal(head.Version + 1, committed.Version);
        return replacement;
    }
}
