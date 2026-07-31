# Section 7 Remediation Test-Recovery Record

Date: 2026-07-30

Status: live remediation audit; **not an approval, exit verdict, or authorization to begin
Section 8**.

## Purpose and method

This record supersedes the one-sentence retirement rationale in
`developer-facing-interface-section-07-provisional-test-retirement-2026-07-30.md` for recovery
purposes. It compares the current shared tree with the clean detached recovery worktree at
`X:\Projects\GitHub\Workflow-orca-recovery` (`d76192f089dd07f68e310c21fe4e5a38dd93cf7f`).
It does not edit, delete, or reinterpret any immutable review, verdict, request, or manifest.

The audit counted source declarations, not data-row expansions:

```powershell
[regex]::Matches(
    (Get-Content -Raw <file>),
    '(?m)^\s*\[(?:Fact|Theory)(?:\(|\])').Count
```

`Compile Remove` entries used only to keep compile/package fixture source out of the guard
assembly, and the zero-test
`OrcaCore.TestSupport/StructuredExecution/StructuredExecutionReferenceModel.cs` helper, are not
test retirement and are excluded from the figures below.

## Live remediation checkpoint

This section is the current numeric checkpoint and supersedes the earlier blocker totals below.
It was recounted from the live project files after the acceptance, Core, Ephemeral, Durable,
PostgreSQL, and inactive-project recovery passes:

- explicit excluded test files: **98 files / 549 declarations** (the zero-test
  `ReferenceLinearFiberInterpreter.cs` helper remains outside these figures);
- acceptance: **42/42 passing**, including 37 declarations recovered from the provisional
  retirement;
- Core: **413/413 passing**;
- Ephemeral: **64/64 passing**, including 52 current public recovery declarations / 53 executed
  cases;
- Durable: **72/72 passing**, including 28 restored current-contract cases;
- the four assigned Core authoring files are enabled with **46 declarations / 58 executed
  cases**;
- `PublicDefinitionCompilerContractTests` adds **11 declarations / 13 executed cases** while
  preserving the original mixed-tier 33-declaration compiler source under exclusion;
- all four files formerly available only in the recovery worktree are present and enabled:
  **34 current declarations**. Five child/legacy declarations were not silently carried into v1
  and are itemized below.

The exact current excluded-source classification is:

| Current class | Files | Declarations | Notes |
| --- | ---: | ---: | --- |
| R | 59 | 311 | Internal/IR source with named executable replacement evidence |
| D8 | 20 | 76 | Section 8 boundary |
| L | 14 | 75 | Later/non-v1 source |
| **B** | **0** | **0** | No unsupported retirement blocker remains |
| New L split | 1 | 2 | `LegacyOperationsAcceptanceTests.cs` |
| Mixed D8/L split | 1 | 5 | `R4DurableEngineDeferredTests.cs`: one Saga, four non-v1 cases |
| Mixed R/D8 mapped source | 1 | 6 | `StrongValueContractTests.cs`: five restored workflow declarations, one Section-8 DAG declaration |
| Mixed R/L mapped source | 2 | 74 | `DefinitionCompilerTests.cs`: 30 replaced/internal, 3 non-v1; `StructuredFiberExecutionTests.cs`: 33 restored publicly, 8 non-v1 |
| **Total** | **98** | **549** | |

At declaration level the same total is R 379, D8 78, L 92, and B 0. Mixed files are
shown separately above so a single file is not misleadingly credited to two classes.

## Reconciled inventory

| Inventory | Files | `[Fact]` / `[Theory]` declarations | Disposition |
| --- | ---: | ---: | --- |
| Original provisional retirement | 128 | 707 | Prior record |
| Restored public acceptance files | 15 | 37 current | Compiled and executed in the 42/42 lane; 8 superseded declarations are preserved in the disposition map |
| Restored PostgreSQL certification binding | -1 | -16 local | All 40 binding cases and the complete 79-test PostgreSQL project pass against Docker |
| **Current explicit test-file exclusions** | **98** | **549** | Live recount after remediation; classification above is authoritative |
| Files formerly present only in recovery worktree | 4 restored | 34 current | All are present and enabled; 5 original declarations have explicit deferred/superseded disposition |
| Five formerly inactive projects | 31 source files with declarations | 146 original declarations | Separate preserved inventory; five new integration declarations added |

The PostgreSQL binding also inherits 23 declarations from
`EventStoreCertificationTests` and one from `ContinueAsNewCertificationTests`. Therefore the
re-enabled class now exposes 40 certification tests (16 local + 24 inherited), not merely the 16
local declarations subtracted from the old retirement table.

## Original classification result

The original classifications were:

