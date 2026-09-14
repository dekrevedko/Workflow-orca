# Harmonization Task 6.6 independent review verdict

**Date:** 2026-09-13
**Reviewer:** independent review
**Scope reviewed:** the twenty-two-entry Task 6.6 freeze on base
`4f061089bae7602a7d1f255436a0e6d8507c39df`, tree `ebc5bb7ec1ec306c9e489f92ef4092d763683716`, named
by `harmonize-downstream-capability-specs-task-6-6-dirty-manifest-2026-09-13.txt`, including the
Task 6.5 AA-1 carry-forward.
**Authorization scope:** creation of the Task 6.6 checkpoint only. This verdict does not authorize
Task 6.7, reshape task 9.6 membership changes, change archival, or Task 8.0.

## Summary

Task 6.6 gives the future-capability registry one exact identity across both normative trees,
splits deferred capabilities from removed concepts, cites that identity from the two canonical
requirements and their owning reshape deltas byte-identically, refreshes OpenSpec provenance through
a new dated record, and closes AA-1. All nine claims reproduced independently — the 176-row
provenance record by a from-scratch reimplementation — and every declared negative control is red.

One regression is recorded as a non-blocking P2: repointing the provenance fixture at the new
refresh record removes the only durable immutability pin the dated 2026-08-18 artifact had. I
measured this on both sides of the change rather than inferring it.

## Method

All probing ran in disposable `git worktree` copies. The target worktree was created from
`4f061089` with all twenty-two frozen entries copied byte-for-byte and its porcelain confirmed
byte-identical to the reviewed worktree's; a second, clean worktree at the same base was used only
to measure pre-change behaviour. The reviewed worktree was never modified, `HEAD` never moved, and
nothing was staged or committed in it. Both worktrees were removed and pruned, and the one
throwaway commit is unreachable from every ref.

A harness defect is worth disclosing: one probe run aborted mid-sequence on a console-encoding
error and left a guide edit un-reverted in the disposable worktree. Before any further probe I
restored that file from the frozen target and re-verified all twenty-two target files byte-identical
and the porcelain identical; every later probe re-verified restoration by hash.

## 1. Freeze anchors reproduced

- Raw commit-real porcelain: **1,569 bytes**, SHA-256
  `410e31b8dfa03bdf8118cf5cb67c3f26979958295d8d1fea3df926ee1cd600a7`; the published dirty
  manifest is byte-identical to it.
- Scoped content record, excluding only the self-referential provenance fixture: **21 rows,
  2,969 bytes**, SHA-256 `708817fca0a9c628bccf66a7d598c02175652c546a5e88e3a93386ce492660c4`.

Twenty-two entries: eighteen modified, four untracked, zero staged.

## 2. The approved Task 6.5 second-hardening chain carries zero drift

Checkpoint `867d81873cd832a530d216ae4192edea560570ca` contains exactly the six approved real paths,
and its tree `6f6c40afd10fc0db0c76d868c1d97fed481667cf` is **the same object my own simulated
checkpoint produced during the previous review**. Its recovered manifest (589 bytes / `8066b603…`)
and blob-projected content record (5 rows / 858 bytes / `504b6a2e…`) are byte-identical to the
approved anchors, and the previous verdict landed byte-exact at 14,382 bytes / `e1f472e6…`.

The approval-evidence commit is `bbb763f9ab65023da849a2b0bd35f5a773f1aeb7`. The full SHA quoted for
it in the covering chat message differs in one character and does not resolve; no provenance record
carries that spelling, so nothing needs correcting in the repository.

## 3. Provenance fixture integrity

Schema 10; nine archived freezes; eight entries. Every archived freeze — including the newly added
`6.5-second-post-review-hardening` at 5 rows / 858 bytes — recomputes byte-exact from its own
checkpoint commit, all eight entries' historical records recompute exactly, and **all eight
`currentWorktreeMatchPaths` sets are exactly maximal**. The pin refresh is honest: entry 5.1 drops
`openspec/specs/developer-facing-surface/spec.md` and entry 6.5 drops reshape `tasks.md`, the
crosswalk fixture, `RecoveryCrosswalkGuards.cs`, and `TaskAccountingGuards.cs` — precisely the
previously pinned paths this target modifies.

## 4. Claims 1 and 2 — one registry, two classifications

