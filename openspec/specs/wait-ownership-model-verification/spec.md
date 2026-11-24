## Purpose

Define how OrcaCore verifies ephemeral wait ownership with a finite TLA+ model, executable C# schedules, and reproducible evidence. Qualification applies to the recorded bounds and environmental assumptions.

## Requirements

### Requirement: The pilot models a bounded ephemeral ownership protocol

The project SHALL maintain a finite TLA+ protocol model and separately expressed contract predicates for ephemeral wait ownership. The baseline model SHALL include two competing instances, two wait occurrences per instance, two event identities, duplicate delivery, continuation success and escaping failure, timeout, cancellation, and exact reservation cleanup. Full keys SHALL distinguish definition, contract name/version, and correlation. The implementation SHALL provide separate contention, key-isolation, and progress configurations with explicit finite bounds, assumptions, observable projection points, and exclusions.

The model SHALL distinguish active waits, unresolved matched reservations, and completed consumption. Same-instance matched-predecessor/successor reservation overlap SHALL NOT be confused with two simultaneous active same-key waits. The pilot SHALL exclude durable persistence, crash recovery, host shutdown, start-idempotency, full scheduler behavior, retention/eviction, and production payload processing. It SHALL NOT claim a durable acknowledgement guarantee or infer iteration identity from a same-key signal. Duplicate-delivery checks SHALL be confined to the same target and modeled lifetime while the relevant identity remains in history; the configured event bound SHALL fit the implementation's retention capacity.

#### Scenario: A successor uses the predecessor's correlation key

- **WHEN** a same-instance continuation registers a successor while its matched predecessor still owns a reservation
- **THEN** the model permits both reservation obligations while checking that only one occurrence is active and that predecessor cleanup preserves the successor's ownership

#### Scenario: The model configuration is used as evidence

- **WHEN** a reviewer inspects a completed run
- **THEN** its instance, occurrence, key, event, delivery, and failure bounds and history-lifetime restriction are explicit, and no result is represented as universal or durable verification

### Requirement: Safety properties cover ownership and competing outcomes

Every normal safety configuration SHALL check WOP-001 through WOP-007: well-formed state; at most one active occurrence per full key; reservation ownership and count consistency; at most one successful resolution per occurrence and no repeated consumption of the same modeled event per target; exact release; escaping-failure rollback consistency; and key/occurrence isolation. The crosswalk SHALL state each property's observation boundary and prohibit excluding intermediate states observable by competing actors. Safety checking SHALL NOT require fairness assumptions to remove violating prefixes.

The checks SHALL distinguish a failed physical continuation attempt from a successful logical resolution and distinguish an escaping exception from a business failure converted into terminal workflow state. Conflicting product requirements or source behavior SHALL be recorded as findings, not silently converted into model assumptions or changed product contracts.

#### Scenario: A competing registration arrives while a matched wait is unresolved

- **WHEN** another instance requests the same key before the first instance's outstanding reservation is settled and released
- **THEN** the model checks that the competing instance cannot acquire ownership or park a conflicting active wait

#### Scenario: Event and timeout contend

- **WHEN** event delivery and a due timer contend for the same occurrence
- **THEN** every explored schedule has at most one successful logical resolution, and the losing path cannot resolve or clean up a successor occurrence

#### Scenario: A continuation throws before successful consumption

- **WHEN** an escaping continuation failure occurs on a modeled reachable path
- **THEN** the model checks restoration of the prior unresolved wait, retained reservation, event eligibility, and relevant timer bookkeeping without recording successful consumption, including any successor state that the source path can create

#### Scenario: An already consumed event is redelivered

- **WHEN** the same modeled envelope is delivered again to the same instance while its identity remains retained
- **THEN** it cannot successfully consume another occurrence, while a new event identity for the same signal-stream key remains eligible for a later active occurrence

### Requirement: Progress claims state their environmental assumptions

The progress configuration SHALL check WOP-008 conditional settlement/release and WOP-009 conditional uncontested reuse separately from safety. Its assumptions SHALL include an alive process, finite external arrivals and injected failures, eventual return of admitted continuations, eventual processing of requested cancellation, and weak fairness of continuously enabled internal scheduling, settlement, and release. Any timer-progress claim SHALL additionally state eventual due-time and dispatch assumptions. The configuration SHALL NOT use symmetry reduction for liveness, promise an external event arrives, promise workflow completion, or claim fairness among perpetual competing registrants.

Terminal or intentionally quiescent states SHALL have explicit stuttering behavior without disabling detection of unexpected deadlocks. Assumption-removal controls SHALL demonstrate a blocked or infinite behavior when continuation-return or scheduling fairness is removed, and classify it as an expected limitation.

#### Scenario: An uncontested key becomes reusable

- **WHEN** all earlier obligations have settled and released, a registration remains eligible with no competing claimant or cancellation, and the declared progress assumptions hold
- **THEN** the model checks that registration eventually acquires the key

#### Scenario: User code never returns

- **WHEN** the continuation-return assumption is removed in a dedicated control
- **THEN** the evidence retains a behavior that prevents settlement and does not report unconditional progress or a runtime defect

