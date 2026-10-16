## MODIFIED Requirements

### Requirement: Acceptance scenarios remain executable across runtime modes
The project SHALL maintain application-interface acceptance scenarios for every declared ephemeral and durable capability, with provider-backed validation where persistence or external infrastructure is required. Kernel-only tests SHALL NOT count as evidence that a missing public path is complete.

#### Scenario: Feature is declared complete
- **WHEN** a first-release capability is considered implemented
- **THEN** its complete developer journey executes through approved public application entry points in every supported mode

### Requirement: Known workflow-engine failure patterns stay covered
The project SHALL cover duplicate/conflicting events, ambiguous wait-pair rejection, pending-inbox ordered reuse, event/wait/timer races, cold activation, start-or-deliver, committed-snapshot fanout, outbox dispatch ambiguity, branch/item ordering, nonempty root-only `Parallel`, once-only/suppressed merges, selector replay, crash recovery and same-attempt redispatch, competing drivers, structural fingerprint drift plus required opaque-code version bumps, detached attempt state, step-timeout late overlap, workflow deadlines, host/definition compatibility, unknown-pool rejection, lease ambiguity/quarantine, grant/cancel and confirmation precedence, creation-versus-current pool capacity, resource-governance conflicts, and DAG child reattachment.

#### Scenario: Regression-prone behavior changes
- **WHEN** a contributor changes wait, join, fan-out, timeout, persistence, routing, lease, DAG, or lifecycle logic
- **THEN** targeted regressions and adjacent schedule/crash cases continue to guard the affected behavior

### Requirement: Provider invariants are explicit
The project SHALL verify that each supported storage, projection, and messaging adapter preserves its declared contract, exact ordinal strong-value equality, certified `orcacore-json-v1` payload detachment, structural fingerprints, complete structured envelopes, step-operation coordinates, deadline state, and one expected-version serialized resource-governance aggregate, or explicitly declares unsupported capabilities. Every shipped production durable provider SHALL pass the complete shared provider-certification suite plus provider-native restart, competing-host, migration-journal, schema, retention, maintenance, poison, and pressure tests against real storage. Governance certification SHALL exercise per-record format/checksum/payload-copy validation, whole-stream defensive copy and exact `1..Version` continuity through `ResourceGovernanceStream.Create`, and non-empty `expectedVersion + 1..N` whole-batch append with no partial commit.

Public-surface verification SHALL also prove that every caller-created string-backed strong value exposes only its private-constructor/`Create(string)` construction path and that runtime-created identities expose no public constructor or `Create` factory. Reflection, source, and clean-consumer guards SHALL reject the superseded `new EventName(...)`-style construction and any mixed family that offers both constructor and factory aliases.

#### Scenario: New provider adapter is introduced
- **WHEN** a new durable store, projection, or dispatcher is added
- **THEN** role-specific certification proves behavioral parity and documented capability limits without relying on provider-default collation or truncation

#### Scenario: Governance provider returns a malformed stream
- **WHEN** certification injects a negative/mismatched version, null entry, gap, duplicate, reordering, unsupported format, bad checksum, or mutation of the source list/payload after construction
- **THEN** load fails before replay or the accepted immutable value remains unchanged, and no aggregate transition is applied

### Requirement: Developer-surface reconciliation gates implementation
The authoritative selected-mode matrix, exact declaration companion `docs/specs/17-public-authoring-contract.cs`, and this change SHALL agree on every concrete workflow signature, the four exact non-nullable `End` overloads, staged typed workflows, nonempty root-only fixed `Parallel`, bounded root-only `ForEach`, nested fan-out absence, success/failure-only joins, one cold-capable `Wait`, scoped-only leasing, deadlines, `StepOperationId` plus attempt-ordinal replay, complete typed DAG/facade contracts, host compatibility, role-specific hosting, package separation, and deferred/removed absence. Every task 3.1 through 3.12, including 3.11a through 3.11d, SHALL be required and independently approved before task 4.0 or product source work proceeds.

#### Scenario: Guard packet describes a superseded contract
- **WHEN** a compile/reflection/behavior fixture still expects `WaitLong`, `Yield`, public `RunExternalJob`, Saga, `WhenFirst`, public children, nested `Parallel`, durable root `ForEach` absence, point acquisition, author TTL, renewal, or time-only reclaim
- **THEN** Phase 0 remains open and the fixture is replaced rather than making product source conform to it

### Requirement: A reference model verifies schedule independence
The project SHALL maintain deterministic reference-model coverage for fixed branches and bounded items across completion permutations, internal turn schedules, duplicate deliveries, replay points, and crash points.

#### Scenario: Generated completion schedules are evaluated
- **WHEN** the same definition and branch/item results execute under different seeded schedules
- **THEN** every run produces the same ordered results, single merge, parent state, and exact residual obligations

