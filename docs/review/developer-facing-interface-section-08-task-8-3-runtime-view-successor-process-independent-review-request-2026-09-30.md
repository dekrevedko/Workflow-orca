# Independent review — DAG hosting runtime-view process gate

Date: 2026-09-30
Change: `admit-dag-hosting-runtime-view`
Task: 0.1 process implementation, before Task 0.2 independent approval
Base: `6d49716384b50e2cbaf503fcd782b63c6384bc1e`
Manifest: [developer-facing-interface-section-08-task-8-3-runtime-view-successor-process-dirty-manifest-2026-09-30.txt](developer-facing-interface-section-08-task-8-3-runtime-view-successor-process-dirty-manifest-2026-09-30.txt)

## Bounded review authority

Review only this self-inclusive 15-path process target. An APPROVE authorizes checkpointing the
ownership/provenance process target, not the `OrcaCore.Dag -> OrcaCore.Dag.Hosting` friend contract,
canonical synchronization, a friend attribute, codec access, or Task 8.3 source. Tasks 1.1–3.2
remain open. Task 1.3/1.4 is explicitly one later atomic reviewed transition.

No `src/**`, canonical `openspec/specs/**`, public API baseline, package source-provenance
fixture, existing authoring delta, or declaration crosswalk is changed. The existing exact
eight product friends and six-signature authoring allowlist stay compiled and unchanged.
This is not the implementation of the proposed ninth friend.

## Approved predecessor closeout

The 19-path closeout landed as `b5fb28e65dbf3fea102ddec1d5fe1cf9d794c659`, with the independently
approved tree `2ae62652c4ab763c539b3c93c2be3bb26398ab26` and parent
`738c3b591b6f6e72de13a7750601cb36430c514f`. Direct-child evidence
`dbc3086da206bde20cc8624816e3f45f1d13f9b8` adds/catalogs the closeout APPROVE byte-exact and
clears the active freeze; activation `6d49716384b50e2cbaf503fcd782b63c6384bc1e` changes only
the Task 3.2 checkbox. Its thirteen tasks are complete.

Verdict: `docs/review/developer-facing-interface-section-08-task-8-2-authoring-friend-closeout-independent-review-verdict-2026-09-29.md`,
7,888 bytes, SHA-256 `221c60c34f1db12c747eb2da20a7107bbe40baba227a9b8e0c223db1e913e531`.
The new guard validates that verdict as one terminal APPROVE, its actual addition, and the
single-parent evidence commit directly on the reviewed closeout checkpoint.

## Target and anchors

The manifest includes itself, this request and both history/freeze fixtures. It preserves raw
`git status --porcelain=v1 --untracked-files=all --no-renames` order and two-character statuses.
Its entry set is independently filtered through `git diff --name-only HEAD` plus
`git ls-files --others --exclude-standard`; content-identical phantom modifications are not
admitted. Main has zero staged paths.

- manifest paths: 15 (6 modified, 9 untracked)
- manifest bytes: 1,303
- manifest SHA-256: `9c4bc8db2f407b29aefe6209f4208487588f2bd094732bb0ccf71c78ce5a23e6`
- semantic record rows: 11
- semantic record bytes: 1,620
- semantic record SHA-256: `35b340f3b6141717e2223c41fd2003abecb98e3e759751ebb94931ea24ca3cc9`

Semantic recipe: take manifest paths except every `docs/review/` path and the two exact fixtures
`immutable-document-history.json` and `review-manifest-provenance.json`; read raw bytes, render
`path<TAB>byte length<TAB>lowercase SHA-256`, sort the rendered strings with
`StringComparer.Ordinal`, join with LF and exactly one final LF, then hash UTF-8 without BOM.
No line-ending normalization is performed for this dirty-content anchor.

The stronger active-freeze recipe includes every target path except the self-referential
`review-manifest-provenance.json`. Render
`XY<TAB>path<TAB>raw byte length<TAB>lowercase SHA-256`, ordinal-sort those rendered strings,
LF-join with one final LF and hash UTF-8. Its final value is pinned in `activeFreeze` after
this request and the immutable-history catalog are written. That anchor and the simulated
tree are published outside this self-inclusive file to avoid circular hashes. Stage exactly
the manifest in an isolated index/copy to reproduce the tree; do not stage main during review.

## Claims to challenge

1. **Only two ordered successor chains.** Schema 6 retains all authoring Proposed,
   ApprovedPending and Complete data unchanged and adds two source-pinned runtime-view rows.
   Only the two exact heading identities permit the active order reshape -> completed
   authoring friend -> proposed runtime view. Every old block hash, canonical hash, operation,
   predecessor, stage, proposed block hash and turns-green task is checked; no fourth record
   or duplicate heading elsewhere is admitted.
2. **No invented approval.** The authoring successor still equals canonical; the newest
   proposed blocks are full MODIFIED requirements but classify as PendingModification.
   Contract, sync, implementation and closeout tasks are open. No source authority is inferred
   from Task 0.1's completed process checkbox.
3. **Provenance remains honest.** The independent capture reproduced the prior 178-row
   record first, then derives 180 rows / 47,327 bytes /
   `77d388fa3a509d7eafdb871b8e87c3eab4a4e2c6cd407ee3dd9d707deed70a77`:
   173 synchronized, 2 superseded historical predecessors, 2 pending modifications,
   3 bootstrap-only; semanticApprovalEligible is false. Canonical stays 14 directories
   with unchanged preambles. Active delta directories are 20 / 1,645 bytes /
   `dfabdc307ab5c49d6bc15c140b7778a5dce0512faf601c83645574fa0e770d62`.