- **R** — true internal/compiled-IR white-box coverage for which named public, scenario, guard, or
  certification evidence exists;
- **D8** — DAG/Saga/child/companion behavior retained for the Section 8 boundary;
- **L** — an intentionally non-v1 or later-staged provider/feature surface;
- **B** — supported public behavior with no one-to-one executed replacement located:
  **blocker; restore or port**.

| Class | Files | Declarations |
| --- | ---: | ---: |
| R — internal/IR with named replacement evidence | 59 | 309 |
| D8 — Section 8 boundary | 20 | 76 |
| L — later-staged/non-v1 feature | 13 | 72 |
| **B — unsupported retirement blocker** | **25** | **205** |
| **Original remaining exclusions** | **117** | **662** |

These figures are retained as the starting audit, not the live result. The current result is the
98/549 checkpoint above. A file remains in class B whenever it mixes supported and deferred
checks without executable replacement; supported checks must be extracted before the residual
source can be deferred.

## Named replacement-evidence keys

These keys make the file ledger readable. They identify the concrete evidence, not a vague
suite-level claim.

- **E-AUTH** — `AuthoringContractGuards`, `PortableAuthoringIntersectionGuards`, the exact
  positive/forbidden consumer compile fixtures, and the executable
  `empty-parallel-diagnostic-parity`, `duplicate-completewithin-eager`, and
  `legal-placement-and-sequential-scopes` scenarios.
- **E-FANOUT** — restored `ControlFlowAcceptanceTests`, `ParallelAcceptanceTests`, and
  `ForEachAcceptanceTests` plus executable `ordered-join-failure`,
  `ancestor-terminal-suppresses-merge`, `foreach-bound-and-snapshot-replay`,
  `restart-readmits-unfinished-items`, and `foreach-lower-limit-and-admitted-slots`.
- **E-EVENT** — restored `WaitAcceptanceTests`; executable `four-event-overloads`,
  `dedup-conflict-redelivery`, `ambiguous-pair-rejection`, `signal-stream-reuse`, and
  `definitionless-continuation-handoff`; provider certification
  `InboxDuplicate_AfterRecordedApplied_IsIgnored` and
  `SameInboxEventId_OnDifferentTargets_IsRecordedIndependently`.
- **E-LIFE** — restored `LifecycleAcceptanceTests` and `TerminalAcceptanceTests`, plus public
  `EphemeralWorkflowFacadeTests` / `DurableLifecycleFacadeTests` cancellation and termination
  race regressions.
- **E-DEADLINE** — restored `TimerAcceptanceTests` and `PolicyAcceptanceTests`, plus executable
  `deadline-persistence-inheritance`, `attempt-copy-fencing-overlap`,
  `committed-retry-increment`, `maxattempts-one-two-expired-replay`, and
  `ancestor-terminal-suppresses-merge`.
- **E-LEASE** — executable lease admission/exit/recovery scenarios, including
  `atomic-multipool-grant`, `queued-cancellation-zero-ticket`,
  `release-before-parent-resume`, `quarantine-before-progression`, and
  `truthful-confirmation-no-time-reclaim`, plus resource-governance provider certification.
- **E-CODEC** — `FixedCodecClosureContractGuards` and executable
  `fixed-codec-determinism`, `attempt-local-replace-state`, and `typed-completion-output`.
- **E-FACADE** — executable `four-typed-registration-handles`,
  `compatibility-order-and-copy`, `get-handle-or-throw-parity`,
  `reduced-snapshot-management`, and the public facade start/output tests.
- **E-HOST** — `FacadeHostingContractGuards`; executable `six-hosting-entry-owners`,
  `role-exclusivity-and-dependencies`, `programmatic-options-copy-validation`,
  `exact-type-throttle`, and `transient-decorator-binding`; current-surface
  `HostingRoleCompositionTests`.
- **E-RECOVERY** — current-surface durable InMemory/PostgreSQL application journeys, executable
  `durable-wait-output`, `definitionless-continuation-handoff`,
  `restart-readmits-unfinished-items`, and provider checkpoint/structured-fiber certification.
- **E-OBS** — `OperationalTelemetryContractGuards` and the exact split-host role scenarios.

The rejected Section 7 review executed the then-frozen infrastructure/scenario evidence. This
record does not promote that rejection into approval. During this remediation, the restored
acceptance lane has independently run 42/42 (37 declarations recovered from the provisional
retirement), and the new current-surface integration lane has run 5/5. Every named replacement
must still be re-run in the eventual frozen exit packet.

## Explicit map of the 29 restored acceptance declarations

