## Purpose

Define the v1 application surface, tier and package boundaries, explicit future-capability registry, and supported consumer journeys.

## Requirements

### Requirement: Public interfaces are tiered by caller role
The system SHALL separate application, provider-authoring, runtime-protocol, and internal implementation interfaces so a normal application dependency does not present provider commit records, kernel commands, checkpoints, driver pumps, fiber/scope identities, or concrete hosted loops as ordinary application concepts.

#### Scenario: Application author references OrcaCore
- **WHEN** an application references only the documented application or hosting package
- **THEN** normal authoring, registration, execution, and management compile without a provider-authoring or runtime-protocol reference

#### Scenario: Provider author implements an adapter
- **WHEN** a provider author intentionally references the provider-authoring package
- **THEN** provider ports and certification contracts are available with only the declared runtime-protocol dependency and no engine implementation dependency

### Requirement: First-release authoring is a complete positive allowlist
The application interface SHALL expose exactly the selected-mode members and signatures jointly approved by `docs/specs/17-selected-mode-capability-matrix.md` and its compile-shaped companion `docs/specs/17-public-authoring-contract.cs`. Concrete receiver names and generic arities SHALL match the companion. A member, overload, or public builder type outside that allowlist SHALL require a reviewed amendment to both artifacts before it appears in source, tests, samples, or packages.

#### Scenario: Consumer explores durable authoring
- **WHEN** a consumer inspects a durable root or nested builder
- **THEN** every discoverable member has approved durable semantics and no host composition step changes the statically available surface

#### Scenario: Provisional implementation already exists
- **WHEN** current source contains a member or overload absent from the approved matrix
- **THEN** implementation removes it rather than treating source existence as public approval

### Requirement: Deferred capabilities are documented without public placeholders
Public `RunExternalJob`, Saga, `WhenFirst`, public `RunChild` or `RunChildren`, nested `Parallel`, nested `While`, nested `ForEach`, durable lambda steps, definition-wide retry, failed-instance/step management retry, pause/resume/archive/purge, workflow-authored `Publish`/`Cancel`, and definition-targeted event fanout SHALL be absent from v1 public assemblies while remaining recorded with rationale and re-entry criteria in the future-capability registry. `WaitLong` and author `Yield` SHALL be removed concepts with no alias or tombstone.

#### Scenario: Public absence baseline is inspected
- **WHEN** reflection, compile, source, and package baselines inspect the first-release surface
- **THEN** no deferred or removed member, adapter, definition, empty implementation, obsolete alias, or reflection-visible placeholder is present

#### Scenario: Deferred work is reconsidered
- **WHEN** a contributor proposes one registered future capability
- **THEN** its required semantics are approved through a new amendment before any public signature or compatibility obligation is introduced

### Requirement: Durable application use is protocol-free
The ordinary application interface SHALL consist of explicit typed definition registration; resultless/resultful typed start and reopen handles; detached snapshot/root-state/output queries; cooperative cancellation request; fenced termination; and exact instance/correlation event delivery without requiring callers to construct durable commands, command timestamps, serialized payloads, checkpoints, fibers, scopes, or provider records. Workflow-instance enumeration, bulk selection/list/count/statistics, and deferred lifecycle operations SHALL be absent. Runtime and provider implementations MAY use internal keyed retrieval for event routing, reconciliation, and single-instance lookup, but that implementation capability SHALL NOT create a public enumeration or bulk-management facade.

#### Scenario: Durable workflow completes after an event
- **WHEN** an application registers a typed durable definition, starts it, and later delivers its awaited event
- **THEN** the facade owns serialization, identifiers, time, commit handling, deduplication, and local progression or at-least-once continuation handoff

#### Scenario: Callback host has no registered definition
- **WHEN** a definition-less host accepts a normalized event for a durable instance
- **THEN** it commits the event and continuation handoff without requiring raw command construction or pretending it can execute the definition locally

#### Scenario: Event arrives before its wait exists
- **WHEN** an instance or unique-correlation delivery finds no matching active wait
- **THEN** delivery returns `NoActiveWait`, does not consume or persist the `EventId` as deduplicated, and permits redelivery of the unchanged envelope after the wait exists

#### Scenario: Runtime resolves a correlation key
- **WHEN** event routing or reconciliation needs one instance or active wait
- **THEN** runtime-owned keyed retrieval MAY be used internally without exposing application enumeration, filtering, or bulk retrieval

