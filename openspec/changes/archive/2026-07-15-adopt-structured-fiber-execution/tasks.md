## 1. Review Gate And Root Baseline

- [x] 1.1 Record explicit approval or requested changes for every item in `design.md` under Review Checklist and resolve the four Open Questions before changing production code.
- [x] 1.2 Before changing production code, amend the canonical requirements to state that this change supersedes CP-001, CP-004, CR-015, CR-044, SG-010 including its per-scope override, the section 13.4 `Parallel` resolution, DR-011, DR-011a, DR-012, AC-204, AC-205, AC-403, DR-AC-032, CP-010 through CP-013, AC-601 through AC-605, and the baseline runtime-resource-governance requirement `Saturation behavior is configurable` where they conflict with cooperative local fibers, structured positions, canonical compensation, strict residual cancellation, isolated `ForEach` items, or owned blocked resource obligations. Reconcile `openspec/changes/add-runtime-concurrency-limits` design sections 3 and 4 so local fibers use committed/in-memory blocked obligations by mode, release the instance turn while blocked, and reserve true concurrency limits for external or cross-instance execution.
- [x] 1.3 Capture the promoted root `OrcaCore.slnx` build and current targeted test baselines, preserving the cursor driver in version control as a diagnostic reference.
- [x] 1.4 Add or update a repository guard proving active projects, documentation, and task commands use root `src/`, `tests/`, `samples/`, and `benchmarks/` and do not recreate `v3-gpt` implementation paths.
- [x] 1.5 Reconcile this change and `reshape-developer-facing-interfaces` around one joint capability matrix, compiler/diagnostic contract, and post-fiber `Parallel`, `WhenFirst`, branch-result, merge, and builder signatures; block section 2 until both changes pass strict validation.
- [x] 1.6 Update both changes so `Build()` returns the compiled-plan-backed definition, `TryBuild()` returns `Validation<TDefinition>`, portable dynamic `WaitForEvent` remains available, and structural `Wait` remains preferred for static event names.
- [x] 1.7 Update both changes and their compile fixtures so durable `ForEach` is absent from the public builder and rejected by the compiler as defense in depth, while continue-as-new is only a structural durable node with root-only quiescent execution.
- [x] 1.8 Reconcile the three-way concurrency taxonomy with `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits`, then update the canonical capability matrix and strict-validate all three affected documents before source work.

## 2. Compiled Plan And Validation

- [x] 2.1 Add failing `OrcaCore.Core.Tests` cases for exactly one root `Init`, exactly one root `End`, no executable nodes after `End`, rejection of nested workflow terminals, and static or deterministic final-state selection of the root `End` outcome required by CR-008.
- [x] 2.2 Add failing post-fiber mode-first builder tests for `Build()`/`TryBuild()`, one reachable `BranchReturn`, structural root-only `ContinueAsNew`, complete successful-path termination, duplicate or blank branch identity, and accumulated diagnostics.
- [x] 2.3 Add failing compiler tests for stable instruction/scope/branch plan identities, positive instruction allowlisting, ephemeral `ForEach` acceptance, durable public-surface absence plus manual-node compiler rejection, serializer availability, post-fiber merge/result type compatibility, positive internal-instruction and scope/fiber limits, and rejection of loop cycles with no quantum-ending operation.
- [x] 2.4 Add failing authoring/compiler tests proving nested builders require no closing nodes and lower to stable `IfJoin`, `LoopBack`, `LoopExit`, `ScopeJoin`, and `ScopeExit` instructions.
- [x] 2.5 Implement compiled instruction, branch, scope, merge, and policy-plan models in root `src/OrcaCore.Core` with no durable provider dependencies.
- [x] 2.6 Implement `DefinitionCompiler` lowering and validation until the tests from 2.1 through 2.4 pass for each mode's supported capabilities, including lowering ephemeral `ForEach` to a dynamic scope and rejecting it for durable execution.
- [x] 2.7 Add failing fingerprint tests covering deterministic recompilation, graph/configuration drift, and same-version replacement, then implement compiler format version and canonical plan fingerprinting.
- [x] 2.8 Add failing atomic-registration tests, then change root durable and ephemeral registries so failed compilation publishes neither a definition nor an executor and same-version fingerprint drift is rejected.

