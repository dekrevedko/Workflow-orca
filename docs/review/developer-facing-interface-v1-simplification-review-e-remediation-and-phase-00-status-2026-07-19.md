# Review-E remediation and Phase 0 status

**Date:** 2026-07-19

**Nature:** Phase 0 guard-packet implementation and review-gate status

## Current gate

Review-E planning remediation has been applied to the live planning packet and passed the
[independent planning re-review](developer-facing-interface-review-e-remediation-independent-rereview-2026-07-19.md).

- **Planning state:** remediation applied; independent re-review approved with no P0/P1/P2 findings.
- **Guard implementation:** tasks 3.1 through 3.12 are complete and independently approved.
- **Phase 0 exit:** approved by the final immutable independent re-review.
- **Product implementation:** task 4.0 and every later product task remain blocked at the requested next-phase review boundary and by task 4.0's explicit prerequisites.

The first completed packet received an immutable
[REJECT verdict](developer-facing-interface-phase-00-guard-packet-independent-review-2026-07-19.md).
All findings from both immutable guard-packet reviews are remediated and submitted through the
[Phase 0 remediation re-review request](developer-facing-interface-phase-00-guard-packet-remediation-rereview-request-2026-07-19.md).
The exact frozen packet then received a final immutable
[APPROVE verdict](developer-facing-interface-phase-00-guard-packet-final-independent-rereview-2026-07-19.md)
with no P0, P1, P2, or P3 gate findings. Task 3.12 is closed; task 4.0 was not started.
The infrastructure lane now passes 43/43. The product lane has 28/28 intentional failures: the
declaration/package gaps plus ten executable behavior-certification theories covering all 95
scenarios in tasks 3.5 through 3.11d. Each future behavior driver must make runtime-recorded calls
matching the frozen assembly, declaring type, member, generic arity, parameter, and return
signature; it must assert every context-owned observation and consume required deterministic
time/barrier seams from inside an observed product call. Compile-fixture infrastructure packs and
uses only the fixture-owned `OrcaCore 0.0.0-phase0` package, compiles the exact companion and full
positive usage, verifies 26 precise negative diagnostics, and rejects an incomplete-package
mutation. Its real-product fixture remains intentionally red. All eight package journeys remain
intentionally red only because the exact `0.0.0-phase0` packages do not exist. The full solution
builds with zero warnings, both coordinated OpenSpec changes pass strict validation,
`git diff --check` passes, and production `src/` has no Phase 0 edits.

## Current authority

Use the following live sources in order:

1. [Document 17](../specs/17-selected-mode-capability-matrix.md), including its exact capability,
   package, diagnostic, lifecycle, hosting, and deferred-surface catalogs.
