# Independent implementation review verdict — Section 4/5 Revision 8 remediation

**Date:** 2026-07-29  
**Verdict:** `REJECT`  
**Scope reviewed:** tasks `4.16`–`4.21`, `5.11`, and `5.13`–`5.15`, including the
claimed Section 4/5 exit state  
**Authorization:** task `6.0` remains open and blocked

## 1. Decision

The target is not ready for Section 4/5 exit. Two release-gate blockers remain:

1. the structural fingerprint does not include the authored
   `ForEachOptions.MaxItems` value; and
2. the supposedly later-section-only ExpectedRed lane still contains the Section 5
   `restart-readmits-unfinished-items` scenario because no executable scenario driver exists.

The broad build and test lanes are green, but they do not exercise the first defect and the second
defect directly contradicts the request's claim that every remaining ExpectedRed failure belongs to
a later section.

## 2. Frozen-target provenance

The requested target reproduced before review and again after all validation, before this verdict
was written.

| Item | Reproduced result |
|---|---|
| Branch | `feature/v3-rebuild` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline ancestry | `8c2dd712` is an ancestor of `HEAD` |
| Porcelain entries | 410 |
| Entry classes | 320 modified / 9 deleted / 81 untracked |
| Manifest byte comparison | exact |
| Raw manifest SHA-256 | `38187E3EE5A56E957AE97B10D177C032F2E540F067ECD73BCED3DF67FD6A0343` |
| LF-normalized sorted-status SHA-256 | `9EFFE2BDE4B7DEC3C845BC8484AA21928BA02310F4DBC05800F275D0AB853BF9` |

No target drift occurred during build, tests, scans, probes, compile-fixture execution, or OpenSpec
validation.

## 3. Gate-blocking findings

### R1 — P1: authored `ForEachOptions.MaxItems` is absent from the structural fingerprint

Canonical requirements say the fingerprint covers inspectable authored structure and codec format,
including authored node/member kinds, ordering, strong values, referenced types, and static request
values:

- `docs/specs/17-selected-mode-capability-matrix.md:484`;
- `docs/specs/04-requirements-core-runtime.md:41`;
- `openspec/specs/workflow-contracts/spec.md:91`; and
- `openspec/specs/quality-and-verification/spec.md:176`.

`ForEachOptions.MaxItems` is public, immutable, supplied at the `ForEach` authoring call, and changes
the admission behavior of the authored node. It is therefore inspectable authored structure, not a
host/compiler acceptance option.

The current implementation loses it:

- `src/OrcaCore.Abstractions/Instances/AuthoringValues.cs:4` exposes `MaxItems`;
- `src/OrcaCore.Core/Building/PublicStagedAuthoring.cs:232` captures the value only inside an
  opaque selector wrapper;
- `src/OrcaCore.Core/Building/SelectedWorkflowAuthoring.cs:152` does not retain `MaxItems` on
  `SelectedForEachAuthoringNode`; and
- `src/OrcaCore.Core/Compilation/DefinitionCompiler.Fingerprint.cs:85` hashes the item/state/result
  types, join/failure policies, `MaxConcurrency`, partitioner, and body, but not `MaxItems`.

An independent clean probe built otherwise identical public ephemeral definitions under the same
`DefinitionId` and `DefinitionVersion`:

```text
maxItems=1 F34E143CF003A23322F1E9409A9CE75B08EFB786C1E2C1CD7892ABED7055391B
maxItems=2 F34E143CF003A23322F1E9409A9CE75B08EFB786C1E2C1CD7892ABED7055391B
maxItems-value-equal=True
maxConcurrency=1 67667575A36FE01CAFAD544B51BCCB61E575F358F2EFC456E1092F374BA3163A
maxConcurrency=2 D039A5B77D25F78E252ED108E1D1616226A037413A3518CAC891BEBA5E9C71C5
maxConcurrency-value-equal=False
```

This permits an inspectable admission-semantics change under an unchanged structural fingerprint.
It invalidates task `5.14` and the request's claim that each approved fingerprint contributor was
mutated independently.

Required remediation:

1. retain authored `MaxItems` in the authoring/compiled structure;
2. include it in the structural fingerprint in both modes; and
3. add an explicit regression proving a `MaxItems`-only mutation changes the fingerprint while
   compiler/runtime acceptance options remain non-contributors.

### R2 — P1: a completed Section 5 behavior scenario remains ExpectedRed with no driver

The ExpectedRed lane reproduces the recorded aggregate count:

```text
Failed: 104, Passed: 0, Skipped: 0, Total: 104
```

However, those 104 failures are not all later-section gaps. One is:

```text
3.6/restart-readmits-unfinished-items must have one reviewable executable driver in
OrcaCore.DeveloperSurface.BehaviorScenarios, but the collection is empty.
```

This is a Section 5 scenario:

- `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/structured-fanout-scenarios.json:10` assigns it
  to tasks `5.8`–`5.9`;
- both tasks are marked complete, and
  `openspec/changes/reshape-developer-facing-interfaces/tasks.md:76` explicitly claims restart
  re-admission green in both engines;
- `tests/OrcaCore.DeveloperSurface.Guards/ExecutableBehaviorContractGuards.cs:22` lists only seven
  Section 5 scenario IDs and omits this scenario; and
- `tests/OrcaCore.DeveloperSurface.BehaviorScenarios/StructuredFanoutScenarioHost.cs` contains the
  seven recorded Section 5 drivers but no restart re-admission driver.

