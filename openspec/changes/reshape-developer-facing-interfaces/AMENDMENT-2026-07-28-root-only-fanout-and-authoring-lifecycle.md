# Amendment 2026-07-28 — Root-Only Fan-Out Confirmed, Authoring Lifecycle, Semantic Appendix

**Status:** revision 8 proposal-lifecycle remediation. Revision 7 corrected the remaining review
findings but retained the wrong sequencing by staging unapproved text in canonical artifacts.
Revision 8 restores the required order: proposal/amendment → independent approval → canonical
synchronization → implementation. Canonical specs and published documentation remain at their
accepted pre-amendment baseline; the exact proposed text lives in active deltas and change-local
artifacts pending task `4.15`. Product source remains unchanged.
**Proposes amendments to:** `reshape-developer-facing-interfaces` (design, specs, tasks),
`docs/specs/17-selected-mode-capability-matrix.md`, `openspec/specs/state-driven-runtime`,
`openspec/specs/structured-fiber-execution`, `add-runtime-concurrency-limits`,
`docs/ephemeral-engine-developer-guide.md`, and the proposed semantic appendix.
**Does not amend:** `docs/specs/17-public-authoring-contract.cs` — see §5.

## Revision history

**Revision 8** corrects the authoring lifecycle of the specification change itself:

- restore every affected canonical OpenSpec capability and published documentation artifact to the
  accepted pre-amendment baseline;
- keep the exact proposed normative replacements in the active change deltas, including the
  previously missing `state-driven-runtime` delta;
- move the semantic appendix from `docs/specs/` to the change-local
  `artifacts/semantic-appendix.md` publication draft;
- require independent approval of this proposal package under `4.15`;
- add post-approval canonical synchronization task `10.14` and block `4.16` and the remediation
  source slice until that synchronization is complete; and
- preserve every earlier request and verdict as immutable history.

**Revision 7** applies the Revision 6 independent-rereview disposition:

- retarget the live §9 plan delta and task `4.15` from rejected Revision 4 to corrected Revision 7;
- preserve every prior request and verdict as immutable history;
- qualify Revision 3's historical L4 shorthand with the current implementation-task-`4.16`
  publication gate; and
- keep `4.15`, `4.16`, and `6.0` open pending a fresh independent verdict.

**Revision 6** applies the consolidated disposition of the conflicting revision-4 reviews:

- keep `4.15` open and supersede revision 4 as the approval target without rewriting either
  immutable verdict;
- point L6 at the active `reshape-developer-facing-interfaces` durable-runtime delta and state that
  canonical promotion remains Section 6 work;
- apply §1.4.5's complete future-capability registry preamble and nested-fan-out re-entry bar;
- reopen `9.10`, `9.12`, and `10.13`, and bind the missing `state-driven-runtime` change delta
  explicitly to pending task `10.9`;
- restore the two omitted deliberately-excluded claims; and
- align every L4 publication gate on implementation task `4.16`.

**Revision 5** records the post-application review honestly without changing the root-only
decision or its normative substance:

- reopen `4.15` until an independent reader approves revision 4's eight mapped corrections;
- record the live `MaxActiveFibers` and fingerprint mismatches as `ExpectedRed` under pending
  tasks `5.13` and `5.14`;
- scope checked tasks `10.12` and `10.13` to planning/specification validation rather than
  product-source conformance; and
- make the semantic appendix's target-versus-current status explicit, including L5, and record the
  requirement to restore two excluded claims omitted during publication.

**Revision 4** applies the approved decision and corrects the final review findings discovered
while mapping revision 3 into the live artifacts:

- omit `MaxParallelBranchesPerScope`; no concrete v1 memory-safety requirement justifies a new
  semantic limit;
- qualify the two-quantity model to one structured root fan-out scope rather than the whole system;
- replace the nonexistent fixed-`MaxItems` counterexample with the real authored/value-envelope
  budget boundary;
- define builder ownership as phase- and scope-bound, not as persistent or genuinely linear;
- close `FailureOccurrence` to external derivation and construction without inventing structural
  equality for `WorkflowFailure`;
- correct capacity safety for downward resize debt and narrow path-token progress to token-only
  deadlock freedom;
- expand fingerprint repair to every unauthorized contributor and give
  `MaxInternalInstructionsPerQuantum` compiler-format ownership; and
- append Section 7 work at `7.14` and `7.15`, after the verified live maximum `7.13`.

**Revision 3** (responding to the second `REQUEST CHANGES`). All findings accepted.

| Finding | Disposition |
|---|---|
| §3 ceiling still wrong | **Accepted.** §3 rewritten around the reference model; `MaxActiveFibers` removed from all four roles; optional structural width bound proposed instead. |
| L4 scope | **Accepted.** Marked proposed and restricted to §2.3 lifecycle rejections. Revision 3 described publication as gated on §2.3; the current rule supersedes that shorthand and withholds publication until implementation task `4.16` lands. |
| `state-driven-runtime` rewrite | **Accepted.** Single option: rewrite around nested `If` and linear child execution; "recursive scopes" qualified in the parent requirement. |
| Evidence bar wording | **Accepted.** "every applicable encoding". |
| Fingerprint and codec tasks were prose only | **Accepted.** Numbered as `5.14`, `7.10`. |
| Proposal-list dispositions drifted | **Accepted.** `WorkflowFragment` → Deferred; extension members → optional later spike; scoped deadlines scheduled before Section 6; dashboard metrics tasked. |
| Compiler-format compatibility | **Accepted.** Retain every format referenced by a nonterminal durable instance; pre-v1 hard cutover permitted. |

