## Context

The original proposal was based on `origin/master` at `ba2b057d7898f516845e90323b4288fb0c1744d8`. Implementation evidence is recorded under `verification/tla/wait-ownership/`; see its verification report for the current verdict. The design does not itself grant runtime certification.

The existing contract rejects a second active wait for one `(DefinitionId, event contract name/version, CorrelationId)`, including within one instance, while allowing sequential reuse after consumption. `EphemeralRoutingIndex.Reserve` separately retains matched reservations until continuation completion and counts overlapping old/new reservations belonging to the same instance. `WorkflowInstance.EnterWait` supplies the same-instance active-wait check that the index alone does not provide. These are distinct obligations.

`ResumeWaitAsync` and `FireWaitTimeoutAsync` temporarily remove a wait, run a continuation, and either settle or restore state on an escaping exception. The ephemeral router also performs snapshot checks and event-identity bookkeeping outside the instance lane. A model that makes a complete route/continuation operation globally atomic would hide the relevant interactions. Conversely, allowing arbitrary same-instance mutations during a serialized lane operation would invent impossible executions.

The primary consumers are runtime maintainers and reviewers of event contracts. Existing runtime requirements remain authoritative; the new capability governs verification evidence.

## Goals / Non-Goals

**Goals:**

- Explore a finite ephemeral ownership protocol across competing instances and successive occurrences.
- Check safety independently from progress, with explicit environmental assumptions.
- Map model transitions and observable outcomes to real synchronization boundaries and deterministic tests.
- Demonstrate that checks detect seeded faults, and report genuine failures without weakening the properties to obtain a passing run.
- Produce reproducible evidence and a cost/benefit decision before extending the technique.

**Non-Goals:**

- Durable acceptance, global durable event identity, broker acknowledgement, SQL transactions, restart, crash recovery, outbox, or retention/eviction guarantees.
- Full workflow execution, user payload/state semantics, event serialization, start-idempotency, host shutdown, resource leases, or complete fiber scheduling. Represent competing branch/item ownership by occurrence labels, not a scheduler implementation.
- Arbitrary delayed-event occurrence disambiguation: same-key successive waits have signal-stream semantics; occurrence-specific intent requires a distinct correlation identity.
- Proving all C# executions, all configurations, or exactly-once physical user-code invocation; no TLAPS proof or automatic C# verification is part of the pilot.
- Product/API changes inferred from a model. A counterexample requiring such a change is separately proposed and reviewed.

## Decisions

### 1. Separate contract predicates from protocol mechanics

Use plain TLA+ with TLC. Place contract predicates in `verification/tla/wait-ownership/WaitOwnershipContract.tla` and implementation-aligned transitions in `EphemeralWaitOwnership.tla`. The former specifies allowed ownership and successful resolution outcomes independently of dictionary/refcount implementation; the latter represents routing, reservations, lane ownership, and continuation stages. Check the contract predicates against protocol states and record how internal steps project to observable transitions or stuttering. This is bounded model validation, not a proof that C# refines the specification.

Plain TLA+ makes transition boundaries explicit. PlusCal is an acceptable learning aid but is not required; a monolithic model of the engine or a full proof system would make the pilot unnecessarily expensive. Existing tests alone remain essential but do not enumerate every ordering of the modeled actions.

### 2. Finite scope and state

Commit explicit configurations rather than silently restricting exploration with state constraints:

| Configuration | Bounds | Purpose |
|---|---|---|
| `SafetySmall` | 2 instances; 1 definition/contract/key; 2 occurrences per instance; 2 event IDs; at most 2 delivery attempts per event per target and 1 escaping continuation failure per occurrence | Contention, same-instance duplicates, sequential reuse, rollback, event/timer/cancellation races |
| `SafetyIsolation` | Same bounds, with key assignments varied across definition, contract name, contract version, and correlation; enumerate two distinct full keys per case | Verify exact key isolation without an enormous Cartesian product |
| `ProgressSmall` | Small safety bounds, no symmetry reduction; finite arrivals/failures and explicit fairness below | Conditional settlement and release |

Occurrence identifiers are finite tuples `(instance, occurrence slot)` with optional owner labels for sibling distinction. Counters stop at their configured maximum and never wrap. Preserve final states through an explicit quiescent stuttering action; do not suppress unexpected deadlock checking globally. Boundary exhaustion is reported as bounded scope, not universal success.

State includes wait lifecycle and identity; active occurrences; correlation owner and reservation count; exact per-occurrence reservation obligations; event ID/equality-class and per-target consumed history; in-flight routing requests; published route visibility; instance-lane ownership; event/timeout/cancellation contender state; continuation result; and terminal/cleanup state. Keep history counters for attempts separate from successful logical resolutions. Exclude payload bytes and use opaque equality classes only where needed. Finite exact sets retain all modeled consumed identities for the pilot lifetime; eviction is excluded. The crosswalk must verify that the event bound fits the implementation's bounded consumed-history capacity; no post-eviction deduplication claim follows.

