# Revision 8 — independent review verdict for task 4.15

**Date:** 2026-07-28
**Verdict:** `APPROVE`
**Scope:** proposal, amendment, delta, and planning coherence only. No product implementation was
reviewed, executed, or edited.
**Authorizes:** task `4.15` only — the implementation owner may mark `4.15` complete and begin
task `10.14`.
**Does not authorize:** canonical synchronization beyond `10.14`'s own scope, product
implementation, task `4.16`, task `5.10`, or task `6.0`.

Revision 4's historical approval is not carried forward. This verdict was derived independently
against Revision 8's text and the unchanged accepted canonical baseline. The owner's approval of the
mathematical direction was treated as context only and did not enter the determination.

This file is immutable. It edits, replaces, renames, and deletes nothing.

---

## 1. Review provenance

### 1.1 Target — independently reproduced

```bash
git rev-parse --abbrev-ref HEAD ; git rev-parse HEAD ; git rev-parse HEAD^{tree}
```

| Item | Recorded in request | Independently reproduced | Match |
|---|---|---|---|
| Branch | `feature/v3-rebuild` | `feature/v3-rebuild` | ✅ |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` | ✅ |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` | `2264e670493ecc76359d42ee5273028eb287a566` | ✅ |
| Self-inclusive sorted porcelain entries | 386 | **386** | ✅ |
| LF-normalized sorted-status SHA-256 | `e53ef4ec598797237f92154fd7f8437487c1d9a8dcd9e1817ab713a1e34e297e` | not reproducible — see finding **I-1** | ⚠️ |
| Reshape tasks | 61 / 75 / 136, 0 duplicate IDs | **61 / 75 / 136, 0 duplicates** | ✅ |

### 1.2 Authority order applied

Reviewed in the order the request prescribes: (1) `docs/specs/17-selected-mode-capability-matrix.md`;
(2) `docs/specs/17-public-authoring-contract.cs`; (3) canonical baseline under `openspec/specs/`;
(4) proposal, design, deltas, and amendment under
`openspec/changes/reshape-developer-facing-interfaces/`; (5) `add-runtime-concurrency-limits`;
(6) the Revision 8 amendment; (7) `artifacts/semantic-appendix.md`; (8) live tasks and the phased
plan. Historical requests and verdicts were read as evidence, not as current authority.

### 1.3 Immutable historical artifacts — preserved

All present; SHA-256 recorded for future audit:

| SHA-256 | File |
|---|---|
| `f001016f92cf056aa1cf6e99203353112504500c1c5e582625ae3b607a41bbad` | `...section-05-exit-review-dirty-manifest-2026-07-27.txt` |
| `53fcb5818fae78f725b1676a2ebaf6fab00c1595ac83fc7bd1bcf3dce17bfa2e` | `...section-05-exit-review-request-2026-07-27.md` |
| `055d4ca879ec7b431f7d92f14074dcd7ba2e61cbd66dd2f38028d188e79b758c` | `...amendment-rev04-task-4-15-independent-approval-verdict-2026-07-28.md` |
| `eff9c17f05cd7ea2d570ad0b5b22e9e1a86f53bb1d7bea490e553976cc5fe607` | `...amendment-revision-06-independent-rereview-request-2026-07-28.md` |
| `c8420e8819377f3037bf303680ea60b57f42dc2375d8f88dea4f0ddda9bdd026` | `...amendment-revision-06-independent-rereview-verdict-2026-07-28.md` |
| `6ded8ad9f0e23a87e38c4977af21ed283e9f0a3fd0a6309d3aaa04e4f6215d4a` | `...amendment-revision-08-independent-review-request-2026-07-28.md` |

The Section 5 manifest hash matches the required
`F001016F92CF056AA1CF6E99203353112504500C1C5E582625AE3B607A41BBAD` exactly, and the file holds
**370** entries. The Revision 4 and Revision 6 verdicts are both present and were not modified.

**Frozen-manifest set comparison:**

```bash
sed 's/\r$//' <manifest> | LC_ALL=C sort > man370.txt
git status --porcelain | sed 's/\r$//' | LC_ALL=C sort > r8_status.txt
comm -13 r8_status.txt man370.txt   # frozen entries lost
comm -23 r8_status.txt man370.txt   # entries added since freeze
```

