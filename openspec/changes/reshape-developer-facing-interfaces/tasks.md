## 1. Reconcile Overlapping Designs and Canonical Requirements

- [x] 1.1 Update this change and `adopt-structured-fiber-execution` to reference one joint selected-mode capability matrix and one shared compiler/diagnostic contract; do not begin source edits before section 1 is complete.
- [x] 1.2 Reconcile the post-fiber workflow, saga, DAG, `Parallel`, `WhenFirst`, typed-result, and merge signatures in both changes and record one approval baseline for all subsequent compile fixtures.
- [x] 1.3 Specify in both changes that durable `ForEach` is absent from public authoring and rejected by the shared compiler when manually constructed, and that continue-as-new is a structural root-only transition requiring quiescent fibers and scope-owned obligations.
- [x] 1.4 Reconcile with `add-runtime-concurrency-limits` on the three-way taxonomy: per-step execution throttle, named cross-instance transient pool, and persisted durable resource lease; record mode availability and distinct names.
- [x] 1.5 Record the separate-package-plus-`OrcaCore`-meta-package decision and the complete permitted dependency graph, including `Provider.Abstractions -> Runtime.Protocol` and the forbidden reverse and application-to-advanced edges.
- [x] 1.6 Record the split-host facade contract: accepted outcomes commit with an at-least-once continuation handoff, inline drive occurs only with a locally registered definition, and results distinguish `AppliedAndProgressed` from `AppliedPendingContinuation`.
- [x] 1.7 Record explicit host-scoped definition registration, typed definition handles without phantom state generics, typed event-routing outcomes, worker-reported failure, application-safe remediation, and scope-owned durable-lease lifecycle in the affected change artifacts.
- [x] 1.8 Update canonical `docs/specs/` requirements and acceptance criteria before source work for definition retry removal, pool taxonomy, portable dynamic waits, structural durable effects, explicit registration, split-host continuation, external-job failure, routing outcomes, management time ownership, remediation, package tiers, and in-memory durable honesty.
- [x] 1.9 Run strict validation for this change, `adopt-structured-fiber-execution`, and `add-runtime-concurrency-limits`; verify their joint references, signatures, and taxonomy match before checking any later task.

## 2. Capture Failing Public and Consumer Guards

- [ ] 2.1 Add a public-signature inspection harness that classifies types as application, provider-authoring, runtime-protocol, or internal and fails on the current cross-tier leaks.
- [ ] 2.2 Add clean package-consumer fixtures for minimal ephemeral hosting, in-memory durable hosting, provider-backed durable hosting, and the small `OrcaCore` meta-package using only intended references.
- [ ] 2.3 Add a provider-author fixture that references `OrcaCore.Provider.Abstractions` plus its declared `OrcaCore.Runtime.Protocol` dependency, implements persistence ports, and has no engine implementation reference.
- [ ] 2.4 Add positive and negative compile fixtures from the reconciled post-fiber signatures for portable, ephemeral-only, durable-only, and absent capabilities, including durable `ForEach` absence and compiler defense in depth.
- [ ] 2.5 Add repository declaration and public-baseline guards that fail until `WorkflowInstanceQueryModel`, `WorkflowStatistics`, `WorkflowStatisticsGroup`, and the selected destructive confirmation contract each have one canonical declaration and the duplicate `DestructiveCommandSafety` types are gone.
- [ ] 2.6 Add a durable-example regression that fails on `Poisoned`, direct `DurableCommandProcessor` use, terminal-then-job command ordering, or raw protocol types in the application journey.
- [ ] 2.7 Add a two-host failing acceptance fixture where host A owns the definition, host B registers no definitions and reports an external-job outcome, and host A's continuation pump must progress the instance exactly once.
- [ ] 2.8 Add failing facade contract tests for typed no-match, ambiguous-match, live-unmatched, paused-target, definition-not-registered, stale-remediation, completion, timeout, and worker-failure outcomes.
- [ ] 2.9 Add failing durable-lease lifecycle tests for normal scope exit, canceled branch, failed scope, workflow terminal transition, and crash/restart expiry recovery.

## 3. Establish Interface Tiers and Dependency Direction