Public ephemeral unmatched delivery has no durable buffering promise. Model `NoActiveWait` as a possible route outcome. Internal pending-event state is included only for reachable runtime schedules exposed by the source mapping; document any excluded ingress path. Do not import durable retained-event or global deduplication properties. Duplicate identity means the same complete modeled envelope sent to the same target; changed-envelope conflict validation is outside this ownership pilot and cannot be credited.

### 3. Action boundaries and assumptions

Represent registration attempt/rejection, correlation lookup, snapshot observation, route enqueue, event or timer claim, continuation success/escaping failure, successor registration, cancellation request/observation, terminal cleanup, and exact reservation release as separate actions wherever source permits interleaving. Do not treat `Accepted` as a consumption boundary without tracing the actual path.

Map each `routingGate` operation to one atomic index action. Preserve instance-lane exclusion across awaited work where the code does so; allow other instances to progress. Router lookup/snapshot/deduplication and lane work do not share one global atomic action. Document projection points for transient bookkeeping and explain which predicates hold at every internal state versus at externally observable/settled boundaries; never exempt a state that another actor can observe.

Inventory the actual facade/structured execution path, timer callback dispatch, and lower-level wait path before finalizing `Next`. Treat lower-level-only behavior separately if public execution cannot reach it. In particular, establish whether successor registration followed by an escaping continuation failure is reachable, and whether cancellation mutates state immediately or queues behind lane work. An unproven exclusion blocks conformance credit for that scenario. A business failure converted into terminal workflow state is not an escaping exception that restores the prior wait.

### 4. Properties to check

These are verification obligations derived from the existing contract or internal accounting; conflicts must be reported, not treated as newly approved product behavior.

| ID / proposed operator | Required meaning |
|---|---|
| WOP-001 `TypeOK` | State domains, ownership references, and counters remain well formed; reservation counts never become negative. |
| WOP-002 `UniqueActiveWait` | At most one active occurrence owns a full correlation key across and within instances. Duplicate registration cannot install a second wait or acquire a second conflicting reservation; subsequent workflow failure cleanup is a separate transition. |
| WOP-003 `ReservationOwnership` | Every active or unresolved matched occurrence retains its reservation; one key has at most one instance owner. At each declared atomic observation boundary its count equals that owner's live reservation obligations. Same-instance old-matched/new-active overlap is permitted. |
| WOP-004 `SingleResolution` | An occurrence has at most one successful logical resolution across event, timeout, or terminal cancellation; an identical event cannot successfully consume twice on the same target in the modeled lifetime. Escaping failed continuation attempts do not count as success. |
| WOP-005 `ExactRelease` | Releasing/cleaning one obligation changes only that obligation's contribution; it cannot erase a successor or unrelated owner's reservation. After its last obligation is settled and released, the key has no owner. |
| WOP-006 `RollbackConsistency` | An escaping continuation failure preserves/restores the prior unresolved wait, reservation, modeled event eligibility, and timer bookkeeping according to the owning path; it creates no successful consumption. If source permits successor creation first, include its actual rollback/cleanup behavior and check WOP-002/003 as well. |
| WOP-007 `KeyAndOccurrenceIsolation` | Matching uses the complete exact key and only the selected current occurrence; cleanup and stale timer callbacks cannot affect a different occurrence. A genuinely new same-key signal can satisfy a later occurrence; repeating an already consumed event cannot. |
| WOP-008 `ConditionalSettlement` | Once an admitted continuation has a final outcome available, it eventually settles or rolls back; terminal cleanup eventually releases owned reservations. Assumptions are explicit below. |
| WOP-009 `ConditionalReuse` | Once all prior obligations are released, a persistent eligible registration with no competing claimant or cancellation eventually acquires the key. This is not a promise of fairness among perpetual contenders. |

Safety runs impose no fairness to hide bad prefixes. Progress runs assume an alive host, finite external requests and injected failures, eventual return of admitted user continuations, eventual processing of requested cancellation, and weak fairness of continuously enabled internal scheduling/settlement/release actions. Timer progress is claimed only with an explicit eventual-due/dispatch assumption. They do not assume that an external event will arrive or that all workflows terminate. Separate assumption-removal controls must exhibit a valid blocked/infinite behavior when a return or scheduling assumption is removed; such behavior is expected, not a runtime defect.

### 5. Reachability and negative controls

