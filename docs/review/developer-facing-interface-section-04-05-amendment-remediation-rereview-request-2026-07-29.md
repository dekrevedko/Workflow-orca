# Independent implementation re-review request — Section 4/5 Revision 8 remediation

**Date:** 2026-07-29  
**Requested verdict:** `APPROVE` or `REJECT`  
**Scope:** the complete Section 4/5 Revision 8 remediation target, with explicit re-review of the
2026-07-29 rejection findings  
**Authorization requested:** Section 4/5 remediation exit and the later start of task `6.0`

This request is an evidence packet, not an approval. Task `6.0` remains open and blocked until a
new independent implementation review approves this exact target.

## 1. Authority and immutable history

Review the target in this order:

1. `docs/specs/17-selected-mode-capability-matrix.md`;
2. `docs/specs/17-public-authoring-contract.cs`;
3. canonical requirements under `openspec/specs/`;
4. `openspec/changes/reshape-developer-facing-interfaces/tasks.md`;
5. the approved Revision 8 amendment and
   `docs/review/developer-facing-interface-amendment-revision-08-canonical-synchronization-record-2026-07-29.md`;
6. implementation plans and immutable historical review evidence.

Canonical specifications control. The superseded request, its manifest, and the immutable rejection
remain unchanged:

- `docs/review/developer-facing-interface-section-04-05-amendment-remediation-independent-review-request-2026-07-29.md`;
- `docs/review/developer-facing-interface-section-04-05-amendment-remediation-dirty-manifest-2026-07-29.txt`;
- `docs/review/developer-facing-interface-section-04-05-amendment-remediation-independent-review-verdict-2026-07-29.md`.

## 2. Frozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | `8c2dd712` is an ancestor of `HEAD` |
| Target | `HEAD` plus the complete current dirty worktree, including this request and manifest |
| Self-inclusive porcelain entries | 414 |
| Entry classes | 321 modified / 9 deleted / 84 untracked |
| Raw-manifest SHA-256 | `0E1CA051AB3ACC76C6F0DC4D5BE985A4EDCEBE30F2555DD11973314532C93F20` |
| LF-normalized sorted-status SHA-256 | `B369B0B2643E9BE4FCF06F0233B9858DF307DDEAC0F66E6266C67DA97735F846` |
| Reshape tasks | 76 complete / 60 pending / 136 total; 0 duplicate IDs |

The authoritative self-inclusive manifest is:

`docs/review/developer-facing-interface-section-04-05-amendment-remediation-rereview-dirty-manifest-2026-07-29.txt`

Reproduce it before reading conclusions and again after validation. Any target drift invalidates the
review.

## 3. Rejection findings and remediation claims

### R1 — authored `MaxItems` did not affect structural identity

The rejected target lowered `ForEachOptions.MaxItems` into admission behavior but did not retain it
in the authored/compiled `ForEach` plan or fingerprint.

The remediated target:

- retains nullable authored `MaxItems` from every public ephemeral/durable `ForEach` completion
  path through `SelectedForEachAuthoringNode<TState>` and `CompiledForEachPlan`;
- writes `MaxItems` into the structural fingerprint immediately before `MaxConcurrency`;
- keeps workflow mode excluded from structural identity;
- includes a public-surface regression proving `MaxItems=1` and `MaxItems=2` produce different
  fingerprints in each mode, while the same authored structure produces the same fingerprint
  across ephemeral and durable modes;
- checks the compiled `MaxItems` and `MaxConcurrency` values in both public modes.

### R2 — `restart-readmits-unfinished-items` had no executable driver

The remediated target adds exactly one runtime-recorded driver to the Section-5 current-physical
lane. It proves through a durable replacement-host schedule that:

- the item selector executes once and its three detached descriptors are committed before
  admission;
- mutating the source after the commit does not alter replay;
- the first host admits item 0 under host ceiling 1;
- a replacement host preserves terminal item 0 and re-admits unfinished items 1 and 2 in index
  order under host ceiling 2;
- `NextAdmissionOffset`, item-fiber bindings, retained outcomes, active waits, final indexed
  aggregation, and one merge are asserted;
- the occurrence reaches a committed terminal management snapshot.

The current-physical lane is green. The scenario deliberately remains in the final-contract
ExpectedRed lane only because Section 7 still owns the canonical package/facade relocation of its
observed public calls; it no longer fails for a missing driver or missing runtime assertion.

### R3 — validation-feed drift discovered during remediation

The first fresh restore after the rejection began failing `NU1903` because NuGet's live advisory
feed newly classified NetMQ's transitive `System.Security.Cryptography.Xml 10.0.6` as vulnerable.
The target centrally enables transitive pinning and pins that dependency to patched version
`10.0.10`. This changes no direct project dependency row or public surface. A normal restoring
solution build now succeeds with zero warnings and zero errors.

## 4. Full Section 4/5 claims that remain in scope

- One internal authoring session enforces `Open`, `JoinPending`, and `Frozen`, phase/scope lifetime
  tokens, successor-epoch join facades, session-owned configuration, and atomic root freeze.
- Completion builders consume only a concrete frozen snapshot; repeated `Build`/`TryBuild` calls
  preserve structure, ordered diagnostics, and fingerprint.
- Lifecycle diagnostics are exactly `SFE-AUTH-LIFECYCLE-001` through
  `SFE-AUTH-LIFECYCLE-005`, including primary and related authored locations.
