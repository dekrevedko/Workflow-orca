# Harmonization Task 5.2 provenance-remediation independent review verdict

**Date:** 2026-08-21

## Verdict: **REJECT** — one blocking finding. Everything else is genuinely fixed and mutation-proven.

I froze the target myself before touching anything: HEAD `ff11ead781f8…` (== remote), tree
`3baddd97…`, **24 entries (15 M, 9 ??), 0 staged**, raw == normalized porcelain 2,008 B
`d46f73eb…`, capability inventory 16 dirs / 0 missing / `5e8a9725…`, content record 3,701 B
`db950441…`. Re-verified byte-identical at the end — the worktree is exactly as the implementation
owner left it.

### The clean-checkout claim holds — independently verified

This was the decisive question, and it passes. A fresh `git worktree` at `ff11ead` (where
`src/OrcaCore.Hosting` is genuinely absent) received the 24-entry target and reproduced content
record `db950441…` exactly. The feed was seeded with `pack-exact-package-feed.ps1`; Infrastructure
passed **214/214**, twice, on a clean base, and the Release build had 0 warnings / 0 errors.

- **M1** — reverting the `Directory.Exists` guard turns both
  `Ledger_CoversTheExactRecoveryDiff…` and the new regression red. With
  `src/OrcaCore.Hosting` restored as debris, the defect moves to
  `src/OrcaCore.Providers.RabbitMq`. The new test at `ProductionDeletionLedgerGuards.cs:58` uses a
  temporary GUID path, so it remains discriminating in a polluted copy.
- **M2** — reverting the archive drain produces `git archive must read recovery checkpoint … but
  found 141`, 3/3 deterministically. Draining stdout to a `MemoryStream`, reading stderr
  concurrently, checking the exit code, and only then parsing is correct by construction and
  empirically.

This corrects the reviewer's round-38 call. `ProductionDeletionLedger…` failed in a mutation
worktree, was correctly ruled unrelated to the mutation, and was then incorrectly classified as an
environment artifact. It was a deterministic defect; 211/211 passed only because the long-lived
working copy contained untracked `bin`/`obj` debris.

### Everything else claimed, independently verified

| Claim | Result |
|---|---|
| Release / Debug builds | 0 / 0, clean worktree included |
| Infrastructure | **214/214** (polluted **and** clean) |
| ExpectedRed | exactly **14**, all in `ExecutableBehaviorExpectedRedGuards` |
| Full guards | 214 passed / 14 red / 228 |
| Provider certification | 96/96 |
| OpenSpec strict | 18/18 |
| `git diff --check` | exit 0 · `src/**` changes: 0 |
| Ledger | 16 complete / 18 pending / 34 |

- **3-state provenance machine works.** **M3** — forging `reviewState: "Approved"` with no APPROVE
  verdict is red (`Expected approvals to be 1, but found 0`). **M4** — creating a commit titled
  `Checkpoint harmonization task 5.2 …` in the throwaway worktree is red, naming the commit. The
  earlier `ContainSingle`-over-all-files gap is fixed: APPROVE verdicts are counted separately, so
  rejection history survives.
- **Task 5.2 request restored byte-exact:** 8,647 B / `e927fd33…`, identical to the bytes reviewed
  in round 38.
- **LF normalization is content-preserving.** The three canonical specs differ from `ff11ead` by
  exactly the eight Task 5.2 operations. The provenance record remains **176 rows / 46,211 B /
  `e1420f36…`, 173 Synchronized / 3 outside / 0 pending / 0 duplicate owners**, canonical
  14/547/`7165dac4…`, active 16/1,321/`5e8a9725…`, 11 removals. The crosswalk is LF with only the
  four count changes.
- **`.gitattributes` companion pin is safe:** the committed `17-public-authoring-contract.cs` blob
  is already LF, so `eol=lf` cannot break `CompanionSha256` on a fresh checkout under any
  `core.autocrlf`.
- **L1 is closed.** Fourteen canonical preambles are pinned individually and as a record: 14 /
  1,233 B / `595528c6…`. **M5** — rewriting a `## Purpose` turns the lane red on the preamble
  mismatch; previously the mutation stayed green.

### Blocking finding

**P1 — the provenance guard records a false historical anchor and structurally forbids the true
one.**

`review-manifest-provenance.json` records, for Task 5.1,
`historicalDirtyContentRecordBytes: 2427` / `e73b7f4b…`, and for Task 5.2, `86ffab08…`. The anchors
actually frozen and independently verified at the time were **2,428 B / `741cfd6b…`** and
**1,793 B / `0068973b…`**.

Rebuilding the Task 5.1 record from `ff11ead`'s own blobs across seven recipes reproduces
`741cfd6b…` (row-sorted, LF, trailing LF) and `b18924f7…` (CRLF-translated) exactly. No recipe
produces `e73b7f4b…`. The round-38 record reproduces as `0068973b…` from its recorded rows;
`86ffab08…` is neither that record nor its path-sorted variant.

`design.md` justifies the discrepancy as "Git normalized mixed line endings," but this repository is
`core.autocrlf=false`, so the dirty worktree bytes are the blob bytes — which is why `741cfd6b…`
was both the frozen dirty anchor and the commit projection. The guard then asserts
`historicalDirty != blob` and pins `2427`.

**M6** — setting the fixture to the values actually frozen at rounds 37 and 38 is red:
`Expected task51.HistoricalDirtyContentRecordBytes to be 2427, but found 2428`.

The guard makes correct provenance unrepresentable. Correct both
`historicalDirtyContentRecord*` values, drop the `Should().Be(2427)` pin and the two `NotBe`
assertions (or require the historical anchor to reproduce from the commit), and remove the false
line-ending rationale from `design.md`.

### Non-blocking findings

- `.gitattributes` pins only the companion contract. Nothing enforces LF on `openspec/specs/*` or
  the crosswalk fixture, so mixed-ending churn can recur silently.
- `historicalDirtyContentRecordSha256` is only regex-shape-checked, not recomputed. Even corrected,
  it remains unenforced without exact historical rows.

### Checkpoint-provenance disposition

The earlier Task 5.1 and Task 5.2 verdicts existed only in the review conversation. Avoiding a write
during a frozen review was correct; allowing the verdict never to reach the repository was not. A
verdict must be written and committed as its own checkpoint after the reviewed commit. Any Task 5.1
verdict must state the 210/211 clean-checkout correction, and every matching verdict file must be
registered with its byte hash.

**Verdict: REJECT**

This verdict does not authorize Task 5.1 approval evidence, a Task 5.2 checkpoint, Task 5.3, the
harmonization exit gate, archival, or reshape Task 8.0.
