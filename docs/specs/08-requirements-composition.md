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
`Parallel` SHALL support multiple branches with isolated branch state and branch identity.
Waits inside different branches remain isolated (branch identity participates in wait
records). Branches MAY execute concurrently; all branch-state commits and join checks pass
through the per-instance serialized path (CR-044).

### CP-002 `WhenAll` join
`WhenAll` SHALL fire its continuation exactly once after all branches complete. The join
check is atomic under the instance serializer — two branches completing simultaneously MUST
NOT both trigger the continuation.

### CP-003 Order-insensitive determinism
Branch completion order MUST NOT change the final committed outcome, and behavior MUST NOT be
sensitive to structurally irrelevant graph changes (adding a no-op step must never change
join/continuation semantics).

### CP-004 `WhenFirst`
`WhenFirst` SHALL define deterministically: which branch wins when completions race (exactly
one winner per documented policy), and what happens to losing branches via an explicit
residual policy — cancelled, ignored, or allowed to finish — with the outcome observable.
Losing-branch waits/timers are resolved per EV-044/CR-032.

### CP-005 Continuation ordering
What executes after branch completion and in what order SHALL be formally specified (join
eligibility, continuation scheduling), not left to implementation accident.

## 8.2 Lightweight `ForEach` (ephemeral engine)

### CP-010 Purpose and boundaries
`ForEach` is the data-driven fanout primitive **inside one instance**: runtime item
discovery, optional batching, bounded in-process concurrency, explicit join/failure behavior.
It SHALL NOT create child instances, lineage, compensation, or durable records, and SHALL be
resultless by default (parent observes item/batch completion status, not return values).

### CP-011 Authoring shape
`ForEach` SHALL accept: an item selector over business state, a partitioner, a body
(sub-flow applied per item/batch), a join policy (`WhenAll` | `WhenAny`), a failure policy
(`FailFast` | `WaitAllThenFail` | `ContinueWithPartialFailures`), an optional residual policy
for `WhenAny` (`CancelRemaining` | `LetRemainingComplete`), and optional `maxConcurrency`.

### CP-012 Runtime model
The parent instance SHALL own group state: total items, dispatch index, active/completed/
failed/cancelled counts, and per-item refs (index, status, error, timestamps) — inspectable
like any runtime state. `WhenAny` cancellation is cooperative and in-process only;
cancellation intent is recorded before the parent continues.

### CP-013 Bounded concurrency
`maxConcurrency` SHALL limit concurrently active items; completion of an item releases the
slot to the next dispatch index.

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
