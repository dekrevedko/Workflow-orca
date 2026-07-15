## Joint implementation baseline

[`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md)
is the only approved selected-mode capability matrix, public builder/result/merge signature
baseline, and compiler/diagnostic contract for this change and
`adopt-structured-fiber-execution`. It also fixes the distinct names and lifetimes shared
with `add-runtime-concurrency-limits`. Compile fixtures and source implementation SHALL use
that baseline directly and SHALL NOT preserve legacy overloads or introduce a second
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
| Common `WorkflowBuilder.ForEach` | Durable registration rejects the definition | Confirmed; make ephemeral-only |
| Common `RunChild` / `RunChildren` | Ephemeral registration rejects the definition | Confirmed; make durable-only |
| Common `WithDefinitionRetry` | Ephemeral registration rejects it and the durable engine never reads definition retry | Expanded; remove until specified and implemented |
| Common `WithPoolKey` | Enforced only by the ephemeral engine and silently ignored by durable execution | Expanded; make it an explicit execution-throttle capability |
| Common durable `StepResult` variants | Ephemeral execution throws only after the step returns one | Confirmed; remove from the portable result set |
| Internal `DurableWorkflowBuilder.WaitLong` | Test-only helper; no public durable authoring path | Confirmed specification violation |
| `OrcaCore.Abstractions` | 201 top-level public type declarations in the current source scan and 208 when nested public result variants are included; 77 top-level types are durable protocol types and 63 are provider types | Confirmed; split by supported seam rather than count alone |
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

The baseline OpenSpec already requires a separate durable authoring surface and shared management vocabulary, so the target is an alignment with existing product direction. The active `adopt-structured-fiber-execution` change overlaps authoring, `StepResult`, compiler validation, saga, DAG, and durable execution. Its structured execution model remains behind this change's application seam; both changes must share one selected-mode capability model.

There is no external compatibility obligation and no live durable-data migration requirement. Source, test fixtures, samples, and development stores may change together.

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

The concrete names may be `EphemeralWorkflowBuilder<TState>` and `DurableWorkflowBuilder<TState>` behind these factories, but callers must make the mode choice before IntelliSense exposes mode-specific methods. The two builders share one internal graph/compiler Module for portable nodes, validation codes, identities, and structured execution plans; they do not duplicate implementations.

This authoring surface is gated on `adopt-structured-fiber-execution`. Before either change edits source, both changes must publish one joint capability matrix, one compiler/diagnostic contract, and reconciled builder signature baselines using the post-fiber typed result and merge shapes. `Build()` produces a definition backed by that shared compiled plan. Durable `ForEach` is absent from the public builder and the compiler also rejects manually constructed durable `ForEach` nodes as defense in depth. `ContinueAsNew` is authored only as a structural durable node; execution is a root-fiber, quiescent transition and never originates from portable `StepResult`.

Capability assignment begins with a positive allowlist:

- portable: `Init`, business step, `If`, `While`, supported structured composition, `Wait`, `Delay` where semantics are intentionally shared, `End`;
- ephemeral-only: in-instance `ForEach` and any host-local feature that only the ephemeral engine currently enforces;
- durable-only: `WaitLong`, children, external jobs, durable resource leases, continue-as-new, durable DAG/saga execution, and any supported definition-wide durable policy;
- mode-dependent: per-step execution throttles and named cross-instance transient pools are exposed only by a selected host that implements their transient semantics;
- absent: features such as definition-wide retry that neither engine currently implements.

Saga authoring follows the same mode-first rule. Ephemeral saga remains supported because it is an implemented, tested application capability; durable saga authoring is exposed only when runtime-owned durable progression exists. Older architecture notes that say the quick engine has no saga support must be corrected or explicitly superseded.

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

1. **Per-step execution throttle**: host-local transient capacity held only around one step execution; portable only for a selected host that enforces it.
2. **Named cross-instance transient pool**: host-local shared capacity across workflow instances, exposed by supported ephemeral or durable hosts but never described as restart-durable.
3. **Durable resource lease**: persisted cross-host capacity with queueing, expiry, recovery, and fiber/scope ownership; durable-only.

A durable lease releases deterministically when its owning scope exits normally, its branch is canceled, its scope fails, or the workflow terminates. Expiry is a crash-recovery backstop, not the normal release mechanism. The structured driver records and reconstructs these scope-owned obligations across restart.

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

Typed durable state is read from the committed execution checkpoint through the configured serializer and registered definition metadata. Missing, archived, incompatible, or purged state produces an explicit application diagnostic; it does not expose checkpoint DTOs.

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

### 11. Gate source work on joint design and canonical requirements

No source implementation begins until this change and `adopt-structured-fiber-execution` both reference the same capability matrix, compiler diagnostic contract, builder signatures, and continue-as-new/ForEach semantics. `add-runtime-concurrency-limits` must also agree on the three-way pool taxonomy before any pool-shaped public member is authored. The gate is satisfied only when all three affected OpenSpec changes pass strict validation.

Before source changes, update the canonical `docs/specs/` requirements and acceptance criteria for removed definition retry, the three pool categories, portable dynamic waits, durable structural effects, explicit definition registration, split-host continuation, external-job failure, event-routing outcomes, management timestamp ownership, application-safe remediation, and package tiers. Final verification links every changed canonical requirement to its public acceptance, compile, consumer, or architecture test.

## Risks / Trade-offs

- **[Overlap with structured-fiber work]** -> Approve one shared capability matrix and authoring shape before applying either change; update both task graphs so the compiler is implemented once.
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

## Migration Plan

1. Complete the joint reconciliation gate: capability matrix, post-fiber builder signatures, three-way pool taxonomy, exact dependency graph, split-host continuation contract, closed design questions, canonical requirements, and strict validation of all three affected changes.
2. Capture the current public surface and add failing architecture, compile, consumer, split-host, and golden-path guards using only the reconciled signatures.
3. Introduce the application, provider-authoring, and runtime-protocol packages and move contracts without changing behavior.
4. Complete and validate the prerequisite structured-fiber compiler and driver core owned by `adopt-structured-fiber-execution`.
5. Add mode-first builders over that compiler, expose `WaitLong`, retain portable dynamic wait, remove unsupported features, and move durable effects out of common `StepResult`.
6. Deepen `DurableWorkflowRuntime` and management with explicit registration, typed handles, split-host continuation, complete external-job outcomes, typed routing, and shared models.
7. Move DAG and saga progression behind runtime-owned loops after the structured execution prerequisites are present.
8. Split engine registration, normalize provider-role extensions, add ZeroMQ registration, internalize hosted loops, and delete shallow/test-only types; this can proceed after facade composition in parallel with DAG/saga closure.
9. Rewrite successful developer journeys and documentation against the final application surface.
10. Run public/package/sample guards, link canonical requirements to acceptance evidence, and delete every superseded surface. No compatibility layer remains.

Rollback is source-level only: revert the change and reset development stores/fixtures. There is no released package or durable-instance migration path to preserve.

## Open Questions

None. Package distribution uses separate tier packages plus a small `OrcaCore` meta-package, portable dynamic `WaitForEvent` is retained alongside preferred structural waits, and builders use `Build()` plus `TryBuild()` returning `Validation<TDefinition>`.
