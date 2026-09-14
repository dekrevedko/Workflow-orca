# Task 6.6 OpenSpec provenance refresh — 2026-09-13

The dated Task 4.2 provenance artifact remains immutable evidence of its original checkpoint.
Task 6.6 changed two canonical requirement bodies together with their active owning deltas to add
the exact future-capability registry path and section name. This record publishes the resulting
current hashes without rewriting the 2026-08-18 artifact.

## Requirement-operation record

- record rows: 176
- record bytes: 46,211
- record SHA-256: `ea8719e768182f4eea097cb280d3487426a9a7450ca7becb72616e567e30b756`
- synchronized operations: 173
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Canonical capability inventory

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`

## Active delta capability inventory

- capability directories: 16
- record bytes: 1,321
- SHA-256: `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`

The record remains semantically eligible because both edited canonical requirements are
byte-identical to their active reshape-owned delta blocks and no pending operation was introduced.
