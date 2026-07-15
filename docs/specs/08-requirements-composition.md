# 8. Composition Requirements (CP)

Scope: parallel branches and joins, first-completion semantics, data-driven fanout
(`ForEach`), and child workflow orchestration (`RunChild`/`RunChildren`). The orchestration
surface is deliberately split by engine: the ephemeral engine stays lightweight and
in-process; heavyweight cross-instance orchestration is durable-only.

| Feature | Ephemeral engine | Durable engine |
|---|---|---|
| `Parallel` / `WhenAll` / `WhenFirst` | yes | yes |
| Lightweight `ForEach` | yes | no |
| `RunChild` / `RunChildren` (child instances, lineage, outbox spawning) | no | yes |
| Compensation for children | no | yes (durable sagas) |

## 8.1 Parallel branches and joins

### CP-001 Branch model
`Parallel` SHALL support multiple branches as cooperatively scheduled local fibers with
stable branch identity, isolated serializable input/private state, one declared result type,
and explicit merge. At most one local branch step body per instance executes at a time.
Waits and every other residual obligation SHALL carry exact fiber/scope ownership. Branch
results, joins, merge, and ownership changes pass through the per-instance serialized path
(CR-044); true concurrent work uses external jobs or child workflow instances.

### CP-002 `WhenAll` join
`WhenAll` SHALL fire its continuation exactly once after all branches complete. The join
check is atomic under the instance serializer — two branches completing simultaneously MUST
NOT both trigger the continuation.

### CP-003 Order-insensitive determinism
Branch completion order MUST NOT change the final committed outcome, and behavior MUST NOT be
sensitive to structurally irrelevant graph changes (adding a no-op step must never change
join/continuation semantics).

### CP-004 `WhenFirst`
`WhenFirst` SHALL select exactly the first committed terminal branch, with stable authored
branch order breaking same-commit ties. A successful winner alone is supplied to the winner
merge; a failed winner fails the scope without merge. Every losing descendant SHALL be
cancelled, and
cancellation/release facts for losing waits, timers, jobs, resources, retries, and other
owned obligations SHALL commit before or atomically with scope completion. Ignore and
let-remaining-complete residual policies are unsupported and SHALL fail definition
validation.

### CP-005 Continuation ordering
What executes after branch completion and in what order SHALL be formally specified (join
eligibility, continuation scheduling), not left to implementation accident.

## 8.2 Lightweight `ForEach` (ephemeral engine)

### CP-010 Purpose and boundaries
`ForEach` is the data-driven fanout primitive **inside one instance**: runtime item
discovery, optional batching, bounded item-fiber admission, isolated item state, and explicit
join/failure behavior. It SHALL compile only for ephemeral execution as a dynamic execution
scope over the shared fiber scheduler and scope reducer. It SHALL NOT create child instances,
lineage, compensation, or durable records. It MAY be resultless, in which case item fibers do
not mutate parent state and the parent observes ordered item status, or resultful through an
explicit deterministic merge over typed item outcomes.

### CP-011 Authoring shape
`ForEach` SHALL accept: an item selector over business state, a partitioner, a body
(sub-flow applied per item/batch against item-private state), a common typed item result, a
join policy (`WhenAll` | `WhenAny`), a failure policy (`FailFast` | `WaitAllThenFail` |
`ContinueWithPartialFailures`), optional positive `maxConcurrency`, and an optional
deterministic merge over ordered `ForEachItemOutcome<TResult>` values. `WhenAny` SHALL accept
only `FailFast`, SHALL cancel every remaining item, and SHALL reject
`LetRemainingComplete`, `WaitAllThenFail`, and `ContinueWithPartialFailures` combinations.

### CP-012 Runtime model
The parent instance SHALL own one dynamic `ForEach` scope containing total descriptors,
dispatch index, admitted/completed/failed/cancelled counts, ordered item outcomes, and exact
item-fiber ownership. A first committed terminal `WhenAny` item wins, with item index breaking
same-commit ties. A failed winner fails without merge; a successful winner supplies one
outcome to optional winner merge. Cancellation of admitted fibers and pending descriptors is
recorded before the parent continues. Item outcomes and management state SHALL be ordered by
stable item index, not completion timing.