2. [The exact C# authoring companion](../specs/17-public-authoring-contract.cs).
3. The affected [canonical requirements](../specs/README.md), including the amended
   [core-runtime](../specs/04-requirements-core-runtime.md),
   [composition](../specs/08-requirements-composition.md),
   [management](../specs/09-requirements-management-operations.md),
   [acceptance](../specs/12-acceptance-criteria.md),
   [scheduler](../specs/14-driving-scenario-eks-job-scheduler.md), and
   [durable-driver](../specs/16-requirements-durable-driver.md) contracts.
4. The live [`reshape-developer-facing-interfaces`](../../openspec/changes/reshape-developer-facing-interfaces/)
   change and coordinated
   [`add-runtime-concurrency-limits`](../../openspec/changes/add-runtime-concurrency-limits/)
   change.
5. The current
   [developer-facing implementation plan](../implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md).

The selected placement rule remains root-only: fixed `Parallel`, bounded `ForEach`, and `While`
are available only on the selected root sequence in both modes. `If` is the only nestable
structural control-flow member. Nested, branch, item, and leased builders expose no `Parallel`,
`ForEach`, or `While`; hand-built or stale nested graphs are rejected by compiler defense.

`OrcaCore` remains the primary non-meta application contracts and authoring package.
`OrcaCore.Dag` consumes only public `OrcaCore` workflow contracts and owns immutable typed
execution-plan authoring and operation contracts, with no v1 visualization surface.
`OrcaCore.Dag.Hosting` alone consumes the named internal durable child-start/join bridge.

## Immutable provenance

The following dated artifacts remain unchanged evidence for the snapshots they inspected or
recorded. This remediation does not rewrite or backdate them:

- the [2026-07-18 simplification amendment](developer-facing-interface-v1-simplification-amendment-2026-07-18.md)
  and [2026-07-19 strong-value amendment](developer-facing-interface-v1-strong-value-construction-amendment-2026-07-19.md);
- the historical [2026-07-18 reviewer prompt](developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md)
  and [Phase 0 status](developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-18.md);
- independent reviews
  [A](developer-facing-interface-v1-simplification-review-2026-07-19.md),
  [B](developer-facing-interface-v1-simplification-review-2026-07-19-b.md),
  [C](developer-facing-interface-v1-simplification-review-2026-07-19-c.md),
  [D](developer-facing-interface-v1-simplification-review-2026-07-19-d.md), and
  [E](developer-facing-interface-v1-simplification-review-2026-07-19-e.md);
- the [four-review consolidated record](developer-facing-interface-v1-simplification-consolidated-review-2026-07-19.md);
- the [root-only owner decision and revalidation](developer-facing-interface-v1-root-only-fan-out-decision-and-revalidation-2026-07-19.md).

The root-only placement decision remains incorporated in live authority. Earlier `READY` verdicts
are historical snapshots and do not advance the current gate.

## Review-E disposition

The table records the selected resolution of every Review-E finding. The independent re-review
must verify the referenced live text; this table is not self-approval.

| Review-E finding | Current disposition | Live authority or supporting evidence |
|---|---|---|
| Stale prompt/status selected nested `Parallel` and a meta-package | **Remediated and routed away.** Positive authoring is root `Parallel`, root `ForEach`, and nested `If`; non-root `Parallel`/`ForEach`/`While` are absent. `OrcaCore` is the primary non-meta package. | [Matrix](../specs/17-selected-mode-capability-matrix.md), [exact companion](../specs/17-public-authoring-contract.cs), [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md), and [active index](../active-implementation-index.md) |
| Zero-branch `Parallel` had no result or rejection contract | **Remediated.** Root-only fixed `Parallel` is nonempty; an empty authored branch set produces `SFE-AUTH-BRANCH-004` and has an explicit Phase 0 misuse fixture. | [Composition requirements](../specs/08-requirements-composition.md), [matrix diagnostics](../specs/17-selected-mode-capability-matrix.md), [workflow-authoring delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md), and [quality delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md) |
| Repeated `CompleteWithin` lacked a rejection channel | **Remediated.** A duplicate declaration is rejected eagerly as `SFE-AUTH-DEADLINE-001` through `WorkflowDefinitionException` and is assigned to deadline guards. | [Core requirements](../specs/04-requirements-core-runtime.md), [matrix](../specs/17-selected-mode-capability-matrix.md), [workflow-authoring delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Engine mode and transient-pool compatibility lacked a closed outcome | **Remediated.** Wrong-mode registration and referenced-but-unconfigured transient pools produce the closed `HostIncompatible` registration result before execution. | [Matrix registration contract](../specs/17-selected-mode-capability-matrix.md), [developer-facing delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/developer-facing-surface/spec.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Unknown durable pools and invalid resize capacity were unspecified | **Remediated.** A typed unknown-pool failure occurs before queue or management mutation; statically inspectable references fail registration through `HostIncompatible`; resize requires positive capacity, unknown pools throw the typed failure, and operation-ID intent reuse retains its closed conflict result. | [Management requirements](../specs/09-requirements-management-operations.md), [matrix resource contract](../specs/17-selected-mode-capability-matrix.md), [management delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Crash replay and retry-budget attempt numbering were not closed | **Remediated.** `AttemptNumber` is a durable retry-policy ordinal; uncertain crash redispatch reuses the same committed operation/attempt/deadline coordinate, and only a committed retry transition increments it. | [Core requirements](../specs/04-requirements-core-runtime.md), [durable-driver requirements](../specs/16-requirements-durable-driver.md), [matrix](../specs/17-selected-mode-capability-matrix.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Startup agreement after persisted pool resize was ambiguous | **Remediated.** `DurableResourcePoolDefinition.Capacity` remains the immutable creation capacity, while snapshot `ConfiguredCapacity` is the replayed current capacity; restart validates the creation definition and never overwrites resize state or debt. | [Management requirements](../specs/09-requirements-management-operations.md), [matrix](../specs/17-selected-mode-capability-matrix.md), [durable-runtime delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/durable-runtime/spec.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Scheduler handoff released capacity before terminal validation | **Remediated.** The terminal outcome step remains inside the scheduler lease immediately after `Wait` and validates the committed job identity, operation/protection identity, and terminal state before lexical exit. | [Scheduler handoff](../eks-scheduler-handoff.md) and [canonical scheduler scenario](../specs/14-driving-scenario-eks-job-scheduler.md) |
| Application timeout snapshot contradicted the exact projection | **Remediated.** Ordinary snapshots retain only their exact public fields: active wait deadlines and terminal timeout through status/failure. Running workflow and attempt deadline/ordinal facts remain runtime/BCL telemetry or internal facts; lease quarantine is separate advanced diagnostics. | [Matrix projection contract](../specs/17-selected-mode-capability-matrix.md), [management requirements](../specs/09-requirements-management-operations.md), and [management delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md) |
| CP-022 assigned failed dependencies both mapping failure and dependency blocking | **Remediated.** A non-success direct dependency prevents mapper invocation and makes the dependant `DependencyBlocked`; mapping-invalid remains limited to opaque invalid access or projector failure. | [Composition requirements](../specs/08-requirements-composition.md), [matrix DAG contract](../specs/17-selected-mode-capability-matrix.md), and [quality delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md) |
| Admitted-item and lease-race semantics lacked exact Phase 0 traceability | **Remediated.** Guards now cover the lower host/node bound, parked admitted-item retention, ceiling-one progress, cancel-before-grant, atomic grant/cancel winners, restart boundaries, and no-ghost accounting. | [Acceptance criteria](../specs/12-acceptance-criteria.md), [concurrency delta](../../openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md), [quality delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Task 3.10 lacked a Phase 0 outbox/lineage seam | **Remediated by an explicit phase boundary.** Phase 0 proves the exact friend edge, public-child absence, one stable child identity, and observable reattachment. AC-613/614 remain mandatory implementation acceptance assigned to tasks 8.4, 8.5, and 8.10; only their internal seam detail is outside Phase 0, so no public or friend-only query API is invented for guards. | [Acceptance criteria](../specs/12-acceptance-criteria.md), [matrix package boundary](../specs/17-selected-mode-capability-matrix.md), [repository-foundation delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/repository-foundation/spec.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Optional nullable `End` conflated omission and explicit null | **Remediated.** The exact companion exposes four `End` overloads: unnamed and non-null named-outcome forms for each resultless/resultful completion builder. | [Exact companion](../specs/17-public-authoring-contract.cs), [matrix completion contract](../specs/17-selected-mode-capability-matrix.md), and [workflow-authoring delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md) |
| CR-008 required `End` even for perpetual rollover | **Remediated.** An ordinarily completing workflow selects `End`; a perpetual durable generation selects unconditional root `ContinueAsNew`; exactly one generation-terminal form is required. | [Core requirements](../specs/04-requirements-core-runtime.md), [matrix compiler contract](../specs/17-selected-mode-capability-matrix.md), and [workflow-authoring delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md) |
| Stop-confirmation rows lacked total precedence | **Remediated.** Confirmation-ID conflict precedence is first: an ID already bound to another token returns `ConfirmationConflict` before token lifecycle is evaluated; the overlap cases are assigned to the exhaustive confirmation guards. | [Matrix lease contract](../specs/17-selected-mode-capability-matrix.md), [management requirements](../specs/09-requirements-management-operations.md), [quality delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md), and [reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md) |
| Supporting DAG prose promised visualization and a forbidden runtime edge | **Remediated.** DAG owns immutable typed execution-plan authoring/operations without visualization; only DAG hosting consumes the internal durable bridge. | [Glossary](../specs/03-domain-model-and-glossary.md), [historical Phase 4b routing](../implementation/phases/phase-4b-dag-external-jobs/README.md), [matrix package graph](../specs/17-selected-mode-capability-matrix.md), and [repository-foundation delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/repository-foundation/spec.md) |
| Decorator direction and hosting-entry count were stale descriptions | **Remediated.** A decorator binds the immediately preceding eligible business step; hosting verification uses the exact entry-point allowlist rather than a stale count. | [Glossary](../specs/03-domain-model-and-glossary.md), [core requirements](../specs/04-requirements-core-runtime.md), [matrix hosting inventory](../specs/17-selected-mode-capability-matrix.md), and [quality delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md) |

## Independent re-review request

The next reviewer must inspect the live authority rather than infer current requirements from the
immutable dated prompt, status, amendments, or reviews. At minimum, the reviewer must confirm:

1. every Review-E row above is represented consistently in document 17, its exact companion,
   canonical requirements, both active OpenSpec changes, and the supporting routes;
2. root-only `Parallel`, `ForEach`, and `While` have positive root fixtures and negative
   nested/branch/item/leased fixtures;
3. all 15 section-3 tasks are authorable without inventing a public signature, internal protocol,
   lifecycle transition, package edge, or timing race;
4. task 3.12 still requires actual execution evidence and a subsequent independent guard review;
5. task 4.0 remains blocked.

The independent planning review changed guard-retarget readiness from **NOT READY** to **READY**.
Task 3.12 still requires actual execution evidence and a separate independent review of the
completed guard packet before Phase 0 may exit.
