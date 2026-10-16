# Proposed DAG successor OpenSpec provenance checkpoint

Date: 2026-09-27

This is a process-only checkpoint. It admits exactly two proposed successor deltas while
`reshape-developer-facing-interfaces` remains their active predecessor. It does not approve the
new friend contract, synchronize canonical requirements, or authorize Task 8.2 product source.
The previous complete 176-row record remains immutable in the superseded-artifact catalog.

## Reproduction

Enumerate every non-archived `openspec/changes/*/specs/*/spec.md` requirement. Normalize each
requirement block to LF, strip trailing empty lines, and stop at the next requirement or `##`
heading. Classify it against the verbatim canonical heading and block. Render each row as
`change<TAB>capability<TAB>capability-kind<TAB>operation<TAB>requirement<TAB>state<TAB>delta-block-sha256<TAB>canonical-block-sha256-or-dash`,
sort ordinally, join with LF and one final LF, then hash UTF-8 without a BOM.

- record rows: 178
- record bytes: 46,764
- record SHA-256: `a5833488353c035ac0a54b1388bde8b5dfed26b439b1fca1f0c43cc3d936cefb`
- synchronized operations: 173
- pending canonical operations: 2
- declared new-capability requirements outside the canonical set: 3
- duplicate `(capability, requirement)` owners: 2 exact registered predecessor/successor pairs; 0 unregistered pairs
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 2 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Canonical-capability inventory

The 14 canonical capability directories remain unchanged, ordinal-sorted with `/` and one final LF.

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`

The 14 normalized canonical preambles remain unchanged: 1,233 record bytes and SHA-256
`595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.

## Capability-directory inventory

Enumerate every active `openspec/changes/*/specs/*/` directory, render a repository-relative
forward-slash path with one trailing slash, ordinal-sort and join with LF and one final LF.

- capability directories: 18
- record bytes: 1,488
- SHA-256: `da7c00f44cc9e9d14b6756e5572d8b461c56d5454e315c4bec357d9d51fe4e50`
- directories lacking `spec.md`: 0

## Pending operations and owners

| Canonical capability | Pending | Turns green |
|---|---:|---|
| `developer-facing-surface` | 1 | admit-dag-authoring-friend-boundary task 1.3 |
| `repository-foundation` | 1 | admit-dag-authoring-friend-boundary task 1.3 |
| **Total** | **2** | **only after contract approval and canonical synchronization** |

The old reshape blocks are still byte-identical to canonical. The new `MODIFIED` blocks are
different by design and stay `PendingModification` until an independent contract verdict and Task
1.3 synchronize them. Structural OpenSpec validation is not semantic approval. The source-pinned
post-gate registry names both predecessor operations, both successor operations, the one proposed
stage, and the open synchronization task. All other duplicate owners remain prohibited.
