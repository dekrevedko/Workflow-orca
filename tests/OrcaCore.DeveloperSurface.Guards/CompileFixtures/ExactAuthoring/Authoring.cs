using OrcaCore;
using OrcaCore.Durable.Hosting;
using OrcaCore.Hosting;

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
        typeof(EphemeralWorkflowRef<>), typeof(EphemeralWorkflowRef<,>),
        typeof(DurableWorkflowRef<>), typeof(DurableWorkflowRef<,>),
        typeof(EventContractVersion), typeof(WorkflowEventContract), typeof(WorkflowEventContract<>),
        typeof(WorkflowEventRoute), typeof(WorkflowInboundEvent), typeof(WorkflowInboundEvent<>),
        typeof(WorkflowEventAcceptanceResult), typeof(WorkflowEventAcceptanceRejection),
        typeof(WorkflowOutboundEvent), typeof(WorkflowEventDispatchFailure), typeof(WorkflowEventDispatchResult),
        typeof(IWorkflowEventIngress), typeof(IWorkflowEventDispatcher),
        typeof(OrcaCoreEphemeralEngineBuilder), typeof(OrcaCoreDurableEngineBuilder)
    ];
}
