# Structured Fiber Execution Decision Review - 2026-07-13

Reviewer scope: complete architecture, requirements, feasibility, and
implementation-readiness review of the `adopt-structured-fiber-execution`
OpenSpec change against the promoted root implementation, the canonical
requirements under `docs/specs/`, the OpenSpec baselines, and the concurrent
`add-runtime-concurrency-limits` change. Review-only; no decision document,
source file, or test was modified.

## Verdict

**REQUEST CHANGES**

Implementation (`/opsx:apply`) may NOT begin yet. The architecture itself is
sound — no P0 was found, the fiber/scope/merge model is coherent, feasible in
C#, and clearly superior to the cursor split/merge model it replaces — but
three P1 gaps block safe implementation: the `ForEach` primitive is entirely
unaddressed by the package, the concrete canonical-spec contradictions the
proposal itself says "must be reconciled before either implementation begins"
are not enumerated anywhere, and the deterministic minting rule for runtime
`ScopeId`/`FiberId` instances across loop iterations, replay, and duplicate
continuation claims is unspecified. All required changes are document-only;
none invalidates a design decision.

Counts: **0 × P0, 3 × P1, 6 × P2, 4 × P3.**

## Findings

### F-01 (P1, CONFIRMED) — `ForEach` is completely unaddressed by the decision package

- **Decision/spec:** proposal.md "What Changes" and design.md Decisions 3, 4, 7
  redefine every *local branch construct* as an isolated-input, typed-result
  fiber scope and remove detached local work, but never mention `ForEach`.
  tasks.md has no `ForEach` task in any section (2–12). None of the nine spec
  deltas names it.
