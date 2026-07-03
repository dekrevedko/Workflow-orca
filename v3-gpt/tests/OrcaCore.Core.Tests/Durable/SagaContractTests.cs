using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using Xunit;

namespace OrcaCore.Core.Tests.Durable;

public sealed class SagaContractTests
{
    [Fact]
    [Trait("AC", "AC-401")]
    [Trait("AC", "AC-404")]
    public void SagaCommandAndEventCatalog_ExposesCompensationFacts()
    {
        typeof(RequestSagaCompensationCommand).Should().BeAssignableTo<WorkflowCommand>();
        typeof(SagaCompensationRequestedEvent).Should().BeAssignableTo<WorkflowEvent>();
        typeof(SagaCompensationStartedEvent).Should().BeAssignableTo<WorkflowEvent>();
        typeof(SagaCompensationCompletedEvent).Should().BeAssignableTo<WorkflowEvent>();
        typeof(SagaCompensationFailedEvent).Should().BeAssignableTo<WorkflowEvent>();
    }
}
