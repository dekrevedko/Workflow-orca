# Issue #2 Section 3 qualification

Source base: `6253f6cd57f81afdde709ad705e9ba686a2818a2` on
`codex/issue-2-ephemeral-shutdown`. The canonical checkpoint is
`2e51fbdda5ebb6832c5ea93361b58acaf79b2b5e`, tree
`0f4ad1fbb68b87fb3aee5af428b8da4d8dd60b18`; the source base is its
verdict-only child. Git verifies both parents, the sole verdict path and its
2,897 bytes / SHA-256 `12f99ea87b68d9bb1a4f6b7abb6229c7be53a02a1a514e8635c493553ee952c5`.

This single target completes source/consumer tasks 3.1–3.4 and folds in the
canonical registry binding and checkboxes 2.1/2.2. Task 3.5 remains open for
independent review/checkpoint; both source-approval fields remain null. No
source commit, verdict, push, PR, merge, archive or issue change is implied.
The companion PR description is review material only.

| Check | Final candidate result |
|---|---|
| OpenSpec `validate --all --strict --no-interactive` | 23/23 |
| Full solution Release `-warnaserror` | 0 warnings / 0 errors |
| Benchmark `--smoke` | All scenarios passed |
| Full Infrastructure | 256/256 |
| Six unfiltered CI unit projects | 969/969: Core 354, Ephemeral 352, Durable 102, Acceptance 37, Hosting 24, ProviderCertification 100 |
| PostgreSQL / SQL Server / integration | 103/103, 74/74, 11/11; no skips |
| Ephemeral unfiltered `dotnet test` under load | 30 consecutive green runs × 352 = 10,560 passing executions; eight CPU workers on 32 logical processors, all workers exited |
| CI coverage tool and gate | ReportGenerator 5.5.11, seven final CI collectors, `+OrcaCore.Engine.*`: 15,987/20,211 lines = 79.1005%, above 75%; provider data excluded |
| Exact contract package feed | All twelve current-source `0.0.0-phase0` packages |
| CI pack and fresh external consumer scripts | All twelve `0.1.0-preview.issue2.20261019.1` packages; public typed workflow result 42 |
| Shutdown package-only consumer | 48/48; three owner/drain recipes, repeated fresh roots, root/scoped/external sentinels, pending starts, Incomplete retention and cancellation |

Validation used disposable short-path worktree `E:/oc2s` with the same base and
byte-equal candidate source. The six unit projects use CI's unfiltered
`dotnet test --no-build --no-restore -c Release` path and coverage collector;
Infrastructure uses its normal disposition filter. The final load series starts
after the cancellation-source fix; earlier successful runs are not substituted.
Final TRX/collector/package hashes and short logs are retained under the author's
ignored `.artifacts/publishing-validation/issue-2-source/qualification/` folder.

The new suite adds 135 runtime cases without editing the 217 existing cases.
It includes both retention modes, the 72-case pre-handle root/branch/item and
capacity/cancellation/timeout-child matrix, all AC-543–554, callback/producer/
physical/pin handoffs, Closed progress, local observations and post-Completed
dependency isolation. Test-first behavior assertions and early harness failures
are classified separately in local proof. An intermediate load run found a
real race: immediate CTS disposal after `CancelAsync` discarded queued handlers.
The final scope retains the source through logical, physical and dispatch owners;
the original failing test and an additional queued-handler case are green.

Only ephemeral product source changes. Exactly the seven reviewed hosting types,
both error codes and the positive/negative compile/package/API baselines ship
together. The other eleven package source hashes and API baselines, role
fingerprints, dependencies, friend list, authoring companion, canonical/delta
specs, predecessor evidence and retired declaration history remain unchanged.
The ephemeral source hash is
`6153eba93271e0371d28a06ff5f922441df8c1341908853c5669afd85e00145e`.

Provenance remains 195 rows / 51,501 bytes /
`0699286d812312ee349c6c631c863eab4b7464e6ed9a10b7282d775a509a9962`:
184 synchronized, 8 superseded, 3 planning, 0 pending; 18 predecessors unchanged.
Declaration inventory: 358 files / 1,497 declarations; active 209 / 809,
excluded 131 / 688, fixture 18 / 0. Guides describe this reviewed source
candidate separately from older published packages. Issue #1 archival remains a
separate maintenance target.

Consumer limits are explicit: its application-event dependency is public app
signalling, while actual workflow-event routing during Closed is covered by
production-router tests. `RunAsync` incomplete-stop disposal is an intentionally
unsafe negative control. Concurrent hosted stops promise no drain window.
Incomplete proves no physical external stop and requires retaining every owner.

Cleanup preserves source and compact review proof. `dotnet clean` succeeded in
the author worktree. Automatic approval review rejected removal of the failed
author consumer cache and the remaining generated directories/local feed packages
with only `blocked by policy`; those residual files are preserved. Disposable
validation and external-consumer cleanup is recorded in the review request.