- `comm -13` → **zero rows**. Every one of the 370 frozen entries survives.
- `comm -23` → **16 rows** (386 − 370), listed in §5.3. Filtering them for
  ` (src|tests|samples|benchmarks)/` returns **NONE**.

---

## 2. Determination results

| # | Required determination | Result |
|---|---|---|
| 1 | Revision 8 requirements isolated from canonical specs and published documentation | **CONFIRMED** (§3) |
| 2 | Every proposed normative change represented in active deltas, incl. `state-driven-runtime` | **CONFIRMED** (§4) |
| 3 | Every MODIFIED/REMOVED requirement resolves against canonical or historical `HEAD` | **CONFIRMED** — 54 current canonical, 1 historical `HEAD`, 0 unresolved (§4.2) |
| 4 | Zero duplicate delta operations | **CONFIRMED** — 0 duplicates at both `(change, capability, requirement)` and `(capability, requirement)` (§4.3) |
| 5 | All eight §10 questions independently answered | **CONFIRMED** — all eight affirmative (§6) |
| 6 | Corrected dependency ordering, prospective ExpectedRed, `6.0` blocked | **CONFIRMED**, with advisory **I-2** (§7) |
| 7 | Product source and `17-public-authoring-contract.cs` unchanged by Revision 8 | **CONFIRMED** (§3.2, §5.3) |

---

## 3. Determination 1 — proposal isolation

### 3.1 Canonical specs carry no Revision 8 text

```bash
grep -rn "Authoring handles are phase-bound\|JoinPending\|FailureOccurrence\|\
authored and occurrence provenance\|MaxActiveFibers\|live-fiber\|\
MaxParallelBranchesPerScope\|SFE-AUTH-LIFECYCLE" openspec/specs/
```
→ **zero matches.**

```bash
grep -rn "SHALL NOT include compiler format\|compiler acceptance or fairness limits\|\
path-token capacity alone\|retain every compiler format referenced by a nonterminal\|\
hard cutover\|no separate branch or live\|\
Compiler format, workflow mode, definition identity/version, and compiler options" openspec/specs/
```
→ three matches, all pre-existing accepted Section-5 *parked-item-slot* wording
(`quality-and-verification:171,183`, `structured-fiber-execution:108`). None is Revision 8
proposal text: every Revision-8-specific marker — the live-fiber negation, the narrowed
`path-token capacity alone` phrasing, the fingerprint exclusion enumeration, and the
compiler-format retention rule — is **absent** from canonical.

