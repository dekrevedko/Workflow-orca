using System.Diagnostics;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;

namespace OrcaCore.Core.Tests.Contracts;

public class StepResultTests
{
    [Fact]
    public void StepResult_Variants_HaveValueEquality()
    {
        var error = new OrcaCoreException("boom");
        var correlationId = new CorrelationId("corr-1");

        StepResult.Completed completedA = new();
        StepResult.Completed completedB = new();
        completedA.Should().Be(completedB);

        StepResult.Failed failedA = new(error);
        StepResult.Failed failedB = new(error);
        failedA.Should().Be(failedB);

        StepResult.WaitForEvent waitA = new("Approved", correlationId);
        StepResult.WaitForEvent waitB = new("Approved", correlationId);
        waitA.Should().Be(waitB);

        StepResult.Yield yieldA = new();
        StepResult.Yield yieldB = new();
        yieldA.Should().Be(yieldB);
    }

    [Fact]
    public void StepResult_SwitchOverVariants_IsExhaustive()
    {
        StepResult[] results =
        [
            new StepResult.Completed(),
            new StepResult.Failed(new OrcaCoreException("boom")),
            new StepResult.WaitForEvent("Approved", new CorrelationId("corr-1")),
            new StepResult.Yield(),
        ];

        foreach (var result in results)
        {
            var describe = Describe(result);
            describe.Should().NotBeNullOrEmpty();
        }

        static string Describe(StepResult result) => result switch
        {
            StepResult.Completed => "completed",
            StepResult.Failed failed => $"failed:{failed.Error.Message}",
            StepResult.WaitForEvent wait => $"wait:{wait.EventName}",
            StepResult.Yield => "yield",
            _ => throw new UnreachableException(),
        };
    }
}
