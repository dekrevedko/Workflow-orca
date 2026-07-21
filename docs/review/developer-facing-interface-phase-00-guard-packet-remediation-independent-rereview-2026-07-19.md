# Phase 0 guard-packet remediation: independent re-review

**Review date:** 2026-07-19  
**Verdict:** **REJECT**  
**Gate disposition:** Phase 0 exit is not approved. Task 3.12 and task 4.0 remain open. No product
source work is authorized by this review.

## Reviewed snapshot

- Baseline commit and `HEAD`: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Baseline tree: `caaf9dec4e7ce4066525dfddeef06e9a82f3018f`.
- Review target: that baseline plus the exact 114 paths in
  `docs/review/developer-facing-interface-phase-00-guard-packet-remediation-dirty-manifest-2026-07-19.txt`.
- Manifest SHA-256: `8AD73846600E956C5AA7BDCBAF14602F6A50D44B55A79F227354B46BA52ED5B1`.
- Manifest comparison before this immutable review was added: 114 expected, 114 actual, zero
  missing, zero extra.
- Product-source scope: `git diff --name-only -- src` returned no path.

This review used the xUnit v3 executable runner directly. It did not infer execution from
`dotnet test` or from build success.

## Findings

### P1-1 - The product-positive compile gate compiles the companion declarations, not the product declarations

`ProductAuthoring.csproj` references package `OrcaCore`, but also links
`../ExactAuthoring/Authoring.cs` and `PositiveUsage.cs` into the same compilation
(`tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/ProductAuthoring/ProductAuthoring.csproj:6-10`).
`Authoring.cs` is a copied compile-shaped declaration surface. Consequently the positive usage can
bind to those local declarations while the package has an absent, incomplete, or incompatible API.
Package/source type conflicts are warnings by default and do not make the build fail. The expected-red
guard only checks that this project exits zero, while the separate forbidden project checks 26
negative calls (`tests/OrcaCore.DeveloperSurface.Guards/AuthoringContractGuards.cs:113-125`).

This does not remediate original P1-2. A conforming package is not what turns the positive signature
gate green; merely making the package restorable can do so because the test supplies the expected
types itself.

**Required remediation:** compile `PositiveUsage.cs` against the packed product package without
linking or compiling any companion declarations. Keep companion compilation as a separate
infrastructure lane. Treat conflict warnings as errors where a declaration shadow could conceal the
package surface, then demonstrate that a deliberately incomplete package fails the positive lane.

### P1-2 - Behavior guards trust adapter self-attestation and can turn green without executing product behavior

The adapter contract returns booleans, schedules, transitions, assertions, assembly names, and entry
point strings supplied entirely by the adapter
(`tests/OrcaCore.DeveloperSurface.BehaviorContracts/Phase0BehaviorContract.cs:5-39`). The guard checks
those returned shapes, but has no independent observation tying an operation, transition, assertion,
or named entry point to a product invocation
(`tests/OrcaCore.DeveloperSurface.Guards/ExecutableBehaviorContractGuards.cs:37-67`). An assembly
reference and a reported assembly-name string are not proof that a scenario called it.

The current adapter demonstrates the weakness: it fabricates deterministic-time and coordination
claims, two scheduled operations, two changed transitions, an assembly name, and a public-entry-point
label without executing the scenario against product behavior
(`tests/OrcaCore.DeveloperSurface.BehaviorAdapter/Phase0ProductBehaviorAdapter.cs:7-42`). It remains red
only because it self-reports `Passed: false` and a failed assertion. Changing those self-reported
values would satisfy the guard without implementing the product scenario.

This does not remediate original P1-3. The scenario ledgers are routed through executable test code,
but they are not yet genuine executable product-behavior gates.

**Required remediation:** each scenario must be implemented as reviewable test code that invokes
the exact public or approved-friend product seam and asserts independently observed state/results.
Deterministic time and race coordination must be provided by concrete injected clocks/barriers or
friend certification seams whose use is visible to the test, not asserted by an adapter boolean.
Remove the generic evidence object as the authority for whether its own claims passed, or supplement
it with independent typed observations that cannot be manufactured without the named product call.
Add a mutation/negative-control test proving a no-op or fabricated adapter cannot make any scenario
green.

### P2 - none

### P3 - none

## P1 remediation disposition

| Original finding | Re-review disposition |
|---|---|
| P1-1 legacy assemblies/four tiers | Remediated. The frozen inventory contains all eleven target assemblies and all nine audiences; exact project rows, package edges, friend edges, recursive public-signature traversal, and missing-assembly checks are executable. |
| P1-2 string-only compile lane | **Not remediated.** Companion compilation and 26 forbidden diagnostics execute, but the product-positive project supplies the companion declarations locally and therefore does not prove the package surface. |
| P1-3 prose-only behaviors | **Not remediated.** Tests execute and fail at scenario assertions, but their purported execution evidence is self-reported and can be fabricated without product behavior. |
| P1-4 wrong DAG failure owner | Remediated. Expected owners are derived from the frozen ownership catalog, and typed `DAG-*` failures resolve to namespace/assembly `OrcaCore.Dag` while workflow/shared types resolve to `OrcaCore`. |

## Reproduced commands and results

| Command | Result |
|---|---|
| `dotnet run --project tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore -- -trait "Disposition=Infrastructure" -noColor` | xUnit v3.2.2 executable runner; 37 total, 37 passed, 0 failed, 0 skipped. |
| `dotnet run --project tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore -- -trait "Disposition=ExpectedRed" -noColor` | xUnit v3.2.2 executable runner; 28 total, 0 passed, 28 intentional failures, 0 skipped; exit 1. All ten behavior theories reached scenario assertions rather than failing adapter discovery. |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green` | Exit 0; companion and positive usage compiled; script reported 26 precise forbidden-member CS1061 diagnostics. |
| same script with `-Disposition ExpectedRed` | Exit 1 by design; one product red because `OrcaCore 0.0.0-phase0` is absent. |
| `run-package-fixtures.ps1 -Disposition ExpectedRed` | Exit 1 by design; eight named package-consumer reds. |
| `dotnet build OrcaCore.slnx -c Release --no-restore --nologo -v minimal` | Succeeded; 0 warnings, 0 errors. |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | Valid. |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | Valid. |
| `git diff --check` | Exit 0. |
| `git diff --name-only -- src` | No changed path. |
| manifest/status exact comparison using `git status --porcelain=v1 -uall` | 114 expected, 114 actual, 0 missing, 0 extra. |

The task ledger reports 29 done, 83 pending, 112 total. Task 3.12 remains unchecked at
`openspec/changes/reshape-developer-facing-interfaces/tasks.md:38`, and task 4.0 remains unchecked and
explicitly blocked at line 42. Those states are correct for this rejection.

## Gate conclusion

The reproduced counts, strict validations, manifest integrity, source-scope claim, assembly/owner
remediations, and direct xUnit v3 execution are credible. They do not compensate for the two
remaining false-green paths. Phase 0 cannot exit until the product-positive compile lane consumes
only product declarations and the behavior scenarios independently prove real product execution.

