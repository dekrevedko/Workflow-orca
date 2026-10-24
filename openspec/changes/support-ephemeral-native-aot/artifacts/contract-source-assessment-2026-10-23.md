# Issue #13 source assessment and scope

Read-only inspection at master 3b7f98aa0748a4d9c3f54d4604e566ed61f21bad.
No source implementation, native publication or new performance run occurred.

## Observed baseline

The independent issue #3 task-2 verdict approves JIT 16/16 on both native hosts
and records every trimmed/AOT cell Unsupported, including reflection-enabled
diagnostic extras. Default first failure: WF-DEFINITION-INVALID/SFE-TYPE-002 at
DefinitionCompilerOptions.cs:58. Trimmed extra: nonpublic-constructor lookup in
FacadeValueFactory/PayloadFingerprint. AOT extra: RegisterCore runtime property
lookup or missing ValueState/Int32[] metadata. These are demonstrated failures,
not all possible downstream failures and not a supported recipe.

## Inspected implementation paths

| Path group | Current mechanism | Contract disposition |
|---|---|---|
| src/OrcaCore.Abstractions/Serialization/FixedWorkflowValueCodec.cs and Core-linked copy | Private static options; reflective shape/graph/property inspection; runtime Type JSON calls | One shared registered metadata state; fixed-options generated contracts; byte/shape parity and explicit metadata failure. |
| Serialization/WorkflowFailureJsonConverters.cs; Ids/StrongStringValueJsonConverterFactory.cs; facade/event/input fingerprint paths | Product converters and reflective strong/failure construction | Product-owned statically closed converters/direct construction; no app converter hook; preserve bytes. |
| src/OrcaCore.Abstractions/Internal/TypedAuthoringBoundary.cs; Core/Building/TypedAuthoringOperations.cs | Typed operations installed by module initialization with Assembly.Load fallback | Keep JIT compatibility; static role registration installs the native authoring path before Build, with no DI resolution. |
| src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs | JsonSerializerOptions.Default.GetTypeInfo can disguise missing metadata as unsupported shape | Same registry as actual codec; distinguish SFE-TYPE-003 from SFE-TYPE-002. |
| Core/Execution/StructuredInvocationCache.cs; StepResultWaitAccessor.cs; ForEachSnapshotMaterializer.cs; Building/StructuredBranchBuilders.cs | Runtime generic adapters, expression compilation, context/snapshot construction | Preserve closed generic knowledge through internal typed operations and validate value/reference paths natively. |
| Core/Execution/FailureProvenance.cs; Ephemeral execution failures | Nonpublic constructor paths for failure records | Direct construction under existing friend boundaries; nested failure-tree parity. |
| Ephemeral/Facade/EphemeralWorkflowFacade.cs; Catalog/Queries/EventRouter; EphemeralWorkflowEngine.cs | Object property discovery, reflective generic install/start/resume, result and product value construction | Static typed registration/start/wait/event/query operations across the whole journey, with unchanged ownership. |
| Ephemeral hosting, lifetime and retention | Existing registration/duplicate-role, Closed/Stopping/Completed, physical owners and retirement | Behavior unchanged; setup must install the static Core path and never resolve services or capture scoped dependencies. |

The approved task-2 source-risk inventory has 90 rows; use it as the historical
starting map and disposition each site plus newly reached analyzer warnings in
task 2. A risk row is not a demonstrated runtime failure. No warning count is
invented for the proposed implementation, and the historical 268 warning
identities/316 occurrences/120 runtime findings remain bound to their old source.

## Current target exclusions verified by path scope

No src/, canonical openspec/specs/, docs/, dependency/project/package input,
public API/compile/package baseline, runtime test, workflow, earlier change
artifact or post-gate registry edit is part of this target. Only the new change
directory and the provenance fixture plus independent guard pins change.
Existing Abstractions grants already include Core/Ephemeral; no extra friend is
needed. OrcaCore.Core exports no public API at base and stays internal-only under
this catalogue. The new class belongs to the main OrcaCore package/assembly.
