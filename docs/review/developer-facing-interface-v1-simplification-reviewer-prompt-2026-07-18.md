# Independent review prompt: OrcaCore v1 planning contract and Phase 0 guard readiness

**Date:** 2026-07-18

You are an independent senior .NET public-API, durable-runtime, and workflow-authoring reviewer.
Review the complete proposed **first-release planning contract** for OrcaCore and determine
whether the Phase 0 guard-retarget instructions are sufficient to implement next without
inventing signatures or semantics.

This is a planning-contract review, not a claim that source or guards implement the proposal.
Do not modify any input file; create only the required review output. Produce that document with
exact path/line evidence.

## Product context

OrcaCore is greenfield and has not shipped. Compatibility is not a goal. Superseded APIs are
deleted rather than aliased, deprecated, or retained as placeholders.

The first likely consumer is an advanced Kubernetes Job scheduler, potentially hosted on EKS,
while the ephemeral in-process engine remains important. Kubernetes, AWS, and job scheduling
must remain in a separate outward-dependent companion project, which may initially share
`OrcaCore.slnx`. No OrcaCore package or `OrcaCore.Dag` may depend on that project or its SDKs.

## Review verdicts required

Give two explicit verdicts:

1. **Planning contract:** `APPROVE`, `APPROVE WITH CHANGES`, or `REJECT`.
2. **Guard-retarget readiness:** `READY` or `NOT READY` for a guard-only packet covering all 15
   mandatory section-3 tasks: 3.1-3.10, 3.11a-3.11d, and 3.12.

Planning approval does not approve Phase 0 exit or product implementation. Guards have not
been retargeted or rerun.

## Authority and precedence

Review current files, not summaries, in this order:

1. `docs/specs/17-selected-mode-capability-matrix.md` - normative capability, signature,
   compiler, concurrency, package, deferred/removal, registration, and projection baseline.
2. `docs/specs/17-public-authoring-contract.cs` - exact normative authoring declarations paired
   with document 17.
3. Affected canonical requirements under `docs/specs/`, especially documents 03-06, 08-10,
   and 12-16.
4. `openspec/changes/reshape-developer-facing-interfaces/` - proposal, design, tasks, and every
   delta spec.
5. `openspec/changes/add-runtime-concurrency-limits/` - proposal, design, tasks, and runtime
   resource-governance delta.
6. `openspec/specs/runtime-resource-governance/spec.md` - current canonical governance spec.
7. Supporting files listed below.

Historical dated reviews and the 2026-07-16 amendment are supplied context only. Do not edit,
rewrite, or treat their superseded recommendations as current authority; this review does not
need to prove byte identity against an unavailable earlier snapshot.

## Complete reviewed-file list

Review every current file in this list (directory shorthand in the precedence section is expanded
here so the packet is mechanically bounded):

- `docs/specs/README.md`
- `docs/specs/01-concept-and-goals.md`
- `docs/specs/02-lessons-from-prior-art.md`
- `docs/specs/03-domain-model-and-glossary.md`
- `docs/specs/04-requirements-core-runtime.md`
- `docs/specs/05-requirements-events-waits-timers.md`
- `docs/specs/06-requirements-durable-execution.md`
- `docs/specs/07-requirements-saga.md`
- `docs/specs/08-requirements-composition.md`
- `docs/specs/09-requirements-management-operations.md`
- `docs/specs/10-provider-model-and-extensibility.md`
- `docs/specs/11-non-functional-requirements.md`
- `docs/specs/12-acceptance-criteria.md`
- `docs/specs/13-phasing-and-open-questions.md`
- `docs/specs/14-driving-scenario-eks-job-scheduler.md`
- `docs/specs/15-requirements-observability-otel.md`
- `docs/specs/16-requirements-durable-driver.md`
- `docs/specs/17-selected-mode-capability-matrix.md`
- `docs/specs/17-public-authoring-contract.cs`
- `openspec/changes/reshape-developer-facing-interfaces/proposal.md`
- `openspec/changes/reshape-developer-facing-interfaces/design.md`
- `openspec/changes/reshape-developer-facing-interfaces/tasks.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/developer-facing-surface/spec.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/durable-runtime/spec.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/repository-foundation/spec.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/saga-orchestration/spec.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md`
- `openspec/changes/reshape-developer-facing-interfaces/specs/workflow-contracts/spec.md`
- `openspec/changes/add-runtime-concurrency-limits/proposal.md`
- `openspec/changes/add-runtime-concurrency-limits/design.md`
- `openspec/changes/add-runtime-concurrency-limits/tasks.md`
- `openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md`
- `openspec/specs/runtime-resource-governance/spec.md`

