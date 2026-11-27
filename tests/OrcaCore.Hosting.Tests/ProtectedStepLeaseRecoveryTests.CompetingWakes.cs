using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class ProtectedStepLeaseRecoveryTests
{
    private sealed class WakeAppendProbe(InstanceId waiter, StreamVersion version, Task resume) : DagEventStoreProbe,
        IWorkflowStartIdempotencyStore, IWorkflowInboxStore
    {
        internal TaskCompletionSource<ProviderCommitBatch> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource EnterProvider { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<Result<AppendEventsResult>> Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // The command processor discovers these provider capabilities on its event-store instance.
        private IWorkflowInboxStore Inbox => (IWorkflowInboxStore)Inner;
        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(string key, CancellationToken token) =>
            ((IWorkflowStartIdempotencyStore)Inner).GetStartedAsync(key, token);
        public Task<Option<InboxStartIntentRecord>> GetStartIntentAsync(string key, CancellationToken token) =>
            Inbox.GetStartIntentAsync(key, token);
        public Task<Option<InboxRecord>> GetAsync(InstanceId id, EventId eventId, CancellationToken token) =>
            Inbox.GetAsync(id, eventId, token);
        public Task<Option<InboxRecord>> GetByEventIdAsync(EventId eventId, CancellationToken token) =>
            Inbox.GetByEventIdAsync(eventId, token);
        public Task<InboxMatchSnapshot> GetMatchSnapshotAsync(InboxMatchRequest request, CancellationToken token) =>
            Inbox.GetMatchSnapshotAsync(request, token);
        public Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(long after, int count, CancellationToken token) =>
            Inbox.ListReceivedAsync(after, count, token);
        public Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(DateTimeOffset at, int count, CancellationToken token) =>
            Inbox.ListHandoffRetriesAsync(at, count, token);

        public override async Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch, CancellationToken token)
        {
            if (!waiter.Equals(batch.StreamId.InstanceId) || batch.ExpectedVersion != version || !Ready.TrySetResult(batch))
                return await base.AppendAsync(batch, token);

            await EnterProvider.Task.WaitAsync(token);
            Result<AppendEventsResult> result;
            try
            {
                result = await base.AppendAsync(batch, token);
            }
            catch (Exception error)
            {
                Returned.SetException(error);
                throw;
            }
            Returned.SetResult(result);
            // Neither facade may advance past its first append until both SQL outcomes are recorded.
            await resume.WaitAsync(token);
            return result;
        }
    }

    private static async Task RacePromotedWaiterAsync(InstanceId waiter, StreamVersion version, WaitId waitId,
        EventId grant, ResourcePoolTicket ticket, string wakePath, Func<DagEventStoreProbe, Task> ingress,
        Func<DagEventStoreProbe, Task> redrive, Action releaseBody, CancellationToken token)
    {
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var driveProbe = new WakeAppendProbe(waiter, version, resume.Task);
        var grantProbe = new WakeAppendProbe(waiter, version, resume.Task);
        var driveCall = redrive(driveProbe);
        var grantCall = ingress(grantProbe);
        try
        {
            var timeout = TimeSpan.FromSeconds(30);
            async Task<ProviderCommitBatch> PreparedAsync(WakeAppendProbe probe, Task caller)
            {
                if (await Task.WhenAny(probe.Ready.Task, caller).WaitAsync(timeout, token) == caller)
                {
                    await caller;
                    Assert.Fail("Public wake returned before offering a workflow append at the queued version.");
                }
                return await probe.Ready.Task;
            }
            var driveBatch = await PreparedAsync(driveProbe, driveCall);
            var grantBatch = await PreparedAsync(grantProbe, grantCall);
            var cancelled = Assert.Single(driveBatch.Events.OfType<WorkflowWaitCancelledEvent>());
            var acquired = Assert.Single(driveBatch.Events.OfType<WorkflowResourcePoolAcquiredEvent>());
            Assert.Equal(waitId, cancelled.WaitId);
            Assert.Equal(ticket, Assert.Single(acquired.Tickets));
            var matched = Assert.Single(grantBatch.Events.OfType<WorkflowWaitMatchedEvent>());
            Assert.Equal(waitId, matched.WaitId);
            Assert.Equal(grant, matched.MatchedEventId);

            var (winner, loser) = wakePath == "GrantEventWins" ? (grantProbe, driveProbe) : (driveProbe, grantProbe);
            winner.EnterProvider.SetResult();
            var committed = await winner.Returned.Task.WaitAsync(timeout, token);
            Assert.True(committed.IsSuccess, committed.IsFailure ? committed.Error.Message : null);
            loser.EnterProvider.SetResult();
            var rejected = await loser.Returned.Task.WaitAsync(timeout, token);
            Assert.True(rejected.IsFailure);
            Assert.IsType<WorkflowConcurrencyException>(rejected.Error);
            Assert.Equal(EventStoreConflict.ExpectedVersionMismatch(version, committed.Value.NewVersion).Error.Message,
                rejected.Error.Message);
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"{wakePath} on B={waiter} at stream={version.Value}, winner committed={committed.Value.NewVersion.Value}; " +
                $"stale SQL append rejected: {rejected.Error.Message}");
        }
        catch
        {
            driveProbe.EnterProvider.TrySetCanceled();
            grantProbe.EnterProvider.TrySetCanceled();
            resume.TrySetCanceled();
            releaseBody();
            // Join both public calls before tearing down their database, retaining the barrier failure.
            _ = await Record.ExceptionAsync(() => Task.WhenAll(driveCall, grantCall));
            throw;
        }
        resume.SetResult();
        await Task.WhenAll(driveCall, grantCall);
    }
}
