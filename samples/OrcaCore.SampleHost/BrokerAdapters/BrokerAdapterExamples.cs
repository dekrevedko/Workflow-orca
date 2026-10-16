using OrcaCore.Durable.Hosting;

namespace OrcaCore.SampleHost.BrokerAdapters;

/// <summary>
/// Describes the application-owned outcome of one broker SDK publish call.
/// </summary>
public abstract record BrokerPublishOutcome
{
    private protected BrokerPublishOutcome()
    {
    }

    public sealed record Published : BrokerPublishOutcome;

    public sealed record Retryable : BrokerPublishOutcome
    {
        private string code = null!;

        public Retryable(string code, string? detail = null)
        {
            Code = code;
            Detail = detail;
        }

        public string Code
        {
            get => code;
            init => code = ValidateFailureCode(value);
        }

        public string? Detail { get; init; }
    }

    public sealed record Permanent : BrokerPublishOutcome
    {
        private string code = null!;

        public Permanent(string code, string? detail = null)
        {
            Code = code;
            Detail = detail;
        }

        public string Code
        {
            get => code;
            init => code = ValidateFailureCode(value);
        }

        public string? Detail { get; init; }
    }

    private static string ValidateFailureCode(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A broker failure code cannot be blank.", nameof(value))
            : value;
}

/// <summary>
/// Tells the application-owned broker consumer how to settle one inbound delivery.
/// Infrastructure exceptions are deliberately not represented: they escape to the broker SDK's
/// retry policy.
/// </summary>
public enum BrokerReceiveDisposition
{
    Acknowledge,
    DeadLetter
}

/// <summary>
/// MassTransit-style adapter: the application supplies its <c>IPublishEndpoint</c> call through
/// <paramref name="publishAsync"/> and completes/faults the consume context from the returned
/// disposition.
/// </summary>
public sealed class MassTransitStyleWorkflowEventAdapter(
    IWorkflowEventIngress ingress,
    Func<WorkflowOutboundEvent, CancellationToken, ValueTask<BrokerPublishOutcome>> publishAsync)
    : WorkflowBrokerAdapter(ingress, publishAsync)
{
    public ValueTask<BrokerReceiveDisposition> ConsumeAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptAsync(inboundEvent, cancellationToken);

    public ValueTask<BrokerReceiveDisposition> ConsumeAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptAsync(inboundEvent, cancellationToken);
}

/// <summary>
/// Rebus-style adapter: the application supplies its <c>IBus.Send</c>/<c>Publish</c> call and lets
/// an ingress exception fail the handler so Rebus performs its configured retry behavior.
/// </summary>
public sealed class RebusStyleWorkflowEventAdapter(
    IWorkflowEventIngress ingress,
    Func<WorkflowOutboundEvent, CancellationToken, ValueTask<BrokerPublishOutcome>> publishAsync)
    : WorkflowBrokerAdapter(ingress, publishAsync)
{
    public ValueTask<BrokerReceiveDisposition> HandleAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptAsync(inboundEvent, cancellationToken);

    public ValueTask<BrokerReceiveDisposition> HandleAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptAsync(inboundEvent, cancellationToken);
}

/// <summary>
/// SNS/SQS-style adapter: the application maps outbound contracts to SNS topics and deletes an SQS
/// message only for <see cref="BrokerReceiveDisposition.Acknowledge"/>.
/// </summary>
public sealed class SnsSqsStyleWorkflowEventAdapter(
    IWorkflowEventIngress ingress,
    Func<WorkflowOutboundEvent, CancellationToken, ValueTask<BrokerPublishOutcome>> publishAsync)
    : WorkflowBrokerAdapter(ingress, publishAsync)
{
    public ValueTask<BrokerReceiveDisposition> ReceiveAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptAsync(inboundEvent, cancellationToken);

    public ValueTask<BrokerReceiveDisposition> ReceiveAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptAsync(inboundEvent, cancellationToken);
}

/// <summary>
/// Keeps the broker-neutral OrcaCore result mapping identical across the three application-owned
/// adapter styles while leaving every broker SDK type outside OrcaCore packages.
/// </summary>
public abstract class WorkflowBrokerAdapter : IWorkflowEventDispatcher
{
    private readonly IWorkflowEventIngress ingress;
    private readonly Func<WorkflowOutboundEvent, CancellationToken, ValueTask<BrokerPublishOutcome>> publishAsync;

    private protected WorkflowBrokerAdapter(
        IWorkflowEventIngress ingress,
        Func<WorkflowOutboundEvent, CancellationToken, ValueTask<BrokerPublishOutcome>> publishAsync)
    {
        this.ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
        this.publishAsync = publishAsync ?? throw new ArgumentNullException(nameof(publishAsync));
    }

    public async ValueTask<WorkflowEventDispatchResult> DispatchAsync(
        WorkflowOutboundEvent outboundEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outboundEvent);
        return await publishAsync(outboundEvent, cancellationToken).ConfigureAwait(false) switch
        {
            BrokerPublishOutcome.Published => new WorkflowEventDispatchResult.Succeeded(),
            BrokerPublishOutcome.Retryable retryable =>
                new WorkflowEventDispatchResult.RetryableFailure(
                    WorkflowEventDispatchFailure.Create(retryable.Code, retryable.Detail)),
            BrokerPublishOutcome.Permanent permanent =>
                new WorkflowEventDispatchResult.PermanentFailure(
                    WorkflowEventDispatchFailure.Create(permanent.Code, permanent.Detail)),
            _ => throw new InvalidOperationException("The broker publisher returned an unknown outcome.")
        };
    }

    protected async ValueTask<BrokerReceiveDisposition> AcceptAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);
        return MapAcceptance(await ingress.AcceptAsync(inboundEvent, cancellationToken).ConfigureAwait(false));
    }

    protected async ValueTask<BrokerReceiveDisposition> AcceptAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);
        return MapAcceptance(await ingress.AcceptAsync(inboundEvent, cancellationToken).ConfigureAwait(false));
    }

    private static BrokerReceiveDisposition MapAcceptance(WorkflowEventAcceptanceResult result) => result switch
    {
        WorkflowEventAcceptanceResult.Accepted => BrokerReceiveDisposition.Acknowledge,
        WorkflowEventAcceptanceResult.Duplicate => BrokerReceiveDisposition.Acknowledge,
        WorkflowEventAcceptanceResult.Rejected => BrokerReceiveDisposition.DeadLetter,
        _ => throw new InvalidOperationException("The workflow event ingress returned an unknown result.")
    };
}
