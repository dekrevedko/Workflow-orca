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
Public `RunExternalJob`, Saga, `WhenFirst`, public `RunChild` or `RunChildren`, nested `Parallel`, nested `While`, nested `ForEach`, durable lambda steps, definition-wide retry, failed-instance/step management retry, pause/resume/archive/purge, and workflow-authored `Cancel` SHALL be absent from v1 public assemblies while remaining recorded with rationale and re-entry criteria in the future-capability registry at `docs/specs/13-phasing-and-open-questions.md` §13.4 ("Future-capability registry"). Durable workflow-authored `Publish` and explicit definition-fanout routing SHALL exist only through the specified durable inbox/outbox contract and SHALL NOT revive any obsolete raw publish/fanout command or provider-shaped API. `WaitLong` and author `Yield` SHALL be removed concepts with no alias or tombstone.

#### Scenario: Public absence baseline is inspected
- **WHEN** reflection, compile, source, and package baselines inspect the first-release surface
- **THEN** no deferred or removed member, adapter, definition, empty implementation, obsolete alias, or reflection-visible placeholder is present

#### Scenario: Deferred work is reconsidered
- **WHEN** a contributor proposes one registered future capability
- **THEN** its required semantics are approved through a new amendment before any public signature or compatibility obligation is introduced

### Requirement: Durable application use is protocol-free
The ordinary application interface SHALL consist of independently declared immutable typed definitions/references; explicit application-configuration registration; exact-reference handle lookup; resultless/resultful typed start and reopen handles; detached snapshot/root-state/output queries; cooperative cancellation request; fenced termination; and durable self-routing event acceptance without requiring callers to construct durable commands, command timestamps, serialized provider payloads, checkpoints, fibers, scopes, wait identities, or provider records. Workflow-instance enumeration, bulk selection/list/count/statistics, and deferred lifecycle operations SHALL be absent. Runtime and provider implementations MAY use internal keyed retrieval and committed target snapshots for event routing, reconciliation, fanout, and single-instance lookup, but that implementation capability SHALL NOT create a public enumeration or bulk-management facade.

#### Scenario: Durable workflow completes after an event
- **WHEN** an application stages a typed durable definition during host composition, starts it through its exact registered reference, and later accepts its awaited self-routing event
- **THEN** the facade owns serialization, identifiers, time, inbox/outbox commit handling, deduplication, and local progression or at-least-once continuation handoff

#### Scenario: Callback host has no registered definition
- **WHEN** a definition-less host accepts a normalized event for a durable instance
- **THEN** it durably owns the complete envelope and route intent and records any continuation handoff without requiring raw command construction or pretending it can execute the definition locally

#### Scenario: Event arrives before its wait exists
- **WHEN** a valid direct or correlation event is accepted before its matching durable wait is registered
- **THEN** the event remains durably pending, `NoActiveWait` is not returned, and an atomically racing or later matching wait claims it in durable acceptance order

#### Scenario: Matching instance is cold
- **WHEN** an accepted event matches a persisted wait owned by an instance that is not resident in memory
- **THEN** a definition-owning pump loads the exact bound definition and checkpoint, makes the instance runnable, and applies the event without requiring broker redelivery

### Requirement: Closed registration and start results retain a cast-free success path
Workflow registration SHALL return a closed `Registered`/`HostIncompatible`/`Conflict` result, while workflow start and DAG registration/start operations SHALL retain their closed accepted-or-conflict or registered-or-conflict results so callers can inspect failure values and `WasExisting`; the DAG registration and start results SHALL additionally carry the closed `RunInputConflict` variants defined below. `DefinitionHostCompatibilityFailure` SHALL be a closed union of `EngineModeMismatch(WorkflowMode HostMode, WorkflowMode DefinitionMode)`, `MissingTransientPools(IReadOnlyList<TransientPoolName>)`, `MissingDurableResourcePools(IReadOnlyList<ResourcePoolName>)`, and `MissingWorkflowEventDispatcher`; missing-name lists SHALL be copied, distinct, and ordinal-sorted. Registration SHALL validate mode first, statically inspectable host capabilities second, and fingerprint conflict third without mutation. `WorkflowRegistrationResult<TDefinitionHandle>` and `WorkflowStartResult<TInstanceHandle>` SHALL expose `GetHandleOrThrow()` returning the registered/accepted handle. The common registry SHALL additionally expose four exact typed `GetRequiredHandle` overloads for already registered ephemeral/durable resultless/resultful references; lookup SHALL never register and an absent or stale exact reference SHALL throw `WorkflowDefinitionNotRegisteredException` with code `WF-DEFINITION-NOT-REGISTERED` carrying the requested reference's `DefinitionId`, `DefinitionVersion`, and `DefinitionFingerprint` as immutable structured properties. Host incompatibility SHALL throw `WorkflowDefinitionHostCompatibilityException` with fixed code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the failure; conflict paths SHALL throw `WorkflowDefinitionRegistrationConflictException` carrying `DefinitionRegistrationConflict` and `WorkflowStartIdempotencyConflictException` carrying `StartIdempotencyConflict`, respectively. `DagRegistrationResult<TRunInput>` and `DagStartResult` SHALL expose the same-named success projection and SHALL throw `DagDefinitionRegistrationConflictException` or `DagStartIdempotencyConflictException` carrying the corresponding closed conflict value, or `DagRunInputContractMismatchException` for a `RunInputConflict` variant. The helpers SHALL NOT replace or hide the inspectable result unions.

`DagRegistrationResult<TRunInput>` SHALL be the closed union of `Registered(DagDefinitionHandle<TRunInput> Handle)`, `Conflict(DefinitionRegistrationConflict Error)`, and `RunInputConflict(DefinitionId DefinitionId, DefinitionVersion DefinitionVersion, DefinitionFingerprint DefinitionFingerprint, Type ExistingRunInputType, Type AttemptedRunInputType)`. `DagStartResult` SHALL be the closed union of `Accepted(DagRunHandle Handle, bool WasExisting)`, `Conflict(StartIdempotencyConflict Error)`, and `RunInputConflict(DagRunId ExistingRunId, DefinitionId DefinitionId, DefinitionVersion DefinitionVersion, DefinitionFingerprint DefinitionFingerprint, Type AttemptedRunInputType)`. A `RunInputConflict` SHALL report that the definition identity, version, and structural fingerprint are equal while the declared run-input contract differs, and SHALL be returned, never thrown, by `Register` and `StartOrGetAsync`; the start variant SHALL retain the run id of the run that owns the start key. `GetHandleOrThrow()` on either variant SHALL throw `DagRunInputContractMismatchException` with stable code `DAG-RUN-INPUT-CONTRACT-MISMATCH` carrying the definition identity, version, fingerprint, attempted run-input type, the existing run-input type when it is known on that host, and the existing run id when one exists.

#### Scenario: Caller wants ordinary success flow
- **WHEN** registration or start returns its success variant and the caller invokes `GetHandleOrThrow()`
- **THEN** the exact typed definition, instance, DAG-definition, or DAG-run handle is returned without a cast

#### Scenario: Caller wants conflict detail
- **WHEN** registration or start returns a conflict variant
- **THEN** the caller may inspect the union directly, while `GetHandleOrThrow()` throws the exact typed exception carrying that same immutable conflict value

#### Scenario: Workflow definition is incompatible with its host
- **WHEN** registration finds an engine-mode mismatch, missing statically inspectable transient/durable pools, or a durable `Publish` without an application dispatcher
- **THEN** it returns the copied closed `HostIncompatible` failure before registry mutation or fingerprint-conflict evaluation

#### Scenario: Registered reference is resolved
- **WHEN** a caller requests the handle for an exact typed reference already installed by application configuration
- **THEN** the matching typed handle is returned without registration, a cast, keyed DI, or a second workflow client surface

