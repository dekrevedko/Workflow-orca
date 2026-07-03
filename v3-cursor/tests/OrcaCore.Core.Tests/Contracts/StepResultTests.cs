using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class StepResultTests
{
    [Fact]
    public void StepResult_Variants_HaveValueEquality()
    {
        var completedA = new StepResult.Completed();
        var completedB = new StepResult.Completed();
        completedA.Should().Be(completedB);

        var error = new OrcaCoreException("step failed");
        var failedA = new StepResult.Failed(error);
        var failedB = new StepResult.Failed(error);
        failedA.Should().Be(failedB);

        var correlation = new CorrelationId("corr-1");
        var waitA = new StepResult.WaitForEvent("OrderCreated", correlation);
        var waitB = new StepResult.WaitForEvent("OrderCreated", correlation);
        waitA.Should().Be(waitB);

        var yieldA = new StepResult.Yield();
        var yieldB = new StepResult.Yield();
        yieldA.Should().Be(yieldB);
    }

    [Fact]
    public void StepResult_SwitchOverVariants_IsExhaustive()
    {
        StepResult[] results =
        [
            new StepResult.Completed(),
            new StepResult.Failed(new OrcaCoreException("failed")),
            new StepResult.WaitForEvent("evt", new CorrelationId("c")),
            new StepResult.Yield(),
        ];

        foreach (var result in results)
        {
            var label = ClassifyStepResult(result);
            label.Should().NotBeNullOrEmpty();
        }
    }

    private static string ClassifyStepResult(StepResult result) =>
        result switch
        {
            StepResult.Completed => nameof(StepResult.Completed),
            StepResult.Failed => nameof(StepResult.Failed),
            StepResult.WaitForEvent => nameof(StepResult.WaitForEvent),
            StepResult.Yield => nameof(StepResult.Yield),
            _ => throw new InvalidOperationException("Unexpected StepResult variant."),
        };
}
