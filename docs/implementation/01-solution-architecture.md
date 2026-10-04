# 01. Solution Architecture

## 1. Projects and module boundaries

```text
OrcaCore.slnx
src/
  OrcaCore                      ← primary application contracts/authoring package: strong
                                  values, typed definitions/references/handles, events/results
  OrcaCore.Core                 ← private engine-agnostic compiler/runtime internals
  OrcaCore.Engine.Ephemeral     ← in-process execution and ephemeral hosting
  OrcaCore.Runtime.Protocol     ← advanced persisted runtime/governance records and results
  OrcaCore.Provider.Abstractions← advanced durable-store/transport SPIs
  OrcaCore.Engine.Durable       ← event-sourced aggregate/interpreter internals
  OrcaCore.Durable.Hosting      ← public durable engine/ingress registration and options,
                                  advanced management/recovery/diagnostics, and hosted
                                  continuation/timer/outbox/governance services
  providers/
    OrcaCore.Providers.InMemory ← complete development/test durable provider role
    OrcaCore.Providers.PostgreSql ← complete certified production durable provider role
    OrcaCore.Providers.SqlServer ← complete certified production durable provider role
  OrcaCore.Dag                  ← optional public typed DAG authoring/validation only
  OrcaCore.Dag.Hosting          ← only DAG-to-durable bridge; consumes the named versioned
                                  internal child start/join seam from Durable.Hosting
apps/ or integrations/ (working location; finalized before project creation)
  <CompanionSchedulerProject>    ← Kubernetes Job API, optional EKS/AWS composition,
                                    manifests, watcher/reconciler, cron/tenant policy; may
                                   live in this solution but is not an OrcaCore package
tests/
  OrcaCore.TestSupport           ← hand-rolled port fakes, FakeTimeProvider harness,
                                   builders for envelopes/definitions (shared library)
  OrcaCore.Core.Tests
  OrcaCore.Engine.Ephemeral.Tests
  OrcaCore.Engine.Durable.Tests
  OrcaCore.Acceptance.Tests      ← AC-xxx catalog tests against PUBLIC API only
  OrcaCore.ProviderCertification ← abstract test classes (a library, not a test project):
                                   the PR-024 suite any provider must pass
  OrcaCore.Providers.PostgreSql.Tests   ← inherits certification suite + Testcontainers
  OrcaCore.Providers.SqlServer.Tests    ← inherits certification suite + Testcontainers
```

The twelve `OrcaCore*` entries above are the exhaustive first-release package manifest. Every
manifest project packs under its exact project/package ID. `OrcaCore` is the primary application
package, not a dependency-only meta-package; optional engine, provider, advanced, and DAG roles are
selected by referencing their owning packages explicitly.

Public ownership is exact: `OrcaCore.Engine.Ephemeral` owns the ephemeral extension class/options;
`OrcaCore.Durable.Hosting` owns the durable engine/ingress extension class, durable host options,
and advanced durable management/recovery facade; `OrcaCore.Providers.InMemory` owns its
development/test provider extension; `OrcaCore.Providers.PostgreSql` owns its production provider
extension/options; `OrcaCore.Providers.SqlServer` owns its production provider extension/options;
`OrcaCore.Dag` owns typed DAG authoring/validation; and
`OrcaCore.Dag.Hosting` owns DAG options, registration, coordination, and the sole durable friend
bridge. `OrcaCore.Core` and `OrcaCore.Engine.Durable` own implementation internals;
`OrcaCore.Runtime.Protocol` and `OrcaCore.Provider.Abstractions` own their respective advanced
records and SPIs.

## 2. Dependency rules (enforced; violations fail review)

```text
OrcaCore.Core                  -> OrcaCore
OrcaCore.Engine.Ephemeral      -> OrcaCore + OrcaCore.Core
OrcaCore.Runtime.Protocol      -> OrcaCore
OrcaCore.Provider.Abstractions -> OrcaCore + OrcaCore.Runtime.Protocol
OrcaCore.Engine.Durable        -> OrcaCore + OrcaCore.Core + OrcaCore.Runtime.Protocol
                                 + OrcaCore.Provider.Abstractions
OrcaCore.Durable.Hosting       -> OrcaCore + OrcaCore.Engine.Durable
                                 + OrcaCore.Provider.Abstractions
each provider adapter          -> OrcaCore + OrcaCore.Runtime.Protocol
                                 + OrcaCore.Provider.Abstractions
OrcaCore.Dag                   -> OrcaCore
OrcaCore.Dag.Hosting           -> OrcaCore.Dag + OrcaCore.Durable.Hosting
```