### Requirement: Crash tests cover every scope commit edge
Durable verification SHALL inject restart or lost-response conditions after scope creation, item snapshot commit, each branch/item result, final terminal result, before/after merge, before first step dispatch, after external effect with lost response, before attempt transition commit, after step-attempt deadline, after operation identity/attempt-ordinal allocation, lease request commit, grant, quarantine, release, and DAG node child start. Attempt crashes SHALL prove redispatch reuses operation ID, attempt ordinal, and absolute deadline without consuming policy budget. Lease handoff crash coverage SHALL use only the deterministic friend certification seam at the four post-commit barriers `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`, `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`; sleep/timing races or provisional command/fact APIs SHALL NOT satisfy task 3.11d.

#### Scenario: Crash occurs after final result but before merge
- **WHEN** the final required result commits and the host stops before merge commits
- **THEN** restart performs one merge from persisted ordered inputs without rerunning completed bodies

#### Scenario: Host is stopped at each lease handoff barrier
- **WHEN** the friend gate signals and blocks after each named durable commit and certification stops the host before the next protocol command
- **THEN** restart completes or compensates the same occurrence without a ghost/double grant and repeated barrier facts remain equal

### Requirement: Provider suites round-trip real fiber envelopes
Provider certification and relational integration suites SHALL persist, load, claim, replace-host, and resume real nested branch/item envelopes including committed bounded item snapshots, typed pending results, workflow/step deadlines, operation occurrence coordinates, and exact durable lease obligations.

#### Scenario: Provider host is replaced during nested execution
- **WHEN** one host stops with runnable/parked nested scopes and another host resumes
- **THEN** the provider preserves scheduling, identities, results, deadlines, ownership, and continuation behavior

### Requirement: Runtime parity covers structured composition
Acceptance coverage SHALL execute equivalent portable definitions in both modes and compare business output, fixed outcome metadata, authored/index result order, merge behavior, lifecycle state, timeout decisions, and residual cleanup. Durable-only recovery details MAY differ without changing authored meaning.

#### Scenario: Root Parallel and bounded root ForEach run in both modes
- **WHEN** equivalent definitions receive the same branch/item results and events
- **THEN** both modes produce the same typed parent output and leave no orphan waits, timers, or obligations

## ADDED Requirements

### Requirement: Every code and test removal has complete burden-of-proof evidence
The project SHALL maintain an exhaustive reviewed ledger for every physically deleted,
project-orphaned, or compile-excluded production file/symbol family and every retired test
declaration. Each production entry SHALL be classified exactly as removed, replaced/relocated,
deferred, or dead/duplicate and SHALL cite its normative requirement, current owner or future task,
recovery location, and executable evidence. Removing a prohibited application facade SHALL NOT
remove still-required runtime, provider, host/operator, persistence, observability, retention, or
cleanup behavior. Compile failure after an API reshape, `<Compile Remove>`, solution exclusion,
aggregate passing counts, and the existence of a successor method SHALL NOT satisfy this
requirement.

#### Scenario: Obsolete application facade carried an operator capability
- **WHEN** broad application statistics, query, archive, purge, hosting, or observer members are removed
- **THEN** verification proves their required application signatures are absent while every retained provider/operator projection, BCL diagnostic, retention, cleanup, and restart behavior executes through its approved owner

#### Scenario: Retired declaration names a successor
- **WHEN** a retired `[Fact]` or `[Theory]` is mapped to an active test or scenario
- **THEN** the crosswalk proves equivalent setup, invoked boundary, failure or crash schedule, persistence/restart point, and assertions rather than merely resolving the successor coordinate

#### Scenario: Provider project or migration is removed
- **WHEN** a provider project, adapter, migration, or certification binding leaves the active build
- **THEN** its ship/defer/replace/remove disposition agrees with the package manifest and tasks, a greenfield first-create schema needs no compatibility DDL, and no still-required provider behavior or test obligation becomes orphaned

#### Scenario: Section exit target is frozen
- **WHEN** Section 7 is proposed for independent approval
- **THEN** all packaged assemblies have reviewed current-build and fresh-package API baselines, the package feed is rebuilt from the frozen source, the deletion ledger has zero unresolved entries, and no reflection bridge substitutes for the approved typed package boundary

### Requirement: Public surfaces are approved mechanically
Each of the exact 12 packaged assemblies SHALL have one approved deterministic public baseline
covering every externally visible type, constructor, method, property, field, event, generic
arity/constraint, modifier, and signature. Verification SHALL compare the current build and fresh
packages, reject missing or extra inventory, and fail on unreviewed additions, removals,
placeholder symbols, wrong-tier placement, or aliases for superseded APIs. Qualified metadata and
source guards SHALL additionally reject non-public placeholders for removed concepts.

#### Scenario: Implementation type becomes public
- **WHEN** a contributor publishes a hosted loop, checkpoint mapper, converter, compiler type, test profile, or integration-specific DTO
- **THEN** the baseline fails until a supported external scenario and correct tier are approved

