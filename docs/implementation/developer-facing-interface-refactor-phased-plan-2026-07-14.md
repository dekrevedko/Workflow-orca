# Developer-facing interface refactor: phased implementation and review plan

**Date:** 2026-07-14

**Status:** Ready for implementation after plan approval

**Primary change:** [`reshape-developer-facing-interfaces`](../../openspec/changes/reshape-developer-facing-interfaces/)

**Coordinated change:** [`add-runtime-concurrency-limits`](../../openspec/changes/add-runtime-concurrency-limits/)

**Normative capability matrix:** [`17-selected-mode-capability-matrix.md`](../specs/17-selected-mode-capability-matrix.md)

**Phase 0 kickoff:** [`developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md`](developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md)
**Plan review remediation:** [`developer-facing-interface-phased-plan-review-remediation-2026-07-15.md`](../review/developer-facing-interface-phased-plan-review-remediation-2026-07-15.md)

## 1. Outcome and execution stance

Implement the final developer-facing OrcaCore API directly on the accepted structured-fiber
runtime. The repository is greenfield: existing source shapes are provisional, there are no
external API clients to preserve, and no compatibility shim, obsolete alias, dual execution
path, or parallel public surface is permitted.

The work is divided into eleven dependency-ordered phases. Every phase ends at a mandatory
review gate. After publishing the phase implementation report and a copy-ready reviewer
prompt, implementation pauses. The next phase does not begin until the review verdict and all
required remediation are resolved.

The design permits hosting/provider work to proceed in parallel with DAG/saga closure after
facade composition. This plan deliberately serializes those slices behind separate review
gates. The choice favors isolated public-surface and runtime-semantics review over throughput;
parallel work is allowed only inside a phase when write ownership and verification evidence
remain independent, and it does not bypass the phase review order.

The OpenSpec task lists remain the traceability source of truth. This document controls
execution order, evidence, and review boundaries; it does not replace the requirements or task
checkboxes.

## 2. Rules that apply to every phase

1. Work only in the repository-root implementation: `src/`, `tests/`, `samples/`,
   `benchmarks/`, `OrcaCore.slnx`, and root documentation.
2. Apply the canonical requirement amendments named by the relevant OpenSpec `x.0` task
   before changing source for that section.
3. Execute implementation in bounded TDD packets. Each packet starts with a test that fails
   for the intended contract reason, implements the minimum final shape, refactors, and reruns
   every affected suite.
4. Keep packets within the repository task protocol target of about ten files and 500 changed
   lines. Split a larger OpenSpec task into named packets without changing its acceptance
   meaning. Check the OpenSpec task only after all packets and verification for that task pass.
   Phase 0 is the explicit exception to ordinary green semantics: verification passes when the
   guard infrastructure builds and the guard fails for its intended contract reason, with the
   exact result recorded in the expected-red ledger. Checking a Phase 0 task means its guard is
   proven to detect the approved gap; it does not claim the product behavior is already green.
5. Treat public compile fixtures, package-consumer fixtures, provider-author fixtures, public
   signature baselines, architecture tests, samples, and provider certification as product
   tests. Internal unit tests alone cannot complete a developer-facing task.
6. Delete a provisional path as soon as its final replacement is green. Do not carry an adapter
   into a later phase merely to reduce repository edits.
7. Preserve unrelated working-tree changes. Record the exact phase diff and commits in the
   phase report.
8. Report exact passed, failed, and skipped counts for every executed suite. A skipped or
   environment-gated test requires a reason and the command needed to run it.
9. Run strict OpenSpec validation and Markdown/link checks whenever the phase changes
   requirements or documentation. Run `git diff --check` in every phase.
10. Stop after the phase review request. A review gate is not satisfied by self-attestation
    from the implementation session.
11. Whenever production code in an `OrcaCore.Engine.*` assembly changes, reproduce the
    CI-equivalent coverage collection/report and prove aggregate engine line coverage remains
    at or above 80%. Record the measured value and command in the phase report; this is a hard
    CI gate, not a review-only guideline.
