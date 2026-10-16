# VERDICT: REJECT - Section 4 final remediation independent exit re-review

**Date:** 2026-07-22
**Decision:** Section 4 exit is rejected. Section 5 and task 5.0 remain blocked.

The frozen target and every required validation reproduced, and four of the six requested
remediations are sound. Approval is nevertheless prohibited because the fixed-codec remediation
still leaves a public serializer replacement path and rejects polymorphism only at the root value,
not throughout the supported value graph. These are release blockers against the frozen
non-replaceable-codec and unsupported-graph contracts.

## 1. Provenance and immutable review boundary

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Reviewed `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`.
- Reviewed tree: `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`.
- Frozen dirty manifest:
  `docs/review/developer-facing-interface-section-04-final-remediation-dirty-manifest-2026-07-22.txt`.
- Manifest SHA-256:
  `CF3E1BE1BAC6C32A081940E3BCF5D30430BB7F2FE9870E06950767E03351B90B`.
- Reproduced manifest: 332 entries - 285 modified, 8 deleted, 39 untracked.
- `git status --porcelain=v1 --untracked-files=all` matched all 332 manifest lines in exact order
  with zero differences before any review artifact was written. The same comparison was repeated
  after all build, test, fixture, and validation commands and still had zero differences.
- OpenSpec task state reproduced as 45 done, 67 pending, 112 total. Tasks 4.0 through 4.14 are
  checked; task 5.0 is open.
- The request, manifests, source, tests, tasks, specs, existing documentation, and all prior review
  artifacts were treated as immutable inputs.
- This reviewer authored exactly one path: this verdict. During its final creation, a separate task
  concurrently created the untracked
  `developer-facing-interface-section-04-final-remediation-exit-approval-2026-07-22.md` at
  `2026-07-22T20:27:12.5806201-07:00`; this verdict was created at
  `2026-07-22T20:27:13.4384868-07:00`. The concurrent file was not present at either pre-write
  manifest gate, was not authored or modified by this reviewer, and is outside the frozen 332-path
  target. Excluding these two newly authored review paths from final status still leaves the exact
  332 manifest entries with zero differences. The concurrent approval does not address either
  P1 finding below and is not accepted as authorization to begin Section 5.

## 2. Release-blocking findings

### P1-1 - The fixed serializer remains publicly replaceable

The contract requires a non-replaceable `orcacore-json-v1` codec and no ordinary hosting serializer
hook:

- `openspec/specs/workflow-contracts/spec.md:238-239`
- `openspec/changes/reshape-developer-facing-interfaces/specs/developer-facing-surface/spec.md:185-186`
- `docs/specs/12-acceptance-criteria.md:118-120`

The three named plugin/router/options types were deleted, but an equivalent replacement path
remains:

1. `IWorkflowPayloadSerializer` is a public implementation interface at
   `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs:207-221`.
2. `DurableWorkflowRuntime` publicly accepts any implementation at
   `src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs:30-41`.
3. Hosting uses
   `TryAddSingleton<IWorkflowPayloadSerializer, JsonWorkflowPayloadSerializer>()` and resolves that
   interface at `src/OrcaCore.Hosting/OrcaCoreServiceCollectionExtensions.cs:55,79`. A caller's
   earlier registration therefore wins.
4. The existing hosting test fixture exercises precisely that path: it registers a custom
   `IWorkflowPayloadSerializer` before `AddOrcaCore()` at
   `tests/OrcaCore.Hosting.Tests/OrcaCoreHostingServiceCollectionTests.cs:258-272`; the provider
   implements the interface at lines 358-370 and delegates its methods at lines 587-595.

The new hosting regression at
`tests/OrcaCore.Hosting.Tests/WorkflowPayloadSerializationRegistrationTests.cs:13-39` resolves the
default service and rejects only three exact deleted type names. It never attempts a replacement
and does not reject the still-public serializer interface or runtime constructor.

This also leaves fingerprint binding unsafe. `DefinitionCompiler` builds its canonical value from
compiler options, authored structure, and type schema identities at
`src/OrcaCore.Core/Compilation/DefinitionCompiler.cs:28-40`; `CompiledWorkflowPlan` then hashes
format version, mode, definition identity/version, and that canonical value at
`src/OrcaCore.Core/Compilation/CompiledWorkflowPlan.cs:65-66`. No
`orcacore-json-v1` format identifier is present. A custom serializer can therefore change persisted
bytes/content metadata without changing the definition fingerprint.

**Required remediation:** remove or make implementation-only every path by which application code
can supply another workflow-value serializer, make durable hosting/runtime use the fixed codec
directly, bind the codec format explicitly into plan identity, and add negative tests that attempt
replacement both through DI ordering and direct runtime construction.

