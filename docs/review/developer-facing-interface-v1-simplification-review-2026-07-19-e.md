# Independent review E: OrcaCore v1 planning contract and Phase 0 guard readiness

**Review date:** 2026-07-19

**Reviewer stance:** independent senior .NET public-API, durable-runtime, and workflow-authoring review

**Scope:** every current file bounded by `developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md`

This review judges the current files in the prompt's authority order. It does not judge the stale
product source or claim that Phase 0 guards have been implemented. Because other independent
reviews already occupy the prompt's requested output name, this review uses the next free `-e`
suffix and does not replace those artifacts.

The dated reviewer prompt itself says nested `Parallel` is selected. That statement is stale.
Current higher authority selects **root-only** fixed `Parallel`: the matrix says so at
`docs/specs/17-selected-mode-capability-matrix.md:29-34,754-758`, the exact companion exposes only
the two root scope/join families at `docs/specs/17-public-authoring-contract.cs:721-807`, and the
active task requires non-root absence at
`openspec/changes/reshape-developer-facing-interfaces/tasks.md:27`. This review applies the current
root-only decision.

## Verdicts

1. **Planning contract: APPROVE WITH CHANGES.** The selected surface is coherent enough to retain,
   but the findings below must be resolved before its guard packet is treated as executable
   authority.
2. **Guard-retarget readiness: NOT READY.** A guard-only agent would currently have to choose
   semantics for empty `Parallel`, duplicate workflow deadlines, crash replay attempt numbering,
   host/definition and pool compatibility, post-resize restart, confirmation precedence, snapshot
   contents, and internal DAG lineage/outbox coverage. The supplied guard status also directs the
   agent to implement a deferred member.

There is no P0 finding. Product implementation remains correctly blocked; this verdict does not
approve Phase 0 exit.

## Findings

### P1 — The live review/guard packet still instructs agents to guard a deferred API

**Evidence.** The reviewer prompt lists nested `Parallel` as shipped and does not list it among
misuses (`docs/review/developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md:142-145,205-214`).
The current implementation status asks for positive nested-`Parallel` fixtures
(`docs/review/developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-18.md:109-117`).
Current authority instead permits only root `Parallel` and requires its absence from every nested,
branch, item, and leased builder (`docs/specs/17-selected-mode-capability-matrix.md:570-587,754-758`;
`openspec/changes/reshape-developer-facing-interfaces/tasks.md:27`). The same status twice calls
`OrcaCore` a small meta-package
(`docs/review/developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-18.md:79,105`), while the matrix
makes it the primary non-meta application package
(`docs/specs/17-selected-mode-capability-matrix.md:1798-1804`) and task 3.2 says the same
(`openspec/changes/reshape-developer-facing-interfaces/tasks.md:25`).

**Impact.** A guard agent can either make a correct root-only product fail its packet or reintroduce
a deliberately deferred public member. Package consumers can also encode the wrong dependency
expectation.

**Smallest normative remediation.** Supersede the dated prompt/status with a current guard request:
positive root `Parallel`, root `ForEach`, and nested `If`; negative nested `Parallel`/`ForEach`/`While`;
and “primary non-meta `OrcaCore` package.” Keep the old dated artifacts immutable and route away from
them.

### P1 — Zero-branch `Parallel` has no legality or result semantics

**Evidence.** Both roots accept an `Action<...BranchScopeBuilder...>` that may add no branch and then
expose either join (`docs/specs/17-public-authoring-contract.cs:200-202,260-262,723-775`). CP-001
requires every existing branch to have an ID and return but sets no minimum scope cardinality
(`docs/specs/08-requirements-composition.md:23-30`). Empty `ForEach` is explicitly valid and has an
empty merge (`docs/specs/08-requirements-composition.md:80-87`), making the `Parallel` silence
material. The exhaustive workflow diagnostic catalog contains no empty-parallel diagnostic
(`docs/specs/17-selected-mode-capability-matrix.md:1402-1427`).

**Impact.** Implementers and task-3.6 guards must invent whether the delegate is rejected, whether
the merge runs, and which result list it receives.

**Smallest normative remediation.** Prefer requiring at least one branch, add one stable eager or
build diagnostic, and require its task-3.6 fixture. Alternatively, explicitly make zero branches
valid and specify exactly one merge with an empty ordered list.

### P1 — Repeated `CompleteWithin` is forbidden without a rejection channel

**Evidence.** `CompleteWithin` returns the same root builder, so two calls compile
(`docs/specs/17-public-authoring-contract.cs:173-174,234-235`). The matrix says it appears at most
once (`docs/specs/17-selected-mode-capability-matrix.md:627-629`), but its exhaustive diagnostics
have no corresponding code (`docs/specs/17-selected-mode-capability-matrix.md:1402-1427`).

**Impact.** Last-wins, first-wins, eager rejection, and accumulated build error produce different
workflow deadlines. A silent choice directly affects terminality and external cleanup.

**Smallest normative remediation.** Specify one stable error and channel—prefer eager
`WorkflowDefinitionException`, analogous to repeated decorators—and add the exact misuse to task
3.9.

### P1 — Engine-mode and referenced transient-pool compatibility have no closed registration outcome

**Evidence.** One service provider selects exactly one engine mode
(`docs/specs/17-selected-mode-capability-matrix.md:1919-1930`), but the one public
`IWorkflowDefinitionRegistry` accepts all four ephemeral/durable definition families
(`docs/specs/17-selected-mode-capability-matrix.md:2088-2097`). Its closed result models only a
structural-fingerprint conflict (`docs/specs/17-selected-mode-capability-matrix.md:2067-2075`). An
ephemeral definition may reference a `TransientPoolName`
(`docs/specs/17-public-authoring-contract.cs:170-171`), while host options separately list pools
(`docs/specs/17-selected-mode-capability-matrix.md:1498-1502`), but no outcome is specified for a
missing name.

**Impact.** Wrong-mode registration compiles. A misspelled or omitted transient pool could be
ignored, fail at an arbitrary time, or run without the promised capacity protection.

**Smallest normative remediation.** Either split registries by selected mode or add a typed closed
registration/startup compatibility failure covering wrong mode and every missing referenced pool.
Assign positive and negative fixtures to task 3.7.