### Requirement: Selected-mode capability separation is compile verified
Consumer fixtures SHALL prove presence of the complete approved root/nested surface and absence of unsupported, deferred, removed, and wrong-mode members.

#### Scenario: Ephemeral fixture compiles
- **WHEN** a consumer authors staged typed ephemeral workflows
- **THEN** ephemeral lambdas, nested `If`, root `Parallel`, bounded root `ForEach`, `WhenAll*`, descriptor-based `Wait`, delays, and timeouts compile while durable ingress/publish, nested fan-out, durable leasing/continue-as-new, and every deferred/removed member are unavailable

#### Scenario: Durable fixture compiles
- **WHEN** a consumer authors staged typed durable workflows
- **THEN** named steps, nested `If`, root `Parallel`, bounded root `ForEach`, `WhenAll*`, descriptor-based cold-capable `Wait`, durable `Publish`, deadlines, scoped leasing, and root continue-as-new compile while lambdas, transient pools, nested `Parallel`/`ForEach`/`While`, and deferred/removed members are unavailable

#### Scenario: Lease nested fixture compiles
- **WHEN** durable root, conditional, branch, and item bodies author one lexical acquisition with no active ancestor
- **THEN** acquisition compiles, its dedicated leased builder omits all fan-out, nested acquisition, and continue-as-new, sequential root-loop scopes compile, and hand-built ancestry conflicts report exact `SFE-AUTH-LEASE-001/003` locations

### Requirement: Typed workflow contract is compile and behavior verified
Fixtures SHALL prove staged `Init`/body/`End`, exactly four completion overloads per mode with eager null rejection, all four resultful/resultless definitions and references, fixed outcome metadata, output atomicity, builder stage restrictions, definition fingerprint conflict, mode-specific application-configuration `AddWorkflow`, exact-reference `GetRequiredHandle`, absent/stale-reference failure, atomic staged-batch bootstrap before progression, and the common registry's deterministic mode/static-pool/dispatcher/fingerprint compatibility ordering with no mutation on failure.

#### Scenario: Resultful definition completes
- **WHEN** a typed input is initialized, processed, and projected by `End<TOutput>`
- **THEN** one atomic terminal commit makes typed output and optional fixed outcome available through the typed handle

#### Scenario: Completion stage is inspected
- **WHEN** a consumer reaches the completion builder
- **THEN** only `Build` and `TryBuild` remain available

#### Scenario: Completion overload baseline is inspected
- **WHEN** guards inspect each ephemeral and durable root builder
- **THEN** only the two resultless and two resultful `End` overloads exist, and explicit null selector/outcome calls throw `ArgumentNullException`

#### Scenario: Application configuration is repeated on a replacement host
- **WHEN** a replacement host stages the same exact definition batch in a different module-call order
- **THEN** preflight installs the same catalog before any pump starts, exact duplicates are idempotent, and cold exact-reference lookup succeeds

### Requirement: Diagnostic codes and authored locations are a complete stable contract
Phase 0 guards SHALL baseline every emitted build/runtime code below, reject an undocumented code or one code assigned to more than one meaning, and prove `Build` exception diagnostics and `TryBuild` diagnostics have identical codes, severity, primary/related locations, and canonical order where both paths are reachable. Message text is explanatory and noncontractual.

The complete workflow compiler/runtime catalog is:

- `SFE-AUTH-ROOT-001` `MissingRootInit`;
- `SFE-AUTH-ROOT-002` `MultipleRootInit`;
- `SFE-AUTH-ROOT-003` `MissingRootTerminal`;
- `SFE-AUTH-ROOT-004` `MultipleRootTerminal`;
- `SFE-AUTH-PATH-001` `IncompleteSuccessfulPath`;
- `SFE-AUTH-CAP-001` `CapabilityNotAvailable`;
- `SFE-AUTH-BRANCH-001` `DuplicateBranchIdentity`;
- `SFE-AUTH-BRANCH-002` `MissingBranchReturn`;
- `SFE-AUTH-BRANCH-003` `MultipleBranchReturn`;
- `SFE-AUTH-BRANCH-004` `EmptyParallelScope`;
- `SFE-AUTH-JOIN-001` `MissingJoin`;
- `SFE-AUTH-JOIN-002` `InvalidMergeContract`;
- `SFE-AUTH-DECORATOR-001` `MisplacedDecorator`;
- `SFE-AUTH-DEADLINE-001` `DuplicateWorkflowDeadline`;
- `SFE-AUTH-LIFECYCLE-001` `SupersededBuilderHandle`;
- `SFE-AUTH-LIFECYCLE-002` `JoinAlreadySelected`;
- `SFE-AUTH-LIFECYCLE-003` `FrozenAuthoringSession`;
- `SFE-AUTH-LIFECYCLE-004` `ExpiredLexicalBuilderHandle`;
- `SFE-AUTH-LIFECYCLE-005` `ConcurrentAuthoringConflict`;
- `SFE-AUTH-LOOP-001` `NonProgressingLoop`;
- `SFE-AUTH-LEASE-001` `LeaseAncestryConflict`;
- `SFE-AUTH-LEASE-003` `LeaseBlocksContinueAsNew`;
- `SFE-TYPE-001` `IncompatibleStateOrResultType`;
- `SFE-TYPE-002` `CodecUnsupportedShape`;
- `SFE-LIMIT-001` `InvalidForEachLimit`;
- `SFE-RUN-001` `NonQuiescentContinueAsNew`; and
- `SFE-RUN-002` `LeaseAncestryViolation`.

