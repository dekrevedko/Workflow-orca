# 8. Composition Requirements (CP)

Scope: fixed parallel branches, all-terminal joins, bounded data-driven fanout, and the
separate typed DAG front-end. First-release composition deliberately omits winner races,
general public child-workflow nodes, and multilevel dynamic fanout.

| Feature | Ephemeral workflow | Durable workflow |
|---|---:|---:|
| Root `Parallel(...).WhenAll(...)` / `WhenAllOutcomes(...)` | yes | yes |
| Root bounded `ForEach(...).WhenAll*` | yes | yes |
| Nested `If` | yes | yes |
| Nested `Parallel` / nested `ForEach` / nested `While` | no | no |
| `OrcaCore.Dag` planning | separate package | separate package |
| DAG node execution | no | durable child instance per node |
| Public `RunChild` / `RunChildren` | no | no |
| `WhenFirst` | no | no |

The approved public signatures and builder availability are owned by
[document 17](17-selected-mode-capability-matrix.md); this document defines their behavior.

## 8.1 Fixed parallel branches and joins

### CP-001 Branch model
Root-sequence-only `Parallel<TResult>` SHALL create a fixed authored scope of cooperatively
scheduled local fibers and SHALL contain at least one branch. A zero-branch scope is rejected
through `Build`/`TryBuild` as `SFE-AUTH-BRANCH-004`; it never invokes either merge with an empty
list. Every branch has a validated, scope-unique `AuthoredBranchId`, isolated
private state
copied from a read-only parent snapshot, one declared result type, and exactly one reachable
`Return`. `AuthoredBranchId` is distinct from internal branch addresses, `FiberId`, `ScopeId`,
and runtime scope-entry occurrence. Branch results/outcomes carry the authored ID and are
ordered by authored branch ordinal, never completion time.

At most one unfenced local attempt per workflow instance owns commit authority. A fenced
token-ignoring attempt may overlap physically against its discarded copy while a retry/sibling
progresses. A parked branch releases the instance turn so runnable siblings may advance. Waits, timers, leases, and every
other residual obligation carry exact fiber/scope ownership. True simultaneous work occurs
across workflow instances or external systems, not through concurrent mutation of one parent
state.

### CP-002 `WhenAll` success join
`WhenAll` SHALL wait until every branch succeeds or fails and commit one join decision. If every
branch succeeds, its pure merge consumes a read-only parent snapshot and ordered
`BranchResult<TResult>` list, returns the complete replacement parent state, and commits that
replacement atomically with scope completion. The merge MAY be reevaluated before the winning
commit and therefore SHALL be deterministic and side-effect-free. If one branch fails, that
failure fails the scope after every sibling reaches success/failure. If several fail, the scope
uses `WorkflowFailure` code `SFE-JOIN-FAILED` with causes ordered by authored branch ordinal.

### CP-003 `WhenAllOutcomes` inspection join
`WhenAllOutcomes` SHALL also wait until every branch succeeds or fails, then evaluate a pure
merge over the ordered closed success/failure `BranchOutcome<TResult>` list. There is no
branch-level cancelled outcome variant in v1. The merge returns the complete replacement parent
state and scope completion succeeds; it MAY be reevaluated before one winning commit. Authors
store the business summary in parent state and use a following `If` to accept it, transform it,
or fail. The runtime does not infer workflow status from branch completion order.

### CP-004 No automatic sibling cancellation
Neither v1 all-terminal join cancels a sibling merely because another branch failed. Every
branch is allowed to reach success/failure, subject to ancestor instance cancellation,
termination, or workflow deadline. An ancestor cancellation/deadline suppresses both merge
forms, fences active branches, and proceeds through instance cleanup/quarantine; it does not
manufacture cancelled branch outcomes. A failed sibling is not proof that another sibling's
protected work stopped.

