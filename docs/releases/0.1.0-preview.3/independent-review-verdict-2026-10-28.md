# Preview.3 release preparation — adversarial self-review

Verdict: **APPROVE for local preparation**, with no blocking findings.
This is a separate adversarial author review, not external approval or permission
to checkpoint, push or publish. The user's current authorization covered the merge,
issue closure, master synchronization and release preparation.

## Frozen target

Base/parent: `82811cd41aae577d8a1ae13258790d3da0c30d72`.
Branch: `codex/release-0.1.0-preview.3`.
Manifest: `manifest.txt`, exactly 25 paths, including itself and the request.
Candidate tree: `c7b50f1eb28831acf6b84422c9a93d944b83b9f9`.
Content SHA-256: `1c47e3ad530141182534d94cf2390dd5fbaf27b1c13114d18376d1ca6c813c19`.
Porcelain SHA-256 before this excluded verdict:
`8145f1fbe3f2f81701a747c29e93b0ebffad76679ddd3013d056f1d9f841e534`.
No frozen file drifted during review. The actual index remains empty.
This verdict is excluded and belongs only in a sole direct child of a future
approved checkpoint.

## Checks and conclusions

- PR #14's merge is a real two-parent commit, with parents `3b7f98a` and
  `d1b2019`; its tree equals the approved PR head. Issue #13 is closed as
  completed. Local master and origin/master equal the merge. Master CI
  [40821352504](https://github.com/dekrevedko/Workflow-orca/actions/runs/40821352504)
  passed all three jobs. The unrelated primary checkout was preserved.
- Schema 24 completes only the two outstanding issue #13 checkboxes and current
  stage. The new completion receipt is hash-pinned and its parents, tree,
  ancestry and issue/CI snapshots are checked. All prior registry records and
  immutable approvals remain exact.
- Historical harness checks now resolve through approved Git snapshots. Current
  product-source checks remain enforced. No product source, public API,
  canonical/delta requirement, executable baseline or provenance row changed.
  The 206-row provenance pin remains exact with zero pending operations.
- Normal publication now depends on full CI and the six-cell native workflow.
  Its existing-feed mode verifies the original twelve-package CI artifact and
  never packs another feed. Every report binds the requested version and package
  hashes; missing/mismatched versions fail. Native failure prevents initial
  release assets and both registry publishes. Existing-release retry modes keep
  their reviewed immutable-asset behavior. Validation jobs have read permissions;
  publication credentials remain confined to their original jobs.
- Notes and guides distinguish preview.3 preparation from preview.2 availability,
  and selected ephemeral support from unsupported engines/RIDs/event operations.
  The old unsupported assessment remains historical. The fixed codec and
  consumer's C# assertions were not changed.

Fresh checks: OpenSpec strict **25/25**; Release build **0 warnings / 0 errors**;
Infrastructure **309/309**; final documentation/Git-binding replay **13/13**;
native gate tests **9/9**; actionlint **zero errors**. Twelve preview.3 validation
packages passed source/version/checksum validation, a fresh-cache external
consumer returned `42`, and publication-script controls passed **8/8**.
ReSharper 2026.2.3.1 on four guards (Release, Any CPU, net10.0, no-build after a
successful compatible build) reported **0 errors, 53 warnings, 46 notes**, with
**no new findings** against the reviewed baseline. Raw SARIF is retained.
The first preparation run's encoding/test-assumption mistakes were corrected;
they are recorded in validation.json rather than counted as product failures.

## Limits and next gate

The new hosted release/native composition has not executed during preparation.
It must pass on the approved final release source before publication. Prior
native source evidence remains 384/384 from run 40762764617. The local package
receipt belongs to merged source 82811cd, not future tag assets. NuGet.org has
no candidate-version collision in any of twelve indexes and no remote tag
exists; GitHub package-version availability could not be read with the local
CLI token (missing read:packages). Recheck before publishing with authorized
registry credentials; do not infer availability from that access failure.

The target remains uncommitted and unpushed. No release tag, registry push,
release dispatch, new PR or publication occurred. Approval must name the frozen
target before its checkpoint; publication is a separate gate.
