# VERDICT: REJECT — Section 4 remediation independent exit re-review

**Review date:** 2026-07-22  
**Change:** `reshape-developer-facing-interfaces`  
**Decision:** Section 4 does not meet the immutable exit bar. **Section 5 and task 5.0 remain blocked.**

## 1. Provenance and immutable review boundary

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Reviewed HEAD: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`.
- Reviewed HEAD tree: `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of HEAD (`git merge-base --is-ancestor` exit 0).
- Frozen target: HEAD plus every entry in `developer-facing-interface-section-04-remediation-dirty-manifest-2026-07-21.txt`.
- Frozen manifest: 322 entries: 284 modified, 5 deleted, 33 untracked.
- Manifest SHA-256: `854903DCE160AF1E2D6CD3285DED6903851A4E9A8C4CDE1D3965ADF9D30E0CE7`.
- Before any repository write, `git status --porcelain=v1 --untracked-files=all` reproduced all 322 entries with zero delta. The same zero-delta comparison was repeated after validation and immediately before this verdict was added.
- OpenSpec state remained 45 complete, 67 pending, 112 total. Tasks 4.0-4.14 were checked; task 5.0 was open.
- Both prior Section-4 reviews and every prior manifest were treated as immutable inputs and were not edited.
- This review created exactly this one verdict file. It did not edit source, tests, tasks, specs, existing documentation, or prior review artifacts.

## 2. Release-blocking findings

### P1-1 — Static resource requests are discarded, so the structural fingerprint contract is still false

The normative contract requires structural fingerprints to include inspectable node/member kinds, ordering, strong values, referenced types, **static resource requests**, and codec format while excluding opaque delegate behavior:

- `docs/specs/17-selected-mode-capability-matrix.md:473-480`
- `openspec/specs/workflow-contracts/spec.md:90`

The public static and selector resource-scope methods do not author any resource node or retain either request form:

- `src/OrcaCore.Core/Building/PublicStagedAuthoring.cs:449-467`
- `src/OrcaCore.Core/Building/PublicNestedAuthoring.cs:156-173`
- `src/OrcaCore.Core/Building/PublicBranchAuthoring.cs:128-131,159-160`

Each overload null-checks the request, invokes the body against the existing underlying builder, and returns. The static `ResourceLeaseRequest` value is otherwise unused; selector identity is also unused. Consequently two same-ID/version definitions that differ only by static pool/units author the same graph.

`DefinitionCompiler.Compile` hashes `DescribeOptions`, `DescribeSequence`, and ordered schema identities at `src/OrcaCore.Core/Compilation/DefinitionCompiler.cs:28-35`. `DefinitionCompiler.Fingerprint.cs:22-155` has no resource-scope/request case because no request authoring node exists. The focused fingerprint tests cover graph, fixed outcome, compiler options, partitioner configuration, branch ID, and captured opaque values, but contain no static-request regression.

This is not an opaque external-request-construction change. A static `ResourceLeaseRequest` is explicitly inspectable structure and must affect the fingerprint. The current implementation therefore violates the additional fingerprint remediation claimed by the re-review request.

**Required remediation:** retain a structural resource-scope authoring node; bind static pool/unit values into the canonical fingerprint; use only an opaque marker for selector bodies/captures; add same-ID/version regressions proving static request drift changes the fingerprint while opaque selector-body/capture drift does not.

### P1-2 — Section-4 state/codec drivers are false greens relative to their own frozen scenarios

The four Section-4 driver invocations pass, but three do not execute the behavior declared by the frozen scenario rows:

1. `fixed-codec-determinism` requires supported-graph round trips, byte/type determinism, detachment, null handling, pre-commit rejection of unsupported/cyclic/polymorphic graphs, and absence of a codec replacement seam (`state-and-codec-scenarios.json:5`). `Phase0ScenarioHost.cs:21-35` only builds one compatible definition and asserts a nonempty fingerprint. It never serializes or deserializes a payload and exercises none of the rejection cases.
2. `structural-fingerprint-opacity` says to change opaque delegates and then use an explicit version bump (`state-and-codec-scenarios.json:6`). `Phase0ScenarioHost.cs:37-61` proves equal fingerprints for two equivalent delegates and drift for an added `Delay`, but never performs the required version bump and does not cover static request values. The Core tests also omit static requests.
3. `attempt-local-replace-state` requires a failed attempt, retry isolation, successful replacement commit, and no mutable committed-state exposure (`state-and-codec-scenarios.json:7`). `Phase0ScenarioHost.cs:63-83` reflectively constructs one `StepContext`, calls `ReplaceState`, and checks two object references. It performs no failure, retry, or commit. Separate engine tests cover useful retry behavior, but they do not make this guard-owned scenario driver truthful.

