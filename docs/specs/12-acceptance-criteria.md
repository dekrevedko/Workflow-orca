# 12. Acceptance Criteria Catalog (AC)

Consolidated, numbered acceptance criteria. Each criterion is stated Given/When/Then and
tagged with the requirements it verifies. Groups: core (AC-0xx), events/waits (AC-1xx),
composition (AC-2xx), durable (AC-3xx), saga (AC-4xx), management/operations (AC-5xx),
child workflows & fanout (AC-6xx). Criteria marked **[provider]** belong to the provider
certification suite (PR-024).

## Core runtime (AC-0xx)

- **AC-001** *Straight-line completion* — Given `Init → Step → End`, when started, the
  instance reaches `Completed` and business state reflects the step's writes. [CR-010,
  CR-020]
- **AC-002** *Conditional branch* — Given `If`, exactly one branch executes (then/else per
  condition) and flow continues after the block. [CR-010]
- **AC-003** *Loop repeats until false* — Given `While` true for 3 iterations, the body runs
  exactly 3 times and the workflow completes after the 4th check. [CR-010]
- **AC-004** *Step failure* — Given a failing/throwing step, the instance reaches `Failed`,
  later steps do not execute, and error details are inspectable. [CR-014]
- **AC-005** *Illegal lifecycle triggers rejected* — Given a terminal instance, any trigger
  (event, resume, retry-in-ephemeral) is rejected with a clear error. [CR-030]
- **AC-006** *Serialized outcome under concurrency* — Given a waiting instance and two
  concurrent resume attempts, one valid sequential outcome results; no double continuation.
  [CR-040]
- **AC-007** *Racing branch completions serialize* — Given parallel branches completing
  concurrently, branch-state updates commit in a serial order and join semantics remain
  deterministic. [CR-040, CP-002]
- **AC-008** *Builder validation accumulates* — Given a definition with several structural
  errors, build reports all of them together. [CR-002]
- **AC-009** *No live-instance leakage* — Given any public API result, mutating it does not
  affect engine state; typed state reads return copies and mismatched types fail clearly.
  [CR-021]
- **AC-010** *Completion blocked by unresolved runtime work* — Given outstanding
  runtime-owned waits/timers at an apparent terminal path, completion is rejected or the work
  is cancelled per explicit policy. [CR-032]
- **AC-011** *Synchronous completion bridge* — Given an effectively-immediate workflow
  started through the completion bridge, the caller can await/poll to a terminal snapshot
  (`Completed` or `Failed`) without gaining access to live internal state. [CR-016, CR-021]
- **AC-012** *Named End outcome recorded* — Given a workflow with named `End` outcomes, the
  reached outcome name is recorded in queryable runtime metadata and carried on the
  completion lifecycle event. [CR-008]
- **AC-013** *Yield commits progress* — Given a step that yields N times before completing,
  each yield commits accumulated business-state progress (surviving a crash in durable mode),
  the instance stays `Running` throughout, no duplicate effects occur, and the step completes
  exactly once. [CR-017]
- **AC-014** *Graceful cancel* — Given a running instance with an in-flight step and active
  waits, when cancelled, the step observes its `CancellationToken`, waits and timers move to
  `Cancelled`, cancellation policies apply, and the instance commits `Cancelled`. [CR-031,
  EV-044]
- **AC-015** *Forced terminate* — Given a running instance, when terminated, no further step
  execution occurs beyond the current commit boundary, runtime-owned waits/timers are
  forcibly resolved, no policies or compensation run, and the instance commits `Terminated`.
  [CR-031]

## Events, waits, timers (AC-1xx)

- **AC-101** *Wait enters waiting state* — Executing `Wait` sets status `Waiting` with one
  inspectable active wait. [EV-021, EV-040]
- **AC-102** *Matching event resumes exactly once* — A matching event resumes from the wait
  point once, with the payload available to the next step. [EV-022, EV-023]
- **AC-103** *Non-matching event does not resume* — Wrong `EventName` or `CorrelationId`
  leaves the instance waiting. [EV-020]
- **AC-104** *Out-of-order event buffered then consumed* — An event arriving before its wait
  is buffered and consumed when the wait registers, without re-sending. [EV-030]
