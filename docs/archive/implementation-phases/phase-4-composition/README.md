# Phase 4 — Composition at Scale (spec Slice 4)

**Goal**: ephemeral `ForEach`; durable `RunChild`/`RunChildren` with lineage, outbox
spawning, durable throttling, exactly-once resume token.

**Entry criteria**: Phase 3 exit green.
**Exit criteria**: AC-601…615 green (AC-616 waits for Phase 5 sagas).

## Task index (expanded by T4-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T4-00 | Expand index | Sonnet | Template expansion |
| T4-01 | Deterministic partitioners | Haiku | `Item`/`Batch(size)`/`Batch(selector)`/`Custom`; stable ordering (CP-030); shared by both engines |
| T4-02 | Ephemeral `ForEach`: group state + dispatch | Sonnet | Parent-owned group/item refs, bounded concurrency via lane-aware dispatch (CP-010…013); AC-601…603 |
| T4-03 | `ForEach` join/failure/residual policies | Haiku | `WhenAll/WhenAny`, `FailFast/WaitAllThenFail/ContinueWithPartialFailures`, cancellation-intent-before-continue; AC-604, AC-605 |
| T4-04 | Child lineage model | Haiku | `ParentInstanceId`/`RootInstanceId` in identity + projections; tree queries (CP-020); AC-614 |
| T4-05 | `RunChild`: single child + `Wait` join | Sonnet | Child-start via unified outbox record kind, synthetic parent wait, completion propagation (CP-021/022); AC-606 (single), AC-615 |
| T4-06 | `RunChildren`: dynamic fanout + durable scheduler state | Sonnet | Deterministic child ids, item snapshot stability, start-window enqueue (CP-022/025); AC-607, AC-608 |
| T4-07 | Durable throttling | Haiku | `NextDispatchIndex`/`ActiveChildren` reconstruction across restart (CP-023); AC-609 |
| T4-08 | Exactly-once parent resume (barrier token) | Sonnet | Durable resume token, idempotent consumption, restart replay (CP-024); AC-610, AC-611 |
| T4-09 | `WhenAny` residuals + mixed outbox kinds | Sonnet | `CancelRemaining/LetRemainingComplete/DetachRemaining` recorded before resume; child-start + external records coexisting (CP-021, DU-033); AC-612, AC-613 |

## Phase-wide guardrails

- `ForEach` never creates child instances; `RunChild(ren)` never appears on the ephemeral
  surface (CP table). Compensation APIs do not exist yet anywhere (Phase 5).
- Group-level retry is out of scope by design (CP-021) — reject it in review.