### P1 — Well-formed but unconfigured durable pools and invalid resize capacities are unspecified

**Evidence.** Pool definitions require positive capacity
(`docs/specs/09-requirements-management-operations.md:216-224`), while request factories validate
shape, units, and duplicate names (`docs/specs/17-selected-mode-capability-matrix.md:813-831`). A
dynamic selector can nevertheless produce a valid but unconfigured `ResourcePoolName`.
`IDurableResourcePoolManagement.GetAsync` and `ResizeAsync` expose no not-found union/exception,
and `ResizeAsync` accepts an unconstrained `int`
(`docs/specs/17-selected-mode-capability-matrix.md:1582-1595`).

**Impact.** Acquisition might park forever or mutate no known aggregate entry; management
implementations must invent unknown-pool and zero/negative-resize behavior.

**Smallest normative remediation.** Define one typed unknown-pool failure before queue mutation;
use registration/startup diagnostics when a static request can be inspected; specify `GetAsync`
and `ResizeAsync` not-found behavior; and state whether resized capacity must be positive or merely
nonnegative. Guard each boundary in tasks 3.7/3.11a/3.11d.

### P1 — Crash replay and retry-budget attempt numbering do not form a closed state machine

**Evidence.** `AttemptNumber` starts at one and increments for each invocation
(`docs/specs/17-selected-mode-capability-matrix.md:372-376`;
`docs/specs/04-requirements-core-runtime.md:201-207`). A crash after an external effect but before
commit re-runs the step (`docs/specs/16-requirements-durable-driver.md:197-204`). `maxAttempts`
includes the initial attempt, and restart may neither grant extra attempts nor reset a deadline
(`docs/specs/16-requirements-durable-driver.md:242-270`).

With `maxAttempts == 1`, post-effect/pre-commit host loss leaves three incompatible readings: replay
physical invocation as attempt 1 despite “increments per invocation”; invoke attempt 2 despite the
budget; or refuse replay despite at-least-once execution.

**Impact.** Providers can disagree about whether work is retried, whether its deadline is reused,
and whether a restart consumes policy budget. This is an external-effect identity and liveness
boundary.

**Smallest normative remediation.** Define `AttemptNumber` as a durable retry-policy attempt
coordinate rather than a count of physical CLR dispatches. Commit operation ID, attempt coordinate,
and absolute attempt deadline before dispatch; uncertain host-loss replay reuses them, and only a
committed retry transition increments them. Add pre-dispatch, post-effect/lost-response,
deadline-expired, and `maxAttempts == 1` deterministic guards to task 3.9.

### P1 — Startup agreement after persisted pool resize is ambiguous

**Evidence.** Host definitions contain `Capacity`
(`docs/specs/17-selected-mode-capability-matrix.md:1533-1545`); runtime snapshots expose mutable
`ConfiguredCapacity` (`docs/specs/17-selected-mode-capability-matrix.md:1554-1560`). First startup
creates definitions, later hosts must present identical name/capacity/review values, and later
capacity changes use `ResizeAsync`
(`docs/specs/17-selected-mode-capability-matrix.md:1750-1760`). Resize/debt must survive restart
(`docs/specs/12-acceptance-criteria.md:364-373`).

**Impact.** After create-at-N and resize-to-M, a restarting host configured with N might be rejected
against current M or might overwrite M. Configuring M might instead be rejected against the
creation record. Either interpretation breaks a normal operational journey.

**Smallest normative remediation.** Name immutable `InitialCapacity` separately from replayed
current configured capacity. Later hosts compare only the immutable creation definition; startup
never overwrites replayed current capacity. Add a task-3.11d guard: create N, resize M/create debt,
replace the host with original definition N, and prove M/debt remain unchanged.

### P1 — The supporting Kubernetes handoff releases scheduler capacity before terminal validation

**Evidence.** The sample puts `ApplyKubernetesJobOutcome` after the lease
(`docs/eks-scheduler-handoff.md:61-67`). Canonical JS-008 requires submit, `Wait`, and outcome
handling inside the lease when the Job consumes capacity for its lifetime
(`docs/specs/14-driving-scenario-eks-job-scheduler.md:138-150`). Event matching has no payload
predicate, and resumed payload is available only to the first following step
(`docs/specs/05-requirements-events-waits-timers.md:66-83`). Normal lexical completion releases
before the parent resumes (`docs/specs/17-selected-mode-capability-matrix.md:960-968`).

**Impact.** A malformed, stale, or incorrectly normalized terminal event can free scheduler
capacity before Job UID, operation identity, protection token, and true terminality are checked.

**Smallest normative remediation.** Move the apply/validation step inside the scheduler lease,
immediately after `Wait`; require it to validate the committed Job reference, UID,
`StepOperationId`, protection token, and terminal state before lexical exit.

### P1 — The management timeout requirement contradicts the exact application snapshot

**Evidence.** The management delta says application snapshots and advanced diagnostics distinguish
the workflow deadline, wait timeout, individual step-attempt deadline/outcome, retry attempt, and
terminal workflow timeout
(`openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md:102-107`).
The exact `WorkflowInstanceSnapshot` contains status, outcome, failure, and active waits but no
workflow deadline or attempt facts; only each active wait has an optional deadline
(`docs/specs/17-selected-mode-capability-matrix.md:2160-2179`).

**Impact.** Task 3.7's exact snapshot guard and task 3.9's timeout guards cannot both satisfy the
text without inventing fields or silently weakening the requirement.

**Smallest normative remediation.** State which facts belong to the ordinary application snapshot
and which belong only to advanced diagnostics, or add exact typed projections/signatures before
guards are written.

### P1 — Canonical DAG dependency failure assigns two different outcomes

**Evidence.** CP-022 says the mapper runs after all direct dependencies succeed, yet includes
failed-dependency access among `DAG_INPUT_MAPPING_INVALID` cases
(`docs/specs/08-requirements-composition.md:138-147`). CP-024 says every non-success dependency
blocks transitive dependants (`docs/specs/08-requirements-composition.md:158-167`). The matrix says
failed dependencies prevent mapper invocation and yield `DependencyBlocked`; mapping-invalid is
for opaque invalid access/projector failure
(`docs/specs/17-selected-mode-capability-matrix.md:1289-1297,1311-1315`).

