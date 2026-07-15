# Developer-Facing Interface Refactor - Post-Fiber Review Brief

Date: 2026-07-14

Change under review: `reshape-developer-facing-interfaces`

Related active change: `add-runtime-concurrency-limits`

Accepted substrate: archived `2026-07-15-adopt-structured-fiber-execution`

## Review outcome requested

Decide whether the rebased OpenSpec materials are ready for source implementation from the
point of view of an external .NET developer consuming OrcaCore. The review should approve,
approve with changes, or reject the proposed application surface and implementation sequence.

This is a greenfield API with no API clients or external compatibility dimension. Existing
repository paths are provisional implementation choices, not compatibility contracts. A reviewer
should select the clearest final compile-time API and delete every inferior parallel path,
alias, shim, or dual execution route.

## Materials

Normative and change artifacts:

- [selected-mode capability and signature matrix](../specs/17-selected-mode-capability-matrix.md)
- [proposal](../../openspec/changes/reshape-developer-facing-interfaces/proposal.md)
- [design](../../openspec/changes/reshape-developer-facing-interfaces/design.md)
- [implementation tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md)
- [developer-facing surface delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/developer-facing-surface/spec.md)
- [workflow-authoring delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md)
- [workflow-contracts delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/workflow-contracts/spec.md)
- [durable-runtime delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/durable-runtime/spec.md)
- [management-and-querying delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md)
- [repository-foundation delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/repository-foundation/spec.md)
- [quality-and-verification delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md)
- [saga-orchestration delta](../../openspec/changes/reshape-developer-facing-interfaces/specs/saga-orchestration/spec.md)

Review and dependency context:

- [original OpenSpec review](developer-facing-interface-openspec-review-2026-07-13.md)
- [structured-fiber implementation status](structured-fiber-execution-implementation-status-2026-07-13.md)
- [runtime-concurrency proposal](../../openspec/changes/add-runtime-concurrency-limits/proposal.md)
- [runtime-concurrency design](../../openspec/changes/add-runtime-concurrency-limits/design.md)
- [runtime-concurrency governance delta](../../openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md)

## Current validation state

- `reshape-developer-facing-interfaces`: **15 of 111 tasks complete** after recording the
  accepted fiber baseline; remaining tasks are implementation work.
- `add-runtime-concurrency-limits`: **7 of 15 tasks complete** after accepting the delivered
  ephemeral baseline; remaining tasks cover durable and cross-mode governance.
- `openspec validate reshape-developer-facing-interfaces --strict`: **valid**.
- `openspec validate add-runtime-concurrency-limits --strict`: **valid**.
- No application source or test implementation was changed by this documentation rebase.

## Why the rebase was required

The original proposal correctly treated structured fibers as a prerequisite. That prerequisite
has since delivered and been archived with its full verification matrix. The active public-API
change still described the compiler, driver, mode-first factories, `WaitLong`, durable
`ForEach` rejection, child workflows, and structural continue-as-new as future work.

The implementation also revealed new public-surface problems that did not exist when the
original proposal was drafted:

- both mode-first builders return the same `WorkflowDefinition<TState>`;
- `WorkflowDefinition<TState>.CompiledPlan` exposes executable IR;
- compiled plan/instruction/scope/branch/policy identity types are public;
- the common nested `BranchBuilder` exposes ephemeral-only `WithPoolKey` inside durable
  structured authoring;
- public compiler-shaped options and serializer registries appear directly on normal builders;
- `ActiveWaitSnapshot` exposes `FiberId`, `ScopeId`, and `WaitSequence` routing internals;
- typed state inspection had no explicit rule distinguishing root business state from
  branch-private fiber state.

The rebase turns those facts into explicit contracts and tasks rather than leaving them for
implementation-time interpretation.

## Principal design decisions for approval

### 1. Mode selection is present in the definition type

`EphemeralWorkflowBuilder<TState>.Build()` returns
`EphemeralWorkflowDefinition<TState>` and durable authoring returns
`DurableWorkflowDefinition<TState>`. Normal engine registration accepts only its matching
definition family. The refactor deletes `RequiresDurableEngine`, registration-time mode
inference, and the fallback plan path.

Review question: does this make invalid cross-mode registration unrepresentable without
duplicating the definition implementation?

### 2. Selected mode survives nested authoring

Ephemeral and durable public branch/scope builder families share implementation internally but
retain different static types. An ephemeral-only transient-pool method cannot reappear inside a
durable `Parallel` or `WhenFirst` merely because both roots reuse one branch class.

Review question: are separate public branch families the clearest compile-time boundary, or is
there an equally safe smaller signature that preserves IntelliSense absence?

### 3. Executable compiler IR is not an application contract

