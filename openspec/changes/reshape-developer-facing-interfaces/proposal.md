Joint implementation baseline: [`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md).
That document is the single normative selected-mode matrix, post-fiber signature baseline,
compiler/diagnostic contract, and concurrency taxonomy shared with
`adopt-structured-fiber-execution` and `add-runtime-concurrency-limits`.

## Why

OrcaCore's application Interface is currently mixed with durable kernel, provider, hosting, and orchestration Interfaces, so ordinary consumers can author unsupported definitions and must use raw commands to complete common durable workflows. The project has no external users yet, and its specification already calls for mode-specific authoring, making this the lowest-cost point to establish compile-time capability separation and one successful application path before provisional types become compatibility obligations.

## What Changes

- **BREAKING** Replace the mode-ambiguous authoring surface with explicit ephemeral and durable authoring entry points. A caller selects execution mode before capability-specific methods become discoverable; `WaitLong`, child workflows, external jobs, durable resources, continue-as-new, and durable definition policies are available only on the durable Interface, while ephemeral `ForEach` remains ephemeral-only.
- **BREAKING** Remove durable-only outcomes from the common `StepResult` Interface. Express durable orchestration through durable authoring nodes or an equally capability-safe durable step Interface so unsupported results cannot first fail during ephemeral execution.
- **BREAKING** Make `DurableWorkflowRuntime` the complete application facade for explicit host-scoped definition registration, typed start-or-get handles, event delivery, external-job completion/failure/timeout, and instance progression. Application operations own time and identifiers where appropriate and return application-level results rather than raw commands or `DurableCommandResult`; a split callback host commits the outcome and guarantees continuation handoff even when it cannot execute the definition locally.
- **BREAKING** Move `DurableCommandProcessor`, durable commands/events, commit/checkpoint records, driver pumps, observers, DAG scheduling primitives, and provider ports out of the normal application Interface into explicit runtime-protocol and provider-authoring seams. Make implementation-only hosted services, converters, profiles, and adapters internal or delete them when the deletion test shows no supported external use.
- **BREAKING** Align ephemeral and durable management around asynchronous selection and instance handles with consistent `ListAsync`, `CountAsync`, `GetAsync`, `GetStateAsync<TState>`, active-wait, and lifecycle command naming. Preserve ephemeral `EvictAsync`, `EvictTerminalAsync`, `DetectStuckAsync`, and `GetLifecycleEventsAsync` as explicitly in-memory capabilities; keep persistence-only capabilities and application-safe durable remediation explicitly durable.
- **BREAKING** Remove overloads that can only throw, make the default destructive-confirmation value unconfirmed, and require confirmation only where destructive breadth or retention semantics justify it.
- Close durable DAG and saga progression behind runtime-owned loops. Keep DAG planning distinct from durable execution and demote the interim saga command adapter and caller-driven DAG scheduler from the application Interface.
- Replace ambiguous all-in-one hosting registration with explicit engine-mode registration, and standardize provider extensions by provider role (store, projection cache, or dispatcher), including a ZeroMQ dispatcher extension.
- Publish separate application, engine, hosting, provider-authoring, and runtime-protocol packages plus a small `OrcaCore` meta-package; allow the provider-authoring package to depend on runtime protocol facts it persists while forbidding the reverse edge and every application-to-advanced edge.
- Define concurrency vocabulary in three distinct classes: per-step execution throttles, named cross-instance transient pools, and persisted durable resource leases with scope-owned release and crash recovery.
- Standardize builder completion and validation behavior: reject local invalid arguments immediately, aggregate graph-wide diagnostics at build time, and use the same completion vocabulary across workflow, saga, and DAG builders.
- Preserve strong identifier types while rejecting empty/default identifiers at every public construction and operation seam; a default value must never become a valid workflow, command, or stream identity.
- Move optional OpenTelemetry exporters out of base hosting so a consumer pays for only the observability Adapter it selects.
- Add public-surface approval/compile tests, package-consumer smoke tests, a joint capability matrix with `adopt-structured-fiber-execution`, canonical requirement updates, and successful public-Interface-only samples. The durable sample must exercise a live external job and must never present `Poisoned` as the expected result.

## Capabilities

### New Capabilities

- `developer-facing-surface`: Defines application, provider-authoring, runtime-protocol, and internal Interface tiers; application hosting entry points; provider-role registration conventions; and the supported golden paths for library consumers.

### Modified Capabilities

- `workflow-authoring`: Makes execution mode explicit before capability-specific authoring, publicly exposes durable `WaitLong`, and standardizes validation and builder completion.
- `workflow-contracts`: Restricts common step outcomes and application contracts to mode-portable concepts while relocating provider and durable protocol contracts to their supported seams.
- `durable-runtime`: Requires the durable runtime facade to own normal durable progression and application-level operation results without raw-command use.
- `management-and-querying`: Aligns query and instance-handle vocabulary, typed state access, time ownership, and destructive-operation safety across execution modes.
- `saga-orchestration`: Requires durable saga execution and compensation progression to be runtime-owned rather than caller-driven through an interim command adapter.
- `repository-foundation`: Replaces the broad abstractions package topology with explicit application, provider-authoring, and runtime-protocol dependency direction.
- `quality-and-verification`: Adds public-Interface baselines, compile-time capability checks, package-consumer tests, and successful golden-path sample assertions.

## Impact

- Affected root projects: `OrcaCore.Abstractions`, `OrcaCore.Core`, both engine projects, `OrcaCore.Hosting`, all provider projects, root samples, and their tests. The root implementation remains authoritative.
- The change intentionally breaks provisional source and package compatibility; no compatibility shims, obsolete aliases, or migration path are required.
- The active `adopt-structured-fiber-execution` change also reshapes workflow authoring, `StepResult`, DAG, saga, and durable execution. Its compiler, structured driver, typed merge/result shapes, and root-only quiescent continue-as-new semantics are an explicit prerequisite: both changes must share one capability matrix and signature baseline before source implementation begins.
- The active `add-runtime-concurrency-limits` change owns named cross-instance transient-pool enforcement. This change owns the developer-facing taxonomy and must not expose that capability in a mode until the selected host implements it.
- Provider authors retain supported extension contracts, but those contracts move to explicit packages/namespaces and are no longer presented as ordinary application dependencies.
- Application tests and samples migrate away from `DurableCommandProcessor`, caller-supplied command timestamps, raw command/event records, and manually reconstructed DAG scheduling state.