**Revision 2** (responding to the first `REQUEST CHANGES`) accepted four P1s: live-fiber progress
unsound, expressiveness argument too strong, task-number collision, appendix citation failures.

The citation rule has now caught seven overstatements of mine across two rounds. That is the
argument for adopting it.

## 0. Summary

Root-only fan-out is confirmed. The analysis also surfaced one defect in authoring ownership, one
spec-versus-spec conflict, one conformance conflict, one implementation quantity with no
requirement behind it, and several overstated semantic claims.

Net effect on the v1 boundary: **unchanged**. Net effect on the exact public signature baseline:
**unchanged** except for failure provenance (§5).

| # | Item | Kind | Artifact |
|---|---|---|---|
| 1 | Root-only fan-out confirmed, with a scoped equivalence and the complete residue | reasoning | design, matrix |
| 2 | Authoring session / frozen definition | **defect fix** | design, spec, tasks |
| 3 | Root-fan-out concurrency model; `MaxActiveFibers` removed | **scope decision** | specs + tasks |
| 4 | Compiler limits vs. structural fingerprint | **conformance conflict** | spec + tasks |
| 5 | Failure provenance | small public surface addition | matrix, spec |
| 6 | Semantic appendix with a citation rule | new, non-normative | change-local publication draft |
| 7 | Cross-artifact reconciliation | **spec conflict** | `state-driven-runtime`, concurrency change |
| 8 | Persisted-collection allowlist | numbered codec task | tasks |

## 1. Root-only fan-out is confirmed

### 1.1 Decision

`Parallel` and `ForEach` remain root-only. `While` remains root-only. Nested fan-out remains
absent from every nested, branch, item, and leased builder. The existing rejection and its guards
are preserved unchanged.

### 1.2 The argument that decided it

| Journey | Shape | Unblocked by nested `Parallel`? | Root-only encoding |
|---|---|---|---|
| Per tenant, run several heterogeneous checks concurrently | `ForEach ∘ Parallel` | yes | flattening (§1.2.1) |
| Per region, deploy and verify concurrently | `ForEach ∘ Parallel` | yes | flattening (§1.2.1) |
| While processing batches, fan out the current batch | `While ∘ ForEach` | no — needs nested `ForEach` | flattening if batches are known up front and independent; otherwise none |
| In a parallel branch, process discovered items | `Parallel ∘ ForEach` | no — needs nested `ForEach` | staging (§1.2.2) |

**These encodings are scoped observational simulations, not equivalences.** Each holds only when
its preconditions are met (§1.2.3).

#### 1.2.1 Flattening — heterogeneous work within a group

Nested `If` is endo and unbounded in item bodies, so one item body simulates *n* heterogeneous
bodies by case analysis over a tagged item. The data path is sanctioned by the
`workflow-authoring` scenario *"Item needs shared parent data."*

| Property | Nested | Flattened | |
|---|---|---|---|
| Grouped failure reporting | per group | one ordered list; item carries group id | recoverable in merge |
| Mixed join policy per level | inner/outer differ | `WhenAllOutcomes` → merge → root `If` | recoverable |
| Sibling cancellation on failure | none | none | equivalent |
| Ancestor suppression | both merges suppressed | one merge suppressed | equivalent |
| Per-group concurrency limit | yes | one global `MaxConcurrency` | **lost** |
| Sequential dependency between groups | preserved | **removed** — all items are peers | **lost** |
| Item budget | groups + branches | groups × branches | **can cross a normative boundary** |

The last row is not merely quantitative. `MaxItems` is authored per node rather than a fixed
platform ceiling, but raising it does not remove the fixed-codec payload, snapshot, parent-state,
and envelope bounds. A flattened tagged-item snapshot can cross one of those fixed acceptance
boundaries even when each nested group's local data would fit. A representation rejected by a
normative value-size boundary is not an encoding of the original definition.

#### 1.2.2 Sequential staging — data-dependent fan-out

Both joins return the root builder and each merge replaces `TState`, so root scopes sequence
without bound and stage *n*'s results are stage *n+1*'s inputs:

```
W     ::= Init ; Stage* ; Term
Stage ::= Seq(Op) | If(…) | While(…) | Parallel(Branch⁺)⋈Merge | ForEach(items)⋈Merge
```

Journey 4 becomes two stages. Costs, both potentially disqualifying:

- **One barrier per stage.** A latency and throughput property.
- **Intermediate results are routed through parent state.** Nesting keeps a group's intermediate
  data fiber-local; staging must materialise the union into `TState`. Where that exceeds the
  parent-state, payload, or snapshot budget, staging is **rejected** even though each nested group
  would have fit. This is a capability difference, not a performance one.

#### 1.2.3 Preconditions and conclusion

A root-only encoding substitutes for a nested shape only when all three hold:

1. **Budget.** The flattened or staged form fits its authored `MaxItems` value and the applicable
   parent-state, payload, snapshot, and envelope limits.
2. **Dependency.** Flattening requires that later groups not depend on state committed by earlier
   groups; staging requires that a barrier between phases be acceptable.
3. **Observation.** Per-group concurrency, grouped failure attribution, and cross-group pipelining
   are either preserved or not required.

> Common bounded journeys have root-only encodings when their flattened or staged representations
> fit v1 budgets and preserve the observations the application relies on.

Nested `Parallel` unblocks two journeys that have encodings under these preconditions, and does not
unblock the two that need nested `ForEach`. The v1 decision stands; the residue is stated in §1.4.

