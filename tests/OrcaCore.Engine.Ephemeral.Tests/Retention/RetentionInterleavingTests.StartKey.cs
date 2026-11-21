using AwesomeAssertions;
using Xunit;
using StartResult = OrcaCore.WorkflowStartResult<OrcaCore.WorkflowInstanceHandle<string>>;

namespace OrcaCore.Engine.Ephemeral.Tests.Retention;

public sealed partial class RetentionInterleavingTests
{
    [Fact]
    public async Task StartKeyRetirement_SweepSkipsHeldPinAndReplayKeepsOriginalBinding()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new Host(2, async _ =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Guard, Token);
        });
        var definition = host.Register();
        var old = await host.Start(definition, "old");
        await host.Start(definition, "unpinned");
        var key = StartIdempotencyKey.Create("old");
        var read = old.GetSnapshotAsync(Token).AsTask();
        try
        {
            await entered.Task.WaitAsync(Guard, Token);
            host.Clock.Advance(TimeSpan.FromSeconds(10));
            host.Registry.ListInstances().Select(i => i.InstanceId).Should().Equal([old.InstanceId],
                "the same sweep retires the unpinned peer while this read holds the old lifetime pin");
            var replay = await definition.StartOrGetAsync("old", key, Token);
            replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<List<string>>>.Accepted>()
                .Which.WasExisting.Should().BeTrue();
            replay.GetHandleOrThrow().InstanceId.Should().Be(old.InstanceId);
            (await definition.StartOrGetAsync("new", key, Token)).Should()
                .BeOfType<WorkflowStartResult<WorkflowInstanceHandle<List<string>>>.Conflict>();
        }
        finally { release.TrySetResult(); }
        (await read.WaitAsync(Guard, Token)).InstanceId.Should().Be(old.InstanceId);
        host.Registry.ListInstances().Should().BeEmpty();
        var fresh = await definition.StartOrGetAsync("new", key, Token);
        fresh.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<List<string>>>.Accepted>()
            .Which.WasExisting.Should().BeFalse();
        fresh.GetHandleOrThrow().InstanceId.Should().NotBe(old.InstanceId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartKeyRetirement_ReplayDecisionSurvivesReplacementDuringPinRelease(bool compatible)
    {
        using var host = new Host(1);
        using var finishCleanup = new ManualResetEventSlim();
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        var definition = host.Registry.Register(Workflow.Ephemeral<string>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => value)
            .Then(context =>
            {
                if (Interlocked.Increment(ref starts) == 1)
                {
                    // Register before facade publication so old-key cleanup is still outstanding at the barrier.
                    host.Engine.TryGetFacadeInstance(context.Execution.WorkflowInstanceId, out var runtime).Should().BeTrue();
                    runtime!.Lifetime.RegisterCleanup(finishCleanup, () =>
                    {
                        cleanupEntered.TrySetResult();
                        finishCleanup.Wait(Guard, Token).Should().BeTrue();
                    });
                }
                return ValueTask.CompletedTask;
            }).End(state => state.Value).Build()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("key");
        // Settle between timer ticks so age can expire just before the next sweep.
        host.Clock.Advance(TimeSpan.FromMilliseconds(500));
        var old = (await definition.StartOrGetAsync("old", key, Token)).GetHandleOrThrow();
        var oldConflict = (await definition.StartOrGetAsync("new", key, Token))
            .Should().BeOfType<StartResult.Conflict>().Which.Error;
        oldConflict.ExistingInputFingerprint.Should().Be(FacadeValueFactory.PayloadFingerprint("old"));
        oldConflict.AttemptedInputFingerprint.Should().Be(FacadeValueFactory.PayloadFingerprint("new"));
        host.Clock.Advance(TimeSpan.FromMilliseconds(9500));
        host.Clock.Advance(TimeSpan.FromMilliseconds(500));
        host.Engine.TryGetFacadeInstance(old.InstanceId, out var retained).Should().BeTrue();
        retained!.Lifetime.IsRetired.Should().BeFalse();

        var replay = Task.Run(async () => await definition.StartOrGetAsync(compatible ? "old" : "new", key, Token), Token);
        WorkflowInstanceHandle<string> replacement;
        try
        {
            await cleanupEntered.Task.WaitAsync(Guard, Token);
            replay.IsCompleted.Should().BeFalse();
            retained.Lifetime.IsRetired.Should().BeTrue();
            host.Registry.TryGetInstance(old.InstanceId, out _).Should().BeTrue("the original facade cleanup is paused");
            var fresh = (await definition.StartOrGetAsync("new", key, Token)).Should().BeOfType<StartResult.Accepted>().Which;
            fresh.WasExisting.Should().BeFalse();
            replacement = fresh.Handle;
            replacement.InstanceId.Should().NotBe(old.InstanceId);
        }
        finally { finishCleanup.Set(); }

        var captured = await replay.WaitAsync(Guard, Token);
        if (compatible)
        {
            var original = captured.Should().BeOfType<StartResult.Accepted>().Which;
            original.WasExisting.Should().BeTrue();
            original.Handle.InstanceId.Should().Be(old.InstanceId);
            var error = await Record.ExceptionAsync(async () => await original.Handle.GetSnapshotAsync(Token));
            error.Should().BeOfType<WorkflowInstanceNotFoundException>().Which.InstanceId.Should().Be(old.InstanceId);
        }
        else
        {
            captured.Should().BeOfType<StartResult.Conflict>().Which.Error.Should().BeEquivalentTo(oldConflict);
        }
        var current = (await definition.StartOrGetAsync("new", key, Token)).Should().BeOfType<StartResult.Accepted>().Which;
        current.WasExisting.Should().BeTrue();
        current.Handle.InstanceId.Should().Be(replacement.InstanceId);
        (await replacement.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Completed);
        starts.Should().Be(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartKeyRetirement_CallBeforeSweepInspectsReplacementAfterFingerprinting(bool compatible)
    {
        using var host = new Host(1);
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        var definition = host.Registry.Register(Workflow.Ephemeral<string>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<FingerprintInput>(input => input.Value)
            .Then(_ => { Interlocked.Increment(ref starts); return ValueTask.CompletedTask; })
            .End(state => state.Value).Build()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("key");
        var old = (await definition.StartOrGetAsync(new FingerprintInput("old"), key, Token)).GetHandleOrThrow();
        var delayedInput = new FingerprintInput(compatible ? "new" : "old", () =>
        {
            entered.TrySetResult();
            resume.Wait(Guard, Token).Should().BeTrue();
        });
        var delayed = Task.Run(async () => await definition.StartOrGetAsync(delayedInput, key, Token), Token);
        WorkflowInstanceHandle<string> replacement;
        try
        {
            await entered.Task.WaitAsync(Guard, Token);
            host.Clock.Advance(TimeSpan.FromSeconds(10));
            host.Registry.ListInstances().Should().BeEmpty();
            var fresh = (await definition.StartOrGetAsync(new FingerprintInput("new"), key, Token))
                .Should().BeOfType<StartResult.Accepted>().Which;
            fresh.WasExisting.Should().BeFalse();
            replacement = fresh.Handle;
            replacement.InstanceId.Should().NotBe(old.InstanceId);
        }
        finally { resume.Set(); }
        var result = await delayed.WaitAsync(Guard, Token);
        if (compatible)
        {
            var replay = result.Should().BeOfType<StartResult.Accepted>().Which;
            replay.WasExisting.Should().BeTrue();
            replay.Handle.InstanceId.Should().Be(replacement.InstanceId);
        }
        else
        {
            var conflict = result.Should().BeOfType<StartResult.Conflict>().Which.Error;
            conflict.ExistingInputFingerprint.Should().Be(FacadeValueFactory.PayloadFingerprint(new FingerprintInput("new")));
            conflict.AttemptedInputFingerprint.Should().Be(FacadeValueFactory.PayloadFingerprint(new FingerprintInput("old")));
        }
        starts.Should().Be(2);
    }

    public sealed class FingerprintInput(string value, Action? onRead = null)
    {
        private Action? observe = onRead;
        public string Value
        {
            get
            {
                Interlocked.Exchange(ref observe, null)?.Invoke();
                return value;
            }
        }
    }
}
