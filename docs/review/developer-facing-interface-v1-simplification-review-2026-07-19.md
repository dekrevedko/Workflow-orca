# Independent review: OrcaCore v1 planning contract and Phase 0 guard readiness (2026-07-19)

Reviewer: independent senior .NET public-API / durable-runtime / workflow-authoring reviewer.
Inputs reviewed in precedence order: `docs/specs/17-selected-mode-capability-matrix.md`,
`docs/specs/17-public-authoring-contract.cs`, canonical requirements 03–16, both OpenSpec changes
and their deltas, `openspec/specs/runtime-resource-governance/spec.md`, the v1 amendment, and the
listed supporting docs. Both changes pass `openspec validate --strict` (§9). No input file was
modified.

This is a re-audit after the v1 simplification. The prior 2026-07-16 review is treated as
historical; findings below are re-derived from the current files. Where a prior finding is now
resolved I say so in §3 rather than re-litigating it.

---

## 1. Verdicts

**Planning contract: APPROVE WITH CHANGES.**

The simplification is a large net improvement. Removing `WhenFirst`, Saga, `RunExternalJob`,
`WaitLong`, author `Yield`, definition-wide retry, and public child orchestration collapsed a
half-dozen unfinished state machines into a coherent, mostly-shippable core. The two decisions that
most improve the surface over the prior baseline — **scoped-only `AcquireResources(request, body)`**
and **validated reference-type identities with `Parse`/`TryParse`** — each directly resolve a P1
from the previous review (§3). Staged construction that makes *build-before-`End`*
compile-impossible is the single best structural idea in the contract.

The changes required before approval are narrow: fix the `ContinueAsNew` fluent shape and its
inexpressible conditional form (P1-1); reconcile the matrix's generic join-builder notation with the
twelve concrete join/scope types the `.cs` enumerates (P1-2); and give operators a way to *see*
quarantined lease capacity given that there is deliberately no force-release (P1-3).

**Guard-retarget readiness: NOT READY.**

Not because the packet is under-scoped — §7 shows tasks 3.1–3.12 map cleanly onto executable seams —
but because two contract ambiguities would force a guard-only agent to *invent* public type names it
is forbidden to invent. A compile/baseline fixture for task 3.1/3.4 cannot assert the exact
`Parallel`/`ForEach` join and scope types while the matrix (§17.2.4, one generic
`ParallelJoinBuilder<TParentBuilder,…>`) and the `.cs` (twelve concrete classes) disagree (P1-2); and
a fixture for `ContinueAsNew` cannot assert its completion shape while the `.cs` returns a
*continuable* builder with no diagnostic for the dead `End` that must follow (P1-1). Resolve P1-1 and
P1-2 and the packet is READY; P1-3 and the P2s can land as guard work proceeds.

---

## 2. Findings

### P1

#### P1-1 — `ContinueAsNew` returns a continuable builder, is root-only, and cannot be chosen conditionally; the canonical long-running pattern is inexpressible and the fluent shape invites dead code

**Evidence.**
`17-public-authoring-contract.cs:282-284`:

```csharp
public DurableWorkflowBuilder<TInput, TState> ContinueAsNew(
    Func<ReadOnlyStateSnapshot<TState>, TState> replacementState) =>
    throw new NotSupportedException();
```

It returns `DurableWorkflowBuilder<TInput, TState>` — the same builder that still requires a later
`End(...)` to reach a `CompletionBuilder` and `Build()` (`17-...contract.cs:286-291`; `Build` exists
only on the completion builders, `:66-85`). The availability table places `ContinueAsNew` at "root
only … no / no / no" for nested/branch/leased contexts
(`docs/specs/17-selected-mode-capability-matrix.md:534`), and the capability row calls it "Durable
root only … Valid only at a quiescent root location" (`17-...matrix.md:40`). DU-042 requires it for
"Long-lived durable root workflows" that start "a fresh execution position"
(`docs/specs/06-requirements-durable-execution.md:141-146`).

Two defects follow, neither addressed in the contract nor in the §17.6 deferred table:

1. **Dead-code shape.** `ContinueAsNew` is a terminal rollover — no authored node can run after the
   generation restarts — yet its return type lets the author keep chaining, and the *type system
   forces* a trailing `End()` (unreachable) to satisfy `Build()`. The contract reserves no diagnostic
   for authored nodes after `ContinueAsNew`, so `.ContinueAsNew(s => next).Then<X>().End()` compiles
   with `Then<X>` and `End` both dead. `End` produces a `CompletionBuilder`; `ContinueAsNew` does not,
   so the two terminal transitions have different fluent shapes for the same job.
