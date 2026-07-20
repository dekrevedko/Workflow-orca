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
`OrcaCore.Dag` and Kubernetes/AWS/job-scheduler companions are separate outward projects and
are excluded from the `OrcaCore` application package dependency closure (PR-005). `OrcaCore`
is the primary contracts/authoring package, not a dependency-only meta-package.
Hosting uses the explicit engine/ingress/provider/DAG role registrations in PR-040; there is no
catch-all registration or implicit runtime-mode selection.

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
- Runtime-generated operation/protection identifiers are opaque immutable values with canonical
  parse/serialization paths and no author-selectable constructor or implicit string conversion.
- Caller-created idempotency/operation values such as `StopConfirmationId` and
  `ResourcePoolOperationId` are strongly typed, validated through their sole `Create(string)`
  factory, and never interchangeable with runtime-generated identities. Their constructors are
  private, and they expose no `New`, `Parse`/`TryParse`, implicit conversion, raw-string overload,
  or construction alias.

### NF-012 Test-first acceptance
Every **behavioral** requirement in documents 04–10 SHALL be verified by acceptance
criteria (document 12, including the scenario criteria of document 14 by reference) or by
the provider certification suite. **Structural and contract-shape** requirements — e.g.
contract shapes (CR-011, CR-015), pipeline structure (DU-011), port definitions
  (PR-010…016), API-surface rules (MG-011 and document 17) — are verified by unit tests in the
implementation program and exercised indirectly by many acceptance criteria; they do not
each carry a dedicated AC. The coverage classes are stated in document 12's preamble.
Acceptance tests are written against public surfaces, not internals. Provider invariants
ship as a reusable certification suite (PR-024).

## 11.3 Determinism and correctness

### NF-020 Deterministic core
Given identical inputs (definition version, state, command/event sequence), orchestration
decisions SHALL be deterministic (CR-012, DU-013). No wall-clock, randomness, or ambient
static state in the decision path; time enters through the timer/clock abstraction. One
identity/version SHALL bind one structural fingerprint. Changed authored structure conflicts at
registration; changing selector/projector/merge/output bodies, step configuration, DAG mapping
logic, external-request construction, or other opaque code requires a new version because v1
does not pretend to hash code.

### NF-021 Delivery guarantees are explicit
The official guarantees per mode: at-least-once outbox dispatch (DU-032); exactly-once
*committed effect* per instance via dedup + serialized commit (EV-031/032, DU-030); no
exactly-once *delivery* claim to external systems. Documentation SHALL state these plainly.

### NF-022 Orchestration policies are engine-owned
Workflow retry, per-attempt timeout, workflow deadline, replay, and terminal semantics SHALL be
implemented by the OrcaCore runtime over its deterministic clock and durable state. Polly or
another resilience library MAY be used inside application/provider calls, but SHALL NOT define
or replace orchestration semantics.

## 11.4 Performance and scalability posture

### NF-030 Performance philosophy
Semantic correctness precedes optimization; but designs SHALL avoid known scalability traps:
unbounded history loading (checkpoints, DU-010), fanout without batching/pagination paths,
per-query business-payload deserialization (projections), and hot-path payload predicate
evaluation.

### NF-031 Capacity controls
Host-owned execution-path/DAG-node limits, optional root-`ForEach` concurrency, ephemeral-only
transient pools and durable pools (MG-060…065), exact bounded retry policies (CR-006), and
active-instance caps (MG-051)
are the sanctioned load-shaping tools. Authors cannot raise host ceilings. Explicit performance
targets and benchmarks are a production-readiness-phase deliverable.

## 11.5 Security posture

### NF-040 Security baseline
- Payloads use fixed `orcacore-json-v1`: no dynamic type resolution from untrusted content,
  unsupported polymorphic/cyclic graphs reject at registration, and providers cannot replace
  codec policy (PR-016).
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
  (NF-021), lifecycle transition tables and terminal behavior (CR-030/031), structural
  fingerprint versus opaque-code version-bump rules (DU-040..043), typed workflow I/O and DAG mapping, stable
  step-operation identity, scoped lease quarantine/stop proof, and the authoring rules for
  steps (CR-012). Removed and deferred members SHALL not appear as usable v1 examples.