- `docs/implementation/00-stack-decisions.md`
- `docs/implementation/01-solution-architecture.md`
- `docs/implementation/02-engineering-conventions.md`
- `docs/implementation/README.md`
- `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/README.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/PROGRESS.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-00-expand-task-index.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-01-durable-pool-store.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-02-acquisition-as-wait.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-03-ticket-expiry-pool-operations.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-04-run-external-job-composite.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-05-dag-builder-validation.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-06-run-cancellation-dag-observability.md`
- `docs/README.md`
- `docs/active-implementation-index.md`
- `docs/project-technical-overview.md`
- `docs/ephemeral-engine-developer-guide.md`
- `docs/end-to-end-plan.md`
- `docs/production-readiness.md`
- `docs/eks-scheduler-handoff.md`
- `docs/orleans-engine/README.md`
- `docs/plans/README.md`
- `docs/plans/current-roadmap.md` (historical snapshot; audit only its routing banner)
- `docs/requirements/README.md`
- `docs/architecture/README.md`
- `docs/review/integration-tests/01-hosting-and-hosted-services.md`
- `docs/review/developer-facing-interface-v1-simplification-amendment-2026-07-18.md`
- `docs/review/developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-18.md`
- this reviewer prompt

For the Phase 4b README, progress log, and T4B task files, audit only that their superseded/
historical routing is conspicuous and points to the current authority. Their preserved task bodies
are implementation history, not candidate v1 requirements.

## Accepted scope decisions

Challenge their completeness or internal safety, but do not report the decision itself as a
finding merely because a different product could choose more features:

- ship staged typed workflows, fixed optional outcome metadata, immutable validated
  `DefinitionId`/`DefinitionVersion`, and fingerprint-bound definitions;
- ship codec-detached attempt state with `StepContext<TState>.ReplaceState` and fixed certified
  `orcacore-json-v1`; the codec is not replaceable in v1;
- ship named steps in both modes and inline lambda bodies in ephemeral mode only;
- ship root `If`/`While`, nested `If`, nested `Parallel`, `Wait`, `Delay`, root durable
  `ContinueAsNew`, `CompleteWithin`, and `WithStepTimeout`;
- ship `Parallel(...).WhenAll*` and finite root `ForEach(...).WhenAll*` in both modes; empty
  `ForEach` is valid, `WhenAllOutcomes` carries success/failure only, and ancestor terminality
  suppresses merge;
- ship runtime-created stable `StepOperationId` plus diagnostic `AttemptNumber`;
- ship scoped-only no-author-TTL/no-renewal durable resource leasing and generic trusted
  `IDurableResourceLeaseRecovery` confirmation;
- ship one host-owned execution-path token model, with fan-out parent release/reacquisition and a
  separately counted admitted-item `ForEach` cap; exact-step-type host throttles; and at most one
  named transient pool per ephemeral step, bound to the immediately preceding business step. V1
  has no independent host-wide advancement/general-
  body ceiling, fail-fast/capacity-wait-timeout policy, or custom transient-governance SPI;
  driver segment budgets remain separate fairness mechanics;
- ship per-target event dedup and exactly-one-active-wait correlation routing; defer definition
  fanout;
- ship separate typed `OrcaCore.Dag`, sole `OrcaCore.Dag.Hosting` internal bridge, one internal
  durable child instance per node, fixed dependency-failure behavior, and `MaxConcurrentNodes`;
- ship exact role-specific engine/event-ingress/DAG hosting entry points and locally certified
  packages; defer only external publishing/signing/release automation;
- ship one serialized durable resource-governance aggregate per provider partition with no force
  release or time-only reclaim;
- defer public `WhenFirst`, Saga, `RunExternalJob`, `RunChild`/`RunChildren`, nested `While`,
  nested dynamic fan-out, durable lambdas, and definition-wide retry;
- remove `WaitLong` and author `Yield`;
- keep Kubernetes/AWS/jobs in a separate companion project with outward-only dependencies.

## Required review method

### 1. Signature-by-signature completeness

Walk every signature and availability claim in document 17. Prove that analogous ephemeral and
durable members differ only where the matrix says so. Check staged transitions, generic type
flow, selector snapshot types, parameter order, nullability, factory-only values, resultless
overloads, and whether a complete workflow can be authored without an unapproved type.