**Impact.** Implementations and guards can report either node `Failed` or `DependencyBlocked`,
changing terminal snapshots and failure closure.

**Smallest normative remediation.** Remove “failed-dependency” from CP-022. State that the mapper
is not invoked and the dependent becomes `DependencyBlocked`.

### P1 — Mandatory concurrency and lease races are not assigned to exact Phase 0 guards

**Evidence.** Host/node composition and parked admitted-item counting are normative
(`openspec/changes/add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md:117-126,153-163`;
`docs/specs/12-acceptance-criteria.md:426-429`), but task 3.6 names path-token behavior without the
separate admitted-item cap (`openspec/changes/reshape-developer-facing-interfaces/tasks.md:29`).
Canonical AC-527 requires one outcome for grant versus cancellation/failure/termination
(`docs/specs/12-acceptance-criteria.md:422-425`), and the quality delta explicitly requires
cancellation-before-grant and grant/cancel races
(`openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md:172-173`),
but tasks 3.11a-3.11d do not name those winner cases
(`openspec/changes/reshape-developer-facing-interfaces/tasks.md:34-37`).

**Impact.** A guard-only implementation can legitimately finish the literal tasks while missing
capacity-accounting races and conflating parked item admission with runnable tokens.

**Smallest normative remediation.** Add deterministic task-3.6 guards for lower-of limits, parked
item admission, and ceiling-one progress. Add task-3.11a/3.11d cases for cancel-before-grant,
cancel racing the atomic grant, restart at each winner boundary, and exact no-ghost accounting.

### P1 — Task 3.10 has no exact Phase 0 seam for required unified outbox and lineage behavior

**Evidence.** AC-613 requires internal DAG starts to share the runtime outbox and AC-614 requires
`RootInstanceId`/`ParentInstanceId` navigation
(`docs/specs/12-acceptance-criteria.md:504-512`). Task 3.10 covers mapping, statuses, reattachment,
admission, and the friend bridge, but not those two behaviors
(`openspec/changes/reshape-developer-facing-interfaces/tasks.md:33`). The public DAG snapshot exposes
only `ChildInstanceId` (`docs/specs/17-selected-mode-capability-matrix.md:1186-1197`); the matrix
names the bridge but gives no exact lineage query or internal protocol declaration
(`docs/specs/17-selected-mode-capability-matrix.md:1838-1843`).

**Impact.** A Phase 0 guard author must invent internal record/query types or omit canonical
acceptance criteria.

**Smallest normative remediation.** Either explicitly defer AC-613/614 to implementation
verification and limit Phase 0 to the exact friend edge plus observable one-child/reattachment
behavior, or specify the exact internal test seam and add it to task 3.10.

### P2 — Optional nullable `End` cannot distinguish unnamed completion from invalid explicit null

**Evidence.** All four `End` signatures use `WorkflowOutcomeName? outcome = null`
(`docs/specs/17-public-authoring-contract.cs:212-217,286-291`;
`docs/specs/17-selected-mode-capability-matrix.md:399-413`). CR-008 says null/default outcome values
are rejected rather than treated as unnamed (`docs/specs/04-requirements-core-runtime.md:73-80`).
C# makes an omitted optional argument and explicit `null` indistinguishable.

**Impact.** A consumer cannot know whether `End(null)` is a valid unnamed completion or rejected
input, and guards cannot prove both claims.

**Smallest normative remediation.** Replace each optional nullable parameter with overload pairs:
one unnamed `End` and one taking a non-null `WorkflowOutcomeName`.

### P2 — Canonical CR-008 wrongly requires `End` for perpetual rollover workflows

**Evidence.** CR-008 says every workflow has exactly one root `End`
(`docs/specs/04-requirements-core-runtime.md:73-75`). The selected compiler requires one generation
terminal—`End` or unconditional durable `ContinueAsNew`
(`docs/specs/17-selected-mode-capability-matrix.md:620-625,1375-1376,1410`).

**Impact.** A canonical requirement rejects an explicitly selected valid definition family.

**Smallest normative remediation.** Distinguish ordinarily completing workflows from perpetual
rollover definitions and require exactly one selected generation-terminal form.

### P2 — Stop-confirmation rows are exhaustive individually but have no precedence

**Evidence.** The matrix lists lifecycle-based results and “confirmation ID already bound to
another token” as separate rows (`docs/specs/17-selected-mode-capability-matrix.md:1030-1038`). A
live, already-confirmed, unknown, or normally released token can be supplied with an ID already
bound elsewhere and satisfy two rows. Task 3.11c demands an exact exhaustive matrix
(`openspec/changes/reshape-developer-facing-interfaces/tasks.md:36`).

**Impact.** Providers can return different statuses for the same harmless-but-important stale
proof. Capacity is unchanged, so this is P2 rather than P1.

**Smallest normative remediation.** Define total evaluation order. Prefer checking whether the
confirmation ID is bound to another token first, then evaluating token lifecycle. Add the three
overlap cross-products to task 3.11c.

### P2 — Supporting package/routing prose makes two superseded promises

**Evidence.** The Phase 4b README's routing banner is conspicuous, but its later “current
disposition” says `OrcaCore.Dag` owns visualization
(`docs/implementation/phases/phase-4b-dag-external-jobs/README.md:40-45`). V1 expressly makes no
visualization promise (`docs/specs/17-selected-mode-capability-matrix.md:48,1812`). The canonical
glossary also says `OrcaCore.Dag` depends on workflow “runtime seams”
(`docs/specs/03-domain-model-and-glossary.md:322-326`), while the exact graph permits
`OrcaCore.Dag -> OrcaCore` and assigns the durable bridge only to `OrcaCore.Dag.Hosting`
(`docs/specs/17-selected-mode-capability-matrix.md:1812-1813,1833-1843`).

**Impact.** Consumers and project authors can add an unapproved visualization surface or a
forbidden package edge despite otherwise-correct routing banners.

