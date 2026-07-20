using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class DurableExampleExpectedRedGuards
{
    private static readonly string[] ForbiddenJourneyTokens =
    [
        "Poisoned",
        "DurableCommandProcessor",
        "CompleteExternalJobCommand",
        "RunExternalJobCommand",
        "TimeoutExternalJobCommand",
        "StreamVersion"
    ];

    [Fact]
    public void DurableHostJourney_UsesOnlyApplicationFacade()
    {
        var journey = DurableJourney();

        ForbiddenJourneyTokens.Where(journey.Contains).Should().BeEmpty(
            "the documented durable application journey cannot teach protocol or poison handling");
        journey.Should().Contain("runtime.ExternalJobs.CompleteAsync",
            "external workers report through the typed application facade");
    }

    [Fact]
    public void DurableHostJourney_AuthorsLiveJobBeforeEndAndCompletesItBeforeTerminalAssertion()
    {
        var journey = DurableJourney();
        var definition = journey.IndexOf("Workflow.Durable", StringComparison.Ordinal);
        definition.Should().BeGreaterThanOrEqualTo(0);
        var structuralJob = journey.IndexOf(".RunExternalJob(", definition, StringComparison.Ordinal);
        structuralJob.Should().BeGreaterThan(definition, "the live job belongs to the authored workflow graph");
        var end = journey.IndexOf(".End(", definition, StringComparison.Ordinal);
        end.Should().BeGreaterThan(structuralJob, "the workflow cannot become terminal before dispatching the job");
        var start = journey.IndexOf("StartOrGetAsync", end, StringComparison.Ordinal);
        start.Should().BeGreaterThan(end);
        var completion = journey.IndexOf("runtime.ExternalJobs.CompleteAsync", start, StringComparison.Ordinal);
        completion.Should().BeGreaterThan(start);
        var terminalAssertion = journey.IndexOf("WorkflowStatus.Completed", completion, StringComparison.Ordinal);

        terminalAssertion.Should().BeGreaterThan(completion,
            "the sample proves the facade completion progressed the live instance to terminal state");
    }

    private static string DurableJourney()
    {
        var source = File.ReadAllText(Path.Combine(
            FixtureDefinitions.RepositoryRoot(),
            "samples",
            "OrcaCore.Examples",
            "ExampleRunner.cs"));
        const string signature = "private static async Task RunDurableHostApiAsync";
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        signatureIndex.Should().BeGreaterThanOrEqualTo(0);
        var openingBrace = source.IndexOf('{', signatureIndex);
        openingBrace.Should().BeGreaterThan(signatureIndex);

        var depth = 0;
        for (var index = openingBrace; index < source.Length; index++)
        {
            depth += source[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };
            if (depth == 0)
            {
                return source[openingBrace..(index + 1)];
            }
        }

        throw new InvalidOperationException("Could not locate the end of RunDurableHostApiAsync.");
    }
}
