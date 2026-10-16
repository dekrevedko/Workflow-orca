# Developer-facing interface Phase 00 status (2026-07-18)

## Outcome

**PLANNING READY FOR INDEPENDENT REVIEW. PHASE 0 IS NOT READY FOR EXIT OR PRODUCT
IMPLEMENTATION.**

The 2026-07-18 first-release simplification is now reconciled across the planning baseline and
awaits independent review. This planning packet neither authorizes product/guard implementation
nor treats historical guard evidence as proof of the revised contract. Existing guards still
describe parts of the superseded API.

Task 4.0 remains blocked. The next implementation packet is guard-only and must retarget all 15
mandatory section-3 tasks: 3.1-3.10, 3.11a-3.11d, and 3.12. Task 3.12 reruns every retargeted lane,
records actual evidence, and obtains independent review before Phase 1 begins.

Current OpenSpec progress:

- `reshape-developer-facing-interfaces`: **15 done / 97 pending / 112 total**;
- Phase 0 section 3 within reshape: **0 done / 15 pending / 15 total**;
- `add-runtime-concurrency-limits`: **7 done / 9 pending / 16 total**.

## Authority and history

Current authority, in order:

1. [`17-selected-mode-capability-matrix.md`](../specs/17-selected-mode-capability-matrix.md)
2. [`17-public-authoring-contract.cs`](../specs/17-public-authoring-contract.cs) for exact authoring declarations
3. Current canonical requirements under `docs/specs/`
4. `openspec/changes/reshape-developer-facing-interfaces/`
5. `openspec/changes/add-runtime-concurrency-limits/`
6. The revised phased implementation plan

The dated independent reviews, Phase 0 review rounds, and
[`developer-facing-interface-phase-00-lease-contract-amendment-draft-2026-07-16.md`](developer-facing-interface-phase-00-lease-contract-amendment-draft-2026-07-16.md)
remain immutable historical evidence. The new
[`developer-facing-interface-v1-simplification-amendment-2026-07-18.md`](developer-facing-interface-v1-simplification-amendment-2026-07-18.md)
records later owner decisions rather than editing history.

## Current first-release delta

The guard target changed materially:

- staged typed workflow input/output plus resultless definitions/references;
- codec-detached per-attempt state with `StepContext<TState>.ReplaceState` and fixed certified
  `orcacore-json-v1` registration;
- validated immutable `DefinitionId`/`DefinitionVersion` and definition fingerprint conflict;
- ephemeral-only inline lambda steps;
- one `Wait`; removed `WaitLong` and author `Yield`;
- `Parallel(...).WhenAll*` in both modes;
- finite root `ForEach(...).WhenAll*` in both modes, including valid empty input and durable
  snapshot/restart; `WhenAllOutcomes` contains success/failure only and ancestor terminality
  suppresses merge;
- deferred `WhenFirst`, Saga, public external jobs, public child workflows, nested `While`,
  nested dynamic fan-out, durable lambdas, and definition-wide retry;
- workflow/step deadlines and stable `StepOperationId`/diagnostic `AttemptNumber`;
- instance-targeted `EventId` deduplication, unique correlation routing, and no definition fanout;
- host-owned countable execution-path tokens plus separately counted admitted `ForEach` items,
  exact-step-type throttles, and one transient pool per ephemeral step; saturation parks, with no
  independent advancement/general-body ceiling, fail-fast/capacity-wait-timeout policy, or custom
  transient-governance SPI; driver segment budgets remain separate fairness mechanics;
- scoped-only durable resource acquisition and generic `IDurableResourceLeaseRecovery` stop
  confirmation;
- separate `OrcaCore.Dag`, sole `OrcaCore.Dag.Hosting` durable bridge, and separate
  outward-dependent Kubernetes/AWS/job companion;
- role-specific host registration (`AddOrcaCoreEphemeralEngine`, `AddOrcaCoreDurableEngine`,
  callback-only `AddOrcaCoreDurableEventIngress`, development/test in-memory provider, and
  `AddOrcaCoreDag`) with no catch-all or hosted-service toggle;
- one durable resource-governance aggregate per provider partition through the
  `Runtime.Protocol`/`Provider.Abstractions` tiers and no force release/time reclaim.

