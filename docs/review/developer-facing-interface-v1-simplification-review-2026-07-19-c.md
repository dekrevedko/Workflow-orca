# Independent review: OrcaCore v1 planning contract and Phase 0 guard readiness

**Date:** 2026-07-19  
**Reviewer role:** senior .NET public-API, durable-runtime, and workflow-authoring reviewer  
**Prompt:** [`developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md`](developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md)  
**Output file:** `developer-facing-interface-v1-simplification-review-2026-07-19-c.md`  
**Note:** This review is one of several independent reviews run in parallel; it does not coordinate
findings with peer reviewers and judges only the files listed in the prompt.

This is a planning-contract review. It does not claim that product source or existing Phase 0
guards implement the proposal.

---

## 1. Verdicts

| Gate | Verdict |
|---|---|
| **Planning contract** | **APPROVE WITH CHANGES** |
| **Guard-retarget readiness** | **NOT READY** for a guard-only packet covering tasks 3.1–3.10, 3.11a–3.11d, and 3.12 |

The first-release surface is largely coherent, signature-complete for the journeys that matter,
and correctly deletes rather than aliases superseded APIs. A small set of normative
contradictions and underspecified guard seams would force a guard-only agent to invent or guess
behavior. Fix those before retargeting.

Planning approval does **not** approve Phase 0 exit or product implementation.

---

## 2. Findings (severity-ordered)

### P1-1 — Durable lease availability contradicts itself inside document 17

**Evidence**

- Capability row limits leasing to “Durable root and durable branch/item bodies”:
  `docs/specs/17-selected-mode-capability-matrix.md:43`.
- Availability table and exact declarations permit `AcquireResources` on durable nested
  conditional/loop builders: `17-selected-mode-capability-matrix.md:533`, `:762-768`;
  `docs/specs/17-public-authoring-contract.cs:379-387`.
- Reshape authoring delta requires conditional leasing:
  `openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md:182-183`.
- Reshape design still restates the narrower row:
  `openspec/changes/reshape-developer-facing-interfaces/design.md:83`.
- Sequential root-`While` lease scopes are explicitly legal
  (`17-selected-mode-capability-matrix.md:835-836`; amendment §5), which **requires** nested-builder
  acquisition.

**Impact**

A guard agent can equally require or reject `AcquireResources` inside a durable `While`/`If` body
and still cite document 17. Tasks 3.4 and 3.11a cannot choose a correct compile fixture.

**Smallest remediation**

Amend matrix row 17.1 and `design.md` Decision 3 to match the availability table and companion:
durable root, durable nested conditional/loop (when no lease is active), and durable branch/item
bodies. Keep leased builders omitting nested acquisition.

---

### P1-2 — Amendment claims fingerprint conflict for opaque code; document 17 forbids that

**Evidence**

- Amendment: changing request construction / behavior-affecting selectors under the same
  identity/version “is a fingerprint conflict”
  (`docs/review/developer-facing-interface-v1-simplification-amendment-2026-07-18.md:110-112`).
- Document 17: fingerprint excludes selector/projector/merge/output delegates, step
  construction/configuration, DAG mapping, and external-request construction; “the version bump
  is the sole v1 contract for opaque code changes”
  (`docs/specs/17-selected-mode-capability-matrix.md:402-409`).
- OpenSpec design and task 3.5 correctly use the version-bump rule
  (`openspec/changes/reshape-developer-facing-interfaces/design.md:234-238`; `tasks.md:28`).

**Impact**

A guard written from the amendment would assert an impossible detectable conflict and fail
honest structural-only fingerprint fixtures. External create-or-observe adapters could be
misled into expecting automatic detection of changed request construction under a reused
`DefinitionVersion`.

**Smallest remediation**

Rewrite amendment §3 to match document 17: same-version opaque-code changes are unsupported and
must bump `DefinitionVersion`; they are not fingerprint conflicts.

---

### P1-3 — Task 3.11d still requires undefined / non-observable accounting labels

**Evidence**

- Task 3.11d requires “ownership-equal facts, isolated restoration, contended
  conservation/direct transfer”
  (`openspec/changes/reshape-developer-facing-interfaces/tasks.md:37`).
- Quality delta repeats the same labels without setup/observable seams
  (`openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md:117`).