- **AC-105** *Duplicate event deduplicated* — The same `EventId` delivered twice produces one
  consumption and one continuation. [EV-031]
- **AC-106** *Correlation-targeted routing resumes exactly one* — With multiple waiting
  instances on distinct correlations, a correlation-targeted event resumes only the unique
  match. [EV-010, EV-012]
- **AC-107** *Ambiguous/missing correlation rejected* — Zero matches → clear "no active
  wait" error; multiple matches → clear ambiguity error. [EV-012]
- **AC-108** *Definition fanout is scoped* — Fanout delivers only to instances of the target
  definition. [EV-010]
- **AC-109** *Wait-in-loop isolation* — Each `While` iteration creates a fresh wait; an event
  for a previous iteration cannot resume a later one. [EV-043]
- **AC-110** *Parallel waits isolated by branch* — With different waits in parallel branches,
  one matching event resumes only its branch. [EV-021, CP-001]
- **AC-111** *Timer completes after due time* — A delay/timer step continues the workflow
  exactly once after its due time. [EV-050]
- **AC-112** *Timer/event race deterministic* — Waiting on event + timeout simultaneously,
  exactly one wins per policy; the loser is cancelled/ignored per policy; terminal behavior
  is deterministic. [EV-051]
- **AC-113** *Step timeout policy enforced* — Exceeding a configured step timeout triggers
  the configured action deterministically, reflected in lifecycle events. [EV-052, MG-041]
- **AC-114** *No event loss on crash between match and commit* — **[provider]** A crash after
  match but before commit leaves the wait `Active` and the event re-matchable; no lost
  events, no double effect. [EV-032, DU-020]
- **AC-115** *Efficient bulk retrieval* — Given many instances, retrieving a set by IDs or by
  filter is a single bulk operation returning all matches — no per-instance round-trips and
  no dependency on optional indexing infrastructure. [EV-013]

## Composition (AC-2xx)

- **AC-201** *Join exactly once* — `Parallel` + `WhenAll`: the continuation runs exactly once
  after all branches complete. [CP-002]
- **AC-202** *Order-insensitive outcome* — Branches completing in different orders across
  runs yield the same committed outcome. [CP-003]
- **AC-203** *Graph-shape insensitivity* — Adding a structurally irrelevant no-op step does
  not change join/continuation behavior. [CP-003]
- **AC-204** *WhenFirst winner deterministic* — With near-simultaneous branch completions,
  exactly one winner per documented policy. [CP-004]
- **AC-205** *Losing-branch policy observable* — Losing branches follow the configured
  residual policy and the outcome is inspectable. [CP-004]

## Durable execution (AC-3xx)

- **AC-301** *Durable wait survives restart* — **[provider]** A workflow suspended in a
  durable wait remains resumable after host stop/start. [DU-013, EV-040/041]
- **AC-302** *Rehydration restores committed state only* — **[provider]** After a crash
  mid-execution, rehydration restores the last committed state; no partial transition
  observed. [DU-020]
- **AC-303** *WaitLong durable-only* — `WaitLong` is accepted on the durable surface with
  cold-wait semantics; it is absent from the ephemeral builder and rejected on any forced
  path. [EV-041]
- **AC-304** *Cold eviction and lazy resume* — After `WaitLong` registration commits, the
  instance is evictable; a later matching event rehydrates and resumes it. [EV-041, MG-050]
- **AC-305** *Restart-safe dedup* — **[provider]** Duplicate events delivered before and
  after restart produce no duplicate committed outcomes. [DU-030]
- **AC-306** *Version binding* — An instance started under version N remains bound to N as
  newer versions deploy. [DU-040]
- **AC-307** *Incompatible change fails explicitly* — **[provider]** Deploying an
  incompatible definition version never silently corrupts a suspended instance: it continues
  under version rules or fails with explicit diagnostics. [DU-041]
- **AC-308** *Durable inspection without payload* — Operators query instances by status,
  definition, version, and wait state from durable metadata only. [DU-070]
- **AC-309** *Concurrent durable resume serializes* — **[provider]** Racing resume attempts
  on a durable instance produce exactly one committed outcome. [DU-022]