12. Phase 0 records the live verification baseline. The planning reference is 107 integration
    tests passed, 0 failed, and 1 intentionally skipped (`INT_JS_018`), plus the post-fiber
    verification matrix at 1,218 passed, 0 failed, and 1 skipped. Later phase reports compare
    against the Phase 0 baseline and explain every intentional count change rather than
    assuming these planning-time counts remain fixed.

## 3. Phase map

| Phase | Outcome | OpenSpec task coverage | Review proves |
|---|---|---|---|
| 0 | Public and consumer guard baseline | reshape 3.1-3.11 | The tests describe the intended external contracts and fail only for known current gaps |
| 1 | Final mode-first authoring surface | reshape 4.0-4.14 | Builders and definitions expose only mode-valid capabilities; execution IR is absent from the application API |
| 2 | Structural durable effects | reshape 5.0-5.6; partial 5.8 | External jobs and durable leases are structural fiber nodes; portable results contain no durable-only outcomes |
| 3 | Durable transient governance | reshape 5.7 and completion of 5.8; concurrency 3.1-5.2 | Every durable host enforces the same transient-pool semantics, including restart re-admission |
| 4 | Package and interface tiers | reshape 6.0-6.10 | Application, provider-authoring, runtime-protocol, hosting, and engine dependency directions are exact |
| 5 | Complete durable application facade | reshape 7.0-7.8 | An application can register, start, route events, and report every external-job outcome without raw protocol use |
| 6 | Unified management surface | reshape 7.9-7.15 | Ephemeral and durable management share application vocabulary while retaining honest mode-specific capabilities |
| 7 | Runtime-owned DAG and saga progression | reshape 8.0-8.9 | Durable orchestration resumes, compensates, and recovers without caller-owned protocol state |
| 8 | Explicit hosting, providers, and observability | reshape 8.10-8.15 | Host composition and provider roles are explicit, validated, certifiable, and minimally packaged |
| 9 | Golden developer journeys and documentation | reshape 9.1-9.8 | Every documented journey runs using only the packages and API intended for its audience |
| 10 | Final certification and removal proof | reshape 10.1-10.10 | All suites, packages, samples, signatures, dependency rules, and absence scans pass with traceable evidence |

## 4. Phase details

### Phase 0 - Capture public and consumer guards

**Goal:** Turn the approved design into executable external-consumer constraints before
changing the implementation.

**Steps:**

1. Add a tier-aware public-signature inspection harness and declaration-uniqueness guards.
2. Define clean consumer and provider-author fixture harnesses without referencing packages
   that have not been created yet.
3. Add positive and negative compile fixtures for root and nested authoring capabilities.
4. Add state-copy, wait-projection, golden-sample, routing, split-host, facade-outcome, and
   durable-lease lifecycle guards.
5. Record each expected red result by guard ID, command, and intended missing behavior.

**Verification:** Build every new harness; run each guard independently; run the unaffected
baseline suites; prove that no failure comes from fixture setup, missing restore inputs, typos,
or an incorrect expected signature.

**Exit criteria:** Tasks 3.1-3.11 are implemented; every intended gap has one or more
executable guards; the red ledger contains no unexplained failure. This is the only phase
whose review package may contain intentionally red contract guards. The normal build and
unaffected suites must remain green, and the red ledger must shrink monotonically afterward.
For Phase 0 task checkboxes, a verified expected-red guard is a passing deliverable under Rule
4 even though the product contract it protects remains red.

**Review focus:** Consumer realism, correctness of desired signatures, completeness of
negative capability tests, split-host semantics, and whether any guard accidentally encodes
the provisional implementation instead of the final contract.

### Phase 1 - Consolidate mode-first authoring and hide execution IR

**Goal:** Make the workflow, saga, and DAG authoring surface capability-safe, immutable, and
free of executable compiler details.

**Steps:**

1. Apply the section-4 canonical requirement amendments.
2. Introduce distinct immutable ephemeral and durable workflow definition types and matching
   registration boundaries.