| Restored file | Declarations | Public behaviors restored | Current result |
| --- | ---: | --- | --- |
| `StraightLineAcceptanceTests.cs` | 2 | completion/state; inspectable failure | 2/2 pass |
| `WaitAcceptanceTests.cs` | 4 | wait inspection; matching payload; nonmatch; concurrent resume | 4/4 pass |
| `TimerAcceptanceTests.cs` | 1 | ephemeral delay completion after due time | 1/1 pass |
| `LifecycleAcceptanceTests.cs` | 1 | documented lifecycle event guarantees | 1/1 pass |
| `RetentionAcceptanceTests.cs` | 1 | archive terminal instance and remove from active set | 1/1 pass |
| `PolicyAcceptanceTests.cs` | 2 | step timeout; bounded idempotent retry | 2/2 pass |
| `TerminalAcceptanceTests.cs` | 6 | completion; outcome; cancel; terminate; illegal terminal triggers; destructive safety | 6/6 pass |
| `ControlFlowAcceptanceTests.cs` | 2 | `If`; `While` | 2/2 pass |
| `ParallelAcceptanceTests.cs` | 5 | ordered/shape-independent merge; branch wait; serialized branch race | 5/5 pass |
| `ForEachAcceptanceTests.cs` | 5 | isolated items; `WhenAll`; max concurrency; wait-all/fail; ordered outcomes | 5/5 pass |
| **Total** | **29** | | **29/29 pass** |

These are mechanical public-surface ports. None requires a test friend edge or a compatibility
shim.

## Additional acceptance recovery and explicit supersession

| Enabled file | Current declarations | Current v1 behavior | Result |
| --- | ---: | --- | --- |
| `LoopWaitAcceptanceTests.cs` | 1 | `While` + `Wait` occurrence isolation | pass |
| `MailboxAcceptanceTests.cs` | 2 | duplicate replay and unresolved current event handling | pass |
| `ManagementAcceptanceTests.cs` | 1 | detached state snapshot through the reduced facade | pass |
| `OperationsAcceptanceTests.cs` | 1 | transient-pool concurrency | pass |
| `RoutingAcceptanceTests.cs` | 3 | instance/definition correlation scope and registration ambiguity | pass |
| **Additional recovered** | **8** | | **8/8 pass** |

The declaration delta is explicit:

- the former mailbox early-buffering case is superseded by v1 `NoActiveWait`;
- five broad list/count/statistics management cases are outside the reduced v1 management
  contract;
- the two legacy stuck-query cases are preserved in excluded
  `LegacyOperationsAcceptanceTests.cs`, have no active AC traits, and are waived as superseded
  management behavior by the task-7.7 AC-507/AC-508 waiver.

Together with the earlier 29 declarations, this restores 37 declarations from the retirement.
The complete active acceptance project is 42/42 passing.

## Re-enabled PostgreSQL certification binding

`PostgreSqlProviderCertificationTests.cs` is no longer under `Compile Remove`. The binding now
maps the shared provider contract to the production PostgreSQL implementation:

- 23 inherited event-store declarations, including cross-target inbox identity, atomic start
  binding, checkpoint/structured-fiber recovery, outbox, timer, projection, and concurrency;
- one inherited continue-as-new declaration;
- 16 PostgreSQL-local declarations for migration journaling, clocked journal timestamps,
  replacement-process event and start idempotency, append rollback, outbox/timer atomicity,
  history/query/retention, concurrent claims, cold wait projection, and the retained Saga audit
  projection.

The public/inherited class compiled after the mechanical projection/strong-value port. All 40
binding cases now pass against Docker after the event-dedup/start-binding provider graph
remediation; the complete PostgreSQL project passes 79/79.

## Recovered Core authoring contract

The four mixed authoring files are enabled without a friend edge or reference to compiled IR:

| Enabled file | Original declarations | Enabled declarations | Executed cases | Disposition |
| --- | ---: | ---: | ---: | --- |
| `Building/AuthoringLifecycleTests.cs` | 11 | 11 | 20 | Public lifecycle diagnostics, epoch ownership, callback expiry, concurrency, and stable fingerprints |
| `Building/BranchContractTests.cs` | 4 | 4 | 4 | Public closed outcomes, finite options, heterogeneous private branch state, and item metadata shape |
| `Building/ModeFirstWorkflowBuilderTests.cs` | 13 | 12 | 12 | Public mode/capability/diagnostic/fingerprint contract; one child declaration retained for Section 8 |
| `Building/StagedWorkflowBuilderTests.cs` | 19 | 19 | 22 | Public staged surface, diagnostics, fingerprints, leases, named steps, and root `ForEach` |
| **Total** | **47** | **46** | **58** | **58/58 pass** |

The omitted mode-first declaration was
`ChildWorkflows_AreDurableOnlyStructuralInstructions`. Public child authoring is absent from v1;
the original source remains recoverable at the clean worktree commit and the obligation is D8,
not credited as deleted coverage.

