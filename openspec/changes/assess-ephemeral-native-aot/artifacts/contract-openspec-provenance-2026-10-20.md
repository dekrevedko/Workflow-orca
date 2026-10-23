# Issue #3 pending contract provenance — 2026-10-20

Base 7895262a21f19741c0b5b7fbad30907bccff2cbb. Four new quality-and-verification blocks are PendingAddition;
canonical text, source, baselines and earlier approval records remain unchanged.
Semantic eligibility stays false. Structural validation is not independent approval.

- record rows: 199
- record bytes: 52,401
- record SHA-256: `dd0b1db45dfa689b3df6440bfccee5c4c87802fe4f82b780b52c3616a7c7dc51`
- synchronized operations: 184
- pending canonical operations: 4
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 184 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 4 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

| Capability | Pending | Turns green |
|---|---:|---|
| `quality-and-verification` | 4 | assess-ephemeral-native-aot task 2.1 |

## Independently enumerated inventories

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 27
- record bytes: 2,201
- SHA-256: `b959238b7967bcb8e1d173a8b335a1e4c1d36baa7f39206ec8c99cc2c37026b9`

The 195 base rows remain byte-exact as a subset (51,501 bytes / 0699286d…9962).
All canonical preambles/content/removal records remain exact. The current artifact
and the active-directory inventory are independently pinned, not merely a row count.

## Immutable predecessors

19 records / 3,318 bytes / `6e769382b7e2ddbcb25e16e8a502592a63e9dfdafadd9e1c7c7de161d096dff9`.
The former current `openspec/changes/add-ephemeral-host-shutdown/artifacts/task-2-1-openspec-provenance-2026-10-19.md` is appended at `e7b975df3e0e42ba568c0ac4f1356def496701a1fd53e5a5808180eade642578`. All nineteen files
are raw-byte identical to the base. No previous catalog entry or guard is relaxed.
The record and predecessor TSVs use ordinal LF rows plus final LF. No new source
approval, canonical synchronization, product support or package publication occurs.