Confirm local invalid arguments fail at the fluent call, graph diagnostics accumulate at
`TryBuild`, and no default/value-type bypass remains for definition identity/version or strong
matching values.

### 2. Write realistic consumer programs

Author these complete programs against the proposed signatures and include them in the review:

1. Ephemeral typed-input/output workflow with an inline async lambda, transient pool,
   `Parallel(...).WhenAllOutcomes`, following `If`, codec-detached state replacement, workflow/
   step timeout, and `AddOrcaCoreEphemeralEngine` registration.
2. Durable typed workflow with bounded `ForEach`, branch/item failures summarized through
   `WhenAllOutcomes`, restart, and final typed output.
3. Durable resource journey where one short database lease exits before `Wait`, plus a
   scheduler-capacity lease that intentionally encloses `Wait` until external work terminates.
4. Typed DAG with immutable run input, at least three heterogeneous node workflow references,
   direct dependency output mapping, one failed dependency, an independent ready node, and
   `OrcaCore.Dag.Hosting.AddOrcaCoreDag` over the durable role.
5. Companion Kubernetes Job journey using a named durable create-or-observe step,
   `StepOperationId`, lease protection token, watcher `EventId`, cancellation/deadline
   reconciliation, and generic stop confirmation without any Kubernetes type in OrcaCore.

Report every missing signature, ambiguous state transition, or place where the sample can
silently do the wrong thing.

### 3. Adversarial misuse pass

Attempt and classify each program as compile-impossible, fluent-call rejection, build
diagnostic, registration/startup diagnostic, runtime defense, or silently accepted:

- build before `End`; use output type inconsistent with `DurableWorkflowRef`;
- empty/default/invalid definition ID/version; reuse identity/version with a changed
  fingerprint;
- durable lambda step; inline lambda with unsafe captured mutable state in ephemeral mode;
- nested `While`, nested `ForEach`, `WhenFirst`, Saga, `RunExternalJob`, `RunChildren`,
  `WaitLong`, and `Yield`;
- attach retry/step-timeout to `Wait`, `End`, a join, or an empty branch;
- `WhenAll` with one failure; `WhenAllOutcomes` whose following `If` rejects the summary;
- unbounded/over-limit durable `ForEach`; valid empty durable `ForEach`; host ceiling below/above
  node cap; parked item versus runnable path-token accounting; host ceiling 1 fan-out progress;
- reuse `EventId` with changed content; register two active waits for the same
  `(DefinitionId, EventName, CorrelationId)`; attempt definition-targeted fanout;
- use `AttemptNumber` as an external idempotency key; reuse one operation ID for another loop
  visit/item/branch/generation;
- point/fiber-lifetime lease, empty lease request, duplicate pool, non-positive units,
  transient/durable name swap, author TTL, renewal, holder ID;
- nested acquisition under live ancestry; sequential root-loop scope; sibling scopes;
  `ContinueAsNew` inside the leased body;
- release quarantine from elapsed time, delete acknowledgement, workflow terminal status,
  mismatched protection token, stale confirmation ID, or a force-release operation;
- DAG `OutputOf` undeclared/non-direct/wrong-typed dependency; caller-owned ready/completed
  sets; public child-workflow node;
- an OrcaCore/`OrcaCore.Dag` reference to Kubernetes, AWS, the companion project, or a Job DTO.
- catch-all `AddOrcaCore`, separate hosted-service toggle, codec replacement, transient pools in
  durable options, durable pools in ephemeral options, host-wide advancement/general-body
  ceilings, fail-fast/capacity-wait-timeout admission, multiple named pools on one step, non-exact
  step-throttle scopes, pool decoration silently binding the following rather than preceding step,
  custom transient-governance SPI, or DAG registration without the durable
  engine role.

Any silently accepted misuse affecting identity, external effects, lease capacity, mapping, or
dependency direction is at least P1.

### 4. Durable semantics stress

Review these state machines, not only signatures:

- `StepOperationId` allocation/commit and survival across retry, step-timeout reconciliation,
  crash before/after external create, expected-version conflict, and competing drivers;
- `AttemptNumber` increment, attempt deadline, late result fencing, and whole-workflow
  `CompleteWithin` restart behavior;
- detached state-copy/`ReplaceState` commit, retry from the same committed state, fixed-codec
  registration/certification, and physical-slot retention for token-ignoring fenced bodies;