- Rejected governed authoring operations leave the graph unchanged, and the executable portable
  intersection covers both modes and every applicable root/nested/branch/item/leased role without
  adding a public portable builder.
- Failure provenance uses runtime-created root/branch/item occurrences, preserves ordered
  aggregate/cause provenance across detachment and fixed-codec round trips, and keeps the approved
  equality semantics and discriminator allowlist.
- No live-fiber authored acceptance limit or replacement branch-width limit remains.
- Fingerprints include codec format and inspectable authored structure, including `MaxItems`, and
  exclude compiler format, mode, identity/version, and compiler options. Compiler-profile
  compatibility owns `MaxInternalInstructionsPerQuantum`.
- Tagged flattening, authored `MaxItems`, encoded-value admission, supported nested `If`, linear
  child execution, fixed-codec replay, and root-only fan-out remain conformant.
- `docs/specs/17-public-authoring-contract.cs` has no worktree diff.

## 5. Recorded validation evidence

These are claims for the reviewer to reproduce, not evidence to trust.

| Check | Recorded result |
|---|---|
| `dotnet build OrcaCore.slnx -v minimal` | 0 warnings / 0 errors |
| Core suite | 448 passed / 0 failed |
| Ephemeral suite | 165 passed / 0 failed |
| Durable suite | 305 passed / 0 failed |
| Hosting suite | 15 passed / 0 failed |
| Infrastructure guards | 69 passed / 0 failed on three consecutive isolated runs |
| Expected-red lane | 104 intentional named failures / 0 passes |
| Section 4 behavior scenarios | 4 passed / 0 failed |
| Section 5 current-physical behavior scenarios | 8 passed / 0 failed |
| Lifecycle-focused regressions | 20 passed / 0 failed |
| Public/API/member-absence guards | 11 passed / 0 failed |
| Failure provenance/value/envelope Core regressions | 19 passed / 0 failed |
| Fingerprint-named Core regressions | 9 passed / 0 failed |
| Durable failure-envelope mapper regressions | 4 passed / 0 failed |
| Structured execution — Ephemeral | 43 passed / 0 failed |
| Structured execution/replay — Durable | 48 passed / 0 failed |
| Green compile-fixture script | passed |
| Product-authoring ExpectedRed compile disposition | passed with 0 remaining product-authoring gaps |
| Strict OpenSpec validation | 17 passed / 0 failed |
| Active-product forbidden scans | 0 matches for `MaxActiveFibers`, `SFE-LIMIT-003`, `SFE-LIMIT-008`, `MaxParallelBranchesPerScope`, and `MaxConcurrentBranches` |
| Public authoring companion diff | 0 lines |
| `git diff --check` | exit 0; line-ending notices only |

The 104 ExpectedRed failures are not passing evidence. They remain mapped to later sections. The
restart scenario now also has complementary green current-physical evidence, so its final-contract
red does not represent unfinished Section-5 behavior.

## 6. Minimum independent commands

Use Windows PowerShell in this environment:

```powershell
dotnet build OrcaCore.slnx -v minimal
dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore --filter "Disposition=ExpectedRed"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
openspec.cmd validate --all --strict --no-interactive
git diff --exit-code -- docs/specs/17-public-authoring-contract.cs
git diff --check
```

The ExpectedRed guard command is expected to exit nonzero with exactly 104 named failures. The
ExpectedRed compile disposition is expected to exit zero and report zero remaining product-authoring
gaps.

Also independently:

- mutate `MaxItems` in otherwise identical public ephemeral and durable definitions and inspect
  both the compiled plan and fingerprint;
- execute `restart-readmits-unfinished-items`, inspect the committed envelope after each host
  replacement, and distinguish its green current-physical evidence from later package/facade reds;
- re-derive lifecycle locations, failure provenance, codec closure, fingerprint contributors,
  structured fan-out/replay, nested-fan-out absence, and public-member absence from source and
  behavior rather than trusting names or this packet;
- verify the transitive pin resolves `System.Security.Cryptography.Xml` to `10.0.10` without adding
  a direct project dependency or changing a package-matrix row;
- verify task accounting, zero duplicate task IDs, and that task `6.0` remains open;
- reproduce the manifest byte-for-byte before and after all checks.

## 7. Questions requiring an explicit answer

1. Does authored `MaxItems` now survive authoring/lowering and change structural identity in both
   modes while mode itself remains excluded?
2. Does `restart-readmits-unfinished-items` execute the complete replacement-host schedule and
   fail the final-contract lane only for work genuinely owned by a later section?
3. Are the original Section 4/5 lifecycle, failure-provenance, codec, fingerprint, limits, and
   structured-fan-out claims still complete after these changes?
4. Does the dependency pin repair the newly surfaced advisory without changing direct package
   ownership or public API?
5. Are all 104 ExpectedRed failures genuinely later-section gaps?
6. Does the frozen manifest reproduce before and after validation with no target drift?

## 8. Verdict instructions

Write one new dated immutable verdict under `docs/review/`. Do not edit any existing request,
manifest, verdict, source, test, spec, task, plan, status, or documentation file.

Return `APPROVE` only if this exact Section 4/5 remediation target is complete and no release blocker
remains. Approval authorizes the implementation owner to begin task `6.0` in a later turn; it does
not mark that task complete. Otherwise return `REJECT` with source-grounded blockers.