### Requirement: Closed registration and start results retain a cast-free success path
Workflow registration SHALL return a closed `Registered`/`HostIncompatible`/`Conflict` result, while workflow start and DAG registration/start operations SHALL retain their closed accepted-or-conflict or registered-or-conflict results so callers can inspect failure values and `WasExisting`. `DefinitionHostCompatibilityFailure` SHALL be a closed union of `EngineModeMismatch(WorkflowMode HostMode, WorkflowMode DefinitionMode)`, `MissingTransientPools(IReadOnlyList<TransientPoolName>)`, and `MissingDurableResourcePools(IReadOnlyList<ResourcePoolName>)`; missing-name lists SHALL be copied, distinct, and ordinal-sorted. Registration SHALL validate mode first, statically inspectable pool references second, and fingerprint conflict third without mutation. `WorkflowRegistrationResult<TDefinitionHandle>` and `WorkflowStartResult<TInstanceHandle>` SHALL expose `GetHandleOrThrow()` returning the registered/accepted handle. Host incompatibility SHALL throw `WorkflowDefinitionHostCompatibilityException` with fixed code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the failure; conflict paths SHALL throw `WorkflowDefinitionRegistrationConflictException` carrying `DefinitionRegistrationConflict` and `WorkflowStartIdempotencyConflictException` carrying `StartIdempotencyConflict`, respectively. `DagRegistrationResult<TRunInput>` and `DagStartResult` SHALL expose the same-named success projection and SHALL throw `DagDefinitionRegistrationConflictException` or `DagStartIdempotencyConflictException` carrying the corresponding closed conflict value. The helpers SHALL NOT replace or hide the inspectable result unions.

#### Scenario: Caller wants ordinary success flow
- **WHEN** registration or start returns its success variant and the caller invokes `GetHandleOrThrow()`
- **THEN** the exact typed definition, instance, DAG-definition, or DAG-run handle is returned without a cast

#### Scenario: Caller wants conflict detail
- **WHEN** registration or start returns a conflict variant
- **THEN** the caller may inspect the union directly, while `GetHandleOrThrow()` throws the exact typed exception carrying that same immutable conflict value

#### Scenario: Workflow definition is incompatible with its host
- **WHEN** registration finds an engine-mode mismatch or missing statically inspectable transient/durable pools
- **THEN** it returns the copied closed `HostIncompatible` failure before registry mutation or fingerprint-conflict evaluation

### Requirement: Completion waits are notification-driven and race-free
`WorkflowInstanceHandle<TOutput>.WaitForOutputAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<TOutput>` containing the fixed-codec-detached output after successful completion or throw `WorkflowOutputUnavailableException` carrying terminal status and failure when the instance terminalizes without output. A same-named `ValueTask<TOutput>` convenience extension on `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` SHALL project through `GetHandleOrThrow()`. `DagRunHandle.WaitForTerminalAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<DagRunSnapshot>` containing the detached terminal snapshot. Durable implementations SHALL use subscribe-then-recheck around the committed notification boundary so completion cannot be lost; these operations SHALL NOT poll. Caller cancellation SHALL cancel only the local wait and SHALL NOT cancel the workflow instance or DAG run.

#### Scenario: Completion races wait subscription
- **WHEN** output or DAG terminal state commits immediately before, during, or after wait subscription
- **THEN** the wait observes the committed result exactly once without a polling interval or lost-notification window

#### Scenario: Caller abandons a local wait
- **WHEN** the wait cancellation token is cancelled
- **THEN** only that caller's wait ends and runtime execution remains governed by its separate cancellation API

### Requirement: Infrastructure integrations depend outward from OrcaCore
No OrcaCore application, engine, hosting, DAG, provider-authoring, or runtime-protocol package SHALL reference Kubernetes, AWS, job-scheduler application code, or their SDK types. A companion integration MAY remain in the same solution and SHALL depend outward on OrcaCore.

#### Scenario: Kubernetes scheduler is included in the solution
- **WHEN** a companion scheduler submits ordinary Kubernetes `batch/v1` Jobs or uses AWS-specific discovery
- **THEN** its manifests, clients, credentials, external identifiers, and SDK dependencies remain outside every OrcaCore public signature and dependency closure

#### Scenario: Scheduler uses durable safety contracts
- **WHEN** scheduler code performs a bounded create-or-observe call and protects external capacity
- **THEN** it uses generic `StepOperationId`, event delivery, `LeaseProtectionToken`, and trusted stop confirmation rather than adding a Kubernetes-specific OrcaCore node

