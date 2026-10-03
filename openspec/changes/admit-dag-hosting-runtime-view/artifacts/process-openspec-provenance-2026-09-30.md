# Proposed DAG hosting runtime-view successor provenance

Date: 2026-09-30

This is the Task 0.1 process-only review target for `admit-dag-hosting-runtime-view`.
The separately approved authoring-friend closeout is committed and activated. Only two exact
headings gain a proposed third owner; no canonical requirement, product source, friend
metadata, public API, codec access, or Section 8 runtime behavior changes.

## Completed predecessor and proposed chain

- Authoring source checkpoint: `a9f835f939d683500ca231c7ba491ab8eae2aaae`.
- Authoring source evidence: `055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8`.
- Closeout checkpoint: `b5fb28e65dbf3fea102ddec1d5fe1cf9d794c659`, approved tree `2ae62652c4ab763c539b3c93c2be3bb26398ab26`.
- Direct-child closeout evidence: `dbc3086da206bde20cc8624816e3f45f1d13f9b8`.
- Checkbox-only closeout activation and this target's base: `6d49716384b50e2cbaf503fcd782b63c6384bc1e`.
- Closeout APPROVE: `docs/review/developer-facing-interface-section-08-task-8-2-authoring-friend-closeout-independent-review-verdict-2026-09-29.md`, 7,888 bytes, SHA-256 `221c60c34f1db12c747eb2da20a7107bbe40baba227a9b8e0c223db1e913e531`.

For each heading the ordered chain is
`reshape-developer-facing-interfaces -> admit-dag-authoring-friend-boundary -> admit-dag-hosting-runtime-view`.
The original Proposed and ApprovedPending rows, Complete implementation evidence and both
historical delta blocks are preserved. Canonical remains byte-equal to the completed authoring
successor; the newest full MODIFIED block is not synchronized. No fourth owner is admitted.

| Capability | Requirement | Completed predecessor / unchanged canonical SHA-256 | Proposed runtime-view block SHA-256 |
|---|---|---|---|
| `developer-facing-surface` | Implementation package boundaries use exact internal friends | `bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793` | `5561f46b69ff482a78b81deccf969b0cc0e0e726bd3708b3280549069bfec78d` |
| `repository-foundation` | Dependency direction remains one-way | `bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9` | `feab5ce4444e9738b7d1913336734439427ba1ceab46ca60bfe27b6859d0b26f` |

## Reproduction

Enumerate every non-archived `openspec/changes/*/specs/*/spec.md` requirement.
Normalize blocks to LF, strip trailing empty lines, and stop at the next requirement or
`## ` heading. Only the two exact reshape predecessors classify as
`SupersededByApprovedSuccessor`, through their pinned historical hashes and completed
authoring successor. The proposed runtime-view blocks classify normally as
`PendingModification`.

Render `change<TAB>capability<TAB>capability-kind<TAB>operation<TAB>requirement<TAB>state<TAB>delta-block-sha256<TAB>canonical-block-sha256-or-dash`,
sort the rendered lines ordinally, join with LF and one final LF, then hash UTF-8 without a BOM.

- record rows: 180
- record bytes: 47,327
- record SHA-256: `77d388fa3a509d7eafdb871b8e87c3eab4a4e2c6cd407ee3dd9d707deed70a77`
- synchronized operations: 173
- pending canonical operations: 2
- declared new-capability requirements outside the canonical set: 3
- duplicate `(capability, requirement)` owners: 2 exact three-owner chains; 0 unregistered chains
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 2 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 2 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

The independent capture first reproduced the prior 178-row record at 46,784 bytes and
`0dd47120d11d2442fccc217902386dd36d4bbf57b7fb476bd7c670dbbbaa5257`.
The previous human record remains byte-unchanged in the permanent superseded catalog,
which now holds four records and hashes to `566eda77e13dd20d505e7ab3f75a8d99b7e7c40231c442885cdd9d0ae60e5855`.

## Canonical-capability inventory

Render repository-relative `openspec/specs/*/` directories with forward slashes and one trailing
slash, sort ordinally, LF-join with one final LF.

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`

Canonical preambles remain 14 / 1,233 bytes / `595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.
All canonical files are unchanged.

## Capability-directory inventory

Render every active `openspec/changes/*/specs/*/` directory as a repository-relative
forward-slash path with one trailing slash, ordinal-sort, LF-join with one final LF.

- capability directories: 20
- record bytes: 1,645
- SHA-256: `dfabdc307ab5c49d6bc15c140b7778a5dce0512faf601c83645574fa0e770d62`
- directories lacking `spec.md`: 0

## Pending operations and ownership

| Canonical capability | Pending | Turns green |
|---|---:|---|
| `developer-facing-surface` | 1 | admit-dag-hosting-runtime-view task 1.3 |
| `repository-foundation` | 1 | admit-dag-hosting-runtime-view task 1.3 |

Task 1.3 must land atomically with 1.4 after the separate friend contract approval.
All contract, sync, source and permanent-closeout tasks stay open in this process target.

## Carried review note and scope

The Task 8.0 map's 8.4 row now names the independently approved Task 8.2 checkpoint and
checkpointed Task 3.2 closeout; it no longer claims authoring implementation review is pending.
It explicitly distinguishes the separate, still-proposed Dag-to-Dag.Hosting runtime view.
The owning source pin is refreshed, while its existing sole DAG-to-durable bridge statement
and all other map rows are unchanged.

The proposal carries the requested codec re-sequencing into contract Task 1.1; the existing
reshape task ledger is not silently changed here. No authoring codec member or test friend
is proposed. The internal evaluator's behavior will be tested at the Hosting boundary.