The complete DAG build catalog is `DAG-AUTH-NODE-001` `DuplicateNodeIdentity`; `DAG-AUTH-DEPENDENCY-001` `DuplicateDependency`; `DAG-AUTH-DEPENDENCY-002` `SelfDependency`; `DAG-AUTH-DEPENDENCY-003` `Cycle`; `DAG-AUTH-DEPENDENCY-004` `ForeignPlanReference`; `DAG-AUTH-MAP-001` `MissingMapInput`; and `DAG-AUTH-MAP-002` `DuplicateMapInput`. Opaque mapper violations use only the runtime node failure code `DAG_INPUT_MAPPING_INVALID`, never a fabricated build diagnostic.

`AuthoredLocation.Value` SHALL use the exact grammar `workflow:$` or `dag:$` followed by zero or more slash-delimited tokens from `n:dddddddd`, `if:true`, `if:false`, `while:body`, `parallel:dddddddd`, `foreach:body`, `lease:body`, and `dag-node:dddddddd`. Every ordinal is zero-based and formatted as exactly eight invariant-culture decimal digits. Values SHALL contain no localized or free-form message text. A synthetic root diagnostic uses the applicable bare `workflow:$` or `dag:$`; all primary/related location lists SHALL be de-duplicated and sorted by `AuthoredLocation.Value` using ordinal string comparison. Stable public exception/failure codes SHALL additionally match the authoritative document-17 exception catalog.

#### Scenario: Every diagnostic fixture is enumerated
- **WHEN** Phase 0 generates one minimal failing workflow/DAG fixture for each applicable catalog entry and runtime defense
- **THEN** every expected code is emitted with the exact meaning/location grammar, no undocumented or duplicate-meaning code appears, and `Build`/`TryBuild` parity holds

#### Scenario: Nested diagnostic locations are produced
- **WHEN** diagnostics identify nested conditional, loop, parallel, item, lease, or DAG-node paths
- **THEN** primary and related locations use only canonical grammar tokens, zero-based eight-digit ordinals, invariant ordering, and no message-derived text

#### Scenario: Decorator is misplaced eagerly
- **WHEN** retry, timeout, or transient-pool decoration has no valid immediately preceding business step or is duplicated for that step
- **THEN** the fluent call throws the catalogued `SFE-AUTH-DECORATOR-001` failure at its canonical authored location rather than inventing a compile-time C# error or undocumented code

#### Scenario: Workflow deadline is repeated eagerly
- **WHEN** a second `CompleteWithin` call is authored
- **THEN** it throws `SFE-AUTH-DEADLINE-001` with the second call primary, first call related, and the first deadline unchanged

### Requirement: Authoring lifecycle, fingerprint coverage, and failure provenance are executable
Verification SHALL prove the `Open`/`JoinPending`/`Frozen` session lifecycle, successor-epoch root
façades, expired callback handles, one atomic winner for concurrent authoring, unchanged graph for
each rejected lifecycle operation, root-terminal frozen snapshots, and repeated-build structural
stability. It SHALL prove the structural fingerprint includes exactly inspectable authored
structure plus codec format and excludes compiler format, mode, definition identity/version, every
compiler option, opaque code, and author contributors. It SHALL also prove failure provenance
attaches at failure creation, root/branch/item occurrence constructors are runtime-only, one-failure
propagation is unchanged, multiple causes retain ordered individual provenance, and the closed
`root`/`branch`/`item` discriminator allowlist round-trips through `orcacore-json-v1`.

#### Scenario: Completion builder observes a frozen snapshot
- **WHEN** a stale façade attempts mutation after root terminal selection and the completion builder is invoked repeatedly
- **THEN** the mutation receives its lifecycle code, the graph is unchanged, and every build has equal structure, ordered diagnostics, and fingerprint

#### Scenario: Non-authored fingerprint input changes
- **WHEN** compiler format, workflow mode, definition identity/version, or one compiler option changes without changing authored structure or codec format
- **THEN** the structural-fingerprint guard requires equality and verifies compatibility through the separately owned binding

#### Scenario: One failed item is projected
- **WHEN** one `ForEach.WhenAll` item failure is detached and round-tripped
- **THEN** its authored location and item index remain intact without a synthesized join aggregate

