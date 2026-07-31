# Task 4.15 — independent approval verdict for revision 4 of the root-only fan-out and authoring-lifecycle amendment

**Date:** 2026-07-28
**Verdict:** `APPROVE`
**Scope:** planning and specification approval only. No product implementation was reviewed, executed, or edited.
**Gate satisfied:** task `4.15` of `reshape-developer-facing-interfaces`.
**Gate NOT satisfied by this verdict:** task `6.0` remains blocked. Task `4.15` is deliberately left
unmarked in `tasks.md`; the implementation owner marks it after consuming this verdict.

This file is immutable. It supersedes no earlier review and rewrites no history.

---

## 1. Review provenance

### 1.1 Target

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| Review target | current working tree at `HEAD` plus uncommitted changes |
| Object under review | `openspec/changes/reshape-developer-facing-interfaces/AMENDMENT-2026-07-28-root-only-fanout-and-authoring-lifecycle.md`, **revision 4**'s eight mapped corrections, as applied to the live artifacts |
| Amendment file status line | reads "revision 5 review-state correction"; revision 5 changes no revision-4 decision and reopens only gate `4.15`. Revision 5's own bookkeeping is assessed only where it bears on revision 4's mappings (findings F4, F5). |

### 1.2 Prior review artifacts — immutability attestation

Both files named in the review request are present and unmodified.

```bash
certutil -hashfile "docs\review\developer-facing-interface-section-05-exit-review-dirty-manifest-2026-07-27.txt" SHA256
```

Result: `f001016f92cf056aa1cf6e99203353112504500c1c5e582625ae3b607a41bbad`
— matches the required `F001016F92CF056AA1CF6E99203353112504500C1C5E582625AE3B607A41BBAD` exactly.

```bash
grep -c . docs/review/developer-facing-interface-section-05-exit-review-dirty-manifest-2026-07-27.txt
```

Result: `370` entries — the frozen Section-5 packet is intact.

`docs/review/developer-facing-interface-section-05-exit-review-request-2026-07-27.md` is present
(12,636 bytes) and was read, not written.

**Set comparison of the frozen manifest against the current tree:**

```bash
git status --short | sort > cur.txt
sort docs/review/...-dirty-manifest-2026-07-27.txt > man.txt
comm -13 cur.txt man.txt   # frozen entries missing from current
comm -23 cur.txt man.txt   # current entries absent from frozen
```

- `comm -13` returned **zero rows**: every one of the 370 frozen entries still exists. Nothing in the
  frozen packet was deleted or reverted.
- `comm -23` returned **15 rows** — the amendment's mapped artifacts and the two new documents:

```
 M docs/ephemeral-engine-developer-guide.md
 M docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md
 M docs/specs/17-selected-mode-capability-matrix.md
 M openspec/changes/add-runtime-concurrency-limits/design.md
 M openspec/changes/add-runtime-concurrency-limits/proposal.md
 M openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md
 M openspec/changes/add-runtime-concurrency-limits/tasks.md
 M openspec/changes/reshape-developer-facing-interfaces/proposal.md
 M openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md
 M openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md
 M openspec/changes/reshape-developer-facing-interfaces/specs/workflow-contracts/spec.md
 M openspec/specs/runtime-resource-governance/spec.md
 M openspec/specs/state-driven-runtime/spec.md
?? docs/specs/18-semantic-appendix.md
?? openspec/changes/reshape-developer-facing-interfaces/AMENDMENT-2026-07-28-...md
```

All 15 are under `docs/` or `openspec/`. **No product-source path appears**, corroborating amendment
§11's claim that it does not modify product source. Content-level verification for paths already
marked ` M`/`??` in the frozen manifest was performed with `git diff HEAD -- <path>`, because a
status line cannot discriminate pre- from post-amendment content (see observation O5).

### 1.3 Authority order applied

The amendment specifies authority through its own header and per-correction artifact assignments:

- **Amends** (header order): `reshape-developer-facing-interfaces` (design, specs, tasks) →
  `docs/specs/17-selected-mode-capability-matrix.md` → `openspec/specs/state-driven-runtime` →
  `openspec/specs/structured-fiber-execution` → `add-runtime-concurrency-limits` →
  `docs/ephemeral-engine-developer-guide.md`.
- **Does not amend:** `docs/specs/17-public-authoring-contract.cs` (§5, §11).
- Per-correction artifact assignment: §1.3, §1.4.5, §2.3, §3.4, §4, §5, §7, §9.