The codec claim is additionally contradicted by current public product seams:

- `src/OrcaCore.Abstractions/Providers/IWorkflowPayloadCodec.cs:8` publicly permits replacement codecs.
- `src/OrcaCore.Engine.Durable/Execution/ContentTypeWorkflowPayloadSerializer.cs:8-69` routes writes through a configurable codec collection.
- `src/OrcaCore.Hosting/WorkflowPayloadSerializationOptions.cs:5-16` publicly selects the write content type and explicitly documents serialization plugins.
- `src/OrcaCore.Hosting/OrcaCoreServiceCollectionExtensions.cs:56-61` registers all `IWorkflowPayloadCodec` implementations and selects the configured writer.

`StateAndCodecGreenGuards.Product_ContainsTheFixedCodecAndRuntimeCreatedSnapshotContract` at `StateAndCodecContractGuards.cs:43-50` checks only that one exported snapshot type exists and that some source line contains `orcacore-json-v1`. It does not reject the replacement seam or prove deterministic codec behavior.

The `fixed-codec-determinism` row has `turnsGreenTask: 4.3`; it is not assigned to a later section. These passing guards therefore cannot certify task 4.14 or Section-4 exit.

**Required remediation:** implement the frozen fixed codec without a public replacement seam, add complete deterministic/rejection/detachment tests, and make each Section-4 scenario driver execute every behavior named by its setup and assertion. Add an explicit version-bump fingerprint case and a real fail/retry/commit attempt-local case.

### P1-3 — The required infrastructure lane is nondeterministic and failed its first independent run

The first exact infrastructure command failed with 52 passed and 1 failed. `InfrastructureGuards.ExactAuthoringPositiveFixture_Compiles` received:

`CSC error CS2012: Cannot open ... CompileFixtures\ExactAuthoring\obj\Release\net10.0\ExactAuthoring.dll for writing ... used by another process.`

An immediate rerun passed 53/53. Source inspection explains the intermittent self-collision:

- `InfrastructureGuards.cs:37-63` starts a `dotnet build` of `ExactAuthoring.csproj`.
- `AuthoringContractGuards.cs:198-221` concurrently launches `run-compile-fixtures.ps1 -Disposition Green` from another xUnit class.
- `run-compile-fixtures.ps1:48-53` builds the same `ExactAuthoring.csproj` into the same Release `obj/bin` paths.
- The guard project contains no collection/assembly configuration disabling this cross-class parallel execution and supplies no isolated output paths or interprocess lock.

The second run demonstrates nondeterminism rather than resolution. A release-certification lane must be independently reproducible and cannot intermittently fail through its own fixture orchestration.

**Required remediation:** serialize the two compile checks or give each invocation isolated intermediate/output paths; then rerun the exact infrastructure command repeatedly without a lock collision.

## 3. Six requested blocker remediations

| Task | Re-review disposition | Independent evidence |
|---|---|---|
| 4.3 exact consumer/package compile | Remediation present, but Section-4 codec/fingerprint certification still blocks exit | `PositiveUsage.cs:109-136` uses typed metadata overloads. The green script builds current Core, packs current `OrcaCore` to fixture-local `obj/product-feed`, uses isolated caches, compiles positive consumers, verifies 26 source plus 26 package CS1061 locations, and rejects the incomplete package. Both compile dispositions exit 0. |
| 4.5 replacement-host terminal recovery | Resolved | `DurableStructuredFiberDriverTests.cs:29-79` starts and parks on one runtime, raises the event through a replacement runtime sharing persisted storage, then verifies checkpoint output, completed status, and fixed outcome. Focused test passed. |
| 4.7 durable named-step host DI | Resolved | `DurableStructuredFiberDriverTests.cs:82-120,2402-2426` authors public `Then<ConstructorInjectedDurableStep>()`, resolves it through the registry service provider, executes its dependency, and asserts the requested type. Focused test passed. |
| 4.8 root-only fan-out | Resolved | The public branch `Parallel` member is absent; the 26-call forbidden fixtures compile-fail from source and fresh package. `DefinitionCompiler.cs:296-364` reports root-only errors for hand-built branch/item nested scopes, and `DefinitionCompilerTests.cs:151-259` covers root-nested plus branch/item placements. Focused tests passed. |
| 4.12 legacy fallback/mode inference | Resolved for the named blocker | `WorkflowBuilder.cs` is deleted. Source contains no `LegacyWorkflowBuilder`, `SelectedWorkflowBuilder`, `CreateLegacyTestPlan`, `ContainsDurableOnlyNodes`, `requiresDurableEngine`, or `RequiresDurableEngine` except the guard's forbidden-symbol strings. The live internal `WorkflowAuthoringSession` is used by separate explicit ephemeral/durable adapters rather than a Boolean-inferred legacy plan path. |
| 4.14 guard migration | **Not resolved** | The lane is scenario-specific and the 91 later cases are individually named, but the three false-green drivers above do not satisfy their frozen scenarios, and the infrastructure lane races itself. |

