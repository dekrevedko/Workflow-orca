# Task 2.1 stream-identity provenance checkpoint

Date: 2026-10-07. Base 89e3b18. The four-block transition is independently approved and checkpointed at 1770b2b / evidence 18bf865. This source target is uncommitted and independently review-gated.

Requirement record: 184 rows / 48504 B / fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8; 173 synchronized, eight superseded and three bootstrap-only. Zero pending operations; semanticApprovalEligible=true is synchronization eligibility, not source approval. Requirement blocks and directory inventories are unchanged from the approved transition.

## Exact preamble correction

Hash the complete LF-normalized prefix before the first requirement heading, WITHOUT trimming trailing whitespace or blank lines. Render capability<TAB>SHA-256, ordinal capability order, LF rows with one final LF. This corrects the predecessor artifact's inaccurate words "trimmed at the end"; the artifact itself remains byte-exact in the superseded catalog. Preambles: 14 / 1233 B / 595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c.

## Whole canonical content record

For each canonical openspec/specs/*/spec.md, normalize CRLF or CR to LF; UTF-8 without BOM, with NO trimming. Render repository-relative path<TAB>normalized byte count<TAB>SHA-256, ordinal path order, LF rows and one final LF. 14 rows / 1635 B / cee7cd376777d5cc99c42e1de04cd692c7cc8270c85deb9a976ed94b7395baa0. The fixture lists every file; the guard pins the aggregate in source. Hand-editing an unowned requirement is now detected independently of the active review freeze. Refresh requires an explicitly reviewed mechanical sync and updated source pin, never routine metadata refresh.

## Unresolved operations

| Capability | Pending operations | Sync owner |
|---|---:|---|
| none | 0 | none |

Canonical directory inventory: 14 / 547 B / 7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f. Active delta directory inventory: 22 / 1808 B / 2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2.

Permanent superseded catalog: 14 artifacts / 2468 B / 47b4201ffdd9edf6707d3679afa0a18c47f145f4107795def5f4fd5f0844e050. Recipe: path<TAB>normalized SHA-256; ordinal rows, LF and one final LF. All old artifacts, packets and verdicts remain unchanged.

## Executable claim table

- record rows: 184
- record bytes: 48,504
- record SHA-256: `fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8`
- synchronized operations: 173
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 8 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 22
- record bytes: 1,808
- SHA-256: `2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2`