No alias, obsolete tombstone, placeholder member, or compatibility overload is permitted.

## Task disposition

| Task | Disposition | Reason/action |
|---|---|---|
| 3.1 | **Reopened** | Guard exact document-17/companion signatures and diagnostics, classify every public/advanced/internal tier, and reject unapproved overloads, placeholders, aliases, friend assemblies, and cross-tier leaks. |
| 3.2 | **Reopened** | Define clean packed consumers for minimal ephemeral, provider-backed durable, callback-only ingress, in-memory durable development/test provider, DAG/DAG hosting, the small meta-package, and outward-only companion. |
| 3.3 | **Reopened** | Guard the sole `Provider.Abstractions -> Runtime.Protocol` edge, governance-store load/expected-version append and record validation, plus forbidden application/engine/reverse edges. |
| 3.4 | **Reopened** | Guard every exact authoring/definition/reference declaration, `TryBuild`/`Build` diagnostic parity, mode/root/lease restrictions, qualified `StepResult.*`, and all deferred/removed absence. |
| 3.5 | **Reopened** | Guard strong values, fixed codec, structural fingerprint/version bump, detached attempt state/`ReplaceState`, typed completion, projection opacity, and in-flight/private-state exclusion. |
| 3.6 | **Reopened** | Guard ordered success/failure outcomes, deterministic join failure, ancestor merge suppression, no sibling cancellation, valid empty bounded `ForEach`, item replay, and exact path-token fan-out behavior. |
| 3.7 | **Reopened** | Guard exact registry/definition/instance/output/event and hosting/options surfaces, reduced management, exact path/step/transient-pool configuration and preceding-step binding; reject provisional capacity policies/SPIs, bulk/query/statistics, deferred management, catch-all/toggles, and serializer hooks. |
| 3.8 | **Reopened** | Build application-only typed ephemeral/durable journeys covering registration/start/reopen, ordinary waits, typed output, all four event overloads/routes, dedup/conflict/non-consuming results, unique correlation, stream reuse, and definition-less continuation. |
| 3.9 | **Reopened** | Guard retry classification, all deadlines/inheritance, attempt-copy fencing/late overlap, stable operation identity, attempt increments, ambiguous create-or-observe, competing drivers, and distinct occurrence identity. |
| 3.10 | **Reopened** | Guard complete resultless/resultful DAG build/operations, direct typed mapping and input commit, ordinals/status/start/output/cancellation/failure/reattachment, node ceiling, and sole hosting bridge. |
| 3.11a | **Reopened** | Guard scoped static/selector lease request/admission, replay, atomic grant, fiber parking, legal loop/siblings, ancestry and continue-as-new defenses, and qualified `StepResult.*` absence. |
| 3.11b | **Reopened** | Guard release-before-parent-resume on normal/definite pre-effect exit and exact quarantine on cancellation/deadline/ambiguous submit/process loss/forced termination before merge/progression. |
| 3.11c | **Reopened** | Guard the exhaustive stop-confirmation result matrix and races, proof/token retention, stale/mismatch defense, per-ticket review marks, causal release-gap recovery, `LeaseLost`, and no time/renewal reclaim. |
| 3.11d | **Reopened** | Guard startup agreement, serialized provider-partition aggregate, FIFO atomic grants, four-stage handoff recovery, atomic expected-version append, ownership/accounting, resize debt, tombstones, and no ghost/double grant. |
| 3.12 | **Reopened/pending** | Run every task-3 lane, record actual pass/expected-red counts and blockers, refresh the packet, and obtain independent approval of all 15 tasks before 4.0. |
| 4.0+ | **Blocked** | No product source work until the retargeted Phase 0 guard packet is independently approved. |

## Required guard-only remediation

### 3.1-3.3 - tiers, registration, and packed consumers

- Classify ordinary application, `OrcaCore.Dag`, `OrcaCore.Dag.Hosting`, durable hosting,
  runtime-protocol, provider-author, provider-adapter, companion, and internal surfaces.
- Guard the only allowed advanced edge
  `OrcaCore.Runtime.Protocol <- OrcaCore.Provider.Abstractions <- provider adapters` and the
  sole DAG bridge edge `OrcaCore.Durable.Hosting <- OrcaCore.Dag.Hosting`.
