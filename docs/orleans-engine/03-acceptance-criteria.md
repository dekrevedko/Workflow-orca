# 03. Orleans Engine Acceptance Criteria (OE-AC)

Format: Given/When/Then, one observable behavior each. Tests carry
`[Trait("AC", "OE-AC-xxx")]`. Unless stated, the fixture is a TestingHost cluster with
in-memory providers.

## Basic execution through grains

### OE-AC-001 Start commits through the grain
Given a registered durable definition
When `OrleansWorkflowEngine.StartOrGetAsync` is called
Then the instance grain processes a durable advancement segment containing the start commit,
and the event store contains the `Started`/version-binding facts with the projection summary
visible (OE-010, OE-011, DU-040).

### OE-AC-002 Wait suspends the turn; event resumes
Given a workflow that runs a step then `Wait`s for a correlated event
When started
Then the start call returns with status Waiting (no held grain call), every pre-wait step is
committed at a durable safe boundary, and a subsequent `RaiseEventAsync` with the matching
correlation resumes and completes the instance (OE-011, OE-030).

### OE-AC-003 Query without activation
Given a waiting instance whose activation was deactivated
When management queries run (status, active waits)
Then results come from projections and the grain activation count does not increase (OE-052).

## Idempotency and concurrency

### OE-AC-010 Duplicate event ignored
Given a completed delivery of `EventId` E
When E is delivered again (same or new activation, after restart)
Then the outcome is `DuplicateIgnored` with no second committed transition (OE-041).

### OE-AC-011 Racing deliveries: one winner per version
Given two concurrent conflicting deliveries for one instance
When both are processed
Then commits form a single serial order; any loser surfaces a version conflict, never a
partial or doubled mutation (OE-013, OE-021).

### OE-AC-012 Duplicate activation defense
Given two activations of the same instance forced to append concurrently (simulated via
direct processor calls bypassing grain routing)
When both attempt expected-version append
Then exactly one wins; the other observes the conflict result (OE-021).

### OE-AC-013 StartOrGet is cluster-safe under races and retries
Given N concurrent `StartOrGetAsync` calls with one idempotency key issued through
different clients/silos, plus a retry whose original call result was lost
When all calls complete
Then every caller receives the same `InstanceId`, exactly one instance and one stream
exist, and the idempotency store holds exactly one reservation (OE-042, OE-015).

## Lifecycle and restart

### OE-AC-020 Idle deactivation, then resume
Given a waiting instance
When its activation is deactivated (idle collection / explicit `DeactivateOnIdle`)
and the awaited event later arrives
Then a fresh activation rehydrates from checkpoint + tail and completes correctly (OE-022, OE-050, OE-051, OR-001).

### OE-AC-021 Silo restart preserves cold waits
Given a `WaitLong` instance on a 2-silo cluster
When the hosting silo is stopped and the event is delivered afterwards
Then the instance resumes on a surviving/new silo with full continuity (OE-032).

### OE-AC-022 Timer fires late-but-once after full downtime
Given a scheduled durable timer and a full cluster stop past the due time
When the cluster restarts and the timer pump runs
Then the timer fires exactly once, late, and the timeout branch executes (OE-031, OE-032).

### OE-AC-023 Claimed-but-undelivered timer still fires
Given a due timer whose claim was taken but whose grain delivery failed (simulated pump
crash / call timeout before commit)
When the recovery mechanism runs (reclaim after lease expiry, or pump retry)
Then the timer's committed outcome occurs exactly once — never zero times (lost) and
never two committed fires (OE-033, OE-015).

## Distribution

### OE-AC-030 Cross-silo routing
Given instances placed across a 2-silo cluster
When correlated events are raised through a client attached to either silo
Then every event reaches its instance regardless of placement (OE-040, OE-010).

### OE-AC-031 Outbox dispatch from silos
Given a workflow publishing an outbound message
When the commit completes
Then the outbox record is dispatched at-least-once by a silo-hosted pump and marked
dispatched (DU-031/032 unchanged under Orleans hosting).

## Composition, contracts, and observability

