using AwesomeAssertions;
using OrcaCore.Engine.Ephemeral.Tests.Shutdown;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Facade;

public sealed class ConcurrentStartTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnrelatedStart_CompletesWhileAnotherStartIsStillRunningItsFirstStep()
    {
        using var host = new HostShutdownTests.Host();
        var slow = host.Register(host.Definition());
        var fast = host.Register(Workflow.Ephemeral<HostShutdownTests.State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new HostShutdownTests.State(input)).End().Build());

        var slowStart = host.Start(slow, "slow");
        await host.Body.Entered.Task.WaitAsync(Guard, TestToken);

        var fastStart = await host.Start(fast, "fast").WaitAsync(Guard, TestToken);
        fastStart.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>()
            .Which.WasExisting.Should().BeFalse();
        slowStart.IsCompleted.Should().BeFalse("the slow workflow is still inside its first step");

        host.Body.Release.TrySetResult();
        (await slowStart.WaitAsync(Guard, TestToken)).Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
    }

    [Fact]
    public async Task SameKeyStartedWhileTheFirstIsInFlight_ReplaysTheSingleInstance()
    {
        using var host = new HostShutdownTests.Host();
        var definition = host.Register(host.Definition());

        var first = host.Start(definition, "same");
        await host.Body.Entered.Task.WaitAsync(Guard, TestToken);
        var second = host.Start(definition, "same");
        second.IsCompleted.Should().BeFalse("a same-key start waits for the in-flight owner of that key");

        host.Body.Release.TrySetResult();
        var original = await first.WaitAsync(Guard, TestToken);
        var replay = await second.WaitAsync(Guard, TestToken);
        original.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>().Which.WasExisting.Should().BeFalse();
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>().Which.WasExisting.Should().BeTrue();
        replay.GetHandleOrThrow().InstanceId.Should().Be(original.GetHandleOrThrow().InstanceId);
    }

    [Fact]
    public async Task ConcurrentStartsReachingTheSameWait_AdmitExactlyOne()
    {
        using var host = new HostShutdownTests.Host();
        var contract = WorkflowEventContract.Create(EventName.Create("concurrent-start"), EventContractVersion.Initial);
        var correlation = CorrelationId.Create("shared");
        using var contenders = new Barrier(2);
        var token = TestToken;
        var definition = host.Register(Workflow.Ephemeral<HostShutdownTests.State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new HostShutdownTests.State(input))
            .Wait(contract, _ =>
            {
                if (!contenders.SignalAndWait(Guard, token))
                    throw new TimeoutException("Both starts must reach the correlation selector.");
                return correlation;
            })
            .End().Build());

        var first = host.Start(definition, "first");
        var second = host.Start(definition, "second");

        var outcomes = await Task.WhenAll(Observe(first), Observe(second)).WaitAsync(Guard, TestToken);
        outcomes.Count(error => error is null).Should().Be(1);
        outcomes.Single(error => error is not null).Should().BeOfType<AmbiguousWaitRegistrationException>();

        static async Task<Exception?> Observe(Task<WorkflowStartResult<WorkflowInstanceHandle>> start)
        {
            try { await start; return null; }
            catch (Exception error) { return error; }
        }
    }

    [Theory]
    [InlineData("pool")]
    [InlineData("throttle")]
    public async Task CapacityGrant_DoesNotCreateASecondActiveWait(string capacity)
    {
        using var host = new HostShutdownTests.Host(capacity: capacity);
        var contract = WorkflowEventContract.Create(EventName.Create("review-probe"), EventContractVersion.Initial);
        var correlation = CorrelationId.Create("shared");
        using var permitSecondWait = new ManualResetEventSlim();
        var token = TestToken;
        var governed = Workflow.Ephemeral<HostShutdownTests.State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new HostShutdownTests.State(input))
            .Then<HostShutdownTests.Body>();
        if (capacity == "pool") governed.WithTransientPool(TransientPoolName.Create("shutdown"));
        var definition = host.Register(governed.Wait(contract, state =>
            {
                if (state.Value.Value == "second" && !permitSecondWait.Wait(Guard, token)) throw new TimeoutException();
                return correlation;
            })
            .End().Build());

        var first = definition.StartOrGetAsync("first", StartIdempotencyKey.Create("first"), token).AsTask();
        await host.Body.Entered.Task.WaitAsync(Guard, token);
        var second = definition.StartOrGetAsync("second", StartIdempotencyKey.Create("second"), token).AsTask();
        var queued = (await second.WaitAsync(Guard, token)).GetHandleOrThrow();
        var waiting = await queued.GetSnapshotAsync(token);
        waiting.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        waiting.ActiveWaits.Should().BeEmpty();

        host.Body.Release.TrySetResult();
        var firstHandle = (await first.WaitAsync(Guard, token)).GetHandleOrThrow();
        (await firstHandle.GetSnapshotAsync(token)).ActiveWaits.Should().ContainSingle();
        permitSecondWait.Set();

        await queued.WaitForSettlementAsync(token).AsTask().WaitAsync(Guard, token);
        var rejected = await queued.GetSnapshotAsync(token);
        rejected.Status.Should().Be(WorkflowInstanceStatus.Failed);
        rejected.Failure!.Code.Should().Be("WF-WAIT-AMBIGUOUS");
        rejected.ActiveWaits.Should().BeEmpty();
        (await firstHandle.GetSnapshotAsync(token)).ActiveWaits.Should().ContainSingle();
    }

    [Fact]
    public async Task CancellationDuringFingerprinting_StopsAReplay()
    {
        using var host = new HostShutdownTests.Host();
        var definition = host.Registry.Register(Workflow.Ephemeral<HostShutdownTests.State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FingerprintInput>(input => new HostShutdownTests.State(input.Value))
            .End().Build()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("replay");
        await definition.StartOrGetAsync(new FingerprintInput(), key, TestToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestToken);

        var error = await Record.ExceptionAsync(async () =>
            await definition.StartOrGetAsync(new FingerprintInput(cancellation.Cancel), key, cancellation.Token));
        cancellation.IsCancellationRequested.Should().BeTrue();
        error.Should().BeAssignableTo<OperationCanceledException>();
    }

    public sealed class FingerprintInput
    {
        private readonly Action? onRead;
        public FingerprintInput() { }
        public FingerprintInput(Action onRead) => this.onRead = onRead;
        public string Value
        {
            get
            {
                onRead?.Invoke();
                return "input";
            }
        }
    }
}