- **Code/spec evidence:** `ForEach` is a fully implemented ephemeral primitive:
  builder surface [WorkflowBuilder.cs:203](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L203),
  nodes [Nodes.cs:136](../../src/OrcaCore.Core/Definitions/Nodes.cs#L136),
  runner/scheduler [ForEachNodeRunner.cs](../../src/OrcaCore.Engine.Ephemeral/Execution/ForEachNodeRunner.cs),
  [ForEachWorkScheduler.cs:127](../../src/OrcaCore.Engine.Ephemeral/Execution/ForEachWorkScheduler.cs#L127)
  (honors `ForEachResidualPolicy.CancelRemaining` vs `LetRemainingComplete`),
  step-context surface [StepContext.cs:45](../../src/OrcaCore.Abstractions/Steps/StepContext.cs#L45).
  It is canonical: CP-010..013 in
  [08-requirements-composition.md:45](../../docs/specs/08-requirements-composition.md#L45)
  and AC-601..605 in [12-acceptance-criteria.md:242](../../docs/specs/12-acceptance-criteria.md#L242).
- **Why it matters:** `ForEach` bodies run business steps against the *shared
  mutable parent `TState`* (CP-010 declares it resultless; the parent observes
  status, not values) and `WhenAny + LetRemainingComplete` creates exactly the
  detached local work Decision 7 removes. If `ForEach` lowers to the compiled
  plan, it violates Decision 4 (isolated branch input, typed result) and
  Decision 7 (no detached local fibers). If it does not lower, the positive
  instruction allowlist (Decision 1) rejects every existing `ForEach`
  definition and task 5.5 ("remove or bypass the old ephemeral
  parallel/WhenFirst runners") leaves the old shared-state interpreter alive
  for `ForEach` alone — precisely the dual execution surface Decision 10
  rejects for the durable engine. Either way the package currently gives an
  implementer no answer.
- **Required change:** add an explicit `ForEach` decision to design.md and a
  matching `workflow-authoring`/`structured-fiber-execution` delta: either
  (a) compile `ForEach` to a fiber scope kind with per-item isolated input and
  a status-style result, redefining or removing `LetRemainingComplete`, or
  (b) exclude and retire/park `ForEach` (with the CP-010..013 and AC-601..605
  amendments that implies).
- **Requirement/task to add:** a normative `ForEach` requirement plus at least
  one scenario; ordered tasks in sections 2 (compiler acceptance/rejection),
  5 (ephemeral behavior), and 11 (canonical doc updates).

### F-02 (P1, CONFIRMED) — Canonical-spec contradictions are not enumerated, and task 1.2 names only the concurrency change

- **Decision/spec:** proposal.md Impact: "This change intentionally supersedes
  … the future-concurrency wording in `add-runtime-concurrency-limits`; review
  must reconcile that change before either implementation begins."
  tasks.md 1.2 reconciles only "cooperative local-fiber semantics" with that
  change "and the affected canonical requirements" — without naming them.
  Task 11.4 defers "update canonical requirements" to after implementation.
- **Evidence — direct contradictions the package must name and amend:**
  - [08-requirements-composition.md:33](../../docs/specs/08-requirements-composition.md#L33)
    CP-004: `WhenFirst` residual policy SHALL offer "cancelled, ignored, or
    allowed to finish"; the workflow-authoring delta
    ([spec.md:46](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/workflow-authoring/spec.md#L46))
    rejects ignore/let-remaining. AC-205 and DR-AC-032
    ([16-requirements-durable-driver.md:556](../../docs/specs/16-requirements-durable-driver.md#L556))
    verify the removed policies.
  - [07-requirements-saga.md:29](../../docs/specs/07-requirements-saga.md#L29) SG-010:
    default compensation order is "reverse successful-completion order,
    overridable explicitly per scope"; the saga delta
    ([spec.md:13](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/saga-orchestration/spec.md#L13))
    replaces sibling ordering with reverse canonical authored order and is
    silent on the per-scope override. AC-403 cites SG-010.
  - [04-requirements-core-runtime.md:87](../../docs/specs/04-requirements-core-runtime.md#L87)
    CR-015 mandates execution position "as a call-stack of frames", and DR-012
    ([16-requirements-durable-driver.md:145](../../docs/specs/16-requirements-durable-driver.md#L145))
    normatively requires persisting "the CR-015 frame stack". Envelope
    format 2 (design.md Decision 10) replaces the frame stack with linear
    fiber positions plus scope records. DR-011/DR-011a command carriage
    wording is likewise cursor-envelope-shaped.
  - [13-phasing-and-open-questions.md:203](../../docs/specs/13-phasing-and-open-questions.md#L203)
    records a *normative conflict resolution*: "branches MAY execute
    concurrently (with serialized commits); earlier interim behavior
    (coordinated sequential branches) is not the specified target", echoed by
    CP-001 and CR-044. The design reverses this to cooperative-fiber
    semantics; the recorded resolution must be explicitly superseded, not
    left standing.
  - [add-runtime-concurrency-limits/design.md:56](../../openspec/changes/add-runtime-concurrency-limits/design.md#L56)
    Decision 4 plans pool acquisition for concurrently scheduled local
    branches, and its default saturation policy is an in-memory async wait —
    the runtime-resource-governance delta
    ([spec.md:21](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/runtime-resource-governance/spec.md#L21))
    instead requires committing a blocked obligation and ending the quantum.
    Which behavior applies to *transient in-process pools* in the ephemeral
    engine is unresolved.
- **Why it matters:** the proposal's own gate ("reconcile before either
  implementation begins") cannot be verified or completed when the conflicts
  live only in reviewers' heads. Left implicit, implementers will honor DR-012
  or CP-004 verbatim and diverge from the fiber model, or vice versa.
- **Required change:** extend task 1.2 (or add task 1.2a) to enumerate exactly
  the conflicting IDs above (CP-001, CP-004, CR-015, CR-044, SG-010, 13.4
  `Parallel` resolution, DR-011/011a/012, AC-204/205, AC-403, DR-AC-032, and
  the `add-runtime-concurrency-limits` design §3/§4) with the intended
  amendment for each, and state precedence (this change supersedes) in
  proposal.md Impact.
- **Requirement/task to modify:** tasks.md 1.2; proposal.md Impact list.

### F-03 (P1, CONFIRMED) — Deterministic runtime identity minting for scopes and fibers is unspecified

- **Decision/spec:** design.md Decision 6: "Fiber identities derive from scope
  identity and authored branch index, not allocation timing." Decision 1 gives
  the *plan* stable `ScopePlanId`/`BranchId`. Nothing defines how a *runtime*
  `ScopeId` instance is minted when the same `StartScope` instruction executes
  repeatedly — a `WhenAll` inside a `While` body creates a new scope every
  iteration, and after `ContinueAsNew` the same instruction re-executes in a
  new generation. tasks.md 3.2 ("Implement shared `FiberId`, `ScopeId` …")
  contains no determinism rule; no spec delta scenario covers scope re-entry.
- **Code evidence:** the current model this replaces disambiguates re-entry
  positionally (`LoopIteration` on frames,
  [DurableExecutionEnvelope.cs:183](../../src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs#L183));
  the new envelope field list (design.md Decision 10) has no analogue. The
  event-routing delta scenario "Nested scopes reuse a branch name"
  ([spec.md:26](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/event-routing-and-waits/spec.md#L26))
  covers *name* collisions, not *iteration* re-entry.
- **Why it matters:** durable replay correctness depends on identity
  determinism. After a crash between the scope-creation commit and the next
  advancement, or on a duplicate continuation claim (DR-AC-015 class), the
  replaying host must resolve the *same* `ScopeId`/`FiberId` for the same
  logical iteration; obligations, branch results, and the persisted next-fiber
  position are all keyed by these ids (Decision 8, Decision 10). If ids are
  freshly minted per activation (GUIDs) they must come from the committed
  envelope only — and the rule for the *first* commit of a re-entered scope
  (iteration counter? parent-fiber sequence number? committed allocation
  fact?) is exactly what is missing. An implementer can silently choose
  allocation-timing-dependent ids and produce orphan obligations or duplicate
  scopes under replay.
- **Required change:** add a normative requirement to the
  `structured-fiber-execution` delta: runtime scope and fiber identities SHALL
  be deterministic functions of committed state (e.g., `ScopePlanId` + owning
  fiber id + a persisted per-fiber scope-entry sequence), identical across
  replay, duplicate claims, and host replacement; include a scenario for a
  scope inside a loop crossing a crash boundary.
- **Requirement/task to add:** new requirement + scenario as above; extend
  tasks 3.1/3.2 with an identity-determinism test, and tasks 7.3/7.5 with a
  loop-scope replay crash case.

### F-04 (P2, CONFIRMED) — "Bounded internal instructions" has no normative bound or exceeded-bound behavior

- **Decision/spec:** design.md Decision 6: a quantum is "at most one user step
  invocation plus a bounded number of internal control instructions required
  to reach a stable next position". The `structured-fiber-execution` delta and
  tasks 3.5 repeat "bounded" without a limit, an outcome when the limit is
  hit, or a diagnostic.
- **Code evidence:** the risk is real today: builder validation only requires
  a *non-empty* `While` body
  ([WorkflowBuilder.cs:490](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L490)),
  so a body containing only structural nodes (e.g. an `If` with empty arms)
  under an always-true condition spins through structural transitions with
  zero kernel commands — `MaxCommandsPerSegment` never triggers because no
  command is issued
  ([DurableDriverSegmentRun.cs:143](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L143)
  counts commands only); only the 30 s elapsed deadline ends the hot spin.
  The compiled-plan model (`LoopBack` structural instruction, Decision 14)
  reproduces the same shape.
- **Why it matters:** without a normative internal-instruction budget the
  fairness guarantee ("every continuously runnable fiber receives a turn
  within a bounded number of sibling quanta") is unenforceable for
  structural-only spins, and the behavior at the bound (yield? fail? park?)
  is observable semantics that must not differ between engines.
- **Required change:** add the internal-instruction budget as a normative
  compiled-plan/interpreter requirement with a defined outcome (recommend:
  forced end-of-quantum re-queue, plus a build-time rejection of loop bodies
  with no quantum-ending instruction), and a scenario.
- **Requirement/task to modify:** `structured-fiber-execution` delta
  scheduling requirement; tasks 3.5 (test the bound) and 2.3 (compiler check).

### F-05 (P2, CONFIRMED) — Uncancellable-shape rejection is worded loser-only; `WhenAll` fail-fast and root cancellation need the same rule

- **Decision/spec:** design.md Decision 7: "If a **losing** branch can own
  work that the runtime cannot durably cancel … compilation rejects the
  shape." tasks.md 8.5: "child-owning or otherwise uncancellable **loser**
  shapes." But `WhenAll` fail-fast (Decision 7) cancels *remaining
  descendants* on first branch failure, and Decision 8 uses the same
  traversal for parent cancellation/termination — a `WhenAll` sibling owning
  a child workflow is equally uncancellable today.
- **Evidence:** the spec delta already states the general form —
  [structured-fiber-execution/spec.md:106](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/structured-fiber-execution/spec.md#L106)
  ("a **branch shape** … no durable cancellation or detachment contract →
  compilation rejects") — so design.md and tasks.md are *narrower than their
  own normative spec*.
- **Why it matters:** implementing task 8.5 as written would ship compilation
  that rejects child-owning `WhenFirst` losers but accepts child-owning
  `WhenAll` branches, whose failure path then needs the exact cancellation
  contract that was the reason for rejection — a stranded-instance or
  blocked-scope-completion path (scope "SHALL NOT complete … until every
  non-detached owned obligation is … durably released").
- **Required change:** align design.md Decision 7 wording and task 8.5 with
  the spec's general "branch shape" rule (any branch whose induced
  cancellation set contains undurably-cancellable work, under any join
  policy, is rejected until the protocol exists).
- **Requirement/task to modify:** design.md Decision 7 paragraph; tasks 8.5.

### F-06 (P2, CONFIRMED) — Root `ContinueAsNew` with active scopes has tasks but no normative requirement

- **Decision/spec:** tasks 8.6/8.7 test and implement "root `ContinueAsNew`
  with active scopes, and cleanup before root rollover … so rollover cannot
  silently discard owned work" — but no spec delta contains a requirement or
  scenario defining that semantics (reject? cancel-then-roll? both are
  defensible). The workflow-authoring delta only bans `ContinueAsNew` *inside
  branches*.
- **Code evidence (current defect the tasks respond to):**
  [DurableDriverSegmentRun.Steps.cs:140](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs#L140)
  — `ContinueAsNewAsync` clears **all** cursors and rebuilds a fresh root
  without cancelling sibling cursors' active waits/timers, and nothing
  prevents a *branch* step from returning `ContinueAsNew<TState>`
  ([StepResult.cs:36](../../src/OrcaCore.Abstractions/Steps/StepResult.cs#L36) is
  reachable from any step): committed sibling waits/timers become orphans.
- **Why it matters:** tests written for 8.6 need a specified expected
  behavior; TDD traceability (quality delta requirement) demands the
  requirement exist first. This is also a live P-class defect in the current
  driver worth recording independently (see Gap Map).
- **Required change:** add a `durable-runtime` (or `structured-fiber-execution`)
  requirement: root `ContinueAsNew` SHALL either be rejected while any scope
  is active or SHALL complete the ownership cleanup traversal in/before the
  rollover commit; one scenario for rollover with an active nested scope.
- **Requirement/task to add:** new requirement + scenario; tasks 8.6/8.7 then
  trace to it.

### F-07 (P2, CONFIRMED) — The mandated reference model has no build task

- **Decision/spec:** the quality delta requires "The project SHALL maintain a
  deterministic reference model for fibers and scopes"
  ([quality-and-verification/spec.md:11](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/quality-and-verification/spec.md#L11)),
  and task 12.3 *runs* "seeded reference-model permutations" — but no task in
  sections 2–11 builds the reference model, the definition/schedule
  generators, or the comparison harness.
- **Why it matters:** the reference model is the primary schedule-independence
  oracle for the whole refactor; discovering at 12.3 that it doesn't exist
  collapses the final gate into hand-written examples. It should be built
  alongside the shared model (section 3) so sections 4–10 can use it.
- **Required change:** add an ordered task (recommend after 3.6, before 4.6)
  to implement the reference model + generators + comparison harness, with
  its own failing-first test.
- **Requirement/task to add:** new task in section 3 or 4; 12.3 then traces
  to it.

### F-08 (P2, PLAUSIBLE) — Wait-match determinism is undefined when multiple owner-distinct waits share the same event identity

- **Decision/spec:** the event-routing delta modifies matching to use "the
  declared event identity and correlation **together with the intended owner
  context**" ([spec.md:20](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/event-routing-and-waits/spec.md#L20)).
  An external event carries no fiber identity (EV-001 envelope), so when two
  sibling fibers register waits with identical `(EventName, CorrelationId)`,
  "intended owner" is undecidable from the event; the delta does not say
  whether this is rejected at delivery, matched oldest-first, matched in
  fiber-scheduler order, or forbidden at registration.
- **Evidence:** EV-011 allows duplicate registrations ("wait registration
  always succeeds; no registration-time uniqueness",
  [05-requirements-events-waits-timers.md:32](../../docs/specs/05-requirements-events-waits-timers.md#L32));
  EV-023 requires exactly-once resume. Current durable matching resolves by
  wait record, and the kernel picks a match — the delta's new wording implies
  a stronger guarantee than any rule it states.
- **Why it matters (PLAUSIBLE):** whether this is a defect depends on an
  undecided detail — the deterministic selection rule (or an instance-level
  uniqueness restriction). Whichever is chosen changes observable resume
  order across engines and replay, so it must be written down. Resolved by:
  one sentence choosing the rule + one scenario with two identical sibling
  waits.
- **Required change:** specify deterministic selection (recommend: single
  candidate required per instance, else deterministic registration-commit
  order) in the event-routing delta; add a scenario and a test task under 8.8.

### F-09 (P2, PLAUSIBLE) — Compensation eligibility relative to scope merge is undefined

- **Decision/spec:** design.md Decision 13 orders sibling compensation but
  never states *when* a branch's forward actions become eligible for
  parent-scope compensation: if a `WhenAll` fails after branch A committed
  compensable forward actions but before merge, is A's work covered by the
  failing scope's compensation, and does a *successful* merge transfer
  eligibility to the parent scope? The saga delta
  ([spec.md:13](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/saga-orchestration/spec.md#L13))
  defines ordering "for the covered scope" without defining coverage at the
  merge boundary. SG-003 (compensation scopes) gives no answer for fiber
  scopes.
- **Why it matters (PLAUSIBLE):** saga work is correctly sequenced last
  (Decision 13, tasks section 9), so this does not block sections 2–8; but
  task 9.1 tests cannot be written first without this decision, and the
  review-lens question ("Is the compensation eligibility/commit point relative
  to scope Merge defined?") currently has no documented answer. Resolved by:
  an explicit eligibility rule (recommend: forward records become
  parent-visible at branch-result commit; scope failure covers all committed
  descendant records; merge success transfers coverage upward unchanged).
- **Required change:** add the eligibility/commit-point rule to the saga delta
  with a pre-merge-failure scenario; gate task 9.1 on it.

### F-10 (P3, CONFIRMED) — Executing context of `ScopeJoin`/`ScopeExit` should be stated explicitly

- design.md Decision 14 gives both instructions runtime significance while the
  parent fiber is blocked and children have terminated — no fiber "is at"
  either instruction when they run. The durable delta's atomic-transition
  scenario implies they execute as scope-reducer transitions inside the final
  branch-result / merge commits, but no sentence says so. One clarifying
  paragraph (they are reducer-evaluated at commit boundaries, not
  fiber-quantum instructions; their `InstructionId`s anchor diagnostics and
  continuation targets only) removes an implementable ambiguity in
  tasks 3.6/7.4. (design.md:284-303)

### F-11 (P3, CONFIRMED) — Canonical glossary and named-`End`-outcome contract not reflected

- [03-domain-model-and-glossary.md](../../docs/specs/03-domain-model-and-glossary.md)
  fixes canonical vocabulary; `Fiber`, `ExecutionScope`, `BranchReturn`,
  `Merge`, quantum, and plan fingerprint are absent (task 11.4 is generic —
  name document 03 explicitly). Separately, design.md Decision 2 says business
  outcomes are represented "in typed workflow state or a **future** explicit
  outcome value", but CR-008 named `End` outcomes already exist normatively
  and in code ([Nodes.cs:51](../../src/OrcaCore.Core/Definitions/Nodes.cs#L51),
  [WorkflowBuilder.cs:307](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L307));
  with exactly one root `End`, per-`End` outcome names lose their remaining
  expressive role. State whether CR-008 outcome naming survives (as a value
  computed before `End`) or is amended.

### F-12 (P3, CONFIRMED) — `Parked` in the shared status derivation needs mode-scoping language

- design.md Decision 9 lists `Parked` in the fiber-derived status set, and the
  state-driven-runtime delta says persistence residency is "the only
  mode-specific difference"; DR-017 defines `Parked` as durable-only and the
  ephemeral engine has no parking sites. One sentence scoping `Parked` (and
  fingerprint/envelope diagnostics) to durable mode keeps the parity
  requirement (task 3.7, delta "Runtime modes share execution semantics")
  testable as written. (design.md:199, state-driven-runtime delta:22)

### F-13 (P3, CONFIRMED) — Envelope v2 field list should name loop/scope-entry progress explicitly

- The current envelope persists `LoopIteration` per frame
  ([DurableExecutionEnvelope.cs:183](../../src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs#L183));
  Decision 10's field list has no equivalent. With F-03 resolved via a
  persisted scope-entry/iteration sequence, that counter becomes a required
  envelope field, not a diagnostic; EV-043 (iteration-scoped waits) and
  wait-isolation regressions (task 8.8) also benefit from it being explicit.
  Fold into the F-03 amendment.

## Decision Checklist

Every checkbox in design.md → Review Checklist:

| # | Checklist item | Ruling | Reason |
|---|---|---|---|
| 1 | Local `Parallel` = deterministic cooperative fiber execution | **ACCEPT WITH CHANGE** | Correct resolution of the concurrency conflict, but the recorded contrary resolution in 13.4 and CP-001/CR-044 wording must be explicitly superseded (F-02) |
| 2 | Branches cannot mutate parent state; typed serializable results | **ACCEPT WITH CHANGE** | Right call; requires the `ForEach` decision because `ForEach` bodies mutate shared `TState` by canonical design (F-01) |
| 3 | `WhenAll` requires explicit merge in authored branch order | **ACCEPT** | Deterministic, replay-safe, and the only defensible default given no universal auto-merge exists |
| 4 | `WhenAll` initially fail-fast, no partial merge | **ACCEPT** | Strict initial policy with alternate policies deferred is correct scoping |
| 5 | `WhenFirst` = first committed terminal, authored-order ties, cancel losers | **ACCEPT** | "First committed terminal" is the only definition that is deterministic under replay |
| 6 | `Ignore`/`LetRemainingComplete` removed from initial interface | **ACCEPT WITH CHANGE** | Justified (durable driver never implemented them — see Gap Map); CP-004/AC-205/DR-AC-032 must be amended and the `ForEach` residual analogue decided (F-01, F-02) |
| 7 | Exactly one root `Init`/`End`; branches use `BranchReturn` | **ACCEPT** | Eliminates the branch-terminates-instance hazard; current permissive nested-`End` validation confirms the gap is real ([WorkflowBuilder.cs:400](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L400)) |
| 8 | No public `EndIf`/`EndWhile`/`EndParallel`; compiler emits joins/exits | **ACCEPT** | Nested builder delegates already carry the structure; closing tokens would only add malformed states |
| 9 | `ContinueAsNew` is root-only | **ACCEPT WITH CHANGE** | Root-only is right; the rollover-with-active-scopes semantics needs a normative requirement (F-06) |
| 10 | Persisted round-robin scheduler; `Yield` ends the quantum | **ACCEPT WITH CHANGE** | Sound and restart-safe in principle; requires the identity-minting rule (F-03) and a normative internal-instruction bound (F-04) |
| 11 | Explicit obligation ownership; uncancellable shapes fail compilation | **ACCEPT WITH CHANGE** | Generalize the rejection beyond `WhenFirst` losers to any join/cancellation path (F-05) |
| 12 | Envelope format 2 replaces cursor envelopes, no migration/legacy executor | **ACCEPT** | Correct for a library with zero production instances; explicit format diagnostics retained |
| 13 | Registration atomic, binds version + fingerprint | **ACCEPT** | Current catalog/registry split is demonstrably non-atomic ([DurableWorkflowRuntime.cs:74](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L74)); task 2.8 covers it |
| 14 | Engines share compiler/scheduler/scope/join/merge semantics | **ACCEPT** | The `WhenFirst` residual drift between engines (Gap Map, item D-2) proves duplicated semantics do not hold |
| 15 | Saga sibling compensation uses canonical authored order | **ACCEPT WITH CHANGE** | Right for schedule independence; SG-010 must be amended (incl. its per-scope override) and the merge-boundary eligibility defined (F-02, F-09) |
| 16 | DAG execution remains child-instance based | **ACCEPT** | Consistent with resolved open question 15 (2026-07-02); avoids a second scheduler |
| 17 | Test-first; cursor branching deleted after replacement gates | **ACCEPT WITH CHANGE** | Ordering is sound; the reference-model oracle needs its own build task before the gates that use it (F-07) |

## Traceability Matrix

For each of the 14 design decisions:

| D | Design section | Normative requirement(s) | Task(s) | Test project(s) | Current implementation replaced / retained | Mapping status |
|---|---|---|---|---|---|---|
| 1 | Compile to instruction plans | SFE "Definitions compile to stable executable plans"; WC "Executable plan identity is explicit"; WA "Validation covers complete structured reachability" | 2.1–2.8 | OrcaCore.Core.Tests | Replaces builder-only validation ([WorkflowBuilder.cs:333](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L333)) + registration-time capability walk ([DurableDriverCatalog.cs:33](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverCatalog.cs#L33)) | OK |
| 2 | One root entry/exit | WA "Successful workflow flow has one root entry and exit"; WA "Every branch has one branch return" | 2.1, 2.2, 8.6, 8.7 | Core.Tests; Durable.Tests | Replaces permissive `ContainsEnd` nested-`End` acceptance ([WorkflowBuilder.cs:400](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L400)) | OK (F-06 for rollover requirement; F-11 for CR-008 outcome note) |
| 3 | Fibers + recursive scopes | SFE "A fiber advances one linear instruction position"; SFE "Structured scopes preserve the parent fiber" | 3.1–3.6 | Core.Tests | Replaces `DurableDriverCursor` split/join ([DurableDriverSegmentRun.cs:368](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L368)) and ephemeral runners ([ParallelNodeRunner.cs](../../src/OrcaCore.Engine.Ephemeral/Execution/ParallelNodeRunner.cs), [WhenFirstNodeRunner.cs](../../src/OrcaCore.Engine.Ephemeral/Execution/WhenFirstNodeRunner.cs)) | **WEAK** — runtime id minting unspecified (F-03) |
| 4 | Isolated branch state, typed results | SFE "Branches cannot mutate parent business state directly"; WC "Branch data contracts are separate from StepResult"; WC "Branch and merge contracts are type checked" | 4.1–4.3 | Core.Tests; Acceptance.Tests | New — no current enabling contract (all branches share one `TState`, [Nodes.cs:268](../../src/OrcaCore.Core/Definitions/Nodes.cs#L268)) | OK (F-01: `ForEach` exclusion undecided) |
| 5 | Pure replacement-state merge | SFE "Merge is explicit, deterministic, and side-effect free" | 4.4, 4.5; crash edges 7.3, 7.4 | Core.Tests; Durable.Tests | New — no current merge concept (join promotes one cursor, [DurableDriverSegmentRun.cs:526](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L526)) | OK |
| 6 | Persisted round-robin scheduling | SFE "Local fibers use cooperative scheduling"; SDR "Yield is a fiber scheduling operation" | 3.3–3.5, 7.1, 7.7 | Core.Tests; Durable.Tests | Replaces ordinal-first cursor selection ([DurableDriverSegmentRun.cs:159](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L159)) | **WEAK** — internal-instruction bound not normative (F-04) |
| 7 | Strict join/residual policies | SFE "Join policies define one scope outcome"; WA MODIFIED "Parallel authoring defines deterministic join structure" | 4.6, 8.5, 11.2 | Core.Tests; Acceptance.Tests; Durable.Tests | Replaces `WhenFirstResidualPolicy` ([WhenFirstResidualPolicy.cs](../../src/OrcaCore.Core/Definitions/WhenFirstResidualPolicy.cs)) and `WhenFirstJoin` ([WhenFirstJoin.cs:46](../../src/OrcaCore.Engine.Ephemeral/Execution/WhenFirstJoin.cs#L46)) | **WEAK** — loser-only wording (F-05) |
| 8 | Explicit obligation ownership | SFE "Scope ownership covers every residual obligation"; ERW both deltas; RRG "Resource ownership follows fibers and scopes" | 8.1–8.4, 5.4, 8.8 | Durable.Tests; Ephemeral.Tests | Replaces descriptive `BranchScope` string labels ([DurableDriverSegmentRun.cs:795](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L795)) and per-join cleanup ([DurableDriverSegmentRun.cs:543](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L543)) | OK (F-08 match determinism) |
| 9 | Status from fiber runnability | SDR "Instance status derives from aggregate fiber runnability" | 3.7 | Core.Tests; Acceptance.Tests | Replaces instance-wide `Waiting` on any wait registration | OK (F-12 `Parked` scoping) |
| 10 | Envelope format 2, no migration | DR "Durable envelope records the complete structured execution state"; DR "Development refactor does not retain cursor execution"; DR MODIFIED rehydration + version binding | 6.1–6.6 | Abstractions serialization tests; Durable.Tests | Replaces envelope v1 ([DurableExecutionEnvelope.cs:23](../../src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs#L23)) | OK (F-13 iteration field) |
| 11 | Shared core + persistence adapters | SDR "Runtime modes share execution semantics" | 2.5, 3.2–3.6, 5.2, 7.2 | All suites | Replaces duplicated ephemeral/durable orchestration; retains host/processor/aggregate/outbox/provider seams ([DurableWorkflowDriver.cs](../../src/OrcaCore.Engine.Durable/Driver/DurableWorkflowDriver.cs)) | OK |
| 12 | Future true-parallel path | RRG "Local fiber scheduling is distinct from host concurrency" (partially); otherwise a design property | none (intentional) | n/a | n/a | ACCEPTABLE — non-executable rationale; note it is design-only |
| 13 | Saga/DAG on the new model | SO ADDED + MODIFIED requirements | 9.1–9.4 | Durable.Tests; Integration | Extends kernel saga commands; retains `DurableDagRunner` | **WEAK** — merge-boundary eligibility (F-09); SG-010 amendment (F-02) |
| 14 | Structural instructions, no closing nodes | SFE "Compiled plans contain explicit structural continuations"; WA "Fluent blocks do not require authored closing nodes" | 2.4 | Core.Tests | New — current interpreter infers joins from tree/frames | OK (F-10 executor clarity) |

Missing/weak mappings called out: D3 (F-03), D6 (F-04), D7 (F-05), D13
(F-09); reference-model requirement (quality delta) → **no implementing
task** (F-07); `ForEach` → **no decision, requirement, or task at all**
(F-01).

## Current Implementation Gap Map

**Reusable seams (retain; contracts remain valid):**

- Durable host loop, conflict-retry, park/re-arm, drive modes —
  [DurableWorkflowDriver.cs](../../src/OrcaCore.Engine.Durable/Driver/DurableWorkflowDriver.cs)
- Command processor, one-command/one-commit aggregate model, expected-version
  optimistic concurrency (consumed uniformly at
  [DurableDriverSegmentRun.cs:839](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L839))
- Continuation pump / outbox kind partitioning (DR-034/DR-037), outbox
  materialization and dispatch
- Deterministic child identity + child dispatch / parent resume token —
  [DurableDriverSegmentRun.Children.cs](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Children.cs)
- Provider stores + certification harness, PostgreSQL/SQL Server integration
  fixtures ([tests/OrcaCore.Integration.Tests](../../tests/OrcaCore.Integration.Tests))
- `StepResult` closed control-intent set
  ([StepResult.cs](../../src/OrcaCore.Abstractions/Steps/StepResult.cs)) — extended,
  not replaced, by branch-result contracts
- Fluent builder surface skeleton and accumulated validation
  ([WorkflowBuilder.cs:333](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L333))
- Management, projections, inbox dedup, timer pump, retry/timeout policy
  machinery ([DurableDriverSegmentRun.Steps.cs:368](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs#L368))

**Code that must be replaced:**

- Cursor envelope v1 (`DurableExecutionPosition`/`DurableExecutionCursor`/
  `DurableExecutionFrame`,
  [DurableExecutionEnvelope.cs:72](../../src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs#L72))
- `SplitCursor` / `MergeCompletedCursors` / frame-prefix ownership /
  `Join` reconstruction
  ([DurableDriverSegmentRun.cs:368-541](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L368))
- Ordinal-first `NextRunnableCursor`
  ([DurableDriverSegmentRun.cs:159](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L159))
- Ephemeral `ParallelNodeRunner`, `WhenFirstNodeRunner`, `WhenFirstJoin`,
  shared-`TState` branch execution
- `WhenFirstResidualPolicy` enum and its builder overloads
  ([WorkflowBuilder.cs:182](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L182))

**Current defects worth recording independently of the refactor:**

- **D-1 (CONFIRMED):** branch-initiated `ContinueAsNew` clears all cursors
  without cancelling sibling waits/timers → orphan obligations
  ([DurableDriverSegmentRun.Steps.cs:140](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs#L140));
  nothing restricts `StepResult.ContinueAsNew` to the root path today.
- **D-2 (CONFIRMED):** engine drift on `WhenFirst` residuals — ephemeral honors
  all three policies ([WhenFirstJoin.cs:46](../../src/OrcaCore.Engine.Ephemeral/Execution/WhenFirstJoin.cs#L46));
  the durable driver ignores `ResidualPolicy` entirely and always cancels
  ([DurableDriverSegmentRun.cs:475](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L475),
  node split at [:255](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L255)).
  This substantiates removing the policies rather than "preserving" behavior
  that only one engine has.
- **D-3 (CONFIRMED):** registration is non-atomic — catalog registers the
  executor before the registry can reject
  ([DurableWorkflowRuntime.cs:74](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L74)),
  leaving a resolvable executor for an unregistered definition on failure.
- **D-4 (CONFIRMED):** structural-only loops can spin without issuing commands,
  bounded only by the elapsed-time deadline (see F-04 evidence).

Given the wholesale replacement, fixing D-1..D-4 in the cursor code first is
optional; recording them as regression targets for the new model (they map to
tasks 8.6/8.7, 11.2, 2.8, and F-04 respectively) is mandatory.

**Proposed behavior with no current enabling contract (green-field):**

- Branch input projectors, branch-private state, `BranchReturn<TResult>`,
  merge adapters (all branches currently share one mutable `TState`)
- Compiled plan, instruction identities, fingerprint, compiler format version
- Fiber/scope records, persisted next-fiber position (no scheduler state
  exists in envelope v1)
- `FiberId`/`ScopeId` owner fields on waits/timers/resumes/jobs/tickets/
  children (current records carry a descriptive `BranchScope` string only)
- Durable child-cancellation protocol (its absence is why shapes must be
  rejected, F-05)

## Verification Assessment

- **OpenSpec:** `openspec status --change adopt-structured-fiber-execution
  --json` → all four artifacts `done`, `isComplete: true`.
  `openspec validate adopt-structured-fiber-execution --type change --strict
  --no-interactive` → "Change 'adopt-structured-fiber-execution' is valid".
- **Structural validation:** all six MODIFIED requirement headers in the
  deltas were matched verbatim against `openspec/specs/*/spec.md` baselines
  (workflow-authoring, state-driven-runtime, durable-runtime ×2,
  event-routing-and-waits, saga-orchestration) — all exist. Relative links in
  design.md Context (`08-requirements-composition.md`,
  `13-phasing-and-open-questions.md`, `add-runtime-concurrency-limits/design.md`)
  resolve to existing files.
- **Documents read in full:** proposal.md, design.md, tasks.md, all nine spec
  deltas; canonical docs 03, 04, 05, 06, 07, 08, 12, 13, 16;
  add-runtime-concurrency-limits proposal/design/tasks; baseline specs for
  every modified capability.
- **Code traced:** all fifteen listed implementation files, plus
  `WhenFirstJoin.cs`, `WhenFirstResidualPolicy.cs`, `ForEachNodeRunner.cs`/
  `ForEachWorkScheduler.cs` (reached from the required files), driver
  catalog/registry/runtime, and the continuation/park paths.
- **Tests inspected:** `WorkflowBuilderTests.cs` (22 cases incl. accumulated
  validation and durable-only detection), `ParallelAcceptanceTests.cs`
  (AC-201..203, branch-wait isolation, racing completions),
  `DurableDriverReviewedAcceptanceTests.cs` (17 regression scenarios incl.
  nested `WhenFirst` stranding, multi-host winner, continue-as-new, retry/
  timeout survival), durable suite inventory (50 test classes covering
  driver/host/recovery/versioning/wait/child/resource/external-job/saga),
  ephemeral `ParallelTests`/`WhenFirstTests`/`YieldTests`/`WaitMatchingTests`,
  provider certification checkpoint round-trip (opaque
  `"application/json", [1]` payload at
  [EventStoreCertificationTests.cs:440](../../tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.cs#L440)
  — confirming the quality delta's claim that providers currently certify
  opaque bytes, not real envelopes), and the PostgreSQL/SQL Server
  integration test inventory.
- **Commands run:** `git status --short` (dirty docs preserved untouched),
  the two `openspec` commands above, file listings. **Not run:**
  `dotnet build` / `dotnet test` — this review changes no code, and the task
  baseline capture is explicitly tasks.md 1.3; current-suite greenness was
  not independently re-verified here.
- **Not verified:** runtime behavior of the R14-era saga checkpoint fix
  (out of scope); Redis/RabbitMQ/ZeroMq transport suites (not implicated);
  benchmark contents under `benchmarks/` (existence confirmed only).

## Required Changes Before Apply

Document-only corrections, in order:

1. **[F-01]** Add the `ForEach` decision to design.md, a normative requirement
   + scenarios to the `workflow-authoring` (and, if compiled,
   `structured-fiber-execution`) delta, and tasks in sections 2, 5, and 11 —
   including the fate of `ForEachResidualPolicy.LetRemainingComplete` and the
   CP-010..013 / AC-601..605 amendments.
2. **[F-02]** Rewrite task 1.2 (or add 1.2a) to enumerate the exact canonical
   conflicts and their intended amendments: CP-001, CP-004, CR-015, CR-044,
   SG-010 (incl. per-scope override), 13.4 `Parallel` resolution,
   DR-011/DR-011a/DR-012, AC-204/AC-205, AC-403, DR-AC-032, and
   `add-runtime-concurrency-limits` design §3/§4 saturation + future-parallel
   wording; state precedence in proposal.md Impact.
3. **[F-03 + F-13]** Add a deterministic runtime scope/fiber identity-minting
   requirement (loop re-entry, replay, duplicate claims, `ContinueAsNew`
   generations) with a crash-boundary scenario; add the persisted
   scope-entry/iteration sequence to the Decision 10 envelope field list;
   extend tasks 3.1/3.2 and 7.3/7.5.
4. **[F-04]** Make the internal-instruction budget normative with a defined
   exceeded-bound outcome; add a compiler check for loop bodies with no
   quantum-ending instruction (tasks 2.3, 3.5).
5. **[F-05]** Align design.md Decision 7 and task 8.5 with the spec's general
   "branch shape" cancellation-rejection rule (all join policies and root
   cancellation, not only `WhenFirst` losers).
6. **[F-06]** Add the root-`ContinueAsNew`-with-active-scopes requirement and
   scenario that tasks 8.6/8.7 implement.
7. **[F-07]** Add the reference-model/generator/harness build task (section 3
   or 4) that task 12.3 depends on.
8. **[F-08]** Specify deterministic wait-match selection (or an instance-level
   uniqueness rule) for identical `(EventName, CorrelationId)` waits owned by
   different fibers; scenario + task 8.8 regression.
9. **[F-09]** Define compensation eligibility relative to branch-result commit
   and scope merge in the saga delta; gate task 9.1 on it.
10. **[F-10..F-12]** Clarity fixes: state that `ScopeJoin`/`ScopeExit` are
    reducer-evaluated at commit boundaries; name document 03 (glossary) and
    the CR-008 named-outcome disposition in task 11.4; scope `Parked` and
    fingerprint diagnostics to durable mode in Decision 9 and the
    state-driven-runtime delta.

Tasks already correctly captured in tasks.md (atomic registration 2.8,
envelope v2 6.x, ownership 8.x, legacy deletion 11.1/11.2, provider gates
10.x) are intentionally not repeated here.

## Accepted Risks

**Deliberately accepted because OrcaCore has no consumers and no production
durable instances:**

- No cursor-envelope migration; format-1 data is rejected and development
  stores are reset (Decision 10). Loading is already content-type-guarded
  today ([DurableWorkflowDriver.cs:105](../../src/OrcaCore.Engine.Durable/Driver/DurableWorkflowDriver.cs#L105)).
- Deletion of the legacy cursor executor with no dual-format runtime rollback;
  rollback is source-level to the preserved baseline (Migration Plan step 2 /
  task 1.3).
- Breaking authoring changes: single root `Init`/`End`, `BranchReturn`,
  removal of `WhenFirst.Ignore`/`LetRemainingComplete`, isolated branch state
  replacing shared `TState` in every existing composition test (task 5.3).
- Large-rewrite schedule risk, bounded by vertical test-first slices and the
  retained host/provider seams.

**Intrinsic to a durable engine (not removable by this change; must stay
documented):**

- Step effects remain at-least-once; commits exactly-once (DR-014). Merge is
  *logically* exactly-once only because it is required to be pure — purity of
  user code is unprovable and only guardrail-enforced (Decision 5 risk).
- The plan fingerprint cannot detect changed step *implementation* semantics;
  authors must bump the definition version (Decision 1).
- Envelope growth with deep nesting and large branch results; mitigated but
  not eliminated by depth/fiber/result/envelope limits whose defaults are an
  open question (Open Question 2) and by benchmarks (task 11.5).
- Cooperative one-quantum scheduling increases commit frequency relative to
  run-to-suspension; segment budgets amortize it but the cost must be
  measured, not assumed (task 11.5, DR-051 defaults).

## Remediation Update - 2026-07-13

The decision package was revised after this review. These are document-only
changes; no production source or test implementation was started. The original
`REQUEST CHANGES` verdict remains in force until an independent re-review
confirms the dispositions below and explicitly approves `/opsx:apply`.

| Finding | Document disposition | Status |
|---|---|---|
| F-01 | Added design Decision 15 for ephemeral `ForEach` as a dynamic isolated-item fiber scope; added workflow-authoring, workflow-contract, and structured-fiber requirements; removed `LetRemainingComplete`; made durable compilation reject the capability; added compiler, contract, ephemeral, cleanup, legacy-removal, and reference-model tasks. | Addressed; pending re-review |
| F-02 | Proposal Impact now states precedence and enumerates every conflicting CP, CR, SG, DR, AC, DR-AC, section 13.4, and concurrency-limits decision. Task 1.2 requires those amendments before production code; task 11.4 performs the final documentation sync. | Addressed; pending re-review |
| F-03 | Decisions 3 and 10 now define committed generation/scope-entry identity derivation and envelope progress. The structured-fiber requirement and tasks 3.1, 3.2, 7.3, and 7.5 cover loop re-entry, replay, duplicate claims, and host replacement. | Addressed; pending re-review |
| F-04 | Decision 6 and the scheduler requirement define positive `MaxInternalInstructionsPerQuantum`, default 1024, persisted forced rotation, and compile-time rejection of cycles with no quantum-ending operation. Tasks 2.3 and 3.5 are failing-first gates. | Addressed; pending re-review |
| F-05 | Decision 7 and task 8.5 now reject every shape whose join, failure, or root-cancellation path can induce durably unsupported cancellation, not only `WhenFirst` losers. | Addressed; pending re-review |
| F-06 | Decision 2 and the durable-runtime delta now reject `ContinueAsNew` unless the root is the only nonterminal fiber and no descendant scope or owned obligation remains. Tasks 8.6 and 8.7 pin rejected and valid rollover behavior. | Addressed; pending re-review |
| F-07 | Task 3.8 builds the reference model, generators, crash/schedule inputs, and comparison harness before later behavior gates; task 12.3 explicitly consumes that harness. | Addressed; pending re-review |
| F-08 | The event-routing delta defines persisted per-instance `WaitSequence` selection with a stable `FiberId` tie break. Task 8.8 adds identical event/correlation sibling-wait regressions. | Addressed; pending re-review |
| F-09 | Decision 13 and the saga delta define immediate branch-scope eligibility at forward commit, pre-merge descendant coverage, transfer on successful merge, and deterministic plan-bound per-scope ordering overrides. Task 9.1 tests each boundary. | Addressed; pending re-review |
| F-10 | Decision 14 and the structured-fiber requirement state that `ScopeJoin` and `ScopeExit` are scope-reducer transitions at commit boundaries; task 3.6 implements that boundary. | Addressed; pending re-review |
| F-11 | Decision 2 and workflow-authoring preserve CR-008 through a static or deterministic final-state outcome selector on the single root `End`. Tasks 2.1 and 11.4 test it and explicitly update `docs/specs/03-domain-model-and-glossary.md`. | Addressed; pending re-review |
| F-12 | Decision 9 and the state-driven-runtime delta scope `Parked`, fingerprint, and envelope diagnostics to durable mode; ephemeral mode reports typed start/execution failures. Task 3.7 tests the mode distinction. | Addressed; pending re-review |
| F-13 | Decisions 3 and 10, the durable envelope delta, and task 6.1 explicitly persist generation, loop iteration, next scope-entry sequence, scope plan identity, and scope entry identity. | Addressed; pending re-review |

The package now contains 15 design decisions. A re-review therefore needs to
extend the traceability matrix and decision checklist to include Decision 15
and the added `ForEach` scenarios and tasks.
