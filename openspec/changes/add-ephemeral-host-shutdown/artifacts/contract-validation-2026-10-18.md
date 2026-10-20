# Issue #2 contract validation — 2026-10-18

R1 revalidation, 2026-10-19: F1 drain-phase remediation from tree 7d3f144f.
The rejected target and its verdict are preserved; the seven declarations are unchanged.

Base: `fa88986aaac219c998b15e96748efdf2f245de51` on
`codex/issue-2-ephemeral-shutdown`. This validates an uncommitted contract target,
not implemented shutdown behavior or independent approval.

| Check | Result and scope |
|---|---|
| Strict OpenSpec, all active changes/specs | 23/23; structural validation only |
| Full root solution Release build, warnings as errors | 0 warnings / 0 errors |
| Exact current-source package feed | 12 baseline packages; no proposed API compiled |
| Full CI Infrastructure disposition, actual dotnet test command | 249/249, no failures/skips; CI engine-only coverage include |
| Framework-only DI/cancellation probes | 5/5 prior observations retained byte-exact; no new probes, Hosting/DI 10.0.9 |
| Provenance sensitivity controls | Prior three controls retained as historical evidence; unchanged mechanism, current full Infrastructure rerun |
| Scope audit | Seven proposed public types; six exact pending blocks; 120 individual dispositions across 61 base documents |
| Protected baseline | Product source, canonical/numbered/guide docs, permanent amendment registry, API/compile/package baselines and ordinary index unchanged |
| Guard edit audit | Exactly current artifact hash, catalog count/hash and appended predecessor; no assertion or mechanism change |
| Historical integrity | Original 189-row subset exact; all 17 predecessor files byte-identical to Git base |

Validation ran sequentially from the short disposable worktree `E:/oc2c`, detached
at the base, holding the same contract bytes and staged candidate tree. Native Git
history remained available for the existing commit-binding guards. The ordinary
author worktree/index was not staged or committed. Framework probes ran separately
outside the repository at `E:/oc2p`; no OrcaCore shutdown implementation was added.

Commands:

```powershell
openspec validate --all --strict --no-interactive
dotnet build OrcaCore.slnx -c Release -warnaserror
./tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -OutputDirectory artifacts/phase0-packages -NoBuild
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore -c Release --filter 'Disposition=Infrastructure' --collect:'XPlat Code Coverage' --logger trx --results-directory <outside-target-results> -- 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[OrcaCore.Engine.*]*'
```

The current record has 195 rows / 51,141 B /
`9547ee6c115df3bdf1fecba4e65b1c8c051d1f9e0360bc9e220831cb5b2c00d2`.
178 are Synchronized, 8 SupersededByApprovedSuccessor, 6 PendingAddition and 3
planning-only NewCapabilityOutsideCanonical. Semantic approval is false. The
active directory inventory is 26 / 2,124 B /
`4d9fbe5fbb56d75d8b78273983b9720ea72890d4948a51cfd86b90823d7d91df`.
The 17-predecessor catalog is 2,992 B /
`d08a9b95675da345ccb67eac92d3d67c8cd179fd39a1cc99417aa472cae5df65`.
The current artifact's independently pinned normalized SHA-256 is
`87f527a9a3cc39c6519fe75f860908778f705009fe23381e005104804f16c5e0`.

Task 1.4 stays open for exact independent approval and checkpoint; 2.1 onward stays
open. There is no canonical, product, new public-baseline, issue-closing or push
authority in a structural/process pass. Owning-source tests, source coverage,
provider/integration stress and the future public shutdown package consumer are
not claimed here: they belong to implementation qualification after approval.

The complete self-inclusive manifest is `contract-target-manifest-2026-10-18.txt`.
The independent review request and final base/tree/porcelain/content freeze hashes
are outside the target in `.artifacts/publishing-validation/issue-2-contract/` to
avoid a self-referential tree hash. Local logs, TRX and compact receipts stay there.
Cleanup receipts alongside the R1 freeze record enumerate removed scratch/build/package outputs
and any automatically blocked removal; the review request reports remaining files
explicitly. No source, required historical evidence or global package cache is deleted.

R1 diff scope: four normative blocks change, the other two remain exact; the
proposed seven-type C# fence is byte-equal to the rejected target. Twenty-seven
individual occurrence reasons change with no disposition, base-line, hash or owning
document changes. The original verdict is included unchanged as rejection history,
not approval. This target includes no implementation of the planned drain tests.
The review request provides the new tree/hashes, the diff from 7d3f144f and actual
lane and cleanup receipts. Existing historical framework probe JSON/source remain
byte-exact. Newly grounded Generic Host ordering uses the same pinned Host.cs and
HostOptions.cs sources; the engine consumer recipe remains task 3.4 work.
