# Harmonization task 5.1 checkpoint-provenance independent review verdict

**Date:** 2026-08-21
**Reviewer:** independent, cold-context, audit-only review (no prior involvement in this thread)
**Review target:** commit `ff11ead781f8fef343fafc6e6bc8307d746e4a05`
**Review request:** `docs/review/harmonize-downstream-capability-specs-task-5-1-checkpoint-provenance-independent-review-request-2026-08-21.md`

**Verdict: REJECT**

This verdict supplies the previously-missing Task 5.1 checkpoint-provenance review only. It does
not approve the rejected Task 5.2 target, does not authorize a Task 5.2 checkpoint, does not start
Task 5.3, does not close the harmonization gate, and does not archive either change.

## Method

Reviewed the immutable commit object directly, not the dirty worktree. Created a disposable local
`git worktree` (first at a deep Windows temp path, then re-created at `C:\wt\ff11ead` after the
first location's paths exceeded `MAX_PATH`) detached at `ff11ead781f8fef343fafc6e6bc8307d746e4a05`,
with `core.autocrlf` explicitly forced to `false` and the tree reset so on-disk bytes exactly equal
committed blob bytes (see Finding F2). All build/test/validate commands ran from that worktree. The
worktree was removed after use; this repository's tracked files were not modified by this review.

## 1. Immutable committed-object target — MATCH

```
git cat-file -e ff11ead781f8fef343fafc6e6bc8307d746e4a05^{commit}   -> object exists
git rev-parse ff11ead781f8fef343fafc6e6bc8307d746e4a05^             -> 179421029f62bc0cd4d5968d465cf045f420ba34
git rev-parse ff11ead781f8fef343fafc6e6bc8307d746e4a05^{tree}       -> 3baddd97a6919bf5f674daed33bc236496a234c2
```

Both match the request's frozen parent and tree anchors exactly.

`git diff-tree --no-commit-id --name-status -r ff11ead...` returned exactly 17 entries (2 `A`, 15
`M`). Their path set, compared as a set (not emission order), is identical to the 17 paths recorded
in `docs/review/harmonize-downstream-capability-specs-task-5-1-dirty-manifest-2026-08-20.txt`. **MATCH.**

## 2. Dirty manifest — MATCH

`docs/review/harmonize-downstream-capability-specs-task-5-1-dirty-manifest-2026-08-20.txt`: 1,228
bytes, 17 lines, SHA-256 `271309bd1da188c023610ffdde4065729fbc17f16f3ae786fc17561d1842f898`.
Independently reproduced byte-for-byte. **MATCH.**

## 3. Committed-object content-record projections — NOT INDEPENDENTLY REPRODUCIBLE (disclosed, not fatal alone)

Following this project's own previously-confirmed content-record recipe (raw-line ordinal path
sort, 2-character porcelain-style status, `status\tpath\tlength\tsha256` tab-separated fields, LF
line joins, final LF, UTF-8 no BOM — per `project_freeze_protocol.md`), and using
`git cat-file blob ff11ead:<path>` for the raw projection and
`git cat-file --filters --path=<path> ff11ead:<path>` for the filtered projection over the 17
frozen path/status entries:

- Raw projection: reproduced size **2,428 bytes** (matches the claimed size exactly, and is exactly
  1 byte more than the historical 2,427-byte dirty-worktree record, matching the request's own
  disclosure). Reproduced SHA-256: `f66692b4fddcf043e83b1b6cf06e9c5cfa50bf9c43c4226e46102dcba967f080`.
  Claimed: `741cfd6bbdd46cb4390c2f40c0d21d81d35b3e3749438b38efda44f26da1ff72`. **Does not match.**
- Filtered projection (computed with `core.autocrlf=false`, matching what a Linux CI runner /
  `actions/checkout@v4` default would produce — see Finding F2 for why this setting matters):
  reproduced size **2,428 bytes** (matches claimed size). Reproduced SHA-256:
  `a943a4ed1c49ccebf96bf951cca710a242d3814385742b89d1e5d2ee17891b1f`.
  Claimed: `b18924f74e0f0e8d47d638db9440ebed4709eb597bb7103a36c5be943d1d32ba`. **Does not match.**