### CP-005 Order-insensitive determinism and continuation
Branch completion interleavings and structurally irrelevant graph changes MUST NOT change the
ordered join input, merge result, or continuation count. Concurrent final completions SHALL
select exactly one join commit under instance serialization even if pure merge evaluation is
repeated after a conflict. Release/quarantine of every
branch-owned obligation that must end with the branch SHALL commit before the parent merge or
failure continuation becomes runnable.

## 8.2 Bounded root `ForEach`

### CP-010 Purpose and boundary
`ForEach<TItem,TItemState,TResult>` is the v1 data-driven fanout primitive inside one workflow
instance. It is root-sequence only, finite, and supported in both modes. It creates one private
item fiber per admitted item and never creates child workflow instances. Nested `ForEach` is
absent and rejected by the compiler.

### CP-011 Finite snapshot and stable identity
The item selector SHALL be deterministic and side-effect-free over a read-only parent snapshot.
Durable execution MAY reevaluate it before the selection commit. `ForEachOptions` requires a
positive `MaxItems`; exceeding it or a host payload/item limit fails before any item is admitted.
The runtime copies, validates, and fixed-codec-normalizes the finite item list, commits that
snapshot before first durable admission, then reuses it. An empty snapshot is valid and invokes
the selected merge with an empty ordered list. Stable item identity includes scope occurrence
plus zero-based item index; results never depend on completion time.

### CP-012 Private item state and all-terminal joins
Each item input projector creates isolated `TItemState`; the item body has exactly one reachable
typed `Return`. `WhenAll` and `WhenAllOutcomes` have the same success/failure-only semantics as
CP-002/003 over ordered `ForEachItemResult<TResult>` or `ForEachItemOutcome<TResult>` values.
The pure merge returns the complete replacement parent state, may be reevaluated before one
winning commit, and is suppressed by ancestor cancellation/termination/deadline. Neither join
automatically cancels another item after item failure.

### CP-013 Bounded admission
Optional positive node `MaxConcurrency` limits admitted nonterminal item scopes, including an
item parked in a wait, delay, or durable resource request, until that item becomes terminal. The
effective item limit is the lower of that node cap and host-owned
`MaxConcurrentExecutionPathsPerInstance`;
authors cannot raise the host ceiling. Item scopes admit by stable index. Separately, runnable
root/branch/item paths compete for the countable path tokens in CP-015.

### CP-014 Durable recovery
Durable checkpoints SHALL include the committed item snapshot, next admission index, item
fiber positions, ordered terminal outcomes, owned obligations, and join/merge status. Restart
cannot reselect items, duplicate an item occurrence, exceed the effective limit, or execute the
merge commit twice.

### CP-015 Countable execution-path tokens
A runnable root, branch, or item SHALL own one per-instance path token. It releases that token
when parking on a wait, delay, resource request, or join, and reacquires one before progressing.
A parent releases its token before admitting branches/items and reacquires one only for merge/
continuation, so a ceiling of one cannot deadlock fanout merely because the parent waits. Root
`Parallel` branches and root `ForEach` items share the same token pool; branches queue by
authored ordinal and items by index. A fenced token-ignoring attempt owns no logical path token
but retains its physical step-throttle/transient slot until it returns.

## 8.3 Typed DAG planning and execution

### CP-020 Separate package and dependency direction
`OrcaCore.Dag` SHALL be a separate project/package whose sole direct OrcaCore dependency is
`OrcaCore`; its workflow references remain public contracts. The post-gate
`admit-dag-authoring-friend-boundary` contract approves one authoring-only
`OrcaCore -> OrcaCore.Dag` internal friend for compiler-created build values, subject to an
exact compiled-type/member guard. The friend is compiled in the independently approved
Task 8.2 checkpoint `a9f835f939d683500ca231c7ba491ab8eae2aaae` and creates no reverse
package edge. The separate proposed `admit-dag-hosting-runtime-view` contract would add
`OrcaCore.Dag -> OrcaCore.Dag.Hosting` only for the three-type/fifteen-member closed
runtime view in doc 17 §17.2.6 (the exhaustive proposed signature block); it is not approved or compiled, exposes no delegates/drafts, and
adds no codec or child-start access to Dag. No OrcaCore package other than `OrcaCore.Dag.Hosting` SHALL depend on
`OrcaCore.Dag`; `OrcaCore.Dag.Hosting` is the approved outward DAG host adapter.
The DAG package may remain in the same solution for v1 and compiles to the existing durable
runtime rather than introducing a second workflow engine.

