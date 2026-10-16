using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableOwnershipContractTests
{
    [Fact]
    public void BlockedWorkAndCancellationContractsCarryTypedFiberAndScopeOwners()
    {
        var ownedContracts = new[]
        {
            typeof(WorkflowWaitRegisteredEvent),
            typeof(WorkflowWaitMatchedEvent),
            typeof(WorkflowWaitCancelledEvent),
            typeof(WorkflowResumeConsumedEvent),
            typeof(WorkflowTimerScheduledEvent),
            typeof(WorkflowTimerFiredEvent),
            typeof(WorkflowTimerCancelledEvent),
            typeof(WorkflowChildScheduledEvent),
            typeof(WorkflowChildrenScheduledEvent),
            typeof(WorkflowResourcePoolAcquiredEvent),
            typeof(WorkflowResourcePoolQueuedEvent),
            typeof(WorkflowResourcePoolReleasedEvent),
            typeof(WorkflowExternalJobStartedEvent),
            typeof(WorkflowExternalJobCompletedEvent),
            typeof(WorkflowExternalJobTimedOutEvent),
            typeof(WorkflowExternalJobStopRequestedEvent),
            typeof(ScheduleTimerCommand),
            typeof(AcquireResourcePoolCommand),
            typeof(RunExternalJobCommand),
            typeof(DurableRunChildCommand),
            typeof(DurableRunChildrenCommand),
            typeof(DurableActiveWait),
            typeof(DurableActiveTimer),
            typeof(DurablePendingResume),
            typeof(DurableActiveChild),
            typeof(ResourcePoolTicket),
            typeof(ResourcePoolAcquireRequest),
            typeof(ResourcePoolWaiter),
            typeof(DurableActiveExternalJob),
            typeof(DurableActiveChildGroup),
            typeof(ActiveWaitSnapshot),
            typeof(CheckpointPendingResume),
            typeof(CheckpointActiveTimer),
            typeof(CheckpointActiveWait),
            typeof(CheckpointActiveChild),
            typeof(CheckpointActiveChildGroup),
            typeof(CheckpointActiveExternalJob)
        };

        foreach (var contract in ownedContracts)
        {
            var fiberProperty = contract.GetProperty("FiberId");
            fiberProperty.Should().NotBeNull($"{contract.Name} must identify its owning fiber");
            fiberProperty!.PropertyType.Should().Be(typeof(FiberId?));

            var scopeProperty = contract.GetProperty("ScopeId");
            scopeProperty.Should().NotBeNull($"{contract.Name} must identify its owning scope");
            scopeProperty!.PropertyType.Should().Be(typeof(ScopeId?));
        }
    }
}