`Execution/FailureProvenanceTests.cs` is also enabled by the adjacent recovery pass. The complete
Core project, including the new public compiler suite below, is **413/413 passing**.

## Compiler source disposition

The original `Compilation/DefinitionCompilerTests.cs` remains intact and excluded because it
directly constructs implementation authoring nodes and inspects compiled IR. Its 33 declarations
are no longer one undifferentiated blocker:

| Disposition | Count | Original declarations / replacement |
| --- | ---: | --- |
| Public-bearing behavior restored | 22 | Stable recompilation; both-mode `ForEach`; bounds; durable transient-pool absence; fixed codec/no registry seam; wide fan-out; both loop-progress diagnostics; empty parallel; public control-structure closure; portable cross-mode fingerprint; deterministic/structural/outcome fingerprint; `MaxItems`; branch identity; opaque captures; unsupported delegate; converter root/member/input; lease recursion and rollover-in-lease made unrepresentable |
| Pure implementation-tier IR retained | 8 | Plan indexes/allowed kinds; manual durable root `ForEach`; both hand-built nested-fanout bypasses; hand-built result/merge mismatch; typed scope-plan lowering; collection immutability. Replacements: E-AUTH exact compile fixtures and root-only guards, public branch/type contracts, stable public fingerprints, and package/public-surface guards |
| Superseded/non-v1 | 3 | `WhenAny` failure policies, consumer compiler-limit options, and `WorkflowPartitioner` configuration |
| **Total** | **33** | |

`PublicDefinitionCompilerContractTests.cs` supplies 11 public-only declarations / 13 executed
cases. Existing enabled `BranchContractTests`, `ModeFirstWorkflowBuilderTests`,
`StagedWorkflowBuilderTests`, `WorkflowBuilderTests`, `WorkflowPolicyBuilderTests`, and the exact
compile fixtures own the remaining public mappings. No `OrcaCore.Core.*` implementation type,
`RuntimeDefinition`, or `CompiledPlan` is referenced by the new suite.

## Ephemeral current-contract recovery pass

The Ephemeral recovery pass kept the shared build green while each current public behavior was
ported. It did not restore a friend edge or expose implementation types. The legacy
`StructuredFiberExecutionTests.cs` and its internal
`StructuredExecutionReferenceModel.cs` helper remain intact under `Compile Remove`; their
current-v1 cases were re-derived through `Workflow`, the split hosting entry,
`IWorkflowDefinitionRegistry`, typed handles, `IWorkflowEventClient`, and public management
operations.

| Original source | Original declarations | Restored current declarations | Executed cases | Explicit residual disposition |
| --- | ---: | ---: | ---: | --- |
| `Execution/LoopWaitTests.cs` | 5 | 5 | 5 | none |
| `Execution/NamedStepDependencyInjectionTests.cs` | 4 | 4 | 4 | none |
| `Execution/StructuredFiberExecutionTests.cs` | 41 | 33 in `StructuredFiberExecutionPublicTests.cs` | 34 | 8 non-v1: one authored `Yield`, two `WhenFirst`, one durable-only step intent, two `WhenAny`, one batch partition, and one fail-fast declaration |
| `Governance/HostGovernanceExecutionTests.cs` | 3 | 3 | 3 | none |
| `Governance/HostGovernanceOptionsTests.cs` | 2 | 3 | 3 | one split public validation declaration was added to preserve every original throttle-validation branch |
| `Management/StuckDetectionTests.cs` | 3 | 0 | 0 | all 3 are superseded task-7.7 catch-all stuck query/statistics behavior; the exact v1 facade intentionally exposes no enumeration, bulk query, or stuck-management operation |
| `Timers/EphemeralTimerTests.cs` | 5 | 4 | 4 | one authored `Yield` continuation is outside the exact v1 authoring surface |
| **Total** | **63** | **52** | **53** | **12 explicitly deferred/superseded** |

The 33 structured declarations cover zero-backoff retry fairness, both fixed-branch join
families, wide fan-out, delayed retry continuation failure, transient-pool parking, merge and
projection failures, event-resume context, branch isolation/order, path ceiling one, nested
`If`, bounded/empty/tagged `ForEach`, pre-materialization bounds, cancellation/termination merge
suppression, completion permutations, item admission limits, partial-failure collection,
wait-all failure, and item-state alias isolation. The 34-case lane passed three consecutive
runs. Governance recovery uses explicit gates and status barriers; it contains no wall-clock
`Task.Delay`.

## Remaining acceptance exclusions