- Document 17 does define nearby concrete rules (isolated no-waiter/no-resize numeric
  restoration; release may transfer to a waiter; exact ticket/obligation matching) at
  `17-selected-mode-capability-matrix.md:1410-1432`, but does not define the task’s shorthand
  phrases as named invariants.

**Impact**

A guard-only agent must invent test contracts for those three phrases, which the prompt forbids.

**Smallest remediation**

Replace the three labels in task 3.11d (and the quality delta) with named assertions that cite
document 17 observables: ticket/obligation identity fields, the isolated no-waiter/no-resize
restoration case, and a release-to-waiter reservation transition with stated before/after
counts.

---

### P1-4 — Reviewed hosting integration plan still requires catch-all `AddOrcaCore`

**Evidence**

- `docs/review/integration-tests/01-hosting-and-hosted-services.md` has no superseded banner.
- Existing coverage and missing scenarios still prescribe `AddOrcaCore` /
  `AddOrcaCoreHostedServices`: lines 10, 64–71.
- Document 17 forbids catch-all registration and hosted-service toggles
  (`17-selected-mode-capability-matrix.md:1481-1532`).

**Impact**

This file is in the mechanical review packet. An implementer or later guard author can treat it
as current and restore deleted hosting APIs.

**Smallest remediation**

Add a conspicuous historical/superseded banner routing to document 17 role-specific entry
points, or rewrite the scenarios to `AddOrcaCoreEphemeralEngine` /
`AddOrcaCoreDurableEngine` / `AddOrcaCoreDurableEventIngress` /
`AddOrcaCoreInMemoryDurableProvider` / `AddOrcaCoreDag`.

---

### P1-5 — Acceptance still requires “bulk retrieval” while v1 management forbids bulk query APIs

**Evidence**

- `AC-115` requires retrieving instances “by IDs or by filter” as one bulk operation
  (`docs/specs/12-acceptance-criteria.md:166-168`), citing `EV-013`.
- `EV-013` makes “efficient lookup of waits and instances … part of the core contract”
  (`docs/specs/05-requirements-events-waits-timers.md:58-60`).
- MG-001 / MG-004 and reshape management delta defer public bulk selection/query
  (`docs/specs/09-requirements-management-operations.md:9-14`, `:30-33`;
  `openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md:10-12`).
- Task 3.7 explicitly rejects bulk/query/statistics (`tasks.md:30`).

**Impact**

A guard or acceptance author can invent a public `List`/`Where` surface and still cite AC-115.
That collides with the approved reduced management contract.

**Smallest remediation**

Rewrite `EV-013`/`AC-115` as provider-internal / host-projection performance requirements with
no public bulk selection API, or move them to the deferred registry beside AC-516.

---

### P2-1 — “DAG visualization” is claimed without any public signature

**Evidence**

- Capability row and amendment V1-12 claim “validation, and visualization”
  (`17-selected-mode-capability-matrix.md:46`; amendment `:80`).
- Section 17.2.6 and the companion declare only build/register/start/snapshot/output/cancel
  members; OpenSpec reshape has no visualization requirement.

**Impact**

Not required by task 3.10 today, but a later agent could invent `ToMermaid`/`Render` APIs under
document 17 authority.

**Smallest remediation**

Remove “visualization” from the capability claim until a concrete signature is amended in, or
add the exact public member(s) to 17.2.6.

---

### P2-2 — Concurrency OpenSpec never states `MaxConcurrentNodes` parked-child occupancy

**Evidence**

- Document 17 requires `MaxConcurrentNodes` to count started nonterminal children, including
  children parked in `Wait`, until node terminality
  (`17-selected-mode-capability-matrix.md:1383-1385`, `:1144-1148`).
- `add-runtime-concurrency-limits` design only mentions a generic “DAG-node” limit
  (`openspec/changes/add-runtime-concurrency-limits/design.md:83`) and neither the delta spec nor
  tasks name the parked-child rule.
- Canonical CP-025 states separation but not parked occupancy
  (`docs/specs/08-requirements-composition.md:164-168`).

**Impact**

An implementer following only the concurrency change could free DAG admission when a child
parks. Reshape task 3.10 cites `MaxConcurrentNodes` but not this lifetime rule explicitly.

**Smallest remediation**

Add the parked-nonterminal occupancy rule to the concurrency delta, CP-025, and task 3.10 /
concurrency tasks.

---

### P2-3 — Host option / strong-value namespaces are incomplete in the normative sketch

**Evidence**

