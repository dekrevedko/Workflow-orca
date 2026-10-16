# Structured Fiber Execution Decision Re-Review - 2026-07-13

Independent re-review of the corrected `adopt-structured-fiber-execution`
decision package, including verification of the claimed dispositions of
F-01 through F-13 from
[structured-fiber-execution-review-2026-07-13.md](structured-fiber-execution-review-2026-07-13.md).
The prior report was treated as history, not authority: every disposition was
re-checked against the current proposal, design, tasks, all nine spec deltas,
the canonical documents, the concurrency-limits change, and the unchanged root
implementation. Review-only; no decision document, source file, or test was
modified.

## Verdict

**APPROVE**

Implementation (`/opsx:apply`) may begin. All three P1 findings and every P2/P3
finding from the prior review are CLOSED with verified artifact evidence (one,
F-09, closed with a residual nit). No new P0 or P1 was found and no blocking
ambiguity remains. The re-review surfaced one new P2 and two new P3 items —
all narrow, one-paragraph clarifications inside the newly added `ForEach` and
`ContinueAsNew` material — none of which blocks the start of implementation:
each is pinned to a later slice whose failing-first test task will force the
decision, and the recommended resolution is stated below.

Counts: **0 × P0, 0 × P1, 1 × P2, 2 × P3** (new findings only; all 13 prior
findings closed).

Two conditions of the approval, both already encoded in the package itself:

1. Task 1.2 (amending the enumerated canonical requirements and the
   `add-runtime-concurrency-limits` change) executes **before any production
   code**, exactly as tasks.md now orders it. The canonical documents are, as
   of this re-review, still unamended — that is expected; the enumeration and
   precedence statement now exist in proposal Impact, which is what the prior
   F-02 required.
2. The Review Checklist (21 items) is formally ticked at task 1.1 using the
   dispositions in this report.

## Findings

### R-01 (P2, CONFIRMED) — `ForEach.WhenAny` result, failed-winner, and failure-policy interplay are unspecified

