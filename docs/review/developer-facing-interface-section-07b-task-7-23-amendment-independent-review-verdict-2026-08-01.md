# Section 7B task 7.23 amendment — independent review verdict

**Date:** 2026-08-01
**Reviewer:** independent from-scratch review
**Review request:**
`docs/review/developer-facing-interface-section-07b-task-7-23-amendment-independent-review-request-2026-08-01.md`

## Verdict

**APPROVE**

Task `7.23` has established one coherent, exact, pre-source contract across the matrix, the
compile-shaped companion, the reshape design, and both new deltas. Every load-bearing claim was
reproduced independently. No previously reported result was trusted, and no external scratchpad
draft was consulted.

### What this verdict authorizes

- the implementation owner checking task `7.23` after consuming this verdict; and
- beginning task `7.24` guard retargeting in a later implementation turn.

### What this verdict does not authorize

- product implementation during this review;
- completion of tasks `7.24`–`7.34`;
- Section 7 exit;
- a checkpoint commit; or
- task `8.0`.

Current product code does not implement the amended contract. That expected-red implementation gap
was excluded from the verdict basis by the review request and was not treated as a defect.

## 1. Frozen target — reproduced twice

Reproduced once before validation and again after all validation. **Zero drift.**

| Anchor | Expected | Reproduced |
|---|---|---|
| HEAD | `ac46d99543daf85c0fa3234272997ba40f47f96b` | identical |
| HEAD tree | `28f4033c4773ea7761afa905c9836fd25866f1c0` | identical |
| porcelain entries (`--untracked-files=all`) | 430 | 430 |
| raw ordered SHA-256 | `db8d70860d6646f79e8c00f634a076131b390c626e1446a4f08b1fc8bdb49ef1` | identical |
| NUL-expanded normalized records | 460 | 460 |
| normalized LF SHA-256 | `fc290960af8b593645a1f50862d28c773e79de662ae46482326343bdf23baff0` | identical |
| capability directories | 16 | 16 |
| capability-directory SHA-256 | `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5` | identical |
| capability directories lacking `spec.md` | 0 | 0 |

The raw `git status --porcelain=v1 --untracked-files=all` output is **line-for-line identical** to the
checked-in manifest
`docs/review/developer-facing-interface-section-07b-task-7-23-amendment-dirty-manifest-2026-08-01.txt`
(`diff` empty; the checked-in file carries the same SHA-256).

The normalized anchor was computed with the exact Decision 6 pipeline: NUL split, one trailing `CR`
removed per record, ordinal byte sort, LF join plus one final LF.

As instructed, the 426-entry `--untracked-files=normal` synchronization freeze was **not** compared
directly against these anchors. The 430-entry target is independently reproducible on its own terms.

## 2. Artifact identity — all five reproduce exactly

| Artifact | Lines | SHA-256 |
|---|---:|---|
| `docs/specs/17-selected-mode-capability-matrix.md` | 2722 | `1B5BC96B6AA1641CB96F88AF61666306EB1F9FE78412F064BA8257FE28EF1F47` |
| `docs/specs/17-public-authoring-contract.cs` | 1287 | `41F6472C2774363D2AB922C608922E787EC241333E1D1C0B76B0C6D529AB8EC3` |
| reshape `design.md` | 573 | `DA0D0068842D120068537B63C661FE844E3923EFC62EF57B83BB4A935513FD6E` |
| `event-routing-and-waits` delta | 129 | `B56A88A05217437ACADAF04E1F6404AAC8225E9140053C2E94520BE7D10BBA2F` |
| `durable-persistence-and-outbox` delta | 106 | `946ED0CE5572AD703A94E1EC660BD26ADD92D065E3E6303BA276F576418AD528` |

The matrix status line records this as a Section 7B amendment pending independent approval, with
tasks 7.24–7.34 blocked until task 7.23 records that approval.

## 3. Semantic review — 16 of 16 verified

