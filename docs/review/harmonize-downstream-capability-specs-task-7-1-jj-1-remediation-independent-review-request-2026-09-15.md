# Harmonization Task 7.1 JJ-1 remediation independent review request

**Date:** 2026-09-15
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the remediated Task 7.1 documentation/guard checkpoint

The reviewed base remains Task 6.6 second-hardening activation commit
`99657834684deefa2d22cb6526d714c4566b7ae0`. Preserve the original Task 7.1 request and the
2026-09-15 immutable `REJECT` verdict byte-for-byte. That verdict rejected only JJ-1: its
independent scan reported the correct 86-source population and 10,655-byte record length but a
different aggregate SHA-256.

The remediation does **not** replace the published digest with the verdict's proposed value. A
fresh reproduction using the artifact's stated ordinal-path recipe yields the original
`74fc76977157666e7f018d1449c9729e5d0112c5ec395f98b6b75336c83774f6`. To make the disagreement
fully diagnosable, the target now preserves the exact rendered 86-row, 10,655-byte source record at
`openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-1-active-guide-positive-call-source-record-2026-09-15.tsv`.
The dated scan artifact names that companion, and guard source pins its exact byte count, LF row
count, terminal LF, absence of CR, and SHA-256. The companion is historical evidence; the live
positive-call guard continues to rescan the evolving corpus without freezing future documentation
content.

The exact remediated target reuses the registered
`harmonize-downstream-capability-specs-task-7-1-dirty-manifest-2026-09-15.txt` path. The immutable
`REJECT` verdict retains the rejected target's 926-byte / `76b82093...` anchor; this remediation
request explicitly discloses that the live manifest now names the superseding target rather than
rewriting the original request or verdict. Recompute the commit-real raw manifest and scoped content
record from the unchanged base. The self-referential review-provenance fixture is the only
content-record exclusion.

## Decisive JJ-1 checks

1. Independently enumerate the same four source classes: 2 root guidance files, 42 active
   documentation files, 14 canonical OpenSpec files, and 28 active change planning/delta files.
2. For every source, normalize CRLF and lone CR to LF, encode UTF-8 without BOM, and render
   `<forward-slash path>\t<normalized byte length>\t<lowercase SHA-256>`.
3. Sort the 86 rows using ordinal path comparison, join them with LF plus one final LF, and compare
   the resulting bytes directly with the checked-in TSV companion. They must be byte-identical,
   10,655 bytes, and hash to `74fc76977157666e7f018d1449c9729e5d0112c5ec395f98b6b75336c83774f6`.
4. Verify the corrected scan artifact is 3,117 bytes and hashes to
   `452bbc7b69cc72b8ecd2b245045ba34a2dcff4a925020213cb3bf53bc8f57441`.
5. Mutate one companion-record byte without changing guard source: the focused Task 7.1 guard must
   fail on the immutable source-record assertion; restore it byte-exactly afterward.

If a fresh recomputation still differs, report the first differing rendered row and its source-file
hash rather than only the aggregate. This is the evidence needed to distinguish a population,
normalization, ordering, or content difference.

## Original Task 7.1 obligations retained

- The executable guard rescans the live active corpus and reports zero positive removed/deferred
  call forms.
- The ephemeral, Kubernetes scheduler, and Orleans guides retain their exact §13.4 re-entry links.
- HH-1 remains closed by recursive provenance-artifact discovery across `openspec/changes/**`.
- II-1 remains closed by ending the removed-concepts region at the next `###` subsection and
  rejecting removed identifiers everywhere outside that exact region.
- Task 7.1 remains complete at 25/34 tasks; no `src/**` or canonical `openspec/specs/**` file is in
  scope.

## Validation to reproduce

- Debug and Release non-incremental warnings-as-errors builds: 0 warnings / 0 errors;
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; Provider Certification 96;
- PostgreSQL 101; SQL Server 72; Integration 11;
- Infrastructure 222/222 and exactly 14 separately classified intentional expected reds, for 236
  total guard cases;
- focused Task 6.6/7.1/crosswalk/accounting and provenance controls green;
- OpenSpec strict 18/18 and harmonization ledger 25 complete / 9 open / 34 total;
- `git diff --check` clean.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.