3. Internalize compiled plans, instructions, scopes, branches, policies, and compiler identity
   indexes; expose only authored metadata and stable diagnostics.
4. Replace compiler-shaped configuration with application-facing authoring options; move
   engine budgets to hosting/runtime options.
5. Split root and nested public builders by selected mode while sharing the implementation
   graph and compiler internally.
6. Replace remaining wait helpers and all repository authoring call sites with the final
   mode-first surface.
7. Delete the mixed-mode builder, inferred-mode/fallback path, definition retry surface, and
   unused definition policy state.
8. Standardize `Build()` and `TryBuild()`, argument validation, graph diagnostics, immutable
   metadata, and strong-ID/version validation.
9. Add explicit ephemeral saga authoring; keep durable saga authoring absent until Phase 7.
10. Turn the Phase 0 authoring, definition, IR-leak, and identifier guards green and approve
    the resulting public signature baseline.

**Verification:** Authoring unit suites; positive and negative compile fixtures; public
signature and IR-leak guards; repository call-site scans; tests proving metadata immutability,
local versus aggregate validation, and invalid-ID rejection; affected samples and benchmarks.

**Exit criteria:** Tasks 4.0-4.14 are complete; no application signature exposes compiled IR;
no alternate builder or fallback executor remains; root and nested capability matrices match
the normative selected-mode matrix.

**Review focus:** External discoverability, symmetry of root/nested builders, diagnostic
quality, absence of implementation vocabulary, and proof that repository rewrites did not
motivate a compatibility layer.

### Phase 2 - Complete structural durable effects

**Goal:** Express durable external jobs and resource leases as first-class structured-fiber
nodes with exact ownership and recovery semantics.

**Steps:**

1. Apply the section-5 canonical requirement amendments.
2. Implement typed external-job nodes with serializer-aware identity, payload, result, and
   completion/failure/timeout policies.
3. Implement durable resource-lease nodes owned by the requesting fiber/scope with
   deterministic release and crash-recovery expiry.
4. Lower both node families directly into the existing compiled fiber plan and resume their
   exact owners.
5. Rewrite repository tests and call sites, then remove durable-only outcomes from portable
   `StepResult`.
6. Rename ephemeral pool authoring to the approved transient-pool vocabulary; retain durable
   surface absence and compiler defense until Phase 3 completes.
7. Keep per-step throttles in host policy and turn all structural-effect, portable-result,
   nested-capability, and lease-lifecycle guards green.

**Verification:** Compiler/lowering tests; durable restart tests; cancellation, failed-scope,
terminal-transition, and expiry tests; compile-absence fixtures; scans for removed portable
results and old pool vocabulary.

**Exit criteria:** Reshape tasks 5.0-5.6 and the Phase-2 portion of 5.8 pass; the structural
nodes work across restart; portable contracts cannot express durable-only effects. Task 5.7
and durable transient-pool authoring remain explicitly pending for Phase 3.

**Review focus:** Fiber/scope ownership, determinism, replay safety, serializer boundaries,
effect-policy ergonomics, and deletion ordering.

### Phase 3 - Complete durable transient governance

**Goal:** Make named transient pools and execution throttles honest, enforceable host
capabilities across every supported durable host.

**Steps:**

1. Define durable host options for advancement, per-step execution, named transient pools,
   wait/fail-fast behavior, and optional timeout without using lease vocabulary.
2. Enforce durable advancement and step-body limits while keeping pump and infrastructure
   dispatch capacity explicit and separate.
3. Persist only blocked-fiber admission obligations; reset host-local capacity and re-evaluate
   exact-owner admission after restart.
4. Prove N-over-K saturation, sibling progress, cancellation/failure release, fairness, and
   restart behavior across supported hosts.
5. Expose durable transient-pool authoring only after all enforcement/restart evidence passes.
6. Add stable metrics/debug counters and operator documentation that distinguish throttles,
   transient pools, and durable leases.
7. Strict-validate both coordinated changes and finish reshape tasks 5.7 and 5.8.

