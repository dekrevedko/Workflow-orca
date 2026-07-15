# 17. Selected-Mode Capability and Signature Matrix

Status: **normative post-fiber implementation baseline**, revised 2026-07-14.

This document is the published matrix required by DU-002. It is also the single
authoring, compiler, and concurrency contract shared by the active
`reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits` changes and by
the archived `adopt-structured-fiber-execution` baseline. An implementation, sample, or
compile fixture that conflicts with this document is non-conforming.

## 17.1 Selected-mode capability matrix

`Portable` means one authored concept and one compiled instruction contract with the same
business semantics in both modes. Different persistence or residency guarantees are stated
explicitly. A static mode-first builder exposes only capabilities guaranteed by every
supported host for that mode. Host configuration may set or tighten limits, but it cannot add
methods to the compile-time surface selected by `Workflow.Ephemeral` or `Workflow.Durable`.

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
| Per-step execution throttle | Host configuration | Host configuration | Host-local capacity held only around one step body. The baseline adds no builder method; host policy may target all steps or stable authored categories. |
| Named cross-instance transient pool | Ephemeral only | Absent until durable enforcement lands | Host-local shared capacity; admission is re-evaluated after restart and ownership is not persisted. Ephemeral authoring uses the explicit transient-pool name rather than lease vocabulary. |
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
    DefinitionVersion definitionVersion,
    WorkflowAuthoringOptions? options = null);

DurableWorkflowBuilder<TState> Workflow.Durable<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion,
    WorkflowAuthoringOptions? options = null);

EphemeralWorkflowDefinition<TState> EphemeralWorkflowBuilder<TState>.Build();
Validation<EphemeralWorkflowDefinition<TState>> EphemeralWorkflowBuilder<TState>.TryBuild();

DurableWorkflowDefinition<TState> DurableWorkflowBuilder<TState>.Build();
Validation<DurableWorkflowDefinition<TState>> DurableWorkflowBuilder<TState>.TryBuild();

EphemeralSagaBuilder<TState> Saga.Ephemeral<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

EphemeralSagaDefinition<TState> EphemeralSagaBuilder<TState>.Build();
Validation<EphemeralSagaDefinition<TState>> EphemeralSagaBuilder<TState>.TryBuild();

DurableSagaBuilder<TState> Saga.Durable<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

DurableSagaDefinition<TState> DurableSagaBuilder<TState>.Build();
Validation<DurableSagaDefinition<TState>> DurableSagaBuilder<TState>.TryBuild();

WorkflowDagBuilder Dag.Plan();
```

Workflow and saga `Build()` and `TryBuild()` invoke the same internal definition compiler.
`Build()` returns an immutable mode-specific application definition; on failure it throws one
`WorkflowDefinitionException` containing every discoverable compiler diagnostic.
`TryBuild()` returns the same diagnostics through `Validation<TDefinition>` and publishes no
definition. Registration never recompiles into a different plan. The compiled plan is retained
behind an internal engine accessor and is not a public property of either application
definition. DAG planning uses the same completion and diagnostic contract, while returning
immutable `WorkflowDagPlan` rather than a workflow execution plan.

`WorkflowAuthoringOptions`, its positive definition limits, payload serializer/copy registry,
and deterministic fingerprint-contributor contract are application-authoring concepts. The
builder does not expose compiler-named `WithCompilerOptions` or
`WithTypeSerializerRegistry` methods. `DefinitionCompilerOptions`, compiled instruction/plan
types, and compiler-specific serializer/fingerprint interfaces are implementation details.

The initial application option set is explicit:

| Application option | Contract |
|---|---|
| `MaxStructuredDepth` | Positive maximum authored/nested scope depth. |
| `MaxActiveExecutionPaths` | Positive maximum simultaneously materialized root, branch, and item execution paths for one instance. |
| `MaxBranchResultPayloadBytes` | Positive serialized-size limit for one branch or item result before merge. |
| `PayloadSerializers` | Registry of supported application payload serializers. |
| `StateCopiers` | Registry of deterministic deep-copy contracts used for branch input and detached state. |
| `FingerprintContributors` | Deterministic contributors for opaque authored configuration. |

`MaxStructuralOperationsPerTurn` (the current internal-instruction quantum) and
`MaxCheckpointPayloadBytes` (the current serialized-envelope limit) are engine/hosting
configuration, not workflow-authoring options. Their application-facing names do not expose
instruction-quantum or envelope implementation vocabulary.

Structured branch authoring uses one common serializable result type per scope:

```csharp
EphemeralWorkflowBuilder<TParentState> Parallel<TResult>(
    Action<EphemeralBranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         IReadOnlyList<BranchResult<TResult>>,
         TParentState> merge);

DurableWorkflowBuilder<TParentState> Parallel<TResult>(
    Action<DurableBranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         IReadOnlyList<BranchResult<TResult>>,
         TParentState> merge);

EphemeralWorkflowBuilder<TParentState> WhenFirst<TResult>(
    Action<EphemeralBranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         BranchResult<TResult>,
         TParentState> merge);

