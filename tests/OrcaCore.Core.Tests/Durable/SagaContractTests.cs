using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Core.Tests.Durable;

public sealed class SagaContractTests
{
    [Fact]
    [Trait("AC", "AC-401")]
    [Trait("AC", "AC-404")]
    public void SagaCommandAndEventCatalog_ExposesCompensationFacts()
    {
        typeof(RequestSagaCompensationCommand).Should().BeAssignableTo<WorkflowCommand>();
        typeof(SagaCompensationRequestedEvent).Should().BeAssignableTo<DurableWorkflowEvent>();
        typeof(SagaCompensationStartedEvent).Should().BeAssignableTo<DurableWorkflowEvent>();
        typeof(SagaCompensationCompletedEvent).Should().BeAssignableTo<DurableWorkflowEvent>();
        typeof(SagaCompensationFailedEvent).Should().BeAssignableTo<DurableWorkflowEvent>();
    }
}
