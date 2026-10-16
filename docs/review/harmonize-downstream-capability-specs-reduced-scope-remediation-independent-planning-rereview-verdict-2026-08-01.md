# Harmonize Downstream Capability Specs — Reduced-Scope Remediation Independent Planning Re-Review Verdict

**Date:** 2026-08-01
**Reviewer:** independent planning re-review (audit-only)
**Target:** `openspec/changes/harmonize-downstream-capability-specs` (reduced scope, post-remediation)

**Re-review of:** `docs/review/harmonize-downstream-capability-specs-reduced-scope-independent-planning-rereview-verdict-2026-08-01.md` (REJECT — B1/B2/B3)
**Predecessor record:** `docs/review/harmonize-downstream-capability-specs-independent-planning-review-verdict-2026-08-01.md` (REJECT — P1-1…P2-2)

Both prior records remain immutable and are not edited by this verdict.

---

## 1. Verdict

# APPROVE

All three blocking findings are independently verified as resolved. The remediation is surgical: a
record-by-record diff of the previous and current frozen manifests shows the **only** porcelain
changes are the `M` flag dropping off the restored archive file and the addition of the prior
verdict. Nothing else moved.

This approval satisfies task `1.5` and authorizes **planning only**.

| # | Prior blocker | Status |
|---|---|---|
| B1 | Archived historical plan rewritten in the frozen target (repeat of P1-5) | ✅ **RESOLVED** — byte-for-byte restored, verified against the HEAD predecessor blob |
| B2 | Empty delta directories contradicting Decision 1, invisible to CLI and freeze | ✅ **RESOLVED** — 5 directories removed, Decision 1 now true, new third freeze anchor added and reproduced |
| B3 | Proposal Impact contradicted task 7.6 on test scope | ✅ **RESOLVED** — Impact and design Non-Goals corrected in lockstep |

---

## 2. Provenance and manifest hashes

Reproduced independently **before** and **after** the full validation suite. All values match; the
validation suite was non-mutating.

| Value | Stated | Reproduced (pre) | Reproduced (post) | Match |
|-------|--------|------------------|-------------------|-------|
| HEAD | `ac46d99543daf85c0fa3234272997ba40f47f96b` | same | same | ✅ |
| Tree | `28f4033c4773ea7761afa905c9836fd25866f1c0` | same | same | ✅ |
| Porcelain entries | 423 | 423 | 423 | ✅ |
| Raw SHA-256 | `0f5213e391958c3abedf0414f61d2191e1cfba134dad41cf577d53436f8ab2d0` | same | same | ✅ |
| Normalized records | 453 | 453 | 453 | ✅ |
| Normalized SHA-256 | `469572cfb991da603f2fd5f74fc0cd3612ae402a595ff443ee9b724c6a171c52` | same | same | ✅ |
| Capability directories | 16 | 16 | 16 | ✅ |
| Directory inventory SHA-256 | `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5` | same | same | ✅ |
| Capability directories lacking `spec.md` | 0 | 0 | 0 | ✅ |

Pipelines used, exactly as specified by design Decision 6 — status anchors:

```text
git status --porcelain=v1 -z
| split on NUL | drop trailing empty record | strip one trailing CR per record
| sort by ordinal byte order | join with LF + one final LF | SHA-256
```

Capability-directory anchor (new in this revision):

```text
glob openspec/changes/*/specs/*/
| repository-relative, '/' separators, one trailing '/'
| sort by ordinal byte order | join with LF + one final LF | SHA-256
```

### Exact manifest delta from the previous freeze

Computed by differencing the previous freeze's normalized record set (452 records, captured during
the prior review) against the current one (453 records):

```
- RM docs/archive/plans/end-to-end-plan-pre-v1.md
+ R  docs/archive/plans/end-to-end-plan-pre-v1.md
+ ?? docs/review/harmonize-downstream-capability-specs-reduced-scope-independent-planning-rereview-verdict-2026-08-01.md
```

This is the strongest available evidence for the remediation's containment. The `M` flag dropping is
B1's fix made visible; the added record is the prior verdict. **No other porcelain record changed**,
which independently proves — rather than merely accepts — the claim that no product source, test, or
sample was edited. The five directory removals are correctly absent, since Git cannot represent empty
untracked directories; they are covered by the new inventory anchor instead.

---

## 3. Validation results

| Command / check | Result |
|---|---|
| `openspec.cmd validate --all --strict` | **18 passed, 0 failed**, exit 0 |
| `openspec.cmd list --json` | harmonize `5/33`; reshape `107/158`; add-runtime-concurrency-limits `16/16`; bootstrap `0/8` |
| Independent cross-change duplicate-heading scan | **0 duplicates** across 175 `(capability, heading)` delta entries |
| Duplicate `MODIFIED` requirement owners | **0** |
| Capability directories without `spec.md` | **0 of 16** |
| Empty directories anywhere under `openspec/changes/` (excl. archive) | **0** |
| `git diff --check` | exit 0; 0 non-warning findings |
| Retained delta headings vs canonical | 4 REMOVED + 1 MODIFIED — all **verbatim matches** |
| `src/` / `tests/` / `samples/` porcelain entries | 191 / 155 / 0 — **identical to the prior freeze** |
| Three anchors before and after validation | identical (§2) |
| Prior verdict files byte-unchanged | ✅ (§7) |
| HEAD / tree unchanged | ✅ |

