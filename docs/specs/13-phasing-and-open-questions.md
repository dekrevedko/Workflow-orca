# 13. First-Release Phasing and Remaining Questions

Status: revised 2026-07-19 for the greenfield first-release baseline in document 17 and its
normative C# declaration companion.

## 13.1 Recommended delivery slices

These are implementation slices, not independently shippable compatibility eras. OrcaCore has
not shipped: a superseded provisional API is deleted and its tests are rewritten; aliases,
obsolete tombstones, and placeholder members are not retained.

### Slice 0 — Contract and executable guard alignment

- Make document 17, canonical requirements, coordinated OpenSpec changes, and implementation
  tasks describe one staged typed API.
- Retarget compile/reflection guards away from `WaitLong`, `Yield`, point/fiber-lifetime
  acquisition, `RunExternalJob`, public child nodes, saga, and raw-string identities.
- Prove positive/negative selected-mode surfaces, exact strong values, nested-builder
  availability, structural fingerprints plus opaque-code version-bump guards, fixed codec, and
  expected-red counts before product work.
- Gate: AC-008/016…026, AC-303, AC-526, and strict OpenSpec validation.

### Slice 1 — Typed workflow core in both modes

- Staged `Init<TInput>` -> body -> `End`/`End<TOutput>` construction; fixed optional outcome
  metadata; named DI-created steps in both modes and lambda steps in ephemeral only.
- Root `If`/`While`, nested `If`, `Wait`, `Delay`, exact max-attempt/fixed-delay retry, per-attempt
  `WithStepTimeout`, and whole-workflow `CompleteWithin`.
- Stable `StepOperationId`/`AttemptNumber`, serialized instance execution, durable pre-wait event
  ownership, global event identity, stable fanout membership, and runtime-owned execution quanta.
- Fixed `orcacore-json-v1`, codec-detached attempt state/`ReplaceState`, the closed four-route
  ingress union, unique active-wait registration, and signal-stream loop semantics.
- Durable streams/checkpoints/projections, cold-capable ordinary waits, restart-safe
  continuation, version/structural-fingerprint binding, typed output, exact v1 instance handles,
  and deadline-preserving continue-as-new.
- Gate: applicable AC-0xx/1xx/3xx plus DR-AC criteria updated by document 16.

### Slice 2 — All-terminal composition and governance

- Fixed root `Parallel(...).WhenAll`/`WhenAllOutcomes` in both modes, with isolated state,
  typed ordered results/outcomes, and one replacement-state merge.
- Bounded root `ForEach(...).WhenAll*` in both modes; durable item snapshot/order/recovery;
  lower-of node/host admission.
- Host-owned `MaxConcurrentExecutionPathsPerInstance`, ephemeral transient pools, and exact
  scoped durable `AcquireResources` with ancestry defense, quarantine, reconciliation, and
  trusted provider-neutral stop confirmation.
- Gate: AC-2xx, AC-518…529, AC-601…605, and provider certification for exact lease accounting.

### Slice 3 — Typed DAG and scheduler proof

- Separate `OrcaCore.Dag` package: immutable typed run input, resultful/resultless typed durable
  workflow refs, direct-dependency output mapping, cycle/type validation, structural fingerprint,
  and complete snapshots/statuses/cancellation. V1 makes no DAG visualization promise.
- Internal child instance per node, runtime-owned ready progression, stable failure closure,
  lineage/output inspection, and host `MaxConcurrentNodes`.
- Separate companion scheduler/integration application in the same solution. It uses ordinary
  typed durable steps for bounded idempotent Kubernetes `batch/v1 Job` create-or-observe,
  ordinary `Wait` for normalized watcher events, and generic lease stop-proof recovery.
- Gate: AC-606…614 and every JS-AC criterion in document 14, including a host-replacement and
  ambiguous-submit/stop reconciliation journey.

### Slice 4 — First-release hardening and review

- Every shipped persistence/transport provider passes the reusable certification suite.
- Hosting, samples, management, retention, security review, performance targets, telemetry,
  migration/version guidance, package dependency tests, and documentation examples are green.
