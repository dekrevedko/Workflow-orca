# Harmonization Task 8.1 final audit independent exit review verdict

**Date:** 2026-09-24
**Reviewer:** independent review
**Scope reviewed:** the nine-entry final exit freeze on base
`b660d2d5b58d4b06663dd24f6a94a9c71ab37342` (tree `ec1da3eaecc8b1c0193fef00b125f1d3bddbb323`), named by
`harmonize-downstream-capability-specs-task-8-1-final-audit-dirty-manifest-2026-09-23.txt`.
**Authorization requested:** the Task 8.2 independent exit approval of the synchronized
canonical and documentation result, which gates the Task 8.3 checkpoint. This verdict grants that
approval for the exact frozen target only. It does not authorize reshape Task 8.0 or Section 8
source work.

## Summary

Every audit claim reproduces from Git objects and from my own independent scans:
- **Canonical diff:** from Section 7 exit checkpoint `923ab063` to the base, the diff is 11
  modified canonical files, +471/−107 lines, and a 132,727-byte patch hashing to `e07826ad…`. All
  22 blob identities match the audit table.
- **Canonical corpus:** 14 capabilities with 190 requirement headings and no duplicate within a
  capability.
- **Active deltas:**
  - 4 active changes, 16 delta capability directories, and 176 delta headings;
  - no duplicate `(capability, requirement)` owner and no directory without `spec.md`;
  - the 1,321-byte directory record hashes to `5e8a9725…`.
- **Provenance checkpoint:** 176 rows, 46,211 bytes, `ea8719e7…`. It is eligible for semantic
  approval with zero pending operations, giving 173 synchronized plus 3 bootstrap-only.
- **Vocabulary:** 86 evolving sources with zero stale Section 7B claims and zero positive
  removed or deferred calls.
- **Links:** 85 active Markdown files with 195 file targets and 8 fragment targets, none missing.
- **Source map:** 269 review, 200 archive, 19 normative spec files plus one guide, and 9
  implementation files at base. Only Task 7.3 row 10 changed, and all 22 rows match.
- **SSS-1:** closed. In a real CRLF checkout the Task 7.2 guard is green again, the committed
  prompt blob is pinned, and the routing key is bound.
- **Validation:** every reported count and both freeze anchors reproduce.

One P3 observation is recorded (TTT-1). It does not block the exit.

## Method

There was no dedicated review worktree. Builds, tests, and mutations ran in two disposable
detached worktrees holding the same nine entries. A disposable CRLF clone was built from a bundle
of `HEAD`.

Every script that could commit or reset first asserted that it was inside its disposable copy and
that the main `HEAD` was still `b660d2d5`. Main was never modified. The review created no ref in
the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 9 entries, 799 B, `a0f5a44a…1fff` | identical, before and after probing |
| Scoped content record | 8 rows, 1,281 B, `610ed7ac…3cfc` | identical |
| Entries | 6 modified, 3 untracked, 0 staged | identical |
| Simulated checkpoint | tree `ac37cbf9…` | identical from a copy of the live index; path set equals the manifest (3 A / 6 M) |

- **Prior chain:** checkpoint `23ac19aa` has tree `ee5fe64a`, exactly the approved target.
  Evidence commit `be1664d9` is its only child and adds the 2026-09-23 Task 7.7 verdict
  byte-for-byte (9,056 B, `31dabc0f…`) through a new Task 7.7 entry. Activation `b660d2d5` changes
  only the two transition values.
- **Registry:** the 20 archived freezes and 14 entries reproduce from committed objects, and every
  declared current-match pin is exact. No committed `docs/review/` record is modified.
- **New records:** the request (5,459 B) and manifest are in `appendOnlyRecords`.
- **Scope:** no `src/**` or `openspec/specs/**` change.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Guards, full project | 226 passed, 14 failed, all 14 `ExecutableBehaviorExpectedRedGuards.Scenario_*` |
| `Disposition=Infrastructure`, Release | 226/226, including the canonical synchronization gate |
| `Disposition=ExpectedRed` | exactly 14 failures |
| Strict OpenSpec | 18/18 |
| Ledgers | harmonization 32 complete / 2 open / 34; reshape 129 complete / 30 open / 159, Task 8.0 still blocked |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. Independent reproduction of the audit

