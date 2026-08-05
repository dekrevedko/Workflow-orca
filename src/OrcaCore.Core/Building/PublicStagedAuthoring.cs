using System.Reflection;
using OrcaCore;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Authoring;

/// <summary>
/// Selects workflow execution mode before any mode-specific capability is authored.
/// </summary>
internal static class Workflow
{
    /// <summary>Starts an ephemeral definition.</summary>
    public static EphemeralWorkflowInitBuilder<TState> Ephemeral<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        return new EphemeralWorkflowInitBuilder<TState>(definitionId, definitionVersion);
    }

    /// <summary>Starts a durable definition.</summary>
    public static DurableWorkflowInitBuilder<TState> Durable<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        return new DurableWorkflowInitBuilder<TState>(definitionId, definitionVersion);
    }
}

/// <summary>Requires the single input-to-state initializer for an ephemeral workflow.</summary>
internal sealed class EphemeralWorkflowInitBuilder<TState>
{
    private readonly DefinitionId definitionId;
    private readonly DefinitionVersion definitionVersion;

    internal EphemeralWorkflowInitBuilder(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        this.definitionId = definitionId;
        this.definitionVersion = definitionVersion;
    }

    /// <summary>Defines the external input contract and creates private workflow state.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> Init<TInput>(Func<TInput, TState> createState)
    {
        ArgumentNullException.ThrowIfNull(createState);
        var builder = global::OrcaCore.Core.Building.WorkflowKernel
            .Ephemeral<TState>(definitionId, definitionVersion)
            .Init(createState);
        builder.UseDetachedAttemptState();
        return new EphemeralWorkflowBuilder<TInput, TState>(builder);
    }
}

/// <summary>Requires the single input-to-state initializer for a durable workflow.</summary>
internal sealed class DurableWorkflowInitBuilder<TState>
{
    private readonly DefinitionId definitionId;
    private readonly DefinitionVersion definitionVersion;

    internal DurableWorkflowInitBuilder(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        this.definitionId = definitionId;
        this.definitionVersion = definitionVersion;
    }

    /// <summary>Defines the external input contract and creates private workflow state.</summary>
    public DurableWorkflowBuilder<TInput, TState> Init<TInput>(Func<TInput, TState> createState)
    {
        ArgumentNullException.ThrowIfNull(createState);
        var builder = global::OrcaCore.Core.Building.WorkflowKernel
            .Durable<TState>(definitionId, definitionVersion)
            .Init(createState);
        builder.UseDetachedAttemptState();
        return new DurableWorkflowBuilder<TInput, TState>(builder);
    }
}

/// <summary>Authors the root sequence of an ephemeral workflow.</summary>
internal sealed class EphemeralWorkflowBuilder<TInput, TState>
{
    private readonly global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState> builder;

    internal EphemeralWorkflowBuilder(global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState> builder)
    {
        this.builder = builder;
    }

    internal global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState> RuntimeBuilder => builder;

