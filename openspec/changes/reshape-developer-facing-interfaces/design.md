## Joint implementation baseline

[`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md)
is the only approved selected-mode capability matrix, public builder/result/merge signature
baseline, and compiler/diagnostic contract for this change. The archived
`adopt-structured-fiber-execution` change delivered the compiler and structured runtime behind
that baseline; `add-runtime-concurrency-limits` still shares its distinct transient-governance
names and lifetimes. Compile fixtures and source implementation SHALL use that baseline
directly and SHALL NOT preserve superseded provisional overloads, leak compiled IR, or introduce a second
compiler.

## Context

The root implementation has useful domain vocabulary and a straightforward ephemeral golden path, but its public Interface currently exposes several different seams as one surface:

- the application seam for authoring, starting, signaling, and inspecting workflows;
- the provider-authoring seam for event stores, projections, resource pools, dispatchers, and commit records;
- the runtime-protocol seam for commands, facts, checkpoints, drivers, pumps, and observers;
- implementation types registered by hosting or used only by tests.

That proximity makes shallow Modules look like normal application entry points. `DurableWorkflowRuntime` claims that callers never issue kernel commands, but it exposes only starts and event delivery while `DurableCommandProcessor` has public overloads for jobs, resources, children, saga actions, timers, and lifecycle transitions. The durable example resolves both Modules and reproduces the resulting misuse: it completes a definition, sends external-job commands to the terminal instance, and prints `Poisoned`.

Capability leakage is bidirectional and broader than the submitted findings:

| Current Interface | Current behavior | Disposition |
|---|---|---|
| Mode-first root builders | `Workflow.Ephemeral` / `Workflow.Durable` and the shared compiler are delivered, but both return the same `WorkflowDefinition<TState>` | Keep the factories; introduce distinct immutable definition types and delete registration-time mode detection |
| Superseded mixed-mode `WorkflowBuilder<TState>` | Still coexists with the mode-first factories and exposes mixed capabilities | Delete early; no alias or parallel path |
| Nested `BranchBuilder.WithPoolKey` | Common branch type makes an ephemeral-only policy discoverable inside durable `Parallel` | Split public nested branch types by selected mode; keep one internal implementation |
| Common `WithDefinitionRetry` | Ephemeral registration rejects it and the durable engine never reads definition retry | Expanded; remove until specified and implemented |
| Root `WithPoolKey` | Mode-first root exposure is now ephemeral-only and durable compilation rejects it, but the name and nested leak remain | Rename to explicit transient-pool vocabulary and preserve mode through every nested builder |
| Common durable `StepResult` variants | Ephemeral execution throws only after the step returns one | Confirmed; remove from the portable result set |
| Structural waits, children, and continue-as-new | Public mode-first builders and the structured driver now implement them | Accept delivered baseline; remove superseded helper paths rather than rebuilding them |
| Structural external jobs and durable leases | Portable `StepResult` variants and durable driver handling remain; structural authoring nodes are missing | Add nodes and direct compiled lowering before deleting the old result variants |
| `WorkflowDefinition.CompiledPlan` and public compiler IR | Compiled plan, instructions, scope/branch identities, policies, and compiler options are public application-adjacent types | Hide the executable plan behind an internal accessor; retain only domain-facing authoring options and diagnostics |
| `ActiveWaitSnapshot` | Exposes `FiberId`, `ScopeId`, and `WaitSequence` | Project authored wait metadata for applications; keep routing ownership at the advanced seam |
| `OrcaCore.Abstractions` | The 2026-07-13 pre-fiber scan found about 200 top-level public declarations; the fiber baseline added more and the current scan is about 219 | Confirmed; split by supported seam and external scenario rather than a numeric target |
| Durable `Instance(...)` | Returns a list-style query, has no typed state query, and mutations remain root methods requiring timestamps | Confirmed and expanded |
| Public `DurableManagement` construction | Optional processor causes mutations to create a new command runtime and lane | Expanded correctness trap; construction becomes runtime-owned |
| Confirmation enums | `Confirmed` is the zero/default value | Confirmed; default must be unconfirmed |
| No-confirm durable terminate/purge overloads | Always throw | Confirmed; delete impossible Interface members |
| `DurableDagRunner` | Caller supplies completed, failed, and scheduled node sets | Confirmed; runtime owns reconstruction and progression |
| `DurableSagaCommandAdapter` | Publicly described as interim and returns raw commands | Confirmed; keep behind runtime-protocol seam until deleted |
| Provider registration | Different names partly reflect different provider roles; ZeroMQ has no extension | Refined; standardize within role rather than pretend all providers are interchangeable |
| Hosted-service classes and `RedisProviderProfile` | Hosting owns construction; the profile is used only by one test | Confirmed deletion/visibility candidates |
| Builder completion | Workflow and saga expose `Build` plus `BuildValidated`; DAG exposes only `BuildValidated` | Confirmed low-priority consistency issue |
| Strong identifiers | Type distinctions help, but default/empty values remain constructible | Expanded; reject invalid identities at every public seam |
| `SagaDefinition` metadata | Public action records expose executable factories and arrays can be downcast and mutated | Expanded; apply the same immutability treatment as workflow definitions |
| Base hosting dependencies | Pulls every OpenTelemetry exporter into the normal hosting Module | Expanded; split optional observability Adapters |

The baseline OpenSpec already requires a separate durable authoring surface and shared management vocabulary, so the target is an alignment with existing product direction. The archived `adopt-structured-fiber-execution` change now supplies the shared compiler, typed structured scopes, mode-first root builders, scheduler, and durable format-2 execution model. This change must consolidate that delivered substrate behind a smaller application seam rather than retaining its implementation types as public contracts.

There are no API clients, released package contracts, or retained durable-data contracts. Source, test fixtures, samples, and development stores may change together to reach the best final shape.

## Goals / Non-Goals

**Goals:**

- Make the normal application Interface small enough that a developer can author, host, start, signal, complete external work, manage, and inspect a workflow without raw protocol knowledge.
- Make unsupported execution-mode combinations unrepresentable through normal authoring wherever practical and reject the remaining graph-wide incompatibilities at build or registration time.
- Increase Depth: runtime-owned progression, time, identifiers, serialization, deduplication, and provider interaction sit behind a small application Interface.
- Increase Locality: command construction, DAG state reconstruction, saga progression, and hosted loops live in their owning Modules instead of being reproduced by callers and samples.
- Preserve supported provider and custom-host extension scenarios through explicit advanced seams.
- Establish mechanical public-surface and package-consumer verification before release.

**Non-Goals:**

- Preserve provisional names, overloads, namespaces, assembly identities, or binary/source compatibility.
- Redesign command/event persistence semantics, provider consistency, fiber scheduling, or checkpoint format except where their visibility changes.
- Treat every storage or messaging provider as the same role.
- Add compatibility wrappers around the old builder, command processor, management, or hosted-service Interfaces.
- Promise distributed custom-host extensibility without an identified second Adapter and an executable certification scenario.

## Decisions

### 1. Establish four explicit Interface tiers

The target package/assembly direction is:

```text
Application
  OrcaCore.Contracts
  OrcaCore.Core authoring and immutable definitions
  OrcaCore.Engine.Ephemeral and OrcaCore.Engine.Durable facades
  OrcaCore.Hosting integration

Provider authoring
  OrcaCore.Provider.Abstractions
  provider ports, provider commit DTOs, certification contracts

Runtime protocol / custom hosting
  OrcaCore.Runtime.Protocol
  durable commands and facts, host dispatch contracts, driver observations

Implementation
  aggregate handlers, checkpoint mappers, concrete hosted loops, serializers/converters,
  driver executors, internal schedulers, test helpers
```

The permitted advanced dependency graph is explicit:

- `OrcaCore.Runtime.Protocol` owns durable commands, committed facts, checkpoint records, and envelope records and does not reference provider authoring;
- `OrcaCore.Provider.Abstractions` owns provider ports, provider commit DTOs, and certification contracts and MAY reference `OrcaCore.Runtime.Protocol` because providers persist protocol facts and checkpoints;
- application contracts and authoring reference neither advanced package, and application public signatures contain neither advanced package's types;
- provider Adapters reference `OrcaCore.Provider.Abstractions` and the declared protocol dependency transitively or directly as required, while engine/runtime implementations may reference both advanced packages;
- no advanced or implementation project references an engine implementation in reverse.

NuGet distribution uses separate explicit packages for contracts, authoring/core, each engine, hosting, provider authoring, and runtime protocol, plus a small `OrcaCore` meta-package for the documented default application experience. Package-consumer tests prove that the meta-package or documented explicit application set is sufficient without direct protocol references. This keeps tier boundaries mechanically enforceable without making the common onboarding path assemble every application package manually.

The post-fiber types are classified explicitly:

| Tier | Types and concepts |
|---|---|
| Application authoring | Mode-specific immutable definitions, workflow/saga/DAG builders, read-only parent/branch snapshots, typed branch/item outcomes, validation diagnostics, domain-facing authoring limits, payload serializer/copy registration, deterministic definition-fingerprint contribution |
| Application inspection | Root workflow snapshots and detached root business state, authored wait/path metadata, lifecycle and statistics models |
| Runtime protocol | `FiberId`, `ScopeId`, durable format-2 envelopes, raw park reasons, commands/facts, checkpoints, stream versions, obligation ownership, continuation records |
| Implementation | Compiled workflow plans/instructions/scopes/branches/policies, compiler identity indexes, schedulers, reducers, driver executors, hosted loops, converters, registry internals |

Implementation assemblies use internal accessors or explicit friend-assembly access for the
compiled plan retained by a definition. Cross-assembly implementation convenience is not a
reason to publish compiled IR through an application package. If a future plan-inspection
tool becomes a supported product seam, it receives a separate read-only inspection model;
it does not expose executable delegates or make the runtime plan an application contract.

Alternative considered: keep one `OrcaCore.Abstractions` assembly and rely on namespaces and documentation. Rejected because IntelliSense, package dependencies, public signatures, and accidental construction still present all seams as equally supported.

Alternative considered: internalize all commands and events. Rejected because provider and custom-host Adapters need a supported wire/protocol contract; those types belong at an explicit advanced seam, not necessarily inside the Implementation.

### 2. Select execution mode before authoring capabilities become available

Use explicit factories with mode-specific builders:

```csharp
var quick = Workflow.Ephemeral<OrderState>(definitionId, definitionVersion)
    .Init<OrderInput>(OrderState.From)
    .ForEach(...)
    .End("Accepted")
    .Build();

var durable = Workflow.Durable<OrderState>(definitionId, definitionVersion)
    .Init<OrderInput>(OrderState.From)
    .WaitLong("approval", state => state.CorrelationId)
    .RunExternalJob(...)
    .End("Approved")
    .Build();
```

The delivered concrete root names are `EphemeralWorkflowBuilder<TState>` and
`DurableWorkflowBuilder<TState>`. Callers make the mode choice before IntelliSense exposes
mode-specific methods. The two builders share one internal graph/compiler Module for
portable nodes, validation codes, identities, and structured execution plans; they do not
duplicate implementations.

The post-fiber consolidation changes their completion types to
`EphemeralWorkflowDefinition<TState>` and `DurableWorkflowDefinition<TState>`. Both expose
immutable identity, version, mode, fingerprint, and authored metadata, but neither exposes a
public `CompiledPlan`. An ephemeral engine cannot accept a durable definition and a durable
registry cannot accept an ephemeral definition through its normal static contract; the old
`RequiresDurableEngine` registration check and `CompiledWorkflowPlan.FromLegacy` fallback are
deleted with the superseded mixed-mode builder.

Selected mode also survives nested authoring. Public `EphemeralBranchScopeBuilder` /
`EphemeralBranchBuilder` and `DurableBranchScopeBuilder` / `DurableBranchBuilder` families
share internal machinery but expose only capabilities guaranteed for their mode. A method
cannot reappear inside `Parallel` merely because the implementation reused a common branch
class.

The structured-fiber prerequisite is satisfied and archived. Its compiler and driver are the
baseline: durable `ForEach` is absent from the public builder and compiler-rejected when
manually constructed; `ContinueAsNew` is a structural durable node executed only by a
quiescent root fiber. This change consolidates those delivered paths rather than introducing
parallel authoring or execution implementations.

Capability assignment begins with a positive allowlist:

- portable: `Init`, business step, `If`, `While`, supported structured composition, `Wait`, `Delay` where semantics are intentionally shared, `End`;
- ephemeral-only: in-instance `ForEach` and any host-local feature that only the ephemeral engine currently enforces;
- durable-only: `WaitLong`, children, external jobs, durable resource leases, continue-as-new, durable DAG/saga execution, and any supported definition-wide durable policy;
- host policy: per-step execution throttles are configured outside the definition in the initial baseline and apply only around business-step bodies;
- ephemeral-only for now: named cross-instance transient-pool authoring, because the ephemeral host enforces it and the durable builder cannot vary by later DI host selection;
- absent: features such as definition-wide retry that neither engine currently implements.

Saga authoring follows the same mode-first rule and produces distinct immutable
`EphemeralSagaDefinition<TState>` and `DurableSagaDefinition<TState>` types over one internal
representation. Ephemeral saga remains supported because it is an implemented, tested
application capability; durable saga authoring is exposed only when runtime-owned durable
progression exists. Older architecture notes that say the quick engine has no saga support
must be corrected or explicitly superseded.

Normal builders do not expose compiler-shaped `WithCompilerOptions` or
`WithTypeSerializerRegistry` methods. Optional application configuration is supplied as
`WorkflowAuthoringOptions` at the mode factory. Its application members are
`MaxStructuredDepth`, `MaxActiveExecutionPaths`, `MaxBranchResultPayloadBytes`, payload
serializer registration, state copier registration, and deterministic fingerprint
contributors. Structural-operations-per-turn and checkpoint-payload limits remain
engine/hosting settings. Raw
compiler options, compiler serializer registries, and compiled-plan fingerprint plumbing are
internal. This preserves legitimate custom serialization without teaching ordinary authors
the executable IR.

Alternative considered: keep one builder and set a `RequiresDurableEngine` flag. Rejected because it converts a compiler/discoverability problem into a registration failure and already misses ephemeral-only and silently ignored features.

Alternative considered: one builder with `.ForMode(...)` at build time. Rejected because unsupported methods remain discoverable until the end of authoring.

### 3. Keep the portable step result small and structural orchestration on builders

The common `IStep<TState>` result set retains only outcomes with intentionally portable semantics: success, expected failure, dynamic `WaitForEvent`, and cooperative yield. Dynamic `WaitForEvent` remains portable because both engines implement the case where the event name is selected only after business code runs; the structural `Wait` node is preferred when the event name is statically known because it enables earlier validation and visualization. `ContinueAsNew`, external-job dispatch, and durable resource acquisition leave common `StepResult`.

Durable orchestration is authored as durable nodes with state selectors for dynamic values:

```csharp
.RunExternalJob(
    id: state => $"risk/{state.OrderId}",
    payload: state => new RiskRequest(state.OrderId),
    resources: state => [ResourceLease.Require("risk-workers")],
    timeout: TimeSpan.FromMinutes(15))
.ContinueAsNew(state => state.NextGeneration())
```

This gives the authoring Module leverage: it validates placement and serialization requirements before execution and lets the durable driver own dispatch, waiting, deduplication, timeout, and resume. If a later use case proves that a step must select a durable effect dynamically, add a separate durable step contract; do not widen portable `StepResult` again.

`WithPoolKey` and `AcquireResources` are not aliases. The shared taxonomy, coordinated with `add-runtime-concurrency-limits`, has three categories:

1. **Per-step execution throttle**: host-local transient capacity held only around one step execution; configured as host policy in the initial public baseline, not as a builder capability that appears or disappears according to DI composition.
2. **Named cross-instance transient pool**: host-local shared capacity across workflow instances. It is authorable on the ephemeral builder because every supported ephemeral host enforces it. It remains absent from durable authoring until the durable implementation and every supported durable host enforce the same reset/re-admission semantics.
3. **Durable resource lease**: persisted cross-host capacity with queueing, expiry, recovery, and fiber/scope ownership; durable-only.

A durable lease releases deterministically when its owning scope exits normally, its branch is canceled, its scope fails, or the workflow terminates. Expiry is a crash-recovery backstop, not the normal release mechanism. The structured driver records and reconstructs these scope-owned obligations across restart.

Host options configure the capacities for transient throttle/pool keys and may disable a host
at startup when required configuration is missing. They do not change which methods exist on
a statically typed workflow builder. Adding durable transient-pool authoring later requires a
capability-matrix amendment plus durable-host acceptance evidence; it is not unlocked by a
runtime capability probe after a definition has already compiled.

Alternative considered: create durable subclasses of `StepResult`. Rejected as the default because an `IStep<TState>` returning the base type still permits a durable subtype to reach an ephemeral engine at runtime.

### 4. Deepen `DurableWorkflowRuntime` through focused submodules

`DurableWorkflowRuntime` remains the application facade, but it composes focused Modules instead of accumulating every operation as a flat method list:

```csharp
var orders = runtime.Definitions.Register(orderDefinition);
var started = await orders.StartOrGetAsync("orders/6001", input, ct);

var report = await runtime.ExternalJobs.CompleteAsync(
    started.InstanceId,
    externalJobId: "risk/order-6001",
    completionId,
    new RiskResult(...),
    ct);

var instance = runtime.Management.Instance(started.InstanceId);
var snapshot = await instance.GetAsync(ct);
var state = await instance.GetStateAsync<OrderState>(ct);
```

Registration is explicit and host-scoped. `runtime.Definitions.Register(definition)` returns a `DurableDefinitionHandle<TState>` that binds the state type and definition identity, so `StartOrGetAsync(key, input, ct)` infers the input type without a phantom `TState` generic. Start does not register a definition as a side effect. Starting through an unregistered identity returns a stable `DefinitionNotRegistered` application result or diagnostic. A payloadless event overload accepts an event name and routing key without forcing `RaiseEventAsync<object?>(..., null, ...)`.

The application facade owns:

- `TimeProvider` timestamps;
- command and event identity generation, while accepting optional caller idempotency keys where retry identity is meaningful;
- payload serialization and content type;
- raw command dispatch, committed outcome translation, and continuation handoff;
- operation-specific duplicate/no-op/rejection mapping;
- provider capability diagnostics.

Every accepted operation commits its protocol outcome and guarantees at-least-once continuation handoff through the continuation outbox. If the bound definition is registered locally, the facade may drive inline to a stable suspension or terminal point and returns `AppliedAndProgressed`. A definition-less callback/reporting host commits the outcome, enqueues continuation, and returns `AppliedPendingContinuation`; the definition-owning host's continuation pump performs the drive. The application promise is therefore guaranteed progression, not unconditional inline driving.

External jobs expose `CompleteAsync`, `FailAsync`, and `TimeoutAsync`. Completion and failure use the same caller-stable report/deduplication identity contract. Failure carries an application failure reason and optional payload, commits a distinct worker-failure protocol fact, and resumes the authored failure-policy path before timeout. A repeated failure report returns the stable duplicate result.

Application results use stable domain terms such as `Created`, `AppliedAndProgressed`, `AppliedPendingContinuation`, `Duplicate`, `AlreadyTerminal`, `NoMatch`, `AmbiguousMatch`, `TargetPaused`, or an explicit typed failure. Instance, definition, and correlation event routing treat no-match, ambiguous-match, unmatched-live-instance, and paused-target outcomes as expected typed results; only programming errors such as empty identifiers, blank event names, or invalid arguments throw. They do not expose `DurableCommandResult`, stream commit batches, poison internals, or protocol stream versions. Operational diagnostics still carry command identifiers and stream versions through logging/telemetry and the advanced protocol Interface.

Poison remediation belongs on the durable instance handle, not a root application signature containing `StreamVersion`. Diagnostics may issue an opaque application remediation ticket, or the handle may use a stable compare-and-act conflict contract. Raw stream-version rearm remains available only in `OrcaCore.Runtime.Protocol`. A stale ticket returns a stable conflict; an acknowledged current poison can be rearmed and guaranteed continuation handoff follows the same split-host rule.

`DurableWorkflowRuntime` construction is hosting-owned. A supported manual factory may accept application options and documented Adapters, but its public constructor does not require `DurableCommandProcessor`, definition registries, provider serializers, or driver observers. `DurableManagement` also becomes runtime-owned and never creates a fallback processor or independent mutation lane.

Alternative considered: add every `ProcessAsync` overload to the runtime. Rejected because it would reproduce the command processor as a shallow facade and make application callers understand protocol ordering.

### 5. Share management vocabulary and keep capability-specific handles

Create a shared query seam with two real Adapters, one ephemeral and one durable:

```text
IWorkflowManagement
  All()
  ForDefinition(id)
  Instance(id)

IWorkflowSelection
  Where(predicate)
  ListAsync(ct)
  CountAsync(ct)
  GetAsync(ct)
  GetActiveWaitsAsync(ct)
  StatisticsAsync(ct)

IWorkflowInstanceHandle
  GetAsync(ct)
  GetStateAsync<TState>(ct)
  CancelAsync(ct)
  TerminateAsync(ct)
```

Durable handles add pause/resume, history, archive metadata, and application-safe durable remediation. Ephemeral handles preserve explicitly in-memory operations: `EvictAsync`, `EvictTerminalAsync`, `DetectStuckAsync`, and `GetLifecycleEventsAsync`. These are named and documented as memory-retention and diagnostics capabilities, not as durable retention. Retention, resource-pool administration, and fleet-wide operator actions sit in focused durable management Modules rather than on the instance query itself.

All public query methods are asynchronous. The ephemeral Adapter may complete synchronously, but callers can use one shape without sync-over-async when switching to a provider-backed Adapter. A shared model does not exist yet: implementation consolidates the duplicate `WorkflowInstanceQueryModel`, `WorkflowStatistics`, and `WorkflowStatisticsGroup` declarations into one application-tier definition each, replaces the duplicate `DestructiveCommandSafety` declarations with the single `DestructiveOperationConfirmation` contract below, then deletes the engine-local copies. Both engines use those canonical query, snapshot, and statistics contracts unless a capability has intentionally different data.

Typed durable state is read from the committed execution checkpoint through the configured serializer and registered definition metadata. `GetStateAsync<TState>` returns a detached copy of the last committed **root workflow business state**. It never selects or returns branch-private or item-private fiber payloads. Ephemeral management uses the same registered serializer/deep-copy contract used for branch isolation and never returns the live in-memory state reference. Missing copier/serializer support, missing state, archive, incompatibility, or purge produces an explicit application diagnostic; it does not expose checkpoint DTOs.

Application active-wait snapshots describe authored facts: wait kind, immutable
`AuthoredLocation`, event name and correlation, residency, relevant logical timing, and opaque
`WaitId`. `AuthoredLocation` is the same structured path used by compiler diagnostics and is
stable for an unchanged authored graph. `WaitId` is an application targeting/diagnostic handle
without routing semantics. `FiberId`, `ScopeId`, and `WaitSequence` remain runtime
routing/ownership facts and are removed from the application projection. A certified custom
host can obtain them through an advanced runtime observation contract when operational
diagnosis genuinely requires them.

Normal application mutations own timestamps through the runtime clock. Explicit timestamps remain only on protocol commands and deterministic test fixtures.

Alternative considered: keep synchronous ephemeral queries because they are cheap. Rejected because the duplicate Interface has already drifted and prevents common tooling from moving between modes.

### 6. Make destructive intent impossible to confirm accidentally

Delete application overloads whose only behavior is to throw. Single-instance `TerminateAsync` needs no separate confirmation because selecting the instance is already explicit. Selection-wide termination and purge require a non-default confirmation value:

```csharp
public enum DestructiveOperationConfirmation
{
    None = 0,
    Confirmed = 1
}
```

The runtime rejects `None`, `default`, and invalid enum values. Authorization remains a host concern and is documented separately from an accidental-operation guard.

Alternative considered: retain throwing overloads as teaching aids. Rejected because an Interface member that can never succeed is misleading and increases the test surface without leverage.

### 7. Separate pure DAG planning from runtime-owned durable execution

The current `WorkflowDagRunner` is a pure planner; rename it accordingly or fold its behavior into `WorkflowDagPlan`. It may remain on the application Interface for visualization and validation. The current `DurableDagRunner` becomes internal or advanced. The durable runtime accepts a plan and root identity, reconstructs scheduled/completed/failed nodes from committed state, schedules ready children, and repeats after child outcomes without caller-supplied state sets.

Durable saga progression follows the same rule. The driver commits forward-action and compensation facts as part of executing a registered durable saga definition. `DurableSagaCommandAdapter` is deleted once that path exists; until then it is advanced and cannot be presented as the normal saga Interface.

Alternative considered: document the current runners as advanced. Rejected for normal use because deleting them would spread reconstruction, idempotency, and scheduling loops across every caller, showing that the Modules are currently too shallow.

### 8. Register engines explicitly and providers by role

Hosting entry points are explicit:

```csharp
services.AddOrcaCoreEphemeral();

services.AddOrcaCoreDurable();
services.AddOrcaCorePostgreSqlStore(connectionString);
services.AddOrcaCoreRabbitMqDispatcher(options);
services.AddOrcaCoreHostedServices();
```

`AddOrcaCore()` no longer silently registers both engines plus in-memory durable defaults. `AddOrcaCoreInMemoryDurable` is explicitly development/test-only, emits a startup diagnostic or warning that restart durability is not provided, and is excluded from production-readiness examples.

Provider extension names state their role:

- PostgreSQL / SQL Server: durable store;
- Redis: projection cache;
- RabbitMQ / ZeroMQ: dispatcher.

Conventions are standardized within a role: argument validation, options/factory overload patterns, ownership of supplied connections, replacement semantics, diagnostics, and return type. A role-specific difference is documented rather than hidden behind uniform naming.

Concrete hosted-service implementations become internal and are registered by factory or internal implementation type. Keep a public type only when an external caller has a documented supported reason to construct, derive from, or implement it. `RedisProviderProfile` fails the deletion test and is removed; deleting it removes complexity instead of moving it to callers.

Optional OpenTelemetry exporters move to focused integration packages. Base hosting retains telemetry instruments and extension hooks but does not force console, OTLP, and Prometheus exporter dependencies on applications that did not select them.

Alternative considered: use identical `AddOrcaCoreX` names for every provider. Rejected because stores, projection caches, and dispatchers are not substitutable Adapters at one seam.

### 9. Standardize builder validation without weakening aggregate diagnostics

All builders expose:

- `Build()` for the common success path, throwing one definition exception containing graph-wide diagnostics;
- `TryBuild()` returning the existing `Validation<TDefinition>` with the definition or all graph-wide validation errors for tooling.

Required delegates, names, durations, counts, and local enum values are non-nullable and validated when the fluent method is called. Cross-node concerns such as missing root terminals, duplicate names, cycles, reachability, illegal scope placement, and unsupported capability combinations remain aggregated at build time.

Strong IDs remain distinct types, but every application and advanced entry point validates that IDs are non-empty and versions are positive. `default(DefinitionId)`, `default(InstanceId)`, `Guid.Empty`-backed IDs, zero definition versions, and equivalent invalid protocol identities fail at the nearest public seam with stable diagnostics. Definition records expose immutable snapshots of authored metadata; executable factories and backing arrays are not publicly mutable or downcastable.

Alternative considered: defer every invalid value to build for maximum aggregation. Rejected because it lets an invalid local call travel far from its source and makes nullable required parameters part of the Interface.

### 10. Treat the public Interface as a first-class test surface

Add five mechanical guards:

1. approved public-type/signature baselines per application and advanced package;
2. compile-time authoring tests that prove mode-specific capability presence and absence;
3. a provider-author fixture that compiles with `OrcaCore.Provider.Abstractions` and its declared `OrcaCore.Runtime.Protocol` dependency but no engine implementation;
4. consumer projects that reference only the documented application/hosting/provider packages;
5. sample assertions that run the documented golden paths and fail on `Poisoned`, unsupported capability, or raw protocol use.

The application public baseline additionally asserts that `WorkflowInstanceQueryModel`, `WorkflowStatistics`, `WorkflowStatisticsGroup`, and `DestructiveOperationConfirmation` each have exactly one public declaration. Compile fixtures use the reconciled post-fiber signatures and include a split-host external-job journey where a definition-less callback host reports an outcome and a definition-owning host progresses it through the continuation pump.

Acceptance tests continue to exercise public application entry points. Kernel and provider tests may use advanced seams or matching `InternalsVisibleTo`; they cannot justify promoting an implementation type into the application Interface.

### 11. Treat archived fibers as the accepted substrate and gate only remaining public decisions

`adopt-structured-fiber-execution` is complete, promoted to canonical specs, and archived.
Its compiler, diagnostic families, typed structured signatures, durable `ForEach` rejection,
and root-only quiescent continue-as-new behavior are accepted substrate rather than remaining
tasks. Source work for this change begins after this post-fiber artifact rebase passes strict
validation and review approves the mode-specific definitions, nested builder split,
application/IR classification, and application wait/state projections.

`add-runtime-concurrency-limits` must agree on the three-way taxonomy and mode-guaranteed
discoverability before transient-governance source work proceeds. That active change blocks
durable transient-pool exposure, not unrelated authoring consolidation, package splitting, or
facade work.

Before each source slice, update any affected canonical `docs/specs/` requirement and
acceptance criteria for removed definition retry, the three pool categories, portable dynamic
waits, durable structural effects, explicit definition registration, split-host continuation,
external-job failure, event-routing outcomes, management timestamp ownership,
application-safe remediation, package tiers, root-state inspection, and application-safe wait
metadata. Final verification links every changed canonical requirement to its public
acceptance, compile, consumer, or architecture test.

## Risks / Trade-offs

- **[Public refactor accidentally rebuilds structured execution]** -> Treat the archived fiber compiler and driver as fixed substrate; consolidate and hide them without creating a second graph, compiler, or executor.
- **[One shared definition type preserves mode ambiguity]** -> Return distinct immutable ephemeral and durable definition types and make engine registration accept only its own application definition family.
- **[Common nested builders reintroduce capability leaks]** -> Preserve selected mode in every public branch/scope builder and compile-test both root and nested method absence.
- **[Hiding compiler IR removes useful configuration]** -> Replace compiler-shaped methods with domain-facing authoring limits, serializer/copy registration, and deterministic fingerprint contribution; keep executable plans internal.
- **[Definition-less callback host cannot drive inline]** -> Commit every accepted outcome with an at-least-once continuation outbox record and distinguish inline progression from pending continuation in application results.
- **[Implicit definition registration creates host-order bugs]** -> Make registration an explicit host-scoped operation that returns a typed definition handle; start never mutates registration state.
- **[Separate builders drift]** -> Share one internal graph/compiler Module and test portable semantics once against both builder Adapters.
- **[Facade grows shallow submodules]** -> Apply the deletion test to each submodule and expose it only when it hides command construction, ordering, idempotency, serialization, or progression.
- **[Advanced package split creates dependency cycles]** -> Add architecture tests for allowed project references and reject public application signatures containing advanced-package types.
- **[Async ephemeral queries add minor allocation/ceremony]** -> Prefer `ValueTask` only when measurement warrants it; consistency and provider substitution are the primary leverage.
- **[Typed durable state can be expensive or unavailable]** -> Read only on explicit request, document payload cost, and return capability/state-lifecycle diagnostics without leaking checkpoint records.
- **[Provider renaming creates many edits]** -> There are no external consumers; change all providers, samples, and tests atomically and add one role-convention certification base.
- **[Removing raw access blocks a legitimate host]** -> Keep certified runtime-protocol and provider-authoring seams in explicit advanced packages; require an executable custom-host scenario before expanding them.
- **[Public surface count falls but useful extension points disappear]** -> Judge each type by supported external scenario, not a numeric target. Public approval files make removals reviewable.

### 12. Amend canonical requirements immediately before each source section

Canonical requirements are not deferred to the documentation sweep. The amendment gate is:

| Source section | Canonical capabilities amended before its first source task |
|---|---|
| 4 - authoring and IR | `workflow-authoring`, `workflow-contracts`, `quality-and-verification` |
| 5 - durable effects and concurrency | `workflow-authoring`, `workflow-contracts`, `durable-runtime`, `runtime-resource-governance` |
| 6 - tiers and packages | `repository-foundation`, `developer-facing-surface`, `quality-and-verification` |
| 7 - facade and management | `durable-runtime`, `management-and-querying`, `developer-facing-surface` |
| 8 - DAG, saga, hosting, providers | `saga-orchestration`, `durable-runtime`, `developer-facing-surface`, provider-role capabilities |

The task graph records this map as a completed design gate. Contributors apply the listed
canonical amendments before the corresponding source section, not as a bulk retrofit in
section 9.

## Greenfield Implementation Plan

The executable phase order, verification ladder, report contents, and mandatory independent
review gate after every phase are defined in
[`docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`](../../../docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md).
The sequence below is the design-level summary; the linked plan controls implementation and
review boundaries while this task list remains the completion record.

1. Approve this post-fiber rebase: mode-specific definitions, nested mode preservation, compiler/fiber tier placement, root-state/wait projections, mode-guaranteed concurrency discoverability, and strict validation.
2. Capture the current public surface and add failing architecture, compile, consumer, split-host, and golden-path guards using only the reconciled signatures.
3. Consolidate delivered authoring first: introduce mode-specific definitions, split nested public builders, replace compiler-shaped options, and delete the superseded mixed-mode `WorkflowBuilder`, `RequiresDurableEngine`, and fallback plan path.
4. Add structural external-job and durable-lease nodes that lower directly to the archived fiber plan; only then remove durable variants from portable `StepResult`.
5. Introduce the application, provider-authoring, and runtime-protocol packages and move the already-classified contracts without carrying superseded public paths into new packages.
6. Deepen `DurableWorkflowRuntime` and management with explicit registration, typed handles, split-host continuation, complete external-job outcomes, typed routing, shared models, root-state inspection, and authored wait projections.
7. Move DAG and saga progression behind runtime-owned loops over the archived structured driver.
8. Split engine registration, normalize provider-role extensions, add ZeroMQ registration, internalize hosted loops, and delete shallow/test-only types; this can proceed after facade composition in parallel with DAG/saga closure.
9. Rewrite successful developer journeys and documentation against the final application surface.
10. Run public/package/sample guards, link canonical requirements to acceptance evidence, and prove every superseded surface is absent. No compatibility layer remains.

Rollback is source-level only: revert the change and reset development stores/fixtures. No released package, external client, or durable-instance compatibility contract exists.

## Open Questions

None. Package distribution uses separate tier packages plus a small `OrcaCore` meta-package, portable dynamic `WaitForEvent` is retained alongside preferred structural waits, and builders use `Build()` plus `TryBuild()` returning `Validation<TDefinition>`.
