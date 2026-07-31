# VERDICT: REJECT - Section 4 codec remediation independent exit re-review

**Review date:** 2026-07-22  
**Change:** `reshape-developer-facing-interfaces`  
**Decision:** Section 4 exit is rejected. Section 5 and task 5.0 remain blocked.

The two previously reported codec defects were remediated in their narrow paths: the durable
start-input serializer is no longer publicly replaceable, its recursive validator handles the
declared object/collection/dictionary graph, approved `JsonDerivedTypeAttribute` contracts still
round-trip, and `orcacore-json-v1` is now part of the compiled-plan fingerprint seed.

Approval is nevertheless prohibited. The recursive validator is not the codec used by current
typed state/output paths, public raw start commands can still persist caller-selected content and
bytes without the fixed codec, application `JsonConverter` attributes remain an unguarded
serializer hook, and the green executable scenario cannot exercise all of the rejection paths it
claims to prove. These are release blockers against the frozen fixed-codec contract and the
independent review questions.

## 1. Provenance and immutable boundary

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Reviewed `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`.
- Reviewed tree: `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`.
- Frozen manifest:
  `docs/review/developer-facing-interface-section-04-codec-remediation-dirty-manifest-2026-07-22.txt`.
- Frozen manifest SHA-256:
  `4A9BE59EF3171186EEAFDD2FFAB250B618303D8420F2EB5DA1225E32AD0D39A0`.
- The manifest contains exactly 339 entries: 288 modified, 8 deleted, and 43 untracked.
- `git status --porcelain=v1 --untracked-files=all` matched the manifest exactly, with zero
  differences, before validation and again immediately before this verdict was written.
- After this verdict was added, excluding this one exact new path left the same 339 manifest
  entries with zero differences.
- OpenSpec task state reproduced as 45 done, 67 pending, 112 total. Task 4.14 is checked and task
  5.0 is unchecked.
- Source, tests, tasks, specs, existing documentation, requests, manifests, and prior review
  artifacts were treated as immutable inputs. This review authored exactly one path: this verdict.

## 2. Release-blocking findings

### P1-1 - Graph validation is bypassed by current typed state and output codecs

The canonical contract applies `orcacore-json-v1` to supported input, state, result, output, event,
and DAG values and requires unsupported cyclic or unapproved polymorphic graphs to be rejected
before commit:

- `openspec/specs/workflow-contracts/spec.md:239`
- `openspec/changes/reshape-developer-facing-interfaces/design.md:138`
- `docs/specs/12-acceptance-criteria.md:118-120`

The new recursive validation exists only in the durable start-input serializer:

- `JsonWorkflowPayloadSerializer.Serialize` calls `ValidateGraph` at
  `src/OrcaCore.Engine.Durable/Execution/JsonWorkflowPayloadSerializer.cs:29-35`.
- Its recursive implementation covers properties, collection elements, and dictionary keys/values
  at lines 66-191.

Current structured execution does not use that serializer for all values:

1. Durable resultful `End` output is projected and passed to `codec.Serialize` at
   `src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Terminals.cs:88-100`.
2. That codec delegates to `IWorkflowTypeSerializerRegistry.Serialize` at
   `src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Models.cs:12-28`.
3. The default registry directly calls
   `JsonSerializer.SerializeToUtf8Bytes(value, declaredType)` without `ValidateGraph` at
   `src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs:39-60`.
4. Ephemeral output and detached state use the same unvalidated registry through
   `src/OrcaCore.Engine.Ephemeral/Execution/InMemoryExecutionStateAdapter.Helpers.cs:45-58` and
   `src/OrcaCore.Engine.Ephemeral/Execution/InMemoryExecutionStateAdapter.cs:276-282,402-423`.

Consequently, a resultful `End<Base>(...)` whose selector returns an unapproved `Derived` is not
rejected by the new graph validator. Default `System.Text.Json` is asked to serialize it as the
declared base type, which can accept the value while omitting derived data. The same gap applies to
current detached state and other structured values routed through this registry. This is a current
Section-4 typed completion/state defect, not a later package/facade red.

The nine focused durable codec tests exercise `JsonWorkflowPayloadSerializer` directly or exercise
durable start input. None exercises polymorphic typed output or the structured state codec in
either engine.

**Required remediation:** make one fixed graph-validating codec govern every implemented
Section-4 input/state/result/output path, then add regressions for unapproved polymorphism and
cycles in typed output and detached state in both engines. Durable cases must prove rejection
before provider mutation.

### P1-2 - Equivalent ordinary-consumer codec bypasses remain

The direct public serializer interface/runtime-constructor/DI-ordering defect from the prior
rejections is closed, but two equivalent application-controlled paths remain.

#### Public raw start path

- `DurableCommandProcessor` and its constructors are public at
  `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:13,29-43`.
- Its `ProcessAsync(StartWorkflowCommand, ...)` overload is public at lines 79-89.
- `StartWorkflowCommand` publicly accepts caller-selected `InputContentType` and raw
  `InputPayload` at `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs:43-70`.
