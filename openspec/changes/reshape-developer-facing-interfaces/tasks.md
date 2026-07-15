## 1. Reconcile the Cross-Change Contract

- [x] 1.1 Publish one selected-mode capability matrix and compiler/diagnostic contract shared by this change, `adopt-structured-fiber-execution`, and `add-runtime-concurrency-limits`.
- [x] 1.2 Reconcile typed workflow, saga, DAG, `Parallel`, `WhenFirst`, result, return, and merge signatures before source implementation.
- [x] 1.3 Specify durable `ForEach` surface absence plus compiler defense and structural root-only quiescent continue-as-new.
- [x] 1.4 Record the three-way per-step throttle / named transient pool / durable lease taxonomy.
- [x] 1.5 Record separate tier packages plus the small `OrcaCore` meta-package and the full permitted dependency graph, including `Provider.Abstractions -> Runtime.Protocol` and the forbidden reverse edge.
- [x] 1.6 Record split-host continuation: commit plus at-least-once handoff, inline drive only with a local definition, and `AppliedAndProgressed` versus `AppliedPendingContinuation`.
- [x] 1.7 Record explicit host-scoped definition registration, typed handles, typed routing, worker-reported failure, application-safe remediation, and scope-owned durable leases.
- [x] 1.8 Update the affected canonical requirements for the initial reconciliation decisions.
- [x] 1.9 Strict-validate the three coordinated changes and record the matching signatures/taxonomy before structured-fiber implementation.

## 2. Accept the Archived Structured-Fiber Baseline

- [x] 2.1 Confirm `adopt-structured-fiber-execution` completed all tasks, passed its full verification matrix, promoted its deltas, and was archived as `2026-07-15-adopt-structured-fiber-execution`.
- [x] 2.2 Accept the delivered shared compiler, immutable fingerprinted plans, typed structured scopes/results/merges, scheduler, scope-owned obligations, and durable format-2 driver as fixed substrate.
- [x] 2.3 Accept the delivered `Workflow.Ephemeral<TState>` / `Workflow.Durable<TState>` root factories and workflow `Build()` / `TryBuild()` completion vocabulary without creating a second builder or compiler.
- [x] 2.4 Accept delivered root capability separation for durable `ForEach`, public `WaitLong`, portable dynamic wait, child workflows, and structural root-only continue-as-new.
- [x] 2.5 Rebase this proposal, design, delta specs, matrix, and task graph from prerequisite implementation to post-fiber consolidation and strict-validate the result.
- [x] 2.6 Record the section 4-8 canonical-requirement amendment map in the design; each section applies its listed canonical amendments before its first source task rather than deferring them to documentation cleanup.

## 3. Capture Failing Public and Consumer Guards

- [ ] 3.1 Add a public-signature inspection harness that classifies types as application, provider-authoring, runtime-protocol, or internal and fails on current cross-tier leaks.
- [ ] 3.2 Define the clean package-consumer fixture harness and assertions for minimal ephemeral hosting, in-memory durable hosting, provider-backed durable hosting, and the small `OrcaCore` meta-package without referencing packages that do not yet exist.
- [ ] 3.3 Define the provider-author fixture harness and assertions for the declared `Provider.Abstractions -> Runtime.Protocol` edge without adding an unrestorable project before those packages exist.
- [ ] 3.4 Add positive and negative compile fixtures for root and nested ephemeral/durable authoring, including mode-specific definition registration, durable `ForEach` absence, nested transient-pool absence, and compiler defense in depth.
- [ ] 3.5 Add public-baseline guards proving application definitions expose no compiled IR and application wait snapshots expose opaque `WaitId` plus `AuthoredLocation` but no fiber/scope/wait-sequence routing identities.
- [ ] 3.6 Add behavior guards proving `GetStateAsync<TState>` returns only detached committed root state through the configured serializer/copier and active-wait queries project stable authored matching metadata.
- [ ] 3.7 Add declaration guards that fail until `WorkflowInstanceQueryModel`, `WorkflowStatistics`, `WorkflowStatisticsGroup`, and `DestructiveOperationConfirmation` each have one canonical declaration.
- [ ] 3.8 Add a durable-example regression that fails on `Poisoned`, direct `DurableCommandProcessor` use, terminal-then-job ordering, or raw protocol types in the application journey.
- [ ] 3.9 Add a two-host failing acceptance fixture where a definition-less callback host reports an external-job outcome and a definition-owning continuation pump progresses it exactly once.
- [ ] 3.10 Add failing facade tests for no-match, ambiguous-match, live-unmatched, paused-target, definition-not-registered, stale-remediation, completion, timeout, and worker-failure outcomes.
- [ ] 3.11 Add failing durable-lease lifecycle tests for normal scope exit, canceled branch, failed scope, workflow terminal transition, and crash/restart expiry recovery.