**Smallest normative remediation.** Replace “visualization” with immutable execution-plan
ownership and explicitly state no v1 visualization surface. Say `OrcaCore.Dag` consumes only
public workflow contracts; only `OrcaCore.Dag.Hosting` consumes the internal durable bridge.

### P3 — Two canonical descriptions no longer match their exact inventories

**Evidence.** Canonical decorator prose says a decorator is attached “immediately before” a
business step (`docs/specs/03-domain-model-and-glossary.md:83-87`;
`docs/specs/04-requirements-core-runtime.md:53-57`), while the fluent call decorates the already
authored immediately preceding step (`docs/specs/17-selected-mode-capability-matrix.md:594-597`).
The quality delta says “five role-specific hosting entry points”
(`openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md:269-274`),
but the exact surface has six: ephemeral engine, durable engine, durable ingress, in-memory
provider, PostgreSQL provider, and DAG (`docs/specs/17-selected-mode-capability-matrix.md:1857-1916`).

**Impact.** These are documentation/guard-count traps rather than semantic blockers.

**Smallest normative remediation.** Say “attached to the immediately preceding business step” and
“all six exact entry points” (or reference the exact allowlist without a count).

## Explicit non-findings

- Ephemeral and durable staged builders are symmetric where intended. Only ephemeral lambdas and
  transient pools versus durable leasing/continue-as-new differ by mode; completion builders expose
  only `Build`/`TryBuild` (`docs/specs/17-public-authoring-contract.cs:18-85,149-292`).
- The current root-only fan-out decision is internally coherent in the matrix, companion, OpenSpec
  deltas, and tasks. The defect is stale review/guard routing, not the decision itself.
- Typed resultless/resultful definitions, references, handles, fixed output, and fixed-codec detached
  state avoid phantom state types and preserve cast-free consumer flow.
- Strong values are immutable factory/parser-controlled reference values; non-null `default(struct)`
  bypasses are gone, and public boundaries still reject null
  (`docs/specs/17-selected-mode-capability-matrix.md:323-346`).
- Ordinary timeout fencing, physical throttle/transient-slot retention for token-ignoring late
  bodies, and stricter no-overlap leased retry are explicitly distinguished
  (`docs/specs/17-selected-mode-capability-matrix.md:360-380`).
- Durable `ForEach` selection commit, index identity, empty merge, restart reuse, ordered outcomes,
  ancestor merge suppression, path-token release, admitted-item accounting, and ceiling-one
  progress are coherent. The finding is missing guard traceability, not missing runtime semantics.
- Leasing correctly has no author TTL, renewal, holder ID, force release, or time-only reclaim. The
  exact lexical lifecycle, capacity-reserving quarantine, trusted generic confirmation, four-stage
  reservation protocol, and provider-partition aggregate are otherwise coherent.
- Detached quarantine blocks `ContinueAsNew` across the whole generation
  (`docs/specs/09-requirements-management-operations.md:299-304`;
  `docs/specs/16-requirements-durable-driver.md:675-677`).
- `OrcaCore.Dag` is typed and outward-only; callers do not own progression sets, public child
  orchestration is absent, parked child instances still count against `MaxConcurrentNodes`, and
  independent nodes may progress after another branch fails.
- The exact package graph is outward-only and the ordinary application surface does not expose
  provider protocol or companion SDK types. A scan of `.csproj`, props, and targets under `src`,
  `tests`, `samples`, and `benchmarks` found no Kubernetes or AWS SDK reference.
- Historical Phase 4b/task routing banners are conspicuous and point to current authority. The one
  visualization sentence identified above is later body text, not a banner failure.
- No planning artifact claims an executed expected-red count. Task 3.12 correctly requires actual
  pass/expected-red/blocker counts.

## Dimension scores

| Dimension | Score | Rationale |
|---|---:|---|
| Consistency | 6.5/10 | Exact matrix/companion alignment is strong; canonical and guard-status drift remains material. |
| Comprehensiveness | 7.5/10 | Most public/runtime states are closed; the findings are concentrated at boundary cross-products and crash/startup transitions. |
| Developer orientation | 8.0/10 | Staging, typed handles, role-specific hosting, and absence-by-type give a clear ordinary journey. |
| Misuse resistance | 6.5/10 | Many illegal members are statically absent, but mode/pool registration, zero fan-out, deadline repetition, and null outcome remain unresolved. |
| Durable safety | 7.0/10 | Fencing, lexical leases, quarantine, governance serialization, and DAG recovery are strong; attempt replay and resize restart need closure. |
| Package isolation | 9.0/10 | Exact outward-only package ownership is strong; only two supporting prose statements drift. |

## Five consumer programs

The examples below use only proposed public shapes. Application DTOs and named `IStep<TState>`
implementations are abbreviated, but every OrcaCore call is intended to match document 17 and its
companion.

### 1. Ephemeral typed input/output, lambda, transient pool, outcomes, and deadlines

```csharp
var inlineIo = TransientPoolName.Create("inline-io");

var definition =
    Workflow.Ephemeral<EphemeralState>(
            DefinitionId.Parse("11111111-1111-1111-1111-111111111111"),
            DefinitionVersion.Initial)
        .Init<EphemeralInput>(input => new EphemeralState(input.Seed, input.FailRight, 0, 0))
        .Then(static async (context, cancellationToken) =>
        {
            await Task.Delay(1, cancellationToken);
            context.ReplaceState(context.State with { Value = context.State.Value + 1 });
        })
        .WithTransientPool(inlineIo)
        .WithStepTimeout(TimeSpan.FromSeconds(2))
        .Parallel<BranchValue>(branches =>
        {
            branches.Branch(
                AuthoredBranchId.Create("left"),
                root => new BranchState(root.Value.Value, fail: false),
                branch => branch
                    .Then<RunBranch>()
                    .Return(state => new BranchValue(state.Value.Value)));

            branches.Branch(
                AuthoredBranchId.Create("right"),
                root => new BranchState(root.Value.Value, root.Value.FailRight),
                branch => branch
                    .Then<RunBranch>()
                    .Return(state => new BranchValue(state.Value.Value)));
        })
        .WhenAllOutcomes((root, outcomes) => root.Value with
        {
            Successes = outcomes.Count(x => x is BranchOutcome<BranchValue>.Succeeded),
            Failures = outcomes.Count(x => x is BranchOutcome<BranchValue>.Failed)
        })
        .If(
            state => state.Value.Failures != 0,
            rejected => rejected.Then<RejectSummary>())
        .CompleteWithin(TimeSpan.FromSeconds(30))
        .End(
            state => new EphemeralOutput(state.Value.Value, state.Value.Successes),
            WorkflowOutcomeName.Create("accepted"))
        .Build();

services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
{
    StructuredExecution = new StructuredExecutionHostOptions
    {
        MaxConcurrentExecutionPathsPerInstance = 4,
        StepThrottles = []
    },
    TransientPools = [TransientPoolDefinition.Create(inlineIo, capacity: 2)]
});

var typedDefinition = registry.Register(definition).GetHandleOrThrow();
var start = await typedDefinition.StartOrGetAsync(
    new EphemeralInput(10, failRight: false),
    StartIdempotencyKey.Create("ephemeral-sample-1"),
    cancellationToken);
var output = await start.WaitForOutputAsync(cancellationToken);
```