- [ ] 3.1 Create or rename the separate contracts, authoring/core, ephemeral engine, durable engine, hosting, provider-authoring, and runtime-protocol projects/packages plus the small `OrcaCore` meta-package.
- [ ] 3.2 Move strong IDs, application events, snapshots, errors, validation primitives, portable step contracts, and canonical management models into the application contract project without advanced references.
- [ ] 3.3 Move durable commands, committed facts, host dispatch protocol, checkpoint/envelope records, raw stream-version remediation, and supported driver observations into `OrcaCore.Runtime.Protocol`.
- [ ] 3.4 Move provider ports, provider commit DTOs, retention/resource persistence DTOs, and certification contracts into `OrcaCore.Provider.Abstractions` with only the declared runtime-protocol dependency and no engine reference.
- [ ] 3.5 Update engine, hosting, provider, test, sample, solution, and package references so the graph exactly matches the permitted-edge list.
- [ ] 3.6 Add architecture tests for every allowed edge and reject every unlisted project reference, the protocol-to-provider reverse edge, and application public signatures containing advanced types.
- [ ] 3.7 Add approved public type/signature baselines for every application and advanced assembly and review each approved type against a supported external scenario.
- [ ] 3.8 Split optional console, OTLP, and Prometheus exporter dependencies from base hosting into focused observability packages and verify minimal consumer dependency closure.

## 4. Satisfy the Structured-Fiber Execution Prerequisite

- [ ] 4.1 Complete the shared compiler and typed structured-node/result/merge work owned by `adopt-structured-fiber-execution` before implementing mode-first builders in this change.
- [ ] 4.2 Make `Build()` consume the shared compiler and return a compiled-plan-backed definition with the jointly approved diagnostic contract.
- [ ] 4.3 Complete the structured driver support for fiber reconstruction, scope-owned obligations, cancellation, scheduling, and resumption required by durable leases, DAGs, sagas, and root-only continue-as-new.
- [ ] 4.4 Update the joint capability matrix and signature baselines to the implemented post-fiber shapes without introducing a second compiler or provisional builder surface.
- [ ] 4.5 Run the structured-fiber unit, compiler, driver, and strict OpenSpec validation suites and record the prerequisite as complete before section 5.

## 5. Make Authoring Mode-First and Capability-Correct

- [ ] 5.1 Introduce the approved ephemeral and durable workflow factories/builders over the shared compiled graph.
- [ ] 5.2 Implement the portable-node allowlist once and verify identical semantics for both selected-mode Adapters where a capability is shared.
- [ ] 5.3 Expose in-instance `ForEach` only on ephemeral authoring and make the shared compiler reject a manually constructed durable `ForEach` node with the approved diagnostic.
- [ ] 5.4 Expose structural `Wait` and public durable `WaitLong`, retain portable dynamic `StepResult.WaitForEvent`, and remove the internal test-only durable builder helper.
- [ ] 5.5 Move children, external jobs, durable resource leases, and structural root-only continue-as-new to durable authoring and verify nested/non-quiescent continue-as-new rejection.
- [ ] 5.6 Remove `WithDefinitionRetry`, its unused definition policy state, and its tests until definition-wide retry is separately specified and implemented.
- [ ] 5.7 Implement distinct authoring contracts for per-step throttles, named cross-instance transient pools, and durable resource leases, exposing each only where the selected host enforces its contract.
- [ ] 5.8 Remove durable-only variants from portable `StepResult` and implement typed durable external-job nodes with serializer-aware state selectors and authored failure policy.
- [ ] 5.9 Standardize workflow, saga, and DAG completion on `Build()` plus `TryBuild()` returning the existing `Validation<TDefinition>`.
- [ ] 5.10 Make required fluent arguments non-nullable, validate local arguments at call time, aggregate graph diagnostics at build time, and deeply freeze workflow, saga, and DAG metadata.
- [ ] 5.11 Reject empty/default strong IDs and non-positive versions in factories, builders, registration, runtime operations, management, and advanced protocol entry points.
- [ ] 5.12 Introduce the explicit ephemeral saga authoring entry point and keep durable saga authoring absent until runtime-owned forward and compensation progression in section 7 is complete.
- [ ] 5.13 Convert the authoring fixtures into passing presence/absence, compiler-defense, dynamic-wait, pool-taxonomy, build-validation, and identifier guards.

## 6. Deepen the Durable Facade and Align Management

