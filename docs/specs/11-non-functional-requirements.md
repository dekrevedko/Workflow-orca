# 11. Non-Functional Requirements (NF)

## 11.1 Platform and packaging

### NF-001 Target platform
The library SHALL target modern .NET (baseline: .NET 10). All project scaffolding uses the
`dotnet` CLI.

### NF-002 Embeddable library
OrcaCore is a set of NuGet-shaped class libraries embedded in a host application: no required
server, sidecar, or external platform. Minimum footprint: an abstractions/contracts package
(referenced by user workflow code) and a runtime package; providers and hosting integration
ship separately as they appear. Runtime depends on abstractions, never the reverse.

### NF-003 Versioning and stability posture
Until the runtime model stabilizes, the project is explicitly pre-production: APIs and
persistence shapes may change, and this SHALL be stated. Published packages with semantic
versioning and a breaking-change policy are a deliberate later-phase gate (see document 13),
not an initial promise.

## 11.2 Code quality baseline

### NF-010 Compiler discipline
Nullable reference types enabled; warnings as errors; immutable contracts where practical
(records for results, envelopes, snapshots, definitions).

### NF-011 API discipline
- All potentially work-performing public APIs async with `CancellationToken` (CR-013).
- No live mutable internals exposed (CR-021, MG-005).
- Closed result hierarchies (sealed variants) so the runtime handles every case exhaustively.
- Public API remains idiomatic .NET; internal functional primitives per PR-050.

### NF-012 Test-first acceptance
Every requirement in documents 04–10 maps to acceptance criteria (document 12); acceptance
tests are written against public surfaces, not internals. Provider invariants ship as a
reusable certification suite (PR-024).

## 11.3 Determinism and correctness

### NF-020 Deterministic core
Given identical inputs (definition version, state, command/event sequence), orchestration
decisions SHALL be deterministic (CR-012, DU-013). No wall-clock, randomness, or ambient
static state in the decision path; time enters through the timer/clock abstraction.

### NF-021 Delivery guarantees are explicit
The official guarantees per mode: at-least-once outbox dispatch (DU-032); exactly-once
*committed effect* per instance via dedup + serialized commit (EV-031/032, DU-030); no
exactly-once *delivery* claim to external systems. Documentation SHALL state these plainly.

## 11.4 Performance and scalability posture

### NF-030 Performance philosophy
Semantic correctness precedes optimization; but designs SHALL avoid known scalability traps:
unbounded history loading (checkpoints, DU-010), fanout without batching/pagination paths,
per-query business-payload deserialization (projections), and hot-path payload predicate
evaluation.

### NF-031 Capacity controls
Concurrency limits and named pools (MG-060/061), bounded retry policies (CR-006), and
active-instance caps (MG-051) are the sanctioned load-shaping tools. Explicit performance
targets and benchmarks are a production-readiness-phase deliverable.

## 11.5 Security posture

### NF-040 Security baseline
- Payloads are opaque data: no dynamic type resolution from untrusted payload content during
  deserialization (serializer contracts are explicit, PR-016).
- Management commands are host-mediated: the library exposes no network surface itself;
  authentication/authorization of operator actions belongs to the host, and destructive
  breadth requires explicit safety semantics (MG-004).
- A dedicated security review is a production-readiness gate (document 13).

## 11.6 Documentation obligations

### NF-050 Contract documentation
The product SHALL document, as user-facing contract (not internals): the feature matrix per
axis combination (DU-002), wait/timer wake-up semantics (EV-042), the **transient timer vs
durable timer (reminder-style wake-up) split** — when to use each, what each survives, and
worked usage examples (EV-050; the Orleans timer/reminder analogy), delivery guarantees
(NF-021), lifecycle transition tables and terminal behavior (CR-030/031), versioning and
deployment rules (DU-040..042), and the authoring rules for steps (CR-012) and compensation
idempotency (SG-012).