This direct-edge list is exhaustive. In particular, `OrcaCore.Durable.Hosting` receives protocol
types only through its declared dependencies and must not add a direct `Runtime.Protocol` edge.
Architecture checks reject every unlisted reference and every friend assembly outside the
current exact closed graph below. The separately approved `OrcaCore -> OrcaCore.Dag` authoring
friend is compiled in the independently approved Task 8.2 checkpoint
`a9f835f939d683500ca231c7ba491ab8eae2aaae` and included in that graph.

- `OrcaCore.Dag` depends directly only on the `OrcaCore` package. Its workflow references
  remain public application contracts; the approved post-gate authoring friend allows only
  the five compiler-created build-value constructors and one shared fingerprint operation,
  enforced at exact compiled-member signatures after approval. It never references engine
  internals, a provider implementation, Kubernetes, or AWS. It owns typed authoring and
  validation only and makes no v1 visualization or renderer promise.
- `OrcaCore.Dag.Hosting` is the sole bridge from a typed DAG plan to durable child execution.
  It depends on `OrcaCore.Dag` and `OrcaCore.Durable.Hosting` and uses one named, versioned
  internal child-start/join contract through
  `InternalsVisibleTo("OrcaCore.Dag.Hosting")`. The seam is same-solution/release-train internals,
  not a public workflow or provider SPI; no second DAG-to-durable product bridge is permitted
  without a matrix amendment. The independently approved ninth `OrcaCore.Dag -> OrcaCore.Dag.Hosting`
  runtime-view friend lets the candidate adapter consume only three internal types and fifteen
  exact method/getter signatures. Its contract is independently approved at `43d869fc29e7daa3ec567d4602960f458eb98492` and canonical at independently approved checkpoint `a0da21ba9597e3864a3d4134120fbb0138417bd7`; compiled in this source candidate, awaiting independent source approval/checkpoint, changes no package edge,
  and exposes no mapper delegate, draft, constructor, codec or child API.
- The companion scheduler depends outward on `OrcaCore.Dag.Hosting` plus documented public
  application/hosting packages. No dependency points from OrcaCore back to the companion.
- **No core or engine project depends on a provider adapter.** Durable hosting consumes advanced
  ports (`IWorkflowEventStore`, `IMessageDispatcher`, `ITimerScheduler`, …) from
  `OrcaCore.Provider.Abstractions`; an
  application composition root may reference explicit provider-registration packages and wire
  them through DI. This is the "program to interfaces" rule made structural.
- Provider roles are **explicitly registered** by their owning package. The production calls are
  exactly
  `OrcaCore.Providers.PostgreSql.OrcaCorePostgreSqlProviderServiceCollectionExtensions.AddOrcaCorePostgreSqlDurableProvider(IServiceCollection, PostgreSqlDurableProviderOptions)` and
  `OrcaCore.Providers.SqlServer.OrcaCoreSqlServerProviderServiceCollectionExtensions.AddOrcaCoreSqlServerDurableProvider(IServiceCollection, SqlServerDurableProviderOptions)`.
  The get-only `ConnectionString` and `Schema` values are fixed by programmatic construction,
  copied, and rejected when null/empty/whitespace before any partial provider registration. There
  is no shorter provider alias, raw connection-string overload, configuration-binding overload,
  reflection discovery, cross-provider facade, or partial provider-role registration.
- `Providers.InMemory` is a complete certified development/test provider, not test code: it is the
  executable documentation of port semantics, but it makes no process-restart durability claim.
- Kubernetes, EKS/AWS, and external-job adapters are application/integration concerns, not
  OrcaCore provider roles. They live in separate outward-dependent project(s), even while
  sharing `OrcaCore.slnx`; no OrcaCore package takes their SDK dependency.
- `OrcaCore.Dag` owns typed node/dependency mapping and validation. `OrcaCore.Dag.Hosting`
  evaluates each opaque direct-dependency mapping at runtime after dependencies succeed; the
  approved-pending runtime view receives detached successful outputs decoded by the durable bridge before
  evaluation. Codec normalization/fingerprinting/commit remain on that bridge, before
  mapped input is committed or a child starts. Invalid access or projector failure produces stable
  `DAG_INPUT_MAPPING_INVALID` and starts no child for that node. The host then starts/reattaches one
  durable child workflow instance through the internal bridge; neither package exposes a
  general-purpose public `RunChildren` node.

Phase 0 packs the exact manifest as version `0.0.0-phase0` to
`artifacts/phase0-packages`. Clean fixtures restore it through `PackageReference` only; this local
feed is verification infrastructure, not an external-publishing or release-version promise. The
fixture runner rebuilds and repacks every manifest project before use, then checks the packaged
implementation assembly against the current Release output and the checked-in normalized
per-package source SHA-256 record. A missing or superseded feed therefore fails before consumer
compilation.

