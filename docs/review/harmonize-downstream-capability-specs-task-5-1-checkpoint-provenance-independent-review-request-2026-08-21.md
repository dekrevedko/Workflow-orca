# Harmonization task 5.1 checkpoint-provenance independent review request

**Date:** 2026-08-21  
**Requested verdict:** `APPROVE` or `REJECT`  
**Review target:** commit `ff11ead781f8fef343fafc6e6bc8307d746e4a05`  
**Parent:** `179421029f62bc0cd4d5968d465cf045f420ba34`  
**Tree:** `3baddd97a6919bf5f674daed33bc236496a234c2`

This is an implementation-owner request, not an approval. It asks an independent reviewer to
evaluate the already-committed, immutable Task 5.1 object after Task 5.2's independent review found
that the repository contained only Task 5.1's original review request, not a verdict. No reviewer
should infer approval from the commit, its green validation, or the completed Task 5.1 checkbox.

An `APPROVE` verdict supplies the missing Task 5.1 checkpoint provenance only. It does not approve
the rejected Task 5.2 target, authorize a Task 5.2 checkpoint, start Task 5.3, close the
harmonization gate, archive either change, or authorize reshape Task 8.0.

## Immutable committed-object target

Review the exact Git object, not the current dirty worktree. A separate detached worktree or an
equivalent read-only object inspection may be used. Reproduce:

```powershell
git cat-file -e 'ff11ead781f8fef343fafc6e6bc8307d746e4a05^{commit}'
git rev-parse 'ff11ead781f8fef343fafc6e6bc8307d746e4a05^'
git rev-parse 'ff11ead781f8fef343fafc6e6bc8307d746e4a05^{tree}'
git diff-tree --no-commit-id --name-status -r ff11ead781f8fef343fafc6e6bc8307d746e4a05
```

The parent and tree must match the anchors above. The changed-path set must contain exactly the 17
entries recorded in
`docs/review/harmonize-downstream-capability-specs-task-5-1-dirty-manifest-2026-08-20.txt`.
Compare paths as a set because that historical manifest is path-sorted evidence, not Git's raw
emitted order.

## Required evidence disclosure

The original request froze raw porcelain SHA-256
`33a4d11c3ba86a9cc7dc9a538ea84bcdf5a1ea9cbca2eb680963daf846ae81c8`. The checked-in manifest is
1,228 bytes across 17 LF-terminated lines and has SHA-256
`271309bd1da188c023610ffdde4065729fbc17f16f3ae786fc17561d1842f898`. That digest is also the
path-sorted projection digest and therefore differs from the historical raw anchor. Preserve the
manifest byte-for-byte and describe it as `SetOnlyPathSorted`; do not relabel its digest as raw.

The original dirty-worktree content record was 2,427 bytes with SHA-256
`e73b7f4ba2789ca613fa08b8dbfe3658eb31c0b85a6cb75baeecddbe76e7090c`. It is historical freeze
evidence, not a reproducible commit anchor: Git's commit normalized at least one mixed-line-ending
file, and neither stored blobs nor checkout filters reproduce that exact dirty-worktree record.
Do not relabel it as commit evidence.

Instead, independently reproduce both exact committed-object projections from the 17 frozen
status/path entries:

- raw Git blobs: 2,428-byte content record, SHA-256
  `741cfd6bbdd46cb4390c2f40c0d21d81d35b3e3749438b38efda44f26da1ff72`;
- blobs after Git checkout filters: 2,428-byte content record, SHA-256
  `b18924f74e0f0e8d47d638db9440ebed4709eb597bb7103a36c5be943d1d32ba`.

Use `git cat-file blob ff11ead:<path>` for the first projection and
`git cat-file --filters --path=<path> ff11ead:<path>` for the second. The immutable commit object,
its parent, its tree, the exact path set, and these two committed projections are the reproducible
target identity for this retroactive review. The one-byte record-size difference from the historical
dirty target must be disclosed, not concealed.

## Technical claims to re-derive

Use the original request
`docs/review/harmonize-downstream-capability-specs-task-5-1-independent-review-request-2026-08-20.md`
for the full claim wording. At minimum independently confirm:

1. The eight Task 4.3/Task 5.1 review observations have discriminating infrastructure regressions:
   wrapper-type direction, full-history checkout enforcement, permanent-catalog cardinality,
   recursive scheduler-sensitive clock scanning, strict-subset helper behavior, version-agnostic
   checkout discovery, generated-tree exclusions, and post-`**Completed:**` count parsing.
2. Exactly 42 canonical operations match the authoritative reshape deltas:
   `developer-facing-surface` (7), `durable-persistence-and-outbox` (5), `durable-runtime` (8),
   `event-routing-and-waits` (8), `state-driven-runtime` (2), `workflow-authoring` (3), and
   `workflow-contracts` (9).
3. The authored-`Yield` canonical requirement is removed with its exact historical block retained;
   the permanent catalog contains 11 entries with SHA-256
   `81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd`.
4. The provenance record contains 176 rows: 165 synchronized, eight pending for Task 5.2, and three
   declared bootstrap requirements outside the canonical set. Semantic approval remains ineligible
   at this checkpoint.
5. No `src/**`, public API, package, sample, provider schema, or runtime behavior changed in the
   exact 17-path commit delta.

## Validation

Run validation against a clean checkout of commit `ff11ead` so current remediation changes cannot
contaminate the result:

```powershell
dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror
dotnet build OrcaCore.slnx -c Debug --no-restore --no-incremental -m:1 -warnaserror

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=ExpectedRed"
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj -c Release --no-build --no-restore

openspec.cmd validate --all --strict
git diff --check
```

The historical owner packet expected clean Debug and Release builds, infrastructure 211/211,
exactly 14/14 intentional expected-red failures, provider certification 96/96, and strict OpenSpec
18/18. Treat those numbers as claims to reproduce, not as current-worktree results.

## Verdict instructions

Create exactly one new immutable verdict at:

`docs/review/harmonize-downstream-capability-specs-task-5-1-checkpoint-provenance-independent-review-verdict-2026-08-21.md`

Record the exact commit, parent, tree, 17-path set comparison, manifest disposition and both
manifest hashes, the historical content-record limitation, both committed content-record
projections, technical findings, commands/results, and exactly
`APPROVE` or `REJECT`. Do not edit the reviewed commit, the original request, its historical
manifest, or the Task 5.2 verdict.

If approved, the repository owner must preserve the verdict in a distinct approval-provenance
commit before any new Task 5.2 freeze or checkpoint. The independent reviewer should create only
the verdict file and should not mark Task 5.2 approved.