## 4. Consolidate Mode-First Authoring and Hide Execution IR

- [ ] 4.0 Apply the mapped canonical `workflow-authoring`, `workflow-contracts`, and `quality-and-verification` amendments before task 4.1 source work.
- [ ] 4.1 Introduce immutable `EphemeralWorkflowDefinition<TState>` and `DurableWorkflowDefinition<TState>` application types and make normal engine registration accept only the matching family.
- [ ] 4.2 Remove public `WorkflowDefinition<TState>.CompiledPlan`; retain the executable plan through an implementation-only accessor and keep immutable authored identity/version/mode/fingerprint metadata public.
- [ ] 4.3 Internalize compiled plan/instruction/scope/branch/policy and compiler identity-index types so no application package signature contains executable IR.
- [ ] 4.4 Replace public compiler-shaped options and fluent methods with application-tier `WorkflowAuthoringOptions`: `MaxStructuredDepth`, `MaxActiveExecutionPaths`, `MaxBranchResultPayloadBytes`, payload serializers, state copiers, and fingerprint contributors; move turn/checkpoint limits to engine/hosting options.
- [ ] 4.5 Split public nested scope/branch builders by selected mode while sharing implementation internally; make root and nested capability absence identical.
- [ ] 4.6 Remove the superseded internal durable-wait helper and rewrite its remaining tests through public durable `WaitLong` authoring.
- [ ] 4.7 Delete the superseded mixed-mode `WorkflowBuilder<TState>` and rewrite repository tests, samples, benchmarks, saga helpers, and fixtures directly against the final mode-first API.
- [ ] 4.8 Delete `RequiresDurableEngine`, `CompiledWorkflowPlan.FromLegacy`, registration-time mode inference, and the dual fallback/compiled execution path.
- [ ] 4.9 Remove `WithDefinitionRetry`, its unused definition policy state, and tests until definition-wide retry is separately specified and implemented.
- [ ] 4.10 Standardize saga and DAG completion on `Build()` plus `TryBuild()` returning `Validation<TDefinition>`; remove `BuildValidated()`.
- [ ] 4.11 Make required fluent arguments non-nullable, validate local arguments at call time, aggregate graph diagnostics at build time, and deeply freeze workflow, saga, and DAG metadata.
- [ ] 4.12 Reject empty/default strong IDs and non-positive versions in factories, builders, registration, runtime operations, management, and advanced protocol entry points.
- [ ] 4.13 Introduce explicit ephemeral saga authoring returning `EphemeralSagaDefinition<TState>` and keep `DurableSagaDefinition<TState>` authoring absent until runtime-owned durable progression passes section 8.
- [ ] 4.14 Turn the section-3 authoring and definition fixtures green and approve the resulting application signatures before package moves.

## 5. Complete Structural Durable Effects and Concurrency Vocabulary

