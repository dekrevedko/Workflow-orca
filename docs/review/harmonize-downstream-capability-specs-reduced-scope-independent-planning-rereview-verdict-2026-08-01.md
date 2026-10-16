# Harmonize Downstream Capability Specs — Reduced-Scope Independent Planning Re-Review Verdict

**Date:** 2026-08-01
**Reviewer:** independent planning re-review (audit-only)
**Target:** `openspec/changes/harmonize-downstream-capability-specs` (reduced scope)
**Supersedes as re-review of:** `docs/review/harmonize-downstream-capability-specs-independent-planning-review-verdict-2026-08-01.md` (REJECT). That record remains immutable and is not edited by this verdict.

---

## 1. Verdict

# REJECT

The scope reduction itself is sound. Ownership is now unique, the retained deltas are correct, both
manifest hashes reproduce exactly, and every validation command passes. The change is rejected on
three blocking findings, the first of which is a **verbatim repeat of the prior rejection's P1-5**
that the prior verdict required to be remediated *before* a new freeze.

Per instruction, no "approve with changes" option is issued.

| # | Finding | Class |
|---|---------|-------|
| B1 | Archived historical plan still rewritten in the frozen target (repeat of prior P1-5) | P1 |
| B2 | Four empty delta directories inside harmonize contradict Decision 1 and are invisible to both the CLI and the freeze algorithm | P1 |
| B3 | Proposal Impact contradicts task 7.6 / Decision 7 on whether tests change | P2, blocking for a planning approval |

---

## 2. Provenance and manifest hashes

Reproduced independently **before** and **after** the full validation suite. All six orientation
values match; validation was non-mutating.

| Value | Stated | Reproduced (pre) | Reproduced (post) | Match |
|-------|--------|------------------|-------------------|-------|
| HEAD | `ac46d99543daf85c0fa3234272997ba40f47f96b` | same | same | ✅ |
| Tree | `28f4033c4773ea7761afa905c9836fd25866f1c0` | same | same | ✅ |
| Porcelain entries | 422 | 422 | 422 | ✅ |
| NUL-expanded normalized records | 452 | 452 | 452 | ✅ |
| Raw SHA-256 | `c2d52302f5b046d0542891292eccec8e2b15be88074e51f50114674876f9690c` | same | same | ✅ |
| Authoritative sorted-LF SHA-256 | `4ff6e35bfedf5157748826c9a8ef557586d3e7175938e97b9cb5770084badc1e` | same | same | ✅ |

`Tree` is HEAD's commit tree (`git rev-parse HEAD^{tree}`). The **index** tree is
`6d954206eb2fc82bd6187a51aa60b2f05c7b97f7` and is deliberately different — the worktree is dirty by
design at a planning freeze.

Pipeline used, exactly as specified by design Decision 6:

```text
git status --porcelain=v1 -z
| split on NUL
| drop trailing empty record
| strip one trailing CR per record
| sort by ordinal byte order
| join with LF + one final LF
| SHA-256
```

Raw anchor computed over `git status --porcelain=v1` with LF endings and trailing LF
(30,429 bytes, 422 lines).

**Worktree surface at the frozen target:** `src/` 191, `tests/` 155, `samples/` 0, `openspec/` 21,
`docs/` 53. These are `reshape-developer-facing-interfaces` Section 7 work, not harmonize's.

---

## 3. Validation results

