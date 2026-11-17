# Task 0.1 execution plan: process-only successor gate (2026-11-15)

Status: **plan only**. Nothing in this document has been executed. No guard source, fixture, registry row, canonical spec or product source was changed to produce it. It was written after RV-4 was approved for the pre-gate design review (projected tree `7aad8b4cff4e323ed7f17fd256288ad2c311903d`) and is the only file added to the change since that approval.

Its purpose is to make Task 0.1 mechanical: every value below that could be computed without changing code has been computed against the parent commit `da1a4729ea2cdb9474eb219eb507760574f83dcb` and the RV-4 deltas.

## 1. Template

The last gate of this kind is commit `b55056c0d9136f509296167ae5984881e7b5de97` ("Checkpoint DAG run successor process gate"). It changed exactly: the change directory; `Fixtures/post-gate-amendment-path.json`; `Fixtures/openspec-provenance-checkpoint.json`; `Fixtures/immutable-document-history.json`; `Fixtures/review-manifest-provenance.json`; `OpenSpecCorpusGuards.cs`; and the review request and manifest. Task 0.1 should change the same set and nothing else.

## 2. The five proposed rows

Hash recipe, as implemented by `OpenSpecCorpusGuards.ReadRequirementBlocks` and `Sha256(string)`: the block is the lines from the `### Requirement:` heading through the last non-empty line before the next requirement or `## ` heading, joined with LF, no trailing newline; lowercase hex SHA-256 of its UTF-8 bytes. The recipe was checked by reproducing two hashes already in the registry (`3a848931…` and `f0dbc156…`, the canonical friend blocks).

For every heading the immediate predecessor's block is byte-identical to the current canonical block, so each chain is clean.

| # | Capability | Requirement | Owner chain (oldest first) | Immediate predecessor / operation | Predecessor = canonical block SHA-256 | Proposed block SHA-256 |
|---|---|---|---|---|---|---|
| 1 | developer-facing-surface | Implementation package boundaries use exact internal friends | reshape (ADDED) -> admit-dag-authoring-friend-boundary (MODIFIED) -> admit-dag-hosting-runtime-view (MODIFIED) -> this | admit-dag-hosting-runtime-view / MODIFIED | `3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab` | `a55739a1ab663d7d5f3671d7c0bf1360273f708be9871c8d001fe9a861de02f0` |
| 2 | repository-foundation | Dependency direction remains one-way | reshape (MODIFIED) -> admit-dag-authoring-friend-boundary (MODIFIED) -> admit-dag-hosting-runtime-view (MODIFIED) -> this | admit-dag-hosting-runtime-view / MODIFIED | `f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8` | `96d1b6248ced6c4e5682d980b108abdca86de0d82961310ec06a63f9d4b89892` |
| 3 | developer-facing-surface | Completion waits are notification-driven and race-free | reshape (ADDED) -> expose-ephemeral-failures-and-settlement (MODIFIED) -> this | expose-ephemeral-failures-and-settlement / MODIFIED | `75d08ccdd5fec3e6eff08fed12972548212774fcce684af3f91b9de45a6833c1` | `14f49ff22a1fb51f913ce431dadb15d9f77557fdb30cfdce5dfa4c9d6791b07a` |
| 4 | management-and-querying | DAG terminal waiting is notification-driven | reshape (ADDED) -> this | reshape-developer-facing-interfaces / ADDED | `0e00b3bc7ae11980a67c0f5878577473c8e237e66c9769c2ad9b7425b547924e` | `02fb7fe339c68c23604c07aeede15d7e6ba1791425bc2d138abae582525c642e` |
| 5 | developer-facing-surface | Closed registration and start results retain a cast-free success path | reshape (ADDED) -> this | reshape-developer-facing-interfaces / ADDED | `5afa82ae478b50c595c572d9fb99157e4e76105606847d35ad402b09f3d3441e` | `d14accd6a3fdfe38f6f6bfdffa60783aeb1e7ef9492850a7cc5340676295ffce` |

Every row: `change = admit-dag-public-handle-seam`, `successorOperation = MODIFIED`, `stage = Proposed`, `turnsGreenTask = 1.3`.

Other predecessor blocks, for the supersession history that Task 1.4 will need: friends reshape `e12c77e5d3a8eaf30dbe31a68ffe4baec3b105023dd5f1512d3f062917a312d0`, authoring `bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793`; dependency-direction reshape `d0d512ea59ea8da595770b5437d7faa7565773c6cd8845850d2dea6010b54b29`, authoring `bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9`; completion-waits reshape `df231a9e8e8794cbc8da86c63a58fdfdf2b7c598745c84e8beedcdbc5d035914`.

If any delta text changes before Task 0.1 runs, recompute column seven; the other columns depend only on the parent commit.

## 3. Registry fixture (`post-gate-amendment-path.json`)

- Raise `schemaVersion` from 24 to 25.
- Add one array, following the `proposedDagRunSuccessors` shape from the template commit, suggested name `proposedDagFacadeSeamSuccessors`, with the five rows above and the fields `capability`, `requirement`, `change`, `predecessorChange`, `predecessorOperation`, `successorOperation`, `stage`, `turnsGreenTask`, `predecessorBlockSha256`, `proposedBlockSha256`.
- Leave every existing row, stage and evidence record unchanged.

