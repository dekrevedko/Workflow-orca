# REJECT — Section 4 exit review for `reshape-developer-facing-interfaces`

**Review date:** 2026-07-21  
**Reviewer:** Codex, independent review pass  
**Decision:** Section 4 does not meet the exit bar. Tasks 4.3, 4.5, 4.7, 4.8, 4.12, and 4.14 must be reopened. **Do not authorize Section 5.**

## Review target and provenance

- Requested baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863` (`caaf9dec4e7ce4066525dfddeef06e9a82f3018f`).
- Actual stable HEAD reviewed: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` (`2264e670493ecc76359d42ee5273028eb287a566`).
- The requested baseline is an ancestor of HEAD; HEAD is one commit ahead, `Complete Phase 0 developer surface guard packet`.
- Worktree target: the exact 305-entry porcelain list frozen in `developer-facing-interface-section-04-exit-review-dirty-manifest-2026-07-21-c.txt`.
- Frozen-manifest SHA-256: `3AE91FCED1C97AC41941EA01E0018CA6BC8B131F8BCCD4D46E0549F347B1404A`.
- Immediately before this review record was added: frozen entries 305, current entries 305, comparison delta 0; HEAD and tree matched the values above.
- Two earlier dated manifests are retained as immutable audit history. The first was superseded when another review artifact appeared; the `-b` manifest was superseded when `tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/ExactAuthoring/PositiveUsage.cs` ceased to be modified. The complete required evidence was rerun after the final `-c` freeze.
- `docs/review/developer-facing-interface-section-04-independent-exit-review-2026-07-21.md` appeared from a concurrent actor during this pass. It is included in the frozen target, but its conclusions were not used as review evidence.
- Decision 17 remains intact at `openspec/changes/reshape-developer-facing-interfaces/design.md:318-335`. The source-to-canonical amendment map is present, and the final Phase 0 approval exists at `docs/review/developer-facing-interface-phase-00-guard-packet-final-independent-rereview-2026-07-19.md`.
- No reviewed product source, test, OpenSpec, task, or existing documentation file was edited by this review. Review activity added only immutable review/manifest artifacts.

## Release-blocking findings

### P1 — Task 4.8's nested fan-out prohibition is not implemented by the public branch surface or compiler defense

`src/OrcaCore.Core/Building/StructuredBranchBuilders.cs:281-296` publicly exposes `BranchBuilder<TBranchState,TResult>.Parallel<TNestedResult>`. The compiler then deliberately accepts that node: `src/OrcaCore.Core/Compilation/DefinitionCompiler.Capabilities.cs:92-95` recursively validates `BranchStructuredScopeAuthoringInstruction` instead of rejecting it outside the root.

This is executable, not dead compatibility code. `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/StructuredFiberExecutionTests.cs:479-524` authors and runs nested branch `Parallel`, and the 167-test ephemeral suite passes it. The manual defense test at `tests/OrcaCore.Core.Tests/Compilation/DefinitionCompilerTests.cs:151-195` inserts `While`, `Parallel`, and `ForEach` only into a root `If`; it does not exercise hand-built branch, item, or leased instruction adjacency. The checked task requires both static absence and compiler rejection for nested, branch, item, and leased builders.

**Required action:** remove branch/item/leased nested fan-out entry points, reject every hand-built non-root fan-out representation at compilation, and add regressions for each forbidden location.

### P1 — Task 4.12's mixed-mode legacy plan path still exists

`src/OrcaCore.Core/Building/WorkflowBuilder.cs:16-313` still defines `LegacyWorkflowBuilder<TState>`. Its build path calls `ContainsDurableOnlyNodes` and `CompiledWorkflowPlan.CreateLegacyTestPlan`. `src/OrcaCore.Core/Compilation/CompiledWorkflowPlan.cs:142-152` still accepts `requiresDurableEngine` and infers `Durable` versus `Ephemeral` mode from that Boolean.

The public absence guard is too narrow: `tests/OrcaCore.DeveloperSurface.Guards/AuthoringContractGuards.cs:176-183` checks that a public property is gone and that source does not contain the token `FromLegacy`; it does not detect `LegacyWorkflowBuilder`, `CreateLegacyTestPlan`, or Boolean mode inference. Numerous tests and benchmarks still instantiate this path, so it is also a duplicate execution route rather than unreachable residue.

**Required action:** delete the legacy builder, legacy plan constructor, inference helper, and duplicate consumers; add a source/metadata guard for the actual forbidden symbols.

### P1 — Task 4.14 has not turned the Section-4 guards green