The earlier P2 compile-metadata and compiler-placement coverage findings are resolved. The earlier P3 observation that internal `SagaBuilder`/`WorkflowDagBuilder` remain as unexported Section-7/8 transition code remains noted and non-blocking; this review does not approve them as final package layout.

## 4. Structural-fingerprint disposition

| Required property | Evidence | Disposition |
|---|---|---|
| Opaque selector/projector/condition/output bodies and captured values excluded | `DefinitionCompiler.Fingerprint.cs` uses `<opaque>` markers; opacity driver and captured-value Core regression pass | Satisfied for tested delegate forms |
| Graph/node ordering changes affect fingerprint | Delay/conditional/branch regressions pass | Satisfied |
| Compiler options affect fingerprint | `Fingerprint_IsDeterministic_AndChangesWithGraphOrCompilerConfiguration` passes | Satisfied |
| Partitioner configuration affects fingerprint | `Fingerprint_ChangesWithForEachPartitionerConfiguration` passes | Satisfied |
| Strong values and referenced types affect fingerprint | Event/outcome/branch values and assembly-qualified types are included by `DescribeSequence`; fixed-outcome and branch-ID regressions pass | Partially evidenced |
| Codec format affects fingerprint | Ordered schema identities enter the canonical value, but the public runtime codec remains replaceable without a corresponding structural binding | **Not satisfied** |
| Static resource-request values affect fingerprint | Request values are discarded before authoring/compilation; no test exists | **Not satisfied — release blocker** |
| Explicit version bump is the sole opaque-change contract | Version participates in `CompiledWorkflowPlan` hashing, but the frozen scenario driver does not execute the required version-bump case | Coverage incomplete |

## 5. Expected-red review

The exact lane discovered 104 tests: 0 passed, 104 failed, 0 skipped. Exit code 1 is intentional for this lane.

Programmatic comparison of the 91 actual `ExecutableBehaviorExpectedRedGuards.Scenario...` names against all frozen fixture rows after excluding the four Section-4 IDs produced:

- expected scenarios: 91
- actual named scenario failures: 91
- name delta: 0
- every remaining scenario has at least one unresolved owner in Sections 5-9
- only four `Phase0Scenario` driver attributes exist, exactly the Section-4 IDs

| Fixture | Individually named reds | Later owner |
|---|---:|---|
| `state-and-codec-scenarios.json` | 5 | Section 7 portions of the mixed 4.x/7.x rows |
| `structured-fanout-scenarios.json` | 9 | Section 5 |
| `facade-hosting-scenarios.json` | 11 | Section 7 |
| `application-journey-scenarios.json` | 8 | Sections 7-9 |
| `deadline-retry-scenarios.json` | 10 | Section 6 |
| `dag-contract-scenarios.json` | 9 | Section 8 |
| `lease-authoring-admission-scenarios.json` | 10 | Section 6 |
| `lease-retry-exit-scenarios.json` | 8 | Section 6 |
| `lease-discovery-confirmation-scenarios.json` | 10 | Sections 6-7 |
| `governance-accounting-scenarios.json` | 11 | Section 7 |

The remaining 13 exact non-scenario failures are intentional later product gaps:

1. `ApplicationJourneyExpectedRedGuards.TypedJourneyPackages_AreAvailableForCleanConsumerBuilds` — missing exact later packages, first observed at `OrcaCore.Dag`.
2. `DagContractExpectedRedGuards.ExactDagAssembliesAndTypesExist` — Section 8 DAG assemblies absent.
3. `NormativeContractExpectedRedGuards.ProductPublicSignatures_HaveNoForbiddenRecursiveTierEdges` — complete Section-7 package graph absent.
4. `FacadeHostingExpectedRedGuards.Product_ExportsTheCommonRegistryAndExactEventClient` — Section-7 facade absent.
5. `LeaseDiscoveryAndGovernanceExpectedRedGuards.Product_ContainsExactFriendBarrierFacts` — Section-7 barrier/friend facts absent.
6. `PackageConsumerExpectedRedGuards.LocalFeed_ContainsEveryExactManifestPackageWithDeclaredDependencies` — Section-7 exact feed incomplete.
7. `ProviderAuthorExpectedRedGuards.ExactProviderAuthoringProjects_ExistForTheFixtureToCompileAgainst` — Section-7 protocol/provider projects absent.
8. `LeaseAuthoringAndExitExpectedRedGuards.Product_ContainsScopedAcquireResourcesAndFinalLifecycle` — Section-6 lease lifecycle absent.
9. `LeaseDiscoveryAndGovernanceExpectedRedGuards.Product_ContainsFinalDiagnosticsRecoveryAndGovernanceStore` — Sections 6-7 diagnostics/governance absent.
10. `NormativeContractExpectedRedGuards.ProductBuiltInFailures_HaveOneCanonicalDeclarationInTheirNormativeOwner` — later Section 6-8 failures absent.
11. `NormativeContractExpectedRedGuards.ProductAssemblies_MatchExactV1Manifest` — Section-7 assembly manifest absent.
12. `NormativeContractExpectedRedGuards.ProductProjects_DeclareExactPackageIdAssemblyNameAndRowScopedEdges` — Section-7 projects absent.
13. `NormativeContractExpectedRedGuards.ProductFriendAssemblies_AreExactlyApproved` — Section-7/8 final friend graph absent.

No red was caused by restore failure, test discovery failure, missing test assembly, or assertion infrastructure. The 91 scenario reds are exactly missing later drivers; the 13 others name the intended later product/package gaps. The expected-red inventory itself is acceptable.

## 6. Commands and exact reproduced results

| Command | Result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | Exit 0; 0 warnings, 0 errors |
| `dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-restore --no-build -v minimal` | 404 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore --no-build -v minimal` | 148 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-restore --no-build -v minimal` | 277 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --filter "Disposition=Infrastructure" -v minimal` | **First run exit 1: 52 passed, 1 failed with CS2012 fixture-output lock. Immediate exact rerun: 53 passed, 0 failed.** Nondeterministic lane is P1-3. |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --no-build --filter "Disposition=ExpectedRed" -v minimal` | Exit 1 by design; 0 passed, 104 intentional named failures, 0 skipped; 91 scenario + 13 other |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green` | Exit 0; fresh current package, exact/product positives, 26+26 forbidden CS1061 sites, incomplete-package control |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed` | Exit 0; expected-red compile fixtures 0, Section-4 package proof green |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | Exit 0; valid |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | Exit 0; valid |
| `git diff --check` | Exit 0; no whitespace errors; LF-to-CRLF notices only |

Additional focused verification, not a substitute for the required commands:

- durable replacement-host plus durable DI tests: 2 passed
- compiler root-only plus fingerprint tests: 6 passed
- four Section-4 scenario drivers: 4 passed, but P1-2 explains why three assertions are insufficient

## 7. Exit decision

**Section 4 exit is REJECTED.**

The original compile, recovery, DI, fan-out, and legacy-path remediations are present, and the expected-red inventory is correctly individualized. Approval is nevertheless prohibited because:

1. static lease-request structure is discarded and cannot affect the structural fingerprint;
2. the green fixed-codec/fingerprint/attempt-state drivers do not execute their frozen contracts, while a public codec replacement seam remains; and
3. the required infrastructure lane failed its first independent run through a self-induced file-lock race.

**May Section 5 or task 5.0 begin? NO.** Resolve every P1 finding, freeze a new exact manifest, rerun the entire validation packet without an infrastructure retry, and obtain a new independent approval with no unresolved release blocker.