- Hosting extension methods have namespaces (`17-selected-mode-capability-matrix.md:1484-1517`).
- `StructuredExecutionHostOptions`, engine options, throttle/pool types, and most strong values
  in §17.2.1 / §17.4 appear without namespace.
- Lease recovery namespace is specified in prose only (`:892-893`).

**Impact**

Task 3.1/3.7 tier classification can place options in `OrcaCore` vs `OrcaCore.Hosting`
inconsistently without inventing behavior, but still inventing assembly/namespace placement.

**Smallest remediation**

State exact namespaces (or “same assembly/namespace as hosting extensions”) for every public
options/recovery type in document 17.

---

### P2-4 — “Following `If` … end/fail” has no `Fail` authoring member

**Evidence**

- Join prose tells authors to use a following `If` to “end/fail”
  (`17-selected-mode-capability-matrix.md:707-708`).
- Completion builders expose only successful `End`; nested builders have no `Fail`/`Throw`
  member (`17-public-authoring-contract.cs` completion and nested families).
- Terminal failure is expressible only via `Then<TStep>()` returning `StepResult.Failed` (or an
  ephemeral lambda that fails).

**Impact**

Consumer programs work, but authors can misread “end/fail” as a missing API or as encoding
business failure inside successful `End` output. Misuse fixture “`WhenAllOutcomes` whose
following `If` rejects the summary” needs an explicit failing step.

**Smallest remediation**

Clarify in §17.2.4 that business rejection after `WhenAllOutcomes` is either (a) successful
`End` with classified `TOutput`, or (b) a named/lambda step that returns `StepResult.Failed`.
Do not imply a fluent `Fail`.

---

### P3-1 — Amendment flattens `StepExecutionContext` onto `StepContext`

**Evidence**

Amendment §3 lists `WorkflowInstanceId` / `OperationId` / `AttemptNumber` as direct
`StepContext` members (`amendment:98-102`); document 17 nests them under
`StepContext.Execution` (`17-selected-mode-capability-matrix.md:225-241`).

**Impact**

Low: document 17 wins. Still a foot-gun for samples copied from the amendment.

**Smallest remediation**

Align amendment wording to `context.Execution.*`.

---

## 3. Explicit non-findings (examined and endorsed)

These accepted scope decisions were challenged for completeness/safety and are endorsed:

- Staged typed workflows, fixed optional `WorkflowOutcomeName`, immutable
  `DefinitionId`/`DefinitionVersion`, structural-only fingerprint + mandatory opaque-code
  version bump.
- Codec-detached attempt state, `ReplaceState`, fixed certified `orcacore-json-v1`, non-replaceable
  codec.
- Named steps in both modes; ephemeral-only lambdas; no durable lambdas.
- Root `If`/`While`, nested `If`, nested `Parallel`, `Wait`, `Delay`, durable root
  `ContinueAsNew`, `CompleteWithin`, `WithStepTimeout`.
- `WhenAll` / `WhenAllOutcomes`; finite root `ForEach` with valid empty merge; ancestor merge
  suppression; no sibling auto-cancel.
- Runtime `StepOperationId` + diagnostic `AttemptNumber`.
- Scoped-only durable leasing; no author TTL/renewal/holder; quarantine until trusted
  confirmation; no force release / time-only reclaim.
- One path-token model with parent release/reacquire; separately counted admitted `ForEach`
  items; exact-type step throttles; at most one transient pool per ephemeral preceding step; no
  fail-fast/capacity-wait-timeout / custom transient SPI / host-wide advancement ceiling.
- Per-target event dedup; exactly-one active wait by `(DefinitionId, EventName, CorrelationId)`;
  definition fanout deferred.
- Separate `OrcaCore.Dag` + sole `OrcaCore.Dag.Hosting` friend bridge; one durable child per
  node; fixed dependency-failure behavior.
- Role-specific hosting entry points; small meta-package excludes optional integrations;
  Kubernetes/AWS/jobs outward-only companion.
- Deferred: `WhenFirst`, Saga, `RunExternalJob`, `RunChild`/`RunChildren`, nested `While`/`ForEach`,
  durable lambdas, definition-wide retry.
- Removed: `WaitLong`, author `Yield`.
- Phase 4b README/PROGRESS/T4B-* have conspicuous historical routing to document 17.
- `docs/plans/current-roadmap.md` routing banner is adequate via `docs/plans/README.md`.
- Status/amendment correctly report Phase 0 section 3 as 0/15 and do not claim guards already
  prove the amended contract.
