## 1. Establish the source and contract mapping

- [ ] 1.1 Record the implementation revision and build `verification/tla/wait-ownership/crosswalk.md` linking EV-011/020/023/043/044/051 and the OpenSpec event requirements to WOP-001 through WOP-009, source symbols, and existing test assertions.
- [ ] 1.2 Trace public structured/static and dynamic registration, route lookup/snapshot/deduplication, instance-lane execution, timer dispatch, continuation escape, and cancellation cleanup; document atomic/observable boundaries and lower-level-only paths.
- [ ] 1.3 Resolve successor-registration-then-failure reachability, cancellation ordering, bounded consumed-history assumptions, and available test seams with source/schedule evidence. Record any contract conflict explicitly before finalizing model guards.
- [ ] 1.4 Run an independent scoped review of the mapping and assumptions; resolve findings before model conformance is claimed.

## 2. Build the finite contract and protocol models

- [ ] 2.1 Create `WaitOwnershipContract.tla` with independently expressed WOP-001 through WOP-007 predicates and documented observation boundaries.
- [ ] 2.2 Create `EphemeralWaitOwnership.tla` with bounded instances/occurrences/events, explicit lane and index ownership, routing stages, continuation success/escape, successor registration, timer competition, cancellation, and exact release; map internal steps to contract transitions or stuttering.
- [ ] 2.3 Add `SafetySmall` and `SafetyIsolation` configurations with explicit delivery/failure bounds, no counter wrap or silent pruning, and history capacity compatible with the tested implementation.
- [ ] 2.4 Add `ProgressSmall` for WOP-008/009 with explicit fairness and environment assumptions, no symmetry reduction, intentional quiescent stuttering, and detection of unexpected deadlocks.
- [ ] 2.5 Parse and run each normal configuration to complete exploration, retaining state counts and any failures; run an independent scoped model/design review and resolve findings before qualification.

## 3. Qualify reachability and fault detection

- [ ] 3.1 Add and retain witnesses for cross-instance and same-instance contention, handover, sequential same-key reuse, duplicate delivery, key isolation, both event/timer orders, cancellation before/during continuation, and escaping-failure restoration; document the successor-then-failure witness or justified unreachability.
- [ ] 3.2 Add dedicated qualification variants for premature release, missing same-instance guard, successor-reservation deletion, failed-continuation consumption, and dual event/timer resolution; require the specified WOP failure and readable trace for each.
- [ ] 3.3 Add continuation-return and scheduling-assumption removal controls and demonstrate the expected non-progress behaviors without crediting them as normal-model successes.
- [ ] 3.4 Independently review qualification coverage and result classification; reject unreachable faults, parse errors, unrelated failures, or exhausted budgets as successful negative controls.

## 4. Connect model traces to C# regression evidence

- [ ] 4.1 Verify and run the cited `ConcurrentStartTests`, `StructuredFiberExecutionPublicTests`, `WaitMatchingTests`, `LoopWaitTests`, and `TimerEventRaceTests`; credit only assertions and schedules that match the crosswalk.
- [ ] 4.2 Add missing deterministic witnesses for reservation handover/release, rollback, duplicate delivery, cancellation, and isolation using barriers/task completion sources and controlled time; cover public structured and dynamic paths with no sleep-based ordering or public test hooks.
- [ ] 4.3 Minimize and classify every actionable counterexample. For an implementation defect, retain a failing deterministic regression on the affected revision and track a separately scoped fix plus passing rerun; for abstraction errors, correct the model and rerun affected qualification. Record an explicit no-defect result when applicable.
- [ ] 4.4 Independently review source/action/test correspondence and unresolved discrepancies, then run the full ephemeral test project and applicable repository guards for any changed source/tests. Preserve the distinction between selected-schedule evidence and whole-program proof.

## 5. Add reproducible local and CI execution

- [ ] 5.1 Select and record a compatible versioned TLC/Java pair, official artifact URL, and checksum; add `scripts/verification/Invoke-WaitOwnershipModelCheck.ps1` with explicit profiles, bounded time/heap, and separate normal/witness/mutation/assumption-control result classification.
- [ ] 5.2 Verify runner handling of a passing run, expected property counterexample, parse error, unrelated failure, and timeout; retain commands, tool/model/config hashes, state counts, traces, and clear non-passing incomplete outcomes.
- [ ] 5.3 Add a dedicated CI workflow with relevant model/runner/workflow/requirement/source/test triggers and manual dispatch; measure the initial 5-minute per-profile and 15-minute model-job budgets and revise them explicitly if needed. Keep dependencies development-only and branch protection unchanged.
- [ ] 5.4 Run an independent scoped tooling/CI review, then validate the normal profiles, all qualification controls, and linked C# regressions on the same recorded source/model revision.

## 6. Close the pilot with a bounded verdict

- [ ] 6.1 Publish a verification report and complete crosswalk containing exact bounds/assumptions, source revision and any dirty-diff hash, tool/config/model hashes, completed run evidence, test results, counterexample dispositions, and unresolved limitations.
- [ ] 6.2 Independently review the final evidence and classify the result as passed within stated scope, failed, or inconclusive. Grant qualification only when all mandatory profiles, controls, witnesses, mappings, and regressions satisfy the capability spec without unresolved in-scope discrepancies.
- [ ] 6.3 Record measured cost/benefit and an extend/narrow/stop recommendation; keep any durable wait/handoff work in a separate proposal and run strict OpenSpec validation before archival consideration.