#### Scenario: Registered reference is absent or stale
- **WHEN** no installed definition matches the reference's exact mode, identity, version, fingerprint, and typed contract
- **THEN** lookup throws `WorkflowDefinitionNotRegisteredException` carrying the requested definition id, version, and fingerprint and starts or registers nothing

#### Scenario: DAG registration or start meets another run-input contract
- **WHEN** DAG registration or start finds an equal definition identity, version, and structural fingerprint under another declared run-input contract
- **THEN** it returns the closed `RunInputConflict` variant for inspection, the start variant carries the run id of the owner of the start key, and `GetHandleOrThrow()` throws `DagRunInputContractMismatchException` carrying the same values

### Requirement: Completion waits are notification-driven and race-free
`WorkflowInstanceHandle<TOutput>.WaitForOutputAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<TOutput>` containing the fixed-codec-detached output after successful completion or throw `WorkflowOutputUnavailableException` carrying terminal status and failure when the instance terminalizes without output. A same-named `ValueTask<TOutput>` convenience extension on `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` SHALL project through `GetHandleOrThrow()`. `DagRunHandle.WaitForTerminalAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<DagRunSnapshot>` containing the detached terminal snapshot, or throw `DagRunFaultedException` while the run is in the nonterminal integrity-fault state as the `management-and-querying` DAG terminal-wait requirement defines. Durable implementations SHALL use subscribe-then-recheck around the committed notification boundary so completion cannot be lost; these operations SHALL NOT poll. Caller cancellation SHALL cancel only the local wait and SHALL NOT cancel the workflow instance or DAG run.

Logical output/terminal completion SHALL remain separate from physical settlement.
The new WorkflowInstanceHandle.WaitForSettlementAsync(CancellationToken) SHALL be
available on resultless and inherited resultful handles and SHALL NOT change any
existing output wait, DAG wait, timeout or cancellation contract. Ephemeral output
waits SHALL expose an eligible original step exception only through the existing
wrapper's InnerException; WorkflowFailure.ExceptionType SHALL remain detached text.
Only the two exact members in this change's approved catalogue SHALL be added.
The normative declaration companion SHALL change in the reviewed canonical and
numbered-document transition. Executable public-API/compile/package baselines
SHALL change with actual source, never during a contract or canonical-only target.

#### Scenario: Completion races wait subscription
- **WHEN** output or DAG terminal state commits immediately before, during, or after wait subscription
- **THEN** the wait observes the committed result exactly once without a polling interval or lost-notification window

#### Scenario: Caller abandons a local wait
- **WHEN** the wait cancellation token is cancelled
- **THEN** only that caller's wait ends and runtime execution remains governed by its separate cancellation API

#### Scenario: A timed-out physical step keeps running
- **WHEN** output waiting reports TimedOut before the noncooperative body and its ownership handoffs finish
- **THEN** the independent settlement wait remains pending and existing output waiting does not silently become a physical join

#### Scenario: DAG wait observes a faulted run
- **WHEN** an authoritative recheck of the DAG wait, whether the first or one following a committed-change notification, observes the integrity-fault state
- **THEN** the wait ends with `DagRunFaultedException` without polling and without returning a snapshot that claims a terminal result, while a fault that was reconciled before the recheck is not reported and the wait continues

### Requirement: Infrastructure integrations depend outward from OrcaCore
No OrcaCore application, engine, hosting, DAG, provider-authoring, or runtime-protocol package SHALL reference Kubernetes, AWS, job-scheduler application code, or their SDK types. A companion integration MAY remain in the same solution and SHALL depend outward on OrcaCore.

#### Scenario: Kubernetes scheduler is included in the solution
- **WHEN** a companion scheduler submits ordinary Kubernetes `batch/v1` Jobs or uses AWS-specific discovery
- **THEN** its manifests, clients, credentials, external identifiers, and SDK dependencies remain outside every OrcaCore public signature and dependency closure

#### Scenario: Scheduler uses durable safety contracts
- **WHEN** scheduler code performs a bounded create-or-observe call and protects external capacity
- **THEN** it uses generic `StepOperationId`, event delivery, `LeaseProtectionToken`, and trusted stop confirmation rather than adding a Kubernetes-specific OrcaCore node

### Requirement: Hosting registration selects execution mode explicitly
Microsoft hosting SHALL expose exactly `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`, `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`, callback-only `AddOrcaCoreDurableEventIngress()`, development/test `AddOrcaCoreInMemoryDurableProvider()`, production `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`, production `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`, and `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)` as role-selection entries. The engine entries SHALL return `OrcaCoreEphemeralEngineBuilder` and `OrcaCoreDurableEngineBuilder`; each builder's two resultless/resultful `AddWorkflow` overloads SHALL accept only already-built definitions for that mode and return the same builder for composition. `OrcaCore.Engine.Ephemeral` SHALL own `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions` and the ephemeral builder; `OrcaCore.Durable.Hosting` SHALL own `OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`, the durable builder, `IWorkflowEventIngress`, and `IWorkflowEventDispatcher`; provider and DAG extensions SHALL remain in their owning package-specific classes. No public extension class SHALL be split across assemblies. Registration SHALL validate programmatically constructed typed options immediately and freeze an immutable owned copy, SHALL be idempotent only for the same role/options, and SHALL reject conflicting duplicates. Before readiness or any progression loop starts, the engine SHALL preflight the complete staged workflow batch and install it atomically; exact duplicate identity/version/fingerprint entries SHALL be idempotent and any incompatibility/conflict SHALL leave the registry unchanged and start no loop. Host options SHALL NOT claim direct `IConfiguration` binder compatibility and v1 SHALL expose no public binder DTO, converter, configuration-section overload, catch-all `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, serializer replacement hook, workflow/event assembly scan, or active superseded hosting scenario.

#### Scenario: Ephemeral host is configured
- **WHEN** a developer selects ephemeral hosting
- **THEN** the returned builder stages only ephemeral definitions and the role registers the common definition registry, execution services, and configured transient-pool catalog while durable ingress, durable publish/outbox, store, protocol worker, and reconciliation services are absent

#### Scenario: Durable host is configured
- **WHEN** a developer selects durable hosting with one complete certified provider role set
- **THEN** the returned builder stages only durable definitions and the role registers the durable definition registry, self-routing ingress, pending-inbox matching, progression loops, and optional configured external-event outbox dispatch

#### Scenario: Both engines are selected in one service provider
- **WHEN** one `IServiceProvider` is configured with both ephemeral and durable engine roles
- **THEN** registration/startup fails deterministically instead of selecting a mode implicitly or exposing ambiguous registry/event ownership

#### Scenario: Definition names pools absent from the host
- **WHEN** an ephemeral definition names one or more unconfigured transient pools or a durable definition contains one or more unconfigured static lease requests
- **THEN** registration returns the complete sorted `HostIncompatible` missing-pool failure before mutation; selector-created durable names remain runtime validation

#### Scenario: Application module stages workflows
- **WHEN** application configuration calls `AddWorkflow` directly or through an application-owned grouping extension
- **THEN** the exact immutable definitions are explicit in the composition root and no reflection, attribute discovery, registrar class, or environment-dependent graph construction occurs

#### Scenario: Staged definition batch conflicts
- **WHEN** startup preflight finds a wrong-mode definition, missing static host capability, or the same identity/version with another fingerprint
- **THEN** no staged definition is installed and no worker, timer, continuation, inbox, outbox, or DAG loop starts

#### Scenario: In-memory durable host is configured
- **WHEN** a developer selects the durable engine plus `AddOrcaCoreInMemoryDurableProvider`
- **THEN** startup and documentation identify it as development/test-only with no restart-durability claim