Friction: generic flow is cast-free and the detached `ReplaceState` journey is clear. A missing
`inline-io` host definition has no specified result (P1 above). An author wanting all diagnostics
uses `TryBuild`; `Build` throws the same ordered set.

### 2. Durable bounded `ForEach`, outcome summary, restart, and typed output

```csharp
var definition =
    Workflow.Durable<BatchState>(
            DefinitionId.Parse("22222222-2222-2222-2222-222222222222"),
            DefinitionVersion.Initial)
        .Init<BatchInput>(input => new BatchState(input.Items, []))
        .CompleteWithin(TimeSpan.FromHours(1))
        .ForEach<WorkItem, ItemState, ItemResult>(
            state => state.Value.Items,
            ForEachOptions.Create(maxItems: 100, maxConcurrency: 4),
            item => new ItemState(item.Index, item.Item),
            item => item
                .Then<PrepareItem>()
                    .WithRetry(2, TimeSpan.FromSeconds(1))
                .Wait(
                    EventName.Create("item-ready"),
                    state => CorrelationId.Create($"item:{state.Value.Item.Id}"))
                .Then<FinishItem>()
                .Return(state => new ItemResult(state.Value.Index, state.Value.Item.Id)))
        .WhenAllOutcomes((root, outcomes) => root.Value with
        {
            Summary = outcomes.Select(ToItemSummary).ToArray()
        })
        .End(
            state => new BatchOutput(state.Value.Summary),
            WorkflowOutcomeName.Create("summarized"))
        .Build();
```

Host A registers the PostgreSQL provider and durable engine, registers this definition before host
start, starts an instance, and retains its `InstanceId`. After host/process replacement, host B
uses the same provider, options, and definition/version, registers before progression, and calls
the new typed definition handle's `GetInstanceAsync(instanceId)`. Events are redelivered with
stable envelopes; successful and failed item outcomes remain index-ordered, unfinished items are
re-admitted under the host/node lower bound, the merge commits once, and
`WaitForOutputAsync` returns `BatchOutput` without a cast.

Friction: “restart” correctly means host replacement/reopen, not a public failed-instance restart.
The state/identity journey is complete. The physical-invocation versus durable-attempt ambiguity
identified above still affects a crash during `PrepareItem` or `FinishItem`.

### 3. Short database lease and whole-Job scheduler lease

```csharp
var databaseRequest = ResourceLeaseRequest.Create(
    ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
var schedulerRequest = ResourceLeaseRequest.Create(
    ResourceLeaseRequirement.Require(ResourcePoolName.Create("scheduler-capacity")));

var definition =
    Workflow.Durable<JobState>(definitionId, DefinitionVersion.Initial)
        .Init<JobInput>(JobState.Create)
        .CompleteWithin(TimeSpan.FromHours(2))

        // Short database use: exact release commits before the following wait.
        .AcquireResources(databaseRequest, leased => leased
            .Then<LoadSubmissionMetadata>())
        .Wait(SchedulerEvents.Approved, state => state.Value.ApprovalCorrelation)

        // Scheduler capacity is held for create/observe, external lifetime, and validation.
        .AcquireResources(schedulerRequest, leased => leased
            .Then<CreateOrObserveKubernetesJob>()
                .WithStepTimeout(TimeSpan.FromSeconds(30))
                .WithRetry(3, TimeSpan.FromSeconds(2))
            .Wait(
                SchedulerEvents.JobTerminal,
                state => state.Value.JobCorrelation,
                TimeSpan.FromHours(1))
            .Then<ApplyValidatedKubernetesTerminalReport>())

        .End<JobOutput>(state => state.Value.Output)
        .Build();
```

Friction: the API cleanly distinguishes a short protected operation from capacity held across a
cold wait. The supporting handoff currently puts terminal validation outside the second scope; the
corrected shape above is required to avoid premature release.

### 4. Typed heterogeneous DAG with failed dependency and independent work

```csharp
DurableWorkflowDefinition<ValidateInput, ValidatedJob> validateWorkflow = BuildValidate();
DurableWorkflowDefinition<SubmitInput, JobReceipt> submitWorkflow = BuildSubmit();
DurableWorkflowDefinition<ObserveInput, JobOutcome> observeWorkflow = BuildObserve();
DurableWorkflowDefinition<AuditInput, AuditReceipt> auditWorkflow = BuildAudit();

var dag = Dag.Define<SchedulerRunInput>(DefinitionId.New(), DefinitionVersion.Initial);

var validate = dag
    .Node<ValidateInput, ValidatedJob>(
        DagNodeId.Create("validate"), validateWorkflow.Reference)
    .MapInput(context => new ValidateInput(context.RunInput.SpecUri));

var submit = dag
    .Node<SubmitInput, JobReceipt>(
        DagNodeId.Create("submit"), submitWorkflow.Reference)
    .DependsOn(validate)
    .MapInput(context => new SubmitInput(
        context.RunInput.Cluster,
        context.OutputOf(validate)));

var observe = dag
    .Node<ObserveInput, JobOutcome>(
        DagNodeId.Create("observe"), observeWorkflow.Reference)
    .DependsOn(submit)
    .MapInput(context => new ObserveInput(context.OutputOf(submit)));

var audit = dag
    .Node<AuditInput, AuditReceipt>(
        DagNodeId.Create("independent-audit"), auditWorkflow.Reference)
    .MapInput(context => new AuditInput(context.RunInput.AuditTarget));

var plan = dag.Build();

services.AddOrcaCorePostgreSqlDurableProvider(
    new PostgreSqlDurableProviderOptions(connectionString, "orca"));
services.AddOrcaCoreDurableEngine(durableOptions);
services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 2 });

var definition = dagRegistry.Register(plan).GetHandleOrThrow();
var start = await definition.StartOrGetAsync(
    new SchedulerRunInput(specUri, cluster, auditTarget),
    StartIdempotencyKey.Create(runKey),
    cancellationToken);
var terminal = await start.GetHandleOrThrow().WaitForTerminalAsync(cancellationToken);
```

