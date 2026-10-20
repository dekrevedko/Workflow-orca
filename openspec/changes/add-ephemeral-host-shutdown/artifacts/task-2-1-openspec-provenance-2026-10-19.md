# Issue #2 canonical-transition provenance — 2026-10-19

Base 4bd7c3e; contract 3c8d33c / verdict 4bd7c3e. F2 correction is included in
canonical review. Six ADDED blocks are exact canonical copies; no earlier block
or preamble is rewritten. Provenance eligibility is structural synchronization,
not canonical/source/semantic approval by an independent reviewer. Both new
amendment records remain ContractApprovedCanonicalReviewPending; source is null.

- record rows: 195
- record bytes: 51,501
- record SHA-256: `0699286d812312ee349c6c631c863eab4b7464e6ed9a10b7282d775a509a9962`
- synchronized operations: 184
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 184 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Independently enumerated inventories

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 26
- record bytes: 2,124
- SHA-256: `4d9fbe5fbb56d75d8b78273983b9720ea72890d4948a51cfd86b90823d7d91df`

All canonical preambles, 12 unowned whole canonical files, historical removal
evidence and the original 189-row subset remain exact. Only the two append-only
canonical files update the whole-content record: 14 /
1,635 B / `fea34ba8470c71fb94152b7589b69727871fcce632b14762f74403429bad434b`.
Full sorted requirement record: task-2-1-provenance-record-2026-10-19.tsv. Recipe is
the unchanged guard parser and LF-normalized blocks, ordinal tab rows plus final LF.

## Immutable predecessors

18 predecessor records / 3,155 B / `81cf9d8c6cfa653aef653a1185d598bb93ec1b750ba76fddea1d1a2120094028`.
The former current `openspec/changes/add-ephemeral-host-shutdown/artifacts/contract-openspec-provenance-2026-10-18.md` is appended at `87f527a9a3cc39c6519fe75f860908778f705009fe23381e005104804f16c5e0`. All 18 predecessor files
are raw-byte identical to the base commit, including the approved pending contract
record. No prior catalog entry is removed or relaxed. The current normalized hash
is independently pinned in guard source and fixture; directory and removal pins
remain unchanged. The new post-gate schema is 17, preserving all previous records.

Tasks 2.1/2.2 stay open for exact independent review and activation; 3.1–3.5 remain
open with no source approval. API/compile/package baselines and product source are
unchanged. Markdown process passes are not behavioral or package-consumer evidence.