- Review is scoped first to corrected Phase 0 guards, then to the complete v1 public surface;
  dated review artifacts remain immutable and new findings go in a new dated document.
- Gate: all non-deferred acceptance criteria, strict OpenSpec validation, full repository test
  lanes, and a review verdict permitting release.

## 13.2 Open questions to resolve during implementation

No unresolved question below may silently alter the approved v1 public authoring signatures or
semantics. Any such change requires an explicit document-17/OpenSpec amendment first.

1. **Durable history retention depth** — providers must preserve every fact required for active
   recovery, audit, output, lease safety, and the configured inspection window; the exact
   archival duration remains host/provider policy.
2. **Provider emulation** — relational providers may emulate append/expected-version semantics
   if PR-020/021 and the full certification suite hold; physical schema is not public API.
3. **Eviction strategy** — idle timeout, LRU-like pressure eviction, or hybrid may vary while
   MG-050 safety and cold-capable durable `Wait` remain invariant.
4. **Lifecycle telemetry durability split** — exactly which nonterminal diagnostic events are
   best-effort versus durable remains a hosting/observability decision; terminal facts and
   required audit/lease events remain durable.
5. **Completion wait implementation detail** — the public contract is fixed as notification-driven,
   race-free `WaitForOutputAsync`/`WaitForTerminalAsync` with caller-local cancellation and no
   polling; implementation may choose the internal notification primitive.

## 13.3 Normative first-release decisions

- **One workflow semantic kind, two execution modes.** Saga is deferred; ephemeral and durable
  workflow builders share business meaning and differ in guarantees.
- **Typed staged construction.** `Init` owns input-to-state creation; successful `End` atomically
  commits typed output plus optional fixed outcome. Dynamic classification belongs in output.
- **Immutable identity/version.** `DefinitionId` and positive `DefinitionVersion` are validated
  immutable reference values. One identity/version binds one structural fingerprint; opaque code
  changes require a new version and are not falsely detected by hashing delegates.
- **Fixed payload codec and detached attempts.** `orcacore-json-v1` is nonreplaceable in v1.
  Every attempt receives a codec-detached copy; only the winning success commits, and immutable/
  value state uses `ReplaceState`.
- **One public wait concept.** Ordinary durable `Wait` is restart-safe and cold-capable under
  host residency policy. `WaitLong` is removed. Dynamic `StepResult.WaitForEvent` remains only
  for post-step selection.
- **No author yield.** Runtime quanta/checkpoint scheduling own fairness; `Yield` is removed.
- **All-terminal joins.** `WhenAll` succeeds only when all succeed;
  `WhenAllOutcomes` exposes ordered typed success/failure outcomes. Neither automatically cancels
  a sibling; ancestor cancellation/termination/deadline suppresses merge. `WhenFirst` is deferred.
- **Bounded dynamic fanout.** Root `ForEach` ships in both modes with finite item snapshot,
  stable index order, explicit max items, optional node concurrency, and durable recovery.
- **Nesting.** Of the conditional/loop/fanout operators, only `If` nests; `Parallel`, `While`,
  and `ForEach` are root-sequence only.
- **Host-owned concurrency.** Authors cannot raise
  `MaxConcurrentExecutionPathsPerInstance`; `ForEach` takes the lower host/node limit. DAG
  `MaxConcurrentNodes` is separate.
- **Scoped-only durable leasing.** Non-empty atomic `AcquireResources(request, body)` holds only
  for its lexical body, has no author TTL/renewal/holder ID, permits sequential/loop-iteration
  scopes and independent root-fanout branch/item scopes, forbids active ancestor/descendant
  acquisition, exposes no fanout from its leased body, and quarantines ambiguous protected work
  until generic trusted stop/fence proof.
- **Two timeout levels.** `WithStepTimeout` bounds one attempt; `CompleteWithin` is anchored at
  workflow start and survives `ContinueAsNew`. Runtime orchestration is not a Polly policy.
- **Stable external-effect identity.** One `StepOperationId` survives a logical step visit's
  retries/replay; attempt number is diagnostic. OrcaCore does not claim exactly-once external
  API effects.