- **AC-310** *Outbox publish-after-commit* — **[provider]** No message dispatches unless its
  outbox record committed with the state transition; failures around the boundary cannot
  yield state-without-message or message-without-state beyond documented at-least-once.
  [DU-031/032]
- **AC-311** *Idempotent start* — `StartOrGet` with a repeated key returns the existing
  instance; no duplicate is created. [DU-053]
- **AC-312** *History pressure observable* — Growing stream/checkpoint/outbox volume is
  detectable through supported statistics. [DU-052, MG-031]
- **AC-313** *Continue-as-new preserves identity* — History rollover preserves logical
  identity and documented continuity semantics. [DU-042]
- **AC-314** *Retention-safe purge* — **[provider]** Archive/purge follows policy and never
  removes active instances or breaks in-flight handling. [DU-051, PR-022]
- **AC-315** *Multi-node single mutator* — **[provider, advanced]** With multiple hosts able
  to process one instance, only one committed execution path succeeds at a time. [DU-060]
- **AC-316** *No critical persistence in shutdown hooks* — A host killed before deactivation
  hooks run loses nothing: transitions were committed at safe boundaries. [DU-021]

## Saga (AC-4xx)

- **AC-401** *Success without compensation* — All forward steps succeed → saga `Completed`;
  no compensation runs. [SG-013]
- **AC-402** *Failure triggers compensation* — A failing step after completed forward steps
  starts compensation for eligible steps. [SG-011]
- **AC-403** *Deterministic compensation order* — Compensations run in reverse
  successful-completion order (or the explicit override). [SG-010]
- **AC-404** *Compensation failure observable* — A failing compensating action yields the
  distinct `CompensationFailed` outcome. [SG-013]
- **AC-405** *Timeout/compensation interaction* — A forward-step timeout applies the
  documented timeout outcome and compensation policy. [SG-014]
- **AC-406** *Durable saga resumes without duplication* — **[provider]** Host restart
  mid-saga resumes from durable state without duplicate forward or compensating actions.
  [SG-020]
- **AC-407** *Compensation audit complete* — A compensated saga shows forward actions,
  compensations, order, and outcome on inspection. [SG-021]
- **AC-408** *Manual recovery recorded* — An allowed operator recovery on
  `CompensationFailed` transitions per policy and records the intervention. [SG-022]
- **AC-409** *Repeated compensation idempotent* — Re-requesting compensation produces no
  duplicate effects. [SG-012]

## Management & operations (AC-5xx)

- **AC-501** *Filtered queries over metadata* — `Where(x => …)` over snapshots lists/counts
  only matching instances. [MG-001/002]
- **AC-502** *Query/command symmetry* — The same selection drives `List()` and a command
  (e.g. `Retry()`), subject to mode support. [MG-003]
- **AC-503** *Statistics by definition and status* — Grouped counts return correctly across
  definitions and statuses. [MG-030]
- **AC-504** *Idle instance evicted safely* — An idle instance is evicted; later resume and
  inspection work from durable state. [MG-050]
- **AC-505** *Terminal instance evicted after handling* — Post-terminal-handling instances
  leave memory and stay durably queryable. [MG-053]
- **AC-506** *Eviction never duplicates mutators* — Under concurrent eviction/reactivation
  pressure, at most one logical mutator exists. [MG-052]
- **AC-507** *Stuck step signal* — A step exceeding its stuck threshold emits a stuck
  lifecycle event and appears in queries. [MG-040]
- **AC-508** *Stuck instance signal* — A non-progressing non-terminal instance beyond
  threshold emits a stuck event and is queryable. [MG-040]
- **AC-509** *Lifecycle publication* — Significant transitions publish lifecycle events per
  documented durability guarantees. [MG-020/021]
- **AC-510** *Retry policy bounded and idempotent* — A retrying step honors its limit and
  produces no duplicate committed outcomes. [CR-006]
- **AC-511** *Concurrency limits honored* — Configured advancement/step limits and named
  pools bound concurrent execution without breaking per-instance serialization.
  [MG-060/061]
- **AC-512** *Pause stops advancement* — Given a running durable instance, when paused, it
  reaches `Paused` at the next safe commit boundary; an in-flight step's committed result is
  kept but no further advancement occurs. [MG-013]
