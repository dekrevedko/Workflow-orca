# Issue #16 task 2.1 canonical-prepared provenance — 2026-11-01

Contract checkpoint 0cae922a1d68f4c55f9bfc19b50060bfcdbaaa83, approved tree cf054886e39c20d9294b3baac55e58fa19c6bbdf,
verdict-only child 7697c7981c02d03408cbccf9a2e0b9bade33ca14. All 218 requirement identities remain; the nine
retained predecessors are now explicitly superseded by the contract-approved
successors. The twelve current delta blocks are byte-equal to their canonical blocks.
This prepares canonical text for independent review, not canonical/source approval.
Zero pending text operations makes the structural semantic-approval gate eligible;
the new registry still requires canonical review and keeps source authorization false.
All prior global approval records and all 28 predecessor artifact bytes are unchanged.

- record rows: 218
- record bytes: 58,043
- record SHA-256: `f6e5ed0ad28a597fbbea17a62a10c9cfd6e05f580e0051f62f51d558d7a24783`
- synchronized operations: 198
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 198 |
| `SupersededByApprovedSuccessor` | 17 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Independently pinned inventories

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 36
- record bytes: 2,954
- SHA-256: `b03677cb7dd3dce316f7e36963e74170000f3989a292e5361296353adcdcb632`

The predecessor catalogue is 28 rows / 4,817 bytes /
`8c936afa9d013abda87bb1c43b692520ef8748ff8bd5f604f0d507e77dbc72c5`. The previous current record `openspec/changes/expose-ephemeral-failures-and-settlement/artifacts/contract-openspec-provenance-2026-10-29.md` remains pinned
at `9e654e3efc789b0b08cd6a00a7914cf12cfb487ab89381fa1149a302b443f28f`. Canonical preambles, directory inventory, removals,
the global post-gate registry and prior verdicts remain unchanged. Source/runtime
API/compile/package baselines remain unchanged. Tasks 2.1/2.2 and 3.1–3.6 stay open.