    /// <summary>Adds a named business step.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        builder.AddNamedStep<TStep>();
        return this;
    }

    /// <summary>Adds an inline asynchronous ephemeral step.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> Then(Func<StepContext<TState>, ValueTask> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        builder.Then(() => new InlineEphemeralStep<TState>((context, _) => body(context)));
        return this;
    }

    /// <summary>Adds a cancellation-aware inline asynchronous ephemeral step.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        builder.Then(() => new InlineEphemeralStep<TState>(body));
        return this;
    }

    /// <summary>Decorates the preceding business step with retry.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay);
        return this;
    }

    /// <summary>Decorates the preceding business step with a timeout.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        builder.DecoratePreviousWithTimeout(timeout);
        return this;
    }

    /// <summary>Decorates the preceding named step with a host-local transient pool.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> WithTransientPool(TransientPoolName pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        builder.DecoratePreviousWithPool(pool.Value);
        return this;
    }

    /// <summary>Sets the single start-relative workflow deadline.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        builder.SelectDeadline(timeout);
        return this;
    }

    /// <summary>Adds a nested conditional.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.If(
            state => condition(AuthoringContractFactory.Snapshot(state)),
            nested => then(new EphemeralNestedBuilder<TInput, TState>(nested)),
            otherwise is null
                ? null
                : nested => otherwise(new EphemeralNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    /// <summary>Adds a root-only loop.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> body)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(body);
        builder.While(
            state => condition(AuthoringContractFactory.Snapshot(state)),
            nested => body(new EphemeralNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    /// <summary>Adds a structural event wait.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.Wait(eventName.Value, state => correlation(AuthoringContractFactory.Snapshot(state)));
        return this;
    }

    /// <summary>Adds a structural event wait with a timeout.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(
            eventName.Value,
            state => correlation(AuthoringContractFactory.Snapshot(state)),
            WaitMode.Resident,
            timeout);
        return this;
    }

    /// <summary>Adds a structural delay.</summary>
    public EphemeralWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        builder.Delay(duration);
        return this;
    }

    /// <summary>Authors fixed root branches and returns the required join stage.</summary>
    public EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches)
    {
        ArgumentNullException.ThrowIfNull(branches);
        return new EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult>(
            this,
            builder.BeginRootJoin(),
            branches);
    }

    /// <summary>Authors bounded root items and returns the required join stage.</summary>
    public EphemeralForEachJoinBuilder<TInput, TState, TResult> ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<EphemeralItemBuilder<TItemState, TResult>> body)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(body);
        var join = builder.BeginRootJoin();
        return new EphemeralForEachJoinBuilder<TInput, TState, TResult>(
            this,
            merge => builder.CompleteRootForEach<TItem, TItemState, TResult>(
                join,
                parent => AuthoringContractFactory.BoundedItems(
                    items(AuthoringContractFactory.Snapshot(parent.Value)), options.MaxItems),
                global::OrcaCore.Core.Definitions.WorkflowPartitioner<TItem>.Items(),
                item => input(new ForEachItemInput<TItem>(item.Index, item.Items.Single())),
                branch => body(new EphemeralItemBuilder<TItemState, TResult>(branch)),
                global::OrcaCore.Core.Definitions.ForEachJoinPolicy.WhenAll,
                global::OrcaCore.Core.Definitions.ForEachFailurePolicy.WaitAllThenFail,
                options.MaxItems,
                options.MaxConcurrency,
                (parent, outcomes) => ((Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState>)merge)(
                    AuthoringContractFactory.Snapshot(parent.Value),
                    outcomes.Select(AuthoringContractFactory.ItemResult).ToArray())),
            merge => builder.CompleteRootForEach<TItem, TItemState, TResult>(
                join,
                parent => AuthoringContractFactory.BoundedItems(
                    items(AuthoringContractFactory.Snapshot(parent.Value)), options.MaxItems),
                global::OrcaCore.Core.Definitions.WorkflowPartitioner<TItem>.Items(),
                item => input(new ForEachItemInput<TItem>(item.Index, item.Items.Single())),
                branch => body(new EphemeralItemBuilder<TItemState, TResult>(branch)),
                global::OrcaCore.Core.Definitions.ForEachJoinPolicy.WhenAll,
                global::OrcaCore.Core.Definitions.ForEachFailurePolicy.ContinueWithPartialFailures,
                options.MaxItems,
                options.MaxConcurrency,
                (parent, outcomes) => ((Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState>)merge)(
                    AuthoringContractFactory.Snapshot(parent.Value),
                    outcomes.Select(AuthoringContractFactory.ItemOutcome).ToArray())));
    }

    /// <summary>Selects resultless completion.</summary>
    public EphemeralWorkflowCompletionBuilder<TInput> End()
    {
        builder.End();
        return new EphemeralWorkflowCompletionBuilder<TInput>(PublicDefinitionBuilder.TryBuild(builder));
    }

    /// <summary>Selects resultless completion with a fixed outcome.</summary>
    public EphemeralWorkflowCompletionBuilder<TInput> End(WorkflowOutcomeName outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        builder.End(outcome.Value);
        return new EphemeralWorkflowCompletionBuilder<TInput>(PublicDefinitionBuilder.TryBuild(builder));
    }

    /// <summary>Selects typed completion.</summary>
    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        builder.AddTypedEnd(state => output(AuthoringContractFactory.Snapshot(state)));
        return new EphemeralWorkflowCompletionBuilder<TInput, TOutput>(
            PublicDefinitionBuilder.TryBuild(builder));
    }

    /// <summary>Selects typed completion with a fixed outcome.</summary>
    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(outcome);
        builder.AddTypedEnd(
            state => output(AuthoringContractFactory.Snapshot(state)),
            outcome.Value);
        return new EphemeralWorkflowCompletionBuilder<TInput, TOutput>(
            PublicDefinitionBuilder.TryBuild(builder));
    }
}