### 1.3 Artifact changes

**`design.md` — Risks / Trade-offs.** Replace the existing root-only entry:

> - **[Root-only fan-out cannot express nested concurrency in v1]** -> Accept the smaller
>   first-release surface. The authored language is closed under *sequential* composition of
>   fan-out and open only under *nesting*. Two encodings cover common shapes, each conditionally.
>   **Flattening** handles heterogeneous per-group work but does not preserve per-group concurrency
>   limits or sequential dependency between groups, and its multiplicative item representation can
>   exceed a fixed payload/snapshot/envelope bound where a nested form would fit. **Sequential staging** handles data-dependent fan-out
>   at the cost of one barrier per stage, and routes intermediate results through parent state, so
>   it can exceed state/payload/snapshot budgets where nesting would keep the data fiber-local.
>   Neither is a general equivalence. The residue is unbounded data-dependent repetition; a future
>   amendment must define recursive identity, admission, merge, and lease rules.

**Matrix — root `Parallel` rows.**

> Root fan-out scopes compose sequentially without bound: each join returns the root builder and
> each merge replaces `TState`. Data-dependent fan-out is authored as successive stages, separated
> by one barrier per stage, with intermediate results materialised in parent state and therefore
> subject to state and payload budgets.

**Matrix — `ForEach` row.**

> Heterogeneous per-group work is authored by emitting one tagged item per (group, unit) pair and
> dispatching on the tag in the item body. Sanctioned only where flat item identity, an authored
> item bound plus fixed encoded-value budgets, flat failure aggregation, a single `MaxConcurrency`, and the
> absence of sequential dependency between groups are all acceptable. Not equivalent to nesting.

**Matrix — future-capability registry, nested `Parallel` row.**

> | Nested `Parallel` | Reviewed 2026-07-28 and declined for v1. Tagged-item flattening and
> sequential staging cover common cases under explicit budget/dependency/observation preconditions;
> nesting primarily removes barriers and does not solve unbounded data-dependent repetition. | A
> measured latency/throughput case the sanctioned encodings cannot meet; exact allowed locations;
> recursive identity and token/admitted-item semantics; merge/failure behavior; lease interaction;
> a sound conditional progress contract (§3); occurrence provenance as an ancestry path; and
> compiler/runtime evidence. |

## 1.4 Complete expressiveness residue

### 1.4.1 The nine prohibited nestings

| outer \ inner | `While` | `Parallel` | `ForEach` |
|---|---|---|---|
| **`While`** | fold into one loop plus a phase variable in `TState` | **lost** | **lost** |
| **`Parallel`** | **partially lost** | flatten, within preconditions | staging, within preconditions |
| **`ForEach`** | **partially lost** | flatten, within preconditions | flatten, within preconditions |

`While ∘ While` costs readability, not capability. `{Parallel, ForEach} ∘ While` — a branch or item
cannot loop; bounded polling survives as step retry, which encodes "wait until ready" as "fail
until ready". `While ∘ {Parallel, ForEach}` narrows to the case where the loop's continuation
condition depends on the parallel stage's results.

### 1.4.2 The four shapes that are not expressible

1. Converge until stable.
2. Drain a queue that the parallel work itself refills.
3. Unbounded retry of the failed subset (bounded retry unrolls into two or three staged scopes).
4. Unbounded per-item or per-branch polling.

### 1.4.3 The precise statement

> Root-only-but-repeatable fixes the **number of parallel stages at authoring time**. Any workflow
> whose count of parallel phases depends on runtime data is not expressible, and any workflow whose
> flattened or staged form exceeds a v1 budget is rejected even where a nested form would fit.

### 1.4.4 Conditional `ContinueAsNew` is the better future target

```
Parallel(…).WhenAll(merge)  →  If(converged) End  else ContinueAsNew(next state)
```

Its structural precondition is already satisfied: `ContinueAsNew` requires a quiescent root, which
is the state after a merge commits, and the absolute `CompleteWithin` deadline is inherited without
resetting. It needs no recursive identity, admission, merge, lease-ancestry, or concurrency work.
Its real cost is output and lineage semantics, since a definition would need both `End<TOutput>`
and `ContinueAsNew`, crossing the resultless/resultful arity split. Durable-only.

**This is an observation, not a proposal.** Conditional `ContinueAsNew` remains deferred with its
existing re-entry criteria unchanged.

**Recovery scorecard:** conditional `ContinueAsNew` recovers shapes 1–3; nested `While` recovers
shape 4; nested `Parallel` recovers none.

### 1.4.5 Artifact change and the nested-fan-out evidence bar

**Matrix — future-capability registry preamble.**

> Deferred capabilities are scored against the authoring shapes they restore, not against their
> individual appeal. As of 2026-07-28 the v1 residue is unbounded data-dependent repetition, plus
> the budget and observation preconditions under which root-only encodings substitute for nested
> ones. Conditional/finite `ContinueAsNew` restores the former at the generation level and is the
> cheapest such recovery; nested `While` restores unbounded per-unit iteration. Nested `Parallel`
> restores neither and was declined on that basis.
>
> **Re-entry bar for nested fan-out.** A concrete workload demonstrating material latency,
> throughput, memory, or **budget-acceptance** failure against **every applicable root-only
> encoding**. A definition rejected by `MaxItems`, parent-state, payload, or snapshot limits under
> every applicable encoding, but acceptable when nested, qualifies as budget-acceptance evidence.
> It is not claimed that the root-only encodings preserve every bounded workload.

## 2. Authoring session and frozen definitions

