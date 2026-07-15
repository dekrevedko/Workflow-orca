## MODIFIED Requirements

The normative selected-mode matrix, exact post-fiber authoring signatures, and shared
compiler/diagnostic contract are defined in
[`docs/specs/17-selected-mode-capability-matrix.md`](../../../../../docs/specs/17-selected-mode-capability-matrix.md).

### Requirement: Regular workflows are authored fluently
The system SHALL provide explicit ephemeral and durable fluent workflow-authoring entry points for regular workflows. Each entry point SHALL expose portable infrastructure nodes such as `Init`, `End`, `If`, `While`, supported structured composition, and `Wait` alongside user-defined business steps, plus only the capabilities guaranteed by every supported host for the selected execution mode. Later host composition SHALL NOT change which methods are discoverable on the statically selected builder.

#### Scenario: Ephemeral workflow is defined
- **WHEN** a contributor selects ephemeral workflow authoring
- **THEN** they can compose portable control flow, business steps, and implemented ephemeral capabilities without seeing durable-only methods

#### Scenario: Durable workflow is defined
- **WHEN** a contributor selects durable workflow authoring
- **THEN** they can compose portable control flow, business steps, and implemented durable capabilities without seeing ephemeral-only methods

### Requirement: Durable workflows have a separate authoring surface
The system SHALL provide distinct public ephemeral and durable authoring surfaces and immutable `EphemeralWorkflowDefinition<TState>` and `DurableWorkflowDefinition<TState>` application definition types (or equivalently explicit names). `WaitLong`, child workflows, external jobs, durable resource leases, continue-as-new, durable DAG/saga execution, and definition versioning SHALL be modeled explicitly and validated before execution. Normal engine registration SHALL accept only the matching definition family and SHALL NOT infer mode from `RequiresDurableEngine` or a public compiled plan.

#### Scenario: Definition mode is selected
- **WHEN** an ephemeral or durable workflow is built successfully
- **THEN** its static definition type records the selected mode and cannot be registered through the other engine's normal registration contract

#### Scenario: Durable-only wait is authored
- **WHEN** a contributor needs a cold durable wait
- **THEN** `WaitLong` is publicly available from the durable builder and absent from ephemeral authoring

#### Scenario: Ephemeral-only fanout is attempted in durable mode
- **WHEN** a contributor authors a durable definition
- **THEN** the ephemeral in-instance `ForEach` capability is absent unless durable semantics for that capability have been separately specified and implemented

#### Scenario: Durable ForEach node is constructed manually
- **WHEN** a custom or test graph bypasses the public builder and contains an in-instance `ForEach` node in durable mode
- **THEN** the shared compiler rejects it with the same stable capability diagnostic as other unsupported durable nodes

### Requirement: Built definitions are immutable
Workflow, saga, and DAG builders SHALL produce immutable definitions whose public metadata collections cannot be downcast and mutated and whose executable factories are not exposed as mutable application metadata.

#### Scenario: Definition is registered
- **WHEN** a completed definition is built and registered with an engine
- **THEN** later caller mutation cannot change its authored graph, executable factories, policies, or compensation metadata

## ADDED Requirements

### Requirement: Every authored capability has selected-mode semantics
Every public builder method SHALL have implemented semantics for the mode that exposes it and SHALL NOT be silently ignored or deferred to a predictable runtime failure.

#### Scenario: Unsupported definition retry is considered
- **WHEN** no selected engine implements definition-wide retry semantics
- **THEN** no public builder exposes `WithDefinitionRetry`

#### Scenario: Execution throttle is configured
- **WHEN** a host applies a per-step execution throttle to all steps or stable authored categories
- **THEN** the policy is enforced around one business-step body and is not represented as a durable resource lease or a host-dependent builder method

### Requirement: Selected mode is preserved through nested authoring
Every public structured scope and branch builder SHALL retain the root workflow's selected mode in its static type while sharing implementation internally. A capability absent at the root SHALL remain absent inside `Parallel`, `WhenFirst`, loops, and future nested scopes.

#### Scenario: Durable branch authoring is inspected
- **WHEN** a developer authors a branch inside a durable `Parallel`
- **THEN** ephemeral-only transient-pool and `ForEach` methods are absent rather than discoverable and rejected later by the compiler

### Requirement: Application definitions hide executable compiler IR
Application workflow definitions SHALL expose immutable identity, version, mode, fingerprint, and authored metadata without exposing compiled instructions, scopes, policies, executable delegates, or the executable plan as public application properties. Engines SHALL obtain the retained plan through implementation-only access.

#### Scenario: Application definition surface is inspected
- **WHEN** the public application baseline inspects both mode-specific definition types
- **THEN** neither definition exposes `CompiledWorkflowPlan`, `CompiledInstruction`, compiler identity types, or mutable executable metadata

### Requirement: Authoring configuration uses application vocabulary
Normal mode-first builders SHALL accept domain-facing `WorkflowAuthoringOptions` containing `MaxStructuredDepth`, `MaxActiveExecutionPaths`, `MaxBranchResultPayloadBytes`, payload serializer registration, state copier registration, and deterministic fingerprint contributors. Scheduling-turn and checkpoint-payload limits SHALL remain engine/hosting configuration. Builders SHALL NOT expose compiler-shaped `WithCompilerOptions` or `WithTypeSerializerRegistry` methods or require application code to reference compiled-plan types.

#### Scenario: Custom payload serializer is registered
- **WHEN** an application needs a supported serializer for branch or state payloads
- **THEN** it configures the application authoring options without naming compiler implementation contracts

### Requirement: Builder validation has one consistent completion model
Workflow, saga, and DAG builders SHALL expose `Build()` as the throwing common path and `TryBuild()` returning the existing `Validation<TDefinition>` as the non-throwing aggregate-diagnostics path. Fluent methods SHALL reject invalid local arguments immediately, while graph-wide structural diagnostics SHALL be aggregated at build time.

#### Scenario: Local duration is invalid
- **WHEN** an author supplies a non-positive delay or timeout to a fluent method
- **THEN** that method rejects the argument at the call site with a stable diagnostic

#### Scenario: Graph has multiple structural errors
- **WHEN** a definition is missing required terminals and contains duplicate or unreachable structure
- **THEN** the non-throwing build reports all graph-wide diagnostics in one result

### Requirement: Concurrency authoring uses a three-way taxonomy
Authoring names and contracts SHALL distinguish per-step execution throttles, named cross-instance transient pools, and persisted durable resource leases. A builder capability SHALL appear only when every supported host for that selected mode implements its declared lifetime and recovery semantics; host configuration SHALL NOT add builder methods after mode selection.

#### Scenario: Developer selects a pool feature
- **WHEN** a developer inspects per-step throttle, transient-pool, and durable-lease methods
- **THEN** the Interface, documentation, and result contracts distinguish step-local lifetime, host-local cross-instance lifetime, and persisted cross-host scope lifetime

#### Scenario: Durable host lacks transient-pool enforcement
- **WHEN** a durable host does not implement named cross-instance transient pools
- **THEN** durable authoring does not expose the transient-pool method in any host profile and does not silently ignore its metadata

### Requirement: Continue-as-new is a root structural transition
Continue-as-new SHALL be authored only as a durable structural node and SHALL execute only as a root-fiber transition after child fibers and scope-owned obligations are quiescent. It SHALL NOT be returnable from portable `StepResult`.

#### Scenario: Nested fiber attempts continue-as-new
- **WHEN** a continue-as-new node is placed in a nested or non-quiescent fiber scope
- **THEN** the shared compiler or driver rejects the transition with a stable structural diagnostic