The `Disposition=ExpectedRed` lane still reports 22 failures and 7 passes. Most importantly, `state-and-codec-scenarios.json` remains an aggregate product failure even though four of its scenarios have Section-4-only green tasks: `definition-id-nonempty` (4.2), `fixed-codec-determinism` (4.3), `structural-fingerprint-opacity` (4.4), and `attempt-local-replace-state` (4.5). The required runtime drivers are absent under `tests/OrcaCore.DeveloperSurface.BehaviorScenarios`; enforcement is at `tests/OrcaCore.DeveloperSurface.Guards/ExecutableBehaviorContractGuards.cs:219-257`.

Two authoring guards that now pass are still declared expected-red at `tests/OrcaCore.DeveloperSurface.Guards/AuthoringContractGuards.cs:144-146`. The exact product compile script's expected-red branch also aborts at `tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1:79-84` because the product fixture unexpectedly compiles. A passing assertion left in an expected-red lane is not a green Section-4 certification.

**Required action:** add and execute all Section-4-owned behavior drivers, move satisfied guards to the green/infrastructure disposition, and leave expected-red only for scenarios whose first unresolved implementation owner is a later section.

### P1 — Task 4.3's product-package compile proof is not bound to the review target

The normative companion compile lane is green, but the product project at `tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/ProductAuthoring/ProductAuthoring.csproj:5-9` restores `OrcaCore` version `0.0.0-phase0` only from `artifacts/phase0-packages`. The package currently making that build pass is `artifacts/phase0-packages/OrcaCore.0.0.0-phase0.nupkg`, SHA-256 `A1DD6A75456B8B74DB54A4A554399241E3E9C823EA00C5CCB0A84190E8E8296F`; `.gitignore:7` excludes `*.nupkg`, so it is absent from the frozen review manifest and is not proven to have been packed from the reviewed HEAD plus diff.

`tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/ExactAuthoring/PositiveUsage.cs:109-112` also reads definition metadata through `dynamic`, which cannot provide compile-time proof of exact property names and types.

**Required action:** pack the exact review target in the evidence command, consume that fresh package in an isolated feed/cache, and make exact metadata access statically typed.

### P1 — Task 4.5 lacks the required durable recovery proof for terminal output/status/outcome

The implementation constructs terminal output and submits it with the durable completion envelope in `src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Terminals.cs:88-124`. The direct test at `tests/OrcaCore.Engine.Durable.Tests/Driver/DurableStructuredFiberDriverTests.cs:29-65` confirms output, completed projection status, and fixed outcome after one run, but it does not dispose the runtime, instantiate a replacement host, rehydrate the checkpoint, and reopen the typed result.

The review found no durable replacement-host recovery test for resultful `End`. Atomic same-command construction is necessary but does not prove recovery preservation, which is an explicit exit-review requirement.

**Required action:** add a deterministic replacement-host recovery regression that verifies typed output, terminal status, and fixed outcome from persisted state.

### P1 — Task 4.7 lacks executable durable DI activation coverage

The ephemeral path has an exact constructor-injection test at `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/NamedStepDependencyInjectionTests.cs:46-72`. Durable tests define many `IStep<TState>` implementations, but the reviewed durable test tree contains no `IServiceProvider`/constructor-injection assertion proving that a public durable authored step is created from host services.

The exit bar requires every task to be implemented and tested, and task 4.7 specifically requires named DI-created durable steps.

**Required action:** add a durable public-authoring test that registers a constructor-dependent step, runs it through the durable runtime, and asserts the host service resolution and result.

## Non-blocking quality findings

### P2 — The exact positive compile fixture weakens metadata checks with `dynamic`

As noted above, `PositiveUsage.cs:109-112` defers metadata member binding to runtime. Even after the package provenance issue is fixed, this fixture should use typed overloads for all four definition shapes and both durable reference shapes so a property rename or type drift fails compilation.

### P2 — The compiler-defense regression covers only one container shape

`DefinitionCompilerTests.cs:151-195` is a useful root-sequence negative control, but its name overstates coverage. Separate tests should inject forbidden nodes into conditional branches, parallel branches, `ForEach` items, and leased scopes and assert the exact diagnostic/location for each.

## Reviewed implementation that is acceptable

- Strong reference-value construction, nonempty identifier parsing, and core value contracts are implemented and covered by the 438-test Core suite.
- The four terminal overload families and eager argument checks are present. Ephemeral output/status/outcome atomicity is exercised at `NamedStepDependencyInjectionTests.cs:14-43`; durable same-commit construction is present, subject to the recovery gap above.
- Portable `StepResult` is reduced to the approved completed/failed/wait surface, with quantum-yield and other engine results internal.
- `WaitLong` is absent from the public authoring surface; durable cold wait and internal quantum yield are acceptable.
- Public definitions expose authored metadata while compiled plans remain non-public. Current friend access is treated as an explicit Section-7 package-transition seam, not approval of the final package graph.
- The diagnostic catalog and `WorkflowDefinitionException` contract are present, and the Core suite covers deadline and empty-root-parallel diagnostics.