### OE-AC-040 Orleans dependency and persistence isolation
Given the Orleans engine project and test projects exist
When repository guards scan project references, source attributes, and grain persistence usage
Then `Microsoft.Orleans.*` references and Orleans serializer attributes appear only in the
engine/test whitelist, existing projects have no Orleans dependency, and workflow truth is
not stored through `IPersistentState` or `JournaledGrain` (OE-001, OE-003, OE-020).

### OE-AC-041 Transport envelopes are version tolerant
Given v1 command/result envelopes
When command/result payloads round-trip through `WorkflowCommandCodec` and the Orleans
serializer, including unknown-kind, newer-schema-version, and old-shape payload cases
Then supported shapes decode losslessly, unsupported shapes fail fast with diagnostics, and
no domain type requires Orleans attributes (OE-060, OE-002).

### OE-AC-042 Composition starts through one seam
Given a TestingHost silo configured with provider ports and `UseOrcaCoreOrleans`
When the silo starts and resolves engine services
Then the current-phase engine services are registered from the single composition seam
(`DurableCommandProcessor` and options first, facade and pumps as their tasks land);
missing required ports fail fast; non-PostgreSQL behavior tests use TestingHost/in-memory
providers (OE-070, OE-080).

### OE-AC-043 Facade mirrors durable operation surface
Given the durable runtime public operation surface
When the Orleans facade is inspected and exercised for start, event delivery, and management
operations as they become available
Then methods use durable result types and durable semantics, with no Orleans-only operation
and no combined delivery surface that merges correlation-targeted delivery with fanout
(OE-071, OE-002).

### OE-AC-044 Cancellation never creates partial mutation
Given a grain operation with a pre-canceled token or a token canceled during provider work
When the operation is invoked through the Orleans facade or grain
Then the token is observed through codec, processor, provider calls, and pump delivery; the
result is either no committed mutation or one complete durable commit boundary, never a
partially observable mutation (OE-014).

### OE-AC-045 Pumps follow silo lifecycle
Given timer and outbox pumps registered in a multi-silo cluster
When the silo lifecycle starts, reaches `ServiceLifecycleStage.Active`, and then shuts down
Then pumps do not claim or deliver before Active, stop through the silo lifecycle, and are
not registered as plain `BackgroundService`s (OE-072).

### OE-AC-046 Turn budget and diagnostics are observable
Given representative successful, version-conflict, timer-pump, and over-budget grain turns
When the operations run under a metrics/activity listener
Then per-turn outcome, duration, activation, timer-pump, and version-conflict diagnostics
are emitted, the turn-budget warning fires at the configured threshold, a hard segment
budget yields by leaving a durable continuation when the instance remains runnable, and the
Orleans response timeout is not raised to hide slow inline steps (OE-012, OE-053).

## Load and pressure

### OE-AC-060 Per-turn I/O stays bounded under sustained load
Given a sustained mixed workload (starts, deliveries, timer fires) against a 2-silo
cluster with PostgreSQL providers
When the run completes
Then per-turn latency stays within the documented budget, stream-tail length stays bounded
by checkpoint cadence, and provider I/O metrics show no unbounded growth (OE-023;
feeds the OOQ-6 decision).

## Capstone e2e

### OE-AC-050 Driving scenario, in-memory cluster
Given the customer-approval workflow (request step → `WaitLong` for approval with timer
timeout → branch on outcome → publish result), on a 2-silo TestingHost cluster
When the scenario runs three ways — (a) approval arrives, (b) timeout fires, (c) approval
arrives after a silo restart mid-wait
Then each path completes with the correct branch, exactly-once committed outcomes, correct
history projection, and dispatched outbox messages (OE-082).

### OE-AC-051 Driving scenario, PostgreSQL-backed
Given the same scenario with PostgreSQL providers (Testcontainers) and production-shaped
clustering (OOQ-4 resolution)
When path (c) — restart mid-wait — runs
Then the instance survives silo loss with all OE-AC-050 assertions holding against the
real database (OE-082).

### OE-AC-052 Durable acceptance parity subset
Given the engine-observable durable acceptance criteria selected by OT5-01
When the same expectations run against the Orleans engine fixtures
Then the subset passes without semantic deltas; any excluded durable AC has an explicit
reason tied to infrastructure, not behavior divergence (OE-002, OE-081).
