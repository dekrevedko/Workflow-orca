# Approved-pending DAG run transition provenance

Date: 2026-10-05. Base d59f8307977dc71d1b77444b9711a0468c65f93d.
Prepared 1.3/1.4 target; source remains unauthorized until its independent review/checkpoint.

## Exact record recipe

Enumerate every active non-archived change's proposal capability classification,
delta operation and requirement block. LF-normalize; start at ### Requirement:
and stop at the next requirement or ## section; strip trailing empty lines and
hash without a final LF. Compare with canonical. ONLY the eight source-pinned
predecessor identities with exact old/current hashes become SupersededByApprovedSuccessor.
Render change<TAB>capability<TAB>capability-kind<TAB>operation<TAB>requirement<TAB>state<TAB>delta-sha<TAB>canonical-sha-or-dash;
ordinal-sort complete rows, LF-join with one final LF, UTF-8 without BOM, SHA-256.

- record rows: 184
- record bytes: 48,504
- record SHA-256: `fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8`
- synchronized operations: 173
- superseded: 8
- declared new-capability requirements outside the canonical set: 3
- pending canonical operations: 0
- semantic approval eligible: yes
- unregistered duplicate owners: 0

## Unresolved-operation table

| Capability | Pending operations | Sync owner |
|---|---:|---|
| none | 0 | none |

Eligibility is measured synchronization only, not source authorization or this
transition's independent approval.

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

Exactly four reshape predecessors are newly superseded, four current successor
blocks are synchronized; all unrelated provenance identities/states remain unchanged.
The RC-3 Proposed hashes remain immutable registry history. The runtime delta's
only change since e6802a3 is MAY -> SHALL; its new canonical hash is separately pinned.

## Independent inventories

Canonical: 14 / 547 B / 7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f.
- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
Preambles: 14 / 1233 B / 595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c.
Active capability directories: 22 / 1808 B / 2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2; missing spec.md: 0.
- capability directories: 22
- record bytes: 1,808
- SHA-256: `2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2`
Directory records are ordinal-sorted repository-relative paths with forward slashes
and trailing slash, LF-joined with one final LF. Preamble records are capability<TAB>
SHA-256 of the LF-normalized prefix before the first requirement, trimmed at the end.

## Permanent catalog

13 artifacts / 2291 B / 8555cbe864794fa08ba8b15399f5ddb63efa027c37f7ee43b8708542b481b9df.
Recipe: path<TAB>normalized-SHA, ordinal-sort, LF-join/final LF. The prior RC-3
provenance artifact is retained unchanged at bee1707d4f68bf2d00cc68e8d3ebfb1911d346379cbf533f5003c12b41ea4640.
No archival or refreeze shrinks this historical catalog.