`openspec/specs/` shows five modified files (`durable-runtime`, `quality-and-verification`,
`structured-fiber-execution`, `workflow-authoring`, `workflow-contracts`). Each modification is the
previously accepted Section 4/5 canonical synchronization, not Revision 8 content — confirmed by the
greps above and by heading comparison (canonical already carries the Section-5 renames
`Local fibers use bounded execution-path scheduling` and
`Bounded root ForEach uses dynamic isolated item fibers`, which Revision 8's delta then MODIFIES).

`openspec/specs/state-driven-runtime/spec.md` and `openspec/specs/runtime-resource-governance/spec.md`
are **clean**. `state-driven-runtime` still carries its pre-amendment text verbatim — line 13 retains
"SHALL represent branching through explicit recursive scopes" and lines 19–20 retain the unreachable
scenario "Nested composition is interpreted / a child fiber reaches another branch construct". The
reconciliation exists only as a proposed delta. This is exactly the required isolation.

### 3.2 Published documentation untouched

```bash
git status --porcelain -- docs/specs/          # → empty
git status --porcelain -- docs/ephemeral-engine-developer-guide.md   # → empty
```

- **Capability matrix diff: empty** ✅
- **Ephemeral guide diff: empty** ✅
- **`docs/specs/17-public-authoring-contract.cs` diff: empty** ✅ (§5.1, command 2)
- `grep -n "18-semantic-appendix\|semantic appendix" docs/specs/17-selected-mode-capability-matrix.md`
  → **zero matches**. The matrix contains no reference to an unpublished document, so isolation
  leaves no dangling link. This is a genuine improvement over the Revision 4 application, where the
  matrix referenced an appendix under `docs/specs/`.

### 3.3 Appendix location

```bash
ls docs/specs/18-semantic-appendix.md
# → No such file or directory
```

**Absent** ✅. The appendix exists only as the change-local publication draft
`openspec/changes/reshape-developer-facing-interfaces/artifacts/semantic-appendix.md`, whose status
line correctly states publication "SHALL NOT" occur under `docs/specs/` before approval and canonical
synchronization.

---

## 4. Determinations 2–4 — delta completeness, baseline resolution, duplicates

### 4.1 Operation inventory

Every `## <OP> Requirements` / `### Requirement:` pair was parsed from both change packages:

| Scope | ADDED | MODIFIED | REMOVED | Total |
|---|---:|---:|---:|---:|
| `reshape-developer-facing-interfaces` | 80 | 44 | 6 | **130** |
| `add-runtime-concurrency-limits` | 2 | 5 | 0 | 7 |
| Combined | 82 | 49 | 6 | 137 |

Reshape total **130** and reshape MODIFIED+REMOVED **50** reproduce the recorded claim exactly.

Per-capability (reshape): `developer-facing-surface` 15, `durable-runtime` 17,
`management-and-querying` 12, `quality-and-verification` 24, `repository-foundation` 8,
`saga-orchestration` 6, **`state-driven-runtime` 1**, `structured-fiber-execution` 9,
`workflow-authoring` 20, `workflow-contracts` 18.

### 4.2 Baseline resolution

Each MODIFIED/REMOVED heading was resolved by exact `### Requirement: <name>` match, first against
current canonical `openspec/specs/<capability>/spec.md`, then against `git show HEAD:<same path>`.

| Resolution | Combined | Reshape only |
|---|---:|---:|
| Current canonical | 54 | **49** |
| Historical `HEAD` | 1 | **1** |
| **Unresolved** | **0** | **0** |

The single historical case:

```
HISTORICAL-HEAD  reshape  durable-runtime  REMOVED  Wait and WaitLong have distinct residency behavior
```

Sound. `WaitLong` was deleted by accepted task `4.9`, so the earlier accepted canonical
synchronization already removed this requirement; the delta's REMOVED operation correctly resolves
against the historical `HEAD` baseline. This is precisely the case the request anticipated. Reshape
figures reproduce the recorded "49 current canonical; 1 historical `HEAD`" exactly.

### 4.3 Duplicate operations

```bash
cut -f1,2,4 ops.tsv | sort | uniq -d   # (change, capability, requirement) → empty
cut -f2,4   ops.tsv | sort | uniq -d   # (capability, requirement)          → empty
```

**Zero duplicate delta operations**, both within and across the two active changes ✅.

### 4.4 `state-driven-runtime` delta — present and correct

`openspec/changes/reshape-developer-facing-interfaces/specs/state-driven-runtime/spec.md` contains
exactly one operation:

```
MODIFIED  Interpreter executes control flow deterministically
```

It removes "SHALL represent branching through explicit recursive scopes" in favour of "SHALL
represent supported nested `If` through explicit conditional continuations and supported root
`Parallel`/`ForEach` through explicit single-entry/single-exit scopes", adds the disambiguator "This
requirement does not authorize fan-out inside a child body", deletes the unreachable scenario
"Nested composition is interpreted", and replaces it with two reachable scenarios covering nested
`If` and linear child execution. This closes the traceability gap I raised as finding F3 against
Revision 4.

### 4.5 Each proposed change area has delta representation

| Proposed area | Delta location |
|---|---|
| Authoring lifecycle (`Open`/`JoinPending`/`Frozen`) | `workflow-authoring` delta:173 `Authoring handles are phase-bound and definitions are frozen`; five `SFE-AUTH-LIFECYCLE-001..005` codes in `quality-and-verification` delta |
| Failure provenance | `workflow-contracts` delta:155 `Workflow failures carry authored and occurrence provenance` |
| Fingerprint contributors | `workflow-contracts` and `structured-fiber-execution` deltas |
| Root fan-out / live-fiber negation | `durable-runtime`, `quality-and-verification`, `structured-fiber-execution`, `workflow-contracts` deltas + `runtime-resource-governance` delta of the coordinated change |
| Compiler-format compatibility | `durable-runtime` and `workflow-contracts` deltas |
| `state-driven-runtime` reconciliation | `state-driven-runtime` delta |

No proposed normative change was found without delta representation.

---

## 5. Determination 7 and validation commands

### 5.1 Required commands

**Command 1**
```bash
openspec.cmd validate --all --strict --no-interactive
```
```
✓ change/add-runtime-concurrency-limits      ✓ spec/quality-and-verification
✓ change/bootstrap-orcacore-spec-baseline    ✓ spec/repository-foundation
✓ spec/durable-persistence-and-outbox        ✓ change/reshape-developer-facing-interfaces
✓ spec/durable-runtime                       ✓ spec/runtime-resource-governance
✓ spec/event-driven-prototype                ✓ spec/saga-orchestration
✓ spec/event-routing-and-waits               ✓ spec/state-driven-runtime
✓ spec/management-and-querying               ✓ spec/structured-fiber-execution
                                             ✓ spec/workflow-authoring
                                             ✓ spec/workflow-contracts
Totals: 16 passed, 0 failed (16 items)
```
**EXIT=0.** Reproduces the recorded "16 passed / 0 failed".

**Command 2**
```bash
git diff --exit-code -- docs/specs/17-public-authoring-contract.cs
```
**EXIT=0**, no output. Corroborated by `git status --porcelain` on the same path → empty. The exact
public-authoring declaration companion is byte-identical to `HEAD` ✅.

**Command 3**
```bash
git diff --check
```
**EXIT=0.** Output contains only advisory `warning: ... LF will be replaced by CRLF` lines from
git's autocrlf normalization; **zero** whitespace-error lines (`grep -v '^warning:'` → 0 lines) ✅.

### 5.2 Independent verifications

| Check | Recorded | Reproduced | Match |
|---|---|---|---|
| OpenSpec strict validation | 16 / 0 | 16 passed, 0 failed | ✅ |
| Delta operations (reshape) | 130; 50 MOD/REM | 130; 50 | ✅ |
| Unresolved / duplicate operations | 0 / 0 | 0 / 0 | ✅ |
| Baseline resolution | 49 canonical + 1 historical | 49 + 1 | ✅ |
| Draft semantic citations | 15 resolved / 0 unresolved | **15 / 0** | ✅ |
| Draft excluded claims | 15 | **15** | ✅ |
| Proposal-packet local links | 25 checked / 0 missing | **25 / 0** | ✅ |
| Tasks | 61 / 75 / 136; 0 duplicate IDs | **61 / 75 / 136; 0** | ✅ |
| Matrix diff | empty | empty | ✅ |
| Ephemeral guide diff | empty | empty | ✅ |
| Public authoring contract diff | empty | empty | ✅ |
| Published semantic appendix | absent | absent | ✅ |
| Sorted-status entry count | 386 | 386 | ✅ |
| Sorted-status SHA-256 | `e53ef4ec…` | not reproducible | ⚠️ **I-1** |

**Citations.** All 15 markdown citations in `artifacts/semantic-appendix.md` were resolved by exact
`### Requirement:` heading match in the linked file: 15 hit exactly once, **0 failed**. Fourteen point
at active deltas (`../specs/…`) and one at canonical (`../../../specs/durable-runtime/spec.md` for
`Durable mutation is crash-safe`). L6 now cites the reshape `durable-runtime` delta with an explicit
note that canonical promotion remains Section 6 work — this closes the broken citation I raised as
finding F1 against Revision 4.

**Links.** The change package contains 15 local links (all in the appendix draft), all resolving. The
phased plan `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`
contributes 10 more, all resolving. **25 checked / 0 missing** — the recorded figure reproduces once
the phased plan is included.

**Tasks.** `61` complete, `75` pending, `136` total; 136 extracted IDs with `sort | uniq -d` empty.

### 5.3 Product source unchanged

The 16 entries present now but absent from the frozen 370-entry manifest:

```
 M docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md
 M openspec/changes/add-runtime-concurrency-limits/{design,proposal,tasks}.md
 M openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md
 M openspec/changes/reshape-developer-facing-interfaces/proposal.md
 M openspec/changes/reshape-developer-facing-interfaces/specs/{quality-and-verification,
                                    workflow-authoring,workflow-contracts}/spec.md
?? docs/review/…amendment-rev04-task-4-15-independent-approval-verdict-2026-07-28.md
?? docs/review/…amendment-revision-06-independent-rereview-request-2026-07-28.md
?? docs/review/…amendment-revision-06-independent-rereview-verdict-2026-07-28.md
?? docs/review/…amendment-revision-08-independent-review-request-2026-07-28.md
?? openspec/changes/reshape-developer-facing-interfaces/AMENDMENT-2026-07-28-…md
?? openspec/changes/reshape-developer-facing-interfaces/artifacts/
?? openspec/changes/reshape-developer-facing-interfaces/specs/state-driven-runtime/
```

Every entry is under `docs/` or `openspec/`. Filtering for `src/`, `tests/`, `samples/`, or
`benchmarks/` returns **NONE**. Amendment §11's claim that it does not modify product source holds ✅.

Also confirmed relative to the Revision 4 application: `docs/specs/17-selected-mode-capability-matrix.md`,
`docs/ephemeral-engine-developer-guide.md`, `openspec/specs/state-driven-runtime/spec.md`,
`openspec/specs/runtime-resource-governance/spec.md`, and `docs/specs/18-semantic-appendix.md` have
all been **reverted or removed** from the dirty set — the isolation rollback is real, not asserted.

---

## 6. Determination 5 — the eight section 10 questions, independently answered

**Q1 — Scoped observational simulation (§1.2.3) as justification of record, and §1.4's residue as its
honest cost statement? → ACCEPTED.**
The three preconditions (budget / dependency / observation) are stated as conjunctive and the
encodings are labelled "scoped observational simulations, not equivalences". §1.4 states the cost
without softening it: the nine prohibited nestings, the four inexpressible shapes, and the precise
statement that root-only-but-repeatable fixes the number of parallel stages at authoring time. §1.4.5
now carries the **complete** future-capability registry preamble and the nested-fan-out re-entry bar,
including the "every applicable root-only encoding" wording, the budget-acceptance evidence class,
and the disclaimer "It is not claimed that the root-only encodings preserve every bounded workload."
This closes finding F2 from my Revision 4 verdict; task `9.10` correctly schedules its publication
post-approval.

**Q2 — Reference model (§3.1) as the normative statement of v1 concurrency? → ACCEPTED.**
Fixed `Parallel`: all `B` branch fibers exist at scope start, queue for path tokens by authored
ordinal, no admission resource. Root `ForEach`: `A = min(C_path, C_node)`, parking releases a token
but retains a slot, so completion is conditional. Sequential root scopes do not overlap, so no
recursive footprint analysis is needed. All three are carried by the `structured-fiber-execution`,
`workflow-contracts`, and `runtime-resource-governance` deltas with matching scenarios, and §3.1's
honesty about revision 2's false claim ("true of the specifications and false of the implementation")
is retained.

**Q3 — Removal of `MaxActiveFibers` in all four roles (§3.4) with no `MaxParallelBranchesPerScope`?
→ APPROVED.**
§3.2's determinism argument is correct: a non-authored, host-variable ceiling that terminally fails a
workflow makes outcome depend on deployment, which is a determinism violation, not a safety net.
§3.4's three clauses — remove all four roles and retire `SFE-LIMIT-003`/`-008`; add no replacement
bound; never let an implementation-defined ceiling terminally fail a workflow or reuse an authoring
diagnostic code — are internally consistent and evidence-grounded. Verified:

```bash
grep -rn "MaxActiveFibers\|MaxParallelBranchesPerScope\|SFE-LIMIT-003\|SFE-LIMIT-008" \
     openspec/specs/ openspec/changes/*/specs/ docs/specs/
# → zero matches
```

No spec, delta, or published document asserts any of the four as v1 semantics, and the negation
("no separate branch or live-fiber admission resource") appears in eight delta locations. §10 item 3
is now posed as a genuine question rather than pre-asserted as approved — this closes observation O1
from my Revision 4 verdict.

**Q4 — Authoring-session requirement (§2.3) with clarifications (§2.4) and restricted atomicity
(§2.5)? → CORRECT.**
The `workflow-authoring` delta requirement carries the three-state session, epoch- and
lexical-scope-bound handles, a *distinct* successor-epoch façade on join (§2.4 clarification 3),
callback-local expiry, atomic freeze at root terminal, snapshot-bound completion builders, repeated
`Build`/`TryBuild` structural equivalence with identical ordered diagnostics and fingerprints (§2.4
clarification 4), the one-atomic-winner race (§2.4 clarification 5), and session-owned workflow-wide
configuration. The state set has exactly three members, honouring §2.4 clarification 2. §2.5's
restriction holds: the unchanged-graph guarantee attaches to exactly the five lifecycle rejections,
which map one-to-one onto `SFE-AUTH-LIFECYCLE-001..005`; pre-existing decorator and deadline
diagnostics retain their own behavior. §2.6's storage-location note is carried by task `6.13`,
correctly sequenced after `4.16`.

**Q5 — Exact `FailureOccurrence` contract (§5)? → APPROVED.**
The `workflow-contracts` delta states every §5 rule without drift: externally non-derivable abstract
record with exactly `Root`, `Branch(AuthoredBranchId)`, `Item(int index)`; `private protected` base
constructor and internal variant constructors; non-null branch identity and nonnegative index;
attachment at failure creation rather than at join; single `WhenAll` failure propagating unchanged;
`SFE-JOIN-FAILED` carrying the owning scope fiber's occurrence while each ordered cause retains its
own; **occurrence variants use record value equality while `WorkflowFailure` retains reference
equality**; detachment copying location, occurrence, and causes; and the closed versioned
`root`/`branch`/`item` discriminator allowlist under `orcacore-json-v1`. Three scenarios back it,
including the defensive-default requirement for consumer switches. The critical negative — that no
structural equality is invented for `WorkflowFailure` — is stated identically across the delta, the
amendment, and task `5.11`. `17-public-authoring-contract.cs` is verifiably unaffected.

**Q6 — Nine laws (§6.2), 15 citations, 15 exclusions, gated L4? → ACCEPTED.**
Every law is weaker than or equal to its cited requirement; none over-claims. L1 does not quantify
over schedules; L2 is confined to committed state; L3 is codec-equality only; L5 states collision
resistance as an assumption, not a theorem; L6 admits `ReservedUnits > ConfiguredCapacity` under
downward resize rather than asserting a false invariant; L7 claims token-only deadlock freedom, not
global progress; L8/L9 correctly split the withdrawn live-fiber law across the two places a resource
actually exists, and L9's proviso is stated as a property of authored work. **15/15 citations resolve
to exact requirement headings.** **15 excluded claims** are present — including the two omitted at
Revision 4 ("`TryBuild` is unreachable after an eager diagnostic. It remains reachable." and "Fan-out
rank one is a computational complexity class."), closing my finding F4. **L4 is Reserved**, gated on
implementation task `4.16` consistently in the draft, §9's `9.12`, and the task file — closing my
finding F5.

**Q7 — `state-driven-runtime` rewrite direction (§7), including qualifying "recursive scopes"?
→ AGREED.** See §4.4. §7's stronger instruction — do not preserve unreachable nested fan-out as a
normative substrate capability, because a specification must not describe behavior no accepted
definition can produce — is followed exactly. The coordinated-change half of §7 is also honoured: the
`runtime-resource-governance` delta asserts the two-quantity model jointly and explicitly refuses to
collapse exact-step throttles, transient pools, durable leases, or DAG-node admission.

**Q8 — Compiler-format compatibility rule (§4)? → AGREED.**
The `workflow-contracts` delta closes fingerprint coverage with "only", excludes compiler format,
workflow mode, definition identity/version, and compiler options, retains codec format, and adds the
retention rule: a durable host retains every compiler format referenced by a nonterminal instance
until terminalization or explicit migration, with a bounded pre-v1 hard cutover permitted when no
supported persisted instance exists. `structured-fiber-execution`'s delta states the same exclusion
from the plan side. `MaxInternalInstructionsPerQuantum` keeps its normative positive default of 1024
while its ownership moves to compiler format / the runtime fairness contract, settled in one place as
§3.5 demanded.

---

## 7. Determination 6 — dependency ordering and task state

Verified directly against `openspec/changes/reshape-developer-facing-interfaces/tasks.md`:

| Task | State | Text (verified) |
|---|---|---|
| `4.15` | `[ ]` open | "independently approve revision 8 … **against the unchanged canonical baseline**" |
| `10.14` | `[ ]` open | "**POST-APPROVAL REQUIRED:** after task 4.15, synchronize every approved delta into canonical OpenSpec specs; complete tasks 9.10 and 9.12 …" |
| `9.10` | `[ ]` open | "**After task 4.15 and as part of task 10.14**, document tagged-item flattening and sequential staging …" |
| `9.12` | `[ ]` open | "**After task 4.15 and as part of task 10.14**, publish the change-local … semantic-appendix draft …" |
| `4.16` | `[ ]` blocked | "**BLOCKED by 4.15 and 10.14**" |
| `5.10` | `[ ]` blocked | "after task 4.15 approves the proposal, complete task 10.14's canonical synchronization … before the remediation slice" |
| `6.0` | `[ ]` blocked | unchanged; Section 6 source work not authorized |
| `10.9`, `10.13` | `[x]` complete | scoped to proposal traceability and proposal-packet validation; neither authorizes synchronization or implementation |

The ordering is correct in every respect: `4.15` approves Revision 8 **before** canonical
synchronization; `9.10` and `9.12` execute **as part of** post-approval `10.14`; `10.14` performs
synchronization; `4.16` and the Section 4/5 remediation slice are gated on **both**; `6.0` remains
blocked. Amendment §11 restates the same boundaries and adds that the amendment "does not authorize
canonical synchronization before fresh independent approval."

**ExpectedRed framing.** The governing statement in `tasks.md:84-91` is correctly prospective:

> **Prospective revision-8 source-conformance disposition (2026-07-28): `ExpectedRed`.** The accepted
> canonical baseline remains unchanged until task 4.15 approves revision 8 and task 10.14
> synchronizes the approved target. **If** that target is approved and synchronized, task 5.13 must
> … These are **prospective proposal-to-source gaps, not current canonical-conformance failures** or
> passing implementation evidence.

This satisfies the determination. The amendment's own §9 bullet retains the older framing — see
advisory **I-2**.

---

## 8. Findings

**No gate-blocking findings.** Two informational items, neither affecting the verdict, neither
requiring action before `4.15` is marked or `10.14` begins.

### I-1 (Informational — provenance bookkeeping; no gate effect)

The recorded LF-normalized sorted-status SHA-256
`e53ef4ec598797237f92154fd7f8437487c1d9a8dcd9e1817ab713a1e34e297e` did not reproduce under eleven
serialization conventions:

| Variant | SHA-256 |
|---|---|
| `LC_ALL=C sort`, trailing newline (and `--short` equivalent, and `sort -u`) | `d41b1c3f4882471d03674bf095e6eecb7d6df4ac75ce8ddd05c9c826846480c8` |
| default-locale sort, no trailing newline | `6e14a3f16cfbde4c76d60694a0e614d27ac00856ab06345161beddc31552dabc` |
| `LC_ALL=C sort`, no trailing newline | `7dd0547a7a2c8feb6719e36040f233709a5924c43c057633aca9c9c9161ae1dd` |
| CRLF-joined, trailing CRLF | `a0da5f9dabbfded07ebe444958628e99de21e827c2a729c81d42ebc40a74a84e` |
| CRLF-joined, no trailing CRLF | `29140ef8e03a20ee644ba8acc575bbea3b776b4bd4b1c078b31099c650f494e4` |
| unsorted porcelain | `4199a2ed9a1c1782b8c59351b5c5b3db54f66349e107d7ad07fa8abe5afa0ca8` |
| paths only, sorted | `661f4c6710042c243f0f2701187a60cb2e02c332b7daee8c65d1e36860ebcef6` |
| excluding the self-inclusive request entry (385) | `8a548aed5eb8b0317cd8341291a7e1b795bf1844d5b7f584ba55072c368f22aa` |
| excluding the Revision 4 verdict | `4ec912b167522e55c0dfd152b814e238923f7f18f702885563ad7cb18dec8b2b` |

The **entry count (386) reproduces exactly**, every one of the 370 frozen manifest entries is
present, and the full 16-entry delta was enumerated and audited (§5.3), so the tree's composition is
independently established without relying on the hash. This is a hashing-convention mismatch in the
request's bookkeeping, not evidence of tree divergence. **Recommendation:** record the exact
serialization pipeline alongside future hashes so they are reproducible by a third party.

### I-2 (Advisory — internal wording inconsistency; no gate effect)

Amendment §9's Section 5 bullet reads "**Current disposition: `ExpectedRed`.** The product source
still exercises `MaxActiveFibers` … and still includes **unauthorized** fingerprint contributors,"
whereas `tasks.md` frames the same gap as "**prospective** proposal-to-source gaps, **not current
canonical-conformance failures**."

Assessment: the amendment's wording is defensible on the merits, because the *currently accepted*
canonical requirement `Executable plan identity is explicit` already closes fingerprint coverage with
"…and codec format **only**", so contributors outside that list are already non-conforming today,
independent of Revision 8. Revision 8's delta only makes the exclusions explicit. Under that reading
neither statement claims that Revision 8's un-synchronized text is already binding.

The two artifacts nonetheless describe the same gap differently. The determination is satisfied by
`tasks.md`, which is the artifact that governs task execution; §9 is narrative. **Recommendation:**
align §9's bullet with `tasks.md`'s prospective phrasing, or state explicitly that the fingerprint
gap is a pre-existing canonical-conformance gap distinct from the Revision 8 proposal. No gate effect
either way.

### Revision 4 findings — remediation status

| Rev 4 finding | Status in Revision 8 |
|---|---|
| F1 — L6 cited a requirement absent from the linked file | **Closed.** L6 cites the reshape `durable-runtime` delta; 15/15 citations resolve. |
| F2 — §1.4.5 registry preamble and re-entry bar not applied | **Closed.** Present in full in §1.4.5; publication scheduled under `9.10`. |
| F3 — no `state-driven-runtime` delta | **Closed.** Delta added with one MODIFIED operation. |
| F4 — two excluded claims missing | **Closed.** 15/15 present. |
| F5 — L4 gate stated two ways | **Closed.** Aligned on task `4.16` everywhere. |
| O1 — §10 item 3 pre-answered | **Closed.** Now posed as a question. |
| O3 — normative text applied before approval | **Closed.** This was the defect Revision 8 exists to correct; canonical and published artifacts are back at the accepted baseline. |

---

## 9. Verdict

**`APPROVE`.**

The proposal package is complete: every proposed normative change is represented in an active delta,
all 55 MODIFIED/REMOVED operations resolve against the accepted canonical baseline or the applicable
historical `HEAD` baseline, there are zero duplicate delta operations, strict validation passes 16/16,
and all eight section 10 decisions are independently sound. Canonical specifications, the capability
matrix, the ephemeral guide, the exact public-authoring declaration companion, and product source are
verifiably unmodified by Revision 8, and the semantic appendix exists only as a change-local
publication draft. The dependency ordering is correct and the ExpectedRed disposition is prospective
in the governing artifact.

No gate-blocking finding remains.

**Effect:** the implementation owner may mark task `4.15` complete and begin task `10.14`.
Nothing else is authorized.

---

## 10. Post-write state and non-modification attestation

**Working tree before writing this verdict:**
`386` self-inclusive sorted porcelain entries; `LC_ALL=C`-sorted LF-normalized SHA-256
`d41b1c3f4882471d03674bf095e6eecb7d6df4ac75ce8ddd05c9c826846480c8`.

**Working tree after writing this verdict:** `387` entries — the sole difference being one new
untracked entry:

```
?? docs/review/developer-facing-interface-amendment-revision-08-task-4-15-independent-review-verdict-2026-07-28.md
```

The target path was confirmed absent before writing; nothing was overwritten.

**No existing file was edited.** Specifically unmodified by this review: every canonical spec under
`openspec/specs/`; every artifact under `openspec/changes/`; `docs/specs/17-selected-mode-capability-matrix.md`;
`docs/specs/17-public-authoring-contract.cs`; `docs/ephemeral-engine-developer-guide.md`;
`docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`; `tasks.md`; the
Section 5 request and its 370-entry manifest; the Revision 4, Revision 6, and Revision 8 requests and
verdicts; and all product source and tests.

**Task state left as required:** `4.15` unmarked; `10.14` open; `4.16`, `5.10`, and `6.0` blocked.
