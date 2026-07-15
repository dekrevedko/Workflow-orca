# Developer-Facing Interface Reshape — Post-Fiber OpenSpec Review (2026-07-14)

Reviewer scope: full consumer-perspective review of the rebased
`reshape-developer-facing-interfaces` OpenSpec change (proposal, design, tasks, all eight
spec deltas), the shared normative matrix `docs/specs/17-selected-mode-capability-matrix.md`,
the rebased `add-runtime-concurrency-limits` change, and the archived
`2026-07-15-adopt-structured-fiber-execution` baseline, all validated against the current
root `src/`, `tests/`, `samples/`, and `openspec/` trees and against the actual working-tree
rebase diff. Review-only; no source, spec, or proposal file was modified.

## 1. Verdict

**APPROVE WITH CHANGES**

Counts: **0 × P0, 1 × P1, 5 × P2, 5 × P3.**

Every rebase claim I could execute or read against source verified — most to the exact
line. The seven principal decisions are all sound; none is rejected. What blocks
`/opsx:apply` is a small set of document-level defects introduced or left behind by the
rebase itself: one delta modifies a baseline requirement under a heading that does not
exist in the baseline (so the superseded requirement would survive archive and contradict
the new one), the promoted canonical baseline still contains the fiber change's
pre-implementation reconciliation gate written from the archived change's perspective, one
fixture scenario contradicts the matrix's own branch rules, the sibling concurrency change
misclassifies two added requirements as modified, and the task graph defers canonical-spec
amendments that its own delta requires before source work. All fixes are text edits;
none disturbs the architecture.

Validation results (section 9): both changes strict-validate, `git diff --check` is clean.

## 2. Executive summary — implementation readiness

The rebase does what it claims: it converts `adopt-structured-fiber-execution` from a
pending prerequisite into an accepted, archived substrate, and it converts the
implementation-revealed public-surface problems (shared definition type, public compiled
IR, nested `WithPoolKey` leak, compiler-shaped builder options, fiber-routing fields on
`ActiveWaitSnapshot`) into explicit contracts and tasks. All ten source-fact claims in the
review brief are true today (section 3). Every requirement from the 2026-07-13 review
(F-01 through F-15) survived the rebase and is now encoded in a delta, the matrix, or a
task — I found no lost requirement.

The change is close to implementable. The one P1 is mechanical but real: the
workflow-contracts delta renames the requirement it modifies, so the archive merge cannot
replace the baseline "execution hints" requirement — which still permits authoring
pool-key metadata in any mode and would directly contradict the new mode-guaranteed rule.
The P2s are: a fixture scenario that overstates nested/root capability parity against the
matrix's own branch restrictions; the stale promoted reconciliation-gate requirement in
canonical `quality-and-verification` that textually still blocks the very source work this
change schedules; the concurrency change's two ADDED-as-MODIFIED requirements (which break
the strict-reconciliation state that reshape task 5.7 depends on); the task graph's
deferral of canonical-spec amendments to section 9 despite the delta requiring them before
source; and section-3 consumer/package fixture tasks that cannot be authored before the
section-6 packages exist.

After the blocking document edits land, source implementation may begin with task 3.1.

## 3. Claim-verification table

