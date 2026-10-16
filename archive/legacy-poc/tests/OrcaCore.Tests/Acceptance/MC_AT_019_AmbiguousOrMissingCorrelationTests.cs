
namespace OrcaCore.Tests;

// RWI-AT-009: Ambiguous or missing correlation is rejected clearly
//
// When no active wait matches a correlation-targeted event, the call must fail
// with a NoActiveWaitException whose message clearly identifies the problem.
// When more than one instance matches the same (EventName, CorrelationId), the
// call must fail with an AmbiguousCorrelationException so the caller knows the
// correlation key is not unique.

public class MC_AT_019_AmbiguousOrMissingCorrelationTests
{
    private sealed class OrderState
    {
        public string OrderId { get; set; } = "order-1";
    }

    // ── No active wait ──────────────────────────────────────────────────────

    [Fact]
    public async Task No_active_wait_for_event_name_throws_NoActiveWaitException()
    {
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        await typed.Start(new OrderState { OrderId = "order-1" });

        // Different event name – nothing waiting for "OrderRejected"
        var ex = await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("OrderRejected", "order-1", null, "evt-1")));

        Assert.Contains("no active wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_active_wait_for_correlation_id_throws_NoActiveWaitException()
    {
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        await typed.Start(new OrderState { OrderId = "order-1" });

        // Correct event name but wrong correlation ID
        var ex = await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("OrderApproved", "order-unknown", null, "evt-1")));

        Assert.Contains("no active wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_active_wait_when_engine_has_no_instances_throws_NoActiveWaitException()
    {
        await using var engine = new WorkflowEngine();

        var ex = await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("SomeEvent", "corr-1", null, "evt-1")));

        Assert.Contains("no active wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_active_wait_after_instance_completes_throws_NoActiveWaitException()
    {
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new OrderState { OrderId = "order-1" });

        // Resume and complete the instance
        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("OrderApproved", "order-1", null, "evt-resume"));

        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snapshot.InstanceId).Get().Status);

        // Now try correlation-targeted – the wait was unregistered on completion
        var ex = await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("OrderApproved", "order-1", null, "evt-2")));

        Assert.Contains("no active wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Ambiguous correlation ───────────────────────────────────────────────

    [Fact]
    public async Task Two_instances_with_same_correlation_key_throws_AmbiguousCorrelationException()
    {
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        // Both instances register (EventName="OrderApproved", CorrelationId="order-1")
        await typed.Start(new OrderState { OrderId = "order-1" });
        await typed.Start(new OrderState { OrderId = "order-1" });

        var ex = await Assert.ThrowsAsync<AmbiguousCorrelationException>(() =>
            engine.RaiseEvent(new EventEnvelope("OrderApproved", "order-1", null, "evt-1")));

        Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ambiguity_resolved_after_one_instance_completes_via_instance_scope()
    {
        // Start two instances with the same correlation key.
        // Use instance-targeted routing to resolve one, then correlation-targeted
        // routing succeeds because only one active wait remains.
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        var snap1 = await typed.Start(new OrderState { OrderId = "order-1" });
        var snap2 = await typed.Start(new OrderState { OrderId = "order-1" });

        // Resolve instance 1 via direct instance-targeted routing (bypasses ambiguity check)
        await engine.Instance(snap1.InstanceId)
            .RaiseEvent(new EventEnvelope("OrderApproved", "order-1", null, "evt-resolve-1"));

        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snap1.InstanceId).Get().Status);
        Assert.Equal(WorkflowStatus.Waiting, engine.Instance(snap2.InstanceId).Get().Status);

        // Now only one active wait remains – correlation-targeted routing should succeed
        await engine.RaiseEvent(new EventEnvelope("OrderApproved", "order-1", null, "evt-resolve-2"));

        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snap2.InstanceId).Get().Status);
    }
}