- **Canonical diff:** the audit's exact command produced 132,727 bytes, SHA-256
  `e07826ad8dfeb18d6ba4f48f1f0ea6d392ce96e44048ca3eebc521ff8a589453`. `--numstat` gives the eleven
  per-file counts in the table, and `git rev-parse` gives all 22 blobs. No canonical file was added
  or deleted. `event-driven-prototype`, one of harmonization's two remaining deltas, is unchanged
  since `923ab063`.
- **Ownership:** an independent scan of `openspec/specs/*/spec.md` and all non-archived
  `openspec/changes/*/specs/*/spec.md` reproduces every count above. The harmonization change owns
  only `event-driven-prototype` and `state-driven-runtime`.
- **Vocabulary and links:** the scans reuse the guard's eight classifier expressions but my own
  enumeration and replay. The link scan resolves URL-decoded relative paths and GitHub-style heading
  anchors, and excludes fenced code, external schemes, review and archive records, and change
  artifacts. Its totals match the audit exactly.
- **Source map:** base counts from `git ls-tree` are:
  - 269 review and 200 archive files; the archive splits into implementation-phases 109,
    architecture 24, requirements 13, plans 40, research 7, durable 5, reviews 1, and root 1;
  - 20 `docs/specs` files, of which `18-semantic-appendix.md` is the guide;
  - 9 implementation, 11 root, 2 observability, and 1 Orleans file.

  The map now labels the counts as a dated snapshot. The Task 7.3 artifact's normalized digest
  `532be867…` equals the guard constant.

## 4. Controls

| Probe | Mutation | Result |
|---|---|---|
| K1 | archived prompt content edited in the working tree | **red** |
| K2 / K3 | archived prompt / provenance record converted to CRLF in the working tree (positive controls) | green |
| K4 | archived prompt committed as a CRLF-only blob in the disposable copy | **red**, `HEAD` blob differs from the move-commit blob |
| K5 / K6 | archive-index old-path key changed / prompt link retargeted | each **red** |
| K7 | source-map review count changed without a row-10 refresh | **red** |
| K8 | row 10 reverted to its pre-8.1 hash | **red** |

**CRLF check:** a real `core.autocrlf=true` clone of the base fails 7 Infrastructure guards,
including Task 7.2 on SSS-1. The same clone at the committed target fails only the six
long-standing ones, with Task 7.2 green.

## 5. TTT-1 (P3): observation

Unlike every Task 7.x artifact, the Task 8.1 audit artifact and its completion text are not pinned
by any guard. After the checkpoint they could change without a red result, because the immutable
history guard covers only `docs/archive/` and `docs/review/`. Its figures are reproducible from
the commands it records and were reproduced here, so this affects only later tamper evidence.

## 6. Reviewer hygiene and checkpoint instructions

`HEAD` is `b660d2d5b58d4b06663dd24f6a94a9c71ab37342`, with the nine frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees, clone, and bundle are removed.

This approval covers only the frozen bytes. For Task 8.3:
- stage exactly the 9 manifest paths from the live index;
- confirm the tree is `ac37cbf9d74162b679c1d36ed0922685f6a8c84c` with parent `b660d2d5`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict and catalog it in `appendOnlyRecords`;
- register it as an `APPROVE` row in an entry whose `task-{task}*verdict-*.md` glob discovers it,
  for example a Task 8.1 entry;
- archive the active freeze.

The activation must then pin that evidence commit by its full id. Reshape Task 8.0 and Section 8
remain gated by their own approvals.

## Determination

The synchronized canonical tree, active deltas, vocabulary, links, source classification, and
task accounting all reproduce independently. No duplicate owner or pending operation remains, and
the last Task 7.7 observations are closed with proofs in both line-ending modes. The harmonization
change is ready for its Task 8.3 checkpoint.

**Verdict:** **APPROVE**