- [ ] 6.1 Add a hosting-owned durable composition root and remove public construction that requires command processors, registries, provider serializers, driver budgets, or observers.
- [ ] 6.2 Implement explicit `runtime.Definitions.Register(definition)` registration returning a typed definition handle; remove start overloads that register as a side effect and return stable `DefinitionNotRegistered` diagnostics.
- [ ] 6.3 Implement typed start and event-delivery contracts without phantom state generics, add a payloadless event overload, and document/test the complete instance/definition/correlation routing result mapping.
- [ ] 6.4 Implement typed external-job completion with application-owned time, serialization, report identity, commit translation, deduplication, and continuation status.
- [ ] 6.5 Implement typed external-job timeout reporting with the same application/protocol separation and race semantics.
- [ ] 6.6 Add the worker-reported failure command/fact and typed `FailAsync` facade operation with reason, optional payload, shared report identity, authored failure-policy progression, and stable duplicate behavior.
- [ ] 6.7 Atomically pair every accepted facade outcome with a continuation-outbox handoff, drive inline only for a locally registered definition, and return `AppliedAndProgressed` or `AppliedPendingContinuation` accurately.
- [ ] 6.8 Move supported raw command access and raw stream-version remediation behind the runtime-protocol package and add a custom-host certification fixture without engine internals.
- [ ] 6.9 Create the canonical asynchronous management root, selection, query, statistics, and instance-handle contracts and consolidate/delete the duplicate query/statistics/safety declarations.
- [ ] 6.10 Adapt ephemeral management to the shared vocabulary while preserving `EvictAsync`, `EvictTerminalAsync`, `DetectStuckAsync`, and `GetLifecycleEventsAsync` as in-memory retention/diagnostic capabilities.
- [ ] 6.11 Implement durable instance handles, typed committed-state access, active waits, pause/resume/history, and stable not-found, wrong-state, archived, purged, and incompatible-state diagnostics.
- [ ] 6.12 Route all durable management mutations through the runtime-owned lane and remove fallback processor creation, caller timestamps, and caller protocol identifiers.
- [ ] 6.13 Implement poison remediation on the durable instance handle with an opaque ticket or stable compare-and-act token; return stable conflict for stale tickets and guarantee continuation after accepted rearm.
- [ ] 6.14 Delete always-throwing overloads, make single-instance termination confirmation-free, and require `None = 0, Confirmed = 1` confirmation only for broad termination and purge.
- [ ] 6.15 Migrate durable application acceptance tests from raw commands to the facade and add passing tests for all routing, split-host, external-job, remediation, management-lane, confirmation, and cross-mode query outcomes.

## 7. Close DAG, Saga, and Scope-Owned Execution Loops

- [ ] 7.1 Apply the deletion test to `WorkflowDagRunner` and fold it into `WorkflowDagPlan` or rename it as a pure planner with no durable-execution implication.
- [ ] 7.2 Add a durable DAG facade entry point accepting a validated plan and root identity without caller-provided progress sets, command IDs, or timestamps.
- [ ] 7.3 Reconstruct DAG scheduled, completed, failed, and blocked state from checkpoint/history/projections after start and restart.
- [ ] 7.4 Schedule newly ready child batches idempotently without duplicating in-flight nodes.
- [ ] 7.5 Resume DAG planning after committed child outcomes until suspension, blockage, failure, or terminal completion.
- [ ] 7.6 Add DAG acceptance tests for restart, duplicate drive, in-flight preservation, blocked reporting, and terminal progression.
- [ ] 7.7 Register durable saga definitions explicitly and implement runtime-owned forward-action execution and committed audit through the structured driver.
- [ ] 7.8 Implement automatic compensation selection, reverse progression, compensation failure, restart recovery, and manual-remediation transitions.
- [ ] 7.9 Add the durable saga authoring entry point only after tasks 7.7 and 7.8 pass, using the shared post-fiber compiler and no interim command Adapter.
- [ ] 7.10 Expose durable saga audit and supported remediation through management without raw action, scope, command, or timestamp identifiers.
- [ ] 7.11 Delete `DurableSagaCommandAdapter` and demote/delete caller-driven durable DAG scheduling types after runtime-owned paths pass acceptance tests.
- [ ] 7.12 Add end-to-end durable saga tests for forward success, timeout, reverse compensation, compensation failure, restart recovery, and application-level remediation.
- [ ] 7.13 Implement and verify deterministic durable-lease release on normal scope exit, branch cancellation, scope failure, and workflow terminal transition, plus crash/restart expiry recovery.

## 8. Make Hosting and Provider Integration Explicit

