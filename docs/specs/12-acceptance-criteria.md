# 12. Acceptance Criteria Catalog (AC)

Consolidated, numbered acceptance criteria. Each criterion is stated Given/When/Then and
tagged with the requirements it verifies. Groups: core (AC-0xx), events/waits (AC-1xx),
composition (AC-2xx), durable (AC-3xx), deferred-saga reservations (AC-4xx),
management/operations (AC-5xx), and fanout/DAG (AC-6xx). Criteria marked **[provider]** belong to the provider
certification suite (PR-024).

**Scenario criteria by reference:** the job-scheduler criteria (`JS-AC-001…016`) live in
[14-driving-scenario-eks-job-scheduler.md](14-driving-scenario-eks-job-scheduler.md) §14.4
next to the `JS-` requirements they verify, and are part of this catalog by reference —
phase gates (document 13) cite them alongside `AC-xxx`.

**Coverage classes** (see NF-012): (1) *direct* — a behavioral requirement is named in a
criterion's tag; (2) *certification* — provider invariants verified by the reusable
**[provider]** suite; (3) *structural* — contract shapes, pipeline structure, port
definitions, and API-surface rules are verified by unit tests in the implementation
program and exercised indirectly by many criteria here, without a dedicated AC each.

## Core runtime (AC-0xx)

- **AC-001** *Straight-line completion* — Given `Init → Step → End`, when started, the
  instance reaches `Completed` and business state reflects the step's writes. [CR-010,
  CR-020]
- **AC-002** *Conditional branch* — Given `If`, exactly one branch executes (then/else per
  condition) and flow continues after the block. [CR-010]
- **AC-003** *Root loop repeats until false* — Given root `While` true for 3 iterations, the body runs
  exactly 3 times and the workflow completes after the 4th check. [CR-010]
- **AC-004** *Step failure* — Given a failing/throwing step, the instance reaches `Failed`,
  later steps do not execute, and error details are inspectable. [CR-014]
- **AC-005** *Terminal instances never reopen* — Given `Completed`, `Failed`, `TimedOut`,
  `Cancelled`, or `Terminated`, cancellation request/termination returns the exact
  already-terminal outcome, direct durable event ingress returns
  `Rejected(DirectInstanceTerminal)`, and no retry/resume member exists. [CR-030, MG-010..013]
- **AC-006** *Serialized outcome under concurrency* — Given a waiting instance and two
  concurrent resume attempts, one valid sequential outcome results; no double continuation.
  [CR-040]
- **AC-007** *Racing branch completions serialize* — Given root-`Parallel` branches completing
  concurrently, private branch outcomes commit in a serial order, pure merge evaluation may
  repeat, and exactly one join/replacement-state commit wins. [CR-040, CP-002/005]
- **AC-008** *Builder validation accumulates* — Given a definition with several structural
  errors, build reports all of them together. [CR-002]
- **AC-009** *No live-instance leakage* — Given any public API result, mutating it does not
  affect engine state; typed state reads return copies and mismatched types fail clearly.
  [CR-021]
- **AC-010** *Completion blocked by unresolved runtime work* — Given outstanding
  runtime-owned waits/timers at an apparent terminal path, completion is rejected or the work
  is cancelled per explicit policy. [CR-032]
- **AC-011** *Notification-driven completion bridge* — Given a resultful workflow start/handle
  or DAG-run handle, `WaitForOutputAsync`/`WaitForTerminalAsync` subscribes then rechecks
  authoritative state, observes terminality at every registration race without polling or casts,
  and caller cancellation cancels only the local wait. Terminal workflow without output throws
  typed `WorkflowOutputUnavailableException`; all five workflow terminal statuses remain
  representable.
  [CR-016, CR-021, CR-030]
- **AC-012** *Typed output and fixed outcome commit together* — Given
  `End<TOutput>(selector, outcome)`, completion exposes the projected typed output and optional
  fixed `WorkflowOutcomeName` atomically. `End()` is resultless/unnamed; null/default/empty
  outcome values reject; no dynamic outcome-name selector compiles. [CR-008, DU-043]
- **AC-013** *Runtime-owned quantum yields fairly* — Given a runnable workflow that reaches
  its host segment budget, completed transitions remain committed, a successor continuation
  is left, the instance lane is released, and execution resumes without an author `Yield`
  member or duplicate committed transition. [CR-017]
- **AC-014** *Graceful cancel* — Given a running instance with an in-flight step and active
  waits, when cancelled, the step observes its `CancellationToken`, waits and timers move to
  `Cancelled`, definite cleanup or lease-quarantine transfer commits, and the instance reaches
  `Cancelled`. [CR-031, EV-044]
- **AC-015** *Forced terminate* — Given a running instance, when terminated, no further step
  execution occurs beyond the current commit boundary, runtime-owned waits/timers are
  forcibly resolved, no policies run, and the instance commits `Terminated`.
  [CR-031]

- **AC-016** *Mode-first authoring and shared compilation* - Ephemeral and durable factories
  expose only their positive capability allowlists; `Build()` and `TryBuild()` produce the
  same compiled plan or the same ordered diagnostics, and unsupported manually constructed
  nodes fail before registration. [CR-001/002/009, DU-002]