Phase 3 documentation defines semantics and operator behavior using the approved role
vocabulary. Package and extension names finalize in Phases 4 and 8, so Phase 9 performs an
explicit consistency pass and updates those documents without reopening Phase 3 semantics.

**Verification:** Concurrency change tasks 3.1-5.2; durable and cross-mode governance suites;
structured scheduler and per-instance serialization regressions; restart tests; metrics
contract tests; selected-mode compile fixtures.

**Exit criteria:** Every remaining `add-runtime-concurrency-limits` task and reshape 5.7-5.8
are complete; durable authoring exposes no host capability that a supported host can omit;
restart never treats stale host-local slot ownership as durable truth.

**Review focus:** Taxonomy clarity, capacity ownership, starvation/fairness, infrastructure
exclusions, restart semantics, and cross-change consistency.

### Phase 4 - Establish package and interface tiers

**Goal:** Make package selection and dependency direction communicate the intended audience
without exposing engine internals to applications or provider authors.

**Steps:**

1. Apply the section-6 canonical requirement amendments.
2. Create or rename the application contracts/authoring, ephemeral engine, durable engine,
   hosting, provider-authoring, runtime-protocol, focused observability, and small `OrcaCore`
   meta-packages.
3. Instantiate and run clean application consumer fixtures for minimal ephemeral, in-memory
   durable, provider-backed durable, and meta-package journeys.
4. Instantiate the provider-author fixture with only the declared provider-authoring and
   runtime-protocol dependency.
5. Move application, protocol, and provider contracts to their final tiers. As part of task
   6.4, consolidate the existing duplicate query/statistics/destructive-confirmation model
   declarations into exactly one application-tier declaration each before approving package
   signatures; do not move duplicate declarations as separate public contracts.
6. Rewrite project, package, sample, test, and solution references to the permitted graph.
7. Add exhaustive allowed-edge and forbidden-edge architecture tests.
8. Approve public type/signature baselines for every package and prove minimal dependency
   closure, including optional observability packages.

**Verification:** Clean restore/build in isolated consumer fixtures; package dependency
inspection; architecture tests for every edge; public signature baselines; application-public
signature leak guards; pack smoke tests for packages created in this phase.

**Exit criteria:** Tasks 6.0-6.10 are complete; every package has a named audience and external
scenario; application packages have no advanced references; `Runtime.Protocol` cannot depend
on `Provider.Abstractions`; provider authors require no engine implementation package.

**Review focus:** Names, package ergonomics, dependency closure, public type placement,
provider-authoring sufficiency, canonical management-model placement, and whether the
meta-package remains small.

### Phase 5 - Complete the durable application facade

**Goal:** Let an application perform every ordinary durable workflow operation through a
typed facade with explicit registration and correct split-host continuation behavior.

**Steps:**

1. Apply the section-7 canonical requirement amendments.
2. Add a hosting-owned durable composition root; remove public construction that requires
   processors, registries, provider serializers, driver budgets, or observers.
3. Implement explicit host-scoped durable definition registration returning typed handles.
4. Implement typed start and event delivery, including payloadless events and the complete
   instance/definition/correlation routing matrix.
5. Implement external-job complete, timeout, and worker-reported failure operations with
   application-owned time/identity, serialization, deduplication, and stable results.
6. Pair each accepted result atomically with continuation handoff; progress inline only when
   the definition is registered locally.
7. Place supported raw commands and stream-version operations solely in the certified
   runtime-protocol seam.

**Verification:** Facade unit/acceptance suites; no-match, ambiguous, live-unmatched, paused,
unregistered, duplicate, failure, timeout, and race outcomes; two-host continuation tests;
custom-host certification; public signature and application dependency guards.

**Exit criteria:** Tasks 7.0-7.8 are complete; relevant Phase 0 facade and split-host guards
are green; the ordinary application journey uses no raw protocol type; accepted outcomes are
never stranded without continuation.

**Review focus:** API ergonomics, registration lifecycle, routing result semantics,
idempotency, atomic commit/handoff, split-host correctness, and the advanced-seam boundary.