There is useful lower-level durable coverage, including
`SelectedForEach_ReplacementHostUsesCommittedItemSnapshotWithoutReselectingItems`, but the review
request explicitly requires every Section 4/5 behavior scenario to execute and requires no
Section 4/5 scenario to remain red. A lower-level test does not satisfy the missing public behavior
certification.

Required remediation:

1. add the public executable restart/readmission driver, including a replacement host with a
   changed host ceiling, committed snapshot reuse, terminal-item preservation, and deterministic
   unfinished-item re-admission;
2. include it in the current-physical-owner Section 5 infrastructure lane; and
3. leave only its genuinely later package-owner seam ExpectedRed, if applicable.

## 4. Validation evidence

| Check | Independent result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore -m:1 -v minimal` | passed; 0 warnings / 0 errors |
| Core suite | 447 passed / 0 failed |
| Ephemeral suite | 165 passed / 0 failed |
| Durable suite | 305 passed / 0 failed |
| Hosting suite | 15 passed / 0 failed |
| Infrastructure guards | 68 passed / 0 failed |
| ExpectedRed lane | 104 failed / 0 passed; aggregate count reproduced, classification rejected by R2 |
| Section 4 behavior scenarios | 4 passed / 0 failed |
| Recorded Section 5 behavior scenarios | 7 passed / 0 failed; incomplete scenario set per R2 |
| Lifecycle-focused regressions | 20 passed / 0 failed |
| Focused failure/compiler/model regressions | 46 passed / 0 failed |
| Focused durable envelope/graph-position regressions | 11 passed / 0 failed |
| Wide fan-out and public `ForEach` focus — ephemeral | 6 passed / 0 failed |
| Wide fan-out and public `ForEach` focus — durable | 6 passed / 0 failed |
| Portable-intersection and fixed-codec closure guards | 6 passed / 0 failed |
| Green compile fixtures | passed; fresh package, positive consumers, 26 + 26 forbidden calls, negative package |
| ExpectedRed compile disposition | passed; 0 remaining |
| Strict OpenSpec validation | 17 passed / 0 failed |
| Public authoring companion diff | no diff |
| `git diff --check` | exit 0; line-ending notices only |
| Reshape task accounting | 76 complete / 60 pending / 136 total; 0 duplicate IDs |
| Task `6.0` | open |

## 5. Required-question answers

1. **Lifecycle phase/scope rules:** **Yes**, against the canonical
   `SFE-AUTH-LIFECYCLE-001`–`005` catalog. The focused lifecycle lane passed 20/20, including exact
   primary/related locations and unchanged graph checks.
2. **Frozen completion snapshots:** **Yes.** Completion builders hold a concrete frozen build result
   and repeated build/validation checks are stable.
3. **Public surface and portable parity:** **Yes.** The declaration companion has no diff, the
   compile fixtures cover the concrete roles, and no public portable builder was added.
4. **Failure occurrence/provenance:** **Yes.** The union is externally non-derivable and
   runtime-created, the codec allowlist is closed, and reference/value equality behavior is
   preserved by the focused tests.
5. **Retired live-fiber limits:** **Yes.** Active product scans found no `MaxActiveFibers`,
   `SFE-LIMIT-003`, `SFE-LIMIT-008`, or replacement branch-width/live-fiber acceptance limit.
6. **Fingerprint and durable compiler profile:** **No.** Compiler-profile compatibility is covered,
   and the prohibited non-contributors are excluded, but the approved authored `MaxItems`
   contributor is missing (R1).
7. **Fan-out, bounds, nested `If`, linear execution, and replay:** **No for exit-gate purposes.**
   The product suites and focused wide/bound/tagged tests are green, but the required public
   restart/readmission behavior scenario is absent (R2).
8. **All 104 ExpectedRed failures are later-section gaps:** **No.**
   `restart-readmits-unfinished-items` is assigned only to completed Section 5 tasks and has no
   executable driver.
9. **Frozen manifest before and after validation:** **Yes.** It reproduced byte-for-byte with both
   recorded hashes and no target drift.

## 6. Non-blocking packet defects

- The request calls the lifecycle diagnostics `SFE-AUTH-001`–`005`; canonical authority and product
  use `SFE-AUTH-LIFECYCLE-001`–`005`. This review used the canonical names.
- The request names a nonexistent
  `tests/OrcaCore.DeveloperSurface.Guards/run-expected-red-compile-fixtures.ps1`. The repository's
  actual established command is
  `run-compile-fixtures.ps1 -Disposition ExpectedRed`, and it passed with zero remaining fixtures.

These packet defects should be corrected in a future request but are not additional product
blockers.

## 7. Gate effect

`REJECT`. Do not begin task `6.0`. Preserve this verdict as immutable evidence, remediate R1 and R2,
produce a new self-inclusive frozen manifest and dated request, and obtain a fresh independent
approval.

## 8. Post-write attestation

Before writing this verdict, the frozen 410-entry manifest still reproduced exactly after all
validation. After writing, the working tree contains one additional untracked entry only:

`docs/review/developer-facing-interface-section-04-05-amendment-remediation-independent-review-verdict-2026-07-29.md`

Post-write entry count: `411`  
Post-write LF-normalized sorted-status SHA-256:
`B61B9C238323A10B29DDE39E9D277FB00EA9C8139F40F4B987AB4C0875C2BE7C`