#### Scenario: Callback-only ingress is configured
- **WHEN** a definition-less callback host calls `AddOrcaCoreDurableEventIngress` with one complete certified durable provider role set
- **THEN** it receives durable self-routing event and pending-start-intent persistence plus continuation handoff, but no workflow builder, definition registry, reference lookup, execution worker, timer/reconciliation loop, external outbox dispatcher, or DAG coordinator

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

### Requirement: Implementation package boundaries use exact internal friends
Compiler, execution-kernel, concrete engine, provider, and hosted-loop implementation types SHALL remain internal even when another first-release implementation package consumes them. Cross-assembly implementation access SHALL use only the exact reviewed product friends `OrcaCore` to `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and `OrcaCore.Dag` for application-owned internal authoring/runtime contracts; `OrcaCore.Core` to `OrcaCore.Engine.Ephemeral` and `OrcaCore.Engine.Durable`; `OrcaCore.Engine.Durable` to `OrcaCore.Durable.Hosting`; `OrcaCore.Durable.Hosting` to `OrcaCore.Dag.Hosting`; and `OrcaCore.Dag` to `OrcaCore.Dag.Hosting` for the internal runtime view. The `OrcaCore -> OrcaCore.Dag` grant SHALL be used only for the internal constructors of `Validation<T>`, `WorkflowDiagnostic`, `AuthoredLocation`, `DefinitionFingerprint`, and `WorkflowDefinitionException`, plus one internal canonical-structure hash operation owned by `DefinitionFingerprint`; the exact signatures SHALL be pinned. A compiled-metadata guard SHALL reject every other non-public `OrcaCore` type or member referenced by `OrcaCore.Dag.dll`, including runtime, child-start, engine, hosting, and protocol internals. DAG diagnostics SHALL use the existing `WorkflowDiagnosticCatalog` and `Validation<T>` ordering, and DAG fingerprints SHALL use the same internal UTF-8/SHA-256 operation as workflow compilation without changing existing workflow hashes. The `OrcaCore -> OrcaCore.Dag` authoring grant SHALL NOT add a package reference or give `OrcaCore.Dag` durable child-start access; `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` remains the sole DAG-to-durable runtime bridge. Exact owning white-box test friends and the durable provider-certification barrier friend MAY inspect internals; acceptance, behavior-scenario, compile-fixture, and integration assemblies SHALL NOT. No other friend, public reflection bridge, or exported test helper SHALL exist. Friend grants SHALL NOT create reverse package references.

The `OrcaCore.Dag -> OrcaCore.Dag.Hosting` runtime-view grant SHALL expose only `WorkflowDagPlan<TRunInput>.GetRuntimeView()`, `DagRuntimeView<TRunInput>.Nodes`, `DagRuntimeView<TRunInput>.EvaluateMapping(DagNodeRef, TRunInput, IReadOnlyDictionary<DagNodeRef, object?>)`, and only the twelve descriptor/result getters enumerated below. Its only non-public DAG type references SHALL be `DagRuntimeView<TRunInput>`, `DagRuntimeNodeDescriptor`, and `DagMappedInputResult`; constructors, setters, draft types, raw node plans, mapper delegates, and every other non-public DAG type/member/overload SHALL be forbidden, except the two facade-handle constructors named below. The durable bridge SHALL decode successful committed dependency outputs using their declared output types before the evaluator receives them; Dag SHALL NOT decode bytes. The evaluator SHALL accept runtime-supplied immutable run input and successful direct-dependency outputs, enforce the existing `OutputOf` access rules, and return the typed mapped input with its declared type or the stable `DAG_INPUT_MAPPING_INVALID` failure before input commit or child start. A successful null output SHALL be distinguished from a missing output by presence in the successful-output map and SHALL be valid only for a reference or nullable-value declared type; successful null mapped input SHALL follow the same type rule. C# nullable-reference annotations SHALL NOT be treated as a runtime discriminator. The adapter SHALL NOT receive mapping delegates or mutable authoring drafts. A compiled-metadata guard SHALL inspect `OrcaCore.Dag.Hosting.dll` and reject every non-public `OrcaCore.Dag` type or member reference outside the exact runtime-view signatures and the two facade-handle constructors, including type references in signatures, base types, implemented interfaces, generic arguments, and attributes. Runtime-view behavior SHALL be exercised through Dag.Hosting-level behavior without a new test friend. Fixed-codec normalization, dependency-output materialization, committed-byte fingerprinting, persistence, and child-start/join SHALL remain owned by the durable runtime and its existing hosting bridge; the six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist SHALL NOT gain codec or runtime access. The runtime view SHALL add no public surface, package edge, reflection bridge, or additional friend beyond the named `OrcaCore.Dag -> OrcaCore.Dag.Hosting` proposal; the facade-handle grant below reuses that same friend edge.

The exhaustive proposed signature contract SHALL be exactly the following, with no additional type or member access:

```csharp
// On the existing public WorkflowDagPlan<TRunInput>:
internal DagRuntimeView<TRunInput> GetRuntimeView();

internal sealed class DagRuntimeView<TRunInput>
{
    internal IReadOnlyList<DagRuntimeNodeDescriptor> Nodes { get; }
    internal DagMappedInputResult EvaluateMapping(
        DagNodeRef node,
        TRunInput immutableRunInput,
        IReadOnlyDictionary<DagNodeRef, object?> successfulDirectDependencyOutputs);
}

internal sealed class DagRuntimeNodeDescriptor
{
    internal DagNodeRef Reference { get; }
    internal int AuthoredOrdinal { get; }
    internal DefinitionId ChildDefinitionId { get; }
    internal DefinitionVersion ChildDefinitionVersion { get; }
    internal DefinitionFingerprint ChildFingerprint { get; }
    internal Type InputType { get; }
    internal Type? OutputType { get; }
    internal IReadOnlyList<DagNodeRef> Dependencies { get; }
}

