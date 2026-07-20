using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class SplitHostExpectedRedGuards
{
    [Fact]
    public async Task DefinitionlessCallback_CommitsAndDefinitionOwnerProgressesExactlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DurableBehaviorHarness.Epoch);
        var store = new OrcaCore.Providers.InMemory.InMemoryWorkflowProvider(clock);
        var pools = new OrcaCore.Providers.InMemory.InMemoryResourcePoolStore();

        var baselineId = DefinitionId.New();
        var baselineKey = "baseline-job";
        var baselineOwner = DurableBehaviorHarness.CreateHost(
            store,
            pools,
            clock,
            DurableBehaviorHarness.ExternalJobDefinition(baselineId, baselineKey));
        var callback = DurableBehaviorHarness.CreateHost(store, pools, clock);
        var baselineInstance = await DurableBehaviorHarness.StartAndPumpAsync(
            baselineOwner,
            baselineId,
            cancellationToken);
        (await DurableBehaviorHarness.SnapshotAsync(baselineOwner, baselineInstance, cancellationToken))
            .Status.Should().Be(WorkflowStatus.Waiting);

        var committed = await callback.Processor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = baselineInstance,
                RequestedAt = clock.GetUtcNow(),
                ExternalJobId = baselineKey,
                CompletionEventId = EventId.New()
            },
            cancellationToken);
        committed.Outcome.Should().Be(DurableCommandOutcome.Committed,
            "the shared kernel already supports definition-less commit plus continuation outbox");
        (await DurableBehaviorHarness.SnapshotAsync(baselineOwner, baselineInstance, cancellationToken))
            .Status.Should().NotBe(WorkflowStatus.Completed,
                "the callback commit unblocks the instance but cannot execute its definition");
        (await DurableBehaviorHarness.PumpOnceAsync(baselineOwner, cancellationToken)).Should().BeGreaterThan(0);
        (await DurableBehaviorHarness.SnapshotAsync(baselineOwner, baselineInstance, cancellationToken))
            .Status.Should().Be(WorkflowStatus.Completed);
        DurableBehaviorHarness.ObserveExternalJobCompletionStep.Executions[baselineKey].Should().Be(1);
        await DurableBehaviorHarness.PumpOnceAsync(baselineOwner, cancellationToken);
        DurableBehaviorHarness.ObserveExternalJobCompletionStep.Executions[baselineKey].Should().Be(1,
            "duplicate/stale continuation claims cannot re-run committed work");

        var facadeId = DefinitionId.New();
        var facadeKey = "facade-job";
        var facadeOwner = DurableBehaviorHarness.CreateHost(
            store,
            pools,
            clock,
            DurableBehaviorHarness.ExternalJobDefinition(facadeId, facadeKey));
        var facadeInstance = await DurableBehaviorHarness.StartAndPumpAsync(facadeOwner, facadeId, cancellationToken);
        var externalJobs = ExpectedPublicApi.RequiredProperty(callback.Runtime, "ExternalJobs");
        var applicationResult = await ExpectedPublicApi.InvokeAsync(
            externalJobs,
            "CompleteAsync",
            facadeInstance,
            facadeKey,
            EventId.New(),
            new CompletionPayload("accepted"),
            cancellationToken);
        ExpectedPublicApi.OutcomeName(applicationResult).Should().Be("AppliedPendingContinuation");
        (await DurableBehaviorHarness.SnapshotAsync(facadeOwner, facadeInstance, cancellationToken))
            .Status.Should().Be(WorkflowStatus.Waiting);

        (await DurableBehaviorHarness.PumpOnceAsync(facadeOwner, cancellationToken)).Should().BeGreaterThan(0);
        (await DurableBehaviorHarness.SnapshotAsync(facadeOwner, facadeInstance, cancellationToken))
            .Status.Should().Be(WorkflowStatus.Completed);
        DurableBehaviorHarness.ObserveExternalJobCompletionStep.Executions[facadeKey].Should().Be(1);
        await DurableBehaviorHarness.PumpOnceAsync(facadeOwner, cancellationToken);
        DurableBehaviorHarness.ObserveExternalJobCompletionStep.Executions[facadeKey].Should().Be(1);
    }

    private sealed record CompletionPayload(string Result);
}
