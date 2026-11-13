# 10. Provider Model & Extensibility Requirements (PR)

Scope: the pluggable infrastructure boundary — what providers implement, what the engine
owns, and the invariants every provider must satisfy.

## 10.1 Principles

### PR-001 Interface-first extensibility
When a capability can reasonably vary by runtime, infrastructure, or integration boundary,
it SHALL be modeled behind a contract rather than a hard-coded implementation: persistence,
message dispatch, timer scheduling, outbox pump observability/retry timing, and future
operational hooks. Payload serialization is intentionally not variable in v1: the engine owns
one nonreplaceable codec format, `orcacore-json-v1`.

### PR-002 Engine owns semantics; providers supply capabilities
Product semantics (matching rules, dedup, serialization of execution, lifecycle) are
engine-owned and provider-neutral. Provider contracts expose the capabilities the engine
needs; broker/database specifics MUST NOT leak into workflow definitions or public
management semantics.

### PR-003 No hard infrastructure dependency
The library SHALL run with zero external infrastructure (in-memory providers) and SHALL
support relational databases, document databases, and message brokers (RabbitMQ, SQS,
Kafka, …) through adapters without changing workflow definitions.

### PR-004 Package tiers and dependency direction
The exact first-release package/project manifest SHALL be `OrcaCore`, `OrcaCore.Core`,
`OrcaCore.Engine.Ephemeral`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`,
`OrcaCore.Engine.Durable`, `OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`,
`OrcaCore.Providers.PostgreSql`, `OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`, plus outward-only
companion applications and internal test support. `OrcaCore` is the primary application
contracts/authoring package, not a dependency-only meta-package.
`OrcaCore.Runtime.Protocol` owns durable commands, committed facts, checkpoints, and
envelopes. `OrcaCore.Provider.Abstractions` owns provider ports, commit DTOs, and certification
contracts and MAY reference Runtime.Protocol because providers persist protocol records. The
reverse edge is forbidden. Application packages SHALL reference neither advanced package and
SHALL expose no advanced type in public signatures. Engine/runtime implementations MAY
reference both advanced packages; provider Adapters SHALL reference no engine implementation.

### PR-005 DAG and external integration boundary
`OrcaCore.Dag` SHALL be a separate package that depends on public OrcaCore workflow contracts;
no OrcaCore package depends on it. Kubernetes, AWS, job schedulers, and other external-work
integrations SHALL live in outward companion/integration application projects, which may remain
in the same solution. They own infrastructure SDKs, authentication/discovery, manifests,
watchers/reconcilers, and job DTOs. No Kubernetes/AWS/job-system type or SDK appears in an
  OrcaCore public signature or dependency closure, and the primary `OrcaCore` package excludes
them. A Kubernetes job gateway is an application integration, not an OrcaCore persistence or
transport provider.

The core exposes only provider-neutral identity and orchestration seams needed by such
applications: `StepOperationId`, normalized events with `EventId`, and opaque lease stop-proof
tokens. A new public provider/runtime SPI SHALL be added only after at least one concrete
integration demonstrates that it is provider-neutral; speculative job-specific SPIs are
non-conforming.

## 10.2 Provider ports (durable engine)

The durable persistence boundary SHALL be decomposed into focused ports; a reference provider
MAY compose them behind one logical transaction boundary:

### PR-010 Event store port
Append events with expected stream version (optimistic concurrency); load stream tail after a
version; load/save checkpoints. Version-conflict outcomes are first-class results, not
generic exceptions.

### PR-011 Inbox store port
Atomically accept or classify `WorkflowInboundEvent` by global `EventId` and normalized-envelope
fingerprint; retain direct/correlation records before a wait exists; persist stable definition-
fanout membership and per-target ownership; reserve/materialize compatible start-or-deliver intents;
and mark applied, duplicate, retryable failure, or poison outcomes. Accepted records have no
automatic TTL and SHALL NOT depend on source redelivery.

### PR-012 Outbox store port
Append outbound records in the commit boundary; claim/lease for dispatch; mark dispatched /
failed / poisoned; support backlog inspection (DU-031..033).

### PR-013 Projection store port
Update and query read models: instance summaries, active waits, accepted-event state, history,
typed successful outputs, DAG lineage, and trusted lease diagnostics — the backing for keyed
routing (EV-011), management queries (MG-002, DU-070), and reconciliation. Runtime keyed lookup
does not expose public instance enumeration or bulk retrieval.

### PR-014 Timer scheduler port
Schedule and cancel durable wake-ups that produce timer-fired commands after due time,
surviving restarts (EV-050).

### PR-015 Application and internal dispatch boundaries
`IWorkflowEventDispatcher` SHALL receive only materialized `WorkflowOutboundEvent` values and
return the closed success/retryable/permanent result. Internal continuation/provider dispatch uses
`IMessageDispatcher` and opaque provider records. Selector-aware claims and adapters SHALL make it
impossible for either record family to cross into the other dispatcher. Both dispatch paths SHALL
retain the observability and retry-delay strategy hooks required by DU-032.

### PR-016 Fixed payload codec contract
V1 payload serialization SHALL use the engine-owned, nonreplaceable System.Text.Json-based
codec identified as `orcacore-json-v1`. Registration validates supported type graphs and rejects
unsupported cyclic/polymorphic shapes; the same supported graph and authored order SHALL produce
the same bytes across hosts. Authors normalize unordered sets/maps. Providers store opaque
versioned payload envelopes and preserve exact bytes/format IDs; they do not choose codec,
resolver, naming policy, or polymorphism. Raw bytes never leak into application APIs, and no
serializer replacement hook is exposed by hosting.

### PR-017 Durable resource-governance store
`OrcaCore.Provider.Abstractions.ResourceGovernance.IDurableResourceGovernanceStore` SHALL expose load and one
expected-version atomic append for the `ResourceGovernancePartitionId` stream. The serialized
aggregate owns every pool, queue entry, atomic multi-pool reservation, ticket, review mark,
resize operation, stop-confirmation binding, and tombstone in that partition. Records carry
sequence, format ID, payload bytes, and checksum; append returns closed committed/conflict
outcomes. A provider that offers only per-workflow streams does not implement v1 durable leasing.
One record factory validates positive sequence, supported format, checksum, and copied payload;
the full-stream factory defensively copies and validates exact `1..Version` continuity with zero
version iff empty. Append accepts one copied non-empty batch numbered exactly after the expected
version and commits all or conflicts without partial persistence.


### PR-018 Operational statistics store
`IWorkflowOperationalStore` SHALL refresh provider-owned stuck observations and return retained
operator statistics without exposing broad application enumeration or raw provider records.

### PR-019 Provider maintenance store
`IWorkflowProviderMaintenanceStore` SHALL apply provider-owned retention, archive, purge, and poison
maintenance under host/operator policy. It SHALL preserve atomic ownership and observability rules
and SHALL NOT create public application archive/purge commands.

## 10.3 Provider invariants (certification)

### PR-020 Atomic commit boundary
A provider SHALL commit, atomically or in a clearly defined transactional chain: appended
events, checkpoint update, inbox changes, outbox records, and projection updates (or
projection work scheduling) for one accepted mutation (DU-011).

### PR-021 Per-instance concurrency guarantee
Providers SHALL support the expected-version append (or equivalent) needed for CR-040/DU-022;
concurrent conflicting commits produce exactly one winner and a detectable conflict for the
loser.

### PR-022 Deletion/retention invariants
Provider retention/cleanup honors DU-051: never remove active instances, in-flight dispatch,
lease obligations, or required dedup/confirmation tombstones. Public archive/purge commands are
deferred and are not implied by this storage invariant.

### PR-023 Queryability invariant
Runtime metadata remains queryable per DU-070 regardless of how the provider stores business
payloads.

### PR-024 Provider certification suite
The product SHALL ship a reusable, provider-agnostic invariant test suite (the acceptance
criteria in document 12 marked provider-sensitive) that any adapter must pass — compatibility
and contract tests are part of the core test surface, not left to adapter authors. Certification
SHALL include scalar round-trip and exact ordinal/case-sensitive lookup for every persisted
strong matching value regardless of provider collation; provider-global start-key binding and
definition/version/structural-fingerprint/fixed-codec `PayloadFingerprint` conflict behavior;
serialized-definition/envelope size limits enforced before persistence; fixed-codec golden
vectors for `orcacore-json-v1`; and durable resource-governance aggregate conflicts/crash points,
atomic multi-pool FIFO grants, state-based reserved-unit accounting, pending reservation,
review-mark-retains-capacity, forced-stop quarantine, resize debt/no-new-grant behavior, exact
owner reconciliation/fencing, confirmation status matrix/tombstone retention, grant/cancel/
normal-release-confirmation races, and ticket/unit conservation.
Certification SHALL use only the four named friend post-commit lease barriers — workflow pending
obligation, governance reservation, workflow activation, and governance ownership confirmation —
with immutable partition/obligation/instance/generation/fiber/scope/token/ticket/pool/unit/provider-
generation facts. Each barrier blocks after commit and before the next protocol command so
restart behavior is deterministic without timing sleeps. It SHALL also cover `AmbiguousHeld`
retry retention, no overlapping in-process leased retry, host-loss same-identity retry, quarantine
before ambiguous progression, advanced diagnostics discovery, exact `ListAsync`, closed resize
`Applied`/`Conflict`, and whole-stream/batch validation.
Runtime-created `StepOperationId` and `LeaseProtectionToken`, plus caller-created idempotent
`StopConfirmationId` and `ResourcePoolOperationId`, SHALL round-trip canonically without
cross-family substitution; exact operation/obligation identity survives reconstruction. The
caller-created values use their sole `Create(string)` factory, while runtime-created values use
their canonical parser/converter path; neither family exposes the other family's construction
members.
`DefinitionId`, `InstanceId`, `WaitId`, and `DagRunId` certification SHALL reject canonical
`Guid.Empty` text. Public/runtime failure serialization SHALL preserve the authoritative nonblank
stable `OrcaCoreException.Code`; CLR exception type names/messages are not protocol identity.

## 10.4 Ephemeral provider

### PR-030 In-memory baseline
The ephemeral engine's process-local store is the reference for shared wait matching and serialized
execution but makes no durable ingress acknowledgement promise. The in-memory durable provider SHALL
implement the complete current port contract, including pre-wait inbox retention, fanout/start
intents, public-event outbox dispatch, operational statistics, and maintenance, and SHALL run the
same provider certification as PostgreSQL and SQL Server.

## 10.5 Hosting integration (later phase)

### PR-040 Exact role-based host integration
Hosting SHALL expose exactly these reviewed entry points:

- `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`;
- `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`;
- callback-only `AddOrcaCoreDurableEventIngress()`;
- development/test-only `AddOrcaCoreInMemoryDurableProvider()`;
- production `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`;
- production `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`; and
- `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`.

Registration is idempotent only for the same role/options; conflicting duplicates fail startup.
`OrcaCore.Engine.Ephemeral` owns
`OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions` and the ephemeral builder;
`OrcaCore.Durable.Hosting` owns
`OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`, the durable builder,
`IWorkflowEventIngress`, and `IWorkflowEventDispatcher`. Provider and DAG extensions remain in
their owning package-specific classes; no public extension class is split across assemblies.
Ephemeral and durable engine roles are mutually exclusive. Durable-engine and callback-only roles
own `IWorkflowEventIngress`; only the durable engine owns definitions/workers. The
`OrcaCore.Durable.Hosting` assembly owns the `IWorkflowEventDispatcher` port type, the application
registers its implementation, and the durable engine consumes it when an authored definition uses
`Publish`. Callback-only ingress persists events and hands off continuations but registers no
definition registry, worker, timer/reconciler, dispatcher implementation, or DAG coordinator. The
in-memory durable provider makes no restart claim; PostgreSQL and SQL Server each supply one
complete certified production role. There is no catch-all registration, second hosted-service toggle,
implicit mode selection, serializer hook, binder facade, or application registration of individual
provider runtime-protocol ports.

## 10.6 Internal design conventions (extensibility-adjacent)

### PR-050 Explicit outcome primitives
Internally, the engine SHOULD use exactly three functional primitives — `Result<T>`
(expected success/failure at contract level: routing resolution, command decisions, commit
outcomes), `Option<T>` (present/absent lookups: checkpoints, waits, accepted-event records),
`Validation<T>` (accumulated authoring/build errors) — applied narrowly:

- public happy-path APIs MAY remain exception-based for ergonomics;
- no nesting like `Task<Result<Option<T>>>` without clear semantic need;
- `Validation<T>` only for build/config time, never runtime operational failures;
- no broader functional-abstraction stack.

Adoption order: builders (validation) → internal lookups (option) → command/routing/commit
paths (result).
