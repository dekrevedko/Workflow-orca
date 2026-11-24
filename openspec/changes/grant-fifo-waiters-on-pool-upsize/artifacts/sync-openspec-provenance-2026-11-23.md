# FIFO pool upsize canonical provenance

Task 1.1 synchronizes the FIFO resize requirement and both scenarios from merge commit `85879e23` into the [durable runtime specification](../../../specs/durable-runtime/spec.md). Only the FIFO row changes from pending to synchronized.

- record rows: 249
- record bytes: 66,500
- record SHA-256: `060315346c63509e4bf7b6ddfe55513a721199db74b8e5d9ba2a4161011433ff`
- synchronized operations: 206
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 9
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 206 |
| `SupersededByApprovedSuccessor` | 34 |
| `SupersededByOwnerAuthorizedCandidate` | 0 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 9 |

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 57
- record bytes: 4,575
- SHA-256: `d91e85497acfd7e1f3c4e2be03baf37381da586542b43d27f26e9d7b8cb07ef0`

The active change remains available for review. The separate [wait-ownership task 2.5](../../verify-ephemeral-wait-ownership-with-tla/tasks.md) remains open.