### Requirement: Join and bounded fan-out semantics are executable
Verification SHALL cover a nonempty authored-order root-`Parallel` with `SFE-AUTH-BRANCH-004` `Build`/`TryBuild` parity for an empty scope, every fixed branch fiber existing at scope start, fair authored-order path-token scheduling with no live-fiber admission resource, index-order root-`ForEach` item results, `WhenAll` success/failure gating and ordered `SFE-JOIN-FAILED` causes, `WhenAllOutcomes` success/failure-only aggregation, ancestor terminal merge suppression, no automatic sibling cancellation, one parent-state replacement, valid empty-list merge, item-bound and encoded-value rejection before admission, selector commit/replay, sequential root fan-out stages with barriers, documented tagged-item flattening, and compile/reflection/compiler-defense proof that nested, branch, item, and leased builders expose no `Parallel`. Root `ForEach` guards SHALL prove effective admission is the lower of host and node-local limits; parked items release their runnable path token but retain their admitted-item slot until terminal; restart re-admits unfinished items without persisted host slots; admission follows authored item index; admitted-item dependence on pending work is not claimed to progress; and host ceiling one avoids only parent-held path-token deadlock because the parent releases before child scheduling and reacquires only for merge.

#### Scenario: One branch fails under WhenAllOutcomes
- **WHEN** fixed branches finish with mixed success and failure in arbitrary order
- **THEN** every branch reaches terminal, merge runs once with authored-order outcomes, and a following `If` observes the merged summary

#### Scenario: Durable item selector would change on replay
- **WHEN** a host fails after item snapshot commit
- **THEN** restart reuses the committed snapshot and does not evaluate the selector again

#### Scenario: Parked item and host ceiling compose
- **WHEN** a root `ForEach` item parks while the lower host/node item limit is full
- **THEN** it releases its path token but retains its admitted-item slot, later indices wait, and restart re-admits unfinished items in index order; no global-progress claim is made if admitted items depend on pending ones

### Requirement: Deadline and operation identity semantics are executable
Verification SHALL cover workflow deadline persistence across waits/retries/restart/continue-as-new; eager duplicate-`CompleteWithin` rejection; exact retry eligibility/exclusions; structural wait event/timeout races, losing-obligation cancellation, `WorkflowWaitTimeoutException`, and absence of timeout callbacks; fixed-codec detached mutable and replacement state; per-attempt timeout fencing and honest physical late overlap outside durable lease scopes; persistence before first dispatch of operation ID, retry-policy ordinal, absolute deadline, and in-flight state; same-coordinate crash/lost-response redispatch without budget consumption; ordinal increment only after committed eligible failure/timeout; `maxAttempts` one and two; expired-deadline replay without redispatch; stable `StepOperationId` across ambiguous policy retry/replay/competing hosts; and distinct IDs for loop, branch, item, and generation occurrences. Inside a durable lease scope, verification SHALL prove that a timed-out in-process body blocks the next policy attempt until it actually returns, whereas host-loss recovery may redispatch the same ordinal with the same operation ID/deadline/token/tickets while the obligation remains `AmbiguousHeld`.

#### Scenario: Late step result races retry
- **WHEN** a timed attempt reports after its deadline while a retry uses the same logical operation ID
- **THEN** attempt fencing accepts at most one valid transition and preserves monotonic attempt diagnostics

#### Scenario: Host loss redispatches one policy ordinal
- **WHEN** a host fails before dispatch, after an external effect with a lost response, or before transition commit
- **THEN** restart reuses the operation ID, attempt ordinal, and deadline; `maxAttempts = 1` permits that redispatch but no committed policy retry

#### Scenario: Committed timeout advances the policy ordinal
- **WHEN** a timeout transition commits and `maxAttempts = 2` still has budget
- **THEN** the next attempt alone uses ordinal two; physical invocations of ordinal one do not consume it

#### Scenario: Leased retry cannot overlap its prior local body
- **WHEN** a leased timed-out body remains physically active in the current process
- **THEN** a higher attempt is not admitted until that body returns, while the exact lease stays capacity-reserving and `NotConfirmable`

#### Scenario: Branch wait timeout is aggregated
- **WHEN** a branch wait times out inside a `WhenAllOutcomes` scope while another branch receives its event
- **THEN** the timeout cancels its event obligation, produces one typed failure outcome, and the ordered all-outcomes merge runs once

