# Developer-Facing Interface Reshape — OpenSpec Review (2026-07-13)

Reviewer scope: full consumer-perspective review of the
`reshape-developer-facing-interfaces` OpenSpec change (proposal, design, tasks,
and all seven spec deltas) against the authoritative root `src/`, `tests/`,
`samples/`, `docs/`, and `openspec/` trees, and against the two active changes
`adopt-structured-fiber-execution` and `add-runtime-concurrency-limits`.
Review-only; no proposal, specification, source, test, or sample file was
modified. Lens: Module = Interface + Implementation; Depth = leverage behind a
small Interface; a Seam is real only when two supported Adapters exist; the
deletion test decides whether a Module concentrates or spreads complexity.

## Verdict

**APPROVE WITH CHANGES**

The diagnosis is accurate — every factual claim I could execute or read
verified, several to the exact line and count — and the four-tier direction,
mode-first authoring, facade deepening, and management realignment are the
right shape for a library with zero external consumers. No design decision is
unsound. What blocks `/opsx:apply` is coordination and specification debt, not
architecture: the change is implementable only on top of the structured-fiber
compiler yet its task graph does not gate on it; the split-host external-job
topology contradicts the facade's "drives every accepted transition" promise;
and the advanced-package dependency edge that task 2.3 hits on day one is
undeclared. All required changes are document-level (design, spec deltas,
tasks).

Counts: **0 × P0, 3 × P1, 8 × P2, 4 × P3.**

Verification commands run:

- `openspec validate reshape-developer-facing-interfaces --strict` → **valid** (passed).
- `dotnet run --project samples/OrcaCore.Examples/OrcaCore.Examples.csproj --no-restore` →
  example 06 prints `external-job-result=Poisoned, duplicate-completion=Poisoned`
  after two `Durable command DurableCommand completed with Poisoned` telemetry
  lines — the proposal's headline misuse reproduction is live, today.

---

## 1. Claim verification

Every claim flagged for particular scrutiny, checked against root code.

