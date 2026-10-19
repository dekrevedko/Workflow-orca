# Issue #1 retention contract — R1 independent re-review request

Status: F1–F4 contract remediation; independent re-review requested, no new verdict received. Task 1.3 remains open; 1.4 and every canonical/source/consumer task remain open. This target grants no implementation or checkpoint authority.

This request supersedes the original request's current-state assertions. Preserve the original request, initial assessment, proposed route and `contract-independent-review-verdict-2026-10-16.md` unchanged as RECORD evidence of the rejected nine-file target. They do not describe the R1 target. Current planning authority is proposal/design/tasks/deltas plus this request, the R1 occurrence record, route and provenance artifact.

## Base, scope and explicit exclusions

Branch `issue/1-ephemeral-retention` deliberately remains on `370b239402c8974a45fc0a54f539a823ab470cf4`, the source reviewed in the rejection. It is not rebased and does not claim current GitHub sync. The refreshed `origin/master` snapshot during R1 preparation was `1c86c239fe11b893cc9bd526ce2cf64472323024`, beyond the verdict's `021b2bd` snapshot; both are informational drift, not this target's base. A later rebase requires a new freeze/review. Original checkout work is preserved.

The complete target is the raw self-inclusive `contract-r1-dirty-manifest-2026-10-16.txt` beside this request. It includes this request, that manifest, all current untracked change files, the unchanged rejecting verdict/evidence, and exactly two tracked modifications:

- `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json`
- `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs`

The only test-source edits are exact provenance hashes/catalog metadata; no assertion, algorithm, disposition or CI filter is weakened. All `src/`, canonical specs, numbered docs, declaration companion, executable public-API/compile/package baselines and the permanent post-gate fixture remain byte-exact against HEAD. The ordinary index and HEAD remain unchanged. No commit, canonical sync, push/merge, issue comment/closure, issue #2 work or new independent verdict is part of this target.

## F1 — complete provenance prerequisite belongs here

The contract target refreezes all five reviewer-named prerequisite items together:

1. `recordCount=189`, `recordBytes=49579`, current aggregate SHA-256 `1abed32398a353509dfc7daab037156aaafc0067bd710a78fe5a4cb7820bb127`, and `semanticApprovalEligible=false`. F2/F4 and steady-state corrections alter block hashes, so the rejected `b7172021…` aggregate is historical, not silently retained.
2. Pending additions: `developer-facing-surface` (1), `management-and-querying` (4), both owned by this change's open task 2.2, with its literal count claims intact.
3. Active capability inventory: 24 directories / 1,971 UTF-8 B / `a50788bb2f903a5903ddee5a494a92421e34c347e3a9b67f8a4c935a009dc03a`.
4. New current `contract-r1-openspec-provenance-2026-10-16.md`, with exact executable claims and full adjacent TSV; independently source-pinned normalized artifact hash `833de7f663e22f4faae4c6be66f1f4ca83cb392ed13834a4824957dac2e90ccf`.
5. Previous current `task-2-1-stream-identity-openspec-provenance-2026-10-07.md` retained at exact `f2739090…` and appended to the immutable superseded catalog: 15 records / 2,653 B / `039a151898cfa09f44754020c35314ddb33d050ee9fc8919d4c495df31fb1a65`. Update both source catalog pins and current-artifact pin; retain all previous records.

No separate process amendment or predecessor/successor exception exists: the rejection accepted five unique ADDED identities, host policy outside deferred destructive commands, and no existing canonical block rewrite. Green provenance checks must certify honest pending operations, never semantic/source approval. The old 184-row subset still reproduces 48,504 B / `fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8` exactly.

## F2 — definitive fate for aborted provisional starts

The settlement requirement and provisional scenarios now require cancellation after runtime creation but before acceptance, and exceptions after `Init`, to abort. Before returning the original caller cancellation/exception, the owning lane fences a nonterminal provisional instance to `Terminated` using caller-independent cleanup authority; prior immutable terminal status is preserved. No facade/start-key binding or handle is published. The unit remains outside accepted retention, protects physical work until settlement and releases eagerly afterward even with policy disabled. Acceptance winning the election returns `Accepted`; later start-token cancellation cannot abort it. No abort error promises that external effects did not run.

Tasks 3.1/3.2 require barrier-controlled first-step cancellation, exception-after-Init, acceptance-wins, both policy modes, exact terminal fencing, no orphan `Running` state, original caller errors and complete runtime/router/timer cleanup. These are implementation requirements, not a claim that today's runtime defect has been fixed or a new runtime regression test already passes.