- Compile clean local-package consumers for ephemeral, in-memory durable, provider-backed
  durable, DAG, custom provider/host, the small meta-package, and outward-only companion.
- Prove exact hosting roles/options and absence of catch-all `AddOrcaCore`, a separate
  hosted-service toggle, implicit mode selection, or codec replacement.

### 3.4 - compile and public shape

- Positive fixtures for typed resultless/output workflows and references in both modes.
- Negative fixtures for building before `End`, default/invalid definition identity/version,
  dynamic outcome-name selection, durable lambdas, nested `While`/`ForEach`, and every
  deferred/removed member.
- Positive fixtures for root/nested `If`, nested `Parallel`, `WhenAll`/`WhenAllOutcomes`, root
  bounded `ForEach` in both modes (including a valid empty item snapshot), and scoped lease
  authoring on allowed durable builders.
- Negative fixtures for empty/mutable/default lease request, point acquisition, nested acquire
  under a live lexical scope, lease-body `ContinueAsNew`, raw-string/type swaps, and any author
  TTL/renewal/holder identity.
- Qualified absence scans for every provisional `StepResult` member until source deletion.

### 3.5 - strong values, codec, state/output, and projections

- Definitions expose typed authored metadata but no executable/compiled IR. Wait projections
  expose `WaitId`/`AuthoredLocation` without fiber, scope, or sequence ownership.
- Every attempt begins from the same codec-detached committed state when retried. Only the
  winning copy/`ReplaceState` commits; failed/timed-out/late copies and branch/item-private state
  never leak into root state or output.
- The fixed `orcacore-json-v1` codec rejects unsupported/cyclic/polymorphic shapes at
  registration and certifies deterministic bytes/detached round trips.
- Strong values reject raw/default/type-swapped identities. Fingerprints cover structural
  authored data only; opaque behavior changes require a new definition version.

### 3.6 - joins, bounded ForEach, and path tokens

- Branch/item aggregates are deterministic authored/index order and contain success/failure only.
  `WhenAll` produces deterministic join failure; `WhenAllOutcomes` may merge the summary.
- Ancestor cancellation/termination/deadline suppresses join/merge, ordinary branch failure does
  not cancel siblings, and empty bounded `ForEach` merges once with an empty result.
- Durable item selection commits before admission/replay. Fan-out parents release their path token
  before child admission and reacquire only for merge/continuation.

### 3.7 - exact facades, hosting, management, and transient governance

- One exact typed management surface remains. V1 management
  exposes typed snapshot/state/output, cancellation request, and termination; pause/resume/retry/
  archive/purge aliases and tombstones are absent.
- `StructuredExecutionHostOptions` exposes only the per-instance path ceiling and exact-step-type
  throttle list. Only ephemeral options expose named transient pools, and one step may select one
  pool; the decorator binds the immediately preceding business step. Provisional host-wide
  advancement/general-body ceilings, fail-fast/capacity-wait-timeout
  policy, and custom transient-governance SPIs are absent.

### 3.8 - golden consumer journeys

- Ephemeral typed workflow with inline lambda, transient pool, `Parallel` join, and typed output.
- Durable typed workflow with named steps, bounded durable `ForEach`, deadlines, normal `Wait`,
  scoped lease, and typed output.
- No raw processor/command/checkpoint/fiber/scope/provider identity in application code.
- No public external-job or child-workflow fluent member.

### 3.9 - deadlines, identity, and split-host continuation

- Same `StepOperationId` across attempt retry, timeout reconciliation, replay, process
  replacement, expected-version conflict, and competing drivers.
- New operation ID for loop re-entry, branch, `ForEach` item, and continue-as-new generation.
- `AttemptNumber` increments but never acts as external idempotency key.
- `WithStepTimeout` fences late results; `CompleteWithin` persists across restart and includes
  queue/wait/lease time, remains one original absolute deadline across continue-as-new, and
  suppresses branch/item joins and merges when it wins.
- Definition-less callback host commits one stable `EventId`; a definition-owning pump resumes
  exactly once.
- Per-target event dedup returns duplicate only for identical normalized content and conflict for
  changed content. Correlation routing admits exactly one active wait by
  `(DefinitionId, EventName, CorrelationId)` and rejects ambiguity before parking.

