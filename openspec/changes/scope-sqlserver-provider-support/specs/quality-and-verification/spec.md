## MODIFIED Requirements

### Requirement: Provider invariants are explicit
The project SHALL verify that each supported storage, projection, and messaging adapter preserves its declared contract, exact ordinal strong-value equality, certified `orcacore-json-v1` payload detachment, structural fingerprints, complete structured envelopes, step-operation coordinates, deadline state, and one expected-version serialized resource-governance aggregate, or explicitly declares unsupported capabilities. Both shipped relational providers SHALL pass the complete shared provider-certification suite plus provider-native restart, competing-host, migration-journal, schema, retention, maintenance, poison, and pressure tests against real storage. Governance certification SHALL verify one consistent latest-record/version/partition view for current-state recovery, empty and damaged heads, defensive payload ownership, same-state recovery on valid histories, and contended append retries. Experimental SQL Server certification SHALL distinguish a legacy absent version from a malformed explicit version and verify that a legacy read performs no write. A coherently shortened legacy history does not establish corruption by itself. It SHALL independently exercise explicit full-history `LoadAsync`, per-record format/checksum/payload-copy validation, whole-stream defensive copy and exact `1..Version` continuity through `ResourceGovernanceStream.Create`, and non-empty `expectedVersion + 1..N` whole-batch append with no partial commit.

Public-surface verification SHALL also prove that every caller-created string-backed strong value exposes only its private-constructor/`Create(string)` construction path and that runtime-created identities expose no public constructor or `Create` factory. Reflection, source, and clean-consumer guards SHALL reject the superseded `new EventName(...)`-style construction and any mixed family that offers both constructor and factory aliases.

#### Scenario: New provider adapter is introduced
- **WHEN** a new durable store, projection, or dispatcher is added
- **THEN** role-specific certification proves behavioral parity and documented capability limits without relying on provider-default collation or truncation

#### Scenario: Explicit governance history contains a malformed stream
- **WHEN** certification injects a negative/mismatched version, null entry, gap, duplicate, reordering, unsupported format, bad checksum, or mutation of the source list/payload after construction
- **THEN** explicit history validation fails or the accepted immutable value remains unchanged, and no aggregate transition is applied

#### Scenario: Current recovery is independent of prior state records
- **WHEN** equal valid current snapshots follow different lengths of retained history
- **THEN** the engine deserializes and validates one aggregate state per nonempty load and recovers the same ownership, queue order, management and idempotency results as full replay

#### Scenario: Measure provider costs separately
- **WHEN** certification varies history length at fixed current state and separately grows retained current identities
- **THEN** it records returned records and bytes, physical provider work, latency and allocation; PostgreSQL reads the committed head and latest record in one indexed statement without scanning historical payloads, and experimental SQL Server reports its full-document I/O separately

### Requirement: Package consumer smoke tests guard dependency experience
Verification SHALL pack and restore clean consumers for minimal ephemeral, PostgreSQL-backed durable, SQL Server-backed durable, callback-only ingress through each shipped relational provider, application-supplied event dispatcher, DAG, provider-authoring, runtime-protocol custom-host, and optional companion integration paths.

#### Scenario: Minimal consumer builds from packages
- **WHEN** a clean project restores the documented application packages
- **THEN** the golden path compiles/runs without source-project references, advanced leaks, or optional infrastructure dependencies

### Requirement: Durable messaging is verified across every ownership boundary
Verification SHALL cover fixed-codec event descriptors, complete self-routing envelopes, the exact closed acceptance/rejection unions, `Accepted`/`Duplicate` broker-ack ownership, identity-before-target-state deduplication, direct-target/start-conflict/fanout-limit rejection without partial ownership, direct and correlation pending inboxes, atomic event/wait races, cold rehydration, callback-only start-intent handoff, exact start-or-deliver idempotency, committed provider-state fanout snapshots, per-target deduplication, durable publish/outbox atomicity, dispatcher retry/poison outcomes, and isolation of internal continuation records. In-memory development, PostgreSQL, and SQL Server providers SHALL pass the same logical certification; both relational providers' restart tests SHALL prove persistence and competing-host ownership. No test SHALL treat a returned `NoActiveWait`, later broker redelivery, polling, or a hot in-memory instance as evidence for the durable contract.

#### Scenario: Event arrives before matching wait
- **WHEN** an accepted event survives host replacement before its wait registers
- **THEN** the later wait claims it once without broker redelivery and the exported API contains no durable `NoActiveWait`

#### Scenario: Fanout host fails during materialization
- **WHEN** a crash occurs before or after the fanout target snapshot/per-target inbox commit
- **THEN** recovery observes either no accepted fanout or the same complete target set, never a partial or rediscovered set

#### Scenario: Outbox dispatch is ambiguous
- **WHEN** a dispatcher sends successfully and the host fails before marking the record dispatched
- **THEN** retry uses the same outbound `EventId`, and the dispatcher still receives no provider record or internal continuation kind

#### Scenario: Definition-less ingress package is inspected
- **WHEN** the callback-only host accepts and persists a self-routing event
- **THEN** package/runtime guards prove it has no workflow catalog, handle lookup, definition execution, external outbox dispatcher, or broker SDK dependency
