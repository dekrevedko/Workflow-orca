# 02. Engineering Conventions

Read this before every task. Deviations require an explicit note in the task's PROGRESS line.

## 1. Language & compiler

- .NET 10, `LangVersion` latest (C# 14), `Nullable` enable, `TreatWarningsAsErrors` true,
  `AnalysisLevel` latest, `ImplicitUsings` enable — set once in `Directory.Build.props`.
- Use freely: file-scoped namespaces, records (`record` / `readonly record struct`),
  primary constructors, `required` members, collection expressions `[..]`, pattern matching
  (`switch` expressions with exhaustive handling), `init` setters, `field` keyword where it
  simplifies, `params` collections, target-typed `new`.
- Avoid: unsafe code, reflection (except the single explicit spot in serializer wiring),
  dynamic, preprocessor directives, partial classes (except source-generator targets).

## 2. Types and API design

- **Behavior seams use interfaces**: replaceable services/ports a consumer or host implements
  are interfaces in the owning public contract tier; module-local seams are internal.
  Immutable values, staged builders, definitions, outcomes, and exceptions are concrete public
  contracts when the approved matrix calls for them. Other concrete collaborators are
  `internal sealed`; constructors take interfaces rather than constructing dependencies.
- **Step authoring stays explicit**: the portable surface offers `Then<TStep>()`; host DI
  creates `TStep` and supplies its dependencies. Ephemeral builders additionally offer the
  approved synchronous and asynchronous lambda bodies. Durable builders do not accept
  delegates, captures, step instances, service-provider callbacks, or reflection-based
  constructor arguments because persisted definitions need stable type and fingerprint
  identity.
- **Closed hierarchies for results/events**: `abstract record` base + `sealed record`
  variants (for v1, `StepResult.Completed/Failed/WaitForEvent`). The consumer `switch`
  must be exhaustive — add a `_ => throw new UnreachableException()` arm only where the
  compiler cannot prove exhaustiveness.
- **Attempt state is detached**: contracts, snapshots, envelopes, definitions, and returned
  collections are immutable/detached. Each step attempt receives a codec-detached copy of the
  last committed `TState` through `StepContext<TState>.State`; it may mutate that copy or call
  `ReplaceState`. Only the successful winning attempt commits its final copy. Failed, timed-out,
  cancelled, or fenced copies are discarded, and a retry starts from the same committed state.
- **One fixed workflow-state codec**: v1 uses certified `System.Text.Json` format
  `orcacore-json-v1`; it is not host/provider replaceable. Registration rejects unsupported,
  cyclic, or unsafe polymorphic shapes and certifies deterministic bytes plus detached round
  trips. Persisted/provider envelopes preserve those bytes and format identity.
- **Functional primitives** (spec PR-050): `Result<T>` for expected operational outcomes
  (routing, command decisions, append outcomes), `Option<T>` for absence-without-failure
  (lookups), `Validation<T>` for accumulated build-time errors. Public happy-path APIs may
  throw `OrcaCore*Exception` types instead — never force `Result` chains on end users.
  Never nest `Task<Result<Option<T>>>`; split the method.
- **Strong values follow construction ownership**: caller-created string-backed values
  (`EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `DagNodeId`, `ResourcePoolName`,
  `TransientPoolName`, `StartIdempotencyKey`, `CorrelationId`, `EventId`, `StopConfirmationId`,
  `ResourcePoolOperationId`, `ResourceGovernancePartitionId`) are immutable reference values with
  private constructors and one public `Create(string)` factory. Runtime-created `InstanceId`,
  `WaitId`, `StepOperationId`, `LeaseProtectionToken`, and `DagRunId` instead expose canonical
  `Parse`/`TryParse` paths and no public `Create`. Do not add public constructors, primitive or
  implicit-conversion overloads, or construction aliases beside either family.
- **Definitions are staged and fingerprint-bound**: `Init` is required before body authoring;
  `End` commits typed output and optional fixed outcome metadata before `Build`/`TryBuild`
  becomes available. Reusing `(DefinitionId, DefinitionVersion)` with different authored
  inspectable structure, static values, closed types, inspectable selector placement/type metadata,
  or codec format is a typed conflict, never a silent replacement. The fingerprint does not claim
  to hash selector/delegate IL, captured values, DI behavior, or external code; changing any opaque
  behavior requires a new `DefinitionVersion` and does not itself create a fingerprint conflict.
- **Join outcomes are deliberate**: `WhenAll` merges only all-success results.
  `WhenAllOutcomes` merges ordered typed success/failure outcomes so a following `If` can decide
  business acceptance. Neither auto-cancels siblings. Ancestor instance cancellation,
  termination, or `CompleteWithin` suppresses both joins and their merge. An empty finite
  `ForEach` snapshot is valid and invokes its merge once with an empty ordered list.
- **Structured resources are lexical**: durable resources are acquired only through scoped
  `AcquireResources(request, body)`. No point/fiber-lifetime form, author TTL, renewal,
  caller-supplied holder, or empty `params` overload is permitted. A nested acquisition is
  impossible while an ancestor acquisition is pending or held; concurrently needed pools are
  requested atomically.
- **Greenfield means one surface**: removed/deferred members have no alias, obsolete tombstone,
  public placeholder, or compatibility adapter. `WaitLong` and author `Yield` are removed;
  `WhenFirst`, Saga, public external-job/child members, nested `Parallel`/`While`/`ForEach`,
  durable lambdas, and definition-wide retry remain documented future work only.
- **Optional packages point inward**: `OrcaCore.Dag` depends on OrcaCore application contracts;
  `OrcaCore.Dag.Hosting` is the sole bridge to the named/versioned internal child-start/join
  seam in `OrcaCore.Durable.Hosting`. Kubernetes, AWS, scheduler, and job-system projects may
depend outward on `OrcaCore.Dag.Hosting`, but no OrcaCore package depends on or
  expose their SDK types. `OrcaCore.Runtime.Protocol` and
  `OrcaCore.Provider.Abstractions` are advanced provider tiers, not application references.
- **Event identity is target-scoped after acceptance**: delivery before the target wait is active
  returns non-consuming `NoActiveWait`, writes no mailbox/inbox/dedup state, and permits the same
  `EventId` to be redelivered as its first accepted event after wait registration. Once accepted,
  durable inbox dedup is per target `InstanceId` by `EventId`; identical normalized content is a
  duplicate and changed content is a conflict. Correlation routing selects exactly one active wait by
  `(DefinitionId, EventName, CorrelationId)` and rejects a second active registration before
  parking. Definition-targeted fanout is deferred.
- **Execution-path capacity has one token model**: a runnable root/branch/item owns one host
  token and releases it on wait, delay, resource request, or join. A parent releases before
  fan-out admission and reacquires only for merge/continuation. `ForEach.MaxConcurrency`
  separately counts admitted nonterminal item scopes, including parked items. A timed-out
  token-ignoring body loses commit authority/logical token but retains each physical
  step-throttle/transient slot until return.
- **Hosting roles and owners are explicit**: `OrcaCore.Engine.Ephemeral` owns
  `AddOrcaCoreEphemeralEngine`; `OrcaCore.Durable.Hosting` owns `AddOrcaCoreDurableEngine` and
  callback-only `AddOrcaCoreDurableEventIngress`; `OrcaCore.Providers.InMemory` owns development/test
  `AddOrcaCoreInMemoryDurableProvider`; `OrcaCore.Providers.PostgreSql` owns production
  `AddOrcaCorePostgreSqlDurableProvider`; and `OrcaCore.Dag.Hosting` owns `AddOrcaCoreDag`. Each
  accepts only its approved role-specific options. Hosts construct those options programmatically;
  registration copies and validates them, with no binder-oriented facade. Do not add a catch-all
  `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, or codec replacement hook.

## 3. Async rules

- All potentially-working public APIs: `async`, suffix `Async`, take `CancellationToken`
  (last parameter, no default in internal code; `default` allowed on public facade).
- `ValueTask` only on measured hot paths; otherwise `Task`.
- No `async void`; no `.Result`/`.Wait()`/`GetAwaiter().GetResult()`; no `Task.Run` in
  library code (the host owns threads); `ConfigureAwait(false)` everywhere in `src/`.
- Time only via injected `TimeProvider`; delays via `timeProvider`-aware mechanisms so tests
  control the clock. Randomness only via injected seams. (Determinism — spec NF-020.)
- Workflow `CompleteWithin`, per-attempt `WithStepTimeout`, retry delay, waits, and durable
  deadlines are orchestration semantics owned by OrcaCore. Polly may be used inside an
  application/provider call, but it never defines replay, workflow retry, timeout fencing, or
  terminal state.
- `WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)` and `WithStepTimeout(TimeSpan)`
  attach after one authored step and may each appear at most once; `maxAttempts` includes the
  initial attempt. The original absolute `CompleteWithin` deadline survives
  `ContinueAsNew` generations and wins as terminal `TimedOut` without waiting for a
  token-ignoring body.

## 4. Naming & layout

- One public type per file; file name = type name. Folder = module.
- `OrcaCore.<Project>.<Module>` namespaces mirror folders.
- Options records end in `Options`; ports start with `I` and end in the role
  (`IWorkflowEventStore`); fakes in TestSupport are `Fake<PortName>`.
- No abbreviations in public API (`definition`, not `def`).

## 5. Semantic values

- Every domain-significant route/discriminator/state/error/storage key, limit, retry count,
  timeout, version, and policy value has exactly one named owner: an enum member, value object,
  named constant, or validated options member. Product code and provider SQL bind to that owner;
  they do not repeat the representation as an unexplained inline literal.
- Keep ownership at the narrowest shared boundary that consumes the value. Public constants are
  appropriate only when independently shipped packages must share a persisted representation;
  otherwise prefer an internal owner. Schema indexes should use general key columns instead of
  embedding a duplicated discriminator solely as a partial-index predicate.
- Intrinsic language and algorithmic values remain local when their meaning is self-evident:
  `null`, booleans, empty collections, zero/one used for indexing or arithmetic identity, and
  one-off test fixture data are not domain policy. Name them when changing the value would alter
  a workflow, provider, wire, storage, retry, timeout, capacity, or compatibility contract.

## 6. Errors and logging

- Exception taxonomy (Abstractions): `OrcaCoreException` base →
  `WorkflowDefinitionException` (build/validation), `WorkflowConcurrencyException`,
  `WorkflowRoutingException` (no-match/ambiguous — clear messages are an acceptance
  requirement, EV-012), `WorkflowLifecycleException` (illegal trigger, CR-030),
  `WorkflowVersionException` (DU-041). Messages must state what the caller should do.
- Logging via `ILogger<T>` abstractions with source-generated `LoggerMessage` definitions;
  no string interpolation in log calls; no logging in Abstractions.
- Runtime diagnostics use only BCL `ActivitySource`, `Meter`, and `ILogger<T>` owners. Every
  shipped instrument starts with `orca.`; OpenTelemetry SDKs, exporters, authorization, and
  redaction remain host-owned. Durable gauges and direct operator inspection consume the same
  immutable provider snapshot within one operational sweep interval. Durable last activity is
  authored by committed engine progress; a provider snapshot evaluates stuck candidates against
  its explicit observation time and validated threshold. PostgreSQL pressure queries operate on
  the one-row-per-instance projection/checkpoint join and never periodically aggregate the
  append-only event relation.
- Retention is a provider-maintenance concern, not an application workflow operation. Physical
  cleanup must re-check active instances and live inbox/outbox references atomically and retain
  confirmation, idempotency, and monotonic route-revision tombstones while delayed observations
  can still arrive. Archive timestamps are provider-owned and survive later aggregate projection
  writes; a stream without a current instance projection is not archivable.

## 7. Comments & docs

- XML docs on all public contracts: state the *contract* (guarantees, failure modes,
  threading), not the implementation.
- Inline comments only for non-obvious constraints ("commit must precede mailbox removal —
  EV-032"), citing spec IDs. No narrative/change-log comments.

## 8. Git hygiene (per task)

- One task = one commit (or a small series); message: `T1-04: builder validation
  accumulates errors (CR-002)` — task id first, spec IDs in parentheses.
- Never commit failing tests, commented-out code, or TODOs without an owner task id.
