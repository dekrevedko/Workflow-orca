# Runtime correctness proposed contract provenance

This is a proposed additive contract, with four unresolved canonical additions. Structural validation is not semantic approval. Prior provenance records remain unchanged.

- record rows: 223
- record bytes: 59,170
- record SHA-256: `846a9c20816b71bbeb3ea16ce5ddddfe4b162fc74c50f97829e6f58c095f4898`
- synchronized operations: 198
- pending canonical operations: 4
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 198 |
| `SupersededByApprovedSuccessor` | 18 |
| `PendingAddition` | 4 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 41
- record bytes: 3,330
- SHA-256: `8e51cdfcd607e1f0479f7c3adba7aa6543020b6da081028f590aa63fc741adbe`

| Capability | Pending | Owner |
|---|---:|---|
| `durable-persistence-and-outbox` | 1 | repair-runtime-correctness task 1.2 |
| `durable-runtime` | 1 | repair-runtime-correctness task 1.2 |
| `quality-and-verification` | 1 | repair-runtime-correctness task 1.2 |
| `workflow-contracts` | 1 | repair-runtime-correctness task 1.2 |
