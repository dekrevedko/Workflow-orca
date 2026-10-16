# Harmonization Task 6.1 independent review request

**Date:** 2026-08-22  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** create the coherent Task 6.1 checkpoint commit

This request freezes Task 6.1 on base `87f0b0dd1d0815e9334eb686eb52bb9c30d02513`. The target is
that `HEAD` plus every entry in
`harmonize-downstream-capability-specs-task-6-1-dirty-manifest-2026-08-22.txt`. Exact HEAD/tree,
porcelain, capability-inventory, and content-record hashes are supplied with the handoff after this
self-inclusive request and manifest are final; they are intentionally not embedded here.

An `APPROVE` verdict authorizes only the Task 6.1 checkpoint. It does not complete Tasks 6.2–8.3,
archive the harmonization change, or authorize reshape Task 8.0.

## Prior checkpoint chain to verify

- `76c6340fa003bfd209cfadce7b4ea271d9d7a50c` is the exact ten-path Task 5.3 checkpoint authorized
  by the repository owner.
- `87f0b0dd1d0815e9334eb686eb52bb9c30d02513` changes only the provenance fixture: it converts the
  active Task 5.3 freeze into an immutable historical entry with exact checkpoint/blob evidence.
- Task 5.3 is deliberately recorded as `MissingApproval`: the owner authorized the commit, but no
  immutable independent verdict exists, and the registry does not overstate one.

## Task 6.1 claims to verify

1. `docs/specs/04-requirements-core-runtime.md` contains exactly one new stable numbered
   requirement, `CR-009a Authoring sessions have one explicit lifecycle`.
2. It mirrors the canonical `workflow-authoring` lifecycle without adding semantics:
   `Open`/`JoinPending`/`Frozen`; session, epoch, and lexical-scope handle validity; successor-epoch
   join façades; atomic root-terminal freeze; immutable repeated builds; and mutation-free rejection
   of stale/superseded, duplicate-join, escaped, post-terminal, and losing concurrent operations.
3. It names the closed `SFE-AUTH-LIFECYCLE-001` through `-005` mapping already shipped in document
   17 and product diagnostics.
4. `AuthoringLifecycleTests` is now tagged with `Requirement=CR-009a`. Its incorrect historical
   `AC-021` tag is removed because `AC-021` owns lambda-mode safety; Task 6.3 will add the lifecycle
   acceptance criterion.
5. `Task61_CoreRuntimeDocumentsAuthoringSessionLifecycleAndExecutableEvidence` is a must-green,
   non-vacuous guard over the numbered requirement, canonical requirement, stable diagnostic
   catalog, and representative existing lifecycle tests.
6. The declaration crosswalk moves only by that one new `[Fact]`: 336 physical sources, 1,384
   declarations, 188 active files, 696 active declarations, and the unchanged 866 retired rows.
7. No `src/**`, public API baseline, package manifest, or canonical OpenSpec capability changes.

## Self-review and negative control

- The first guard draft stopped only at a following `###` heading. Self-review changed it to stop at
  the next Markdown heading of level two or deeper, so it cannot absorb the execution-model section.
- Renaming the numbered requirement to `CR-009b` makes the focused guard red with the exact missing
  stable-ID diagnostic. The file was restored before the final packet.
- Historical review-provenance pins were mechanically refreshed with the repository script; no
  frozen row or immutable review document was rewritten.

## Validation to reproduce

- Debug and Release full solution builds, `--no-incremental -m:1 -warnaserror`: 0 warnings / 0 errors.
- `OrcaCore.Core.Tests`: 350/350.
- infrastructure guards: 216/216.
- expected-red guards: exactly the same 14 `ExecutableBehaviorExpectedRedGuards` failures.
- OpenSpec strict: 18/18.
- harmonization ledger: 19 complete / 15 open / 34 total.
- `git diff --check`: exit 0.

## Reviewer instructions

Recompute the freeze anchors from the live target, inspect the numbered requirement against the
canonical lifecycle and source implementation, mutation-test the stable-ID/contract guard, and
confirm the crosswalk delta. Do not edit, stage, or commit the reviewed target. Return one dated
immutable `APPROVE` or `REJECT` verdict.
