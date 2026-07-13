using System.Text.Json;
using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Abstractions.Serialization;

// Metadata-only generation: the .NET 10 fast path serializes a null byte[] property as ""
// instead of null, so a null payload comes back as an empty one after any store round-trip
// (observed on WorkflowStartedEvent.InputPayload / buffered-delivery payloads). The metadata
// path keeps null fidelity; do not re-enable the fast path without a null byte[] round-trip test.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WorkflowStartedEvent))]
[JsonSerializable(typeof(WorkflowContinuedAsNewEvent))]
[JsonSerializable(typeof(WorkflowStepCompletedEvent))]
[JsonSerializable(typeof(WorkflowStepFailedEvent))]
[JsonSerializable(typeof(WorkflowWaitRegisteredEvent))]
[JsonSerializable(typeof(WorkflowWaitMatchedEvent))]
[JsonSerializable(typeof(WorkflowWaitCancelledEvent))]
[JsonSerializable(typeof(WorkflowTimerCancelledEvent))]
[JsonSerializable(typeof(WorkflowResumeConsumedEvent))]
[JsonSerializable(typeof(WorkflowParkedEvent))]
[JsonSerializable(typeof(WorkflowUnparkedEvent))]
[JsonSerializable(typeof(WorkflowContinuationAttemptFailedEvent))]
[JsonSerializable(typeof(WorkflowContinuationAttemptResetEvent))]
[JsonSerializable(typeof(WorkflowTimerScheduledEvent))]
[JsonSerializable(typeof(WorkflowTimerFiredEvent))]
[JsonSerializable(typeof(WorkflowChildScheduledEvent))]
[JsonSerializable(typeof(WorkflowChildrenScheduledEvent))]
[JsonSerializable(typeof(WorkflowChildrenDispatchedEvent))]
[JsonSerializable(typeof(WorkflowChildCompletedEvent))]
[JsonSerializable(typeof(WorkflowParentResumeTokenRecordedEvent))]
[JsonSerializable(typeof(WorkflowParentResumeTokenConsumedEvent))]
[JsonSerializable(typeof(WorkflowChildResidualIntentRecordedEvent))]
[JsonSerializable(typeof(WorkflowChildCompensationScheduledEvent))]
[JsonSerializable(typeof(WorkflowResourcePoolAcquiredEvent))]
[JsonSerializable(typeof(WorkflowResourcePoolQueuedEvent))]
[JsonSerializable(typeof(WorkflowResourcePoolReleasedEvent))]
[JsonSerializable(typeof(WorkflowExternalJobStartedEvent))]
[JsonSerializable(typeof(WorkflowExternalJobCompletedEvent))]
[JsonSerializable(typeof(WorkflowExternalJobTimedOutEvent))]
[JsonSerializable(typeof(WorkflowExternalJobStopRequestedEvent))]
[JsonSerializable(typeof(WorkflowTimerBufferedEvent))]
[JsonSerializable(typeof(WorkflowPausedEvent))]
[JsonSerializable(typeof(WorkflowResumedEvent))]
[JsonSerializable(typeof(WorkflowDeliveryBufferedEvent))]
[JsonSerializable(typeof(WorkflowDeliveryDiscardedEvent))]
[JsonSerializable(typeof(WorkflowCompletedEvent))]
[JsonSerializable(typeof(WorkflowTerminalEvent))]
[JsonSerializable(typeof(SagaForwardActionCompletedEvent))]
[JsonSerializable(typeof(SagaForwardActionTimedOutEvent))]
[JsonSerializable(typeof(SagaCompensationRequestedEvent))]
[JsonSerializable(typeof(SagaCompensationStartedEvent))]
[JsonSerializable(typeof(SagaCompensationCompletedEvent))]
[JsonSerializable(typeof(SagaCompensationFailedEvent))]
[JsonSerializable(typeof(SagaManualRecoveryRecordedEvent))]
[JsonSerializable(typeof(WorkflowRuntimeCheckpointState))]
[JsonSerializable(typeof(DurableExecutionEnvelope))]
[JsonSerializable(typeof(DurableContinuationSignal))]
public sealed partial class OrcaCoreJsonSerializerContext : JsonSerializerContext;