Every claim from the reviewer brief, checked against the current root source.

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | Both delivered mode-first builders return the same `WorkflowDefinition<TState>` | **Confirmed** | Shared base `Build()`/`TryBuild()` return `WorkflowDefinition<TState>` / `Validation<WorkflowDefinition<TState>>` — [SelectedWorkflowBuilder.cs:232](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L232), [:249](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L249); factories at [Workflow.cs:13,23](../../src/OrcaCore.Core/Building/Workflow.cs#L13) (no `WorkflowAuthoringOptions` parameter yet — matrix 17.2 is target, not current) |
| 2 | That definition publicly exposes `CompiledPlan` and still carries `RequiresDurableEngine` / legacy-plan behavior | **Confirmed** | Public `CompiledPlan` property [WorkflowDefinition.cs:52](../../src/OrcaCore.Core/Definitions/WorkflowDefinition.cs#L52), public `RequiresDurableEngine` [:47](../../src/OrcaCore.Core/Definitions/WorkflowDefinition.cs#L47), `CompiledWorkflowPlan.FromLegacy` fallback in the ctor [:26](../../src/OrcaCore.Core/Definitions/WorkflowDefinition.cs#L26); ephemeral registration still reads `RequiresDurableEngine` at [EphemeralWorkflowEngine.cs:118](../../src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs#L118); legacy `WorkflowBuilder<TState>` still public with `WithDefinitionRetry`/`WithPoolKey`/`RunChild` ([WorkflowBuilder.cs:15,102,111,153](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L15)) |
| 3 | Compiled plan, instruction, scope, branch, policy, compiler-option, serializer-registry, and fingerprint-source types are public | **Confirmed** | `CompiledWorkflowPlan` ([CompiledWorkflowPlan.cs:28](../../src/OrcaCore.Core/Compilation/CompiledWorkflowPlan.cs#L28)); `CompiledInstruction`, `CompiledScopePlan`, `CompiledBranchPlan`, `CompiledPolicyPlan`, `CompiledMergePlan`, `CompiledForEachPlan` and three enums all public in [CompiledPlanModels.cs](../../src/OrcaCore.Core/Compilation/CompiledPlanModels.cs); `DefinitionCompilerOptions`, `IWorkflowTypeSerializerRegistry`, `IWorkflowPlanFingerprintSource` public in [DefinitionCompilerOptions.cs:8,39,67](../../src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs#L8); public `WithCompilerOptions`/`WithTypeSerializerRegistry` on the shared builder base — both modes — at [SelectedWorkflowBuilder.cs:262,272](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L262) |
| 4 | Common nested `BranchBuilder<TBranchState,TResult>` exposes `WithPoolKey` inside durable structured authoring | **Confirmed** | [StructuredBranchBuilders.cs:152](../../src/OrcaCore.Core/Building/StructuredBranchBuilders.cs#L152); the durable root's `Parallel`/`WhenFirst` accept the same shared `BranchScopeBuilder<TState,TResult>` ([SelectedWorkflowBuilder.cs:173-208](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L173)), so the ephemeral-only decorator is IntelliSense-discoverable inside durable branches and rejected only later by the compiler |
| 5 | Root durable `ForEach` and root transient-pool authoring are absent and compiler-defended | **Confirmed** | `ForEach` and `WithPoolKey` exist only on `EphemeralWorkflowBuilder<TState>` ([SelectedWorkflowBuilder.cs:371,379](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L371)); durable compiler rejects `ForEach` nodes ([DefinitionCompiler.Capabilities.cs:18-22](../../src/OrcaCore.Core/Compilation/DefinitionCompiler.Capabilities.cs#L18)) and pool-key policies at both root-step and branch-instruction level ([:43](../../src/OrcaCore.Core/Compilation/DefinitionCompiler.Capabilities.cs#L43), [:94](../../src/OrcaCore.Core/Compilation/DefinitionCompiler.Capabilities.cs#L94)) |
| 6 | Public structural `Wait`, `WaitLong`, child workflows, and root-only continue-as-new are delivered | **Confirmed** | Structural `Wait` on the shared base ([SelectedWorkflowBuilder.cs:117](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L117)); durable-only `WaitLong` ([:454](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L454)), `RunChild`/`RunChildren` ([:465,480](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L465)), structural `ContinueAsNew` ([:445](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs#L445)); root-only quiescence is compiler/driver-enforced per the archived baseline (`SFE-RUN-001`) |
| 7 | Structural external-job and durable-lease authoring nodes are missing | **Confirmed** | No `RunExternalJob`, `AcquireResources`, or lease vocabulary anywhere under `src/OrcaCore.Core/Building` (repository-wide search) |
| 8 | Portable `StepResult` still contains durable-only variants | **Confirmed** | `ContinueAsNew<TState>`, `RunExternalJob`, `AcquireResources` at [StepResult.cs:36,44,56](../../src/OrcaCore.Abstractions/Steps/StepResult.cs#L36) |
| 9 | `ActiveWaitSnapshot` still exposes `FiberId`, `ScopeId`, and `WaitSequence` | **Confirmed** | [ActiveWaitSnapshot.cs:48,53,58](../../src/OrcaCore.Abstractions/Instances/ActiveWaitSnapshot.cs#L48) |
| 10 | Durable facade and management still have the claimed application/protocol problems | **Confirmed** | Phantom `TState` generic on `StartOrGetAsync<TInput,TState>` ([DurableWorkflowRuntime.cs:86](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L86)); start registers the definition as a side effect ([:125](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L125)); no-match/ambiguous correlation routing throws `WorkflowRoutingException` ([:212,218](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L212)); `RearmAsync` returns `DurableCommandResult` and takes `DurableRearmRequest` carrying protocol `StreamVersion` ([:276-281,326](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L276)); `DurableManagement` still creates a fresh fallback `DurableCommandProcessor` per mutation ([DurableManagement.cs:349](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L349)); `DestructiveCommandSafety` still has `Confirmed` as its sole zero/default member ([:362-368](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L362)); durable management has no `GetState`/`GetStateAsync` (repository search); `DurableSagaCommandAdapter`, `DurableDagRunner`, `RedisProviderProfile` all still present |

Additional current-state confirmations: `WorkflowAuthoringOptions`,
`EphemeralWorkflowDefinition<TState>`, `DurableWorkflowDefinition<TState>`, and
`DurableDefinitionHandle<TState>` do not exist anywhere in `src/` (correctly listed as
missing work); saga authoring is still one mixed `SagaBuilder<TState>` with
`Build`/`BuildValidated` and the DAG builder still exposes only `BuildValidated()`
([WorkflowDagBuilder.cs:43](../../src/OrcaCore.Core/Building/WorkflowDagBuilder.cs#L43)),
matching tasks 4.10/4.13. **No drifted claim was found**; the rebase describes the current
source accurately.

## 4. Findings

### [P1] R-01 — The workflow-contracts delta renames the requirement it modifies, so the superseded baseline requirement survives archive

- **Verdict:** confirmed (mechanical merge defect with semantic consequences)
- **Evidence:** the delta's `## MODIFIED Requirements` section contains
  "### Requirement: Host-facing execution **governance** remains optional and declarative"
  ([workflow-contracts delta:18](../../openspec/changes/reshape-developer-facing-interfaces/specs/workflow-contracts/spec.md)),
  but the promoted baseline heading is "Host-facing execution **hints** remain optional
  and declarative"
  ([openspec/specs/workflow-contracts/spec.md:33](../../openspec/specs/workflow-contracts/spec.md)).
  `openspec validate --strict` checks delta structure, not heading correspondence with the
  baseline, which is why both changes validate. At archive time the modification cannot
  bind to the baseline requirement: either the archive fails, or the new requirement is
  merged as an addition while the old one survives. The surviving baseline text says
  definitions or steps "carry optional, serializable metadata for host policies such as
  named resource pools" with a scenario in which "an author marks a step or node with a
  named pool identifier" in *any* mode — a direct contradiction of the matrix row
  "Named cross-instance transient pool: Ephemeral only / Absent until durable enforcement
  lands" and of the delta's own mode-guaranteed rule. The diff shows the heading was
  deliberately reworded during this rebase (previously it matched).
- **Impact:** the exact capability rule this change exists to pin — no durable transient-pool
  authoring, throttles as host policy — is contradicted by a canonical requirement that the
  change believes it is replacing but is not.
- **Recommendation:** keep the new body text but restore the exact baseline heading
  "Host-facing execution hints remain optional and declarative" in the MODIFIED section, or
  express the rename explicitly (REMOVED "…hints…" + ADDED "…governance…"). Verify with a
  dry archive or a heading-diff between delta MODIFIED headings and `openspec/specs/`.
- **Regression/acceptance evidence:** an archive dry run (or scripted heading comparison —
  every `### Requirement:` under a `## MODIFIED` section must exist verbatim in the
  corresponding baseline spec) added to task 10.9's strict-validation step.

### [P2] R-02 — The nested-capability fixture scenario contradicts the matrix's own branch rules

- **Verdict:** confirmed (internal contradiction)
- **Evidence:** quality-and-verification delta, scenario "Nested capability fixture
  compiles": "each nested builder exposes **the same selected-mode capability set as its
  root**". The matrix forbids exactly that: "Branches cannot contain workflow `Init`,
  workflow `End`, or `ContinueAsNew`" (17.2), and the delivered branch surface
  ([StructuredBranchBuilders.cs:81-240](../../src/OrcaCore.Core/Building/StructuredBranchBuilders.cs#L81))
  exposes only `Then`/`Wait`/`Delay`/`Parallel`/`WhenFirst`/`Return` plus step policies —
  no `WaitLong`, `RunChild`/`RunChildren`, or (future) external-job/lease nodes. The
  workflow-authoring delta states the correct rule — absence-preservation: "A capability
  absent at the root SHALL remain absent inside `Parallel`, `WhenFirst`, loops, and future
  nested scopes" — but neither the matrix nor any delta states which durable-only
  capabilities the new `DurableBranchBuilder` family *does* expose.
- **Impact:** an implementer writing the task 3.4 fixtures to the literal scenario would
  either produce an unsatisfiable fixture or widen durable branch builders with `WaitLong`
  / child-workflow / external-job methods beyond the delivered substrate — silently
  expanding runtime scope (branch waits today are `Resident`-mode only; a cold branch wait
  or branch-owned child group is undelivered driver behavior).
- **Recommendation:** rephrase the scenario to the subset rule ("a nested builder exposes
  no capability absent at its root, and durable branches cannot discover ephemeral
  transient pools"), and add one matrix sentence enumerating the initial nested capability
  set for each mode (the delivered portable branch surface for both; durable-only nodes
  inside branches explicitly deferred unless separately specified).
- **Regression/acceptance evidence:** the positive/negative nested compile fixtures assert
  the enumerated set exactly; the negative fixture proves `WaitLong`, children, transient
  pools, `ForEach`, `Init`, `End`, and `ContinueAsNew` are absent from durable branch
  builders.

### [P2] R-03 — The promoted canonical baseline still contains the archived fiber change's pre-implementation gate

- **Verdict:** confirmed (stale prerequisite language in `openspec/specs/`, not in the change docs)
- **Evidence:**
  [openspec/specs/quality-and-verification/spec.md:33](../../openspec/specs/quality-and-verification/spec.md):
  "Developer-surface reconciliation gates implementation — Compiler, builder-fixture, and
  structured-driver source changes SHALL NOT begin until **this change** and
  `reshape-developer-facing-interfaces` reference one joint capability matrix … and both
  changes pass strict validation." Written from `adopt-structured-fiber-execution`'s
  perspective and promoted verbatim at archive; "this change" now dangles in a canonical
  spec, and the requirement treats the completed fiber reconciliation as an active gate on
  "builder-fixture source changes" — exactly the work sections 3-4 schedule. The reshape's
  quality delta replaced its *own* gate requirement ("The archived structured-fiber
  baseline is accepted before consolidation") but does not modify or remove this baseline
  one.
- **Impact:** a strict reading blocks the reshape's own source work behind a gate that
  refers to an archived change; the next audit cycle will re-litigate it. This is the one
  place the repository still describes structured fibers as active prerequisite work.
- **Recommendation:** add a MODIFIED entry for "Developer-surface reconciliation gates
  implementation" to the reshape's quality-and-verification delta (superseding it with the
  accepted-baseline formulation, or retiring it into the new requirement), so the archive
  of this change cleans the baseline.
- **Regression/acceptance evidence:** task 10.9's canonical-link check confirms no
  canonical requirement references an archived change as an unmet prerequisite.

### [P2] R-04 — `add-runtime-concurrency-limits` misclassifies two added requirements as MODIFIED, breaking the strict-reconciled state the reshape depends on

- **Verdict:** confirmed (cross-change)
- **Evidence:** the concurrency delta
  ([runtime-resource-governance/spec.md](../../openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md))
  has a single `## MODIFIED Requirements` section containing three requirements.
  "Saturation behavior is configurable" matches the baseline
  ([openspec/specs/runtime-resource-governance/spec.md:52](../../openspec/specs/runtime-resource-governance/spec.md)),
  but "Transient governance is distinct from durable leasing" and the rebase-added
  "Builder discoverability is mode-guaranteed" have no counterpart among the nine baseline
  requirements — they are additions filed as modifications. Secondary: the concurrency
  proposal still lists `state-driven-runtime` under "Modified Capabilities" with no delta
  directory (the composition rule was already promoted by the fiber archive; task 1.2
  records accepting it).
- **Impact:** reshape task 5.7 and design Decision 11 gate durable transient-pool source
  work on a strict-reconciled `add-runtime-concurrency-limits`; that change cannot archive
  cleanly in this state, and the same silent-addition risk as R-01 applies.
- **Recommendation:** in the concurrency delta, split the file into `## MODIFIED` (the
  saturation requirement only) and `## ADDED` (the other two); drop or footnote the
  `state-driven-runtime` capability bullet in its proposal.
- **Regression/acceptance evidence:** same heading-diff check as R-01, run over both active
  changes.

### [P2] R-05 — The task graph defers canonical-spec amendments that its own delta requires before source work

- **Verdict:** confirmed (task/requirement contradiction)
- **Evidence:** quality delta, "Canonical requirements are updated before source":
  canonical `docs/specs/` requirements "SHALL be updated **before source implementation**
  for every changed public contract, including definition retry removal, concurrency
  taxonomy, dynamic waits, structural durable effects, registration, continuation,
  external-job failure, routing results, management time ownership, remediation, and
  package tiers." Design Decision 11 repeats it per slice. But the task graph contains no
  canonical-amendment task between 2.5 and 9.5: sections 3-8 are all guards and source
  work, and canonical updates first appear at 9.5/9.6 with traceability at 10.9. Known
  canonical targets already identifiable today: the stale gate (R-03); workflow-contracts
  "hints" (R-01 handles the delta side); workflow-authoring "Mode-first builders share one
  compiled plan contract"
  ([openspec/specs/workflow-authoring/spec.md:37](../../openspec/specs/workflow-authoring/spec.md)),
  whose scenario "the returned definition **contains** the same compiled-plan … contract"
  reads as public plan exposure and which names `reshape-developer-facing-interfaces` as
  the defining authority from inside a canonical spec; and DR-031/`StepResult` trigger
  semantics once section 5 lands.
- **Impact:** implementers follow tasks, not design prose; the canonical specs will
  describe deleted contracts (definition retry, `StepResult` external jobs, caller
  timestamps) for the entire implementation window, and the change's own delta requirement
  is violated from task 4.1 onward. The prior review flagged this (F-11); the rebase
  answered with the delta requirement but not with tasks.
- **Recommendation:** add a leading sub-task to each of sections 4-8 ("amend the canonical
  requirements affected by this section before its source slices"), or one section-2.6
  task enumerating the amendment list with per-section checkboxes, mirroring archived
  fiber task 1.2.
- **Regression/acceptance evidence:** task 10.9 already links changed canonical
  requirements to evidence; the added tasks make the link set non-empty per section rather
  than a bulk retrofit.

### [P2] R-06 — Section-3 package-consumer and provider-author fixture tasks precede the section-6 packages they reference

- **Verdict:** confirmed (task ordering / executability)
- **Evidence:** task 3.2 requires "clean package-consumer fixtures for … the small
  `OrcaCore` meta-package" and task 3.3 a fixture that "references
  `OrcaCore.Provider.Abstractions` plus its declared `OrcaCore.Runtime.Protocol`
  dependency" — projects and packages that are created by task 6.1. A fixture referencing
  a nonexistent package is not a failing guard; it is an unrestorable project that breaks
  the solution and CI. Task 4.14 then gates package moves on "the section-3 … fixtures"
  turning green, inverting the real dependency for 3.2/3.3. (Tasks 3.1 and 3.4-3.11 are
  fine: a signature-classification harness and Roslyn compile fixtures can genuinely fail
  against current source.)
- **Impact:** the first contributor to execute section 3 in order stalls at 3.2, or
  invents placeholder packages ad hoc — the kind of unreviewed improvisation the task
  graph exists to prevent.
- **Recommendation:** split 3.2/3.3 into "define the consumer/provider fixture harness and
  its assertions" (stays in section 3) and "instantiate the fixtures against the packed
  artifacts" (moves to section 6, immediately after 6.1), and reword 4.14 to gate on the
  authoring/definition fixtures only (its current text already says "authoring and
  definition fixtures", so only 3.2/3.3 need the move).
- **Regression/acceptance evidence:** section 10.3 already re-runs all consumer and
  provider-author fixtures against packed artifacts; no new gate needed.

### [P3] R-07 — Saga definitions remain one shared `SagaDefinition<TState>` while workflow definitions split by mode

- **Evidence:** matrix 17.2: "Saga authoring follows selected mode and produces
  `SagaDefinition<TState>` through `Build()`/`TryBuild()`", with both `Saga.Ephemeral` and
  the deferred `Saga.Durable` factory returning builders that complete into the same type.
  Decision 1's rationale — mode must be unrepresentable at registration, not inferred — is
  not applied to sagas.
- **Impact:** none until durable saga ships (gated behind tasks 8.5-8.7), after which the
  same registration-time mode-inference problem this change deletes for workflows
  reappears for sagas.
- **Recommendation:** before task 8.7, amend the matrix to either name mode-specific saga
  definition types or record explicitly why one shared saga definition type is acceptable
  (e.g., durable saga registration takes a different, explicitly durable path). A one-line
  matrix note now prevents an accidental contract later.

### [P3] R-08 — `WorkflowAuthoringOptions` content is not enumerated, and two current limits carry runtime-protocol vocabulary

- **Evidence:** the deltas define `WorkflowAuthoringOptions` by category ("positive
  definition limits, payload serializer/copy registration, deterministic fingerprint
  contribution") but never enumerate the limits. The current option set
  ([DefinitionCompilerOptions.cs:8-34](../../src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs#L8))
  includes `MaxInternalInstructionsPerQuantum` and `MaxSerializedEnvelopeBytes` — "quantum"
  and "envelope" are classified as runtime-protocol/implementation concepts by this
  change's own tier table.
- **Impact:** task 4.4's implementer must decide ad hoc which limits are "domain-facing"
  and what to rename them; verbatim migration re-leaks the vocabulary the change hides,
  while dropping them silently removes real guardrails.
- **Recommendation:** add the enumerated application option set (and application-vocabulary
  names for the two protocol-shaped limits, or an explicit statement that they remain
  host/engine configuration) to matrix 17.2 before task 4.4.

### [P3] R-09 — The "authored node/path" wait projection has no defined contract, and `WaitId`'s tier is unstated

- **Evidence:** matrix 17.6 and the management delta require wait snapshots to expose
  "authored node/path" but nothing defines its shape or stability. The compiler diagnostic
  contract (17.3) already defines "a structured location identifying the authored node" —
  the natural shared definition, but no document says they are the same. The delta bans
  `FiberId`/`ScopeId`/`WaitSequence` yet is silent on the remaining runtime identity,
  `WaitId` ([ActiveWaitSnapshot.cs:13](../../src/OrcaCore.Abstractions/Instances/ActiveWaitSnapshot.cs#L13)).
- **Recommendation:** one matrix sentence: the application wait path reuses the compiler's
  structured authored-location contract (stable across recompilation of an unchanged
  graph), and state whether `WaitId` stays application-visible (recommended: yes, as an
  opaque handle for event targeting) so the task 3.5/3.6 guards have an exact allowlist.

### [P3] R-10 — Stale evidence counts and an unexplained edit to a prior evidence document

- **Evidence:** the design context table still says `OrcaCore.Abstractions` has "201
  top-level public type declarations in the current source scan"; a scan today yields ~219
  top-level public types (the fiber change added envelope-v2/fiber-identity contracts).
  The working-tree diff also edits the *2026-07-13* fiber implementation-status document,
  changing "eleven additional correctness gaps" to "nine" — a post-hoc correction to an
  evidence document bundled silently into this rebase.
- **Recommendation:** refresh or de-precision the count ("~200 top-level public types at
  the 2026-07-13 scan; the fiber baseline added more"), and note the eleven→nine
  correction in the commit message or the status document itself so the audit trail
  explains why archived-review evidence changed.

### [P3] R-11 — Ephemeral `GetStateAsync` detachment semantics are unspecified

- **Evidence:** the management delta requires "typed **detached** root business-state
  inspection" from both adapters and specifies the durable mechanism (configured
  serializer over the committed checkpoint), but not the ephemeral one, where the root
  state is a live in-memory object. Returning the live reference would let management
  callers mutate executing runtime state — the opposite of "detached".
- **Recommendation:** one delta sentence: the ephemeral adapter produces the detached copy
  through the same registered serializer/deep-copy contract used for branch-input
  isolation, and a state type with no such contract yields the same typed
  incompatible-state diagnostic as durable mode.

## 5. Seven-decision disposition table

| # | Decision | Disposition | Notes |
|---|---|---|---|
| 1 | Distinct immutable `EphemeralWorkflowDefinition<TState>` / `DurableWorkflowDefinition<TState>` | **Approve** | Makes cross-mode registration unrepresentable at compile time without duplicating implementation (both wrap the same internal compiled plan; the shared-internals risk is covered by the "separate builders drift" mitigation and single-compiler rule). Deleting `RequiresDurableEngine` ([WorkflowDefinition.cs:47](../../src/OrcaCore.Core/Definitions/WorkflowDefinition.cs#L47)) and the `FromLegacy` fallback removes a verified runtime-failure path. Deep, mechanically enforceable, IntelliSense-clear. |
| 2 | Mode-specific public nested branch/scope builder families over one internal implementation | **Approve with change** | The leak is real and verified ([StructuredBranchBuilders.cs:152](../../src/OrcaCore.Core/Building/StructuredBranchBuilders.cs#L152) reachable from durable `Parallel`). A single generic-marker branch type with extension-method gating was considered as a smaller signature; it is weaker in IntelliSense (extension resolution surprises) and inconsistent with the delivered CRTP root pattern — four concrete families is the right call. Required change: fix the parity scenario and enumerate the nested capability set (R-02). |
| 3 | Compiler IR internalized; `WorkflowAuthoringOptions` carries domain-facing configuration | **Approve with change** | Internal/friend access is a sound cross-assembly strategy here: unsigned assemblies, both engines already ship from this repository, and the architecture tests (task 6.6) guard the boundary; the "implementation-only project" fallback is available if `InternalsVisibleTo` sprawls. The categories cover everything the current public options provide. Required change: enumerate the option set and rename the two protocol-vocabulary limits (R-08). |
| 4 | Static builders expose only mode-guaranteed capabilities; throttle = host policy; durable transient pools deferred; leases separate | **Approve** | Correctly dissolves the impossible "host-selected builder" promise — a static type cannot vary by later DI composition. The amendment path (matrix amendment + durable-host acceptance evidence, never a runtime probe) is explicit and coherent. Verified consistent across matrix 17.1/17.4, both deltas, and the concurrency change (modulo R-04's mechanical fix). |
| 5 | Structural external-job/lease nodes land before durable-only portable `StepResult` variants are removed | **Approve** | Sequencing (tasks 5.1-5.3 before 5.4) prevents a durable-capability gap, and direct lowering into the archived plan avoids a second effect-translation layer. Task 3.11's lease-lifecycle fixtures (normal exit, canceled branch, failed scope, terminal, crash/expiry) match the scope-owned obligation contract exactly. |
| 6 | `GetStateAsync<TState>` returns detached committed root state; wait snapshots project authored metadata | **Approve with change** | Root-only typed state has a precise rule (registered root state slot, never branch/item payloads — including same-CLR-type collisions, which the added scenario covers well). Authored wait facts are sufficient for operator tooling; routing identities stay at the certified advanced seam. Required changes: define the authored-path contract and `WaitId` tier (R-09) and the ephemeral detachment mechanics (R-11). |
| 7 | Legacy authoring and dual execution fallback deleted before package splitting and facade expansion | **Approve** | Consolidation-first is the lower-rework order: no dependency requires packages first, and moving `WorkflowBuilder<TState>`/`FromLegacy` into new package baselines would force a second round of approved-baseline churn. Task order (section 4 → 5 → 6) encodes it; only the 3.2/3.3 fixture placement needs correction (R-06). |

## 6. Missing requirements/scenarios and cross-change conflicts

Missing or underspecified (all covered by findings above):

1. Nested durable branch capability enumeration (R-02).
2. Canonical amendment of the stale reconciliation gate and the "compiled plan contract"
   scenario wording (R-03, R-05).
3. `WorkflowAuthoringOptions` member enumeration (R-08).
4. Authored wait-path contract and `WaitId` tier (R-09).
5. Ephemeral detached-state mechanics (R-11).
6. Mode-specific saga definition types, or a recorded exception (R-07).

Requirements from the 2026-07-13 review: **all fifteen findings survived the rebase.**
Split-host continuation (F-01), provider→protocol edge (F-03), rearm ticket (F-04),
duplicate-model consolidation (F-05), ephemeral retention (F-06), worker `FailAsync`
(F-07), typed handles without phantom generics (F-08), typed routing outcomes (F-09),
lease lifecycle (F-10), retained dynamic `WaitForEvent` (F-13), packaging + `TryBuild`
(F-14), and explicit registration (F-15) each have a matching requirement, matrix entry,
and task. F-11 (canonical amendments) is the one only partially carried — the requirement
exists but the task graph defers it (R-05). F-12's oversized tasks were split.

Cross-change conflicts:

| Topic | State | Resolution |
|---|---|---|
| Three-way concurrency taxonomy | **Aligned** across matrix 17.4, both reshape deltas, and the concurrency change; identical mode-availability wording | None needed |
| Mode-guaranteed builder discoverability | Aligned in text; the concurrency delta's new requirement is misfiled as MODIFIED | R-04 |
| Workflow-contracts "hints" baseline vs mode-guaranteed governance | **Contradiction survives archive** due to heading rename | R-01 (blocking) |
| Promoted fiber gate in quality-and-verification baseline | Stale; textually blocks reshape source work | R-03 |
| Archived fiber baseline (`ForEach` rejection, root-only quiescent continue-as-new, typed signatures, format-2 ownership) | Reshape consistently treats these as fixed substrate; no delta re-implies a `StepResult` continue-as-new origin; matrix 17.2/17.3 match the promoted `workflow-authoring`/`structured-fiber-execution` specs | None needed |
| Concurrency proposal lists `state-driven-runtime` as modified with no delta | Stale capability listing | Fold into R-04 |

Housekeeping observation (not a finding against this change): `openspec list` shows a
third in-progress change, `bootstrap-orcacore-spec-baseline` (0/8 tasks, last modified
2026-04-15). It is inert but appears in every list/validation sweep; archive or close it.

## 7. Task-graph and readiness assessment

Completed-task audit: tasks 1.1-1.9 and 2.1-2.5 (14 of 103) are marked complete with
verifiable evidence — the revised matrix exists and matches both changes, the archive
directory `2026-07-15-adopt-structured-fiber-execution` is present with all 102 tasks
complete and its verification matrix recorded, both changes strict-validate, and the
implementation-status document records 1,218 passed / 0 failed / 1 skipped. No task is
marked complete without evidence. (Task 1.9's phrasing "before structured-fiber
implementation" is historical wording of a completed task — acceptable.)

The overall sequence — guards (3) → authoring consolidation (4) → structural effects (5)
→ packages (6) → facade/management (7) → DAG/saga/hosting (8) → journeys (9) →
verification (10) — respects the real dependencies, correctly front-loads legacy deletion
before package moves, and correctly gates `StepResult` cleanup (5.4) behind structural
nodes (5.1-5.3) and durable saga authoring (8.7) behind runtime-owned progression
(8.5-8.6). Durable transient-pool exposure is correctly *not* on this change's critical
path (5.7 defers to the concurrency change).

Two corrections needed, both already described: move the package-dependent halves of
3.2/3.3 after 6.1 (R-06), and add per-section canonical-amendment tasks (R-05). With
those, the revised sequence is unchanged in shape:

1. Blocking document edits (section 8 below) + strict revalidation.
2. Section 3 minus package fixtures: 3.1, 3.4-3.11.
3. Section 4 (authoring consolidation, IR hiding, legacy deletion), gated by 4.14.
4. Section 5 (structural effects, then `StepResult` cleanup).
5. Section 6 (tiers/packages) + the relocated consumer/provider fixtures.
6. Section 7 (facade/management); section 8 may partially parallel it after 7.1.
7. Sections 9-10 as written.

## 8. Exact document-level changes required before `/opsx:apply`

Blocking (five edits, all text-only):

1. **`openspec/changes/reshape-developer-facing-interfaces/specs/workflow-contracts/spec.md`** —
   retitle the MODIFIED requirement back to the exact baseline heading
   "Host-facing execution hints remain optional and declarative" (keeping the new body and
   scenarios), or convert to REMOVED + ADDED. (R-01)
2. **`openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md`** —
   (a) add a MODIFIED entry superseding the baseline requirement "Developer-surface
   reconciliation gates implementation" (R-03); (b) reword the "Nested capability fixture
   compiles" scenario to the absence-preservation rule (R-02).
3. **`docs/specs/17-selected-mode-capability-matrix.md`** — add the nested-builder
   capability enumeration sentence for each mode in 17.2. (R-02)
4. **`openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md`** —
   move "Transient governance is distinct from durable leasing" and "Builder
   discoverability is mode-guaranteed" under a new `## ADDED Requirements` header; drop the
   stale `state-driven-runtime` bullet from that change's proposal. (R-04)
5. **`openspec/changes/reshape-developer-facing-interfaces/tasks.md`** — add the
   canonical-amendment lead task(s) for sections 4-8 (R-05) and split/move the
   package-dependent parts of 3.2/3.3 after 6.1, adjusting 4.14's wording if needed (R-06).

Then re-run `openspec validate … --strict` for both changes and the MODIFIED-heading
diff against `openspec/specs/`.

Improvements that may proceed during implementation (non-blocking): matrix enumeration of
`WorkflowAuthoringOptions` members and protocol-vocabulary renames before task 4.4 (R-08);
authored wait-path/`WaitId` contract before tasks 3.5-3.6 (R-09); saga definition-type
note before task 8.7 (R-07); ephemeral detachment sentence before task 7.10 (R-11);
evidence-count refresh and the eleven→nine correction note (R-10).

**Once the five blocking edits land and both changes revalidate, source implementation may
begin. The first safe task slice is task 3.1 (the public-signature inspection harness),
together with tasks 3.4-3.11 — all are executable against current source without any
package or authoring change.**

## 9. Commands run and validation results

All commands ran from the repository root on 2026-07-14 (working tree at `feature/v3-rebuild`,
HEAD `ba2478e9`, with the uncommitted rebase diff under review).

| Command | Result |
|---|---|
| `openspec validate reshape-developer-facing-interfaces --strict` | **valid** |
| `openspec validate add-runtime-concurrency-limits --strict` | **valid** |
| `openspec list --json` | reshape 14/103 in-progress; concurrency 7/15 in-progress; plus stale `bootstrap-orcacore-spec-baseline` 0/8 (see section 6) |
| `git diff --check` | clean (exit 0; only CRLF normalization warnings) |
| `git diff --stat` / full diff read | 16 files, +456/−232 — matrix, both changes' proposal/design/tasks/deltas, and a two-word edit to the 07-13 fiber status document (R-10); the complete rebase diff was reviewed, not only resulting files |
| Focused source verification | `Read`/`grep` over `SelectedWorkflowBuilder.cs`, `Workflow.cs`, `WorkflowBuilder.cs`, `WorkflowDefinition.cs`, `CompiledWorkflowPlan.cs`, `CompiledPlanModels.cs`, `DefinitionCompilerOptions.cs`, `DefinitionCompiler.Capabilities.cs`, `StructuredBranchBuilders.cs`, `StepResult.cs`, `ActiveWaitSnapshot.cs`, `DurableWorkflowRuntime.cs`, `DurableManagement.cs`, `EphemeralWorkflowEngine.cs`, plus repository-wide searches for `WorkflowAuthoringOptions`, mode-specific definition types, `DurableDefinitionHandle`, structural job/lease nodes, `RequiresDurableEngine`, `FromLegacy`, `BuildValidated`, `DurableSagaCommandAdapter`, `DurableDagRunner`, `RedisProviderProfile` — results in section 3 |
| Baseline heading cross-check | `grep '^### Requirement:'` over `openspec/specs/**` compared against every delta `## MODIFIED` heading — mismatches reported as R-01/R-04 |
| Public-type count | top-level public type declarations in `OrcaCore.Abstractions`: ~219 (design says 201 — R-10) |

No implementation was run or changed; per instructions, this review is documentation-only.

---

## 10. Remediation verification addendum (2026-07-14, same day)

The remediation recorded in
[developer-facing-interface-post-fiber-review-remediation-2026-07-14.md](developer-facing-interface-post-fiber-review-remediation-2026-07-14.md)
was re-verified against the updated working tree. **All 11 findings (1 P1, 5 P2, 5 P3) are
fixed as claimed; no fix introduced a new defect.** The repository is greenfield with no
compatibility obligation, and the updated task/design language now consistently frames
existing paths as provisional choices to delete, not to shim.

| Finding | Verified fix |
|---|---|
| R-01 | workflow-contracts delta MODIFIED heading restored to the exact baseline "Host-facing execution hints remain optional and declarative"; new mode-guaranteed body retained |
| R-02 | Quality scenario now states the absence-preservation rule; matrix 17.2 enumerates the exact initial nested capability set for both branch families (and correctly excludes `If`/`While`, matching the delivered branch surface) with an explicit amendment rule for additions |
| R-03 | Stale gate superseded via a MODIFIED entry for "Developer-surface reconciliation gates implementation" (archived baseline as supplier, not prerequisite); the previously duplicated ADDED gate requirement was removed |
| R-04 | Concurrency delta split into MODIFIED (saturation only) + ADDED (distinct-leasing, mode-guaranteed discoverability); the redundant `state-driven-runtime` delta and proposal capability bullet were deleted — its added semantics are already covered by the promoted `runtime-resource-governance` requirements ("Local fiber scheduling is distinct from host concurrency", "Resource waits do not retain an executing fiber quantum"), so no content was lost |
| R-05 | Design Decision 12 records the section 4-8 canonical-amendment map; tasks 2.6 (done) and new gate tasks 4.0/5.0/6.0/7.0/8.0 enforce it; 9.5 reworded accordingly |
| R-06 | Tasks 3.2/3.3 now define harnesses/assertions only; new tasks 6.2/6.3 instantiate them after 6.1 creates the packages |
| R-07 | `EphemeralSagaDefinition<TState>` / `DurableSagaDefinition<TState>` added to matrix 17.2 signatures, the saga-orchestration delta, and tasks 4.13/8.7 |
| R-08 | Matrix 17.2 enumerates the six-member `WorkflowAuthoringOptions` set with application vocabulary; the quantum/envelope limits are explicitly reassigned to engine/hosting configuration under non-leaking names |
| R-09 | Matrix 17.6 and the management delta define `AuthoredLocation` (shared with compiler diagnostics, stable for an unchanged graph) and keep `WaitId` as an opaque application-visible handle |
| R-10 | Design count de-precised (~200 pre-fiber / ~219 current); the eleven→nine correction now carries an editorial note in the fiber status document |
| R-11 | Management delta requires the ephemeral adapter to detach through the registered serializer/deep-copy contract and return the typed incompatible-state diagnostic instead of a live reference |

Re-validation: `openspec validate reshape-developer-facing-interfaces --strict` — **valid**;
`openspec validate add-runtime-concurrency-limits --strict` — **valid**; scripted comparison
of all 15 MODIFIED requirement headings across both changes against `openspec/specs/` —
**all match verbatim**; `git diff --check` — clean. Task counts: reshape 15/111,
concurrency 7/15; totals consistent with the task files.

**Final disposition: APPROVED. All blocking document changes are resolved; source
implementation may begin with task 3.1 together with tasks 3.4-3.11** (tasks 3.2/3.3 define
their harnesses only; their package-backed projects arrive at 6.2/6.3). The next gate after
section 3 is task 4.0 (canonical amendments) before the 4.1 definition-type work.