If `validate` fails, its mapper has already committed input and its child is `Failed`; the mapper
for `submit` is not invoked, `submit` and `observe` become `DependencyBlocked`, and
`independent-audit` remains eligible. The run becomes `Failed` only after no node can progress.

Friction: direct-dependency type flow is good and cast-free. `MapInput` returns the node reference,
so the root `dag` variable must be retained. Failure policy is intentionally fixed. CP-022's stale
failed-dependency mapping sentence must be removed before the behavior guard is written.

### 5. Companion Kubernetes Job create/observe, event, cancellation, and stop proof

The named companion step uses only generic OrcaCore identities at its boundary:

```csharp
public sealed class CreateOrObserveKubernetesJob : IStep<JobState>
{
    public async ValueTask<StepResult> ExecuteAsync(
        StepContext<JobState> context,
        CancellationToken cancellationToken)
    {
        var operationId = context.Execution.OperationId;
        var protectionToken = context.ResourceLease?.ProtectionToken
            ?? throw new InvalidOperationException("A scheduler lease is required.");

        var job = await kubernetes.CreateOrObserveAsync(
            KubernetesCreateRequest.From(context.State, operationId, protectionToken),
            cancellationToken);

        context.ReplaceState(context.State with
        {
            Job = new JobReference(job.Namespace, job.Name, job.Uid, operationId, protectionToken)
        });
        return new StepResult.Completed();
    }
}
```

The watcher normalizes a small `JobTerminalReport`, creates one stable
`WorkflowEvent<JobTerminalReport>` with a stable `EventId`, and delivers to the exact instance or
correlation route. `Duplicate` is success; `EventConflict` is an integrity failure; a non-consuming
`NoActiveWait` is retried later with the identical envelope. The first resumed step validates UID,
operation ID, protection token, and terminality while still in the lexical lease.

On cancellation/deadline, a trusted companion reconciler enumerates
`IDurableResourceLeaseDiagnostics`, stops every token-labelled Job/Pod using UID preconditions,
proves terminal/absent/fenced state, and calls
`ConfirmProtectedWorkStoppedAsync(protectionToken, stableConfirmationId)`. It never treats elapsed
time, workflow terminal status, deletion acknowledgement, or a label alone as proof. Kubernetes
client/DTO types remain in the outward companion; OrcaCore sees only its own step, event, and lease
contracts.

Friction: the public contracts are sufficient. Arbitrary adapter code can still misuse
`AttemptNumber` or lie about stop proof; the matrix correctly declares that an integration trust
boundary and requires companion/provider certification rather than claiming CLR enforcement.

## Adversarial misuse classification

“Silently accepted (valid)” below means the scenario is intentionally legal, not an unguarded
misuse.