- OpenSpec strict validation passes for both coordinated changes.

---

## 4. Dimension scores

| Dimension | Score (1–5) | Notes |
|---|---:|---|
| Consistency | **3** | Strong core alignment; lease-row, amendment fingerprint, AC-115, and hosting-integration leftovers cut the score. |
| Comprehensiveness | **4** | Journeys, leases, DAG, events, concurrency, and packages are covered; visualization and option namespaces are thin. |
| Developer orientation | **4** | Staged builders and mode-split are excellent; “end/fail” after outcomes needs clearer guidance. |
| Misuse resistance | **4** | Strong allowlist/absence story; residual silent risks are ephemeral capture and opaque-code version discipline. |
| Durable safety | **4** | Operation ID, fencing, ForEach snapshot, quarantine, and governance aggregate are well specified. |
| Package isolation | **5** | DAG/companion/provider directionality is clear and consistently restated. |

---

## 5. Five consumer programs (against proposed signatures)

Friction notes follow each program. Programs are planning sketches, not compilable product code.

### Program 1 — Ephemeral typed workflow (lambda, pool, parallel outcomes, state, timeouts)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;

public sealed record OrderIn(string OrderId, decimal Amount);
public sealed class OrderState
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public bool Risky { get; set; }
    public IReadOnlyList<BranchOutcome<decimal>>? Scores { get; set; }
}
public sealed record OrderOut(string OrderId, string Disposition);

public static class EphemeralProgram
{
    public static EphemeralWorkflowDefinition<OrderIn, OrderOut> Define() =>
        Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<OrderIn>(i => new OrderState { OrderId = i.OrderId, Amount = i.Amount })
            .CompleteWithin(TimeSpan.FromSeconds(30))
            .Then(async (ctx, ct) =>
            {
                // codec-detached attempt copy; mutate reference state
                ctx.State.Risky = ctx.State.Amount > 10_000m;
                await Task.Yield();
            })
            .WithTransientPool(new TransientPoolName("cpu"))
            .WithStepTimeout(TimeSpan.FromSeconds(2))
            .Parallel<decimal>(branches => branches
                .Branch(
                    new AuthoredBranchId("rule-a"),
                    s => s.Value,
                    b => b.Then(async (ctx, ct) => { /* score */ }).Return(s => 1m))
                .Branch(
                    new AuthoredBranchId("rule-b"),
                    s => s.Value,
                    b => b.Then(async (ctx, ct) => { /* score */ }).Return(s => 2m)))
            .WhenAllOutcomes((parent, outcomes) =>
            {
                parent.Value.Scores = outcomes;
                return parent.Value;
            })
            .If(
                s => s.Value.Scores!.Any(o => o is BranchOutcome<decimal>.Failed),
                then: nest => nest.Then(async (ctx, ct) =>
                {
                    // business rejection = failed step (no fluent Fail)
                    throw new InvalidOperationException("score-failed");
                }),
                otherwise: nest => nest.Then(ctx =>
                {
                    ctx.ReplaceState(new OrderState
                    {
                        OrderId = ctx.State.OrderId,
                        Amount = ctx.State.Amount,
                        Risky = false,
                        Scores = ctx.State.Scores
                    });
                }))
            .End(s => new OrderOut(
                s.Value.OrderId,
                s.Value.Risky ? "manual" : "auto"))
            .Build();

    public static void Register(IServiceCollection services)
    {
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = Array.Empty<StepExecutionThrottle>()
            },
            TransientPools =
            [
                TransientPoolDefinition.Create(new TransientPoolName("cpu"), capacity: 2)
            ]
        });
    }
}
```

**Friction**

- Rejecting `WhenAllOutcomes` requires a failing step/lambda, not `End`/`Fail` (P2-4).
- `BranchOutcome<T>` pattern matching depends on nested record names from document 17.
- Host option namespaces are assumed (`P2-3`).

### Program 2 — Durable typed workflow with bounded `ForEach`, outcomes, restart, typed output

```csharp
public sealed record BatchIn(IReadOnlyList<string> ItemIds);
public sealed class BatchState
{
    public IReadOnlyList<string> ItemIds { get; init; } = [];
    public IReadOnlyList<ForEachItemOutcome<string>>? Results { get; set; }
}
public sealed record BatchOut(int Succeeded, int Failed);