- [ ] 5.0 Apply the mapped canonical workflow-authoring, workflow-contracts, durable-runtime, and runtime-resource-governance amendments before task 5.1 source work.
- [ ] 5.1 Implement typed structural durable external-job nodes with serializer-aware identity/payload/result selectors and authored completion/failure/timeout policy.
- [ ] 5.2 Implement structural durable resource-lease nodes owned by the requesting fiber/scope with deterministic release and crash-recovery expiry.
- [ ] 5.3 Lower external-job and lease nodes directly into the archived compiled fiber plan and resume their exact owner without translating through portable `StepResult`.
- [ ] 5.4 Remove `ContinueAsNew`, `RunExternalJob`, and `AcquireResources` from portable `StepResult` only after structural paths and rewritten repository tests pass.
- [ ] 5.5 Rename ephemeral `WithPoolKey` to explicit transient-pool vocabulary, keep it absent from durable root and nested builders, and retain durable compiler rejection.
- [ ] 5.6 Keep per-step execution throttles in host policy for the initial baseline; do not add a host-dependent method to static workflow builders.
- [ ] 5.7 Reconcile and strict-validate `add-runtime-concurrency-limits` before durable transient-pool source work; add durable authoring only after every supported durable host enforces reset/re-admission semantics.
- [ ] 5.8 Turn structural-effect, nested-capability, portable-result, transient-pool, and lease-lifecycle fixtures green.

## 6. Establish Interface Tiers and Package Direction

- [ ] 6.0 Apply the mapped canonical repository-foundation, developer-facing-surface, and quality-and-verification amendments before task 6.1 source work.
- [ ] 6.1 Create or rename the separate contracts, authoring/core, ephemeral engine, durable engine, hosting, provider-authoring, and runtime-protocol projects/packages plus the small `OrcaCore` meta-package.
- [ ] 6.2 Instantiate the section-3 package-consumer harness against the created packages and verify minimal ephemeral, in-memory durable, provider-backed durable, and `OrcaCore` meta-package journeys.
- [ ] 6.3 Instantiate the provider-author harness against `OrcaCore.Provider.Abstractions` plus its declared `OrcaCore.Runtime.Protocol` dependency and prove no engine implementation reference is required.
- [ ] 6.4 Move strong IDs, application events, mode-specific definitions, snapshots, errors, validation primitives, and portable step contracts into the application tier without advanced references; consolidate the existing duplicate query/statistics/destructive-confirmation declarations into one canonical application-tier declaration each before approving package signatures.
- [ ] 6.5 Move durable commands/facts, host dispatch protocol, checkpoints/envelopes, raw stream-version remediation, fiber/scope ownership, and supported driver observations into `OrcaCore.Runtime.Protocol`.
- [ ] 6.6 Move provider ports, provider commit DTOs, retention/resource persistence DTOs, and certification contracts into `OrcaCore.Provider.Abstractions` with only the declared runtime-protocol dependency and no engine reference.
- [ ] 6.7 Update engine, hosting, provider, test, sample, solution, and package references so the graph exactly matches the permitted-edge list.
- [ ] 6.8 Add architecture tests for every allowed edge and reject every unlisted reference, the protocol-to-provider reverse edge, and application public signatures containing advanced or implementation types.
- [ ] 6.9 Add approved public type/signature baselines for every application and advanced assembly and review each approved type against a supported external scenario.
- [ ] 6.10 Split optional console, OTLP, and Prometheus exporter dependencies from base hosting into focused observability packages and verify minimal consumer dependency closure.

## 7. Deepen the Durable Facade and Align Management