internal sealed class DagMappedInputResult
{
    internal bool IsValid { get; }
    internal object? Input { get; }
    internal Type? InputType { get; }
    internal string? FailureCode { get; }
}
```

Mapper exceptions derived from `Exception`, including mapper-thrown `OperationCanceledException`, SHALL become `DAG_INPUT_MAPPING_INVALID`, except `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException`, which SHALL NOT be converted. Host/run cancellation observed outside mapper evaluation SHALL retain the runtime cancellation outcome. Before invoking the evaluator, an ordinary declared-type output decode/materialization failure SHALL fail the node as `DAG_INPUT_MAPPING_INVALID`, without invoking its mapper, committing input, or starting a child; protocol-integrity or storage failures SHALL remain runtime failures and SHALL NOT be disguised as mapper failures.

The same `OrcaCore.Dag -> OrcaCore.Dag.Hosting` friend SHALL additionally carry a separately named facade-handle grant limited to exactly two internal constructors, one on the public `DagDefinitionHandle<TRunInput>` and one on the public `DagRunHandle`, with the exact signatures below. Every parameter SHALL be a public type or a delegate over public types; the grant SHALL add no non-public type reference, so the non-public DAG type references of `OrcaCore.Dag.Hosting` SHALL remain exactly the three runtime-view types and its non-public DAG member references SHALL be exactly the fifteen runtime-view signatures plus these two constructors. No other constructor, setter, field, factory, interface, or overload SHALL be reachable through the grant.

`OrcaCore.Dag` SHALL own, inside the handles and result unions, typed union construction for node output, rejection of a node reference from another plan by reference identity before any runtime query, the check that a supplied output matches the declared `TOutput` under the existing null rule, and every throw of `DagRunNotFoundException`, `DagRunDefinitionMismatchException`, `DagRunInputContractMismatchException`, and `DagRunFaultedException`. `OrcaCore.Dag.Hosting` SHALL NOT construct a DAG exception and SHALL signal those outcomes only through delegate results: an absent run and a run bound to another definition identity, version, or fingerprint through the reopen result; a reopened run bound to this exact definition under another declared run-input contract through the reopen flag, which SHALL be used for reopen only; and the integrity-fault state through a null terminal-wait or cancellation result. Every comparison of a start with an existing start-key owner SHALL be a closed `DagStartResult` variant that `OrcaCore.Dag.Hosting` constructs publicly, and a start whose key owner has an equal definition triple and a different run-input contract key SHALL be reported as `DagStartResult.RunInputConflict`, never through the reopen flag. `OrcaCore.Dag.Hosting` SHALL project public snapshots and status values from committed run facts supplied by the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge; every snapshot and result-union value SHALL be constructed through its public constructor, and status values SHALL be public enumeration members. `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure`, and `PayloadFingerprint` values SHALL be created by `OrcaCore.Engine.Durable` and SHALL cross that bridge as opaque public values; the six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist SHALL NOT be widened, `OrcaCore.Dag.Hosting` SHALL NOT become a friend of `OrcaCore`, and `OrcaCore.Dag` SHALL gain no codec, runtime-protocol, provider, or child-management access. `IDagDefinitionRegistry` SHALL be implemented by `OrcaCore.Dag.Hosting` through its public interface with no friend access. The handle delegates SHALL carry no authority beyond the run or definition they were created for, and constructing an equal public data value SHALL confer no management authority. A handle SHALL be valid only within the host process that created it; another DAG-role host SHALL reopen the run with `GetRunAsync`.

A DAG definition binding SHALL be its definition identity, version, structural fingerprint, and declared run-input contract; the structural fingerprint SHALL NOT be assumed to cover `TRunInput`. The durable runtime SHALL own the run-input contract key, derived from the fixed-codec shape of the declared run-input type, and `OrcaCore.Dag` SHALL NOT compute it. Two comparisons SHALL be distinguished. Within one host process, two registrations have the same run-input contract only when their declared run-input types are the same CLR `Type` and their keys are equal; a different `Type` SHALL conflict even when its key is equal. Against a stored run, whose declaring type is not knowable, the installed plan matches the stored run-input contract only when the keys are equal and the durable runtime has verified that the stored run-input bytes decode with the installed declared type and re-encode through the fixed codec to exactly the stored bytes; an equal key SHALL NOT by itself establish compatibility. That verification SHALL complete before a host issues a run handle and before it evaluates any mapper for the run. A well-formed stored payload that fails the verification under the installed binding SHALL make the run locally unavailable exactly as a different key does, and SHALL NOT be recorded as a mapping failure, node failure, or integrity fault. Two key comparisons SHALL be kept apart. The contract key recorded on a stored run-input value SHALL equal the run-input contract key of its own stored graph; a disagreement between those two, like invalid framing, codec format, or fingerprint of a checkpoint or value, is corrupt storage, SHALL keep its existing integrity classification, and SHALL NOT be reclassified as a contract mismatch. A valid stored graph key that differs from the run-input contract key of the installed plan is a binding mismatch. The same integrity check SHALL apply to a start-key owner examined during a replay. Storage failures, integrity failures, and caller cancellation SHALL propagate as exceptions from registration, start, and reopen alongside the closed comparison results, which SHALL NOT absorb them.

Before creating a run or accepting a replay, the durable runtime SHALL encode the submitted run input with the declared run-input type, decode those bytes, and require byte-identical re-encoding. A submitted value that fails SHALL be rejected with `NotSupportedException` before any run is committed and before any start key is bound. This rule and the stored-run verification SHALL be a guarantee about the stored representation only: a change in what the values mean SHALL still require a new definition version. The rule SHALL apply to the DAG run input only; mapped node inputs, child outputs, and ordinary workflow inputs SHALL keep their existing commit paths and failure outcomes.

On one host, registering an identity and version again with an equal fingerprint and the same run-input contract SHALL return `Registered`; a different fingerprint SHALL return `Conflict` carrying `DefinitionRegistrationConflict` and SHALL take precedence; an equal fingerprint with another declared run-input type SHALL return the closed `RunInputConflict` variant and SHALL leave the earlier registration unchanged. `GetRunAsync` SHALL reopen only a run whose stored binding matches all four parts, SHALL report another identity, version, or fingerprint as `DagRunDefinitionMismatchException` first, and SHALL report an equal triple under another run-input contract as `DagRunInputContractMismatchException` carrying the run id. `StartOrGetAsync` SHALL resolve the authoritative owner of the start key, both on retry and after a competing create, and SHALL return `Accepted` with the run id of that owner and `WasExisting = true` only when the definition triple, the run-input contract key, and the normalized run-input bytes are all equal; an equal triple with another run-input contract key SHALL return the closed `DagStartResult.RunInputConflict` variant carrying the run id of that owner; and every other difference SHALL return `Conflict` carrying `StartIdempotencyConflict`, including an equal contract key with different validated run-input bytes. No comparison with an intact existing key owner SHALL be reported by exception from `StartOrGetAsync`; apart from storage, integrity, and cancellation exceptions, only a submitted value that fails the round-trip rule SHALL be rejected by exception. A host SHALL map and admit nodes of a discovered run only when its installed plan matches all four parts of the stored binding. For any other run, which is locally unavailable, that host SHALL skip new mapping and admission without mutation, mapping failure, or integrity fault, while role-independent child start, join, and cancellation work already committed for the run SHALL still be processed by any durable host.

The exhaustive facade-handle signature contract SHALL be exactly the following, with no additional non-public type or member access:

```csharp
// Facade-handle grant: the only two non-public constructors Dag.Hosting may call.
public sealed class DagDefinitionHandle<TRunInput>
{
    internal DagDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TRunInput, StartIdempotencyKey, CancellationToken, ValueTask<DagStartResult>> startOrGet,
        Func<DagRunId, CancellationToken, ValueTask<(
            DagRunHandle? Run,
            DefinitionId? ActualDefinitionId,
            DefinitionVersion? ActualDefinitionVersion,
            DefinitionFingerprint? ActualDefinitionFingerprint,
            bool RunInputContractDiffers)>> getRun);
}