Retain witness traces for cross-instance contention; same-instance sibling rejection; matched-old/new-active handover; key reuse; duplicate delivery; both event/timer orders; cancellation before and during continuation; escaping failure/restoration; key isolation; and the successor-then-failure case if reachable. Existing loop tests with changing correlations do not establish same-key signal-stream behavior.

Use dedicated configurations/qualification modules for these mutations, leaving the normal model unchanged:

| Mutant | Expected failed obligation |
|---|---|
| Release correlation on match before continuation settles | WOP-003, with a second instance attempting registration |
| Remove same-instance duplicate-active registration guard | WOP-002 |
| Delete an entire key when releasing the old occurrence despite a successor reservation | WOP-003 or WOP-005 |
| Mark event consumed before an escaping failure and omit restoration | WOP-006 |
| Permit event and timer to record successful resolutions for one occurrence | WOP-004 |

Each expected failure must be a completed checker result naming an allowed property with a retained trace. Parse errors, unreachable mutant guards, timeouts, out-of-memory, and unrelated invariant failures do not qualify. Witness checks deliberately seek a state (for example through a negated reachability predicate) and are reported separately from normal passing verification.

### 6. Validate against code and tests

Maintain `crosswalk.md` with requirement anchors, property IDs, model actions/projection, exact source symbol and synchronization mechanism, test method and schedule, result, and any unsupported assumption. Seed it with:

| Requirement / property | Source boundary | Existing tests to inspect and reuse |
|---|---|---|
| EV-011; WOP-002/003 | `EphemeralRoutingIndex.Reserve`; `WorkflowInstance.EnterWait` | `Facade/ConcurrentStartTests.ConcurrentStartsReachingTheSameWait_AdmitExactlyOne`; `Execution/StructuredFiberExecutionPublicTests.DuplicateWaitAfterAcceptance_FailsBeforeParkingTheSecondWait` (both structural/dynamic cases) |
| EV-043; WOP-003/005/007 | `ResumeWaitAsync`, reservation release callback | `Execution/StructuredFiberExecutionPublicTests.ConsumedWait_AllowsTheSameKeyToRegisterAndRouteAgain`; `Execution/LoopWaitTests` (distinct-correlation isolation only where applicable) |
| EV-020/023; WOP-004/007 | `RuntimeWaitRecord.Matches`; `WorkflowInstance.RaiseEventAsync`; `EphemeralWorkflowEventRouter.RouteToInstanceCoreAsync` | `Execution/WaitMatchingTests.RaiseEventAsync_TwoConcurrentMatches_OnlyOneContinuationCommits`; `RaiseEventAsync_WrongNameOrCorrelation_LeavesInstanceWaiting` |
| EV-044/051; WOP-004/005/006 | `FireWaitTimeoutAsync`, `CancelWait`, `ResolveBranchRuntimeWork`, `SuspensionScheduler`, timer dispatch | `Timers/TimerEventRaceTests.EventBeforeTimeout_ConsumesEventAndCancelsTimer`; `TimeoutBeforeEvent_FailsAndCancelsWait`; `TimedOutInstance_DoesNotPoisonSameWaitSignatureForFreshInstance` |
| WOP-006/008/009 | `InstanceExecutionLane`, escaping exception paths, cancellation and cleanup | New deterministic witnesses as required; no existing coverage credit merely from a similar test name |

All test paths are relative to `tests/OrcaCore.Engine.Ephemeral.Tests/`. Verify each test's assertions and actual path before claiming coverage. Run both public structured/static and dynamic registration paths; lower-level tests alone are insufficient evidence for public behavior.

For every actionable counterexample: minimize the trace, classify it as contract contradiction, model abstraction error, implementation defect, or unsupported assumption, then drive the relevant C# schedule using barriers, task completion sources, and a controlled `TimeProvider`. Assert observable winner, wait identity, registration outcome, and eventual cleanup; use existing internal test access only where needed to observe a reservation. Avoid sleeps and timing-luck loops. No public test hooks or friend expansion is assumed. Check in a readable mapping from trace actions to test steps; automatic trace-to-test generation is deferred.

An implementation defect requires a failing deterministic regression on the affected revision before a fix and a passing rerun afterwards. A fix is separately scoped; this pilot does not authorize silently changing requirements. If no defect is found, demonstrate representative successful traces and mutation sensitivity rather than inventing a bug. Tests are evidence for the selected schedules, not exhaustive implementation proof.

### 7. Tooling, CI, and completion evidence

During implementation select a versioned official TLC release supporting the required features, pin its checksum and a compatible Java runtime, and record exact command lines. Never use a mutable latest/nightly download. Proposed runner: `scripts/verification/Invoke-WaitOwnershipModelCheck.ps1`, invoking the CLI directly, e.g. `java -cp <pinned-jar> tlc2.TLC -config <config> <module>`. No editor installation is required. Retain textual error traces; JSON export is optional only if supported by the chosen release.

