using System.Text.Json;
using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

public sealed class CorrelationLookupStoreTests
{
    [Fact]
    public async Task LookupByCorrelation_returns_single_match_for_active_wait()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(
            CreateInstance("inst-1", "Approval", "corr-1", mode: WaitMode.Cold),
            CancellationToken.None);

        var result = await store.LookupByCorrelationAsync("Approval", "corr-1", CancellationToken.None);

        Assert.Equal(CorrelationMatchType.SingleMatch, result.MatchType);
        var match = Assert.Single(result.Matches);
        Assert.Equal("inst-1", match.InstanceId);
        Assert.Equal("DefA", match.DefinitionId);
        Assert.Equal("v1", match.DefinitionVersion);
        Assert.Equal(WaitMode.Cold, match.Mode);
    }

    [Fact]
    public async Task LookupByCorrelation_returns_ambiguous_for_multiple_matches()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(
            CreateInstance("inst-1", "Approval", "corr-1", mode: WaitMode.Resident),
            CancellationToken.None);
        await store.CreateAsync(
            CreateInstance("inst-2", "Approval", "corr-1", mode: WaitMode.Cold),
            CancellationToken.None);

        var result = await store.LookupByCorrelationAsync("Approval", "corr-1", CancellationToken.None);

        Assert.Equal(CorrelationMatchType.Ambiguous, result.MatchType);
        Assert.Equal(["inst-1", "inst-2"], result.Matches.Select(x => x.InstanceId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task LookupByCorrelation_ignores_non_active_waits()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(
            CreateInstance("inst-1", "Approval", "corr-1", status: WaitStatus.Matched),
            CancellationToken.None);

        var result = await store.LookupByCorrelationAsync("Approval", "corr-1", CancellationToken.None);

        Assert.Equal(CorrelationMatchType.NoMatch, result.MatchType);
        Assert.Empty(result.Matches);
    }

    private static PersistedInstance CreateInstance(
        string instanceId,
        string eventName,
        string correlationId,
        WaitMode mode = WaitMode.Resident,
        WaitStatus status = WaitStatus.Active)
    {
        return new PersistedInstance(
            instanceId,
            "DefA",
            "v1",
            0,
            JsonSerializer.SerializeToElement(new { value = "payload" }),
            new PersistedRuntimeState(
                WorkflowStatus.Waiting,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow,
                [
                    new PersistedWaitRecord(
                        "wait-1",
                        eventName,
                        correlationId,
                        BranchId: null,
                        DateTimeOffset.UtcNow.AddMinutes(-1),
                        status,
                        mode)
                ],
                [],
                [],
                Error: null,
                new PersistedExecutionPath(
                    BranchId: null,
                    [new PersistedFrame(PersistedFrameKind.Root, "", 1, ScopeId: null)]),
                ActiveParallel: null));
    }
}