§13.4 is now headed exactly `## 13.4 Future-capability registry`, with ordered `### Deferred
capabilities` and `### Removed concepts` subsections. The deferred section holds the same sixteen
table rows as at base, untouched; the removed subsection holds `WaitLong` and author `Yield`.

A reader of `CLAUDE.md` might object that removed concepts "must disappear entirely", so I checked
before accepting the structure. Base §13.4 **already** contained the paragraph stating that
`WaitLong` and author `Yield` are removed, not deferred. The target adds no new mention; it inserts
a heading above existing text. The separation makes the approved reshape design's statement that
these names "are not in this registry" more literally true, and it is what the task's own approved
wording requires ("deferred and removed concepts remain distinguishable and searchable").

## 5. Claim 3 — canonical synchronization is exact and delta-backed

I extracted every requirement block from base and target using the guard's exact block rules. In
each capability exactly one requirement block changed, the same one in canonical and delta:

| Requirement | canonical == delta at base | canonical == delta now | chars |
| --- | --- | --- | --- |
| Deferred capabilities are documented without public placeholders | yes | yes | 1,444 |
| Saga remains an explicit deferred capability | yes | yes | 1,028 |

The complete canonical synchronization diff, recorded here because no repository artifact embeds it
(see DD-1), is one inserted clause per requirement, applied identically to canonical and delta:

- `developer-facing-surface` — "…in the future-capability registry" becomes "…in the
  future-capability registry at `docs/specs/13-phasing-and-open-questions.md` §13.4
  ("Future-capability registry")". Delta block SHA-256 `e2b94e61a26232cf…` → `5e22935dc259c2e3…`.
- `saga-orchestration` — "…retain Saga in the future-capability registry with a re-entry gate…"
  becomes "…retain Saga in the future-capability registry at
  `docs/specs/13-phasing-and-open-questions.md` §13.4 ("Future-capability registry") with a re-entry
  gate…". Delta block SHA-256 `594d562efb74d7dd…` → `93cdcfa516eccbab…`.

No canonical preamble changed, and the per-capability preamble pins are unchanged in the fixture.
The edit is a citation of an inventory that already self-identified as the registry; it adds no
obligation.

**Authority.** Harmonization task 6.6's wording first appears in commit `ad94140` (2026-08-05),
before the canonical-sync gate closed (`ff11ead`, 2026-08-20). The decision is therefore not a
post-gate amendment and needs no post-gate registry record. The `reshape-section-7b-…` post-gate
record binds requirement identities, which are unchanged. The single-owner invariant of harmonize
design §1 also holds: the heading remains solely reshape's.

## 6. Claim 4 — no legacy identity survives

A repository-wide search, not limited to the guard's `docs/**` scope, finds the old heading, the old
anchor, and the former "nothing links the two terms" claim **only** in the three guard-source
constants that forbid them. Every link to §13.4 in the repository uses
`#134-future-capability-registry`, which is the anchor GitHub generates from the new heading.

## 7. Claims 5, 6, 7, and 9 — guards, ownership, AA-1, accounting

- **Claim 5.** `Task66_FutureCapabilityRegistryUsesOneCrossTreeNameAndSeparatesRemovedConcepts`
  binds structure, both cross-references, delta identity, legacy absence, positive guide anchors,
  the source map, the completion record, and the design decision through named constants.
- **Claim 6.** Every Task 6.6 surface names reshape task 9.6 as owner of final membership, and the
  deferred table still lists durable `Publish` and definition-targeted fanout, which 9.6 is open to
  remove. Nothing hides or pre-empts that cleanup.
- **Claim 7.** `Task65CompleteDesignDecision` now pins the whole paragraph. Both round-52 AA-1
  mutations are **red on the focused guard itself**: deleting the decision sentences, and inverting
  them to claim the lifecycle types are public.
- **Claim 9.** One new `[Fact]` alone explains every accounting movement: declarations 1,389 →
  1,390, active 701 → 702, `OpenSpecCorpusGuards.cs` 9 → 10, sources unchanged at 337, and the
  reshape 7.20 attribution now names Task 6.6.

## 8. Claim 8 — the provenance record, reproduced from scratch

