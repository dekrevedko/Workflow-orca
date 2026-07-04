# R2 — Core (Building / Definitions / Lifecycle / Concurrency) — Findings

> **Baseline:** same as [R1](R1-abstractions.md) (build green `-warnaserror`; full suite
> 892 passed / 1 flaky / 16 skipped).
> Scope: `v3-gpt/src/OrcaCore.Core/**` against 04 CR-001…008, CR-030, CR-040…044 (for the shared
> `InstanceLane`), 08 CP-040.

## Findings

### [P1] Durable-only primitives are not rejected at build time for ephemeral use — runtime failure instead — `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs:152`
> **FIXED 2026-07-03:** `WorkflowDefinition.RequiresDurableEngine` computed at `Build()`;
> `EphemeralWorkflowEngine.RegisterDefinition` rejects durable-only definitions before any
> execution (scenario NEG-CR-017). Runtime interpreter failure kept as defense-in-depth.
- **Requirement/convention:** CR-001 ("durable-only … constructs are absent from surfaces where they are invalid"), CR-002 (build-time validation SHALL verify "no durable-only primitives in ephemeral definitions")
- **Evidence:** `WorkflowBuilder<TState>` exposes `RunChild`/`RunChildren` unconditionally (`WorkflowBuilder.cs:217/229`); the ephemeral interpreter handles those nodes with `failureHandler.Fail(..., new NotSupportedException("Durable child workflow nodes require the durable engine."), ...)` — i.e. the instance starts, runs earlier steps (with side effects), then fails mid-execution.
- **Failure scenario:** Author builds a definition with `RunChild` and registers it on the ephemeral engine: `Build()` succeeds, validation passes, the instance executes its first steps (e.g. sends an email) and only then fails at the `RunChild` node — a class of error the spec requires to be impossible to author.
- **Recommendation:** Either (a) split the authoring axis per CR-001 — an ephemeral builder type without `RunChild(ren)` and a durable builder that adds them (Template Method / interface segregation, matches the spec's "builders exist per axis combination"), or minimally (b) tag definitions containing durable-only nodes and have ephemeral registration/start reject them before execution.
- **Confidence:** CONFIRMED

### [P2] Transitively blocked DAG nodes are invisible — a DAG run can stall with no runnable and no blocked-reported nodes — `v3-gpt/src/OrcaCore.Core/Building/WorkflowDagBuilder.cs:206`
> **FIXED 2026-07-03:** `GetBlockedByFailures` computes the transitive closure;
> `WorkflowDagPlan.IsComplete` / `WorkflowDagRunner.IsComplete` added with tests.
- **Requirement/convention:** 14 JS-* (DAG driving scenario), CP composition semantics
- **Evidence:** `GetBlockedByFailures` returns only nodes whose **direct** dependency failed; `GetRunnableNodes` excludes any node with an incomplete dependency. For `A → B → C` with `A` failed: `B` is reported blocked, `C` is neither runnable nor blocked.
- **Failure scenario:** A consumer that marks only reported-blocked nodes as failed and re-queries (`WorkflowDagRunner.GetNextBatches`) converges only if it iterates to fixpoint; a single-pass consumer concludes "no work, not done" and the DAG run hangs without diagnosis.
- **Recommendation:** Compute the transitive closure in `GetBlockedByFailures` (or add `GetUnreachableNodes`), and have `WorkflowDagRunner` expose a terminal-state check (`IsComplete`/`IsStalled`).
- **Confidence:** PLAUSIBLE (depends on how durable orchestration consumes the runner; reference runner itself does not iterate)

### ~~[P3] Empty `If` then branch passes validation~~ — WITHDRAWN (works as intended)
- **Resolution (2026-07-03 fix pass):** empty then-branches are deliberate, pinned behavior —
  `InterpreterControlFlowTests.Run_IfWithEmptyThenBranch_ContinuesAfterBranch` asserts an empty
  branch is a supported no-op that continues after the `If`. Not a defect; no change made.

### [P3] `Then(IStep)` shares one step instance across all workflow instances — statelessness contract undocumented — `v3-gpt/src/OrcaCore.Core/Building/WorkflowBuilder.cs:41`
> **FIXED 2026-07-03:** contract documented on the overload (stateless/thread-safe required;
> factory overload for per-execution state).
- **Requirement/convention:** CR-012 authoring contract, 02 conventions
- **Evidence:** `Then(IStep<TState>? step)` captures the instance in a closure returned for every execution; a step with mutable fields is shared across concurrently executing instances.
- **Failure scenario:** Author gives the step an instance field as scratch state; two concurrent instances interleave writes — data corruption that never reproduces single-threaded.
- **Recommendation:** Document "step instances must be stateless/thread-safe; use the factory overload otherwise" on the overload (and prefer the factory overload in samples).
- **Confidence:** CONFIRMED

## Positives verified (no finding)

- **CR-002 accumulated validation:** `BuildValidated` collects *all* errors (missing Init,
  unreachable End, null delegates, non-positive delays/timeouts/concurrency, duplicate branch
  names, invalid policies) with stable codes and node paths; `Build()` aggregates into one
  `WorkflowDefinitionException`.
- **Immutability:** builder output materializes eagerly (`ReadOnlyList<T>` copies to array —
  no lazy re-enumeration of `yield return` builders); nodes are internal sealed records.
- **CR-030:** `LifecycleMachine` is an explicit `FrozenDictionary` transition table; illegal
  triggers return typed `WorkflowLifecycleException` failures; terminal statuses are a closed
  frozen set.
- **CR-040/042 substrate:** `InstanceLane` (shared by both engines) was traced for the
  enqueue-vs-idle-close race: a late writer hits `ChannelClosedException` → retries into a fresh
  lane; the post-`TryComplete` drain covers items written between the empty-read and the close;
  single-reader channel serializes work items. Bounded (1024, `Wait`), Channels-based per the
  stack decision (closes the R8 "Channels substrate not used" finding for this seam).
- **DAG:** cycle detection with reported cycle path; duplicate/missing-node validation;
  heterogeneous runnable sets now group into per-definition batches (`CreateChildBatches`),
  and `CreateChildBatch` rejects heterogeneous input — closes R6's P1.

## Coverage note

Verified: CR-001 (fluent builder; axis-split gap → P1 above), CR-002, CR-003 (tree,
container nodes), CR-004 (DefinitionId/Version on `WorkflowDefinition`), CR-006 (policies as
decorator set on step/definition), CR-008 (named End outcomes), CR-030, CR-040/042 (in-process
substrate only — cross-host enforcement is R4/R5 scope), CP-040 partially (nesting is unlimited
but structurally validated; no depth limit exists, which CR-003 permits). Not reached: CR-005
runtime semantics (Init execution is engine scope), SagaBuilder/WorkflowPartitioner internals
(skimmed only), CR-007 (no DSL exists — trivially conforms).
