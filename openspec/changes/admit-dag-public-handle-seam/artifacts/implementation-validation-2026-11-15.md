# Public DAG facade implementation validation

Date: 2026-11-15. Worktree `X:/ocv302`, branch `draft/admit-dag-public-handle-seam`, parent `da1a4729ea2cdb9474eb219eb507760574f83dcb`. Uncommitted, owner-authorized combined candidate; external review remains pending.

## Executed evidence

| Lane | Result |
|---|---|
| Clean solution Debug rebuild, warnings as errors | 0 warnings, 0 errors |
| Clean solution Release rebuild, warnings as errors | 0 warnings, 0 errors |
| Hosting, full project | 75/75 |
| Durable engine, full project | 113/113 |
| PostgreSQL, full provider project | 121/121 |
| SQL Server, full provider project | 92/92 |
| Infrastructure, main lane plus final active-freeze check | 318/318 + 1/1 = 319/319 |
| Packed-package API and source-record checks | 2/2 |
| ExpectedRed | 14 failures, separately reported; no passing credit |
| Nine package consumers, including DAG hosting | All compile from twelve freshly packed current-source packages |
| Unchanged RV-2 two-assembly signature proof | 0 warnings, 0 errors; stand-in bridge, no runtime claim |
| Strict OpenSpec, all changes/specs | 27/27, structure only |

`implementation-test-results-2026-11-15.json` retains exact case names, outcomes, durations, result counters and hashes of the original TRX files, plus build/package/proof logs and probe results. Scratch logs are under `X:/dag-seam-task01-20261115/`. The active-freeze check passed after writing the manifest; it is rerun after these evidence-only updates.

Reproduction from the worktree: `dotnet build OrcaCore.slnx -c Debug -t:Rebuild -warnaserror`, then Release; `dotnet test tests/OrcaCore.Hosting.Tests --no-build`; `dotnet test tests/OrcaCore.Engine.Durable.Tests --no-build`; each provider test project with `--no-build --blame-hang-timeout 180s`; `dotnet test tests/OrcaCore.DeveloperSurface.Guards --no-build --filter Disposition=Infrastructure`; separately `--filter Disposition=ExpectedRed`. Run `tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition Green -CompletedSection 8 -CompletedTask 8.6`. This selects consumers through 8.6 and does not activate the reshape task ledger. Run `openspec validate admit-dag-public-handle-seam --strict`.

## Behavioral coverage and limits

The production facade exercises create, exact replay, changed-input conflict, competing create between hosts, registration idempotence and both closed RunInputConflict variants; richer definition mismatch; absent/incompatible reopen; two distinct CLR types with one full name and equal shape digest; lossy input rejected on initial start and replay before mutation; recorded key/format/fingerprint/JSON corruption; locally unavailable recovery without mapping or damage; an incompatible host consuming child joins and cancellation committed by a compatible host; authored-order detached snapshots; output availability and foreign-plan rejection; six fixed failure values; cancellation outcomes and terminal waiting.

Fault testing uses an explicit provider subscription barrier: both the fault and reconciliation commit before the waiter rechecks. A fault still present at recheck throws, including a fault committed after registration. PostgreSQL and SQL Server run the binding/losslessness, fault/barrier and real process-restart certifications. Four fresh processes exercise creation/fault, incompatible reopen, compatible reconciliation/cancellation/fault and inspection of CancellationRequested after restart. This is process restart, not merely host recreation.

The PostgreSQL run exposed checkpoint lineage loss. The provider now persists parent/root identities on load, insert and update. Its checkpoint regression checks preservation and clearing, and the real facade test checks lineage before fault injection. The greenfield initial schema gains two nullable UUID columns; existing deployed database upgrade compatibility is outside this candidate. No provider-port or lease signature changes.

Mapped node inputs, child outputs and ordinary workflow inputs retain their existing normalization. These tests do not certify losslessness for those values. Byte round-trip proves representation, not unchanged application meaning; semantic changes still require a definition-version bump. This is functional certification, not performance or production qualification.

The fourteen ExpectedRed cases remain missing exact scenario-driver/certification obligations owned by the wider reshape ledger. Passing facade/provider tests do not falsely complete them. Earlier Section7Scenario 3.11d failures reported during the draft did not recur: all 318 selected Infrastructure cases, including those guards, pass in this candidate.

## Boundaries and history

Compiled metadata permits exactly three non-public DAG types and seventeen members, with no added friend and no reflection into the coordinator. The mapping-only adapter remains limited to fifteen runtime members. Existing compiled forbidden-reference probes still reject unauthorized constructor/field/setter/type access. The candidate pins fifteen product source paths, eleven current documents/canonical files and the sole changed public API baseline (OrcaCore.Dag); package-source provenance reflects the actual assemblies.

Twelve candidate mutation cases were rejected (two unnamed owners, proposed/predecessor/source/canonical byte drift, invented approval, five missing rows); all six before/after baselines pass. A separate actual unowned canonical-file mutation is rejected by the whole-file guard. Task 0.1's earlier probe record remains intact.

All fourteen canonical files reconstruct exactly from parent plus the five unchanged RV-4 blocks. Forty-two prior draft packet/artifact files remain byte-identical, including all ten earlier request/manifest files. The process packet remains byte-exact. No real index entries are staged, no commit exists, and other worktrees are untouched. Historical approvals and Complete evidence are preserved; the ten superseded predecessor operations are explicitly candidate-owned, not relabelled approved.
