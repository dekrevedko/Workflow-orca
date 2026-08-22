# Harmonization task 5.1 checkpoint-provenance independent review verdict (round 2)

**Date:** 2026-08-21
**Reviewer:** independent, cold-context, audit-only review (no prior involvement in this thread)
**Review target:** commit `ff11ead781f8fef343fafc6e6bc8307d746e4a05`
**Review request:** `docs/review/harmonize-downstream-capability-specs-task-5-1-checkpoint-provenance-independent-review-request-2026-08-21.md`

**Verdict: REJECT**

This is the second independent-review round on the same request document. The first round's
verdict is preserved unedited at
`docs/review/harmonize-downstream-capability-specs-task-5-1-checkpoint-provenance-independent-review-verdict-2026-08-21.md`
(**REJECT**). This file is a new, distinctly named record — it does not overwrite that file. This
verdict supplies the previously-missing Task 5.1 checkpoint-provenance review only. It does not
approve the rejected Task 5.2 target, does not authorize a Task 5.2 checkpoint, does not start Task
5.3, does not close the harmonization gate, and does not archive either change.

## Review target and scope note (read first)

Since the round-1 REJECT, the implementation owner reported remediation: two new guard tests, a new
`review-manifest-provenance.json` fixture, a claimed fix for the round-1 `DirectoryNotFoundException`
latent defect, and a `task-5-2-review-remediation-2026-08-21.md` record — all as **uncommitted**
working-tree state layered on top of `ff11ead`. `HEAD` is still `ff11ead781f8fef343fafc6e6bc8307d746e4a05`.

The request document is explicit and unchanged from round 1: "Review the exact Git object, not the
current dirty worktree" and "Run validation against a clean checkout of commit `ff11ead` so current
remediation changes cannot contaminate the result." That instruction is the authoritative scope for
this review, and it has not been edited between rounds (verified: the request file's content is
unchanged; not touched by this review). Per that instruction, **this review's target is the
immutable commit `ff11ead` in isolation** — not `ff11ead` plus the current uncommitted remediation.
The uncommitted remediation is examined below only to check the implementation owner's factual claim
that it "fixes" a round-1 finding, because that claim is independently checkable and bears on whether
the underlying commit-code defect still exists anywhere in the repository. It does not change what
is being approved or rejected: only a distinct, committed provenance object would let a future
reviewer approve a different, larger target.

## Method

