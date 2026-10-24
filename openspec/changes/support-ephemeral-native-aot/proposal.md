## Why

[Issue #13](https://github.com/dekrevedko/Workflow-orca/issues/13) owns the compatibility fixes identified by issue #3. The public ephemeral journey currently publishes for trimming and Native AOT but fails at runtime; applications need a reproducible supported path with reflection JSON disabled.

## What Changes

- Add a bounded support contract for .NET 10 self-contained JIT, fully trimmed JIT and Native AOT on Windows x64 and Ubuntu 24.04 x64. Every original assessment scenario must execute successfully; unsupported is not a completion outcome for this issue.
- Add one public metadata registration surface, `OrcaCore.WorkflowJsonMetadata.Register<TContext>()`, using a System.Text.Json source-generated metadata context. Keep `orcacore-json-v1`, its options, product converters, byte/fingerprint behavior, limits and rejection rules fixed. This expressly amends metadata acquisition without admitting replacement serializers, application converters or arbitrary resolvers.
- Apply one explicit .NET 10 Generated-mode admission restriction: reject a reachable JSON member represented by a synthetic member-initializer parameter when its JsonIgnore policy permits omission. Preserve legacy reflection behavior and byte/value parity on the common admitted shapes; the restriction applies even when a particular submitted value would be present. Add the exact F1 omitted/present controls and explicit P3 enum-policy/handwritten-context rejection cases.
- Establish exact process-wide registration/sealing semantics and a static bootstrap recipe: register the ephemeral DI role, register metadata, then author definitions. Preserve existing JIT recipes, DI ownership, shutdown, admission, retention and handles.
- Replace unsupported reflection/dynamic generic dispatch on the selected paths with statically reachable typed operations. Audit downstream steps, events/waits, retry/composition and failure handling rather than treating the first fixed exception as completion.
- Add precise missing-metadata diagnostics and qualify public candidate packages on native Windows Server 2025 and Ubuntu 24.04. Preserve issue #3's historical unsupported evidence and qualify any new support claim separately.
- Use one contract gate, one coherent canonical/docs/source/tests/native-evidence gate, and a publication gate. No source implementation occurs in this target.

## Capabilities

### New Capabilities

None. Extend the selected ephemeral mode and existing verification capabilities.

### Modified Capabilities

- `workflow-contracts`: generated metadata registration and fixed-codec parity/failure rules.
- `developer-facing-surface`: exact public API and initialization/ownership semantics.
- `structured-fiber-execution`: statically reachable closed typed execution.
- `quality-and-verification`: six native support cells and warning/claim gates.

## Impact

Base: GitHub-synced master `3b7f98aa0748a4d9c3f54d4604e566ed61f21bad`.
Branch: `codex/issue-13-native-aot-trimming`.

This contract changes only its new OpenSpec directory and mechanical provenance fixture/guard pins. Source, canonical/numbered specs, declaration companion, API/compile/package baselines, dependencies and earlier artifacts remain unchanged pending review. The later implementation owns those transitions together with their source and exact approval binding.

The selected closure is OrcaCore, OrcaCore.Core and OrcaCore.Engine.Ephemeral plus actual framework/hosting dependencies. No new package, dependency or friend grant is proposed. Durable/DAG/providers, plugins, dynamic loading, new RIDs and a twelve-package AOT claim are outside scope. Existing JIT usage stays compatible; generated-metadata mode has an explicit startup-order requirement and finite value-type closure. No existing unsupported record or released-version claim is overwritten.

R1 supersedes the rejected c0fe638a contract's parity/support wording. Its dated artifacts and rejection verdict remain byte-exact; the R1 remediation, protocol, routing and provenance artifacts govern this revision. The public type/member catalogue is unchanged. The Generated-mode shape restriction is an explicit admission-policy exception to rejection parity, not a new wire format or a change to unconfigured legacy JIT.