### Requirement: Durable lease guards prove protocol behavior
Lease verification SHALL use only scoped `AcquireResources(ResourceLeaseRequest, body)` and cover factory invariants, static/dynamic/multi-pool unknown-name rejection through `ResourcePoolNotConfiguredException` before queue/provider mutation, selector commit/replay, atomic grants, park-only-requesting-fiber, sequential loop scopes, sibling independence, ancestry compiler/runtime defense, and the exact lifecycle `Queued -> PendingCommit -> Held -> ReviewMarked -> AmbiguousHeld -> Quarantined -> Released` with cancellation-before-grant and `LeaseLost` side paths. It SHALL prove exact release before parent resume for unambiguous exits; same `StepOperationId`, attempt ordinal, deadline, protection token, tickets, and reserved capacity across host redispatch; no overlapping in-process leased policy retry; no ambiguity erasure from a successful retry alone; and quarantine transfer before progression on ambiguous scope exit, exhaustion, cancellation, deadline, forced termination, or abandonment. It SHALL also cover confirmation precedence `ConfirmationConflict` then `AlreadyConfirmed` then `NotConfirmable` then `Released` then `TokenNotFound`, including bound-ID cross-products; immutable `IDurableResourceLeaseDiagnostics` discovery; mixed-pool per-ticket review deadlines; mark-without-time-reclaim; causal orphan/release-gap recovery; missing-ticket `LeaseLost`; queued-cancel versus reservation/activation races and stale commands; isolated restoration; contended unit conservation; FIFO admission; immutable creation-definition startup agreement after replayed resize; exact four-barrier handoff recovery; governance stream validation and append conflict; tombstone retention; typed unknown `GetAsync`/`ResizeAsync`; nonpositive resize rejection before operation binding; closed resize replay/conflict; and resize debt.

#### Scenario: Review timestamp passes
- **WHEN** an active or ambiguous owner remains beyond its review timestamp
- **THEN** the test requires an audited mark with capacity still reserved and rejects expiry-based reclaim or renewal

#### Scenario: Terminal action lacks stop proof
- **WHEN** workflow timeout, cancellation, or forced termination occurs while protected work may exist
- **THEN** verification requires exact capacity-reserving quarantine and rejects release caused by terminal status, deletion acknowledgement, or elapsed time

#### Scenario: Successful retry reaches an ambiguous lease exit
- **WHEN** a retry succeeds after the obligation entered `AmbiguousHeld` and no trusted stop/fence proof has committed
- **THEN** the test requires exact transfer to `Quarantined` before parent/merge progress and rejects direct release or restoration to `Held`

#### Scenario: Trusted stop proof is replayed
- **WHEN** a reconciler repeats one `StopConfirmationId` for the matching protection token
- **THEN** capacity releases at most once and the confirmation cannot affect another occurrence

#### Scenario: Outstanding lease has no integration label
- **WHEN** a crash occurs before the companion persists an external-work label
- **THEN** `IDurableResourceLeaseDiagnostics.EnumerateOutstandingAsync` still returns one immutable token-correlated owner/ticket projection and ordinary workflow handles expose none of those advanced facts

#### Scenario: Lease ownership facts are compared
- **WHEN** the four-stage handoff reaches active ownership or is recovered from any named barrier
- **THEN** workflow and governance facts are equal on partition, obligation, instance, generation, fiber, scope entry, protection token, ticket, pool, units, and provider generation

#### Scenario: Isolated and contended release are measured correctly
- **WHEN** exact release occurs first with no waiter/resize and then with an eligible FIFO waiter
- **THEN** the isolated case restores the prior reserved count exactly, while the contended case may transfer immediately but preserves `sum(reserved obligation units) == pool ReservedUnits`, releases the old ticket once, and assigns a distinct successor ticket

#### Scenario: Resize operation ID changes intent
- **WHEN** certification repeats one resize ID with the same mutation and then with another pool or capacity
- **THEN** exact replay returns the originally recorded `Applied`, changed intent returns `Conflict` with recorded/attempted facts, and the conflict mutates no state

#### Scenario: Host restarts after pool resize
- **WHEN** a pool is created at N, resized to M with possible debt, and the host restarts with the original creation definition N
- **THEN** startup retains current M/debt; supplying M or another value as creation capacity fails even when it equals current capacity

#### Scenario: Cancellation races every grant barrier
- **WHEN** cancellation is injected before and after queued obligation, governance reservation, workflow activation, and ownership confirmation commits
- **THEN** the serialized winner yields either zero-ticket `CancelledBeforeGrant`, exact pre-activation compensation, or proven release/quarantine, and every delayed command is an idempotent stale no-op

### Requirement: Typed DAG execution is acceptance tested
`OrcaCore.Dag` and `OrcaCore.Dag.Hosting` fixtures SHALL prove resultless/resultful nodes, typed immutable run input, structural `Build` rejection of missing/duplicate `MapInput`, runtime direct-dependency-only output mapping, `DAG_INPUT_MAPPING_INVALID` before node-input commit/child start for invalid opaque mapper access, node input commit before start, child state privacy, one deterministic child instance per node occurrence, explicit registration/start/reopen/snapshot/output/cancellation results, authored ordinal/status/failure mapping, restart/duplicate-drive reattachment, dependency failure blocking, valid independent-node progression, `MaxConcurrentNodes` counting started nonterminal children including parked children, notification-driven terminal waiting, and the single versioned friend child bridge. Phase 0 SHALL verify the exact friend edge, absence of public `RunChild`/`RunChildren`, stable observable child identity, and reattachment without inventing pre-source internal record/query types. The full unified-outbox and `RootInstanceId`/`ParentInstanceId` implementation acceptance required by canonical AC-613/AC-614 SHALL remain mandatory and SHALL be verified by implementation tasks 8.4, 8.5, and 8.10 after those internal seams exist.

