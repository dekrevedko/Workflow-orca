# Durable Refactor / Cleanup Added Components

Saved on 2026-03-16.

## Scope

This is a review-oriented inventory of the main classes, records, enums, interfaces, and internal collaborators added during the durable refactor / cleanup phase.

It is not the full historical inventory of every durable foundation type ever introduced. It focuses on the components added as part of the review-remediation and API-cleanup work.

## Runtime facade and durable management

- `DurableWorkflowEngine`
- `DurableWorkflowEngine<TState>`
- `DurableWorkflowEngineOptions`
- `DurableInstanceScope`
- `DurableSelectionScope`
- `DurableInstanceSnapshot`

## Durable engine decomposition

- `DurableOutboxPump`
- `DurableEventRouter`
- `DurableDefinitionRegistry`
- `DurableRegisteredDefinition`
- `DurableInstanceManager`
- `DurableInstanceRegistry`
- `DurableInstanceRegistration`

## Durable authoring surface

- `DurableWorkflowBuilder<TState>`
- `DurableBranchBuilder<TState>`
- `DurableParallelBuilder<TState>`
- `DurableWorkflowDefinition<TState>`

## Outbox / dispatch / retry / poison handling

- `IOutboxDispatcher`
- `IOutboxPumpObserver`
- `IOutboxPumpDelayStrategy`
- `IOutboxPoisonHandler`
- `OutboxDispatchResult`
- `OutboxDispatchItemResult`
- `OutboxPumpCycleResult`
- `OutboxPumpDelayContext`
- `ExponentialOutboxPumpDelayStrategy`

## Fanout result contracts

- `FanoutDispatchResult`
- `FanoutInstanceResult`

## Correlation staging and routing support

- `ICorrelationMutationSink`
- `StagedCorrelationMutationSink`

## Durable payload registration and serialization safety

- `IDurablePayloadTypeResolver`
- `DurablePayloadTypeRegistry`
- `DurablePayloadSerializationException`
- `DurablePayloadDeserializationException`

## Retention / management policy

- `DurableArtifactRetentionPolicy`
- `DurableArtifactRetentionCutoffs`

## Query / expression cleanup

- `DefinitionScopedQueryBuilder`
- `DurableWorkflowQueryValidator`

## Exception hierarchy and explicit durable/routing exceptions

- `WorkflowEngineException`
- `WorkflowDefinitionException`
- `WorkflowStoreException`
- `WorkflowRoutingException`
- `DefinitionAlreadyRegisteredException`
- `DefinitionVersionMismatchException`
- `DurableDefinitionRehydrationException`
- `NoActiveWaitException`
- `AmbiguousCorrelationException`

## Wait semantics cleanup

- `WaitMode`

## Durable persistence contract additions used by the refactor

- `IWorkflowStore`
- `InMemoryWorkflowStore`
- `ConcurrencyException`
- `CorrelationLookupResult`
- `CorrelationMatch`
- `CorrelationMatchType`
- `PersistedFrameKind`

## Review notes

- Some of these types are public runtime surface.
- Some are internal collaborators introduced to break up `DurableWorkflowEngine`.
- Some are provider-facing or persistence-facing infrastructure rather than typical application-facing APIs.

## Likely follow-up review buckets

- Public runtime APIs:
  - `DurableWorkflowEngine`
  - `DurableInstanceScope`
  - `DurableSelectionScope`
  - retention-policy APIs
- Provider-facing APIs:
  - `IWorkflowStore`
  - `DurableArtifactRetentionCutoffs`
  - persistence DTO and correlation lookup contracts
- Internal collaborator quality:
  - `DurableEventRouter`
  - `DurableOutboxPump`
  - `DurableInstanceManager`
  - `DurableDefinitionRegistry`