### Requirement: Hosting registration selects execution mode explicitly
Microsoft hosting SHALL expose exactly `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`, `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`, callback-only `AddOrcaCoreDurableEventIngress()`, development/test `AddOrcaCoreInMemoryDurableProvider()`, production `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`, production `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`, and `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`. `OrcaCore.Engine.Ephemeral` SHALL own `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions`; `OrcaCore.Durable.Hosting` SHALL own `OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`; provider and DAG extensions SHALL remain in their owning package-specific classes. No public extension class SHALL be split across assemblies. Registration SHALL validate programmatically constructed typed options immediately and freeze an immutable owned copy, SHALL be idempotent only for the same role/options, and SHALL reject conflicting duplicates. Host options SHALL NOT claim direct `IConfiguration` binder compatibility and v1 SHALL expose no public binder DTO, converter, or configuration-section overload. It SHALL expose no catch-all `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, serializer replacement hook, or active superseded hosting scenario.

#### Scenario: Ephemeral host is configured
- **WHEN** a developer selects ephemeral hosting
- **THEN** the role registers the common definition registry, execution services, configured transient-pool catalog, and ephemeral `IWorkflowEventClient` routing while durable engine, store, protocol worker, and reconciliation services are absent

#### Scenario: Durable host is configured
- **WHEN** a developer selects durable hosting with one complete certified provider role set
- **THEN** the role registers the durable definition registry, progression loops, and durable `IWorkflowEventClient` routing including ingress and continuation handoff

#### Scenario: Both engines are selected in one service provider
- **WHEN** one `IServiceProvider` is configured with both ephemeral and durable engine roles
- **THEN** registration/startup fails deterministically instead of selecting a mode implicitly or exposing ambiguous registry/event ownership

#### Scenario: Definition names pools absent from the host
- **WHEN** an ephemeral definition names one or more unconfigured transient pools or a durable definition contains one or more unconfigured static lease requests
- **THEN** registration returns the complete sorted `HostIncompatible` missing-pool failure before mutation; selector-created durable names remain runtime validation

#### Scenario: In-memory durable host is configured
- **WHEN** a developer selects the durable engine plus `AddOrcaCoreInMemoryDurableProvider`
- **THEN** startup and documentation identify it as development/test-only with no restart-durability claim

#### Scenario: Callback-only ingress is configured
- **WHEN** a definition-less callback host calls `AddOrcaCoreDurableEventIngress` with one complete certified durable provider role set
- **THEN** it receives durable event persistence and continuation handoff but no definition registry, execution worker, timer/reconciliation loop, or DAG coordinator

#### Scenario: Durable ingress is redundantly composed with an engine
- **WHEN** callback-only durable ingress and an engine role are registered in one service provider
- **THEN** startup rejects the conflicting ownership because the durable engine already owns durable ingress and the callback role is definition-less

#### Scenario: DAG hosting is configured
- **WHEN** a durable-engine host calls `AddOrcaCoreDag`
- **THEN** only the DAG coordinator and registry are added through `OrcaCore.Dag.Hosting`, and absence of the durable-engine role fails startup

#### Scenario: Superseded hosting composition is scanned
- **WHEN** active documentation, fixtures, samples, or tests are inspected
- **THEN** none invokes catch-all `AddOrcaCore`, `AddOrcaCoreHostedServices`, a combined engine role, or another superseded registration path

### Requirement: Provider registration names the adapter role
Every provider package SHALL identify whether it supplies a complete durable role set, projection cache, dispatcher, or another documented role and SHALL follow common validation, ownership, replacement, diagnostics, and certification conventions within that role. `OrcaCore.Providers.PostgreSql` and `OrcaCore.Providers.SqlServer` SHALL each supply one complete independently certified production durable role set. PostgreSQL SHALL own `OrcaCore.Providers.PostgreSql.OrcaCorePostgreSqlProviderServiceCollectionExtensions.AddOrcaCorePostgreSqlDurableProvider(IServiceCollection, PostgreSqlDurableProviderOptions)`; SQL Server SHALL own `OrcaCore.Providers.SqlServer.OrcaCoreSqlServerProviderServiceCollectionExtensions.AddOrcaCoreSqlServerDurableProvider(IServiceCollection, SqlServerDurableProviderOptions)`. Each options type SHALL expose only get-only `ConnectionString` and `Schema` fixed by programmatic construction; registration SHALL copy the options and reject null, empty, or whitespace values before registering any provider service. No parallel connection-string overload, configuration-binding overload, partial-role interpretation, shared Relational facade, or cross-provider extension owner SHALL exist.

#### Scenario: PostgreSQL durable provider is registered
- **WHEN** a production host supplies nonblank connection string and schema values
- **THEN** one call registers the complete certified durable provider role set without requiring application registration of individual ports

#### Scenario: PostgreSQL provider options are invalid
- **WHEN** the options object or either required textual value is null, empty, or whitespace
- **THEN** the registration call rejects before any partial provider services are visible

#### Scenario: SQL Server durable provider is registered
- **WHEN** a production host supplies nonblank SQL Server connection string and schema values
- **THEN** one call registers the complete independently certified durable provider role set without requiring application registration of individual ports

#### Scenario: SQL Server provider options are invalid
- **WHEN** the options object or either required textual value is null, empty, or whitespace
- **THEN** the registration call rejects before any partial provider services are visible

### Requirement: Public types require a supported external scenario
A production type SHALL remain public only when an application, provider author, or custom host has a documented supported reason to construct, implement, derive from, parse, or name it in a public signature.

#### Scenario: Hosting owns a background loop
- **WHEN** base hosting registers a timer, continuation, reconciliation, or operational sweep implementation
- **THEN** the implementation type remains internal unless a supported replacement or construction scenario is documented and tested

### Requirement: Strong identity and matching contracts remain distinct
The application interface SHALL use non-defaultable validating reference `DefinitionId`/`DefinitionVersion`, immutable validated caller-created `EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `DagNodeId`, `ResourcePoolName`, `TransientPoolName`, `StartIdempotencyKey`, `CorrelationId`, `EventId`, `StopConfirmationId`, `ResourcePoolOperationId`, `ResourceGovernancePartitionId`, and runtime-created identities at their approved seams. Every caller-created string-backed value SHALL have a private constructor and one `Create(string)` factory; runtime-created identities SHALL not expose that factory. The interface SHALL offer no public constructor, implicit primitive conversion, or parallel primitive overload for the caller-created family; persistence equality for textual matching SHALL be exact ordinal and case-sensitive. `DefinitionId.New()` SHALL never return `Guid.Empty`; `DefinitionId.Parse` SHALL reject its canonical text with `ArgumentException`, and `TryParse` SHALL return `false` with a null result. The same nonempty GUID parser rule SHALL apply to public runtime-created `InstanceId`, `WaitId`, and `DagRunId`.

