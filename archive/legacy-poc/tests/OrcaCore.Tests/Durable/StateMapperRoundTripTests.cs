using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

public sealed class StateMapperRoundTripTests
{
    private sealed record RoundTripState(
        string Id,
        bool UseThen = false,
        bool Loop = false,
        string? Payload = null);

    private record BasePayload(string Value);

    private sealed record DerivedPayload(string Value, string Extra) : BasePayload(Value);

    private sealed record ApprovalPayload(string Value);

    [Fact]
    public async Task Round_trip_preserves_top_level_wait_and_runtime_metadata()
    {
        var definition = new WorkflowBuilder<RoundTripState>("RootWaitFlow")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        var instance = new WorkflowInstance<RoundTripState>(
            "instance-root",
            definition.DefinitionId,
            new RoundTripState("corr-1"));
        var correlationIndex = new CorrelationIndex();

        await WorkflowRuntime.ExecuteAsync(instance, definition, correlationIndex);

        var runtime = instance.RuntimeState;
        runtime.PendingEvents.Add(new PendingEvent(
            new EventEnvelope("Buffered", "corr-1", "payload-1", "evt-buffered"),
            DateTimeOffset.UtcNow,
            Consumed: false));
        runtime.ConsumedEventIds.Add("evt-consumed");
        runtime.Error = WorkflowError.FromMetadata("persisted error", "System.InvalidOperationException", "Wait", DateTimeOffset.UtcNow);

        var persisted = StateMapper.ToPersistedState(instance, definitionVersion: "v1", concurrencyToken: 7);
        var restored = StateMapper.FromPersistedState(persisted, definition);

        Assert.Equal("v1", persisted.DefinitionVersion);
        Assert.Equal(7, persisted.ConcurrencyToken);
        Assert.Equal(instance.BusinessState, restored.BusinessState);
        Assert.Equal(runtime.Status, restored.RuntimeState.Status);
        Assert.Equal(runtime.CreatedAt, restored.RuntimeState.CreatedAt);
        Assert.Equal(runtime.LastTransitionAt, restored.RuntimeState.LastTransitionAt);
        Assert.Single(restored.RuntimeState.ActiveWaits);
        Assert.Single(restored.RuntimeState.PendingEvents);
        Assert.Contains("evt-consumed", restored.RuntimeState.ConsumedEventIds);
        Assert.Equal("persisted error", restored.RuntimeState.Error?.Exception.Message);
        Assert.Equal("System.InvalidOperationException", restored.RuntimeState.Error?.ExceptionType);
        Assert.Equal("", restored.RuntimeState.MainPath.Frames[0].NodePath);
        Assert.Equal(1, restored.RuntimeState.MainPath.Frames[0].Index);
        Assert.Null(restored.RuntimeState.MainPath.Frames[0].ScopeId);
        Assert.Equal("payload-1", Assert.IsType<string>(restored.RuntimeState.PendingEvents[0].Envelope.Payload));
    }

    [Fact]
    public async Task Round_trip_restores_if_branch_frame_stack()
    {
        var definition = new WorkflowBuilder<RoundTripState>("IfFlow")
            .Init()
            .If(
                state => state.UseThen,
                then: branch => branch.Wait("ThenEvent", state => state.Id),
                @else: branch => branch.Wait("ElseEvent", state => state.Id))
            .End()
            .Build();

        var instance = new WorkflowInstance<RoundTripState>(
            "instance-if",
            definition.DefinitionId,
            new RoundTripState("corr-if", UseThen: true));

        await WorkflowRuntime.ExecuteAsync(instance, definition, new CorrelationIndex());

        var persisted = StateMapper.ToPersistedState(instance);
        var restored = StateMapper.FromPersistedState(persisted, definition);

        Assert.Collection(
            restored.RuntimeState.MainPath.Frames,
            frame =>
            {
                Assert.Equal("", frame.NodePath);
                Assert.Equal(1, frame.Index);
            },
            frame =>
            {
                Assert.Equal("0/then", frame.NodePath);
                Assert.Equal(1, frame.Index);
                Assert.False(string.IsNullOrWhiteSpace(frame.ScopeId));
            });
    }

