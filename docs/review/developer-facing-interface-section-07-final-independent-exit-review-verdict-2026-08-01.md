# Developer-Facing Interface — Section 7 Final Independent Exit Review Verdict

**Date:** 2026-08-01
**Disposition:** Immutable audit-only review record
**Scope:** Whole current Section 7 target (added, modified, renamed, deleted, compile-excluded, orphaned)

---

## 1. Verdict

# REJECT

The exact frozen target SHALL NOT receive the Section 7 checkpoint commit. Task `8.0` remains
blocked. No Section 8 work is authorized.

This verdict is binary and carries no "approve with changes" option. The target fails at the
planning gate, at the task-completion gate, at the public-surface gate, at the behavioral gate, at
the deletion-burden gate, and at the test-evidence gate independently. Any one of the seven P1
findings below is sufficient for rejection on its own.

---

## 2. Provenance and manifest hashes

Reproduced independently at review start; re-reproduced unchanged after every validation command.

| Item | Value |
|---|---|
| Branch | `feature/v3-rebuild` |
| HEAD | `ac46d99543daf85c0fa3234272997ba40f47f96b` |
| HEAD tree | `28f4033c4773ea7761afa905c9836fd25866f1c0` |
| `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` ancestry | confirmed ancestor of HEAD |
| `50254d08175431896d580ecfcc93d8e49e1c2ec7` ancestry | confirmed ancestor of HEAD |
| Ordered `git status --porcelain=v1` entries | 419 |
| Raw ordered-manifest SHA-256 | `e029d51dfd771f8c34811b8952ef5fdbe12b8b4f0874520af8443290666a671f` |
| LF-normalized sorted-path SHA-256 | `52fae12b84a553cc9ddbd608e87acad15f10d533a936c8d8e1266d7cbbc8028b` |

### Status-class counts

| Class | Count | Meaning |
|---|---|---|
| ` M` | 332 | tracked, modified, unstaged |
| `??` | 40 | untracked entries (41 files after directory expansion) |
| `R ` | 29 | staged renames |
| ` D` | 17 | tracked, deleted, unstaged |
| `RM` | 1 | staged rename plus unstaged modification |
| **Total** | **419** | |

Tracked/untracked split: 379 tracked path entries (332 modified + 17 deleted + 30 rename entries)
and 41 untracked files. Added (untracked) 41, modified 333 (including the `RM` entry's
modification), deleted 17, renamed 30.

The orientation values supplied in the review request reproduced exactly. No unexplained drift was
found at freeze time.

---

## 3. Planning and specification consistency — **FAIL**

### P1-A — Seventeen Section 7 tasks are open; the change's own ledger blocks the checkpoint

`openspec/changes/reshape-developer-facing-interfaces/tasks.md` (working-tree copy, the current
authority) carries **155 tasks: 108 complete, 47 open**. `openspec list` independently reports
`108/155`. Section 7 alone has **17 open tasks**:

| Section | Open tasks |
|---|---|
| 7A | `7.16`, `7.17`, `7.19`, `7.20`, `7.22` |
| 7B | `7.23`, `7.24`, `7.25`, `7.26`, `7.27`, `7.28`, `7.29`, `7.30`, `7.31`, `7.32`, `7.33`, `7.34` |

The whole of Section 7B — durable messaging, event contracts, ingress, inbox, fanout,
start-or-deliver, `Publish`, dispatcher, and provider persistence — is **unimplemented**. The
Section 7 disposition text in the same file states: "Task 8.0 remains blocked until every task below
is complete, the target is refrozen, a focused independent review approves it, and the exact
approved target is checkpointed." Task `8.0` itself is labelled **BLOCKED**.

Task `7.22` — "run the combined post-Section-7 validation packet, freeze one new exact manifest, and
obtain focused immutable independent approval before the mandatory coherent checkpoint commit" — is
the gate this review would satisfy, and it explicitly requires *every* Section 7A and 7B task to be
complete first. That precondition is not met. The target is not a completed Section 7; it is
Section 7A partially closed with Section 7B entirely unstarted.

The task-accounting guard `ChangeTaskLedger_CountsLetterSuffixedIdsAndHasNoDuplicates` passes and
correctly counts the four letter-suffixed IDs (`3.11a`–`3.11d`). Task accounting is internally
sound; the accounting simply reports an incomplete section.

### P1-B — Task `7.23`'s amendment is unapproved, yet its semantics are already written into the proposal and its inverse is already deleted from source

The working-tree `openspec/changes/reshape-developer-facing-interfaces/proposal.md` has been
rewritten (uncommitted) to require `IWorkflowEventIngress`, pre-wait inbox buffering, cold
reactivation, definition fanout, exact-definition start-or-deliver, durable `Publish`,
`IWorkflowEventDispatcher`, explicit `WorkflowEventContract` descriptors, and mode-specific engine
builders with `AddWorkflow`. Its **Modified Capabilities** list now names `event-routing-and-waits`
and `durable-persistence-and-outbox`.

