## ADDED Requirements

### Requirement: Public Interfaces are tiered by caller role
The system SHALL separate application, provider-authoring, runtime-protocol, and internal implementation Interfaces so a normal application dependency does not present provider commit records, kernel commands, checkpoints, driver pumps, or concrete hosted loops as ordinary application concepts.

#### Scenario: Application author references OrcaCore
- **WHEN** an application references only the documented application or hosting package
- **THEN** its public signatures and normal discovery surface contain application contracts and facades without requiring a provider-authoring or runtime-protocol package reference

#### Scenario: Provider author implements an Adapter
- **WHEN** a provider author references the documented provider-authoring package
- **THEN** the provider ports and certification contracts required at that seam are available with only the declared runtime-protocol dependency for persisted facts and checkpoints and without a dependency on an engine implementation

### Requirement: Durable application golden path is command-free
The durable application Interface SHALL support explicit host-scoped definition registration, typed idempotent start, event delivery, external-job completion/failure/timeout reporting, guaranteed continuation, management, and typed inspection without requiring the caller to construct or submit a durable protocol command.

#### Scenario: External job completes
- **WHEN** an application starts a durable definition that dispatches an external job and later reports its completion through the documented application Interface
- **THEN** the runtime owns serialization, command identity, time, deduplication, commit handling, and either local progression or an at-least-once continuation handoff

#### Scenario: Callback host has no registered definition
- **WHEN** a worker callback host reports an accepted external-job outcome without registering the workflow definition locally
- **THEN** it commits the outcome, returns `AppliedPendingContinuation`, and guarantees continuation delivery so a definition-owning host can progress the instance

#### Scenario: Application uses an advanced host
- **WHEN** a custom host intentionally needs raw command or event protocol access
- **THEN** it opts into the runtime-protocol Interface explicitly rather than receiving that Interface through the normal application facade

### Requirement: Hosting registration selects execution mode explicitly
Hosting registration SHALL require an explicit ephemeral, durable, or intentionally named combined/in-memory development choice and SHALL NOT silently register both engines and a durable provider through an ambiguous default method.

#### Scenario: Ephemeral host is configured
- **WHEN** a developer selects ephemeral hosting
- **THEN** durable engine, provider, command processor, and durable worker registrations are not added implicitly

#### Scenario: Durable host is configured
- **WHEN** a developer selects durable hosting
- **THEN** the host validates the required durable store and worker capabilities before accepting application work

#### Scenario: In-memory durable host is configured
- **WHEN** a developer selects `AddOrcaCoreInMemoryDurable`
- **THEN** hosting identifies it as development/test-only and emits a startup diagnostic that restart durability is not provided

### Requirement: Provider registration names the Adapter role
Every provider package SHALL expose registration that identifies whether the Adapter supplies a durable store, projection cache, dispatcher, or another documented role, and SHALL follow common validation, ownership, replacement, and diagnostics conventions within that role.

#### Scenario: Messaging dispatcher is selected
- **WHEN** a developer configures RabbitMQ or ZeroMQ for outbox delivery
- **THEN** both provider packages offer consistently named dispatcher registration and document ownership of supplied options and native clients

#### Scenario: Provider roles differ
- **WHEN** Redis supplies only a projection cache while PostgreSQL supplies durable stores
- **THEN** registration names and capability diagnostics preserve that distinction rather than implying that the two Adapters are interchangeable

### Requirement: Public types have a supported external scenario
A production type SHALL remain public only when an application, provider author, or custom host has a documented supported reason to construct, implement, derive from, or name it in a public signature.

#### Scenario: Hosting owns a background loop
- **WHEN** base hosting registers a concrete timer, outbox, continuation, or operational sweep implementation
- **THEN** the implementation type remains internal unless a supported external construction or replacement scenario is documented and tested

#### Scenario: Shallow profile is deleted
- **WHEN** deleting a public profile or one-value abstraction removes complexity without moving it into supported callers
- **THEN** the type is removed instead of retained as a public compatibility artifact

### Requirement: Optional integrations do not burden base hosting
Base hosting SHALL expose extension hooks for observability without taking dependencies on every optional exporter; exporter-specific dependencies SHALL be selected through focused integration packages or explicit application references.

#### Scenario: Host does not use OpenTelemetry exporters
- **WHEN** an application uses OrcaCore hosting without console, OTLP, or Prometheus export
- **THEN** those exporter packages are not required by the base hosting dependency graph

### Requirement: Package distribution preserves tier boundaries
The system SHALL publish separate application-contract, authoring/core, engine, hosting, provider-authoring, and runtime-protocol packages plus a small `OrcaCore` meta-package for the documented default application path.

#### Scenario: Application chooses the meta-package
- **WHEN** a developer references the `OrcaCore` meta-package for the documented default experience
- **THEN** the application golden path is available without exposing provider-authoring or runtime-protocol types in application signatures

#### Scenario: Advanced author chooses an explicit package
- **WHEN** a provider author or custom host needs an advanced seam
- **THEN** they reference its explicit package and do not receive an engine implementation dependency