## 4. Provenance checkpoint fixture (`openspec-provenance-checkpoint.json`)

Current values at the parent: schema 6, 218 records, 58,043 bytes, `f6e5ed0a…`, semantic approval eligible, no pending operations, 36 capability directories.

| Field | Expected after Task 0.1 | How it is obtained |
|---|---|---|
| `semanticApprovalEligible` | `false` | five unresolved change-to-canonical operations |
| `pendingCanonicalOperations` | developer-facing-surface 3, management-and-querying 1, repository-foundation 1; each `turnsGreenChange = admit-dag-public-handle-seam`, `turnsGreenTask = 1.3` | from the rows above |
| `recordCount` | 223 if the record has one row per delta requirement, as the template's 180 -> 184 for four rows suggests | confirm from the guard output |
| `recordBytes`, `recordSha256` | not precomputed | take from the guard's computed record |
| `capabilityDirectoryCount` | 39 (this change adds three capability directories) | confirm from the guard output |
| `capabilityDirectoryBytes`, `capabilityDirectorySha256` | not precomputed | take from the guard's computed record |
| `artifactPath`, `artifactNormalizedSha256` | the new process provenance artifact (section 6) | hash after writing it |

Report these values in the Task 0.1 review request as CLAUDE.md requires, and report structural validation only as structural validation.

## 5. Guard source (`OpenSpecCorpusGuards.cs`)

Three edits, all stage-aware. None of them may weaken a check for any heading or change other than the ones named.

1. **Owner-chain check.** `ValidateActivePostGateOwners` hard-codes `[reshape, admit-dag-authoring-friend-boundary, RuntimeViewChange]`. For the two friend headings it must accept exactly that chain plus `admit-dag-public-handle-seam` while the new rows are `Proposed`, whether predecessors are active or dated-archived, and reject every other owner. It is called from the runtime-view successor loop; the runtime-view rows themselves must keep validating unchanged (their block is still the synchronized canonical block at this stage).
2. **Settlement validator.** The expose-ephemeral-failures-and-settlement successor loop asserts `owners.Should().HaveCount(2)` for each of its nine headings and then requires its own successor block to be the synchronized canonical block. For "Completion waits are notification-driven and race-free" only, at stage `Proposed`, it must accept this change as the one named third owner and still require the settlement block to be canonical. Its other eight headings stay exactly two-owner. At Task 1.4 this check moves to its ApprovedPending form, where the settlement completion-waits block is an exact hash-bound superseded predecessor.
3. **New proposed-row validation.** A loop over the five new rows, modelled on the proposed DAG-run successors validation added by the template commit: stage is `Proposed`; the predecessor resolves to exactly one active or dated-archived change with the recorded operation and block hash; the proposed block hash matches the delta; the canonical block still equals the predecessor block; the heading's active owners are exactly its chain; `tasks.md` Task 0.2 is deliberately not pinned open so checkbox-only activation stays green, and Task 1.3 is pinned open.

The headings 4 and 5 have a single predecessor (reshape) and no existing post-gate validator; only the new loop and the generic duplicate-owner check in `Task53_ReshapeSolelyOwnsFriendTopologyAndDurableGovernanceAggregate` and `CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners` apply to them. Find where those two tests reject a second owner and admit exactly these five rows through the registry, not through a wildcard.

## 6. Other files

- `artifacts/process-openspec-provenance-<date>.md` in this change: the measured provenance record, pending-operation table and recipe, as the template's `process-openspec-provenance-2026-10-04.md` did.
- `Fixtures/immutable-document-history.json` and `Fixtures/review-manifest-provenance.json`: catalogue the new review request, manifest and artifacts, and the four earlier draft packets if the catalogue requires every file under `docs/review/`.
- `tasks.md`: add a dated "prepared for process review" note under Task 0.1. Do not check 0.1 or 0.2.
- Review request and self-inclusive manifest for the Task 0.1 target.

## 7. Exit tests and probes

Green: `CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners`; `Task53_ReshapeSolelyOwnsFriendTopologyAndDurableGovernanceAggregate`; every existing runtime-view, authoring-friend and settlement guard; the complete must-be-green lane with `Disposition=Infrastructure`; strict OpenSpec validation (reported as structural).

Red when probed, each restored afterwards:

| Probe | Expected failure |
|---|---|
| add an unnamed further owner to a friend heading | owner-chain check |
| add an unnamed further owner to the completion-waits heading | settlement validator |
| add a second owner to one of the settlement validator's other eight headings | settlement validator, unchanged behaviour |
| change one byte of a proposed delta block | proposed block hash |
| change one byte of a predecessor block | predecessor block hash |
| set a new row's stage to `ApprovedPending` without canonical sync | stage check |
| check Task 1.3 in `tasks.md` | open-task pin |
| remove one of the five rows while its delta stays | duplicate-owner check |

## 8. What this plan does not settle

- The exact member names and line positions in `OpenSpecCorpusGuards.cs`; they were read, not edited, and the file is about seven thousand lines.
- Whether `recordCount` and `capabilityDirectoryCount` move by exactly five and three; both are inferences from one earlier gate.
- Whether the review catalogue fixtures must list the four earlier draft packets.
- The eleven `3.11d` guard cases need `OrcaCore.ProviderCertification` built before the Guards run, as the first review established.
