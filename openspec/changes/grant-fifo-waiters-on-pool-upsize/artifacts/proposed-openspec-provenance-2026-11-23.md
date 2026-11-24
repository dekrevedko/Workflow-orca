# FIFO pool upsize proposal provenance

The proposal adds one pending requirement to the durable runtime. The existing governance recovery and scoped grant specifications remain the canonical production contracts.

- record rows: 249
- record bytes: 66,440
- record SHA-256: `8568ab2a2395488ba55f785c0e209a1253d0f8a313ff9d379085798ee8fe8b98`
- synchronized operations: 205
- pending canonical operations: 1
- declared new-capability requirements outside the canonical set: 9
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 205 |
| `SupersededByApprovedSuccessor` | 34 |
| `SupersededByOwnerAuthorizedCandidate` | 0 |
| `PendingAddition` | 1 |
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

| Pending capability | Operations | Turns green |
|---|---:|---|
| `durable-runtime` | 1 | grant-fifo-waiters-on-pool-upsize task 1.1 |