- The lifecycle handler copies both fields into `WorkflowStartedEvent` without fixed-codec
  validation at
  `src/OrcaCore.Engine.Durable/Aggregates/DurableLifecycleCommandHandler.cs:18-34`.
- Hosting registers `DurableCommandProcessor` as a resolvable concrete service at
  `src/OrcaCore.Hosting/OrcaCoreServiceCollectionExtensions.cs:65-80`.

An ordinary application can therefore resolve the processor and commit arbitrary bytes, including
a foreign content type or arbitrary bytes labelled `orcacore-json-v1`, before any later driver read
rejects them. The new public-surface tests search serializer type names and runtime constructor
parameter names; they do not inspect or exercise this route.

#### Application `JsonConverter` path

After graph walking, `JsonWorkflowPayloadSerializer` invokes default `System.Text.Json` at
`JsonWorkflowPayloadSerializer.cs:33-35`. The validator handles `JsonDerivedTypeAttribute` and
`JsonIgnoreAttribute` but neither rejects nor allowlists application type/property
`JsonConverterAttribute` declarations. Default `System.Text.Json` honors those converters.
`DefaultWorkflowTypeSerializerRegistry.TryGetSchemaIdentity` accepts a type when default STJ can
produce metadata at `DefinitionCompilerOptions.cs:67-88`.

An application converter can therefore select arbitrary or nondeterministic bytes while the
payload is labelled `orcacore-json-v1`. No current regression rejects or constrains that hook.

**Required remediation:** prevent ordinary application access to raw start persistence or validate
that boundary with the fixed codec before mutation. Define and enforce the allowed converter
policy, including any necessary product-owned allowlist, and add negative regressions for
application type/property converters. If a public advanced raw protocol is intentionally exempt,
the frozen contract and exit question require an explicit amendment rather than a false
application-surface closure claim.

### P1-3 - The executable scenario remains false-green for its claimed coverage

The request states that the executable codec scenario proves nested-property, collection, and
dictionary rejection plus failure before commit. It does not:

1. `Phase0ScenarioHost.cs:124-137` makes one start call with invalid derived values in `Member`,
   `Items`, and `Map` simultaneously. `ValidateGraph` throws at the first invalid value; that one
   execution cannot reach and prove all three traversal branches.
2. The scenario catches cycle/polymorphism exceptions at lines 107-137 but does not compare provider
   calls, streams, or events before and after either failure. It therefore does not prove the
   "before commit" assertion frozen in
   `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/state-and-codec-scenarios.json:5`.
3. Focused unit tests separately cover a nested property, a collection element, and a dictionary
   value, but there is no dictionary-key regression. The single provider-mutation regression
   covers only the nested-property case.
4. The four scenario guards still pass because observing one expected exception satisfies the
   driver even when the collection and dictionary paths were never reached.

The implementation source does recurse over both dictionary keys and values, but source inspection
is not executable proof of the required branches. The request explicitly makes truthful green
evidence part of the exit bar.

**Required remediation:** execute root, nested property, collection element, dictionary key, and
dictionary value as independent cases; prove zero provider mutation for every durable rejection
path; and make the guard fail if any named path is not separately observed.

## 3. Remediations that are sound

The following parts of the frozen remediation are accepted:

- `IWorkflowPayloadSerializer` and `JsonWorkflowPayloadSerializer` are internal implementation
  details at `JsonWorkflowPayloadSerializer.cs:9-21`.
- The public `DurableWorkflowRuntime` constructor has no serializer parameter and creates the fixed
  implementation; the injection seam is internal at
  `src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs:33-61`.
- Hosting neither registers nor resolves a workflow payload serializer and invokes that fixed
  public constructor at `OrcaCoreServiceCollectionExtensions.cs:71-82`.
- The start-input validator rejects root, nested-property, collection-element, and
  dictionary-value unapproved polymorphism in the focused tests; the source also walks dictionary
  keys.
- A statically approved exact `JsonDerivedTypeAttribute` contract round-trips successfully.
- The focused provider test proves nested start-input polymorphism is rejected with zero event-store
  calls.
- `CompiledWorkflowPlan.CodecFormat` is `orcacore-json-v1`, and the fingerprint seed is exactly
  `FormatVersion|CodecFormat|mode|definitionId|definitionVersion|canonicalStructure` at
  `src/OrcaCore.Core/Compilation/CompiledWorkflowPlan.cs:28-68`.
- `DefinitionModelTests.CompiledPlanFingerprint_BindsTheFixedCodecFormat` independently recomputes
  the SHA-256 value with BCL hashing and a hard-coded codec literal at
  `tests/OrcaCore.Core.Tests/Definitions/DefinitionModelTests.cs:15-28`.
- Earlier static-request retention/resource-scope fingerprinting, attempt-state, reduced-surface,
  consumer-compile, root-only fan-out, legacy-removal, and infrastructure-race remediations remain
  present and their focused/full lanes show no regression.
