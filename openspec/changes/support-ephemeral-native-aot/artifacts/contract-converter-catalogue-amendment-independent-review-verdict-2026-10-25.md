# Issue #13 converter catalogue amendment review — 2026-10-25

## Verdict: APPROVE recommendation, amendment only

No findings in the frozen planning amendment. This is a separate self-review
pass, not review by another agent/human and not explicit user approval. It does
not authorize public exposure, a task-2 checkpoint or task-2.3 work. The user
must approve this exact amendment before the converter's accessibility changes.

Reviewed eight-path scope:
[manifest](contract-converter-catalogue-amendment-manifest-2026-10-25.txt),
[request](contract-converter-catalogue-amendment-review-request-2026-10-25.md),
[normative supplement](contract-converter-catalogue-amendment-2026-10-25.md).
Base HEAD remains `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`.
Frozen content SHA-256:
`477b88fde7bc4a1320cac82cdfa4fecab4e7c09c852fa3e517dd6f8a202cd8b5`.
The request and this verdict are excluded from the eight-path hash. No frozen
path changed during review.

## What I reviewed

- Read the exact six-file planning diff against the pre-amendment state, the
  complete supplement and the actual factory/strong-value declarations. The
  compiled-consumer failure retained from task 2.2 identifies inaccessible
  product attribution independently of WorkflowJsonMetadata. Runtime metadata
  is not a remedy for that compiler failure.
- The additional catalogue is one public sealed type in its existing namespace,
  its implicit parameterless constructor and its two existing public overrides.
  The base type, parameter/return types and metadata API match the actual source
  and prior contract. Only the factory class's accessibility token may change
  after approval; there is no new constructor implementation, public annotation,
  generic bridge, dependency, friend or metadata hook.
- Matched both eleven-entry type switches, all eleven JsonConverter attributes
  and every Create/Parse mapping against the supplement. The private converter,
  bodies, string encoding/decoding/validation/null behavior and unsupported-type
  exception remain fixed. Existing source file hashes are pinned; neither
  source file was changed in preparation or review.
- Product type attribution is distinguished from prohibited application
  converter settings. NS-02d expressly rejects the now-accessible product
  factory when listed in context options, before publication/seal/work, with
  valid retry. Ordinary attributed strong types remain the positive case.
- NS-06g retains the original blocked EventName DTO and adds the complete
  eleven-type root/nested/collection corpus through public packages. Both
  Metadata and Default generation modes must compile and execute in every
  native cell, with legacy byte/value/fingerprint comparisons and an independent
  literal JSON expectation. NS-06h covers the exact public factory boundary,
  unsupported direct requests and absence of metadata/DI/host side effects.
- All original AOT-01–16, NS-01–12 and R1 subcases remain required. No reflection
  switch, private bridge, warning suppression, root omission, new supported
  shape or broader engine/package claim is introduced. The legacy control
  retains its unconfigured semantics rather than calling Register.
- Current proposal/design/tasks and three delta files agree. Seven requirement
  identities/counts remain unchanged. The exact canonical, numbered-document,
  API/declaration/compile/package, registry and provenance transitions are owned
  by task 2.2 after explicit approval. Earlier immutable protocols/verdicts stay
  byte-exact; inherited exact-composition guards must be reconciled explicitly,
  never broadly waived. No historical evidence is rewritten to imply approval.

## Validation and state

Strict OpenSpec passes 25/25 and git diff --check is clean. Source/table/hash,
checkbox and scope checks passed. All 66 other captured input files, including
the partial implementation, tests, canonical specs, numbered docs, baselines,
registry, guards and earlier review artifacts, are byte-identical. No source
exposure occurred: the factory is still internal and both it and StrongValues.cs
are byte-equal to HEAD. The author index is empty and HEAD is unchanged.

This is planning-only evidence. No new build/runtime/native or package test was
run, and no whole Infrastructure pass or green source target is claimed. The
previous task-2.2 ReSharper reports/triage are preserved; Markdown changes do not
receive a new code-inspection claim. After approval, the actual accessibility
change must pass matching API/compile/package tests and scoped ReSharper before
the next source review. Qualification may reveal further diagnostics; those
retain the contract's individual classification/remediation gate.

The task-2.2 source review remains REJECT/incomplete until its compiler blocker
is actually resolved and its separate F2 nested-failure corpus passes without
weakened assertions. No task checkbox changed. Task 2 remains uncommitted; no
task-2.3 advance, push, publication, PR or issue edit occurred.

## Cleanup

This pass created only compact planning/review receipts. It created no checkout,
build output, restore/package cache or ReSharper cache. The scoped output scan
found no bin/obj/TestResults/inspection-cache directories under the active
source/test/tool/sample/benchmark roots, and E:/oc13t22 remains absent. Earlier
policy-blocked cleanup was not retried or bypassed. Unrelated work and the
current review deliverables are preserved.