## 3. Shared Fiber And Scope Model

- [x] 3.1 Add failing reducer tests for fiber creation, runnable/blocked/completed/failed/cancelled transitions, preserved parent position, recursive scope lifecycle, deterministic root identity per `ContinueAsNew` generation, and deterministic scope/fiber identity across loop re-entry, replay, duplicate claims, and host replacement.
- [x] 3.2 Implement shared `FiberId`, `ScopeId`, fiber records, scope records, phases, blocked reasons, and transition results in root Core Modules. Mint the root from instance id and committed generation, mint each scope from parent fiber, scope plan id, and persisted parent scope-entry sequence, and mint children from scope id plus authored branch id or `ForEach` item index; persist the increment atomically with scope start.
- [x] 3.3 Add failing scheduler tests for authored-order startup, round-robin turns, newly created and resumed fibers, same-transition tie ordering, and bounded starvation.
- [x] 3.4 Implement the deterministic runnable queue and next-fiber cursor without persistence-specific code.
- [x] 3.5 Add failing quantum tests for one user-step invocation, suspension operations, branch return, failure, `Yield` requeue behavior, and `MaxInternalInstructionsPerQuantum` with positive validation, default 1024, persisted progress, forced successful requeue, and sibling rotation when the limit is reached.
- [x] 3.6 Implement the shared linear fiber interpreter and scope reducer against `CompiledWorkflowPlan` until quantum and lifecycle tests pass; evaluate `ScopeJoin` and `ScopeExit` as reducer transitions at commit boundaries rather than as selected-fiber instructions.
- [x] 3.7 Add failing aggregate-runnability and mode-reporting tests, then implement `Running`, `Waiting`, terminal, durable-only `Parked`, and ephemeral typed-failure derivation from the complete fiber/scope state.
- [x] 3.8 Add failing oracle-comparison tests, then build the deterministic fiber/scope reference model, generated definition and schedule inputs, seeded replay/crash permutations, and the comparison harness consumed by later merge, cancellation, provider, and final-verification tests.

## 4. Branch State, Results, And Merge

- [x] 4.1 Add failing contract and builder tests for branch input projection, branch-private state, one common serializable `TResult`, heterogeneous outcomes represented through an authored union or record, and item-index-ordered `ForEachItemOutcome<TResult>` values.
- [x] 4.2 Implement branch input/state/result and `ForEachItemOutcome<TResult>` contracts plus builder nodes without extending ordinary `StepResult` into a business-result channel.
- [x] 4.3 Add failing alias-isolation tests proving branch input materialization is a deep serialization or registered-copy operation and cannot mutate the parent or sibling data.
- [x] 4.4 Add failing merge tests for authored result order, replacement parent state, serialization-before-commit, merge exceptions, and no branch-body rerun.
- [x] 4.5 Implement pure synchronous `WhenAll` and winner merge adapters with canonical ordered inputs and explicit diagnostics on merge failure.
- [x] 4.6 Add failing join-policy tests for `WhenAll` fail-fast behavior and `WhenFirst` first-terminal winner, same-transition tie breaking, failed winner, and mandatory loser cancellation.

## 5. Ephemeral Execution Adapter

- [x] 5.1 Add failing ephemeral tests for straight-line compiled execution, nested scopes, cooperative sibling fairness, fiber-local waits, `Yield`, `WhenAll`, `WhenFirst`, and `ForEach` through the same scheduler and scope reducer.
- [x] 5.2 Implement the in-memory execution-state Adapter over the shared compiler, interpreter, scheduler, and scope reducer.
- [x] 5.3 Convert existing root ephemeral composition tests, including `ForEach`, from shared mutable branch state to isolated branch/item results and explicit merge where parent state changes.
- [x] 5.4 Add ephemeral cleanup tests for losing waits, timers, jobs, resources, and nested descendants, then implement the shared ownership traversal used by the Adapter.
- [x] 5.5 Add failing ephemeral `ForEach` tests for partitioning, item-index ordering, admitted-nonterminal `maxConcurrency`, isolated item state, `WhenAll` failure policies, first-committed-terminal `WhenAny` selection, same-commit item-index ties, failed winner without merge, successful single-winner merge input, rejection of non-`FailFast` `WhenAny` combinations, and mandatory residual cancellation.
- [x] 5.6 Implement dynamic `ForEach` scopes over the shared scheduler and reducer until the tests from 5.5 pass; pending cancelled items SHALL NOT require fiber creation.
- [x] 5.7 Remove or bypass the old ephemeral parallel, `WhenFirst`, `ForEachNodeRunner`, and `ForEachWorkScheduler` execution paths only after the new acceptance set is green.