| Attempt | Classification and expected boundary |
|---|---|
| Build before `End`/`ContinueAsNew` | **Compile-impossible**: body builders expose no build member. |
| Output type inconsistent with `DurableWorkflowRef<TInput,TOutput>` | **Compile-impossible** through invariant generic type flow. |
| Null/default/invalid definition ID or version | **Fluent-call rejection** at the workflow factory/boundary; text parsing/construction rejects before it reaches the builder. |
| Same identity/version with changed structural fingerprint | **Registration/startup diagnostic** through typed `Conflict` or `GetHandleOrThrow`. |
| Durable lambda | **Compile-impossible**: durable builders expose only `Then<TStep>()`. |
| Ephemeral lambda captures unsafe mutable state | **Silently accepted** at runtime; explicitly an author obligation because CLR purity is not claimed. |
| Nested `Parallel`, `While`, or `ForEach` | **Compile-impossible** on approved builders; **build diagnostic** `SFE-AUTH-CAP-001` for a hand-built/stale graph. |
| `WhenFirst`, Saga, `RunExternalJob`, `RunChildren`, `WaitLong`, or `Yield` | **Compile-impossible** because the members/types are absent. |
| Retry/step-timeout immediately after `Wait` or in an empty branch | **Fluent-call rejection** with `SFE-AUTH-DECORATOR-001`. |
| Retry/step-timeout after `End` | **Compile-impossible** on a completion builder. |
| Retry/step-timeout on a join builder | **Compile-impossible**; the join exposes only `WhenAll*`. After the join call it would decorate the join/structural node and must be **fluent-call rejection**. |
| Duplicate retry/step-timeout/transient decorator on one step | **Fluent-call rejection** with `SFE-AUTH-DECORATOR-001`. |
| Duplicate `CompleteWithin` | **Unspecified**; P1 finding above. |
| Zero-branch `Parallel` | **Unspecified**; P1 finding above. |
| Branch/item with no reachable `Return` | **Build diagnostic** `SFE-AUTH-BRANCH-002`. |
| `WhenAll` with one failed branch/item | **Runtime defense/defined runtime failure** after all siblings terminate; merge skipped. |
| `WhenAllOutcomes` followed by an `If` whose named step rejects | **Runtime defense/defined business failure**; the outcome merge succeeds and the following step fails. |
| Unbounded `ForEach` | **Compile-impossible** because `ForEachOptions` is mandatory. |
| Non-positive `ForEachOptions` | **Fluent-call/factory rejection**. |
| Selected list over `MaxItems` | **Runtime defense** before partial copying/admission. |
| Empty selected list | **Silently accepted (valid)**; one merge receives an empty list. |
| Host ceiling below/above node cap | **Runtime defense**: effective cap is the lower value. |
| Parked item versus runnable path token | **Runtime defense**: parked item keeps the admitted-item slot but releases the path token. |
| Host ceiling 1 fan-out | **Runtime defense/valid progress**: parent releases before child/item admission and reacquires for merge. |
| Reuse `EventId` with changed normalized content | **Runtime defense**: `EventConflict`. |
| Two active waits for the same `(DefinitionId, EventName, CorrelationId)` | **Runtime defense**: `AmbiguousWaitRegistrationException`; no event is consumed. |
| Definition-targeted event fanout | **Compile-impossible**: no route exists. |
| Use `AttemptNumber` as external idempotency identity | **Silently accepted by arbitrary adapter code**; a required provider/companion certification failure, not inspectable by core. |
| Reuse one operation ID for another loop/item/branch/generation | **Compile-impossible for author-created IDs**; **silently possible in arbitrary cached adapter code** and must fail integration certification. Runtime-created contexts themselves mint distinct IDs. |
| Point/fiber-lifetime acquisition | **Compile-impossible**: only scoped `AcquireResources` exists. |
| Empty lease request, duplicate pool, non-positive units | **Fluent-call/factory rejection** before provider mutation. |
| Transient/durable pool-name swap | **Compile-impossible** through distinct strong types. |
| Author TTL, renewal, holder ID | **Compile-impossible**: no member exists. |
| Valid but unconfigured durable pool | **Unspecified**; P1 finding above. |
| Nested acquisition under a live ancestor | **Compile-impossible** on leased builders; **build diagnostic** `SFE-AUTH-LEASE-001` and **runtime defense** `SFE-RUN-002` for stale plans. |
| Sequential root-loop lexical scope | **Silently accepted (valid)** after exact previous release. |
| Independent sibling scopes | **Silently accepted (valid)**; only the requesting fiber parks. |
| `ContinueAsNew` inside leased body | **Compile-impossible**; **runtime defense** `SFE-RUN-001` for stale plans or outstanding detached quarantine. |
| Release quarantine from elapsed time, workflow status, deletion acknowledgement, or label | **Compile-impossible through ordinary APIs**; trusted confirmation returns only after adapter-owned causal proof. A lying reconciler is a certified trust-boundary violation. |
| Mismatched protection token, stale/bound confirmation ID | **Runtime defense** through `TokenNotFound`, `AlreadyConfirmed`, or `ConfirmationConflict`; overlap precedence remains P2. |
| Force release | **Compile-impossible**: no operation exists. |
| DAG `OutputOf` wrong-typed/resultless dependency | **Compile-impossible**. |
| DAG `OutputOf` undeclared/non-direct/foreign dependency hidden in mapper | **Runtime defense** `DAG_INPUT_MAPPING_INVALID` before input commit/child start. |
| `OutputOf` a failed dependency | Mapper is not invoked; dependent is **runtime** `DependencyBlocked`; canonical CP-022 needs correction. |
| Caller-owned DAG ready/completed sets | **Compile-impossible**: no public progression API. |
| Public child-workflow DAG node/`RunChild` | **Compile-impossible**: no public member. |
| OrcaCore/`OrcaCore.Dag` reference to Kubernetes, AWS, companion, or Job DTO | **Architecture/package guard failure**; no allowed package edge. |
| Catch-all `AddOrcaCore` or separate hosted-service toggle | **Compile-impossible**: absent APIs. |
| Codec replacement | **Compile-impossible**: no hook. |
| Transient pools in durable options / durable pools in ephemeral options | **Compile-impossible** through split option types. |
| Host-wide advancement/general-body ceiling or fail-fast/capacity-wait-timeout admission | **Compile-impossible**: absent options. |
| Multiple named transient pools on one step | **Fluent-call rejection** as repeated pool decorator. |
| Base/assignable/category/global step throttle | **Compile-impossible**: only exact `For<TStep>` exists. |
| Pool decoration binds the following step | **Runtime behavior guard failure**; normative binding is the immediately preceding eligible step. |
| Custom transient-governance SPI | **Compile-impossible**: no SPI. |
| DAG registration without durable-engine role | **Registration/startup diagnostic**. |
| Mixed durable and ephemeral engine roles, or durable ingress beside durable engine | **Registration/startup diagnostic**. |
| Wrong-mode definition or missing transient pool | Compiles; result is **unspecified**, P1 finding above. |

## Lifecycle analysis

### Operation identity and detached attempts

The logical occurrence coordinate is correct: each step visit gets one runtime-created
`StepOperationId`; retry, timeout reconciliation, replay, expected-version conflict, and competing
drivers preserve it; loop, item, branch, and generation occurrences get distinct IDs. Every
invocation receives a fixed-codec-detached copy; only the winning completion commits
`ReplaceState`, and a late fenced body cannot commit. Ordinary late bodies retain physical
throttle/transient slots until return, while a leased same-process retry waits for its predecessor.

The open transition is host-loss replay of an uncertain physical dispatch. “Invocation” and
durable retry-policy “attempt” must be separated as described in the P1 finding. Once that is done,
pre-dispatch identity/deadline commit and post-dispatch lost-response guards can deterministically
prove the at-least-once boundary without minting extra retry budget.

### Deadlines, waits, and events

`CompleteWithin` is start-relative, persisted, survives host replacement and every
continue-as-new generation, covers admission/retry/delay/wait/lease queue/cleanup, suppresses
merges, and terminalizes `TimedOut`. Step timeout applies to one attempt and wait timeout applies to
one structural wait; neither proves external work stopped. Event-name/correlation matching,
per-instance `EventId` dedup, and event/timer/workflow-deadline commit races are structurally
defined. The remaining public-projection problem is that the management delta promises deadline
and attempt distinctions that the exact ordinary snapshot cannot represent. Duplicate
`CompleteWithin` also needs one rejection transition.

### Durable `ForEach`

