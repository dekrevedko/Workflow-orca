# Harmonization Task 6.4 independent review request

**Date:** 2026-09-01
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create the coherent Task 6.4 checkpoint commit

This request freezes Task 6.4 on base `c23dc4e732881fb2ddb7792836e740d72ce2644f`. The target
is that `HEAD` plus every entry in
`harmonize-downstream-capability-specs-task-6-4-dirty-manifest-2026-09-01.txt`. Exact HEAD/tree,
raw commit-real porcelain, capability-inventory, and scoped content-record anchors are supplied
with the handoff after this self-inclusive request, manifest, artifact, and active-freeze registry
entry are final.

An `APPROVE` verdict authorizes only the Task 6.4 checkpoint. It does not complete Tasks 6.5-8.3,
archive the harmonization change, or authorize reshape Task 8.0.

## Prior checkpoint chain to verify

- `609e9c4523bd0db06fff42d01ab3e65db2e52964` is the exact independently approved sixteen-path
  Task 6.3 checkpoint.
- `db0ae75c715c83bd0a45d4b94b6ef6b8310a278d` records the independent verdict and closes U-1
  through U-4: immutable `CR-009a`, resolved companion anchors, corrected task-7.20 provenance,
  and order-independent trait evidence.
- `c23dc4e732881fb2ddb7792836e740d72ce2644f` mechanically activates the Task 6.3 approval evidence
  and is the clean base for Task 6.4.

## Task 6.4 claims to verify

1. `docs/specs/18-semantic-appendix.md` retains `MaxActiveFibers` only in the explicitly
   non-normative `Deliberately excluded claims` section and immediately states that Task 5.13
   removed that quantity.
2. Current product source contains zero `MaxActiveFibers` occurrences.
3. Every non-review documentation occurrence is exactly inventoried: the semantic appendix,
   Task 6.4's completion record and disposition artifact, three explicit removal owners in reshape, and the dated historical
   amendment.
4. `Task64_MaxActiveFibersMentionsAreHistoricalOrExplicitlyNegative` is a must-green,
   non-vacuous corpus guard that rejects any additional active-document or product-source mention.
5. Review records under `docs/review/` remain historical evidence and are deliberately outside the
   active-document inventory.
6. The declaration crosswalk advances only for the new guard fact: 337 physical sources / 1,388
   declarations and 189 active files / 700 active declarations; the retired ledger remains 866.
7. No `src/**`, `openspec/specs/**`, public API baseline, or package-manifest change exists.

## Findings remediated before this review

The prior Task 6.3 review's U-1 through U-4 are already committed in `db0ae75c`: symmetric
whole-block immutability for `CR-009a`, exact Markdown target and heading resolution for AC-029's
three companions, the corrected 7.20 provenance sentence naming Task 6.3, and order-independent
class-trait verification.

Task 6.4 self-review found one additional inherited coupling: the Task 6.2 guard terminated Task
6.3 only at an unchecked `6.4` checkbox. Checking off Task 6.4 therefore made Infrastructure red.
The target now recognizes the Task 6.4 boundary independent of checkbox state, while Task 6.4's
own guard still requires completion. The first broad run demonstrated the red state; the rebuilt
lane is 219/219.

## Negative controls

Five isolated, byte-restored controls were exercised:

1. adding a product-source `MaxActiveFibers` occurrence made Task 6.4 red;
2. adding an active-document occurrence made Task 6.4 red;
3. removing the semantic appendix's negative/excluded disposition made Task 6.4 red;
4. reopening Task 6.4 made Task 6.4 red; and
5. adding a historical `docs/review/` occurrence stayed green.

The restored control is green. The full Infrastructure run additionally exposed and then verified
the Task 6.2 boundary correction described above.

## Validation to reproduce

- Debug and Release full solution builds, `--no-incremental -m:1 -warnaserror`: 0 warnings /
  0 errors.
- Core 350/350; Ephemeral 79/79; Durable 99/99; Acceptance 37/37; Hosting 24/24;
  ProviderCertification 96/96.
- PostgreSQL 101/101, SQL Server 72/72, Integration 11/11, run sequentially.
- Infrastructure guards 219/219.
- Expected-red guards: exactly the same 14 `ExecutableBehaviorExpectedRedGuards` failures.
- Full guards: 219 passed / 14 expected red / 233 total.
- OpenSpec strict 18/18.
- Harmonization ledger: 22 complete / 12 open / 34 total.
- `git diff --check`: exit 0; zero `src/**` and zero `openspec/specs/**` entries.

## Reviewer instructions

Recompute the freeze anchors from the live commit-real target. Inspect every `MaxActiveFibers`
occurrence rather than trusting the expected inventory. Mutation-test product source, active docs,
loss of the negative appendix disposition, Task 6.4's checkbox, and the Task 6.3-to-6.4 boundary.
Confirm review-history exclusions cannot hide an active claim, recompute the crosswalk delta, and
verify the full must-green/expected-red split. Do not edit, stage, or commit the reviewed target.
Return one dated immutable `APPROVE` or `REJECT` verdict.