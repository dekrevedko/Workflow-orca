# Harmonization Task 6.2 independent review request

**Date:** 2026-08-31  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** create the coherent Task 6.2 checkpoint commit

This request freezes Task 6.2 on base `532940a3865b8eec85c315dca9da09b874cb4173`. The target is
that `HEAD` plus every entry in
`harmonize-downstream-capability-specs-task-6-2-dirty-manifest-2026-08-31.txt`. Exact HEAD/tree,
raw porcelain, capability-inventory, and scoped content-record anchors are supplied with the handoff
after this self-inclusive request, manifest, artifact, and active-freeze registry entry are final.

An `APPROVE` verdict authorizes only the Task 6.2 checkpoint. It does not complete Tasks 6.3-8.3,
archive the harmonization change, or authorize reshape Task 8.0.

## Prior checkpoint chain to verify

- `87da8c2b1430cc388e60412692415aedbe4c2196` is the exact fourteen-path Task 6.1
  post-review-hardening checkpoint authorized by the repository owner.
- `532940a3865b8eec85c315dca9da09b874cb4173` is the separate two-path evidence transition that
  archives that freeze and records the owner's bounded authorization without calling it independent
  review evidence.
- Task 6.2 starts from that clean checkpoint and contains no product-source change.

## Task 6.2 claims to verify

1. `docs/specs/04-requirements-core-runtime.md` contains exactly one new stable numbered
   requirement, `CR-014a Workflow failures retain authored and runtime occurrence provenance`.
2. It mirrors the approved canonical `quality-and-verification` requirement without adding
   semantics: one immutable `AuthoredLocation`; one runtime-created closed
   `Root`/`Branch(AuthoredBranchId)`/`Item(index)` occurrence; creation-time attachment;
   unchanged one-failure propagation; ordered multi-cause aggregation; and versioned
   `orcacore-json-v1` round-trip with malformed/unknown rejection.
3. `FailureProvenanceTests` is tagged `Requirement=CR-014a`; its unrelated historical
   `AC-022` tag is removed. `AC-022` is truthfully relocated to the existing structural-change
   and opaque-capture fingerprint tests, so the acceptance catalog remains green without
   mislabeling failure provenance.
4. Active ephemeral and durable runtime regressions both execute a failing authored parallel branch,
   assert its exact authored location and `FailureOccurrence.Branch`, and assert authored outcome
   order. The durable test also reads the persisted checkpoint state through
   `DurableExecutionEnvelopeV2`.
5. `Task62_CoreRuntimeDocumentsWorkflowFailureProvenanceAndExecutableEvidence` is a must-green,
   non-vacuous guard over the numbered requirement, synchronized canonical/reshape requirement,
   Core codec/union/aggregation tests, truthful `AC-022` evidence, compile-included runtime tests,
   and the exact three-project CI lane.
6. The declaration crosswalk advances exactly to 337 physical sources / 1,386 declarations and
   189 active files / 698 active declarations; the 866-row retired ledger remains unchanged.
7. No `src/**`, public API baseline, package manifest, or canonical OpenSpec capability changes.

## Self-review and negative controls

- The first draft reused source-only legacy durable evidence. Focused test discovery exposed that the
  files were compile-excluded, so the draft was discarded. The final target adds one current,
  compile-included durable runtime regression and makes the guard fail if either runtime evidence
  file is moved behind `Compile Remove`.
- Mutation control: adding
  `<Compile Remove="Driver\DurableFailureProvenanceTests.cs" />` makes the focused guard red with
  the exact compile-included diagnostic; the project file was restored byte-for-byte.
- The broad Core lane exposed that removing the false `AC-022` tag left the real acceptance
  criterion uncovered. The target now relocates that tag to the two existing fingerprint tests and
  the structural guard pins the corrected ownership.
- Historical current-worktree pins were mechanically refreshed with the repository script; immutable
  commit projections and prior review artifacts were not rewritten.

## Validation to reproduce

- Debug and Release full solution builds, `--no-incremental -m:1 -warnaserror`: 0 warnings /
  0 errors.
- Focused `Requirement=CR-014a`: Core 8/8, Ephemeral 1/1, Durable 1/1.
- Full suites: Core 350/350, Ephemeral 79/79, Durable 99/99, Acceptance 37/37, Hosting 24/24,
  ProviderCertification 96/96.
- Infrastructure guards: 217/217.
- Expected-red guards: exactly the same 14 `ExecutableBehaviorExpectedRedGuards` failures.
- Full guards: 217 passed / 14 expected red / 231 total.
- OpenSpec strict: 18/18.
- Harmonization ledger: 20 complete / 14 open / 34 total.
- `git diff --check`: exit 0; zero `src/**` entries.

## Reviewer instructions

Recompute the freeze anchors from the live target. Compare `CR-014a` with the synchronized
canonical/reshape requirement and the runtime construction/codec implementation. Confirm all three
requirement filters discover tests, mutation-test the compile-included and stable-ID/contract pins,
verify the `AC-022` relocation, and recompute the crosswalk delta. Do not edit, stage, or commit the
reviewed target. Return one dated immutable `APPROVE` or `REJECT` verdict.