The selector runs over detached parent state, produces a finite list, validates/copies/codec-checks
it, and commits it before first item admission. Replay reuses the committed snapshot. Stable item
identity is scope occurrence plus index; partial terminal outcomes survive restart; unfinished
items are re-admitted. Empty selection merges once with an empty list. `WhenAll` waits for all and
skips merge on failure; `WhenAllOutcomes` produces ordered success/failure data; ancestor terminal
commit suppresses either merge.

The parent releases its runnable token before admission and reacquires for merge. A parked item
releases that token but remains admitted against the node cap; effective cap is the lower of node
and host. These semantics are coherent. Task 3.6 must name the separate admitted-item and
ceiling-one assertions so they are not lost in a generic “path-token” fixture.

### Durable leasing and governance

One normalized nonempty request commits before provider mutation and is granted atomically. Only
the requesting fiber parks. The lexical lifecycle is
`Queued -> PendingCommit -> Held -> ReviewMarked -> AmbiguousHeld -> Quarantined -> Released`,
with allowed skips but no reversal or successor ticket under one obligation. Exact
operation/token/tickets/units/provider generation survive recovery. Normal/definite exit releases
before parent visibility; ambiguous exit transfers to capacity-reserving quarantine before
progress. Time only marks for review. Trusted causal stop/fence confirmation releases once; no
ordinary force path exists.

One serialized provider-partition aggregate, expected-version whole-batch append, FIFO atomic
multi-pool grants, four persisted handoff stages, reconciliation, tombstones, and resize debt form
a strong base. Remaining open transitions are valid-but-unknown pools, cancel/grant winner guards,
confirmation-status precedence, and initial-versus-current capacity on restart after resize. The
supporting EKS sample must keep terminal validation inside the lease.

### DAG

A mapper can read immutable run input and committed output from declared direct resultful
dependencies. It runs only after those dependencies succeed; valid normalized input commits before
one idempotent internal child start and is reused after restart. A failed child blocks transitive
dependants, independent nodes continue, parked children retain DAG admission, and reattachment uses
the stable child instance. Callers neither pump progression nor see public child orchestration.

The matrix supplies a coherent runtime state machine, but CP-022 must stop classifying a failed
dependency as mapping-invalid. Phase 0 also needs an explicit disposition for unified internal
outbox and root/parent lineage coverage: either an exact internal test seam or a declared later
implementation-verification boundary.

## Phase 0 guard-coverage matrix

| Task | Executable seam required by current planning | Assessment |
|---|---|---|
| 3.1 | Exact reflection/source baselines, diagnostics, namespaces/assemblies, tier and friend-edge guards | **Covered by task text** |
| 3.2 | Local-feed package-only consumers and exact direct/transitive graph | **Blocked by stale status** calling `OrcaCore` a meta-package; task text itself is correct |
| 3.3 | Provider-author/custom-host consumer, governance store/stream validation, forbidden edges | **Covered by task text** |
| 3.4 | Positive/negative compile fixtures and `Build`/`TryBuild` parity | **Blocked** by stale positive nested-`Parallel` instruction and unspecified empty `Parallel` |
| 3.5 | Strong values, fixed codec, fingerprints, detached state, typed completion/projection opacity | **Covered by task text** |
| 3.6 | Join/fan-out outcomes, restart, merge suppression, path-token reference model | **Partial**: add empty-`Parallel` disposition and separate host/node/parked-item/ceiling-one guards |
| 3.7 | Registry/facade/event/hosting/options/reflection/startup fixtures | **Blocked** by wrong-mode/missing-pool outcomes and ordinary snapshot contradiction |
| 3.8 | Clean application golden paths, event routes/dedup/conflict, typed output/reopen | **Covered by task text** |
| 3.9 | Retry/deadline/crash/competing-driver and occurrence identity guards | **Blocked** by duplicate `CompleteWithin` and physical-dispatch versus durable-attempt replay ambiguity |
| 3.10 | DAG compile/runtime/package/bridge fixtures | **Blocked** by CP-022 contradiction and missing Phase 0 disposition for unified outbox/lineage |
| 3.11a | Lease authoring, request replay, admission, ancestry | **Partial**: add unknown pool and cancellation-before-grant cases |
| 3.11b | Retry, definite exit, ambiguity, quarantine-before-progress | **Covered by task text**; corrected EKS journey is supporting evidence |
| 3.11c | Diagnostics, exhaustive confirmation, reconciliation/certification | **Blocked** until confirmation-result precedence is total |
| 3.11d | Governance store/accounting, four crash barriers, resize/debt | **Blocked** by post-resize restart semantics; add grant/cancel winner boundaries |
| 3.12 | Execute all lanes, record exact pass/expected-red/blocker counts, independent re-review | **Gate wording is correct, but cannot approve/run as the final readiness gate until the blocked/partial rows are amended** |

No expected-red count is asserted. Timing/race cases can be deterministic if implemented against
persisted barriers, controlled `TimeProvider`, expected-version conflicts, and explicitly ordered
winner commits; no wall-clock race should be accepted as a Phase 0 proof.

## Does an unresolved decision block guard implementation?

**Yes.** Guard implementation is blocked by these unresolved decisions, not merely by stale source:

1. zero-branch `Parallel` legality and diagnostic/merge result;
2. duplicate `CompleteWithin` rejection channel;
3. engine-mode and static/dynamic pool-reference compatibility outcomes;
4. unknown durable pool and resize-capacity boundaries;
5. durable attempt coordinate across uncertain host-loss replay;
6. initial versus current pool capacity at post-resize startup;
7. ordinary versus advanced timeout/attempt projection ownership;
8. total confirmation-result precedence;
9. Phase 0 internal seam or deferral for unified DAG outbox and lineage;
10. correction of CP-022 plus the stale nested-`Parallel`/meta-package guard instructions.

These are small, local amendments. They do not require reopening the selected product shape.

## Validation performed

From repository root:

```text
openspec.cmd validate reshape-developer-facing-interfaces --strict
  PASS: Change 'reshape-developer-facing-interfaces' is valid

openspec.cmd validate add-runtime-concurrency-limits --strict
  PASS: Change 'add-runtime-concurrency-limits' is valid

git diff --check
  PASS: exit 0; line-ending conversion warnings only, no whitespace errors
```

No source or stale guard build was treated as planning evidence. No input file was edited.
