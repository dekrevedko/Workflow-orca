# Harmonization Task 6.3 independent review request

**Date:** 2026-08-31  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** create the coherent Task 6.3 checkpoint commit

This request freezes Task 6.3 on base `2dbf2b94168e4338c1fa8af742573545287f2f9d`. The target
is that `HEAD` plus every entry in
`harmonize-downstream-capability-specs-task-6-3-dirty-manifest-2026-08-31.txt`. Exact HEAD/tree,
raw commit-real porcelain, capability-inventory, and scoped content-record anchors are supplied
with the handoff after this self-inclusive request, manifest, artifact, and active-freeze registry
entry are final.

An `APPROVE` verdict authorizes only the Task 6.3 checkpoint. It does not complete Tasks 6.4-8.3,
archive the harmonization change, or authorize reshape Task 8.0.

## Prior checkpoint chain to verify

- `7abf95e3d691214201027f47ee8b7e0365f715b7` is the exact independently approved fifteen-path
  Task 6.2 checkpoint.
- `bf6eb959e112dd594e0322a2dcad5e97fe217d54` records the independent verdict and remediates
  findings T-1 through T-4.
- `2dbf2b94168e4338c1fa8af742573545287f2f9d` is the mechanical evidence activation and the clean
  base for Task 6.3.

## Task 6.3 claims to verify

1. `CR-009a` links only to new `AC-028`, and `CR-014a` links only to new `AC-029`.
2. `AC-028` covers the complete authoring-session lifecycle: `Open` / `JoinPending` / `Frozen`,
   successor epochs, immutable terminal snapshot, pre-mutation rejection, and repeated-build
   stability.
3. `AC-029` covers creation-time authored/runtime provenance, unchanged one-failure propagation,
   one owning multi-cause join failure, authored-branch and dynamic-item ordering, non-negative
   indexes, the closed fixed-codec graph, and unknown/missing/malformed rejection.
4. AC-029 explicitly cites `CR-014a`, synchronized `quality-and-verification`, canonical
   `structured-fiber-execution`, and document 17's public-contract companion.
5. `AC-022` is not reused by failure provenance and remains structural-fingerprint evidence.
6. Core authoring/failure tests and the active Ephemeral/Durable runtime regressions carry the
   exact new AC traits; the repository catalog guards and exact CI step make them executable.
7. `Task63_AcceptanceCriteriaMapLifecycleAndFailureProvenanceBidirectionally` is a must-green,
   non-vacuous guard over the complete mapping and source attribution.
8. The crosswalk advances only by the guard fact to 337 physical sources / 1,387 declarations and
   189 active files / 699 active declarations; the retired ledger remains 866.
9. No `src/**`, `openspec/specs/**`, public API baseline, or package manifest changes.

## Self-review and negative controls

- Five isolated, byte-restored mutations made the focused Task 6.3 guard red: lost requirement
  backlink, weakened AC binding, lost runtime trait, narrowed CI filter, and lost normative source
  citation. The restored control is green.
- Self-review corrected the initial omission of companion-source links from AC-029 itself.
- The first broad Infrastructure run found Task 6.2 still locating the open Task 6.3 checkbox. The
  final target requires the completed heading, was rebuilt, and runs 218/218.
- Historical current-worktree pins were refreshed only by the repository script; immutable review
  artifacts and committed projections were not rewritten.

## Validation to reproduce

- Debug and Release full solution builds, `--no-incremental -m:1 -warnaserror`: 0 warnings /
  0 errors.
- Focused AC evidence: Core 28/28, Ephemeral 1/1, Durable 1/1.
- Full suites: Core 350/350, Ephemeral 79/79, Durable 99/99, Acceptance 37/37, Hosting 24/24,
  ProviderCertification 96/96.
- PostgreSQL 101/101, SQL Server 72/72, Integration 11/11 (run provider/container lanes
  sequentially).
- Infrastructure guards 218/218.
- Expected-red guards: exactly the same 14 `ExecutableBehaviorExpectedRedGuards` failures.
- Full guards: 218 passed / 14 expected red / 232 total.
- OpenSpec strict 18/18.
- Harmonization ledger: 21 complete / 13 open / 34 total.
- `git diff --check`: exit 0; zero `src/**` and zero `openspec/specs/**` entries.

## Reviewer instructions

Recompute the freeze anchors from the live commit-real target. Compare AC-028/AC-029 against both
numbered requirements and every named normative companion. Confirm all three AC filters discover
tests, mutation-test the bidirectional links, traits, source citations, and exact CI step, and
recompute the crosswalk delta. Do not edit, stage, or commit the reviewed target. Return one dated
immutable `APPROVE` or `REJECT` verdict.