Consistent with this change's own Decision 2, strict validation is recorded as structural evidence
only and is not reported here as semantic or provenance approval.

---

## 4. Blocker-by-blocker verification

### B1 — Archived plan restored byte-for-byte ✅

`docs/archive/plans/end-to-end-plan-pre-v1.md` now has **zero** unstaged diff, and its status moved
from `RM` to `R `. Verified by three-way content hash:

| Source | SHA-256 |
|---|---|
| Working-tree file | `2e031541de5aeb629ca3c452d7b4944de92eb24d86118cd5c6647297deefeb28` |
| Staged blob (`:docs/archive/plans/end-to-end-plan-pre-v1.md`) | `2e031541de5aeb629ca3c452d7b4944de92eb24d86118cd5c6647297deefeb28` |
| **HEAD predecessor** (`HEAD:docs/end-to-end-plan.md`) | `2e031541de5aeb629ca3c452d7b4944de92eb24d86118cd5c6647297deefeb28` |

The restoration is verified against the *true historical predecessor*, not merely against the staged
blob — so the historical `Statistics()` and `AddOrcaCoreOpenTelemetry(...)` guidance is intact. The
corrected guidance retains its legitimate home in the active `docs/end-to-end-plan.md`. A
repository-wide sweep confirms no archived historical content file is modified;
`docs/archive/README.md` remains an active index, which task 7.2 expressly permits.

Prior finding P1-5 and its restatement B1 are both fully discharged.

### B2 — Empty directories removed and structurally prevented ✅

All five directories are gone, and a repository-wide `find -type d -empty` under
`openspec/changes/` (excluding archive) returns nothing:

- `harmonize.../specs/durable-persistence-and-outbox/`
- `harmonize.../specs/durable-runtime/`
- `harmonize.../specs/event-routing-and-waits/`
- `harmonize.../specs/repository-foundation/`
- `add-runtime-concurrency-limits/specs/state-driven-runtime/`

Three further improvements go beyond the minimum I required:

1. **Decision 1's assertion is now factually true.** The inventory confirms harmonize owns exactly
   two capability directories.
2. **Decision 6 gained a third freeze anchor** that directly closes the blindness I identified —
   it states plainly that "Git status cannot represent empty untracked directories," pins the glob,
   separator, trailing slash, ordinal sort, LF join and final LF, and requires recomputation before
   and after validation. I reproduced it exactly from the written specification alone.
3. **Task 3.3 was upgraded from a one-shot fix to a recurring gate** — it now enumerates every
   `openspec/changes/*/specs/*/` directory and fails the sweep when any lacks `spec.md`. Task 8.1
   was extended to record the inventory count/hash and the zero-missing result at the exit gate.

The disposition of the `add-runtime-concurrency-limits` directory is recorded in both Decision 7 and
task 3.3, with the reasoning that the completed change declares only `runtime-resource-governance`
and OpenSpec reports only that delta — which I confirm. The four harmonize directories are
dispositioned by Decision 1, which is the correct location, since their removal *is* the substance
of the relinquishment.

### B3 — Test scope corrected coherently ✅

Proposal **Impact** now reads:

> No product runtime `src/`, **behavior tests**, samples, packages, provider schema, or runtime
> behavior is changed by this planning change. Planning may require documentation/OpenSpec
> corrections and **narrowly scoped infrastructure guard tests** that enforce the approved
> synchronization contract.

Design **Non-Goals** was corrected in lockstep, with the same behavior-test/guard-test distinction
and an explicit pointer to the task ledger. This is now consistent with task 7.6's
`ForbiddenPublicSymbols` regression in `tests/OrcaCore.DeveloperSurface.Guards/`. A grep across all
harmonize artifacts found no residual contradictory "no tests" claim.

---

## 5. Retained capability ownership

Unchanged from the reduced target and re-verified after remediation.

- **Owned by harmonize:** `event-driven-prototype` (2 ADDED, 4 REMOVED) and `state-driven-runtime`
  (1 MODIFIED — `Ephemeral mode has explicit limitations` only).
- **Relinquished to `reshape-developer-facing-interfaces`:** `event-routing-and-waits`,
  `durable-persistence-and-outbox`, `repository-foundation`, `durable-runtime` — now with no delta
  file *and* no delta directory.
- All four REMOVED headings and the single MODIFIED heading match canonical **verbatim**; every
  REMOVED carries `**Reason**` and `**Migration**`. `event-driven-prototype` remains non-empty.
