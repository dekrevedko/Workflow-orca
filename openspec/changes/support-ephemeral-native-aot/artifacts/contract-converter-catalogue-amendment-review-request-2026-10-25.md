# Issue #13 converter catalogue amendment review request — 2026-10-25

Review the eight paths in [the manifest](contract-converter-catalogue-amendment-manifest-2026-10-25.txt),
against the approved R1 contract and the pre-amendment task-2 state. The exact
[amendment](contract-converter-catalogue-amendment-2026-10-25.md) adds only the
existing sealed factory, its implicit public constructor and its two overrides.
Six current planning files are revised; one normative supplement and the
self-inclusive path manifest complete this scope. This request and the later
verdict are excluded from the content hash.

Base HEAD: `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`.
Approved contract checkpoint: `6122025828c7e8e3525ad8f2e24048024cbbb2d5`.
Pre-amendment partial source snapshot: tree
`ec1d2cadbaa73efda377d9ad716e700bd85073fa` (incomplete/red, uncommitted).
Eight-path amendment content SHA-256:
`477b88fde7bc4a1320cac82cdfa4fecab4e7c09c852fa3e517dd6f8a202cd8b5`.
Hash rule: sort repository-relative paths ordinally; concatenate UTF-8 path,
NUL, raw file bytes, NUL, then SHA-256. The manifest includes itself. Compact
per-file hashes, pre-edit planning text and the exact six-file diff are retained
under ignored `.artifacts/publishing-validation/issue-13-converter-amendment/`.

## Verification and limits

- Strict OpenSpec: 25/25; whitespace clean. Seven requirement identities/counts
  remain 2/2/1/2, with no change to structured-fiber-execution.
- All 66 other captured input files are byte-identical, including source, tests,
  canonical/numbered docs, declaration/API/compile/package baselines, registry,
  guards and prior evidence. All checkboxes are unchanged; task 2.2 stays open.
- Actual factory/strong-type files are byte-equal to HEAD and the factory remains
  internal. Both eleven-entry factory switches, every parser mapping and all
  eleven JsonConverter attributes match the amendment's table.
- No new C# edits, build, runtime/native test or package claim. Existing task-2.2
  ReSharper SARIF/triage is preserved; InspectCode is not rerun for Markdown.
  Matching API/compile/package tests and scoped ReSharper run after approval
  with the actual accessibility change.
- Full Infrastructure is not claimed. The exact-delta/provenance guards still
  describe the R1 composition and must be reconciled explicitly after amendment
  approval; none is loosened here. This planning review does not authorize a
  checkpoint or call the partial source target green.
- Task-2.2 F2 (nested-failure corpus) remains unresolved. The amendment supplies
  no implementation approval, assertion waiver, native qualification or task-2.3
  advance. Public exposure still waits for explicit user approval.
- No temporary checkout, build, restore, package or ReSharper cache is created
  for this documentation-only pass. No bin/obj/TestResults/inspection-cache
  directories exist under src/tests/tools/samples/benchmarks. E:/oc13t22 remains
  absent. Earlier policy-blocked cleanup is not retried; unrelated work is
  preserved. Compact review receipts remain.

## Requested review

Check exact signatures, sealedness/implicit constructor, unchanged converter
bodies/attributes and closed table, product attribution versus prohibited
application settings, NS-02d/NS-06g–h across both generation modes/native cells,
all original R1 cases, owner routing and explicit approval boundaries. A separate
self-review is transparent evidence, not an external reviewer or user approval.
Keep task 2 uncommitted regardless of this review's result.
