# T1-03: Definition graph model

**Difficulty**: Haiku        **Depends on**: T1-01
**Spec**: CR-002 (immutability), CR-003, CR-004, CR-015        **AC**: none directly

## Goal
The immutable composite tree a built workflow definition compiles to, plus the
execution-pointer frame model the interpreter will walk. Pure data — no execution logic.

## Read first
- Spec: [specs/04-requirements-core-runtime.md](../../../specs/04-requirements-core-runtime.md) §4.1–4.2 (CR-003, CR-015)
- `v3/src/OrcaCore.Abstractions/Steps/` (T1-01)

## Deliverables
In `v3/src/OrcaCore.Core/Definitions/` (internal except the definition handle):
- Node hierarchy (immutable records): `InitNode` (owns input→state construction delegate,
  CR-005), `BusinessStepNode` (holds a step factory — `Func<IStep<TState>>` or DI key; no
  step instances captured), `EndNode` (optional outcome name — CR-008 data only),
  `IfNode` (condition delegate + then/else sequences), `WhileNode`, `ParallelNode`
  (list of named branches), `WaitNode` (event name + correlation selector delegate),
  `SequenceNode`.
- `WorkflowDefinition<TState>` — public, immutable: `DefinitionId`, `DefinitionVersion`,
  root sequence; no mutable anything (verified by test).
- `ExecutionPointer` — stack of `Frame` records (node path + per-container position:
  sequence index, loop iteration counter, branch id) per CR-015; value-equal, cloneable.

## Tests to write FIRST
In `v3/tests/OrcaCore.Core.Tests/Definitions/`:
1. `Definition_IsDeeplyImmutable` — node lists are read-only; records expose no setters
2. `NodeTree_Nesting_ComposesFreely` — If containing Parallel containing Wait constructs
   and round-trips structurally
3. `ExecutionPointer_PushPop_TracksNestedPosition` — enter If→then→step; frames reflect it
4. `ExecutionPointer_ValueEquality_HoldsForSamePath`
5. `ExecutionPointer_LoopFrames_CarryIterationCounter` — two iterations produce distinct
   pointers (foundation for EV-043 later)

## Implementation notes
- Delegates in nodes must be pure/deterministic by convention (CR-012, NF-020): document it
  on each delegate-holding member.
- Branch identity: `ParallelNode` branches carry stable `BranchId` (ordinal + name) —
  wait isolation in T1-08/T1-12 depends on it.

## Out of scope
- The builder (T1-04), interpretation (T1-05), validation rules, durable/DAG/saga node
  kinds.

## Definition of done
- [ ] All listed tests green; zero warnings
- [ ] No execution logic anywhere in `Definitions/`
- [ ] PROGRESS.md updated; committed as "T1-03: definition model (CR-003/015)"
