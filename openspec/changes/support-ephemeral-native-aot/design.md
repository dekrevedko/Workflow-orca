## Context

Issue #13 follows the approved issue #3 assessment at native run 39721105482. Both native JIT controls passed 16/16. Default trimmed/AOT artifacts failed during definition/schema resolution; explicitly enabling JSON reflection advanced to nonpublic-constructor, runtime-property and closed-type metadata failures. Those results remain immutable historical facts. This contract authorizes no product implementation.

## Goals / Non-Goals

Goals: support the finite public ephemeral journey under default reflection-disabled trimming/AOT; preserve the fixed codec and ownership; make metadata/configuration failures actionable; remove unsupported dynamic dispatch throughout the selected paths; qualify actual public packages on six native cells.

Non-goals: durable/DAG/provider qualification, dynamic plugins, unrestricted runtime-discovered CLR types, alternate serializers, application converters, additional packages or friends, other RIDs, a performance advantage, or changing the earlier assessment to say it passed. Package-wide compatibility attributes require broader proof than this selected journey and are not promised here.

## Decisions

### 1. One additive public metadata surface

Assembly/package OrcaCore, namespace OrcaCore:

```csharp
public static class WorkflowJsonMetadata
{
    public static void Register<TContext>()
        where TContext : System.Text.Json.Serialization.JsonSerializerContext, new();
}
```

This is the entire added public type/member catalogue. No instance constructor, reset, unregister, resolver/options/converter parameter, new registration overload or package is added. Existing public signatures and role fingerprints remain unchanged. Implementation may replace private reflection with direct operations under existing friend grants. Any additional public attribute, generic-parameter annotation or signature requires an explicit reviewed catalogue amendment before baselines change.

The application supplies one ordinary System.Text.Json source-generated context covering every application-owned input, state, output, event, item, branch/result and collection closure. It uses metadata generation (Metadata or Default), default serializer settings and no custom converter or resolver. Product-owned primitive/strong-value/failure contracts are installed by the library. Product-owned converters always retain priority. Unsupported source-generation shapes, including the explicit .NET 10 omission/member-initializer restriction in decision 5, are diagnosed explicitly and are outside the finite native support boundary; ordinary JIT's existing reflection path remains available when metadata is never registered.

The supplied context describes types; it does not choose wire behavior. The engine creates its own fixed options and requests metadata against those options through the context's generated resolver implementation. It ignores app context settings as configuration and rejects nondefault options/custom converters, serialization-only contexts and mutable/custom resolver chains. Validate the actual metadata graph before use, including property converters, collection closure, derived types and required read/write metadata. Neither accepting JsonTypeInfo instances nor trusting app report serialization constitutes a solution. P3 adds two mandatory configuration negatives: a source-generated context with UseStringEnumConverter=true must be rejected even when its Options.Converters list is empty and engine options are default; the review's handwritten context that renames Value to changed must also be rejected despite satisfying JsonSerializerContext/new() and presenting default options. Both raise NotSupportedException identifying the context and unsupported policy/provenance, install nothing, preserve retry before first use and cause no workflow effects. Generic constraints, default options and a zero converter count alone are insufficient validation. Bounded validation of source-generation configuration/provenance and the actual metadata remains required; copied marker strings are not a security attestation. These are ordinary configuration conformance controls, with no promise to authenticate deliberately forged application code.

Alternative: a serializer/options/resolver hook would change fixed-codec semantics and is rejected. An additional OrcaCore source generator is unnecessary for this first supported boundary; use the .NET generator and preserve generic knowledge at the existing typed authoring boundary.

### 2. Process-wide startup configuration with an irreversible seal

The fixed codec is currently static and compiled into both OrcaCore and Core. These copies must share one authoritative metadata registration and seal through existing internal access; divergent registries are forbidden. Configuration is process-wide, including multiple service providers. It is not a DI service, per-host option, provider-owned disposable, or something a stopped host can reset.

Initially Unconfigured. The first successful `Register<TContext>()` constructs one owned context and installs Generated mode atomically. Repeating that exact CLR context type is a no-op, before or after the seal, and does not construct another instance. A different context type throws InvalidOperationException before construction/mutation; combine all roots into one context instead. Context construction/options validation failures publish nothing and leave configuration retryable. A failed conflicting registration cannot alter the incumbent.

The first workflow authoring/type-contract/codec operation seals the mode for the process, including a lookup that subsequently fails. Without registration it seals legacy Reflection mode; a later first registration throws InvalidOperationException. With registration it seals Generated mode; missing metadata never falls back to reflection. Concurrent configuration and first use have one linearization order: either registration wins in full, or sealing wins and registration fails. Construction/validation must not hold a global lock while executing application context code; reserve/publish safely and reject reentrancy rather than deadlock. Configuration failures identify the existing/requested context and whether the process is sealed. There is no public reset. Registry-isolation tests run in fresh processes.