## Explicit transitional internals

These do not independently block Section 4 because Decision 17 assigns their final ownership to later sections, but they are not approved as final-v1 layout:

- Existing `InternalsVisibleTo` edges used for compiled IR/runtime access remain transitional until tasks 7.1 and 7.11 establish the final package and barrier graph.
- Durable saga, external-job, child-workflow, checkpoint, and related protocol records remain in current assemblies pending the task-7.3 `Runtime.Protocol` move and the task-7.7/7.8 management/projection reductions.
- Internal execution-quantum yield remains permitted by task 4.9; no public authoring `Yield` approval is implied.
- No accidental Section-5 package extraction was found. The dirty project-metadata paths are only `Directory.Build.props` and `src/OrcaCore.Abstractions/OrcaCore.Abstractions.csproj`; no new package project appears in the manifest.

## Task-by-task disposition

| Task | Disposition | Review basis |
|---|---|---|
| 4.0 | Satisfied | Phase 0 has a final independent approval; Decision 17 and its canonical amendment gate remain intact. |
| 4.1 | Satisfied | Reference identifiers, positive versions, null/empty rejection, and tests are present. |
| 4.2 | Satisfied | Strong-value families and construction constraints are implemented; contract tests pass. |
| 4.3 | **Reopen** | Product package compile proof is not frozen/reproducible, exact metadata uses `dynamic`, and the product lane remains expected-red. |
| 4.4 | Satisfied | Resultless/resultful definitions and durable references expose metadata without public state. |
| 4.5 | **Reopen** | Durable terminal recovery preservation is not tested. |
| 4.6 | Satisfied | Portable result reduction, context/operation identity, detached attempt state, and retry isolation have direct coverage. |
| 4.7 | **Reopen** | No durable host-DI activation regression was found. |
| 4.8 | **Reopen** | Branch `Parallel` remains public, compilable, and executable; compiler defense accepts it. |
| 4.9 | Satisfied | Public `WaitLong`/`Yield` are absent and internal quantum yield is retained. |
| 4.10 | Satisfied for Section 4 | Deferred public authoring families are absent. Protocol/management residues are explicitly assigned to Section 7. |
| 4.11 | Satisfied for Section 4 | Public definition opacity holds; friend/package finalization remains a declared Section-7 transition. |
| 4.12 | **Reopen** | Legacy builder, legacy plan fallback, Boolean mode inference, and duplicate consumers remain. |
| 4.13 | Satisfied | Catalog, exception, severity/location, deadline, and empty-parallel coverage are present. |
| 4.14 | **Reopen** | Section-4 scenarios and authoring guards have not been migrated out of expected-red. |

**Disposition count:** 9 satisfied; 6 reopened; 15 total.

## Expected-red inventory and later-task mapping

The exact lane result is 29 discovered tests: 7 pass and 22 fail. Nonzero exit is expected for a genuine red lane, but the named failures and their first unresolved owner must still be correct.