- per-target event dedup conflict and structural event/timeout/workflow-deadline winner;
- durable `ForEach` selector commit before item admission, stable item identity, partial
  completion/restart, valid empty merge, merge-at-most-once, ancestor merge suppression, path
  token release/reacquisition, and host/node admitted-item composition;
- lexical lease pending/held/waiting/completed/quarantined/released transitions; exact token/
  obligation/provider-generation matching; grant/cancel and release/waiter races; resize debt;
- trusted `IDurableResourceLeaseRecovery` idempotency, stale confirmation defense, and operator
  dead ends;
- DAG node-input commit, internal child-start idempotency, dependency failure blocking,
  independent progress, restart reconstruction, `MaxConcurrentNodes` isolation, and the sole
  `OrcaCore.Dag.Hosting` bridge.
- provider-partition governance aggregate expected-version append, atomic multi-pool FIFO grant,
  crash/conflict recovery, resize debt, quarantine, confirmation, and tombstone retention.

### 5. Package and integration audit

Verify:

- `OrcaCore.Dag` depends on public OrcaCore application contracts, OrcaCore never depends on it,
  and `OrcaCore.Dag.Hosting` is the only bridge to `OrcaCore.Durable.Hosting` internals;
- the companion scheduler depends outward on documented public packages;
- no OrcaCore public signature/dependency closure contains Kubernetes, AWS, EKS, Job, manifest,
  watcher, cluster, or scheduler DTOs;
- provider/runtime SPIs are sufficient for their advanced audience but do not leak into the
  ordinary application journey;
- the small `OrcaCore` meta-package excludes optional integrations.
- exact role-specific hosting registration replaces catch-all/toggle composition, and the
  callback-only event-ingress role cannot progress definitions or run loops it does not own;
- all documented package IDs pack and support clean local consumers; only external publication,
  signing, SourceLink release setup, and release automation are deferred.

### 6. Guard-retarget readiness audit

For every reopened task 3.1-3.10 and 3.11a-3.11d, map every required public/runtime
guarantee to at least one proposed compile fixture, expected-red behavior guard, architecture
test, or package consumer. Then verify task 3.12 requires actual execution counts and independent
review of all 15 tasks. Flag:

- a requirement with no executable seam;
- a guard instruction that still assumes a deferred/removed member;
- a guard that would require inventing an unspecified signature;
- a timing/race assertion that cannot be deterministic;
- an expected-red count or claim presented without execution.

Decide whether the task instructions are sufficiently exact for a guard-only implementation
agent to proceed without design authority.

## Suggested validation commands

Run from the repository root, using `openspec.cmd` on Windows if PowerShell script execution is
disabled:

```powershell
openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
```

Also inspect repository-wide references to every removed/deferred member and forbidden SDK.
Building guard/source projects is optional context because they are known stale; if run, report
the results as current-implementation evidence, not planning compliance.

## Known pending implementation state

- Product source still contains provisional members.
- Existing Phase 0 guards have not been retargeted to this amendment.
- All 15 section-3 tasks (3.1-3.10, 3.11a-3.11d, and 3.12) are reopened; task 3.12 is the run/re-review gate.
- Current OpenSpec progress is reshape 15 done / 97 pending / 112 total and coordinated
  concurrency 7 done / 9 pending / 16 total.
- Phase 0 section 3 itself is 0 done / 15 pending / 15 total.
- Exact expected-red counts are intentionally unknown until the guard-only packet runs.
- Task 4.0 and all product implementation are blocked.

Do not report these facts as new findings unless a planning artifact falsely claims otherwise.

## Required output

Produce one review document with:

1. the two verdicts;
2. severity-ordered P0/P1/P2/P3 findings, each with exact evidence, developer/operator impact,
   and smallest normative remediation;
3. explicit non-findings for decisions examined and endorsed;
4. dimension scores for consistency, comprehensiveness, developer orientation, misuse
   resistance, durable safety, and package isolation;
5. the five consumer programs and friction notes;
6. the misuse classification table;
7. a lifecycle analysis for operation identity, deadlines, durable `ForEach`, leasing, and DAG;
8. a task 3.1-3.10/3.11a-3.11d guard-coverage matrix plus task 3.12 gate assessment;
9. an explicit answer to whether any unresolved decision still blocks guard implementation;
10. the exact validation commands/results you ran.

Write the review only to:

`docs/review/developer-facing-interface-v1-simplification-review-2026-07-19.md`

Do not implement, edit, reformat, or create any other repository file. Judge the current planning
contract and whether it is ready for the next guard-only packet.
