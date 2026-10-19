# Issue #1 task 2.1 working canonical provenance — 2026-10-16

Base: `f1eb86e31306d0aab88d8f84d38c925ef2eaf4c7`. Five ADDED requirements now match
the working canonical blocks. Independent review/checkpoint of this transition (task 2.2)
and all source work remain pending. Semantic eligibility here describes content synchronization,
not a new review verdict or implementation approval. Actual contract approval is bound in
`task-2-1-amendment-registry-2026-10-16.md` and the two source-pending fixture entries.

## Executable claim table

- record rows: 189
- record bytes: 49,879
- record SHA-256: `0f56a33d2aa17e6848ac8f3bf4769ec1a8604d0dadb25a7f9f9b8092a673f459`
- synchronized operations: 178
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 178 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Corpus inventories

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 24
- record bytes: 1,971
- SHA-256: `a50788bb2f903a5903ddee5a494a92421e34c347e3a9b67f8a4c935a009dc03a`

Canonical preambles are unchanged (14 / 1,233 B / `595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`).
Whole canonical content record: 14 / 1,635 B / `cfc99533be6893654dc128f6d6a1b2448fb3b689780b490de617a2147abf49a0`.
Only developer-facing-surface and management-and-querying gain new canonical blocks; the
original canonical preambles and all existing requirement blocks remain identical to base.
The standalone authoring companion, executable API/compile/package baselines and product
source remain at base; source task 4.2 owns their coordinated change.

Full sorted row record: `task-2-1-provenance-record-2026-10-16.tsv`. Recipe: parse each
active delta/canonical block, LF normalize, trim trailing blank block lines only, hash the
block without a terminal LF, apply the exact historical successor map, ordinal-sort tab
records and append one LF. The non-issue subset still reproduces 184 / 48,504 B /
`fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8`.

The permanent superseded catalog is 16 / 2,824 B / `08f0c3efdae341fd4ef85b1c3f038479eb7560d177a22b33aab51e8f527748b6`.
The previous current R1 artifact is appended at `833de7f663e22f4faae4c6be66f1f4ca83cb392ed13834a4824957dac2e90ccf`; all 16 predecessor files keep
their bytes. Current artifact hash, predecessor catalog and whole-canonical content record
are independently pinned by guard source; no assertion is removed or made optional.
