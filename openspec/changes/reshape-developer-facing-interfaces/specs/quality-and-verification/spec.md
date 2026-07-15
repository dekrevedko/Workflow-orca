## MODIFIED Requirements

### Requirement: Acceptance scenarios remain executable across runtime modes
The project SHALL maintain acceptance scenarios through the documented application Interfaces for ephemeral and durable modes, with provider-backed validation where a capability requires external infrastructure. Kernel-only tests SHALL NOT be used as evidence that a missing application entry point is complete.

#### Scenario: Feature is declared complete
- **WHEN** a runtime capability is considered implemented
- **THEN** its supported developer workflow can be exercised through a public application entry point in every declared mode

### Requirement: Provider invariants are explicit
The project SHALL verify that each supported storage, projection, and messaging Adapter preserves the contract for its declared role, follows the role's registration conventions, and explicitly declares unsupported capabilities instead of drifting silently.

#### Scenario: New provider Adapter is introduced
- **WHEN** a new durable store, projection cache, or dispatch Adapter is added
- **THEN** role-specific certification confirms behavioral parity, registration behavior, ownership semantics, and documented capability limits

### Requirement: Developer-surface reconciliation gates implementation
The completed and archived structured-fiber baseline SHALL supply the shared compiler/diagnostic contract, typed structured signatures, durable `ForEach` rejection, root-only quiescent continue-as-new behavior, and runtime ownership used by this public-interface refactor. Those delivered behaviors SHALL NOT remain an active prerequisite or be recreated by a second compiler or driver. Transient-governance source work SHALL additionally agree with `add-runtime-concurrency-limits` on the three-way taxonomy and mode-guaranteed discoverability.

#### Scenario: Public-interface implementation begins after rebase review
- **WHEN** this change passes strict validation and review approves mode-specific definitions, nested builder rules, tier placement, and application projections
- **THEN** authoring consolidation, package, facade, and management work may proceed while durable transient-pool exposure remains gated by its active change

## ADDED Requirements

### Requirement: Public surfaces are approved mechanically
Every application, provider-authoring, and runtime-protocol assembly SHALL have an approved public-type/signature baseline, and verification SHALL fail on unreviewed additions, removals, or types placed in the wrong Interface tier.

#### Scenario: Implementation type becomes public
- **WHEN** a contributor makes a hosted loop, checkpoint mapper, converter, test profile, or aggregate handler public
- **THEN** the public-surface guard fails until a supported external scenario and tier placement are reviewed

#### Scenario: Shared management declaration is duplicated
- **WHEN** repository and public-surface scans inspect `WorkflowInstanceQueryModel`, `WorkflowStatistics`, `WorkflowStatisticsGroup`, and the selected destructive confirmation contract
- **THEN** verification fails unless each canonical application type is declared exactly once and each superseded engine-local declaration is absent

### Requirement: Mode capability separation is compile-verified
Verification SHALL prove both the presence of supported methods and the absence of unsupported methods on ephemeral and durable authoring Interfaces.

#### Scenario: Ephemeral capability fixture compiles
- **WHEN** a consumer fixture authors an ephemeral definition
- **THEN** portable and ephemeral methods compile while `WaitLong`, children, external jobs, durable leases, and continue-as-new are unavailable

#### Scenario: Durable capability fixture compiles
- **WHEN** a consumer fixture authors a durable definition
- **THEN** `WaitLong` and implemented durable capabilities compile while ephemeral-only `ForEach` and unsupported definition-wide retry are unavailable

#### Scenario: Post-fiber signatures are guarded
- **WHEN** compile fixtures and approval baselines are generated
- **THEN** they use the archived typed result, merge, and structural-node shapes while keeping compiled-plan shapes out of the application baseline

#### Scenario: Nested capability fixture compiles
- **WHEN** positive and negative fixtures author branches inside ephemeral and durable structured scopes
- **THEN** no nested builder exposes a capability absent from its selected-mode root, and durable branches cannot discover ephemeral transient pools

#### Scenario: Definition type fixture compiles
- **WHEN** a consumer builds one ephemeral and one durable definition
- **THEN** each engine registration accepts only its matching mode-specific definition type without reading `RequiresDurableEngine` or public compiled-plan metadata

### Requirement: Samples assert successful application outcomes
Runnable samples SHALL use only the documented application Interface for their intended audience and SHALL assert unambiguous successful or expected suspended outcomes instead of printing kernel failure states as demonstration output.

#### Scenario: Durable external-job sample runs
- **WHEN** the durable example is executed in verification
- **THEN** it dispatches an external job from a live definition, reports completion through the application Interface, reaches the documented state, and fails if any operation returns or logs a poisoned outcome

### Requirement: Package-consumer smoke tests guard dependency experience
Verification SHALL build fresh consumer projects from package references or equivalent packed artifacts so transitive dependencies, namespaces, registration extensions, and advanced-seam leakage are tested from outside the repository source graph.

#### Scenario: Minimal consumer builds from packages
- **WHEN** the repository packs and restores its documented application packages into a clean consumer project
- **THEN** the golden path compiles and runs without direct references to internal source projects or advanced packages

### Requirement: Application projections exclude runtime routing internals
Verification SHALL prove that application definitions, instance snapshots, active-wait snapshots, and typed state operations do not expose compiled execution IR, fiber/scope identities, wait registration sequence, format-2 envelopes, or branch-private state.

#### Scenario: Projection baseline is inspected
- **WHEN** public signature and behavior tests inspect application definition and management models
- **THEN** definitions expose authored metadata, waits expose authored path/matching facts, and typed state returns only detached root business state

### Requirement: Canonical requirements are updated before source
Canonical `docs/specs/` requirements and acceptance criteria SHALL be updated before source implementation for every changed public contract, including definition retry removal, concurrency taxonomy, dynamic waits, structural durable effects, registration, continuation, external-job failure, routing results, management time ownership, remediation, and package tiers.

#### Scenario: Final traceability is checked
- **WHEN** implementation verification completes
- **THEN** every changed canonical requirement links to its public acceptance, compile, consumer, architecture, or provider-certification evidence

### Requirement: Split-host continuation is acceptance-tested
Verification SHALL include a two-host durable acceptance fixture in which the callback host has no registered definitions and the definition-owning host progresses committed work from the continuation handoff.

#### Scenario: Definition-less callback reports job failure
- **WHEN** the callback host reports a worker failure and returns `AppliedPendingContinuation`
- **THEN** the definition-owning host pump consumes the continuation and progresses the authored failure policy exactly once
