# Issue #2 canonical transition validation — 2026-10-19

Base: `4bd7c3e2f179a92bb5161edc16fbc0950d4b4126`, the verdict-only child of
contract checkpoint `3c8d33c1c89a3b7d4f1b0cd3a8c49ced5f3e4d21`, whose tree is
`b1611b09f2774bab0a84ae2a1492abd71a1e4459` and parent is
`fa88986aaac219c998b15e96748efdf2f245de51`.

The transition is uncommitted and canonical-review pending. F2 permits initialization
admitted before Closed to finish and publish Accepted during drain; only a still-
unaccepted start losing Stopping's acceptance/abort election is fenced. Queued pre-Init
starts still reject. The corrected two management blocks are the only normative
changes from the approved contract; four other blocks and all seven declarations
remain exact. All six additions are append-only canonical copies.

| Lane | Result and scope |
|---|---|
| Strict OpenSpec | 23/23 |
| Full root Release, warnings as errors | 0 warnings / 0 errors |
| Full Infrastructure, actual dotnet test | 253/253; no failures/skips; CI engine-only coverage include |
| Exact unchanged-source package feed | 12 baseline packages; no shutdown API compiled |
| New transition guards | Four facts: Git binding, pending authority, exact canonical/declaration copies, all 120 occurrence resolutions |
| Protection audit | Product source, runtime source tests, all executable API/compile/package baselines and authoring companion unchanged |

Commands, run sequentially in the exact mirrored short worktree `E:/oc2t`:

```powershell
openspec validate --all --strict --no-interactive
dotnet build OrcaCore.slnx -c Release -warnaserror
./tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -OutputDirectory artifacts/phase0-packages -NoBuild
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore -c Release --filter 'Disposition=Infrastructure' --collect:'XPlat Code Coverage' --logger trx --results-directory <outside-target-results> -- 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[OrcaCore.Engine.*]*'
```

Provenance: 195 rows / 51,501 B /
`0699286d812312ee349c6c631c863eab4b7464e6ed9a10b7282d775a509a9962`;
184 Synchronized, 8 SupersededByApprovedSuccessor, 3 planning-only outside canonical,
zero pending operations. Structural semanticApprovalEligible is true; independent
canonical approval and source approval remain null. The original 189-row subset,
18 predecessor files, all canonical preambles and 12 unowned whole canonical files
are unchanged. Directory inventory stays 26. Current artifact and whole-canonical
hashes are independently pinned; the schema-17 registry preserves all prior records.

The source-inventory fixture adds precisely one four-fact guard file: 352 physical
sources / 1,460 declarations, 204 active files / 772 active declarations. Retired
declarations, dispositions and credit stay exact. The maintained Task 7.20 note
records these counts. The maintained Task 7.3 table refreshes exactly 14 rows for
intentionally edited active documents, with its guard digest. The DAG freeze permits
only the same two canonical paths, now composed of the five issue #1 additions and
six exact issue #2 additions; DAG source restrictions and original canonical content
remain intact. These are explicitly reviewed process prerequisites, not source approval.

Numbered owners MG-066–069, PR-041, NF-032, OB-027, AC-543–554, doc 17's full
declarations/ownership/error table and 22 document targets are reconciled. The ledger
accounts for all 120 original occurrences individually: 81 Retain and 37 Augment
matched lines remain exact, two named Edit lines change, and no template default
disposition supplies approval. Guides distinguish canonical intent from unavailable
shutdown implementation/packages and cover three drain recipes, count-only Incomplete
diagnosis before Stopping, the concurrent-stop caveat and RunAsync's changed exception
plus unconditional disposal. Issue #1 archival is scheduled separately, not performed.

Only task 1.4 is newly ticked, recording the already approved contract checkpoint.
Tasks 2.1/2.2 and source/qualification 3.1–3.5 remain open. The author index is empty.
No source behavior, source-acceptance execution, package-consumer proof, publication
or issue action is claimed. Existing framework-only evidence is retained unchanged.

The self-inclusive manifest is `task-2-1-transition-manifest-2026-10-19.txt`. Exact
base/tree/porcelain/content freeze hashes, final logs/TRX and cleanup receipts are
in the ignored `.artifacts/publishing-validation/issue-2-transition/` review handoff.
Owned validation build/feed/fixture outputs are removed after verification; required
review evidence and previously policy-blocked scratch/source material are preserved.