Byte counts for both projections match the request's claims exactly, which corroborates the field
layout (status/path/length/sha, tab-separated, LF-joined, 17 rows). Several plausible field-order
and status-format variants were also tried; none produced the claimed digests. The commit's tree,
parent, path set, and the underlying manifest file all verify cleanly (Sections 1–2), so this is not
a wholesale fabrication of the commit — but the two specific SHA-256 anchors the request labels as
"the reproducible target identity for this retroactive review" could not be independently
reproduced despite a byte-exact structural match. This is disclosed as an open, unresolved
discrepancy rather than concealed.

## 4. Technical claims re-derived

- **Claim 2 (42 canonical operations by capability):** `openspec/changes/harmonize-downstream-capability-specs/tasks.md`
  line 125 states exactly `developer-facing-surface (7), durable-persistence-and-outbox (5),
  durable-runtime (8), event-routing-and-waits (8), state-driven-runtime (2), workflow-authoring (3),
  workflow-contracts (9)` = 42. **MATCH.**
- **Claim 3 (permanent catalog, 11 entries, SHA-256):** the hash
  `81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd` is embedded as a source
  constant in `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs` line 19 at this
  commit — self-consistent with the guard that enforces it. **MATCH (as coded).**
- **Claim 4 (provenance record 176/165/8/3, semantic approval ineligible):**
  `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-4-2-openspec-provenance-record-2026-08-18.md`
  states record rows: 176, synchronized: 165, pending: 8, declared new-capability requirements
  outside canonical: 3, semantic approval eligible: no. **MATCH.**
- **Claim 5 (no `src/**`, public API, package, sample, provider-schema, or runtime-behavior
  change):** the 17-path diff-tree set contains only `docs/review/**`, `openspec/**`, and
  `tests/OrcaCore.DeveloperSurface.Guards/**` / `tests/OrcaCore.ProviderCertification/**` paths. No
  `src/**` or `samples/**` path appears. **MATCH.**
- **Claim 1 (eight discriminating infrastructure regressions):** not independently re-derived line
  by line against mutation tests in this pass (out of proportion for this review's scope given the
  decisive findings below); the corpus guard file compiles and the guard tests that exercise these
  regressions are part of the Infrastructure lane addressed in Finding F1.

## 5. Validation — DOES NOT REPRODUCE THE CLAIMED NUMBERS

Run against the disposable clean worktree of `ff11ead`:

| Command | Claimed | Reproduced |
|---|---|---|
| `dotnet build -c Release --no-incremental -warnaserror` | clean | **Clean, 0 warnings, 0 errors** |
| `dotnet build -c Debug --no-restore --no-incremental -warnaserror` | clean | **Clean, 0 warnings, 0 errors** |
| Guards `Disposition=Infrastructure` | 211/211 | **209/211 (2 failing) — see F1** |
| Guards `Disposition=ExpectedRed` | 14/14 intentional red | **14/14 (matches)** |
| `OrcaCore.ProviderCertification` | 96/96 | **96/96 (matches)** |
| `openspec.cmd validate --all --strict` | 18/18 | **18/18 (matches)** |
| `git diff --check` | clean | **Clean** |

### Finding F1 (material): Infrastructure guard lane is not 211/211 on a genuine clean checkout of `ff11ead`

Two guard tests fail reproducibly (three independent runs) on a from-scratch `git worktree` of this
exact commit, after seeding the local package feed via `pack-exact-package-feed.ps1` (required by
several other guards in the same lane and not itself part of the reviewed diff):

