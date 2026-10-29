# Issue #13 R1 contract provenance — 2026-10-23

Base 3b7f98aa0748a4d9c3f54d4604e566ed61f21bad. Seven additions are pending canonical
review/synchronization; no source or semantic approval is claimed. All 199 base
rows and all 22 predecessor files are unchanged. Guard changes only pin the new
current artifact and append the former master current artifact and rejected contract current artifact to the permanent catalog.

- record rows: 206
- record bytes: 54,112
- record SHA-256: `72138cf1e243aa337a81466cd47400cf2c5cd7efe32536d8c6e0d931b394ee5b`
- synchronized operations: 188
- pending canonical operations: 7
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 188 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 7 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

| Capability | Pending | Turns green |
|---|---:|---|
| `developer-facing-surface` | 2 | support-ephemeral-native-aot task 2.1 |
| `quality-and-verification` | 2 | support-ephemeral-native-aot task 2.1 |
| `structured-fiber-execution` | 1 | support-ephemeral-native-aot task 2.1 |
| `workflow-contracts` | 2 | support-ephemeral-native-aot task 2.1 |

## Independently pinned inventories

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 31
- record bytes: 2,509
- SHA-256: `5f49aa48d8ced1404d25d039ba71de5562e54419f587cd220b9ebecadc108add`

Predecessor catalog: 22 rows / 3,806 bytes /
`ec4da5dfa4ecfc9c62c5e4a2063de75c4abd016d03e33fe659e1abe6a58f3e23`. The previous current artifact is
`openspec/changes/assess-ephemeral-native-aot/artifacts/task-2-openspec-provenance-2026-10-21.md` at
`64b80c3defb2c055376dc3c03c02819e835a2b633e94f2b2006731e26858d12c`. All earlier rows and canonical inventories,
content/preambles, historical removals and approval registries retain base values.
The record TSV is ordinal-sorted UTF-8 LF with one final LF; requirement blocks
use the guard's line-based trailing-empty-line normalization. Structural green
validation is not independent approval or a source-support claim.