| File | Declarations | Class | Current-surface status / named evidence |
| --- | ---: | :---: | --- |
| `ChildWorkflowAcceptanceTests.cs` | 10 | D8 | Public child APIs are absent from v1; retain for Section 8 child/DAG extraction. |
| `DagAcceptanceTests.cs` | 2 | D8 | Owned by tasks 8.1-8.10 and `typed-dag-contract`. |
| `DagObservabilityAcceptanceTests.cs` | 1 | D8 | Owned by task 8.10 snapshot/status verification. |
| `LegacyOperationsAcceptanceTests.cs` | 2 | L | Preserved superseded stuck-query cases; AC-507/508 waived under task 7.7 and no active AC traits remain. |
| `SagaAcceptanceTests.cs` | 4 | D8 | Retain for the Section 8 companion/Saga decision. |
| `WhenFirstAcceptanceTests.cs` | 2 | L | `WhenFirst` is not in the exact v1 authoring intersection; retain for a later approved feature slice. |
| `YieldAcceptanceTests.cs` | 1 | L | Public `Yield` is not in the exact v1 authoring surface; retain for a later approved feature slice. |

## Remaining Core exclusions

| File | Declarations | Class | Current-surface status / named evidence |
| --- | ---: | :---: | --- |
| `Building/DagBuilderTests.cs` | 8 | D8 | Task 8.2/8.3 build diagnostics and mapping contract. |
| `Building/PartitionerTests.cs` | 3 | R | Internal partitioner mechanics replaced at the public boundary by E-FANOUT and five restored `ForEach` acceptances. |
| `Building/SagaBuilderTests.cs` | 2 | D8 | Section 8 companion/Saga decision. |
| `Compilation/DefinitionCompilerTests.cs` | 33 | R/L mapped | Original mixed-tier source retained; 22 public-bearing declarations restored, 8 IR-only declarations have named evidence, and 3 are non-v1. See compiler source disposition above. |
| `Contracts/StrongValueContractTests.cs` | 6 | R/D8 mapped | Five ordinary workflow strong-value/JSON declarations execute in `WorkflowStrongValueContractTests.cs`; the one DAG identity declaration remains assigned to Section 8. |
| `Definitions/DefinitionModelTests.cs` | 7 | R | Internal model shape replaced by E-AUTH, E-FANOUT, and exact packed-consumer surface checks. |
| `Execution/BranchIsolationTests.cs` | 1 | R | E-FANOUT public branch isolation/merge evidence. |
| `Execution/FiberIdentityTests.cs` | 3 | R | E-FANOUT occurrence/path-token scenarios. |
| `Execution/ForEachScopeReducerTests.cs` | 2 | R | E-FANOUT ordered item outcomes and replay. |
| `Execution/JoinPolicyReducerTests.cs` | 4 | R | E-FANOUT join-policy scenarios and restored Parallel/ForEach acceptances. |
| `Execution/LinearFiberInterpreterTests.cs` | 8 | R | Internal interpreter mechanics replaced by restored StraightLine/ControlFlow/Wait and E-FANOUT. |
| `Execution/ReferenceLinearFiberInterpreter.cs` | 0 | R | Helper only; travels with the internal interpreter retirement. |
| `Execution/ScopeMergeAdapterTests.cs` | 3 | R | E-FANOUT once-only/suppressed merge evidence. |
| `Execution/ScopeReducerTests.cs` | 4 | R | E-FANOUT completion/cancellation/terminal schedules. |
| `Execution/StructuredExecutionReferenceModelTests.cs` | 3 | R | E-FANOUT completion permutations; the public scenario is the supported evidence owner. |
| `Hosting/StructuredExecutionHostOptionsTests.cs` | 2 | R | `HostGovernanceOptionsTests` executes positive-capacity, nonpositive-capacity, non-step-type, and copied-option behavior; the executable `exact-type-throttle` scenario rejects duplicate exact step types through the split host entry. |
| `Primitives/ValidationTests.cs` | 3 | R | Internal validation accumulator replaced by public `Build`/`TryBuild` diagnostic parity in E-AUTH. |

## Remaining Durable exclusions