| Claim | Verdict | Evidence |
|---|---|---|
| `WithDefinitionRetry` supported by neither engine | **Confirmed (asymmetrically worse)** | Authoring: [WorkflowBuilder.cs:111](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L111) sets `definitionPolicies`. Ephemeral rejects at registration: [EphemeralWorkflowEngine.cs:125-130](../../src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs#L125). The durable engine never reads definition-level `Policies.Retry` — the only durable retry reads are step-level: [DurableDriverSegmentRun.Steps.cs:404,418,454,478](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs#L404). So the same authored method throws in one mode and is *silently ignored* in the other — two different failure modes for one public method. |
| `WithPoolKey` silently ignored by durable execution | **Confirmed** | Policy carried on [WorkflowPolicySet.cs:7](../../src/OrcaCore.Core/Definitions/WorkflowPolicySet.cs#L7); the only runtime read is the ephemeral step executor: [StepExecutor.cs:38](../../src/OrcaCore.Engine.Ephemeral/Execution/StepExecutor.cs#L38) (`EnterStepAsync(stepNode.Policies.PoolKey, …)`). No durable source file reads `PoolKey`. |
| Public `WaitLong` authoring is absent | **Confirmed** | The only `WaitLong` author surface is `internal sealed class DurableWorkflowBuilder<TState>` — [DurableWorkflowBuilder.cs:6-24](../../src/OrcaCore.Engine.Durable/Building/DurableWorkflowBuilder.cs#L6) — used by exactly one test file (`tests/OrcaCore.Engine.Durable.Tests/Execution/DurableWaitTests.cs`); it produces a detached `DurableWaitDefinition` list, not a registrable `WorkflowDefinition<TState>`. The baseline spec (`openspec/specs/durable-runtime/spec.md`, "Wait and WaitLong have distinct residency behavior") already requires it publicly. Specification violation confirmed. |
| `DurableManagement` fallback creates an independent mutation lane | **Confirmed (worse than stated)** | [DurableManagement.cs:347-350](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L347): `RequiredCommandProcessor()` returns `commandProcessor ?? new DurableCommandProcessor(RequiredWorkflowEventStore(), resourcePoolStore)`. The parameterless-runtime processor ctor builds *its own* `DurableCommandRuntime` ([DurableCommandProcessor.cs:29-35](../../src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs#L29)). Because the fallback is evaluated **per call**, every mutation on an unconfigured `DurableManagement` gets a *fresh* lane registry — two concurrent `PauseAsync` calls from the same management object do not even share a lane with each other, let alone with the runtime's driver. Only optimistic concurrency at the store saves correctness; the in-process serialization guarantee is silently absent. |
| Durable management lacks typed business-state access | **Confirmed** | No `GetState` anywhere in `OrcaCore.Engine.Durable.Management`; [DurableManagementQuery.cs:43-47](../../src/OrcaCore.Engine.Durable/Management/DurableManagementQuery.cs#L43) `GetAsync` is `snapshots.Single()` (LINQ `InvalidOperationException` as its not-found contract). Ephemeral has both a real instance handle and typed state: [EphemeralManagement.cs:392](../../src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs#L392) `GetState<TState>()`. |
| Durable sample completes its definition then issues raw job commands and prints `Poisoned` | **Confirmed, reproduced live** | [ExampleRunner.cs:266-269](../../samples/OrcaCore.Examples/ExampleRunner.cs#L266) builds `Init → End("DurableStarted")`; `StartOrGetAsync` drives to terminal ([DurableWorkflowRuntime.cs:107](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L107)); raw `RunExternalJobCommand`/`CompleteExternalJobCommand` at [ExampleRunner.cs:288-323](../../samples/OrcaCore.Examples/ExampleRunner.cs#L288) then hit the terminal instance. Executed output: `external-job-result=Poisoned, duplicate-completion=Poisoned`. The sample's own narration is stale twice over: the comment at lines 325-327 claims start "does not interpret the definition to its End" (it does — the snapshot prints `Completed`), and the comment at 253-254 promises the duplicate completion returns `NoOp` (it returns `Poisoned`). |
| Provider registration inconsistency is role-specific | **Confirmed** | `AddOrcaCorePostgreSql`, `AddOrcaCoreSqlServer`, `AddOrcaCoreRabbitMq` (role-less names) vs `AddOrcaCoreRedisProjectionCache` (role-named) — see the five `*ServiceCollectionExtensions.cs` files under `src/OrcaCore.Providers.*`. ZeroMQ has a full dispatcher implementation (`ZeroMqMessageDispatcher.cs`, options, publisher seam) and **no registration extension at all**. |
| `OrcaCore.Abstractions` mixes application, provider, and protocol Interfaces | **Confirmed, counts exact** | Public type declaration scan: 77 under `Abstractions/Durable/`, 63 under `Abstractions/Providers/`, ~208 total including nested — matching the design's stated 77/63/208 precisely. `StepResult` (application) sits four directories away from `WorkflowCommand` (protocol) in the same package. |
| Base hosting depends on all OpenTelemetry exporters | **Confirmed** | [OrcaCore.Hosting.csproj:9-11](../../src/OrcaCore.Hosting/OrcaCore.Hosting.csproj#L9): `OpenTelemetry.Exporter.Console`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Exporter.Prometheus.AspNetCore` are unconditional dependencies of the one hosting package. |

Other design-table rows verified: common `ForEach` rejected by durable
registration at [DurableDriverCatalog.cs:42-47](../../src/OrcaCore.Engine.Durable/Driver/DurableDriverCatalog.cs#L42);
`RunChild`/`RunChildren` rejected by ephemeral registration via
`RequiresDurableEngine` at [EphemeralWorkflowEngine.cs:118-123](../../src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs#L118);
durable-only `StepResult` variants (`ContinueAsNew`, `RunExternalJob`,
`AcquireResources`, [StepResult.cs:36-59](../../src/OrcaCore.Abstractions/Steps/StepResult.cs#L36))
throw in ephemeral only **after the step has executed and returned**
([StepExecutor.cs:171-173](../../src/OrcaCore.Engine.Ephemeral/Execution/StepExecutor.cs#L171));
`DurableCommandProcessor` exposes 22 public/`internal` `ProcessAsync` overloads;
no-confirm `TerminateAsync`/`PurgeAsync` overloads that can only throw at
[DurableManagement.cs:100-107](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L100)
and [215-219](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L215);
`DestructiveCommandSafety` has `Confirmed` as its **only** member, therefore the
zero/default value ([DurableManagement.cs:362-368](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L362)) —
`RequireDestructiveSafety(default, …)` passes; `DurableDagRunner` demands
caller-reconstructed `CompletedNodeIds`/`FailedNodeIds`/`ScheduledNodeIds`/`RequestedAt`
([DurableDagRunner.cs:58-65](../../src/OrcaCore.Engine.Durable/Execution/DurableDagRunner.cs#L58));
`DurableSagaCommandAdapter` is self-described "interim" and returns raw commands
with caller-supplied `CommandId`/timestamps ([DurableSagaCommandAdapter.cs:8-33](../../src/OrcaCore.Engine.Durable/Execution/DurableSagaCommandAdapter.cs#L8));
`RedisProviderProfile` is referenced by exactly one test
(`tests/OrcaCore.Providers.Redis.Tests/RedisProjectionProviderTests.cs:44`);
`AddOrcaCore()` silently registers both engines plus the entire in-memory
durable stack ([OrcaCoreServiceCollectionExtensions.cs:28-113](../../src/OrcaCore.Hosting/OrcaCoreServiceCollectionExtensions.cs#L28));
workflow/saga builders expose `Build` + `BuildValidated` while the DAG builder
exposes only `BuildValidated` ([WorkflowDagBuilder.cs:43](../../src/OrcaCore.Core/Building/WorkflowDagBuilder.cs#L43));
`SagaDefinition` stores `forwardActions.ToArray()` behind `IReadOnlyList<>`
([SagaDefinition.cs:25-26](../../src/OrcaCore.Core/Definitions/SagaDefinition.cs#L25)) —
downcast-and-mutate is possible, and forward actions carry executable factories.
One correction in the change's favor: `WorkflowDefinition<TState>` already keeps
`RootSequence` and `Policies` internal ([WorkflowDefinition.cs:42-44](../../src/OrcaCore.Core/Definitions/WorkflowDefinition.cs#L42)),
so the immutability work is genuinely saga/DAG-scoped, not workflow-wide.

The design's context table is, in short, the most accurately evidenced OpenSpec
context section in this repository. The findings below are therefore about what
the proposal *misses or leaves ambiguous*, not what it gets wrong.

---

## 2. Findings

### [P1] F-01 — The facade's "drives every accepted transition" promise is unimplementable on hosts that cannot drive

- **Verdict:** underspecified
- **Evidence:** durable-runtime delta, "Durable runtime is the complete
  application facade": *"the runtime drives every accepted transition to a
  stable suspension or terminal point"*; design Decision 4: the facade owns
  *"raw command dispatch and automatic drive-to-stability"*; tasks 4.3/4.4.
  But driving requires the bound definition version registered on the driving
  host: `DriveAsync(…, DurableDriveMode.Required, …)` parks driver-owned
  instances whose definition is not registered
  ([DurableWorkflowRuntime.cs:294-305](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L294), DR-016),
  and today the split is deliberate: `CompleteExternalJobCommand` commits a
  fact, and the continuation pump on a definition-hosting node advances it
  ([DurableContinuationPump](../../src/OrcaCore.Engine.Durable/Driver/DurableContinuationPump.cs)).
  Neither the delta nor the design says what `runtime.ExternalJobs.CompleteAsync`
  does when the *reporting* process is a thin callback host (webhook receiver,
  queue consumer) with no definitions registered — the normal production
  topology for external jobs.
- **Developer impact:** a worker-callback author builds a minimal ASP.NET host
  referencing only the documented application package, calls
  `ExternalJobs.CompleteAsync`, and — depending on how an implementer reads
  "drives every accepted transition" — either gets an exception/parked instance
  (definition not registered) or the requirement is quietly weakened during
  implementation. Either way the flagship golden path of this change is
  ambiguous exactly where it matters.
- **Recommendation:** amend design Decision 4 and the durable-runtime delta to
  a two-part contract: application operations **commit** the outcome with
  at-least-once continuation handoff (continuation outbox), and **drive inline
  only when the bound definition version is locally registered**; the returned
  application result distinguishes `AppliedAndProgressed` from
  `AppliedPendingContinuation` (or the runtime hides the difference and the
  requirement text says "the runtime *ensures* progression" rather than
  "drives"). Add a scenario for the definition-less reporting host.
- **Regression/acceptance coverage:** an acceptance test where host A registers
  the definition and host B (no registrations, same store) reports the job
  completion; assert the instance reaches the documented state via host A's
  pumps and that host B's call returns a stable applied result.

### [P1] F-02 — The task graph does not gate on `adopt-structured-fiber-execution`, though half of it builds on that change's compiler

- **Verdict:** confirmed (coordination gap)
- **Evidence:** Task 3.1 requires mode-first builders "over one shared internal
  graph and compiler Module" — the compiled instruction plan is the *other*
  change's deliverable (fiber proposal "What Changes": compiled single-entry
  plans, compile-before-registration). Tasks 6.5–6.8 (durable saga) explicitly
  run "through the durable runtime/structured execution driver". Meanwhile the
  fiber change rewrites the very builder signatures this change's compile-time
  capability fixtures (tasks 1.7, 3.14) will bake in: typed branch results and
  explicit merges for `Parallel`/`WhenFirst`/`ForEach`
  (fiber workflow-authoring delta, "Parallel authoring defines deterministic
  join structure", "ForEach authoring uses the structured scope contract"),
  root-only `Init`/`End`, and branch returns. Design examples in this change
  (Decision 2/3) still show pre-fiber shapes (`.ForEach(...)` with partitioner
  args, `.End("Accepted")` mid-graph semantics unstated). Task 1.1 says
  "reconcile" but no section-3 or section-6 task names the fiber prerequisite,
  and tasks.md has no cross-change ordering statement at all.
- **Developer impact:** if implementation sessions execute tasks in listed
  order, section 3 builds mode-first builders and negative-compile baselines
  against the legacy node graph, and the fiber change then breaks every
  fixture, sample, and approved public-signature baseline a second time —
  double churn across the exact surfaces this change exists to stabilize.
- **Recommendation:** (a) add explicit gates: tasks 3.1–3.14 depend on the
  fiber change's compiler and builder-shape decisions; 6.2–6.8 depend on its
  structured driver; (b) rewrite Decision 2/3 examples in the reconciled
  signatures once task 1.1 lands; (c) resolve the two direct text conflicts —
  ForEach ("durable compilation SHALL reject" vs "capability is absent from the
  durable builder"; keep both: absent from the surface *and* compiler
  defense-in-depth, and say so in both changes) and ContinueAsNew (fiber treats
  it as a runtime transition needing a quiescent root scope; this change makes
  it a structural node; both hold only if the structural node is the *sole*
  author path and the fiber deltas stop implying a `StepResult` origin).
- **Regression/acceptance coverage:** the shared capability matrix (task 8.3)
  becomes a joint artifact referenced by both change documents; capability
  compile fixtures are written once, against reconciled signatures.

### [P1] F-03 — The dependency edge between `OrcaCore.Provider.Abstractions` and `OrcaCore.Runtime.Protocol` is undeclared, and task 2.3/2.4 hits it immediately

- **Verdict:** underspecified
- **Evidence:** design Decision 1 assigns "provider ports, provider commit
  DTOs, certification contracts" to Provider.Abstractions and "durable commands
  and facts, host dispatch contracts, checkpoint/envelope protocol" to
  Runtime.Protocol, and declares only: application packages expose neither;
  providers may depend on Provider.Abstractions; runtime implementations may
  depend on both. But the provider ports *store the protocol*:
  `IWorkflowEventStore` appends/loads `WorkflowEvent` facts, commit records
  reference checkpoints ([ProviderCommitContracts.cs:146](../../src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs#L146)
  carries `ContinueAsNewGeneration`; `CheckpointWrite.RuntimeState` carries
  child materialization used by [DurableManagement.cs:279-299](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L279)).
  Either Provider.Abstractions references Runtime.Protocol (an edge the tier
  diagram neither permits nor forbids), or facts/checkpoints are duplicated as
  provider DTOs, or they move wholesale into Provider.Abstractions (making the
  "protocol" package nearly empty). The repository-foundation delta's
  one-way-direction requirement is silent on the advanced-to-advanced edge, so
  the architecture tests demanded by task 2.6 cannot be written unambiguously.
- **Developer impact:** a provider author's dependency closure either quietly
  includes the entire durable command protocol (recreating today's "everything
  is one Abstractions package" problem one tier down) or the implementer
  invents the split ad hoc mid-task, and the public-surface baselines get
  approved against an undebated topology.
- **Recommendation:** decide and record in Decision 1: recommended —
  Runtime.Protocol holds commands *and* facts *and* checkpoint/envelope
  records; Provider.Abstractions holds ports and commit DTOs and **may
  reference Runtime.Protocol** (persisting the protocol is the provider's job;
  that edge is honest); forbid the reverse edge. Update the
  repository-foundation delta so the architecture check names all permitted
  edges among the four tiers.
- **Regression/acceptance coverage:** the task 2.6 architecture test asserts
  the full permitted-edge list; the provider-authoring consumer fixture (task
  1.5) proves a provider compiles against Provider.Abstractions (+ its declared
  edge) without any engine reference.

### [P2] F-04 — Poison remediation (`RearmAsync`) is left on the application facade with protocol types in its signature

- **Verdict:** confirmed
- **Evidence:** `DurableRearmRequest(StreamVersion ExpectedStreamVersion, bool AcknowledgePoison)`
  at [DurableWorkflowRuntime.cs:326-328](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L326).
  Decision 4 bans `DurableCommandResult`, commit batches, and "poison internals"
  from application results and routes stream versions to
  logging/telemetry/advanced seams — but neither the design nor the
  durable-runtime/management deltas assign re-arm anywhere, and its request
  type is built from a protocol identity (`StreamVersion`) that the
  workflow-contracts delta forbids in application signatures. Task 5.7 mentions
  "remediation" for durable instance handles without resolving the parameter
  problem.
- **Developer impact:** an operator tool built on the documented application
  package cannot express "re-arm this parked instance" without first learning
  stream versions from an advanced seam — or the type survives the reshape and
  the signature guard (task 2.6 / workflow-contracts "no application signature
  leaks a protocol type") fails on the runtime's own facade.
- **Recommendation:** place re-arm on the durable instance handle's operator
  extension (task 5.7) with an application-safe precondition token (e.g., an
  opaque remediation ticket obtained from the same handle's diagnostics, or
  simple compare-and-act semantics with a stable conflict result), and let the
  raw `StreamVersion` variant live in Runtime.Protocol for custom hosts.
- **Regression/acceptance coverage:** management regression (task 5.10) adds
  parked/poisoned re-arm through the handle: stale precondition → stable
  conflict result; acknowledged poison → instance progresses.

### [P2] F-05 — Duplicate same-named public types across engines are real, already bite the sample, and no task names them

- **Verdict:** confirmed (missing from the change's own inventory)
- **Evidence:** `WorkflowInstanceQueryModel` exists twice
  ([EphemeralManagement.cs:477](../../src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs#L477)
  and [Durable/Management/WorkflowInstanceQueryModel.cs:9](../../src/OrcaCore.Engine.Durable/Management/WorkflowInstanceQueryModel.cs#L9));
  `WorkflowStatistics`/`WorkflowStatisticsGroup` exist in both
  `OrcaCore.Engine.Ephemeral.Management` and `OrcaCore.Abstractions.Instances`;
  `DestructiveCommandSafety` exists per engine
  ([EphemeralManagement.cs:558](../../src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs#L558),
  [DurableManagement.cs:362](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L362)).
  The sample already needs two `GroupText` overloads to print statistics from
  the two engines ([ExampleRunner.cs:367-375](../../samples/OrcaCore.Examples/ExampleRunner.cs#L367)).
  Design Decision 5's current-state framing ("Both engines use the shared
  `WorkflowInstanceQueryModel` … unless") describes the *target* as if it were
  partly true today; tasks 5.1/5.2 imply consolidation but the deletion sweep
  (9.7) does not name these collisions.
- **Developer impact:** any tool referencing both engines needs alias-qualified
  code for identically named concepts; after the reshape, a leftover duplicate
  silently survives into a baseline because no task lists it.
- **Recommendation:** add the four duplicated names explicitly to task 5.1
  (single shared contracts in the application tier) and to the 9.7 deletion
  checklist; correct Decision 5's wording to state that the shared query model
  does not exist yet.
- **Regression/acceptance coverage:** public-surface baseline review asserts
  each of these names exists exactly once across shipped assemblies.

### [P2] F-06 — Ephemeral retention/eviction vocabulary is dropped by the management realignment

- **Verdict:** underspecified
- **Evidence:** `EphemeralManagement.Evict`, `EvictTerminal`
  ([EphemeralManagement.cs:67-77](../../src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs#L67)),
  `DetectStuck` (line 171), and lifecycle-event queries have no place in the
  proposed shared vocabulary (`IWorkflowManagement`/`IWorkflowSelection`/
  `IWorkflowInstanceHandle`, design Decision 5) nor in the
  management-and-querying delta, which assigns retention only to "focused
  durable management Modules". Ephemeral retention is a known open P1 in this
  repository's audit history; terminal-instance memory growth is its only
  mitigation today.
- **Developer impact:** an implementer either deletes eviction (regressing the
  open retention issue) or bolts it onto the shared handle ad hoc; either
  outcome is unreviewed.
- **Recommendation:** add one requirement to the management-and-querying delta:
  ephemeral handles/roots keep explicitly ephemeral memory-retention operations
  (evict, evict-terminal, stuck detection), named as in-memory operations so
  they cannot be mistaken for durable retention.
- **Regression/acceptance coverage:** ephemeral management tests keep eviction
  and stuck-detection scenarios green through the vocabulary migration.

### [P2] F-07 — The ExternalJobs submodule has no worker-reported *failure* operation, and the protocol has no such command to wrap

- **Verdict:** underspecified
- **Evidence:** the durable command set contains `RunExternalJobCommand`,
  `CompleteExternalJobCommand`, `TimeoutExternalJobCommand`
  ([WorkflowCommand.cs:266,317,333](../../src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs#L266)) —
  timeout is a timer-sweep concern, not a worker verb. Task 4.3 mirrors this:
  "typed external-job completion and timeout reporting". Nothing lets a worker
  report "the job ran and failed" with a payload/reason distinct from silently
  letting the timeout fire.
- **Developer impact:** a worker that catches a permanent failure at t+5s must
  either fake a completion with an error-shaped payload (pushing failure
  semantics into business state) or let the instance burn the full 15-minute
  timeout before progressing — both are the kind of workaround this change
  exists to eliminate.
- **Recommendation:** add a `FailAsync` (worker-reported failure with reason
  and the same completion-identity deduplication) to the external-job facade
  requirement and a matching protocol command task; define its interaction
  with authored failure policy.
- **Regression/acceptance coverage:** acceptance test: job dispatched → worker
  reports failure → instance takes the authored failure path before timeout;
  duplicate failure report returns the stable duplicate result.

### [P2] F-08 — Phantom generic parameters defeat inference on the flagship start API, and the proposal's friction list misses it

- **Verdict:** confirmed (gap in scope)
- **Evidence:** `StartOrGetAsync<TInput, TState>(string idempotencyKey, DefinitionId, DefinitionVersion, TInput input, …)`
  ([DurableWorkflowRuntime.cs:86-94](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L86))
  uses `TState` only for the registry resolve — it appears in no parameter, so
  C# cannot infer it and callers must always write both type arguments;
  `EphemeralWorkflowEngine.StartAsync<TInput, TState>` has the same shape
  ([EphemeralWorkflowEngine.cs:138](../../src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs#L138)).
  Payload-less event raising needs `RaiseEventAsync<object?>(…, null, …)` to
  disambiguate. The sample writes explicit type arguments everywhere. Design
  Decision 4's `StartOrGetAsync("orders/6001", definition, input, ct)` example
  would infer — but only for the definition-instance overload; the
  id+version overload the registry-based path needs is not addressed, and
  neither tasks nor the quality delta mention overload/inference verification.
- **Developer impact:** the very first line of the golden path requires
  spelled-out generics or produces CS0411; discoverability of the "small
  facade" suffers at its front door.
- **Recommendation:** in the reconciled facade design, remove phantom type
  parameters: either return a typed definition handle from registration
  (`runtime.Definition<TState>(id, version).StartOrGetAsync(key, input, ct)`)
  or drop `TState` from start and validate state-type at the registry. Add
  "golden-path calls compile without explicit type arguments" to the
  compile-fixture tasks (3.14/1.5).
- **Regression/acceptance coverage:** consumer fixtures compile the documented
  snippets exactly as documented (no extra type arguments), enforced by the
  sample-assertion gate.

### [P2] F-09 — Exception-versus-result rules for event routing are still unassigned

- **Verdict:** underspecified
- **Evidence:** today `RaiseEventByCorrelationAsync` throws
  `WorkflowRoutingException` for zero and for multiple matches
  ([DurableWorkflowRuntime.cs:210-221](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L210))
  while duplicate deliveries return a result outcome. Decision 4 defines the
  application vocabulary (`Created`, `Applied`, `Duplicate`, `AlreadyTerminal`,
  "or an explicit typed failure") and the durable-runtime delta covers
  terminal-instance mapping, but no requirement states which routing outcomes
  are results and which are exceptions — no-match, ambiguous-match, unmatched
  event name on a live instance, delivery to a paused instance.
- **Developer impact:** callers wrap every raise in try/catch *and* inspect
  results; retries around "no active wait *yet*" (a routine race with the
  driver committing the wait) get built on exception filters.
- **Recommendation:** add one requirement enumerating routing outcomes and
  their contract: recommended — no-match and ambiguous-match are typed results
  (they are expected operational states, especially no-match-yet races), and
  exceptions are reserved for caller programming errors (empty ids, null
  names). Fold into task 4.5's mapping table.
- **Regression/acceptance coverage:** routing acceptance tests assert the
  chosen contract for all four outcomes across both delivery entry points.

### [P2] F-10 — The lease authoring node's lifecycle (release, scope binding) has no requirement anywhere

- **Verdict:** underspecified
- **Evidence:** today `StepResult.AcquireResources` acquires tickets for a
  holder key with optional lease duration ([StepResult.cs:56-59](../../src/OrcaCore.Abstractions/Steps/StepResult.cs#L56));
  release paths are expiry, operator force-release
  ([DurableManagement.cs:194-202](../../src/OrcaCore.Engine.Durable/Management/DurableManagement.cs#L194)),
  and job completion. The workflow-authoring delta requires only that throttles
  and leases be *named distinctly*; the workflow-contracts delta only that the
  lease use "the durable resource-lease contract". Nothing specifies when a
  structural lease node's tickets are released (scope exit? explicit release
  node? terminal transition? compensation?), which is exactly the semantics an
  author must know to avoid deadlocking a capacity-1 pool. The fiber change
  meanwhile makes obligations fiber/scope-owned — the natural answer — but
  neither change states it for the *authoring* node.
- **Developer impact:** an author acquires a lease in a branch, the branch is
  cancelled by `WhenFirst`, and whether the ticket is released or leaks until
  expiry is implementation-defined.
- **Recommendation:** add a workflow-authoring requirement: durable lease
  acquisition is scope-bound; tickets release deterministically on scope exit,
  branch cancellation, failure, and terminal transitions, with expiry as the
  crash backstop. Coordinate the wording with the fiber change's scope-owned
  obligations (task 1.1/1.2).
- **Regression/acceptance coverage:** acceptance tests for lease release on
  normal exit, cancelled branch, failed scope, and crash-restart expiry.

### [P2] F-11 — Canonical `docs/specs/` amendments have no task, unlike the sibling change

- **Verdict:** confirmed (missing task)
- **Evidence:** this change deletes/changes behavior that canonical
  requirements describe — e.g. the `DurableWorkflowRuntime` doc header cites
  DR-032/DR-061 for a claim the change rewrites; `StepResult.RunExternalJob`
  cites DR-031 trigger semantics being restructured into nodes; pool-hint
  requirements sit in the contracts specs. `docs/specs/` is the declared source
  of truth (CLAUDE.md), and the fiber change models the discipline with its
  task 1.2 (enumerate and amend conflicting canonical requirements before
  refactoring). Here, task 9.8 only "updates traceability links" at the very
  end, and task 8.5 fixes narrative docs, not canonical requirement text.
- **Developer impact:** after implementation, canonical requirements describe
  a `StepResult`-based external-job contract and a definition-retry policy
  that no longer exist; the next audit cycle relitigates them.
- **Recommendation:** add a section-1 task mirroring fiber 1.2: enumerate every
  canonical requirement touched by removed/relocated capabilities
  (`WithDefinitionRetry`, pool hints, `StepResult` durable variants, raw
  command journeys, management timestamps) and amend them with the change.
- **Regression/acceptance coverage:** task 9.8 traceability then links each
  amended requirement to its new public acceptance test.

### [P3] F-12 — Several tasks are too broad for one implementation session

- **Verdict:** confirmed
- **Evidence:** task 3.8 spans four assemblies and both engines (remove three
  `StepResult` variants + implement typed job and continue-as-new nodes +
  serializer-aware selectors + driver execution + tests); task 4.3 bundles
  identity, time, serialization, dedup, commit translation, *and* re-driving;
  task 6.3 is the whole DAG reconstruction/scheduling/resume loop; task 6.5 is
  the entire runtime-owned durable saga feature that the saga delta itself
  treats as the gating condition for exposing durable saga authoring (3.9).
- **Recommendation:** split 3.8 (contracts/authoring vs driver execution vs
  test migration), 4.3 (completion path vs timeout path), 6.3 (reconstruction
  vs scheduling vs resume), 6.5 (registration/forward path vs compensation
  path); order 3.9's durable half explicitly after 6.5.

### [P3] F-13 — Open question 2 (`WaitForEvent`): keep the portable dynamic result

- **Verdict:** review decision requested by the change itself
- **Evidence:** both engines implement it today
  ([StepExecutor.cs:167-168](../../src/OrcaCore.Engine.Ephemeral/Execution/StepExecutor.cs#L167);
  durable wait routing) — a real two-Adapter seam, unlike the durable-only
  results being removed. The structural `Wait` node takes a static event name;
  only the dynamic result covers event names computed from state. Removing it
  buys stronger visualization at the cost of a genuine dynamic capability and
  contradicts the fiber change's contracts delta, which keeps `StepResult`
  "limited to control intent" — waiting *is* control intent.
- **Recommendation:** answer open question 2 as "retain portable dynamic
  `WaitForEvent`", document that structural waits are preferred where the name
  is static, and close the question in design.md (task 1.3).

### [P3] F-14 — Open questions 1 and 3: recommendations

- **Verdict:** review decisions requested by the change itself
- **Recommendation:** Q1 — separate explicit packages (`OrcaCore.Contracts`,
  engines, hosting) plus a small `OrcaCore` meta-package: the tier story *is*
  the product story, and package boundaries are the only mechanically
  enforceable tier boundary NuGet consumers see; a single merged package would
  re-blur what task 2.x separates. Q3 — `Build()` + `TryBuild()` returning the
  existing `Validation<T>`: `TryBuild` matches BCL convention closely enough,
  `ValidateAndBuild` reads as though it validates *more* than `Build` does
  (it doesn't; both validate). Rename the DAG builder to the same pair
  (its `BuildValidated`-only surface is the outlier).

### [P3] F-15 — `StartOrGetAsync(definition, …)` registering the definition as a side effect should not survive into the new facade unexamined

- **Verdict:** confirmed (minor contract ambiguity)
- **Evidence:** [DurableWorkflowRuntime.cs:118-134](../../src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs#L118)
  calls `RegisterDefinition(definition)` on every start. Registration mutates
  the process-wide driver catalog and registry; a start API that silently
  changes host-level routing state (including what DR-016 parking decisions
  hinge on) conflates an application operation with a host-configuration
  operation. Decision 4's example keeps this overload without comment.
- **Recommendation:** in the reshaped facade, make registration explicit and
  host-scoped (hosting builder or a `Definitions` submodule); the
  definition-instance start overload should require prior registration (stable
  not-registered diagnostic) or be dropped.

---

## 3. Finding-disposition table — major proposal decisions

| # | Decision (design.md) | Disposition | Notes |
|---|---|---|---|
| 1 | Four Interface tiers, one-way dependencies | **Approve with changes** | Correct and overdue; the advanced-to-advanced edge must be declared (F-03); packaging open question resolved per F-14 |
| 2 | Mode-first builders behind `Workflow.Ephemeral/Durable` factories | **Approve with changes** | Right shape; kills the throw-at-registration / ignore-at-runtime split verified above. Signatures must be authored post-fiber-reconciliation (F-02); capability allowlist is correctly classified (portable/ephemeral/durable/absent all verified against code) |
| 3 | Durable effects become structural nodes; `StepResult` stays portable-only | **Approve with changes** | Verified that today's variants throw only after step execution in ephemeral; structural nodes also fix the raw `byte[]` payload ergonomics ([StepResult.cs:46](../../src/OrcaCore.Abstractions/Steps/StepResult.cs#L46)). Keep dynamic `WaitForEvent` (F-13); specify lease lifecycle (F-10); the "separate durable step contract if needed later" escape hatch is the correct anti-widening rule |
| 4 | Deepen `DurableWorkflowRuntime` via focused submodules | **Approve with changes** | Genuinely deep, not a renamed processor: it internalizes time, identity, serialization, dedup, mapping, *and* progression that today's callers hand-roll (sample lines 288-323 are the proof). Must resolve split-host drive semantics (F-01), rearm placement (F-04), job-failure verb (F-07), phantom generics (F-08) |
| 5 | Shared async management vocabulary + instance handles + typed durable state | **Approve with changes** | Two real Adapters make this a real seam; drift verified (sync/async, `Single()`, duplicated types). Add duplicate-type consolidation (F-05) and ephemeral retention vocabulary (F-06). Typed state from the committed checkpoint through the configured serializer is feasible — checkpoint runtime state is already read by management today |
| 6 | Non-default destructive confirmation; delete throw-only overloads | **Approve** | Verified that `default(DestructiveCommandSafety)` currently *passes* the guard — the proposed `None = 0` fix is exactly right; single-instance terminate without confirmation is consistent with handle-as-scope-guard |
| 7 | DAG planning vs runtime-owned durable execution; retire interim adapters | **Approve with changes** | `WorkflowDagRunner` is a pure planner (verified: [WorkflowDagBuilder.cs:340](../../src/OrcaCore.Core/Building/WorkflowDagBuilder.cs#L340), no I/O); `DurableDagRunner`'s caller-supplied state sets and `DurableSagaCommandAdapter`'s raw commands fail the deletion test as claimed. Sequencing depends on fiber change (F-02); split oversized tasks (F-12) |
| 8 | Explicit engine registration; provider extensions by role; internalize hosted loops; drop `RedisProviderProfile`; split OTel exporters | **Approve** | All claims verified exactly (`AddOrcaCore` composition root, role-inconsistent names, missing ZeroMQ extension, one-test profile, three exporter deps). Role-based naming honestly reflects that stores/caches/dispatchers are different seams — the "identical overloads everywhere" alternative was correctly rejected |
| 9 | Immediate local validation + aggregated graph diagnostics; strong-ID rejection; deep immutability | **Approve** | Current builder defers even null delegates to build ([WorkflowBuilder.cs:47-49](../../src/OrcaCore.Core/Building/WorkflowBuilder.cs#L47) accepts `null` and records a flag); IDs are default-constructible `readonly record struct`s; saga metadata is downcastable. Scope note: workflow definitions already hide root/policies, so effort concentrates on saga/DAG |
| 10 | Public surface as a test surface (baselines, compile fixtures, consumer projects, sample assertions) | **Approve** | The reproduced `Poisoned` output is the strongest argument for task 1.6's regression; all four guard types are mechanical and none requires new infrastructure beyond a Roslyn compile fixture |

---

## 4. Missing requirements and scenarios

Beyond the findings above:

1. **Worker-reported external-job failure** (F-07) — no requirement, no command.
2. **Definition-less reporting host** (F-01) — no scenario for the dominant
   external-job topology.
3. **Ephemeral memory retention** (F-06) — evict/stuck vocabulary unplaced.
4. **Routing outcome contract** (F-09) — no-match / ambiguous-match / paused
   target unassigned between results and exceptions.
5. **Lease release semantics** (F-10) — scope binding of durable tickets.
6. **Registration as an explicit application operation** (F-15) — the
   developer-facing-surface delta's golden-path requirement lists
   "definition registration" but no scenario pins whether start implies it.
7. **`AddOrcaCoreInMemoryDurable` capability honesty** — the in-memory durable
   store loses everything on restart; the development-registration requirement
   should oblige the registration to say so in its diagnostics, or the "in
   memory" name carries the entire warning (minor, wording-level; noted only
   because the tier's purpose is preventing accidental production use).

## 5. Conflicts with active OpenSpec changes

No two deltas modify the same baseline requirement heading (verified by
heading comparison across both change trees), so OpenSpec-level merges are
clean; every conflict is semantic:

| Topic | `adopt-structured-fiber-execution` says | This change says | Resolution needed |
|---|---|---|---|
| Durable `ForEach` | "Durable compilation SHALL reject `ForEach` explicitly" | Capability "absent" from durable authoring surface | Both, explicitly: surface absence + compiler guard; cross-reference in both deltas (task 1.1) |
| `ContinueAsNew` | Runtime transition requiring quiescent root scope; banned in branches | Structural durable builder node, removed from `StepResult` | Compatible only if the structural node is the sole author path; fiber deltas must not re-imply a step-result origin |
| `Parallel`/`WhenFirst`/`ForEach` signatures | Typed branch results, explicit merges, no shared mutable branch state | Listed as "portable structured composition" with legacy-shaped examples | Reshape's examples, fixtures, and baselines must be authored against fiber shapes (F-02) |
| Compiler/validation ownership | Compile-before-registration, aggregate structural diagnostics, plan fingerprint | `Build()`/`TryBuild()` completion vocabulary, aggregate diagnostics | One compiler Module, one diagnostics contract; `Build` returns the compiled plan-backed definition — state this jointly |
| Waits | Fiber/scope-owned wait obligations | Portable `Wait` node + open question on dynamic `WaitForEvent` | Keep dynamic result (F-13); wait ownership is implementation detail behind the authoring seam — consistent |
| Pools | Blocking a fiber quantum on unavailable capacity; redefines concurrency-change design §3-4 | Binary split: transient host throttle (ephemeral-only) vs durable lease | `add-runtime-concurrency-limits` defines *cross-instance in-process* named pools — a third category straddling the binary split. The reconciliation in task 1.2 must be three-way, producing one pool taxonomy (per-step throttle / cross-instance transient pool / durable lease) with mode availability for each |

The durable saga and DAG closure sections (6.x) are **dependent on**, not in
conflict with, the fiber change's structured driver — but only prose in the
design says so (see F-02).

## 6. Proposed revised implementation sequence

Reordered to respect the fiber dependency and to front-load the decisions that
invalidate later work if they slip:

1. **Joint reconciliation gate** (this change 1.1–1.3 + fiber 1.2, together):
   shared capability matrix, reconciled builder signatures, pool taxonomy
   (three-way, with `add-runtime-concurrency-limits`), advanced-package edge
   (F-03), external-job topology contract (F-01), open questions 1–3, canonical
   `docs/specs/` amendment list (F-11). No source work before this lands.
2. **Failing guards** (1.4–1.7): public-signature harness, consumer fixtures,
   the `Poisoned` sample regression, capability fixtures — written against the
   *reconciled* signatures.
3. **Tier moves without behavior change** (2.1–2.8): contracts projects,
   dependency direction, architecture tests, OTel exporter split, baselines.
4. **Fiber compiler and structured execution core** (the other change's
   sections through its compiler/driver gates) — prerequisite for 5–7 below.
5. **Mode-first authoring over the shared compiler** (3.1–3.14), including
   public `WaitLong`, `WithDefinitionRetry` removal, structural durable
   effects, immutability, strong-ID rejection.
6. **Durable facade deepening** (4.1–4.8, with 4.3 split per F-12 and the
   F-01/F-07 contract), then **management alignment** (5.1–5.10, including
   F-04 rearm placement, F-05 consolidation, F-06 ephemeral retention).
7. **DAG and saga closure** (6.1–6.8, split per F-12), deleting
   `DurableSagaCommandAdapter` and demoting `DurableDagRunner` only after
   runtime-owned paths pass acceptance.
8. **Hosting/provider role normalization** (7.1–7.8), which depends on the
   facade's composition root (4.2) but not on 5–6; may run parallel to 6.
9. **Journeys, docs, capability matrix** (8.1–8.7) including canonical spec
   amendments.
10. **Verification and deletion sweep** (9.1–9.8) with the duplicate-type
    checklist added to 9.7.

## 7. Review decisions to resolve before `/opsx:apply`

1. Shared capability matrix and reconciled builder/result signatures with
   `adopt-structured-fiber-execution`, including the ForEach and
   ContinueAsNew wording conflicts (F-02).
2. External-job completion/progression contract for hosts without registered
   definitions, and the corresponding rewording of "drives every accepted
   transition" (F-01).
3. The permitted dependency edge between `OrcaCore.Provider.Abstractions` and
   `OrcaCore.Runtime.Protocol`, and which package owns facts and checkpoint
   records (F-03).
4. Three-way pool taxonomy with `add-runtime-concurrency-limits` (throttle /
   transient cross-instance pool / durable lease) and each category's mode
   availability (F-02/F-10 companion).
5. Design open questions 1–3 (packaging, dynamic `WaitForEvent`, `TryBuild`
   naming) — required by the change's own task 1.3; recommendations in
   F-13/F-14.
6. Rearm/poison-remediation tier placement and its application-safe request
   shape (F-04).

## 8. Final recommendation

**Approve with changes.** The change correctly identifies every capability
leak, shallow Module, and misuse trap it claims — several verified to the
exact line, count, and runtime output — and its ten decisions all survive the
deletion test and the two-Adapter seam test. Required before implementation:
resolve the six decisions above and apply the document-level amendments in
F-01…F-11 (facade progression contract, cross-change gating, advanced-package
edge, rearm placement, duplicate-type consolidation, ephemeral retention,
job-failure verb, routing outcome contract, lease lifecycle, canonical spec
amendment task, task splits). None of these invalidates the proposal's
architecture; all of them are cheaper to fix in the documents than in the 78
tasks built on top of them.
