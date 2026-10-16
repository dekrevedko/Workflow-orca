## Joint implementation baseline

[`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md)
is the only approved selected-mode capability matrix, public builder/result/merge signature
baseline, and compiler/diagnostic contract for this change and
`reshape-developer-facing-interfaces`. It also fixes the three concurrency lifetimes shared
with `add-runtime-concurrency-limits`. Compile fixtures and source implementation SHALL use
those signatures directly and SHALL NOT introduce a provisional common builder or a second
compiler.

## Context

The current durable driver stores one workflow business-state payload and a collection of frame-stack cursors. `SplitCursor` copies execution frames into branch cursors, `MergeCompletedCursors` infers container ownership from frame prefixes, and joining reconstructs a continuation cursor from one completed branch. The cursor therefore carries too many responsibilities: instruction position, branch identity, scope ancestry, scheduling priority, wait routing, join candidacy, and replay state.

This design has already produced order-dependent stranding defects in nested `Parallel` and `WhenFirst` shapes. It also cannot express the isolated branch-state requirement in [composition requirements](../../../docs/specs/08-requirements-composition.md), because every durable cursor and every ephemeral branch currently shares the same mutable `TState`.

The repository also contains conflicting concurrency direction. [The canonical phasing document](../../../docs/specs/13-phasing-and-open-questions.md) describes concurrent branch execution as the target, while [add-runtime-concurrency-limits](../add-runtime-concurrency-limits/design.md) permits sequential branch interpretation as an interim implementation. This decision resolves the conflict by defining observable local semantics as cooperative fiber execution. External jobs and child instances remain the mechanisms for actual concurrent work. Isolated branch inputs and results keep future true parallel scheduling possible without another authoring-contract change.

OrcaCore has no external consumers and no production durable instances. Compatibility with cursor envelopes, shared-state `Parallel`, or provisional residual policies is not a constraint. Durable format versioning and plan binding remain requirements because they are intrinsic to a durable engine, not because old development data must be migrated.

The rebuilt implementation has been promoted from `v3-gpt` to the repository root. The root `OrcaCore.slnx`, `src/`, `tests/`, `samples/`, `benchmarks/`, and active `docs/` now define the only implementation structure used by this design and its tasks. The removed `v3-gpt` paths are historical deletion entries and must not receive new files or references.

The following existing Seams remain valuable and are not redesigned wholesale:

- durable host and continuation pump;
- command processor and one-command/one-commit aggregate model;
- optimistic concurrency and atomic provider commit;
- outbox materialization and dispatch;
- deterministic remote child identifiers and child dispatch;
- provider certification infrastructure.

## Goals / Non-Goals

**Goals:**

- Reduce interpreter complexity to one selected fiber, one instruction position, and a small set of scope operations.
- Make branch and residual ownership explicit instead of inferred from cursor paths.
- Give every structured branch one entry, one branch return, and one typed result.
- Make successful join outcomes independent from branch completion order.
- Provide bounded cooperative fairness that survives durable restart.
- Compile and reject invalid definitions before any instance starts.
- Share compiled-plan, fiber, scope, and join semantics between ephemeral and durable engines.
- Preserve a future path to true parallel branch computation without changing workflow semantics.
- Continue implementation through test-first slices with crash and provider verification at each persistence milestone.

**Non-Goals:**

- Preserving or migrating cursor-based execution envelopes.
- Preserving shared mutable state between local branches.
- Running local business-step bodies concurrently in the first structured-fiber implementation.
- Supporting detached local fibers, fire-and-forget branches, `WhenFirst.Ignore`, or `WhenFirst.LetRemainingComplete`.
- Adding typed child-workflow return values; local branch results do not change the existing child-workflow contract.
- Building a second in-instance DAG scheduler; DAG nodes remain child workflow instances.
- Guaranteeing exactly-once external side effects beyond the existing at-least-once effect and exactly-once commit model.

## Decisions

### 1. Compile authored trees into explicit instruction plans

The mode-first builders owned by `reshape-developer-facing-interfaces` use `Build()` as the throwing common path and `TryBuild()` returning `Validation<TDefinition>` as the non-throwing aggregate-diagnostics path. Both call one `DefinitionCompiler`, which lowers the immutable authored tree into a `CompiledWorkflowPlan`; the returned definition is backed by that compiled plan and contains:

- stable `InstructionId` values;
- structured jump and loop targets;
- `ScopePlan` records for `WhenAll` and `WhenFirst`;
- stable `ScopePlanId` and authored `BranchId` values;
- branch input, local-state, result, and merge type metadata;
- fully resolved retry, timeout, cancellation, resource, and serializer policies;
- a positive allowlist of instructions supported by the selected execution mode;
- compiler format version and deterministic plan fingerprint.

The public durable application registry explicitly registers the already built definition on a host and publishes the definition, plan, and executor atomically. Start never registers as a side effect. A failed validation or publication publishes nothing. Re-registering the same definition identifier and version with a different fingerprint fails.

Ephemeral and durable builders consume the same internal graph but expose different positive allowlists. `ForEach` exists only on the ephemeral public builder; a manually constructed durable `ForEach` graph still fails compiler validation as defense in depth. The post-fiber `Parallel`, `WhenFirst`, branch-result, and merge signatures in this design are the only signatures used by the companion change's compile fixtures and public baselines. Portable dynamic `StepResult.WaitForEvent` remains available for event names selected during business execution; the structural `Wait` node is preferred when the name is statically known. `ContinueAsNew` is authored only as a durable structural node and never as a `StepResult` variant.

The fingerprint covers plan structure, stable node identities, declared types, policy values, serializer schema identities, and merge contract identity. It cannot reliably hash arbitrary step implementation code, so authors must still increment definition version when deployed step behavior changes.

Alternative considered: continue interpreting the authored node tree directly. Rejected because runtime tree traversal keeps structural validation, capability discovery, policy resolution, and execution-position identity distributed across the driver.

### 2. Use one root entry and one root exit

An authored workflow has exactly one root `Init` and one root `End`. Every successful root path converges on that `End`. The root `End` may declare a static named outcome or a deterministic outcome selector over final typed workflow state. The runtime evaluates that selector at `End` and records the resulting CR-008 outcome in completion metadata and lifecycle publication. Multiple business outcomes therefore do not require multiple structural `End` nodes.

Failure, cancellation, termination, and poisoning are runtime terminal transitions and do not need to execute `End`. Durable version or envelope incompatibility parks the instance without executing `End`.

A branch never contains root `Init`, root `End`, or `ContinueAsNew`. It has one branch entry and one reachable `BranchReturn<TResult>`. The compiler can therefore treat a branch as a linear instruction position with structured jumps, loops, waits, and nested scope instructions, rather than as a miniature workflow lifecycle.

Root `ContinueAsNew` is valid only when the root fiber is the sole nonterminal fiber and no active descendant scope or outstanding owned obligation exists. Any violation rejects rollover and fails the workflow with stable runtime diagnostic `SFE-RUN-001` (`ContinueAsNewRequiresQuiescentRoot`); generation, replacement state, and ownership remain unchanged. It never parks or silently re-executes the step. Because detached local work is unsupported and the root is blocked while a child scope is active, valid structured execution naturally reaches `ContinueAsNew` only after prior scopes finish. The new generation increments before deriving its new root `FiberId`.

Alternative considered: allow any branch to execute workflow `End`. Rejected because one child could terminate the whole instance while siblings still own work.

### 3. Represent execution with fibers and recursive scopes

A `Fiber` is one independently advancing local instruction position. A minimal fiber record contains:

```text
FiberId
OwningScopeId
InstructionId
Phase: Runnable | Blocked | Completed | Failed | Cancelled
LoopIteration
NextScopeEntrySequence
LocalStatePayload
ResultPayload
BlockedReason and ObligationId
Retry, yield, and diagnostic state
```

An `ExecutionScope` owns a parent fiber and its child fibers:

```text
ScopeId
ScopePlanId
ScopeEntrySequence
ParentScopeId
ParentFiberId
Kind: WhenAll | WhenFirst | ForEach
Phase: Created | Running | Joinable | Merging | Completed | Failed | Cancelled
Ordered ChildFiberIds
Join and cancellation policy
WinnerFiberId
Committed branch results
Owned obligation references
```

When a parent executes `StartScope`, the runtime preserves the parent fiber, marks it blocked on that scope, and creates child fibers in authored branch order. Joining marks the same parent runnable at the instruction after the scope. The runtime never reconstructs parent continuation state from a completed child.

Nested composition creates another scope owned by the currently selected child fiber. The same scope lifecycle applies at every Depth.

Runtime identity minting is a deterministic function of committed state:

```text
RootFiberId = Hash(InstanceId, ContinueAsNewGeneration, "root")
ScopeId = Hash(ParentFiberId, ScopePlanId, Parent.NextScopeEntrySequence)
ChildFiberId = Hash(ScopeId, AuthoredBranchId or ForEachItemIndex)
```

The scope-start transition persists the chosen `ScopeId`, child `FiberId` values, and incremented parent `NextScopeEntrySequence` atomically. Replay and duplicate claims load the committed values. Competing pre-commit attempts derive the same values from the same expected parent sequence, and optimistic concurrency permits only one increment. A loop re-entering the same `ScopePlanId` uses the next committed sequence. `ContinueAsNew` increments generation and therefore creates a distinct root identity and identity tree even when the same plan instructions execute again.

Alternative considered: create a separate workflow instance for every local branch. Rejected because it multiplies streams, leases, inboxes, version bindings, and management records for short-lived local control flow. Explicit `RunChild` and `RunChildren` already provide the remote-instance model when isolation and distribution justify that cost.

### 4. Isolate branch state and return typed results

The suspended parent owns the parent `TState`. Child fibers do not receive that mutable object. Each branch declares:

- a pure input projector from a parent snapshot to serializable branch input;
- a private branch-state type used by that branch's steps;
- one result type shared by the containing scope;
- a pure branch-return projector from branch state to `TResult`.

Different branches may use different private state types. Runtime adapters erase those private types after compilation, while every branch in one scope returns the common `TResult`. Authors represent heterogeneous logical outcomes with an authored record or discriminated union.

Branch input is materialized through serialization or another registered deep-copy contract when the scope starts. A shallow object copy is insufficient because aliased references would recreate shared mutable state.

Alternative considered: clone the entire parent `TState` for every branch and merge complete state copies. Rejected as the default because it multiplies checkpoint size, exposes accidental unrelated mutations, and still requires a generic conflict-resolution policy that the runtime cannot infer correctly.

Alternative considered: retain shared `TState` because cooperative scheduling prevents simultaneous mutation. Rejected because serialization removes memory races but does not remove order dependence. The result of read-modify-write branch logic would remain scheduler-visible and would prevent future true parallel scheduling.

### 5. Make merge a pure replacement-state function

`WhenAll` declares one merge over successful results in stable authored branch order. `WhenFirst` declares one winner merge over the selected successful result. Conceptually:

```text
TState Merge(
    ReadOnlyParentSnapshot<TState> parent,
    IReadOnlyList<BranchResult<TResult>> orderedResults)
```

Merge returns replacement parent state. It is synchronous and receives no runtime context, clock, cancellation token, provider, or dispatch Interface. The runtime serializes the replacement state before committing it. This makes merge replayable and prevents a partially mutated parent object from escaping when merge fails.

Merge is logically exactly once: a crash before commit re-executes merge from committed inputs, while a crash after commit observes the merged envelope. Completed branch bodies never rerun merely because merge is retried.

The runtime cannot prove arbitrary user code is mathematically pure. The restricted Interface, deterministic inputs, serialization-before-commit, analyzer rules where practical, and replay tests provide the enforceable guardrails.

Alternative considered: automatically merge changed properties from branch state copies. Rejected because collection edits, reference aliases, deletes, domain invariants, and conflicting writes have no universal correct resolution.

### 6. Use persisted round-robin cooperative scheduling

Each instance has a stable runnable queue and persisted next-fiber position. Fiber identities derive from scope identity and authored branch index, not allocation timing.

A quantum executes at most one user step invocation plus at most `MaxInternalInstructionsPerQuantum` internal control instructions. The initial default is 1024 and configuration must be positive. A wait, resource block, child/job dispatch, branch return, scope transition, `Yield`, failure, or terminal instruction ends the quantum. `Yield` leaves the fiber at its current instruction and moves it behind already-runnable siblings.

Reaching the internal-instruction limit ends the quantum successfully, persists the current instruction/loop progress and scheduler position in durable mode, records a diagnostic counter, and requeues the fiber behind already-runnable siblings. It does not fail or park the workflow. The compiler rejects every reachable loop cycle whose body contains no user-step invocation, suspension operation, `Yield`, branch return, scope transition, or terminal instruction, because such a cycle can never make business progress between forced rotations.

New child fibers are appended in authored order. Fibers resumed together are appended in stable `FiberId` order. The segment command and elapsed-time budgets count work across quanta; when a segment ends with runnable fibers, its commit includes a successor continuation signal.

Every durable commit that changes runnable membership also persists the next-fiber position. Restart therefore continues the rotation rather than repeatedly selecting the lexicographically first cursor.

Alternative considered: always run the first runnable branch to suspension. Rejected because a repeatedly yielding or long straight-line branch can starve siblings and because fairness state cannot be recovered after restart.

### 7. Keep initial join and residual policies strict

Initial `WhenAll` behavior is fail-fast:

- merge executes only if every branch succeeds;
- the first committed branch failure fails the scope;
- remaining descendants are cancelled;
- merge does not run on partial results.

Initial `WhenFirst` behavior is first terminal completion:

- the first terminal branch committed wins;
- same-commit ties use authored branch order;
- a successful winner executes winner merge;
- a failed winner fails the scope without merge;
- every losing descendant is cancelled.

`Ignore` and `LetRemainingComplete` are removed from the initial Interface. Both require detached-scope lifetime, management visibility, ownership after parent continuation, and terminal workflow rules that defeat the intended single-exit scope simplification. They can return only through a separate reviewed detached-work capability.

If any branch can create work that a possible join failure, loser selection, parent cancellation, termination, or rollover path cannot durably cancel or detach, compilation rejects the complete branch shape. This applies to every join policy, including a child-owning `WhenAll` that may fail fast, not only to `WhenFirst` losers. It does not defer the failure until cancellation is attempted.

Alternative considered: preserve all current residual enum values. Rejected because the durable driver currently does not implement their distinct semantics and there are no external consumers requiring compatibility.

### 8. Make all residual work explicitly owned

Every wait, timer, pending resume, child group, external job, resource acquisition, retry record, and cancellation request carries `InstanceId`, `FiberId`, and `ScopeId`. Ownership is persisted in the aggregate facts and execution envelope; branch labels remain descriptive metadata only.

Scope cancellation traverses descendants from leaves toward the parent and emits the appropriate cancel, release, or stop facts. Scope completion commits only after all non-detached owned work is terminal or durably released. Parent workflow cancellation and termination use the same traversal from the root scope.

Resource acquisition follows the same scheduling rule. When capacity is unavailable, the ephemeral Adapter records an in-memory owned resource obligation and the durable Adapter commits one; both end the selected fiber quantum and release the instance mutation turn. Neither mode awaits capacity while retaining the instance turn. A grant makes the exact owning fiber runnable under normal queue ordering.

This creates one Locality for cleanup logic instead of separate wait, timer, child, job, and resource cleanup embedded in join code.

### 9. Derive instance status from fibers

Fiber blocking and workflow residency are distinct:

- `Running`: at least one fiber is runnable, or an advancement commit is in progress;
- `Waiting`: every nonterminal fiber is blocked on a committed obligation or scope join;
- `Completed`: root `End` committed and no owned work remains;
- `Failed`, `Cancelled`, `Terminated`: root lifecycle reached the corresponding terminal state after required cleanup policy;
- durable-only `Parked`: definition, plan fingerprint, envelope version, poison, or unsupported-capability diagnostics prevent safe durable execution.

A branch registering a wait therefore blocks only that fiber. The instance becomes `Waiting` only when no sibling can run. Ephemeral mode reports unsupported definitions or invalid execution state through typed start/execution failure and does not enter `Parked`; durable fingerprint and envelope diagnostics are outside ephemeral parity.

### 10. Replace the execution envelope instead of migrating it

The new durable envelope uses format version 2 and contains:

```text
EnvelopeVersion = 2
CompilerFormatVersion
DefinitionId and DefinitionVersion
InstanceId and ContinueAsNewGeneration
PlanFingerprint
RootFiberId
NextFiberId
Parent business-state payload
Fiber records, including LoopIteration and NextScopeEntrySequence
Scope records, including ScopePlanId and ScopeEntrySequence
Pending branch-result payloads
Owned-obligation references
Continuation retry and segment diagnostics
```

The old cursor envelope is not converted. Development stores and fixtures are reset. Loading a stale envelope produces an explicit format diagnostic; the runtime does not select a legacy executor.

Keeping an explicit format version is still required so future released versions can make deliberate migration or parking decisions.

Alternative considered: support both formats until old instances drain. Rejected because there are no production instances and dual execution would double the behavioral surface during the refactor.

### 11. Share semantics and separate persistence adapters

The target Module structure is:

```text
OrcaCore.Core
  DefinitionCompiler
  CompiledWorkflowPlan
  LinearFiberInterpreter
  ScopeReducer
  FiberScheduler

OrcaCore.Engine.Ephemeral
  InMemoryExecutionStateAdapter
  Existing ephemeral host/lifecycle integration

OrcaCore.Engine.Durable
  DurableExecutionStateAdapter
  DurableWorkflowDriver host
  DurableCommandProcessor and aggregate commit integration
```

The compiler, scheduler rules, scope reducer, join selection, merge ordering, and status derivation are shared. The ephemeral Adapter keeps state in memory. The durable Adapter maps the same logical transitions to commands, facts, envelopes, outbox records, and optimistic commits.

The current `DurableDriverSegmentRun` cursor split/merge Implementation is replaced rather than wrapped. The host, lane, continuation, aggregate, and provider Seams remain where their contracts still apply.

Alternative considered: independently implement the new model in both engines. Rejected because the present engines already drift on `WhenFirst` residual behavior, demonstrating that duplicated orchestration semantics are difficult to maintain.

### 12. Preserve a path to true parallel computation

Initial local scheduling remains cooperative. Because child fibers operate on isolated input and private state and communicate only through persisted results, a later scheduler can execute branch computation concurrently without changing authoring or merge semantics.

Even in a future concurrent scheduler:

- one logical mutator still serializes commits to the parent instance;
- each accepted branch result is idempotent;
- merge still uses stable authored order;
- resource governance still limits external work;
- external side effects retain their documented delivery guarantees.

True distribution continues to use external jobs or child workflow instances. This refactor does not turn every local fiber into a separate stream or lease.

### 13. Build saga and DAG behavior on the new model

Saga compensation records carry fiber and scope ownership. A committed compensable action is immediately eligible inside its current branch scope. If that branch or its containing scope fails or is cancelled before merge, compensation covers every committed descendant action. A successful branch result makes its compensation records visible to the containing scope, and successful scope merge transfers those records to the parent scope without changing their stable identities.

Sequential actions compensate in reverse committed sequence order. Sibling-fiber actions compensate in reverse canonical authored branch/instruction order so wall-clock completion does not change the result. A scope may declare an ordering override only when the override is deterministic, compiled into the plan fingerprint, and based on stable authored identities rather than runtime completion timing.

DAG nodes remain child workflow instances coordinated by the parent. The local parent fiber waits on the child group as one owned obligation. This avoids adding an unrelated in-instance DAG scheduler.

Saga and DAG advancement should continue only after the compiler, fiber scheduler, scopes, ownership, and crash-safe merge are established.

### 14. Compile structural closing positions without public closing nodes

The public fluent Interface does not add `EndIf`, `EndWhile`, `EndParallel`, or similar balancing operations. Nested builder delegates already establish authored structure, avoid mismatched opening and closing tokens, and allow validation to reason over a real tree.

The compiled plan still makes every continuation position explicit:

```text
IfTest(condition, thenTarget, elseTarget)
Then instructions -> Jump(IfJoin)
Else instructions -> Jump(IfJoin)
IfJoin -> next instruction

LoopTest(condition, bodyTarget, loopExit)
Body instructions -> LoopBack(LoopTest)
LoopExit -> next instruction

StartScope(scopePlan)
ScopeJoin -> evaluate join policy
ScopeExit -> finish cleanup/merge and resume parent
```

`IfJoin`, `LoopBack`, and `LoopExit` are structural instructions. They provide stable `InstructionId` values, continuation targets, reachability checks, replay positions, diagnostics, and visualization anchors, but do not terminate a workflow or fiber.

`ScopeJoin` and `ScopeExit` are runtime-significant structural positions evaluated by `ScopeReducer` at commit boundaries; no runnable fiber is selected to execute them as a quantum. The final branch-result transition evaluates `ScopeJoin`. Cleanup and merge transitions evaluate `ScopeExit`, whose `InstructionId` anchors diagnostics and the parent continuation target. `ScopeExit` makes the preserved parent runnable only after the required commit succeeds.

`BranchReturn<TResult>` and root `End` remain authored semantic instructions because they produce a branch result or complete the successful workflow lifecycle.

Alternative considered: expose paired public opening and closing nodes. Rejected because they add authoring noise and malformed nesting states without simplifying the interpreter; the compiler needs explicit continuation targets, not user-authored balancing tokens.

### 15. Compile ephemeral ForEach as a dynamic fiber scope

`ForEach` remains an ephemeral capability in the initial refactor, but it no longer uses a separate shared-state scheduler. The compiler lowers it to `ScopeKind.ForEach`, and the shared `FiberScheduler` and `ScopeReducer` execute it through the in-memory Adapter. Durable compilation rejects `ForEach` with a capability diagnostic until durable dynamic local fanout is separately approved.

At scope start, the item selector and partitioner materialize a stable ordered list of work descriptors. Each admitted descriptor creates an item fiber whose identity derives from the runtime `ScopeId` and stable item index. Every item receives isolated serializable item input and private item state. It cannot mutate parent `TState`.

Every item produces a runtime-owned `ForEachItemOutcome<TResult>` containing item index, terminal status, optional typed result, and failure metadata. Results are ordered by item index, never completion timing. `maxConcurrency` becomes a limit on admitted nonterminal item fibers; cooperative execution still invokes one local step body per instance at a time.

`WhenAll` waits for the configured failure policy. `FailFast` cancels remaining admitted work and never merges partial results. `WaitAllThenFail` observes all item outcomes and fails without merge when any item failed. `ContinueWithPartialFailures` may invoke an explicit deterministic merge over the complete ordered outcome list. A resultless authoring overload remains possible by omitting merge; the parent state is unchanged and management exposes item outcomes.

`WhenAny` mirrors `WhenFirst`: it selects the first committed terminal item, uses item index for same-commit ties, and accepts only `ForEachFailurePolicy.FailFast`. A failed winner fails the scope without merge. A successful winner passes exactly one `ForEachItemOutcome<TResult>` to the optional winner merge as a single-element item-index-ordered list. `WhenAny` combined with `WaitAllThenFail` or `ContinueWithPartialFailures` is rejected during definition compilation. Every remaining admitted item fiber is cancelled before parent continuation. `ForEachResidualPolicy.LetRemainingComplete` is removed because it creates detached local work. Pending, not-yet-admitted item descriptors are cancelled without creating fibers.

Alternative considered: retire `ForEach`. Rejected because dynamic data fanout, partitioning, failure policies, and admission limits are useful, already specified behaviors that fit the scope model once shared parent mutation and detached residuals are removed.

## Risks / Trade-offs

- **[Large internal rewrite]** -> Preserve the current green baseline in version control, implement in vertical test-first slices, and keep existing host/provider Seams until replacement behavior is proven.
- **[Checkpoint size grows with active fibers]** -> Prefer authored projections and compact typed results over full parent-state copies; enforce configurable Depth, active-fiber, result-size, and envelope-size limits.
- **[User merge code is nondeterministic]** -> Restrict the merge Interface, serialize before commit, document purity, add analyzers where practical, and verify replay with schedule and crash permutations.
- **[One-step quanta increase commit or scheduling overhead]** -> Permit bounded internal instruction runs, preserve segment budgets, measure envelope churn, and optimize only without weakening fairness.
- **[Fail-fast semantics discard potentially useful partial results]** -> Keep the initial policy strict; add alternate failure policies only as separately specified scope policies.
- **[No cursor migration invalidates development data]** -> Detect format 1 explicitly and provide a documented store-reset procedure; do not silently misinterpret it.
- **[Plan fingerprint cannot detect arbitrary code changes]** -> Require definition-version changes for deployed step-code semantics and use fingerprinting to detect structural and declared-contract drift.
- **[Concurrent OpenSpec governance change drifts]** -> Review and amend `add-runtime-concurrency-limits` so it treats cooperative local fibers as the chosen semantics and external/cross-instance work as the concurrency-governed paths.
- **[Child-owning losers cannot yet be cancelled]** -> Reject those definitions until a durable child-cancel command and acknowledgement protocol exist.

## Migration Plan

1. Approve this proposal, specification deltas, design decisions, and review checklist. Amend every conflicting canonical requirement and concurrent OpenSpec decision listed in proposal Impact before implementation.
2. Preserve the current green driver state in version control as a diagnostic baseline; do not extend cursor branching with saga or DAG behavior.
3. Add failing compiler and builder-validation tests, then implement compiled plans, stable identities, fingerprints, and atomic registration.
4. Add failing scheduler, scope-reducer, identity, and reference-model tests, then implement fibers, scopes, status derivation, and deterministic cooperative scheduling in shared Core Modules.
5. Add failing branch-contract, isolation, result, merge, and join-policy tests, then implement typed branch and ordered `ForEach` outcomes plus pure replacement-state merge.
6. Add ephemeral parity tests and move `Parallel`, `WhenFirst`, and `ForEach` to the shared logical execution model, including isolated item state and mandatory `WhenAny` residual cancellation.
7. Add envelope v2 round-trip and crash tests, then implement the durable state Adapter and driver integration.
8. Move waits, timers, pending resumes, resources, jobs, and child groups to explicit fiber/scope ownership with cancellation and root-control regressions.
9. Complete saga compensation and child-instance DAG integration over the new ownership model.
10. Run provider certification and host-replacement integration tests against PostgreSQL and SQL Server using real nested envelopes.
11. Delete cursor split/merge code, obsolete envelope records, old ephemeral branch/item schedulers, shared-state execution, and superseded tests.
12. Reset development stores and fixtures, complete canonical requirements and feature-matrix synchronization, and run the final verification gates.

Rollback during development is source-level only: revert to the preserved baseline and reset development stores. There is no dual-format runtime rollback path.

## Resolved Interface Decisions

Approved on 2026-07-13 as part of the implementation gate:

1. The fluent Interface uses `BranchScopeBuilder<TParentState, TResult>`. `Parallel<TResult>` and `WhenFirst<TResult>` receive a scope-builder delegate; each `.Branch<TBranchState>(branchId, inputProjector, build)` owns a `BranchBuilder<TBranchState, TResult>` whose explicit `.Return(resultProjector)` emits `TResult`. `WhenAll` merge receives `ReadOnlyParentSnapshot<TParentState>` plus `IReadOnlyList<BranchResult<TResult>>`; `WhenFirst` winner merge receives the selected `BranchResult<TResult>`. `ForEach<TItem, TItemState, TResult>` uses the same item-private builder and result contract. This is the only reflection-erased boundary: authored generic types are validated and compiled before runtime execution.
2. Initial configurable defaults are maximum scope depth 32, maximum 256 active fibers per instance, maximum 256 KiB serialized branch/item result payload, and maximum 4 MiB serialized execution envelope. Static violations fail compilation or registration; dynamic fanout/result/envelope violations fail the owning scope before an oversized transition commits. Benchmarks may justify changing defaults only through an explicit reviewed configuration change.
3. Merge returns replacement `TState` directly. Exceptions, null replacement where disallowed, or serialization failure produce explicit scope failure diagnostics; a validation wrapper is not part of the initial Interface.
4. Compiler diagnostics expose stable machine-readable codes from the first compiler slice, with structured instruction/scope/branch locations. Human-readable message text may evolve without changing the stable code.

## Review Checklist

Review approval must explicitly confirm all of the following before `/opsx:apply` begins:

- [x] This change and `reshape-developer-facing-interfaces` reference one joint capability matrix, compiler/diagnostic contract, and post-fiber signature baseline, and both pass strict validation.
- [x] Mode-first builders use `Build()` plus `TryBuild()`, portable dynamic `WaitForEvent` remains available, durable `ForEach` is absent plus compiler-rejected, and continue-as-new is only a durable structural node.
- [x] Local `Parallel` means deterministic cooperative fiber execution, not concurrent local step bodies.
- [x] Branches cannot mutate parent state and communicate through typed serializable results.
- [x] `WhenAll` requires explicit merge in authored branch order.
- [x] `WhenAll` is initially fail-fast and does not merge partial results.
- [x] `WhenFirst` selects the first committed terminal branch, uses authored order for same-commit ties, and cancels all losers.
- [x] `WhenFirst.Ignore` and `WhenFirst.LetRemainingComplete` are removed from the initial Interface.
- [x] Ephemeral `ForEach` uses dynamic isolated item fibers; durable mode rejects it, and `LetRemainingComplete` is removed.
- [x] There is exactly one root `Init` and one root `End`; branches use `BranchReturn`.
- [x] Public fluent authoring has no `EndIf`, `EndWhile`, or `EndParallel`; the compiler emits explicit join and exit instructions.
- [x] `ContinueAsNew` is root-only and rejects rollover while any descendant scope or owned obligation remains active.
- [x] Runtime scope/fiber identities derive from committed generation, plan identity, parent fiber, and persisted scope-entry sequence.
- [x] Scheduler state is persisted round-robin state, `Yield` ends the current fiber quantum, and internal instructions have a forced-rotation budget.
- [x] Every residual obligation has explicit fiber/scope ownership and unsupported cancellation shapes fail compilation.
- [x] Identical sibling waits are selected by persisted registration order with a stable fiber-id tie break.
- [x] Envelope format 2 replaces cursor envelopes without migration or a legacy executor.
- [x] Definition registration is atomic and binds authored version plus plan fingerprint.
- [x] Ephemeral and durable engines share compiler, scheduler, scope, join, and merge semantics.
- [x] Saga actions become compensation-eligible at forward commit; scope failure covers committed descendants and successful merge transfers that coverage upward.
- [x] Saga sibling compensation uses canonical authored order rather than wall-clock completion order while retaining deterministic plan-bound per-scope overrides.
- [x] DAG execution remains child-instance based.
- [x] Implementation proceeds test-first and cursor branching is deleted after replacement gates pass.