    [Fact]
    public async Task Round_trip_restores_while_body_frame_stack()
    {
        var definition = new WorkflowBuilder<RoundTripState>("WhileFlow")
            .Init()
            .While(
                state => state.Loop,
                body => body.Wait("LoopEvent", state => state.Id))
            .End()
            .Build();

        var instance = new WorkflowInstance<RoundTripState>(
            "instance-while",
            definition.DefinitionId,
            new RoundTripState("corr-while", Loop: true));

        await WorkflowRuntime.ExecuteAsync(instance, definition, new CorrelationIndex());

        var persisted = StateMapper.ToPersistedState(instance);
        var restored = StateMapper.FromPersistedState(persisted, definition);

        Assert.Collection(
            restored.RuntimeState.MainPath.Frames,
            frame =>
            {
                Assert.Equal("", frame.NodePath);
                Assert.Equal(0, frame.Index);
            },
            frame =>
            {
                Assert.Equal("0/body", frame.NodePath);
                Assert.Equal(1, frame.Index);
                Assert.False(string.IsNullOrWhiteSpace(frame.ScopeId));
            });
    }

    [Fact]
    public async Task Round_trip_restores_parallel_branch_paths()
    {
        var definition = new WorkflowBuilder<RoundTripState>("ParallelFlow")
            .Init()
            .Parallel(parallel => parallel
                .Branch("alpha", branch => branch.Wait("AlphaEvent", state => state.Id))
                .Branch("beta", branch => branch.Wait("BetaEvent", state => state.Id)))
            .End()
            .Build();

        var instance = new WorkflowInstance<RoundTripState>(
            "instance-parallel",
            definition.DefinitionId,
            new RoundTripState("corr-parallel"));

        await WorkflowRuntime.ExecuteAsync(instance, definition, new CorrelationIndex());

        var persisted = StateMapper.ToPersistedState(instance);
        var restored = StateMapper.FromPersistedState(persisted, definition);

        Assert.NotNull(restored.RuntimeState.ActiveParallel);
        Assert.Equal(2, restored.RuntimeState.ActiveParallel!.BranchPaths.Count);

        var alpha = restored.RuntimeState.ActiveParallel.BranchPaths["alpha"];
        var beta = restored.RuntimeState.ActiveParallel.BranchPaths["beta"];

        Assert.Equal("alpha", alpha.BranchId);
        Assert.Equal("beta", beta.BranchId);
        Assert.Single(alpha.Frames);
        Assert.Single(beta.Frames);
        Assert.Equal("0/branch/alpha", alpha.Frames[0].NodePath);
        Assert.Equal("0/branch/beta", beta.Frames[0].NodePath);
        Assert.Equal(1, alpha.Frames[0].Index);
        Assert.Equal(1, beta.Frames[0].Index);
        Assert.False(string.IsNullOrWhiteSpace(alpha.Frames[0].ScopeId));
        Assert.False(string.IsNullOrWhiteSpace(beta.Frames[0].ScopeId));
        Assert.NotEqual(alpha.Frames[0].ScopeId, beta.Frames[0].ScopeId);
    }

    [Fact]
    public async Task Round_trip_restores_registered_custom_payload_type()
    {
        var definition = new WorkflowBuilder<RoundTripState>("CustomPayloadFlow")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        var registry = new DurablePayloadTypeRegistry()
            .Register<string>("string")
            .Register<ApprovalPayload>("approval");

        var instance = new WorkflowInstance<RoundTripState>(
            "instance-custom-payload",
            definition.DefinitionId,
            new RoundTripState("corr-custom"));

        await WorkflowRuntime.ExecuteAsync(instance, definition, new CorrelationIndex());
        instance.RuntimeState.PendingEvents.Add(new PendingEvent(
            new EventEnvelope("Buffered", "corr-custom", new ApprovalPayload("approved"), "evt-custom"),
            DateTimeOffset.UtcNow,
            Consumed: false));

        var persisted = StateMapper.ToPersistedState(instance, payloadTypeResolver: registry);
        var restored = StateMapper.FromPersistedState(persisted, definition, payloadTypeResolver: registry);

        var payload = Assert.IsType<ApprovalPayload>(restored.RuntimeState.PendingEvents[0].Envelope.Payload);
        Assert.Equal("approved", payload.Value);
        Assert.Equal("approval", persisted.RuntimeState.PendingEvents[0].Envelope.PayloadTypeKey);
    }

