## MODIFIED Requirements

### Requirement: Provider invariants are explicit
The project SHALL verify that each supported storage, projection, and messaging adapter preserves its declared contract, exact ordinal strong-value equality, certified `orcacore-json-v1` payload detachment, structural fingerprints, complete structured envelopes, step-operation coordinates, deadline state, and one expected-version serialized resource-governance aggregate, or explicitly declares unsupported capabilities. Every shipped production durable provider SHALL pass the complete shared provider-certification suite plus provider-native restart, migration-journal, schema, retention, maintenance, poison, and pressure tests against real storage. PostgreSQL and SQL Server SHALL additionally certify concurrent competing hosts; SQLite SHALL certify rejection of a second live owner, sequential host replacement after graceful and abrupt termination, and concurrent same-host logical conflicts/fences. SQLite SHALL pass every logical invariant in its complete durable role; its single-owner deployment scope SHALL NOT be interpreted as permission to omit a port or feature. Governance certification SHALL exercise per-record format/checksum/payload-copy validation, whole-stream defensive copy and exact `1..Version` continuity through `ResourceGovernanceStream.Create`, and non-empty `expectedVersion + 1..N` whole-batch append with no partial commit.

Public-surface verification SHALL also prove that every caller-created string-backed strong value exposes only its private-constructor/`Create(string)` construction path and that runtime-created identities expose no public constructor or `Create` factory. Reflection, source, and clean-consumer guards SHALL reject the superseded `new EventName(...)`-style construction and any mixed family that offers both constructor and factory aliases.

#### Scenario: New provider adapter is introduced
- **WHEN** a new durable store, projection, or dispatcher is added
- **THEN** role-specific certification proves behavioral parity and documented capability limits without relying on provider-default collation or truncation

#### Scenario: Governance provider returns a malformed stream
- **WHEN** certification injects a negative/mismatched version, null entry, gap, duplicate, reordering, unsupported format, bad checksum, or mutation of the source list/payload after construction
- **THEN** load fails before replay or the accepted immutable value remains unchanged, and no aggregate transition is applied

#### Scenario: A SQLite certification case depends on concurrent server hosts
- **WHEN** the shared suite contains a live multi-host deployment scenario
- **THEN** the applicability record preserves PostgreSQL/SQL Server's original obligation and maps SQLite to owner rejection, sequential boots, or concurrent same-host logical owners as appropriate without silently skipping the logical invariant

#### Scenario: SQLite durability evidence is reviewed
- **WHEN** a real-file certification packet is proposed for approval
- **THEN** it identifies actual managed/native packages, required settings, crash/lock/disposal/failure evidence, native Windows/Linux execution, measured operating limits, and any still-pending DAG source/bridge work; passing tests alone do not approve the packet

### Requirement: Durable messaging is verified across every ownership boundary
Verification SHALL cover fixed-codec event descriptors, complete self-routing envelopes, the exact closed acceptance/rejection unions, `Accepted`/`Duplicate` broker-ack ownership, identity-before-target-state deduplication, direct-target/start-conflict/fanout-limit rejection without partial ownership, direct and correlation pending inboxes, atomic event/wait races, cold rehydration, callback-only start-intent handoff, exact start-or-deliver idempotency, committed provider-state fanout snapshots, per-target deduplication, durable publish/outbox atomicity, dispatcher retry/poison outcomes, and isolation of internal continuation records. In-memory development, PostgreSQL, SQL Server, and SQLite providers SHALL pass the same logical certification. PostgreSQL and SQL Server restart tests SHALL prove persistence and concurrent competing-host ownership; SQLite real-file tests SHALL prove persistence, sequential replacement, rejection of a second live owner, and same-host logical-owner fencing. No test SHALL treat a returned `NoActiveWait`, later broker redelivery, polling, or a hot in-memory instance as evidence for the durable contract.

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

#### Scenario: SQLite process dies after event acceptance
- **WHEN** a SQLite host is terminated after acknowledging acceptance and a fresh owning process later starts from the same file
- **THEN** the new host progresses the original envelope and retained ownership once logically without broker redelivery, while ambiguous external outbox delivery retains its stable EventId and at-least-once contract
