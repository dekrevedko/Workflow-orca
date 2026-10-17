# Issue #1 R1 contract provenance — 2026-10-16

Source remains pinned to `370b239402c8974a45fc0a54f539a823ab470cf4`. This is the current contract-target provenance prerequisite after F1–F4 remediation, not canonical synchronization or product/source approval. The five requirement identities are unchanged, but their blocks include the abort/start-to-wait corrections, so their new hashes and the aggregate differ from the rejected `b7172021…` record. Historical 184-row subset: 48,504 B / `fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8`, reproduced exactly.

Recipe: parse all active change requirements and canonical blocks as `OpenSpecCorpusGuards` does; normalize block line endings to LF, strip only trailing empty block lines, hash UTF-8 without a final block LF, classify exact superseded predecessors, ordinal-sort tab-delimited rows, and append one final LF. The full current record is `contract-r1-provenance-record-2026-10-16.tsv` beside this artifact. Canonical file contents, preambles and historical-removal catalogs are unchanged.

## Executable claim table

- record rows: 189
- record bytes: 49,579
- record SHA-256: `1abed32398a353509dfc7daab037156aaafc0067bd710a78fe5a4cb7820bb127`
- synchronized operations: 173
- pending canonical operations: 5
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 5 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Unresolved operations

| Capability | Pending operations | Sync owner |
|---|---:|---|
| `developer-facing-surface` | 1 | add-ephemeral-terminal-retention task 2.2 |
| `management-and-querying` | 4 | add-ephemeral-terminal-retention task 2.2 |

## Unchanged canonical inventory and current delta inventory

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 24
- record bytes: 1,971
- SHA-256: `a50788bb2f903a5903ddee5a494a92421e34c347e3a9b67f8a4c935a009dc03a`

Canonical preamble inventory: 14 / 1,233 B / `595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`. Whole canonical content record: 14 / 1,635 B / `cee7cd376777d5cc99c42e1de04cd692c7cc8270c85deb9a976ed94b7395baa0`. Their bytes and guard pins remain unchanged.

## Permanent superseded artifact catalog

15 records / 2653 B / `039a151898cfa09f44754020c35314ddb33d050ee9fc8919d4c495df31fb1a65`. Recipe: ordinal `path<TAB>LF-normalized artifact SHA-256` rows plus final LF. The former current `task-2-1-stream-identity-openspec-provenance-2026-10-07.md` is appended at exact normalized hash `f2739090a7644767e8de4cb3584d501f84a509b00fa9b2baf5007efc7bbf0333`; all 15 predecessors stay byte-exact. The current artifact hash is pinned independently in guard source and the fixture.

## Gate interpretation

The must-be-green Infrastructure guard must pass on the complete contract target by checking this honestly pending state. `semanticApprovalEligible` stays false. No approval, canonical change, numbered-document edit, executable API/compile/package baseline change, source implementation, commit, push, issue comment/closure or issue #2 work follows from a structural pass. The rejecting verdict authorizes remediation and another independent review only.
