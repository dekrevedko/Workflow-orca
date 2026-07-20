using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class FacadeOutcomeExpectedRedGuards
{
    [Fact]
    public async Task CorrelationRouting_NoMatch_IsTypedInsteadOfThrowing()
    {
        var host = DurableBehaviorHarness.CreateHost();
        var cancellationToken = TestContext.Current.CancellationToken;
        var definitionsBefore = host.Registry.List().ToArray();
        var instancesBefore = await host.Store.ListAsync(new WorkflowProjectionQuery(), cancellationToken);

        object? result = null;
        Exception? failure = null;
        try
        {
            result = await host.Runtime.RaiseEventByCorrelationAsync(
                "Approved",
                new CorrelationId("missing"),
                new Payload("value"),
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        host.Registry.List().Should().BeEquivalentTo(definitionsBefore,
            "a no-match route cannot register or replace a definition");
        (await host.Store.ListAsync(new WorkflowProjectionQuery(), cancellationToken))
            .Should().BeEquivalentTo(instancesBefore,
                "a no-match route cannot create or mutate an instance projection");
        failure.Should().BeNull("no match is an expected application routing outcome");
        ExpectedPublicApi.OutcomeName(result).Should().Be("NoMatch");
    }

    [Fact]
    public async Task CorrelationRouting_AmbiguousMatch_IsTypedAndSelectsNothing()
    {
        var definitionId = DefinitionId.New();
        var host = DurableBehaviorHarness.CreateHost(
            definition: DurableBehaviorHarness.WaitDefinition(definitionId, "shared", "Approved"));
        var first = await DurableBehaviorHarness.StartAndPumpAsync(
            host, definitionId, TestContext.Current.CancellationToken);
        var second = await DurableBehaviorHarness.StartAndPumpAsync(
            host, definitionId, TestContext.Current.CancellationToken);

        object? result = null;
        Exception? failure = null;
        try
        {
            result = await host.Runtime.RaiseEventByCorrelationAsync(
                "Approved",
                new CorrelationId("shared"),
                new Payload("value"),
                cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        failure.Should().BeNull("ambiguity is an expected application routing outcome");
        ExpectedPublicApi.OutcomeName(result).Should().Be("AmbiguousMatch");
        (await DurableBehaviorHarness.TailAsync(host, first, TestContext.Current.CancellationToken))
            .OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty("an ambiguous route must not choose the first target");
        (await DurableBehaviorHarness.TailAsync(host, second, TestContext.Current.CancellationToken))
            .OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty("an ambiguous route must not choose the second target");
    }

    [Fact]
    public async Task InstanceRouting_LiveUnmatched_IsDistinctFromMissingInstance()
    {
        var definitionId = DefinitionId.New();
        var host = DurableBehaviorHarness.CreateHost(
            definition: DurableBehaviorHarness.WaitDefinition(definitionId, "none", "OtherEvent"));
        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host, definitionId, TestContext.Current.CancellationToken);
        var before = await DurableBehaviorHarness.TailAsync(
            host,
            instanceId,
            TestContext.Current.CancellationToken);

        var result = await host.Runtime.RaiseEventAsync(
            instanceId,
            "Approved",
            new CorrelationId("none"),
            cancellationToken: TestContext.Current.CancellationToken);

        var after = await DurableBehaviorHarness.TailAsync(
            host,
            instanceId,
            TestContext.Current.CancellationToken);
        after.Should().BeEquivalentTo(before, options => options.WithStrictOrdering(),
            "a live but unmatched event cannot append a fact or advance the instance");
        ExpectedPublicApi.OutcomeName(result).Should().Be("LiveUnmatched");
    }

    [Fact]
    public async Task InstanceRouting_PausedTarget_IsTypedAndDoesNotApplyEvent()
    {
        var definitionId = DefinitionId.New();
        var host = DurableBehaviorHarness.CreateHost(
            definition: DurableBehaviorHarness.WaitDefinition(definitionId, "paused", "Approved"));
        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host, definitionId, TestContext.Current.CancellationToken);
        var paused = await host.Runtime.Management.PauseAsync(
            instanceId,
            host.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);
        paused.Outcome.Should().Be(DurableCommandOutcome.Committed);

        var result = await host.Runtime.RaiseEventAsync(
            instanceId,
            "Approved",
            new CorrelationId("paused"),
            cancellationToken: TestContext.Current.CancellationToken);

        ExpectedPublicApi.OutcomeName(result).Should().Be("TargetPaused");
        var tail = await DurableBehaviorHarness.TailAsync(host, instanceId, TestContext.Current.CancellationToken);
        tail.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Start_DefinitionNotRegistered_IsTypedAndDoesNotMutateRegistration()
    {
        var host = DurableBehaviorHarness.CreateHost();
        var definitionId = DefinitionId.New();
        var cancellationToken = TestContext.Current.CancellationToken;
        var definitionsBefore = host.Registry.List().ToArray();
        var instancesBefore = await host.Store.ListAsync(new WorkflowProjectionQuery(), cancellationToken);
        object? result = null;
        Exception? failure = null;
        try
        {
            result = await host.Runtime.StartOrGetAsync<string, DurableBehaviorHarness.GuardState>(
                "missing-definition",
                definitionId,
                DefinitionVersion.Initial,
                "input",
                cancellationToken);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        host.Registry.List().Should().BeEquivalentTo(definitionsBefore,
            "start against an unknown definition cannot register it as a side effect");
        (await host.Store.ListAsync(new WorkflowProjectionQuery(), cancellationToken))
            .Should().BeEquivalentTo(instancesBefore,
                "definition-not-registered cannot create an instance or append a start transition");
        failure.Should().BeNull("missing host registration is an application result, not an exception");
        ExpectedPublicApi.OutcomeName(result).Should().Be("DefinitionNotRegistered");
    }

    [Fact]
    public async Task Remediation_StaleTicket_IsCompareAndActSafe()
    {
        var definitionId = DefinitionId.New();
        var host = DurableBehaviorHarness.CreateHost(
            definition: DurableBehaviorHarness.WaitDefinition(definitionId, "stale", "Never"));
        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host, definitionId, TestContext.Current.CancellationToken);
        var before = (await DurableBehaviorHarness.TailAsync(
            host,
            instanceId,
            TestContext.Current.CancellationToken)).Count;
        var handle = host.Runtime.Management.Instance(instanceId);

        var result = await ExpectedPublicApi.InvokeAsync(
            handle,
            "RearmAsync",
            new RemediationTicket("stale-ticket"),
            TestContext.Current.CancellationToken);

        ExpectedPublicApi.OutcomeName(result).Should().Be("StaleRemediation");
        var after = (await DurableBehaviorHarness.TailAsync(
            host,
            instanceId,
            TestContext.Current.CancellationToken)).Count;
        after.Should().Be(before, "a stale compare-and-act token cannot mutate the stream");
    }

    [Theory]
    [InlineData("CompleteAsync", "AppliedAndProgressed", "Completed")]
    [InlineData("TimeoutAsync", "AppliedAndProgressed", "Failed")]
    [InlineData("FailAsync", "AppliedAndProgressed", "Failed")]
    public async Task ExternalJobOutcome_UsesFacadeAndReturnsProgressionDisposition(
        string operation,
        string expectedOutcome,
        string expectedWorkflowStatus)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var definitionId = DefinitionId.New();
        var key = $"job-{operation}";
        var host = DurableBehaviorHarness.CreateHost(
            definition: DurableBehaviorHarness.ExternalJobDefinition(definitionId, key));
        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(host, definitionId, cancellationToken);
        (await DurableBehaviorHarness.SnapshotAsync(host, instanceId, cancellationToken))
            .Status.Should().Be(WorkflowStatus.Waiting);
        var externalJobs = ExpectedPublicApi.RequiredProperty(host.Runtime, "ExternalJobs");

        object? result = operation switch
        {
            "CompleteAsync" => await ExpectedPublicApi.InvokeAsync(
                externalJobs,
                operation,
                instanceId,
                key,
                EventId.New(),
                new Payload("complete"),
                cancellationToken),
            "TimeoutAsync" => await ExpectedPublicApi.InvokeAsync(
                externalJobs,
                operation,
                instanceId,
                key,
                cancellationToken),
            _ => await ExpectedPublicApi.InvokeAsync(
                externalJobs,
                operation,
                instanceId,
                key,
                EventId.New(),
                "worker rejected the request",
                new Payload("failure-detail"),
                cancellationToken)
        };

        ExpectedPublicApi.OutcomeName(result).Should().Be(expectedOutcome);
        (await DurableBehaviorHarness.SnapshotAsync(host, instanceId, cancellationToken))
            .Status.ToString().Should().Be(expectedWorkflowStatus);
    }

    private sealed record Payload(string Value);

    private sealed record RemediationTicket(string Value);
}