| Command / check | Result |
|---|---|
| `openspec.cmd status --change harmonize-downstream-capability-specs --json` | `isComplete: true`; proposal/design/specs/tasks all `done` |
| `openspec.cmd validate harmonize-downstream-capability-specs --strict` | **valid**, exit 0 |
| `openspec.cmd validate --all --strict` | **18 passed, 0 failed**, exit 0 |
| `openspec.cmd list --json` | harmonize `5/33` in-progress; reshape `107/158`; add-runtime-concurrency-limits `16/16`; bootstrap `0/8` |
| `openspec.cmd show harmonize... --json` | `deltaCount: 7` (6 × `event-driven-prototype`, 1 × `state-driven-runtime`) |
| Independent cross-change duplicate-heading scan | **0 duplicates** across 175 `(capability, heading)` delta entries |
| `git diff --check` | exit 0; 0 non-warning findings (line-ending warnings only) |
| Manifest hashes before/after | identical (§2) |
| `src/`, `tests/`, `samples/` vs frozen target | unchanged; full-porcelain hash reproduces |
| Prior verdict files byte-unchanged | ✅ (§8) |
| HEAD / tree unchanged | ✅ |

Planning accounting **5/33** confirmed by both manual count and the CLI. Checked: `1.1`, `1.2`,
`1.3`, `1.4`, `3.2`.

Strict validation proves structure only. Consistent with this change's own Decision 2, it is not
reported here as semantic or provenance approval.

---

## 4. Retained capability ownership

**Q1 — scope limitation: ACCURATE.** The proposal limits harmonize to `event-driven-prototype`,
`state-driven-runtime`, and cross-tree provenance/documentation/acceptance/archive/synchronization
process. Sections 2–8 of `tasks.md` contain no product runtime semantics.

**Q2 — four overlapping capabilities: CONTENT absent, DIRECTORIES present.** No delta *file* exists
for `event-routing-and-waits`, `durable-persistence-and-outbox`, `repository-foundation`, or
`durable-runtime`, and the CLI reports none. But four empty *directories* remain — see **B2**.

**Q3 — reshape ownership completeness: YES, and a semantic superset.** Verified requirement-by-
requirement against the harmonize deltas as they stood at HEAD (only two were ever tracked;
`durable-runtime` and `repository-foundation` never held tracked files):

- `event-routing-and-waits` — reshape MODIFIES all five headings harmonize also modified, and
  *retains* the two headings harmonize would have REMOVED (`Out-of-order events can be buffered and
  later consumed`, `Event routing supports direct, correlation, and fanout targeting`). Harmonize's
  ADDED `Events are not buffered before wait registration` and `Event routing is instance-targeted or
  correlation-targeted` directly **contradicted** the approved Section 7B contract; deleting them is
  a correction, not a loss. Harmonize's ADDED `Ambiguous wait pairs are rejected at registration` is
  carried by reshape as a scenario under `Waits match by declared event identity and correlation`.
