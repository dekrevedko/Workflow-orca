Joint implementation baseline: [`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md).
That document is the single normative selected-mode matrix, post-fiber signature baseline,
compiler/diagnostic contract, and concurrency taxonomy shared with
`reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits`.

## Why

The current durable driver represents branches as frame-stack cursors and infers joins and ownership from cursor paths. Repeated stranding defects, shared-state ambiguity, and growing special cases show that this model will not remain maintainable as nested composition, saga, DAG, waits, children, jobs, and resource ownership are completed.

OrcaCore is still in active development with no external consumers, so this is the right time to replace the execution model directly instead of preserving provisional behavior or adding a legacy compatibility path.

## What Changes

- **BREAKING** Replace cursor-path branching with compiled, single-entry/single-exit instruction plans executed as explicit fibers inside recursive execution scopes.
- **BREAKING** Define local `Parallel` and `WhenFirst` as cooperative fiber composition. Local workflow code advances one fiber quantum at a time; actual concurrent work remains available through external jobs and child workflow instances.
- **BREAKING** Redefine ephemeral `ForEach` as a dynamically populated fiber scope with isolated item input/state and ordered item outcomes. Remove `ForEachResidualPolicy.LetRemainingComplete`; `WhenAny` cancels every remaining item fiber. Durable mode rejects `ForEach` during compilation until durable local fanout is separately specified.
- **BREAKING** Replace shared mutable branch state with immutable branch input and serializable typed branch results. `WhenAll` requires an explicit deterministic merge; `WhenFirst` promotes only its selected winner result.
- **BREAKING** Require exactly one root `Init` and one root `End` in an authored workflow. Branches terminate through branch return, and branch-local `Init`, workflow `End`, and `ContinueAsNew` are invalid.
- Compile and validate definitions before registration, producing stable instruction, scope, and fiber identities plus an executable-plan fingerprint.
- Integrate the compiler with the mode-first builders from `reshape-developer-facing-interfaces`: `Build()` returns a compiled-plan-backed definition, `TryBuild()` returns aggregate `Validation<TDefinition>`, durable `ForEach` is absent from authoring and rejected by the compiler as defense in depth, and continue-as-new is authored only as a durable structural root transition.
- Keep structured closing nodes out of the public fluent Interface. The compiler emits explicit `IfJoin`, `LoopBack`, `LoopExit`, `ScopeJoin`, and `ScopeExit` instructions as stable continuation positions.
- Persist explicit fiber state, scope state, scheduler position, branch results, owned obligations, and plan fingerprint in a new durable execution envelope.
- Derive workflow `Running` and `Waiting` status from aggregate fiber runnability rather than treating each branch wait as an instance-wide suspension.
- Make waits, timers, pending resumes, children, external jobs, resource tickets, retry state, and cancellation explicitly owned by a fiber and scope.
- Remove the current cursor split/merge implementation after the new execution path passes its verification gates. No legacy executor or active-checkpoint migration is required; stale development data may be rejected or reset.
- Preserve the existing durable host, command processor, aggregate command/commit model, optimistic concurrency, continuation outbox, provider stores, and remote child-dispatch path where their contracts remain valid.

## Capabilities

### New Capabilities

- `structured-fiber-execution`: Compiled instruction plans, cooperative fibers, recursive execution scopes, deterministic scheduling, typed branch results, join policies, merge semantics, and scope-owned obligations.

### Modified Capabilities

- `workflow-authoring`: Require root-only workflow terminals, branch return, typed merge declarations, fiber-scoped `ForEach`, complete reachability checks, and rejection of unsupported scope compositions before registration.
- `workflow-contracts`: Add branch input/result and compiled-plan identity contracts while keeping ordinary `StepResult` limited to control intent.
- `state-driven-runtime`: Define local cooperative concurrency, fiber quanta, aggregate runnability, deterministic continuation, and engine parity.
- `durable-runtime`: Persist and restore the complete fiber/scope execution model and bind suspended instances to a compiled-plan fingerprint.
- `event-routing-and-waits`: Route and release waits through explicit fiber/scope ownership and distinguish fiber blocking from instance-wide waiting.
- `saga-orchestration`: Define deterministic compensation ordering and cleanup for work completed inside nested scopes.
- `runtime-resource-governance`: Align resource acquisition and release with fiber ownership and cooperative scheduling while retaining cross-instance limits.
- `quality-and-verification`: Require reference-model, crash-injection, fairness, nesting, cleanup, and relational-provider coverage for structured fibers.

## Impact

- The promoted implementation at the repository root is authoritative. All implementation work targets root `src/`, `tests/`, `samples/`, `benchmarks/`, `docs/`, and `OrcaCore.slnx`; the removed `v3-gpt` subtree is not an implementation target.
- Affected authoring surface: `src/OrcaCore.Core`, `WorkflowBuilder<TState>`, composition nodes, definition validation, branch result and merge contracts.
- Affected runtime surface: `src/OrcaCore.Engine.Ephemeral`, `src/OrcaCore.Engine.Durable`, and durable driver position, scheduling, join, wait, child, job, resource, yield, cancellation, and continuation handling.
- Affected persistence surface: `src/OrcaCore.Abstractions`, provider projects under root `src/`, durable execution envelope serialization, checkpoint mapping, event/outbox materialization, definition registration, and plan version binding.
- Affected verification surface: root `tests/` projects, provider certification, PostgreSQL and SQL Server integration suites, benchmarks, samples, and documentation traceability.
- This change is authoritative over the conflicting current requirements and decisions listed below. Their canonical text and acceptance traceability SHALL be amended in task 1.2 before source refactoring begins:
  - CP-001, CR-044, and the recorded section 13.4 `Parallel` resolution: local step bodies use cooperative fiber execution; true concurrency remains in external jobs and child instances, while commits stay serialized.
  - CP-004, AC-204, AC-205, and DR-AC-032: initial `WhenFirst` always cancels losers; ignored and let-remaining-complete local residuals are removed.
  - CP-010 through CP-013 and AC-601 through AC-605: ephemeral `ForEach` uses isolated dynamic item fibers, ordered item outcomes, cooperative admission limits, and no let-remaining residual.
  - CR-015, DR-011, DR-011a, and DR-012: compiled instruction positions plus explicit fiber/scope records replace the mandatory frame-stack/cursor representation.
  - SG-010 and AC-403: sequential compensation remains reverse committed sequence order; sibling-fiber compensation defaults to reverse canonical authored order, with only deterministic plan-bound scope overrides.
  - `add-runtime-concurrency-limits` design sections 3 and 4: unavailable capacity blocks the selected fiber and ends its quantum; ephemeral mode records this in memory and durable mode commits an owned obligation. The runtime does not await a pool while retaining the instance mutation turn.
- Source work for the compiler, builder fixtures, and structured driver is gated on joint reconciliation with `reshape-developer-facing-interfaces`: both changes must reference one capability matrix, one compiler/diagnostic contract, the same post-fiber typed result and merge signatures, portable dynamic `WaitForEvent`, durable `ForEach` absence plus compiler rejection, and root-only quiescent continue-as-new semantics, and both changes must pass strict validation.