/// <summary>Authors the root sequence of a durable workflow.</summary>
internal sealed class DurableWorkflowBuilder<TInput, TState>
{
    private readonly global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder;

    internal DurableWorkflowBuilder(global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder)
    {
        this.builder = builder;
    }

    internal global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> RuntimeBuilder => builder;

    /// <summary>Adds a named business step.</summary>
    public DurableWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        builder.AddNamedStep<TStep>();
        return this;
    }

    /// <summary>Decorates the preceding business step with retry.</summary>
    public DurableWorkflowBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay);
        return this;
    }

    /// <summary>Decorates the preceding business step with a timeout.</summary>
    public DurableWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        builder.DecoratePreviousWithTimeout(timeout);
        return this;
    }

    /// <summary>Sets the single start-relative workflow deadline.</summary>
    public DurableWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        builder.SelectDeadline(timeout);
        return this;
    }

    /// <summary>Adds a nested conditional.</summary>
    public DurableWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.If(
            state => condition(AuthoringContractFactory.Snapshot(state)),
            nested => then(new DurableNestedBuilder<TInput, TState>(nested)),
            otherwise is null ? null : nested => otherwise(new DurableNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    /// <summary>Adds a root-only loop.</summary>
    public DurableWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> body)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(body);
        builder.While(
            state => condition(AuthoringContractFactory.Snapshot(state)),
            nested => body(new DurableNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    /// <summary>Adds a cold-capable structural event wait.</summary>
    public DurableWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(eventName.Value, state => correlation(AuthoringContractFactory.Snapshot(state)), WaitMode.Cold);
        return this;
    }

    /// <summary>Adds a cold-capable structural event wait with a timeout.</summary>
    public DurableWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(
            eventName.Value,
            state => correlation(AuthoringContractFactory.Snapshot(state)),
            WaitMode.Cold,
            timeout);
        return this;
    }

    /// <summary>Adds a structural delay.</summary>
    public DurableWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        builder.Delay(duration);
        return this;
    }

    /// <summary>Authors fixed root branches and returns the required join stage.</summary>
    public DurableWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches)
    {
        ArgumentNullException.ThrowIfNull(branches);
        return new DurableWorkflowParallelJoinBuilder<TInput, TState, TResult>(
            this,
            builder.BeginRootJoin(),
            branches);
    }

    /// <summary>Authors bounded root items and returns the required join stage.</summary>
    public DurableForEachJoinBuilder<TInput, TState, TResult> ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<DurableItemBuilder<TItemState, TResult>> body)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(body);
        var join = builder.BeginRootJoin();
        return new DurableForEachJoinBuilder<TInput, TState, TResult>(
            this,
            merge => builder.CompleteRootForEach<TItem, TItemState, TResult>(
                join,
                parent => AuthoringContractFactory.BoundedItems(
                    items(AuthoringContractFactory.Snapshot(parent.Value)), options.MaxItems),
                global::OrcaCore.Core.Definitions.WorkflowPartitioner<TItem>.Items(),
                item => input(new ForEachItemInput<TItem>(item.Index, item.Items.Single())),
                branch => body(new DurableItemBuilder<TItemState, TResult>(branch)),
                global::OrcaCore.Core.Definitions.ForEachJoinPolicy.WhenAll,
                global::OrcaCore.Core.Definitions.ForEachFailurePolicy.WaitAllThenFail,
                options.MaxItems,
                options.MaxConcurrency,
                (parent, outcomes) => ((Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState>)merge)(
                    AuthoringContractFactory.Snapshot(parent.Value),
                    outcomes.Select(AuthoringContractFactory.ItemResult).ToArray())),
            merge => builder.CompleteRootForEach<TItem, TItemState, TResult>(
                join,
                parent => AuthoringContractFactory.BoundedItems(
                    items(AuthoringContractFactory.Snapshot(parent.Value)), options.MaxItems),
                global::OrcaCore.Core.Definitions.WorkflowPartitioner<TItem>.Items(),
                item => input(new ForEachItemInput<TItem>(item.Index, item.Items.Single())),
                branch => body(new DurableItemBuilder<TItemState, TResult>(branch)),
                global::OrcaCore.Core.Definitions.ForEachJoinPolicy.WhenAll,
                global::OrcaCore.Core.Definitions.ForEachFailurePolicy.ContinueWithPartialFailures,
                options.MaxItems,
                options.MaxConcurrency,
                (parent, outcomes) => ((Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState>)merge)(
                    AuthoringContractFactory.Snapshot(parent.Value),
                    outcomes.Select(AuthoringContractFactory.ItemOutcome).ToArray())));
    }

    /// <summary>Authors a static durable resource scope.</summary>
    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        builder.AddResourceLease(
            request,
            nested => body(new DurableLeaseWorkflowBuilder<TInput, TState>(nested)));
        return this;
    }

    /// <summary>Authors a state-selected durable resource scope.</summary>
    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        builder.AddResourceLease(
            state => request(AuthoringContractFactory.Snapshot(state)),
            nested => body(new DurableLeaseWorkflowBuilder<TInput, TState>(nested)));
        return this;
    }

    /// <summary>Selects terminal durable rollover.</summary>
    public DurableWorkflowCompletionBuilder<TInput> ContinueAsNew(
        Func<ReadOnlyStateSnapshot<TState>, TState> replacementState)
    {
        ArgumentNullException.ThrowIfNull(replacementState);
        builder.ContinueAsNew(state => replacementState(AuthoringContractFactory.Snapshot(state)));
        return new DurableWorkflowCompletionBuilder<TInput>(PublicDefinitionBuilder.TryBuild(builder));
    }

    /// <summary>Selects resultless completion.</summary>
    public DurableWorkflowCompletionBuilder<TInput> End()
    {
        builder.End();
        return new DurableWorkflowCompletionBuilder<TInput>(PublicDefinitionBuilder.TryBuild(builder));
    }

    /// <summary>Selects resultless completion with a fixed outcome.</summary>
    public DurableWorkflowCompletionBuilder<TInput> End(WorkflowOutcomeName outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        builder.End(outcome.Value);
        return new DurableWorkflowCompletionBuilder<TInput>(PublicDefinitionBuilder.TryBuild(builder));
    }

    /// <summary>Selects typed completion.</summary>
    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        builder.AddTypedEnd(state => output(AuthoringContractFactory.Snapshot(state)));
        return new DurableWorkflowCompletionBuilder<TInput, TOutput>(
            PublicDefinitionBuilder.TryBuild(builder));
    }

    /// <summary>Selects typed completion with a fixed outcome.</summary>
    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(outcome);
        builder.AddTypedEnd(
            state => output(AuthoringContractFactory.Snapshot(state)),
            outcome.Value);
        return new DurableWorkflowCompletionBuilder<TInput, TOutput>(
            PublicDefinitionBuilder.TryBuild(builder));
    }
}