public sealed class ProcessItemStep : IStep<ItemState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<ItemState> context, CancellationToken ct)
    {
        // use context.Execution.OperationId for external idempotency
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

public sealed class ItemState
{
    public string ItemId { get; init; } = "";
}

public static DurableWorkflowDefinition<BatchIn, BatchOut> DefineDurableBatch() =>
    Workflow.Durable<BatchState>(DefinitionId.Parse("11111111-1111-1111-1111-111111111111"), new DefinitionVersion(1))
        .Init<BatchIn>(i => new BatchState { ItemIds = i.ItemIds })
        .CompleteWithin(TimeSpan.FromHours(2))
        .ForEach<string, ItemState, string>(
            s => s.Value.ItemIds,
            ForEachOptions.Create(maxItems: 100, maxConcurrency: 8),
            input => new ItemState { ItemId = input.Item },
            body => body.Then<ProcessItemStep>().Return(s => s.Value.ItemId))
        .WhenAllOutcomes((parent, outcomes) =>
        {
            parent.Value.Results = outcomes;
            return parent.Value;
        })
        .If(
            s => s.Value.Results!.OfType<ForEachItemOutcome<string>.Failed>().Any(),
            then: n => n.Then<RejectBatchStep>(),
            otherwise: null)
        .End(s => new BatchOut(
            s.Value.Results!.Count(o => o is ForEachItemOutcome<string>.Succeeded),
            s.Value.Results!.Count(o => o is ForEachItemOutcome<string>.Failed)))
        .Build();
```

**Friction**

- Empty `ForEach` is valid and merges once — good for restart tests.
- Durable selector/list mutation after commit cannot affect replay — correct.
- No durable lambda; named steps only — correct.
- Restart is host/runtime behavior, not an authoring member — samples must use provider-backed
  hosting, not a workflow API.

### Program 3 — Durable resource journey (short DB lease vs Wait-enclosing capacity lease)

```csharp
public sealed class SchedulerState
{
    public string JobKey { get; set; } = "";
    public string? ExternalRef { get; set; }
}

public sealed class WriteDbStep : IStep<SchedulerState> { /* short work */ }
public sealed class CreateOrObserveJobStep : IStep<SchedulerState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<SchedulerState> ctx, CancellationToken ct)
    {
        var op = ctx.Execution.OperationId;
        var token = ctx.ResourceLease!.ProtectionToken;
        // label external work with op + token; no K8s types here
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

public static DurableWorkflowDefinition<string, string> DefineLeaseJourney() =>
    Workflow.Durable<SchedulerState>(DefinitionId.New(), DefinitionVersion.Initial)
        .Init<string>(key => new SchedulerState { JobKey = key })
        // short DB lease exits before Wait
        .AcquireResources(
            ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(new ResourcePoolName("db-connections"))),
            lease => lease.Then<WriteDbStep>())
        // capacity lease intentionally encloses Wait until external work ends
        .AcquireResources(
            ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(new ResourcePoolName("scheduler-slots"))),
            lease => lease
                .Then<CreateOrObserveJobStep>()
                .Wait(
                    new EventName("job.terminated"),
                    s => new CorrelationId(s.Value.JobKey)))
        .End(s => s.Value.JobKey)
        .Build();
```

**Friction**

- Nested-builder leasing must be legal for analogous `While`-scoped patterns (P1-1).
- Quarantine on cancel/deadline is runtime, not visible in the fluent program — correct.

### Program 4 — Typed DAG over durable role

```csharp
using OrcaCore.Dag;
using OrcaCore.Dag.Hosting;

public sealed record DagIn(string RunKey);
public sealed record PrepOut(string Artifact);
public sealed record WorkOut(string Result);
// Node C is resultless

public static WorkflowDagPlan<DagIn> DefineDag(
    DurableWorkflowRef<PrepOut> prep,          // actually DurableWorkflowRef<DagIn-mapped, PrepOut>
    DurableWorkflowRef<PrepOut, WorkOut> work,
    DurableWorkflowRef<WorkOut> finish) =>
    Dag.Define<DagIn>(DefinitionId.New(), DefinitionVersion.Initial)
        .Node(new DagNodeId("prep"), /* DurableWorkflowRef<DagIn, PrepOut> */ prepRef)
        .DependsOn()
        .MapInput(ctx => /* map from RunInput */ default!)
        // ... wire three heterogeneous refs: A(resultful), B(resultful depends on A), C(resultless independent)
        .Node(new DagNodeId("work"), workRef)
        .DependsOn(prepNode)
        .MapInput(ctx => ctx.OutputOf(prepNode))
        .Node(new DagNodeId("sidecar"), resultlessRef) // independent ready node
        .DependsOn()
        .MapInput(ctx => ctx.RunInput)
        .Build();