- Reshape's ownership of the relinquished requirements remains a verified **semantic superset**
  (fixed `orcacore-json-v1` codec; operator-tier retention with `Archive`/`Purge` deferred and
  absent), as established in the prior verdict §4.

`state-driven-runtime` is touched by both harmonize and reshape on **disjoint headings** — the
arrangement Decision 1 describes, and duplicate-free.

---

## 6. Duplicate-heading result

**Zero duplicates.** An independent directory-walking scan of all four active changes parsed 175
`(capability, requirement heading)` delta entries and found no heading owned by two changes and no
heading duplicated within a change. A narrower scan restricted to `MODIFIED` owners — the specific
last-writer-wins hazard Decision 1 targets — also returned **0**.

---

## 7. Gates, immutability, and worktree confirmations

- **Is task 1.5 satisfied?** **Yes.** This verdict is the new dated immutable independent approval of
  the reduced planning target against the unchanged canonical baseline. Task `1.5` may now be checked.
- **May canonical synchronization begin?** **Yes — and only as scoped.** Task `2.1` is unblocked for
  the two approved deltas (`event-driven-prototype`, `state-driven-runtime`) into `openspec/specs/`,
  with the hand-applied `Purpose` revisions of task `2.2` and the exact canonical diff recorded.
  Task `2.3` remains binding: canonical messaging, persistence/outbox, repository-friend, and
  governance requirements must change **only** by the approved reshape synchronization. No harmonize
  sync may touch those four capabilities.
- **Section 7 checkpointing and task 8.0 remain BLOCKED.** Explicitly confirmed. The Section 7 final
  independent exit review of 2026-08-01 is itself a **REJECT** (P1-A…P1-H); harmonize tasks `8.1`–`8.3`
  are unmet; and reshape must complete its own approved checkpoint. Neither this approval nor strict
  validation authorizes Section 8. Harmonize tasks `5.1`–`5.3` remain the correct gate on reshape.
- **Approval scope.** This authorizes **planning only**. It does not approve canonical
  synchronization *completion*, product implementation, any checkpoint commit, or task 8.0.

**Verdict-file immutability**, hashed before and after the validation suite:

| File | SHA-256 | Before | After |
|---|---|---|---|
| `harmonize-...-independent-planning-review-verdict-2026-08-01.md` | `521d5613184423ee86b870109918b6c078dace6b05e4dd62ac5fe2f6230e4fe9` | ✅ | ✅ |
| `developer-facing-interface-section-07-final-independent-exit-review-verdict-2026-08-01.md` | `f9760ffe3b817223e47b05f7f66dfbfc2e9bf946f633c20a04474c8989009674` | ✅ | ✅ |
| `harmonize-...-reduced-scope-independent-planning-rereview-verdict-2026-08-01.md` | `5a077f1a71d85ed1c74c2e01f128de3a7c792463d63c13163e5185d5df794228` | ✅ | ✅ |

- **No existing file was edited.** This review was audit-only: no canonical spec synchronized, no task
  checkbox changed, no proposal/design/delta/tasks edit, no product source, test, sample, package,
  provider-schema, or migration change.
- **No commit was created.** HEAD remains `ac46d99543daf85c0fa3234272997ba40f47f96b` and the HEAD tree
  remains `28f4033c4773ea7761afa905c9836fd25866f1c0`, verified after all validation.
- **This verdict file is the sole worktree addition.** Porcelain count moves 423 → 424 solely by its
  addition.

---

## 8. Non-blocking observations carried forward

Recorded for the exit gate; none affects this approval.

1. **Decision 6's raw anchor still defers line endings** to "the review packet's stated line-ending
   convention" rather than pinning LF on its own terms. LF reproduced correctly in all three reviews,
   but the two normalized anchors are self-contained while the raw one is not. Consider pinning it at
   the Section 8 freeze.
2. **Tasks 6.4, 7.1, 7.2, 7.3, and 7.5 remain open despite being materially performed** in the current
   documentation tree. Leaving them open is the conservative choice and Section 8 re-verifies them;
   noted only so the exit gate does not mistake performed work for unstarted work.
3. **Reshape's contract remains unapproved** (Section 7 exit REJECT). Harmonize's canonical
   synchronization of its own two deltas is independent of that and may proceed, but tasks `5.1`–`5.3`
   must gate anything that depends on reshape's semantics.

---

## 9. Authorized next sequence

1. Check task `1.5`, citing this verdict.
2. Execute tasks `2.1`–`2.3`: synchronize only the two approved deltas, hand-apply the approved
   `Purpose` wording, record the exact canonical diff, strict-validate, and confirm no harmonize edit
   reached the four reshape-owned capabilities.
3. Proceed with tasks 3–7 (corpus sweep, process correction, numbered requirements/acceptance,
   documentation and guard coherence).
4. At the Section 8 gate, freeze with all three anchors — raw, normalized, and capability-directory
   inventory — and obtain a separate immutable exit approval.
5. Checkpointing and task 8.0 stay blocked until both this change and reshape complete their own
   approval and checkpoint gates.
