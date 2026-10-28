# Issue #13 task 2.3 review request

Review the private typed-dispatch target and the proposed minimal public throttle
metadata amendment. Task 2.3 is not complete and task 2.4 must not start. Keep the
whole task-2 target uncommitted; the proposed public annotation is unapplied.

- HEAD/base parent: `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`.
- Prior task 2.2 source baseline: `2e276cbeddf908ff41d75c762582fec80e6737b8`.
- Exact 127-path [manifest](task-2-3-review-manifest-2026-10-26.txt), including itself.
- Candidate tree: `ceef1c99341a39a70c77f13f530ca6ecc3d244f8`.
- Content SHA-256: `56d38b00ab738071ee7820cfadc23103ccfd5cbfb75fbee8a6d54658e810d298`.
- Porcelain SHA-256: `9f0b810c5673822f906a7a58c33507961d801f1f6b563f5332d84df758316c23`.
- Content rule: ordinal UTF-8 repository-relative path, NUL, raw bytes, NUL.
  This request and its forthcoming verdict are excluded. All tree blobs were
  compared byte-for-byte with the manifest, and the temporary index was removed.
  The author index is empty; no commit, push, PR or issue edit occurred.

[Evidence](task-2-3-implementation-evidence-2026-10-26.md) records red/setup/green
distinctions, scope and limits. [ReSharper triage](task-2-3-resharper-review-2026-10-26.md),
the 90-row original-site dispositions, 14 supplemental sites and 42 individual
analyzer records are adjacent. Compact raw receipts remain under
.artifacts/publishing-validation/issue-13-task-2-3/.

Required lanes: OpenSpec 25/25; Release 0 warnings/0 errors; Infrastructure
301/301; six CI unit projects with coverage 969/969. PackageReference-only cases
38/38, plus six selected package probes in each of local full trim and Native
AOT, with default JSON reflection disabled. ReSharper completed with zero errors
in both valid solution graphs. Native diagnostic publications retain a selected
IL2090 finding and therefore do not establish warning-clean native support.
Windows 11 development probes do not satisfy task 2.4's native host matrix.

Read the diff against the approved task 2.2 tree. Check private registration/start
and event bridges, callback lifetime and generic closure, struct state replacement,
ordered nullable/outcome construction, fixed codec parity, early array admission,
and the exact source allowlist. Earlier runtime tests, API baselines, canonical
specs, approval evidence and all retired rows must remain unchanged. Review the
resolved nonpublic-getter and missing-array regressions without weakening tests.

The remaining gate is public StepExecutionThrottle.For<TStep>. Its existing
GetInterfaces validation produces IL2090 in both native profiles. Read
[the proposed amendment](contract-throttle-metadata-catalogue-amendment-2026-10-26.md):
it adds only Interfaces metadata to the existing generic parameter. Do not apply
it or alter the API baseline before explicit approval. Confirm no broader public
bridge, dependency, runtime option, root/suppression or compatibility badge.

This is a separate self-review pass, not an external reviewer or user approval.
A favorable amendment review does not authorize applying the public annotation.
Retain all author deliverables and compact receipts; remove owned validation
worktree/build/restore/inspection output after the final drift check.