### 2.1 The defect

Public builder operations return the same mutable receiver, and completion builders capture a
deferred callback over that receiver, so `Build()` is a function of builder state *at `Build`
time*, not at `End` time. Both join methods can additionally mutate the same root through one
retained join builder. Currently ungoverned: stale aliases across a pending join, double join
selection, post-terminal mutation observed by an already-returned completion builder, handles
escaping their `Action<TBuilder>` callback, unstable repeated builds, and workflow-wide
configuration held per façade rather than per session.

### 2.2 Decision

Keep the fluent surface exactly as declared, over a lifecycle-controlled session with a frozen
internal AST. Builders remain mutable, phase- and scope-bound façades; they do **not** become persistent
builders, because `Action<TBuilder>` would become `Func<TBuilder, TBuilder>`.

```
Open(epoch 1)
   ├─ ordinary operator ──────────▶ Open(epoch 1)
   ├─ Parallel / ForEach ─────────▶ JoinPending(epoch 1)
   │                                  └─ WhenAll* ─▶ Open(epoch 2), new façade instance
   └─ End / ContinueAsNew ────────▶ Frozen(snapshot) ─▶ Build/TryBuild ─▶ immutable definition
```

### 2.3 Proposed requirement text

> ### Requirement: Authoring handles are phase-bound and definitions are frozen
>
> Workflow authoring SHALL be governed by one session whose state is `Open`, `JoinPending`, or
> `Frozen`. Every builder handle SHALL be valid only for the session epoch and lexical scope in
> which it was produced. A join SHALL return a new façade bound to the successor epoch. Applying
> an operator through a superseded handle, selecting more than one join for one scope, applying
> any operator after a root terminal, or using a nested/branch/item/leased handle after its
> authoring callback has returned SHALL fail with a catalogued diagnostic and SHALL leave the
> authored graph unchanged. A root terminal SHALL atomically freeze the authored graph; the
> returned completion builder SHALL build only that frozen snapshot. Repeated `Build()` or
> `TryBuild()` on one completion builder SHALL produce structurally equivalent definitions with
> identical ordered diagnostics and identical fingerprints. Concurrent authoring against one
> session SHALL admit at most one atomic winner; each rejected operation SHALL leave the graph
> unchanged. Workflow-wide authoring configuration SHALL be held by the session.

### 2.4 Clarifications

1. **Diagnostic codes and locations.** Each of the five failure modes needs a catalogued
   `SFE-AUTH-*` code with primary and related locations. These are new reservations.
2. **`TerminalSelected` is not a state.** Freezing is atomic with terminal selection; the state set
   is `Open`, `JoinPending`, `Frozen`.
3. **A join returns a new façade instance.** Otherwise the stale alias and the returned builder are
   the same object and epoch checking cannot distinguish them.
4. **Repeated builds are structurally equivalent, not reference-identical.**
5. **Concurrency race outcome.** One atomic winner; the loser's operation is rejected and the graph
   is unchanged.

### 2.5 Scope of the unchanged-graph guarantee

The atomicity guarantee above covers **only the five lifecycle rejections named in §2.3**. Other
eager authoring errors — misplaced or duplicated decorators, duplicate `CompleteWithin` — retain
their existing per-diagnostic behavior. Extending atomicity to every eager authoring error is a
separate decision and is not proposed here. L4 (§6.2) is scoped accordingly.

### 2.6 Timing note

`CompleteWithin` and the three-argument `Wait` overload currently validate and discard their
`TimeSpan`. These are expected-red Section 6 gaps. The session refactor SHALL create the storage
location; Section 6 supplies the behavior.

## 3. Concurrency quantities, and the removal of `MaxActiveFibers`

### 3.1 The reference model

Within one structured root fan-out scope, the structured-fiber scheduler owns exactly **two**
concurrency quantities, both already specified:

```
Fixed root Parallel(B)
    all B branch fibers exist at scope start
    branches queue for path tokens by authored ordinal
    parking releases a path token
    ⇒ no admission resource; fair token scheduling prevents token-only starvation

Root ForEach
    A = min(C_path, C_node) nonterminal items admitted
    C_node = ForEachOptions.MaxConcurrency when present, otherwise C_path
    parking releases a path token but RETAINS an admitted-item slot
    ⇒ admission is a real resource; completion is conditional (§6.2, L9)

Sequential root scopes do not overlap
    ⇒ no recursive footprint analysis is required
```

Sources: `structured-fiber-execution` "Structured scopes preserve the parent fiber" and "ForEach
reaches its effective admission limit"; `workflow-contracts` "Execution-path tokens have one
countable model".

Revision 2 claimed v1 had no third quantity. That was true of the specifications and **false of the
implementation** (§3.3).

### 3.2 Why revision 2's defensive ceiling was also wrong

Revision 2 proposed retaining `MaxActiveFibers` as an implementation-defined ceiling whose
exhaustion is a runtime failure. That makes workflow outcome depend on a non-authored quantity and
can differ across hosts or restarts, which is a determinism violation dressed as a safety net.
Withdrawn.

### 3.3 What the implementation actually does

`MaxActiveFibers` currently serves **four** distinct roles:

| Role | Site |
|---|---|
| Compile-time rejection of a computed peak | `DefinitionCompiler.Limits.cs:106-113`, `SFE-LIMIT-008` |
| Runtime admission cap on `ForEach` items | `ScopeReducer.cs:242` — `availableFiberSlots` bounds `Take(...)` |
| Runtime terminal failure | `ScopeReducer.cs:243`, throwing the same `SFE-LIMIT-008` at line 883 |
| Structural fingerprint contributor | `DefinitionCompiler.Fingerprint.cs:173` |