### CP-021 Typed immutable run input and node references
A DAG definition declares one immutable run-input type. Every `DagNodeId` is validated and
unique. A resultful node references `DurableWorkflowRef<TNodeInput,TNodeOutput>` and yields
`DagNodeRef<TNodeOutput>`; a resultless node references `DurableWorkflowRef<TNodeInput>` and
yields non-generic `DagNodeRef`. No node selects a definition by raw string or inspects another
workflow's private state. Both node kinds may be dependencies. The compiler rejects cycles,
missing/duplicate dependencies, duplicate IDs, missing/duplicate `MapInput`, and self/foreign
references before registration. Typed references make wrong-output-type and resultless-node
`OutputOf` calls compile-impossible.

### CP-022 Direct-dependency input mapping
A node input projector MAY read the immutable DAG-run input and typed successful outputs of its
declared direct resultful dependencies only. Because the mapper delegate is opaque, the runtime
validates every `OutputOf` access during mapper invocation after all direct dependencies succeed.
If any direct dependency is non-success, the mapper is not invoked and the dependent node becomes
`DependencyBlocked`. An undeclared/non-direct or unavailable-output access, or a projector failure,
fails the node deterministically as `DAG_INPUT_MAPPING_INVALID` before mapped-input commit or
child start;
independent ready nodes may still progress. The runtime fixed-codec-normalizes a valid input,
records `MappedInputFingerprint` over the bytes, commits it before child start, and reuses it
after restart. The proposed runtime-view amendment clarifies that the durable bridge decodes
successful committed outputs before evaluation; a present successful null is valid for a
reference or nullable-value declared type, as proposed in doc 17 §17.2.6. Missing/wrong-type
outputs and nonnullable-value null remain invalid. This clarification is not yet approved
or implemented. The proposed mapper-exception and bridge decode-failure classifications in
doc 17 §17.2.6 apply before commit/start; runtime cancellation and protocol/storage failures
are not disguised as mapping failures. Graph/mapping structure is in the structural DAG fingerprint; changing projector
logic requires a new DAG `DefinitionVersion`.

Small immutable DTOs SHOULD flow as node outputs. Large datasets, artifacts, and logs SHALL be
stored externally and represented by typed references.

### CP-023 One durable child instance per node occurrence
Each executable DAG node occurrence SHALL run as one durable child workflow instance with
stable `ParentInstanceId`/`RootInstanceId` lineage. Runtime-owned internal child-start and join
records provide idempotency, recovery, cancellation propagation, and typed output handoff.
They are not public `RunChild`/`RunChildren` authoring members.

### CP-024 Runtime-owned progression
The runtime SHALL admit ready nodes, observe committed child terminal facts, persist node
status, and continue until the DAG is terminal; callers do not pump ready/completed sets.
`DagNodeStatus` is `Pending`, `Ready`, `Running`, `CancellationRequested`, `Succeeded`, `Failed`,
`Cancelled`, or `DependencyBlocked`; `DagRunStatus` is `Running`, `CancellationRequested`,
`Succeeded`, `Failed`, or `Cancelled`. Concurrent dependency completions cannot start a node
twice. Child `Failed`, `TimedOut`, or `Terminated` maps to node `Failed` with stable child-failure
code; child cancellation not caused by DAG cancellation maps to `Failed`/`CHILD_CANCELLED`.
Every non-success blocks transitive dependants as `DependencyBlocked`; independent nodes may
continue.

