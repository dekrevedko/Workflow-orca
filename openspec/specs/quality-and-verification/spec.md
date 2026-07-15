## Purpose

Define the acceptance, provider-invariant, and traceability expectations that protect OrcaCore from semantic regressions during rebuild and future development.
## Requirements
### Requirement: Acceptance scenarios remain executable across runtime modes
The project SHALL maintain acceptance scenarios that can be executed against ephemeral mode and durable mode, with provider-backed validation added where the capability requires external infrastructure.

#### Scenario: Feature is declared complete
- **WHEN** a runtime capability is considered implemented
- **THEN** the corresponding acceptance behavior can be exercised through executable tests in the supported runtime modes

### Requirement: Known workflow-engine failure patterns stay covered
The project SHALL prioritize acceptance coverage for previously observed workflow-engine failure patterns such as duplicate events, wait races, branch ordering, crash recovery, and versioning drift.

#### Scenario: Regression-prone behavior changes
- **WHEN** a contributor changes wait handling, persistence, routing, or lifecycle logic
- **THEN** tests covering the known regression patterns continue to guard the affected behavior

### Requirement: Provider invariants are explicit
The project SHALL verify that supported storage and messaging provider combinations preserve the same behavioral contract or explicitly declare unsupported capabilities instead of drifting silently.

#### Scenario: New provider adapter is introduced
- **WHEN** a new durable provider or dispatch adapter is added
- **THEN** invariant tests confirm parity for the supported feature set or document the unsupported behaviors explicitly

### Requirement: Documentation and tests remain traceable to requirements
Requirements, acceptance criteria, architecture notes, and executable tests SHALL stay traceable enough that contributors can map a runtime behavior from spec to docs to code validation.

#### Scenario: Contributor investigates a missing behavior
- **WHEN** a contributor needs to understand whether a behavior is intentional, planned, or regressed
- **THEN** they can follow the trace from specification to supporting docs and tests without relying on chat history alone

### Requirement: Developer-surface reconciliation gates implementation
Compiler, builder-fixture, and structured-driver source changes SHALL NOT begin until this change and `reshape-developer-facing-interfaces` reference one joint capability matrix, compiler/diagnostic contract, post-fiber builder signatures, portable dynamic-wait decision, durable `ForEach` two-layer rejection rule, and root-only quiescent continue-as-new rule, and both changes pass strict validation.

#### Scenario: Companion change signatures differ
- **WHEN** the companion change's compile fixtures or public baselines use pre-fiber builder, branch-result, or merge shapes
- **THEN** implementation remains gated until both artifacts use the post-fiber contract

### Requirement: Structured-fiber changes follow test-first verification
Each implementation slice that changes compilation, scheduling, scope lifecycle, merge, ownership, or replay SHALL begin with a failing executable test for the intended behavior or reproduced defect before production code is changed.

#### Scenario: Contributor implements a scope transition
- **WHEN** a new scope behavior or defect fix is started
- **THEN** the affected test suite first demonstrates the missing behavior and then passes after the implementation change

### Requirement: A reference model verifies schedule independence
The project SHALL maintain a deterministic reference model for fibers and scopes and SHALL compare runtime results against it across generated branch trees, completion permutations, yield schedules, duplicate deliveries, and crash points.

#### Scenario: Generated completion schedules are evaluated
- **WHEN** the same authored definition and branch results are executed under different seeded schedules
- **THEN** every run produces the same canonical merged state, one parent continuation, and no orphan owned obligations

### Requirement: Crash tests cover every scope commit edge
Durable verification SHALL inject restart or lost-response conditions after scope creation, after each branch result, before and after merge, during loser cleanup, and between scheduler turns.

#### Scenario: Crash occurs after final result but before merge
- **WHEN** the final required branch result commits and the host stops before merge commits
- **THEN** restart performs one merge from persisted results and does not rerun completed branch bodies

### Requirement: Provider suites round-trip real fiber envelopes
Provider certification and PostgreSQL and SQL Server integration suites SHALL persist, load, claim, replace-host, and resume real nested fiber/scope envelopes rather than validating only generic opaque runtime-state bytes.

#### Scenario: Provider host is replaced during nested execution
- **WHEN** one host stops with nested scopes containing runnable and blocked fibers and another host resumes the instance
- **THEN** the provider preserves exact scheduling, ownership, branch results, and continuation behavior

### Requirement: Runtime parity covers structured composition
Acceptance coverage SHALL execute equivalent structured-fiber definitions in ephemeral and durable modes and SHALL compare business outcome, merge order, lifecycle status, and residual cleanup.

#### Scenario: Nested WhenAll and WhenFirst run in both modes
- **WHEN** equivalent nested definitions receive the same branch completions and events
- **THEN** both modes produce the same observable result and leave no orphan waits, timers, children, jobs, or resources