It is threaded through both engines — durable driver at four sites, ephemeral adapter at six. The
two runtime roles exist because the static peak calculation is known-incomplete (a `ForEach`
without `MaxConcurrency` contributes one), so the runtime throw is its backstop.

None of the four roles has a normative requirement behind it.

### 3.4 Decision

1. **Remove `MaxActiveFibers`** as compiler validation, runtime admission input, runtime terminal
   failure, and fingerprint contributor. Retire `SFE-LIMIT-003` and `SFE-LIMIT-008`.
2. **Do not add `MaxParallelBranchesPerScope` in v1.** Fixed branches are a finite authored list,
   and no concrete memory-safety requirement or evidence justifies a new semantic acceptance
   boundary. Allocation exhaustion remains an infrastructure concern, not authored workflow
   meaning.
3. **No implementation-defined ceiling may terminally fail a workflow.** If a defensive allocation
   failure remains, it SHALL be classified as infrastructure incompatibility or fault, not as
   authored workflow semantics, and SHALL NOT reuse an authoring diagnostic code.

### 3.5 Recorded as re-entry criteria, not v1

If a future nested-fan-out amendment introduces a live-fiber quantity, the sound form is
conditional and the condition must be stated:

> Given capacity `C ≥ parent + one child`, admission is fair and every child is eventually
> admitted **provided every admitted child eventually releases its slot independently of
> unadmitted siblings.** That proviso is not statically checkable: a parked child may await an
> event an unadmitted sibling would produce. A capacity model must therefore either require
> capacity sufficient for all fixed branches, or treat parked fibers as suspendable rather than
> slot-retaining, and must state the residual dependency risk. Ownership — host policy or compiler
> format — must be settled in one place.

## 4. Compiler limits and the structural fingerprint

Fingerprint coverage is stated three times as a closed list, and none includes compilation limits.
Any implementation folding limits into the structural fingerprint is non-conforming.

| Limit | Owner | Fingerprint |
|---|---|---|
| Scope depth | compiler format version | excluded |
| `MaxInternalInstructionsPerQuantum` | compiler format version / runtime fairness contract | excluded |
| `MaxActiveFibers` | **removed entirely** (§3.4) | n/a |
| Serialized result size | codec acceptance profile | excluded |
| Serialized envelope size | envelope / provider compatibility | excluded |

The repair covers more than compilation limits. The structural fingerprint SHALL exclude compiler
format, workflow mode, definition identity, and definition version because those are already
distinct binding values, as well as every compiler option. The codec format remains included
because the closed coverage clause explicitly authorizes it.

**Compiler-format compatibility rule.** A host SHALL retain support for every compiler format
referenced by a nonterminal durable instance. A format SHALL be retired only after those instances
terminalize or are explicitly migrated. Because no released package or durable-data compatibility
contract exists pre-v1, a format bump before first release MAY be a hard cutover when no supported
persisted instances exist.

`MaxInternalInstructionsPerQuantum` is the exception: its positive default of 1024 is already
normative in `structured-fiber-execution`. The remaining implementation constants do not become
normative merely because they exist; each requires workload and provider-certification evidence.

## 5. Failure provenance

`AuthoredLocation` answers *which authored instruction*, remains authored-only, and is not a
fingerprint contributor. Runtime occurrence identity is separate: under root `ForEach.WhenAll` a
single failed item propagates its `WorkflowFailure` unchanged, discarding the item index.

```csharp
public AuthoredLocation AuthoredLocation { get; }   // non-null, derived, authored grammar only
public FailureOccurrence Occurrence { get; }        // non-null closed union

public abstract record FailureOccurrence
{
    private protected FailureOccurrence();

    public sealed record Root : FailureOccurrence
    {
        internal Root();
    }

    public sealed record Branch : FailureOccurrence
    {
        internal Branch(AuthoredBranchId branchId);
        public AuthoredBranchId BranchId { get; }
    }

    public sealed record Item : FailureOccurrence
    {
        internal Item(int index);
        public int Index { get; }
    }
}
```

A non-null union with an explicit `Root` variant is chosen over null-means-root so absence is never
ambiguous. The `private protected` base constructor and internal variant constructors make the set
externally non-derivable and runtime-created. C# v1 does not provide compiler-enforced exhaustive
matching for this hierarchy; consumers use `switch` with a defensive default.

- **Attachment point.** Set when the failure is created, not when the scope joins.
- **Aggregate origin.** A synthesized `SFE-JOIN-FAILED` carries the occurrence of the scope's
  owning fiber; each ordered cause retains its own.
- **Construction.** `Branch` rejects a null identity and `Item` rejects a negative index.
- **Equality and copying.** Occurrence variants retain record value equality. `WorkflowFailure`
  retains its existing reference equality; detachment copies authored location and occurrence by
  value and copies causes recursively.
- **Round-trip.** Both round-trip under `orcacore-json-v1`; occurrence uses the versioned
  discriminator allowlist `root`, `branch`, and `item`.

No generalized ancestry path is added. `17-public-authoring-contract.cs` is unaffected.

## 6. Semantic appendix — non-normative, with a citation rule

### 6.1 Admission rule

> Every law SHALL cite the normative requirement it abstracts. A law with no citation is either a
> missing requirement or an invented promise, and the appendix SHALL say which. The appendix is
> non-normative and SHALL NOT create a promise the runtime does not make.

### 6.2 Laws

