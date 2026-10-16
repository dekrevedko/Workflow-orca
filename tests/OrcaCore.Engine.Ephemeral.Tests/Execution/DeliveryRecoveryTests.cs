using AwesomeAssertions;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Engine.Ephemeral.Internal;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class DeliveryRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly WorkflowEventContract Contract = WorkflowEventContract.Create(
        EventName.Create("recovery"), EventContractVersion.Initial);
    private static readonly CorrelationId Correlation = CorrelationId.Create("request");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedResume_RestoresWaitAndBufferedEventForOneSuccessfulRetry(bool bufferFirst)
    {
        var instance = Create();
        var envelope = Envelope("first");
        var attempts = 0;
        if (bufferFirst) await instance.RaiseEventAsync(envelope, Now, TestContext.Current.CancellationToken);
        var wait = Wait(instance, (_, _) => ++attempts == 1
            ? Task.FromException(new InvalidOperationException("resume failed")) : Task.CompletedTask);
        Func<Task> first = () => bufferFirst
            ? instance.MatchPendingEventAsync(wait, Now.AddSeconds(1), TestContext.Current.CancellationToken)
            : instance.RaiseEventAsync(envelope, Now.AddSeconds(1), TestContext.Current.CancellationToken);

        await first.Should().ThrowAsync<InvalidOperationException>();
        instance.ToSnapshot().ActiveWaits.Should().ContainSingle().Which.Status.Should().Be("Active");
        instance.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Waiting);
        if (bufferFirst)
            await instance.MatchPendingEventAsync(wait, Now.AddSeconds(2), TestContext.Current.CancellationToken);
        else
            await instance.RaiseEventAsync(envelope, Now.AddSeconds(2), TestContext.Current.CancellationToken);
        await instance.RaiseEventAsync(envelope, Now.AddSeconds(3), TestContext.Current.CancellationToken);

        attempts.Should().Be(2, "the failed delivery must be retryable and the successful delivery deduplicated");
        instance.HasUnresolvedRuntimeWork.Should().BeFalse();
    }

    [Fact]
    public async Task FailedTimeout_RestoresWaitAndEarlierBufferedDelivery()
    {
        var instance = Create();
        var delivered = 0;
        await instance.RaiseEventAsync(Envelope("buffered"), Now, TestContext.Current.CancellationToken);
        var wait = Wait(instance, (_, _) => { delivered++; return Task.CompletedTask; });

        Func<Task> timeout = () => instance.FireWaitTimeoutAsync(wait, Now.AddSeconds(2),
            _ => Task.FromException(new InvalidOperationException("timeout continuation failed")),
            TestContext.Current.CancellationToken);
        await timeout.Should().ThrowAsync<InvalidOperationException>();
        await instance.MatchPendingEventAsync(wait, Now.AddSeconds(3), TestContext.Current.CancellationToken);

        delivered.Should().Be(1);
        instance.HasUnresolvedRuntimeWork.Should().BeFalse();
    }

    [Fact]
    public async Task SuccessfulTimeout_DiscardsLateDeliveryAndDoesNotInvokeResumeTwice()
    {
        var instance = Create();
        var resumes = 0;
        var wait = Wait(instance, (_, _) => throw new InvalidOperationException("late event resumed"));
        await instance.FireWaitTimeoutAsync(wait, Now.AddSeconds(1),
            _ => { resumes++; return Task.CompletedTask; }, TestContext.Current.CancellationToken);
        await instance.RaiseEventAsync(Envelope("late"), Now.AddSeconds(2), TestContext.Current.CancellationToken);
        await instance.FireWaitTimeoutAsync(wait, Now.AddSeconds(3),
            _ => { resumes++; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        resumes.Should().Be(1);
        instance.HasUnresolvedRuntimeWork.Should().BeFalse();
    }

    [Fact]
    public async Task FailedDelayContinuation_RetainsTimerForRetryAndKeepsOtherWaitsSuspended()
    {
        var instance = Create();
        var timer = instance.EnterDelay(null, Now);
        var wait = Wait(instance, (_, _) => Task.CompletedTask);
        Func<Task> fire = () => instance.FireDelayAsync(timer, Now.AddSeconds(1),
            _ => Task.FromException(new InvalidOperationException("delay continuation failed")),
            TestContext.Current.CancellationToken);
        await fire.Should().ThrowAsync<InvalidOperationException>();
        var resumed = 0;
        await instance.FireDelayAsync(timer, Now.AddSeconds(2),
            _ => { resumed++; return Task.CompletedTask; }, TestContext.Current.CancellationToken);
        await instance.FireDelayAsync(timer, Now.AddSeconds(3),
            _ => { resumed++; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        resumed.Should().Be(1);
        instance.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        instance.CancelWait(wait);
        instance.Status.Should().Be(WorkflowInstanceStatus.Running);
        instance.HasUnresolvedRuntimeWork.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalCommand_CancelsOutstandingTimersAndWaits(bool terminate)
    {
        var instance = Create();
        var timer = instance.EnterDelay(null, Now);
        var wait = Wait(instance, (_, _) => Task.CompletedTask);
        var cancelledTimers = 0;
        var cancelledWaits = 0;
        timer.SetCancel(() => cancelledTimers++);
        wait.SetCancelLoser(() => cancelledWaits++);
        using var token = instance.CreateLinkedExecutionToken(CancellationToken.None);

        var terminal = terminate ? instance.Terminate(Now.AddSeconds(1)) : instance.Cancel(Now.AddSeconds(1));

        terminal.Status.Should().Be(terminate ? WorkflowInstanceStatus.Terminated : WorkflowInstanceStatus.Cancelled);
        cancelledTimers.Should().Be(1);
        cancelledWaits.Should().Be(1);
        instance.HasUnresolvedRuntimeWork.Should().BeFalse();
        if (!terminate) token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void PublishedState_RemainsDetachedFromMutableExecutionStateAndReturnedCopies()
    {
        var instance = Create();
        var runtime = (IWorkflowInstance)instance;
        var before = (State)runtime.CopyState();
        before.Values.Add(99);
        runtime.PublishState();
        instance.State.Values.Add(2);
        var published = (State)runtime.CopyState();
        published.Values.Should().Equal(1);
        published.Values.Add(3);
        ((State)runtime.CopyState()).Values.Should().Equal(1);
        instance.State.Values.Should().Equal(1, 2);
    }

    [Fact]
    public void StuckStepAndLifecycleHistory_AreBoundedAndSnapshotsRemainDetached()
    {
        var instance = Create(maxLifecycleEvents: 2);
        instance.StartStep("workflow:$/n:00000001", Now, TimeSpan.FromSeconds(5));
        var before = instance.ToSnapshot();
        instance.MarkStuckStep("workflow:$/n:00000001", Now.AddSeconds(2));
        instance.MarkStuckStep("workflow:$/n:00000001", Now.AddSeconds(3));
        instance.CompleteStep("workflow:$/n:00000001", Now.AddSeconds(4));
        instance.RecordLifecycleEvent("one", null, instance.Status, Now.AddSeconds(5));
        instance.RecordLifecycleEvent("two", null, instance.Status, Now.AddSeconds(6));
        var after = instance.ToSnapshot();

        before.HasStuckStep.Should().BeFalse();
        before.ActiveStep.Should().NotBeNull();
        after.ActiveStep.Should().BeNull();
        after.LifecycleEvents.Select(item => item.EventName).Should().Equal("one", "two");
    }

    private static RuntimeWaitRecord Wait(WorkflowInstance<State> instance,
        Func<EventEnvelope, CancellationToken, Task> resume) =>
        instance.EnterWait(Contract, Correlation, null, Now, "workflow:$/n:00000001", Now.AddSeconds(1), resume);

    private static EventEnvelope Envelope(string id) => EphemeralContractAdapter.EventEnvelope(
        EventId.Create(id), Contract, Correlation, Now, ReadOnlyMemory<byte>.Empty);

    private static WorkflowInstance<State> Create(int maxLifecycleEvents = 100) => new(
        InstanceId.Parse("00000000-0000-0000-0000-000000000001"), DefinitionId.New(),
        DefinitionVersion.Initial, new State { Values = [1] }, Now, maxLifecycleEvents: maxLifecycleEvents);

    private sealed class State
    {
        public List<int> Values { get; init; } = [];
    }
}