Each correction was resolved against the artifact the amendment names for it, in that order. Where a
statement appears in more than one artifact, the canonical OpenSpec requirement under
`openspec/specs/**` was treated as controlling for conformance, consistent with amendment §4's own
method (it grounds the fingerprint repair in the closed coverage clause) and with design Decision 17.

**Exclusion verified:**

```bash
git status --porcelain -- docs/specs/17-public-authoring-contract.cs
grep -n "WorkflowFailure\|FailureOccurrence\|AuthoredLocation" docs/specs/17-public-authoring-contract.cs
```

Both returned empty. The file is untouched and contains none of the §5 symbols, so §5's addition
genuinely cannot desynchronize it. §11's first and second bullets hold.

---

## 2. Verification of the eight revision-4 mapped corrections

### C1 — Omit `MaxParallelBranchesPerScope` — **CONFIRMED**

No artifact introduces the type or any replacement branch-width bound. The omission is stated
affirmatively rather than merely by absence:

- `docs/specs/17-selected-mode-capability-matrix.md:1909` — "V1 has no `MaxActiveFibers` workflow
  semantic and no replacement `MaxParallelBranchesPerScope`; allocation exhaustion is an
  infrastructure fault, never a different authored workflow outcome."
- `openspec/changes/reshape-developer-facing-interfaces/design.md:326` — "No replacement
  branch-width limit is added without evidence for a concrete v1 safety requirement."
- `tasks.md` 5.13 — "add no replacement branch-width limit and classify allocation exhaustion as
  infrastructure failure."

A repository-wide grep for `MaxParallelBranchesPerScope` outside the amendment returns nothing.

### C2 — Two-quantity model qualified to one structured root fan-out scope — **CONFIRMED**

The scope qualifier is present at every site, and each site explicitly refuses to collapse the other
system limits:

- Matrix §17.x — "Within one structured root fan-out scope, only item 3 and the root-`ForEach`
  admitted-item cap are owned by the structured-fiber scheduler. That statement is deliberately
  scope-local: exact-step throttles, transient pools, DAG-node admission, and durable leases remain
  separate system resources…"
- `add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md` — same claim as
  normative text, plus "This scope statement does not merge or replace exact-step throttles, named
  transient pools, durable resource leases, or the independent DAG-node ceiling."
- `add-runtime-concurrency-limits/design.md` §4 — same, as design rationale.
- `tasks.md` 10.12 — "asserting exactly two structured-fiber scheduler quantities within a root
  fan-out scope, no live-fiber admission resource, and no confusion with step throttles, transient
  pools, durable leases, or DAG-node admission."

Both change packages assert it jointly, as §7 requires.

### C3 — Real authored/value-envelope budget boundary replaces the fixed-`MaxItems` counterexample — **CONFIRMED (with F2 on the re-entry bar)**

