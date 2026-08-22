# Harmonization Task 5.2 refreeze independent review request

**Date:** 2026-08-22

**Requested verdict:** `APPROVE` or `REJECT`

**Exact review target:** `c996e3a08f55697e1814cf84a7581c9f05473142`

**Target tree:** `d2d5bcc496bf618dbae7a960aef20754b0c0c8cb`

**Task 5.1 checkpoint:** `ff11ead781f8fef343fafc6e6bc8307d746e4a05`

**Task 5.2 content materialization:** `d0e7c4821199b8b1ee13d5f6fd22f79133abc576`

**Task 5.1 approval-evidence checkpoint:** `5140208c7b82332ada8b7a39848888ddd58eb89d`

**Approval activation checkpoint:** `c996e3a08f55697e1814cf84a7581c9f05473142`

This request asks an independent reviewer to re-review Task 5.2 on the exact immutable tree above.
The prior Task 5.2 verdicts remain immutable `REJECT` evidence: their technical checks passed, but
Task 5.1 lacked committed approval provenance. That dispositive blocker is now closed by an external
Task 5.1 `APPROVE` verdict, its distinct evidence commit, and the following mechanical activation.

An `APPROVE` verdict applies only to this exact Task 5.2 target. It does not itself start Task 5.3,
archive the harmonization change, or authorize reshape Task 8.0. Preserve every earlier request and
verdict byte-for-byte.

## Immutable chain to verify

```powershell
git rev-parse c996e3a08f55697e1814cf84a7581c9f05473142^{tree}
git rev-parse c996e3a08f55697e1814cf84a7581c9f05473142^
git rev-parse 5140208c7b82332ada8b7a39848888ddd58eb89d^
git rev-parse d0e7c4821199b8b1ee13d5f6fd22f79133abc576^
git diff-tree --no-commit-id --name-status -r d0e7c4821199b8b1ee13d5f6fd22f79133abc576
git diff-tree --no-commit-id --name-status -r 5140208c7b82332ada8b7a39848888ddd58eb89d
git diff-tree --no-commit-id --name-status -r c996e3a08f55697e1814cf84a7581c9f05473142
```

The chain must be linear: `ff11ead` → `d0e7c482` → `5140208c` → `c996e3a`.
`d0e7c482` is explicitly recorded as pre-approval Task 5.2 content, not mislabeled as an approved
checkpoint. `5140208c` must contain the external Task 5.1 verdict and green transition state.
`c996e3a` may only activate the already-existing evidence SHA and complete remediation task 5.2a.

## Task 5.2 synchronization to re-verify

Independently compare the active reshape deltas with all 14 canonical capabilities:

- exactly eight canonical operations were applied: `management-and-querying` (1),
  `quality-and-verification` (5), and `repository-foundation` (2);
- the other canonical capabilities were not changed by Task 5.2;
- every operation is delta-backed with the same `ADDED`, `MODIFIED`, or `REMOVED` disposition;
- the provenance corpus has 176 rows, 173 synchronized operations, three declared requirements
  outside canonical, zero pending operations, and zero duplicate owners;
- all 14 canonical preambles remain byte-owned by their recorded LF-normalized hashes;
- Task 5.1's synchronized-removal catalog remains permanent and contains 11 entries.

## New-finding remediation included in this review

The following four fixes were implemented after the provenance-remediation approval and are
explicitly part of this review target:

1. **N-1 — exact review-only whitespace policy.** The design, task ledger, and remediation artifact
   disclose `docs/review/**/*.md whitespace=-trailing-space`. The normative guard asserts the exact
   four-line `.gitattributes` allowlist in order. Replacing the review rule with `* -whitespace`
   must fail.
2. **N-2 — content-based Task 5.2 history.** The registry names `d0e7c482...` as a pre-approval
   content commit. The guard must rediscover it from all three owned canonical spec paths plus the
   checked 5.2 task state; removing the registry entry must fail even though the commit subject does
   not say `Checkpoint harmonization task 5.2`.
3. **N-3 — non-self-referential approval transition.** `ApprovalAwaitingEvidenceCommit` registered
   the external verdict while leaving the evidence SHA null and Task 5.2 blocked. Commit
   `5140208c...` preserved that green state. Only `c996e3a...` changed it to `Approved` and recorded
   the already-existing evidence commit. Prematurely setting `Approved` with a null SHA must fail.
4. **N-4 — maximal opportunistic byte pins.** The guard recomputes the maximal historical-row set
   still matching current bytes. Task 5.1 records 11 matches and Task 5.2 records four. Emptying
   either available set must fail.

The implementation self-review mutation-tested all four negative cases and restored a 3/3 focused
control before the evidence commit. Review the implementation and repeat the mutations rather than
accepting this statement.

## Validation to reproduce at the target

```powershell
dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror
dotnet build OrcaCore.slnx -c Debug --no-incremental -m:1 -warnaserror

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --filter "Disposition=Infrastructure" -m:1
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --filter "Disposition=ExpectedRed" -m:1
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj -c Release --no-build -m:1

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --filter "FullyQualifiedName~ReviewManifests_PreserveRawGitOrderOrDiscloseSetOnlyEvidence|FullyQualifiedName~ReviewCheckpointProvenance_BlocksTask52CheckpointUntilTask51ApprovalExists|FullyQualifiedName~CompanionBaseline_HasExactReviewedNamespaceArityAndSignatures" -m:1

openspec.cmd validate --all --strict
git diff --check ff11ead781f8fef343fafc6e6bc8307d746e4a05..c996e3a08f55697e1814cf84a7581c9f05473142
```

Expected results at the target: Release and Debug 0 warnings / 0 errors; Infrastructure 214/214;
exactly 14 intentional `ExecutableBehaviorExpectedRedGuards` failures; ProviderCertification 96/96;
focused provenance/attribute guards 3/3; OpenSpec 18/18; harmonization ledger 17 complete / 17 open /
34 total; and clean `git diff --check`.

## Verdict instructions

Create exactly one new immutable verdict at:

`docs/review/harmonize-downstream-capability-specs-task-5-2-refreeze-independent-review-verdict-2026-08-22.md`

Record the exact target commit and tree, the eight-operation sync result, the Task 5.1 approval
chain, N-1 through N-4 mutation results, validation results, and exactly one terminal `APPROVE` or
`REJECT` line. Do not edit the reviewed commits, any prior request or verdict, either historical
dirty manifest, or the machine-readable historical row data.
