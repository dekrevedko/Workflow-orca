# Independent review request: DAG facade seam Task 0.1 process gate

Date: 2026-11-15. Worktree: `X:/ocv302`. Branch: `draft/admit-dag-public-handle-seam`.
Parent: `da1a4729ea2cdb9474eb219eb507760574f83dcb`. Uncommitted; no real-index staging.

## Review scope and authority

Review the target named by `developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-process-dirty-manifest-2026-11-15.txt` in this directory. This is Task 0.1 process preparation. APPROVE permits a checkpoint of this exact process exception, followed by separately recorded independent verdict evidence and checkbox-only activation under Task 0.2. It does not approve Task 1.2, authorize canonical synchronization or product source, or close reshape Task 8.6.

RV-4 received pre-gate design approval in the review conversation. No verdict file or checkpoint is fabricated from that conversation. The current proposed blocks remain byte-identical to RV-4. The execution plan is preserved as written. Task 0.1 adds only a preparation note in tasks.md, process artifacts, the guard edit, four fixtures and this review packet. All task checkboxes remain open. Historical draft statements in the planning documents describe the earlier draft state; this request and the dated preparation note record the current process target. Document reconciliation remains Task 1.1.

## Exact ownership exception

Registry schema advances from 24 to 25. Existing rows, stages and evidence are unchanged. The new `proposedDagFacadeSeamSuccessors` array has five source-pinned rows, each MODIFIED, Proposed, with Task 1.3 as the open synchronization task.

| Capability | Heading | Owner chain, oldest first |
|---|---|---|
| developer-facing-surface | Implementation package boundaries use exact internal friends | reshape -> authoring-friend -> runtime-view -> facade seam |
| repository-foundation | Dependency direction remains one-way | reshape -> authoring-friend -> runtime-view -> facade seam |
| developer-facing-surface | Completion waits are notification-driven and race-free | reshape -> expose-ephemeral-failures-and-settlement -> facade seam |
| management-and-querying | DAG terminal waiting is notification-driven | reshape -> facade seam |
| developer-facing-surface | Closed registration and start results retain a cast-free success path | reshape -> facade seam |

The complete identities, predecessor operations and old/new block hashes are in the registry and the unchanged execution plan. The new validation loop requires the exact row set, resolves each immediate predecessor and proposed owner, verifies operations and hashes, requires canonical to remain equal to the predecessor, and rejects every unnamed active owner. The shared friend-owner check permits the fourth owner only through its exact Proposed row. Resolution accepts one active or one dated-archived record and rejects ambiguity.

The settlement validator admits the facade as the third completion-waits owner at Proposed stage while still requiring the settlement block to be canonical. Its other eight headings retain the existing two-owner assertion. Task 1.3/1.4 must later replace these proposed-stage constraints in the same reviewed atomic target; merely changing the registry stage fails today. Task 1.3 is pinned open. Tasks 0.1/0.2 remain unpinned for subsequent process activation.

The first full Infrastructure run exposed three additional historical registry checks in `EphemeralShutdownTransitionGuards.cs`, `NativeSupportTransitionGuards.cs` and `EphemeralFailureSettlementTransitionGuards.cs`. These now call a shared projection that first verifies schema 25 and JSON equality with the exact five source-pinned Proposed rows, then removes only that extension and restores schema 24 for their existing historical comparisons. All prior comparison logic remains. These three necessary guard files extend the execution plan's predicted file set; no product file is added.

## Provenance refreeze

- Requirement record: 223 rows / 59,416 bytes / SHA-256 `ca4411bd09c9770fdb8d123c9e85f5a35bfd787f9568bf10b41e23721a7411de`.
- States: 198 synchronized, 17 previously superseded, 5 pending modifications, 3 new-capability requirements outside canonical.
- Pending operations: developer-facing-surface 3, management-and-querying 1, repository-foundation 1. All turn green at `admit-dag-public-handle-seam` Task 1.3.
- Semantic approval eligible: **false**. Strict OpenSpec validation is structural only.
- Active capability directories: 39 / 3,184 bytes / SHA-256 `862963abaa1e185edfdc378405b03362d055fc46a5741cb1a17d031c31611853`.
- Canonical files, preambles and content anchors are unchanged.

The guard's own parser and classifier produced the measured record. The new `artifacts/process-openspec-provenance-2026-11-15.md` is pinned in the fixture and source. The previous current provenance artifact remains unchanged and is the twenty-ninth entry in the permanent superseded catalogue.

## Review packet preservation

All five earlier draft packets (ten request/manifest files) are byte-identical to the handoff. They are now appended to `immutable-document-history.json`, together with this request and manifest, because that catalogue enumerates every file under docs/review. Preserved draft artifacts and proofs remain unchanged.

`review-manifest-provenance.json` records this target as its active freeze. The earlier non-harmonization draft packets do not require fabricated rejected/approved entries: those entries require verdict evidence that does not exist as files. Their bytes are covered by immutable history and the active freeze instead. Existing review entries and evidence remain unchanged except for any mechanically refreshed current-worktree matches.

The active manifest is self-inclusive and retains raw commit-real porcelain order. Its content record uses status, path, raw byte count and SHA-256, ordinal-sorted with LF and one final LF. The only exclusion is the self-referential `review-manifest-provenance.json` fixture, exactly as the existing guard requires. The entire target is projected through a copied index for the handoff; no real-index staging is performed. No proposed tree/hash claim is embedded circularly in this request.

## Validation and review questions

Validation is recorded in `openspec/changes/admit-dag-public-handle-seam/artifacts/task-0-1-process-gate-validation-2026-11-15.md`. The two formerly red ownership guards pass. All eight planned negative cases reject the mutation, including each of the five row removals. Direct settlement-validator probes establish that its own checks still reject unnamed owners. The complete Infrastructure lane passed 319/319 with zero skips after the twelve exact local packages were packed. The full solution Release build passed with zero warnings and errors. ProviderCertification is built first to supply the eleven 3.11d compiled-evidence guards.

Please judge whether:

1. The five exceptions are exact and source-pinned, including the complete predecessor chains and stage/task constraints.
2. The shared friend-owner and settlement checks admit only the named Proposed successor and preserve their predecessor/canonical assertions and unrelated headings.
3. Provenance records all five pending operations and false semantic eligibility, with historical evidence unchanged.
4. Packet classification and negative probes establish a reviewable process target without treating pre-gate design approval as process, canonical or product-source authority.

No proof rebuild, prototype rerun, PostgreSQL/SQL Server runtime test, real-restart test or product behavior certification is claimed here. No independent verdict, commit or activation has been created. Task 0.2 is the next gate after independent process approval.
