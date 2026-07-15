# 17. Selected-Mode Capability and Signature Matrix

Status: **normative implementation baseline**, approved 2026-07-13.

This document is the published matrix required by DU-002. It is also the single
authoring, compiler, and concurrency contract shared by the active
`adopt-structured-fiber-execution`, `reshape-developer-facing-interfaces`, and
`add-runtime-concurrency-limits` changes. An implementation, sample, or compile fixture
that conflicts with this document is non-conforming.

## 17.1 Selected-mode capability matrix

`Portable` means one authored concept and one compiled instruction contract with the same
business semantics in both modes. Different persistence or residency guarantees are stated
explicitly. `Host-selected` means the method is exposed only by a selected host builder that
actually enforces the stated transient semantics.

| Capability | Ephemeral workflow | Durable workflow | Contract |
|---|---|---|---|
| `Init`, business step, `If`, `While`, `End` | Portable | Portable | One root `Init`, one root `End`; nested blocks need no closing nodes. |
| `Parallel<TResult>` / `WhenAll` | Portable | Portable | Cooperative isolated fibers, authored-order typed results, one explicit replacement-state merge. |
| `WhenFirst<TResult>` | Portable | Portable | First committed terminal branch wins; authored order breaks same-transition ties; failed winner fails without merge; all losers are cancelled. |
| Structural `Wait` | Portable | Portable | Preferred for statically known event names; durable mode persists the obligation. |
| Dynamic `StepResult.WaitForEvent` | Portable | Portable | Retained for an event name selected only after step execution. |
| `Delay` and `StepResult.Yield` | Portable | Portable | Same control intent; durable mode persists progress and durable timer state. |
| In-instance `ForEach` | Ephemeral only | Absent and compiler-rejected | Dynamic isolated item fibers; no detached residual work. |
| `WaitLong` | Absent | Durable only | Cold durable wait. |
| `ContinueAsNew` | Absent | Durable only | Structural node, root fiber only, and only when all descendant scopes and owned obligations are quiescent. |
| Child workflows / durable fanout | Absent | Durable only | Separate child instances, not local fibers. |
| External jobs | Absent | Durable only | Structural dispatch/wait node with completion, worker-failure, and timeout outcomes. |
| Per-step execution throttle | Host-selected | Host-selected | Host-local capacity held only around one step body. |
| Named cross-instance transient pool | Host-selected | Host-selected | Host-local shared capacity; admission is re-evaluated after restart and ownership is not persisted. |
| Durable resource lease | Absent | Durable only | Persisted cross-host capacity owned by a fiber/scope with deterministic release, queueing, expiry, and recovery. |
| Saga | Ephemeral supported | Exposed only with runtime-owned durable progression | Compensation uses the same compiled structured execution plan; no interim command adapter is an application API. |
| DAG planning | Portable pure plan | Portable pure plan | Planning/visualization is side-effect free. |
| Durable DAG execution | Absent | Durable only | Runtime reconstructs and progresses committed child state; callers do not supply scheduler state sets. |
| Definition-wide retry | Absent | Absent | No public authoring method until semantics are separately specified and implemented. Step/scope retry remains a policy decorator. |
| Management and typed state | In-memory Adapter | Durable Adapter | Shared async query vocabulary; capability-specific handles expose only implemented operations. |
| Persistence and restart recovery | Absent | Durable only | Requires a configured durable store; in-memory durable hosting is development/test-only and makes no restart claim. |

## 17.2 Approved authoring signatures

The signatures below are the approval baseline for source implementation, compile fixtures,
samples, and public-signature files. Names may vary only through an explicit amendment to
this document; equivalent provisional overloads are not permitted.

```csharp
EphemeralWorkflowBuilder<TState> Workflow.Ephemeral<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

DurableWorkflowBuilder<TState> Workflow.Durable<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

TDefinition Build();
Validation<TDefinition> TryBuild();

EphemeralSagaBuilder<TState> Saga.Ephemeral<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

DurableSagaBuilder<TState> Saga.Durable<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

WorkflowDagBuilder Dag.Plan();
```

Workflow and saga `Build()` and `TryBuild()` invoke the same `DefinitionCompiler`. `Build()`
returns an immutable definition backed by the resulting `CompiledWorkflowPlan`; on failure
it throws one `WorkflowDefinitionException` containing every discoverable compiler
diagnostic. `TryBuild()` returns the same diagnostics through `Validation<TDefinition>` and
publishes no definition. Registration never recompiles into a different plan. DAG planning
uses the same completion and diagnostic contract, while returning immutable
`WorkflowDagPlan` rather than a workflow execution plan.

Structured branch authoring uses one common serializable result type per scope:

```csharp
EphemeralWorkflowBuilder<TParentState> Parallel<TResult>(
    Action<BranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         IReadOnlyList<BranchResult<TResult>>,
         TParentState> merge);

DurableWorkflowBuilder<TParentState> Parallel<TResult>(
    Action<BranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         IReadOnlyList<BranchResult<TResult>>,
         TParentState> merge);

EphemeralWorkflowBuilder<TParentState> WhenFirst<TResult>(
    Action<BranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         BranchResult<TResult>,
         TParentState> merge);

DurableWorkflowBuilder<TParentState> WhenFirst<TResult>(
    Action<BranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         BranchResult<TResult>,
         TParentState> merge);

BranchScopeBuilder<TParentState, TResult> Branch<TBranchState>(
    string branchId,
    Func<ReadOnlyParentSnapshot<TParentState>, TBranchState> inputProjector,
    Action<BranchBuilder<TBranchState, TResult>> build);

BranchBuilder<TBranchState, TResult> Return(
    Func<ReadOnlyBranchSnapshot<TBranchState>, TResult> resultProjector);
```