- `durable-persistence-and-outbox` — reshape's retained `Durable payloads cross boundaries through
  explicit serializers` body now mandates the single fixed `orcacore-json-v1` codec and forbids
  serializer/converter hooks, superseding harmonize's ADDED `Durable payloads use the fixed certified
  codec`. Its retained `Retention and purge preserve runtime correctness` body mandates
  provider/operator-tier ownership with `Archive`/`Purge` deferred and absent, superseding
  harmonize's ADDED `Retention and purge remain operator-tier`.

Ownership is structurally complete. The *contents* of reshape's deltas remain gated by reshape's own
exit review, which is currently a REJECT (§7) — correctly handled by harmonize tasks 5.1–5.3.

**Retained delta correctness.** All four REMOVED headings in `event-driven-prototype` and the single
MODIFIED heading in `state-driven-runtime` match the canonical headings **verbatim**; every REMOVED
carries both `**Reason**` and `**Migration**`. `event-driven-prototype` removes all four canonical
requirements and adds two, leaving the capability non-empty.

---

## 5. Duplicate-heading result

**Zero duplicates.** An independent directory-walking scan of all four active changes parsed 175
`(capability, requirement heading)` delta entries and found no heading owned by two changes, and no
heading duplicated within one change.

Delta ownership by change:

| Change | Capabilities |
|---|---|
| `harmonize-downstream-capability-specs` | `event-driven-prototype`, `state-driven-runtime` |
| `reshape-developer-facing-interfaces` | 12 incl. all four relinquished |
| `add-runtime-concurrency-limits` | `runtime-resource-governance` |
| `bootstrap-orcacore-spec-baseline` | `spec-driven-planning` |

`state-driven-runtime` is touched by both harmonize and reshape, but on **disjoint headings** —
harmonize owns only `Ephemeral mode has explicit limitations`. This is the arrangement Decision 1
describes and it is duplicate-free.

---

## 6. Blocking findings

### B1 — P1 (repeat of prior P1-5): the archived historical plan is still rewritten in the frozen target

`docs/archive/plans/end-to-end-plan-pre-v1.md` is staged as a byte-identical rename of
`docs/end-to-end-plan.md` (`RM`), but then carries an **unstaged 2-hunk, ~12-line modernization** of
historical content:

- `- management `Statistics()` / durable projection statistics` → replaced with host-owned BCL
  diagnostics wording;
- the historical `AddOrcaCoreOpenTelemetry(...)` hosting task → rewritten to "application-host owned";
- the historical `Statistics()` e2e test bullet → rewritten.

This is the **same file, same two hunks, same rationale** as prior finding P1-5. The prior verdict's
"Required sequence before re-review" item 4 read: *"Restore the archived pre-v1 plan byte-for-byte,
keep current guidance in the active plan, strict-validate all active changes, and freeze a new exact
target."* The target was refrozen **without** that remediation.

The frozen target therefore contradicts the design submitted with it:

- design Decision 5 — "Dated reviews and archived plans remain **byte-immutable**";
- design **Non-Goals** — "Edit immutable historical reviews or archived record content";
- task `7.2` — "Keep dated status/audit/review records immutable under `docs/archive/`";
- `CLAUDE.md` — "Nothing under `docs/archive/` is evidence of current behavior."

A repository-wide sweep confirms this is the **only** archived *historical content* file modified.
`docs/archive/README.md` is also modified but is an active index, which task 7.2 expressly permits.
An active plan does now exist at the old path (`?? docs/end-to-end-plan.md`), so the corrected
guidance has a legitimate home — the archived copy simply must be restored byte-for-byte.

Open task 7.2 does not absolve this: the prior review escalated it to a P1 gate condition on the
*freeze*, not to ordinary pending work.

**Required:** restore `docs/archive/plans/end-to-end-plan-pre-v1.md` to its byte-exact predecessor
content, keep the modernized wording only in the active plan, and refreeze.

### B2 — P1: four empty delta directories contradict Decision 1 and escape every gate

These exist and are empty (created 2026-08-01 20:39, when the deltas were deleted):

```
openspec/changes/harmonize-downstream-capability-specs/specs/durable-persistence-and-outbox/
openspec/changes/harmonize-downstream-capability-specs/specs/durable-runtime/
openspec/changes/harmonize-downstream-capability-specs/specs/event-routing-and-waits/
openspec/changes/harmonize-downstream-capability-specs/specs/repository-foundation/
```

Three compounding problems:

1. **Decision 1 is factually false as written.** It states: *"This change contains no delta directory
   for those capabilities, even when an earlier harmonize version held byte-identical text."* It
   contains exactly four.
2. **No task owns them.** Decision 7 and task `3.3` establish that an empty delta directory is an
   unresolved ambiguity requiring an explicit recorded disposition — *"Neither an empty directory nor
   an impossible negative guard may silently satisfy completeness."* Yet they own only the
   `add-runtime-concurrency-limits` instance. The scope reduction created four more of the identical
   class and assigned none.
3. **The freeze cannot see them.** Git does not track empty directories, so Decision 6's pipeline
   yields a byte-identical hash whether or not these exist. The reproducible-freeze guarantee has
   **zero coverage** for precisely the artifact class this change exists to govern. `openspec show`
   is likewise blind (`deltaCount: 7`).

The practical risk is the one Decision 2 was written to prevent: a directory-walking implementation
of its gate ("enumerates every canonical capability and **every active delta heading**") reads
harmonize as claiming six capabilities, four with zero headings — indistinguishable from a dropped
delta. My own independent scan hit exactly this and had to special-case it.

**Required:** delete the four directories (or record an explicit disposition per Decision 7), correct
Decision 1's assertion, and extend task 3.3 — or add a task — to cover empty delta directories in
*this* change, not only in `add-runtime-concurrency-limits`. Consider noting in Decision 6 that the
manifest pipeline is empty-directory-blind, so this class needs a separate check.

### B3 — P2, blocking for a planning approval: proposal Impact contradicts task 7.6

Proposal **Impact** asserts:

> No product `src/`, **tests**, samples, packages, provider schema, or runtime behavior is changed by
> this planning change.

Task `7.6` requires:

> Correct or delete namespace-pinned `ForbiddenPublicSymbols` entries that cannot match the current
> assembly owners; **add a regression** proving each forbidden symbol fails under its exact current or
> historical qualified owner…

`ForbiddenPublicSymbols` is defined in `tests/OrcaCore.DeveloperSurface.Guards/PublicApiBaseline.cs`
and `tests/OrcaCore.DeveloperSurface.Guards/PublicApiBaselineGuards.cs`. Decision 7 repeats the
requirement. The plan therefore **does** change tests, and review question 11 cannot be answered
affirmatively as the proposal is written.

This matters beyond wording for a change whose thesis is that scope declarations must be exact and
that coverage may not be inferred from prose (Decision 3).

**Required:** amend the Impact statement to except the guard-test correction required by task 7.6,
or move task 7.6 to the owning change.

---

## 7. Direct answers to the review questions

| # | Question | Answer |
|---|---|---|
| 1 | Scope accurately limited to the two capabilities + cross-tree process? | **Yes** |
| 2 | Four overlapping capabilities absent? | **Content yes; four empty directories remain — B2** |
| 3 | Reshape provides complete ownership of relinquished requirements? | **Yes — a semantic superset** |
| 4 | Zero duplicate `(capability, heading)` owners? | **Yes — 0 of 175** |
| 5 | Proposal / design / deltas / tasks mutually coherent? | **No — B2 (Decision 1 false) and B3 (Impact vs 7.6)** |
| 6 | Decision 1 prevents last-writer-wins duplicate sync? | **Yes in substance** — byte identity explicitly rejected as coordination; verified 0 duplicates. Weakened only by B2's residual directories |
| 7 | Decision 3 requires full re-entry for late amendments? | **Yes** — canonical OpenSpec, numbered requirements, acceptance criteria, implementation tasks, executable evidence, refreeze, and independent approval; mirrored by task 4.3 |
| 8 | Decision 6 freeze algorithm complete and independently reproducible? | **Reproducible — verified twice, both anchors exact.** Two gaps: the raw anchor defers line endings to "the review packet's stated convention" rather than pinning LF (LF reproduced correctly); and the algorithm is structurally blind to empty directories (B2) |
| 9 | Tasks 3.3 / 7.6 / 7.7 own their three subjects? | **Yes** — 3.3 owns the runtime-concurrency empty directory; 7.6 owns impossible namespace-pinned forbidden-symbol guards; 7.7 owns Phase-0 kickoff prompt archive provenance. Confirmed 7.7 is real: the move is currently ` D docs/implementation/…-phase-00-kickoff-prompt-2026-07-15.md` + untracked add, so Git records no rename |
| 10 | Prior rejection and Section 7 verdict preserved unchanged? | **Yes — both byte-identical (§8)** |
| 11 | No product source/test/sample/package/schema/migration change? | **No — the plan changes tests via task 7.6 (B3)**. No `src/`, samples, package, schema, or migration change is introduced by harmonize |
| 12 | Canonical synchronization blocked until task 1.5 approval? | **Yes** — task 1.5 is `**REQUIRED**` and unchecked; task 2.1 is `**POST-APPROVAL**`; Decision 2 requires approval first |
| 13 | Approval authorizes planning only? | **Yes as scoped** — and moot, since this is a REJECT |

**Task ordering and sufficiency.** Open tasks are not treated as a rejection reason. The ordering is
correct: freeze/approve (1) → sync (2) → sweep (3) → process (4) → reshape coordination (5) →
requirements/acceptance (6) → docs/guards (7) → exit gate (8). Prior finding P2-2 is partly
addressed — task 3.2 is now decided and checked, and tasks 7.2/7.3 no longer name paths that moved
under `docs/archive/plans/`. Tasks 6.4, 7.1, 7.2, 7.3, and 7.5 remain open despite being materially
performed; leaving them open is conservative and acceptable, since Section 8 re-verifies them. The
one genuine sufficiency gap is B2: no task owns harmonize's own empty directories.

**Coordination context (not a new finding).** The Section 7 final exit verdict of 2026-08-01 is a
**REJECT** (P1-A … P1-H), and its P1-C is *"`harmonize-downstream-capability-specs` contradicts
`reshape` and is itself unapproved"* — the finding this scope reduction correctly answers. Reshape's
own contract remains unapproved, which is why harmonize tasks 5.1–5.3 must gate on it.

---

## 8. Immutability and worktree confirmations

**Prior verdict files — byte-unchanged**, hashed before and after the full validation suite:

| File | SHA-256 | Before | After |
|---|---|---|---|
| `docs/review/harmonize-downstream-capability-specs-independent-planning-review-verdict-2026-08-01.md` | `521d5613184423ee86b870109918b6c078dace6b05e4dd62ac5fe2f6230e4fe9` | ✅ | ✅ |
| `docs/review/developer-facing-interface-section-07-final-independent-exit-review-verdict-2026-08-01.md` | `f9760ffe3b817223e47b05f7f66dfbfc2e9bf946f633c20a04474c8989009674` | ✅ | ✅ |

- **Is task 1.5 satisfied?** **No.** This re-review is a REJECT, so the required new dated immutable
  independent approval does not exist. Task 1.5 remains open.
- **May canonical synchronization begin?** **No.** Task 2.1 is gated on 1.5 approval, which is not
  granted. `openspec/specs/` must not be touched.
- **Section 7 checkpointing and task 8.0 remain BLOCKED.** Explicitly confirmed. The Section 7 final
  independent exit review is itself a REJECT, harmonize task 8.3 is unmet, and reshape must complete
  its own approved checkpoint. Neither this verdict nor strict validation authorizes Section 8.
- **No existing file was edited.** This review was audit-only: no canonical spec synchronized, no task
  checkbox changed, no proposal/design/delta/tasks edit, no product source, test, sample, package,
  provider-schema, or migration change.
- **No commit was created.** HEAD remains `ac46d99543daf85c0fa3234272997ba40f47f96b` and the HEAD tree
  remains `28f4033c4773ea7761afa905c9836fd25866f1c0`, verified after all validation.
- **This verdict file is the sole worktree addition.** Porcelain count moves 422 → 423 solely by the
  addition of this file.

---

## 9. Required sequence before the next re-review

1. Restore `docs/archive/plans/end-to-end-plan-pre-v1.md` byte-for-byte; keep the corrected guidance
   only in the active `docs/end-to-end-plan.md` (**B1**).
2. Remove the four empty harmonize delta directories with a recorded disposition; correct Decision 1's
   "no delta directory" assertion; give the empty-directory class a task owner in this change and note
   the manifest pipeline's empty-directory blindness in Decision 6 (**B2**).
3. Reconcile the proposal's Impact statement with task 7.6's guard-test regression (**B3**).
4. Re-run `openspec.cmd validate --all --strict`, the duplicate-heading scan, and `git diff --check`;
   refreeze and publish new raw and sorted-LF anchors with entry and expanded-record counts.
5. Obtain a fresh dated immutable independent approval. Only then may task 1.5 be checked and
   canonical synchronization of the two retained deltas begin.
