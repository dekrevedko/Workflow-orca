using AwesomeAssertions;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Lifecycle;
using Xunit;

namespace OrcaCore.Core.Tests.Lifecycle;

public sealed class SagaLifecycleTests
{
    [Fact]
    [Trait("AC", "AC-401")]
    [Trait("AC", "AC-404")]
    public void SagaLifecycle_CompensatedAndCompensationFailed_AreTerminal()
    {
        LifecycleMachine.TerminalStatuses.Should().Contain(
            [
                WorkflowStatus.Compensated,
                WorkflowStatus.CompensationFailed
            ]);
    }
}