## 6. Envelope V2 And Version Binding

- [x] 6.1 Add failing abstraction serialization tests for envelope format 2 containing instance id, `ContinueAsNew` generation, plan binding, parent state, fibers, scopes, loop iteration, per-fiber next scope-entry sequence, scope plan/entry identities, scheduler position, results, ownership references, and diagnostics.
- [x] 6.2 Implement envelope v2 contracts and source-generated serialization in root `src/OrcaCore.Abstractions`.
- [x] 6.3 Add failing mapper tests for nested runnable and blocked fibers, pending results, merge state, retry/yield state, and exact round-trip equality.
- [x] 6.4 Implement durable envelope mapping and aggregate checkpoint integration without retaining cursor-format interpretation.
- [x] 6.5 Add failing stale-envelope and plan-fingerprint tests, then implement explicit `Parked` diagnostics for format, compiler, definition, and fingerprint mismatch.
- [x] 6.6 Add development store/fixture reset documentation and remove any test expectation that format-1 cursor envelopes resume.

## 7. Durable Fiber Driver

- [x] 7.1 Add failing durable driver tests for one linear root fiber, command/elapsed segment budgets, successor continuation emission, and persisted next-fiber rotation.
- [x] 7.2 Implement durable fiber segment execution through the existing durable host, command processor, aggregate commit, and continuation outbox Seams.
- [x] 7.3 Add failing crash tests after scope creation including loop-scope re-entry, after each branch result, immediately before merge, after merge response loss, and between sibling turns; every replay SHALL recover the committed scope-entry sequence and the same runtime identities.
- [x] 7.4 Implement atomic scope/result/merge transitions so replay never skips a fiber, duplicates parent continuation, or reruns a completed branch body.
- [x] 7.5 Add failing multi-host tests for duplicate continuation claims, optimistic conflicts, reload-before-retry, deterministic same-transition winner selection, and reuse of committed scope/fiber identities rather than allocating duplicate loop scopes.
- [x] 7.6 Implement durable conflict recovery and continuation retention for the fiber envelope while preserving per-instance serialized mutation.
- [x] 7.7 Add failing nested fairness tests with a repeatedly yielding first branch, then verify restart preserves the next sibling turn.

## 8. Obligation Ownership And Root Control

- [x] 8.1 Add failing aggregate and driver tests requiring `FiberId` and `ScopeId` on waits, timers, pending resumes, resource tickets, external jobs, child groups, retries, and cancellation records.
- [x] 8.2 Implement explicit owner fields through root abstractions, aggregate state, commands/events, codecs, checkpoints, projections, and management snapshots.
- [x] 8.3 Add failing post-order cancellation tests for nested scope failure, `WhenFirst` loser selection, parent cancellation, termination, and merge failure.
- [x] 8.4 Implement one scope cleanup traversal that emits wait/timer cancellation, resource release, job stop, and supported child-cancel facts before owner removal.
- [x] 8.5 Add failing registration tests for every branch shape whose failure, join, or root-cancellation path can induce cancellation of child-owning or otherwise durably uncancellable work, then reject those definitions until their cancellation protocol exists.
- [x] 8.6 Add failing root-control tests for branch `End`, branch `ContinueAsNew`, rejection of root `ContinueAsNew` while any descendant scope or owned obligation is nonterminal, and valid rollover when the root is the sole nonterminal fiber.
- [x] 8.7 Implement root-only terminal and `ContinueAsNew` rules so invalid rollover leaves generation, state, and ownership unchanged, while valid rollover increments the generation before minting the new root identity.
- [x] 8.8 Add failing wait-matching tests for nested scopes that reuse local branch names and for owner-distinct waits sharing the same event name and correlation. Persist a per-instance registration `WaitSequence`, resume the lowest sequence with stable `FiberId` tie breaking, and route the completion to that exact owner.

