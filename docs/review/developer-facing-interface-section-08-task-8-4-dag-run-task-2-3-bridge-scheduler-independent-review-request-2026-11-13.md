# Independent review: DAG Task 2.3 bridge, scheduler and child-pump starvation fix

Date: 2026-11-13. Change: admit-durable-dag-run-progression. Main checkout on feature/dag-run-task-2-3.
Base 048c3a6ecd3541f30c57d9f600190e63f6a16d84; base tree cc97c9e0f1923f1edf989295d3e77d8d0ad41f88.
One combined uncommitted target, 55 paths (36 modified, 19 added). The live index is empty.
Manifest: [developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-bridge-scheduler-dirty-manifest-2026-11-13.txt](developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-bridge-scheduler-dirty-manifest-2026-11-13.txt).

## Authority and scope

Task 2.2 checkpoint 2fd7a7d789203cb1b780abf5478d0e67c070e7f7 and evidence 3dae4aff9e190af2fa4acbc93d3b8aa2d4d1383c remain unchanged and Git-verified. The merge base for this candidate includes origin/master as integrated at 048c3a6e. This candidate is not independently approved and does not activate Task 2.3, reshape 8.4/8.5, or the public Task 8.6 registration/start/handle API. No SQLite, canonical/delta, friend, package-edge or public API baseline change.

The proposed implementation clarification appends to proposal/design/tasks without rewriting the approved contract artifacts. Durable.Hosting rebuilds the run-input binding from the exact typed plan installed on this boot; it never resolves a CLR type from storage. Definition/version, fingerprint and canonical type-shape key are independent checks. Uninstalled definitions remain locally unavailable. Child input/output use workflow definition/version role keys, with the child checkpoint schema checked separately against the compiled workflow. Older synthetic type-hash fixture keys are not a production format.

## Review priorities

- F1: a retryable/not-yet child and a fault write that could not commit keep their existing claim until expiry. Wrong-lane and cancellation cleanup still release; neither deferred nor uncertain work is acknowledged. Provider certification saturates the first claim batch at sizes 1 and 3 on InMemory, PostgreSQL and SQL Server, proves the healthy start and expiry reclaim, and checks the attempt number.
- Bridge: fixed-codec normalization and eager detached decode run outside a turn. Missing type binding writes nothing; an exact run creation retries idempotently; mismatched input, graph, or identity cannot reopen. Review null presence, ordinary decode failures versus protocol faults, and the separately checked child schema.
- Turns and lifecycle: admission, join, cancellation and reconciliation share one local FIFO and provider fence. Check bounded renewal, one mutation, release/abandon, cancel versus late admission, retained receipt identity, authored order and capacity. Poison reconciliation rebuilds retryable signals from the owning run and exact child facts; it does not erase the old poison diagnostic or release uncertain slots.
- Scheduler: one dedicated, reusable evaluation worker per run on a boot host, with active and retired lane gauges. Work is coalesced and visited via a bounded FIFO; evaluate only the lowest Ready node when child capacity is available. No mapper owns a lease or claim. A newly Ready lower node invalidates the higher receipt. Same-version discovery can skip held work/full capacity but not a lost wake. Check the addressed hub versus unaddressed progression paths and bounded discovery wrap.
- Boundaries: the shipped Hosting DLL is checked against an exact 26-type/43-member Durable.Hosting reference inventory and the original Dag 3-type/15-member allowlist. The guard forbids direct provider references and the runtime-view scanner rejects reflection, UnsafeAccessor and expression bridges in shipped Hosting and its linked harness. The runtime-view source itself is now bound to the original approved blob.

## Author validation and limits

Debug and Release solution builds with plain -warnaserror: zero warnings/errors; 12 freshly packed packages. Core 390, Ephemeral 405, Durable 113, Acceptance 37, Hosting 50, ProviderCertification 116. The focused shipped-scheduler and batch-1/batch-3 starvation cases passed on each of InMemory, PostgreSQL and SQL Server. The shipped scheduler test uses reflection only in the test harness to call internal pre-8.6 entry points; the product assemblies are not linked copies and the store and hosted pumps are real. It proves a committed dependency output is decoded, the admitted order is 0 then 1, and cancellation of a blocked mapper does not stop another run. It is not a claim of exhaustive two-host, controlled-clock, or crash-injection DG-P certification.

Infrastructure pre-freeze 319/319; exactly 14 documented ExpectedRed failures. OpenSpec strict 26/26. Green compile fixtures pass; eight green package fixtures and the single expected dag-hosting red. Initial source-pin/accounting failures and a stale concurrent Windows test executable lock were not counted as passes. The final frozen-state result and any broader container-lane results are reported separately in the handoff.

Canonical provenance unchanged: 218 rows / 58,043 B / f6e5ed0ad28a597fbbea17a62a10c9cfd6e05f580e0051f62f51d558d7a24783, zero pending. The declaration inventory is 383 sources / 1,612 declarations; 227 active sources / 924 active declarations. Only the three changed package source records are refreshed. The exact source-candidate scope is pinned separately from the historical Task 2.2 source and verdict.

## Freeze and verdict

Manifest: raw git status --porcelain=v1 --untracked-files=all --no-renames, commit-real set, LF/final LF: 3795 B / 35a038698a856e488298a56e7190a8440b5a2e2f9dd0391f2061710792024939.
Semantic content: exclude docs/review/**, immutable-document-history.json and review-manifest-provenance.json. Ordinal-sort path<TAB>raw bytes<TAB>raw SHA-256, LF-join plus final LF: 51 rows / 6808 B / 2ccb210387d985ecd56c1b1f5772b13bbdb136374050e2b45e526e4dd776c43c.
Active content: include every manifest row except review-manifest-provenance.json, render XY<TAB>path<TAB>raw bytes<TAB>raw SHA-256, ordinal-sort and LF-join plus final LF. The fixture binds its byte count and digest; the handoff gives the self-inclusive copied-index tree. No extra checkout or commit rehearsal was performed in accordance with the main-checkout-only request.

Write one dated immutable APPROVE or REJECT for this exact target; a verdict is an extra path, not part of the checkpoint. Only after approval, verify zero drift, checkpoint the exact manifest tree and make a direct-child evidence commit that adds/catalogs the verdict and clears the active freeze while retaining the history pointer. Do not activate or advance another task based on this request alone.
