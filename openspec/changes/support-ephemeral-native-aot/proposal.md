## Why

[Issue #13](https://github.com/dekrevedko/Workflow-orca/issues/13) owns the compatibility fixes identified by issue #3. The public ephemeral journey currently publishes for trimming and Native AOT but fails at runtime; applications need a reproducible supported path with reflection JSON disabled.

## What Changes

- Add a bounded support contract for .NET 10 self-contained JIT, fully trimmed JIT and Native AOT on Windows x64 and Ubuntu 24.04 x64. Every original assessment scenario must execute successfully; unsupported is not a completion outcome for this issue.
- Add the public metadata registration surface, `OrcaCore.WorkflowJsonMetadata.Register<TContext>()`, using a System.Text.Json source-generated metadata context. The approved converter catalogue amendment dated 2026-10-25 additionally specifies public accessibility for the existing sealed `OrcaCore.Abstractions.Ids.StrongStringValueJsonConverterFactory`, its parameterless constructor and its two existing overrides, so ordinary external generated DTOs can reference the product converter. Keep `orcacore-json-v1`, its options, the eleven-type converter table/body and strong-value attributes, byte/fingerprint behavior, limits and rejection rules fixed. This amends metadata acquisition/accessibility without admitting replacement serializers, application converters or arbitrary resolvers.
- Apply one explicit .NET 10 Generated-mode admission restriction: reject a reachable JSON member represented by a synthetic member-initializer parameter when its JsonIgnore policy permits omission. Preserve legacy reflection behavior and byte/value parity on the common admitted shapes; the restriction applies even when a particular submitted value would be present. Add the exact F1 omitted/present controls and explicit P3 enum-policy/handwritten-context rejection cases.
- Establish exact process-wide registration/sealing semantics and a static bootstrap recipe: register the ephemeral DI role, register metadata, then author definitions. Preserve existing JIT recipes, DI ownership, shutdown, admission, retention and handles.
- Replace unsupported reflection/dynamic generic dispatch on the selected paths with statically reachable typed operations. Audit downstream steps, events/waits, retry/composition and failure handling rather than treating the first fixed exception as completion.
- Add precise missing-metadata diagnostics and qualify public candidate packages on native Windows Server 2025 and Ubuntu 24.04. Preserve issue #3's historical unsupported evidence and qualify any new support claim separately.
- Use one contract gate, one coherent canonical/docs/source/tests/native-evidence gate, and a publication gate. The R1 contract checkpoint contained no source implementation; task 2 is approved at 8566986 / 1ad2a59; task 3.1 is publication preparation under review.

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

The original R1 contract checkpoint changed only its new OpenSpec directory and mechanical provenance fixture/guard pins. It deferred source, canonical/numbered specs, declaration companion and API/compile/package baselines to task 2. That coherent target is approved and checkpointed at 8566986 / user-verdict child 1ad2a59. The explicitly approved 2026-10-25 amendment is now applied in task 2.2: the exact accessibility change, amended canonical text and matching declaration/API/compile/package baselines move together with source. Typed dispatch and the six native cells are approved; publication/merge gates remain pending. Earlier dated contracts, verdicts, protocols and artifacts remain unchanged.

The selected closure is OrcaCore, OrcaCore.Core and OrcaCore.Engine.Ephemeral plus actual framework/hosting dependencies. No new package, dependency or friend grant is proposed. Durable/DAG/providers, plugins, dynamic loading, new RIDs and a twelve-package AOT claim are outside scope. Existing JIT usage stays compatible; generated-metadata mode has an explicit startup-order requirement and finite value-type closure. No existing unsupported record or released-version claim is overwritten.

R1 superseded the rejected c0fe638a contract's parity/support wording without changing its public catalogue. Its dated artifacts and rejection verdict remain byte-exact. The explicitly approved [converter catalogue amendment](artifacts/contract-converter-catalogue-amendment-2026-10-25.md) specifies the converter catalogue expansion, supplemental NS-02d/NS-06g–h controls and owner routing; its explicit user approval is recorded in [the catalogue binding](artifacts/task-2-2-catalogue-approval-binding-2026-10-25.json). The R1 remediation, protocol, routing and provenance artifacts otherwise remain authoritative. The Generated-mode shape restriction is an explicit admission-policy exception to rejection parity, not a new wire format or a change to unconfigured legacy JIT.

The explicitly approved 2026-10-26 throttle metadata amendment authorizes only
DynamicallyAccessedMembers(Interfaces) on the existing TStep parameter of
OrcaCore.Hosting.StepExecutionThrottle.For<TStep>(int maxConcurrency). The body,
capacity-first validation, exception parameter names, exact named-step identity
and physical-slot ownership stay unchanged. NS-09a covers concrete/inherited
IStep contracts, annotated generic callers and invalid types/capacities in JIT,
full trim and Native AOT, and requires IL2090 to disappear without suppression.
Generic application wrappers must propagate this interface metadata requirement.
This method-scoped proof grants no package-wide compatibility badge. Approval is
bound in artifacts/task-2-3-throttle-approval-binding-2026-10-26.json; the original
proposal and held review remain immutable. Task 2 is approved at 8566986 / 1ad2a59; publication remains pending.

### Approved 2026-10-27 public-consumer boundary

NS-10 follows the bounded public recipe approved in the adjacent amendment:
typed inbound value construction/materialization, typed StepResult.WaitForEvent
parking and physical cancellation/join, timers/retries, transient-pool grants,
root Parallel and bounded ForEach. The public ephemeral closure exports neither
live event ingress/resumption nor outgoing Publish. These receive no native
credit. Existing internal event regressions remain required, without substituting
for public-package execution. NS-07/08/11 prove public value rejection and
retained-instance state; they make no public delivery claim. Every original
AOT-01 through AOT-16 assertion, NS group and mandatory subcase remains required.
A future ingress/publish capability requires its own reviewed public contract.
This is an explicit qualification-scope adjustment, not the old full event
requirement passing. Source/public API and the fixed codec are unchanged.

### Post-review codec corrections (2026-10-28)

The whole-AOT review found an extra Generated-mode write-only rejection and a
pre-existing runtime-validation gap for included fields/internal getters. The
correction target changes three internal serialization files to enforce the
existing contract in both modes. Write-only members retain legacy admission;
unsupported runtime subtypes/collections actually read by the serializer now
reject rather than silently losing data. Valid bytes/fingerprints and getter
counts remain unchanged. No public catalogue, canonical/delta requirement or
executable API baseline changes. Run 40762764617 requalifies these exact corrected
bytes in six native cells, 384 cases total. This target and publication preparation
remain uncommitted for review; the old checkpoint's approval does not approve the
corrections, and the temporary evidence push authorizes no issue-branch push or PR.