## F3 — executable baselines follow implementing source

Task 2.2 contains signature text in doc 17's full options block and ownership table, normative reconciliation, permanent post-gate records and provenance only. The authoring-only declaration companion explicitly stays unchanged because it declares no host options. Executable public-API baselines, positive/negative compile fixtures and package-source records change atomically with members in 4.2, and are reviewed in 4.5. No new public API is compiled in this contract target.

## F4 — the start-to-observe gap is normative

The management delta includes the explicit count-pressure scenario between `StartOrGetAsync` returning `Accepted` and direct/helper output-wait admission: not-found for a job that already ran, then fresh-ID replay of the expired key that may repeat effects. Task 4.3 requires the deterministic barrier case. Task 2.1 requires both guide warnings: accepted-then-not-found is not proof a run never started; size `MaxRetainedInstances` above completions possible in the caller's start-to-observe gap, while retaining independent effect deduplication where needed.

## P3 resolutions and permanent registry disposition

`contract-r1-occurrence-dispositions-2026-10-16.md` classifies every expanded keyword match, including `EphemeralEngineHostOptions`, `HostOptions`, `StartOrGetAsync`, lifetime and memory: 499 hits / 54 files, all individually mapped in the adjacent TSV. Its record is 128,197 UTF-8 B / `6d19956655c8951c72517204b460437916e5172d135b56bb45f91418491c5e4b`.

The dispositions name repository-foundation's topology/table dependency, runtime-resource-governance's no-independent-admission-cap rule and option placement, doc 17 §17.4/table/distinct limits, MG-020 events, durable-only clarification of MG-050/051/053 and doc 03 terminal-memory prose, NF-031, and explicit exclusion of the hosting-absent declaration companion. Current numbered/canonical docs are not edited before approval. The delta uses steady-state member/default text, explicitly owns event-router `accepted` deduplication records, documents a valid one-tick interval's CPU footgun, and describes pin-first/retire-first eligibility without a fictitious reader-drain phase.

Permanent post-gate records are required at 2.2 for both capabilities once real 1.3 approval and 1.4 checkpoint evidence exists. No fabricated approval or current duplicate-owner exception is installed; the R1 route separates this future transition from the present contract-target provenance prerequisite.

## Required validation and freeze protocol

Strict OpenSpec and a Release warnings-as-errors solution build passed during remediation (22/22 structural items, 0 warnings/errors). The initial long-path Infrastructure run passed the corrected provenance guard but finished 246/247 because `run-compile-fixtures.ps1` failed deleting a generated `.deps.json` at the long path after its compile checks. That is an unexpected harness-cleanup failure, not an expected-red contract lane. The full lane must pass at the short disposable path before this target is declared ready.

Rehearse the entire target in a fresh detached short worktree at the pinned base, including all untracked additions and exact tracked modifications. Stage it there, check EOF/whitespace and the candidate tree, then build/pack/run the full Infrastructure lane on those exact bytes:

```powershell
openspec.cmd validate --all --strict --no-interactive
dotnet build OrcaCore.slnx -c Release -warnaserror --nologo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -OutputDirectory artifacts/phase0-packages -NoBuild
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore -c Release --filter "Disposition=Infrastructure" --logger trx
```

Capture native Git porcelain with every untracked file and its raw NUL form before/after validation. The manifest names itself and this request. Pin the complete `status<TAB>path<TAB>raw byte length<TAB>lowercase SHA-256` content record, ordinal-sorted with LF and one final LF, after all target files exist. The completed-run result and raw/content/tree anchors are delivered with this request in the author’s final freeze report; local logs/raw NUL capture live under ignored `.artifacts/publishing-validation/issue-1-r1/`. Do not treat an unstated result as a passing run. Any difference after validation invalidates the freeze and requires another review.

The reviewer’s old `E:/ocv190` worktree was removed only after its nine-file / 1,371 B content record reproduced the rejection anchor `eff7c4a9cb23017841334f574353e897acc1bff12446241ca397aa7f6792a5af`. The new rehearsal is author-owned validation, not independent review.

## Requested verdict

Return APPROVE or REJECT for the exact self-inclusive frozen R1 contract/provenance target, findings by location, and the exact next authorized action. Only a fresh independent approval permits task 1.4 checkpointing after drift verification. Canonical/numbered-document synchronization, executable-baseline implementation, runtime bug fixes and issue completion remain later separately reviewed gates. Do not infer approval from the accepted decisions inside the earlier rejecting verdict.