### Requirement: Model qualification detects intended faults and reaches required scenarios

The pilot SHALL retain reachable witnesses for cross-instance contention, same-instance duplicate rejection, predecessor/successor handover, sequential reuse, duplicate delivery, both timer/event orders, cancellation before and during continuation, escaping failure/restoration, and full-key isolation. Successor-then-failure behavior SHALL have either a witness or a source-backed explanation of unreachability; an unsupported exclusion SHALL block conformance credit.

Dedicated faulty variants SHALL check premature release on match, removal of the same-instance active-wait guard, deletion of the successor reservation during predecessor release, consumption recorded before failed continuation without restoration, and dual event/timer resolution. Each variant SHALL violate its documented WOP property and retain a counterexample trace. Syntax errors, unrelated failures, unreachable mutation guards, or exhausted resource budgets SHALL NOT count as successful fault detection. Witness and mutation results SHALL remain separate from passing normal-model results.

#### Scenario: A premature-release variant is checked

- **WHEN** the variant drops ownership at match before continuation settlement and a competing instance attempts registration
- **THEN** qualification requires a reservation-ownership violation with a trace showing the faulty release; a checker crash or parse error does not satisfy qualification

#### Scenario: A required scenario is unreachable

- **WHEN** a witness search cannot reach the required handover or rollback state
- **THEN** the pilot investigates model guards and actual source reachability and withholds conformance credit until the discrepancy is explained

### Requirement: Verification is linked to actual source and executable tests

The pilot SHALL maintain a crosswalk from authoritative requirement anchors to WOP properties, model actions and observable projection, source symbols, synchronization boundaries, deterministic test schedules, and evidence. It SHALL cover the public structured and dynamic wait-registration paths as well as relevant lower-level mechanisms. Atomicity claims SHALL be justified by actual locks or instance-lane serialization; route lookup, snapshot reads, deduplication, and continuation settlement SHALL NOT be collapsed into a global atomic operation unless the source establishes that boundary.

Every actionable counterexample SHALL be classified as a contract contradiction, abstraction error, implementation defect, or unsupported assumption. Implementation defects SHALL have a minimized schedule and a failing deterministic C# regression on the affected revision before a separately scoped fix, followed by passing validation. Representative successful traces SHALL also be replayed as deterministic tests or mapped to existing tests whose actual assertions establish the trace outcomes. Tests SHALL use controlled scheduling/time and observable ownership or resolution assertions rather than timing luck. A passing TLC run SHALL NOT be reported as proof of C# correctness.

#### Scenario: A model assumes serialization across an await

- **WHEN** an action mapping relies on the instance lane remaining exclusive during continuation execution
- **THEN** the crosswalk identifies the owning source boundary and a deterministic schedule supporting that assumption, while retaining cross-instance interleaving

#### Scenario: A counterexample exposes a possible implementation defect

- **WHEN** a trace permits stale cleanup to remove a newer reservation
- **THEN** the trace is minimized and reproduced against the corresponding C# path before a defect is claimed, or the abstraction mismatch and its correction are recorded

#### Scenario: Existing tests appear to cover repeated waits

- **WHEN** the crosswalk credits a loop or sequential-wait test
- **THEN** it verifies whether the test reuses the same key or changes correlation and credits only the behavior actually asserted

### Requirement: Runs are reproducible and verdicts preserve their limits

Implementation SHALL provide a development-only CLI runner and dedicated bounded CI workflow using a pinned official TLC release with checksum verification and a compatible pinned Java runtime. Normal runs SHALL require completed exploration with every configured property passing. Each run SHALL record source revision and any dirty-diff hash, model/config/tool hashes, tool versions, commands, bounds, assumptions, state counts, elapsed time, memory/time limits, result classification, traces, and linked C# test results. Timeout, incomplete exploration, or missing code-mapping evidence SHALL be classified as inconclusive rather than passing. Bounds SHALL NOT be silently reduced.

The workflow SHALL run on relevant model, runner, workflow, requirement, source, and test changes and support manual dispatch. Tool dependencies SHALL NOT enter production packages. CI integration SHALL NOT itself change branch protection. A final verdict SHALL distinguish `passed within stated scope`, `failed`, and `inconclusive`, list unresolved findings, and recommend whether to extend the pilot. Qualification SHALL require all mandatory configurations, witnesses, fault-detection controls, progress-assumption controls, source mappings, and applicable regressions, with no unresolved in-scope semantic discrepancy. A useful failed experiment SHALL NOT be represented as qualified runtime behavior.

#### Scenario: A model run exceeds its budget

- **WHEN** TLC reaches the configured time or memory limit before completing required checks
- **THEN** the job records an inconclusive non-passing result and retains partial evidence without silently shrinking the model

#### Scenario: All pilot obligations are satisfied

- **WHEN** normal profiles complete, controls behave as expected, code/test mappings are supported, and no in-scope discrepancy remains
- **THEN** the verdict states only that the recorded finite model and selected implementation schedules passed within their documented scope, with durable follow-on work requiring a separate proposal
