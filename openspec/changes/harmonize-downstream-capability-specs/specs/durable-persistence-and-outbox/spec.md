## ADDED Requirements

### Requirement: Durable payloads use the fixed certified codec
The durable system SHALL serialize workflow, event, and outbox payloads through the non-replaceable certified `System.Text.Json` format `orcacore-json-v1` so persisted data is restored deterministically. Providers SHALL persist and return codec-detached values and SHALL NOT substitute provider-specific ad hoc serialization. V1 SHALL NOT expose a serializer hook, replaceable payload-envelope abstraction, schema-resolution SPI, or hosting override for the codec. Unsupported cyclic or unapproved polymorphic shapes SHALL fail before commit.

#### Scenario: Persisted payload is read after restart
- **WHEN** the runtime rehydrates a durable instance or outbox record
- **THEN** payload materialization occurs through the certified `orcacore-json-v1` codec instead of provider-specific ad hoc logic

#### Scenario: Host attempts to replace serialization
- **WHEN** a host or provider attempts to register an alternative serializer, payload-envelope abstraction, or schema resolver
- **THEN** no such extension point exists and the guard suite rejects the addition

### Requirement: Retention and purge remain operator-tier
Retention and purge of inbox, outbox, and history artifacts SHALL remain provider and operator concerns that preserve live runtime correctness and documented terminal-instance inspection guarantees. They SHALL NOT appear as pause, resume, archive, purge, retention, or history members on the v1 ordinary application surface, which defers those operations.

#### Scenario: Old operational artifacts are purged
- **WHEN** operator-tier retention cleanup removes expired durable artifacts
- **THEN** active runtime correctness and documented inspection behavior remain intact

#### Scenario: Application looks for a purge command
- **WHEN** an application caller looks for purge, archive, or retention mutation on an instance handle
- **THEN** the member is absent and remains recorded in the future-capability registry

## MODIFIED Requirements

### Requirement: Workflow store persists first-class durable records
The durable system SHALL persist workflow instances, waits, inbox deduplication state, outbox state, history, and related queryable runtime records through an explicit `IWorkflowStore` boundary owned by `OrcaCore.Provider.Abstractions`. Durable resource-governance records SHALL use the separate expected-version `IDurableResourceGovernanceStore` append contract rather than being folded into this boundary. Neither store SHALL be reachable from ordinary application contracts.

#### Scenario: Durable state is committed
- **WHEN** a workflow transition is durably recorded
- **THEN** the store contract preserves the instance snapshot and its related operational records as first-class durable data

#### Scenario: Application code looks for the store
- **WHEN** an ordinary application consumer references `OrcaCore` and a hosting package
- **THEN** provider store contracts are absent from the application surface and remain provider-authoring concerns

## REMOVED Requirements

### Requirement: Durable payloads cross boundaries through explicit serializers
**Reason**: The approved v1 contract fixes serialization to the non-replaceable certified `orcacore-json-v1` codec and prohibits any ordinary hosting hook that replaces it. Requiring replaceable payload-envelope and schema-resolution abstractions contradicts that codec decision and the guard that rejects serializer hooks. Replaced by "Durable payloads use the fixed certified codec".

**Migration**: No released consumer migration exists. Providers persist and return codec-detached values produced by the fixed codec; types that require custom serialization are normalized into codec-supported shapes before commit rather than served by a custom serializer.

### Requirement: Retention and purge preserve runtime correctness
**Reason**: The requirement did not state its tier, so as written it promised operations the v1 application surface defers. The retention behavior itself is retained and re-scoped to the provider and operator tier. Replaced by "Retention and purge remain operator-tier".

**Migration**: No released consumer migration exists. Retention remains available to operators and providers; applications do not gain pause, resume, archive, purge, retention, or history members on instance handles in v1.
