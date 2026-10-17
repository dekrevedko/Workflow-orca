# Issue #1 task 2.2 canonical binding — independent review request

Review this small uncommitted binding target on `issue/1-ephemeral-retention`, based on
`3eee5165e94ef91a0a1ef6eb4ec4b80ae1ea528f`. The preceding transition is already independently
approved and checkpointed. This target only binds that approval and proposes completing
tasks 2.1/2.2; it needs its own independent approval/checkpoint before source task 3.1 starts.

## Exact six-path scope

1. `openspec/changes/add-ephemeral-terminal-retention/tasks.md`: flip only 2.1 and 2.2 to checked.
2. `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json`: schema 13,
   both records at `CanonicalApprovedSourcePending`, actual canonical evidence paths, and
   one shared exact checkpoint/verdict evidence object. Source approval stays null.
3. `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs`: bind actual canonical
   Git evidence, require those two tasks complete, retain all source tasks open and preserve
   the existing contract/canonical requirement/owner checks.
4. `openspec/changes/add-ephemeral-terminal-retention/artifacts/task-2-2-canonical-binding-2026-10-17.md`:
   the dated, independently source-pinned binding record and seven carried P3s.
5. This request, `artifacts/task-2-2-binding-independent-review-request-2026-10-17.md`.
6. The self-inclusive native porcelain manifest,
   `artifacts/task-2-2-binding-dirty-manifest-2026-10-17.txt`.

The reviewer's new verdict is excluded. All earlier artifacts remain byte-exact. No product
source, canonical/delta text, numbered spec, guide, API/compile/package baseline, other task
text/checkbox, integration or remote issue change is part of this target.

## Evidence that must bind through Git

| Anchor | Exact value |
|---|---|
| Transition checkpoint | `d1d3cf669e966a567155205715ebc53c398875d5` |
| Checkpoint sole parent | `f1eb86e31306d0aab88d8f84d38c925ef2eaf4c7` |
| Checkpoint tree | `83a08eaad4511500f482b7beace3a9455678a3ff` |
| Verdict-only direct child | `3eee5165e94ef91a0a1ef6eb4ec4b80ae1ea528f` |
| Canonical approval evidence path | `openspec/changes/add-ephemeral-terminal-retention/artifacts/task-2-1-transition-independent-review-verdict-2026-10-16.md` |
| Verdict bytes / raw SHA-256 | 14,826 / `67abed678ba58bd72411b79b5ab4276299689197f067123a84acf6f7a19d747a` |

The validator pins the complete evidence value, checks both sole-parent relationships and
the checkpoint tree, requires exactly an added verdict in the child, and compares current
raw verdict bytes with the Git blob and digest. It also requires the APPROVE heading and
ancestry from that verdict child to HEAD. Git failures are fatal. The prior contract approval
and its historical registry artifact remain independently pinned without edits.

Both capability entries point to this actual canonical verdict. Source evidence remains
null and tasks 3.1–5.3 remain open; all requirement identities, canonical equality, owner
paths and acceptance references stay checked. The new binding artifact supersedes the
earlier artifacts' pending-review status claims without rewriting them. Requirement
provenance stays 189 rows / 49,879 bytes / `0f56a33d2aa17e6848ac8f3bf4769ec1a8604d0dadb25a7f9f9b8092a673f459`,
zero pending. Its fixture, TSV, artifact, inventories and guard digest pins are unchanged.

## Review and validation

Confirm that tasks.md differs from the base by exactly two checkbox bytes; there are no
other task edits. Confirm the fixture's other approval records and every previous artifact
are unchanged. Review schema 13 and the narrow canonical evidence validator for missing
object, incorrect parent/tree/child, changed verdict, missing canonical evidence, fabricated
source approval, stage rollback and premature source-state failures.

The accompanying final freeze/validation record reports actual results. Required lanes:
strict OpenSpec, Release warnings-as-errors, all Infrastructure guards, negative mutation
probes, exact staged disposable rehearsal and zero drift across final validation.

```powershell
openspec.cmd validate --all --strict
dotnet build OrcaCore.slnx -c Release -warnaserror --nologo -v minimal
& C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -OutputDirectory artifacts/phase0-packages -NoBuild
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore -c Release --filter 'Disposition=Infrastructure' -v minimal
```

Use full Git history in a fresh short worktree at the exact base; the author uses `E:/ocv196`.
Copy/stage only the manifest paths. Build, pack and test sequentially; populate the exact
package feed before the full lane. Twelve disposable mutation cases cover stage rollback,
either completed checkbox reverting, source approval, wrong checkpoint/tree/child/verdict
hash, verdict byte drift, missing per-record canonical evidence, premature source completion
and binding-artifact drift. Each must fail, with raw bytes restored between runs.

Freeze recipe: native `git -c core.quotepath=false status --porcelain=v1
--untracked-files=all`, with LF and separately `-z`. The manifest equals the native LF
output, including itself and this request. For every row, record
`status<TAB>path<TAB>raw byte length<TAB>lowercase SHA-256`; ordinal-sort complete rows,
join with LF and append one LF. Seed a copied index from base, add exactly those six
paths and calculate its Git tree. Compare staged scope/tree and raw file bytes with the
short disposable copy. Keep the ordinary issue index empty; recheck every freeze anchor
after validation. Final anchors accompany this request outside the self-inclusive target.

## Pending authority

Return APPROVE or REJECT for this exact binding target. Specify whether approval authorizes
checkpointing it and the required handling of verdict evidence. Until the approved binding
is checkpointed, the proposed checked boxes grant no source authority. No commit, source
slice, rebase/merge, push, pull request or issue change follows from preparing this request.
All seven P3s remain carried in the transition verdict and dated binding record.