    [Fact]
    public void Round_trip_fails_for_unknown_persisted_payload_type_key()
    {
        var definition = new WorkflowBuilder<RoundTripState>("UnknownPayloadFlow")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        var persisted = new PersistedInstance(
            "instance-unknown-payload",
            definition.DefinitionId,
            "v1",
            0,
            System.Text.Json.JsonSerializer.SerializeToElement(new RoundTripState("corr-unknown")),
            new PersistedRuntimeState(
                WorkflowStatus.Waiting,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow,
                [
                    new PersistedWaitRecord(
                        "wait-1",
                        "Approval",
                        "corr-unknown",
                        BranchId: null,
                        DateTimeOffset.UtcNow.AddMinutes(-1),
                        WaitStatus.Active,
                        WaitMode.Resident)
                ],
                [
                    new PersistedPendingEvent(
                        new PersistedEventEnvelope(
                            "Buffered",
                            "corr-unknown",
                            System.Text.Json.JsonSerializer.SerializeToElement(new { Value = "approved" }),
                            "unknown-key",
                            "evt-unknown"),
                        DateTimeOffset.UtcNow,
                        Consumed: false)
                ],
                [],
                Error: null,
                new PersistedExecutionPath(
                    BranchId: null,
                    [new PersistedFrame(PersistedFrameKind.Root, "", 1, ScopeId: null)]),
                ActiveParallel: null));

        var ex = Assert.Throws<DurablePayloadDeserializationException>(() =>
            StateMapper.FromPersistedState(persisted, definition));

        Assert.Contains("unknown-key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Round_trip_fails_with_diagnostic_exception_for_unknown_persisted_node_path()
    {
        var definition = new WorkflowBuilder<RoundTripState>("InvalidNodePathFlow")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        var persisted = new PersistedInstance(
            "instance-invalid-node",
            definition.DefinitionId,
            "v1",
            0,
            System.Text.Json.JsonSerializer.SerializeToElement(new RoundTripState("corr-invalid")),
            new PersistedRuntimeState(
                WorkflowStatus.Waiting,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow,
                [
                    new PersistedWaitRecord(
                        "wait-1",
                        "Approval",
                        "corr-invalid",
                        BranchId: null,
                        DateTimeOffset.UtcNow.AddMinutes(-1),
                        WaitStatus.Active,
                        WaitMode.Resident)
                ],
                [],
                [],
                Error: null,
                new PersistedExecutionPath(
                    BranchId: null,
                    [new PersistedFrame(PersistedFrameKind.IfBranch, "missing/path", 0, ScopeId: "scope-1")]),
                ActiveParallel: null));

        var ex = Assert.Throws<DurableDefinitionRehydrationException>(() =>
            StateMapper.FromPersistedState(persisted, definition));

        Assert.Contains("instance-invalid-node", ex.Message, StringComparison.Ordinal);
        Assert.Contains(definition.DefinitionId, ex.Message, StringComparison.Ordinal);
        Assert.Contains("v1", ex.Message, StringComparison.Ordinal);
        Assert.Contains("missing/path", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Round_trip_preserves_declared_payload_type_for_buffered_events()
    {
        var definition = new WorkflowBuilder<RoundTripState>("DeclaredPayloadFlow")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        var registry = new DurablePayloadTypeRegistry()
            .Register<BasePayload>("base", "schema-base")
            .Register<DerivedPayload>("derived", "schema-derived");
        var serializer = new JsonPayloadEnvelopeSerializer(registry);

        var instance = new WorkflowInstance<RoundTripState>(
            "instance-declared-payload",
            definition.DefinitionId,
            new RoundTripState("corr-declared"));

        await WorkflowRuntime.ExecuteAsync(instance, definition, new CorrelationIndex());
        instance.RuntimeState.PendingEvents.Add(new PendingEvent(
            new EventEnvelope(
                "Buffered",
                "corr-declared",
                new DerivedPayload("approved", "extra"),
                "evt-declared",
                typeof(BasePayload)),
            DateTimeOffset.UtcNow,
            Consumed: false));

        var persisted = StateMapper.ToPersistedState(instance, payloadEnvelopeSerializer: serializer);
        var restored = StateMapper.FromPersistedState(persisted, definition, payloadEnvelopeSerializer: serializer);

        Assert.Equal("base", persisted.RuntimeState.PendingEvents[0].Envelope.PayloadTypeKey);
        var payload = Assert.IsType<BasePayload>(restored.RuntimeState.PendingEvents[0].Envelope.Payload);
        Assert.Equal("approved", payload.Value);
    }
}