Each branch has exactly one reachable final `Return`. Branches cannot contain workflow
`Init`, workflow `End`, or `ContinueAsNew`. Branch input is copied through the configured
serializer or registered deep-copy contract before child execution. A merge is synchronous,
pure with respect to runtime services, runs at most once after the join commit boundary, and
returns the complete replacement parent state. `WhenAll` receives results in authored order;
`WhenFirst` receives only the selected winner result.

Ephemeral `ForEach<TItem, TItemState, TResult>` uses the same item-private builder and return
contract. Its merge receives `IReadOnlyList<ForEachItemOutcome<TResult>>` in item-index order.
The durable builder has no `ForEach` member, and the compiler still rejects a manually
constructed durable node as defense in depth.

Saga authoring follows selected mode and produces `SagaDefinition<TState>` through
`Build()`/`TryBuild()`. The `Saga.Durable` factory is not shipped until the structured durable
driver owns forward and compensation progression. DAG planning produces immutable
`WorkflowDagPlan` through the same completion pair. Durable DAG execution is an operation on
the durable runtime over a registered plan, not a caller-driven runner.

Structural durable effects are builder nodes. In particular, `ContinueAsNew` accepts a
deterministic replacement-state selector on the durable root builder. It is not a
`StepResult`. Portable `StepResult` remains limited to completed, expected failure, dynamic
`WaitForEvent`, and cooperative `Yield`.

## 17.3 Shared compiler and diagnostic contract

The `DefinitionCompiler` applies a positive instruction allowlist for the selected mode and
produces one immutable `CompiledWorkflowPlan` with a format version and canonical
fingerprint. It validates the complete graph before registration, including:

- one root entry and exit and complete successful-path termination;
- legal structured nesting, reachability, branch identities, branch returns, and merge ownership;
- result, merge, serializer, and copy-contract compatibility;
- positive scope-depth, active-fiber, internal-instruction, result-size, and envelope-size limits;
- no loop cycle that can consume a quantum forever without a step, suspension, yield, branch return, failure, or exit;
- selected-mode capability support, including manual unsupported-node construction.

Diagnostics are deterministic and contain a stable machine-readable code, severity, and a
structured location identifying the authored node and, where applicable, the compiled
instruction, scope, and branch. Code and location are compatibility contracts; message text
may improve. Diagnostics from one compile are ordered by authored graph location and then by
code. Invalid local arguments are rejected at the fluent call; graph-wide errors accumulate
through `TryBuild()`.

The initial compiler code families are reserved as follows:

| Family | Meaning |
|---|---|
| `SFE-AUTH-*` | Local or graph-wide authoring structure. |
| `SFE-CAP-*` | Capability absent for the selected mode or host. |
| `SFE-TYPE-*` | Branch result, merge, serializer, or copy-contract incompatibility. |
| `SFE-LIMIT-*` | Static configured limit violation. |
| `SFE-PLAN-*` | Lowering, identity, reachability, fingerprint, or internal plan invariant. |
| `SFE-RUN-*` | Runtime structured-execution rejection after a valid plan, including `SFE-RUN-001` for non-quiescent continue-as-new. |

## 17.4 Three concurrency lifetimes

The following terms are not aliases and MUST remain distinct in APIs, docs, persistence, and
telemetry:

1. A **per-step execution throttle** is host-local and held only while one business-step body executes.
2. A **named cross-instance transient pool** is host-local shared capacity across instances. A blocked fiber records an owned in-memory or committed admission obligation, releases the instance turn, and re-evaluates admission after host restart without claiming that a prior slot survived.
3. A **durable resource lease** is persisted cross-host capacity. It is owned by the requesting fiber/scope and releases deterministically on normal scope exit, branch cancellation, scope failure, or workflow termination. Expiry is a recovery backstop.

Local structured branches are cooperative: at most one business-step body for one workflow
instance executes at a time. True concurrency is explicit through different workflow
instances, child workflows, external jobs, or infrastructure dispatch.

## 17.5 Package and host boundary

The application tier does not expose provider or protocol types. `OrcaCore.Runtime.Protocol`
owns durable commands, facts, checkpoints, and envelopes. `OrcaCore.Provider.Abstractions`
owns provider ports and commit/certification contracts and may reference Runtime.Protocol;
the reverse edge is forbidden. Engine/runtime implementations may reference both. Separate
packages are distributed for the tiers plus a small `OrcaCore` application meta-package.

Definitions are registered explicitly and per host. Registration returns a typed definition
handle so start calls require no phantom state generic. An accepted operation always commits
an at-least-once continuation handoff. It returns `AppliedAndProgressed` only when a locally
registered definition was driven inline; a definition-less callback host returns
`AppliedPendingContinuation` and leaves progression to a definition-owning host pump.
