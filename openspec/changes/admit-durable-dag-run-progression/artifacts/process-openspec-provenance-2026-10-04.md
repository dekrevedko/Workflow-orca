# Proposed durable DAG run progression: process provenance

Date: 2026-10-04

This process-only target is based on child-protocol evidence commit
`095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`. It proposes four exact
requirement successors; it does not approve the run contract, canonical sync,
public/advanced APIs, provider schemas or source. Reshape 8.4/8.5 remain open.

## Requirement record recipe

Enumerate every active non-archived delta requirement. Normalize CRLF/CR to LF;
start at `### Requirement: `, stop at the next such heading or `## `, and
strip trailing empty lines. Hash the full resulting block without a final LF.
Read the exact proposal's New/Modified capability classification and operation.
Only the four already-reviewed runtime-view supersession records, bound to both
old and current block hashes, classify as SupersededByApprovedSuccessor. Everything
else is compared with the current canonical block. Render
`change<TAB>capability<TAB>capability-kind<TAB>operation<TAB>requirement<TAB>state<TAB>delta-block-sha256<TAB>canonical-block-sha256-or-dash`,
ordinal-sort complete rows, LF-join with one final LF, encode UTF-8 without BOM,
and SHA-256 hash. The source-pinned process exception changes ownership permission,
not provenance classification: all four new blocks remain PendingModification.

- record rows: 184
- record bytes: 48,464
- record SHA-256: `d76b82216c3504f40788299162961b8dbdef1da89268bf6eaafe7092532418d5`
- synchronized operations: 173
- pending canonical operations: 4
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no
- duplicate owners: 2 previously reviewed three-owner chains and 4 proposed two-owner chains; 0 unregistered chains

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 4 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 4 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Pending sync ownership

| Capability | Count | Turns green |
|---|---:|---|
| `durable-persistence-and-outbox` | 3 | admit-durable-dag-run-progression task 1.3 |
| `durable-runtime` | 1 | admit-durable-dag-run-progression task 1.3 |

## Canonical inventory and preambles

Render directory paths repository-relative with forward slashes and one trailing
slash; ordinal-sort, LF-join with one final LF, UTF-8 without BOM, SHA-256.

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`

Canonical preambles remain 14 / 1,233 bytes /
`595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.
All canonical files and prior delta blocks remain unchanged.

## Active delta directory inventory

Use that same directory recipe for active `openspec/changes/*/specs/*/` paths.

- capability directories: 22
- record bytes: 1,808
- SHA-256: `2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2`
- directories lacking spec.md: 0

## Permanent provenance history

The superseded provenance catalog now has 8 entries, including the
previous current record `approved-pending-openspec-provenance-2026-10-03.md`
unchanged at `612d4b9e3ac08278c2e700139cad9780bb6d9d6230a240b9e57ba7627732969b`.
Render `path<TAB>normalized-sha256`, ordinal-sort, LF-join with one final LF:
1,396 bytes / `5e9e7dc9195ed1a05113ad6bcdd777473407906bc118727d74be4beb751cf45d`.
Neither archival nor routine refreeze removes historical catalog entries.

## Review-note disposition and boundaries

The new design explicitly corrects five provisional assumptions without editing
the approved child artifact: the real AppendAsync port, explicit public/provider
stream amendment, current fenced owner's limit after restart, DAG-role-only ready
claim partition, and CapacityDeferred distinct from AdmissionStopped. Present-null
bytes and caller-independent bound child identity are also retained. Exact lifecycle
signatures and schema changes belong to Task 1.1 contract review. Process task 0.2
is not pinned open, permitting checkbox-only activation; 1.1–3.2 remain open.
No friend, codec, source, canonical or expected-red disposition changes occur.
