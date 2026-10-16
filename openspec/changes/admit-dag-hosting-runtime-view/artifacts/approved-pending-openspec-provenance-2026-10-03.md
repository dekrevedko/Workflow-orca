# Approved-pending DAG hosting runtime-view canonical provenance

Date: 2026-10-03

This is the prepared atomic Tasks 1.3/1.4 target, based on activation
`64d97c644475f6dfbe183540bf546c7ab48eab46`. The independently approved contract is checkpoint
`43d869fc29e7daa3ec567d4602960f458eb98492` (tree
`5c9d63fe0b30ea4465054ac01c986a8b80707e62`), with its single-parent,
direct-child evidence `1ea7f44b5a32d058e04b913f387317c21747add5`.
The verdict's normalized and raw SHA-256 is
`5ff6b3322d3071149fefe583fd74fba35afa5d28ea6a81ca50637bcdb029b457`.
This is a canonical/process transition, not runtime source approval.

## Requirement ownership and exact reproduction

Enumerate every active non-archived change's delta requirement. Normalize blocks to LF,
strip trailing empty lines, and terminate at the next requirement or `## ` heading.
Render `change<TAB>capability<TAB>capability-kind<TAB>operation<TAB>requirement<TAB>state<TAB>delta-block-sha256<TAB>canonical-block-sha256-or-dash`,
ordinal-sort, LF-join with one final LF, encode UTF-8 without BOM, and hash SHA-256.
Only the four schema-7 predecessor rows with exact change/capability/heading/operation,
old-block hash and newest canonical-block hash classify as superseded. The two runtime-view
blocks now match canonical exactly. Both Proposed rows and the completed authoring evidence
remain immutable history; the two current rows are ApprovedPending.

- record rows: 180
- record bytes: 47,347
- record SHA-256: `40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`
- synchronized operations: 173
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- duplicate owners: 2 exact three-owner chains; 0 unregistered chains
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 4 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Permanent history catalogs

The seven superseded provenance artifacts are retained without edits. Render
`path<TAB>normalized-sha256`, ordinal-sort, LF-join with one final LF:
1,222 bytes, SHA-256
`a4c8f08e730b3f0b9482dbf528af02dd33914f8986cdd509963899b169564739`.
The newest predecessor is `contract-rv-remediation-openspec-provenance-2026-10-02.md`,
normalized SHA-256 `515a968b5027f8120b6a6ff4e87525542329fed42b6d5e6fa0eea8ad1f055125`;
its historical record remains 180 / 47,327 bytes / `79f1638357e3b1aa35aef28a4f7c45233acc135036d5f6c1b274e687d454b947`.

Separately, the permanent superseded-contract catalog holds the original rejected contract
and approved RV-remediation contract. Their exact normalized hashes are independently pinned
in guard source and schema 7, with paths resolved through active-or-dated-archived records.
That closes the review's unpinned-old-contract observation without rewriting old status text.
Neither catalog shrinks when its owning change is archived. Archival itself is not performed
here and requires a separately reviewed inventory/path refreeze.

## Canonical inventory and preambles

Render repository-relative directory paths with forward slashes and one trailing slash,
ordinal-sort, LF-join with one final LF.

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`

Canonical preambles remain 14 / 1,233 bytes /
`595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.
Only the two approved requirement blocks change. All other canonical requirements,
heading order and preambles remain byte-identical to the activation base.

## Active delta directory inventory

Use the same directory recipe for active `openspec/changes/*/specs/*/` paths.

- capability directories: 20
- record bytes: 1,645
- SHA-256: `dfabdc307ab5c49d6bc15c140b7778a5dce0512faf601c83645574fa0e770d62`
- directories lacking `spec.md`: 0

## Runtime-rule ownership and sequencing

The approved package-boundary requirement and doc 17 specify the evaluator/null policy.
The existing canonical durable-runtime and immutable-authoring requirements are unchanged
and hash-pinned. This transition explicitly confirms eager bridge decoding of every
successful direct resultful dependency output before evaluation, including outputs the
mapper does not read. A decode failure is `DAG_INPUT_MAPPING_INVALID` at that node.
The reviewed reshape 8.4/8.5 bridge contract must implement and test that rule; no codec
reference or codec access is granted to OrcaCore.Dag.

Mapper exceptions map to `DAG_INPUT_MAPPING_INVALID`, including mapper-thrown cancellation,
except process-integrity failures. Only the OutOfMemoryException exclusion is testable
in-process. Stack-overflow/access-violation exclusions are process policy, not catch or
unsafe-test claims. External execution cancellation remains separate from mapper failure.

Task 1.3 is prepared complete. Task 1.4 remains open for this independent checkpoint gate
and is deliberately unpinned to permit its later checkbox-only activation. Source tasks
2.1–3.2 remain open. The current compiled graph still has eight friends: the ninth
Dag-to-Dag.Hosting friend is independently approved and canonical but not compiled.
No product source, friend attribute, codec, package edge or public API changes here.
