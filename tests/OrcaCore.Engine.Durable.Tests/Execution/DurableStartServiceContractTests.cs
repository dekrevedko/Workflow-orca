using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers.Dag;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Internal;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableStartServiceContractTests
{
    [Fact]
    public async Task FailedStart_LegacyInboxProviderPreservesTheOriginalFailureDiagnostic()
    {
        var store = new LegacyInboxProvider();
        var service = new DurableStartService(new DurableCommandProcessor(store));
        var request = new StartOrGetRequest(
            "legacy-provider-start",
            DefinitionId.New(),
            DefinitionVersion.Initial,
            "definition-fingerprint",
            "input-fingerprint",
            Input: null,
            new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));

        var act = () => service.StartOrGetAsync(request, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().StartWith(
                "StartOrGet could not start workflow for key 'legacy-provider-start':",
                "a legacy provider's unsupported pending-intent lookup must not replace the start failure");
    }

    [Fact]
    public void WorkflowInputFingerprint_UsesOneByteIdenticalFixedCodecImplementation()
    {
        const string input = "order-42";
        var serializedPath = DurableWorkflowInputFingerprint.Create(input);
        var applicationPath = DurableContractAdapter.PayloadFingerprint(input).Value;

        applicationPath.Should().Be(serializedPath);
    }

    private sealed class LegacyInboxProvider : IWorkflowEventStore, IWorkflowInboxStore
    {
        public Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(
            WorkflowStreamId streamId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(
            DagRunStartKeyV1 startKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(
            WorkflowStreamId streamId, StreamVersion afterVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(
            DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(
            DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(
            DagRunFenceV1 fence, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task CancelDagRunLeaseRequestAsync(
            Guid runId, Guid ownerId, Guid requestId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(
            DagRunDiscoveryRequestV1 request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(
            DagRunSubscriptionV1 subscription, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<int> PruneExpiredDagRunObserversAsync(
            int maxCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow-only test double does not implement DAG persistence.");

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Option<CheckpointWrite>.None);

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken) =>
            Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                batch.ExpectedVersion,
                batch.ExpectedVersion));

        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowEvent>>([]);

        public Task<Option<InboxRecord>> GetByEventIdAsync(
            EventId eventId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Option<InboxRecord>.None);

        public Task<Option<InboxRecord>> GetAsync(
            InstanceId instanceId,
            EventId eventId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Option<InboxRecord>.None);

        public Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
            long afterAcceptanceSequence,
            int maxCount,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<InboxRecord>>([]);

        public Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
            DateTimeOffset eligibleAt,
            int maxCount,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<InboxRecord>>([]);
    }
}