- **DAG is separate but v1.** `OrcaCore.Dag` plans typed graphs; each executable node is one
  durable child workflow instance driven through an internal protocol, not public child nodes.
  Nodes may be resultful or resultless; statuses, snapshots, order, failure mapping, and
  cancellation are closed contracts.
- **V1 management stays instance-scoped.** Typed handles expose snapshot/root state/output,
  cancellation request, and termination; events use instance/correlation routes. Bulk fluent
  management, pause/resume, failed-instance retry, history query, archive, and purge are deferred.
- **Role-based hosting.** Ephemeral engine, durable engine, callback-only durable ingress,
  dev/test in-memory durable provider, PostgreSQL production provider, SQL Server production
  provider, and DAG hosting have distinct entry points/options; there is no catch-all mode selector
  or serializer hook.
- **Integration direction is outward.** Kubernetes, AWS, jobs, watchers, reconcilers, and their
  SDKs belong to a separate companion project. No OrcaCore package depends on them; `OrcaCore`
  is the primary contracts/authoring package, not a meta-package.

## 13.4 Future-capability registry

This section is the **future-capability registry** named and cross-referenced by the canonical
OpenSpec `developer-facing-surface` and `saga-orchestration` capabilities. OpenSpec defines the
absence and re-entry obligation; the deferred-capability table is the human-readable inventory of
future work and the questions a future amendment must close. Removed concepts are recorded in a
separate subsection so their names stay searchable without misclassifying them as future promises.

### Deferred capabilities

Deferred capabilities remain documented but have no v1 member, alias, tombstone, placeholder,
positive compile fixture, or implementation task that pretends the contract is approved:

| Capability | Future amendment must close |
|---|---|
| `WhenFirst` | winner/tie rule, loser fate, result/failure merge, lease/protected-work interaction |
| Saga | typed action/result API, durable compensation progression, failure/remediation, restart evidence |
| Public `RunExternalJob` | generic request/result, dispatch topology, identity, timeout/stop, report dedup, lease bracket |
| Public `RunChild`/`RunChildren` | typed mapping, group result, cancellation and failure policy |
| Nested `Parallel` | recursive scope identity, admission, merge, recovery, and lease interaction rules |
| Nested `While` | re-entry/nesting rules and compiler/runtime evidence |
| Nested `ForEach` | combined bounds, identity, admission, payload/recovery and merge rules |
| Durable lambda steps | stable delegate/capture/code-version identity |
| Definition-wide retry | reset point, retained input/state/output, attempts and terminal policy |
| Failed-instance/step management retry | new-generation identity, retained state/input, output invalidation, lineage, authorization |
| Public pause/resume | lifecycle/admission, in-flight attempt, wait/timer buffering, restart, lease interaction |
| Public archive/purge | authorization, retention/reference safety, provider certification |
| Additional durable storage providers beyond PostgreSQL and SQL Server, including DynamoDB | complete current provider-port coverage, greenfield first-create schema, restart/competing-host certification, retention/poison parity, package ownership and dependency boundaries |
| Workflow-authored `Cancel` | target, terminal outcome, descendant/lease cleanup, authorization |

Durable workflow-authored `Publish` and definition-targeted event fanout are current Section 7B
capabilities. They use the transactional workflow-event outbox and stable fanout target ownership,
respectively, and are not future-registry entries.

### Removed concepts

`WaitLong` and author `Yield` are **removed**, not deferred: their useful behavior is supplied by
durable `Wait` residency and runtime-owned quanta. Their names must disappear from public
assemblies after the refactor.

Broker-SDK-specific OrcaCore packages and the old Redis projection-only adapter are also removed,
not deferred v1 placeholders. Applications or outward companion projects adapt
`IWorkflowEventDispatcher` to RabbitMQ, ZeroMQ, or another transport. PostgreSQL and SQL Server are
the two v1 production durable storage providers. Any later storage provider re-enters only through
the registry row above and must implement the complete provider contract rather than revive one of
the provisional project shapes.
