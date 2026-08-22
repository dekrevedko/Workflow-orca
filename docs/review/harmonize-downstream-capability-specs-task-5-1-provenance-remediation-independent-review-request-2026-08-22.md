# Harmonization Task 5.1 provenance-remediation independent review request

**Date:** 2026-08-22

**Requested verdict:** `APPROVE` or `REJECT`

**Task 5.1 checkpoint:** `ff11ead781f8fef343fafc6e6bc8307d746e4a05`

**Remediation checkpoint:** `d0e7c4821199b8b1ee13d5f6fd22f79133abc576`

**Remediation tree:** `ef8f948b4e50d7b784c16e148f9ea1ec9c669bfc`

**Remediation parent:** `ff11ead781f8fef343fafc6e6bc8307d746e4a05`

This request asks an independent reviewer to decide whether the immutable Task 5.1 checkpoint is
approved after its clean-checkout and review-provenance defects were closed by the immediately
following remediation checkpoint. The two existing Task 5.1 `REJECT` verdicts and both Task 5.2
`REJECT` verdicts are immutable evidence and must remain unchanged.

An `APPROVE` verdict supplies only the missing Task 5.1 approval evidence required by task 5.2a. It
does not approve Task 5.2, authorize a Task 5.2 checkpoint, start Task 5.3, close the harmonization
gate, archive either change, or authorize reshape Task 8.0. After approval, the repository owner
must commit the new verdict as a distinct approval-evidence checkpoint before Task 5.2 is refrozen.

## Immutable commit chain

Verify both objects directly:

```powershell
git rev-parse ff11ead781f8fef343fafc6e6bc8307d746e4a05^{tree}
git rev-parse d0e7c4821199b8b1ee13d5f6fd22f79133abc576^
git rev-parse d0e7c4821199b8b1ee13d5f6fd22f79133abc576^{tree}
git diff-tree --no-commit-id --name-status -r d0e7c4821199b8b1ee13d5f6fd22f79133abc576
```

The remediation parent and tree must match the anchors above. Its changed-path set must contain
exactly 25 paths. Four canonical specs that raw status intermittently reported as modified are
byte-identical to `ff11ead` and correctly absent from the commit:

- `openspec/specs/event-driven-prototype/spec.md`
- `openspec/specs/runtime-resource-governance/spec.md`
- `openspec/specs/saga-orchestration/spec.md`
- `openspec/specs/structured-fiber-execution/spec.md`

## Findings the remediation must close

1. **Clean-checkout infrastructure:** retired-project discovery must treat an absent retired root as
   absent rather than throwing `DirectoryNotFoundException`; historical crosswalk discovery must
   drain `git archive` before parsing its TAR payload.
2. **Line-ending reproducibility:** `.gitattributes` must pin the public-authoring companion,
   canonical OpenSpec markdown, and the Section 7 declaration crosswalk to LF, with direct byte
   assertions proving no CR bytes survive.
3. **Historical content records:** schema 4 must reconstruct Task 5.1 as 2,428 bytes / SHA-256
   `741cfd6bbdd46cb4390c2f40c0d21d81d35b3e3749438b38efda44f26da1ff72`
   and Task 5.2 as 1,793 bytes / SHA-256
   `0068973b0dbd4c1f79086a0262cefe62b728911362b5cd43fe472e0c7daebc8a`
   from exact status/path/byte/hash rows. Task 5.1 must equal the raw commit-blob projection.
4. **Review-state integrity:** Task 5.1 remains `Rejected` until one external `APPROVE` exists;
   every recorded verdict is byte-hashed; no Task 5.2 checkpoint may exist or bypass the future
   approval-evidence commit.
5. **Commit-real freezes (new review finding):** the freeze projection must retain only paths in
   tracked content diffs plus untracked files. Mutation-test removal of that filter; it must re-admit
   the synthetic status-only phantom and fail. Confirm the remediation commit contains 25 paths,
   not the unstable 29-entry raw-status view.
6. **Opportunistic Task 5.2 evidence (new review observation):** the four historical rows still
   byte-identical in the current tree—the Task 4.3 artifact, post-gate fixture, Task 5.2 dirty
   manifest, and Task 5.2 request—must be pinned against current bytes. A coherently rehashed row
   must pass aggregate reconstruction and then fail this current-byte check.

The complete disposition is recorded in
`openspec/changes/harmonize-downstream-capability-specs/artifacts/task-5-2-review-remediation-2026-08-21.md`.
Review the implementation rather than accepting the artifact's claims.

## Validation to reproduce at the remediation checkpoint

Run from a clean checkout of `d0e7c4821199b8b1ee13d5f6fd22f79133abc576`, seeding the package
feed prerequisite when the guard diagnostics request it:

```powershell
dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror
dotnet build OrcaCore.slnx -c Debug --no-incremental -m:1 -warnaserror

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=ExpectedRed"
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj -c Release --no-build --no-restore

openspec.cmd validate --all --strict
git diff --check
```

Expected results: Release and Debug 0 warnings / 0 errors; Infrastructure 214/214; exactly 14
intentional `ExecutableBehaviorExpectedRedGuards` failures; ProviderCertification 96/96; OpenSpec
18/18; harmonization ledger 16 complete / 18 open / 34 total; and clean `git diff --check`.

Also run the two focused provenance guards and independently mutation-test findings 5 and 6:

```powershell
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ReviewManifests_PreserveRawGitOrderOrDiscloseSetOnlyEvidence|FullyQualifiedName~ReviewCheckpointProvenance_BlocksTask52CheckpointUntilTask51ApprovalExists"
```

## Verdict instructions

Create exactly one new immutable verdict at:

`docs/review/harmonize-downstream-capability-specs-task-5-1-provenance-remediation-independent-review-verdict-2026-08-22.md`

Record both commit identities, the exact 25-path remediation scope, the two new finding
dispositions, mutation results, validation results, and exactly one terminal `APPROVE` or `REJECT`
line. Do not edit either reviewed commit, any prior request or verdict, the historical Task 5.1 or
Task 5.2 manifest, or Task 5.2's rejected target.

