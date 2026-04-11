using OrcaCore.Abstractions.Enums;
using OrcaCore.Abstractions.Models;
using OrcaCore.EventDrivenPrototype.Definitions;
using OrcaCore.EventDrivenPrototype.Engine;
using OrcaCore.EventDrivenPrototype.Persistence;

namespace OrcaCore.EventDrivenPrototype.Tests;

public sealed class PrototypeEventRoutingTests
{
    private static PrototypeCheckpointState Checkpoint(
        IReadOnlyList<WaitRecord> waits,
        IReadOnlyList<PendingEvent> pending,
        IReadOnlySet<string> consumed,
        int version = 1) =>
        new(
            "inst-1",
            "def",
            "v1",
            typeof(object),
            new object(),
            WorkflowStatus.Waiting,
            0,
            waits,
            pending,
            consumed,
            version);

    [Fact]
    public void ClassifyRaiseToInstance_duplicate_consumed()
    {
        var env = new EventEnvelope("e", "c", null, "evt-1");
        var cp = Checkpoint([], [], new HashSet<string>(StringComparer.Ordinal) { "evt-1" });

        var d = PrototypeEventRouting.ClassifyRaiseToInstance(cp, env);

        Assert.Equal(PrototypeEventRouting.RaiseToInstanceKind.DuplicateConsumed, d.Kind);
        Assert.Null(d.MatchingWait);
    }

    [Fact]
    public void ClassifyRaiseToInstance_matching_wait_wins_over_pending_duplicate()
    {
        var wait = new WaitRecord("w1", "ready", "c1", null, DateTimeOffset.UtcNow, WaitStatus.Active, WaitMode.Resident);
        var env = new EventEnvelope("ready", "c1", null, "evt-1");
        var pending = new[] { new PendingEvent(env, DateTimeOffset.UtcNow, false) };
        var cp = Checkpoint([wait], pending, new HashSet<string>(StringComparer.Ordinal));

        var d = PrototypeEventRouting.ClassifyRaiseToInstance(cp, env);

        Assert.Equal(PrototypeEventRouting.RaiseToInstanceKind.MatchingWaitResume, d.Kind);
        Assert.Same(wait, d.MatchingWait);
    }

    [Fact]
    public void ClassifyRaiseToInstance_duplicate_pending_buffer_when_no_matching_wait()
    {
        var env = new EventEnvelope("ready", "c1", null, "evt-1");
        var pending = new[] { new PendingEvent(env, DateTimeOffset.UtcNow, false) };
        var cp = Checkpoint([], pending, new HashSet<string>(StringComparer.Ordinal));

        var d = PrototypeEventRouting.ClassifyRaiseToInstance(cp, env);

        Assert.Equal(PrototypeEventRouting.RaiseToInstanceKind.DuplicatePendingBuffer, d.Kind);
    }

    [Fact]
    public void ClassifyRaiseToInstance_buffer_unmatched_when_no_wait_and_no_duplicate()
    {
        var env = new EventEnvelope("ready", "c1", null, "evt-1");
        var cp = Checkpoint([], [], new HashSet<string>(StringComparer.Ordinal));

        var d = PrototypeEventRouting.ClassifyRaiseToInstance(cp, env);

        Assert.Equal(PrototypeEventRouting.RaiseToInstanceKind.BufferUnmatchedEvent, d.Kind);
    }

    [Fact]
    public void TryGetDefinition_success_and_failure()
    {
        var registered = RegisteredPrototypeDefinition.Create(
            new EventDrivenWorkflowDefinition<object>("x", "v1", []));

        var dict = new Dictionary<(string, string), RegisteredPrototypeDefinition>
        {
            [("x", "v1")] = registered
        };

        var ok = PrototypeEventRouting.TryGetDefinition(dict, "x", "v1");
        var bad = PrototypeEventRouting.TryGetDefinition(dict, "y", "v1");

        Assert.True(ok.IsSuccess);
        Assert.Same(registered, ok.Value);
        Assert.True(bad.IsFailure);
        Assert.Contains("not registered", bad.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryRequireCheckpoint_success_and_failure()
    {
        var cp = Checkpoint([], [], new HashSet<string>(StringComparer.Ordinal));
        var ok = PrototypeEventRouting.TryRequireCheckpoint(cp, "inst-1");
        var missing = PrototypeEventRouting.TryRequireCheckpoint(null, "inst-1");

        Assert.True(ok.IsSuccess);
        Assert.Same(cp, ok.Value);
        Assert.True(missing.IsFailure);
        Assert.Contains("not found", missing.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }
}
