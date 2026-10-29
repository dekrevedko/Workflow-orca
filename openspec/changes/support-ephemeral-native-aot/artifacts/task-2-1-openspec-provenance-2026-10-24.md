# Issue #13 canonical-transition provenance — 2026-10-24

Contract 6122025 / verdict-only child 10aec46 are Git-bound. Seven reviewed R1
delta blocks append to four canonical specs without altering a prior line.
Source approval/native qualification remain unset. The semantic-eligibility flag
below measures canonical provenance completeness, not source or product approval.

- record rows: 206
- record bytes: 54,532
- record SHA-256: `97aacc1e9529861bec52ff1ae5572983046c3829267d619e255afaa80a7f8413`
- synchronized operations: 195
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 195 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 31
- record bytes: 2,509
- SHA-256: `5f49aa48d8ced1404d25d039ba71de5562e54419f587cd220b9ebecadc108add`

All 199 master baseline rows retain their content; the seven issue #13 rows now
have exact delta/canonical hashes. All 23 predecessor artifacts remain raw-byte
identical to committed originals, including the rejected and R1 contract records.
Catalog: 3,973 bytes / `3d29203f3126de79cadede604c3cdb1471ad6036327d518e85a9b7586c9f00a4`.
Whole canonical content: 14 files / 1,635 record bytes /
`6119b5af026bde03770398e70d278bf2f1cb9f341dea6e0ed68c20c18ac46d45`. Only the seven approved blocks are added; canonical
preambles, removal records and capability-directory inventories are unchanged.
Record ordering and normalization match the executable guard.