- **AC-513** *Events during pause are buffered, never lost, never resume* — **[provider]**
  Given a paused instance, delivered events and timer firings are durably buffered, do not
  advance or un-pause the instance, and are not lost. [MG-013, EV-030, DU-030]
- **AC-514** *Resume replays buffered deliveries in order* — Given a paused instance with
  buffered deliveries, `Resume` (default Replay) re-enters execution and processes them in
  arrival order through standard matching, with deduplication still applying. [MG-013,
  EV-031]
- **AC-515** *Paused state survives restart* — **[provider]** Given a paused instance, after
  host stop/start it rehydrates as `Paused` and still requires an explicit `Resume`.
  [MG-013]
- **AC-516** *Destructive breadth safety* — Given a broad destructive selection
  (`All().Terminate()`, `Where(...).Purge()`), invocation without the explicit safety
  semantics (force/confirmation/preview per the API contract) is rejected; with them, the
  command proceeds and reports affected counts. [MG-004]
- **AC-517** *Resume with discard drops the pause-window buffer auditably* — Given a paused
  instance with buffered deliveries, `Resume` with Discard drops them without matching,
  records each discard durably, and the discarded `EventId`s can never match later.
  [MG-013, DU-030]
- **AC-518** *Durable pool capacity never exceeded* — **[provider]** Given a durable pool of
  capacity N and many instances across different definitions requesting tickets, at most N
  tickets are held at any moment — including across host restarts. [MG-062]
- **AC-519** *Acquisition-as-wait with FIFO grant* — Given an exhausted pool, a requesting
  instance suspends (cold-capable) and resumes when a ticket frees, in request order.
  [MG-062]
- **AC-520** *Symmetric ticket release on all terminal paths* — Success, failure, timeout,
  cancellation, and termination of the guarded scope each release its held tickets exactly
  once. [MG-062]
- **AC-521** *Expiry is audible, never silent* — An expired ticket is handled per pool
  policy and produces a lifecycle event and queryable state. [MG-064]
- **AC-522** *All-or-nothing multi-pool grant* — A scope requiring tickets from several
  pools never holds a partial set while waiting for the rest. [MG-063]

## Child workflows & fanout (AC-6xx)

Ephemeral `ForEach`:

- **AC-601** *Runtime batching* — Batch size 10 over 23 items creates exactly 3 work items.
  [CP-011, CP-030]
- **AC-602** *ForEach WhenAll* — Parent continues only after all work items complete.
  [CP-011]
- **AC-603** *ForEach bounded concurrency* — `maxConcurrency` limits active items. [CP-013]
- **AC-604** *WaitAllThenFail* — All items finish, then the parent fails. [CP-011]
- **AC-605** *WhenAny cancellation intent first* — Cancellation intent is recorded before the
  parent continues. [CP-012]

Durable `RunChildren`:

- **AC-606** *Child WhenAll join* — Parent continues only after all children complete.
  [CP-021]
- **AC-607** *Deterministic child ids across restart* — **[provider]** Restart duplicates no
  children. [CP-025]
- **AC-608** *Item snapshot stability* — Partitioning is preserved across restart. [CP-025,
  CP-030]
- **AC-609** *Durable throttling across restart* — **[provider]** `maxConcurrency` holds
  after restart. [CP-023]
- **AC-610** *Barrier fires exactly once* — Concurrent child completions trigger one parent
  resume. [CP-024]
- **AC-611** *Resume token reused* — **[provider]** Restart replays the recorded token; no
  second token is minted. [CP-024]
- **AC-612** *WhenAny residual recorded durably* — Residual cancellation intent is durable
  before parent resume. [CP-021, CP-024]
- **AC-613** *Unified outbox carries mixed kinds* — Child-start and external-message records
  coexist in one outbox. [DU-033]
- **AC-614** *Lineage queries* — `RootInstanceId`/`ParentInstanceId` navigation works across
  nested groups. [CP-020]
- **AC-615** *Child completion/failure propagation* — Configured join/failure policies govern
  parent behavior on child completion and failure. [CP-026]
- **AC-616** *Explicit child compensation* — `Compensate(group, spec)` spawns one
  compensation per completed child; cancellation never triggers it implicitly; repeats are
  idempotent. [CP-035, SG-011/012]