1. **`EventContractVersion` positive and immutable; exact name+version identity.** — PASS.
   Matrix 102–107 declares the immutable get-only `int Value` / `Initial` / `IEquatable<>` shape.
   Positivity is normatively asserted by the reshape `workflow-contracts` delta (`DefinitionVersion`
   and `EventContractVersion` SHALL be "distinct immutable non-defaultable reference values with
   positive validating constructors and `Initial`") and by design Decisions 23 and the `Wait`
   discussion. Payloadless and typed `WorkflowEventContract` descriptors carry exactly
   `EventName` + `EventContractVersion` (matrix 2457–2477, companion 27–52), with exact-ordinal
   identity and an explicit prohibition on CLR/assembly/serializer/destination/attribute identity.
   *Observation:* the matrix's strong-value prose paragraph (line 349) names only `DefinitionVersion`
   when stating the non-positive rejection rule. This is an incompleteness in restatement, not a
   contradiction — nothing in the matrix asserts an unbounded `EventContractVersion`.

2. **Four descriptor-based wait overloads at every structural wait location; no `EventName`-only
   wait.** — PASS. Machine-verified: 12 wait-capable builder families × exactly 4 overloads = 48
   declarations, with no other count anywhere. Matrix 541–557 declares the same four inline forms;
   `StepResult.WaitForEvent` (matrix 580–585) exists only in payloadless and typed descriptor form.
   A targeted scan for any `Wait(EventName …)` / `WaitForEvent(EventName …)` signature across the
   matrix, companion, and design returned zero hits.

3. **Exactly eight durable sequential builder families with exactly two `Publish` overloads;
   none elsewhere.** — PASS. Machine-verified: `DurableWorkflowBuilder`, `DurableNestedBuilder`,
   `DurableBranchBuilder`, `DurableItemBuilder`, `DurableLeaseWorkflowBuilder`,
   `DurableLeaseNestedBuilder`, `DurableLeaseBranchBuilder`, `DurableLeaseItemBuilder` — 8 families,
   2 overloads each, 16 total. All 14 ephemeral types expose zero. Completion, join, and DAG
   surfaces expose zero (`Publish` appears in the matrix only at lines 39, 559, 563, 2707, 2714 —
   never in the DAG section 17.2.6).

4. **`WorkflowEventRoute` closed to four cases.** — PASS. `Direct(InstanceId)`,
   `Correlation(DefinitionId)`, `DefinitionFanout(DefinitionId)`, and
   `StartOrDeliver<TInput>(DefinitionId, DefinitionVersion, StartIdempotencyKey, TInput
   WorkflowInput)`. The abstract base has a `private protected` constructor, closing external
   derivation. Workflow input is a distinct route member, never inferred from event payload (design
   486; both deltas). Matrix 2479–2490 and companion 1095–1110 are byte-equivalent after
   whitespace/body normalization.

5. **`WorkflowInboundEvent` payloadless and typed completeness.** — PASS. Contract, `EventId`,
   `CorrelationId`, optional `EventId? CausationEventId`, `DateTimeOffset OccurredAt` (UTC per
   Decision 24), `WorkflowEventRoute Route`; the typed form adds `new WorkflowEventContract<TPayload>
   EventContract` and `TPayload Payload`. Fixed-codec detachment is asserted by the
   `durable-persistence-and-outbox` delta ("Durable payloads cross boundaries through explicit
   serializers", `orcacore-json-v1`) and matrix 370–380. Matrix 2492–2536 and companion 1112–1162
   are byte-equivalent after normalization.

6. **`IWorkflowEventIngress` — exactly two `AcceptAsync` overloads, closed result.** — PASS.
   Byte-equivalent between matrix 2605–2613 and companion 1270–1279, owned by
   `OrcaCore.Durable.Hosting` in both.

7. **Rejection union is exactly five variants.** — PASS. `EventConflict`,
   `DirectInstanceNotFound`, `DirectInstanceTerminal`, `StartConflict(StartIdempotencyConflict)`,
   `FanoutLimitExceeded`. No sixth variant, no missing variant, in the matrix, the companion,
   design 478, or the `durable-runtime` delta.

8. **Identity before target state; only `Accepted`/`Duplicate` acknowledgement-safe; no durable
   `NoActiveWait`; no caller redelivery loop.** — PASS. Matrix 2673–2687 states identity comparison
   precedes route/target-state evaluation, that identical redelivery returns `Duplicate` even after
   progression or terminalization, that only `Accepted` and `Duplicate` mean durable ownership, and
   that no pending-event TTL or `NoActiveWait` result silently discards ownership. The
   `event-routing-and-waits` delta carries the matching requirement and both scenarios. Every
   `NoActiveWait` occurrence across the active changes is a negation or an explicitly superseded
   historical annotation; none is a live outcome.

9. **Decisions 23–25 agree with both deltas on buffering, races, cold activation, callback-only
   handoff, fanout membership, and start-or-deliver ownership.** — PASS. Direct events buffer in the
   target inbox; correlation events without a unique active wait buffer in a route-level inbox keyed
   by definition/contract/correlation; acceptance, registration, claim, timeout, cancellation, and
   consumption serialize to one committed winner; the `Accepted events reactivate cold durable work`
   ADDED requirement matches design 482; callback-only ingress commits envelope, pending start
   intent, and continuation handoff without loading definitions or executing workflow code; fanout
   snapshots the complete current nonterminal target set across versions, accepts an empty set,
   excludes later instances, and reuses membership on redelivery; start-or-deliver atomically owns
   one pending start intent keyed by `StartIdempotencyKey` bound to exact identity/version and
   normalized input. Matrix 2682–2696 states the same in the same terms.

10. **`WorkflowOutboundEvent` complete projection with descriptor-checked payload access.** — PASS.
    Contract, event, correlation, optional causation, origin instance/definition/version, occurrence
    time; payload reachable only through `GetPayload<TPayload>(WorkflowEventContract<TPayload>)`.
    No provider record, dispatch attempt, claim, or retry state is exposed. Matrix 2558–2579 and
    companion 1186–1210 compared identical with zero differences.

11. **One exact `DispatchAsync` signature; closed result; internal continuations isolated.** — PASS.
    `ValueTask<WorkflowEventDispatchResult> DispatchAsync(WorkflowOutboundEvent, CancellationToken =
    default)` in both artifacts and design 494. Result closed to
    `Succeeded`/`RetryableFailure(WorkflowEventDispatchFailure)`/`PermanentFailure(...)`. Matrix 2713
    and the `durable-persistence-and-outbox` delta both state internal continuation records use their
    own runtime pump and never reach the application dispatcher, with a dedicated scenario.

12. **Typed references; mode-specific engine builders with only their two `AddWorkflow` overloads;
    atomic staged batch.** — PASS. All four definition families expose a matching typed `Reference`
    (matrix 485–490, companion 123–205). `OrcaCoreEphemeralEngineBuilder` and
    `OrcaCoreDurableEngineBuilder` each expose exactly two `AddWorkflow` overloads accepting only
    their own mode's already-built definitions and returning the same builder; namespace
    `OrcaCore.Hosting` in both artifacts. Assembly ownership is fixed by the `developer-facing-surface`
    delta (`OrcaCore.Engine.Ephemeral` owns the ephemeral extension class and builder;
    `OrcaCore.Durable.Hosting` owns the durable extension class, builder, `IWorkflowEventIngress`,
    and `IWorkflowEventDispatcher`), which also mandates atomic staged-batch preflight before
    readiness with idempotent exact duplicates and no partial install — matching design 313 and
    matrix 2081–2099.

13. **`MissingWorkflowEventDispatcher` participates in compatibility validation.** — PASS.
    Present in the closed `DefinitionHostCompatibilityFailure` union (matrix 2204–2205) and in the
    `developer-facing-surface` delta. Matrix 2714 and design 496 both state a definition containing
    `Publish` is host-incompatible when no dispatcher is registered.

14. **Deferred registry no longer lists durable `Publish` or definition fanout; authored `Cancel`
    remains deferred.** — PASS within the reviewed scope, with one tracked residual (see §5.1).
    Matrix §17.6 contains no `Publish` or fanout row and retains `Workflow-authored Cancel`
    (line 2153). The reshape `developer-facing-surface` delta's MODIFIED requirement "Deferred
    capabilities are documented without public placeholders" drops both from the deferred list,
    retains `Cancel`, and adds the positive constraint that durable `Publish` and definition-fanout
    routing exist only through the specified durable inbox/outbox contract.

15. **Five superseded symbols absent from both artifacts.** — PASS. `IWorkflowEventClient`,
    `EventDeliveryStatus`, `EventDeliveryResult`, `DeliverToInstanceAsync`, and
    `DeliverByCorrelationAsync` each return zero occurrences in both the matrix and the companion.

16. **Matrix and companion agree exactly.** — PASS. Every event-surface block was compared under
    whitespace- and body-normalization: `WorkflowEventRoute`, both `WorkflowInboundEvent` forms, the
    rejection and acceptance unions, `WorkflowOutboundEvent`, the dispatch failure/result family, and
    the two hosting interfaces are identical member-for-member, including generic arity, overload
    count, parameter order, nullability, return types, and result closure. Every type the companion
    references but does not declare (24 distinct names) is declared by document 17. No invented
    member and no missing declaration was found. Three immaterial, non-contradictory differences are
    recorded in §5.

## 4. Mechanical validation — 9 of 9

| # | Check | Expected | Result |
|---|---|---|---|
| 1 | `openspec.cmd validate --all --strict` | 18 passed, 0 failed | **18 passed, 0 failed**, exit 0 |
| 2 | `git diff --check` | exit 0 | **exit 0**; 320 output lines, all `LF will be replaced by CRLF` advisories; **0** trailing-whitespace / space-before-tab / indent findings |
| 3 | Cross-change active-delta ownership scan | 175 entries, 0 duplicate owners | **175** entries; **0** duplicate `(capability, requirement heading)` owners; 0 repeated rows overall. Per change: reshape 158, add-runtime-concurrency-limits 7, harmonize 7, bootstrap 3 |
| 4 | Superseded-symbol scan | zero for all five | **0/0** in both artifacts |
| 5 | Companion placement scan | 12×4 waits, 8×2 publishes, ephemeral×0 | **12 builders × 4 = 48 waits; 8 builders × 2 = 16 publishes; 14 ephemeral types × 0** |
| 6 | Standalone C# syntax parse | 0 syntax errors | **0 syntax, namespace, and declaration errors.** All 306 diagnostics were `CS0246` (unresolved document-17 reference types), the single acceptable class — no `CS1xxx` parse error, no namespace error, no declaration error |
| 7 | `openspec.cmd list` and direct task count | reshape 107/158, 7.23 open, 7.24 open | **107/158**; tasks.md carries 158 checkboxes, 107 checked / 51 unchecked; **7.23 unchecked**, **7.24 unchecked** |
| 8 | No amendment change to `src/`, `tests/`, `samples/` | unchanged | **Established** — see method and limitation below |
| 9 | Anchors reproduce after validation | no drift | **All nine anchors and all five artifact hashes identical** |

### Additional verification performed beyond the required packet

- **Full semantic compile of the companion.** With reviewer-authored stubs supplying only the 24
  document-17 reference types (written outside the repository, never added to it), the companion
  compiles with **0 errors and 0 warnings** on `net10.0` with `Nullable=enable` and
  `LangVersion=latest`. This proves more than syntax: the payloadless/typed `Wait`, `Publish`, and
  `AcceptAsync` pairs are not ambiguous under overload resolution; the `WorkflowEventContract<TPayload>`
  and `WorkflowInboundEvent<TPayload>` `new`-hiding and `private protected` base-constructor chains
  are legal; and the nested `StartOrDeliver<TInput>` record legally derives from its closed base.

- **Delta heading provenance (the P1 class this project has previously shipped).** Every `MODIFIED`
  requirement heading in both new deltas matches its canonical heading **verbatim**: 7 of 7 in
  `event-routing-and-waits` and 4 of 4 in `durable-persistence-and-outbox` — complete coverage of
  each canonical requirement set, with no heading mismatch. The 1 + 2 `ADDED` requirements introduce
  genuinely new headings that collide with no canonical requirement. `openspec validate --strict`
  was **not** relied on for this; it checks structure, not provenance.

- **`## Purpose` hand-application exposure.** Both canonical Purpose sections are capability-scoped
  and generic; neither asserts a two-route or non-buffering model. These two deltas therefore create
  no silent Purpose-loss gap at synchronization time.

- **Harmonize ownership reconciliation, reviewed rather than assumed.** The
  `harmonize-downstream-capability-specs` change now owns only `event-driven-prototype`
  (2 ADDED, 4 REMOVED) and `state-driven-runtime` (1 MODIFIED). It no longer owns
  `event-routing-and-waits` or `durable-persistence-and-outbox`, so the contradictory
  non-buffering/fanout-removal ownership is genuinely gone — independently confirmed by the
  zero-duplicate-owner result in check 3. Its surviving `state-driven-runtime` requirement bars
  ephemeral exposure of durable-only capabilities "such as" root `ContinueAsNew` or scoped
  `AcquireResources`; the open-ended phrasing does not conflict with durable-only `Publish`.

### Check 8 — exact method and its limitation

The prior 426-entry synchronization manifest is **not checked in**, and the review request forbids
comparing the 426/456 anchors directly against this target's 430/460 anchors (different untracked
modes). A manifest-to-manifest diff was therefore impossible, and a `HEAD` comparison is
uninformative because `src/` and `tests/` are legitimately dirty from the ongoing uncommitted
refactor (191 and 156 manifest entries respectively).

Method actually used — filesystem modification times, with build output (`bin`, `obj`, `.vs`)
excluded:

| Tree | Source files | Newest source mtime | Files modified at/after 19:00 |
|---|---:|---|---:|
| `src/` | 364 | 2026-08-01 15:32:10 | **0** |
| `tests/` | 384 | 2026-08-01 15:48:02 | **0** |
| `samples/` | 31 | 2026-07-30 20:09:57 | **0** |

Every amendment artifact is strictly later: reshape `design.md` 19:22:53, the two deltas 19:33:12 and
19:33:15, the matrix 22:07:24, the companion 22:10:33, the manifest 22:16:01, the review request
22:17:51. The amendment window opens roughly 3.5 hours after the last source-tree modification, so no
amendment work touched `src/`, `tests/`, or `samples/`. Corroborating evidence: `git stash list` is
empty and the reflog shows no operation after the `ac46d99` commit — no reset, checkout, or stash
could have masked an intermediate source change.

**Limitation, stated rather than assumed:** modification times are not cryptographic provenance and
could in principle be rewritten. This conclusion is strong directional evidence, not a hash-level
proof. A checked-in manifest at each freeze would make this check fully reproducible in future gates.

## 5. Findings — none release-blocking

### 5.1 Tracked residual: the human-readable deferred registry still lists both capabilities

`docs/specs/13-phasing-and-open-questions.md` §13.4 — which the file itself and
`docs/normative-source-map.md` both identify as **the** future-capability registry — still contains
rows for `Workflow-authored Publish` and `Definition-targeted event fanout`, while the amended
contract ships both in v1.

This is **not** unexplained drift and **not** a gate failure, for three independently verified
reasons:

1. Removing them is **explicitly owned by open reshape task `9.6`**, which reads in part: "remove
   durable `Publish` and explicit fanout from that registry". No Section 7 task claims doc 13.
2. The **normative** requirement is already correctly amended in the reshape
   `developer-facing-surface` delta; doc 13 §13.4 is the human-readable inventory, and the source map
   records that doc 13 carries no requirement IDs.
3. The working-tree change to doc 13 (+5 lines) is a clarifying preamble naming §13.4 as the
   future-capability registry; it does not touch the table. Explained drift, consistent with the
   `HEAD` commit's cross-tree linking work.

The canonical `openspec/specs/developer-facing-surface/spec.md` likewise still lists both as
deferred. That is **correct by design**: canonical specs are derived and are synchronized only after
approval, and the review request itself states neither change may synchronize canonical specs before
approval.

**Recommendation (not a condition of approval):** carry §5.1 forward explicitly into the task 9.6
work item so the registry and canonical sync close together and doc 13 does not outlive the
amendment.

### 5.2 Matrix documentation-completeness observations

- The "exact availability table" at matrix 646–658 has **no `Publish` row**, and the leased-builder
  enumeration at 1054–1056 lists "step/`If`/wait/delay/return" without naming `Publish`. Neither
  passage *excludes* `Publish` — the leased exclusion list is explicit and names only fan-out,
  `AcquireResources`, and `ContinueAsNew` — and placement is unambiguous from §17.1 row 39 ("Durable
  sequential root, nested, branch, item, and leased builders") plus the companion's exact 8 × 2
  realization. Worth closing when task 7.24 authors placement guards, so guards are not written
  against the incomplete table.
- `TSelectedDurableBuilder` (matrix 559, 563) is not defined in the inline-name paragraph at 635–643
  that defines `TBuilder`, `TRootBuilder`, `TSelectedEphemeralBuilder`, and `TSelectedNestedBuilder`.
  It is covered only by the blanket rule at 76–81 that any name beginning with `TSelected` is a
  compact index into the companion. Adequate, but weaker than the sibling names.

### 5.3 Immaterial artifact differences (verified non-contradictory)

- The companion declares `Equals(EventContractVersion?)` and `Equals(WorkflowEventContract?)`, which
  the matrix implies through `IEquatable<T>` and omits uniformly for all its strong values. These are
  **required** to make the companion valid C#, not invented members.
- The companion's engine-builder internal constructors are parameterless where the matrix shows
  `(IServiceCollection services)`. Internal constructors are not public surface, and the companion
  deliberately avoids a `Microsoft.Extensions.DependencyInjection` reference. The two public
  `AddWorkflow` overloads are identical in both artifacts.
- The companion carries throwing bodies and `= null!` initializers absent from the matrix's
  declaration-only listings; its own header declares this is a declaration artifact, not product
  source.

None of these alters a public member, generic arity, overload count, placement, result closure, or
return type.

## 6. Basis for the verdict

`REJECT` was tested against every listed ground and none was met: no semantic contradiction, no
missing or invented member, no incorrect placement, no stale two-route or non-buffering contract, no
duplicate delta ownership, no unverifiable target, and no unexplained worktree drift. The matrix,
companion, design, and both deltas describe one exact contract, and all reproducible evidence
remained stable across a full re-run after validation.

## 7. Review hygiene

- No source, test, sample, OpenSpec artifact, normative document, task file, manifest, or existing
  review record was edited.
- Task `7.23` was **not** checked by this review; tasks `7.24`–`7.34` were **not** started.
- All probes, the stub compile, and every derived file were kept outside the repository.
- No commit was created.
- This verdict is the sole new path added to the repository.
