# Wait-ownership pilot canonical synchronization and archive

The [canonical capability](../../../../specs/wait-ownership-model-verification/spec.md) contains all six requirements and fifteen scenarios from the [reviewed delta](../specs/wait-ownership-model-verification/spec.md). The requirement text is identical to the delta at master commit `5c7594b0`. Removing the archived proposal from the active inventory retires exactly six rows. The other 243 active rows retain their previous values.

The original pilot exposed WOP-F001. The six original joint searches remain inconclusive. At the approved bounds, the twelve complementary target searches passed after the runtime fix, including both progress checks. [Qualification record](../../../../../verification/tla/wait-ownership/full-bound-results.md) · [Hosted evidence](https://github.com/dekrevedko/Workflow-orca/actions/runs/45326482610)

- record rows: 243
- record bytes: 65,048
- record SHA-256: `84625e3e0b80802c992b2ce60efe3c2ffb4720f2b8016b21d72db5626e82a5f6`
- synchronized operations: 206
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
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
| `NewCapabilityOutsideCanonical` | 3 |

- canonical capability directories: 15
- record bytes: 597
- SHA-256: `bfbd9338f06536e6dfb44b07087bea92e44fbfe0aab5e046d189837fcc50f070`

- capability directories: 56
- record bytes: 4,476
- SHA-256: `d62e1d0e7373df4085a4f156b59b46d6fe3d2400f2fc53e0b24151ccf27043ae`

The [previous FIFO checkpoint](https://github.com/dekrevedko/Workflow-orca/blob/3cead4b4019edece46a67d6281b690a40a5b000e/openspec/changes/grant-fifo-waiters-on-pool-upsize/artifacts/sync-openspec-provenance-2026-11-23.md) has SHA-256 `f52f7127112bf87b038698e6906dbb9b2c83cd49b3cb62f8cba46d4d9322b044`. Its current copy changes only the task-ledger link to a permalink at that checkpoint.