### Phase 6 - Align ephemeral and durable management

**Goal:** Provide one coherent asynchronous management vocabulary with honest mode-specific
capabilities and application-safe durable remediation.

**Steps:**

1. Introduce canonical management roots, selections, and instance handles over the
   application-tier query/statistics/confirmation models established in Phase 4; delete any
   remaining duplicate public declarations.
2. Adapt ephemeral management while retaining its in-memory eviction, stuck detection, and
   lifecycle-event capabilities.
3. Implement durable handles with detached typed root state, authored active-wait projections,
   lifecycle operations, history, and stable state/capability diagnostics.
4. Route every durable mutation through the runtime-owned lane; remove per-call processor
   creation, caller timestamps, and caller protocol identifiers.
5. Implement compare-and-act poison remediation with a stable opaque ticket and continuation
   after accepted rearm.
6. Remove always-throwing overloads and use a safe `None = 0, Confirmed = 1` confirmation only
   for broad termination and purge.
7. Turn all remaining routing, remediation, management-lane, projection, confirmation, and
   cross-mode query fixtures green.

**Verification:** Cross-mode management contract suites; detached-copy and incompatible-state
tests; authored-wait projection leak guards; mutation concurrency/race tests; stale-ticket
tests; destructive confirmation tests; declaration and public-signature guards.

**Exit criteria:** Tasks 7.9-7.15 are complete; management exposes one declaration per canonical
model; application queries reveal no fiber routing identity; no application mutation accepts a
protocol sequence/version, command ID, or timestamp.

**Review focus:** Vocabulary consistency, capability honesty, copy/serialization semantics,
mutation ordering, stale-remediation safety, error stability, and destructive-operation scope.

### Phase 7 - Close durable DAG and saga loops

**Goal:** Move durable orchestration progression, restart recovery, and compensation ownership
fully into the runtime.

**Steps:**

1. Apply the section-8 canonical requirement amendments.
2. Make DAG planning pure and add a durable facade that accepts only a validated plan and root
   identity.
3. Reconstruct DAG progress from committed state, schedule ready children idempotently, and
   resume after restart without caller progress sets, command IDs, or timestamps.
4. Implement explicitly registered durable saga forward execution and committed audit through
   the structured driver.
5. Implement reverse compensation, compensation failure, restart recovery, and manual
   remediation transitions.
6. Add `DurableSagaDefinition<TState>` authoring only after runtime progression and
   compensation tests pass.
7. Delete the provisional durable saga adapter and caller-driven DAG scheduling surface.

**Verification:** DAG restart/duplicate/in-flight/blocked/terminal tests; saga success,
timeout, compensation, compensation-failure, restart, and remediation tests; management audit
tests; scans for caller-owned progression identifiers and removed adapters.

**Exit criteria:** Tasks 8.0-8.9 are complete; both durable orchestration models progress after
restart through runtime-owned loops; application APIs contain no caller-managed protocol state.

**Review focus:** Durable state reconstruction, idempotency, compensation order, failure and
manual-remediation semantics, audit visibility, and authoring/runtime symmetry.

### Phase 8 - Make hosting, providers, and observability explicit

**Goal:** Make host mode, durability expectations, provider roles, validation, ownership, and
optional telemetry obvious at composition time.

**Steps:**

1. Implement explicit ephemeral and durable hosting registration and remove or give a single
   precise meaning to ambiguous all-in-one registration.
2. Mark in-memory durable hosting as development/test-only and return a stable diagnostic when
   restart durability is expected.
3. Add startup validation for required store, projection, outbox, continuation, timer,
   serializer, and worker capabilities.
4. Normalize PostgreSQL and SQL Server durable-store registration, Redis projection-cache
   registration, RabbitMQ dispatcher registration, and equivalent ZeroMQ dispatcher
   registration.
5. Extend provider certification for roles, registration order, replacement, resource
   ownership, and unsupported-capability diagnostics.