- [ ] 8.1 Implement explicit ephemeral and durable registration, make `AddOrcaCoreInMemoryDurable` development/test-only with a no-restart-durability startup diagnostic, and remove or repurpose ambiguous `AddOrcaCore()`.
- [ ] 8.2 Add startup validation proving production durable hosting has the required store, projection, outbox, continuation, timer, serializer, and worker capabilities before accepting work.
- [ ] 8.3 Rename PostgreSQL and SQL Server extensions to the durable-store convention and standardize validation, ownership, diagnostics, and replacement behavior.
- [ ] 8.4 Keep Redis explicitly projection-cache scoped and standardize its options and native Adapter ownership without advertising event-store capability.
- [ ] 8.5 Rename RabbitMQ registration to the dispatcher convention and add equivalent `AddOrcaCoreZeroMqDispatcher` registration with options and publisher ownership tests.
- [ ] 8.6 Extend provider certification for role declarations, registration-order independence, replacement semantics, native resource ownership, and unsupported-capability diagnostics.
- [ ] 8.7 Make concrete timer, outbox, continuation, and operational sweep hosted implementations internal and register them through hosting-owned factories.
- [ ] 8.8 Delete `RedisProviderProfile`, internalize `DurableCommandRuntime`, converters, and other implementation-only types without supported external scenarios, and update tests through permitted internal visibility.

## 9. Rewrite Developer Journeys and Documentation

- [ ] 9.1 Rewrite the durable example around explicit definition registration and a live external job, report completion through the typed facade, assert duplicate behavior and final state, and fail on `Poisoned` or raw protocol use.
- [ ] 9.2 Add a split-host sample/test journey where a definition-less callback host reports completion or failure and a definition-owning continuation pump progresses the workflow.
- [ ] 9.3 Update every sample to reference only documented application, hosting, and role-specific provider packages for its audience.
- [ ] 9.4 Publish the joint ephemeral/durable capability matrix covering portable dynamic waits, post-fiber authoring, all three concurrency categories, sagas, DAGs, management, persistence, and hosting.
- [ ] 9.5 Update the root README, developer guides, production-readiness notes, durable-driver requirements, canonical specs, and sample READMEs for explicit registration, split-host continuation, typed routing, job failure, and application-safe remediation.
- [ ] 9.6 Correct stale claims about start-or-get driving/registration, public `WaitLong`, ephemeral saga support, provider-role interchangeability, in-memory durable restart guarantees, and durable `ForEach` behavior.
- [ ] 9.7 Document stable error modes, typed state availability/cost, destructive confirmation versus authorization, provider ownership, and the three-way concurrency taxonomy.
- [ ] 9.8 Add one provider-authoring guide and one custom-host/runtime-protocol guide, each backed by its clean certification fixture and clearly separated from the application quick start.

## 10. Verify, Trace, and Remove Superseded Surface

- [ ] 10.1 Run Core, ephemeral engine, durable engine, hosting, acceptance, and provider unit suites after each affected slice and fix every public-Interface regression.
- [ ] 10.2 Run PostgreSQL and SQL Server restart suites plus dispatcher/provider certification sequentially where shared infrastructure requires it.
- [ ] 10.3 Pack every documented application, hosting, provider, meta, and advanced package and build/run all clean consumer and provider-author fixtures against the artifacts.
- [ ] 10.4 Run public-signature, duplicate-declaration, and exact permitted-project-graph guards and review every baseline change.
- [ ] 10.5 Run every sample under automated assertions and verify no durable journey contains poisoned, unsupported, implicit-registration, or raw-protocol behavior.
- [ ] 10.6 Run split-host continuation, event-routing matrix, external-job completion/timeout/failure, durable-lease lifecycle, DAG, saga, management-concurrency, and destructive-confirmation acceptance suites.
- [ ] 10.7 Run `dotnet build OrcaCore.slnx` and the full non-container solution test suite, then record environment-gated container commands and results.
- [ ] 10.8 Delete obsolete builders, implicit-registration overloads, raw application remediation, duplicate `WorkflowInstanceQueryModel`, `WorkflowStatistics`, `WorkflowStatisticsGroup`, and `DestructiveCommandSafety` declarations, aliases, public hosted implementations, profiles, and compatibility shims.
- [ ] 10.9 Run repository-wide reference and declaration scans proving that every superseded entry point and duplicate type is gone and the four intended tiers remain.
- [ ] 10.10 Strict-validate `reshape-developer-facing-interfaces`, `adopt-structured-fiber-execution`, and `add-runtime-concurrency-limits`, then link every changed canonical requirement to its public acceptance, compile, consumer, architecture, or provider-certification evidence.
