using OrcaCore;

public static class ExactAuthoring
{
    public static readonly Type[] RequiredFamilies =
    [
        typeof(EphemeralWorkflowInitBuilder<>), typeof(DurableWorkflowInitBuilder<>),
        typeof(EphemeralWorkflowBuilder<,>), typeof(DurableWorkflowBuilder<,>),
        typeof(EphemeralNestedBuilder<,>), typeof(DurableNestedBuilder<,>),
        typeof(EphemeralBranchBuilder<,>), typeof(DurableBranchBuilder<,>),
        typeof(EphemeralItemBuilder<,>), typeof(DurableItemBuilder<,>),
        typeof(DurableLeaseWorkflowBuilder<,>), typeof(DurableLeaseNestedBuilder<,>),
        typeof(DurableLeaseBranchBuilder<,>), typeof(DurableLeaseItemBuilder<,>),
        typeof(EphemeralWorkflowParallelBranchScopeBuilder<,,>),
        typeof(DurableWorkflowParallelBranchScopeBuilder<,,>),
        typeof(EphemeralWorkflowParallelJoinBuilder<,,>), typeof(DurableWorkflowParallelJoinBuilder<,,>),
        typeof(EphemeralForEachJoinBuilder<,,>), typeof(DurableForEachJoinBuilder<,,>),
        typeof(EphemeralWorkflowCompletionBuilder<>), typeof(EphemeralWorkflowCompletionBuilder<,>),
        typeof(DurableWorkflowCompletionBuilder<>), typeof(DurableWorkflowCompletionBuilder<,>),
        typeof(EphemeralWorkflowDefinition<>), typeof(EphemeralWorkflowDefinition<,>),
        typeof(DurableWorkflowDefinition<>), typeof(DurableWorkflowDefinition<,>),
        typeof(DurableWorkflowRef<>), typeof(DurableWorkflowRef<,>)
    ];
}