| Law | Statement | Cites |
|---|---|---|
| **L1** Commit-order invariance | For a **fixed set of logical child outcomes** `O`, `canon(π·O) = canon(O)`; the merge is applied to `canon(O)`. Does **not** quantify over schedules — different schedules may yield different `O` through timeouts, races, or external observation. | `state-driven-runtime` "Interpreter executes control flow deterministically" + scenario; `structured-fiber-execution` "Merge is explicit, deterministic, and side-effect free" |
| **L2** Committed-state replay | `π_state ∘ ⟦w⟧ ∘ crash = π_state ∘ ⟦w⟧`. External effects equivalent only under the create-or-observe assumption. | `durable-runtime` "Durable mutation is crash-safe", "Scope transitions and effects commit atomically"; `workflow-contracts` "Step operation identity is stable and opaque" |
| **L3** Codec idempotence | `enc(dec(enc(v))) = enc(v)` for supported, author-normalized `v`. Codec-level equality only; **not** CLR `Equals` or reference identity. | `workflow-contracts` "Durable values use one fixed detached codec" |
| **L4** Build agreement *(proposed)* | For every completion builder produced, `Build = orThrow ∘ TryBuild`. Eager local diagnostics are raised at the fluent call and are not members of `TryBuild`'s accumulated set. Reachability of `TryBuild` after an eager rejection holds **for the five lifecycle rejections governed by §2.3 only**; other eager authoring errors carry no atomicity guarantee. | `workflow-authoring` "Mode-first builders share one compiled plan contract", "Validation covers complete structured reachability"; **§2.3 of this amendment — proposed. Publish only after implementation task `4.16` lands.** |
| **L5** Fingerprint factorization | `h = h̄ ∘ q_∼`, where `∼` identifies terms differing only in opaque delegate content. Detection of structural drift **additionally assumes** `h̄` separates the mutations of interest; certification exercises designated mutations, and collision resistance is an assumption, not a theorem. | `workflow-contracts` "Executable plan identity is explicit" |
| **L6** Capacity accounting with resize debt | `ReservedUnits = Σ units(o)` over `PendingCommit`, `Held`, `ReviewMarked`, `AmbiguousHeld`, and `Quarantined`; `Debt = max(0, ReservedUnits - ConfiguredCapacity)`. Downward resize may make `ReservedUnits > ConfiguredCapacity`, but no new grant occurs while debt is positive or when the whole next request would exceed configured capacity. Availability recovery still requires external proof. | active `reshape-developer-facing-interfaces` durable-runtime delta "Durable leases are lexical occurrence-owned obligations" + active `quality-and-verification` delta lease-accounting scenarios; canonical promotion is post-approval task `10.14` |
| **L7** Path-token join freedom | No path token is held across a join edge, so path-token capacity alone cannot create a parent-held-token deadlock at a ceiling of one. This is neither item-admission progress nor global progress. | `structured-fiber-execution` "Local fibers use bounded execution-path scheduling"; `workflow-contracts` "Execution-path tokens have one countable model" |
| **L8** Fixed-branch existence | All `B` branch fibers of a fixed root `Parallel` exist at scope start and queue for path tokens by authored ordinal. There is no branch admission resource; every continuously runnable branch's access to a token depends on token fairness, while its eventual terminality may still depend on authored effects. | `structured-fiber-execution` "Structured scopes preserve the parent fiber"; `workflow-contracts` "Execution-path tokens have one countable model" |
| **L9** `ForEach` admission is conditional | At most `min(C_path, C_node)` nonterminal items are admitted; parking releases a path token but retains an admitted-item slot. Every item eventually terminalizes **provided admitted items do not depend on pending ones.** That proviso is a property of the authored work, not a runtime guarantee. | `structured-fiber-execution` "ForEach reaches its effective admission limit"; `workflow-contracts` "Execution-path tokens have one countable model" |

L8 and L9 replace the withdrawn live-fiber law: they state what the two quantities actually
guarantee, scoped to where each resource exists. L9's proviso is an existing v1 property, not a
new risk.

### 6.3 Claims deliberately excluded

- *Merge is symmetric.* No requirement says so; L1 replaces it.
- *Canonical ordering is schedule-invariant.* Only commit-order-invariant for fixed outcomes.
- *Replay stability covers external effects.* It does not.
- *Codec round-trip yields CLR-equal or identical objects.* Only codec-equal.
- *`TryBuild` is unreachable after an eager diagnostic.* It is reachable.
- *Every eager authoring rejection leaves the graph unchanged.* Only the §2.3 lifecycle rejections.
- *Structural drift is guaranteed detected.* Requires a collision-resistance assumption.
- *Global deadlock freedom.* Only path-token join deadlock freedom is provable.
- *`ForEach` items are unconditionally all eventually admitted.* Conditional; see L9.
- *V1 has no third concurrency quantity.* True of the specs, false of the implementation (§3.3).
- *Fan-out rank one is a computational complexity class.* Step bodies are arbitrary code.
- *`Peak(definition)` computable at compile time derives the capability boundary.* It does not.
- *The scope tree's acyclicity implies no circular wait.* The wait-for relation over a counted pool
  is not the scope tree.
- *Fixed `Parallel` requires whole-set reservation.* `WhenAll` requires terminality, not
  simultaneity.
- *Root-only encodings are equivalences.* Scoped observational simulations (§1.2.3).

This section is staged in change-local `artifacts/semantic-appendix.md` as a non-normative
publication draft. Task `9.12` publishes it under `docs/specs/` only after independent approval and
canonical synchronization. The draft carries the target-versus-current disclaimer and all fifteen
excluded claims. L4 remains withheld until implementation task `4.16` lands.

