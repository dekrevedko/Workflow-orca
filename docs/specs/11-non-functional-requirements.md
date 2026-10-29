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
durable resident-activation caps (MG-051)
are the sanctioned load-shaping tools. A residency cap is not an independent host-wide
workflow-instance/advancement admission ceiling. Ephemeral `MaxRetainedInstances` (MG-054)
bounds only eligible settled-terminal data; active/unsettled/pinned work, caller-held values,
registered definitions and payload byte size can exceed that data-count budget. It never frees
physical capacity or proves external work stopped. Authors cannot raise host ceilings. Explicit performance
targets and benchmarks are a production-readiness-phase deliverable.

### NF-032 Shutdown budgets preserve real ownership
An observer timeout SHALL bound observation, not release physical capacity or DI
ownership. Blocked noncooperative attempts/callbacks remain owned across Incomplete;
the application SHALL serialize disposal and retain its real provider/scopes until
Completed. Closure cannot promise effect rollback or durable restart. Registered
monotonic TimeProvider governs pending finite observations; no hard real-time,
forced-kill, thread-abort or byte/RSS bound is implied. Cached Completed SHALL avoid
TimeProvider/user work. Generic Host drain/stop shares one host shutdown budget;
plain-DI drain uses the application submission/ownership protocol. [MG-067–069, PR-041]

### NF-033 Native publishing assessment is profile-specific research
The optional public ephemeral assessment SHALL use one external PackageReference-only
consumer on native Windows Server 2025 x64 and Ubuntu 24.04 LTS x64, each published
as Release self-contained untrimmed JIT, fully trimmed JIT and Native AOT. Native VMs
are allowed; Docker, WSL and cross-publish alone do not qualify. Record exact package,
SDK/runtime/toolchain, source/input identity and managed/native dependency closure.
The six base cells leave JsonSerializerIsReflectionEnabledByDefault unset and execute
before four separately identified trimmed/AOT extra cells explicitly setting it true.
Record each default's effective switch, actual public failure phase/code, exact
exception/inner messages and diagnostics. A metadata rejection must not be renamed.
Extra cells are at most AssessedConditional and never replace a default failure.
Every compiler/trimmer/AOT/native finding SHALL be individually Corrected, Unsupported
or a proven bounded LocalRequirement; retain detailed diagnostics/counts and reject
unknown or changed findings. No blanket suppression, private rooting, whole-product
root, serializer hook, product annotation/API/dependency change or compatibility badge.
The full AOT-01–16 public catalogue SHALL execute, with barriers and truthful physical
ownership, in both untrimmed JIT controls; other cells retain explicit causal failures
and blocked cases. A complete record can conclude unsupported, never invent support.
Missing native hosts or a broken JIT control prevent completion. Executable cells use
three retained warm-ups and twenty recorded interleaved runs of an identical workload,
with launch-to-ready, cumulative user+kernel CPU, peak/quiescent process memory and
separately named no-forced-GC managed estimates/last-collection heap observations.
Keep platform memory meanings, units/phases, failures and unavailable values explicit;
no zero placeholders, cold-cache claim, cross-machine ranking or performance ceiling.
Independent final assessment review precedes publishing any profile-specific conclusion.
See quality-and-verification and the issue #3 task-2 protocol; fixed codec and DI
ownership remain governed by PR-016/041 and NF-032, without product changes.

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

## NF-034 Ephemeral native support has an exact qualified boundary

The selected ephemeral path SHALL qualify .NET 10 self-contained untrimmed JIT,
full-trim JIT and Native AOT on native windows-2025/win-x64 and Ubuntu 24.04/linux-x64.
Trimmed/AOT artifacts SHALL retain and assert SDK-default disabled reflection JSON.
Every original AOT-01–16 assertion plus NS-01–12 and mandatory NS-02a–c/NS-06a–f
SHALL pass in each applicable cell. A successful publish, skipped case or
Unsupported classification SHALL NOT complete this support issue. Legacy JIT
remains a separate control. NF-033's original assessment remains historical.

The exact one-context process seal, static bootstrap and ownership rules SHALL
be enforced without replacing the fixed codec. Generated mode's explicit .NET 10
omittable synthetic-initializer restriction SHALL reject declared closures with
SFE-TYPE-002 before effects, for omitted and present values; SFE-TYPE-003 SHALL
identify missing metadata. Common admitted shapes SHALL retain exact bytes,
fingerprints and detached values. No DI/scope disposal or admission rule changes.

Each generator/trim/AOT warning SHALL have an individual disposition; supported
OrcaCore paths SHALL be remediated. No blanket suppression, reflection-enabling
switch or private/whole-assembly root establishes support. Real native public-
package evidence SHALL bind exact source/package/harness/toolchain identities.
Package-wide IsAotCompatible/IsTrimmable or extra public annotations require a
separate reviewed exposed-surface proof/catalogue amendment. Keep conservative
package metadata and implementation-pending guide wording until qualification.
No performance advantage, new provider/DAG/RID support or released preview.2
support follows from contract or canonical approval. [AC-563–574]

NF-034 supplemental NS-02d/NS-06g–h require ordinary public-package Metadata and Default contexts for all eleven attributed strong-string types, root/nested/collection byte/value/fingerprint parity and unsupported direct-factory/configuration negatives. The catalogue accessibility change is not native qualification; every existing six-cell and individual-warning gate remains required.

The approved 2026-10-26 throttle annotation adds only
DynamicallyAccessedMembers(Interfaces) to StepExecutionThrottle.For<TStep>'s
existing generic parameter. NS-09a requires preserved validation/order/identity
for valid/inherited/invalid steps and capacities, and removal of IL2090 in trim
and AOT. It grants no package-wide badge or native-host qualification.

### Approved 2026-10-27 public-consumer boundary

NS-10 follows the bounded public recipe approved in the adjacent amendment:
typed inbound value construction/materialization, typed StepResult.WaitForEvent
parking and physical cancellation/join, timers/retries, transient-pool grants,
root Parallel and bounded ForEach. The public ephemeral closure exports neither
live event ingress/resumption nor outgoing Publish. These receive no native
credit. Existing internal event regressions remain required, without substituting
for public-package execution. NS-07/08/11 prove public value rejection and
retained-instance state; they make no public delivery claim. Every original
AOT-01 through AOT-16 assertion, NS group and mandatory subcase remains required.
A future ingress/publish capability requires its own reviewed public contract.
This is an explicit qualification-scope adjustment, not the old full event
requirement passing. Source/public API and the fixed codec are unchanged.

Qualification status (2026-10-28): user-approved corrected checkpoint d8c55f3 /
review-only child 25fa36c bind the bounded source/native recipe and its explicit
converter, throttle and public-consumer amendments. All six native cells pass,
384 cases total; publication is pending.
This status does not relax NF-034 or grant live event/publishing, other engine/RID
or released-preview.2 support. [Qualified recipe](../ephemeral-native-support.md)
