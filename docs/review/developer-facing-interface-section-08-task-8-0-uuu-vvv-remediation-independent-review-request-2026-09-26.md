# Reshape Task 8.0 UUU-1/VVV-1 remediation — independent re-review request

Date: 2026-09-26. Base: `4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`.
This supersedes the uncommitted 11-entry Task 8.0 target rejected by the
unchanged `developer-facing-interface-section-08-task-8-0-requirement-gate-independent-review-verdict-2026-09-26.md`.
The original request, manifest, and REJECT verdict remain byte-exact and are
registered in the immutable review-history catalog. No Section 8 source work
has started; this is still a planning gate, not an 8.1 implementation target.

Review the exact target in the companion
`developer-facing-interface-section-08-task-8-0-uuu-vvv-remediation-dirty-manifest-2026-09-26.txt`.
Use raw Git porcelain order. The manifest is 1,361 bytes with SHA-256
`762e8e68e9b2c44d260ae3afbef4be6435fc330b7b663b4a8fffee32d1daa4ee`.
For a non-self-referential content record, exclude only this
request and `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json`;
for every other manifest line emit the two status bytes, TAB, path, TAB, raw
byte length, TAB, lowercase SHA-256, sort records ordinally, LF-join with one
final LF, and hash UTF-8. The record has 13 rows, 2,074 bytes, SHA-256
`c0ef31d2f9c4ad3c0bcbb3d057d65fba13f040cbf354d26b4bddf3490bfdd5f0`.
The two excluded paths remain in the raw manifest and
the universal review-history guard checks the request's normalized bytes.

## Findings to verify

1. **UUU-1:** The remediated Task 8.0 artifact maps all JS-001–JS-010 and
   JS-AC-001–JS-AC-018 from normative document 14 to Section 8 tasks. Its 8.5
   row cites DU-033 and requires one logical outbox with disjoint public versus
   internal claims. It interprets child-start/join handoffs as canonical
   internal continuations of DAG progression without assigning them the
   existing `Continue` record discriminator or creating a second transport.
   Check that interpretation
   against canonical durable persistence/runtime, DU-033, AC-613 and doc 17;
   a real disagreement needs an approved amendment before 8.5 source.
2. **VVV-1:** The guard-verified Section 7 source inventory is used to count
   only active compiled trait-bearing sources as executable evidence. Today
   JS-AC-007 alone has one; 23 criteria are tagged only in `<Compile Remove>`d
   sources and seven have no trait source. The artifact explicitly denies
   executable credit for all excluded tests, and task 8.10 owns compile-aware
   catalog correction plus compiled behavior tests for every AC-606–AC-618 and
   JS-AC-001–JS-AC-018. Marking 8.10 complete now must turn the infrastructure
   gate red with the missing IDs. Removing JS-AC-007's active trait, changing a
   crosswalk status, or deleting a numbered map row must also be detected.
3. **Waiver:** AC-618 names 8.6 and 8.10, not 8.7. The compiled Core catalog
   tests must remain green; a waiver is not executable acceptance evidence.
4. **Provenance:** The first Task 8.0 REJECT verdict is registered byte-exact;
   no prior review artifact was edited. The documentation status, Task 7.3
   row-9 refresh, package-skeleton decision, nine-scenario expected-red
   handoff, and zero-pending canonical sync remain the previously reviewed
   decisions. Verify all 25 canonical headings still exist verbatim.

Reproduce Release `-warnaserror`, the Infrastructure and intentional ExpectedRed
lanes separately, strict OpenSpec validation, `git diff --check`, both anchors,
and a simulated checkpoint path set/tree. No target commit or 8.1 source work
is authorized without a new dated independent `APPROVE` verdict.