#### Scenario: Consumer swaps two matching roles
- **WHEN** consumer code passes a transient-pool name as a durable resource pool, an event name as an outcome, or an author string as runtime operation identity
- **THEN** the normal application call does not compile

#### Scenario: Empty GUID identity is parsed
- **WHEN** a caller parses the canonical `Guid.Empty` text as `DefinitionId`, `InstanceId`, `WaitId`, or `DagRunId`
- **THEN** `Parse` throws `ArgumentException`, `TryParse` returns `false` with null, and no boundary can persist the value

### Requirement: Public failures carry stable codes
Every public runtime failure SHALL derive from `OrcaCoreException` and expose a nonblank stable `Code`. Each built-in failure class SHALL bind one fixed code. Normalized author or integration exceptions SHALL use one documented generic code; raw exception messages and CLR type names SHALL remain diagnostic text rather than protocol identity. Result-to-exception helpers SHALL preserve their closed conflict or failure value as structured exception data.

#### Scenario: Failure is persisted and projected
- **WHEN** a built-in or normalized application failure crosses a durable, DAG, management, or build boundary
- **THEN** its stable nonblank code survives while mutable message wording and CLR type names do not become matching keys

### Requirement: Package distribution preserves tier boundaries
`OrcaCore` SHALL be the primary application contracts/authoring package and SHALL NOT be a meta-package. The exact first-release package manifest SHALL contain `OrcaCore`, `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`, `OrcaCore.Engine.Durable`, `OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`, `OrcaCore.Providers.PostgreSql`, `OrcaCore.Providers.SqlServer`, `OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`. `OrcaCore` SHALL own ordinary application values, authoring, definitions/references, handles, events, and result/error contracts without an engine, advanced, provider, DAG, Kubernetes, or AWS dependency. Optional integrations SHALL require their explicit package.

#### Scenario: Application references OrcaCore
- **WHEN** a developer references only the `OrcaCore` package
- **THEN** application contracts and authoring are available without silently selecting an engine, provider, DAG, Kubernetes, AWS, or scheduler integration

#### Scenario: Advanced implementer chooses an explicit package
- **WHEN** a provider author or custom host needs an advanced service-provider interface
- **THEN** they reference that package intentionally and do not receive an engine implementation dependency

### Requirement: DAG authoring makes no visualization promise
`OrcaCore.Dag` SHALL expose typed immutable DAG input, node references, direct dependency mapping, validation, and execution-facing plans. V1 SHALL expose no visualization renderer, visualization package, or visualization-specific public projection. Adding visualization SHALL require a separately reviewed immutable node/edge projection that does not expose executable mapper delegates.

#### Scenario: DAG package surface is inspected
- **WHEN** a consumer references `OrcaCore.Dag`
- **THEN** typed planning and validation are present while no visualization claim or renderer is part of the first-release contract

### Requirement: Application values use one fixed certified codec
The application/runtime boundary SHALL use the non-replaceable certified `System.Text.Json` format `orcacore-json-v1` for durable values, selector snapshots, idempotency bytes, and detached query projections. Unsupported cyclic or unapproved polymorphic shapes SHALL fail before commit, and no ordinary hosting hook SHALL replace the codec.

#### Scenario: Caller mutates a submitted value
- **WHEN** workflow input, event payload, item list, or returned state/output is copied across an application boundary
- **THEN** codec round-trip detaches the committed/returned value so later caller mutation cannot change runtime bytes