## 7. Cross-artifact reconciliation

**`openspec/specs/state-driven-runtime` — scenario conflict.** The requirement *"Interpreter
executes control flow deterministically"* says the runtime *"SHALL represent branching through
explicit recursive scopes"*, and carries a scenario *"Nested composition is interpreted"* whose
WHEN is *"a child fiber reaches another branch construct"*. Root-only fan-out makes that
unreachable.

**Rewrite the scenario around supported nested `If` and linear child execution**, and qualify or
remove "recursive scopes" in the parent requirement. Do **not** preserve unreachable nested fan-out
as a normative substrate capability — a specification must not describe behavior no accepted
definition can produce.

**`add-runtime-concurrency-limits` — coordination.** That change specifies path-token admission and
distinguishes admitted `ForEach` items from runnable tokens, which matches §3.1 exactly. With
`MaxActiveFibers` removed, the structured-fiber scheduler in one root fan-out scope owns exactly two
concurrency quantities — execution-path tokens and admitted `ForEach` items — and both changes must
assert that jointly.
This does not erase exact-step throttles, transient pools, durable leases, or the independent DAG
node ceiling elsewhere in the system.

## 8. Deferred and declined

| Proposal | Disposition | Reason |
|---|---|---|
| Nested fixed `Parallel` | **Declined for v1** | §1.2, §1.4 |
| Live-fiber capacity requirement | **Withdrawn**; `MaxActiveFibers` removed | §3 |
| Capability lattice replacing modes | Declined | Mode communicates persistence, replay, eviction, version binding, and `Parked` semantics. Orleans is another durable host. |
| Persistent/functional public builders | Declined | `Action<TBuilder>` would become `Func<TBuilder, TBuilder>`; §2 obtains the law dynamically. |
| Extension-member public surface | **Optional spike, later** | Reduces implementation declarations, not the member set each consumer sees; introduces capability interfaces and rewrites the normative companion and guards. Revisit only if declaration drift becomes painful; generating checked-in façade code from the normative contract is the preferred alternative. |
| `WorkflowFragment` primitive | **Deferred** | Ordinary C# helper methods over mutable builders cover today's reuse cases. Reconsider if a reuse case appears that helpers cannot express. |
| `ForEach₂` / product-index constructor | Declined | A Cartesian selector expresses the flat form; grouped semantics belong to nested fan-out. |
| `Call` / durable frame stack | Declined | A reserved envelope field settles none of the semantics. |
| `h_opaque` IL/MVID CI gate | Declined | Codegen noise becomes hard failures while transitive behavior changes stay invisible. |
| Blanket rejection of unordered containers | Declined as stated | Replaced by task `7.14`. |
| C# 15 unions / closed hierarchies | Deferred | .NET 11 preview. Keep closed abstract records, match via `switch`, avoid `is`+downcast. |
| Scoped branch/item/lease deadlines | **Explore before Section 6** — task `6.12` | `Delay` and retry backoff are effects, not deadlines; the semilattice covers `CompleteWithin`, `WithStepTimeout`, and `Wait` timeout only. |

## 9. Plan deltas

**Append-only numbering.** Verified pre-amendment maxima: section 4 ended at `4.14`, section 5 at
`5.9`, section 6 at `6.11`, and section 7 at `7.13`. Revision 8 keeps approval gate `4.15` open;
it does not redefine completed implementation work.

**Section 4.**

- `4.15` **REQUIRED gate, pending:** obtain fresh independent approval of Revision 8's amendment,
  active deltas, and change-local publication draft against the unchanged canonical baseline.
  Canonical synchronization is forbidden before this approval.
- `4.16` **Blocked by `4.15` and `10.14`.** Implement the authoring session:
  `Open`/`JoinPending`/`Frozen`, per-scope lifetime tokens,
  joins returning successor-epoch façades, session-owned workflow-wide configuration, atomic freeze
  at the root terminal.
- `4.17` Completion builders build only the frozen snapshot; repeated `Build`/`TryBuild` are
  structurally equivalent with identical ordered diagnostics and fingerprints.
- `4.18` Reserve and catalogue the new `SFE-AUTH-*` lifecycle diagnostics with primary and related
  locations.
- `4.19` Consolidate duplicated façade logic behind one internal authoring kernel; keep concrete
  mode/role-specific public builders.
- `4.20` Lifecycle regressions: superseded-handle use, post-terminal mutation, double join
  selection, escaped callback handle, repeated-build structural equivalence, concurrent authoring
  with one atomic winner.
- `4.21` Portable-intersection parity guard. **No public portable builder type.**

**Section 5.**

- `5.10` **REQUIRED gate, reopened:** after `4.15` approves the proposal, complete canonical
  synchronization under `10.14` before the remediation source slice.
- `5.11` Add `WorkflowFailure.AuthoredLocation` and the closed `FailureOccurrence` union per §5,
  attached at failure creation, with aggregate-origin, construction, copying, and codec round-trip rules. Owned
  by section 5 because task `5.1` owns `WorkflowFailure` and structured outcomes.
- `5.12` Reconcile the `state-driven-runtime` requirement and scenario per §7.
- `5.13` **Remove `MaxActiveFibers` in all four roles** (§3.3): compiler validation, `ScopeReducer`
  admission input, runtime terminal failure, and fingerprint contributor; retire `SFE-LIMIT-003`
  and `SFE-LIMIT-008`; update both engine call sites; add no replacement branch-width bound.