- **Decision/spec:** design.md Decision 15 defines `WhenAll` against all three
  failure policies (`FailFast`, `WaitAllThenFail`, `ContinueWithPartialFailures`)
  but the `WhenAny` paragraph defines only selection ("first committed terminal
  item, item-index ties, cancel remaining"). Three sub-questions are unanswered:
  (a) does a *failed* first terminal item fail the scope (the `WhenFirst`
  failed-winner rule, Decision 7) or does selection skip to the next terminal
  item; (b) does an authored merge execute over the winner's
  `ForEachItemOutcome<TResult>`, and over what list; (c) what do the
  combinations `WhenAny + WaitAllThenFail` and
  `WhenAny + ContinueWithPartialFailures` mean — the workflow-authoring delta
  ([spec.md:44](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/workflow-authoring/spec.md#L44))
  requires *declaring* both a join policy and a failure policy, so the
  authoring surface admits combinations the design never defines. The
  structured-fiber `ForEach` scenarios
  ([spec.md:107](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/structured-fiber-execution/spec.md#L107))
  cover selection and cancellation only.
- **Why it matters:** task 5.5 mandates failing-first tests for
  "first-committed-terminal `WhenAny` selection" — those tests cannot pin the
  failed-winner or merge sub-cases without a decided semantic, and an
  undefined-but-authorable policy combination is exactly the class of gap this
  change set out to remove (compile-time rejection over runtime surprise).
- **Required change (one paragraph in Decision 15 + one scenario):** state
  that `WhenAny` follows the `WhenFirst` contract — a failed first committed
  terminal item fails the scope without merge; a successful winner's outcome
  (alone, as a single-element index-ordered list) feeds the optional merge —
  and that `WhenAny` accepts only `FailFast` (or that the failure-policy
  declaration is ignored/rejected for `WhenAny`), with validation rejecting
  incoherent combinations.
- **Requirement/scenario/task:** add a failed-first-terminal-item and an
  invalid-combination scenario to the `structured-fiber-execution` or
  `workflow-authoring` delta; task 5.5 then covers both. Not blocking for
  sections 1–4; must be resolved before task 5.5 is written — recommended now
  as a one-paragraph edit.

### R-02 (P3, CONFIRMED) — Lifecycle outcome of a rejected root `ContinueAsNew` is unstated

- **Decision/spec:** design.md Decision 2 and the durable-runtime delta
  ([spec.md:36](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/durable-runtime/spec.md#L36))
  say a non-quiescent rollover "is rejected without rollover or state change"
  and "the existing generation and execution envelope remain unchanged" — but
  not what the instance does next. The root fiber just returned
  `ContinueAsNew` from a quantum; the natural options are: the step result is
  converted to a step failure (CR-014 → `Failed`), or the instance parks, or
  the rejection is a validation-time impossibility. Decision 2's own argument
  ("valid structured execution naturally reaches `ContinueAsNew` only after
  prior scopes finish") suggests the runtime case is near-unreachable and the
  honest answer is "treated as a workflow failure with an explicit
  diagnostic" — but task 8.6's failing test needs the expected observable
  outcome written down.
- **Required change:** one sentence in Decision 2 and the durable-runtime
  scenario naming the post-rejection lifecycle state (recommend: explicit
  workflow failure diagnostic, never silent no-op). Resolve before task 8.6.

### R-03 (P3, CONFIRMED) — Baseline "Saturation behavior is configurable" needs an explicit carve-out for fiber turns

- **Decision/spec:** the baseline
  [runtime-resource-governance spec (Saturation behavior is configurable)](../../openspec/specs/runtime-resource-governance/spec.md)
  allows an operator-configured in-process *async wait* on a saturated pool;
  the delta requirement
  ([spec.md:21](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/runtime-resource-governance/spec.md#L21))
  states the runtime "SHALL NOT await resource capacity while retaining the
  instance turn." These are reconcilable (fail-fast still allowed; the blocking
  *form* changes from awaiting to a blocked obligation) and proposal Impact's
  final bullet plus task 1.2 already target `add-runtime-concurrency-limits`
  §3/§4 — but the baseline requirement itself is not in the 1.2 enumeration
  and its "async wait for a slot" wording will read as contradicting the delta
  after apply. Add the baseline saturation requirement (or a MODIFIED delta for
  it) to the 1.2 amendment list. Document-only, one list entry.

No other new findings. Residual risks are listed under Accepted Risks.

## Prior Finding Disposition

Each disposition independently verified against current artifacts; the code
evidence was re-checked (git status shows no `src/` or `tests/` modifications
since the prior review — the implementation is unchanged, so prior `file:line`
evidence remains valid).

| Prior | Status | Evidence |
|---|---|---|
| **F-01** `ForEach` unaddressed (P1) | **CLOSED** | Design Decision 15 (dynamic isolated-item fiber scope, `ScopeKind.ForEach`, durable rejection, `LetRemainingComplete` removed); proposal "What Changes" BREAKING bullet; new requirements: workflow-authoring "ForEach authoring uses the structured scope contract" ([spec.md:43](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/workflow-authoring/spec.md#L43)), structured-fiber "Ephemeral ForEach uses dynamic isolated item fibers" ([spec.md:92](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/structured-fiber-execution/spec.md#L92)), workflow-contracts "Dynamic item outcomes are ordered contracts" ([spec.md:17](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/workflow-contracts/spec.md#L17)); tasks 2.3, 2.6, 4.1, 4.2, 5.3, 5.5, 5.6, 5.7, 11.2, and the CP-010..013/AC-601..605 amendments in task 1.2. Durable rejection matches current behavior ([DurableDriverCatalog.cs:42](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverCatalog.cs#L42)), preserving DR-010/DR-AC-016. Residual nit spun off as R-01 (P2). |
| **F-02** Canonical conflicts unenumerated (P1) | **CLOSED** | proposal.md Impact now enumerates, with precedence ("This change is authoritative over…"), all six conflict groups: CP-001/CR-044/13.4; CP-004/AC-204/AC-205/DR-AC-032; CP-010..013/AC-601..605; CR-015/DR-011/DR-011a/DR-012; SG-010+override/AC-403; concurrency-limits §3/§4 with the mode-specific blocked-obligation resolution. Task 1.2 requires the amendments **before production code**; task 11.4 performs the final sync. The canonical docs are still unamended — correctly, since amending them is 1.2's gated work, not a review precondition. One baseline-spec addition to the list flagged as R-03 (P3). |
| **F-03** Runtime identity minting (P1) | **CLOSED** | Decision 3 defines `RootFiberId = Hash(InstanceId, Generation, "root")`, `ScopeId = Hash(ParentFiberId, ScopePlanId, Parent.NextScopeEntrySequence)`, `ChildFiberId = Hash(ScopeId, BranchId or ItemIndex)`, with atomic persistence of the sequence increment and replay/duplicate-claim/optimistic-conflict behavior spelled out. New requirement "Runtime scope and fiber identities derive from committed state" with loop-re-entry-across-restart and `ContinueAsNew`-generation scenarios ([spec.md:51](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/structured-fiber-execution/spec.md#L51)); tasks 3.1, 3.2, 7.3, 7.5 all name the identity cases. Sound: derivation inputs are all committed values; competing pre-commit attempts derive identical ids and expected-version admits one. |
| **F-04** Internal-instruction bound (P2) | **CLOSED** | Decision 6: `MaxInternalInstructionsPerQuantum`, positive, default 1024; reaching it ends the quantum *successfully* with persisted progress, diagnostic counter, and requeue; compiler rejects reachable loop cycles with no quantum-ending operation. Scenarios "Internal-instruction budget is reached" and "Loop cannot end a quantum" ([spec.md:73](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/structured-fiber-execution/spec.md#L73)); tasks 2.3 and 3.5. |
| **F-05** Loser-only cancellation wording (P2) | **CLOSED** | Decision 7 now rejects any shape whose "join failure, loser selection, parent cancellation, termination, or rollover path" induces undurably-cancellable work, "including a child-owning `WhenAll` that may fail fast"; task 8.5 rewritten to match ("every branch shape whose failure, join, or root-cancellation path…"). Consistent with the spec's general rule ([spec.md:152](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/structured-fiber-execution/spec.md#L152)). |
| **F-06** Rollover with active scopes (P2) | **CLOSED** | New durable-runtime requirement "ContinueAsNew requires a quiescent root scope" with rejected and quiescent scenarios ([spec.md:36](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/durable-runtime/spec.md#L36)); Decision 2 quiescence rule; tasks 8.6/8.7 pin rejection leaving generation/state/ownership unchanged and valid rollover incrementing generation before minting the new root. This also supersedes the current defect (branch `ContinueAsNew` discarding sibling obligations, [DurableDriverSegmentRun.Steps.cs:140](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs#L140)). Residual one-sentence gap spun off as R-02 (P3). |
| **F-07** Reference model had no build task (P2) | **CLOSED** | Task 3.8 builds the reference model, generated definition/schedule inputs, seeded replay/crash permutations, and comparison harness "consumed by later merge, cancellation, provider, and final-verification tests"; task 12.3 now explicitly runs "the reference model, generators, and comparison harness built in 3.8" including dynamic `ForEach` items. Ordering is correct: after the shared model (3.1–3.7), before the join/merge gates (4.6) that use it. |
| **F-08** Wait-match determinism (P2) | **CLOSED** | Event-routing delta: persisted per-instance `WaitSequence` at registration; identical-identity multi-candidate matching selects lowest sequence with stable `FiberId` tie break; new "Event matches identical sibling waits" scenario; MODIFIED isolation requirement includes `WaitSequence` ([spec.md:4](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/event-routing-and-waits/spec.md#L4)). Task 8.8 persists the sequence and tests owner-distinct identical waits. Additive to EV-020/EV-011 (no new canonical conflict). |
| **F-09** Compensation eligibility vs merge (P2) | **CLOSED** | Decision 13: immediate eligibility at forward commit within the branch scope; pre-merge scope failure covers all committed descendant actions; branch-result commit exposes records to the containing scope; successful merge transfers records upward with stable identities; overrides only when deterministic, plan-fingerprint-bound, and authored-identity-based. Saga delta MODIFIED requirement restates all of it with "Scope fails before merge" and "Parent fails after successful merge" scenarios ([spec.md:13](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/saga-orchestration/spec.md#L13)); task 9.1 tests each boundary. |
| **F-10** `ScopeJoin`/`ScopeExit` executor (P3) | **CLOSED** | Decision 14: both are "runtime-significant structural positions evaluated by `ScopeReducer` at commit boundaries; no runnable fiber is selected to execute them as a quantum," with the final-branch-result and cleanup/merge transitions assigned. New scenario "Scope reaches a join position" ([spec.md:36](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/structured-fiber-execution/spec.md#L36)); task 3.6 implements the reducer boundary. |
| **F-11** Glossary + CR-008 outcomes (P3) | **CLOSED** | Decision 2: root `End` supports a static named outcome or a deterministic final-state outcome selector recorded as CR-008 metadata; workflow-authoring requirement + scenario updated ([spec.md:4](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/workflow-authoring/spec.md#L4)); task 2.1 tests the selector; task 11.4 explicitly names `docs/specs/03-domain-model-and-glossary.md` and the plan/fiber/scope/quantum/branch-return/merge/fingerprint terms and CR-008 preservation. |
| **F-12** `Parked` mode scoping (P3) | **CLOSED** | Decision 9 marks `Parked` durable-only and defines the ephemeral typed-failure path; state-driven-runtime parity requirement scoped to "capabilities supported by both modes" with the "Unsupported or incompatible execution is reported by mode" scenario ([spec.md:22](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/state-driven-runtime/spec.md#L22)); task 3.7 tests the mode distinction. |
| **F-13** Envelope loop/scope-entry fields (P3) | **CLOSED** | Decision 10 field list adds `InstanceId`, `ContinueAsNewGeneration`, fiber `LoopIteration` + `NextScopeEntrySequence`, scope `ScopePlanId` + `ScopeEntrySequence`; durable-runtime envelope requirement mandates them with the "Scope inside a loop is rehydrated" scenario ([spec.md:4](../../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/durable-runtime/spec.md#L4)); task 6.1 enumerates every field. |

## Decision Checklist

All 21 checkboxes in design.md → Review Checklist:

| # | Checklist item | Ruling | Reason |
|---|---|---|---|
| 1 | Local `Parallel` = deterministic cooperative fiber execution | **ACCEPT** | Conflicting canonical wording (CP-001/CR-044/13.4) is now enumerated with precedence in proposal Impact and amended by task 1.2 before code |
| 2 | Branches cannot mutate parent state; typed serializable results | **ACCEPT** | `ForEach`, the last shared-state holdout, is now covered by Decision 15 |
| 3 | `WhenAll` explicit merge in authored branch order | **ACCEPT** | Unchanged; deterministic and replay-safe |
| 4 | `WhenAll` initially fail-fast, no partial merge | **ACCEPT** | Unchanged; strict initial policy correctly scoped |
| 5 | `WhenFirst` first committed terminal, authored-order ties, cancel losers | **ACCEPT** | Unchanged; only replay-deterministic definition |
| 6 | `WhenFirst.Ignore`/`LetRemainingComplete` removed | **ACCEPT** | CP-004/AC-204/AC-205/DR-AC-032 amendments now in the 1.2 list; durable driver never implemented them ([DurableDriverSegmentRun.cs:475](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L475)) |
| 7 | Ephemeral `ForEach` = dynamic isolated item fibers; durable rejects; no `LetRemainingComplete` | **ACCEPT WITH CHANGE** | Sound and it preserves useful CP-010..013 behavior; `WhenAny` result/failed-winner/failure-policy interplay needs one paragraph (R-01) |
| 8 | Exactly one root `Init`/`End`; branches use `BranchReturn` | **ACCEPT** | Now CR-008-compatible via the outcome selector |
| 9 | No public closing nodes; compiler emits joins/exits | **ACCEPT** | Unchanged |
| 10 | `ContinueAsNew` root-only + quiescent-root rejection | **ACCEPT WITH CHANGE** | Quiescence rule is right and normatively specified; name the post-rejection lifecycle outcome (R-02) |
| 11 | Identities from committed generation/plan/parent/sequence | **ACCEPT** | Deterministic over committed inputs; replay/duplicate-claim/conflict behavior specified (closes prior F-03) |
| 12 | Persisted round-robin; `Yield` ends quantum; forced-rotation budget | **ACCEPT** | Budget is normative with default, validation, outcome, and compiler rejection of no-progress cycles |
| 13 | Explicit ownership; unsupported cancellation shapes fail compilation | **ACCEPT** | Generalized to every join/failure/root-control path |
| 14 | Identical sibling waits by registration order + fiber-id tie break | **ACCEPT** | Deterministic, additive to EV-020, tested in 8.8 |
| 15 | Envelope format 2, no migration/legacy executor | **ACCEPT** | Field list is now complete including loop/entry progress |
| 16 | Registration atomic, binds version + fingerprint | **ACCEPT** | Task 2.8 fixes the demonstrated catalog/registry non-atomicity ([DurableWorkflowRuntime.cs:74](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L74)) |
| 17 | Engines share compiler/scheduler/scope/join/merge semantics | **ACCEPT** | Parity requirement correctly scoped to shared capabilities with mode-specific `Parked`/typed-failure reporting |
| 18 | Saga eligibility at forward commit; failure covers descendants; merge transfers upward | **ACCEPT** | Fully specified with both boundary scenarios (closes prior F-09) |
| 19 | Saga sibling compensation canonical order + plan-bound overrides | **ACCEPT** | SG-010 (incl. override) in the 1.2 amendment list; override constrained to deterministic plan-fingerprint-bound authored identities |
| 20 | DAG remains child-instance based | **ACCEPT** | Unchanged; consistent with resolved open question 15 |
| 21 | Test-first; cursor deletion after gates | **ACCEPT** | Reference-model oracle now has its own build task (3.8) ahead of the gates that consume it |

## Traceability Matrix

All 15 design decisions:

| D | Design section | Normative requirement(s) | Task(s) | Test project(s) | Replaced / retained | Status |
|---|---|---|---|---|---|---|
| 1 | Compiled instruction plans | SFE "Definitions compile to stable executable plans"; WC "Executable plan identity is explicit"; WA "Validation covers complete structured reachability" | 2.1–2.8 | Core.Tests | Replaces builder-only validation ([WorkflowBuilder.cs:333](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L333)) + catalog capability walk ([DurableDriverCatalog.cs:33](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverCatalog.cs#L33)) | OK |
| 2 | One root entry/exit + outcome selector + quiescent rollover | WA "Successful workflow flow has one root entry and exit"; WA "Every branch has one branch return"; DR "ContinueAsNew requires a quiescent root scope" | 2.1, 2.2, 8.6, 8.7 | Core.Tests; Durable.Tests | Replaces permissive nested-`End` acceptance ([WorkflowBuilder.cs:400](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L400)) and unguarded rollover ([DurableDriverSegmentRun.Steps.cs:140](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs#L140)) | OK (R-02 one-sentence outcome) |
| 3 | Fibers, scopes, deterministic identity minting | SFE fiber/scope requirements + "Runtime scope and fiber identities derive from committed state" | 3.1, 3.2, 7.3, 7.5 | Core.Tests; Durable.Tests | Replaces cursor split/join ([DurableDriverSegmentRun.cs:368](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L368)) and ephemeral runners | OK (prior weak mapping resolved) |
| 4 | Isolated branch state, typed results | SFE "Branches cannot mutate parent business state"; WC branch-data + type-check requirements | 4.1–4.3 | Core.Tests; Acceptance.Tests | New — no current contract (shared `TState`, [Nodes.cs:268](../../src/OrcaCore.Core/Definitions/Nodes.cs#L268)) | OK |
| 5 | Pure replacement-state merge | SFE "Merge is explicit, deterministic, and side-effect free" | 4.4, 4.5; crash edges 7.3, 7.4 | Core.Tests; Durable.Tests | New — join currently promotes one cursor ([DurableDriverSegmentRun.cs:526](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L526)) | OK |
| 6 | Persisted round-robin + quantum budget | SFE "Local fibers use cooperative scheduling" (incl. budget scenarios); SDR "Yield is a fiber scheduling operation" | 2.3, 3.3–3.5, 7.1, 7.7 | Core.Tests; Durable.Tests | Replaces ordinal-first selection ([DurableDriverSegmentRun.cs:159](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L159)) | OK (prior weak mapping resolved) |
| 7 | Strict join/residual policies + generalized rejection | SFE "Join policies define one scope outcome"; SFE ownership rejection scenario; WA MODIFIED parallel authoring | 4.6, 8.5, 11.2 | Core.Tests; Acceptance.Tests; Durable.Tests | Replaces `WhenFirstResidualPolicy` + `WhenFirstJoin` ([WhenFirstJoin.cs:46](../../src/OrcaCore.Engine.Ephemeral/Execution/WhenFirstJoin.cs#L46)) | OK (prior weak mapping resolved) |
| 8 | Explicit obligation ownership + resource turn rule | SFE "Scope ownership covers every residual obligation"; ERW deltas; RRG all three requirements | 5.4, 8.1–8.4, 8.8 | Durable.Tests; Ephemeral.Tests | Replaces descriptive `BranchScope` labels ([DurableDriverSegmentRun.cs:795](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L795)) | OK |
| 9 | Status from fiber runnability, mode-scoped `Parked` | SDR "Instance status derives from aggregate fiber runnability"; SDR parity requirement | 3.7 | Core.Tests; Acceptance.Tests | Replaces instance-wide `Waiting` on wait registration | OK (prior F-12 resolved) |
| 10 | Envelope format 2 (complete field list) | DR envelope + atomic-transition + no-cursor requirements; DR MODIFIED rehydration/version binding | 6.1–6.6 | Abstractions tests; Durable.Tests | Replaces envelope v1 ([DurableExecutionEnvelope.cs:23](../../src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs#L23)) | OK (prior F-13 resolved) |
| 11 | Shared core + persistence adapters | SDR "Runtime modes share execution semantics" | 2.5, 3.2–3.6, 5.2, 7.2 | All suites | Retains host/processor/aggregate/outbox/provider seams ([DurableWorkflowDriver.cs](../../src/OrcaCore.Engine.Durable/Driver/DurableWorkflowDriver.cs)) | OK |
| 12 | Future true-parallel path | RRG "Local fiber scheduling is distinct from host concurrency" (partial); otherwise design rationale | none (intentional) | n/a | n/a | ACCEPTABLE (design-only, as before) |
| 13 | Saga/DAG on the new model + eligibility boundaries | SO ADDED + MODIFIED requirements incl. both merge-boundary scenarios | 9.1–9.4 | Durable.Tests; Integration | Extends kernel saga commands; retains `DurableDagRunner` | OK (prior F-09 resolved) |
| 14 | Structural instructions + reducer-evaluated join/exit | SFE "Compiled plans contain explicit structural continuations" incl. reducer scenario; WA "Fluent blocks do not require authored closing nodes" | 2.4, 3.6 | Core.Tests | New | OK (prior F-10 resolved) |
| 15 | Ephemeral `ForEach` as dynamic fiber scope | SFE "Ephemeral ForEach uses dynamic isolated item fibers"; WA "ForEach authoring uses the structured scope contract"; WC "Dynamic item outcomes are ordered contracts" | 2.3, 2.6, 4.1, 4.2, 5.3, 5.5–5.7, 11.2 | Core.Tests; Ephemeral.Tests | Replaces `ForEachNodeRunner`/`ForEachWorkScheduler` ([ForEachWorkScheduler.cs:127](../../src/OrcaCore.Engine.Ephemeral/Execution/ForEachWorkScheduler.cs#L127)) and `ForEachResidualPolicy.LetRemainingComplete`; retains durable rejection ([DurableDriverCatalog.cs:42](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverCatalog.cs#L42)) | **WEAK on one sub-case** — `WhenAny` result/failure-policy semantics (R-01) |

No missing mappings remain; the single weak mapping is R-01's `WhenAny`
sub-case inside D15.

## Current Implementation Gap Map

The root implementation is unchanged since the prior review (no `src/` or
`tests/` modifications in git status); the prior gap map remains accurate.
Summary with re-verified anchors:

- **Reusable seams:** durable host/conflict-retry/park/re-arm
  ([DurableWorkflowDriver.cs](../../src/OrcaCore.Engine.Durable/Driver/DurableWorkflowDriver.cs)),
  command processor + one-command/one-commit aggregate, continuation pump +
  kind-partitioned outbox, deterministic child dispatch
  ([DurableDriverSegmentRun.Children.cs](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Children.cs)),
  provider stores + certification harness, `StepResult` control-intent set,
  builder skeleton with accumulated validation, retry/timeout policy machinery.
- **To be replaced:** cursor envelope v1 and frame model
  ([DurableExecutionEnvelope.cs:72](../../src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs#L72)),
  `SplitCursor`/`MergeCompletedCursors`/ordinal-first selection
  ([DurableDriverSegmentRun.cs:159](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L159)),
  ephemeral `ParallelNodeRunner`/`WhenFirstNodeRunner`/`WhenFirstJoin`,
  **now also** `ForEachNodeRunner`/`ForEachWorkScheduler` and
  `ForEachResidualPolicy.LetRemainingComplete` (tasks 5.7/11.2 updated
  accordingly).
- **Current defects (recorded as regression targets, not pre-fixes):**
  branch `ContinueAsNew` orphaning sibling obligations
  ([DurableDriverSegmentRun.Steps.cs:140](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs#L140))
  → tasks 8.6/8.7; durable/ephemeral `WhenFirst` residual drift
  ([DurableDriverSegmentRun.cs:475](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L475)
  vs [WhenFirstJoin.cs:46](../../src/OrcaCore.Engine.Ephemeral/Execution/WhenFirstJoin.cs#L46))
  → task 11.2; non-atomic registration
  ([DurableWorkflowRuntime.cs:74](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L74))
  → task 2.8; structural-only hot spin bounded only by elapsed deadline
  ([DurableDriverSegmentRun.cs:143](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs#L143))
  → tasks 2.3/3.5.
- **Green-field (no current enabling contract):** branch input projectors /
  private state / `BranchReturn<TResult>` / merge adapters;
  `ForEachItemOutcome<TResult>`; compiled plan + fingerprint; fiber/scope
  records + persisted scheduler position + scope-entry sequences;
  `FiberId`/`ScopeId`/`WaitSequence` owner fields (current records carry a
  descriptive `BranchScope` string only); durable child-cancellation protocol
  (absence drives compile-time rejection).

## Verification Assessment

- **OpenSpec:** `openspec status --change adopt-structured-fiber-execution
  --json` → all four artifacts `done`, `isComplete: true`;
  `openspec validate … --strict --no-interactive` → "valid".
- **Structural validation:** all six MODIFIED requirement headers re-matched
  against `openspec/specs/*/spec.md` baselines (unchanged and matching);
  design.md relative links and the proposal's enumerated requirement IDs
  cross-checked against `docs/specs/04/07/08/12/13/16` — every listed ID
  exists and each listed conflict is real (re-verified CP-004, SG-010,
  CR-015, DR-012, 13.4, AC-204/205, AC-403, DR-AC-032, CP-010..013,
  AC-601..605, concurrency design §3/§4).
- **Documents read in full this pass:** corrected proposal.md, design.md
  (15 decisions, 21-item checklist), tasks.md (12 sections), all nine spec
  deltas, the prior review's Remediation Update, and the updated re-review
  prompt. Canonical docs and concurrency change re-consulted at the cited
  anchors.
- **Code and tests:** git status confirms no `src/` or `tests/` changes since
  the prior review's full trace (all fifteen required files plus
  `ForEachNodeRunner`/`ForEachWorkScheduler`, `WhenFirstJoin`, catalog/
  registry/runtime/driver, and the durable/ephemeral/provider/integration
  test inventories). Prior `file:line` evidence carried forward and spot-
  re-verified where cited.
- **Commands run:** `git status --short`, the two `openspec` commands.
  **Not run:** `dotnet build`/`dotnet test` (review-only; baseline capture is
  task 1.3). Canonical documents are intentionally not yet amended — that
  work is gated behind task 1.2 and was verified as *scheduled*, not done.
- **Not verified:** benchmark contents; Redis/RabbitMQ/ZeroMq suites
  (unimplicated); runtime behavior of kernel saga commands (saga slice is
  sequenced last).

## Required Changes Before Apply

**None block `/opsx:apply`.** The following document-only clarifications are
required no later than their governing slices and are each a one-paragraph
edit best made immediately:

1. **[R-01, before task 5.5]** Define `ForEach.WhenAny` failed-first-terminal
   behavior (recommend: mirror `WhenFirst` — failed winner fails the scope
   without merge), the winner-merge input shape, and validation of
   `WhenAny` × failure-policy combinations; add the two scenarios.
2. **[R-02, before task 8.6]** State the lifecycle outcome of a rejected
   non-quiescent root `ContinueAsNew` (recommend: explicit workflow failure
   with diagnostic).
3. **[R-03, within task 1.2]** Add the baseline
   `runtime-resource-governance` "Saturation behavior is configurable"
   requirement to the 1.2 amendment list so its "async wait for a slot"
   wording is reconciled with the blocked-obligation/quantum-release rule.

Additionally, as conditions already encoded in the package: execute task 1.2
(canonical amendments) before any production code, and formally tick the
21-item Review Checklist at task 1.1 using this report's dispositions.

## Accepted Risks

**Deliberately accepted (no consumers, no production durable instances):**

- No cursor-envelope migration; format-1 data rejected, development stores
  reset; no dual-format runtime rollback (source-level rollback only).
- Breaking authoring changes: single root `Init`/`End` with outcome selector,
  `BranchReturn`, isolated branch/item state, removal of `WhenFirst.Ignore`/
  `LetRemainingComplete` and `ForEachResidualPolicy.LetRemainingComplete`;
  every existing composition test is rewritten (tasks 5.3, 11.2).
- Large-rewrite schedule risk, bounded by test-first vertical slices, the
  preserved cursor baseline, and retained host/provider seams.
- Canonical documents are temporarily ahead-of-amendment until task 1.2 runs;
  the enumerated precedence list in proposal Impact is the interim authority.

**Intrinsic to a durable engine (documented, not removable):**

- Step effects at-least-once; commits exactly-once. Merge is logically
  exactly-once only because purity is required — purity of user code remains
  guardrail-enforced, not provable.
- Plan fingerprint cannot detect changed step implementation semantics;
  definition-version discipline remains an authoring obligation.
- Envelope growth with deep nesting, dynamic `ForEach` items, and large
  results; depth/fiber/result/envelope limit defaults remain Open Question 2,
  gated by task 11.5 benchmarks.
- Cooperative one-quantum scheduling plus forced-rotation commits increase
  commit frequency; segment budgets amortize it, and 11.5 must measure it.