## 3. Public vs internal

- Ordinary public surface: approved immutable values, staged builders, definitions/references,
  engine/hosting facades, typed snapshots/state/output, cancellation/termination operations, and
  the focused `OrcaCore.Dag` authoring package. Replaceable behavior seams are interfaces in their
  owning tier; approved immutable values/builders/definitions/outcomes are concrete public types.
  Other implementation collaborators remain `internal`, `sealed` by default.
- Current product friends are exact: `OrcaCore` grants `OrcaCore.Core`,
  `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and `OrcaCore.Dag` for authoring;
  `OrcaCore.Core` grants both engines;
  `OrcaCore.Engine.Durable` grants `OrcaCore.Durable.Hosting`; and
  `OrcaCore.Durable.Hosting` grants `OrcaCore.Dag.Hosting`; and `OrcaCore.Dag` grants
  `OrcaCore.Dag.Hosting` for the exact runtime view. The authoring-only eighth
  edge grants no durable child-start access; its source is independently approved at Task 8.2
  checkpoint `a9f835f939d683500ca231c7ba491ab8eae2aaae`.
  Owning white-box test friends are exact for Core, both engines, Durable Hosting, PostgreSQL, and
  SQL Server;
  `OrcaCore.Engine.Durable -> OrcaCore.ProviderCertification` is the sole cross-package test edge.
  Acceptance, behavior-scenario, compile-fixture, and integration projects get **no** internals and
  prove the public or advanced provider contract is sufficient.
- Microsoft-hosting composition is role-specific: application hosts call
  `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)` or
  `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`; callback-only hosts call
  `AddOrcaCoreDurableEventIngress()`. Development/tests may add
  `AddOrcaCoreInMemoryDurableProvider()`, production durable hosts add
  either `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)` or
  `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`, and DAG hosts add
  `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)` on top of the durable-engine role.
  `OrcaCore.Engine.Ephemeral` owns
  `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions`; `OrcaCore.Durable.Hosting`
  separately owns `OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`. No public
  extension class is partial or duplicated across assemblies. Registration copies/validates
  programmatically constructed options, freezes an immutable owned copy, and includes its hosted
  loops. The options are not advertised as `IConfiguration`-binder DTOs. A catch-all
  `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, or codec replacement
  hook is forbidden.
- Ephemeral and durable engine roles are mutually exclusive in one service provider. Each selected
  engine role owns its definition registry and execution services. Durable-engine and callback-only
  roles expose `IWorkflowEventIngress`; the callback-only role owns event persistence and
  continuation handoff without registering a definition registry, worker, timer/reconciler, or DAG
  coordinator. Outbound workflow events use `IWorkflowEventDispatcher`, never the internal
  continuation dispatcher.
- Workflow/DAG registration and start return inspectable closed result unions. Their typed
  `GetHandleOrThrow()` helpers are only cast-free success projections. Output and DAG-terminal
  waits are notification-driven subscribe-then-recheck operations; they never poll, and caller
  cancellation cancels only the local wait.
- The four Phase 0 resource-governance crash barriers are friend-only test seams named
  `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`,
  `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`. They expose fixed-codec
  immutable facts only and do not create a public authoring/runtime API.

## 4. Port catalog (interfaces allowed here; signatures indicative, not code)

Provider ports and their commit DTOs are advanced contracts in `OrcaCore.Provider.Abstractions`
and are implemented by provider adapters. `OrcaCore.Runtime.Protocol` separately owns advanced
durable commands, facts, checkpoints, and resource-governance records (spec PR-010…016). Neither
tier is an ordinary workflow-application reference:

- `IWorkflowEventStore` — load one checkpoint, append one complete `ProviderCommitBatch` with
  optimistic concurrency, and load a stream tail after one `StreamVersion`.
- `IWorkflowInboxStore` — get one persisted inbox record by `(InstanceId, EventId)`; accepted inbox
  writes travel atomically in `ProviderCommitBatch`.
- `IWorkflowStartIdempotencyStore` — get one persisted start binding by idempotency key; accepted
  bindings travel atomically in `ProviderCommitBatch` with definition and input fingerprints.
- `IWorkflowOutboxStore` — claim/lease `OutboxWrite` records carrying the provider-maintained
  positive dispatch-attempt ordinal, inspect one record state, mark a result, or release a claim.
  Newly committed outbox writes start with attempt zero and travel atomically in `ProviderCommitBatch`.