| File | Declarations | Class | Current-surface status / named evidence |
| --- | ---: | :---: | --- |
| `Aggregates/DurableAggregateTests.cs` | 9 | R | Internal aggregate transitions represented through E-LIFE, E-EVENT, and E-RECOVERY. |
| `Aggregates/DurableChildWorkflowStateTests.cs` | 6 | D8 | Task 8.4/8.5 internal child protocol. |
| `Aggregates/DurableExternalJobStateTests.cs` | 3 | D8 | Tasks 8.8/8.9 companion job journey; no public generic job node. |
| `Aggregates/DurableResourcePoolStateTests.cs` | 6 | R | E-LEASE and resource-governance certification. |
| `Aggregates/DurableOwnershipContractTests.cs` | 1 | R | E-LEASE exact fiber/scope/ticket ownership. |
| `Aggregates/DurableTerminalCleanupOrderTests.cs` | 1 | R | E-LEASE release/quarantine-before-parent-progression. |
| `Aggregates/DurableTimerAggregateTests.cs` | 4 | R | E-DEADLINE plus timer provider certification. |
| `Aggregates/DurableTimerStateTests.cs` | 7 | R | E-DEADLINE plus timer provider certification. |
| `Aggregates/DurableWaitStateTests.cs` | 3 | R | E-EVENT wait occurrence/dedup and E-DEADLINE losing-obligation cancellation. |
| `Composition/ChildLineageTests.cs` | 2 | D8 | Task 8.4/8.10 lineage. |
| `Composition/RunChildTests.cs` | 2 | D8 | Task 8.4/8.5 internal child start/join. |
| `Continuations/ContinueAsNewAggregateTests.cs` | 4 | R | Active `ContinueAsNewAcceptanceTests`, E-DEADLINE inheritance, and inherited provider certification. |
| `Definitions/DurableDefinitionRegistryTests.cs` | 6 | R | E-FACADE registration, compatibility ordering, and typed handles. |
| `Driver/DurableDeadlineExecutionTests.cs` | 5 | R | E-DEADLINE exact persisted deadline/retry schedules. |
| `Driver/DurableDriverAcceptanceTests.cs` | 7 | R | E-RECOVERY public application/host replacement plus E-FANOUT restart. |
| `Driver/DurableDriverHostAcceptanceTests.cs` | 10 | R | E-HOST and E-RECOVERY split-role drive/ingress evidence. |
| `Driver/DurableDriverReviewedAcceptanceTests.cs` | 14 | R | E-DEADLINE, E-EVENT, E-FANOUT, and E-LEASE reviewed scenarios. |
| `Driver/DurableFiberEnvelopeMapperTests.cs` | 4 | R | E-RECOVERY provider checkpoint/structured-fiber certification. |
| `Driver/DurableLeaseExecutionTests.cs` | 12 | R | E-LEASE admission, retry, exit, and quarantine scenarios. |
| `Driver/DurableStepThrottleCoordinatorTests.cs` | 3 | R | E-HOST exact-type throttle plus E-LEASE admission ordering. |
| `Driver/DurableStepThrottleExecutionTests.cs` | 5 | R | E-HOST/E-LEASE runtime admission scenarios. |
| `Driver/DurableStructuredFiberDriverTests.cs` | 47 | R | Internal driver schedules represented by E-FANOUT, E-DEADLINE, E-EVENT, E-LEASE, and E-RECOVERY. |
| `Events/DurableInboxTests.cs` | 5 | L | Early-event buffering is superseded by v1 `NoActiveWait` without consuming the event ID; current per-target dedup is owned by E-EVENT. |
| `Execution/DurableAggregateLoaderTests.cs` | 2 | R | E-RECOVERY checkpoint/tail reconstruction. |
| `Execution/DurableCheckpointMapperTests.cs` | 2 | R | E-RECOVERY checkpoint round-trip/host replacement. |
| `Execution/DurableCommandPipelineTests.cs` | 9 | R | Provider atomicity/collision certification plus E-RECOVERY serialized drive. |
| `Execution/DurableCommitMaterializerTests.cs` | 7 | R | Provider atomic batch certification and E-EVENT/E-LEASE committed outcomes. |
| `Execution/DurableCommitPipelineTests.cs` | 6 | R | Provider atomicity/outbox certification and E-RECOVERY. |
| `Execution/DurableDagRunnerTests.cs` | 2 | D8 | Task 8.5 runtime-owned DAG progression. |
| `Execution/DurableInboxPreflightTests.cs` | 4 | R | E-EVENT public duplicate/conflict classification and provider inbox certification. |
| `Execution/DurableResourcePoolCommitEffectsTests.cs` | 3 | R | E-LEASE four post-commit barriers and conservation certification. |
| `Execution/DurableWaitTests.cs` | 7 | L | Primarily `WaitLong`/eviction behavior outside the exact v1 public surface; retain for a later approved slice. |
| `Lifecycle/DurableLifecycleEventTests.cs` | 1 | R | E-LIFE public lifecycle snapshots/results. |
| `Management/DurableManagementTests.cs` | 10 | L | Pause/resume/history/DAG reconstruction and broad destructive operator surface are not in reduced v1 management. |
| `Management/DurableQueryTests.cs` | 4 | L | Broad projection query/statistics surface was replaced by the reduced v1 facade. |
| `Management/DurableStatisticsTests.cs` | 2 | L | Broad statistics surface is outside reduced v1 management. |
| `R4DurableEngineDeferredTests.cs` | 5 | D8/L | One Saga declaration remains D8; early buffering, pause/resume, broad management, and history inspection are non-v1. The eight current R4 declarations are enabled separately. |
| `Recovery/DurableCheckpointStateSurvivalTests.cs` | 4 | D8 | Saga and child resume-token state; retain for task 8 child/Saga work. |
| `ResourceGovernance/DurableResourcePoolManagementTests.cs` | 2 | R | E-LEASE management input closure and resize/debt scenarios. |
| `ResourcePools/PoolAcquisitionTests.cs` | 5 | R | E-LEASE atomic multi-pool/FIFO/cancel and provider certification. |
| `Sagas/ChildCompensationTests.cs` | 3 | D8 | Section 8 child/Saga boundary. |
| `Sagas/CompensationDecisionTests.cs` | 3 | D8 | Section 8 Saga boundary. |
| `Sagas/CompensationFailureTests.cs` | 4 | D8 | Section 8 Saga boundary. |
| `Sagas/DurableSagaCommandAdapterTests.cs` | 2 | D8 | Section 8 Saga boundary. |
| `Sagas/DurableSagaStateTests.cs` | 3 | D8 | Section 8 Saga boundary. |
| `Sagas/SagaPolicyInteractionTests.cs` | 2 | D8 | Section 8 Saga boundary. |
| `Sagas/StructuredSagaOwnershipTests.cs` | 5 | D8 | Section 8 Saga/fiber ownership boundary. |
| `Versioning/DurableVersioningTests.cs` | 4 | R | Corrected v1 behavior is owned by E-FACADE compatibility and replacement-host start-binding tests; the old changed-input expectation is superseded. |