The premise is factually correct in the live specs: `MaxItems` is author-declared per node, not a
platform ceiling (`openspec/specs/structured-fiber-execution/spec.md` — "`ForEachOptions.MaxItems`
SHALL be positive"; matrix — `ForEachOptions.Create` validates it). The replacement boundary is
mapped in all four assigned places:

- Matrix `ForEach`/root-`Parallel` prose — "the authored `MaxItems`, and fixed encoded-value budgets"
  and "within fixed codec/payload/snapshot/envelope limits".
- `design.md` risk entry — "authored item limits, fixed encoded-value budgets".
- `docs/ephemeral-engine-developer-guide.md` — same phrasing in the v1-corrections block.
- `tasks.md` 5.15 — "prove an authored `MaxItems` violation and an encoded-value limit violation are
  rejected before partial admission without treating `MaxItems` as a fixed platform ceiling."

The one place this correction did **not** reach is the nested-fan-out re-entry bar — see finding F2.

### C4 — Builder ownership defined as phase- and scope-bound, not persistent or linear — **CONFIRMED**

The strongest-mapped correction of the eight. §2.3's proposed requirement is now canonical:

`openspec/specs/workflow-authoring/spec.md` — "Requirement: Authoring handles are phase-bound and
definitions are frozen", carrying all of: the three-state session (`Open`/`JoinPending`/`Frozen`);
epoch- and lexical-scope-bound handles; joins returning a *distinct* successor-epoch façade (§2.4
clarification 3); callback-local expiry; atomic freeze at root terminal; completion builders bound to
the snapshot; repeated-build structural equivalence with identical ordered diagnostics and
fingerprints (§2.4 clarification 4); session-owned workflow-wide configuration. Four scenarios cover
stale root handle, escaped callback handle, repeated build, and the concurrent race with one atomic
winner (§2.4 clarification 5).

§2.4 clarification 2 is honoured — the state set has exactly three members and no `TerminalSelected`.

**§2.5's restriction is correctly preserved.** The unchanged-graph guarantee attaches to exactly the
five lifecycle rejections and is not generalized to other eager authoring errors. The matrix
allocates exactly five codes, one per mode: `SFE-AUTH-LIFECYCLE-001` `SupersededBuilderHandle`,
`-002` `JoinAlreadySelected`, `-003` `FrozenAuthoringSession`, `-004` `ExpiredLexicalBuilderHandle`,
`-005` `ConcurrentAuthoringConflict`. Pre-existing `SFE-AUTH-DECORATOR-001` and
`SFE-AUTH-DEADLINE-001` retain their prior per-diagnostic behavior, exactly as §2.5 requires.

The "not persistent, not genuinely linear" half is stated where it belongs: design Decision 18 — "This
obtains safe lifecycle behavior without redesigning every `Action<TBuilder>` body into a persistent
functional API" — and §8's declined row for persistent/functional builders is unchanged.

§2.6's storage-location note is carried by task `6.13` ("Store and enforce workflow and wait
deadlines in the authoring-session location created by task 4.16"), correctly sequenced after `4.16`.

### C5 — `FailureOccurrence` closed to external derivation and construction, without inventing `WorkflowFailure` structural equality — **CONFIRMED**

Mapped with unusual precision. `openspec/specs/workflow-contracts/spec.md` — "Requirement: Workflow
failures carry authored and occurrence provenance":

> …`FailureOccurrence` SHALL be an externally non-derivable runtime-created abstract record with
> exactly `Root`, `Branch(AuthoredBranchId)`, and `Item(int index)` variants; its base constructor
> SHALL be `private protected`, variant constructors SHALL be internal, branch identity SHALL be
> non-null, and item index SHALL be nonnegative.

Every §5 sub-rule is present and correct:

| §5 rule | Mapped |
|---|---|
| Attachment at creation, not at join | "Provenance SHALL attach when the failure is created rather than at join." |
| Aggregate origin | "A synthesized `SFE-JOIN-FAILED` SHALL carry the owning scope fiber's occurrence while each ordered cause retains its own provenance." |
| Single `WhenAll` failure propagates unchanged | present, plus a dedicated scenario |
| Construction validation | non-null branch identity, nonnegative index |
| Equality — the correction itself | "Occurrence variants SHALL retain record value equality; `WorkflowFailure` SHALL retain reference equality." |
| Copying on detachment | "Detachment SHALL copy location, occurrence, and causes." |
| Codec round-trip | "closed versioned occurrence discriminator allowlist `root`, `branch`, and `item`" |
| No compiler-enforced exhaustiveness | scenario "Consumer matches an occurrence" mandates a defensive default |

The critical negative — that `WorkflowFailure` does **not** acquire structural equality — is stated
identically in the canonical spec, the matrix, design Decision 19, and task `5.11`. Four artifacts
agree; none drifts toward value equality.

`AuthoredLocation` is non-null and derived, and is correctly excluded from every fingerprint coverage
statement.

### C6 — Capacity safety corrected for downward resize debt; path-token progress narrowed to token-only deadlock freedom — **CONFIRMED (with F1 on L6's citation target)**

**Resize-debt half.** L6's substance was checked line-by-line against normative text and matches
exactly. `openspec/changes/reshape-developer-facing-interfaces/specs/durable-runtime/spec.md:199`
("Durable leases are lexical occurrence-owned obligations") states: "`Queued` and
`CancelledBeforeGrant` reserve zero; `PendingCommit`, `Held`, `ReviewMarked`, `AmbiguousHeld`, and
`Quarantined` reserve every exact ticket unit" — the same five reserving statuses L6 sums over — and
"Downward resize MAY create `max(0, reserved - configured)` debt, SHALL revoke nothing, and SHALL
block new grants until debt is zero and the whole next request fits." The cited pool-shrink scenario
exists: "Scenario: Pool shrinks below reservations → no reservation is revoked, debt is visible, and
no new grant occurs until release or upward resize clears debt and the next request fits." L6's
"Availability recovery still requires external proof" is backed by "Only trusted causal stop
confirmation or an end-to-end protected-resource fence MAY move `Quarantined` to `Released`."

The correction is sound: the law admits `ReservedUnits > ConfiguredCapacity` rather than asserting an
invariant the spec does not make.

**Token-only-progress half.** The narrowing is applied everywhere the old over-claim lived:

- `openspec/specs/workflow-contracts/spec.md` and the matrix now read "path-token capacity alone
  cannot deadlock", replacing the unqualified "cannot deadlock".
- `openspec/specs/structured-fiber-execution/spec.md` adds the scenario "Parent fans out under a
  ceiling of one" ending "…so path-token capacity alone cannot create a parent-held-token deadlock."
- `add-runtime-concurrency-limits` proposal, design, spec, and tasks `3.1`/`3.5`/`4.1` all carry the
  narrowed form.
- The complementary honesty — that admitted-item dependence on pending work has no global-progress
  promise — is added as normative text *and* as a scenario ("Admitted `ForEach` items depend on
  pending work") in the governance spec, and mirrored in the matrix, sfe spec, design, guide, and
  task `4.1`.

Appendix L7 correspondingly states "This is not a claim of item-admission progress or global workflow
progress." The over-claim is retired consistently.

### C7 — Fingerprint repair expanded to every unauthorized contributor; `MaxInternalInstructionsPerQuantum` given compiler-format ownership — **CONFIRMED**

**Repair scope.** `openspec/specs/workflow-contracts/spec.md`:

> The fingerprint SHALL cover inspectable authored node/member kinds, ordering, strong values,
> referenced step/workflow types, static resource requests, and codec format **only**. It SHALL NOT
> include compiler format, workflow mode, definition identity/version, compiler acceptance or
> fairness limits, delegate IL, DI/step configuration, external adapter behavior, opaque mapping
> logic, or author-supplied contributors.

The positive clause is closed by "only", so the coverage list is genuinely closed regardless of the
enumeration in the negative clause (see observation O2). The same closure appears in
`openspec/specs/structured-fiber-execution/spec.md` ("Compiler format, workflow mode, definition
identity/version, and compiler options SHALL remain separate bindings and SHALL NOT contribute"), its
change delta, the matrix, design, and task `5.14`. Codec format is retained in all five, as §4
requires.

**`MaxInternalInstructionsPerQuantum` ownership.** Its normative status is retained
(`structured-fiber-execution` — "at most the positive configured `MaxInternalInstructionsPerQuantum`,
whose default is 1024"), it is named as fingerprint-excluded in the matrix, and ownership is assigned
by the new version-bump checklist: "Bump compiler format when compiler-owned lowering, scope-depth,
instruction-fairness, or acceptance semantics change." Task `5.14` binds it "through
compiler-format/runtime compatibility". Ownership is settled in one place, as §3.5 demanded.

**Retired codes.** `SFE-LIMIT-003` and `SFE-LIMIT-008` appear in no diagnostic catalog. Grep across
`docs/specs/`, `openspec/specs/`, and both change packages returns only `SFE-LIMIT-001`
(`InvalidForEachLimit`), which is legitimately retained, plus task `5.13` naming the two codes for
source retirement. §3.4's "SHALL NOT reuse an authoring diagnostic code" is satisfied by construction.

**Compiler-format compatibility rule** (checklist item 8) is applied in three artifacts with
consistent wording, including the pre-v1 hard-cutover allowance: canonical `workflow-contracts`
("A durable host SHALL retain every compiler format referenced by a nonterminal instance until the
instance terminalizes or is explicitly migrated; before a released compatibility contract exists, a
format bump MAY use a hard cutover when no supported persisted instance exists"), the matrix
§17.3, and `design.md`. The scenario "Same version has different behavior" backs it at registration
and resume.

### C8 — Section 7 work appended at `7.14`/`7.15` after the verified live maximum `7.13` — **CONFIRMED**

The amendment's §9 numbering claim was independently reproduced against `HEAD`:

```bash
git show HEAD:openspec/changes/reshape-developer-facing-interfaces/tasks.md \
  | grep -oE '^- \[[ x]\] +[0-9]+\.[0-9]+[a-z]*' | ...
```

Pre-amendment maxima at `HEAD`: section 4 → **4.14**, section 5 → **5.9**, section 6 → **6.11**,
section 7 → **7.13** — all four exactly as §9 claims. (Sections 9 and 10, which §9 does not claim,
were at 9.9 and 10.11.) `HEAD` total: **112** tasks, matching the Section-5 exit request.

Post-amendment: **135** ids, **zero duplicates** (`sort | uniq -d` empty), maxima 4.21, 5.15, 6.13,
7.15, 9.13, 10.13. All 23 additions are strictly append-only; no existing id was redefined or reused.

Counts reconcile across artifacts: `tasks.md` yields **63 done / 72 pending / 135 total**, which is
precisely what `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`
now claims. Coordinated change: **7 done / 9 pending / 16 total**, also as claimed. Task `4.15` is
`[ ]` unchecked, and task `6.0` is `[ ]` unchecked.

---

## 3. Section 10 review checklist — determinations

| # | Checklist item | Determination |
|---|---|---|
| 1 | Scoped observational simulation with its three preconditions (§1.2.3) as the justification of record, and the residue (§1.4) as its honest cost statement | **Accepted, with F2.** The preconditions (budget / dependency / observation) and the "not equivalences" qualifier are mapped into the matrix, design Decision 20, the design risk entry, and the guide. §1.4's residue is mapped only through design Decision 20's "does not solve unbounded data-dependent repetition"; §1.4.5's designated matrix preamble was not applied. |
| 2 | Reference model (§3.1) as the normative statement of v1 concurrency | **Accepted.** Fixed `Parallel` = all `B` fibers exist, no admission resource, fair token queueing by authored ordinal; root `ForEach` = `A = min(C_path, C_node)` with parking releasing a token but retaining a slot; sequential root scopes do not overlap. All three are now normative in `structured-fiber-execution`, `workflow-contracts`, and the governance spec, with matching scenarios. |
| 3 | Remove `MaxActiveFibers` in all four roles (§3.4) and omit `MaxParallelBranchesPerScope` | **Concurred.** Reviewed independently despite the item being pre-answered (observation O1). All four roles are negated normatively; both diagnostic codes are absent from every catalog; no replacement bound exists. §3.2's determinism argument for withdrawing the defensive ceiling is sound — a non-authored quantity that can differ across hosts must not decide workflow outcome. Source divergence is honestly carried as `ExpectedRed` under `5.13`. |
| 4 | Authoring-session requirement (§2.3) with clarifications (§2.4) and restricted atomicity scope (§2.5) | **Correct.** See C4. All five clarifications are honoured; the atomicity restriction is preserved and not silently widened; five diagnostic codes match the five governed failure modes. |
| 5 | Exact `FailureOccurrence` contract (§5) | **Approved.** See C5. Declaration, construction closure, validation, attachment point, aggregate origin, equality asymmetry, copying, and codec allowlist are all mapped without drift. `17-public-authoring-contract.cs` is verifiably unaffected. |
| 6 | Nine laws (§6.2), including L8/L9 replacing the withdrawn live-fiber law and L4's gated publication | **Accepted, with F1, F4, F5.** All nine laws are weaker than or equal to their cited requirements — none over-claims. L8/L9 correctly split the withdrawn law into the two places where a resource actually exists, and L9's proviso is stated as a property of authored work rather than a runtime guarantee. L4 is withheld. Twelve of thirteen citations resolve; L6's does not (F1). |
| 7 | `state-driven-runtime` rewrite direction (§7), including qualifying "recursive scopes" in the parent requirement | **Agreed, with F3.** "SHALL represent branching through explicit recursive scopes" is replaced by "SHALL represent supported nested `If` through explicit conditional continuations and supported root `Parallel`/`ForEach` through explicit single-entry/single-exit scopes", plus the disambiguating sentence "This requirement does not authorize fan-out inside a child body." The unreachable scenario "Nested composition is interpreted" is removed and replaced by two reachable scenarios. §7's stronger instruction — do not preserve unreachable nested fan-out as a normative substrate capability — is followed exactly. Traceability gap only (F3). |
| 8 | Compiler-format compatibility rule (§4) | **Agreed.** See C7. Applied in three artifacts with the retention rule, the terminalize-or-migrate condition, and the bounded pre-v1 hard-cutover allowance. |

All eight checklist items are answered affirmatively. No finding contradicts an approved decision,
introduces a false normative statement, or blocks task `4.16`.

---

## 4. Findings

No P0 or P1 findings. Five findings, all documentation-fidelity or traceability, each with a named
owner. None is a condition of this approval; each should be closed under the task identified.

### F1 (P2) — Appendix law L6 cites a requirement absent from the file it links

`docs/specs/18-semantic-appendix.md:107-109` cites
`durable-runtime: Durable leases are lexical occurrence-owned obligations`, linking
`../../openspec/specs/durable-runtime/spec.md`.

Evidence:

```bash
grep -c "^### Requirement: Durable leases are lexical occurrence-owned obligations$" \
  openspec/specs/durable-runtime/spec.md     # -> 0
grep -n "^### Requirement:" openspec/specs/durable-runtime/spec.md   # 11 requirements, none about leases
grep -rn "lexical occurrence-owned" openspec/specs/                  # -> no matches
```

The requirement is real but lives at
`openspec/changes/reshape-developer-facing-interfaces/specs/durable-runtime/spec.md:199` — it has not
yet been amended into canonical, because lease requirements belong to Section 6, which is blocked.
The remaining twelve citations across L1–L9 all resolve against their linked files.

This is a link-target defect, not an invented promise: L6's substance was verified against real
normative text (see C6). It is nonetheless a violation of §6.1's own admission rule as published, and
it is a recurrence of the failure class that revision 2 accepted as a P1.

**Fix:** cite the change-package path, or annotate L6 as pending Section 6's canonical amendment, in
the same style already used for L4 and L5. **Owner:** task `9.12`.

### F2 (P2) — §1.4.5's matrix future-capability-registry preamble was not applied

§1.4.5 designates one artifact change: the future-capability registry preamble in
`docs/specs/17-selected-mode-capability-matrix.md`. The preamble at line 2147 is unchanged
pre-amendment text. Greps for `Re-entry bar`, `residue`, `scored against`, `every applicable`, and
`budget-acceptance` across the matrix return nothing.

Not carried into any artifact as a result:

- the scoring principle — deferred capabilities scored against the authoring shapes they restore;
- the dated statement of the v1 residue;
- the recovery scorecard (conditional `ContinueAsNew` restores shapes 1–3; nested `While` restores
  shape 4; nested `Parallel` restores none);
- **the nested-fan-out re-entry bar**, specifically the "every applicable root-only encoding" wording
  that revision 3 explicitly accepted, the budget-acceptance evidence class, and the disclaimer "It
  is not claimed that the root-only encodings preserve every bounded workload."

§1.3's nested-`Parallel` registry *row* did land and carries a weakened substitute — "A measured
latency/throughput case that the sanctioned encodings cannot meet" — but it drops budget-acceptance
evidence entirely. That is the evidence class correction C3 exists to create, so C3's own consequence
for future re-entry is the part that did not reach an artifact.

Nothing currently stated is wrong; the future re-entry bar is under-specified relative to what was
approved. This is the only §1.x artifact change of the four not applied.

**Fix:** apply §1.4.5's preamble text to the matrix registry. **Owner:** task `9.10` (or a new
Section 9 task).

### F3 (P2) — The `state-driven-runtime` canonical amendment has no delta in the change package

`openspec/specs/state-driven-runtime/spec.md` was amended canonically under task `5.12`, but
`openspec/changes/reshape-developer-facing-interfaces/specs/` contains no `state-driven-runtime`
directory. Every other capability this amendment touches has one — including
`structured-fiber-execution`, whose `## MODIFIED Requirements` delta mirrors the same
already-applied canonical text and was maintained for exactly this purpose.

Consequence: the §7 reconciliation — a removed scenario and a qualified requirement — is invisible to
the change package. `openspec archive` will not reproduce it, and task `10.9`'s instruction to
"compare every MODIFIED/REMOVED requirement to the canonical baseline" has no delta to compare
against for this capability. (`openspec/changes/add-runtime-concurrency-limits/specs/state-driven-runtime`
exists but belongs to the other change and does not record these edits.)

**Fix:** add a `MODIFIED Requirements` delta for `state-driven-runtime` mirroring the applied
canonical text. **Owner:** task `10.9`.

### F4 (P3) — The published appendix omits two of §6.3's fifteen excluded claims

Amendment §6.3 lists fifteen deliberately excluded claims and states "This section is published in
`docs/specs/18-semantic-appendix.md`". The published list has thirteen. Missing:

- "*`TryBuild` is unreachable after an eager diagnostic.* It is reachable."
- "*Fan-out rank one is a computational complexity class.* Step bodies are arbitrary code."

Revision 5 asserts it "restores the target-versus-current disclaimer and the two excluded claims that
were omitted during publication." The disclaimer and L5 note did land in the appendix (lines 8–11,
87–88); the two claims did not. This is revision 5's stated intent not yet applied and is not a defect
in revision 4's substance, but the working tree does not match §6.3's publication claim.

**Fix:** add the two bullets to the appendix. **Owner:** task `9.12`.

### F5 (P3) — L4's publication gate is stated two different ways

§6.2's L4 row and §6.3's closing sentence gate publication on **§2.3 landing**. §9's task `9.12` and
the published appendix gate it on **task `4.16` landing**. §2.3 has now landed as a canonical
requirement, so the two gates disagree: the former would permit publication today, the latter would
not.

The appendix uses the stricter gate, which is the safe choice and consistent with §6.1's rule against
promising behavior the runtime does not yet make. No action is required beyond aligning the amendment's
two statements if it is revised again.

**Fix:** harmonize §6.2/§6.3 wording with `9.12`. **Owner:** informational; no task required.

---

## 5. Observations (non-findings)

- **O1 — Checklist item 3 is pre-answered.** §10 item 3 reads "**Approved:** remove `MaxActiveFibers`
  in all four roles…" rather than posing a question as items 1, 2, 4–8 do. A checklist that asserts
  its own answer cannot elicit an independent one. The item was reviewed on its merits regardless and
  is concurred with; recorded so the asymmetry is not mistaken for an oversight in this verdict.
- **O2 — Narrower negative clause in the canonical fingerprint requirement.** `workflow-contracts`
  enumerates "compiler acceptance or fairness limits" where design, matrix, and task `5.14` say
  "every compiler option". No conformance gap results, because the positive coverage clause is closed
  with "only", which already excludes every compiler option. Worth keeping in view when `5.14`'s
  coverage guard is written, so the guard asserts the closed positive list rather than the illustrative
  negative one.
- **O3 — Normative content was applied before this approval.** Tasks `5.10`, `9.10`–`9.13`, and
  `10.12`/`10.13` are checked, so the canonical specs already carry revision 4's mappings while gate
  `4.15` was open. Revision 5 records this honestly and §11 restates it. The practical consequence is
  that a `REJECT` would have required reverting applied canonical edits rather than merely withholding
  them. This verdict approves the mapped content, so the ordering causes no harm here; it is flagged as
  a process risk for future `x.0`/approval gates.
- **O4 — §1.3's design.md risk-entry text was paraphrased rather than transcribed.** The amendment
  supplies exact replacement text; the applied entry is shorter. Every substantive element survives —
  barriers, budgets, per-group concurrency, sequential dependency, non-equivalence, and the evidence
  requirement — split across the risk entry and design Decision 20. Substance preserved; recorded for
  completeness.
- **O5 — Manifest granularity.** For paths already marked ` M` or `??` in the frozen 370-entry
  manifest, the status line cannot distinguish pre- from post-amendment content; an untracked
  directory entry likewise masks changes to files beneath it (relevant to
  `openspec/changes/reshape-developer-facing-interfaces/specs/structured-fiber-execution/`). All
  content-level conclusions in this verdict therefore rest on `git diff HEAD -- <path>` and direct file
  reads, not on manifest membership.

---

## 6. Validation evidence

| # | Command | Result |
|---|---|---|
| 1 | `openspec validate --all --strict` | **16 passed, 0 failed (16 items)**; exit 0. Includes `change/reshape-developer-facing-interfaces`, `change/add-runtime-concurrency-limits`, and all seven canonical specs touched by the amendment. |
| 2 | `certutil -hashfile "docs\review\...-section-05-exit-review-dirty-manifest-2026-07-27.txt" SHA256` | `f001016f92cf056aa1cf6e99203353112504500c1c5e582625ae3b607a41bbad` — matches the required value. |
| 3 | `grep -c . docs/review/...-dirty-manifest-2026-07-27.txt` | `370` entries. |
| 4 | `comm -13` frozen manifest vs. `git status --short` | zero rows — no frozen entry lost. |
| 5 | `comm -23` current vs. frozen manifest | 15 rows, all under `docs/` or `openspec/`; no product-source path. |
| 6 | `git status --porcelain -- docs/specs/17-public-authoring-contract.cs` | empty — file untouched. |
| 7 | `grep -oE '^- \[[ x]\] +[0-9]+\.[0-9]+[a-z]*' tasks.md \| sort \| uniq -d` | empty — no duplicate task ids among 135. |
| 8 | per-section maxima, post-amendment | 4.21, 5.15, 6.13, 7.15, 9.13, 10.13. |
| 9 | per-section maxima at `HEAD` (pre-amendment) | 4.14, 5.9, 6.11, 7.13 — §9's claim reproduced exactly. |
| 10 | `grep -c '^- \[x\]' / '^- \[ \]'` on `reshape` tasks | 63 done / 72 pending / 135 total — matches the phased plan's claim. |
| 11 | same on `add-runtime-concurrency-limits` | 7 done / 9 pending / 16 total — matches. |
| 12 | 13 × `grep -c "^### Requirement: <name>$"` for every appendix citation | 12 resolve (`1`); `Durable leases are lexical occurrence-owned obligations` in `openspec/specs/durable-runtime/spec.md` returns `0` → finding F1. |
| 13 | `grep -rn "^### Requirement:" openspec/specs/durable-runtime/spec.md` | 11 requirements; none concerns leases — confirms F1 is not a naming variant. |
| 14 | `grep -rn "SFE-LIMIT" docs/specs/ openspec/specs/ openspec/changes/…` | only `SFE-LIMIT-001` in catalogs; `-003`/`-008` appear solely in task `5.13`'s retirement instruction. |
| 15 | `grep -rn "MaxActiveFibers" docs/ openspec/` (excluding `docs/review/`) | every planning-artifact occurrence is a negation, a removal instruction, or an `ExpectedRed` disclosure; no artifact asserts it as v1 semantics. |
| 16 | `grep -rn "MaxParallelBranchesPerScope"` outside the amendment | matrix negation only; no introduction anywhere. |
| 17 | `grep -n "Re-entry bar\|residue\|scored against\|every applicable\|budget-acceptance"` across matrix, appendix, design, proposal, guide | no matches → finding F2. |
| 18 | `find openspec/changes -maxdepth 3 -name state-driven-runtime -type d` | only under `add-runtime-concurrency-limits` → finding F3. |
| 19 | file-existence check for all six appendix link targets | all present. |

No build, test, or product-source command was run; none is in scope for a planning-artifact gate, and
tasks `10.12`/`10.13` are explicitly scoped to planning/specification coherence.

---

## 7. What this verdict authorizes and what it does not

**Authorizes:**

- Task `4.15` may be marked complete by the implementation owner on the basis of this verdict.
- Task `4.16` and the Section-4 lifecycle remediation (`4.16`–`4.21`) are unblocked.
- Revision 4's eight mapped corrections stand as applied. The canonical edits already in the working
  tree are approved as written; no revert is required.

**Does not authorize and does not assert:**

- Task `6.0` and Section-6 source work remain **blocked**.
- No claim of product-source conformance is made. `MaxActiveFibers` remains live in
  `DefinitionCompiler.Limits.cs`, `ScopeReducer`, both engine integrations, and
  `DefinitionCompiler.Fingerprint.cs`, and unauthorized fingerprint contributors remain present.
  These are `ExpectedRed` gaps owned by tasks `5.13` and `5.14`, exactly as the amendment and
  `tasks.md` record. This verdict confirms the disclosure is accurate; it does not close the gaps.
- Tasks `5.11`, `5.13`, `5.14`, and `5.15` remain open and are not affected by this approval.
- No Section-5 exit verdict is issued or implied. The 370-entry Section-5 packet remains an
  immutable input, unreviewed here.
- Appendix law L4 remains withheld pending task `4.16`.

## 8. Files written by this review

Exactly one:

- `docs/review/developer-facing-interface-amendment-rev04-task-4-15-independent-approval-verdict-2026-07-28.md` (this file)

No existing file was edited, including `tasks.md`, any spec, any planning document, product source,
tests, the historical Section-5 request, and the historical Section-5 manifest. Task `4.15` was not
marked. The frozen manifest was not regenerated; this verdict file is a new untracked entry outside it
by design.