- **AC-017** *Staged signatures compile as approved* - Public compile fixtures use the exact
  staged `Init`/body/`End`, branch/item-private state, typed return, replacement-state merge,
  and typed DAG shapes in document 17 without compatibility overloads or deferred members.
  [CR-001/009, CP-001]
- **AC-018** *Package dependency graph is exact* - Architecture tests accept every declared
  tier edge, including Provider.Abstractions to Runtime.Protocol, and reject the reverse edge,
  application-to-advanced references, and provider-to-engine references. [PR-004]
- **AC-019** *Domain identities are not interchangeable primitives* - Public compile/signature
  fixtures require validated immutable `DefinitionId`, positive `DefinitionVersion`,
  `EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `ResourcePoolName`,
  `TransientPoolName`, `StartIdempotencyKey`, `DagNodeId`, `CorrelationId`, `EventId`,
  `StopConfirmationId`, `ResourcePoolOperationId`, and `ResourceGovernancePartitionId` through
  private constructors plus the sole public `Create(string)` factory, and runtime-created
  `InstanceId`, `WaitId`, `StepOperationId`, `LeaseProtectionToken`, and `DagRunId` through their
  approved parser/converter paths. Caller-created values expose no public constructor, `New`,
  `Parse`/`TryParse`, implicit conversion, raw-string overload, or construction alias; runtime
  identities expose no `Create`. Raw strings and cross-family values do not compile, and
  null/default or invalid constructed values fail before persistence. `DefinitionId.New()` is
  never empty, and canonical `Guid.Empty` text is rejected by `DefinitionId`, `InstanceId`,
  `WaitId`, and `DagRunId` parse/try-parse paths. [CR-004/008/009/022, EV-003, CP-001,
  DU-053/056, MG-060]
- **AC-020** *Step operation is stable and attempt is not identity* - Retrying, replaying, or
  replacing the durable host preserves one `StepOperationId`. `AttemptNumber` is a durable
  retry-policy ordinal: operation ID, ordinal, absolute attempt deadline, and in-flight dispatch
  marker commit before first dispatch; host-loss replay before a winning transition reuses them,
  while only a committed retry transition increments the ordinal. Another loop
  visit, branch, item, or continue-as-new generation receives a new operation ID. [CR-019,
  DU-056]
- **AC-021** *Lambda steps are mode-safe* - Ephemeral builders accept the approved sync/async
  lambda bodies; durable builders expose no lambda overload and accept named DI-created step
  types. [CR-013a]
- **AC-022** *Structural fingerprint and opaque-code versioning are honest* - Registering the
  same identity/version with changed node/member structure, strong values, referenced types,
  static request values, or codec format returns a typed fingerprint conflict. Changing only a
  selector/projector/merge/output body, step configuration, DAG mapping logic, or external-
  request construction does not pretend to change the fingerprint and is accepted only under a
  new `DefinitionVersion`; guards document this mandatory author action. [CR-004, DU-040/041]
- **AC-023** *Attempt-local state is isolated* - A step mutates its state copy, then fails or
  times out while ignoring cancellation. A retry starts from the last committed copy, may run
  while the old body still returns, and only its winning success can replace committed state;
  `ReplaceState` supports immutable/value state. [CR-011/018/043]
- **AC-024** *The v1 codec is fixed* - Registration accepts supported `orcacore-json-v1` type
  graphs, rejects unsupported cyclic/polymorphic graphs, and produces identical bytes for the
  same normalized graph/order across hosts. No serializer replacement hook exists. [PR-016]
- **AC-025** *Retry surface is exact* - `WithRetry(maxAttempts, fixedDelay?)` counts the initial
  policy attempt, accepts zero/non-negative fixed delay only, and exposes no predicate, backoff
  family, terminal callback, definition-wide retry, or failed-instance retry. Business failure,
  normalized exception, and step-attempt timeout consume attempts; cancellation/deadline,
  validation/codec/lease/runtime-invariant and commit-conflict paths do not. Duplicate/misplaced
  decorators fail at authoring. An uncertain host-loss replay reuses the current attempt and does
  not consume the budget, so `maxAttempts = 1` permits replay of attempt one but no policy retry.
  [CR-006]
- **AC-026** *Hosting roles are explicit* - Compile/startup fixtures expose only
  `AddOrcaCoreEphemeralEngine`, `AddOrcaCoreDurableEngine`, callback-only
  `AddOrcaCoreDurableEventIngress`, dev/test `AddOrcaCoreInMemoryDurableProvider`, production
  `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`, production
  `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`, and
  `OrcaCore.Dag.Hosting.AddOrcaCoreDag`. Durable-engine/callback roles own
  `IWorkflowEventIngress`; the durable hosting assembly owns the `IWorkflowEventDispatcher` port,
  the application registers its implementation, and the durable engine consumes it for authored
  `Publish`. Each production provider supplies one complete certified role. Conflicting roles,
  incomplete providers, DAG without durable engine, catch-all registration, individual port
  registration, and serializer replacement fail or are absent. [PR-040]

- **AC-027** *Public failure codes are stable* - Every public runtime exception derives from
  `OrcaCoreException`, exposes its authoritative nonblank fixed `Code`, and round-trips without
  using CLR type names or messages as protocol identity. Normalized arbitrary author/integration
  exceptions use the documented generic code, and result helpers preserve structured conflict/
  failure values. [CR-009, PR-024]
- **AC-028** *Authoring lifecycle freezes atomically and rejects invalid handles* - Given one
  authoring session, root fan-out moves it from `Open` to `JoinPending`, one join winner returns a
  successor `Open` epoch, and a root terminal freezes one immutable snapshot. Stale, superseded,
  escaped callback-local, duplicate-join, post-terminal, and losing concurrent handles reject with
  the catalogued lifecycle diagnostic before graph mutation; repeated build/validation preserves
  structure, diagnostics, and fingerprint. [CR-009a]
- **AC-029** *Failure provenance survives creation, propagation, aggregation, and codec* - Given
  root, branch, and item failures, each receives one immutable authored location and runtime-created
  occurrence at creation. One-failure propagation preserves the failure unchanged; a multi-cause
  join creates one owning failure while retaining each cause, authored branch order, dynamic item
  index order, and non-negative item indexes. `orcacore-json-v1` round-trips the closed
  `root`/`branch`/`item` graph and rejects unknown versions or discriminators, missing variant data,
  negative indexes, and malformed payloads rather than coercing them. [CR-014a]
  Normative companions: [`quality-and-verification` executable evidence](../../openspec/specs/quality-and-verification/spec.md#requirement-authoring-lifecycle-fingerprint-coverage-and-failure-provenance-are-executable),
  [`structured-fiber-execution` join ordering](../../openspec/specs/structured-fiber-execution/spec.md#requirement-join-policies-define-one-scope-outcome), and the
  [public-contract companion](17-selected-mode-capability-matrix.md).

## Events, waits, timers (AC-1xx)

- **AC-101** *Wait parks the requesting fiber* — Executing `Wait` creates one inspectable
  active wait and parks only its requesting fiber; status is `Waiting` only when no sibling is
  runnable. Durable registration is cold-evictable without another authoring member.
  [EV-021, EV-040]
- **AC-102** *Matching event resumes exactly once* — A matching event resumes from the wait
  point once, with the payload available to the next step. [EV-022, EV-023]
- **AC-103** *Non-matching event does not resume* — Wrong `EventName` or `CorrelationId`
  leaves the instance waiting. [EV-020]
- **AC-104** *Durable pre-wait event is retained* — **[provider]** Direct or correlation ingress
  accepted before its matching wait persists, survives host replacement, and is claimed by the
  later wait without broker redelivery. Ephemeral waits make no durable acknowledgement promise.
  [EV-030, DU-030]
- **AC-105** *Global event identity is stable* — The same `EventId` and normalized envelope is a
  duplicate before or after route/target progression; changed content is `EventConflict`. Fanout
  deduplicates each retained target independently. [EV-031]
- **AC-106** *Correlation routing resumes exactly one* — With multiple waiting instances on
  distinct correlations, a correlation route resumes only the unique match. [EV-010/011]
- **AC-107** *Ambiguous wait registration is rejected* — A second active registration for
  `(DefinitionId, EventName, CorrelationId)`, including inside one instance, throws
  `AmbiguousWaitRegistrationException` before parking or changing the index. [EV-011]
- **AC-108** *Definition fanout has stable membership* — **[provider]** First acceptance snapshots
  the complete current nonterminal persisted target set atomically; empty is valid, later instances
  are excluded, redelivery reuses the set, and every target progresses or poisons independently.
  [EV-010/030/031]
- **AC-109** *Loop waits are a signal stream* — Each `While` iteration gets a fresh `WaitId`;
  after one event consumes the active wait, a later iteration may reuse the pair and consume a
  later event. Occurrence-specific authors encode the occurrence in `CorrelationId`. [EV-043]
- **AC-110** *Parallel waits isolated by branch* — With different waits in branches of one root
  `Parallel`, one matching event resumes only its branch. [EV-021, CP-001]
- **AC-111** *Timer completes after due time* — A delay/timer step continues the workflow
  exactly once after its due time. [EV-050]
- **AC-112** *Timer/event race deterministic* — Waiting on event + timeout simultaneously,
  exactly one runtime-owned obligation wins and the loser is cancelled. A timeout winner fails
  the current root/branch/item with `WorkflowWaitTimeoutException`; `WhenAllOutcomes` exposes a
  nested failure as data and no timeout callback runs. [EV-051]
- **AC-113** *Attempt and workflow deadlines differ* — Exceeding `WithStepTimeout` fences one
  attempt, discards/fences its copy with `StepAttemptTimeoutException`, and a retry receives a
  new attempt deadline/number while keeping `StepOperationId`; `CompleteWithin` remains anchored
  at workflow start across admission, waits, retries, restart, and `ContinueAsNew`, then commits
  terminal `TimedOut` with `WorkflowDeadlineExceededException` and suppresses merges. [CR-018,
  EV-052, MG-041] Host-loss replay before the attempt-timeout winner commits reuses the persisted
  attempt deadline/number and never resets either bound.
- **AC-114** *No accepted-event loss around apply commit* — **[provider]** A crash before the
  consuming transition leaves the wait `Active` and the accepted record re-matchable; a crash
  after commit cannot double-apply it. Source acknowledgement after `Accepted`/`Duplicate` does
  not lose the event.
  [EV-032, DU-020/030]
- **AC-115** *Internal keyed routing has no public bulk promise* — Routing uses runtime-owned exact
  keys without an application-visible collection; public instance enumeration, multi-ID/filter
  retrieval, count/statistics, and bulk mutation are absent/deferred. [EV-013, MG-001/004]
- **AC-116** *Closed acceptance and portable dynamic wait* — Durable ingress returns only
  `Accepted`, `Duplicate`, or `Rejected` with `EventConflict`, `DirectInstanceNotFound`,
  `DirectInstanceTerminal`, `StartConflict`, or `FanoutLimitExceeded`. Invalid arguments remain
  exceptions. Structural `Wait` and dynamic `StepResult.WaitForEvent` both create one correctly
  owned obligation in each mode. [EV-012, EV-030/031, EV-045]
- **AC-117** *EventName equality is provider-independent* - `EventName.Create("Approval")` and
  `EventName.Create("approval")` are distinct, leading/trailing whitespace is rejected rather
  than trimmed, scalar round-trip preserves the exact value, and correlation routing returns the
  same result through every certified provider regardless of its default collation.
  [EV-003/020, PR-024]
- **AC-118** *Route union is exact* — Reflection and fresh-package consumers expose exactly direct,
  correlation, definition-fanout, and start-or-deliver routes. Start-or-deliver keeps fixed-codec
  workflow input and `StartIdempotencyKey` distinct from the event payload and rejects
  incompatible existing bindings before ownership. [EV-010]
- **AC-119** *Durable Publish is transactional and isolated* — A durable authored `Publish` commits
  one fixed-codec workflow event atomically with workflow state, retries with the same event
  identity, and reaches only `IWorkflowEventDispatcher`; internal continuation records never do.
  [EV-060, DU-031/033]
- **AC-120** *Acceptance defines broker acknowledgement* — Only `Accepted` or `Duplicate` permits
  the source to acknowledge. Rejections retain no false ownership, while unresolvable accepted
  records become observable poison rather than disappearing. [EV-012/030/032]

## Composition (AC-2xx)

- **AC-201** *Join commit exactly once* — Root `Parallel` + `WhenAll`: the pure merge may be
  reevaluated, but replacement state and continuation commit exactly once after all branches
  succeed. [CP-002/005]
- **AC-202** *Order-insensitive outcome* — Branches completing in different orders across
  runs yield the same committed outcome. [CP-003]
- **AC-203** *Graph-shape insensitivity* — Adding a structurally irrelevant no-op step does
  not change join/continuation behavior. [CP-003]
- **AC-204** *WhenAllOutcomes exposes every branch result* — Closed success/failure outcomes
  arrive in authored order; no cancelled variant exists. The pure merge stores a summary and a
  following `If` can decide business acceptance. [CP-003]
- **AC-205** *No automatic sibling cancellation* — A failing branch does not cancel a sibling
  under either v1 join. `WhenAll` waits for all success/failure and fails without merge;
  `WhenAllOutcomes` waits for all and commits one merged replacement. Ancestor cancellation,
  termination, or deadline suppresses both merges rather than creating cancelled branch
  outcomes. [CP-002/003/004]
- **AC-206** *Nested composition allowlist* — Nested `If` compiles in every approved nested
  body. `Parallel`, `While`, and `ForEach` compile only in the root sequence; they are absent
  from nested, branch, item, and leased builders and fail validation on a hand-built nested
  graph. [CP-032]

## Durable execution (AC-3xx)

- **AC-301** *Durable wait survives restart* — **[provider]** A workflow suspended in a
  durable wait remains resumable after host stop/start. [DU-013, EV-040]
- **AC-302** *Rehydration restores committed state only* — **[provider]** After a crash
  mid-execution, rehydration restores the last committed state; no partial transition
  observed. [DU-020]
- **AC-303** *Removed wait/yield names are absent* — Public assemblies, builders, reflection
  guards, and compile fixtures contain no `WaitLong` or author `Yield` alias, obsolete
  tombstone, or placeholder. [CR-017, EV-040]
- **AC-304** *Every durable wait is cold-capable* — After ordinary `Wait` registration
  commits, the activation may be evicted; a later matching event rehydrates and resumes it.
  Keeping it resident is behaviorally equivalent. [EV-040, MG-050]
- **AC-305** *Restart-safe dedup* — **[provider]** Duplicate events delivered before and
  after restart produce no duplicate committed outcomes. [DU-030]
- **AC-306** *Version and structural-fingerprint binding* — An instance started under version
  N remains bound to N, fixed codec format, and its structural fingerprint as newer versions
  deploy. [DU-040]
- **AC-307** *Same-version structural drift fails explicitly* — **[provider]** Registering the
  same identity/version with another structural fingerprint returns a typed conflict. Opaque
  code changes are exercised under a new version because they are intentionally not hashed.
  [DU-041]
- **AC-308** *Durable inspection without payload* — Operators query instances by status,
  definition, version, and wait state from durable metadata only. [DU-070]
- **AC-309** *Concurrent durable resume serializes* — **[provider]** Racing resume attempts
  on a durable instance produce exactly one committed outcome. [DU-022]
- **AC-310** *Outbox dispatches after commit and preserves kind isolation* — **[provider]** No
  continuation, lifecycle, child intent, or workflow-authored public event dispatches unless its
  outbox record committed with the state transition. `IWorkflowEventDispatcher` receives only
  public workflow events; internal pumps never receive them. Failures preserve the documented
  at-least-once identity and poison behavior. [DU-031/032/033]
- **AC-311** *Idempotent start* — `StartOrGetAsync` with a repeated `StartIdempotencyKey` returns
  the existing instance; case variants are distinct, provider restart preserves the binding,
  and the key cannot be passed as an `InstanceId`. Reuse against a different definition,
  version, structural fingerprint, or fixed-codec `PayloadFingerprint` returns stable
  `StartIdempotencyConflict` rather than an instance from the wrong typed handle. Exact scalar
  round-trip and provider-independent equality hold within the configured serialized-envelope
  limits. [DU-053, PR-024]
- **AC-312** *History pressure observable* — Growing stream/checkpoint/outbox volume is
  detectable through supported statistics. [DU-052, MG-031]
- **AC-313** *Continue-as-new preserves the right continuity* — Root rollover commits a fresh
  generation/replacement state under the same instance/definition/version/fingerprint and
  original workflow deadline only after every owned obligation and lexical lease scope exits.
  [DU-042]
- **AC-314** *Retention-safe cleanup; public purge absent* — **[provider]** Provider cleanup
  never removes active instances, in-flight handling, lease obligations, or required tombstones;
  public `Archive`/`Purge` members and fixtures are absent. [DU-051, PR-022]
- **AC-315** *Multi-node single mutator* — **[provider, advanced]** With multiple hosts able
  to process one instance, only one committed execution path succeeds at a time. [DU-060]
- **AC-316** *No critical persistence in shutdown hooks* — A host killed before deactivation
  hooks run loses nothing: transitions were committed at safe boundaries. [DU-021]

- **AC-317** *In-memory durable hosting is honest* - Selecting the in-memory durable host
  outside explicit development/test use emits the required diagnostic, and no restart-
  durability sample or claim is published for it. [DU-001]
- **AC-318** *Registration is explicit and inferable* - `IWorkflowDefinitionRegistry` returns
  the correct typed definition handle; `StartOrGetAsync` compiles without a phantom state
  generic, and no start-by-raw-identity overload can register implicitly. [DU-054]
- **AC-319** *Split-host event progresses exactly once* - A definition-less callback host
  returns `WorkflowEventAcceptanceResult.Accepted` only after inbox ownership commits; the
  definition-owning host later progresses exactly once. The result does not expose whether
  progression happened inline. [DU-030/031, DU-055]
- **AC-320** *External report is event-idempotent* - A watcher reuses one `EventId` for retry
  or redelivery of the same logical report; the event commits once and a duplicate returns the
  stable duplicate outcome without resuming twice. [DU-030/056]
- **AC-321** *Create-or-observe identity survives retry and split hosts* - A bounded durable
  step retry/replay presents the same `StepOperationId` to an application adapter; a later
  loop/item/branch/generation occurrence receives another ID. No public external-job facade or
  job-specific identity is required. [CR-019, DU-055/056]

## Deferred saga scenario reservations (AC-4xx)

These scenarios preserve future compensation intent only. They are excluded from v1 gates and
must not have positive public-surface fixtures until document 07's amendment gate is approved.

- **AC-401** *Success without compensation* — All forward steps succeed → saga `Completed`;
  no compensation runs. [SG-013]
- **AC-402** *Failure triggers compensation* — A failing step after completed forward steps
  starts compensation for eligible steps. [SG-011]
- **AC-403** *Deterministic compensation order* — Compensations run in reverse
  committed sequence order for linear actions and reverse canonical authored order for
  sibling-fiber actions, or a deterministic plan-bound per-scope override. Equivalent
  completion interleavings produce the same order. [SG-010]
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

- **AC-501** *Snapshot is complete and immutable* — A handle snapshot exposes the exact
  definition binding, status, timestamps, outcome/failure, and ordered active-wait snapshots;
  wait snapshots own their optional structural deadline, and terminal workflow timeout appears
  through status/failure. It intentionally omits the absolute workflow deadline and active
  step-attempt ordinal/deadline/outcome, which remain telemetry/runtime facts; lease quarantine is
  queried separately through trusted diagnostics. Constructing or mutating a copy grants no
  authority. [MG-001/002/005]
- **AC-502** *Root-state query is honest* — `GetStateAsync<TState>` returns a fixed-codec-
  detached copy of only the last committed root state, rejects type mismatch, and never exposes
  private branch/item or in-flight attempt state. [MG-003]
- **AC-503** *Typed output variants are closed* — Resultful handles return `Pending`,
  `Available`, or `Unavailable`; resultless handles have no output member. [MG-003]
- **AC-504** *Cancellation request is idempotent* — A live instance returns `Requested`, a
  repeat returns `AlreadyRequested`, and a terminal instance returns `AlreadyTerminal`; the
  public status passes through `CancellationRequested` when work remains. [MG-010]
- **AC-505** *Termination is idempotent and fenced* — Termination returns `Terminated` once and
  `AlreadyTerminal` thereafter, prevents progression, and quarantines ambiguous protected work
  rather than claiming it stopped. [MG-010, MG-064]
- **AC-506** *Eviction never duplicates mutators* — Under concurrent eviction/reactivation
  pressure, at most one logical mutator exists. [MG-052]
- **AC-507** *Stuck step signal* — A step exceeding its host threshold emits a stuck lifecycle
  event and appears in host/operator projections. [MG-040]
- **AC-508** *Stuck instance signal* — A non-progressing non-terminal instance beyond its host
  threshold emits a stuck event and is queryable operationally. [MG-040]
- **AC-509** *Lifecycle recording* — Significant transitions record lifecycle events per the
  documented durability guarantees. [MG-020/021]
- **AC-510** *Retry policy bounded and isolated* — `maxAttempts` includes the initial attempt;
  it counts durable policy attempts rather than physical crash-replay invocations. Optional fixed
  delay is honored, failed/timed-out copies are discarded, the in-flight marker commits before
  first dispatch, uncertain host-loss replay reuses the same attempt coordinate/deadline, and no
  duplicate commit occurs. [CR-006/011]
- **AC-511** *Concurrency limits honored* — Path tokens, step throttles, ephemeral transient
  pools, DAG node limits, and durable leases enforce their distinct lifetimes without breaking
  per-instance serialization. [MG-060/061]

The following IDs are explicit **deferred-surface reservations** and are excluded from v1 gates:

- **AC-512** `Pause`/`Paused` absence and future admission semantics. [MG-013]
- **AC-513** Future pause-window event/timer buffering semantics. [MG-013]
- **AC-514** Future `Resume` replay semantics. [MG-013]
- **AC-515** Future paused-state restart semantics. [MG-013]
- **AC-516** Future bulk-selection/destructive breadth safety. [MG-004]
- **AC-517** Future resume-with-discard semantics. [MG-013]

No v1 positive fixture may reference those members/statuses; absence guards are the only
first-release evidence for AC-512…517.
- **AC-518** *Durable pool admission and accounting are exact* — **[provider]** Given a
  durable pool of capacity N and many instances across definitions, every grant proves that
  reserved units (`PendingCommit`, `Held`, `ReviewMarked`, `AmbiguousHeld`, or `Quarantined`)
  plus that request's units for the pool do not exceed N. Queued/released/cancelled requests
  reserve zero.
  A downward resize below existing reservations records debt as
  `max(0, reserved - configured)`, revokes nothing, and permits no new grant until exact release
  or upward resize clears the debt and the next request fits — including across restart,
  grant/cancel races, and reconciliation through one partition-wide expected-version aggregate.
  Pool-definition `Capacity` remains the immutable creation value: after resizing N to M, a host
  restarted with the original N definition succeeds without resetting replayed current capacity M
  or debt, while presenting a changed creation value fails startup even when it equals M.
  [MG-062/065, PR-017]
- **AC-519** *Acquisition-as-wait with FIFO grant* — Given an exhausted pool, a requesting
  fiber parks (cold-capable) and resumes when a ticket frees, in request order, while an
  unrelated runnable sibling may continue.
  [MG-062]
- **AC-520** *Scoped exact-owner release and forced-stop quarantine* — Normal lexical-body
  completion and causally definite failure/cancellation release the exact lease obligation and
  ticket/unit set once before branch/item return, merge, or parent resume. Retryable timeout,
  ambiguous submit, or recoverable host loss first retains same operation/token/tickets/capacity
  as `AmbiguousHeld`; no next in-process leased retry overlaps a still-running prior body, host-
  loss retry may proceed, and success alone does not erase ambiguity. Ambiguous scope exit,
  exhaustion, cancellation, deadline, forced termination, or abandonment transfers to a
  capacity-reserving `Quarantined` state before progression; terminal status alone never frees it.
  Confirmed stop or an end-to-end protected-resource fence then permits exact release once. An isolated
  no-waiter/no-resize case restores numeric pre-acquisition capacity; a contended case may
  transfer freed units directly to the next waiter while preserving conservation. [MG-062/065]
- **AC-521** *Review marks trigger reconciliation; they are not a TTL* — **[provider]** Reaching a
  pool-owned review deadline records one auditable/queryable `ReviewMarked` fact while capacity remains
  held. An exact live or reconstructable owner remains held without renewal; a causally proven
  released/never-committed owner, or terminal owner with committed cleanup plus confirmed stop
  or end-to-end fencing, is recovered exactly once; bare terminal/`AmbiguousHeld` state is reserved
  for operator action; and a missing ticket/fence produces `LeaseLost` rather than silent
  reacquisition. [MG-064]
- **AC-522** *All-or-nothing multi-pool grant* — A fiber occurrence requesting tickets from
  several pools never holds a partial set while waiting for the rest. [MG-063]

- **AC-523** *Internal remediation stays outside application handles* - Malformed continuation/
  runtime-state faults never guess position or hot-loop; application handles expose no stream
  version, `Rearm`, `Resume`, or management `Retry`, and committed timestamps come from the
  runtime clock. [MG-014, MG-032]
- **AC-524** *Concurrency lifetimes and modes remain distinct* - Compile/API fixtures distinguish
  per-step throttle, ephemeral-only host-local transient pool, per-instance path token, DAG-node
  ceiling, and persisted durable lease. A blocked fiber
  releases the instance turn; every lexical acquisition-scope exit performs exact release or
  transfers forced-stop cleanup to a capacity-reserving quarantine obligation, while expiry
  only marks and initiates exact owner reconciliation. Forced termination cannot release a
  transient slot while its actual guarded body still runs. [MG-060/061/062/064]
- **AC-525** *Durable acquisition ancestry is deadlock-safe* - Path-sensitive compiler tests
  allow mutually exclusive `If` scopes, sequential same-fiber scopes, a fully lexical root-loop
  scope per iteration, independent root-`Parallel` branches/root-`ForEach` items, and a parent
  acquisition after child release; leased builders expose no `Parallel`, `ForEach`, or `While`;
  reject acquisition inside an active ancestor lease and `ContinueAsNew` before scope exit.
  Runtime tests reject the same violations before pool mutation for manual/stale plans.
  [MG-063]
- **AC-526** *Dynamic lease selection is replay-stable* - A null request, invalid requirement,
  or duplicate pool fails before provider mutation. `ResourceLeaseRequest.Create` cannot be
  empty, copies its inputs, and rejects null items. A valid normalized selection commits before
  acquisition and is reused after restart; the selector may retry only before that commit. A
  static or dynamic request naming any unconfigured durable pool fails with
  `ResourcePoolNotConfiguredException` / `WF-RESOURCE-POOL-NOT-CONFIGURED`, reports the complete
  missing-name set, and creates no queue entry, ticket, operation, or provider mutation.
  [MG-062]
- **AC-527** *Grant versus cancellation has one outcome* - **[provider]** A race among grant,
  owner cancellation/failure/forced-terminal cleanup, and host restart leaves one active exact
  obligation, one exact release, or one exact capacity-reserving quarantine, never a ghost
  ticket, partial request, or lost capacity. Cancellation winning while `Queued` commits
  cancelled-before-grant with zero units; reservation winning first retains `PendingCommit`
  tickets, then cancellation before activation compensates them exactly, while cancellation after
  activation uses proven release or quarantine. Delayed loser commands are stale idempotent
  no-ops. [MG-064/065]
- **AC-528** *Host ceilings cannot be raised by authors* - Fixed root-parallel branches admit in
  authored order under `MaxConcurrentExecutionPathsPerInstance`; root `ForEach` uses the lower
  of host ceiling and optional node cap; DAG `MaxConcurrentNodes` remains separate and counts a
  started nonterminal child even while that child is parked in a workflow wait. [MG-060]
- **AC-529** *Trusted confirmation matrix is exact* - Live lexical ownership returns
  `NotConfirmable` for `PendingCommit`, `Held`, `ReviewMarked`, or `AmbiguousHeld`; first valid
  quarantine confirmation returns `Released`; retry returns `AlreadyConfirmed`; and normally
  released/unknown/purged tokens return `TokenNotFound`. Precedence is total: a confirmation ID
  bound to another token returns `ConfirmationConflict` before target-token state is evaluated;
  an ID already accepted for the same token or a token released by another accepted confirmation
  returns `AlreadyConfirmed` before live/unknown state evaluation. Normal-release/
  confirmation races serialize, tombstones survive the dedup window, and no successor token is
  released. [MG-064]
- **AC-530** *Resource-governance aggregate is atomic* - **[provider]** One configured partition
  serializes all pools, atomic requests, FIFO queueing, tickets, reviews, resize operations,
  confirmations, and tombstones. The four friend-only post-commit barriers deterministically stop
  after workflow pending obligation, governance reservation, workflow activation, and governance
  ownership confirmation with equal immutable owner/ticket facts; recovery leaves all requested
  pools reserved or none granted, never a partial/ghost/double grant. Full-stream factory and
  append-batch guards reject null/gap/duplicate/reordered/version-mismatched or partial records.
  [MG-065, PR-017/024]
- **AC-531** *Pool management has no force release* - `ListAsync` and `GetAsync` return exact
  configured/reserved/debt/queue/review snapshots. `ResizeAsync` returns closed `Applied` for the
  first mutation and exact replay, `Conflict` with recorded/attempted facts for changed intent,
  mutates nothing on conflict, and public/advanced reflection contains no force-release member.
  Unknown `GetAsync`/`ResizeAsync` pool names fail with
  `ResourcePoolNotConfiguredException` / `WF-RESOURCE-POOL-NOT-CONFIGURED`; resize capacity must be
  positive and zero/negative throws `ArgumentOutOfRangeException`. Validation precedes operation-ID
  binding, append, grant, or mutation.
  [MG-064]
- **AC-532** *Outstanding lease diagnostics are discoverable* - Trusted
  `IDurableResourceLeaseDiagnostics` enumerates every queued/reserved/reconciliation-required or
  `LeaseLost` obligation, including crash-before-label cases, and token lookup correlates immutable
  workflow/scope/status/confirmation/pool/unit/review/provider-generation facts. Ordinary
  workflow handles expose none of these facts, and remote adapters own authorization/redaction.
  [MG-001/064]

## Bounded fanout and typed DAG (AC-6xx)

- **AC-601** *Finite item snapshot* — A pure root `ForEach` selector may be evaluated more than
  once before one fixed-codec snapshot commit, produces stable index-ordered descriptors, and
  exceeding positive `MaxItems` fails before partial admission. Durable restart reuses the
  committed snapshot; an empty snapshot produces one merge commit from an empty list even if
  pure evaluation repeats. [CP-011/014]
- **AC-602** *ForEach WhenAll* — Parent continues only after every item is terminal; if all
  succeed, one ordered result merge commits; if any fails, the merge is skipped and the scope
  fails after all success/failure outcomes, aggregating multiple causes by item index. [CP-012]
- **AC-603** *ForEach bounded admission* — Optional node `MaxConcurrency` composes as the lower
  value with the host execution-path ceiling and remains enforced after durable restart. Only
  one unfenced attempt owns instance commit authority; a token-ignoring fenced body may overlap
  physically without committing. [CP-013/014]
- **AC-604** *ForEach WhenAllOutcomes* — Ordered closed success/failure outcomes produce one
  replacement-state commit; pure merge evaluation may repeat, a following `If` makes the
  business decision, and no sibling item is cancelled merely for failure. Ancestor cancellation/
  deadline suppresses merge. [CP-012]
- **AC-605** *Nested fanout rejected* — Root `Parallel` and root `ForEach` compile in both
  modes; nested `Parallel`/`ForEach` are absent from nested, branch, item, and leased builders,
  and a hand-built nested occurrence fails validation.
  [CP-010/032]
- **AC-606** *Typed DAG diamond maps direct outputs* — Given `A -> {B,C} -> D`, every node uses
  one typed durable workflow reference; D starts once only after B/C successful outputs commit,
  and its pure projector may reevaluate before one fixed-codec mapped-input commit while seeing
  only run input plus declared direct outputs, never private child state.
  [CP-021/022/026]
- **AC-607** *DAG structural build and opaque mapping failures are distinct* — `Build` returns
  accumulated diagnostics for cycles, duplicate node/edges, self/foreign references, and missing/
  duplicate `MapInput`. Typed APIs make wrong/resultless output use compile-impossible. An
  undeclared/non-direct/foreign output access hidden in mapper code fails during mapping as
  `DAG_INPUT_MAPPING_INVALID` before mapped-input commit or child start; dependants block while
  independent nodes remain valid and continue. [CP-021/022]
- **AC-608** *DAG node identity survives restart* — **[provider]** One node occurrence creates
  at most one child instance; mapped input, lineage, ready/blocked state, and successful output
  handoff survive host replacement. [CP-022/023/024]
- **AC-609** *DAG node ceiling survives restart* — **[provider]** `MaxConcurrentNodes` bounds
  admitted child instances independently from workflow fiber and resource limits before and
  after restart; every started nonterminal child, including one parked in a workflow wait,
  continues to count until terminal. [CP-025]
- **AC-610** *Concurrent dependency completion starts once* — Racing child completions commit
  one ready/start transition for their dependent; caller code never pumps ready/completed sets.
  [CP-024]
- **AC-611** *DAG failure closure is stable* — Child failed/timed-out/terminated/cancelled-
  outside-DAG-cancel maps to stable node `Failed` codes; transitive nodes become
  `DependencyBlocked`, independent nodes continue, and a non-cancel-request run becomes
  `Failed`. [CP-024]
- **AC-612** *No public child orchestration leak* — DAG execution works through internal
  child-start/join records while public assemblies and positive compile fixtures contain no
  `RunChild` or `RunChildren` member. [CP-023]
- **AC-613** *Unified outbox carries internal DAG starts* — Internal child-start and other
  runtime-owned records coexist in one outbox without exposing child start or workflow
  publication as authoring API.
  [DU-033, CP-023]
- **AC-614** *DAG lineage queries* — `RootInstanceId`/`ParentInstanceId` navigation links a DAG
  run to node child instances without exposing their private business state. [CP-023/024]

AC-613 and AC-614 remain mandatory first-release criteria. Phase 0 guards preserve the public
boundary and required ownership/absence rules; executable unified-outbox and lineage integration
verification may turn green in later durable/DAG/provider implementation slices, but it MUST pass
before first release. This schedules the executable verification and does not defer or weaken
either criterion.

- **AC-615** *Resultless DAG node composes* — A resultless durable node produces non-generic
  `DagNodeRef`, can satisfy `DependsOn`, exposes no output, and cannot be passed to `OutputOf`.
  [CP-021/026]
- **AC-616** *DAG cancellation is complete* — Request prevents new admission, marks pending/
  ready nodes `Cancelled`, running nodes/children `CancellationRequested`, preserves
  `DependencyBlocked`, and commits run `Cancelled` only after running children terminate.
  [CP-028]
- **AC-617** *DAG snapshots are complete and ordered* — Every terminal snapshot contains every
  node in authored ordinal with dependency, child, input fingerprint, output availability,
  timestamps, and failure fields; ready ties use the same ordinal. [CP-027]
- **AC-618** *DAG terminal wait is reactive* — `DagRunHandle.WaitForTerminalAsync` observes a
  terminal commit before/during/after notification registration through subscribe/recheck,
  returns one authoritative terminal snapshot without polling, and caller cancellation cancels
  only the local wait rather than the DAG or children. [CR-016, MG-005]
