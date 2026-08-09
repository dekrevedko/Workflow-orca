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
        var applicationPath = DurableApplicationContractFactory.PayloadFingerprint(input).Value;

        applicationPath.Should().Be(serializedPath);
    }

    private sealed class LegacyInboxProvider : IWorkflowEventStore, IWorkflowInboxStore
    {
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
