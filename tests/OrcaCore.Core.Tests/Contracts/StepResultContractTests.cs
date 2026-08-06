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
        var eventContract = WorkflowEventContract.Create(
            EventName.Create("OrderApproved"),
            EventContractVersion.Initial);
        new StepResult.WaitForEvent(eventContract, correlationId)
            .Should().Be(new StepResult.WaitForEvent(eventContract, correlationId));
        var typedContract = WorkflowEventContract<string>.Create(
            eventContract.EventName,
            eventContract.Version);
        new StepResult.WaitForEvent<string>(typedContract, correlationId)
            .Should().Be(new StepResult.WaitForEvent<string>(typedContract, correlationId));
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
                _ when result.GetType().IsGenericType &&
                    result.GetType().GetGenericTypeDefinition() == typeof(StepResult.WaitForEvent<>) =>
                    "typed-wait",
                _ => throw new UnreachableException()
            };
        }

        Describe(new StepResult.Completed()).Should().Be("completed");
        Describe(new StepResult.Failed(new WorkflowLifecycleException("failed"))).Should().Be("failed");
        Describe(new StepResult.WaitForEvent(
                WorkflowEventContract.Create(EventName.Create("Event"), EventContractVersion.Initial),
                CorrelationId.Create("corr")))
            .Should().Be("wait");
        Describe(new StepResult.WaitForEvent<string>(
                WorkflowEventContract<string>.Create(EventName.Create("TypedEvent"), EventContractVersion.Initial),
                CorrelationId.Create("typed-corr")))
            .Should().Be("typed-wait");
    }

    [Fact]
    public void StepResult_ExportsOnlyTheFourPortableVariants()
    {
        typeof(StepResult).GetNestedTypes(BindingFlags.Public)
            .Select(type => type.Name)
            .Should().BeEquivalentTo("Completed", "Failed", "WaitForEvent", "WaitForEvent`1");

        Action nullEvent = () => new StepResult.WaitForEvent(null!, CorrelationId.Create("corr"));
        Action nullCorrelation = () => new StepResult.WaitForEvent(
            WorkflowEventContract.Create(EventName.Create("Event"), EventContractVersion.Initial),
            null!);
        nullEvent.Should().Throw<ArgumentNullException>().WithParameterName("eventContract");
        nullCorrelation.Should().Throw<ArgumentNullException>().WithParameterName("correlationId");
        Action nullTypedEvent = () => new StepResult.WaitForEvent<string>(
            null!,
            CorrelationId.Create("corr"));
        nullTypedEvent.Should().Throw<ArgumentNullException>().WithParameterName("eventContract");
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