- `5.14` **Fingerprint conformance repair:** remove compiler format, workflow mode, definition
  identity/version, and every compiler option from the structural fingerprint while retaining codec
  format and inspectable authored structure; add a guard asserting the closed coverage list and
  bind `MaxInternalInstructionsPerQuantum` through compiler-format/runtime compatibility instead.
- **Current disposition: `ExpectedRed`.** The product source still exercises `MaxActiveFibers` in
  the compiler, reducer, both engines, and fingerprint path, and still includes unauthorized
  fingerprint contributors. Checked documentation tasks do not close these gaps; `5.13` and
  `5.14` do.
- `5.15` Regression asserting the tagged-item flattening journey compiles and merges as documented,
  including preservation of group identity and documented flat aggregation; separately prove an
  authored `MaxItems` violation and an encoded-value limit violation are rejected before partial
  admission without treating `MaxItems` as a fixed platform ceiling.

**Section 6.**

- `6.12` **Explore scoped branch/item/lease deadlines** before section-6 implementation: determine
  whether `CompleteWithin`, `WithStepTimeout`, and `Wait` timeout compose as one scoped
  meet-semilattice, and whether scoped deadlines are a v1 or deferred capability. Produce a
  decision record, not an implementation.
- `6.13` Store and enforce the workflow deadline and `Wait` timeout in the location created by
  `4.16` (existing expected-red gaps).

**Section 7.**

- `7.14` **Persisted-collection allowlist:** choose one sanctioned sequence representation and one
  sanctioned map representation whose enumeration semantics are part of `orcacore-json-v1`; reject
  every other declared or runtime collection shape before commit; decide explicitly whether
  dictionaries remain usable. Until this lands, the current "same normalized graph/order" author
  contract stands.
- `7.15` **Operational telemetry:** quarantined-unit gauge per pool, oldest-quarantined-obligation
  age, and fenced-bodies-still-running count, with dashboard documentation.

**Section 9.**

- `9.10` **Pending post-approval publication:** apply §1.3 and §1.4.5's proposed matrix wording
  only after `4.15`, documenting both sanctioned encodings, bulk-synchronous fork–join, the complete
  registry preamble, and the re-entry bar.
- `9.11` Document the authoring lifecycle.
- `9.12` **Pending post-approval publication:** promote
  `artifacts/semantic-appendix.md` to `docs/specs/18-semantic-appendix.md` after `4.15` and `10.14`,
  with every citation resolved and all fifteen excluded claims; withhold L4 until `4.16` lands.
- `9.13` Add the opaque-behavior version-bump checklist.

**Section 10.**

- `10.9` **Proposal traceability:** verify the change-local `state-driven-runtime` delta against the
  unchanged canonical baseline, compare every MODIFIED/REMOVED requirement before approval, and
  confirm no duplicate delta operation.
- `10.12` Re-validate both active change packages as planning artifacts, asserting exactly two
  structured-fiber scheduler quantities within a root fan-out scope, no retained-fiber resource,
  and no confusion with step/pool/lease/DAG admission elsewhere.
- `10.13` **Proposal-packet validation:** strict-validate this amendment's canonical-baseline and
  active-delta citations, every named future documentation mapping, and all change-local links,
  independently of product-source implementation. This task does not assert source conformance;
  the expected-red gaps remain under `5.13` and `5.14`.
- `10.14` **Post-approval canonical synchronization:** after `4.15`, sync every approved active
  delta into `openspec/specs/`, apply the approved matrix/guide wording, publish the semantic
  appendix, strict-validate the synchronized result, and record the exact sync diff. This task must
  complete before `4.16` or the remediation source slice begins.

**Guide.** After `4.15` approval, add to the existing v1-corrections block: v1 concurrency is
bulk-synchronous fork–join with flattening and staging as the two sanctioned encodings and their
preconditions; builder handles are phase- and scope-bound and definitions freeze at the root
terminal. Full rewrite remains `9.5`.

## 10. Review checklist

1. Is the scoped observational simulation with its three preconditions (§1.2.3) accepted as the
   justification of record, and the residue (§1.4) as its honest cost statement?
2. Is the reference model (§3.1) accepted as the normative statement of v1 concurrency?
3. Is removal of `MaxActiveFibers` in all four roles (§3.4), with no
   `MaxParallelBranchesPerScope`, approved?
4. Is the authoring-session requirement (§2.3) with its clarifications (§2.4) and its restricted
   atomicity scope (§2.5) correct?
5. Is the exact `FailureOccurrence` contract (§5) approved?
6. Are the nine laws (§6.2) accepted, including L8/L9 replacing the withdrawn live-fiber law and
   L4's gated publication?
7. Is the `state-driven-runtime` rewrite direction (§7) agreed, including qualifying "recursive
   scopes" in the parent requirement?
8. Is the compiler-format compatibility rule (§4) agreed?

## 11. What this amendment does not do

- It does not change `docs/specs/17-public-authoring-contract.cs`.
- It does not change any builder signature, generic arity, or member availability.
- It does not introduce a live-fiber capacity, footprint model, or reservation protocol.
- Revision 8 keeps approval gate `4.15` open; canonical synchronization task `10.14`, publication
  tasks `9.10`/`9.12`, and implementation task `4.16` remain pending.
- Proposal validation tasks `10.9` and `10.13` are complete; neither authorizes canonical
  synchronization or product implementation.
- It does not modify product source or claim that proposed delta text is already canonical.
- It does not authorize canonical synchronization before fresh independent approval.
