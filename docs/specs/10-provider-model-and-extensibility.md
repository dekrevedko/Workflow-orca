# 10. Provider Model & Extensibility Requirements (PR)

Scope: the pluggable infrastructure boundary — what providers implement, what the engine
owns, and the invariants every provider must satisfy.

## 10.1 Principles

### PR-001 Interface-first extensibility
When a capability can reasonably vary by runtime, infrastructure, or integration boundary,
it SHALL be modeled behind a contract rather than a hard-coded implementation: persistence,
message dispatch, timer scheduling, serialization/schema resolution, outbox pump
observability and retry timing, and future step decorators and operational hooks. Concrete
internals are acceptable only where no meaningful extension boundary exists yet.

### PR-002 Engine owns semantics; providers supply capabilities
Product semantics (matching rules, dedup, serialization of execution, lifecycle) are
engine-owned and provider-neutral. Provider contracts expose the capabilities the engine
needs; broker/database specifics MUST NOT leak into workflow definitions or public
management semantics.

### PR-003 No hard infrastructure dependency
The library SHALL run with zero external infrastructure (in-memory providers) and SHALL
support relational databases, document databases, and message brokers (RabbitMQ, SQS,
Kafka, …) through adapters without changing workflow definitions.

## 10.2 Provider ports (durable engine)

The durable persistence boundary SHALL be decomposed into focused ports; a reference provider
MAY compose them behind one logical transaction boundary:

### PR-010 Event store port
Append events with expected stream version (optimistic concurrency); load stream tail after a
version; load/save checkpoints. Version-conflict outcomes are first-class results, not
generic exceptions.

### PR-011 Inbox store port
Record inbound deliveries by `EventId`; query by `EventId`; mark `Applied` /
`DuplicateIgnored` / `Poisoned` (DU-030).

### PR-012 Outbox store port
Append outbound records in the commit boundary; claim/lease for dispatch; mark dispatched /
failed / poisoned; support backlog inspection (DU-031..033).

### PR-013 Projection store port
Update and query read models: instance summaries, active waits, pending events, history,
saga compensation state — the backing for routing (EV-011) and management queries (MG-002,
DU-070).

### PR-014 Timer scheduler port
Schedule and cancel durable wake-ups that produce timer-fired commands after due time,
surviving restarts (EV-050).

### PR-015 Message dispatcher port
Transport adapter for outbox delivery: receives normalized dispatch messages, returns
explicit dispatch outcomes (success / retryable failure / permanent failure). The dispatch
pump SHALL expose observability and retry-delay strategy hooks (DU-032).

### PR-016 Serialization ports
Payload serialization, schema resolution, and envelope serialization SHALL be explicit seams
so payloads cross persistence/dispatch boundaries without provider-specific rules embedded in
step code.

## 10.3 Provider invariants (certification)

### PR-020 Atomic commit boundary
A provider SHALL commit, atomically or in a clearly defined transactional chain: appended
events, checkpoint update, inbox changes, outbox records, and projection updates (or
projection work scheduling) for one accepted mutation (DU-011).

### PR-021 Per-instance concurrency guarantee
Providers SHALL support the expected-version append (or equivalent) needed for CR-040/DU-022;
concurrent conflicting commits produce exactly one winner and a detectable conflict for the
loser.

### PR-022 Deletion/retention invariants
Deletion and purge honor DU-051: never remove active instances; never break in-flight
dispatch or lifecycle handling.

### PR-023 Queryability invariant
Runtime metadata remains queryable per DU-070 regardless of how the provider stores business
payloads.

### PR-024 Provider certification suite
The product SHALL ship a reusable, provider-agnostic invariant test suite (the acceptance
criteria in document 12 marked provider-sensitive) that any adapter must pass — compatibility
and contract tests are part of the core test surface, not left to adapter authors.

## 10.4 Ephemeral provider

### PR-030 In-memory baseline
The ephemeral engine's in-memory store is the reference implementation of instance storage
semantics (serialized execution, mailbox, correlation index) and SHALL pass every
non-durable acceptance criterion. In-memory implementations of the durable ports SHALL exist
for testing and as executable documentation of the contracts.

## 10.5 Hosting integration (later phase)

### PR-040 Host integration package
Host-level integration (`AddOrcaCore()`-style registration, hosted services for pumps/
schedulers, ASP.NET Core endpoint helpers, worker-queue hosting) SHALL be layered on top of
the library without becoming a dependency of the core: the engine remains usable without any
specific host framework.

## 10.6 Internal design conventions (extensibility-adjacent)

### PR-050 Explicit outcome primitives
Internally, the engine SHOULD use exactly three functional primitives — `Result<T>`
(expected success/failure at contract level: routing resolution, command decisions, commit
outcomes), `Option<T>` (present/absent lookups: checkpoints, waits, buffered events),
`Validation<T>` (accumulated authoring/build errors) — applied narrowly:

- public happy-path APIs MAY remain exception-based for ergonomics;
- no nesting like `Task<Result<Option<T>>>` without clear semantic need;
- `Validation<T>` only for build/config time, never runtime operational failures;
- no broader functional-abstraction stack.

Adoption order: builders (validation) → internal lookups (option) → command/routing/commit
paths (result).