1. `ProductionDeletionLedgerInfrastructureGuards.Ledger_CoversTheExactRecoveryDiffCompileExclusionsOrphansAndRetiredPackages`
   throws `DirectoryNotFoundException` for `src/OrcaCore.Hosting`. Root cause verified directly: this
   guard's `ReadRetiredProjectPackages` helper calls `Directory.EnumerateFiles` on the directory of
   each retired `.csproj` path without checking existence first. In the long-lived primary working
   copy at `X:\Projects\GitHub\Workflow-orca`, `src/OrcaCore.Hosting` still physically exists on disk
   as stray, **untracked** (`git ls-files` confirms zero tracked files) `bin/`/`obj/`/empty-folder
   debris left over from before that package's removal — CLAUDE.md itself states this package's
   "source/test roots are absent" from the v1 manifest. A genuinely fresh checkout (a real `git
   worktree add`, or CI's `actions/checkout@v4`) has no such leftover directory, so
   `Directory.EnumerateFiles` throws instead of returning zero matches. This is a latent
   test-robustness defect that only "passes" in the contaminated primary working copy, not on the
   clean checkout the request explicitly requires ("so current remediation changes cannot
   contaminate the result").
2. `RecoveryCrosswalkInfrastructureGuards.TestSourceInventory_AndEveryRetiredDeclarationHaveExactNonCreditedDispositions`
   fails with `git archive` subprocess exit code 141 (broken pipe) reading historical checkpoint
   `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`. The identical `git archive --format=tar d76192f... tests`
   command run directly from a shell succeeds instantly and produces a valid 2.3 MB tar. The failure
   reproduced consistently (3/3 runs) both in the disposable clean worktree and, when the same
   narrow test filter was run in isolation, in the primary working copy itself — indicating a
   pre-existing process/stream-handling fragility in `RecoveryCrosswalkGuards.cs`'s `git archive`
   invocation (unrelated to this commit's 17-path diff, but real and currently reproducible against
   this commit).

Both failures are reproducible, not one-off flakes, and both undermine the specific "Infrastructure
211/211" number the request asks the reviewer to independently confirm on a clean checkout.

### Finding F2 (procedural, contributory): `core.autocrlf` sensitivity

An earlier attempt at the clean-checkout run (with the ambient `core.autocrlf=true` machine default)
additionally failed `NormativeContractInfrastructureGuards.CompanionBaseline_HasExactReviewedNamespaceArityAndSignatures`
because a fresh Windows checkout smudged `docs/specs/17-public-authoring-contract.cs` from LF (the
blob's actual stored bytes, 53,745 bytes, SHA-256 `41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3`)
to CRLF (55,032 bytes, different hash) on write-to-disk, because no `.gitattributes` exists in this
repository to pin line endings and the primary long-lived working copy never had this file
physically rewritten by a checkout event since it was authored. Forcing `core.autocrlf=false` before
checkout fixed this one test. This is disclosed because it also affects the "filtered" content-record
projection in Section 3, and because it means "clean checkout" reproducibility is itself sensitive to
an ambient git config value the request does not pin.

## Overall determination

- The core immutable-object anchors (commit, parent, tree, 17-path set, dirty manifest) all verify
  exactly as claimed.
- The provenance-record and canonical-operation-count technical claims (Section 4) all verify
  exactly as claimed.
- ExpectedRed (14/14), ProviderCertification (96/96), OpenSpec strict validate (18/18), both Debug
  and Release builds, and `git diff --check` all reproduce cleanly.
- However, two claims central to this request's own stated evidentiary standard do **not**
  independently reproduce:
  - the two committed-object content-record SHA-256 anchors in Section 3 (Finding disclosed, sizes
    match but digests do not, despite good-faith reconstruction of this project's own established
    recipe), and
  - the "Infrastructure 211/211" validation claim (Finding F1: 209/211 on a real clean checkout,
    with one of the two failures traced to a latent test defect that depends on stale untracked
    filesystem state absent from any genuinely fresh checkout, including CI's).

Per this repository's mandatory-reviewed-checkpoint rules, apparent green validation and a
completed checklist are explicitly not evidence of approval, and a claim that cannot be
independently reproduced is a finding to report, not a detail to wave through. Finding F1 in
particular directly contradicts what the request says a reviewer must reproduce on a clean checkout,
using a defect that is verifiably present in the reviewed commit's own guard code (not an artifact
of this reviewer's environment beyond ordinary clean-checkout conditions). That is sufficient, on
its own, to withhold approval.

**Verdict: REJECT.**

This REJECT applies only to the Task 5.1 checkpoint-provenance question this request poses. It does
not reopen or re-litigate the already-rejected Task 5.2 target, and it does not by itself block
remediation of Findings F1/F2 followed by a fresh Task 5.1 provenance request if the repository
owner chooses to pursue one.
