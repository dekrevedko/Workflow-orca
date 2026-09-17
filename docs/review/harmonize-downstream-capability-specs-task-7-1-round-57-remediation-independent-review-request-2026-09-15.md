# Harmonization Task 7.1 round-57 remediation independent review request

**Date:** 2026-09-15
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the final remediated Task 7.1 documentation/guard checkpoint

Review this target on immutable base `99657834684deefa2d22cb6526d714c4566b7ae0`. Do not
edit, stage, or commit it. Return one dated immutable verdict.

## Prior rejected freezes are evidence, not the active target

The repository must retain both prior `REJECT` outcomes byte-exact:

1. Initial Task 7.1 freeze: request 4,124 bytes /
   `5d6076f793a2e3d2ca1bd515803eeb2a6d23c758d7ac1b5a7e2d06b19188d943`, raw manifest
   926 bytes / `76b82093f24cb47e5acfe9e55b1ef4a63b811268228355c17430ca532d4cf6e6`,
   verdict 13,297 bytes /
   `b15e501f6726fb3f908cd896a10bfcf459ff68f70188611a9a09af8e71fbd989`.
2. JJ-1 freeze: request 4,496 bytes /
   `4a61f17b2abdef9d321db73a96ed02ffd454a119ef8f0d5ccc7c162b5f8419a3`, raw manifest
   1,282 bytes / `c40b2eb41e1e78752f91e649f2a2ce1db82900b873c9a5f4c586b02b19a458f0`,
   verdict 12,514 bytes /
   `19a64192aab622f6aba83bdb682180e88e3713072bf9d46686bc736b4e623746`.

The JJ-1 request truthfully named the then-reused original manifest path. The new schema records that
request-named path separately from
`docs/review/harmonize-downstream-capability-specs-task-7-1-jj-1-remediation-dirty-manifest-2026-09-15.txt`,
which now preserves the rejected JJ-1 manifest bytes. The active target uses the separate
`docs/review/harmonize-downstream-capability-specs-task-7-1-round-57-remediation-dirty-manifest-2026-09-15.txt`.
Verify discovery accounts for all three manifests and that tampering with any rejected request,
manifest, or verdict fails.

## Round-57 findings to verify

### Ordinal source-record order

The exact companion record is:

`openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-1-active-guide-positive-call-source-record-2026-09-15.tsv`

It must contain exactly 86 unique rows, use LF only with one terminal LF, be 10,655 bytes, sort path
fields by `StringComparer.Ordinal`, and hash to
`56b6d27ece05d0ff536d9ca5114ba3e1856f0f3288f4bf5226bab341e36963f2`.
The first two rows must be `CLAUDE.md` and `README.md`. Independently reconstruct each row as
`<forward-slash path>	<LF-normalized UTF-8 byte length>	<lowercase SHA-256>`.

A case-insensitive/culture-oriented ordering that puts `docs/active-implementation-index.md` before
`README.md` must fail the executable ordinal-order assertion even if every row is otherwise unchanged.
One-byte mutation, row removal, CRLF conversion, missing terminal LF, duplicate path, or changed
digest must also fail.

### Exact companion path

The dated scan artifact must contain the exact backticked companion path above, with no tab or
truncated `ask-...` spelling. Corrupting the path must fail. The artifact must hash to
`55bda56ed62a6b0cf7664a26f0331d35d37b0e207d1352f27fb37808e9b954c4`.

### Original Task 7.1 obligations retained

- The live active-corpus scan still reports zero positive removed/deferred call forms.
- The ephemeral, Kubernetes scheduler, and Orleans guides retain their exact `13.4 re-entry links.
- HH-1 remains closed by recursive provenance-artifact discovery across `openspec/changes/**`.
- II-1 remains closed by ending the removed-concepts region at the next `###` subsection.
- The active ledger and design durably describe the ordinal evidence and rejected-freeze disposition.
- No `src/**` or canonical `openspec/specs/**` path is in scope.
- Task 7.2 remains blocked until this target receives an independent `APPROVE` verdict and checkpoint.

## Validation to reproduce

- Debug and Release non-incremental warnings-as-errors builds: 0 warnings / 0 errors.
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; Provider Certification 96.
- PostgreSQL 101; SQL Server 72; Integration 11.
- Infrastructure guards 222/222.
- Exactly 14 separately classified intentional expected reds, for 236 total guard cases.
- Focused Task 7.1, review-manifest provenance, crosswalk, and accounting controls green.
- OpenSpec strict 18/18.
- Harmonization ledger 25 complete / 9 open / 34 total.
- `git diff --check` clean.

The only content-record exclusion is the self-referential
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`. Recompute the
commit-real raw manifest and scoped worktree content record from the unchanged base and compare
them with the separately published freeze anchors.