DurableWorkflowBuilder<TParentState> WhenFirst<TResult>(
    Action<DurableBranchScopeBuilder<TParentState, TResult>> branches,
    Func<ReadOnlyParentSnapshot<TParentState>,
         BranchResult<TResult>,
         TParentState> merge);

EphemeralBranchScopeBuilder<TParentState, TResult> Branch<TBranchState>(
    string branchId,
    Func<ReadOnlyParentSnapshot<TParentState>, TBranchState> inputProjector,
    Action<EphemeralBranchBuilder<TBranchState, TResult>> build);

DurableBranchScopeBuilder<TParentState, TResult> Branch<TBranchState>(
    string branchId,
    Func<ReadOnlyParentSnapshot<TParentState>, TBranchState> inputProjector,
    Action<DurableBranchBuilder<TBranchState, TResult>> build);

EphemeralBranchBuilder<TBranchState, TResult> Return(
    Func<ReadOnlyBranchSnapshot<TBranchState>, TResult> resultProjector);

DurableBranchBuilder<TBranchState, TResult> Return(
    Func<ReadOnlyBranchSnapshot<TBranchState>, TResult> resultProjector);
```

The two public branch-builder families share one internal implementation but retain the root
builder's selected mode in their static type. An ephemeral-only transient-pool decorator is
therefore absent from durable root and nested branch authoring. Each branch has exactly one
reachable final `Return`. Branches cannot contain workflow
`Init`, workflow `End`, or `ContinueAsNew`. Branch input is copied through the configured
serializer or registered deep-copy contract before child execution. A merge is synchronous,
pure with respect to runtime services, runs at most once after the join commit boundary, and
returns the complete replacement parent state. `WhenAll` receives results in authored order;
`WhenFirst` receives only the selected winner result.

The initial nested capability set is exact. Both ephemeral and durable branch builders expose
business `Then`, step retry/timeout/cancellation decorators, resident structural `Wait`,
`Delay`, nested `Parallel`, nested `WhenFirst`, and terminal `Return`. Ephemeral branches also
expose the named transient-pool decorator. Neither branch family exposes root `Init`/`End`,
`If`, `While`, in-instance `ForEach`, `WaitLong`, child workflows, external jobs, durable
resource leases, or `ContinueAsNew`. Adding any nested capability requires a matrix amendment
and matching runtime plus compile-fixture evidence; root availability alone is insufficient.

Ephemeral `ForEach<TItem, TItemState, TResult>` uses the same item-private builder and return
contract. Its merge receives `IReadOnlyList<ForEachItemOutcome<TResult>>` in item-index order.
The durable builder has no `ForEach` member, and the compiler still rejects a manually
constructed durable node as defense in depth.

Saga authoring follows selected mode and produces `EphemeralSagaDefinition<TState>` or
`DurableSagaDefinition<TState>` through `Build()`/`TryBuild()`. The two definitions share
internal representation but cannot cross normal engine registration contracts. The
`Saga.Durable` factory is not shipped until the structured durable driver owns forward and
compensation progression. DAG planning produces immutable
`WorkflowDagPlan` through the same completion pair. Durable DAG execution is an operation on
the durable runtime over a registered plan, not a caller-driven runner.

Structural durable effects are builder nodes. In particular, `ContinueAsNew` accepts a
deterministic replacement-state selector on the durable root builder. It is not a
`StepResult`. Portable `StepResult` remains limited to completed, expected failure, dynamic
`WaitForEvent`, and cooperative `Yield`.

## 17.3 Shared compiler and diagnostic contract

The internal definition compiler applies a positive instruction allowlist for the selected
mode and produces one immutable compiled plan with a format version and canonical
fingerprint. Compiled plan, instruction, scope, branch, policy, and identity types are not
application public contracts. The compiler validates the complete graph before registration,
including:

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

## 17.6 Application projections versus runtime ownership

Application definitions expose mode, identity, version, fingerprint, and immutable authored
metadata, but not the executable compiled plan. `FiberId`, `ScopeId`, runtime registration
sequence, format-2 envelopes, raw park reasons, and obligation ownership are runtime-protocol
or implementation concepts.

Application active-wait projections expose stable authored facts such as wait kind, authored
node/path, event name, correlation, residency, and relevant logical timing. They do not expose
`FiberId`, `ScopeId`, or `WaitSequence`; an advanced runtime observation may expose those
fields for certified custom-host diagnostics.

The authored path uses the same immutable `AuthoredLocation` contract as compiler diagnostics
and remains stable when an unchanged authored graph is recompiled. It identifies definition
structure, not a runtime execution address. `WaitId` remains application-visible as an opaque
handle for targeting and diagnostic correlation; it carries no fiber, scope, ordering, or
checkpoint semantics.

`GetStateAsync<TState>` returns a detached value representing the last committed root workflow
business state for the registered definition. It never returns branch-private or item-private
fiber state. Incompatible requested type, missing retained state, archive, and purge remain
typed application outcomes.
