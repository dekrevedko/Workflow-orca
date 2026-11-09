using AwesomeAssertions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Failures;

[Trait("Requirement", "CR-014a")]
[Trait("AC", "AC-619")]
public sealed class OriginalStepFailureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ThrownRootFailure_PreservesTheActualObject(bool bounded, bool invalidData)
    {
        Exception original = invalidData ? new InvalidDataException("The original message.") : new IOException("The original message.");
        using var engine = CreateEngine(bounded);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .Then(_ => throw original)
            .End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var start = await definition.StartOrGetAsync("input", StartIdempotencyKey.Create("original"), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () =>
            await start.WaitForOutputAsync(TestContext.Current.CancellationToken));

        error.InnerException.Should().BeSameAs(original);
        error.Failure!.Code.Should().Be("WF-STEP-UNHANDLED");
        error.Failure.Message.Should().Be(original.Message);
        error.Failure.ExceptionType.Should().Be(original.GetType().FullName);
        error.Failure.Occurrence.Should().BeOfType<FailureOccurrence.Root>();
        original.StackTrace.Should().Contain(nameof(ThrownRootFailure_PreservesTheActualObject));
    }

    [Theory]
    [InlineData(false, "root", false)]
    [InlineData(true, "root", false)]
    [InlineData(false, "branch", false)]
    [InlineData(true, "branch", false)]
    [InlineData(false, "item", false)]
    [InlineData(true, "item", false)]
    [InlineData(false, "root", true)]
    [InlineData(true, "root", true)]
    [InlineData(false, "branch", true)]
    [InlineData(true, "branch", true)]
    [InlineData(false, "item", true)]
    [InlineData(true, "item", true)]
    [Trait("AC", "AC-621")]
    [Trait("AC", "AC-622")]
    public async Task FinalRetry_SelectsActualObjectAndPreservesUserInvocationWrapper(bool bounded, string scope, bool wrapper)
    {
        var attempts = new List<Exception>();
        ValueTask Execute(StepContext<State> _)
        {
            Exception error = wrapper ? new TargetInvocationException("equal", new IOException("inner")) : new IOException("equal");
            attempts.Add(error);
            throw error;
        }
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value));
        var body = scope switch
        {
            "root" => root.Then(Execute).WithRetry(2),
            "branch" => root.Parallel<string>(branches => branches.Branch<State>(AuthoredBranchId.Create("failed"),
                    parent => parent.Value, branch => branch.Then(Execute).WithRetry(2).Return(state => state.Value.Value)))
                .WhenAll((parent, _) => parent.Value),
            _ => root.ForEach<string, State, string>(_ => ["item"], ForEachOptions.Create(1),
                    item => new State(item.Item), branch => branch.Then(Execute).WithRetry(2).Return(state => state.Value.Value))
                .WhenAll((parent, _) => parent.Value)
        };
        using var engine = CreateEngine(bounded);
        var definition = new EphemeralWorkflowDefinitionRegistry(engine).Register(body.End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var start = await definition.StartOrGetAsync("input", StartIdempotencyKey.Create("retry"), TestContext.Current.CancellationToken);
        var first = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await start.WaitForOutputAsync(TestContext.Current.CancellationToken));
        var second = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await start.GetHandleOrThrow().WaitForOutputAsync(TestContext.Current.CancellationToken));
        attempts.Should().HaveCount(2);
        first.InnerException.Should().BeSameAs(attempts[1]);
        second.Should().NotBeSameAs(first);
        second.InnerException.Should().BeSameAs(attempts[1]);
        first.Failure!.Code.Should().Be("WF-STEP-UNHANDLED");
        first.Failure.Message.Should().Be(attempts[1].Message);
        first.Failure.ExceptionType.Should().Be(attempts[1].GetType().FullName);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("AC", "AC-620")]
    public async Task ReturnedOrThrownKnownError_RetainsCodeTypeAndObject(bool bounded, bool returned)
    {
        var original = new KnownError("  message stays exact  ");
        using var provider = new ServiceCollection().AddSingleton(new ReturningStep(original)).BuildServiceProvider();
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        { TerminalRetention = bounded ? new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null }, provider);
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value));
        var body = returned ? root.Then<ReturningStep>() : root.Then(_ => throw original);
        var handle = new EphemeralWorkflowDefinitionRegistry(engine).Register(body.End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("input", StartIdempotencyKey.Create("known"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var error = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await instance.WaitForOutputAsync(TestContext.Current.CancellationToken));
        error.InnerException.Should().BeSameAs(original);
        error.Failure!.Code.Should().Be("APP-FAILED");
        error.Failure.Message.Should().Be(original.Message);
        error.Failure.ExceptionType.Should().Be(typeof(KnownError).FullName);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken)).Failure!.ExceptionType.Should().Be(typeof(KnownError).FullName);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, " ")]
    [Trait("AC", "AC-627")]
    public async Task BlankBusinessMessage_UsesDetachedFallbackWithoutChangingTheOriginal(bool bounded, string message)
    {
        var original = new IOException(message);
        using var engine = CreateEngine(bounded);
        var handle = new EphemeralWorkflowDefinitionRegistry(engine).Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(_ => throw original).End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("input", StartIdempotencyKey.Create("blank"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var error = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await instance.WaitForOutputAsync(TestContext.Current.CancellationToken));
        error.Failure!.Message.Should().Be("The step failed without an exception message.");
        error.InnerException.Should().BeSameAs(original);
        original.Message.Should().Be(message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-624")]
    public async Task TrueAggregate_HasOnlyOrderedDetachedTypedCauses(bool bounded)
    {
        using var engine = CreateEngine(bounded);
        var handle = new EphemeralWorkflowDefinitionRegistry(engine).Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Parallel<string>(branches => branches
                .Branch<State>(AuthoredBranchId.Create("first"), parent => parent.Value,
                    branch => branch.Then(_ => throw new IOException("first")).Return(state => state.Value.Value))
                .Branch<State>(AuthoredBranchId.Create("second"), parent => parent.Value,
                    branch => branch.Then(_ => throw new InvalidDataException("second")).Return(state => state.Value.Value)))
            .WhenAll((parent, _) => parent.Value).End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("input", StartIdempotencyKey.Create("aggregate"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var error = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await instance.WaitForOutputAsync(TestContext.Current.CancellationToken));
        error.InnerException.Should().BeNull();
        error.Failure!.Code.Should().Be("SFE-JOIN-FAILED");
        error.Failure.ExceptionType.Should().BeNull();
        error.Failure.Causes.Select(cause => cause.Message).Should().Equal("first", "second");
        error.Failure.Causes.Select(cause => cause.ExceptionType).Should().Equal(typeof(IOException).FullName, typeof(InvalidDataException).FullName);
    }

    private sealed class KnownError(string message) : OrcaCoreException("APP-FAILED", message);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnrelatedRootCancellation_RetainsItsExistingUnacceptedAbort(bool bounded)
    {
        var original = new OperationCanceledException("root cancellation", new CancellationToken(canceled: true));
        using var engine = CreateEngine(bounded);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(_ => throw original).End().Build()).GetHandleOrThrow();
        var error = await Record.ExceptionAsync(async () => await definition.StartOrGetAsync(
            "input", StartIdempotencyKey.Create("root-cancellation"), TestContext.Current.CancellationToken));
        error.Should().BeSameAs(original);
        registry.ListInstances().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("AC", "AC-623")]
    public async Task UnrelatedBranchOrItemCancellation_RetainsItsExistingFailedOutcome(bool bounded, bool item)
    {
        var original = new OperationCanceledException("unrelated business cancellation", new CancellationToken(canceled: true));
        ValueTask Body(StepContext<State> _) => throw original;
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value));
        var body = item
            ? root.ForEach<string, State, string>(_ => ["item"], ForEachOptions.Create(1), value => new State(value.Item),
                child => child.Then(Body).Return(state => state.Value.Value)).WhenAll((parent, _) => parent.Value)
            : root.Parallel<string>(branches => branches.Branch<State>(AuthoredBranchId.Create("unrelated"), parent => parent.Value,
                child => child.Then(Body).Return(state => state.Value.Value))).WhenAll((parent, _) => parent.Value);
        using var engine = CreateEngine(bounded);
        var definition = new EphemeralWorkflowDefinitionRegistry(engine).Register(body.End(state => state.Value.Value).Build()).GetHandleOrThrow();
        WorkflowStartResult<WorkflowInstanceHandle<string>>? accepted = null;
        var startError = await Record.ExceptionAsync(async () => accepted = await definition.StartOrGetAsync(
            "input", StartIdempotencyKey.Create("unrelated"), TestContext.Current.CancellationToken));
        startError.Should().BeNull("an unrelated branch/item cancellation remains an accepted Failed business outcome");
        var instance = accepted!.GetHandleOrThrow();
        var unavailable = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await instance.WaitForOutputAsync(TestContext.Current.CancellationToken));
        unavailable.InnerException.Should().BeSameAs(original);
        unavailable.Failure!.Code.Should().Be("WF-STEP-UNHANDLED");
        unavailable.Failure.ExceptionType.Should().Be(typeof(OperationCanceledException).FullName);
    }

    [Fact]
    [Trait("AC", "AC-633")]
    public async Task Retirement_ClearsTheRuntimeSlot_WithoutChangingTheCallerOwnedWrapper()
    {
        using var engine = CreateEngine(true);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var original = new IOException("caller-owned original");
        var definition = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(_ => throw original).End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var instance = (await definition.StartOrGetAsync("input", StartIdempotencyKey.Create("caller-wrapper"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        engine.TryGetFacadeInstance(instance.InstanceId, out var retiredRuntime).Should().BeTrue();
        var delivered = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await instance.WaitForOutputAsync(TestContext.Current.CancellationToken));
        var pressure = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).End().Build()).GetHandleOrThrow();
        var next = (await pressure.StartOrGetAsync("pressure", StartIdempotencyKey.Create("pressure"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        engine.TryGetFacadeInstance(next.InstanceId, out var nextRuntime).Should().BeTrue();
        await nextRuntime!.Lifetime.WhenSettled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        retiredRuntime!.Lifetime.IsRetired.Should().BeTrue();
        retiredRuntime.OriginalFailure.Should().BeNull();
        delivered.InnerException.Should().BeSameAs(original);
        delivered.InnerException!.Message.Should().Be("caller-owned original");
    }

    [Fact]
    [Trait("AC", "AC-633")]
    public async Task RetiredFailure_DropsTheEngineReferenceWhileItsHandleKeepsOnlyTheReceipt()
    {
        using var engine = CreateEngine(true);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var (instance, weak) = await StartWeakFailureAsync(registry);
        weak.IsAlive.Should().BeTrue();
        var pressure = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).End().Build()).GetHandleOrThrow();
        var next = (await pressure.StartOrGetAsync("pressure", StartIdempotencyKey.Create("pressure"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        engine.TryGetFacadeInstance(next.InstanceId, out var runtime).Should().BeTrue();
        await runtime!.Lifetime.WhenSettled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<WorkflowInstanceNotFoundException>(async () => await instance.GetSnapshotAsync(TestContext.Current.CancellationToken));
        await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        weak.IsAlive.Should().BeFalse();
        GC.KeepAlive(instance);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<(WorkflowInstanceHandle<string>, WeakReference)> StartWeakFailureAsync(EphemeralWorkflowDefinitionRegistry registry)
    {
        var weak = new List<WeakReference>();
        var handle = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(_ => ThrowUncaptured(weak)).End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("input", StartIdempotencyKey.Create("weak"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        return (instance, weak.Single());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("AC", "AC-625")]
    public async Task RecoveredBranch_OnlySuppliesDetachedType_AndDoesNotRetainOriginal(bool bounded, bool item)
    {
        using var engine = CreateEngine(bounded);
        var (handle, weak) = await StartRecoveryAsync(engine, item);
        (await handle.GetStateAsync<State>(TestContext.Current.CancellationToken)).Value.Should().Be(typeof(IOException).FullName);
        (await handle.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(WorkflowInstanceStatus.Waiting);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        weak.IsAlive.Should().BeFalse("a successful recovery must release the obsolete live exception");
        await handle.TerminateAsync(TestContext.Current.CancellationToken);
        await handle.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        GC.KeepAlive(handle);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<(WorkflowInstanceHandle<string>, WeakReference)> StartRecoveryAsync(EphemeralWorkflowEngine engine, bool item)
    {
        var weak = new List<WeakReference>();
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value));
        var recovered = item
            ? root.ForEach<string, State, string>(_ => ["item"], ForEachOptions.Create(1), value => new State(value.Item),
                    child => child.Then(_ => ThrowUncaptured(weak)).Return(state => state.Value.Value))
                .WhenAllOutcomes((_, outcomes) => new State(((ForEachItemOutcome<string>.Failed)outcomes.Single()).Failure.ExceptionType!))
            : root.Parallel<string>(branches => branches.Branch<State>(AuthoredBranchId.Create("recovered"), parent => parent.Value,
                branch => branch.Then(_ => ThrowUncaptured(weak)).Return(state => state.Value.Value)))
                .WhenAllOutcomes((_, outcomes) => new State(((BranchOutcome<string>.Failed)outcomes.Single()).Failure.ExceptionType!));
        var definition = recovered
            .Wait(WorkflowEventContract.Create(EventName.Create("post-recovery"), EventContractVersion.Initial), _ => CorrelationId.Create("post-recovery"))
            .End(state => state.Value.Value).Build();
        var handle = new EphemeralWorkflowDefinitionRegistry(engine).Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("input", StartIdempotencyKey.Create("recovered"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        return (instance, weak.Single());
    }


    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ValueTask ThrowUncaptured(List<WeakReference> references)
    {
        var error = new IOException("not held by the definition or caller");
        references.Add(new WeakReference(error));
        throw error;
    }

    private sealed class ReturningStep(KnownError error) : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken) => ValueTask.FromResult<StepResult>(new StepResult.Failed(error));
    }

    private static EphemeralWorkflowEngine CreateEngine(bool bounded) => new(TimeProvider.System,
        new EphemeralWorkflowEngineOptions
        {
            TerminalRetention = bounded ? new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null
        });

    private sealed record State(string Value);
}
