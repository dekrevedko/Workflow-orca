using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Providers.InMemory;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class DagOutboxPumpTests
{
    [Fact]
    public async Task ChildLane_ReleasesForeignRecordsWithoutCallingHandlerOrAcknowledging()
    {
        var store = new TrackingOutbox(new OutboxWrite(OutboxRecordId.New(), "workflow-event", [1]));
        var handler = new RecordingHandler();
        var pump = new DurableDagChildPump(store, handler);
        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(0);
        store.Request!.KindSelector!.Matches("dag-child-start-v1").Should().BeTrue();
        store.Request.KindSelector.Matches("dag-child-join-v1").Should().BeTrue();
        store.Request.KindSelector.Matches("dag-child-cancel-v1").Should().BeTrue();
        store.Request.KindSelector.Matches("dag-run-ready-v1").Should().BeFalse();
        store.Request.KindSelector.Matches("application").Should().BeFalse();
        handler.Calls.Should().Be(0);
        store.Released.Should().ContainSingle();
        store.Marked.Should().BeEmpty();
    }

    [Fact]
    public async Task ChildLane_AcknowledgesOnlyAfterHandlerCommit()
    {
        var store = new TrackingOutbox(new OutboxWrite(OutboxRecordId.New(), "dag-child-start-v1", [1]) { DagRunOwner = Guid.NewGuid() });
        var committed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler { Result = committed.Task };
        var pumping = new DurableDagChildPump(store, handler).PumpOnceAsync(Request(), TestContext.Current.CancellationToken);
        handler.Calls.Should().Be(1);
        store.Marked.Should().BeEmpty();
        committed.SetResult(true);
        (await pumping).Should().Be(1);
        store.Marked.Should().Equal(OutboxRecordState.Dispatched);
    }

    [Fact]
    public async Task ChildLane_PoisonWaitsForVisibleOwningFaultAndDoesNotAcknowledgeSuccess()
    {
        var store = new TrackingOutbox(new OutboxWrite(OutboxRecordId.New(), "dag-child-start-v1", [1]) { DagRunOwner = Guid.NewGuid() });
        var committed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler { Error = new InvalidDataException("bad child signal"), FaultResult = committed.Task };
        var pumping = new DurableDagChildPump(store, handler).PumpOnceAsync(Request(), TestContext.Current.CancellationToken);
        handler.FaultCalls.Should().Be(1);
        store.Marked.Should().BeEmpty();
        committed.SetResult(true);
        (await pumping).Should().Be(0);
        store.Marked.Should().Equal(OutboxRecordState.Poisoned);
    }

    [Fact]
    public async Task ChildLane_DoesNotPoisonWhenOwningFaultCouldNotCommit()
    {
        var store = new TrackingOutbox(new OutboxWrite(OutboxRecordId.New(), "dag-child-start-v1", [1]) { DagRunOwner = Guid.NewGuid() });
        var handler = new RecordingHandler { Error = new InvalidDataException("bad child signal"), FaultResult = Task.FromResult(false) };
        (await new DurableDagChildPump(store, handler).PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(0);
        store.Marked.Should().BeEmpty();
        store.Released.Should().ContainSingle();
    }

    private static OutboxClaimRequest Request() => new(10, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));

    [Fact]
    public async Task ReadyLane_WithoutCapableSinkMakesNoClaimCall()
    {
        var store = new TrackingOutbox();
        var pump = new DurableDagReadyPump(null!, store, new(), null);
        (await pump.PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(0);
        store.Request.Should().BeNull();
    }

    [Fact]
    public async Task ReadyLane_ReleasesForeignBootAndWrongKindBeforeCallingSink()
    {
        var store = new TrackingOutbox(
            new OutboxWrite(OutboxRecordId.New(), "dag-run-ready-v1", [1]) { RecipientHostId = Guid.NewGuid() },
            new OutboxWrite(OutboxRecordId.New(), "dag-child-join-v1", [1]));
        var sink = new RecordingSink();
        var host = new DurableDagHostIdentity();
        (await new DurableDagReadyPump(null!, store, host, sink).PumpOnceAsync(Request(), TestContext.Current.CancellationToken)).Should().Be(0);
        store.Request!.RecipientHostId.Should().Be(host.HostId);
        store.Request.KindSelector!.Include.Should().BeEquivalentTo("dag-run-ready-v1");
        store.Released.Should().HaveCount(2);
        store.Marked.Should().BeEmpty();
        sink.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ReadyLane_AcknowledgesOnlyAfterAuthoritativeReadAndWakeAcceptance()
    {
        var registrations = new ServiceCollection();
        registrations.AddOrcaCoreInMemoryDurableProvider();
        await using var provider = registrations.BuildServiceProvider();
        var events = provider.GetRequiredService<IWorkflowEventStore>();
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        var (run, _) = await DagPumpRunFixture.CreateAsync(events, workflow, false, TestContext.Current.CancellationToken);
        var write = new OutboxWrite(OutboxRecordId.New(), "dag-run-ready-v1",
            Encoding.UTF8.GetBytes($"{{\"runId\":\"{run.RunId:D}\",\"version\":2,\"subscriptionId\":null}}")) { DagRunOwner = run.RunId };
        var store = new TrackingOutbox(write);
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new RecordingSink { Acceptance = accepted.Task };
        var pumping = new DurableDagReadyPump(events, store, new(), sink)
            .PumpOnceAsync(Request(), TestContext.Current.CancellationToken);
        sink.Calls.Should().Be(1);
        store.Marked.Should().BeEmpty();
        accepted.SetResult();
        (await pumping).Should().Be(1);
        store.Marked.Should().Equal(OutboxRecordState.Dispatched);
    }

    [Fact]
    public async Task ReadyLane_SinkInfrastructureFailureReleasesInsteadOfPoisoningHint()
    {
        var registrations = new ServiceCollection();
        registrations.AddOrcaCoreInMemoryDurableProvider();
        await using var provider = registrations.BuildServiceProvider();
        var events = provider.GetRequiredService<IWorkflowEventStore>();
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        var (run, _) = await DagPumpRunFixture.CreateAsync(events, workflow, false, TestContext.Current.CancellationToken);
        var write = new OutboxWrite(OutboxRecordId.New(), "dag-run-ready-v1",
            Encoding.UTF8.GetBytes($"{{\"runId\":\"{run.RunId:D}\",\"version\":2,\"subscriptionId\":null}}")) { DagRunOwner = run.RunId };
        var store = new TrackingOutbox(write);
        var sink = new RecordingSink { Acceptance = Task.FromException(new InvalidOperationException("sink unavailable")) };
        Func<Task> pumping = () => new DurableDagReadyPump(events, store, new(), sink)
            .PumpOnceAsync(Request(), TestContext.Current.CancellationToken);
        await pumping.Should().ThrowAsync<InvalidOperationException>().WithMessage("sink unavailable");
        store.Released.Should().ContainSingle();
        store.Marked.Should().BeEmpty();
    }

    private sealed class RecordingSink : IDurableDagReadySinkV1
    {
        internal int Calls;
        internal Task Acceptance { get; init; } = Task.CompletedTask;
        public ValueTask HandleAsync(DurableDagReadyNoticeV1 notice, CancellationToken token)
        {
            Calls++;
            return new(Acceptance);
        }
    }

    private sealed class RecordingHandler : IDurableDagChildHandler
    {
        internal int Calls;
        internal int FaultCalls;
        internal Exception? Error { get; init; }
        internal Task<bool> Result { get; init; } = Task.FromResult(true);
        internal Task<bool> FaultResult { get; init; } = Task.FromResult(true);
        public Task<bool> HandleAsync(OutboxWrite record, CancellationToken token)
        {
            Calls++;
            return Error is null ? Result : Task.FromException<bool>(Error);
        }
        public Task<bool> RecordFaultAsync(Guid runId, CancellationToken token)
        {
            FaultCalls++;
            return FaultResult;
        }
    }

    private sealed class TrackingOutbox(params OutboxWrite[] records) : IWorkflowOutboxStore
    {
        internal OutboxClaimRequest? Request;
        internal List<OutboxRecordId> Released { get; } = [];
        internal List<OutboxRecordState> Marked { get; } = [];
        public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(OutboxClaimRequest request, CancellationToken token)
        {
            Request = request;
            return Task.FromResult<IReadOnlyList<OutboxWrite>>(records);
        }
        public Task<Option<OutboxRecordState>> GetStateAsync(OutboxRecordId id, CancellationToken token) =>
            Task.FromResult(Option<OutboxRecordState>.None);
        public Task MarkAsync(OutboxRecordId id, OutboxRecordState state, CancellationToken token)
        {
            Marked.Add(state);
            return Task.CompletedTask;
        }
        public Task ReleaseAsync(OutboxRecordId id, CancellationToken token)
        {
            Released.Add(id);
            return Task.CompletedTask;
        }
    }
}