- All 39 lease/governance executable scenarios remain intentionally red for Sections 6-7.

These resolved points do not cure P1-1 through P1-3.

## 4. Expected-red ownership

The exact expected-red command exited 1 by design with 104 individually named failures, 0 passes,
and 0 skips. There were no restore, build, discovery, setup, or missing-test-assembly signals.

The inventory is 91 executable scenario reds plus 13 product/package/assembly reds:

| Scenario family | Named reds | Later owner |
|---|---:|---|
| State/codec remainder (`3.5`) | 5 | Section 7 portions of mixed rows |
| Structured fan-out (`3.6`) | 9 | Section 5 |
| Facade/hosting (`3.7`) | 11 | Section 7 |
| Application journeys (`3.8`) | 8 | Sections 7-9 |
| Deadline/retry (`3.9`) | 10 | Section 6 |
| DAG (`3.10`) | 9 | Section 8 |
| Lease authoring/admission (`3.11a`) | 10 | Section 6 |
| Lease retry/exit (`3.11b`) | 8 | Section 6 |
| Lease discovery/confirmation (`3.11c`) | 10 | Sections 6-7 |
| Governance/accounting (`3.11d`) | 11 | Section 7 |

The 13 non-scenario failures are the intended later gaps: 1 application-journey package, 1 DAG
contract, 1 facade/hosting, 1 lease authoring/exit, 2 lease discovery/governance, 5 normative
contract, 1 package-consumer, and 1 provider-author failure.

The expected-red inventory is correctly later-owned. It does not contain a regression for any of
P1-1 through P1-3 and therefore cannot authorize Section 4 exit.

## 5. Exact reproduced commands and results

| Command | Independent result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | Exit 0; 0 warnings, 0 errors |
| `dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-restore --no-build -v minimal` | 410 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore --no-build -v minimal` | 148 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-restore --no-build -v minimal` | 282 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj --no-restore --no-build -v minimal` | 15 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --filter "Disposition=Infrastructure" -v minimal` | Run 1: 53/53; run 2: 53/53; run 3: 53/53; every run had 0 failed, 0 skipped, no retry, and no `CS2012` |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --no-build --filter "Disposition=ExpectedRed" -v minimal` | Exit 1 by design; 0 passed, 104 named failures, 0 skipped |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green` | Exit 0; fresh source pack; exact/product-positive consumers compiled; 26 source and 26 package forbidden-member diagnostics verified; incomplete package rejected |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed` | Exit 0; 0 remaining expected-red compile fixtures; Section-4 authoring package proof green |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | Exit 0; valid |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | Exit 0; valid |
| `git diff --check` | Exit 0; no whitespace errors; benign LF-to-CRLF notices only |

Additional focused verification:

- Four Section-4 executable scenario guards: 4 passed, 0 failed, 0 skipped.
- Durable `WorkflowPayloadSerializationTests`: 9 passed, 0 failed, 0 skipped.
- Focused provider pre-mutation polymorphism regression: 1 passed.
- Hosting/public replacement-surface regressions: 2 passed.
- Independent codec-fingerprint regression: 1 passed.

Those focused lanes are green because their assertions do not exercise the blocking paths described
above.

## 6. Answers to the independent review questions

1. **Can an ordinary consumer replace or bypass the fixed codec through any equivalent surface?**
   **No closure is proven.** The old serializer/constructor/DI seam is gone, but the public raw start
   route and application `JsonConverter` route remain.
2. **Does the public durable runtime itself use the fixed codec while its injection seam is
   inaccessible?** **Yes** for that constructor boundary.
3. **Are cycles and unapproved substitutions rejected throughout every required value graph before
   provider mutation?** **No.** Durable start-input coverage improved, but current typed
   state/output paths bypass the validator, dictionary-key execution is absent, and pre-mutation
   proof covers only one nested case.
4. **Does approved `JsonDerivedTypeAttribute` polymorphism round-trip?** **Yes.**
5. **Is `orcacore-json-v1` independently proven in plan identity?** **Yes.**
6. **Does the executable scenario prove all claimed codec paths and pre-commit behavior?** **No.**
7. **Are the 104 expected reds later-owned and lease scenarios still red?** **Yes.** Earlier
   non-codec Section-4 remediations show no regression, but the overall codec blocker remains.
8. **Did the frozen manifest reproduce before one verdict was added?** **Yes:** 339 entries, exact
   delta 0, and the stated SHA-256.

## 7. Exit decision

**Section 4 exit is REJECTED.**

**May Section 5 or task 5.0 begin? NO.** A checked task 4.14 does not override the unresolved
release blockers. Resolve P1-1 through P1-3 (or explicitly amend the governing contract where an
advanced raw boundary is intended), freeze a new exact manifest, rerun the complete packet, and
obtain a new independent approval with no unresolved release blocker.