## Remaining Ephemeral exclusions

| File | Declarations | Class | Current-surface status / named evidence |
| --- | ---: | :---: | --- |
| `Diagnostics/EphemeralDiagnosticsTests.cs` | 1 | R | E-OBS exact public telemetry/diagnostic contract. |
| `Execution/CoreRuntimeScenarioTests.cs` | 7 | L | Uses superseded catch-all registry/event/management methods; retain only for later case extraction. |
| `Execution/DeadlineExecutionTests.cs` | 3 | R | E-DEADLINE workflow deadline, wait timeout, and late-attempt fencing. |
| `Execution/EphemeralRoutingIndexTests.cs` | 2 | R | Internal index mechanics represented by E-EVENT correlation/ambiguity cases. |
| `Execution/ExecutionLaneTests.cs` | 5 | R | Internal lane serialization represented by E-LIFE and restored concurrent wait/branch races. |
| `Execution/InterpreterControlFlowTests.cs` | 10 | R | Internal interpreter mechanics represented by restored ControlFlow/Parallel/ForEach and E-FANOUT. |
| `Execution/InterpreterTests.cs` | 5 | R | Internal interpreter mechanics represented by restored StraightLine/Wait/Terminal. |
| `Execution/MailboxTests.cs` | 7 | L | Early buffering belongs to the superseded mailbox contract; current event results are E-EVENT. |
| `Execution/RoutingTests.cs` | 8 | R | E-EVENT correlation/ambiguity/scoping; registry-wide-scan assertion is an internal implementation check. |
| `Execution/RuntimeTimerRecordTests.cs` | 1 | R | E-DEADLINE/timer public behavior. |
| `Execution/StructuredFiberExecutionTests.cs` | 41 | R/L mapped | 33 current-v1 declarations are restored as 34 public-facade cases in `StructuredFiberExecutionPublicTests.cs`; 8 residual declarations are explicitly non-v1 (`Yield`, `WhenFirst`, durable-only intent, `WhenAny`, batch partition, fail-fast). |
| `Execution/WaitMatchingTests.cs` | 8 | R | Restored Wait acceptance plus E-EVENT sequential/dedup scenarios. |
| `Execution/YieldTests.cs` | 4 | L | Public `Yield` is outside exact v1 authoring. |
| `Execution/YieldContinuationSchedulerTests.cs` | 1 | L | Scheduler exists only for the non-v1 Yield feature. |
| `Lifecycle/LifecycleEventTests.cs` | 1 | R | Restored Lifecycle acceptance and E-LIFE. |
| `Management/EvictTests.cs` | 4 | L | Explicit live-instance eviction is not in reduced v1 management. |
| `Management/ManagementQueryTests.cs` | 18 | L | Broad engine-local selection/statistics surface is superseded by reduced v1 management. |
| `Management/StuckDetectionTests.cs` | 3 | L | All three target the superseded catch-all stuck query/statistics surface removed by task 7.7; the exact v1 application snapshot and management facade expose no stuck-query operation. |
| `Management/TerminalCommandTests.cs` | 10 | R | Restored six-case Terminal acceptance plus E-LIFE committed cancellation/termination races. |
| `Policies/RetryPolicyTests.cs` | 3 | R | Restored Policy acceptance plus E-DEADLINE retry classification/attempt schedules. |
| `Policies/TimeoutPolicyTests.cs` | 1 | R | Restored timeout acceptance plus E-DEADLINE. |
| `Registration/DefinitionRegistrationTests.cs` | 1 | R | E-FACADE fingerprint compatibility and start-conflict behavior. |
| `Sagas/EphemeralSagaTests.cs` | 8 | D8 | Retain for the Section 8 companion/Saga decision. |