I reimplemented the gate's record algorithm independently from its source rules, including
proposal capability kinds, delta operation sections, block termination and trailing-blank trimming,
state classification, rendering, and ordinal sorting. I ran it against both trees:

| Tree | rows | bytes | SHA-256 | states |
| --- | --- | --- | --- | --- |
| base `4f061089` | 176 | 46,211 | `64b8b6df55efda81…` — matches the prior fixture | 173 Synchronized / 3 outside |
| target | 176 | 46,211 | `ea8719e768182f4e…` — matches the refreshed fixture | 173 Synchronized / 3 outside |

Calibrating against base first matters. It shows the reimplementation is faithful, so the target
match is independent evidence rather than a rerun of the guard. The set difference between the two
records is exactly the two rows named in section 5. Zero operations are pending, so this may be
reported as semantic approval, not only structural validation.

The dated 2026-08-18 artifact is byte-unchanged at `9e870909…`, and the refresh artifact's claims
agree with live evidence.

## 9. Validation reproduced from a clean checkout

- Non-incremental `-warnaserror` builds, Debug and Release: **0 warnings, 0 errors** each. Package
  feed: **12 packages**.
- Core 350 · Ephemeral 79 · Durable 99 · Acceptance 37 · Hosting 24 · ProviderCertification 96 ·
  PostgreSQL 101 · SQL Server 72 · Integration 11 — all green, zero skipped.
- Guard lane: **221 passed / 14 failed / 235 total**; every failure enumerated and all fourteen are
  the documented `ExecutableBehaviorExpectedRedGuards.Scenario_…` set.
- `openspec validate --all --strict` **18/18**; ledger **24 complete / 10 open / 34 total**;
  `git diff --check` clean; zero `src/**` changes.
- **Simulated checkpoint:** exactly **22 real paths** (4 added, 18 modified), tree
  `96b68ba33219d493f8c39a235895bfba525c2a70`, clean worktree, and still **221 passed / exactly 14
  expected red** in the committed state.

## 10. Scope and hunk census

Eighteen modified files, four new files, **289 changed lines** across twenty-eight `-U0` hunks; I
read every one. The substantive deletions are all disclosed and intended: the old §13.4 heading and
preamble (content preserved in the rewrite), the source map's name-mismatch note that this task
exists to resolve, the one-clause-shorter canonical/delta sentences, the superseded provenance
pointers, the prior accounting values, and pin-set entries for paths this target modifies. No
deferred table row, requirement scenario, or unrelated normative sentence is removed.

## 11. Mutation and probe results

| Probe | Mutation | Result |
| --- | --- | --- |
| C0 | untouched target | 221/221 plus exactly 14 expected red |
| A1, A2 | delete / invert the Task 6.5 decision sentences (AA-1) | **RED** on `Task65_…` |
| C2 | restore the old §13.4 heading | RED on `Task66_…` |
| C3 | restore the retired anchor in the ephemeral guide | RED on `Task66_…` |
| C4 | typo the Orleans guide's new anchor | RED on `Task66_…` |
| C5 | put `` `WaitLong` `` in the deferred table | RED on `Task66_…` |
| C6 | remove the Saga cross-reference from canonical only | RED on `Task66_…` **and** the canonical gate |
| G3 | remove it coherently from canonical **and** delta | RED on `Task66_…` **and** the canonical gate |
| G4 | drift the refresh record's SHA-256 line | RED on the canonical gate |
| G5a | drift the crosswalk's active-declaration total | RED on the source-inventory guard |
| G5b, G5c | reassign registry membership in the ledger / design | RED on `Task66_…` |
| G6 | revert the 7.20 attribution to Task 6.5 | RED on `Task720CompletionNote_…` |
| G1 | add an ``Authored `Yield` `` row to the deferred table | **GREEN** except the lapsing freeze |
| G2 | add a `` `Yield` `` row to the deferred table | **GREEN** except the lapsing freeze |
| G2b | add a bare `WaitLong` row to the deferred table | **GREEN** except the lapsing freeze |
| H1 (base) | tamper the dated 2026-08-18 artifact, pre-change | RED on the canonical gate |
| H1 (target) | the same tamper, post-change | **RED only on the lapsing freeze** |

Every file was restored and hash-verified after each probe.

## 12. Finding BB-1 (P2, non-blocking) — the dated provenance artifact loses its only durable pin