public sealed class DagRunHandle
{
    internal DagRunHandle(
        DagRunId runId,
        IReadOnlyList<DagNodeRef> planNodes,
        Func<CancellationToken, ValueTask<DagRunSnapshot>> getSnapshot,
        Func<DagNodeRef, CancellationToken, ValueTask<(
            DagNodeStatus Status,
            bool HasOutput,
            object? Output,
            WorkflowFailure? Failure)>> getOutput,
        // null from either delegate: the run is in the integrity-fault state
        Func<CancellationToken, ValueTask<DagRunSnapshot?>> waitForTerminal,
        Func<CancellationToken, ValueTask<DagCancellationRequestStatus?>> requestCancellation);
}
```

#### Scenario: Engine consumes the shared execution kernel
- **WHEN** either engine compiles against `OrcaCore.Core`
- **THEN** it receives type-safe friend access to internal kernel types without making those types externally visible

#### Scenario: Internal application contracts cross implementation packages
- **WHEN** Core or an engine constructs or reads an application-owned internal authoring/runtime contract
- **THEN** it uses compile-checked `OrcaCore` friend access and no reflection, CLR-name lookup, or public compatibility bridge

#### Scenario: Consumer inspects an implementation package
- **WHEN** a fresh-package consumer or exported-API baseline inspects Core, an engine, hosting, or a provider package
- **THEN** only the documented role-specific surface is exported and no compiler IR, concrete loop/store, observer hook, test helper, or reflection bridge is visible

#### Scenario: New friend assembly is proposed
- **WHEN** compiled metadata contains a friend outside the exact product, owning-test, and provider-certification allowlist
- **THEN** the guard fails until a reviewed contract amendment authorizes that boundary

#### Scenario: DAG author constructs compiler-owned values
- **WHEN** `OrcaCore.Dag` builds a typed plan or returns ordered build diagnostics
- **THEN** it constructs only the six allowlisted internal member signatures on the five named authoring value families, reuses the shared catalog, sorter, and canonical hash, and exposes no new public construction path

#### Scenario: DAG references another application internal
- **WHEN** compiled `OrcaCore.Dag.dll` references any other non-public `OrcaCore` type, member, or overload
- **THEN** the compiled-metadata guard fails even though the CLR friend grant would permit the reference

#### Scenario: DAG runtime evaluates one ready node
- **WHEN** the hosting adapter receives a validated plan and all declared direct dependencies have succeeded
- **THEN** its allowlisted runtime-view evaluator returns typed mapped input and its declared type, or DAG_INPUT_MAPPING_INVALID, without exposing delegates or committing bytes or starting a child

#### Scenario: DAG host references an unrelated authoring internal
- **WHEN** compiled OrcaCore.Dag.Hosting.dll refers to any non-public DAG type or member outside the exact runtime-view allowlist
- **THEN** metadata verification fails even when the CLR friend grant permits that reference

#### Scenario: Runtime normalizes node input
- **WHEN** Dag.Hosting submits valid typed mapped input to the existing durable bridge
- **THEN** the durable runtime alone normalizes fixed-codec bytes, fingerprints and commits them once before child start, and Dag gains no codec or runtime-protocol access

#### Scenario: Successful dependency output is null
- **WHEN** a successful committed direct resultful dependency has a null output of a reference or nullable-value declared type
- **THEN** the durable bridge supplies a present null entry, OutputOf returns that successful null, and a missing entry or nonnullable-value null still fails mapping before commit or child start

#### Scenario: DAG host creates public handles
- **WHEN** `OrcaCore.Dag.Hosting` returns a registered definition handle or an accepted, reopened, or replayed run handle
- **THEN** it calls only the two allowlisted handle constructors with delegates over public types, constructs every result and snapshot value through public constructors, and references no other non-public DAG member

#### Scenario: Run reopen finds no matching run
- **WHEN** the reopen delegate reports an absent run or a run bound to another definition identity, version, or fingerprint
- **THEN** the definition handle inside `OrcaCore.Dag` throws `DagRunNotFoundException` or `DagRunDefinitionMismatchException`, and the hosting assembly constructs neither exception

#### Scenario: Two hosts register equal structure with different run input
- **WHEN** a run was created by a plan whose identity, version, and structural fingerprint equal those of a plan registered on another host whose declared run-input type yields a different run-input contract key
- **THEN** that other host neither reopens nor progresses the run, `GetRunAsync` throws `DagRunInputContractMismatchException` carrying the run id, a start with the same key returns `DagStartResult.RunInputConflict` carrying the run id of the owner without creating a run because the definition triple is equal and the contract keys differ, and that host skips new mapping and admission for the run without mutation

#### Scenario: Start key is reused with an equal contract key and different input
- **WHEN** `StartOrGetAsync` submits a run input that passes the round-trip rule for a start key whose owner has an equal definition triple and an equal run-input contract key but different normalized run-input bytes
- **THEN** it returns `Conflict` carrying `StartIdempotencyConflict`, not `RunInputConflict`

#### Scenario: Start-key owner examined during replay is corrupt
- **WHEN** the owner of a start key has a stored run-input value whose recorded contract key disagrees with its own stored graph, or whose framing, codec format, or fingerprint is invalid
- **THEN** the existing integrity failure propagates and no closed start result is returned for that owner

#### Scenario: Run is cancelled through a compatible host while another host cannot map it
- **WHEN** a host with a compatible binding accepts `RequestCancellationAsync` for a run that another durable host treats as locally unavailable
- **THEN** the committed child-cancellation work may be consumed by the pump of any durable host, including the host that skips mapping and admission for that run

#### Scenario: One host registers the same definition with another run-input type
- **WHEN** a second plan with an equal identity, version, and structural fingerprint but another `TRunInput` is registered on a host
- **THEN** registration returns `RunInputConflict` carrying the existing and attempted types even when both types produce an equal contract key, the earlier registration and its handles are unchanged, and `GetHandleOrThrow()` throws `DagRunInputContractMismatchException`

#### Scenario: Redeployed run-input type keeps its key but rejects stored bytes
- **WHEN** a host installs a plan whose run-input type has the same contract key as a stored run but cannot decode the stored run-input bytes, or re-encodes them differently
- **THEN** the stored run is treated as bound to another run-input contract: reopen throws `DagRunInputContractMismatchException`, that host skips new mapping and admission, no mapping failure, node failure, or integrity fault is committed, a child already admitted can still be joined by its role-independent pump, and a host with a compatible binding can reopen and finish the run

#### Scenario: Submitted run input does not round-trip
- **WHEN** `StartOrGetAsync` receives a run input whose encoded bytes do not decode and re-encode to the same bytes under the declared run-input type
- **THEN** it throws `NotSupportedException` before any run is committed or any start key is bound, for a first start and for a replay alike

#### Scenario: Stored run input is corrupt
- **WHEN** the stored run-input value has invalid framing, codec format, or fingerprint, or records a contract key that disagrees with its own stored graph
- **THEN** the existing integrity classification applies and the run is not reported as bound to another run-input contract

#### Scenario: Start is replayed with the same key
- **WHEN** `StartOrGetAsync` is retried, or loses a competing create, for a start key whose owner has an equal definition triple, an equal run-input contract key, and equal normalized run-input bytes
- **THEN** it returns `Accepted` with the run id of that owner and `WasExisting = true`, and a caller-supplied run id that collides with another run remains a separate conflict

#### Scenario: Output is requested with a node from another plan
- **WHEN** `GetOutputAsync` receives a node reference that is not one of the plan nodes of that run
- **THEN** the run handle rejects it by reference identity before invoking any runtime delegate

#### Scenario: Conflict and failure values reach the application
- **WHEN** registration, start, or a snapshot reports a registration conflict, start-idempotency conflict, node failure, or mapped-input fingerprint
- **THEN** that value was created by the durable engine and passed through the existing bridge unchanged, and neither DAG package gained a new application-contract friend or constructor grant

#### Scenario: DAG host references another handle internal
- **WHEN** compiled `OrcaCore.Dag.Hosting.dll` references any non-public member of a facade handle other than its one allowlisted constructor
- **THEN** metadata verification fails even though the CLR friend grant permits the reference

### Requirement: Durable event acceptance has one closed acknowledgement boundary
Both `IWorkflowEventIngress.AcceptAsync` overloads SHALL return `WorkflowEventAcceptanceResult`, the closed union of `Accepted`, `Duplicate`, and `Rejected(WorkflowEventAcceptanceRejection)`. `WorkflowEventAcceptanceRejection` SHALL be the closed union of `EventConflict`, `DirectInstanceNotFound`, `DirectInstanceTerminal`, `StartConflict(StartIdempotencyConflict)`, and `FanoutLimitExceeded`. Only `Accepted` and `Duplicate` SHALL mean OrcaCore durably owns the complete envelope and route intent and the broker MAY be acknowledged. A rejection SHALL commit neither ownership nor a partial fanout target set. Infrastructure, serialization, and cancellation failures SHALL complete exceptionally rather than masquerading as an acknowledgement-safe result. Event-identity deduplication SHALL precede current route-state evaluation, so an identical redelivery of an accepted event remains `Duplicate` after its target progresses or terminalizes.

#### Scenario: Broker handler decides whether to acknowledge
- **WHEN** a MassTransit, Rebus, SNS/SQS, RabbitMQ, or other handler forwards one valid self-routing envelope
- **THEN** it may acknowledge on `Accepted` or `Duplicate`, may apply application dead-letter policy to a typed `Rejected` reason, and does not acknowledge merely because an infrastructure exception occurred

#### Scenario: Accepted event is redelivered after target completion
- **WHEN** the same `EventId` and identical normalized bytes are submitted after the original target has progressed or terminalized
- **THEN** the result remains `Duplicate` rather than changing to `DirectInstanceTerminal`

#### Scenario: Runtime resolves a correlation key
- **WHEN** event routing or reconciliation needs one instance or active wait
- **THEN** runtime-owned keyed retrieval MAY be used internally without exposing application enumeration, filtering, or bulk retrieval

### Requirement: Event integration is transport neutral
The public durable integration boundary SHALL expose a self-routing `IWorkflowEventIngress` and an application-shaped `IWorkflowEventDispatcher` without referencing MassTransit, Rebus, SNS, SQS, RabbitMQ, or any other broker SDK. `Accepted` or identical `Duplicate` ingress results SHALL mean OrcaCore durably owns the normalized event and the broker handler may acknowledge it. Every `Rejected(WorkflowEventAcceptanceRejection)` result SHALL commit no ownership of the submitted envelope. The dispatcher SHALL expose exactly `ValueTask<WorkflowEventDispatchResult> DispatchAsync(WorkflowOutboundEvent outboundEvent, CancellationToken cancellationToken = default)`, receive only the complete outbound event, and never receive provider `OutboxWrite`, internal continuation kinds, claims, stream versions, or checkpoints. Its closed result SHALL be `Succeeded`, `RetryableFailure(WorkflowEventDispatchFailure)`, or `PermanentFailure(WorkflowEventDispatchFailure)` with a nonblank stable failure code and optional diagnostic detail.

#### Scenario: Broker handler forwards an event
- **WHEN** a MassTransit, Rebus, SNS/SQS, RabbitMQ, or custom handler receives an upstream event containing the required route and identities
- **THEN** it injects one ingress interface and forwards the envelope without querying definitions, workflow instances, waits, or engine memory

#### Scenario: Application dispatches an outbound event
- **WHEN** the durable outbox pump invokes the application dispatcher
- **THEN** the adapter maps stable event contract identity to its broker destination and receives all event, correlation, causation, and workflow-origin identifiers without any provider record

### Requirement: Ephemeral terminal retention policy is explicit and host-owned
`OrcaCore.Engine.Ephemeral` SHALL own the immutable `OrcaCore.Hosting.EphemeralTerminalRetentionPolicy` with a private constructor, get-only `int MaxRetainedInstances`, `TimeSpan MaxAge`, and `TimeSpan SweepInterval`, and exactly one public factory `Create(int maxRetainedInstances, TimeSpan maxAge, TimeSpan sweepInterval)`. `EphemeralEngineHostOptions` SHALL expose the nullable init-only member `EphemeralTerminalRetentionPolicy? TerminalRetention`, defaulting to null. With a null/omitted policy, accepted instances and their start bindings SHALL remain retained for the host lifetime and no retention timer SHALL exist; unaccepted aborted provisional units SHALL still follow mandatory settlement cleanup. The factory SHALL reject a count below one, nonpositive maximum age, or nonpositive sweep interval, an interval greater than maximum age, or an interval greater than one day, using `ArgumentOutOfRangeException` for the corresponding parameter in signature validation order. Registration SHALL immediately validate and freeze an owned copy and include the enabled/disabled state and all three values in duplicate-role option agreement; equal values SHALL be idempotent regardless of policy object identity and different values SHALL conflict before service mutation. Every positive interval up to those ceilings, including one tick, SHALL be valid; documentation SHALL identify very small intervals as potentially continuous sweep/CPU overhead and recommend choosing an operationally appropriate interval without silently clamping the value. The policy SHALL create no new registration overload, public lifecycle command, options-binder DTO, serializer hook, package dependency, friend grant, or durable-host option.

#### Scenario: Existing application omits the policy
- **WHEN** an ephemeral host is configured using only its existing structured-execution and transient-pool values
- **THEN** accepted-instance retention and its host-lifetime start bindings remain unchanged and no retention sweep is scheduled, while aborted unaccepted provisional starts still receive mandatory settlement cleanup

#### Scenario: Host chooses bounded retention
- **WHEN** application code sets `TerminalRetention = EphemeralTerminalRetentionPolicy.Create(128, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1))`
- **THEN** the host freezes exactly those values and applies one host-wide eligible-terminal retention budget across registered definitions

#### Scenario: Invalid policy is constructed
- **WHEN** a count is zero/negative, maximum age is nonpositive, or the interval is nonpositive, greater than maximum age, or greater than one day
- **THEN** the factory throws `ArgumentOutOfRangeException` naming the first invalid parameter in signature order before registration or service mutation

#### Scenario: Equivalent or conflicting registrations are composed
- **WHEN** the same role is registered again with equal policy values in another object or with any changed policy value/enabled state
- **THEN** equal values preserve existing idempotent registration and changed values reject before adding services or starting work

#### Scenario: Ordinary consumer package closure is inspected
- **WHEN** compile/reflection/package guards inspect the enabled policy
- **THEN** only the exact proposed ephemeral host signatures are added and no public engine, bulk purge/archive/evict method, durable option, or reflection/friend bridge appears

### Requirement: Ephemeral host shutdown has one supported observation contract
`OrcaCore.Engine.Ephemeral` SHALL own exactly the following new public declarations
in namespace `OrcaCore.Hosting`, with the shown construction restrictions and no
additional members, record-generated surface, public constructor or extension:

```csharp
namespace OrcaCore.Hosting;