Application definitions expose immutable identity, version, mode, fingerprint, and authored
metadata. Compiled plans, instructions, scope/branch/policy models, identity indexes, and
executable delegates remain implementation-only and are accessed across engine assemblies by
internal/friend boundaries or an implementation-only project.

Domain-facing `WorkflowAuthoringOptions` preserve legitimate limits, payload serializer/copy
registration, and deterministic fingerprint contribution without public
`WithCompilerOptions` / `WithTypeSerializerRegistry` methods.

Review question: is the application/implementation split implementable without reintroducing a
public compiled-plan type through another package?

### 4. Static builders expose mode-guaranteed capabilities

A `Workflow.Durable<TState>` static type cannot change according to a DI host selected later.
Therefore:

- per-step execution throttles begin as host policy rather than a host-dependent builder method;
- named transient-pool authoring remains ephemeral-only because its implementation is delivered
  there;
- durable transient-pool authoring remains absent until the runtime and every supported durable
  host enforce restart reset/re-admission semantics;
- persisted durable resource leases remain a separate durable structural capability.

Review question: does this remove the prior impossible "host-selected builder" promise while
leaving a coherent future amendment path?

### 5. Structural durable effects precede portable-result cleanup

External jobs and durable leases become typed structural nodes that lower directly into the
archived fiber plan. Only after those paths pass does the refactor remove `ContinueAsNew`,
`RunExternalJob`, and `AcquireResources` from portable `StepResult`.

Review question: does the sequencing prevent a temporary loss of durable capability and avoid a
second effect-translation layer?

### 6. Application inspection projects authored/business facts

`GetStateAsync<TState>` returns a detached value of the last committed **root workflow
business state**, never branch-private or item-private fiber payloads. Application wait models
expose authored node/path, event/correlation, residency, and logical timing but not `FiberId`,
`ScopeId`, or `WaitSequence`. Advanced runtime observations retain those identities for
certified custom hosts.

Review question: are the projected fields sufficient for application management and operator
tooling without leaking routing ownership?

### 7. Superseded provisional paths are deleted before package splitting

The task graph now removes the superseded mixed-mode builder, mode inference, and dual plan fallback before
moving types into new packages. This avoids approving and transporting superseded APIs into the
new package baselines.

Review question: does any dependency require package splitting first, or is consolidation-first
the lower-rework sequence?

## Delivered versus remaining authoring work

| Area | Disposition after fiber archive |
|---|---|
| Shared compiler, typed scopes/results/merges, fiber driver | Accepted delivered substrate |
| Root `Workflow.Ephemeral` / `Workflow.Durable` factories | Delivered; consolidate |
| Durable `ForEach` absence plus compiler rejection | Delivered |
| Structural `Wait`, public `WaitLong`, portable dynamic wait | Delivered; delete old helper |
| Durable children and root-only continue-as-new | Delivered; delete superseded parallel paths |
| Mode-specific definition types | Missing |
| Mode-safe nested branch builders | Missing |
| Structural external jobs and durable leases | Missing |
| Portable `StepResult` cleanup | Blocked on structural effects |
| Compiler/fiber public-surface cleanup | Missing |
| Saga/DAG `Build()` / `TryBuild()` alignment | Missing |
| Superseded mixed-mode `WorkflowBuilder` and dual execution fallback deletion | Missing and deliberately early |

## Source facts to verify

The reviewer should validate these against the current root source rather than trusting the
proposal:

- [shared definition type and public compiled plan](../../src/OrcaCore.Core/Definitions/WorkflowDefinition.cs)
- [mode-first builders and compiler-shaped options](../../src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs)
- [common nested branch builder](../../src/OrcaCore.Core/Building/StructuredBranchBuilders.cs)
- [superseded mixed-mode builder](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs)
- [public compiled plan](../../src/OrcaCore.Core/Compilation/CompiledWorkflowPlan.cs)
- [public compiled plan models](../../src/OrcaCore.Core/Compilation/CompiledPlanModels.cs)
- [public compiler options and serializer/fingerprint contracts](../../src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs)
- [durable-only portable results](../../src/OrcaCore.Abstractions/Steps/StepResult.cs)
- [application wait snapshot routing fields](../../src/OrcaCore.Abstractions/Instances/ActiveWaitSnapshot.cs)
- [current durable facade](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs)

## Expected reviewer output

The report should contain:

1. an overall verdict: approve, approve with changes, or reject;
2. P0-P3 findings with direct artifact and source evidence;
3. a claim-verification table for the source facts above;
4. a disposition for each of the seven principal decisions;
5. missing or contradictory requirements and scenarios;
6. a task-order audit, including whether prerequisites and deletion order are executable;
7. semantic conflicts with `add-runtime-concurrency-limits` or the archived fiber baseline;
8. exact document-level changes required before implementation;
9. strict-validation commands and results.

The review is documentation-only. It should not implement source changes or silently edit the
proposal under review.