- [ ] 7.0 Apply the mapped canonical durable-runtime, management-and-querying, and developer-facing-surface amendments before task 7.1 source work.
- [ ] 7.1 Add a hosting-owned durable composition root and remove public construction requiring command processors, registries, provider serializers, driver budgets, or observers.
- [ ] 7.2 Implement `runtime.Definitions.Register(DurableWorkflowDefinition<TState>)` returning a typed handle; remove implicit-registration start overloads and return stable `DefinitionNotRegistered` diagnostics.
- [ ] 7.3 Implement typed start and event-delivery contracts without phantom state generics, including payloadless events and the complete instance/definition/correlation routing result matrix.
- [ ] 7.4 Implement typed external-job completion with application-owned time, serialization, report identity, commit translation, deduplication, and continuation status.
- [ ] 7.5 Implement typed external-job timeout reporting with the same application/protocol separation and race semantics.
- [ ] 7.6 Add the worker-reported failure command/fact and typed `FailAsync` facade operation with reason, optional payload, shared report identity, authored failure-policy progression, and stable duplicate behavior.
- [ ] 7.7 Atomically pair every accepted facade outcome with a continuation-outbox handoff, drive inline only for a locally registered definition, and return `AppliedAndProgressed` or `AppliedPendingContinuation` accurately.
- [ ] 7.8 Move supported raw command access and raw stream-version remediation behind the runtime-protocol package and certify a custom host without engine internals.
- [ ] 7.9 Create the canonical asynchronous management root, selection, and instance-handle contracts over the application-tier query/statistics/confirmation models established by task 6.4, and delete any remaining duplicate model/safety declarations.
- [ ] 7.10 Adapt ephemeral management to the shared vocabulary while preserving `EvictAsync`, `EvictTerminalAsync`, `DetectStuckAsync`, and `GetLifecycleEventsAsync` as in-memory capabilities.
- [ ] 7.11 Implement durable handles with root-only typed state, authored active-wait projections, pause/resume/history, and stable not-found, wrong-state, archived, purged, and incompatible-state diagnostics.
- [ ] 7.12 Route all durable management mutations through the runtime-owned lane and remove fallback processor creation, caller timestamps, and caller protocol identifiers.
- [ ] 7.13 Implement poison remediation with an opaque ticket or stable compare-and-act token; return stable stale-ticket conflict and guarantee continuation after accepted rearm.
- [ ] 7.14 Delete always-throwing overloads, make single-instance termination confirmation-free, and require `None = 0, Confirmed = 1` only for broad termination and purge.
- [ ] 7.15 Turn routing, split-host, job-outcome, remediation, management-lane, projection, confirmation, and cross-mode query fixtures green.

## 8. Close DAG/Saga Loops and Make Hosting/Providers Explicit

- [ ] 8.0 Apply the mapped canonical saga-orchestration, durable-runtime, developer-facing-surface, and provider-role amendments before task 8.1 source work.
- [ ] 8.1 Fold `WorkflowDagRunner` into pure `WorkflowDagPlan` behavior or rename it as an unambiguous planner.
- [ ] 8.2 Add a durable DAG facade accepting a validated plan and root identity without caller progress sets, command IDs, or timestamps.
- [ ] 8.3 Reconstruct DAG scheduled/completed/failed/blocked state after start and restart, schedule ready children idempotently, and resume to the next stable point.
- [ ] 8.4 Add DAG acceptance tests for restart, duplicate drive, in-flight preservation, blocked reporting, and terminal progression.
- [ ] 8.5 Register durable saga definitions explicitly and implement runtime-owned forward execution and committed audit through the structured driver.
- [ ] 8.6 Implement automatic reverse compensation, compensation failure, restart recovery, and manual-remediation transitions.
- [ ] 8.7 Add durable saga authoring returning `DurableSagaDefinition<TState>` only after tasks 8.5 and 8.6 pass; expose audit/remediation through management without raw action, scope, command, or timestamp identifiers.
- [ ] 8.8 Delete `DurableSagaCommandAdapter` and demote/delete caller-driven durable DAG scheduling types after runtime-owned paths pass acceptance.
- [ ] 8.9 Add end-to-end durable saga tests for success, timeout, compensation, compensation failure, restart, and remediation.
- [ ] 8.10 Implement explicit ephemeral/durable hosting registration, make in-memory durable development/test-only with a no-restart-durability diagnostic, and remove or repurpose ambiguous `AddOrcaCore()`.
- [ ] 8.11 Add startup validation for required durable store, projection, outbox, continuation, timer, serializer, and worker capabilities.
- [ ] 8.12 Rename PostgreSQL/SQL Server registration to durable-store conventions and standardize validation, ownership, diagnostics, and replacement behavior.
- [ ] 8.13 Keep Redis projection-cache scoped; rename RabbitMQ to dispatcher vocabulary and add equivalent ZeroMQ dispatcher registration.
- [ ] 8.14 Extend provider certification for role declarations, registration-order independence, replacement semantics, native resource ownership, and unsupported-capability diagnostics.
- [ ] 8.15 Internalize concrete timer/outbox/continuation/sweep implementations, delete `RedisProviderProfile`, and internalize converters and other types without supported external scenarios.