### 3.10 - typed DAG

- Immutable typed run input and `DurableWorkflowRef<TInput,TOutput>` node references.
- `OutputOf` accepts only a declared direct dependency with the matching type.
- Node input maps and commits once after direct dependencies succeed, then replays after restart.
- Fixed dependency failure blocks dependants while independent ready nodes may continue.
- One durable child workflow instance per node through an internal protocol; no public
  `RunChildren`.
- Architecture guard: OrcaCore does not depend on `OrcaCore.Dag`; only
  `OrcaCore.Dag.Hosting` reaches the named/versioned internal child seam; neither depends on the
  companion scheduler, Kubernetes, AWS, or job SDKs.

### 3.11a - lease authoring and admission

- `ResourceLeaseRequest.Create(first, additional...)` and strong `ResourcePoolName` make empty
  requests and transient/durable pool swaps impossible.
- Static/dynamic request validation and selector commit happen before provider mutation.
- Lease remains held while its lexical body parks in `Wait`; a following step after scope exit
  observes release and does not reacquire implicitly.
- Root-loop sequential scopes and independent siblings are legal; a descendant acquisition
  under pending/held ancestry fails before pool mutation.
- Leased builders omit nested acquisition and `ContinueAsNew`; runtime defense returns the
  exact diagnostics before any pool mutation. Qualified `StepResult.*` absence scans remain.

### 3.11b - lease exit and quarantine

- Normal completion/definite pre-effect failure releases exactly once; cancellation, deadline,
  ambiguous work, process loss, or forced termination quarantines exact units.

### 3.11c - lease reconciliation

- Only trusted idempotent `IDurableResourceLeaseRecovery` confirmation with matching protection
  token/confirmation identity can release quarantine after stop/terminal/fence proof.
- Review timestamps mark/reconcile but never reclaim by elapsed time; there is no renewal.
- Mixed-pool requests use per-ticket review deadlines; causal orphan/release-gap recovery and
  missing-ticket `LeaseLost` are distinct from ambiguous ownership.

### 3.11d - provider governance, accounting, and races

- Startup definitions must agree. One serialized resource-governance aggregate per configured
  provider partition owns every pool/request/ticket/mark/resize/confirmation/tombstone, FIFO
  atomic multi-pool grant, and four-stage workflow/governance reservation handoff.
- `Runtime.Protocol` record copy/format/checksum and `Provider.Abstractions` expected-version
  whole-batch append conflict behavior are crash/retry certified.
- Isolated restoration, contended conservation/waiter transfer, and resize debt are asserted
  separately.
- Acquire/release facts must match ownership identity; competing release/reconcile/resize
  operations leave no ghost or double grant.
- There is no force-release path; only matching trusted stop/fence confirmation may release
  quarantined capacity.

## Verification state

This was a planning-only pass. No build, unit test, compile fixture, guard lane, coverage report,
or container-backed suite was rerun. Historical counts remain useful only for the historical
contract and must not be presented as evidence for the 2026-07-18 target.

Combined planning checks passed both strict OpenSpec validations, repository-wide
`git diff --check` (line-ending warnings only), and scoped relative Markdown-link checks. These
validate planning structure and hygiene only; they are not product/guard execution evidence.

The planning packet passed these review-handoff gates:

- `openspec.cmd validate reshape-developer-facing-interfaces --strict`
- `openspec.cmd validate add-runtime-concurrency-limits --strict`
- documentation link/Markdown checks
- `git diff --check`

The guard implementation packet must refresh expected-red counts from execution and report
exact passed/failed/skipped values for every lane.

## Review readiness

Ready now:

- a full independent review of the planning contract;
- a readiness review of whether all 15 section-3 tasks (3.1-3.10, 3.11a-3.11d, and 3.12) are
  sufficient to retarget guards without inventing another API, with task 3.12 as the
  execution/re-review gate.

Not ready now:

- Phase 0 exit or task acceptance;
- source implementation;
- a claim that guards represent the new contract;
- a claim that any v1 surface is release-ready.

After planning approval, apply only the guard retarget packet and complete task 3.12: rerun every
section-3 guard lane and strict validation, refresh this status with actual counts, and request a
Phase 0 re-review.