#### Scenario: Node maps an undeclared dependency
- **WHEN** a node projector calls `OutputOf` for a node that is not its direct dependency
- **THEN** runtime mapping evaluation fails with `DAG_INPUT_MAPPING_INVALID` before mapped-input commit or child start, dependency-blocks its dependants, and leaves independent nodes eligible

#### Scenario: Structurally incomplete node is built
- **WHEN** a DAG node has no `MapInput` or attempts to assign it twice
- **THEN** `Build` rejects the inspectable structural fault without claiming to inspect delegate code

#### Scenario: Independent DAG node and parked child are admitted
- **WHEN** an independent node maps only run input and a previously started child is parked nonterminal at the configured node cap
- **THEN** the independent mapping is valid but admission waits because the parked child continues to count against `MaxConcurrentNodes`

#### Scenario: Host fails after child start
- **WHEN** the DAG host restarts after child creation but before local acknowledgement
- **THEN** it reattaches the same node child instead of starting a duplicate

#### Scenario: Phase 0 reaches an implementation-only child seam
- **WHEN** a guard would need an internal outbox record or lineage query type that does not yet exist
- **THEN** Phase 0 proves the public/friend boundary and observable stable child reattachment, while AC-613/AC-614 remain assigned to tasks 8.4, 8.5, and 8.10 rather than fabricating a signature

### Requirement: Completion waits are notification-driven and race-free
Verification SHALL prove `WorkflowInstanceHandle<TOutput>.WaitForOutputAsync`, the `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` helper of the same name, and `DagRunHandle.WaitForTerminalAsync` use subscribe-then-authoritative-recheck notification flow and never periodic polling. Terminal state committed before, during, or after notification registration SHALL be observed without a missed wakeup. Caller cancellation SHALL cancel only the local wait and SHALL NOT mutate workflow, DAG-run, or child cancellation state.

#### Scenario: Terminal commit races output and DAG wait registration
- **WHEN** terminal output or a terminal DAG snapshot commits at every register/recheck interleaving
- **THEN** each wait completes once from authoritative committed state with zero polling-clock advances

#### Scenario: Caller cancels a completion wait
- **WHEN** a caller token wins while the workflow or DAG remains nonterminal
- **THEN** the waiting call cancels locally and no runtime cancellation command or state transition is emitted

### Requirement: Attempt diagnostics retain an explicit trusted-adapter boundary
`AttemptNumber` SHALL remain a public positive durable retry-policy ordinal alongside `StepOperationId`; public-surface guards SHALL retain it and documentation/fixtures SHALL forbid treating it as physical invocation count or external idempotency identity. Provider/companion certification SHALL prove ambiguous crash/replay redispatch uses the same operation ID and ordinal, committed policy retry alone increments the ordinal, and distinct logical occurrences use distinct operation IDs. Because core cannot inspect arbitrary adapter behavior, verification SHALL record operation-ID discipline and trusted stop attestation as certified integration obligations rather than claim runtime prevention of every misuse.

#### Scenario: External-effect adapter is retried
- **WHEN** certification invokes one logical operation across timeout, policy retry, crash redispatch, replay, or competing drivers
- **THEN** the adapter key remains `StepOperationId`, `AttemptNumber` changes only after a committed policy transition, and the fixture fails if ordinal or physical call count becomes the external identity

#### Scenario: Trusted reconciler offers insufficient evidence
- **WHEN** a certification fixture attempts stop confirmation using only elapsed time, delete acknowledgement, terminal workflow status, or an infrastructure label
- **THEN** the obligation remains reserved and the integration fails certification even though the generic runtime trust seam cannot inspect infrastructure-specific proof

### Requirement: Infrastructure separation is architecture verified
Architecture and packed-consumer guards SHALL prove no OrcaCore or `OrcaCore.Dag` dependency on Kubernetes, AWS, scheduler/job application projects, or SDKs, while an outward-dependent companion scheduler can consume generic application contracts.

#### Scenario: Pure Kubernetes companion is packed
- **WHEN** a companion project uses the Kubernetes API without an AWS feature
- **THEN** Kubernetes dependencies remain in that package and no AWS or integration package enters OrcaCore closures