2. **No conditional rollover.** `ContinueAsNew` and `End` are both root-only and neither appears on
   `DurableNestedBuilder` (the `If`/`While` body type, `:342-388` — no `ContinueAsNew`, no `End`). So
   an author cannot write "if more work remains, `ContinueAsNew`; else `End`." The only expressible
   durable long-running shape is an *unconditional* root `ContinueAsNew`: an infinite generation chain
   that can never terminate. The batch-supervisor pattern DU-042 exists for — process a bounded batch,
   roll over to bound history, stop when the queue drains — is not authorable.

**Impact.** A developer building the exact "long-lived durable root workflow" DU-042 names either
writes an infinite workflow (no drain-and-stop) or abandons `ContinueAsNew` and accepts unbounded
history. The dead trailing `End()` is a papercut every rollover workflow carries, and a guard author
(task 3.4/3.9, continue-as-new inheritance) has no completion type to assert.

**Smallest normative remediation.** Make `ContinueAsNew` terminal in the type system: return
`DurableWorkflowCompletionBuilder<TInput>` (mirroring `End()`), so nothing can follow it and no dead
`End` is required. For the conditional case, expose `ContinueAsNew` **and** `End` on
`DurableNestedBuilder` restricted to `If` arms (not loops), so
`If(drained, then: b => b.End(...), otherwise: b => b.ContinueAsNew(...))` is authorable, with the
existing quiescence rule (`:40`) still enforced. If conditional rollover is instead meant to be
deferred, add it to the §17.6 table with a rationale — its current silent absence is the finding as
much as the shape is.

#### P1-2 — The matrix presents one generic `Parallel`/`ForEach` join type; the normative `.cs` enumerates twelve concrete join/scope types. A guard cannot assert the public baseline while the two authorities disagree, and the concrete choice is the single largest driver of surface size

**Evidence.** Matrix §17.2.4 (`17-...matrix.md:636-648`) declares the join as a **single generic
type**:

```csharp
ParallelJoinBuilder<TParentBuilder, TParentState, TResult>
    TParentBuilder.Parallel<TResult>(Action<TBranchScopeBuilder> branches);
TParentBuilder ParallelJoinBuilder<TParentBuilder, TParentState, TResult>.WhenAll(…);
```

The `.cs` declares **twelve** concrete join classes and twelve concrete scope classes — e.g.
`EphemeralWorkflowParallelJoinBuilder<TInput,TState,TResult>` (`:780`),
`EphemeralNestedParallelJoinBuilder` (`:807`), `EphemeralBranchParallelJoinBuilder` (`:837`),
`EphemeralItemParallelJoinBuilder` (`:870`), and the durable and durable-lease equivalents (`:897,
:924, :954, :984, :1014, :1044, :1074, :1107`), each paired with a `…BranchScopeBuilder`. No generic
`ParallelJoinBuilder<,,>` exists in the `.cs`.

Unlike §17.2.3, which explicitly labels its `TBuilder`/`TRootBuilder`/`TSelectedNestedBuilder` as
notation ("`TBuilder` denotes the selected-mode root or nested builder", `:515-522`), §17.2.4 gives
**no** such disclaimer for `ParallelJoinBuilder<TParentBuilder,…>`, `TBranchScopeBuilder`, or
`TSelectedBranchBuilder`. It reads as a real generic contract.