6. Internalize concrete hosted loops, converters, and unsupported public implementation
   types; split optional console, OTLP, and Prometheus dependencies as designed in Phase 4.

**Verification:** Hosting composition and startup-validation tests; package consumer fixtures;
provider certification; PostgreSQL/SQL Server restart tests; RabbitMQ/ZeroMQ dispatcher tests;
registration-order/replacement tests; package dependency inspection and public baseline scans.

**Exit criteria:** Tasks 8.10-8.15 are complete; every registration method names one mode or
provider role; miscomposition fails at startup with a stable diagnostic; concrete hosted loops
are not public application extension points.

**Review focus:** Discoverability, safe defaults, validation timing, provider ownership,
replacement semantics, cross-provider consistency, and dependency cost.

### Phase 9 - Rewrite golden developer journeys and documentation

**Goal:** Make the final API understandable and executable from each supported audience's
documentation alone.

**Steps:**

1. Rewrite the durable sample around explicit registration and a live external job, including
   duplicate reporting and final-state assertions.
2. Add a split-host sample where the callback host has no definitions and the continuation
   owner progresses completion and failure exactly once.
3. Update every sample to use only documented application, hosting, and role-specific provider
   packages.
4. Publish the final capability matrix and update README, developer guides, production notes,
   durable-driver requirements, sample READMEs, and concurrency operator guidance.
5. Document typed state cost/availability, authored wait projections, stable errors,
   confirmation versus authorization, provider ownership, and concurrency lifetimes.
6. Add executable provider-authoring and custom-host/runtime-protocol guides backed by clean
   certification fixtures.

**Verification:** Build and run every sample under assertions; compile every documentation
snippet or its source fixture; check all relative links; scan application journeys for
`Poisoned`, implicit registration, unsupported capability, raw protocol, and internal package
use; strict-validate both active changes.

**Exit criteria:** Reshape tasks 9.1-9.8 are complete; the concurrency documentation completed
in Phase 3 remains consistent with the final packages and samples;
each documented journey is executable with only its documented package references; application
quick starts remain separate from provider and custom-host material.

**Review focus:** First-use ergonomics, conceptual ordering, package selection, error guidance,
sample realism, consistency with public signatures, and absence of implementation vocabulary.

### Phase 10 - Certify the final surface and prove removals

**Goal:** Produce release-quality evidence that the final API, packages, runtime behavior,
documentation, and removal claims agree.

**Steps:**

1. Run all unit, acceptance, hosting, management, provider, restart, dispatcher, DAG, saga,
   split-host, durable-effect, and concurrency suites in the required safe order.
2. Run the same coverage collection and `+OrcaCore.Engine.*` report used by CI and enforce the
   0.80 minimum line rate, recording the measured rate and report artifact.
3. Pack every documented package and build/run clean application, provider-author, and custom
   host consumers exclusively from packed artifacts.
4. Review every public-signature baseline change and exact project/package dependency edge.
5. Run all samples and high-value scenario matrices under automated assertions.
6. Scan the repository for every removed builder, fallback path, portable durable outcome,
   implicit registration path, raw application remediation operation, duplicate declaration,
   public hosted implementation, alias, and parallel provisional path.
7. Strict-validate both OpenSpec changes, compare modified requirement headings with canonical
   specifications, and link every changed requirement to executable evidence.
8. Refresh implementation-status and review-disposition documents with exact counts, skipped
   reasons, packed-consumer results, measured engine coverage, the final public surface
   baseline, and a reconciliation against the live baseline captured in Phase 0.

**Verification:** All commands required by reshape tasks 10.1-10.10. Container-backed suites
run sequentially when shared infrastructure requires it; any environment-gated command is
recorded verbatim and cannot be represented as passed. The Phase 0 live baseline—not an
unverified historical count—is the comparison point. At plan approval, the repository's known
integration reference is 107 passed / 0 failed / 1 skipped (`INT_JS_018`), and the post-fiber
matrix is 1,218 passed / 0 failed / 1 skipped.