## 9. Rewrite Developer Journeys and Documentation

- [ ] 9.1 Rewrite the durable example around explicit registration and a live external job; report completion through the typed facade, assert duplicate behavior/final state, and fail on `Poisoned` or raw protocol use.
- [ ] 9.2 Add a split-host sample/test where a definition-less callback host reports completion/failure and a definition-owning continuation pump progresses the workflow.
- [ ] 9.3 Update every sample to reference only documented application, hosting, and role-specific provider packages for its audience.
- [ ] 9.4 Publish the final ephemeral/durable matrix covering portable dynamic waits, mode-specific definitions and nested builders, all three concurrency categories, sagas, DAGs, management, persistence, and hosting.
- [ ] 9.5 Update README, developer guides, production-readiness notes, durable-driver requirements, and sample READMEs for the final application surface; canonical requirement amendments have already occurred at each section gate.
- [ ] 9.6 Correct stale claims about registration/driving, public `WaitLong`, ephemeral saga support, provider-role interchangeability, in-memory restart guarantees, and durable `ForEach`.
- [ ] 9.7 Document typed root-state availability/cost, authored wait projections versus advanced routing identities, stable error modes, confirmation versus authorization, provider ownership, and concurrency lifetimes.
- [ ] 9.8 Add one provider-authoring guide and one custom-host/runtime-protocol guide, each backed by its clean certification fixture and clearly separated from the application quick start.

## 10. Verify, Trace, and Prove Superseded Surface Is Gone

- [ ] 10.1 Run Core, ephemeral, durable, hosting, acceptance, and provider unit suites after each affected slice and fix every public-interface regression.
- [ ] 10.2 Run PostgreSQL and SQL Server restart suites plus dispatcher/provider certification sequentially where shared infrastructure requires it.
- [ ] 10.3 Pack every documented application, hosting, provider, meta, and advanced package and build/run all clean consumer and provider-author fixtures.
- [ ] 10.4 Run public-signature, duplicate-declaration, exact project-graph, compiled-IR leak, and fiber-routing leak guards and review every baseline change.
- [ ] 10.5 Run every sample under automated assertions and verify no durable journey contains poisoned, unsupported, implicit-registration, or raw-protocol behavior.
- [ ] 10.6 Run split-host continuation, routing matrix, external-job outcomes, durable-lease lifecycle, DAG, saga, management-concurrency, state/wait projection, and destructive-confirmation suites.
- [ ] 10.7 Run `dotnet build OrcaCore.slnx`, the full non-container solution test suite, and the CI-equivalent `OrcaCore.Engine.*` coverage report enforcing the 0.80 minimum line rate, then record environment-gated container commands and results.
- [ ] 10.8 Run repository-wide scans proving the superseded mixed-mode builder, dual execution fallback, durable `StepResult` variants, implicit registration, raw application remediation, duplicate models, public hosted implementations, aliases, and parallel provisional paths are absent.
- [ ] 10.9 Strict-validate `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits`, compare every MODIFIED requirement heading verbatim with its canonical baseline (or dry-run archive), verify the archived fiber baseline remains unchanged, and link every changed canonical requirement to acceptance, compile, consumer, architecture, or provider-certification evidence.
- [ ] 10.10 Refresh the implementation-status and review-disposition documents with exact suite counts, skipped-test reasons, packed-consumer results, measured engine coverage, the final public-surface baseline, and a reconciliation against the live baseline captured in Phase 0.