### P1-2 - Unapproved polymorphism rejection is not graph-wide

`JsonWorkflowPayloadSerializer.Serialize<TPayload>` checks only whether the root runtime type equals
`typeof(TPayload)` at
`src/OrcaCore.Engine.Durable/Execution/JsonWorkflowPayloadSerializer.cs:17-24`. It then passes the
entire object graph to default `System.Text.Json` at lines 26-28 without recursively validating
declared member or collection-element types.

An exact-type outer object, `List<Base>`, dictionary, or other container can therefore contain a
runtime-derived value and bypass the check. Default serialization can accept that graph while
serializing only the declared base contract, causing unsupported shape acceptance and possible
data loss rather than the required pre-commit rejection.

The regression at
`tests/OrcaCore.Engine.Durable.Tests/Execution/WorkflowPayloadSerializationTests.cs:41-53` and the
scenario driver at
`tests/OrcaCore.DeveloperSurface.BehaviorScenarios/Phase0ScenarioHost.cs:62-68` test only a
polymorphic root. They cannot detect nested member or collection-element polymorphism. This does
not satisfy the supported-value-graph language in `openspec/specs/workflow-contracts/spec.md:238-239`
or AC-024.

**Required remediation:** validate the complete declared value graph against an approved static
contract before commit and add nested-property, collection-element, and dictionary-value
polymorphism regressions, including proof that rejection occurs before any provider mutation.

## 3. Six requested remediation dispositions

| Remediation | Independent disposition | Evidence |
|---|---|---|
| Static request retention | **Resolved** | Root and nested overloads route into dedicated resource-scope nodes at `PublicStagedAuthoring.cs:448-470` and `PublicNestedAuthoring.cs:156-176`; branch/item paths do the same at `PublicBranchAuthoring.cs:128-131,159-160`. |
| Fixed codec and closed replacement surface | **Not resolved - release blockers P1-1 and P1-2** | Narrow deterministic/null/cycle/root-polymorphism/content-type tests pass, and three named types are deleted, but the public serializer replacement path and nested-polymorphism acceptance remain. |
| Fingerprint/version driver | **Resolved for the requested driver behavior** | `Phase0ScenarioHost.cs:81-124` proves opaque-delegate equality, graph drift, explicit version drift, and static-request drift. |
| Attempt-state driver | **Resolved** | `Phase0ScenarioHost.cs:127-187` observes `ReplaceState` directly, runs a fail-first `WithRetry(2)` execution, rejects leaked mutation, and observes committed replacement value `2`. |
| Infrastructure nondeterminism | **Resolved** | `CompileFixtureCollection.cs:3-6` disables parallel execution; both process-spawning guard classes use it. The exact lane passed 53/53 on three consecutive first attempts with no `CS2012`. |
| Task 4.14 / Section 4 certification | **Not resolved** | The task is checked, but the fixed-codec scenario is not truthful for the full graph and the replacement surface remains open. A checked task cannot override release blockers. |

The earlier exact-consumer/package compile, replacement-host terminal recovery, durable host-DI
activation, root-only fan-out, legacy fallback/mode-inference removal, compiler-placement, and
metadata remediations remain present. No regression was found in those slices.

## 4. Structural fingerprint and resource-scope review

The resource-scope remediation itself is accepted:

- `SelectedResourceLeaseAuthoringNode` retains static request or opaque selector plus its dedicated
  body at `SelectedWorkflowAuthoring.cs:112-118`.
- Root/nested authoring creates a fresh body list and appends one lexical lease node at
  `SelectedWorkflowBuilder.cs:359-378`.
- Branch/item authoring retains the same three-part structure at
  `StructuredBranchBuilders.cs:278-303,383-386`.
- Root/nested fingerprinting describes the request and recursively describes the body at
  `DefinitionCompiler.Fingerprint.cs:76-79`.
- Branch/item fingerprinting does the same at lines 155-159.
- Static requests encode every requirement in retained order, including pool-name length/value and
  units, at lines 178-202. Selector bodies and captures use the single `selector:opaque` marker.
- `ResourceLeaseRequest` copies and validates its ordered requirement collection at
  `src/OrcaCore.Abstractions/Instances/AuthoringValues.cs:169-198`.

The five focused executions pass: one fact proves same-structure stability, static-request drift,
leased/unleased drift, and selector-capture opacity; four theory cases prove static-request drift at
root, nested, branch, and item placements.

The Section 4/Section 6 boundary also remains intact. Current lowering preserves the lexical marker
and nested body but does not implement permit acquisition, renewal, recovery, release, or protection
tokens. All 39 lease/governance behavior scenarios remain red and later-task mapped.

The resource-request contribution is therefore sound. The overall fingerprint contract is still
blocked by P1-1 because runtime codec selection can change without an explicit codec-format
contribution.