| # | Exact failing guard/fixture | Intended later owner | Review disposition |
|---:|---|---|---|
| 1 | `ProviderAuthorExpectedRedGuards.ExactProviderAuthoringProjects_ExistForTheFixtureToCompileAgainst` | 7.1, 7.3, 7.11, 7.13 | Valid later red |
| 2 | `FacadeHostingExpectedRedGuards.Product_ExportsTheCommonRegistryAndExactEventClient` | 7.5-7.10, 7.13 | Valid later red |
| 3 | `PackageConsumerExpectedRedGuards.LocalFeed_ContainsEveryExactManifestPackageWithDeclaredDependencies` | 7.1, 7.13 | Valid later red |
| 4 | `NormativeContractExpectedRedGuards.ProductBuiltInFailures_HaveOneCanonicalDeclarationInTheirNormativeOwner` | 6.1, 6.4, 6.10; 7.2, 7.5, 7.7; 8.x | Valid composite later red |
| 5 | `LeaseAuthoringAndExitExpectedRedGuards.Product_ContainsScopedAcquireResourcesAndFinalLifecycle` | 6.4-6.9 | Valid later red |
| 6 | `DagContractExpectedRedGuards.ExactDagAssembliesAndTypesExist` | 8.1-8.10 | Valid later red |
| 7 | `LeaseDiscoveryAndGovernanceExpectedRedGuards.Product_ContainsFinalDiagnosticsRecoveryAndGovernanceStore` | 6.8-7.11 | Valid later red |
| 8 | `ExecutableBehavior...("state-and-codec-scenarios.json")` | 4.2-4.8 plus 7.2, 7.5-7.8 | **Invalid as a later-only red; contains unresolved Section-4-only rows** |
| 9 | `NormativeContractExpectedRedGuards.ProductPublicSignatures_HaveNoForbiddenRecursiveTierEdges` | 7.1-7.4 | Valid later red |
| 10 | `NormativeContractExpectedRedGuards.ProductProjects_DeclareExactPackageIdAssemblyNameAndRowScopedEdges` | 7.1-7.4 | Valid later red |
| 11 | `ExecutableBehavior...("deadline-retry-scenarios.json")` | 6.1-6.3 | Valid later red |
| 12 | `LeaseDiscoveryAndGovernanceExpectedRedGuards.Product_ContainsExactFriendBarrierFacts` | 7.11 | Valid later red |
| 13 | `NormativeContractExpectedRedGuards.ProductAssemblies_MatchExactV1Manifest` | 7.1 | Valid later red |
| 14 | `ExecutableBehavior...("structured-fanout-scenarios.json")` | 5.1-5.9 | Valid later red |
| 15 | `NormativeContractExpectedRedGuards.ProductFriendAssemblies_AreExactlyApproved` | 7.1, 7.11, 8.4 | Valid later red |
| 16 | `ExecutableBehavior...("governance-accounting-scenarios.json")` | 7.11 | Valid later red |
| 17 | `ExecutableBehavior...("facade-hosting-scenarios.json")` | 7.5-7.13 | Valid later red |
| 18 | `ExecutableBehavior...("application-journey-scenarios.json")` | 7.5-9.4 | Valid later red |
| 19 | `ExecutableBehavior...("lease-authoring-admission-scenarios.json")` | 6.4-6.7 | Valid later red |
| 20 | `ExecutableBehavior...("lease-discovery-confirmation-scenarios.json")` | 6.8-7.9 | Valid later red |
| 21 | `ExecutableBehavior...("lease-retry-exit-scenarios.json")` | 6.6-6.9 | Valid later red |
| 22 | `ExecutableBehavior...("dag-contract-scenarios.json")` | 8.1-8.10 | Valid later red |

The seven passing tests still carrying `Disposition=ExpectedRed` are:

1. `ApplicationJourneyExpectedRedGuards.TypedJourneyPackages_AreAvailableForCleanConsumerBuilds`
2. `StructuredFanoutExpectedRedGuards.Product_ContainsFinalEmptyParallelAndClosedOutcomeContract`
3. `DeadlineRetryExpectedRedGuards.Product_ContainsFinalDeadlineDiagnosticAndOperationCoordinate`
4. `StateAndCodecExpectedRedGuards.Product_ContainsTheFixedCodecAndRuntimeCreatedSnapshotContract`
5. `NormativeContractExpectedRedGuards.ProductStrongValues_UseExactConstructionFamilies`
6. `AuthoringContractExpectedRedGuards.Product_CompilesEveryPositiveSignatureAndRejectsEveryForbiddenMember`
7. `AuthoringContractExpectedRedGuards.Product_DoesNotExportSupersededAuthoringEntryPointsOrDeferredFamilies`

## Commands and exact results

All commands below were run after the final manifest refresh.

| Command | Exit/result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | Exit 0; 0 warnings, 0 errors |
| `dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-restore -v minimal` | Exit 0; 438 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore -v minimal` | Exit 0; 167 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-restore -v minimal` | Exit 0; 281 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore --filter "Disposition=Infrastructure" --logger "console;verbosity=minimal"` | Exit 0; 45 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore --filter "Disposition=ExpectedRed" --logger "console;verbosity=minimal"` | Exit 1 by red-lane design; 7 passed, 22 failed, 0 skipped |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green` | Exit 0; normative companion and positive usage compiled, 26 exact CS1061 sites verified, incomplete-package control rejected |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed` | Exit 1; script stopped at line 84 because the product authoring fixture unexpectedly compiled |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | Exit 0; valid |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | Exit 0; valid |
| `git diff --check` | Exit 0; no whitespace errors; line-ending notices only |
| frozen-manifest comparison | 305 frozen, 305 current, delta 0 before adding this review; SHA-256 as recorded above |

OpenSpec task count at review time: 45 complete, 67 open, 112 total. All 15 Section-4 boxes were checked before this review; the six listed above are not supported by the implementation/evidence and must be reopened.

## Exit decision

**Section 4 exit: REJECT.**

**May Section 5 start? NO.** Resolve all P1 findings, rerun the evidence against a newly frozen exact manifest, record a new independent approval with no unresolved release blocker, and only then unblock Section 5.