Verified the local repository's `.git/config` before touching anything (`core.autocrlf=false`, an
override of the machine default `core.autocrlf=true` from `C:\Program Files\Git\etc\gitconfig`).
Created a disposable `git worktree add --detach -f C:\wt\ff11ead ff11ead781f8fef343fafc6e6bc8307d746e4a05`
(short path to avoid `MAX_PATH` issues, matching round 1's approach) and ran the frozen commands from
inside it. `git worktree` shares the parent repository's `.git/config`, so a `core.autocrlf` change
made inside the worktree is visible in the primary working copy too; this was used deliberately for
the content-record reproduction (see Finding R1 below) and was **reset back to `false` in the primary
repository afterward** — confirmed by `git config --get core.autocrlf` returning `false` and
`git status`/`git diff --stat` in the primary working copy showing the same 13 modified +
7 untracked entries as before this review began, with no content change. The worktree was removed
(`git worktree remove --force`) after use. This review created no tracked-file changes anywhere in
the repository except this one new verdict file.

## 1. Immutable committed-object target — MATCH

```
git rev-parse ff11ead781f8fef343fafc6e6bc8307d746e4a05^        -> 179421029f62bc0cd4d5968d465cf045f420ba34
git rev-parse ff11ead781f8fef343fafc6e6bc8307d746e4a05^{tree}  -> 3baddd97a6919bf5f674daed33bc236496a234c2
```

Both match the request's frozen parent and tree anchors exactly.

`git diff-tree --no-commit-id --name-status -r ff11ead...` returned exactly 17 entries (2 `A`, 15
`M`). Their path set, compared as a set, is identical to the 17 paths recorded in
`docs/review/harmonize-downstream-capability-specs-task-5-1-dirty-manifest-2026-08-20.txt`.
**MATCH.**

## 2. Dirty manifest — MATCH

`docs/review/harmonize-downstream-capability-specs-task-5-1-dirty-manifest-2026-08-20.txt`: 1,228
bytes, 17 lines, SHA-256 `271309bd1da188c023610ffdde4065729fbc17f16f3ae786fc17561d1842f898`.
Independently reproduced byte-for-byte. **MATCH.**

## 3. Committed-object content-record projections — NOW INDEPENDENTLY REPRODUCED (resolves round 1's open discrepancy)

Round 1 flagged the two committed-object content-record SHA-256 anchors as not independently
reproducible from a good-faith manual reconstruction of the field layout. This round rebuilt the
exact same 17-row record (`status\tpath\tlength\tsha256`, tab-separated, ordinal-sorted by the
formatted row string, LF-joined, trailing LF, UTF-8, no BOM) directly from `git cat-file blob`
(raw) and `git cat-file --filters --path=<path>` (checkout-filtered) over the 17 frozen path/status
entries, computed inside the clean worktree:

- Raw projection: **2,428 bytes**, SHA-256 `741cfd6bbdd46cb4390c2f40c0d21d81d35b3e3749438b38efda44f26da1ff72`.
  **Exact match** to the request's claimed anchor.
- Checkout-filtered projection (computed with `core.autocrlf=true`, the machine/CI-realistic
  setting — see Finding R1): **2,428 bytes**, SHA-256
  `b18924f74e0f0e8d47d638db9440ebed4709eb597bb7103a36c5be943d1d32ba`. **Exact match** to the
  request's claimed anchor.

### Finding R1 (procedural, resolves round 1's Finding F2/Section 3): the recipe was always correct; round 1's manual reconstruction used the wrong `core.autocrlf`

Round 1 reproduced both projections at the *same* 2,428-byte size as claimed but got *different*
hashes (`f66692b4...` and `a943a4ed...`) because it forced `core.autocrlf=false` for the whole
exercise. Re-running the identical recipe with `core.autocrlf=true` (this repository's ambient
machine default, and the realistic value for a Windows-hosted checkout/CI runner with no
`.gitattributes` in this repository) reproduces **both** claimed anchors exactly. This round's
finding is that round 1's non-reproducibility was a reviewer environment-setting mismatch, not a
defect in the claimed anchors or in the underlying recipe. The historical 2,427-byte dirty-worktree
record (SHA-256 `e73b7f4b...`) remains, as disclosed, one byte smaller and not reproducible from the
commit object — that disclosure is accurate and unchanged.

This is a genuine, material improvement over round 1: the request's own stated "reproducible target
identity" claim in Section 3 now holds.

## 4. Technical claims re-derived (all from `ff11ead`'s own committed files, inside the clean worktree)

- **Claim 2 (42 canonical operations by capability):** `openspec/changes/harmonize-downstream-capability-specs/tasks.md`
  states `developer-facing-surface (7), durable-persistence-and-outbox (5), durable-runtime (8),
  event-routing-and-waits (8), state-driven-runtime (2), workflow-authoring (3), workflow-contracts
  (9)` = 42. **MATCH.**
- **Claim 3 (permanent catalog, 11 entries, SHA-256):** `HistoricalCanonicalRemovalCatalogCount = 11`
  and `HistoricalCanonicalRemovalCatalogSha256 = 81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd`
  are present as source constants in `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs`
  at this commit. **MATCH (as coded).**
- **Claim 4 (provenance record 176/165/8/3, semantic approval ineligible):**
  `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-4-2-openspec-provenance-record-2026-08-18.md`
  states 176 rows, 165 synchronized, 8 pending, 3 outside canonical, semantic approval eligible: no.
  **MATCH.**
- **Claim 5 (no `src/**`, public API, package, sample, provider-schema, or runtime-behavior change):**
  the 17-path diff-tree set contains only `docs/review/**`, `openspec/**`, and two
  `tests/OrcaCore.DeveloperSurface.Guards/**` / `tests/OrcaCore.ProviderCertification/**` paths. No
  `src/**` or `samples/**` path appears. **MATCH.**
- **Claim 1 (eight discriminating infrastructure regressions):** not re-derived line-by-line against
  fresh mutation tests in this round either (same proportionality judgment as round 1, given the
  decisive Section 5 finding below); the guard file compiles and its Infrastructure-lane tests
  (except the one in Finding R2) pass in the clean checkout.

## 5. Validation — DOES NOT REPRODUCE THE CLAIMED INFRASTRUCTURE COUNT; ALL OTHER NUMBERS REPRODUCE

Run against a fresh disposable clean worktree of `ff11ead` (package feed seeded via this commit's own
`tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1`, required by two Infrastructure
guards and not itself part of the reviewed diff):

| Command | Claimed | Reproduced |
|---|---|---|
| `dotnet build -c Release --no-incremental -warnaserror` | clean | **Clean, 0 warnings, 0 errors** |
| `dotnet build -c Debug --no-restore --no-incremental -warnaserror` | clean | **Clean, 0 warnings, 0 errors** |
| Guards `Disposition=Infrastructure` | 211/211 | **210/211 (1 failing) — see Finding R2** |
| Guards `Disposition=ExpectedRed` | 14/14 intentional red | **14/14 (matches)** |
| `OrcaCore.ProviderCertification` | 96/96 | **96/96 (matches)** |
| `openspec.cmd validate --all --strict` | 18/18 | **18/18 (matches)** |
| `git diff --check` | clean | **Clean** |

### Finding R2 (material, dispositive): the round-1 `DirectoryNotFoundException` defect is still present in `ff11ead` as committed, and is not fixed anywhere in the repository — committed or uncommitted

`ProductionDeletionLedgerInfrastructureGuards.Ledger_CoversTheExactRecoveryDiffCompileExclusionsOrphansAndRetiredPackages`
still throws `System.IO.DirectoryNotFoundException: Could not find a part of the path
'...\src\OrcaCore.Hosting'` on three independent runs in a genuinely fresh `git worktree` of `ff11ead`
(isolated run and full-lane run both reproduce it). Root cause, re-verified directly against the
worktree source: `ProductionDeletionLedgerGuards.cs`'s `ReadRetiredProjectPackages` helper (same file
and line as round 1) calls `Directory.EnumerateFiles` on a retired project's directory without an
existence check first. `src/OrcaCore.Hosting` does not exist in a genuinely fresh checkout (confirmed:
`git ls-files` in the primary working copy also shows zero tracked files there — the directory in the
long-lived primary copy is untracked `bin/`/`obj/`/empty-folder debris, exactly as round 1 found), so
`Directory.EnumerateFiles` throws.

