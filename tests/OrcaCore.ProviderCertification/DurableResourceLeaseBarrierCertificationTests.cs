using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using Xunit;

namespace OrcaCore.ProviderCertification;

public sealed class DurableResourceLeaseBarrierCertificationTests
{
    [Fact]
    public async Task ScopedLease_ReportsTheFourExactPostCommitBarriersInOrder()
    {
        using var provider = InMemoryProviderPorts.Create();
        var workflows = provider.EventStore;
        var pools = provider.ResourcePoolStore;
        await pools.UpsertPoolAsync(
            new OrcaCore.Abstractions.Providers.ResourcePoolDefinition(
                "database",
                1,
                TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
        var request = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .AcquireResources(request, lease => lease.Then<NoOpStep>())
            .End()
            .Build();
        var gate = new RecordingGate();
        var processor = new DurableCommandProcessor(workflows, pools)
        {
            LeaseCertificationGate = gate
        };
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(new Services()),
            TimeProvider.System,
            DurableDriverBudget.Default);
        runtime.RegisterDefinition(publicDefinition);

        await runtime.StartOrGetAsync<string, State>(
            "barrier-certification",
            publicDefinition.DefinitionId,
            publicDefinition.DefinitionVersion,
            "start",
            TestContext.Current.CancellationToken);

        gate.Facts.Select(fact => fact.Barrier).Should().Equal(
            DurableResourceLeaseCommitBarrier.WorkflowPendingObligationCommitted,
            DurableResourceLeaseCommitBarrier.GovernanceReservationCommitted,
            DurableResourceLeaseCommitBarrier.WorkflowActivationCommitted,
            DurableResourceLeaseCommitBarrier.GovernanceOwnershipConfirmed);
        gate.Facts.Select(fact => fact.ObligationId).Distinct().Should().ContainSingle();
        gate.Facts.Select(fact => fact.LeaseProtectionToken).Distinct().Should().ContainSingle();
        gate.Facts.Skip(1).Should().OnlyContain(fact =>
            fact.Tickets.Count == 1 &&
            fact.Tickets[0].Pool.Value == "database" &&
            fact.Tickets[0].Units == 1 &&
            fact.Tickets[0].ProviderGeneration > 0);
    }

    private sealed class RecordingGate : IDurableResourceLeaseCertificationGate
    {
        internal List<DurableResourceLeaseCommitBarrierFact> Facts { get; } = [];

        public ValueTask OnPostCommitAsync(
            DurableResourceLeaseCommitBarrierFact fact,
            CancellationToken cancellationToken = default)
        {
            Facts.Add(fact);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class State;

    private sealed class NoOpStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class Services : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(NoOpStep) ? new NoOpStep() : null;
    }
}
