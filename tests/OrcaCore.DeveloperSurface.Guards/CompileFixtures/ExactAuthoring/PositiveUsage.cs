using OrcaCore;

public static class PositiveUsage
{
    public static void ExerciseAllStagedFamilies(
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
        EphemeralWorkflowParallelBranchScopeBuilder<object, object, object> ephemeralScope,
        DurableWorkflowParallelBranchScopeBuilder<object, object, object> durableScope,
        EphemeralWorkflowParallelJoinBuilder<object, object, object> ephemeralJoin,
        DurableWorkflowParallelJoinBuilder<object, object, object> durableJoin,
        EphemeralForEachJoinBuilder<object, object, object> ephemeralForEachJoin,
        DurableForEachJoinBuilder<object, object, object> durableForEachJoin,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        EventName eventName,
        CorrelationId correlationId,
        WorkflowOutcomeName outcome,
        AuthoredBranchId branchId,
        TransientPoolName transientPool,
        ResourceLeaseRequest leaseRequest,
        ForEachOptions options)
    {
        _ = Workflow.Ephemeral<object>(definitionId, definitionVersion).Init<object>(_ => new object());
        _ = Workflow.Durable<object>(definitionId, definitionVersion).Init<object>(_ => new object());

        _ = ephemeral.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask)
            .Then((_, _) => ValueTask.CompletedTask).WithRetry(2).WithStepTimeout(TimeSpan.Zero)
            .WithTransientPool(transientPool).CompleteWithin(TimeSpan.Zero)
            .If(_ => true, _ => { }, _ => { }).While(_ => false, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero)
            .Delay(TimeSpan.Zero);
        _ = ephemeral.Parallel<object>(_ => { });
        _ = ephemeral.ForEach<object, object, object>(_ => Array.Empty<object>(), options, x => x.Item, _ => { });
        _ = ephemeral.End(); _ = ephemeral.End(outcome); _ = ephemeral.End(_ => new object()); _ = ephemeral.End(_ => new object(), outcome);

        _ = durable.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).CompleteWithin(TimeSpan.Zero)
            .If(_ => true, _ => { }, _ => { }).While(_ => false, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero)
            .AcquireResources(leaseRequest, _ => { }).AcquireResources(_ => leaseRequest, _ => { });
        _ = durable.Parallel<object>(_ => { });
        _ = durable.ForEach<object, object, object>(_ => Array.Empty<object>(), options, x => x.Item, _ => { });
        _ = durable.ContinueAsNew(_ => new object());
        _ = durable.End(); _ = durable.End(outcome); _ = durable.End(_ => new object()); _ = durable.End(_ => new object(), outcome);

        _ = ephemeralNested.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask).Then((_, _) => ValueTask.CompletedTask)
            .WithRetry(2).WithStepTimeout(TimeSpan.Zero).WithTransientPool(transientPool)
            .If(_ => true, _ => { }).Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero);
        _ = durableNested.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero)
            .AcquireResources(leaseRequest, _ => { }).AcquireResources(_ => leaseRequest, _ => { });

        _ = ephemeralBranch.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask).Then((_, _) => ValueTask.CompletedTask)
            .WithRetry(2).WithStepTimeout(TimeSpan.Zero).WithTransientPool(transientPool).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero).Return(_ => new object());
        _ = ephemeralItem.Then<ProbeStep>().Then(_ => ValueTask.CompletedTask).Then((_, _) => ValueTask.CompletedTask)
            .WithRetry(2).WithStepTimeout(TimeSpan.Zero).WithTransientPool(transientPool).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero).Return(_ => new object());
        _ = durableBranch.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero)
            .AcquireResources(leaseRequest, _ => { }).AcquireResources(_ => leaseRequest, _ => { }).Return(_ => new object());
        _ = durableItem.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero)
            .AcquireResources(leaseRequest, _ => { }).AcquireResources(_ => leaseRequest, _ => { }).Return(_ => new object());

        _ = leaseRoot.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero);
        _ = leaseNested.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero);
        _ = leaseBranch.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero).Return(_ => new object());
        _ = leaseItem.Then<ProbeStep>().WithRetry(2).WithStepTimeout(TimeSpan.Zero).If(_ => true, _ => { })
            .Wait(eventName, _ => correlationId).Wait(eventName, _ => correlationId, TimeSpan.Zero).Delay(TimeSpan.Zero).Return(_ => new object());

        _ = ephemeralScope.Branch<object>(branchId, _ => new object(), _ => { });
        _ = durableScope.Branch<object>(branchId, _ => new object(), _ => { });
        _ = ephemeralJoin.WhenAll((_, _) => new object()); _ = ephemeralJoin.WhenAllOutcomes((_, _) => new object());
        _ = durableJoin.WhenAll((_, _) => new object()); _ = durableJoin.WhenAllOutcomes((_, _) => new object());
        _ = ephemeralForEachJoin.WhenAll((_, _) => new object()); _ = ephemeralForEachJoin.WhenAllOutcomes((_, _) => new object());
        _ = durableForEachJoin.WhenAll((_, _) => new object()); _ = durableForEachJoin.WhenAllOutcomes((_, _) => new object());

        _ = default(EphemeralWorkflowCompletionBuilder<object>)!.Build();
        _ = default(EphemeralWorkflowCompletionBuilder<object>)!.TryBuild();
        _ = default(EphemeralWorkflowCompletionBuilder<object, object>)!.Build();
        _ = default(EphemeralWorkflowCompletionBuilder<object, object>)!.TryBuild();
        _ = default(DurableWorkflowCompletionBuilder<object>)!.Build();
        _ = default(DurableWorkflowCompletionBuilder<object>)!.TryBuild();
        _ = default(DurableWorkflowCompletionBuilder<object, object>)!.Build();
        _ = default(DurableWorkflowCompletionBuilder<object, object>)!.TryBuild();

        ReadMetadata(default(EphemeralWorkflowDefinition<object>)!);
        ReadMetadata(default(EphemeralWorkflowDefinition<object, object>)!);
        ReadMetadata(default(DurableWorkflowDefinition<object>)!);
        ReadMetadata(default(DurableWorkflowDefinition<object, object>)!);
        ReadMetadata(default(DurableWorkflowRef<object>)!);
        ReadMetadata(default(DurableWorkflowRef<object, object>)!);
    }

    private static void ReadMetadata(dynamic value)
    {
        _ = value.Mode; _ = value.DefinitionId; _ = value.DefinitionVersion; _ = value.DefinitionFingerprint;
    }

    private sealed class ProbeStep : IStep<object>;
}