## 9. Saga And DAG Integration

- [x] 9.1 Add failing saga tests proving a committed forward action is immediately compensation-eligible in its branch scope, pre-merge scope failure covers all committed descendant records, successful merge transfers those records upward unchanged, restart preserves coverage, and canonical sibling ordering is independent of completion order while deterministic plan-bound per-scope overrides remain honored.
- [x] 9.2 Implement saga compensation over structured fibers and scopes using reverse sequence order for linear work and reverse canonical authored order for sibling work.
- [x] 9.3 Add failing DAG tests proving the parent fiber treats a child-instance group as one owned obligation and resumes once after the required child policy completes.
- [x] 9.4 Complete DAG advancement through existing child-instance dispatch without adding an in-instance DAG scheduler.

## 10. Providers And End-To-End Recovery

- [x] 10.1 Extend provider certification with real nested envelope v2 round trips, optimistic replacement, continuation claims, and owner-index preservation.
- [x] 10.2 Add PostgreSQL host-replacement tests with mixed runnable/blocked fibers, pending merge results, and exact next-fiber recovery.
- [x] 10.3 Add equivalent SQL Server host-replacement tests and verify provider behavior matches PostgreSQL.
- [x] 10.4 Add end-to-end regressions for crash after final branch result, duplicate external completion, losing-child cleanup when supported, and zero orphan waits/timers/jobs/resources.
- [x] 10.5 Run independent provider suites with isolated build outputs or sequential fallback when shared compiler/output locking appears, and record verified baselines.

## 11. Legacy Removal And Documentation

- [x] 11.1 Delete `DurableDriverCursor`, cursor frame split/join, `MergeCompletedCursors`, cursor candidate selection, and format-1 envelope models after all replacement gates pass.
- [x] 11.2 Remove shared-state ephemeral branch/item execution, old `ForEach` runner/scheduler paths, unsupported `WhenFirst.Ignore`, `WhenFirst.LetRemainingComplete`, and `ForEachResidualPolicy.LetRemainingComplete` Interface values and tests.
- [x] 11.3 Add repository guards preventing references to deleted cursor execution types and preventing runtime parking as a substitute for compiler capability validation.
- [x] 11.4 Complete the final documentation sync after the pre-implementation amendments in 1.2: update `docs/specs/03-domain-model-and-glossary.md` with plan, fiber, execution-scope, quantum, branch-return, merge, and fingerprint terms; preserve CR-008 through the single root `End` outcome selector; update the named CP, CR, SG, DR, AC, and DR-AC entries; and align traceability, root active-implementation documentation, samples, and feature matrices.
- [x] 11.5 Update benchmarks to measure compiler cost, quantum scheduling, envelope size, nested scope advancement, merge replay, and provider checkpoint materialization.

## 12. Final Verification

- [x] 12.1 Run root Core, ephemeral, durable, hosting, acceptance, and provider-certification suites with Release warnings treated as errors.
- [x] 12.2 Run PostgreSQL and SQL Server integration suites, including host replacement and crash/replay scenarios, against clean containers.
- [x] 12.3 Run the reference model, generators, and comparison harness built in 3.8 across seeded nested scopes, dynamic `ForEach` items, completion orders, yields, duplicate deliveries, failures, cancellation, and crash points.
- [x] 12.4 Verify every supported scenario ends with one canonical parent state, at most one merge, one parent continuation, bounded scheduler progress, and zero orphan obligations.
- [x] 12.5 Validate OpenSpec status, canonical document links and IDs, root solution/project references, samples, and benchmark commands before marking the change complete.

## 13. Post-Review Remediation