### CP-013 Bounded concurrency
`maxConcurrency` SHALL limit admitted nonterminal item fibers. Completion of an admitted item
releases admission for the next stable dispatch index. Local item step bodies remain
cooperative and SHALL NOT execute concurrently within one instance.

## 8.3 Child workflows (durable engine): `RunChild` / `RunChildren`

### CP-020 Child instances and lineage
Each child SHALL be a separate workflow instance linked by lineage metadata
(`ParentInstanceId`, `RootInstanceId`), enabling tree queries across nested groups. Children
are resultless by default: the parent observes completion/failure/cancellation status, direct
child state inspection, shared projections, or domain messages — not return values. Typed
child outcomes, if ever needed, are a separate future feature.

### CP-021 Authoring shape
`RunChild(input, child)` runs one child; `RunChildren(items, partition, child, join,
failure, maxConcurrency)` fans out dynamically. Join policies: `Wait`, `WhenAll`, `WhenAny`,
`ContinueEach`, `FireAndForget`. Failure policies: `FailFast`, `WaitAllThenFail`,
`ContinueWithPartialResults`. `WhenAny` residual policies: `CancelRemaining`,
`LetRemainingComplete`, `DetachRemaining`. Group-level retry is intentionally out of scope
(retries belong inside child steps or adapters).

### CP-022 Durable spawning through the outbox
Child starts SHALL be lowered to durable group records plus child-start commands in the
unified outbox (DU-033): materialize items → partition deterministically → persist the group
with all child refs pending → register the parent's synthetic wait (non-fire-and-forget) →
enqueue the initial start window → parent advances to `Waiting` (or onward for
`FireAndForget`). A committed parent state claiming a child was scheduled without a committed
start command is non-conforming.

### CP-023 Durable throttling
`maxConcurrency` SHALL be enforced by durable scheduler state (dispatch index + active
count); restart reconstructs the window from persisted group state. It is an execution rule,
not an observational hint.

### CP-024 Exactly-once parent resume
Group joins SHALL resume the parent exactly once via a durable resume token: the winning
completion records the token; residual cancellation intent (for `WhenAny+CancelRemaining`)
is recorded before resume is emitted; the parent consumes the token idempotently; restart
replays the same token and never mints a second one.

### CP-025 Deterministic identity across restart
Child IDs, item snapshots, and partitioning SHALL be deterministic and stable across restart:
re-running spawn logic after a crash MUST NOT duplicate children or repartition differently.

### CP-026 Child lifecycle propagation
Parent behavior on child completion, failure, and cancellation SHALL follow the configured
join/failure policies deterministically (child failure policy enforced; completion
propagates; cancellation observable).

## 8.4 Shared composition rules

### CP-030 Deterministic partitioners
Partitioning (per-item, fixed batch, state-driven batch size, custom) SHALL produce
deterministic, stable output ordering for the same input — a restart-safety requirement for
any fanout.

### CP-031 Cooperative cancellation
All composition cancellation is cooperative: in-flight work is signalled via
`CancellationToken`; cancellation intent is recorded (durably, in durable mode) before
continuations run.

### CP-035 Child compensation (durable sagas only)
Compensation for completed children SHALL be explicit (`Compensate(group, spec)`-style),
durable-only, spawning one compensation per completed child, idempotent on repeat, and never
implied by cancellation or completion (SG-011/SG-012). Compensation APIs MUST NOT appear on
ephemeral or core surfaces.

## 8.5 Nesting and depth

### CP-040 Nesting rules are explicit
Which containers may nest (e.g., `Parallel` inside `Parallel`, `ForEach` inside branches,
children spawning children) SHALL be explicitly specified per phase; unsupported nesting is a
build-time validation error (CR-002). Child-tree depth limits, if any, are explicit.