### CP-025 DAG concurrency
The DAG host owns `MaxConcurrentNodes`, separate from per-instance execution-path limits and
durable resource pools. The limit counts every started nonterminal child instance, including one
parked in a wait, delay, or lease queue, until that node becomes terminal; releasing an
in-instance path token does not free DAG admission. A node starts only when its dependencies are
satisfied and node admission is available. Restart reconstructs the same ready/running/blocked
sets from committed facts and re-applies the host-owned limit without treating admission as
persisted ownership.

### CP-026 Typed successful output handoff
A child node output becomes dependency-visible only when it commits atomically with successful
workflow completion. Failure, cancellation, termination, or completion without the declared
output cannot satisfy a dependent mapping. A downstream node never reads a live or partially
committed child state object.

### CP-027 Complete deterministic DAG snapshots
Every `DagRunSnapshot` SHALL expose run/definition identity, version, structural fingerprint,
status, timestamps, and every `DagNodeSnapshot` in stable authored ordinal. Each node snapshot
includes ID/ordinal/status, declared dependencies, child instance ID, mapped-input fingerprint,
output availability, ready/start/completion timestamps, and failure. Ready-admission ties and
all snapshot ordering use authored ordinal, never dictionary or wall-clock order.

### CP-028 DAG cancellation
A DAG cancellation request SHALL prevent new admission, immediately mark `Pending`/`Ready`
nodes `Cancelled`, move running nodes to `CancellationRequested`, and request cancellation of
their child instances. `DependencyBlocked` remains explanatory. The run becomes `Cancelled`
only after every running child reaches terminal. Without a DAG cancellation request, any
non-success terminal node makes the run `Failed` rather than `Cancelled`.

## 8.4 Shared composition rules

### CP-030 Parent state changes only through one merge
Branch/item bodies cannot mutate parent state. A join merge is synchronous, deterministic, and
side-effect-free; it receives read-only parent state plus canonical ordered results/outcomes and
returns the complete replacement state. It may run more than once before one winning durable
commit; durable execution commits that state and the join fact atomically.

### CP-031 Cooperative cancellation
Workflow cancellation is cooperative for in-flight local steps and is recorded before
continuations run. Ancestor cancellation, termination, or deadline suppresses pending merges
rather than creating branch/item cancelled outcomes. Cancellation of a fiber with potentially
active protected external work does not free its durable lease until MG-064 proof commits.

### CP-032 Nested capability rules are explicit
Of the conditional/loop/fanout operators, only `If` nests in v1 and is supported in branch,
item, conditional, loop, and lease bodies.
`Parallel`, `While`, and `ForEach` are root-sequence only and are absent from nested, branch,
item, and leased builders. Those builders otherwise retain sequencing, `Wait`, `Delay`, and step
decorators; durable nonleased builders retain `AcquireResources` when no live ancestor lease
exists. A root `Parallel` branch or root `ForEach` item may therefore open an independent durable
lease, and a root `While` may contain a lexical lease scope that fully exits each iteration. A
leased body exposes no loop/fanout operator.
Unsupported nesting is absent from the staged builder and is a build-time diagnostic for
manually constructed graphs.

## 8.5 Explicitly deferred composition

The following remain design-visible future work but have no v1 public member, alias, obsolete
tombstone, reflection-visible placeholder, or positive compile fixture:

- `WhenFirst`: requires one approved winner/tie rule, loser fate, merge/failure contract, and
  protected-resource interaction.
- Public `RunChild`/`RunChildren`: requires a typed parent/child mapping, group result,
  cancellation, and failure-policy amendment; DAG internal commands do not pre-approve it.
- Nested `Parallel`: requires recursive scope identity, admission, merge, recovery, and lease
  interaction rules.
- Nested `ForEach`: requires combined bounds, multilevel identity/admission, payload, recovery,
  and merge rules.
- Nested `While`: requires exact re-entry analysis and runtime evidence.
- Saga child compensation: remains part of the deferred saga package, not composition v1.
