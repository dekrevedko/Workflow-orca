namespace OrcaCore.Tests.Durable;

public sealed class MessagingContractTests
{
    [Fact]
    public void Dispatch_message_defaults_headers_to_empty_non_null_collection()
    {
        var message = new DispatchMessage(
            "msg-1",
            "idem-1",
            "WorkflowWaiting",
            "workflow-status",
            "WorkflowWaiting",
            new DispatchPayload(new byte[] { 1, 2, 3 }, "application/json", "schema-1"));

        Assert.NotNull(message.Headers);
        Assert.Empty(message.Headers);
    }

    [Fact]
    public void Dispatch_payload_and_outcome_expose_explicit_transport_fields()
    {
        var payload = new DispatchPayload(new byte[] { 1, 2, 3 }, "application/json", "schema-1");
        var outcome = new DispatchOutcome(Succeeded: false, Retryable: true, Error: "retry");

        Assert.Equal("application/json", payload.ContentType);
        Assert.Equal("schema-1", payload.SchemaId);
        Assert.False(outcome.Succeeded);
        Assert.True(outcome.Retryable);
        Assert.Equal("retry", outcome.Error);
    }

    [Fact]
    public void Dispatch_message_lineage_fields_are_optional()
    {
        var payload = new DispatchPayload(new byte[] { 1 }, "application/json", "schema-1");
        var message = new DispatchMessage("msg-1", "idem-1", "EventType", "channel", "dest", payload);

        Assert.Null(message.CorrelationId);
        Assert.Null(message.CausationId);
        Assert.Null(message.InstanceId);
        Assert.Null(message.ParentInstanceId);
        Assert.Null(message.RootInstanceId);
        Assert.Null(message.ResumeTokenId);
    }

    [Fact]
    public void Dispatch_outcome_succeeded_false_retryable_false_represents_terminal_failure()
    {
        var outcome = new DispatchOutcome(Succeeded: false, Retryable: false, Error: "fatal error");

        Assert.False(outcome.Succeeded);
        Assert.False(outcome.Retryable);
        Assert.Equal("fatal error", outcome.Error);
    }
}