// Hosting
services.AddOrcaCoreDurableEngine(durableOptions);
services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 2 });
```

**Friction**

- Sketch elides exact `Node`/`MapInput` chaining locals; companion signatures support the shape.
- Failed dependency blocks transitive dependants while independent ready nodes continue —
  runtime, not builder.
- `OutputOf` rejects undeclared/non-direct/wrong-typed deps at build — good.
- No visualization API despite capability claim (P2-1).

### Program 5 — Companion Kubernetes Job journey (no K8s types in OrcaCore)

```csharp
// In companion project only:
public sealed class SubmitK8sJobStep : IStep<JobState>
{
    // uses Kubernetes client types HERE, not in OrcaCore
    public ValueTask<StepResult> ExecuteAsync(StepContext<JobState> ctx, CancellationToken ct)
    {
        var op = ctx.Execution.OperationId;                 // create-or-observe key
        var token = ctx.ResourceLease!.ProtectionToken;     // label Job
        // AttemptNumber must NOT be the external idempotency key
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

// Watcher host (definition-less):
services.AddOrcaCoreDurableEventIngress();
await events.DeliverByCorrelationAsync(
    definitionId,
    WorkflowEvent.Create(
        new EventId(stableWatcherEventId), // reused on redelivery
        new EventName("job.terminated"),
        new CorrelationId(jobKey),
        DateTimeOffset.UtcNow));

// Reconciler confirms stop without K8s types crossing into OrcaCore:
IDurableResourceLeaseRecovery recovery = ...;
await recovery.ConfirmProtectedWorkStoppedAsync(
    protectionToken,
    new StopConfirmationId(reconcilerConfirmationId),
    ct);
```

**Friction**

- Contract is sufficient; companion owns Job DTOs/clients.
- Cancellation/deadline reconciliation is companion policy + quarantine — clearly separated.

---

## 6. Misuse classification table

| Misuse | Classification |
|---|---|
| Build before `End` | Compile-impossible (completion builder required) |
| Output type inconsistent with `DurableWorkflowRef` | Compile-impossible / registration type mismatch |
| Empty/default/invalid definition ID/version | Fluent-call rejection (constructors/factories) |
| Reuse identity/version with changed fingerprint | Registration/startup diagnostic (`DefinitionRegistrationConflict`) |
| Durable lambda step | Compile-impossible |
| Ephemeral lambda capturing mutable outer state | **Silently accepted** (language limitation; residual risk) |
| Nested `While` / nested `ForEach` / `WhenFirst` / Saga / `RunExternalJob` / `RunChildren` / `WaitLong` / `Yield` | Compile-impossible (absent members) |
| Retry/step-timeout on `Wait`/`End`/join/empty branch | Fluent-call rejection |
| `WhenAll` with one failure | Runtime: scope fails after all terminal; merge skipped |
| `WhenAllOutcomes` + following `If` rejects summary | Runtime defense via failing step; no silent success if author fails correctly |
| Unbounded/over-limit durable `ForEach` | Fluent/build rejection before admission |
| Valid empty durable `ForEach` | Accepted; merge once with empty list |
| Host ceiling below/above node cap | Runtime: effective = min; parks, does not fail |
| Parked item vs path-token accounting | Runtime: item still counts against node cap; path token released while parked |
| Host ceiling 1 fan-out progress | Runtime: parent releases before admit; must progress (non-deadlock) |
| Reuse `EventId` with changed content | Runtime: `EventConflict` |
| Two active waits same `(DefinitionId, EventName, CorrelationId)` | Runtime: `AmbiguousWaitRegistrationException` before park |
| Definition-targeted fanout | Compile-impossible / absent route |
| `AttemptNumber` as external idempotency key | **Silently accepted** misuse (documented diagnostic-only; adapter bug) |
| Reuse one `StepOperationId` across loop/item/branch/generation | Runtime defense (new ID per occurrence) |
| Point/fiber-lifetime lease, empty request, duplicate pool, non-positive units | Compile-impossible / fluent rejection |
| Transient/durable name swap | Compile-impossible (distinct types) |
| Author TTL / renewal / holder ID | Compile-impossible (absent) |
| Nested acquisition under live ancestry | Build diagnostic `SFE-AUTH-LEASE-001` + runtime `SFE-RUN-002` |
| Sequential root-loop scope / sibling scopes | Accepted |
| `ContinueAsNew` inside leased body | Compile-impossible on leased builders; runtime `SFE-RUN-001` for hand-built |
| Release from elapsed time / delete ack / terminal status / mismatched token / stale confirmation / force-release | Runtime: no capacity release / typed confirmation results / absent API |
| DAG `OutputOf` undeclared/non-direct/wrong-typed | Build diagnostic |
| Caller-owned ready/completed sets / public child node | Compile-impossible |
| OrcaCore/`OrcaCore.Dag` reference to K8s/AWS/companion/Job DTO | Architecture/package consumer failure |
| Catch-all `AddOrcaCore`, hosted-service toggle, codec replacement | Compile-impossible / startup absent |
| Transient pools in durable options / durable pools in ephemeral options | Startup diagnostic / type shape absence |
| Host-wide advancement/general-body ceilings / fail-fast admission / custom transient SPI | Compile-impossible |
| Multiple named pools on one step / non-exact step-throttle scopes | Fluent/startup rejection |
| Pool decoration binding following rather than preceding step | Must be rejected / bound to preceding (task 3.7) |
| DAG registration without durable engine role | Registration/startup diagnostic |

Any silently accepted misuse affecting identity, external effects, lease capacity, mapping, or
dependency direction is at least P1. The two residual silent cases above are: ephemeral mutable
captures (ephemeral-only), and adapters misusing `AttemptNumber` (documentation/discipline, not
missing API). Opaque-code changes under a reused version are **unsupported**, not
fingerprint-detectable (see P1-2).

---

## 7. Lifecycle analysis

### Operation identity (`StepOperationId`)

- Allocated once per logical step visit; stable across retry, step-timeout reconciliation,
  replay, host replacement, expected-version conflict, and competing drivers.
- New ID on loop re-entry, another `ForEach` item, another branch, or continue-as-new generation.
- Survives crash before/after external create; adapters must create-or-observe with this ID.
- Sufficient and clear for guards 3.9.

### Deadlines / attempts

- `AttemptNumber` starts at 1 and increments per invocation; diagnostic only.
- `WithStepTimeout` fences the attempt copy; late token-ignoring bodies retain physical
  throttle/transient slots until return.
- `CompleteWithin` is absolute from instance start, inherited across continue-as-new, suppresses
  merges on win, does not wait for token-ignoring bodies.
- Event vs wait-timeout vs workflow-deadline has one serialized winner.

### Durable `ForEach`

- Selector validated, codec-round-tripped, and committed before admission; replay reuses snapshot.
- Stable identity = scope occurrence + index; empty list merges once.
- Merge-at-most-once after commit; ancestor terminality suppresses merge.
- Path token released while parked; admitted-item cap still counts parked items; composes as
  min(host, node).

### Leasing

- Lexical pending → held/waiting → completed release, or quarantine on ambiguous protected work.
- Exact token/obligation/provider-generation matching; atomic multi-pool grant; park only the
  requesting fiber.
- Confirmation matrix is exhaustive; normal-release race specified; no time reclaim.
- Missing governance ticket while workflow still pending/held/quarantined → `LeaseLostException`,
  not silent reacquire.
- Blocked for guards until P1-1 and P1-3 are fixed.

### DAG

- Node input commits once after direct deps succeed; one internal child instance per node;
  idempotent start/reattach; independent ready nodes continue after a failed dependency blocks
  transitive dependants.
- `MaxConcurrentNodes` separate from path tokens; parked-child occupancy is in document 17 but
  under-specified in the concurrency OpenSpec (P2-2).
- Sole `OrcaCore.Dag.Hosting` friend bridge is clear and guardable (task 3.10).

---

## 8. Guard-coverage matrix (tasks 3.1–3.12)

| Task | Required guarantees → proposed seam | Ready? |
|---|---|---|
| 3.1 | Exact signatures/diagnostics/tiers/absence | Reflection/source baselines against doc 17 + companion | **Almost** — blocked by P1-1 lease-location choice and P2-3 namespaces |
| 3.2 | Packed consumers (ephemeral, durable, ingress, in-memory, DAG, meta, companion) | Package consumer fixtures | **Yes** |
| 3.3 | Provider.Abstractions → Runtime.Protocol; governance store append/validation; forbidden edges | Provider-author / architecture fixtures | **Yes** |
| 3.4 | Every companion signature; TryBuild/Build parity; mode/root/lease restrictions; deferred absence | Compile fixtures | **No** until P1-1 |
| 3.5 | Strong values, codec, structural fingerprint, opaque version bump, ReplaceState | Behavior + compile fixtures | **Yes** if amendment P1-2 corrected so guards follow doc 17 |
| 3.6 | Joins, empty ForEach, path tokens, ancestor suppression | Behavior fixtures | **Yes** |
| 3.7 | Facades, events, role hosting, options, preceding-step pool binding, absences | Compile + startup + architecture | **Almost** — clarify AC-115 (P1-5) so bulk query is not invented |
| 3.8 | Typed journeys, waits, four event overloads, dedup/conflict, ingress handoff | Application golden paths | **Yes** |
| 3.9 | Retry/deadlines/fencing/operation ID/competing drivers | Behavior + harness | **Yes** |
| 3.10 | DAG build/ops, MaxConcurrentNodes, friend bridge | DAG fixtures | **Almost** — add parked-child occupancy (P2-2) |
| 3.11a | Scoped lease authoring/admission/ancestry/CAN | Compile + runtime | **No** until P1-1 |
| 3.11b | Release-before-resume; quarantine before merge | Behavior | **Yes** |
| 3.11c | Confirmation matrix, races, retention, LeaseLost, no time reclaim | Behavior | **Yes** |
| 3.11d | Governance aggregate accounting | Provider certification | **No** until P1-3 names observables |
| 3.12 | Run all lanes; actual counts; independent re-review of all 15 | Execution gate | **Instruction OK**; cannot pass until 3.1–3.11d retarget is coherent |

No task still assumes a deferred/removed member as shipping. Expected-red counts are correctly
unknown until execution (known pending state; not a finding).

---

## 9. Does any unresolved decision still block guard implementation?

**Yes.**

Blocking before a guard-only packet:

1. Resolve durable lease location contradiction (P1-1).
2. Correct amendment opaque-code/fingerprint wording to match document 17 (P1-2).
3. Replace task 3.11d shorthand with document-17-observable assertions (P1-3).
4. Retarget or banner the hosting integration review doc (P1-4).
5. Demote or rewrite AC-115/EV-013 bulk-retrieval as non-public (P1-5).

Non-blocking but should ship in the same planning fix pass: P2-1…P2-4.

After those planning edits, a guard-only agent can implement tasks 3.1–3.11d from document 17 +
companion + reshape tasks **without design authority**, then execute task 3.12 for counts and
independent re-review. Product implementation (task 4.0+) remains blocked until that gate passes.

---

## 10. Validation commands and results

Ran from repository root on 2026-07-19:

```powershell
openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
```

**Results**

- `Change 'reshape-developer-facing-interfaces' is valid`
- `Change 'add-runtime-concurrency-limits' is valid`
- `git diff --check`: exit 0; CRLF/LF replacement warnings only on already-dirty working tree
  files (no whitespace error reported)

**Additional inspection**

- Authority/docs grep for removed/deferred members shows consistent “removed/deferred” framing in
  document 17, canonical specs 01–16, reshape OpenSpec, and the phased plan.
- Reviewed packet exception: `docs/review/integration-tests/01-hosting-and-hosted-services.md`
  still treats `AddOrcaCore` / `AddOrcaCoreHostedServices` as current (P1-4).
- Product/guard builds were not required; existing guards are known stale relative to this
  amendment and are not evidence of planning compliance.

---

## Closing

**Planning contract: APPROVE WITH CHANGES.**  
**Guard-retarget readiness: NOT READY.**

The v1 simplification is close: the companion declarations, concurrency model, lease quarantine
story, DAG package split, and role-specific hosting are strong enough to author the five required
consumer journeys. Do not start the 15-task guard packet until the five P1 planning defects above
are amended so implementers cannot invent lease locations, fingerprint behavior, governance
assertions, catch-all hosting, or bulk query APIs.
