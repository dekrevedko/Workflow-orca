# OrcaCore Production Readiness Notes

OrcaCore is still pre-production in this implementation track. V1 defines local package IDs and
requires every documented tier to pack and pass clean application/provider/custom-host/DAG
consumer certification. External registry publishing, signing, SourceLink release configuration,
and semantic-versioned release automation remain deferred until the owner reopens them.

The exhaustive first-release package manifest is `OrcaCore`, `OrcaCore.Core`,
`OrcaCore.Engine.Ephemeral`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`,
`OrcaCore.Engine.Durable`, `OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`,
`OrcaCore.Providers.PostgreSql`, `OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`. `OrcaCore` is the
primary application contracts/authoring package, not a dependency-only meta-package. Phase 0 packs
these exact IDs as `0.0.0-phase0` to `artifacts/phase0-packages`; clean fixtures restore them via
`PackageReference` only. This local feed is test evidence, not a publication commitment.

The approved first-release surface is [spec 17](specs/17-selected-mode-capability-matrix.md).
Sections 4 through 7 are implemented and checkpointed; the post-checkpoint Section 7A closure is
removing residual non-event public-surface and test-evidence gaps. Pending Section 7B proposes a
replacement durable messaging/application-catalog contract, but the delivery guarantees below
remain the approved matrix semantics until task 7.23 approves that amendment. No provisional source
member is a compatibility promise. Section 8 remains blocked until the combined Section 7A/7B
target receives independent approval and its mandatory coherent checkpoint commit, and until the
revised non-conflicting `harmonize-downstream-capability-specs` remainder is approved, synchronized,
independently reviewed, and checkpointed.

## Delivery Guarantees

- Durable mode targets at-least-once outbox dispatch.
- Durable mode targets exactly-once committed effect per workflow instance through
  event-id deduplication and serialized expected-version commits.
- OrcaCore does not claim exactly-once delivery to external systems. External consumers
  must remain idempotent.
- Ephemeral mode is in-process only. Its lifecycle events and state are queryable only while
  process-owned runtime state exists; no first-release Saga surface is claimed.
- `Wait` is the single public event-suspension concept. Durable hosts may evict and rehydrate any
  parked wait without changing workflow meaning; `WaitLong` is removed rather than aliased.
- Each step attempt receives a fixed-codec detached copy of committed state and may use
  `StepContext<TState>.ReplaceState`; failed/timed-out/fenced copies cannot commit. V1 fixes the
  certified format to `orcacore-json-v1` and has no serializer replacement hook.
- Durable event deduplication is per target `InstanceId` by `EventId`; correlation routing admits
  exactly one active wait for `(DefinitionId, EventName, CorrelationId)`. Definition-targeted
  fanout is deferred. Pre-wait `NoActiveWait` does not consume the `EventId`, so the same envelope
  may be accepted after the wait registers.
- Typed workflow and DAG registration/start results remain inspectable closed unions;
  `GetHandleOrThrow()` is a cast-free success projection, not a replacement for conflict
  inspection. `WaitForOutputAsync` and DAG terminal waits use notification plus recheck and never
  poll; caller cancellation ends only the local wait.
- `WithStepTimeout` fences one attempt and `CompleteWithin` preserves one original absolute
  deadline across `ContinueAsNew`; neither proves ambiguous external work stopped.
- Durable resource capacity is owned by one serialized governance aggregate per configured
  provider partition. `AcquireResources(request, body)` is legal only at a durable root, inside
  root `If`/`While` bodies, and in independent durable branch/item bodies when no live ancestor
  lease exists. Retryable timeout, ambiguous submit, and recovered in-flight work retain the same
  operation/token/tickets/capacity in `AmbiguousHeld`; if ambiguity survives scope exit,
  exhaustion, cancellation, deadline, termination, or abandonment, it transfers atomically to
  `Quarantined` before parent/join progression. Review time marks ownership for reconciliation;
  there is no force release, renewal, expiry reclaim, or time-only reclaim.
- The four deterministic resource-reservation crash barriers are friend-only test seams named
  `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`,
  `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`. They expose immutable
  correlated facts only and are not future public API.
- DAG mapping code is opaque. The runtime validates `OutputOf` access after direct dependencies
  succeed and before mapped-input commit or child start; invalid access/projector failure records
  `DAG_INPUT_MAPPING_INVALID` and starts no child for that node. `MaxConcurrentNodes` counts
  admitted nonterminal children even while parked, and v1 makes no DAG visualization promise.
- Durable history retention is policy governed. Providers are not required to retain full
  stream history forever, but must preserve the active retention window and essential
  operational facts required for management, recovery, audit, and compliance-oriented
  inspection.
- Definition-driven durable execution is production-gated on document 16: durable
  interpreter, execution-position envelope, lane host, restart-safe continuation signal,
  hard segment budgets, and provider certification. Manual `DurableCommandProcessor` tests
  are useful lower-level coverage but are not sufficient for a production durable engine
  claim.

## Security Checklist

- The library exposes no network listener by itself. Host applications own every HTTP,
  queue, worker, dashboard, and operator-facing endpoint.
- Host applications own authentication, authorization, tenant isolation, rate limits, and
  destructive-operation approval for management commands.
- V1 operator actions are cancellation request, termination, durable pool resize, and trusted
  protected-work stop/fence confirmation. They must be protected by host policy before exposure.
  Pause, resume, failed-instance retry, archive, purge, and force release are not v1 operations.
- Payloads are opaque data at provider boundaries. Do not add dynamic type resolution from
  untrusted payload content.
- Providers and dispatchers must keep credentials in host configuration or platform secret
  stores. Do not bake credentials into workflow definitions or serialized payloads.
- Destructive retention commands must preserve active instances and in-flight dispatch
  safety.
- Durable management termination requires explicit destructive safety confirmation in the
  library API. Host applications must still authenticate and authorize the operator before
  passing that confirmation. Retention/purge remains provider/host policy outside the v1 public
  management surface.
## Versioning Policy

- This workspace has no published package stability promise yet.
- API shapes, provider schemas, and projection formats may change before the packaging gate.
  Because the project is greenfield, removed/deferred provisional members receive no obsolete
  alias, tombstone, or placeholder.
- Breaking changes must update implementation docs, provider certification expectations,
  and sample host guidance in the same change.
- Structural fingerprints cover inspectable authored structure only. Changing selector,
  projector, merge/output code, step construction/configuration, DAG mapping, or external-request
  construction requires a new `DefinitionVersion`; opaque-code changes are not fingerprint
  conflicts in v1.
- Provider schemas need explicit migration notes before any production packaging gate.
- DynamoDB implementation is deferred. Shared provider contracts should remain compatible
  with a future adapter, but no AWS package or table design is part of this run.
- `OrcaCore.Dag` is optional and outward-dependent; `OrcaCore.Dag.Hosting` is the only bridge to
  the named/versioned internal child seam in `OrcaCore.Durable.Hosting`. Kubernetes, AWS, job scheduling, and their
  SDK/DTO types live in companion projects (which may share the solution) and never enter an
  OrcaCore public signature or the primary application package dependency closure.

## Benchmarks

BenchmarkDotNet scenarios live in `benchmarks/OrcaCore.Benchmarks`. Normal PR CI
builds the benchmark project but does not run benchmarks.

Run a short local smoke pass:

```powershell
dotnet run --project benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj -c Release -- --filter *ProviderCommitBenchmarks.AppendCommitBatch* --job Dry
```

Run the full local benchmark suite:

```powershell
dotnet run --project benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj -c Release
```

The current suite covers the ephemeral execution loop, provider
serialization/materialization, management query/projection path, resource pool and timer
scheduling, and provider commit path. External provider benchmark profiles and hard
threshold gates are later production-readiness work.

## Sample Host

The sample host at `samples/OrcaCore.SampleHost` must migrate to Microsoft hosting's exact
role-specific OrcaCore registration:

- `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)` for an ephemeral execution role, or
  `AddOrcaCoreDurableEngine(DurableEngineHostOptions)` for a durable execution role including its
  hosted loops. One service provider cannot select both engine roles. The selected role owns its
  definition registry, execution services, and `IWorkflowEventClient` routing.
- `AddOrcaCoreDurableEventIngress()` for a definition-less callback role; it does not register a
  definition registry, execution worker, timer/reconciliation loop, or DAG coordinator.
- `AddOrcaCoreInMemoryDurableProvider()` only for development/tests; it makes no process-restart
  claim. `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)` adds only DAG
  coordinator/registry on top of a durable-engine role.
- There is no catch-all `AddOrcaCore`, separate `AddOrcaCoreHostedServices`, implicit mode
  selection, serializer replacement hook, provider alias, or direct configuration-binder surface
  in v1. `OrcaCore.Engine.Ephemeral` owns
  `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions`, while
  `OrcaCore.Durable.Hosting` owns the separate
  `OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`; neither class is split
  across assemblies.
- Durable hosted loops execute outbox, timer, continuation, and resource-governance review/
  reconciliation work; review deadlines never release capacity by elapsed time.
- `OrcaCore.Providers.PostgreSql` owns exactly
  `OrcaCorePostgreSqlProviderServiceCollectionExtensions.AddOrcaCorePostgreSqlDurableProvider(IServiceCollection, PostgreSqlDurableProviderOptions)`.
  The one call registers the complete certified production durable role. Its programmatically
  constructed get-only `ConnectionString` and `Schema` are copied and rejected when null, empty,
  or whitespace before any partial provider services become visible; there is no raw-string or
  configuration-binding overload.

The ordinary sample flow keeps the closed registration/start results available for conflict
inspection, uses `GetHandleOrThrow()` for the success path, and awaits
`start.WaitForOutputAsync(token)` rather than polling snapshots.

The sample may intentionally use the development/test in-memory provider so it can build and
smoke-test without external infrastructure, but it must not imply restart durability.