One-context startup is a deliberate bounded contract: all modules must contribute roots to the application's context before workflow use. This avoids order-dependent resolver chains and contexts with conflicting representations. A host does not own or dispose the context; no IServiceProvider or scope may be captured. Registering metadata does not admit work, start timers, construct steps or create a service provider.

### 3. Static bootstrap before authoring on the native path

Recipe: create IServiceCollection; call the existing AddOrcaCoreEphemeralEngine role registration; call WorkflowJsonMetadata.Register<AppJsonContext>(); author/build definitions; build the provider; register/start workflows. Role registration must synchronously install the existing Core typed-authoring boundary through a direct static reference without resolving DI services or building a provider. The metadata call itself never attempts Assembly.Load. No Core public export or extra friend grant is needed.

The existing JIT definition-first recipe remains compatible. The supported native recipe requires role registration before authoring and never relies on the Assembly.Load fallback, reflective module initialization or runtime discovery. Add a native bootstrap negative test with an actionable error for authoring without installation; do not crash with a missing assembly/constructor/property message. A fresh-process JIT control also checks the existing missing-engine diagnostic.

### 4. Preserve generic knowledge through the runtime

Carry closed generic operations from typed authoring/registration rather than reconstructing them from Type/object later. Internal typed adapters cover definition identity and installation, staged definitions, start snapshots, state/output queries, step contexts, delegate bodies/selectors/merges, typed StepResult waits, incoming/outgoing events, snapshots and branch/item materialization. Product value/failure construction uses direct constructors within existing access boundaries. No application bridge to a private implementation is permitted.

The supported paths must not depend on MakeGenericType/MakeGenericMethod, DynamicInvoke, compiled expression IL, property-name dispatch, nonpublic-constructor discovery, dynamic assembly loading or runtime-only generic instantiation. Bounded reflection with proven preserved metadata can remain only when individually justified and AOT-analyzable; a blanket rooting descriptor is not proof. Audit every original risk site and warnings, including paths masked by the first old failure. Statically close value-type and reference-type cases; Native AOT generic sharing must not be assumed to materialize arbitrary value-type instantiations.

### 5. Fixed bytes, explicit metadata failure

Keep `orcacore-json-v1`, exact JSON defaults, enumeration ordering, product converters, 256 KiB value and existing envelope limits, detached values, rejected custom/cyclic/unapproved polymorphic/collection shapes and definition/input fingerprints. On the common admitted shape set, byte, value and rejection parity remain mandatory. The single Generated-mode admission exception below is explicit; legacy Reflection mode retains its existing acceptance and round-trip behavior. Generated contexts do not enter fingerprints or host option agreement. Compare a golden corpus across legacy JIT, generated JIT, trimmed and AOT, including strong IDs and failure trees; check encode/decode and idempotency conflicts, not only equal business results.

Generated mode covers closed public source-generatable DTO/value/nullable/enum shapes subject to the explicit restriction below, the existing allowlisted arrays/lists/string-keyed maps and explicitly declared polymorphic closures that obey the existing codec. Dynamic object slots need every permitted concrete type in the finite context; unapproved runtime subtypes still reject. Unsupported inaccessible members/customizations remain explicit and must not be silently omitted to obtain a round trip.

#### R1 / F1: reject omittable synthetic member initializers on .NET 10

Choose an early declared-shape rejection, not a serializer rewrite or a dependency on .NET 11. In Generated mode, any reachable JSON member for which JsonPropertyInfo.AssociatedParameter.IsMemberInitializer is true and JsonIgnoreAttribute.Condition is present and not Never makes the containing declared value closure unsupported. This conservative rule rejects the entire declared shape even when the current value would be written or its initializer happens to be zero. It covers properties and included fields, roots, transitive DTOs, approved derived types and collection element/value contracts. A normal positional record/value-state with no JsonIgnore omission policy remains admitted; a genuine constructor-bound parameter with IsMemberInitializer=false is not excluded by this rule. An ignored member with no associated initializer parameter is not excluded by this rule either. All other existing codec checks still apply.

Classify from frozen generated metadata and its member-attribute provider after metadata configuration; do not create an application model, run a getter, inspect initializer IL, deserialize a sample or depend on the current value. The public .NET 10 metadata API exposes the initializer flag, and the framework-only R1 probe confirms it and the JsonIgnore policy are available before a model is constructed. Required native evidence must also prove these facts remain available after trimming/AOT; a missing/unprovable classifier is a qualification failure, not permission to accept the shape or root private implementation metadata.

Known definition closure containing this shape fails Build/TryBuild with SFE-TYPE-002 and existing WorkflowDefinitionException/validation shapes. The diagnostic names the root, containing type/member, Generated-mode .NET 10 omission restriction and a remedy (ordinary settable property, a genuine JSON-constructor-bound member, or removal of the omission policy). Direct value/event boundaries throw NotSupportedException with equivalent detail before consuming the event or mutating an instance. This is unsupported shape, never SFE-TYPE-003; no start, user-step execution, start-key binding or commit is permitted for known invalid closure. Do not reject the entire otherwise-valid metadata context merely because it contains an unused excluded type; validate the closure actually requested. A dynamically encountered type is rejected at its boundary before commit.