internal sealed class InlineEphemeralStep<TState>(
    Func<StepContext<TState>, CancellationToken, ValueTask> body) : IStep<TState>
{
    public async ValueTask<StepResult> ExecuteAsync(
        StepContext<TState> context,
        CancellationToken cancellationToken)
    {
        await body(context, cancellationToken).ConfigureAwait(false);
        return new StepResult.Completed();
    }
}

internal static class PublicAuthoringValidation
{
    internal static void Positive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Duration must be positive.");
        }
    }
}

internal static class AuthoringContractFactory
{
    internal static EphemeralWorkflowDefinition<TInput> EphemeralDefinition<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        Construct<EphemeralWorkflowDefinition<TInput>>(
            [
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(object),
                typeof(Type)
            ],
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    internal static EphemeralWorkflowDefinition<TInput, TOutput> EphemeralDefinition<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        Construct<EphemeralWorkflowDefinition<TInput, TOutput>>(
            [
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(object),
                typeof(Type)
            ],
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    internal static DurableWorkflowDefinition<TInput> DurableDefinition<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        Construct<DurableWorkflowDefinition<TInput>>(
            [
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(object),
                typeof(Type)
            ],
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    internal static DurableWorkflowDefinition<TInput, TOutput> DurableDefinition<TInput, TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        object runtimeDefinition,
        Type runtimeStateType) =>
        Construct<DurableWorkflowDefinition<TInput, TOutput>>(
            [
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(DefinitionFingerprint),
                typeof(object),
                typeof(Type)
            ],
            definitionId,
            definitionVersion,
            definitionFingerprint,
            runtimeDefinition,
            runtimeStateType);

    public static ReadOnlyStateSnapshot<TState> Snapshot<TState>(TState state)
    {
        var constructor = typeof(ReadOnlyStateSnapshot<TState>).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single();
        return (ReadOnlyStateSnapshot<TState>)constructor.Invoke([state]);
    }

    public static DefinitionFingerprint Fingerprint(string value)
    {
        return (DefinitionFingerprint)typeof(DefinitionFingerprint).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke([value]);
    }

    public static Validation<T> Valid<T>(T value)
    {
        return CreateValidation(value, []);
    }

    public static Validation<T> Invalid<T>(IReadOnlyList<ValidationError> errors)
    {
        var diagnostics = errors.Select(error => Diagnostic(error)).ToArray();
        return CreateValidation<T>(default, diagnostics);
    }

    public static WorkflowDiagnostic Diagnostic(ValidationError error)
    {
        var code = error.Code switch
        {
            "SFE-AUTH-001_MISSING_ROOT_INIT" => "SFE-AUTH-ROOT-001",
            "SFE-AUTH-015_EMPTY_STRUCTURED_SCOPE" => "SFE-AUTH-BRANCH-004",
            "SFE-AUTH-002_MISSING_ROOT_END" => "SFE-AUTH-ROOT-003",
            "SFE-AUTH-003_MULTIPLE_ROOT_INIT" => "SFE-AUTH-ROOT-002",
            "SFE-AUTH-004_ROOT_INIT_NOT_FIRST" => "SFE-AUTH-PATH-001",
            "SFE-AUTH-005_MULTIPLE_ROOT_END" => "SFE-AUTH-ROOT-004",
            "SFE-AUTH-006_NODE_AFTER_ROOT_END" => "SFE-AUTH-PATH-001",
            "SFE-AUTH-007_MISSING_BRANCH_RETURN" => "SFE-AUTH-BRANCH-002",
            "SFE-AUTH-008_MULTIPLE_BRANCH_RETURN" => "SFE-AUTH-BRANCH-003",
            "SFE-AUTH-009_UNREACHABLE_NODE" => "SFE-AUTH-PATH-001",
            "SFE-AUTH-010_BLANK_BRANCH_IDENTITY" => "SFE-AUTH-BRANCH-001",
            "SFE-AUTH-011_DUPLICATE_BRANCH_IDENTITY" => "SFE-AUTH-BRANCH-001",
            "SFE-AUTH-012_NESTED_ROOT_INIT" => "SFE-AUTH-CAP-001",
            "SFE-AUTH-013_NESTED_ROOT_END" => "SFE-AUTH-CAP-001",
            "SFE-AUTH-014_FOREACH_WHEN_ANY_REQUIRES_FAIL_FAST" => "SFE-AUTH-CAP-001",
            "SFE-AUTH-016_LEASE_ANCESTRY_CONFLICT" => "SFE-AUTH-LEASE-001",
            "SFE-AUTH-017_LEASE_BLOCKS_CONTINUE_AS_NEW" => "SFE-AUTH-LEASE-003",
            "SFE-CAP-001_UNSUPPORTED_INSTRUCTION" => "SFE-AUTH-CAP-001",
            "SFE-TYPE-001_SERIALIZER_UNAVAILABLE" => "SFE-TYPE-002",
            "SFE-TYPE-002_BRANCH_RESULT_MISMATCH" => "SFE-TYPE-001",
            "SFE-PLAN-001_NO_PROGRESS_LOOP" => "SFE-AUTH-LOOP-001",
            var legacy when legacy.StartsWith("SFE-LIMIT-", StringComparison.Ordinal) => "SFE-LIMIT-001",
            _ => throw new InvalidOperationException(
                $"Legacy compiler diagnostic '{error.Code}' has no normative v1 mapping.")
        };
        var location = LocationFromCompilerPath(error.Path);
        var relatedLocations = error.RelatedPath is null
            ? Array.Empty<AuthoredLocation>()
            : [LocationFromCompilerPath(error.RelatedPath)];
        return (WorkflowDiagnostic)typeof(WorkflowDiagnostic).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(
                [code, WorkflowDiagnosticSeverity.Error, location, relatedLocations, error.Message]);
    }

    public static WorkflowDefinitionException DefinitionException(
        IReadOnlyList<WorkflowDiagnostic> diagnostics)
    {
        return (WorkflowDefinitionException)typeof(WorkflowDefinitionException).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke([diagnostics]);
    }

    public static WorkflowDefinitionException DefinitionException(
        string message,
        Exception? innerException = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var detail = innerException is null ? message : $"{message} {innerException.Message}";
        var diagnostic = CreateDiagnostic(
            "SFE-AUTH-CAP-001",
            Location("workflow:$"),
            [],
            detail);
        return DefinitionException([diagnostic]);
    }

    public static WorkflowDefinitionException MisplacedDecorator(string decorator, int authoredOrdinal)
    {
        var location = Location($"workflow:$/n:{authoredOrdinal:D8}");
        var diagnostic = CreateDiagnostic(
            "SFE-AUTH-DECORATOR-001",
            location,
            [],
            $"The {decorator} decorator must appear once immediately after an eligible business step.");
        return DefinitionException([diagnostic]);
    }

    public static WorkflowDefinitionException DuplicateDeadline(
        string firstLocation,
        string secondLocation)
    {
        var first = Location(firstLocation);
        var second = Location(secondLocation);
        var diagnostic = CreateDiagnostic(
            "SFE-AUTH-DEADLINE-001",
            second,
            [first],
            "CompleteWithin may be selected only once per workflow definition.");
        return DefinitionException([diagnostic]);
    }

    public static WorkflowDefinitionException Lifecycle(
        string code,
        string location,
        string relatedLocation,
        string message)
    {
        var primary = Location(location);
        var related = Location(relatedLocation);
        var diagnostic = CreateDiagnostic(
            code,
            primary,
            [related],
            message);
        return DefinitionException([diagnostic]);
    }

    public static WorkflowWaitTimeoutException WaitTimeout(
        EventName eventName,
        CorrelationId correlationId) =>
        (WorkflowWaitTimeoutException)typeof(WorkflowWaitTimeoutException)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .Invoke([eventName, correlationId]);

    public static StepAttemptTimeoutException StepTimeout(
        StepOperationId operationId,
        int attemptNumber,
        TimeSpan timeout) =>
        (StepAttemptTimeoutException)typeof(StepAttemptTimeoutException)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .Invoke([operationId, attemptNumber, timeout]);

    public static WorkflowDeadlineExceededException WorkflowDeadline(
        DateTimeOffset deadline) =>
        (WorkflowDeadlineExceededException)typeof(WorkflowDeadlineExceededException)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .Invoke([deadline]);

    public static ResourcePoolNotConfiguredException ResourcePoolsNotConfigured(
        IReadOnlyList<ResourcePoolName> missingPools) =>
        (ResourcePoolNotConfiguredException)typeof(ResourcePoolNotConfiguredException)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .Invoke([missingPools]);

    public static BranchOutcome<TResult> BranchSucceeded<TResult>(
        AuthoredBranchId branchId,
        TResult result)
    {
        return (BranchOutcome<TResult>)typeof(BranchOutcome<TResult>.Succeeded).GetConstructors(
                BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(constructor => constructor.GetParameters() is
                [
                    { ParameterType: var branchIdType },
                    { ParameterType: var resultType }
                ] &&
                branchIdType == typeof(AuthoredBranchId) &&
                resultType == typeof(TResult))
            .Invoke([branchId, result]);
    }

    public static BranchOutcome<TResult> BranchFailed<TResult>(
        AuthoredBranchId branchId,
        global::OrcaCore.Core.Execution.FiberFailure failure)
    {
        var detached = Failure(failure);
        return (BranchOutcome<TResult>)typeof(BranchOutcome<TResult>.Failed).GetConstructors(
                BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(constructor => constructor.GetParameters() is
                [
                    { ParameterType: var branchIdType },
                    { ParameterType: var failureType }
                ] &&
                branchIdType == typeof(AuthoredBranchId) &&
                failureType == typeof(WorkflowFailure))
            .Invoke([branchId, detached]);
    }

    public static IReadOnlyList<TItem> BoundedItems<TItem>(IReadOnlyList<TItem> items, int maxItems)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count > maxItems)
        {
            const string code = global::OrcaCore.Core.Execution.StructuredExecutionLimitCodes.ForEachItemsExceeded;
            throw new global::OrcaCore.Core.Execution.StructuredExecutionLimitException(
                code,
                $"{code}: ForEach selected {items.Count} items, exceeding MaxItems {maxItems}.");
        }

        return items.ToArray();
    }

    public static ForEachItemResult<TResult> ItemResult<TResult>(
        global::OrcaCore.Core.Building.ForEachItemOutcome<TResult> outcome) =>
        new(outcome.Index, outcome.Result!);

    public static ForEachItemOutcome<TResult> ItemOutcome<TResult>(
        global::OrcaCore.Core.Building.ForEachItemOutcome<TResult> outcome)
    {
        var outcomeType = outcome.Status == global::OrcaCore.Core.Building.ForEachItemTerminalStatus.Succeeded
            ? typeof(ForEachItemOutcome<TResult>.Succeeded)
            : typeof(ForEachItemOutcome<TResult>.Failed);
        var arguments = outcome.Status == global::OrcaCore.Core.Building.ForEachItemTerminalStatus.Succeeded
            ? new object?[] { outcome.Index, outcome.Result }
            : new object?[]
            {
                outcome.Index,
                outcome.Failure is null
                    ? Failure("SFE-JOIN-FAILED", "Item did not succeed.")
                    : Failure(outcome.Failure)
            };
        return (ForEachItemOutcome<TResult>)outcomeType.GetConstructors(
                BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(constructor => constructor.GetParameters() is
                [
                    { ParameterType: var indexType },
                    _
                ] &&
                indexType == typeof(int))
            .Invoke(arguments);
    }

    private static WorkflowFailure Failure(string message) =>
        Failure("SFE-JOIN-FAILED", message);

    private static WorkflowFailure Failure(string code, string message) =>
        Failure(new global::OrcaCore.Core.Execution.FiberFailure(code, message));

    private static WorkflowFailure Failure(global::OrcaCore.Core.Execution.FiberFailure failure) =>
        (WorkflowFailure)typeof(WorkflowFailure).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(
                [
                    failure.Code,
                    failure.Message,
                    global::OrcaCore.Core.Execution.FailureProvenance.Clone(failure.AuthoredLocation),
                    global::OrcaCore.Core.Execution.FailureProvenance.Clone(failure.Occurrence),
                    failure.Causes.Select(Failure).ToArray()
                ]);

    private static Validation<T> CreateValidation<T>(T? value, IReadOnlyList<WorkflowDiagnostic> diagnostics)
    {
        return (Validation<T>)typeof(Validation<T>).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke([value, diagnostics]);
    }

    private static AuthoredLocation Location(string value) =>
        (AuthoredLocation)typeof(AuthoredLocation).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke([value]);

    private static AuthoredLocation LocationFromCompilerPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.Equals(path, "root", StringComparison.Ordinal))
        {
            return Location("workflow:$");
        }

        var tokens = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var canonical = new List<string> { "workflow:$" };
        foreach (var token in tokens.Skip(1))
        {
            if (int.TryParse(token, out var ordinal))
            {
                canonical.Add($"n:{ordinal:D8}");
            }
            else if (string.Equals(token, "then", StringComparison.Ordinal))
            {
                canonical.Add("if:true");
            }
            else if (string.Equals(token, "else", StringComparison.Ordinal))
            {
                canonical.Add("if:false");
            }
            else if (string.Equals(token, "body", StringComparison.Ordinal))
            {
                canonical.Add("while:body");
            }
        }

        return Location(string.Join('/', canonical));
    }

    private static WorkflowDiagnostic CreateDiagnostic(
        string code,
        AuthoredLocation location,
        IReadOnlyList<AuthoredLocation> relatedLocations,
        string message) =>
        (WorkflowDiagnostic)typeof(WorkflowDiagnostic).GetConstructors(
            BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(
                [code, WorkflowDiagnosticSeverity.Error, location, relatedLocations, message]);

    private static TContract Construct<TContract>(Type[] parameterTypes, params object?[] arguments)
    {
        var constructor = typeof(TContract).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            parameterTypes,
            modifiers: null) ?? throw new MissingMethodException(
                typeof(TContract).FullName,
                $".ctor({string.Join(", ", parameterTypes.Select(type => type.FullName))})");
        return (TContract)constructor.Invoke(arguments);
    }
}
