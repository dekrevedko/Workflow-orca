# Independent implementation re-review verdict — Section 4/5 Revision 8 remediation

**Date:** 2026-07-29  
**Repository:** `X:\Projects\GitHub\Workflow-orca`  
**Branch:** `feature/v3-rebuild`  
**Scope:** the exact 414-entry target frozen by
`developer-facing-interface-section-04-05-amendment-remediation-rereview-dirty-manifest-2026-07-29.txt`  
**Disposition:** **APPROVE**

## 1. Verdict

The Section 4/5 remediation target is complete. The two blockers in the earlier immutable
rejection are closed, the newly surfaced dependency advisory is resolved without changing public
package ownership, and no new release blocker was found.

This approval authorizes the implementation owner to begin task `6.0` in a later turn. It does not
mark task `6.0` complete and it does not authorize work beyond that task's own planning gate.

The earlier request, frozen manifest, and rejection verdict remain unchanged as historical
evidence. This file is the new dated verdict for the remediated target.

## 2. Frozen target and review integrity

| Item | Independently reproduced value |
|---|---|
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Frozen entries | 414 |
| Entry classes | 321 modified / 9 deleted / 84 untracked |
| Raw-manifest SHA-256 | `0E1CA051AB3ACC76C6F0DC4D5BE985A4EDCEBE30F2555DD11973314532C93F20` |
| LF-normalized sorted-status SHA-256 | `B369B0B2643E9BE4FCF06F0233B9858DF307DDEAC0F66E6266C67DA97735F846` |
| Before validation | byte-for-byte match; zero missing or additional entries |
| After all validation, before this verdict | byte-for-byte match; zero missing or additional entries |

Creating this verdict intentionally adds this one new untracked review artifact after the frozen
target was validated. No frozen target file was edited by the reviewer.

## 3. Earlier rejection findings

### R1 — authored `MaxItems` structural identity: closed

The public ephemeral and durable paths now retain `ForEachOptions.MaxItems` through the selected
authoring node and compiled `ForEach` plan:

- `src/OrcaCore.Core/Building/SelectedWorkflowAuthoring.cs:162`;
- `src/OrcaCore.Core/Building/PublicStagedAuthoring.cs:233-253,425-445`;
- `src/OrcaCore.Core/Compilation/CompiledPlanModels.cs:300`; and
- `src/OrcaCore.Core/Compilation/DefinitionCompiler.ForEach.cs:63`.

`DefinitionCompiler.Fingerprint.cs:92` writes `MaxItems` immediately before `MaxConcurrency`.
The focused compiled-plan test at
`tests/OrcaCore.Core.Tests/Building/StagedWorkflowBuilderTests.cs:210` proves both modes retain
`MaxItems = 4`. The fingerprint regression at
`tests/OrcaCore.Core.Tests/Compilation/DefinitionCompilerTests.cs:548` independently varies
`MaxItems` between 1 and 2 in both public modes, proves different authored values have different
fingerprints, and proves equal structures remain equal across modes. Both focused tests and the
full nine-test fingerprint slice pass.

Mode therefore remains excluded while authored `MaxItems` is now a structural contributor.

### R2 — `restart-readmits-unfinished-items` executable proof: closed

`StructuredFanoutScenarioHost.RestartReadmitsUnfinishedItems` at
`tests/OrcaCore.DeveloperSurface.BehaviorScenarios/StructuredFanoutScenarioHost.cs:496` is a
complete replacement-host driver. It:

1. commits three detached descriptors while host ceiling 1 admits only item 0;
2. mutates the original source after commit;
3. replaces the host with ceiling 2;
4. preserves terminal item 0 and re-admits items 1 and 2 in index order from the committed
   descriptors;
5. inspects `NextAdmissionOffset`, item-fiber bindings, outcomes, and active waits;
6. replaces the host again with ceiling 1, completes the remaining items, and reads the committed
   terminal management snapshot; and
7. proves one selector call, one merge, and final results
   `["0:zero", "1:one", "2:two"]`.