The exact rejected example is InitState.Count with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Count { get; init; } = 42`. Legacy Reflection mode still round-trips submitted Count=0 through `{}` to 42 and Count=7 through a present member to 7. Generated mode must reject that declared type for both submitted values, never silently return 0 for the omitted case or admit only the present case. Add both root and nested versions. Positive controls retain the original readonly record struct ValueState(int Number), a mutable settable DTO with the same omission/initializer policy, and a genuine constructor-bound record; check omitted and present values against the unchanged legacy behavior. Removing this restriction or admitting an alternative construction strategy requires another reviewed contract amendment; no runtime-version upgrade relaxes it implicitly.

Missing generated metadata is distinct from an unsupported codec shape. Add diagnostic `SFE-TYPE-003` (CodecMetadataMissing): Build/TryBuild report the exact declared type, missing transitive type if different, and the context; Build preserves WorkflowDefinitionException and TryBuild preserves its validation result shape. Keep SFE-TYPE-002 for genuinely unsupported shapes. A direct public value/event codec boundary with no definition reports NotSupportedException naming the missing type/context and generated-context remedy. No new exception type/result variant. Validate known definition closure before admission; validate late event/runtime-value closure before consuming the event or mutating any instance. No user step, commit or start-key binding may occur on such failure. Do not require inspection of opaque user delegate bodies; validate dynamic output immediately at its boundary before commit.

### 6. Native support is an executable gate

Use external PackageReference-only consumers, exact candidate source/package hashes and native windows-2025/win-x64 plus ubuntu-24.04/linux-x64. Each host executes self-contained untrimmed JIT, full-trim JIT and Native AOT. Trimmed/AOT reflection JSON stays SDK-default disabled and the artifact asserts that fact. A generated-metadata JIT run checks parity; a separate unconfigured JIT control checks backwards compatibility. No WSL, container or cross-publish fills a native cell.

Run AOT-01–16 from the immutable task-2 protocol with every original assertion retained, plus NS-01–12 and their mandatory F1/P3 subcases in contract-r1-consumer-protocol-2026-10-23.md. The F1 rejection is an asserted negative case; it does not turn a cell or any original positive journey into Unsupported. Earlier expected-unsupported rows are historical; new success expectations are a separate versioned record. Missing native hosts keep implementation incomplete. One fixed failure, a successful publish, a skipped scenario or a new Unsupported result cannot satisfy issue #13.

Retain detailed trim/AOT/generator diagnostics and individual classifications with no blanket suppression, reflection switch or private/whole-assembly roots. Resolve all OrcaCore-attributable warnings on the supported closure; third-party/toolchain warnings require explicit cause, supported-path safety and independent approval. Unexpected warnings fail the gate. Enable analysis in the affected projects during the source target; add broad IsAotCompatible/IsTrimmable metadata only after every exposed path is actually qualified or truthfully annotated in an amended public catalogue. Otherwise keep package metadata conservative and document the selected recipe.

The normal lanes stay dotnet test as CI invokes them. The user's runner preference is retained for later evaluation; a test-runner migration is outside this issue. No source target may replace CI-path stress evidence with direct-runner evidence.

## Risks / Trade-offs

- Process-global metadata makes late module configuration impossible after first use -> document startup order, one complete context, repeat/scope/concurrency tests and a deterministic sealed error.
- JSON metadata alone does not repair dynamic dispatch -> retain a complete source-risk disposition map and native coverage of typed value and reference cases.
- A source-generated context can omit members or bake custom behavior -> reject customization and inaccessible required members, apply the explicit F1 admission restriction, validate metadata, and prove byte/value/negative-case parity. Assert the two baked-metadata P3 negatives. Another parity issue requires an amendment, not silent codec drift.
- Shared Abstractions/Core implementation currently duplicates codec source -> share registry state through existing internal boundaries; do not refactor unrelated lane ownership.
- Native evidence consumes disk -> serialize publishing where practical, retain compact logs/manifests/results, remove owned output/cache/worktrees after verification and preserve existing review deliverables.

## Migration Plan

1. Freeze this green contract without source/canonical work; independent review first. After explicit approval, commit the exact manifest and then a verdict-only direct child.
2. One coherent target binds approval, appends canonical deltas, reconciles numbered docs/declaration/guide owners and process registry, implements tests and source, updates executable baselines alongside source, and collects both native-host results. It requires independent review before checkpointing. No separate activation/checkbox target.
3. Publish through authorized PR/CI/merge gates with a merge commit. Keep old version/issue #3 results dated; claim support only for the new qualified source/packages/profile. Archival, other engines and broader compatibility remain separate work.

## Open Questions

No deferred public-API or ownership decision. Implementation feasibility, exact warning identities and native execution results are obligations of the later target. If its implementation needs another API, different bytes or a different supported boundary, amend and review the contract before implementation proceeds beyond that boundary.
