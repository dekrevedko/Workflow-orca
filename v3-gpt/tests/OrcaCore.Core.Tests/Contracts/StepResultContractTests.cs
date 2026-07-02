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

        Assert.Equal(new StepResult.Completed(), new StepResult.Completed());
        Assert.Equal(new StepResult.Failed(error), new StepResult.Failed(error));
        Assert.Equal(
            new StepResult.WaitForEvent("OrderApproved", correlationId),
            new StepResult.WaitForEvent("OrderApproved", correlationId));
        Assert.Equal(new StepResult.Yield(), new StepResult.Yield());
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

        Assert.Equal("completed", Describe(new StepResult.Completed()));
        Assert.Equal("failed", Describe(new StepResult.Failed(new WorkflowDefinitionException("failed"))));
        Assert.Equal("wait", Describe(new StepResult.WaitForEvent("Event", new CorrelationId("corr"))));
        Assert.Equal("yield", Describe(new StepResult.Yield()));
    }
}
