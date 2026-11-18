# Phase A runtime correctness qualification

Phase A is complete at the reviewed integration checkpoint `edf464f1c83d76d70121252bff81f51a9dae9708`, tree `b56068a8cd3bdb1306b3c118bb42bb2978e7390d`, subject to the final closeout checkpoint containing this record. Its parents are contract checkpoint `2bb31ee317f71bfc60bbbb06780c2bcdcb6a0264` and merged DAG PR #27, `a822b215fe1dc1d0fea4f498ab0ea0a29d44203d`. The exact source tree was independently accepted; this record closes the remaining evidence ledger, not a new source change.

## Completed scope

- F01: one captured checkpoint for envelope/aggregate loading, a persisted position fact for yield and fiber failure, stale-version rejection, and stable-tail quarantine. The two reviewed cancellation/backoff followups and park-conflict/rearm cases remain covered.
- F03: absolute-clock deadline election, early timer rearming, stable winner publication, and physical-attempt failure/ownership handling.
- F05: unreleased compiler format 2 frames every caller-controlled string as invariant UTF-16 length plus hexadecimal code units; schema ordering is ordinal. A Swedish/invariant culture test forces the old schema sort order to reverse and verifies identical fingerprints.
- F06: normalized host profiles compare every typed option and reject duplicate/conflicting registrations before mutation.
- The exact twenty-second workflow protocol event and the quality/identity/loading obligations are admitted in four existing capabilities and six numbered documents. Existing event mappings and DAG framing are preserved. The contract proposal was independently accepted before synchronization.
- The merged PostgreSQL migration, store and lineage test are byte-identical to PR #27. Shared certification now covers the marker and checkpoint lineage insert, replacement and clearing. Two independently constructed stores sharing one database/schema exercise both position-only commands and both publication read interleavings; assertions require the interleaving to execute, preserve the winner and publish once.
- Public API snapshots, package source provenance and the physical test inventory are refreshed. The inventory is 398 physical files / 1,677 declarations, including 242 active files / 989 active declarations. Historical retirement dispositions and zero unproven replacement credit remain unchanged. Earlier review snapshots remain historical; current exhaustive API guards still constrain the live surface.

## Executed validation

| Lane | Passed | Failed / skipped |
|---|---:|---:|
| PostgreSQL complete project, real `postgres:17-alpine` | 127 | 0 / 0 |
| SQL Server complete project, real SQL Server 2022 container | 98 | 0 / 0 |
| Core complete unit suite | 402 | 0 / 0 |
| Durable complete unit suite | 130 | 0 / 0 |
| Ephemeral complete unit suite | 430 | 0 / 0 |
| Hosting complete unit suite | 94 | 0 / 0 |
| Infrastructure, freshly packed twelve-package feed | 323 | 0 / 0 |
| Strict OpenSpec structure | 29 items | 0 failures |

Release builds used warnings as errors and completed with zero warnings/errors. The exact merge tree was exercised through a disposable commit with the same two parents (`8907d2cd9ae6463d0a873b46e6b1eecac4407ca0`) so ancestry guards could execute before the real reviewed checkpoint. Its tree equals the committed integration tree. All 177 reviewed path hashes were rechecked before checkpointing.

Provider full runs used the same integrated product source as the checkpoint. Subsequent changes added AC trait metadata to existing test methods/classes and corrected documentation guards, without changing provider test bodies or product source. The independent reviewer additionally ran final-tree PostgreSQL 7/7, SQL Server 6/6, coherent-load 17/17, schema/codec 49/49, and declaration/live-API/historical-DAG 13/13 checks: 92 tests, no failures/skips. It independently inspected the full-suite result counters. See [the exact independent verdict](integrated-independent-review.md) and [reviewed manifest](integrated-reviewed-manifest.json).

Before incorporating PR #27, the new PostgreSQL probes failed three lineage assertions with non-null IDs reloaded as null; the event and publication cases passed. After integration, the complete provider suite passes. Earlier failed guard runs were baseline/projection/acceptance-catalog issues and were repaired before the full 323/323 Infrastructure run; none is relabeled ExpectedRed. One overlapping local guard build encountered locked assemblies and was rerun after the prior test process finished. Raw TRX/build logs and disposable probes are held locally, not committed or advertised as portable CI artifacts.

Reproduction from this branch with full Git history: build `OrcaCore.slnx` in Release with `-warnaserror`; run the PostgreSQL and SQL Server test projects unfiltered with Docker available; run the four listed unit projects; build the exact package feed with `tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1` into `artifacts/phase0-packages`; run the guards with `--filter Disposition=Infrastructure`; and run `openspec validate --all --strict --no-interactive`. Focused real-provider names include `PositionOnlyCommitRejectsASecondHostAtTheOldVersion`, `InterleavedCheckpointLoadMustNotCommitPublishTwice`, `AppendAsync_AllWorkflowEventTypes_RoundTripsFromTail` and `CheckpointUpsert_RoundTripsFullRuntimeState`.

## Provenance and boundaries

The executable provenance record contains 228 rows / 60,833 bytes, SHA-256 `d147b1e8f3fa390bf31ad7c2c06bec9cd059159022eb8616ac586df10e274452`: 202 synchronized operations, 23 superseded by approved successors, three planning-only requirements outside the product canonical set, and no unresolved product canonical operations. Structural validation is separate from the exact independent contract acceptance.

[A.5 retention and long-loop evidence](../../../../docs/implementation/architecture-remediation-phase-a-evidence-2026-11-16.md) remains applicable: current providers remove complete instances atomically, so the conditional partial-tail continuity work is not required. The 10,000-iteration experiment at budgets 1 and 256 added 10,000 events and about 3.35 MB serialized JSON, with zero extra commit boundaries. Those event-volume measurements do not establish physical database cost or throughput.

Qualification uses fresh/reset stores and matched protocol/provider binaries. PostgreSQL's changed initial migration is not an upgrade path for already-journaled development databases. No mixed-version rolling upgrade, arbitrary corruption recovery, or separate-process workflow crash guarantee is inferred from the two-store probes. The independently merged DAG process tests retain their own scope. SQL Server remains the experimental adapter selected by the plan; passing correctness tests do not qualify its whole-document/history scaling or publish a revised support contract.

This closes Phase A (F01/F03/F05/F06 and its reviewed followups). Shared value-codec fidelity (F14), distributed workflow observation (F02), governance/retention/interpreter followups, and SQL support/scope publication remain in their owning phases. DAG progression/facade work retains its own approval and qualification record. No later phase is implemented or activated by this closeout.
