using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using System.Diagnostics;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class StepResultContractTests
{
    [Fact]
    public void StepResult_Variants_HaveValueEquality()
    {
        var error = new WorkflowDefinitionException("Definition is invalid.");
        var correlationId = new CorrelationId("order-123");

        new StepResult.Completed().Should().Be(new StepResult.Completed());
        new StepResult.Failed(error).Should().Be(new StepResult.Failed(error));
        new StepResult.WaitForEvent("OrderApproved", correlationId)
            .Should().Be(new StepResult.WaitForEvent("OrderApproved", correlationId));
        new StepResult.Yield().Should().Be(new StepResult.Yield());
    }

    [Fact]
    public void StepResult_SwitchOverVariants_IsExhaustive()
    {
        static string Describe(StepResult result)
        {
            return result switch
            {
                StepResult.Completed => "completed",
                StepResult.Failed => "failed",
                StepResult.WaitForEvent => "wait",
                StepResult.Yield => "yield",
                _ => throw new UnreachableException()
            };
        }

        Describe(new StepResult.Completed()).Should().Be("completed");
        Describe(new StepResult.Failed(new WorkflowDefinitionException("failed"))).Should().Be("failed");
        Describe(new StepResult.WaitForEvent("Event", new CorrelationId("corr"))).Should().Be("wait");
        Describe(new StepResult.Yield()).Should().Be("yield");
    }
}