#### Scenario: Terminal Kubernetes report reaches the lease boundary
- **WHEN** the companion receives a terminal report after `Wait`
- **THEN** it validates payload, Job UID, operation ID, protection token, and terminality inside the lexical lease before release; invalid or unproven reports preserve ambiguity/quarantine

### Requirement: Application projections exclude routing internals
Verification SHALL prove application definitions, instance snapshots, active waits, typed root state, and typed output omit executable plans, fiber/scope identity, raw park reason, wait sequence, provider generation, branch/item-private state, running absolute workflow deadline, and active attempt ordinal/deadline/outcome. Active wait deadlines remain authored application facts; terminal workflow timeout remains visible through status plus failure; quarantine remains a separate advanced diagnostic.

#### Scenario: Projection baseline is inspected
- **WHEN** public signature and behavior tests inspect application models
- **THEN** they expose typed authored/business facts only, while advanced diagnostics remain opt-in

### Requirement: Package consumer smoke tests guard dependency experience
Verification SHALL pack and restore clean consumers for minimal ephemeral, PostgreSQL-backed durable, SQL Server-backed durable, callback-only ingress through each production provider, application-supplied event dispatcher, DAG, provider-authoring, runtime-protocol custom-host, and optional companion integration paths.

#### Scenario: Minimal consumer builds from packages
- **WHEN** a clean project restores the documented application packages
- **THEN** the golden path compiles/runs without source-project references, advanced leaks, or optional infrastructure dependencies

### Requirement: Exact facade, event, hosting, and path-token contracts are guarded
Compile/reflection/behavior guards SHALL prove the exact common registry/definition/instance/event facade; four typed references and exact-reference lookups; mode-specific engine builders and application-configuration `AddWorkflow`; closed host-compatibility results and exceptions including missing dispatcher/not-registered; all seven exact role-specific hosting entry points and split options; absence of catch-all, separate hosted-service toggles, attributes, or assembly scanning; self-routing durable ingress with its exact acceptance/rejection unions; application-shaped `DispatchAsync` with its exact dispatch-result/failure values and no provider leaks; descriptor/correlation matching with ambiguous wait registration rejection; buffered reuse; and the one logical path-token model including parent release before root-only fan-out and physical late-body occupancy.

#### Scenario: Exact application facade baseline changes
- **WHEN** a contributor adds an unapproved route, management operation, hosting role, discovery path, serializer hook, provider-shaped dispatcher value, cancellation outcome, or conflicting token behavior
- **THEN** the corresponding exact-signature or behavior guard fails before source approval

### Requirement: Durable messaging is verified across every ownership boundary
Verification SHALL cover fixed-codec event descriptors, complete self-routing envelopes, the exact closed acceptance/rejection unions, `Accepted`/`Duplicate` broker-ack ownership, identity-before-target-state deduplication, direct-target/start-conflict/fanout-limit rejection without partial ownership, direct and correlation pending inboxes, atomic event/wait races, cold rehydration, callback-only start-intent handoff, exact start-or-deliver idempotency, committed provider-state fanout snapshots, per-target deduplication, durable publish/outbox atomicity, dispatcher retry/poison outcomes, and isolation of internal continuation records. In-memory development, PostgreSQL, and SQL Server providers SHALL pass the same logical certification; both production providers' restart tests SHALL prove persistence and competing-host ownership. No test SHALL treat a returned `NoActiveWait`, later broker redelivery, polling, or a hot in-memory instance as evidence for the durable contract.

#### Scenario: Event arrives before matching wait
- **WHEN** an accepted event survives host replacement before its wait registers
- **THEN** the later wait claims it once without broker redelivery and the exported API contains no durable `NoActiveWait`

#### Scenario: Fanout host fails during materialization
- **WHEN** a crash occurs before or after the fanout target snapshot/per-target inbox commit
- **THEN** recovery observes either no accepted fanout or the same complete target set, never a partial or rediscovered set

#### Scenario: Outbox dispatch is ambiguous
- **WHEN** a dispatcher sends successfully and the host fails before marking the record dispatched
- **THEN** retry uses the same outbound `EventId`, and the dispatcher still receives no provider record or internal continuation kind

#### Scenario: Definition-less ingress package is inspected
- **WHEN** the callback-only host accepts and persists a self-routing event
- **THEN** package/runtime guards prove it has no workflow catalog, handle lookup, definition execution, external outbox dispatcher, or broker SDK dependency

### Requirement: Canonical requirements precede source implementation
Canonical specs, the capability matrix, OpenSpec deltas, tasks, status, and review artifacts SHALL describe one contract before each source slice begins. Historical reviews SHALL remain immutable; a new dated disposition SHALL supersede stale recommendations without rewriting prior evidence.

#### Scenario: Phase 0 is reviewed
- **WHEN** the retargeted expected-red guard packet is ready
- **THEN** review covers every required task 3.1 through 3.12 including all four 3.11 slices, exact expected-red/pass counts are recorded, and task 4.0 remains blocked until independent approval