Neither capability has a delta directory under
`openspec/changes/reshape-developer-facing-interfaces/specs/`. The change declares two modified
capabilities it does not describe. Task `7.23` names exactly this defect ("add the missing
`event-routing-and-waits` and `durable-persistence-and-outbox` deltas") and is unchecked.

Meanwhile the *inverse* contract has already been deleted from product source under pre-7B credit:
`WorkflowDeliveryBufferedEvent`, `WorkflowDeliveryDiscardedEvent`, and `WorkflowTimerBufferedEvent`
are absent from `src/`, and the aggregate buffering replay paths were retired. Task `7.28` will need
route-level and per-instance pending inboxes; the protocol that carried buffering has been removed
before the amendment authorizing its replacement was approved. This is precisely the ordering the
review request forbids: "Reject event/command/protocol deletions or replacements that precede task
7.23 approval."

### P1-C — `harmonize-downstream-capability-specs` contradicts `reshape` and is itself unapproved

`harmonize-downstream-capability-specs` stands at **4/31 tasks**. Task `1.4` (independent approval)
is open, and tasks `2.1`/`2.2`/`2.3` (canonical synchronization) are open.

Its `specs/event-routing-and-waits/spec.md` still `ADDED`s "Events are not buffered before wait
registration", still fixes the closed delivery result at
`Accepted`/`Duplicate`/`NoActiveWait`/`InstanceTerminal`/`EventConflict`, still `REMOVED`s
"Event routing supports direct, correlation, and fanout targeting" with the reason "Definition-
targeted fanout is deferred beyond v1", and still constrains `IWorkflowEventClient` to exactly two
routes.

The live reshape proposal requires the opposite on every one of those points. Reshape task `7.23`
directs that this "contradictory non-buffering/fanout-removal ownership" be removed from harmonize
**before either change synchronizes canonical specs**. It has not been removed.

`openspec validate --all --strict` returns **18 passed / 0 failed**. Both contradictory changes
validate. This is the exact condition `CLAUDE.md` warns about: strict validation checks structure,
not provenance, and cannot detect two valid changes that assert opposite semantics. It is not
evidence of workflow compliance and is not treated as such here.

Canonical synchronization has correctly **not** occurred for harmonize —
`openspec/specs/event-routing-and-waits/spec.md` is unmodified in the working tree. Only
`openspec/specs/quality-and-verification/spec.md` and `openspec/specs/state-driven-runtime/spec.md`
appear as modified, and `git diff HEAD` shows no content delta for them (line-ending normalization
only). No unauthorized canonical hand-edit was found.

### Stale coordination note (informational, not blocking)

Harmonize task `5.1` asserts an `ActiveWaitSnapshot` projection leak exposing
`FiberId`/`ScopeId`/`WaitSequence`/`Mode` with `AuthoredLocation` and deadline missing. The live
type at `src/OrcaCore.Abstractions/Facades/WorkflowFacadeContracts.cs:183` is
`ActiveWaitSnapshot(WaitId, AuthoredLocation, EventName, CorrelationId, RegisteredAt, Deadline?)` —
correct, with no routing identities. Harmonize's coordination record is stale, not the code.

### Historical separation (informational)

The 2026-07-31/08-01 archive migrations are consistent: `docs/normative-source-map.md` classifies
every tree, historical material moved with `git mv` into `docs/archive/`, and the untracked
`docs/end-to-end-plan.md`, `docs/ephemeral-engine-developer-guide.md`,
`docs/ephemeral-engine-diagrams.md`, and `docs/orleans-engine/README.md` are newly authored
active-path replacements for archived predecessors, not resurrected history. One move was performed
without `git mv`: `docs/implementation/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md`
shows as an unstaged deletion with an untracked copy under `docs/archive/plans/`, so
`git log --follow` will not traverse it. `docs/review/` records are unmodified.

---

## 4. Whole-diff implementation findings

Audited `d76192f → 50254d0`, `50254d0 → HEAD`, `50254d0 → working tree`, and
`d76192f → working tree`.

### P1-D — The entire public authoring facade is implemented as a name-based reflection bridge

`src/OrcaCore.Abstractions/Internal/AuthoringKernelProxy.cs` (untracked-in-place; present and
compiled) crosses the `OrcaCore` → `OrcaCore.Core` assembly boundary by:

- `Assembly.Load("OrcaCore.Core")` with a `FileNotFoundException` catch converted to a runtime
  `InvalidOperationException`;
- `GetType(typeName, throwOnError: true)` keyed on **string** type names;
- `GetMethods(...)` matched by **method name**, generic-argument count, and parameter count;
- `MakeGenericMethod` on the late-bound match;
- `GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, ...)` invoked to wrap callback
  objects across the boundary.

Its callers are `Authoring/WorkflowAuthoringFacades.cs`, `Authoring/NestedAuthoringFacades.cs`, and
`Authoring/BranchAuthoringFacades.cs` — the public authoring surface itself.

`src/OrcaCore.Core/Properties/AssemblyInfo.cs` grants `InternalsVisibleTo` only to
`OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and `OrcaCore.Core.Tests`. It does **not**
grant it to `OrcaCore`. The application package therefore has no typed access to the kernel and
substitutes runtime reflection for the approved friend graph.

This violates task `7.17` ("do not replace public leaks with reflection bridges"), the manifest rule
that "implementation package boundaries use an exact internal-friend allowlist", and the review
condition "no reflection bridge bypasses the approved friend graph". Every authoring call is
late-bound and unverified at compile time; trimming and AOT are broken; a renamed kernel method
fails at runtime, not at build.

The repository's own guards agree. Both fail:

- `RemovedCapabilitiesAndConstructionHelpers_HaveNoProductSourceBridge` →
  `src\OrcaCore.Abstractions\Authoring\BranchAuthoringFacades.cs -> AuthoringKernelProxy`
- `RemovedCapabilitiesAndConstructionHelpers_HaveNoProductMetadataType` →
  `OrcaCore.Core::OrcaCore.Core.Authoring.AuthoringContractFactory`

### Friend-graph exception requiring explicit approval

`src/OrcaCore.Durable.Hosting/Properties/AssemblyInfo.cs` grants `InternalsVisibleTo("OrcaCore.Hosting.Tests")`.
`OrcaCore.Hosting.Tests` is not the owning unit-test assembly for `OrcaCore.Durable.Hosting`, and the
assembly whose name it carries (`OrcaCore.Hosting`) no longer exists. The matrix names it as a
white-box owner at `docs/specs/17-selected-mode-capability-matrix.md:1953`, so this is a naming
carry-over rather than a new leak, but it is not the "exact owning unit-test assembly" the manifest
describes and should be renamed or re-approved explicitly.

All other friend edges match the approved graph: Core → both engines, Durable engine → Durable
hosting, Durable hosting → DAG hosting, Durable engine → ProviderCertification, and exact owning
test assemblies.

### P1-E — Greenfield violation: PostgreSQL ships a provisional-schema upgrade path

`src/OrcaCore.Providers.PostgreSql/Migrations/` contains seven ordered migrations. Three are pure
schema-evolution steps against tables created by `001_initial.sql`:

| File | Shape |
|---|---|
| `002_claim_leases.sql` | `alter table orcacore_outbox`, `alter table orcacore_timers` |
| `004_stream_version.sql` | `alter table orcacore_instance_projections` |
| `005_checkpoint_runtime_state.sql` | `alter table orcacore_checkpoints` |

Review rule 9 rejects "provisional-schema upgrade paths", and §6 GREENFIELD DDL requires a
"complete first-create schema". A repository with no compatibility obligations and no released
database has no reason to carry incremental `ALTER TABLE` steps; the correct greenfield shape is one
first-create schema.

The existing scanner does not catch this. `InfrastructureGuards.RelationalResourcePoolOwnership_UsesGreenfieldFirstCreateSchemasOnly`
and `..._ScannerRejectsRenamedFormerPostgreSqlMigration` scan only for *resource-pool ownership*
compatibility ALTERs via `FindOwnershipCompatibilityAlters`. The scanner also reads
`src/OrcaCore.Providers.SqlServer/Migrations`, coupling an active guard to a provider whose project
file has been deleted (see §9).

### Deletions that are correct

The following were verified as genuinely and correctly removed, with metadata negative evidence from
the captured exported-API baselines:

- Saga, child-workflow, and external-job protocol commands/events are absent from
  `OrcaCore.Runtime.Protocol` (86 public types remain; none match saga/child/job/pause/buffer).
- `WorkflowStatistics`, `WorkflowStatisticsGroup`, `WorkflowPressureMetrics`, `SagaAuditSnapshot`,
  `ForEachWorkItemSnapshot`, and the legacy `WaitMode`/`WorkflowStatus` enums are absent from the
  packaged `OrcaCore` public API.
- The four removed internal `StepResult` bridges (`EngineYieldStepResult`,
  `EngineContinueAsNewStepResult<TState>`, `EngineExternalJobStepResult`,
  `EngineAcquireResourcesStepResult`) have neither declarations nor name-based reflection fallbacks —
  task `7.18` is genuinely complete.
- `OrcaCore.Providers.PostgreSql/Migrations/007_resource_ownership.sql` was replaced by
  `007_resource_governance.sql` as a first-create schema, not an upgrade.

---

## 5. Public and package API findings

### Independent capture

The exported public API of all eleven packaged assemblies was captured directly from the current
Release build. All eleven packages were then packed fresh from source at `0.0.0-phase0` into a
scratch feed outside the repository; every packed `lib/net10.0/*.dll` is **byte-identical** to the
corresponding built assembly, so the packed surface equals the current surface.

| Assembly | Baseline lines | Public types |
|---|---:|---:|
| `OrcaCore` | 1385 | 164 |
| `OrcaCore.Runtime.Protocol` | 1205 | 86 |
| `OrcaCore.Provider.Abstractions` | 407 | — |
| `OrcaCore.Dag` | 27 | — |
| `OrcaCore.Durable.Hosting` | 27 | — |
| `OrcaCore.Engine.Ephemeral` | 12 | — |
| `OrcaCore.Providers.PostgreSql` | 8 | — |
| `OrcaCore.Dag.Hosting` | 7 | — |
| `OrcaCore.Providers.InMemory` | 4 | — |
| `OrcaCore.Core` | 2 | 0 (exports nothing — correct) |
| `OrcaCore.Engine.Durable` | 2 | 0 (exports nothing — correct) |

### P1-F — Task 7.16's checked-in baseline does not exist, and the guard that would enforce it fails

`PublicApiBaseline.BaselineDirectory()` resolves to
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1`. **That directory does not exist.**
`EveryTargetAssembly_MatchesTheApprovedExactPublicApiBaseline` fails with
`InvalidOperationException : Approved public API baseline directory is missing`.

The only `.api.txt` files in the tree are under `artifacts/public-api-candidates/candidate/`, which
this change **added to `.gitignore`** (`.gitignore:27`). Candidates are deliberately not checked in,
and candidate capture is explicitly fail-closed ("Candidate capture never approves or updates the
checked-in baseline"). There is therefore **no reviewed baseline for any of the eleven assemblies**.

The baseline machinery itself is well built — the canonical formatter provably covers types,
constructors, methods, properties, fields, events, operators, generic arity, constraints, modifiers,
`in`/`out` parameters, and nested/protected members, and `BaselineInventoryAndCandidateCapture_FailClosed`
proves missing/partial coverage throws. The machinery exists; the approved artifact does not. Task
`7.16` is correctly unchecked.

### P2 — The forbidden-symbol guard is namespace-pinned to namespaces the product no longer uses

`ForbiddenPublicSymbols` names, among ~120 entries,
`OrcaCore::OrcaCore.Abstractions.Instances.ActiveWaitSnapshot` and
`OrcaCore::OrcaCore.Abstractions.Instances.WorkflowInstanceSnapshot`. The live types are
`OrcaCore::OrcaCore.ActiveWaitSnapshot` and `OrcaCore::OrcaCore.WorkflowInstanceSnapshot`. The guard
asserts absence from a namespace nothing currently occupies and cannot fire against the current
surface. `RemovedDeferredAndWrongOwnerPublicSymbols_AreAbsentBeforeBaselineApproval` passes
vacuously for that family. This is a name-only guard that does not verify the exact current
signature, and it is not substitute evidence for the missing baseline.

### P2 — The packed-package comparison lane does not execute

`PublicApiBaseline.CapturePackages` extracts every package into one temporary root and deletes it in
a `finally`. Run against a fresh eleven-package feed it threw
`System.UnauthorizedAccessException : Access to the path 'OrcaCore.Core.dll' is denied.` The
"compare fresh packages" half of task `7.16` is not currently runnable. Packed-versus-built
equivalence was established independently by byte comparison instead.

### P1-G — The repository-local package feed used by every consumer fixture is stale

`artifacts/phase0-packages` holds eleven `0.0.0-phase0` packages. Comparing their assemblies against
a fresh pack of the same projects at the same version:

| Assembly | Repo feed | Fresh pack |
|---|---:|---:|
| `OrcaCore.Runtime.Protocol` | 802 304 B | 500 736 B |
| `OrcaCore.Engine.Durable` | 775 168 B | 541 184 B |
| `OrcaCore.Core` | 417 792 B | 405 504 B |
| `OrcaCore` | 223 232 B | 171 520 B |

All eleven differ. The feed predates the Section 7 deletions entirely. `run-package-fixtures.ps1`
reads only from that feed and never repacks, so the reported "Green package fixtures: 6 built" is
consumer evidence against a **superseded** package set, not the reviewed target. Every
packed-consumer positive and negative claim resting on this feed is unproven for the current target
— which is consistent with task `7.19` (packed-consumer negative evidence) being open.

### Current event surface confirms Section 7B is unimplemented

The packaged `OrcaCore` still exports `OrcaCore.IWorkflowEventClient`. There is no
`IWorkflowEventIngress`, no `IWorkflowEventDispatcher`, no `WorkflowEventContract`, and no
`EventContractVersion` anywhere in the eleven packaged assemblies.

---

## 6. Behavioral and persistence findings

Proven and executing correctly against the current public/host/provider boundaries:

- typed registration, `GetHandleOrThrow()`, `StartOrGetAsync` with `StartIdempotencyKey`, start
  acceptance/conflict unions, and typed reopen;
- durable start idempotency and replacement-host reopen against real PostgreSQL
  (`DurablePostgreSqlApplicationJourneyTests.ReplacementHost_ReopensAndCompletesPersistedWorkflow`,
  Testcontainers `postgres:17-alpine`);
- ephemeral and durable in-memory application journeys; role-specific host composition;
- provider certification, 76/76, including the split workflow-event, inbox, start-idempotency,
  outbox, projection, timer, dispatch, and governance ports;
- PostgreSQL provider certification, 79/79 under Docker (2 m 12 s);
- serialized resource-governance aggregate, lease lifecycle, quarantine, and confirmation
  precedence;
- deadlines, attempt identity, structured joins, path tokens, and fixed-codec closure.

### P1-H — Behavioral scenario evidence is nondeterministic

The guard suite was run twice, unchanged, back to back:

| Run | Total | Passed | Failed |
|---|---:|---:|---:|
| 1 | 187 | 169 | 18 |
| 2 | 187 | 168 | 19 |

Fourteen failures in both runs are `ExecutableBehaviorExpectedRedGuards` (tasks `3.8`, `3.9`, `3.10`,
`3.11b`) and are intentionally red. The remainder are **not**:

| Failure | Run 1 | Run 2 |
|---|---|---|
| `Section7Scenario_...(3.11c, causal-release-gap-recovery)` | FAIL | FAIL |
| `Section6Scenario_...(3.9, maxattempts-one-two-expired-replay)` | pass | FAIL |
| `EveryTargetAssembly_MatchesTheApprovedExactPublicApiBaseline` | FAIL | FAIL |
| `RemovedCapabilitiesAndConstructionHelpers_HaveNoProductMetadataType` | FAIL | FAIL |
| `RemovedCapabilitiesAndConstructionHelpers_HaveNoProductSourceBridge` | FAIL | FAIL |

`causal-release-gap-recovery` fails with
`System.TimeoutException` from `LeaseExitScenarioHost.RunTimedOutRetryAsync`, then **passes** when
the Section 7 scenario family is run in isolation (37/37). The scenario drivers that carry the lease
recovery, reconciliation, and retry evidence are load-sensitive and do not reproduce. Per review rule
7, expected-red failures are not passing tests; per rule 8, a suite that changes verdict between
identical runs is not proof of behavior.

### Section 7B behavior is absent, not merely untested

None of the following exists in the target: descriptor-based waits, contract-checked resumed payload
materialization, pre-wait route-level or per-instance pending inboxes, durable acceptance ordering,
poison/dead-letter state, cold-instance continuation commit with definition/version/fingerprint
rehydration, definition fanout snapshots, exact-definition start-or-deliver, durable `Publish`,
outbound envelopes, or application dispatcher ownership. Publish/outbox atomicity and ambiguous
dispatch retry cannot be assessed because no publish path exists.

---

## 7. Production deletion ledger

Thirty-four production files were deleted between `d76192f` and the current worktree; twenty-seven
additional production source files remain physically present but `<Compile Remove>`d.

### Physical production deletions (34)

| Family | Files | Disposition | Verdict |
|---|---:|---|---|
| `OrcaCore.Hosting` project + catch-all/OTel registration | 4 | REMOVE | **Accepted** — task `7.10` requires deleting catch-all hosting, the OTel SDK facade, binder APIs, and serializer hooks |
| `OrcaCore.Hosting/Telemetry/*` (instruments, gauge collector, observer) | 3 | claimed REMOVE | **Rejected** — internal BCL diagnostics behavior deleted with no relocation (§8) |
| `OrcaCore.Hosting/WorkflowPayloadSerializationOptions.cs` | 1 | REMOVE | Accepted — no serializer hook in v1 |
| Saga builder/definition, `WorkflowDagBuilder`, `WorkflowBuilder` | 4 | REMOVE / RELOCATE | Accepted — Saga deferred and registered; DAG relocated to Section 8 |
| Durable Saga adapter, DAG runner, payload serializers, runtime event names | 4 | REMOVE / DEFER | Accepted for Saga/serializer; `DurableDagRunner` correctly deferred to `8.5` |
| `DurableManagement`, `DurableManagementQuery`, `ProjectionPredicateTranslator`, `WorkflowInstanceQueryModel`, `EphemeralManagement` | 5 | claimed REMOVE | **Rejected** — public facade removal is correct, but the provider/operator projection was removed with it (§8) |
| `IEphemeralStateSnapshotter` | 1 | REMOVE | Accepted |
| `IWorkflowPayloadCodec`, `IResourcePoolStore` (old location) | 2 | RELOCATE | Accepted — relocated to `OrcaCore.Provider.Abstractions` |
| `WorkflowCommand.cs`, `WorkflowEvent.cs` (old location) | 2 | RELOCATE | Accepted — relocated to `OrcaCore.Runtime.Protocol` |
| `OrcaCore.Abstractions.csproj` → `OrcaCore.csproj` | 1 | RELOCATE | Accepted — task `7.1` |
| PostgreSQL `007_resource_ownership.sql` → `007_resource_governance.sql` | 1 | GREENFIELD DDL | Accepted |
| SQL Server `008_resource_ownership.sql` + `.csproj` | 2 | **UNRESOLVED** | **Rejected** — task `10.2` still requires SQL Server (§9) |
| RabbitMq / Redis / Relational `.csproj` | 3 | **UNRESOLVED** | **Rejected** — source files orphaned, test projects dangling (§9) |
| PostgreSQL registration extension (renamed) | 1 | RELOCATE | Accepted |

### Production `<Compile Remove>` entries (27) — no disposition satisfies the burden

| Project | Count | Excluded families |
|---|---:|---|
| `src/OrcaCore.Abstractions/OrcaCore.csproj` | 17 | `RunChildFailurePolicy`, `RunChildrenPolicies`, `ActiveStepSnapshot`, `ActiveWaitSnapshot`, `CompositionBranchOutcomeSnapshot`, `ForEachGroupSnapshot`, `ForEachWorkItemSnapshot`, `ForEachWorkItemStatus`, `LifecycleEventSnapshot`, `SagaAuditSnapshot`, `WaitMode`, `WorkflowInstanceSnapshot`, `WorkflowPressureMetrics`, `WorkflowStatistics`, `WorkflowStatisticsGroup`, `WorkflowStatus`, `Validation` |
| `src/OrcaCore.Engine.Durable/…csproj` | 7 | child-workflow, external-job, and saga command handlers + state; `DurableFiberDriverExecutor.Children.cs` |
| `src/OrcaCore.Provider.Abstractions/…csproj` | 3 | `ArchiveResult`, `PurgeResult`, `RetentionPolicy` |

The review request is explicit: `<Compile Remove>` is **not** deletion proof. These twenty-seven
files remain in `src/` as an uncompiled compatibility reservoir with no normative requirement
citing their absence, no replacement owner recorded for the retention family, and no proof that no
retained behavior depended on their shape. Every one of them still declares `public` types in the
product tree. Some are legitimate recovery sources for deferred capabilities (Saga, children,
external jobs) and would qualify as DEFER if paired with an exact future task and re-entry criteria;
the retention trio and the statistics/projection family do not (§8).

---

## 8. Direct answers on statistics, observability, and retention

### 8.1 Operational statistics — **capability lost, not merely un-faceted**

Removal of the public `Statistics()` facade is correct and authorized. What was removed with it is
not.

`IWorkflowProjectionStore` in `src/OrcaCore.Provider.Abstractions/ProviderPorts.cs` now exposes
exactly four members: `ApplyAsync`, `GetAsync(InstanceId)`, `FindActiveWaitsAsync`, and
`ListLeaseRecoveryCandidatesAsync`. There is **no count, list, filter, group, or statistics
operation on any provider port**. `GetStatisticsAsync` survives only in
`src/OrcaCore.Providers.Redis` and `src/OrcaCore.Providers.SqlServer` — both outside the solution,
both with deleted or provisional project files.

Consequently **none** of the required host/operator capabilities exist anywhere in the active target:

| Required operator answer | Status |
|---|---|
| counts by definition / version / status | **absent** |
| active / waiting / failed / timed-out / terminated counts | **absent** |
| active waits by event | **absent** |
| stuck detection | **absent** |
| stream growth | **absent** |
| checkpoint count and lag | **absent** |
| outbox pending / retryable / claimed | **absent** |
| continuation versus external outbox pressure | **absent** |
| active-instance pressure | **absent** |

`docs/specs/15-requirements-observability-otel.md` §15.0 states the boundary as "Provider/runtime
projections may support operator diagnostics without becoming public application APIs", and OB-080
requires statistics parity. The projection was deleted, not made internal. This is removal of a
required capability behind an obsolete facade.

### 8.2 Observability — **P1: normative catalog is almost entirely unimplemented**

`docs/specs/15-requirements-observability-otel.md` is NORMATIVE (`docs/normative-source-map.md` §3
classifies all twenty `specs/` files NORMATIVE, with only `18` excepted). OB-020 fixes the
instrument prefix at `orca.`.

**Actually emitted by active production source (16 instruments, all `orcacore.*`):**

`orcacore.ephemeral.workflows.started`, `orcacore.ephemeral.events.delivered`,
`orcacore.ephemeral.timers.fired`, `orcacore.ephemeral.terminal_commands`,
`orcacore.governance.host_compatibility.failures`, `orcacore.governance.active_slots`,
`orcacore.governance.configured_limit`, `orcacore.governance.wait_depth`,
`orcacore.governance.cancellations`, `orcacore.resource.quarantined.units`,
`orcacore.resource.quarantined.oldest_age`, `orcacore.execution.fenced_bodies.running`, plus four
throttle gauges.

**Required and missing:**

| Requirement | Required | Present |
|---|---:|---:|
| OB-021 gauges (`orca.instances.active`, `orca.instances.stuck`, `orca.waits.active`, `orca.outbox.pending`, `orca.stream.events`, `orca.checkpoints.count`, `orca.checkpoints.lag`, `orca.resource_pool.*`) | 12 | **0** |
| OB-022 counters (`orca.commands.processed`, `orca.events.applied`, `orca.steps.completed`, `orca.steps.failed`, `orca.outbox.dispatched`, `orca.resource_pool.reconciliations`, `orca.lifecycle.events`, `orca.inbox.duplicates`) | 8 | **0** |
| OB-023 histograms (`orca.commands.duration`, `orca.steps.duration`, `orca.provider.commit.duration`, `orca.outbox.dispatch.duration`, `orca.waits.duration`) | 5 | **0** |
| OB-001/OB-040/OB-050 `ActivitySource` and span catalog | required | **0 `ActivitySource` in all of `src/`** |
| OB-020 prefix | `orca.` | every emitted instrument uses `orcacore.` |

The deleted `src/OrcaCore.Hosting/Telemetry/OrcaCoreTelemetryInstruments.cs` contained exactly the
missing catalog — ten `CreateCounter<long>` (commands processed, events applied, steps completed,
steps failed, outbox dispatched, lifecycle events, inbox duplicates, driver park/poison/version-
binding), seven `CreateHistogram<double>` (commands, steps, waits, driver segment, continuation lag,
and two more), and observable gauges — bound to the `OrcaCoreDiagnostics.*Key` attribute constants.
`OrcaCoreTelemetryGaugeCollector.cs` was the `BackgroundService` that fed the fleet/pressure gauges
from `IWorkflowProjectionStore` and `IResourcePoolStore` on a `PeriodicTimer`.

Both were deleted with **no relocation**. The review request draws the line explicitly: "Removing
the catch-all hosting and OTel SDK-registration facade is allowed. Deleting internal BCL diagnostics
behavior without relocation is not." Worse, relocation is now impossible without restoring provider
ports: the projection store no longer exposes the counts the collector consumed.

Compounding this, the packaged `OrcaCore` still publicly exports
`OrcaCore.Abstractions.Diagnostics.OrcaCoreDiagnostics` with roughly forty `orca.*` attribute-key
constants (`orca.instance.id`, `orca.command.type`, `orca.outbox.kind`, `orca.step.path`, …). It is
a public telemetry vocabulary with no emitters — a public placeholder for a capability that no
longer exists.

`tests/OrcaCore.Integration.Tests/Observability/**` is `<Compile Remove>`d, so no integration
evidence contradicts the above. OB-060's `IOutboxPumpObserver` and OB-AC-006 remain normative while
task `7.17` directs removal of "injectable observer hooks"; no approved amendment reconciles doc 15
with that direction, and per `CLAUDE.md` "If no approved change covers it, **that is the finding**."

The single surviving telemetry guard,
`OperationalTelemetryContractGuards.DurableEngine_PublishesTheThreeExactBclOperationalInstruments`,
asserts only `published.Should().Contain([...three names...])` — presence, not exactness, not values,
not attributes, not transitions, not restart behavior — and reaches the meter through
`Assembly.Load` + `GetType` + `GetProperty` reflection.

### 8.3 Retention — **P1: no provider-owned retention capability exists**

| Required element | Status |
|---|---|
| retention policy | `src/OrcaCore.Provider.Abstractions/RetentionPolicy.cs` is `<Compile Remove>`d |
| archival | `ArchiveResult.cs` `<Compile Remove>`d; no archive path in active source |
| safe physical cleanup | `PurgeForRetentionAsync` exists internally in PostgreSQL and InMemory |
| reference protection | not verifiable — no policy, no caller |
| **production reachability** | **none** |
| certification | `tests/OrcaCore.ProviderCertification/RetentionCertificationTests.cs` exercises it |

Every caller of `PurgeForRetentionAsync` outside the two providers is a test:
`ProviderCertification/RetentionCertificationTests.cs`,
`Providers.PostgreSql.Tests/PostgreSqlProviderCertificationTests.cs`, and
`Providers.PostgreSql.Tests/PostgreSqlRetentionCertificationTests.cs`. No hosted service, sweep,
policy evaluator, or engine path invokes it. `IWorkflowRetentionStore` is on the forbidden-symbol
list and survives only in the out-of-solution SQL Server provider.

The review request is explicit: "A purge helper called only from test fixtures is not sufficient."
Deferring the public Archive/Purge APIs is correct; the provider-owned retention capability behind
them was not preserved, and `tests/OrcaCore.Acceptance.Tests/RetentionAcceptanceTests.cs` was
`<Compile Remove>`d rather than retargeted.

---

## 9. Provider-project dispositions

| Provider | In `OrcaCore.slnx` | Project file | Source | Test project | Disposition |
|---|---|---|---|---|---|
| **PostgreSQL** | yes | present | active | `Providers.PostgreSql.Tests` — 79/79 green under Docker | **Complete and certified**, but ships a provisional-schema upgrade path (§4 P1-E) |
| **InMemory** | yes | present | active | covered by `ProviderCertification` 76/76 | **Complete** — dev/test role |
| **SQL Server** | no | **`.csproj` deleted** | 9 orphaned `.cs` files + `Migrations/` | `Providers.SqlServer.Tests` tracked, **fails to build** | **UNRESOLVED — blocking.** Task `10.2` still requires SQL Server tests. `InfrastructureGuards` still reads `src/OrcaCore.Providers.SqlServer/Migrations`. No normative requirement records its removal |
| **RabbitMQ** | no | **`.csproj` deleted** | 8 orphaned `.cs` files | `Providers.RabbitMq.Tests` tracked, dangling reference | **UNRESOLVED** |
| **Redis** | no | **`.csproj` deleted** | 4 orphaned `.cs` files (incl. surviving `GetStatisticsAsync`) | `Providers.Redis.Tests` tracked, dangling reference | **UNRESOLVED** |
| **Relational** | no | **`.csproj` deleted** | 1 orphaned `.cs` file | none | **UNRESOLVED** |
| **ZeroMQ** | no | present | 7 `.cs` files | `Providers.ZeroMq.Tests` tracked, dangling reference | **UNRESOLVED** — present in tree, absent from manifest and solution |
| `OrcaCore.Hosting` | no | **deleted** | **zero files**; three empty directories remain | `Hosting.Tests` retargeted to Durable.Hosting | Deletion authorized by `7.10`; empty directory husk and two dangling sample references remain |

Four tracked test projects (`SqlServer.Tests`, `RabbitMq.Tests`, `Redis.Tests`, `ZeroMq.Tests`)
reference production `.csproj` files that no longer exist. `dotnet build` on
`tests/OrcaCore.Providers.SqlServer.Tests` fails. These are orphaned tracked artifacts that no
disposition in the ledger covers.

### DAG, Saga, children, external jobs

Correctly separated. `OrcaCore.Dag` and `OrcaCore.Dag.Hosting` exist as packages with the one-way
graph, and their behavioral fixtures are correctly **expected-red** pending Section 8 (task `3.10`
scenarios, `dag-hosting` package fixture at `turnsGreenSection: 8`). No Section 8 behavior is
credited as Section 7 evidence. Saga is future/non-v1 with its registry entry retained; public
children and generic external jobs are deferred with recovery sources preserved. Kubernetes
companion remains outward-only and expected-red at section 8.

---

## 10. Test and retirement accounting

### Counts (independently derived)

| Measure | Value |
|---|---:|
| Recovery-baseline (`d76192f`) tracked test `.cs` files | 239 |
| Current physical test `.cs` files (excl. `bin`/`obj`) | 312 |
| Current physical `[Fact]`/`[Theory]` declarations | 1 260 |
| Test-project `<Compile Remove>` entries | 125 across 8 projects (plus 10 glob patterns in `Integration.Tests`) |
| Executed active test cases (9 assemblies) | **860** |

Per-assembly executed results are in §11.

### Ledger arithmetic

`section-07-r-declaration-crosswalk.json` (schemaVersion 4) declares: `excludedFiles` 102,
`excludedDeclarations` 554 = `rDeclarations` 373 + `section8Declarations` 39 +
`laterOrLegacyDeclarations` 135 + `removedConceptDeclarations` 7 + `unsupportedBlockers` 0. The
arithmetic is internally consistent and `RecoveryCrosswalkInfrastructureGuards` verifies it. Task
`7.20` — "recalculate the current-v1 total … correct declaration arithmetic" — is nevertheless open,
and the ledger's own header states it is not final.

### P1-I — The crosswalk proves existence, not equivalence, and its fan-in is indefensible

`RecoveryCrosswalkGuards` verifies that each retired declaration names an active target that exists,
that the coordinate parses, that the file compiles, and that group unions match member unions. It
compares **nothing** about setup, action, public/provider boundary, concurrency schedule, failure or
crash point, persistence/restart boundary, or assertions. The review request states plainly: "Test
method existence is insufficient."

**373 retired declarations map onto 48 distinct active targets.** The concentration:

| Active target | Retired declarations credited |
|---|---:|
| `scenario:restart-readmits-unfinished-items` | **99** |
| `scenario:four-event-overloads` | 55 |
| `scenario:empty-parallel-diagnostic-parity` | 54 |
| `test:…/ParallelAcceptanceTests.cs#RacingBranchCompletions_SerializeDeterministically` | 44 |
| `scenario:six-hosting-entry-owners` | 44 |
| `scenario:deadline-persistence-inheritance` | 42 |

`restart-readmits-unfinished-items` is a root-`ForEach` restart re-admission scenario. It is credited
as the semantic replacement for, among 99 declarations:

- `ScopeMergeAdapterTests::WhenFirstMerge_ReceivesOnlyTheCommittedWinner` — `WhenFirst` is a
  **deferred** capability; this belongs in the `L`/deferred bucket, not `R`;
- `DurableAggregateTests::DecideYield_WritesCheckpointWithoutCompletingStep` and
  `DurableDriverAcceptanceTests::YieldChunkedStep_CrashMidChunk_ResumesFromLastCommittedChunk` —
  authored `Yield` is a **removed concept**; these belong in `removedConceptDeclarations`;
- `DurableWaitStateTests::PlanBufferedDeliveryReplay_WhenBufferedDeliveryMatchesWait_EmitsMatchAndAppliedInboxWrite`
  and `PlanBufferedReplay_WhenResumeReplaysBufferedTimers_EmitsTimerFiredEvents` — event/timer
  buffering, which Section 7B reinstates and which no current test covers;
- `DurableDriverHostAcceptanceTests::ExternalJobCompletionAfterHostRestart_ResumesFollowingStepExactlyOnce`
  — external jobs are **deferred**;
- `DurableDeadlineExecutionTests::CompleteWithin_ExpiresAfterHostReplacement_AtOriginalAbsoluteDeadline`
  — a deadline/host-replacement behavior with no relationship to item re-admission;
- `DurableDriverHostAcceptanceTests::DuplicateEventAndDuplicateClaim_ExactlyOneStepExecutionCommits`
  — duplicate-claim exactly-once semantics.

A `ForEach` restart re-admission driver cannot be the semantically equivalent executable replacement
for `WhenFirst` winner selection, `Yield` chunk resumption, buffered timer replay, external-job
restart resumption, absolute-deadline survival across host replacement, or duplicate-claim
exactly-once commit. These are **false-equivalent mappings**, and several are additionally
**misclassified** — declarations for deferred and removed concepts carry `R` (retired-with-
replacement) credit that the ledger's own rules reserve for `L`, `D8`, and `removedConcept`. The six
`reclassifiedDeclarations` corrections already applied show the mechanism exists; it was applied to
six rows out of 379.

### P1-J — Observability evidence is a three-name guard, exactly as warned

Only **three** of the 373 retired declarations carry the `E-OBS` evidence key, and all three point at
the same target:

| Retired declaration | Active target |
|---|---|
| `EphemeralDiagnosticsTests::Start_EmitsActivityAndCounter` | `…OperationalTelemetryContractGuards.cs#DurableEngine_PublishesTheThreeExactBclOperationalInstruments` |
| `DurableDriverTelemetryAndOptionsTests::DriverBacklogGauges_SeparateContinuationAndExternalStates` | same (+ two unrelated scenarios) |
| `DurableDriverTelemetryAndOptionsTests::HostedDriverOptions_RejectInvalidValuesAndReachResolvedServices` | same (+ two unrelated scenarios) |

The review request states: "A three-name telemetry guard cannot replace behavioral statistics,
logging, restart, and gauge-value tests." That is literally the mapping in place. Note in particular
that `Start_EmitsActivityAndCounter` asserted `Activity` emission, and the replacement guard asserts
no `Activity` at all — consistent with there being zero `ActivitySource` in production (§8.2).

### Excluded and orphaned test inventory

`tests/OrcaCore.Integration.Tests` excludes `E2E/**`, `Hosting/**`, `MultiNode/**`,
`Observability/**`, `JobScheduler/**`, `Stacks/**`, `Fixtures/**`, `Support/**`, plus the PostgreSQL
and SQL Server engine suites. Five tests remain active, all in `CurrentSurface/`. There is no
multi-node, no stack, no observability, and no hosting integration coverage in the target.
`tests/OrcaCore.Hosting.Tests` compiles 2 of 6 files.

---

## 11. Validation commands and exact results

Docker independently verified: server **29.6.1**, daemon responsive.

| Command | Result |
|---|---|
| `dotnet build OrcaCore.slnx -c Release -warnaserror` | **Build succeeded — 0 Warning(s), 0 Error(s)**, 5.94 s |
| `dotnet test tests/OrcaCore.Core.Tests` | **342 passed**, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Ephemeral.Tests` | **69 passed**, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Durable.Tests` | **61 passed**, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Hosting.Tests` | **4 passed**, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Acceptance.Tests` | **37 passed**, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.ProviderCertification` | **76 passed**, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Integration.Tests` | **5 passed**, 0 failed, 0 skipped (only `CurrentSurface/` compiles) |
| `dotnet test tests/OrcaCore.Providers.PostgreSql.Tests` | **79 passed**, 0 failed, 0 skipped — Docker, 2 m 12 s |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards` (run 1) | **169 passed / 18 failed / 187** |
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards` (run 2, identical invocation) | **168 passed / 19 failed / 187** |
| — of which intentional `ExpectedRed` | 14 (`3.8`, `3.9`, `3.10`, `3.11b` DAG/companion scenarios) |
| — of which **infrastructure failures** | 4 (run 1) / 5 (run 2) — see §6 |
| `dotnet test … --filter Section7Scenario_…` (isolated, ×2) | 37 passed / 0 failed, both runs — `causal-release-gap-recovery` does not reproduce in isolation |
| `run-compile-fixtures.ps1 -Disposition Green` | pass — 26 source-fixture and 26 product-package CS1061 diagnostics verified; incomplete package rejected |
| `run-compile-fixtures.ps1 -Disposition ExpectedRed` | **0 expected-red compile fixtures** — no 7B route-union guards exist |
| `run-package-fixtures.ps1 -Disposition Green` | 6 built — **from a stale feed** (§5 P1-G) |
| `run-package-fixtures.ps1 -Disposition ExpectedRed` | 2 (`dag-hosting`, `kubernetes-companion`), exit 1 by design |
| Fresh pack of all 11 packages at `0.0.0-phase0` to scratch feed | 11 packed; every `lib/net10.0/*.dll` byte-identical to the Release build |
| Public API capture — current assemblies | 11 candidate files, 3 086 lines total |
| Public API capture — fresh packages | **failed**: `UnauthorizedAccessException: Access to the path 'OrcaCore.Core.dll' is denied` |
| `openspec validate reshape-developer-facing-interfaces --strict` | valid |
| `openspec validate harmonize-downstream-capability-specs --strict` | valid |
| `openspec validate add-runtime-concurrency-limits --strict` | valid |
| `openspec validate --all --strict` | **18 passed, 0 failed** — structure only; see §3 P1-C |
| `openspec list` | reshape `108/155`; harmonize `4/31`; add-runtime-concurrency-limits complete |
| Task accounting (`ChangeTaskLedger_CountsLetterSuffixedIdsAndHasNoDuplicates`) | pass — 4 letter-suffixed IDs, no duplicates |
| `dotnet list OrcaCore.slnx package --vulnerable --include-transitive` | **no vulnerable packages** in any active project |
| `git diff --check` | **clean**, exit 0 |
| `dotnet build samples/OrcaCore.Examples` | **8 errors** — `OrcaCore.Abstractions.{Events,Instances,Steps}`, `OrcaCore.Engine.Durable.Management`, `WorkflowStatisticsGroup` missing; `ReadOnlyParentSnapshot<TState>`, `ForEachItemOutcome<TResult>` inaccessible |
| `dotnet build samples/OrcaCore.Dashboard` | **22 errors** — `DurableManagement`, `InMemoryResourcePoolStore`, `WorkflowStatus` missing; `WorkflowDefinition<TState>` inaccessible |
| `dotnet build tests/OrcaCore.Providers.SqlServer.Tests` | **fails** — orphaned by the deleted production project |
| Coverage gate (CI 80% on `OrcaCore.Engine.*`) | **not run** — not reproducible outside CI in this session |

### Nondeterminism, retries, and environmental limitations

- The guard suite was run twice; the verdict differed between runs (18 vs 19 failures). Two Section
  6/7 lease scenarios (`3.11c/causal-release-gap-recovery`, `3.9/maxattempts-one-two-expired-replay`)
  fail with `TimeoutException` under full-suite load and pass in isolation. Recorded as first-run
  nondeterminism, not resolved.
- The packed-package API capture lane could not execute (file-lock during shared-root extraction).
  Packed-versus-built equivalence was established by independent byte comparison instead.
- `samples/OrcaCore.Examples` and `samples/OrcaCore.Dashboard` are tracked but excluded from
  `OrcaCore.slnx`, so the green solution build does not cover them. `CLAUDE.md` documents
  `dotnet run --project samples/OrcaCore.Examples/OrcaCore.Examples.csproj` as a supported command;
  it is broken. Both still `ProjectReference` the deleted `src/OrcaCore.Hosting/OrcaCore.Hosting.csproj`.

---

## 12. Documentation and samples

- Active-path documentation restructure is coherent; `docs/normative-source-map.md` classifies every
  tree, and the crosswalk resolves both normative trees. `docs/review/` is unmodified.
- One archive move (`developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md`) was performed
  without `git mv`, breaking `git log --follow` for that file.
- `docs/production-readiness.md` (GUIDE) still presents `IWorkflowEventClient` routing as the current
  contract. That matches today's code but contradicts the rewritten proposal; task `9.9` owns the
  reconciliation and is open.
- `docs/specs/17-selected-mode-capability-matrix.md:1953` names `OrcaCore.Hosting.Tests` as an owning
  white-box assembly for an assembly that no longer exists.
- The dashboard/Grafana guidance and `docs/specs/15` metric catalog do not match emitted instruments
  (§8.2). `DashboardGuidance_MapsEveryInstrumentAndKeepsAdapterSecurityHostOwned` passes because it
  maps only the three instruments that exist.
- Samples do not compile (§11). Task `9.1`–`9.5` are open, so this is expected, but it means no
  sample journey validates the reviewed surface.

---

## 13. Before/after target drift

| Checkpoint | Entries | Raw ordered SHA-256 | LF-normalized sorted SHA-256 |
|---|---:|---|---|
| Before validation | 419 | `e029d51d…666a671f` | `52fae12b…bbc8028b` |
| After all validation | 419 | `e029d51d…666a671f` | `52fae12b…bbc8028b` |

`diff` of the pre- and post-validation ordered manifests: **identical**. HEAD and HEAD tree
unchanged. **No drift.** No concurrent review artifact appeared during this review.

All scratch scripts, probes, feeds, extracted packages, captured API baselines, and derived manifests
were written outside the repository. Validation lanes that write inside the tree
(`CompileFixtures/*/obj`, `PackageFixtures/obj`, `artifacts/`) touch only `.gitignore`d build output
and produced no tracked-path change, as the reproduced manifest hashes confirm.

---

## 14. Summary of release-blocking findings

| ID | Finding | Section |
|---|---|---|
| **P1-A** | 17 Section 7 tasks open, including all 12 of Section 7B; task `7.22`'s precondition unmet | §3 |
| **P1-B** | Task `7.23` amendment unapproved; proposal declares two capabilities with no deltas; buffering protocol already deleted ahead of the gate | §3 |
| **P1-C** | `harmonize` contradicts `reshape` on buffering, fanout, routes, and dispatch owner; its approval task `1.4` is open; strict validation cannot detect it | §3 |
| **P1-D** | `AuthoringKernelProxy` — the whole public authoring facade crosses the assembly boundary by name-based reflection, bypassing the approved friend graph; two product guards fail on it | §4 |
| **P1-E** | PostgreSQL ships incremental `ALTER TABLE` migrations — a provisional-schema upgrade path in a greenfield repository; the scanner covers only pool-ownership ALTERs | §4 |
| **P1-F** | No checked-in exported API baseline exists for any of the 11 assemblies; the enforcing guard fails; a forbidden-symbol guard is pinned to dead namespaces | §5 |
| **P1-G** | `artifacts/phase0-packages` is stale — all 11 assemblies differ from current; every packed-consumer claim is unproven | §5 |
| **P1-H** | Lease/retry scenario evidence is nondeterministic; the guard suite returns different verdicts on identical runs | §6 |
| **P1-I** | 373 retired declarations map to 48 targets; one scenario absorbs 99 unrelated declarations; deferred and removed concepts carry `R` replacement credit | §10 |
| **P1-J** | Observability, statistics, and retention capabilities were deleted without relocation; three declarations carry all `E-OBS` credit against a three-name presence guard | §8, §10 |
| **P1-K** | Four provider projects deleted with orphaned source and dangling tracked test projects; SQL Server unresolved against task `10.2` | §9 |

---

## 15. Checkpoint and task-8.0 authorization

**Checkpoint commit: PROHIBITED.** The exact frozen target
(`ac46d99` + 419-entry worktree, manifest `e029d51d…`) SHALL NOT be committed as the Section 7
checkpoint.

**Task 8.0: REMAINS BLOCKED.** No Section 8 work is authorized. The gating chain is unchanged and
unsatisfied:

1. every Section 7A and 7B task complete (17 open);
2. `harmonize-downstream-capability-specs` revised to remove the contradictory ownership, then
   approved under its task `1.4`, synchronized under `2.1`–`2.3`, approved as a final target, and
   checkpointed (27 open);
3. the combined target refrozen under task `7.22`;
4. focused independent approval of that exact refrozen target;
5. the checkpoint commit, with the committed tree matching the approved target.

The Section 7 checkpoint `50254d08175431896d580ecfcc93d8e49e1c2ec7` remains the valid recovery
anchor. Nothing in this verdict disturbs it.

---

## 16. Reviewer attestation

- No existing file was edited, staged, restored, deleted, renamed, formatted, or committed.
- No commit, tag, branch, or stash was created.
- The recovery worktree `X:\Projects\GitHub\Workflow-orca-recovery` was not accessed.
- All scratch artifacts were kept outside the repository; no derived manifest was written inside it.
- This verdict file is the sole new repository path authored by this review, and it is immutable.
- The pre- and post-validation manifests are byte-identical, confirming the reviewed target is
  exactly the frozen target.