**Exit criteria:** Tasks 10.1-10.10 are complete; both changes strict-validate; build and all
required tests pass; packed consumers and samples pass; absence scans are clean; every public
type is justified by an external scenario. Archiving is a separate explicit decision after the
final review.

**Review focus:** Independent reproduction, exact counts, packed-artifact behavior, public
surface necessity, canonical/OpenSpec traceability, absence proof, and archive readiness.

## 5. Mandatory phase review protocol

At the end of every phase, perform these actions in order:

1. Stop source implementation and leave the next phase untouched.
2. Check only OpenSpec tasks whose full implementation and verification are complete.
3. Create a detailed report at
   `docs/review/developer-facing-interface-phase-<NN>-<slug>-implementation-status-<YYYY-MM-DD>.md`.
4. Create a filled reviewer prompt at
   `docs/review/developer-facing-interface-phase-<NN>-<slug>-reviewer-prompt-<YYYY-MM-DD>.md`
   using [`developer-facing-interface-phase-review-template.md`](../review/developer-facing-interface-phase-review-template.md).
5. Send the user a review request containing the implemented outcomes, important removals,
   exact verification counts, known deviations, report link, prompt link, and explicit review
   focus.
6. Wait for an independent review verdict. Do not begin the next phase while review is pending.
7. Apply required review changes, add regression coverage for each actionable finding, refresh
   the report and verification evidence, and request re-review when required.

### Required implementation report contents

Every phase report must contain:

- phase objective, OpenSpec task IDs, and final task disposition;
- external developer journey now enabled or made safer;
- public API additions, removals, moves, and signature changes;
- implementation summary by project and file;
- deleted provisional paths and repository-wide absence-scan results;
- tests written first, their initial expected failures, and how they became green;
- exact commands with passed, failed, skipped, and environment-gated counts;
- the Phase 0 baseline comparison and, whenever an engine assembly changed, the measured
  `OrcaCore.Engine.*` line-coverage rate against the hard 0.80 CI minimum;
- public-signature, package, and dependency-graph differences;
- canonical specification, OpenSpec, sample, and documentation changes;
- deviations from this plan, design decisions made, known risks, and remaining dependencies;
- commits/diff scope and unrelated working-tree changes explicitly excluded;
- focused reviewer questions and the criteria for approving the phase.

### Review verdict policy

- **Approve:** record the verdict and begin the next phase.
- **Approve with changes:** remain in the current phase until every required change is applied
  and the report is refreshed. Request re-review for changes that affect public contracts,
  architecture, durable semantics, or the reviewer's verdict basis.
- **Reject:** reopen the phase plan and do not advance.
- Severity P0/P1 findings must be fixed and re-reviewed.
- Severity P2 findings must be fixed before advancing unless the reviewer and user explicitly
  accept a concrete later task with no contract or dependency impact on the next phase.
- Severity P3 findings may be recorded for a named later phase only when they cannot conceal a
  correctness, API, or traceability problem.

### Review request message shape

```text
Phase <NN> - <name> is complete, and implementation is paused for review.

Implemented:
- <external outcome>
- <important API additions/removals>

Verification:
- <command>: <passed>/<failed>/<skipped>
- <command>: <passed>/<failed>/<skipped>

Known deviations or remaining phase-local concerns:
- <none, or precise items>

Review materials:
- Implementation report: <link>
- Copy-ready reviewer prompt: <link>

Please review especially: <focused questions>. I will not start Phase <NN+1> until the
verdict and required remediation are resolved.
```

## 6. Progress accounting

The phase report is the human review record; the OpenSpec task files are the machine-readable
completion record. Counts must agree. A phase cannot be reported complete when a mapped task
is unchecked, except where this plan explicitly splits a task across adjacent phases; such a
task remains unchecked until its final phase portion passes.

The current starting point is planning-complete and source-not-started for this refactor:
reshape sections 1-2 are accepted, reshape sections 3-10 remain implementation work, and the
durable portion of `add-runtime-concurrency-limits` remains coordinated Phase 3 work.