Use one dedicated CI workflow for the model and qualification profiles, triggered by relevant model, runner, workflow, requirement, and owning source/test changes plus manual dispatch. Run focused C# regressions with the existing .NET tooling. Initial budget: at most 5 minutes per configuration and 15 minutes for the model job, with a documented heap cap appropriate to its runner. Measure and revise bounds/budgets explicitly before making the job a required gate; time exhaustion always means inconclusive, never pass. Do not reduce bounds automatically to make CI green. Branch-protection changes are a separate repository administration decision.

For each normal model profile require complete exploration and all configured properties passing; require separately classified mutation/witness/assumption-control outcomes. Evidence records source commit and dirty-diff hash if applicable, model/config/tool hashes, Java/TLC versions, bounds, assumptions, commands, state counts, completion status, elapsed time, memory limit, property results, traces, test names/results, and unresolved findings. A syntax-only pass or a passing model with missing code mapping is not pilot completion.

The final verdict is `passed within stated scope`, `failed`, or `inconclusive`, with a separate recommendation to extend, narrow, or stop adoption. Qualification of the checked baseline requires all mandatory profiles, mutants, witnesses, crosswalk entries, and applicable C# regressions, with no unresolved in-scope semantic discrepancy. A failed or inconclusive research result can be reported as useful discovery but cannot be marked as runtime qualification. Runtime and model changes must refresh the affected evidence.

Primary tool references: [TLA+ core tools](https://github.com/tlaplus/tlaplus), [CLI use](https://github.com/tlaplus/tlaplus/blob/master/USE.md), [finite model exploration](https://learning.tlapl.us/intro/platform/), and [fairness](https://lamport.azurewebsites.net/tla/tutorial/session9.html).

## Risks / Trade-offs

- Model/code drift or unjustified atomicity -> require source-symbol and lock/lane mapping, public-path witnesses, and evidence refresh on affected changes.
- State-space explosion -> separate small contention and isolation configurations; report exact bounds and inconclusive runs rather than silently pruning behavior.
- Vacuous success -> require reachable scenario witnesses, mutation-specific failures, and assumption-removal controls.
- Overstated progress or durability -> keep liveness assumptions explicit and all guarantees confined to an alive ephemeral process and modeled lifetime.
- Ambiguous rollback or reentrant successor behavior -> resolve reachability from source and tests before claiming conformance; retain a failing model if a genuine discrepancy remains.
- Maintenance cost -> keep the pilot development-only and evaluate measured runtime, useful findings, and mapping effort before expanding it.

## Migration Plan

No production migration is required. Implement model and crosswalk first, qualify locally, add deterministic test witnesses, then enable the bounded CI workflow. Removing the pilot removes development verification files and its workflow without changing runtime behavior. Preserve counterexample-derived regression tests that protect valid product requirements.

## Open Questions

- Which exact public-path boundaries allow successor registration before a prior continuation can fail? Task 1 must answer this with source and schedule evidence, not an assumed transaction.
- Does the existing test access expose enough reservation/lane detail for every mandatory witness without new hooks? Resolve in Task 1; propose any additional internal seam explicitly.
- Which versioned TLC/Java pair meets the runner needs, and do the chosen finite profiles fit the initial budgets? Resolve before CI qualification.
- Would the measured benefit justify a separate durable wait/handoff model? Decide only after the pilot verdict; it is outside this change.

## Implementation review adjustments (2026-11-17)

The source model and a deterministic compiled regression both exposed WOP-F001: caller cancellation after successor registration restores two active same-key waits. Normal source configurations retain the failing invariant and report failed qualification. The scoped runtime correction and passing rerun remain open. `WaitMatchingTests.cs` was found excluded from compilation; new compiled tests and the crosswalk replace that proposed coverage credit. Independent review added a stale-callback identity action, mutation, and witness; strengthened race/cancellation witnesses to completed outcomes; and added control-specific fairness with a positive progress control and completion witness. Tooling review added a regression rejecting unrelated temporal-property failures. These additions qualify the diagnostic checks and do not change production requirements or hide the source defect.

## Full-bound acceptance for task 2.5 (2026-11-23)

The owner approved checking each literal target separately if independent review establishes coverage of every original safety and progress obligation. Execute all six configurations for both targets. Retain the two instances, occurrence and event bounds, interleaving points and fairness assumptions. The shared reservation interface admits extra peer histories, so a failing trace requires concrete replay. [Projection and correspondence](../../../../verification/tla/wait-ownership/composition.md)
