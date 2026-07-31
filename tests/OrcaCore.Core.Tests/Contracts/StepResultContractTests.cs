using AwesomeAssertions;
using System.Diagnostics;
using System.Reflection;
using OrcaCore.Abstractions.Errors;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class StepResultContractTests
{
    [Fact]
    public void StepResult_Variants_HaveValueEquality()
    {
        var error = new WorkflowLifecycleException("Definition is invalid.");
        var correlationId = CorrelationId.Create("order-123");

        new StepResult.Completed().Should().Be(new StepResult.Completed());
        new StepResult.Failed(error).Should().Be(new StepResult.Failed(error));
        var eventName = EventName.Create("OrderApproved");
        new StepResult.WaitForEvent(eventName, correlationId)
            .Should().Be(new StepResult.WaitForEvent(eventName, correlationId));
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
                _ => throw new UnreachableException()
            };
        }

        Describe(new StepResult.Completed()).Should().Be("completed");
        Describe(new StepResult.Failed(new WorkflowLifecycleException("failed"))).Should().Be("failed");
        Describe(new StepResult.WaitForEvent(EventName.Create("Event"), CorrelationId.Create("corr")))
            .Should().Be("wait");
    }

    [Fact]
    public void StepResult_ExportsOnlyTheThreePortableVariants()
    {
        typeof(StepResult).GetNestedTypes(BindingFlags.Public)
            .Select(type => type.Name)
            .Should().BeEquivalentTo("Completed", "Failed", "WaitForEvent");

        Action nullEvent = () => new StepResult.WaitForEvent(null!, CorrelationId.Create("corr"));
        Action nullCorrelation = () => new StepResult.WaitForEvent(EventName.Create("Event"), null!);
        nullEvent.Should().Throw<ArgumentNullException>().WithParameterName("eventName");
        nullCorrelation.Should().Throw<ArgumentNullException>().WithParameterName("correlationId");
    }

    [Fact]
    public void StepResult_ContainsNoRemovedOrDurableNestedVariants()
    {
        typeof(StepResult).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Select(type => type.Name.Split('`')[0])
            .Should().NotContain([
                "Yield",
                "ContinueAsNew",
                "RunExternalJob",
                "AcquireResources"
            ]);
    }
}
