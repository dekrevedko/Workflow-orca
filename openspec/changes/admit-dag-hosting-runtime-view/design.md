## Context

Task 8.2 source is independently approved at `a9f835f939d683500ca231c7ba491ab8eae2aaae`, with direct-child verdict evidence `055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8`. The authoring-friend closeout is separately approved: checkpoint `b5fb28e65dbf3fea102ddec1d5fe1cf9d794c659`, evidence `dbc3086da206bde20cc8624816e3f45f1d13f9b8`, and checkbox-only activation `6d49716384b50e2cbaf503fcd782b63c6384bc1e`. All thirteen authoring-amendment tasks are complete. This new proposal does not stack on an incomplete source gate.

The public `WorkflowDagPlan<TRunInput>` exposes only identities and opaque node references. Its node plans, mapper delegates and the `DagNodeInputContext<TRunInput>` constructor are internal. The Hosting project references Dag and Durable.Hosting only; the current exact eight-friend graph has no Dag-to-Hosting grant. Reflection and public executable metadata would contradict the approved contract.

The existing durable runtime already reaches `CoreWorkflowValueCodec` through its reviewed Core friend. `OrcaCore.Dag` must stay an authoring package, not a fixed-codec/runtime participant.

## Goals / Non-Goals

**Goals:** propose one guarded runtime-view friend; preserve the authoring grant; put mapping evaluation on a compile-checked seam; keep codec and child work runtime-owned; preserve every earlier approval; review the process, contract, atomic sync, source, and closeout separately.

**Non-Goals:** no source or friend attribute in this process target; no public factory/compiled structure; no direct Dag-to-Core/engine/protocol dependency; no extra test friend; no changes to mapper code identity, DAG failure policy, admission, persistence or public signatures; no new codec allowlist member.

## Decisions

### 1. Preserve the ordered ownership chain

Only two headings may acquire a third active owner: reshape predecessor -> completed `admit-dag-authoring-friend-boundary` -> proposed `admit-dag-hosting-runtime-view`. The two older blocks retain their source-pinned hashes and stages. Canonical still equals the completed authoring successor. The newest blocks are full `MODIFIED` copies plus the runtime-view proposal, and remain `PendingModification`; semantic approval stays false. A third owner for any other heading, or a fourth owner for either heading, is red.

The process-only gate is not the friend contract review. Tasks 1.1–1.2 follow its own independent checkpoint. Tasks 1.3 and 1.4 are one atomic canonical-sync/ApprovedPending target; the two historical predecessor rows must be superseded only by exact hashes after that approved transition. No source follows until that separate review chain lands.

### 2. One internal runtime view, not raw authoring internals

Dag owns immutable node descriptors and a single mapping-evaluation entry point. Hosting receives only the validated view, typed mapped input and declared input type or `DAG_INPUT_MAPPING_INVALID`; it must not access draft mutation or mapper delegates. Exact internal type/member signatures are proposed in contract Task 1.1 and pinned when their compiled metadata is independently reviewed. Both reference kinds are guarded; generics, attributes, base types and interface implementations cannot bypass the allowlist.

Public metadata/factories would expose executable structure. Moving mapping into Core would break the package graph. Widening the OrcaCore authoring allowlist with the codec would assign runtime work to the wrong package. Those alternatives are excluded.

### 3. Runtime owns values and commit

Hosting hands the evaluator's typed input and declared type to the existing durable bridge. The runtime materializes successful committed dependency outputs, normalizes the mapped input using `orcacore-json-v1`, fingerprints the exact committed bytes, and commits input before child start. Dag neither serializes values nor imports runtime protocol. The implementation follows the existing Core -> engines -> durable hosting seam rather than adding friends.

The contract target must revise reshape 8.3 to mapping validation only and assign the fixed-codec round-trip to 8.4/8.5. This is a reviewed task-ledger re-sequencing, not a change to the existing durable-runtime behavior. The actual bridge implementation remains a later reviewed slice.

### 4. Behavioral evidence belongs at the host boundary

No test friend is added. Compiled Dag.Hosting-level behavior tests must exercise successful mapping, invalid foreign/non-direct/unavailable outputs, mapper failure before commit/start, replay and fixed-codec detachment through the durable bridge. Metadata probes test forbidden types and members separately. The current fourteen intentional guard reds remain a separate lane; this process target does not turn any Section 8 scenario green.

### 5. Complete document disposition, not a partial friend list

Contract Task 1.1 owns `CLAUDE.md`; docs 03, 08 CP-020, 10 PR-005, 17 §17.2.6 and §17.3; binding Decision 22 and solution architecture; project technical overview; and the Task 8.0 map. Document 11 is explicitly excluded because the `OrcaCore` package closure is unchanged. Existing canonical workflow-authoring opacity and durable-runtime DAG progression remain unchanged owners and are checked for consistency. Historical reshape design and dated decisions are linked/excluded, never silently rewritten. Refresh all affected Task 7.3 hashes atomically with their reviewed document edits.

The Task 8.0 map's stale 8.4 status is corrected in this process target: the authoring implementation is already independently approved at the real source checkpoint, while this different runtime view remains proposed. Its sole DAG-to-durable bridge statement remains true.

## Risks / Trade-offs

- Broad CLR friendship -> exact compiled type/member references, public API baselines and mutation probes constrain the consumed seam.
- Another successor accidentally hides pending work -> source-pinned ordered owners and old/new block hashes; two pending operations explicitly remain in the recomputed record.
- Codec work leaks into Dag -> unchanged six-member authoring allowlist and exact package graph; bridge-only codec ownership.
- Reopening historical approvals -> immutable packets and completed registry evidence remain unchanged; new artifacts append status.
- Registry sync order -> Tasks 1.3/1.4 must land atomically; no temporary canonical transition under Proposed.

## Migration Plan

Checkpoint the process target only after independent approval. Then review the complete friend contract and document/ledger dispositions. Synchronize the two approved successors and advance their registry atomically, review and checkpoint. Implement the runtime view and metadata probes as the next independently reviewed source target; build the durable codec/child bridge in its owning 8.4/8.5 slice. Promote the permanent record only with actual source and approval evidence. No archive or runtime source is part of this proposal target.

## Open Questions

- Contract Task 1.1 must spell out the exact evaluator descriptor/signature set and successful-null dependency-output policy against doc 17 before source. No ad hoc test friend is permitted.
- Existing structural DAG fingerprints omit `TRunInput`; this proposal does not amend that approved design. Task 8.6 must settle registration compatibility before changing it.