The scenario is included in the eight-case Section 5 current-physical lane at
`ExecutableBehaviorContractGuards.cs:22-31`, whose 8/8 run passes. A display-name-isolated run
executes both applicable tests: the current-physical case passes, while the final-contract case
fails only because the observed call is still owned by assembly `OrcaCore.Core` rather than the
future canonical `OrcaCore` assembly. The remapping used solely for current physical
certification is explicit at `ExecutableBehaviorContractGuards.cs:469-495`. That assembly-owner
relocation belongs to Section 7; the red no longer reports a missing driver or incomplete runtime
schedule.

### R3 — live NuGet advisory drift: closed

`Directory.Packages.props:4` enables central transitive pinning and line 31 pins
`System.Security.Cryptography.Xml` to `10.0.10`.

Independent checks found:

- all four resolved project asset graphs that contain this dependency resolve exactly
  `System.Security.Cryptography.Xml/10.0.10`;
- no `.csproj` contains a direct `PackageReference` for it;
- the only active project/props/targets declaration is the central `PackageVersion`;
- the selected-mode package matrix contains no new row for it;
- the restoring solution build emits zero NuGet or compiler warnings; and
- `dotnet list OrcaCore.slnx package --vulnerable --include-transitive --no-restore`, using the
  live NuGet source, reports no vulnerable packages for all 32 solution projects.

The NuGet package page also shows `10.0.10` without the high-severity vulnerability marker present
on the affected earlier `10.0.x` versions:
`https://www.nuget.org/packages/System.Security.Cryptography.Xml/10.0.10`.

This is a dependency-resolution repair, not a direct package-ownership or public-API change.

## 4. Original Section 4/5 claims

No regression was found in the original lifecycle, completion snapshot, public authoring,
failure-provenance, fixed-codec, fingerprint, retired-limit, or structured-fan-out claims.

The evidence is not based only on test names:

- the lifecycle source and 20-case focused slice cover phase/scope tokens, successor epochs,
  single-use joins, frozen snapshots, expired callback handles, graph-preserving rejection,
  concurrent authoring, and session-owned configuration;
- the public declaration companion has no worktree diff, the public/member-absence slice is 11/11,
  and the fresh package compile fixtures prove the positive surface plus 26 source and 26 packed
  forbidden-member diagnostics;
- the 19-case provenance/value/envelope slice and four durable envelope-mapper cases cover the
  closed runtime-created occurrence union, detached value preservation, ordered causes, fixed
  discriminator closure, and recursive envelope round trips;
- the nine fingerprint cases cover codec format, public `MaxItems`, inspectable graph structure,
  and the exclusion of mode, identity/version, compiler format/options, and opaque delegates;
- the active product has zero matches for `MaxActiveFibers`, `SFE-LIMIT-003`,
  `SFE-LIMIT-008`, `MaxParallelBranchesPerScope`, and `MaxConcurrentBranches`; and
- the 43-case ephemeral and 48-case durable structured-execution/replay slices pass, including
  bounds, encoded-value admission, tagged flattening, nested `If`, cancellation/termination
  fencing, fixed-codec replay, committed item snapshots, and replacement-host behavior.

## 5. Expected-red classification

The ExpectedRed command exits 1 with exactly:

```text
Failed: 104, Passed: 0, Skipped: 0, Total: 104
```

These failures are deliberately separate from passing evidence. Independent enumeration produces:

- 91 executable-behavior reds:
  - 83 behavior scenarios genuinely assigned to later implementation sections; and
  - eight Section 5 scenarios whose complete current-physical drivers pass, but whose final
    `OrcaCore` package owner remains a Section 7 relocation;
- 13 named later-section contract/package/provider reds:
  - application journey 1;
  - DAG 1;
  - facade/hosting 1;
  - lease authoring/exit 1;
  - lease discovery/governance 2;
  - normative final-contract guards 5;
  - package consumers 1; and
  - provider authoring 1.