public interface IEphemeralWorkflowHostLifetime
{
    ValueTask<EphemeralHostShutdownResult> StopAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default);
}

public abstract class EphemeralHostShutdownResult
{
    private protected EphemeralHostShutdownResult();

    public sealed class Completed : EphemeralHostShutdownResult
    {
        internal Completed();
    }

    public sealed class Incomplete : EphemeralHostShutdownResult
    {
        internal Incomplete(EphemeralHostShutdownIncompleteReason reason, long pendingWorkCount);
        public EphemeralHostShutdownIncompleteReason Reason { get; }
        public long PendingWorkCount { get; }
    }
}

public enum EphemeralHostShutdownIncompleteReason
{
    DeadlineElapsed = 0,
    ObservationCancelled = 1,
    ShutdownFaulted = 2
}

public sealed class EphemeralHostStoppingException : global::OrcaCore.OrcaCoreException
{
    internal EphemeralHostStoppingException();
}

public sealed class EphemeralHostShutdownIncompleteException : global::OrcaCore.OrcaCoreException
{
    internal EphemeralHostShutdownIncompleteException(EphemeralHostShutdownResult.Incomplete result);
    public EphemeralHostShutdownResult.Incomplete Result { get; }
}
```

The inherited stable codes SHALL be `WF-EPHEMERAL-HOST-STOPPING` and
`WF-EPHEMERAL-SHUTDOWN-INCOMPLETE` respectively. The existing ephemeral role entry
SHALL register one reserved owned singleton lifetime per root provider, the same
when resolved from a child scope. Equal existing role/options registration SHALL
remain idempotent; a pre-existing application descriptor for the reserved lifetime
before first role registration SHALL fail with ArgumentException, parameter services,
before collection mutation. There SHALL be no new role entry, option, shutdown mode,
fingerprint value, hosted-service toggle, dependency, serializer hook or friend.

StopAsync SHALL validate timeout first: exactly InfiniteTimeSpan or an inclusive
zero through 4,294,967,294 milliseconds, compared in exact ticks; other values SHALL
throw ArgumentOutOfRangeException for timeout before any effect. An already-cancelled
token SHALL then throw OperationCanceledException with that token without initiating
shutdown; root disposal SHALL next throw ObjectDisposedException. If Completed
already holds after those checks, the call SHALL return its detached cached result
without reading TimeProvider or creating observation resources. Otherwise zero SHALL close,
enter Stopping and observe immediately; infinite SHALL explicitly permit an unbounded observation.
Finite observation time SHALL begin on admission using the registered monotonic
TimeProvider and include queued cancellation/join work. Once admitted, observer
cancellation SHALL be Incomplete(ObservationCancelled), not an exception. Decision
precedence SHALL be accounting fault, stable completion, observer cancellation,
then elapsed budget. The two latter incomplete causes SHALL NOT abort the shared stop.

Host state SHALL progress irreversibly Open -> Closed -> Stopping -> Completed.
Closed SHALL refuse new/replayed starts and compatible registrations but SHALL NOT
queue shutdown cancellation, seal accepted-instance producers or reject otherwise
valid accepted-instance operations. Only an admitted public StopAsync or the engine's
own hosted StopAsync SHALL enter Stopping, closing first if needed, to cancel and
join remaining work. The internal hosted stop SHALL do so even with an already-
cancelled host token, return normally only on Completed and otherwise throw the
typed incomplete exception with its detached result. The host token SHALL bound
observation only; this path SHALL NOT use the public pre-cancelled no-effect branch.

If IHostApplicationLifetime is present, every admission/initialization/attempt gate
SHALL read ApplicationStopping directly and establish Closed from its cancelled
state, even if another handler blocks engine notification. Accepted-instance gates
SHALL still permit ordinary operation and attempts in Closed, checking Stopping
separately. The engine/coordinator SHALL initialize before subscribing. Its bounded
callback SHALL only close, without queuing cancellation/join, invoking application
code or waiting on a lane. Late resolution SHALL close new-start/registration
admission without itself cancelling accepted work. Ordinary output-wait cancellation
SHALL NOT initiate host shutdown. No new close-only API, option or drain result
SHALL be added.

#### Scenario: A valid public stop token was cancelled before invocation
- **WHEN** the public method receives a valid timeout and an already-cancelled token
- **THEN** it throws caller cancellation without closing an open host or scheduling cancellation work

#### Scenario: A host budget is already exhausted on hosted stop entry
- **WHEN** the internal hosted service receives an already-cancelled observation token
- **THEN** the hook closes admission and enters Stopping to cancel/join remaining work, returning Completed only if stable settlement is proved and otherwise throwing the detached typed incomplete result

#### Scenario: Exact API appears only with implementing source
- **WHEN** the contract/canonical gates are prepared before source implementation
- **THEN** these proposed declarations have no compiled/public-baseline credit; source task 3.3 adds their API, compile and package baselines together with source

#### Scenario: Another application stopping callback blocks notification
- **WHEN** the host stopping token is cancelled but another registered callback blocks before the engine callback runs
- **THEN** gates establish Closed from the token and reject new starts/registrations without waiting for notification, while accepted-instance operations/attempts continue and no shutdown cancellation is queued until a stop entry runs

### Requirement: Ephemeral host shutdown preserves dependency ownership
One application-owned session/supervisor SHALL serialize provider/host disposal and
retain the root, relevant scopes and external step dependencies until Completed.
After Completed, the owner SHALL finish other hosted-service stopping before disposal;
no engine-owned step, callback, turn, serializer/notification or continuation SHALL
then access those dependencies. Incomplete SHALL NOT authorize disposal or capacity
reuse. A later finite/infinite observation SHALL be supported while ownership is kept.

Supported drain guidance and consumer qualification SHALL cover these sequences:

- For Generic Host hosted draining, register the engine before an application
  IHostedService drainer and use HostOptions.ServicesStopConcurrently=false. Reverse
  registration stop order SHALL let that drainer await its own pending starts and
  accepted handles while Closed, retaining event producers/dependencies needed by
  those instances, before the engine's hosted StopAsync enters Stopping. Drain waits
  SHALL use the supplied StopAsync budget token, not the already-cancelled
  ApplicationStopping token. Local drain expiry SHALL return control to the engine
  without disposing dependencies or abandoning runtime ownership. Draining and the
  engine stop SHALL share HostOptions.ShutdownTimeout; a fresh engine budget SHALL
  NOT be promised. With concurrent hosted stops, ordering SHALL NOT be described as
  guaranteeing a drain window. Required dependency/event-service stops SHALL occur
  after drain. Blocking ApplicationStopping callbacks SHALL NOT implement drain.
- An explicit Generic Host owner SHALL be able to start and retain IHost, stop its
  submitters, await its pending starts/accepted handles first, then call the engine
  StopAsync, without another engine/IHost stop racing that drain. ApplicationStopping
  alone MAY already make the engine Closed without cancellation. A bounded drain
  expiry SHALL proceed to StopAsync. Incomplete SHALL retain the host for reobservation;
  only Completed SHALL permit finishing IHost.StopAsync and disposal. An explicit
  IHost.StopAsync with the ordered drainer and catch/reobserve SHALL also be supported.
- Plain-DI owners SHALL close their own submission gate and join their submitters/
  pending starts, await accepted handles with an application drain budget, then call
  the lifetime StopAsync to close/cancel/join anything left. The engine SHALL remain
  Open during that app-controlled drain: there is no plain-DI close-only API and
  the application SHALL control all submitters. It SHALL keep event delivery and
  actual dependencies alive, retain provider/scopes on Incomplete and reobserve
  before disposal. Provider Dispose/DisposeAsync SHALL NOT be presented as stop/join.

Incomplete-capable Generic Host applications SHALL own IHost explicitly and retain it
on incomplete hosted stop. Migration documentation SHALL state that the formerly
no-op hosted stop now surfaces Incomplete as an exception escaping Run/RunAsync
(possibly aggregated). Its unconditional disposal finally SHALL still be identified
as unsafe for incomplete-work ownership; throwing SHALL NOT imply it prevented disposal.

The engine SHALL NOT claim to intercept arbitrary external provider disposal, retain
already disposed services, create a hidden root provider, or block synchronous disposal
forever. Unjoined direct disposal SHALL be classified as unsupported abortive teardown:
enter only Closed and record a contained best-effort bounded diagnostic before
teardown, without queuing shutdown cancellation/join, claiming Completed or falsifying
physical ownership. Abortive disposal SHALL NOT be a supported drain mechanism. Applications SHALL serialize
concurrent disposal through the supported owner; arbitrary premature external disposal
is outside the successful-join guarantee.

#### Scenario: The owner disposes after successful physical join
- **WHEN** the supported owner observes Completed, finishes other hosted stops and disposes its root and scopes
- **THEN** disposable dependency sentinels observe no later engine-owned access

#### Scenario: Work ignores cancellation beyond the observation budget
- **WHEN** StopAsync returns Incomplete while physical work or an entered callback still uses a dependency
- **THEN** the owner keeps the actual provider/scopes/dependencies alive, physical capacity remains owned, and another observation can join later actual settlement

#### Scenario: Generic Host RunAsync reaches its disposal finally
- **WHEN** a caller uses ordinary RunAsync and an incomplete hosted stop throws
- **THEN** the new exception (possibly aggregated) and automatic disposal are documented as changed behavior and an unsupported incomplete-work ownership pattern; the exception does not prevent disposal

#### Scenario: An ordered Generic Host service drains before engine stop
- **WHEN** a drainer registered after the engine awaits its handles with sequential hosted stops
- **THEN** ApplicationStopping closes only new admission, accepted work can finish naturally and receive events/new waits, and cancellation begins only at the subsequent engine StopAsync within the same host budget

#### Scenario: Concurrent hosted stops race a drainer
- **WHEN** ServicesStopConcurrently permits the engine stop to enter before the drainer finishes
- **THEN** the documented recipe makes no drain-window guarantee, Stopping may cancel remaining work, and Incomplete still requires explicit retained host ownership

#### Scenario: A plain-DI application drains its own submissions
- **WHEN** the owner closes its application submission gate, joins pending starts and awaits accepted handles before calling StopAsync
- **THEN** the engine stays Open during that controlled drain, event delivery/dependencies stay alive, and the later explicit call closes and cancels/joins any remaining work before disposal

### Requirement: Workflow JSON metadata registration is exact and process scoped
OrcaCore SHALL add public static class `OrcaCore.WorkflowJsonMetadata` with public static void `Register<TContext>() where TContext : System.Text.Json.Serialization.JsonSerializerContext, new()`. The exact public catalogue additionally comprises public sealed `OrcaCore.Abstractions.Ids.StrongStringValueJsonConverterFactory : System.Text.Json.Serialization.JsonConverterFactory`, its public parameterless constructor, public override bool `CanConvert(System.Type typeToConvert)` and public override System.Text.Json.Serialization.JsonConverter `CreateConverter(System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)`. This factory SHALL retain its closed eleven-type table, bodies, private converter and existing strong-value attributes; the only additionally approved public annotation SHALL be System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces) on the existing TStep parameter of OrcaCore.Hosting.StepExecutionThrottle.For<TStep>(int maxConcurrency); no other public type/member/annotation SHALL be added. Constructing/using the factory SHALL NOT register or seal metadata or touch DI/host state. Registration SHALL own one generated metadata context for the process and SHALL expose no reset/unregister, options/resolver/converter parameter, new role option, package or friend. First successful registration SHALL publish atomically; repeating the same CLR context type SHALL be a no-op without reconstruction, including after sealing. A different context type SHALL throw InvalidOperationException without construction or mutation. Construction/validation failure SHALL install nothing and permit retry. Unsupported context policy/provenance, including UseStringEnumConverter=true with otherwise-default runtime options, explicit converter settings even for the product factory, and the handwritten renamed-property context, SHALL raise NotSupportedException identifying the context and rule before publication; an empty converter list or the generic constraints SHALL NOT alone establish conformance. Context construction failures SHALL retain their original cause. The first workflow authoring/type-contract/codec operation SHALL irreversibly seal Generated mode or legacy Reflection mode, including a failing lookup. First registration after a Reflection seal SHALL fail. Configuration/first-use races SHALL linearize, and reentrant configuration SHALL fail explicitly without deadlock. Errors SHALL identify context identities and seal state. Product/public signatures and role fingerprints outside this catalogue SHALL remain unchanged.

#### Scenario: Two service providers use metadata in one process
- **WHEN** startup registers the same context for multiple providers before or after first use
- **THEN** one context remains installed, repeated registration does not invoke application code, providers own no metadata disposal and stopping a host cannot reset configuration

#### Scenario: Configuration races first workflow use
- **WHEN** first registration and first workflow use overlap
- **THEN** the whole context wins before sealing or registration fails after the legacy seal, with no partial context, reflection fallback, deadlock or order-dependent resolver chain

#### Scenario: Configuration fails and is retried
- **WHEN** a context constructor or options validation fails before a successful installation
- **THEN** no registration is visible, the failure preserves its cause and a subsequent valid registration can succeed before first use

#### Scenario: External generated metadata references the attributed product converter
- **WHEN** an ordinary external Metadata or Default context describes a DTO with an attributed strong-string member after the catalogue amendment is approved and implemented
- **THEN** its generator can construct the existing sealed public factory through its public parameterless constructor without a private bridge or suppression, while only the exact catalogued constructor and two overrides become additional members

#### Scenario: An application explicitly adds the product factory to context settings
- **WHEN** registration encounters explicit converter settings listing StrongStringValueJsonConverterFactory rather than only its unchanged product type attribution
- **THEN** it rejects with NotSupportedException identifying the context/policy before publication or seal, performs no workflow work and allows a valid default context to register before first use

#### Scenario: A named-step throttle retains interface metadata
- **WHEN** a concrete or inherited IStep<TState> implementation is supplied to StepExecutionThrottle.For<TStep>, directly or through an annotated generic wrapper, in JIT, full trimming or Native AOT
- **THEN** the exact named step type and requested positive capacity are retained with preserved interface metadata and no IL2090; invalid types still raise ArgumentException for TStep, and nonpositive capacity raises ArgumentOutOfRangeException for maxConcurrency before type validation, without DI resolution or workflow work

### Requirement: Native authoring bootstrap has a static ownership recipe
The supported native recipe SHALL call AddOrcaCoreEphemeralEngine on IServiceCollection, then WorkflowJsonMetadata.Register, then author definitions, build the provider and use existing typed registry/facade APIs. Role registration SHALL synchronously install the Core typed-authoring boundary through a static reference without resolving services, constructing steps, building a provider, loading an assembly dynamically or admitting work. Generated metadata SHALL capture no provider/scope and SHALL introduce no provider-owned disposal. The existing JIT definition-first path and missing-engine diagnostic SHALL remain compatible. A native authoring attempt without the required installation SHALL fail actionably before effects. Metadata SHALL NOT change admission, Closed draining, Stopping/Completed, cancellation, retirement, pins, key expiry or DI dependency ownership.

#### Scenario: Native application configures the engine before building definitions
- **WHEN** it follows the documented role-registration, metadata-registration and authoring sequence
- **THEN** initialization is statically reachable before definition construction and no assembly-load fallback or premature DI resolution occurs

#### Scenario: Metadata is used with a draining or completed host
- **WHEN** existing instances drain while Closed or the host reports Completed
- **THEN** the existing shutdown contract remains authoritative and metadata cannot create new work, touch scoped dependencies or run user workflow code after Completed

### Requirement: Ephemeral settlement observation is a passive handle operation
PackageId/assembly OrcaCore, namespace OrcaCore, SHALL add exactly public
string? WorkflowFailure.ExceptionType { get; } and public ValueTask
WorkflowInstanceHandle.WaitForSettlementAsync(CancellationToken cancellationToken
= default). The getter SHALL remain runtime-created with no public constructor or
setter; the method SHALL be nonvirtual on the existing base handle and inherited
unchanged by WorkflowInstanceHandle<TOutput>. No new helper/overload, timeout,
configuration option, result union, public class, package, friend or provider API
SHALL be added. WorkflowOutputUnavailableException SHALL keep its public shape,
using its inherited InnerException for the original reference.

An already-issued ephemeral handle SHALL retain a small settlement receipt,
without retaining instance payloads or exception slots after retirement. The
receipt SHALL prove terminal final physical quiescence, remain valid after
retirement/Completed/owner disposal, and never target a replacement instance.
A pre-cancelled caller token SHALL throw local OperationCanceledException before
mode/proof checks. Otherwise durable handles SHALL throw NotSupportedException
without provider/DI/runtime lookup, in every durable lifecycle state.

#### Scenario: A resultless or resultful consumer observes settlement
- **WHEN** an accepted ephemeral handle's consumer invokes the base settlement method
- **THEN** it observes the same receipt without a cast, output value or additional public service

#### Scenario: A durable consumer calls settlement
- **WHEN** a durable handle receives a valid noncancelled settlement call
- **THEN** NotSupportedException is returned without storage/provider or DI access; a pre-cancelled token instead produces local cancellation