- `IWorkflowProjectionStore` — apply commit projections, get one exact instance projection, find
  exact active-wait routing candidates, and list trusted lease-recovery candidates. Public workflow
  instance enumeration, bulk management, and history remain deferred or absent; durable inbox
  records, including accepted pre-wait events, are provider-owned runtime state rather than an
  application mailbox.
- `IWorkflowOperationalStore` — refresh stuck-state observations and return provider/operator
  statistics without exposing broad application enumeration.
- `IWorkflowProviderMaintenanceStore` — apply provider-owned retention, archive, purge, and poison
  maintenance behind operator policy; it does not create public workflow archive/purge commands.
- `ITimerScheduler` — schedule durable wake-ups, claim due `FireTimerCommand` records, and complete
  or release one claim.
- `IDurableResourceGovernanceStore` — load one serialized governance aggregate per configured
  provider partition and expected-version append one complete ordered record batch. The aggregate
  owns every pool definition, atomic multi-pool request, ticket, review mark, resize,
  confirmation binding, and tombstone. Partial append, time-only reclaim, and force release are
  forbidden (MG-062…065).
- `IMessageDispatcher` — `DispatchAsync(OutboxWrite, ct) → DispatchResult`
  (`Success`/`RetryableFailure`/`PermanentFailure`).
- Provider payload envelopes preserve the fixed workflow-state codec bytes and format identity.
  The v1 workflow-state codec is certified `System.Text.Json` format `orcacore-json-v1`, not a
  replaceable provider/host SPI. Registration rejects unsupported cyclic/polymorphic state shapes.

These ports are provider/runtime SPIs: advanced interfaces implemented by storage, transport,
or custom-host authors. They are not ordinary workflow-author APIs. In particular, neither
`IMessageDispatcher` nor any other port makes public `RunExternalJob` part of v1. A companion
Kubernetes scheduler uses public workflow/DAG/management contracts plus its own adapter seam.

Each selected provider registers its named `ActivitySource` and `Meter` as a stable ownership
hook. The v1 provider-commit and operational instruments remain engine/hosting-owned so a provider
does not duplicate or proxy the same measurement under a second meter. Provider-specific
instruments require an explicit observability-contract addition; an empty provider meter is
therefore intentional, not evidence that engine-owned provider telemetry is absent.

The **atomic commit boundary** (PR-020) is the `ProviderCommitBatch` accepted by
`IWorkflowEventStore.AppendAsync`. A provider commits its events, optional checkpoint, inbox and
start-idempotency records, outbox writes, projections, and timer work as one accepted mutation;
the other split ports expose the corresponding read/claim/maintenance operations.

## 5. Concurrency model (Channels + TPL)

- **Per-instance execution lane**: one bounded `Channel<InstanceWork>` (or semaphore lane in
  the ephemeral engine) serializes all mutations of an instance — the CR-040 guarantee.
- **Execution-path tokens**: a runnable root/branch/item owns one host-governed token and releases
  it before a wait, delay, resource request, or join. A parent releases before fan-out admission
  and reacquires only for merge/continuation, so a ceiling of one makes progress. A parked
  `ForEach` item still counts against the node-local admitted-item cap even though it has no path
  token.
- **DAG-node admission**: `DagHostOptions.MaxConcurrentNodes` counts admitted nonterminal nodes,
  including child workflows parked in a wait, delay, or lease queue, until each child is terminal.
- **Durable lease scopes**: `AcquireResources(request, body)` exists only at the durable root,
  within root `If`/`While` bodies, and in independent durable branch/item bodies when no live
  ancestor lease exists. Retryable timeout, ambiguous submit, or recovered in-flight work keeps
  the same operation/token/tickets/capacity in `AmbiguousHeld`; quarantine occurs atomically only
  before parent/join progression when ambiguity survives scope exit, retry exhaustion,
  cancellation, deadline, termination, or abandonment.
- **Detached step attempts**: each attempt mutates a codec-detached state copy. A timeout fences
  commit and releases the logical path token, but any granted physical step/transient slot remains
  held until the body returns.
- **Outbox pump**: bounded channel of claimed batches → dispatcher; retry-delay strategy and
  observer hooks as small interfaces (DU-032).
- **Timer service**: min-heap over `TimeProvider` + channel of due firings.
- All loops: `async Task` with cooperative `CancellationToken`; no `Thread`, no `Timer`
  callbacks mutating state directly, no `async void`, no blocking waits (`.Result`/`.Wait()`).

## 6. Module growth rule

New capability = new internal module (folder with an internal interface + implementation)
inside the owning project, or a new plugin project if it touches external infrastructure.
Adding a cross-cutting concern into an existing class instead of a seam is the primary
architecture smell reviewers reject.
