# Task 3.2 Complete provenance

Date: 2026-11-16. The five facade rows are Complete under implementation/canonical approval b9dcdef7 / 598c6d4, integrated-remediation approval 1e960fc4 / 94e9aca5 and the explicit owner activation instruction. The owner-approved RV-4 blocks are unchanged; the reviewers' exclusion of independent contract review is retained. Source and merge evidence remain immutable. The ten predecessor operations now have approved successors rather than a pending owner-authorized candidate. No other Section 8 task is activated.

Rows use the compiled guard's LF-normalized blocks, ordinal ordering and a final LF. Semantic eligibility describes canonical state and does not invent approval.

- record rows: 224
- record bytes: 59,753
- record SHA-256: `647fc0551cd0b9aaf4d42a873e148902a37ecc0b9e0252acff33ffba7138a424`
- synchronized operations: 198
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 198 |
| `SupersededByApprovedSuccessor` | 23 |
| `SupersededByOwnerAuthorizedCandidate` | 0 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 40
- record bytes: 3,265
- SHA-256: `6dfe7b1ca4792b4fe42d9029f9202e4b3aa2dfc3c8f5a0bd3f68e0beab54db1e`

Whole canonical content remains 14 files / 1,635 record bytes / `a25ded5d8e1277de39c84e4d1ecbab07cb19341e84acd62bc07a58de22ce888a`. All 34 superseded provenance artifacts retain their exact bytes. The draft source/document pinning logic now checks approved commits, while current canonical/API and maintained document guards retain live-tree accountability.
