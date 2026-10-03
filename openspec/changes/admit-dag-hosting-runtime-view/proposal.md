## Why

The approved typed DAG plans keep node structure and mapping delegates internal, but their sole runtime adapter, `OrcaCore.Dag.Hosting`, has no compile-checked way to evaluate them. The authoring friend has now been independently implemented, checkpointed, and closed out; it must not be expanded into a codec or child-runtime grant to solve this separate seam.

## What Changes

- Propose exactly one additional product friend, `OrcaCore.Dag -> OrcaCore.Dag.Hosting`, for a closed internal runtime view: immutable node descriptors and one mapping-evaluation entry point returning typed input plus its declared type, or `DAG_INPUT_MAPPING_INVALID`.
- Preserve the existing six-member `OrcaCore -> OrcaCore.Dag` authoring allowlist. No codec, Core reference, runtime protocol reference, public factory, public compiled plan, or public child API is added.
- Keep fixed-codec normalization, dependency-output materialization, committed-input fingerprints, child-start/join and persistence on the durable runtime side of the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge.
- Propose the exact three-type/fifteen-member runtime view in design §6 and clarify successful-null output/input behavior in doc 17 §17.2.6. The existing runtime null check is not claimed compliant until separately reviewed source implements that policy.
- Review a ledger-only re-sequencing of open reshape tasks 8.3–8.5: mapping validation belongs to 8.3; codec materialization/round-trip and input commit belong to the runtime bridge in 8.4/8.5. Canonical durable-runtime/workflow-contracts remain unchanged.
- First review a process-only successor gate for the two exact headings below. Keep the completed authoring amendment and its reshape predecessors byte-exact. This proposal and its deltas are not friend approval, canonical synchronization, or implementation authority.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `developer-facing-surface`: extend “Implementation package boundaries use exact internal friends” with the narrowly guarded DAG runtime-view friend and Hosting-level behavioral evidence, without widening the authoring friend.
- `repository-foundation`: extend “Dependency direction remains one-way” with that ninth product friend, preserving every direct package edge and the sole durable child bridge.

## Impact

The eventual source target affects only the DAG runtime-view implementation, its hosting consumer and exact metadata guards, plus the existing durable child bridge owned by reshape 8.4/8.5. This process target changes no product source, public API, compiled friend metadata, or canonical requirement.

Contract reconciliation must name `CLAUDE.md`; numbered documents 03, 08 CP-020, 10 PR-005 and 17 §17.2.6, §17.3 and §17.5; Decision 22, solution architecture, project technical overview, and the pinned Task 8.0 map. Document 11's package-closure rule and the already-approved workflow-authoring/durable-runtime rules are explicitly checked for consistency rather than silently amended. Refresh affected Task 7.3 rows and their digest in each later reviewed document target. No new test friend is proposed: runtime-view mapping evidence belongs in Dag.Hosting-level behavior through the approved adapter.

Task 1.1's complete disposition is in `artifacts/task-1-1-runtime-view-contract-rv-remediation-2026-10-02.md`.
Only the two proposed delta hashes and their independent source pins change; stage remains Proposed,
pending operations remain two and semantic approval remains false. The separately reviewed atomic
1.3/1.4 target must implement exact supersession of both older owners and archive/withdrawal rules
stated in design §8. This contract preparation changes no product source or canonical requirement.

The rejected first contract packet is retained unchanged. RV-1 leaves Task 1.2 available for checkbox-only activation; the separate atomic 1.3/1.4 target binds its real approval evidence. RV-2 makes both deltas and doc 17 §17.2.6 self-contained. Mapper/decode failure classes, null-rule ownership, archival sequencing, complete §17.5 disposition and durable status/ledger pins are reconciled in this remediation.