4. **Historical records are preserved.** The former current approved-pending artifact is
   added to the permanent superseded catalog, not edited. Four catalog entries hash to
   `566eda77e13dd20d505e7ab3f75a8d99b7e7c40231c442885cdd9d0ae60e5855`.
   Every old provenance artifact and registered review record remains byte-exact.
5. **The closeout observation is fixed.** Only the Task 8.0 map's 8.4 row changes. It now names
   the already-approved source checkpoint and checkpointed closeout, and separately labels
   the new runtime-view seam proposed and uncompiled. Its sole DAG-to-durable bridge statement
   is retained. The guard-source map hash is refreshed to
   `54b6fc8c859f5a40c912bef38b6f4ba8f7159f70ac64fc5724429fe71d26ebb9`.
6. **Codec ownership and next gates are explicit.** The new plan puts immutable descriptors
   and one evaluator in Dag, behavior evidence at the Hosting boundary without a test friend,
   and fixed-codec materialization/normalization/fingerprinting/commit on the durable bridge.
   Task 1.1 must review the exact descriptor/evaluator signatures, successful-null policy and
   reshape 8.3–8.5 ledger re-sequencing; no codec allowlist widening is proposed. The listed
   numbered/binding/guide dispositions are work for that separate contract review.
7. **No accounting shortcut.** No Fact/Theory is added; a private record/helper is not a
   counted test declaration. Existing crosswalk and exact package fixtures are unchanged,
   and their Infrastructure guards pass.

## Validation packet

All runs use the repository SDK pin; Release and Debug are non-incremental with warnings as errors.

| Lane | Result |
|---|---:|
| Debug / Release solution build | 0 warnings / 0 errors |
| Focused process ownership, Debug | 2/2 |
| OpenSpecCorpusGuards, Release | 15/15 |
| Core / Ephemeral / Durable | 350 / 79 / 99 |
| Acceptance / Hosting / ProviderCertification | 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Infrastructure | 240/240 |
| ExpectedRed, separate lane | exactly 14 failed / 0 passed |
| Package consumers, shipped Section 7.34 cutoff | 8 green / 1 expected-red dag-hosting |
| OpenSpec strict | 20/20 |

The fourteen reds are all `ExecutableBehaviorExpectedRedGuards`; this target does not claim
to complete any of them. PostgreSQL took about three minutes in this run; the successful
container count is real, not skipped. The compile runner's ExpectedRed exit 1 is intentional.

Before final freeze, copy the full packet into a short disposable worktree, seed its
twelve-package feed, run Infrastructure, stage only manifest paths there, run committed
diff --check and compare raw bytes with staged blobs. Main must remain uncommitted/unstaged.

Reproduction commands:

```powershell
dotnet build OrcaCore.slnx -c Debug -warnaserror --no-incremental
dotnet build OrcaCore.slnx -c Release -warnaserror --no-incremental
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=ExpectedRed"
pwsh -NoProfile -File tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -NoBuild -OutputDirectory artifacts/phase0-packages
pwsh -NoProfile -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition Green
pwsh -NoProfile -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed
pwsh -NoProfile -File tests/OrcaCore.DeveloperSurface.Guards/refresh-review-manifest-current-matches.ps1 -Check
openspec.cmd validate --all --strict
git diff --check
```

## Isolated negative controls

All fifteen are RED on the canonical process gate, with initial and final 2/2 GREEN controls
and byte-exact restoration of every mutated file in a checked disposable worktree.

| Control | Mutation / intended detector |
|---|---|
| C1 | Drop a runtime-view registry row; exact source-pinned pair |
| C2 | Proposed -> ApprovedPending in the fixture; source-pinned stage |
| C3 | Repoint the runtime predecessor to reshape; exact ordered chain |
| C4 | Change turnsGreenTask to 2.1; exact source-pinned owner |
| C5 | Reword the completed authoring predecessor; historical block hash |
| C6 | Reword the proposed runtime-view block; independent source hash |
| C7 | Rename away one successor heading while keeping a nonempty delta; required three records |
| C8 | Copy an unrelated canonical heading under the proposal; unregistered duplicate owner |
| C9 | Append a fourth active owner record for a governed heading; exact record count |
| C10 | Mark Task 1.3 complete before sync; task must stay Open |
| C11 | Hide both pending operations in the fixture; actualPending recomputed |
| C12 | Assert semanticApprovalEligible true; pinned artifact/computed pending gate |
| C13 | Hand-edit canonical to the proposed block; completed predecessor must remain synchronized |
| C14 | Repoint closeout evidence to activation, with guard rebuilt; single-parent direct-child evidence |
| C15 | Edit the prior approved-pending artifact; permanent superseded source pin |

The first C7 construction removed every heading and correctly hit an earlier nonempty-delta
check; it was revised to keep a valid nonempty block and rerun to hit the three-owner assertion.
That was a harness expectation correction, not an unexpected green product case.

## Reviewer instructions and sequencing

Check both normative trees via `docs/normative-source-map.md`. Review the schema 6 ownership
exception as a process change, not approval of the proposed contract. Challenge wrong stages,
extra owners, forged evidence, canonical hand edits and coherent provenance refreshes.

Use a short isolated review worktree for mutations. Preserve main's HEAD/index, every old
packet and all 15 target bytes. Deliver a dated immutable verdict named
`developer-facing-interface-section-08-task-8-3-runtime-view-successor-process-independent-review-verdict-<date>.md`
with exactly one terminal `**Verdict:** **APPROVE**` or `**Verdict:** **REJECT**` line.
Writing a verdict is separate from the frozen target; do not add it to this approved checkpoint.

If approved: checkpoint exactly these 15 paths, then add/catalog the verdict and clear the
active freeze in the direct-child evidence commit; activate the process-review task only.
The following target is contract Tasks 1.1–1.2. No proposed runtime friend or Task 8.3 source
lands before its contract and atomic-sync review gates.