**Impact.** Task 3.1 (public-signature baseline) and 3.4 ("Every exact staged
factory/init/root/nested/branch/item/leased/scope/join … declaration") must assert exact public type
names. Following the matrix yields one generic type; following the `.cs` yields twenty-four. The
reviewer prompt forbids a guard from "inventing an unspecified signature" — here the guard must choose
between two normative documents, which is the same problem. Separately, this is the dominant cost in a
contract whose stated goal is a *smaller* surface: of 51 public types in the authoring `.cs`, 44 are
builder/scope/join types, and the twelve-way join/scope expansion is roughly half of that. The
single-generic form removes ~22 public types with no loss of type safety, because
`TParentBuilder`/`TParentState`/`TResult` already carry every distinction the concrete classes encode.

**Smallest normative remediation.** Pick one and make both artifacts agree. I recommend the matrix's
single-generic form: replace the twelve `…ParallelJoinBuilder` / `…ParallelBranchScopeBuilder` classes
in the `.cs` with `ParallelJoinBuilder<TParentBuilder, TParentState, TResult>` and
`ParallelBranchScopeBuilder<TParentState, TResult>` (the branch `body` already selects the concrete
branch builder via a separate type parameter, so the parent-builder return is the only thing the
concrete classes add). If the concrete expansion is deliberate, delete the generic form from §17.2.4
and add the same "denotes" disclaimer §17.2.3 uses, so the `.cs` is unambiguously authoritative.

#### P1-3 — There is no force-release (correctly) and no operator visibility into quarantined capacity, so a stuck durable pool is a blind dead-end

**Evidence.** The pool snapshot exposes one aggregate reserved number
(`17-...matrix.md:1289-1295`):

```csharp
public sealed record DurableResourcePoolSnapshot(
    ResourcePoolName Name, int ConfiguredCapacity, int ReservedUnits,
    int ResizeDebt, int QueuedRequestCount, DateTimeOffset? OldestReviewDeadline);
```

Management is `ListAsync` / `GetAsync` / `ResizeAsync` only (`:1297-1311`); "There is no force-release
API" (`:1432`); stop proof is the trusted reconciler SPI `IDurableResourceLeaseRecovery` (`:810-816`).
Quarantine is real and capacity-reserving: "Cancellation, workflow deadline, step timeout, ambiguous
submit, process loss, or forced termination while protected work may exist moves the exact obligation
to quarantine. Quarantined units remain reserved until a trusted idempotent confirmation proves …"
(`:847-850`). The canonical spec asks for more than the snapshot delivers: MG-064 requires management
to inspect "reserved units **by state**, held tickets, over-capacity debt, expiry/reconciliation
state, queue depth, and oldest waiter" (`docs/specs/09-requirements-management-operations.md:291-293`).
The v1 snapshot flattens pending-commit/held/marked/ambiguous/quarantined into one `ReservedUnits`.

**Impact.** An EKS cluster is deleted with jobs mid-flight; no watcher survives to confirm stop;
obligations move to quarantine. The operator sees `ReservedUnits == ConfiguredCapacity`,
`QueuedRequestCount > 0`, `OldestReviewDeadline` in the past — and cannot tell how much is live vs
quarantined, has no force-release, and the only remaining lever (upward `ResizeAsync` to add capacity
around the dead units, `:1412`) is never named as the recovery path. The accepted tradeoff (ambiguous
ownership blocks capacity pending operator action) is safe but not *observable enough to act on*.

**Smallest normative remediation.** Add per-state fields to `DurableResourcePoolSnapshot` (at minimum
`HeldUnits`, `QuarantinedUnits`, `MarkedUnits`) matching MG-064's "reserved units by state." Read-only,
no new mutation. Optionally document upward `ResizeAsync` as the sanctioned capacity-recovery action
when quarantine is permanent (destroyed resource), distinct from the forbidden metadata delete.

### P2

#### P2-4 — Step decorators are chained siblings bound to "the preceding step" by position, not a step-scoped configuration; misuse is a build/fluent-throw rather than compile-impossible

**Evidence.** `17-...contract.cs:163-171` (repeated on every builder family) declares `WithRetry`,
`WithStepTimeout`, `WithTransientPool` as methods returning the same builder. The binding rule is
prose: "decorators attach to the immediately preceding business step, are order-independent, and may
each appear at most once for that step. A duplicate or attachment at any other location is rejected at
the fluent call" (`:536-538`). So `Init<I>(…).WithRetry(3)` (before any step) and
`.Then<A>().WithRetry(3).WithRetry(5)` (duplicate) are representable and caught only by a throwing
fluent call — the one place the contract relies on a runtime throw to guard a shape the goal wants
unrepresentable, and it contradicts "Local invalid arguments fail at the fluent call; graph-wide
diagnostics accumulate through `TryBuild`" (`:1197`) since a mis-attached decorator is neither.

**Impact.** (1) A developer reading `.Then<A>().WithRetry(3)` cannot tell from the types that
`WithRetry` binds to `A`. (2) A guard (task 3.9) must assert a *throw*, not a diagnostic — an
exception to the aggregation model that is currently undocumented.

**Smallest normative remediation.** Move decorators into a per-step closure:
`Then<TStep>(Action<IStepPolicy> configure)` with `Retry`/`Timeout`/(ephemeral)`TransientPool`. This
makes "attach to a step" structural, "at most once" a property of the closure, and "before any step"
unrepresentable — collapsing three fluent-throw rules into the type system and removing one method
from every builder family. If the chained form is kept, state in §17.2.3 that these methods throw
synchronously (an intentional exception to `TryBuild` aggregation) so guard task 3.9 asserts a throw.

#### P2-5 — Ephemeral inline-lambda steps cannot return `StepResult` and cannot be targeted by `StepExecutionThrottle`

**Evidence.** Lambda overloads are `Then(Action<StepContext<TState>>)` and
`Then(Func<StepContext<TState>, CancellationToken, ValueTask>)` (`:156-161`) — void/`ValueTask`, no
`StepResult`. Only `IStep<TState>.ExecuteAsync` returns `ValueTask<StepResult>`
(`17-...matrix.md:286-291`), and `StepResult` still carries `Failed` and dynamic `WaitForEvent`
(`:461-465`). Separately, `StepExecutionThrottle.For<TStep>` is "keyed solely by exact step type"
(`:1366`), and a lambda has no `TStep`.

**Impact.** A lambda step can only succeed or throw — no explicit `Failed`, no dynamic `WaitForEvent`;
and in the only mode where lambdas exist it can be guarded by `WithTransientPool` (binds to preceding
step regardless of kind) but not by a type-keyed throttle. Two capacity mechanisms with different
reach over the same construct, both silent.

**Smallest normative remediation.** Document both asymmetries at §17.2.3. If dynamic `WaitForEvent`
from a lambda is wanted, add a `Func<StepContext<TState>, CancellationToken, ValueTask<StepResult>>`
overload; otherwise consider whether portable `StepResult.WaitForEvent` still earns its place now
that the structural `Wait` node covers the common case.

#### P2-6 — Instance discovery has no application API and is not in the deferred table

**Evidence.** The only way to obtain a `WorkflowInstanceHandle` is `StartOrGetAsync` or
`GetInstanceAsync(InstanceId)` — which requires you to already hold the id (`:1683-1735`). There is no
`All()`/`ForDefinition()`/`Where()`. MG-030 states statistics are "Exposed via the management and
telemetry integration … A public application `Statistics()` member is not part of the v1
instance-handle surface" (`docs/specs/09-requirements-management-operations.md:90-98`). But the §17.6
deferred table (`:1555-1569`) lists pause/resume/archive/purge/retry and does **not** list instance
enumeration/query.

**Impact.** An operator or the companion scheduler that did not persist an `InstanceId` cannot
discover instances through the OrcaCore application surface at all — discovery is entirely via
telemetry/projections. That may be the intended v1 stance, but its absence from the deferred table
makes it indistinguishable from an oversight.

**Smallest normative remediation.** Add one row to §17.6: "Application instance query/enumeration —
deferred; v1 discovery is via telemetry/projections (MG-030) plus `GetInstanceAsync` by known id."

#### P2-7 — `ReadOnlyStateSnapshot<TState>` taxes every selector with `.Value` for marginal benefit and is a positional record so `with`/`Deconstruct` leak

**Evidence.** `public sealed record ReadOnlyStateSnapshot<TState>(TState Value);`
(`17-...matrix.md:206`) wraps state for every condition, correlation selector, branch/item input
projector, merge, `End` output selector, and `ForEach` items selector. The state is *already*
codec-detached before the selector sees it (`:312-313`), and the wrapper only "prevents replacing the
runtime-owned reference; authors must still treat its `Value` as immutable" (`:319-320`).

**Impact.** The wrapper buys the inability to reassign the reference the runtime handed you — a
non-goal, since it is a detached copy — at the cost of `s => s.Value.Field` on every author delegate.
Being positional, it also exposes `with { Value = … }` and `Deconstruct`, so it is *not* read-only in
the way the name claims.

**Smallest normative remediation.** Either pass `TState` directly to read-only selectors (the codec
copy already isolates them) and delete the wrapper, or make it a non-positional readonly struct with a
single `Value` getter and record at §17.2.1 that its sole purpose is nominal signalling.

#### P2-8 — `ForEach` item state can be derived only from the item + index, never from parent state

**Evidence.** The item input projector is `Func<ForEachItemInput<TItem>, TItemState>` where
`ForEachItemInput<TItem>(int Index, TItem Item)` (`:614`, `:670-674`). Contrast `Branch`, whose input
projector reads parent state: `Func<ReadOnlyStateSnapshot<TParentState>, TBranchState>` (`:650-653`).
A fan-out item cannot see shared parent context except what the `items` selector (which does read
parent state) pre-embeds into each `TItem`.

**Impact.** The workaround works but is non-obvious and duplicates parent data N times; a developer
expecting `Branch`/`ForEach` symmetry will look for a parent snapshot in the item projector.

**Smallest normative remediation.** Either add the parent snapshot to the item projector, or document
at §17.2.4 that item state derives solely from the item and shared context is carried in `TItem`.

### P3

- **P3-9 — Failure codes have no single convention.** Compiler diagnostics are `SFE-*` (`:1220`), a
  multi-failure join is `SFE-JOIN-FAILED` (`:702`), but DAG child mapping uses `CHILD_FAILED` /
  `CHILD_TIMED_OUT` / `CHILD_TERMINATED` / `CHILD_CANCELLED` (`:1137-1139`) — two schemes for the one
  `WorkflowFailure.Code` string that merges and DAG snapshots both surface. Give runtime failure codes
  one documented prefix and list the v1 set, or make them a closed family rather than free strings.
- **P3-10 — `EventDeliveryStatus.Accepted` no longer distinguishes inline-progressed from
  pending-continuation.** DU-055's `AppliedAndProgressed`/`AppliedPendingContinuation` split is folded
  into `Accepted` (`:1841-1848`, prose at `:1897-1899`). Fine for a callback host; a definition-owning
  caller can no longer tell whether the instance advanced. If intentional, one sentence at §17.7.
- **P3-11 — `CompleteWithin` is a chainable root method placeable mid-chain** (`:1197` / `.cs:173`).
  "At most once" handles duplication, but placement after several `Then`s reads as if the deadline
  starts there, whereas it "begins at instance start" (`:552`). Consider surfacing it at `Init`.
- **P3-12 — `WorkflowEvent` and `WorkflowEvent<TPayload>` duplicate five members and two factories**
  (`:1802-1839`) and drive a parallel four-overload `IWorkflowEventClient` (`:1854-1875`). A shared
  base or `WorkflowEvent<Unit>` would remove the duplication. Low priority.

---

## 3. Explicit non-findings (examined, endorsed)

- **Scoped-only `AcquireResources(request, body)`** (`:754-784`, V1-10) resolves the prior review's
  top P1 (lease held past the work it protects). Release-before-parent-resume, legal per-iteration
  loop scopes, and "a `Wait` after the scope observes an already-released lease" (`:834-836`) make hold
  duration visible in code structure. The explicit refusal to silently release-and-reacquire at a wait
  (`:837-838`) is the correct, subtle call.
- **Reference-type identities with validation + `Parse`/`TryParse`** (`:68-75`, `:126-132`, `:172-178`;
  "`default` cannot produce a non-null value that bypasses construction", `:301-302`) resolves the
  prior P1 that `default(DefinitionId)` was constructible. `ExternalJobId`'s missing `Parse` (prior P1)
  is moot — external jobs are deferred.
- **Staged construction** (init-only → work → completion-only, `.cs:29-85`): build-before-`End` is
  compile-impossible, not a diagnostic. Best structural decision in the contract.
- **`BranchResult`/`BranchOutcome` and `ForEachItemResult`/`ForEachItemOutcome` symmetry** (`:581-634`)
  resolves the prior naming asymmetry; the deliberate absence of a `Cancelled` outcome variant
  (`:710-713`) correctly prevents a merge from laundering an ancestor terminal transition into success.
- **`WaitLong` removed, durable `Wait` cold-capable by policy** (`:1571-1573`): a cleaner answer than
  the rename the prior review suggested.
- **No acquisition-timeout overload; no force-release** (`:819-820`, `:1432`) with the exhaustive
  `ProtectedWorkStopConfirmationStatus` matrix (`:878-887`): the right safety posture; elapsed time
  never reclaims live/ambiguous ownership (`:1420-1421`).
- **Single serialized governance aggregate per partition** (`:1434-1454`): the four-step idempotent
  reservation handoff, honestly labelled "a deliberate first-release correctness tradeoff for
  mandatory EKS scheduling," is exactly the kind of stated tradeoff a v1 should make.
- **DAG typed `OutputOf`** (`DagNodeRef<TOutput>` vs `DagNodeRef`, `:946-963`): a resultless node
  cannot be passed to `OutputOf` (compile-impossible).
- **`ValueTask` + `…Async` naming, internal ctors, closed abstract result bases** (throughout §17.7):
  idiomatic .NET; consumers pattern-match and never construct authority-bearing types.

---

## 4. Dimension scores

**Consistency — 7/10.** Ephemeral/durable pairs differ only where the matrix says; snapshot/selector
types are uniform; `StartOrGetAsync(input, key, ct)` order matches between workflow and DAG handles
(`:1091`, `:1683`). Deductions: matrix-vs-`.cs` join-type disagreement (P1-2), two failure-code
conventions (P3-9), `ContinueAsNew` diverging from `End` for the same terminal job (P1-1).

**Comprehensiveness — 7/10.** For the stated v1 scope almost every journey is expressible and
deferrals are documented with required future amendments (`:1555-1573`). The real gaps are the
inexpressible conditional `ContinueAsNew` (P1-1) and instance enumeration being absent-but-unlisted
(P2-6). The five consumer programs (§5) all complete except where they hit these.

**Developer orientation — 7/10.** Mode-before-IntelliSense, staged build, typed output, and the
scoped lease `body` read well. Deductions: `.Value` ceremony on every selector (P2-7), decorators
whose binding target is positional (P2-4), lambda steps that silently can't express `StepResult`
(P2-5), and no editor-reachable explanation for why a deferred capability is absent (the §17.6 table
lives in the spec repo, not XML docs).

**Misuse resistance — 8/10.** Build-before-`End`, wrong-mode registration, empty lease request,
transient/durable name swap, and `OutputOf` on a resultless node are all compile-impossible (§6).
Defaults are safe. The one silently-accepted class is dead nodes after `ContinueAsNew` (P1-1);
decorator misplacement is caught but by a throw, not the type system (P2-4).

**Durable safety — 8/10.** The lease state machine, quarantine, `LeaseLostException` on provider
corruption (`:854-858`), serialized grant/cancel/release races (`:1923-1924`), and the reservation
handoff are carefully specified and internally consistent. The one operational soft spot is
observability, not safety: quarantined capacity is correct but invisible (P1-3).

**Package isolation — 9/10.** The dependency direction (`:1458-1466`), the single
`InternalsVisibleTo("OrcaCore.Dag.Hosting")` bridge (`:1468-1473`), and "No Kubernetes/AWS/job-system
type or SDK appears in an OrcaCore public signature or dependency closure" (`:1537`) are precise and
enforceable. Role-specific hosting with no catch-all `AddOrcaCore` (`:1531`) closes the prior
ambiguous-default finding. Not 10 only because the DAG-hosting friend seam rests on process
discipline, not a compiler boundary (acknowledged at `:1471-1473`).

---

## 5. Consumer programs (authored against the `.cs`; friction inline)

**(1) Ephemeral typed workflow: inline lambda, transient pool, `Parallel.WhenAllOutcomes`, following
`If`, state replacement, step + workflow timeout.**

```csharp
var def = Workflow.Ephemeral<OrderState>(defId, DefinitionVersion.Initial)
    .Init<OrderInput>(OrderState.From)
    .CompleteWithin(TimeSpan.FromMinutes(10))                       // FRICTION A: placeable anywhere (P3-11)
    .Then(ctx => ctx.ReplaceState(ctx.State with { Seen = true }))  // sync lambda: no StepResult (P2-5)
    .WithStepTimeout(TimeSpan.FromSeconds(5))                       // FRICTION B: binds to preceding by position (P2-4)
    .Parallel<EnrichFragment>(scope => scope
        .Branch<CrmState>(new AuthoredBranchId("crm"),
            s => CrmState.From(s.Value),                            // FRICTION C: s.Value everywhere (P2-7)
            b => b.WithTransientPool(new TransientPoolName("crm-api"))
                  .Then<FetchCrm>()
                  .Return(s => EnrichFragment.Crm(s.Value)))
        .Branch<GeoState>(new AuthoredBranchId("geo"),
            s => GeoState.From(s.Value),
            b => b.Then<FetchGeo>().Return(s => EnrichFragment.Geo(s.Value))))
    .WhenAllOutcomes((s, outcomes) => s.Value.WithEnrichment(outcomes))
    .If(s => s.Value.EnrichmentOk, then: b => b.Then<Accept>(), otherwise: b => b.Then<FlagForReview>())
    .End<OrderResult>(s => OrderResult.From(s.Value), new WorkflowOutcomeName("processed"))
    .Build();

services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions {
    StructuredExecution = new StructuredExecutionHostOptions {
        MaxConcurrentExecutionPathsPerInstance = 8,
        StepThrottles = new[] { StepExecutionThrottle.For<FetchCrm>(4) } },  // cannot throttle the lambda step (P2-5)
    TransientPools = new[] { TransientPoolDefinition.Create(new TransientPoolName("crm-api"), 4) } });
```
Compiles and is complete; frictions A–C are P2/P3, none blocking.

**(2) Durable bounded `ForEach` with per-item failure summary, restart, typed output.** Works.
`ForEachOptions.Create(maxItems, maxConcurrency)` (`:661-667`) and `WhenAllOutcomes` over
`ForEachItemOutcome<TResult>` give the partial-failure summary; empty input merges once (`:727-728`).
Only friction: item projector cannot read parent state (P2-8), so `items` pre-embeds tenant config.

**(3) Durable resource journey: short DB lease exits before `Wait`, plus a scheduler-capacity lease
enclosing `Wait`.**

```csharp
var def = Workflow.Durable<JobState>(defId, DefinitionVersion.Initial)
    .Init<JobInput>(JobState.From)
    .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(dbPool, 1)),
        lease => lease.Then<WriteSubmissionRow>())                 // DB lease released here, before the wait
    .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(slotPool, 1)),
        lease => lease
            .Then<SubmitJob>()                                     // ctx.ResourceLease.ProtectionToken labels the work
            .Wait(new EventName("job.finished"), s => s.Value.Correlation))  // slot held across the cold wait — correct
    .End(new WorkflowOutcomeName("done"))
    .Build();
```
This is the program the scoped-lease redesign exists for, and it reads exactly right; it alone
validates V1-10.

**(4) Typed DAG (diamond, one failed dependency, independent ready node).** Works.
`Dag.Define<RunInput>(…).Node<…>(id, ref).DependsOn(a, b).MapInput(ctx => new DInput(ctx.OutputOf(a),
ctx.OutputOf(b)))` (`:913-943`) is type-safe; a failed node maps to `CHILD_FAILED` and blocks
transitive dependants while the independent node runs (`:1135-1139`); `AddOrcaCoreDag(new DagHostOptions
{ MaxConcurrentNodes = 16 })` over the durable role. No friction beyond P3-9.

**(5) Companion Kubernetes journey (create-or-observe via `StepOperationId`, protection token, watcher
`EventId`, generic stop confirmation).** Works with no Kubernetes type in any OrcaCore signature: the
step uses `ctx.Execution.OperationId` and `ctx.ResourceLease.ProtectionToken`; the watcher (companion)
calls `IWorkflowEventClient.DeliverByCorrelationAsync`; the reconciler (companion) calls
`IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync`. Boundary holds (§17.5).

---

## 6. Misuse classification

| Attempted wrong program | Result | Evidence |
|---|---|---|
| `Build()` before `End` | **Compile-impossible** | `.cs:66-85`, `:286-291` |
| Register ephemeral def where durable expected / wrong `TInput` on `DurableWorkflowRef` | **Compile-impossible** | `:1665-1675`, `:913-920` |
| Empty/default `DefinitionId`/`DefinitionVersion` | **Fluent/construction** | `:295-302`, `:79-82` |
| Reuse identity/version with changed fingerprint | **Registration** — `DefinitionRegistrationConflict` | `:1881-1883` |
| Durable lambda step | **Compile-impossible** | `.cs:224` |
| Nested `While`/`ForEach`, `WhenFirst`, Saga, `RunExternalJob`, `RunChildren`, `WaitLong`, `Yield` | **Compile-impossible** | `:1549-1573` |
| Attach retry/step-timeout to `Wait`/`End`/join/empty branch; duplicate `WithRetry`; two pools on one step | **Fluent-call throw** (not compile) | `:536-538`, `:1367` — **P2-4** |
| `WhenAll` with one failure | **Runtime** — scope fails, no merge | `:700-704` |
| Empty durable `ForEach` | **Accepted** (valid) | `:727-728` |
| Point/fiber-lifetime lease, empty lease request, duplicate pool, non-positive units | **Compile/fluent** | `:744-752`, `:819-822` |
| Transient/durable pool-name swap; author lease TTL/renewal/holder id | **Compile-impossible** | `:108-118`, `:819-820` |
| Nested acquisition under live ancestor | **Compile + runtime defense** (`SFE-AUTH-LEASE-001`/`SFE-RUN-002`) | `:1211`, `:1214` |
| Sequential root-loop lease scope | **Accepted** (valid) | `:836`, `:1216-1218` |
| `ContinueAsNew` inside leased body | **Compile-impossible** | `.cs:602-762` (absent) |
| Nodes authored after `ContinueAsNew` (dead code) | **Silently accepted** | `.cs:282-284` — **P1-1** |
| Conditional `ContinueAsNew` vs `End` | **Inexpressible** | `:534` — **P1-1** |
| Release quarantine from elapsed time / delete ack / terminal status | **Runtime defense** — remains reserved | `:847-852` |
| Stale/mismatched stop confirmation | **Runtime** — `ConfirmationConflict`/`TokenNotFound` | `:880-887` |
| Force-release API call | **Compile-impossible** (absent) | `:1432` |
| DAG `OutputOf` undeclared/resultless/wrong-typed dependency; caller-owned node sets; public child node | **Compile-impossible / build** | `:961-963`, `:1127-1131`, `:1170` |
| OrcaCore reference to Kubernetes/AWS/Job DTO | **Architecture test** | `:1537` |
| Catch-all `AddOrcaCore` / second hosted-service toggle / codec hook | **Compile-impossible** (absent) | `:1531` |
| Transient pool in durable options / durable pool in ephemeral options | **Compile-impossible** | `:1233-1243` |

One silently-accepted misuse (dead nodes after `ContinueAsNew`) and one inexpressible-but-wanted
program (conditional rollover) — both P1-1. The decorator family is caught, but by a throw not the
type system (P2-4).

---

## 7. Lifecycle analysis and guard-coverage

**Operation identity.** `StepOperationId` stable across retry/timeout-reconciliation/replay/process
replacement/version conflict/competing drivers, new per loop/item/branch/generation (`:332-336`);
`AttemptNumber` diagnostic only. Guardable (task 3.9).

**Deadlines.** `CompleteWithin` one absolute deadline from instance start, inherited across
generations, wins a serialized race, suppresses merges, does not wait for token-ignoring bodies
(`:552-566`); `WithStepTimeout` bounds one attempt, fences the copy, retries with same
`StepOperationId` (`:539-543`). Complete.

**Durable `ForEach`.** Selector committed before admission, replay reuses it, item identity =
scope-occurrence + index, concurrency = min(node cap, host ceiling), empty valid, merge-at-most-once,
ancestor terminal suppresses merge, path-token release/reacquire specified (`:717-728`, `:1373-1385`).
Guardable (task 3.6).

**Leasing.** Lexical pending/held/waiting/quarantined/released transitions, exact
token/obligation/provider-generation matching, grant/cancel and release/waiter races, resize debt,
`LeaseLostException`, no time/renewal reclaim, exhaustive stop-confirmation matrix (§17.2.5, §17.4).
The most rigorously specified area (tasks 3.11a–d) — but see P1-3 for the missing operator-visibility
field.

**DAG.** Node-input commit before child start, internal child-start idempotency, dependency-failure
blocking, independent progress, restart reconstruction, `MaxConcurrentNodes` isolation, sole hosting
bridge (§17.2.6, `:1468-1473`). Guardable (task 3.10).

**Guard-coverage matrix (condensed).**

| Task | Primary seam | Blocking ambiguity? |
|---|---|---|
| 3.1 public baseline | §17.2/§17.7 + `.cs` | **Yes — P1-2** |
| 3.2 packed consumers | §17.5 hosting | no |
| 3.3 provider/protocol edge | `IDurableResourceGovernanceStore` (`:1342-1353`) | no |
| 3.4 staged/nested/leased declarations | `.cs` | **Yes — P1-2, P1-1 (CAN completion type)** |
| 3.5 strong values / codec / projection opacity | §17.2.1 | no |
| 3.6 join/`ForEach` outcomes | §17.2.4 | no |
| 3.7 facades / hosting / reduced management | §17.7 | no (assert P2-6 enumeration absence) |
| 3.8 typed journeys / event routing | §17.7 | no |
| 3.9 retry/deadline/operation identity | §17.2.3 | partial — decorator misuse is a **throw** (P2-4) |
| 3.10 DAG fixtures | §17.2.6 | no |
| 3.11a–d leasing | §17.2.5, §17.4 | no (P1-3 adds a snapshot field to assert) |
| 3.12 run + counts + review | process gate | no |

**Task 3.12 gate.** Correctly requires actual execution counts and whole-section independent review;
expected-red counts are unknown until the packet runs (amendment §7). Appropriate, not a finding.

---

## 8. Does any unresolved decision block guard implementation?

**Yes — two, both narrow.**

1. **P1-2** (join/scope type shape): tasks 3.1/3.4/3.6 cannot assert exact type names while the matrix
   shows one generic `ParallelJoinBuilder<,,>` and the `.cs` shows twelve concrete classes. **Resolve
   before the packet starts.**
2. **P1-1** (`ContinueAsNew` completion shape): task 3.4/3.9 cannot assert what `ContinueAsNew` returns
   or that nodes after it are rejected. **Resolve the return type at minimum;** conditional rollover
   can be deferred if added to §17.6.

Everything else in §7 is executable as written. P1-3 and all P2s add fields/notes, not new type
decisions, and can be implemented as guard work proceeds.

---

## 9. Validation commands run

```
openspec validate reshape-developer-facing-interfaces --strict   → valid
openspec validate add-runtime-concurrency-limits --strict        → valid
git diff --check                                                 → no whitespace errors
grep -cE '^public …(class|record|interface|enum)' 17-public-authoring-contract.cs → 51 (44 builder/scope/join)
```
Guard/source projects were not built (known stale per the amendment); no build result is presented as
planning compliance.

---

## 10. One-line summary

A genuinely smaller and more coherent v1 that is close to guard-ready; fix the `ContinueAsNew` shape,
reconcile the twelve-vs-one join-type ambiguity between the matrix and the `.cs`, and give operators
visibility into quarantined lease capacity — then the Phase 0 guard-only packet can proceed.