Within the 83 later behavior scenarios, the only six entries originating in the Section 4/5
fixture families are explicitly split requirements: five Section 4 value/snapshot/output/projection
contracts retain Section 7 package/facade ownership, and
`ancestor-terminal-suppresses-merge` retains its Section 6 deadline race. No completed Section 4/5
runtime behavior remains unproved.

All 104 are therefore genuine later-section gaps; none is counted as a pass.

## 6. Independent validation

| Check | Independent result |
|---|---|
| Restoring `dotnet build OrcaCore.slnx -v minimal` | passed; 0 warnings / 0 errors |
| Core suite | 448 passed / 0 failed / 0 skipped |
| Ephemeral suite | 165 passed / 0 failed / 0 skipped |
| Durable suite | 305 passed / 0 failed / 0 skipped |
| Hosting suite | 15 passed / 0 failed / 0 skipped |
| Infrastructure run 1 | 69 passed / 0 failed |
| Infrastructure run 2 | 69 passed / 0 failed |
| Infrastructure run 3 | 69 passed / 0 failed |
| ExpectedRed lane | 104 intentional failures / 0 passes |
| Section 4 executable scenarios | 4 passed / 0 failed |
| Section 5 current-physical scenarios | 8 passed / 0 failed |
| `MaxItems` compiled-plan probe | 1 passed / 0 failed |
| `MaxItems` both-mode fingerprint probe | 1 passed / 0 failed |
| Lifecycle-focused regressions | 20 passed / 0 failed |
| Public/API/member-absence guards | 11 passed / 0 failed |
| Failure provenance/value/envelope Core regressions | 19 passed / 0 failed |
| Fingerprint-named Core regressions | 9 passed / 0 failed |
| Durable failure-envelope mapper regressions | 4 passed / 0 failed |
| Structured execution — Ephemeral | 43 passed / 0 failed |
| Structured execution/replay — Durable | 48 passed / 0 failed |
| Green compile fixtures | passed; fresh pack, positive consumers, 26 + 26 forbidden calls, negative package |
| ExpectedRed compile disposition | passed; 0 remaining product-authoring gaps |
| Live NuGet vulnerability audit | all 32 solution projects report no vulnerable packages |
| Strict OpenSpec validation | 17 passed / 0 failed |
| Public authoring companion diff | no diff |
| Active retired-limit scans | zero matches for all five requested symbols/codes |
| `git diff --check` | exit 0; line-ending notices only |
| Reshape task accounting | 76 complete / 60 pending / 136 total |
| Duplicate task IDs | 0 |
| Task `6.0` | open at `openspec/changes/reshape-developer-facing-interfaces/tasks.md:97` |

The first restoring build attempt inside the restricted sandbox could not reach NuGet and returned
`NU1301`. The exact rerun with network access restored every project and passed at zero warnings
and errors. The failure was environmental and did not reproduce once the required package source
was reachable.

## 7. Explicit answers

1. **Does authored `MaxItems` survive authoring/lowering and change structural identity in both
   modes while mode remains excluded?** **Yes.**
2. **Does `restart-readmits-unfinished-items` execute the complete replacement-host schedule and
   fail the final-contract lane only for later work?** **Yes.** The current-physical proof passes;
   the isolated final-contract failure is the Section 7 `OrcaCore.Core` to `OrcaCore` owner move.
3. **Are the original Section 4/5 lifecycle, provenance, codec, fingerprint, limits, and
   structured-fan-out claims still complete?** **Yes.**
4. **Does the pin repair the advisory without changing direct package ownership or public API?**
   **Yes.**
5. **Are all 104 ExpectedRed failures genuinely later-section gaps?** **Yes.** They are 91
   behavior reds plus 13 final contract/package/provider reds, classified above.
6. **Did the frozen manifest reproduce before and after validation without target drift?**
   **Yes.** Both comparisons reproduced all 414 entries and both recorded hashes exactly.

## 8. Authorization boundary

**APPROVE Section 4/5 remediation exit.**

The implementation owner may begin the still-open task `6.0` in a later turn. This verdict neither
completes task `6.0` nor approves any Section 6 product implementation before that planning/review
task is performed.