Critically, I checked whether the implementation owner's claimed fix for this exists anywhere:

- `ProductionDeletionLedgerGuards.cs` **does not appear** in `ff11ead`'s 17-path diff (confirmed
  above under Claim 5/Section 1) — so it cannot have been fixed by the reviewed commit.
- `git status`/`git diff --stat` in the primary working copy (the uncommitted-remediation layer)
  shows **no changes to `ProductionDeletionLedgerGuards.cs` at all** — it is not among the 13
  modified or 7 untracked paths currently in the tree.

The claimed fix for this specific defect does not exist in the repository in any form — neither
committed to `ff11ead` (expected, since that's the immutable review target) nor in the uncommitted
working tree the owner described as remediation. The "Infrastructure 213/213" and "latent bug fixed"
claims relayed to this review do not hold for the artifact that was actually inspected. (The other
round-1 Infrastructure finding, the `git archive` broken-pipe failure in
`RecoveryCrosswalkInfrastructureGuards`, did **not** reproduce in this round's three runs — consistent
with round 1's own characterization of it as process/stream-handling fragility rather than a
deterministic defect. This is disclosed but is not the basis for this verdict.)

### Note on the two new guards (uncommitted, not part of `ff11ead`, examined only for context)

The uncommitted `review-manifest-provenance.json` fixture and its two new
`OpenSpecCorpusGuards` tests are not part of commit `ff11ead` and are therefore out of scope for a
verdict on `ff11ead` itself. For context only: running the new
`ReviewCheckpointProvenance_BlocksTask52CheckpointUntilTask51ApprovalExists` guard in the primary
working copy (which does contain the uncommitted code) independently reproduced the two
content-record hashes from Section 3 above exactly (with `core.autocrlf=true`), but the same guard
run then failed a *different*, later assertion in its own `MissingApproval` branch: it requires zero
files matching `harmonize-downstream-capability-specs-task-5-1*verdict-*.md` in `docs/review/`, yet
the round-1 REJECT verdict already on disk matches that glob, so the assertion
`task51Verdicts.Should().BeEmpty()` fails deterministically today. This is a design gap in the new
guard (it does not yet have a state for "a REJECT verdict exists but no APPROVE exists"), not
something this review is authorized to fix, and not something that changes the verdict on `ff11ead`
— it is reported only because the caller's brief asked whether the two guard failures reported
against the prior round were genuinely fixed. One (Finding R2, `DirectoryNotFoundException`) is
verified **not** fixed anywhere. The other (`git archive` broken pipe) did not reproduce this round.

## Overall determination

- The core immutable-object anchors (commit, parent, tree, 17-path set, dirty manifest) all verify
  exactly as claimed — same as round 1.
- The provenance-record and canonical-operation-count technical claims (Section 4) all verify exactly
  as claimed — same as round 1.
- ExpectedRed (14/14), ProviderCertification (96/96), OpenSpec strict validate (18/18), both Debug and
  Release builds, and `git diff --check` all reproduce cleanly — same as round 1.
- Round 1's Section 3 content-record non-reproducibility is **resolved** this round: both anchors
  reproduce exactly once the recipe is run with the realistic `core.autocrlf=true` setting. This is
  genuine, verified progress.
- However, round 1's decisive Infrastructure-lane finding (Finding F1/R2,
  `DirectoryNotFoundException` in `ProductionDeletionLedgerInfrastructureGuards`) **reproduces
  identically** on a fresh clean-checkout of the same immutable commit `ff11ead`, because the source
  file containing the defect is untouched by both the reviewed commit and the current uncommitted
  remediation. The claimed fix for this specific, named defect does not exist anywhere in this
  repository.

Per this repository's mandatory-reviewed-checkpoint rules, apparent green validation and a reported
remediation summary are not evidence of approval, and a claim that does not independently reproduce
against the actual immutable target is a finding to report, not a detail to wave through. The request
document's own evidentiary standard for this checkpoint (clean-checkout validation reproducing
211/211 Infrastructure) is not met by `ff11ead`, and the specific defect responsible is demonstrably
still live in the repository, not merely stale evidence from a prior round. That is sufficient, on its
own, to withhold approval again.

**Verdict: REJECT.**

This REJECT applies only to the Task 5.1 checkpoint-provenance question this request poses, evaluated
against the immutable commit `ff11ead` in isolation (per the request document's own explicit
clean-checkout instruction). It does not reopen or re-litigate the already-rejected Task 5.2 target.
It does not preclude a future Task 5.1 provenance request succeeding once (a) `ProductionDeletionLedgerGuards.cs`'s
`ReadRetiredProjectPackages` helper is fixed to tolerate a missing retired-project directory, and (b)
that fix, along with the now-verified content-record recipe and any other remediation the owner
wants credited, is committed as part of (or after) the reviewed checkpoint, so that a reviewer's
"clean checkout of the reviewed commit" actually contains it.
