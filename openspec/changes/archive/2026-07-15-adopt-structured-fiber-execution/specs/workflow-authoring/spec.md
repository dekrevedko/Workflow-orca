## ADDED Requirements

The normative selected-mode matrix, exact post-fiber authoring signatures, and shared
compiler/diagnostic contract are defined in
[`docs/specs/17-selected-mode-capability-matrix.md`](../../../../../docs/specs/17-selected-mode-capability-matrix.md).

### Requirement: Mode-first builders share one compiled plan contract
Ephemeral and durable authoring SHALL use the mode-first builders defined by `reshape-developer-facing-interfaces`, one internal authored graph, and one `DefinitionCompiler`. `Build()` SHALL return a compiled-plan-backed definition, and `TryBuild()` SHALL return aggregate diagnostics through `Validation<TDefinition>`.

#### Scenario: Durable definition is built
- **WHEN** an author calls `Build()` on the durable builder
- **THEN** the returned definition contains the same compiled-plan and diagnostic contract consumed by durable registration and structured execution

#### Scenario: Definition has multiple graph errors
- **WHEN** an author calls `TryBuild()` on an invalid mode-specific builder
- **THEN** the result contains all discoverable graph diagnostics without publishing a definition

### Requirement: Durable ForEach has two-layer rejection
Durable public authoring SHALL NOT expose `ForEach`, and the shared compiler SHALL reject a manually constructed durable `ForEach` graph with the selected-mode capability diagnostic.

#### Scenario: Durable author uses normal discovery
- **WHEN** a developer inspects the durable builder
- **THEN** `ForEach` is absent from the public fluent surface

#### Scenario: Durable graph bypasses the builder
- **WHEN** an internal or custom graph contains a durable `ForEach` node
- **THEN** compilation rejects it before registration

### Requirement: ContinueAsNew is authored structurally
Continue-as-new SHALL be available only as a durable structural builder node and SHALL NOT be returnable from portable `StepResult`. Its compiled instruction SHALL retain the root-only quiescence rules of this change.

#### Scenario: Portable step attempts rollover
- **WHEN** a portable step implementation is authored
- **THEN** its result contract contains no continue-as-new variant

### Requirement: Successful workflow flow has one root entry and exit
An authored workflow SHALL contain exactly one root `Init` and exactly one root `End`. Every reachable successful root path SHALL converge on that `End`. The root `End` SHALL support either a static named outcome or a deterministic named-outcome selector over final typed state so CR-008 metadata can represent multiple business outcomes without multiple structural exits. Failure, cancellation, termination, and poison transitions SHALL terminate through runtime lifecycle rules and SHALL NOT be required to execute `End`.

#### Scenario: Workflow has multiple business outcomes
- **WHEN** alternative paths produce accepted, rejected, or manual-review outcomes
- **THEN** those paths set typed outcome data, converge on the one root `End`, and its deterministic selector records the corresponding named outcome in completion metadata

#### Scenario: Root terminal is missing or misplaced
- **WHEN** a definition has no root `End`, more than one root `End`, a nested `Init`, or executable nodes after root `End`
- **THEN** validation reports every detected structural error and does not produce a compiled plan

### Requirement: Every branch has one branch return
Each authored local branch SHALL have one entry and one reachable branch return of the scope's declared result type. Branches SHALL NOT contain workflow `Init`, workflow `End`, or `ContinueAsNew`.

#### Scenario: Branch finishes successfully
- **WHEN** branch execution reaches its branch return
- **THEN** the branch produces its declared serializable result and transitions to completed without terminating the workflow

#### Scenario: Branch contains workflow-global control
- **WHEN** a branch contains `Init`, `End`, or `ContinueAsNew`
- **THEN** definition validation rejects the branch before registration

### Requirement: Validation covers complete structured reachability
Definition validation SHALL verify reachable termination, scope nesting, branch identity, result and merge type compatibility, serializer availability, configured depth and active-fiber limits, and absence of orphan or cross-scope merge references.

#### Scenario: Definition contains multiple invalid scope shapes
- **WHEN** a definition contains several structural, typing, ownership, or limit violations
- **THEN** validation accumulates actionable diagnostics for all discoverable violations in one result

### Requirement: Fluent blocks do not require authored closing nodes
The fluent authoring Interface SHALL express `If`, loops, and branch scopes through structured nested builders and SHALL NOT require manually balanced closing nodes such as `EndIf`, `EndWhile`, or `EndParallel`. Root `End` and `BranchReturn` remain explicit because they have lifecycle and result semantics.

#### Scenario: Author composes an If followed by another step
- **WHEN** an author completes the nested then and else builders
- **THEN** the next fluent operation continues after the conditional without an authored `EndIf`

#### Scenario: Nested block is malformed
- **WHEN** nested builder content has unreachable flow or an invalid terminal
- **THEN** structural validation reports the definition error without relying on matching opening and closing tokens

### Requirement: ForEach authoring uses the structured scope contract
Ephemeral `ForEach` authoring SHALL declare an item selector, deterministic partitioner, item-private state/input contract, body, join policy, failure policy, optional positive `maxConcurrency`, and optional deterministic merge over ordered item outcomes. `WhenAny` SHALL use cancel-remaining semantics and SHALL accept only `FailFast`; `LetRemainingComplete`, `WhenAny + WaitAllThenFail`, and `WhenAny + ContinueWithPartialFailures` SHALL NOT be accepted. Durable compilation SHALL reject `ForEach` explicitly.

#### Scenario: Resultless ForEach is authored
- **WHEN** an author omits a business merge
- **THEN** item outcomes remain inspectable runtime status, parent business state is unchanged by item fibers, and the parent resumes according to join/failure policy

#### Scenario: Resultful ForEach is authored
- **WHEN** an author declares a common item result and merge
- **THEN** the merge receives `ForEachItemOutcome<TResult>` values in stable item-index order

#### Scenario: Detached ForEach residual is requested
- **WHEN** an author requests `WhenAny` with `LetRemainingComplete`
- **THEN** validation rejects the definition before compilation

#### Scenario: Incoherent ForEach WhenAny failure policy is requested
- **WHEN** an author combines `WhenAny` with `WaitAllThenFail` or `ContinueWithPartialFailures`
- **THEN** validation rejects the definition with a stable unsupported-policy-combination diagnostic

## MODIFIED Requirements

### Requirement: Parallel authoring defines deterministic join structure
Parallel workflow authoring SHALL declare branches, one common serializable result type, an explicit join policy, and the merge that produces the deterministic continuation. `WhenAll` SHALL declare a merge over all results, and `WhenFirst` SHALL declare a winner merge. Initial structured-fiber execution SHALL cancel all non-winning `WhenFirst` branches; ignore and let-remaining-complete policies SHALL be rejected until a separate detached-scope contract is approved.

#### Scenario: Parallel workflow is composed
- **WHEN** a contributor adds local parallel branches to a definition
- **THEN** the resulting definition captures branch inputs, branch returns, join policy, typed merge, and deterministic continuation after the scope exit

#### Scenario: Unsupported residual policy is authored
- **WHEN** a contributor selects a `WhenFirst` residual policy that can leave local work running after parent continuation
- **THEN** validation rejects the definition with an explicit unsupported-policy diagnostic
