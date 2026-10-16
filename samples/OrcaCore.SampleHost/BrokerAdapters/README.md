# Application-owned broker adapters

[`BrokerAdapterExamples.cs`](BrokerAdapterExamples.cs) demonstrates the transport boundary for
MassTransit, Rebus, and SNS/SQS without adding any broker SDK dependency to OrcaCore. The example
project references only the OrcaCore application/hosting packages; a real application adds its
selected SDK and supplies the publish delegate:

- MassTransit maps the delegate to `IPublishEndpoint.Publish` and completes the consume context only
  for `BrokerReceiveDisposition.Acknowledge`.
- Rebus maps it to `IBus.Send` or `IBus.Publish`; an exception escapes the handler so Rebus applies
  its configured retry policy.
- SNS/SQS maps outbound contract name/version to an SNS topic and deletes an SQS message only after
  `Accepted` or `Duplicate` becomes `Acknowledge`.

The application delegate switches on the stable `WorkflowEventContract` and calls
`outboundEvent.GetPayload(theTypedContract)` before invoking its SDK. It returns
`BrokerPublishOutcome.Published`, `Retryable`, or `Permanent`; the adapter maps those values to the
closed OrcaCore dispatch result. An exception is intentionally allowed to escape so the durable
outbox retains the record for retry.

For inbound delivery, the broker consumer constructs a complete self-routing
`WorkflowInboundEvent` or `WorkflowInboundEvent<TPayload>` and calls the style-specific
`ConsumeAsync`, `HandleAsync`, or `ReceiveAsync` method. Only `Accepted` and `Duplicate` acknowledge
durable ownership. A closed semantic rejection is dead-lettered by this example; infrastructure,
serialization, and cancellation failures remain exceptional and therefore broker-retryable.