## 5. Expected-red review

The exact command returned exit 1 by design with 0 passed, 104 failed, and 0 skipped. The inventory
contains 91 individually named scenario failures plus 13 named product/package/assembly failures.
No failure was caused by restore, discovery, missing test assembly, fixture setup, or newly-green
behavior.

| Fixture family | Named reds | Later owner |
|---|---:|---|
| State and codec remainder | 5 | Section 7 portions of mixed rows |
| Structured fan-out | 9 | Section 5 |
| Facade and hosting | 11 | Section 7 |
| Application journeys | 8 | Sections 7-9 |
| Deadline and retry | 10 | Section 6 |
| DAG | 9 | Section 8 |
| Lease authoring/admission | 10 | Section 6 |
| Lease retry/exit | 8 | Section 6 |
| Lease discovery/confirmation | 10 | Sections 6-7 |
| Governance/accounting | 11 | Section 7 |

The 13 non-scenario failures are the exact intended gaps for application packages, DAG assemblies,
recursive tier edges, facade/event client, friend barriers, complete local-feed packages,
provider-author projects, final lease lifecycle, diagnostics/governance, canonical later failures,
the exact assembly manifest, row-scoped project edges, and final friend assemblies.

All 39 lease/governance scenarios (10 + 8 + 10 + 11) remain red. In particular,
`LeaseAuthoringAndExitExpectedRedGuards.Product_ContainsScopedAcquireResourcesAndFinalLifecycle`
still fails on missing `AmbiguousHeld`; no Section 6 lifecycle behavior turned green.

## 6. Commands and exact reproduced results

| Command | Result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | Exit 0; 0 warnings, 0 errors |
| `dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-restore --no-build -v minimal` | 409 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore --no-build -v minimal` | 148 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-restore --no-build -v minimal` | 277 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj --no-restore --no-build -v minimal` | 15 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --filter "Disposition=Infrastructure" -v minimal` | Run 1: 53 passed; run 2: 53 passed; run 3: 53 passed. Every run exited 0 with 0 failed/0 skipped and no retry. |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --no-build --filter "Disposition=ExpectedRed" -v minimal` | Exit 1 by design; 0 passed, 104 named failures, 0 skipped |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green` | Exit 0; freshly packed current source; exact/product-positive consumers compiled; 26 source and 26 package forbidden-member CS1061 diagnostics verified; incomplete package rejected |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed` | Exit 0; 0 remaining expected-red compile fixtures; Section 4 authoring package proof green |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | Exit 0; valid |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | Exit 0; valid |
| `git diff --check` | Exit 0; no whitespace errors; benign LF-to-CRLF notices only |

Additional focused verification, not a substitute for the required commands:

- Four Section 4 executable scenario drivers: 4 passed, 0 failed, 0 skipped.
- Static lease fingerprint tests: 5 passed, 0 failed, 0 skipped.
- Durable fixed-codec tests: 4 passed, 0 failed, 0 skipped.
- Hosting codec registration tests: 2 passed, 0 failed, 0 skipped.

Those focused codec results are green because their assertions do not attempt either release-blocking
path described above.

## 7. Answers to the independent review questions

1. **Does every approved `AcquireResources` placement retain lexical request/body structure?**
   **Yes.**
2. **Do static request and leased/unleased drift affect fingerprints while selector identity stays
   opaque?** **Yes for the inspected resource-scope implementation and tests.**
3. **Does Section 6 remain the sole owner of executable lease lifecycle behavior?** **Yes; all 39
   related scenarios remain red.**
4. **Is the codec fully deterministic/rejecting with every public replacement seam gone?** **No.**
   P1-1 and P1-2 are release blockers.
5. **Do the definition-id, fingerprint/version, and attempt-state drivers execute their frozen
   behaviors?** **Yes for the reviewed driver cases.**
6. **Does infrastructure pass repeatedly on the first attempt without a fixture race?** **Yes,
   three consecutive 53/53 runs.**
7. **Are all earlier Section 4 blockers resolved without regression?** **No overall.** The
   consumer/recovery/DI/fan-out/legacy and lease-fingerprint remediations remain resolved, but the
   codec blocker is not.
8. **Did the frozen manifest reproduce exactly before this verdict?** **Yes, twice, with zero
   differences and the stated checksum.**

## 8. Exit decision

**Section 4 exit is REJECTED.**

**May Section 5 or task 5.0 begin? NO.** Task 4.14 must not be treated as independently certified
while P1-1 or P1-2 remains. Resolve both blockers, add regressions that would fail against this
reviewed target, freeze a new exact manifest, rerun the complete validation packet, and obtain a new
independent approval with no unresolved release blocker.
