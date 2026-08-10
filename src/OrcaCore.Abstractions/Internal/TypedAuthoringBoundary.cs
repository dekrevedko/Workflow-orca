using System.Reflection;
using System.Runtime.CompilerServices;

namespace OrcaCore.Internal;

internal sealed class AuthoringKernelHandle
{
    internal AuthoringKernelHandle(object value) =>
        Value = value ?? throw new ArgumentNullException(nameof(value));

    internal object Value { get; }
}

internal sealed class NoAuthoringValue;

internal interface ITypedAuthoringOperations
{
    AuthoringKernelHandle Ephemeral<TState>(DefinitionId definitionId, DefinitionVersion definitionVersion);

    AuthoringKernelHandle Durable<TState>(DefinitionId definitionId, DefinitionVersion definitionVersion);

    AuthoringKernelHandle Init<TInput, TState>(AuthoringKernelHandle handle, Func<TInput, TState> createState);

    void Then<TInput, TState, TResult, TStep>(AuthoringKernelHandle handle) where TStep : IStep<TState>;

    void Then<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<StepContext<TState>, ValueTask> body);

    void Then<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<StepContext<TState>, CancellationToken, ValueTask> body);

    void WithRetry<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        int maxAttempts,
        TimeSpan? fixedDelay);

    void WithStepTimeout<TInput, TState, TResult>(AuthoringKernelHandle handle, TimeSpan timeout);

    void WithTransientPool<TInput, TState, TResult>(AuthoringKernelHandle handle, TransientPoolName pool);

    void CompleteWithin<TInput, TState>(AuthoringKernelHandle handle, TimeSpan timeout);

    void If<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<AuthoringKernelHandle> then,
        Action<AuthoringKernelHandle>? otherwise);

    void While<TInput, TState>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<AuthoringKernelHandle> body);

    void Wait<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan? timeout);

    void Publish<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation);

    void Publish<TInput, TState, TResult, TPayload>(
        AuthoringKernelHandle handle,
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload);

    void Delay<TInput, TState, TResult>(AuthoringKernelHandle handle, TimeSpan duration);

    void AcquireResources<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        ResourceLeaseRequest request,
        Action<AuthoringKernelHandle> body);

    void AcquireResources<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<AuthoringKernelHandle> body);

    void Return<TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TResult> result);

    AuthoringKernelHandle Parallel<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Action<AuthoringKernelHandle> branches);

    AuthoringKernelHandle ForEach<TInput, TState, TItem, TItemState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<AuthoringKernelHandle> body);

    AuthoringKernelHandle End<TInput, TState>(AuthoringKernelHandle handle);

    AuthoringKernelHandle End<TInput, TState>(AuthoringKernelHandle handle, WorkflowOutcomeName outcome);

    AuthoringKernelHandle End<TInput, TState, TOutput>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output);

    AuthoringKernelHandle End<TInput, TState, TOutput>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome);

    AuthoringKernelHandle ContinueAsNew<TInput, TState>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TState> replacementState);

    void Branch<TInput, TState, TResult, TBranchState>(
        AuthoringKernelHandle handle,
        AuthoredBranchId branchId,
        Func<ReadOnlyStateSnapshot<TState>, TBranchState> input,
        Action<AuthoringKernelHandle> body);

    AuthoringKernelHandle ParallelWhenAll<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge);

    AuthoringKernelHandle ParallelWhenAllOutcomes<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge);

    AuthoringKernelHandle ForEachWhenAll<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge);

    AuthoringKernelHandle ForEachWhenAllOutcomes<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge);

    EphemeralWorkflowDefinition<TInput> BuildEphemeral<TInput>(AuthoringKernelHandle handle);

    Validation<EphemeralWorkflowDefinition<TInput>> TryBuildEphemeral<TInput>(AuthoringKernelHandle handle);

    EphemeralWorkflowDefinition<TInput, TOutput> BuildEphemeral<TInput, TOutput>(AuthoringKernelHandle handle);

    Validation<EphemeralWorkflowDefinition<TInput, TOutput>> TryBuildEphemeral<TInput, TOutput>(
        AuthoringKernelHandle handle);

    DurableWorkflowDefinition<TInput> BuildDurable<TInput>(AuthoringKernelHandle handle);

    Validation<DurableWorkflowDefinition<TInput>> TryBuildDurable<TInput>(AuthoringKernelHandle handle);

    DurableWorkflowDefinition<TInput, TOutput> BuildDurable<TInput, TOutput>(AuthoringKernelHandle handle);

    Validation<DurableWorkflowDefinition<TInput, TOutput>> TryBuildDurable<TInput, TOutput>(
        AuthoringKernelHandle handle);
}

internal static class TypedAuthoringBoundary
{
    private const string CoreAssemblyName = "OrcaCore.Core";
    private static ITypedAuthoringOperations? operations;

    internal static ITypedAuthoringOperations Operations
    {
        get
        {
            if (Volatile.Read(ref operations) is not { } current)
            {
                try
                {
                    var core = Assembly.Load(CoreAssemblyName);
                    RuntimeHelpers.RunModuleConstructor(core.ManifestModule.ModuleHandle);
                }
                catch (FileNotFoundException exception)
                {
                    throw new InvalidOperationException(
                        "Workflow authoring requires an explicit OrcaCore engine package. " +
                        "Reference OrcaCore.Engine.Ephemeral or OrcaCore.Durable.Hosting before building definitions.",
                        exception);
                }

                current = Volatile.Read(ref operations) ?? throw new InvalidOperationException(
                    "OrcaCore.Core did not install the typed authoring boundary.");
            }

            return current;
        }
    }

    internal static void Install(ITypedAuthoringOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var prior = Interlocked.CompareExchange(ref operations, value, null);
        if (prior is not null && !ReferenceEquals(prior, value))
        {
            throw new InvalidOperationException("The typed authoring boundary is already installed.");
        }
    }
}