## Remaining Hosting exclusions

| File | Declarations | Class | Current-surface status / named evidence |
| --- | ---: | :---: | --- |
| `ContinuationPumpHostedServiceTests.cs` | 1 | R | Catch-all host internals replaced by E-HOST split durable role and current-surface hosting composition. |
| `DurableDriverTelemetryAndOptionsTests.cs` | 2 | R | E-HOST and E-OBS exact durable role/options/telemetry. |
| `OrcaCoreHostingServiceCollectionTests.cs` | 11 | R | Superseded catch-all registration replaced by E-HOST six exact entry owners and 2 current-surface composition tests. |
| `WorkflowPayloadSerializationRegistrationTests.cs` | 2 | R | Serializer replacement seam removed; E-CODEC and E-HOST lock the fixed codec/no-hook surface. |

## Files formerly available only in the clean worktree

All four are now present and enabled in the active tree.

| Restored file | Original declarations | Current declarations | Disposition |
| --- | ---: | ---: | --- |
| `Core.Tests/Building/WorkflowBuilderTests.cs` | 29 | 24 | Current public build/diagnostic/fingerprint contract restored; five child/legacy cases are explicitly deferred or superseded. |
| `Core.Tests/Building/WorkflowPolicyBuilderTests.cs` | 3 | 3 | Public decorator fingerprint, absent definition policy, and eager invalid retry restored. |
| `Engine.Ephemeral.Tests/Governance/ResourceGovernanceTests.cs` | 4 | 4 | Concurrent step/pool limits, permit-wait timeout exclusion, and per-instance serialization restored. |
| `Engine.Ephemeral.Tests/Timers/TimerEventRaceTests.cs` | 3 | 3 | Event-wins, timeout-wins, and reused wait signature restored. |
| **Total** | **39** | **34** | **No file remains physically deleted.** |

## Five formerly inactive projects

The separate live ledger
`developer-facing-interface-section-07-inactive-test-project-audit-2026-07-30.md` owns this
inventory. This record cross-checked, but did not edit, that ledger.

| Project | Original declarations | Current disposition |
| --- | ---: | --- |
| `OrcaCore.Integration.Tests` provisional sources | 115 | Preserved and explicitly noncompiled by category; five new public-only `CurrentSurface` tests pass 5/5 (four non-container, one PostgreSQL Docker). |
| `OrcaCore.Providers.RabbitMq.Tests` | 7 | Later-staged, non-first-release transport; retained on disk. |
| `OrcaCore.Providers.Redis.Tests` | 8 | Later-staged, non-first-release transport; retained on disk. |
| `OrcaCore.Providers.SqlServer.Tests` | 12 | Open plan-reconciliation obligation because pending task 10.2 still names SQL Server; neither retired nor activated by fiat. |
| `OrcaCore.Providers.ZeroMq.Tests` | 4 | Later-staged, non-first-release transport; retained on disk. |
| **Total** | **146** | No source file deleted. |

`OrcaCore.Integration.Tests` is reactivated in `OrcaCore.slnx`. Its list-tests output is exactly the
five new `CurrentSurface` declarations. Fresh restore/build resolved no RabbitMQ, Redis, SQL Server,
ZeroMQ, NetMQ, or associated Testcontainers package into that lane. The current results are:
solution build 0 warnings/errors; integration build 0/0; non-container 4/4; PostgreSQL Docker 1/1;
full lane 5/5.

## Closure state

The unsupported-retirement class is now empty. Ordinary workflow strong values, typed throttle
validation, duplicate exact-step rejection, structured-fiber public behavior, and the complete
PostgreSQL certification binding all execute on the current surface. The remaining excluded
declarations are individually assigned to named public replacement evidence, Section 8, or an
explicitly non-v1/superseded contract.

This corrected ledger is evidence for, not a substitute for, a fresh independent Section 7 exit
review. Only that review may decide whether the residual R/D8/L classifications and the five
inactive-project dispositions are sufficient.
