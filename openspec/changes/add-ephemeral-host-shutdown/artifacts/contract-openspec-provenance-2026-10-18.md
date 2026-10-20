# Issue #2 contract provenance — 2026-10-18

R1, 2026-10-19: normative Closed/drain versus Stopping remediation; no new approval.

Base: `fa88986aaac219c998b15e96748efdf2f245de51`. Six ADDED shutdown requirements
remain pending canonical review; no canonical block or source implementation is
changed. A green Infrastructure check validates this honestly pending state and
does not supply semantic approval. The 189-row predecessor subset reproduces
byte-for-byte: 49,879 B / `0f56a33d2aa17e6848ac8f3bf4769ec1a8604d0dadb25a7f9f9b8092a673f459`.

## Executable claim table

- record rows: 195
- record bytes: 51,141
- record SHA-256: `9547ee6c115df3bdf1fecba4e65b1c8c051d1f9e0360bc9e220831cb5b2c00d2`
- synchronized operations: 178
- pending canonical operations: 6
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 178 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 6 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Unresolved operations

| Capability | Pending operations | Sync owner |
|---|---:|---|
| `developer-facing-surface` | 2 | add-ephemeral-host-shutdown task 2.2 |
| `management-and-querying` | 4 | add-ephemeral-host-shutdown task 2.2 |

## Independently enumerated inventories

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 26
- record bytes: 2,124
- SHA-256: `4d9fbe5fbb56d75d8b78273983b9720ea72890d4948a51cfd86b90823d7d91df`

All 14 whole canonical files/preambles and all historical removal evidence remain
unchanged. Whole canonical content record: 14 / 1,635 B /
`cfc99533be6893654dc128f6d6a1b2448fb3b689780b490de617a2147abf49a0`. The active directory inventory adds only this
change's two declared capability directories.

Full sorted row record: `contract-provenance-record-2026-10-18.tsv`, without a header.
Recipe: the guard's LF-normalized requirement blocks, trim trailing empty block
lines, hash without terminal LF, apply the exact historical predecessor map,
ordinal-sort tab rows, and append one LF. The baseline subset and proposed route's
six individual block hashes must agree, not merely the aggregate row count.

## Permanent predecessor catalog

17 records / 2,992 B / `d08a9b95675da345ccb67eac92d3d67c8cd179fd39a1cc99417aa472cae5df65`.
The former current `openspec/changes/add-ephemeral-terminal-retention/artifacts/task-2-1-openspec-provenance-2026-10-16.md` is appended at normalized hash
`8b4e385a0a79816e583b54b3bee269d11f2b3c0b14bbc7493246ac7d7d43c0bb`. Every predecessor file is untouched; only current/cumulative
pins are refrozen. The current artifact hash is independently pinned in guard
source and fixture. This contract target contains the fixture, directory inventory,
pending table, current artifact, record and all guard pins together.

No approval fields, permanent amendment activations, compiled/API/compile/package
baselines, canonical specs, numbered documents, guides or product source change.
Task 2.2 remains open and owns the six pending additions. Source tasks remain open.