Before this change, `ValidateReviewArtifact` required the file at `artifactPath` — the 2026-08-18
Task 4.2 record — to hash to `artifactNormalizedSha256 = 9e870909…`. That was its sole durable
protection: entries 5.1 and 5.2 record *earlier* versions of the same path (`1cbb30c4…`,
`9375ac11…`), so neither pins the live bytes. This target necessarily repoints `artifactPath` at the
new refresh record, because the old record's `64b8b6df…` claim can no longer agree with live
evidence. In doing so it releases the old artifact from all executable immutability checking.

I measured both sides. At base, appending a line to the 2026-08-18 artifact reddens the canonical
gate with "must remain byte-accountable after LF normalization". At target, the identical tamper is
red only on the active-freeze manifest comparison, which lapses at the next activation — days away,
not at change archival.

I rate this above the P3 coverage gaps of recent rounds for two reasons:

- it is a **loss** of existing protection rather than a gap that was never closed; and
- the pattern **compounds** — every future provenance refresh will orphan its predecessor the same
  way, while the harmonization design holds that dated records "remain byte-immutable".

It does not block because every frozen byte is correct, the dated artifact is verifiably unchanged,
and the active freeze covers it until activation.

The fix is well precedented in this file. Keep a permanent catalog of superseded provenance
artifacts — path plus normalized SHA-256, pinned in guard source like the synchronized-removal
catalog — append one entry per refresh, and require each listed artifact to still hash to its pin.
I recommend closing it before the next target that touches canonical OpenSpec.

## 13. Observation CC-1 (P3) — the removed-name exclusion is spelling-coupled

`RemovedConceptRegistryNames` holds `` `WaitLong` `` and ``author `Yield` ``, and the deferred
section is checked with `NotContain` on those literal strings. G1, G2, and G2b show that
``Authored `Yield` ``, `` `Yield` ``, or an unquoted `WaitLong` can become deferred-table rows with
every durable guard green. Claim 2 is true of the document as it stands; the guard just proves less
than it reads.

A whole-word check for `WaitLong` and `Yield` across the deferred section closes this, and it is
safe today. I counted zero whole-word occurrences of either identifier in the current deferred
section (sixteen rows) and in the §13.4 preamble.

## 14. Observation DD-1 (P3) — delta amendment and canonical synchronization in one freeze

`CLAUDE.md` prescribes "Get approval, then synchronize" and "Record the exact sync diff", and
harmonize design §2 says synchronization "records the canonical diff". This target amends two
reshape-owned delta blocks and synchronizes them into canonical within the same unapproved freeze.
The new refresh artifact records the resulting hashes, not the diff, and reshape's own ledger has no
note that a harmonization task amended its deltas.

The practical risk is negligible:

- the authority predates the gate;
- the single-owner invariant holds;
- byte identity is proven;
- the edit is a citation only; and
- section 5 of this verdict records the exact diff.

I record it because the two-step order and the recorded diff are the repository's own written rule.
Future cross-change delta amendments should either obtain approval before synchronizing or embed the
exact sync diff in a dated change artifact, with a pointer from the owning change's reconciliation
task (reshape 9.9 is the natural home).

## 15. Checkpoint sequencing

Writing this verdict creates a **twenty-third** entry against the declared twenty-two, which would
redden the active-freeze guard. Commit the approved **twenty-two-path** checkpoint first, then add
this verdict and its registry entry in the following commit. The new 6.6 entry will be the only one
whose discovery glob matches this file.

## 16. Reviewer hygiene

`HEAD` remained at `4f061089bae7602a7d1f255436a0e6d8507c39df` throughout, nothing was staged, and I
created no commit in the reviewed repository, which still showed exactly the twenty-two frozen
entries when this verdict was written. Both disposable worktrees were removed and pruned.

## Determination

All nine claims reproduce independently, including the provenance record by a calibrated
reimplementation rather than by rerunning the guard. Every declared negative control is red, the
chain carries zero drift down to tree identity with my own rehearsal, validation reproduces in full
from a clean checkout, and the simulated checkpoint is clean at exactly the declared twenty-two
paths.

BB-1 is a real regression in immutability enforcement, but it leaves every frozen byte correct and
is best closed as the next hardening. CC-1 and DD-1 are non-blocking.

**Verdict:** **APPROVE**
