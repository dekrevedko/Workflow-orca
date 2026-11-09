# Issue #16 contract provenance — 2026-10-29

Base 0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580. All 206 master rows are preserved;
nine modifications and three additions remain pending canonical review/sync.
No canonical, source or semantic approval is asserted. Guard changes pin this current artifact and append the previous current artifact
to the permanent predecessor catalogue. A separate exact nine-row proposed-owner
registration validates the named old/new owners and all block hashes, with approval
fields null and canonical/source tasks open; it does not grant semantic approval. All 27 predecessor files retain exact base bytes.

- record rows: 218
- record bytes: 57,773
- record SHA-256: `881aa5392e6deee881abde5620fcd50e05ebab38222008f79ff4b86e1a3cee58`
- synchronized operations: 195
- pending canonical operations: 12
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 195 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 3 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 9 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

| Capability | Pending | Turns green |
|---|---:|---|
| `developer-facing-surface` | 2 | expose-ephemeral-failures-and-settlement task 2.1 |
| `management-and-querying` | 6 | expose-ephemeral-failures-and-settlement task 2.1 |
| `quality-and-verification` | 1 | expose-ephemeral-failures-and-settlement task 2.1 |
| `structured-fiber-execution` | 1 | expose-ephemeral-failures-and-settlement task 2.1 |
| `workflow-contracts` | 2 | expose-ephemeral-failures-and-settlement task 2.1 |

## Independently pinned inventories

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 36
- record bytes: 2,954
- SHA-256: `b03677cb7dd3dce316f7e36963e74170000f3989a292e5361296353adcdcb632`

Predecessor catalogue: 27 rows / 4,641 bytes /
`84c6fcef23746282e628a10fe964f58826e9855170ac40610cc6359ab4ebb0e5`. Previous current artifact:
`openspec/changes/support-ephemeral-native-aot/artifacts/task-2-4-boundary-openspec-provenance-2026-10-27.md` at `a6c69937b197e814462d918f42735f02757c08579715d9619a80f3f9dd985ace`.
Canonical directory/content/preamble hashes, historical removals and all approval
registries retain their base values. The TSV is ordinal-sorted UTF-8 LF with one
final LF and the guard's normalized requirement blocks. Green structural checks
validate this pending state and do not authorize canonical or source changes.
