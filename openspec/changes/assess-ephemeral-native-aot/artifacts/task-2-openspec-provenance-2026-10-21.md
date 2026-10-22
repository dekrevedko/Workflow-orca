# Issue #3 task 2 canonical/process provenance — 2026-10-21

Contract eea51c9 / sole verdict child dbd829e. Verdict F1 is incorporated into the
four delta blocks and their append-only canonical copies. Existing canonical lines,
195 predecessor rows, all 20 predecessor artifacts and earlier registries are exact.
Product API/source/dependencies/baselines are unchanged. Assessment approval remains
unset in task-2-approval-binding-2026-10-21.json; canonical eligibility is not approval.

- record rows: 199
- record bytes: 52,641
- record SHA-256: `0a30212be9d098e37e6105111d0b2479a8ae6756c72158da022a3a3a699c82ba`
- synchronized operations: 188
- superseded approved operations: 8
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes (no pending canonical operations)
- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 27
- record bytes: 2,201
- SHA-256: `b959238b7967bcb8e1d173a8b335a1e4c1d36baa7f39206ec8c99cc2c37026b9`
- predecessor catalogue: 20 / 3481 bytes / `eda81ed6ab0eb369eefba2872ff7687030c7f1f47f460d9cca5b81026a471484`

| State | Count |
|---|---:|
| `Synchronized` | 188 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

The fixed current-artifact and predecessor pins are updated together. The former
current contract artifact is an immutable predecessor, never rewritten. Native
assessment completeness is checked separately by its report gate; a missing native
platform or an unclassified diagnostic cannot be green assessment evidence.