- [x] 13.1 Add failing production-engine regressions for a waiting branch with a runnable sibling, then derive ephemeral and durable management status from committed fiber state.
- [x] 13.2 Add a failing ephemeral duplicate-wait regression, then persist in-memory `WaitSequence`, `FiberId`, and `ScopeId` and select the lowest sequence with stable fiber tie breaking.
- [x] 13.3 Add failing durable and ephemeral production-path quantum tests, then enforce `MaxInternalInstructionsPerQuantum`, forced rotation, durable checkpointing, and cooperative ephemeral yielding.
- [x] 13.4 Move the non-production linear interpreter into the Core test tree as an explicit reference interpreter while sharing scheduler, reducer, status, and quantum primitives with production.
- [x] 13.5 Fix logical timer identity, centralize the external-job completion event name, add immutable plan lookup indexes, cache typed hot-path invocation, and remove quadratic scheduler admission checks.
- [x] 13.6 Add a failing checkpoint-only continuation regression and emit a successor `continue` record whenever a format-2 checkpoint remains runnable.
- [x] 13.7 Re-run Core, ephemeral, durable, hosting, acceptance, provider certification, PostgreSQL, SQL Server, integration, strict OpenSpec, and Release warning-as-error gates; update the implementation status with exact baselines.

## 14. Archive-Blocking Review Remediation

- [x] 14.1 Add failing compiler regressions for path-complete no-progress loop detection, non-empty structured scopes, configured depth/fiber limits, and positive serialized-size limits.
- [x] 14.2 Enforce static scope-depth and active-fiber limits during compilation and dynamic active-fiber limits before scope admission.
- [x] 14.3 Add failing result/envelope-size regressions and enforce configured serialized payload limits before ephemeral transitions and durable commits.
- [x] 14.4 Add failing fingerprint and plan-mutation regressions, then bind captured declared configuration and publish deeply immutable compiled plans.
- [x] 14.5 Add failing serializer-contract regressions, then make compiler serializer availability agree with the codecs used by both production engines.
- [x] 14.6 Add failing scope-in-loop retention regressions, then prune completed scope subtrees after merge without breaking owned-obligation cleanup or replay identity.
- [x] 14.7 Add failing ephemeral retry fairness regressions, then represent retry/backoff as an owned blocked fiber transition with at most one user-step invocation per quantum.
- [x] 14.8 Add failing transient-pool sibling regressions, replace semaphore-backed ephemeral governance with bounded token channels, and make saturated local pool acquisition release the instance turn and resume the exact owning fiber after grant or cancellation.
- [x] 14.9 Remove or compiler-reject durable transient-pool authoring until the durable host implements the selected-mode transient governance contract.
- [x] 14.10 Add failing durable wait-sequence regressions and persist a monotonic per-instance next registration sequence in envelope v2.
- [x] 14.11 Re-run focused Core, ephemeral, durable, and acceptance suites plus adjacent envelope/provider guards and strict OpenSpec validation.
- [x] 14.12 Re-run the Release warning-as-error solution build and update the implementation-status review artifact with the remediated evidence.

## 15. Ephemeral Correctness Review Remediation

- [x] 15.1 Add a failing ephemeral regression for `ForEach` `WhenAny` residual cancellation through a nested scope, then reuse post-order ownership traversal so every losing descendant fiber and scope is cancelled before merge.
- [x] 15.2 Add failing ephemeral regressions for throwing merge, branch-return projection, and resumed wait-correlation delegates, then convert non-cancellation delegate failures into observable workflow failure without leaking internal execution exceptions.
- [x] 15.3 Add failing delayed-retry and resource-grant resume regressions, then add last-resort background-task failure handling that terminates the owning instance instead of producing an unobserved exception.
- [x] 15.4 Add a failing user-step `NotSupportedException` regression, then convert it through the ordinary step-failure path while retaining explicit engine handling for unsupported control results.
- [x] 15.5 Remove per-instruction status derivation, duplicate durable-envelope serialization, and linear instruction-successor lookup from the identified hot paths without changing runtime behavior.
- [x] 15.6 Re-run focused and broad runtime suites, strict OpenSpec validation, and the Release warning-as-error build; update the implementation-status review artifact with exact evidence.
