using OrcaCore;

namespace OrcaCore.Core.Authoring;

/// <summary>Authors a nested ephemeral sequence without root-only capabilities.</summary>
internal sealed class EphemeralNestedBuilder<TInput, TState>
{
    private readonly global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState> builder;

    internal EphemeralNestedBuilder(global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState> builder) =>
        this.builder = builder;

    public EphemeralNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        builder.AddNamedStep<TStep>();
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Then(Func<StepContext<TState>, ValueTask> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        builder.Then(() => new InlineEphemeralStep<TState>((context, _) => body(context)));
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        builder.Then(() => new InlineEphemeralStep<TState>(body));
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        builder.DecoratePreviousWithTimeout(timeout);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> WithTransientPool(TransientPoolName pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        builder.DecoratePreviousWithPool(pool.Value);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.If(
            state => condition(AuthoringContracts.Snapshot(state)),
            nested => then(new EphemeralNestedBuilder<TInput, TState>(nested)),
            otherwise is null ? null : nested => otherwise(new EphemeralNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.Wait(eventContract, state => correlation(AuthoringContracts.Snapshot(state)));
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(
            eventContract,
            state => correlation(AuthoringContracts.Snapshot(state)),
            WaitMode.Resident,
            timeout);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        Wait((WorkflowEventContract)eventContract, correlation);

    public EphemeralNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);

    public EphemeralNestedBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        builder.Delay(duration);
        return this;
    }
}

/// <summary>Authors a nested durable sequence without root-only capabilities.</summary>
internal sealed class DurableNestedBuilder<TInput, TState>
{
    private readonly global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder;

    internal DurableNestedBuilder(global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder) =>
        this.builder = builder;

    public DurableNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        builder.AddNamedStep<TStep>();
        return this;
    }

    public DurableNestedBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        builder.DecoratePreviousWithTimeout(timeout);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.If(
            state => condition(AuthoringContracts.Snapshot(state)),
            nested => then(new DurableNestedBuilder<TInput, TState>(nested)),
            otherwise is null ? null : nested => otherwise(new DurableNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(eventContract, state => correlation(AuthoringContracts.Snapshot(state)), WaitMode.Cold);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(
            eventContract,
            state => correlation(AuthoringContracts.Snapshot(state)),
            WaitMode.Cold,
            timeout);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        Wait((WorkflowEventContract)eventContract, correlation);

    public DurableNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);

    public DurableNestedBuilder<TInput, TState> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        PublicPublishAuthoring.Add(builder, eventContract, correlation);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        PublicPublishAuthoring.Add(builder, eventContract, correlation, payload);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        builder.Delay(duration);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseNestedBuilder<TInput, TState>> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        builder.AddResourceLease(
            request,
            nested => body(new DurableLeaseNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    public DurableNestedBuilder<TInput, TState> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseNestedBuilder<TInput, TState>> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        builder.AddResourceLease(
            state => request(AuthoringContracts.Snapshot(state)),
            nested => body(new DurableLeaseNestedBuilder<TInput, TState>(nested)));
        return this;
    }
}

/// <summary>Authors the body of a root durable lease scope.</summary>
internal sealed class DurableLeaseWorkflowBuilder<TInput, TState>
{
    private readonly global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder;

    internal DurableLeaseWorkflowBuilder(global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder) =>
        this.builder = builder;

    public DurableLeaseWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        builder.AddNamedStep<TStep>();
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        builder.DecoratePreviousWithTimeout(timeout);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.If(
            state => condition(AuthoringContracts.Snapshot(state)),
            nested => then(new DurableLeaseNestedBuilder<TInput, TState>(nested)),
            otherwise is null ? null : nested => otherwise(new DurableLeaseNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(eventContract, state => correlation(AuthoringContracts.Snapshot(state)), WaitMode.Cold);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(
            eventContract,
            state => correlation(AuthoringContracts.Snapshot(state)),
            WaitMode.Cold,
            timeout);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        Wait((WorkflowEventContract)eventContract, correlation);

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);

    public DurableLeaseWorkflowBuilder<TInput, TState> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        PublicPublishAuthoring.Add(builder, eventContract, correlation);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        PublicPublishAuthoring.Add(builder, eventContract, correlation, payload);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        builder.Delay(duration);
        return this;
    }
}

/// <summary>Authors a nested body within an existing durable lease scope.</summary>
internal sealed class DurableLeaseNestedBuilder<TInput, TState>
{
    private readonly global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder;

    internal DurableLeaseNestedBuilder(global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder) =>
        this.builder = builder;

    public DurableLeaseNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        builder.AddNamedStep<TStep>();
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        builder.DecoratePreviousWithTimeout(timeout);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.If(
            state => condition(AuthoringContracts.Snapshot(state)),
            nested => then(new DurableLeaseNestedBuilder<TInput, TState>(nested)),
            otherwise is null ? null : nested => otherwise(new DurableLeaseNestedBuilder<TInput, TState>(nested)));
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(eventContract, state => correlation(AuthoringContracts.Snapshot(state)), WaitMode.Cold);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        PublicAuthoringValidation.Positive(timeout, nameof(timeout));
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddWait(
            eventContract,
            state => correlation(AuthoringContracts.Snapshot(state)),
            WaitMode.Cold,
            timeout);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        Wait((WorkflowEventContract)eventContract, correlation);

    public DurableLeaseNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);

    public DurableLeaseNestedBuilder<TInput, TState> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        PublicPublishAuthoring.Add(builder, eventContract, correlation);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        PublicPublishAuthoring.Add(builder, eventContract, correlation, payload);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        builder.Delay(duration);
        return this;
    }
}

internal static class PublicPublishAuthoring
{
    internal static void Add<TState>(
        global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder,
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        builder.AddPublish(eventContract, state => correlation(AuthoringContracts.Snapshot(state)));
    }

    internal static void Add<TState, TPayload>(
        global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder,
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(payload);
        builder.AddPublish(
            eventContract,
            state => correlation(AuthoringContracts.Snapshot(state)),
            typeof(TPayload),
            (Func<TState, TPayload>)(state => payload(AuthoringContracts.Snapshot(state))));
    }
}
