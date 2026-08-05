using OrcaCore;

public static class ProductPositiveUsage
{
    public static void ExerciseCurrentNonMessagingAuthoring(
        EphemeralWorkflowBuilder<object, object> ephemeral,
        DurableWorkflowBuilder<object, object> durable,
        EphemeralNestedBuilder<object, object> ephemeralNested,
        DurableNestedBuilder<object, object> durableNested,
        EphemeralBranchBuilder<object, object> ephemeralBranch,
        DurableBranchBuilder<object, object> durableBranch,
        EphemeralItemBuilder<object, object> ephemeralItem,
        DurableItemBuilder<object, object> durableItem,
        DurableLeaseWorkflowBuilder<object, object> leaseRoot,
        DurableLeaseNestedBuilder<object, object> leaseNested,
        DurableLeaseBranchBuilder<object, object> leaseBranch,
        DurableLeaseItemBuilder<object, object> leaseItem,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        CorrelationId correlationId,
        WorkflowOutcomeName outcome,
        TransientPoolName transientPool,
        ResourceLeaseRequest leaseRequest)
    {
        _ = Workflow.Ephemeral<object>(definitionId, definitionVersion).Init<object>(_ => new object());
        _ = Workflow.Durable<object>(definitionId, definitionVersion).Init<object>(_ => new object());

        _ = ephemeral.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask)
            .Then((_, _) => ValueTask.CompletedTask).WithRetry(2).WithStepTimeout(TimeSpan.Zero)
            .WithTransientPool(transientPool).CompleteWithin(TimeSpan.Zero)
            .If(_ => true, _ => { }, _ => { }).While(_ => false, _ => { }).Delay(TimeSpan.Zero);
        _ = durable.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).CompleteWithin(TimeSpan.Zero)
            .If(_ => true, _ => { }, _ => { }).While(_ => false, _ => { }).Delay(TimeSpan.Zero)
            .AcquireResources(leaseRequest, _ => { }).AcquireResources(_ => leaseRequest, _ => { });

        _ = ephemeralNested.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask).Then((_, _) => ValueTask.CompletedTask)
            .WithRetry(2).WithStepTimeout(TimeSpan.Zero).WithTransientPool(transientPool)
            .If(_ => true, _ => { }).Delay(TimeSpan.Zero);
        _ = durableNested.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Delay(TimeSpan.Zero).AcquireResources(leaseRequest, _ => { }).AcquireResources(_ => leaseRequest, _ => { });

        _ = ephemeralBranch.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask).Then((_, _) => ValueTask.CompletedTask)
            .WithRetry(2).WithStepTimeout(TimeSpan.Zero).WithTransientPool(transientPool).If(_ => true, _ => { })
            .Delay(TimeSpan.Zero).Return(_ => new object());
        _ = ephemeralItem.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask).Then((_, _) => ValueTask.CompletedTask)
            .WithRetry(2).WithStepTimeout(TimeSpan.Zero).WithTransientPool(transientPool).If(_ => true, _ => { })
            .Delay(TimeSpan.Zero).Return(_ => new object());
        _ = durableBranch.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Delay(TimeSpan.Zero).AcquireResources(leaseRequest, _ => { })
            .AcquireResources(_ => leaseRequest, _ => { }).Return(_ => new object());
        _ = durableItem.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Delay(TimeSpan.Zero).AcquireResources(leaseRequest, _ => { })
            .AcquireResources(_ => leaseRequest, _ => { }).Return(_ => new object());

        _ = leaseRoot.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero)
            .If(_ => true, _ => { }).Delay(TimeSpan.Zero);
        _ = leaseNested.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero)
            .If(_ => true, _ => { }).Delay(TimeSpan.Zero);
        _ = leaseBranch.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero)
            .If(_ => true, _ => { }).Delay(TimeSpan.Zero).Return(_ => new object());
        _ = leaseItem.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero)
            .If(_ => true, _ => { }).Delay(TimeSpan.Zero).Return(_ => new object());

        _ = ephemeral.End();
        _ = ephemeral.End(outcome);
        _ = durable.End();
        _ = durable.End(outcome);
        _ = correlationId;
        _ = default(DurableWorkflowRef<object>);
        _ = default(DurableWorkflowRef<object, object>);
    }

    private sealed class ProbeStep : IStep<object>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<object> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
