# Proposed ephemeral shutdown successor provenance

The process checkpoint admits one pending requirement under the two approved
shutdown and issue 16 owners. Current canonical and numbered text remain
byte-for-byte identical to the base. The successor registration supplies no
contract approval.

- record rows: 219
- record bytes: 58,320
- record SHA-256: `acd8999cb0b53db3c832feb0812c42474f4733fc1f3288b5cd5e6f22c18f1c5e`
- synchronized operations: 198
- pending canonical operations: 1
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 198 |
| `SupersededByApprovedSuccessor` | 17 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 1 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

| Capability | Pending | Synchronization task |
|---|---:|---|
| `management-and-querying` | 1 | clarify-ephemeral-start-shutdown task 1.2 |

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 37
- record bytes: 3,035
- SHA-256: `33d3b2dc68ff3c7507aa6612b8d1be3cd205593e072ed057ce781ed714a78c47`

The current block is pinned at `496fe273722960b7365d8ad578ccbd00e11b52ebf442784238f072b20a59b03d`, and the candidate
block is pinned at `af76ed399e86348c0710be62a0bb71222f591e8caed5f0b23a8d7753939fba22`. The issue 16 provenance
artifact remains in the permanent predecessor catalogue.
