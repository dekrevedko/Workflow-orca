# Addressed DAG wake proposal provenance

The proposal adds one pending provider requirement. The canonical outbox and terminal-wait requirements remain at the reviewed baseline. Task 1.1 owns synchronization after independent contract review.

- record rows: 244
- record bytes: 65,261
- record SHA-256: `2b749eeba27e6dfc4f9244562c89c634e47cb160ba4efb1b1c0c5b289bf93ba9`
- synchronized operations: 206
- pending canonical operations: 1
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 206 |
| `SupersededByApprovedSuccessor` | 34 |
| `SupersededByOwnerAuthorizedCandidate` | 0 |
| `PendingAddition` | 1 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

- canonical capability directories: 15
- record bytes: 597
- SHA-256: `bfbd9338f06536e6dfb44b07087bea92e44fbfe0aab5e046d189837fcc50f070`

- capability directories: 57
- record bytes: 4,560
- SHA-256: `1a552a3e8a29ca807733252ccf5e4fd94a73c1cc457a26539510d6ca701b8621`

| Pending capability | Operations | Turns green |
|---|---:|---|
| `durable-persistence-and-outbox` | 1 | retain-first-usable-dag-wake task 1.1 |
