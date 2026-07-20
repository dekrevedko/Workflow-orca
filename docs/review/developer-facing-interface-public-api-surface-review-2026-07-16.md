# Independent review: proposed OrcaCore public API surface (2026-07-16)

Reviewer: independent senior API reviewer. Scope: the proposed developer-facing surface as
defined by `docs/specs/17-selected-mode-capability-matrix.md` §17.1–§17.6, the canonical
requirements it cites, and the two coordinated OpenSpec changes. Method: signature pass over
§17.2, four authored journeys, adversarial misuse pass, lifecycle stress, vocabulary audit,
gap hunt. Both changes pass `openspec validate --strict`. No file was modified.

---

## 1. Verdict

**APPROVE WITH CHANGES.**

Three considerations drove it:

1. **The lifecycle model is sound; the authoring surface for it is incomplete.** The no-author-TTL
   lease contract (§17.4, MG-062/064) is the strongest part of this baseline — capacity accounting
   is state-defined, every recovery mutation is fenced by exact obligation/ticket/generation, and
   the ambiguous-owner tradeoff is stated rather than hidden. I tried to break it and mostly
   could not. But the *author-facing* half has one hole: acquisition is a point-in-time node whose
   release is bound to fiber exit, and there is no scoped form. The driving scenario's headline
   capacity requirement (JS-007: "release happens on the job's terminal outcome") is therefore
   reachable only by decomposing into child instances, or by the reserved job-bracket that task 5.0
   has not approved. Everything else over-holds capacity silently (P1-2).

2. **§17.2 is presented as the complete approval baseline and is not.** It approves `Wait`, `End`,
   `Parallel`, `WhenFirst`, `Branch`, `Return`, `AcquireResources`, the strong names, and the
   completion pair — and omits `Init`, business `Then`, `If`, `While`, `Delay`, `ForEach`,
   `ContinueAsNew`, and child workflows entirely, while stating that "names may vary only through
   an explicit amendment to this document" (17:45–46). Only `RunExternalJob` carries a reservation.
   Phase 4 source work begins on members that have no approved shape (P0-1). This is the same
   invented-contract risk the task-5.0 reservation exists to prevent, at roughly six times the
   scale.

3. **Where the surface is specified, it is genuinely good.** The mode-first split, the strong
   matching-name family, the three-lifetime taxonomy, the split-host `AppliedAndProgressed` /
   `AppliedPendingContinuation` distinction, hiding compiled IR, and the deliberate absence of an
   acquisition-timeout overload are all correct and I endorse them below (§8). None of my findings
   requires re-opening a Decision 1–12; all are additive or editorial.

Approval is conditional on P0-1 and the four P1s being resolved by matrix amendment before their
phase begins — for P0-1 that means before Phase 4, not before Phase 5.

---

## 2. Findings

### P0

#### P0-1 — §17.2 omits approved signatures for eight authoring members it treats as approved

**Evidence.**
`docs/specs/17-selected-mode-capability-matrix.md:44-46`:

> "The signatures below are the approval baseline for source implementation, compile fixtures,
> samples, and public-signature files. Names may vary only through an explicit amendment to this
> document; equivalent provisional overloads are not permitted."

The block that follows (17:48–255, 299–320) approves: the two mode factories, eight strong-name
records, `Build`/`TryBuild` for workflow and saga, `Saga.Ephemeral`/`Saga.Durable`, `Dag.Plan`,
`Parallel`, `WhenFirst`, `Branch`, `Return`, root and branch `Wait`, `WaitLong`, three `End`
overloads, `StepResult.WaitForEvent`, `BranchResult<TResult>`, `WithTransientPool`,
`ResourceLeaseRequirement`, and four `AcquireResources` overloads.

It never gives a signature for:

| Member | Status in the baseline | Evidence |
|---|---|---|
| `Init` | Named as portable capability; CR-005 makes it the single owner of input→state | `17:21`, `docs/specs/04-requirements-core-runtime.md:36-39` |
| business step / `Then` | "Both ephemeral and durable branch builders expose business `Then`" | `17:270-271` |
| `If`, `While` | Portable row; `While` is load-bearing for `SFE-AUTH-LEASE-002` | `17:21`, `17:366-367` |
| `Delay` | Portable row | `17:26` |
| `ForEach<TItem, TItemState, TResult>` | Generic arity given, signature not | `17:278-281` |
| `ContinueAsNew` | "accepts a deterministic replacement-state selector on the durable root builder" — prose only | `17:291-293` |
| child workflows / `RunChildren` | §17.1 row "Durable only"; open question 15 makes `RunChildren`-style orchestration the **resolved DAG compile target** | `17:30`, `docs/specs/13-phasing-and-open-questions.md:155-160,166-168` |
| DAG registration/execution entry | "an operation on the durable runtime over a registered plan" — no member | `17:288-289` |

`openspec/changes/reshape-developer-facing-interfaces/design.md:134-144` then writes the golden
path against two of them (`.Init<OrderInput>(OrderState.From)`, `.ForEach(...)`) — a precedence-3
document exercising members that the precedence-1 baseline does not approve.

**Scenario where it bites.** Task 4.1 ("Introduce immutable `EphemeralWorkflowDefinition<TState>`
… ") and task 4.7 ("rewrite repository tests, samples, benchmarks … directly against the final
mode-first API") both require an implementer to commit to `Init`, `Then`, `If`, `While`, and
`ForEach` shapes. There is no approved shape, so they will be invented at the keyboard, land in
compile fixtures and the public baseline (task 4.14 "approve the resulting application
signatures"), and become de-facto normative through the guard project rather than through the
matrix. That is exactly the cycle that produced the superseded `AcquireLease(id selector,
ResourceLease selector, duration)` invention recorded at
`docs/review/developer-facing-interface-phase-00-lease-contract-amendment-draft-2026-07-16.md:201`.
The reservation mechanism already exists and works; it is simply applied to one member out of nine.

**Smallest normative remediation.** In §17.2, for each member above, either (a) add the approved
signature, or (b) add a one-line reservation in the form used for `RunExternalJob` at 17:342–347,
naming the task that must approve it before its source task begins. `Init`, `Then`, `If`, `While`,
`Delay`, and `ForEach` are portable and already implemented provisionally — approving them costs
eight lines. `ContinueAsNew` and child workflows deserve reservations: `ContinueAsNew` because its
selector shape interacts with `SFE-AUTH-LEASE-003`, child workflows because open question 15 makes
them the DAG substrate and their signature drags in partitioners (CP-030) and dispatch windows
(CP-023).

### P1

#### P1-2 — Durable leases have no scoped release; fiber exit is the only release point, so the driving scenario's own capacity requirement either over-holds or is inexpressible

**Evidence.**
`17:34` — "deterministic release is normal". `17:338-339` — "Every release preserves that exact
obligation, instance, generation, fiber/scope occurrence, ticket, pool, unit, and provider-generation
identity from acquisition." `17:299-301` — "there is no author-facing duration, expiry-backstop,
holder ID, renewal token, public constructor, or mutable initializer". No release member exists on
any builder. Release triggers are exhaustively listed at
`openspec/.../durable-runtime/spec.md:121-124`: "normal fiber exit, branch cancellation, scope
failure, or graceful owner cancellation before owner removal/parent resume".

Against that, `docs/specs/14-driving-scenario-eks-job-scheduler.md:112-114`:

> "- tickets are held for the **entire job lifetime**, across the cold wait and any scheduler
>    restarts …
>  - release happens on the job's terminal outcome — success, failure, timeout-kill (JS-AC-006)
>    and run cancellation (JS-006) included;"

"Release on the job's terminal outcome" is not one of the four release triggers. The only shape in
the baseline that would provide it is reserved: `17:344-347` — "optional job-bracket resource shape
… Any job-bracket resource request uses `ResourceLeaseRequirement`, releases on
completion/failure/acknowledged timeout or acknowledged cancellation/stop".

**Scenario where it bites.** Two distinct bites.

*Silent over-hold.* An author writes the obvious thing on a durable root fiber:

```csharp
.AcquireResources(ResourceLeaseRequirement.Require(dbA, 2))
.RunExternalJob(/* reserved */)          // job ends, reports, resumes the fiber
.Then<PublishResults>()                  // 40 minutes of downstream work
.WaitLong(EventNames.OperatorSignOff, s => s.CorrelationId)   // hours or days
.End(Outcomes.Done);
```

Every rule passes: one acquisition on the inclusive ancestry, no loop, no `ContinueAsNew`. Two
database connections stay reserved across the sign-off wait, because the fiber has not exited. No
compiler diagnostic, no runtime diagnostic, no management signal distinguishing "held and using it"
from "held and idle" — `management-and-querying/spec.md:98-105` exposes reserved-units-by-state,
and this is legitimately `held`. Capacity is lost for the pool's whole configured capacity across
every instance sharing the store. This is my only *silently accepted* misuse-pass result (§7,
row M-13), and it is the one the driving scenario is most likely to hit.

*Forced decomposition.* Sequential use of two pools on one fiber —

```csharp
.AcquireResources(Require(dbA))  // job A
.AcquireResources(Require(dbB))  // job B  → SFE-AUTH-LEASE-001, same-fiber reacquisition
```

— is rejected (`17:335-336`: "same-fiber reacquisition and ancestor/descendant acquisition are
illegal"). The author's only legal encodings are (a) one atomic request holding both pools for the
union of both jobs' lifetimes — worse for capacity, and explicitly what `17:336` recommends
("Resources needed concurrently belong in one request" — they are not concurrent here); (b) wrap
each in a single-branch `Parallel` purely to manufacture a fiber boundary; or (c) child instances.
(b) is the idiom `openspec/.../workflow-authoring/spec.md:138` blesses for loops — "a fresh child
fiber that releases each iteration remains valid" — but a `Parallel<T>` with one `Branch` and a
merge that discards the result is ceremony standing in for a `using` block.

**Smallest normative remediation.** Add a scoped overload to §17.2 alongside the four point-node
overloads, and make it the documented idiom:

```csharp
DurableWorkflowBuilder<TState> AcquireResources(
    IReadOnlyList<ResourceLeaseRequirement> requirements,
    Action<DurableResourceScopeBuilder<TState>> body);
```

The lease occurrence is owned by the scope-entry occurrence already in the identity tuple
(`docs/specs/09-requirements-management-operations.md:212-213` already persists "scope-entry
occurrence"), releases at scope exit on every path, and needs no new protocol state. It collapses
three current rules into structure: `SFE-AUTH-LEASE-002` disappears (a scope in a `While` body
releases per iteration by construction); `SFE-AUTH-LEASE-003` reduces to "not inside a resource
scope"; ancestor/descendant conflict becomes lexical nesting the compiler already sees.
The point-node form can remain for the child-instance-per-job case where fiber exit genuinely is
the intended release. If the scoped form is rejected, the minimum is a normative sentence in §17.4
stating that the fiber is the lease scope and that authors must size fibers to resource lifetime,
plus a `SFE-AUTH-LEASE-004` warning when a lease-holding fiber reaches a cold wait or child
dispatch that is not the resource's own protected work. I do not recommend that second path — the
analysis is undecidable in general and the warning would be advisory noise.

This is also my answer to "what single change would most improve the surface" (§9.6).

#### P1-3 — Durable branch fibers cannot wait cold and cannot dispatch external jobs, so durable fan-out over external work is not expressible in the approved surface

**Evidence.** `17:272-276`:

> "Neither branch family exposes root `Init`/`End`, `If`, `While`, in-instance `ForEach`,
> `WaitLong`, child workflows, external jobs, or `ContinueAsNew`."

Durable branches expose `Wait` (17:228–230) and `AcquireResources` (17:313, 17:271–272). Per
`docs/specs/05-requirements-events-waits-timers.md:108-112` (EV-040), `Wait` "SHALL suspend the
instance (`Waiting` status) with a `Resident` wait record. In ephemeral mode it is
activation-local; in durable mode it is fully durable but the instance may remain hot." EV-041
(`05:113-118`) gives cold eviction only to `WaitLong`.

**Scenario where it bites.** The EKS scenario's fan-out node — "Fan-out node (map over N inputs) →
`RunChildren` + partitioners" (`14:38`) — routes around this by using child instances, which is
consistent. But a developer reaching for the obvious tool does not: `Parallel` over three
enrichment branches where each branch calls out to a service and waits for a callback is the single
most common durable fan-out shape, and the only wait available inside a branch keeps the whole
instance resident for the duration. The developer gets a `Wait` that compiles, persists its
obligation (`17:24`), survives restart, and pins memory for six hours with no diagnostic, because
EV-040 says the instance *may* remain hot — it is a permission, not a bound. The absence is
deliberate (`17:275-276`: "Adding any further nested capability requires a matrix amendment and
matching runtime plus compile-fixture evidence") but it is not *discoverable*: nothing in the
surface, IntelliSense, or a diagnostic tells the author that branch `Wait` is the hot one and that
cold fan-out means child instances. See also §9.5.

**Smallest normative remediation.** Either (a) amend §17.2 to expose `WaitLong` on
`DurableBranchBuilder<TBranchState, TResult>` with the runtime/compile-fixture evidence §17.2
demands — the branch fiber's obligation is already persisted, so this is a residency-policy change,
not a new capability; or (b) keep the absence and add one line to §17.2 next to 17:272-276 naming
the reason and the sanctioned alternative ("cold fan-out is authored as child instances; branch
`Wait` keeps the instance resident"), so the phase-9 documentation and the `SFE-CAP-*` diagnostic
have a normative source. (b) is sufficient; (a) is better if the runtime already supports it.

#### P1-4 — `ExternalJobId` has no construction path outside the JSON converter, which the split-host completion journey requires

**Evidence.** `17:101-105`:

```csharp
public sealed record ExternalJobId
{
    private ExternalJobId(string value);
    public string Value { get; }
}
```

`17:136-139`: "`ExternalJobId` is runtime-generated per occurrence and can be round-tripped by the
library-supplied scalar JSON/protocol converter used by hosting and model binding, but is not
selected by a workflow author. The converter invokes a non-public reconstruction seam; no public
raw-string factory or facade overload is added."

`17:468-471` requires that completion target that ID: "Completion/failure targets that ID and
accepts one `EventId reportId` per logical report … the authored `ExternalJobKey` is not a
completion target."

**Scenario where it bites.** Journey (d) below. A definition-less callback host receives the EKS
watcher's normalized envelope. Per `14:80-83` the watcher raises `JobSucceeded`/`JobFailed`
carrying the job's correlation — in practice a Kubernetes Job label or annotation, i.e. a string
read from a `V1Job` object, a queue message header, or a database column. To call the completion
facade, that host must produce an `ExternalJobId`. It cannot: the constructor is private, there is
no factory, and the only reconstruction seam is a JSON converter. The host's escape hatches are
`JsonSerializer.Deserialize<ExternalJobId>($"\"{raw}\"")` — a string-concatenated JSON literal to
work around a missing parser, which is worse than the factory the rule forbids — or reflection.
Task 3.9's "two-host failing acceptance fixture" will hit this on its first line.

The rule's stated purpose is that the ID "is not selected by a workflow author". A `Parse` on the
type does not violate that: authors cannot invent a valid occurrence, because the runtime resolves
the ID against committed occurrence state and returns a typed no-match — the value's authority
comes from the store, not from the type's constructor visibility. The private constructor buys
nothing here that validation and runtime lookup do not already buy, and it costs the one journey
the surface most needs to get right.

**Smallest normative remediation.** Amend 17:136–139 to add
`public static ExternalJobId Parse(string value)` / `TryParse`, validating with the same rules as
the author-constructible names, and keep "not selected by a workflow author" as a documented
property enforced by the absence of `New()`/`Create()` rather than by the absence of parsing. Keep
the converter as the transport path.

#### P1-5 — The strong-type family is bifurcated: matching names validate at construction, identities do not

**Evidence.** `17:130-134` for the eight new names: "immutable value objects with validating
construction and value equality … All author-constructible names reject null, empty, whitespace,
leading/trailing whitespace, and any explicitly specified type limit".

The identity family it composes with does not:
`src/OrcaCore.Abstractions/Ids/DefinitionId.cs:9-17` — `public readonly record struct DefinitionId`
with `public DefinitionId(Guid value) { Value = value; }`; no validation, `default(DefinitionId)`
is `Guid.Empty`, and `new DefinitionId(Guid.Empty)` is accepted. `EventId` (`Ids/EventId.cs:9-17`)
is identical in shape and is the type §17.5 uses for the cross-host `reportId` matching contract.

The baseline's response is to re-reject them everywhere:
`openspec/.../workflow-contracts/spec.md:42-49` — "Every public application and advanced operation
SHALL reject empty/default strong identifiers … at the nearest seam"; `17:139` — "Every
public/advanced seam rejects null/default again".

**Scenario where it bites.** Every seam, forever. `Workflow.Durable<TState>(default, default)`
compiles today and must be caught by a guard, a test, and a diagnostic at each of the dozens of
public entry points the surface is growing — whereas `new EventName(null!)` is impossible to get
past the constructor once. This is the single largest source of the "reject again at every seam"
tax in the change, and on a repository whose review prompt states "there are no external consumers,
no compatibility constraints, and no migration obligations", the tax is voluntary. The review goal
says "identities and matching values typed" and rates *misuse resistance* by whether "wrong chains
[are] impossible or rejected early" — the matching-values half achieves impossible, the identity
half achieves rejected-late-and-repeatedly.

Secondary: `DefinitionId` is a GUID, but definition identity is a *cross-host, cross-deploy,
authoring-and-configuration* matching contract — DU-053 binds the start key to it (`06:160-161`),
`ForDefinition(id)` filters by it (`design.md:288`), and `orca.definition.version` is a telemetry
dimension (`15`). Every other value in that class became a validated name in this change. A GUID
constant pasted into an authoring file is the least greppable, least operator-legible choice
available, and it is inconsistent with `EventName`/`WorkflowOutcomeName`/`ResourcePoolName` for no
recorded reason.

**Smallest normative remediation.** Add one row to §17.2's strong-name block giving `DefinitionId`,
`InstanceId`, `DefinitionVersion`, and `EventId` the same treatment the eight names received
(validating construction, no default-representable invalid value), or — if the struct shape is
being kept for allocation reasons at protocol volume — add one sentence to §17.2 recording that
exception, its reason, and that the "reject default again at every seam" requirement exists to pay
for it. The current documents assert both "strong identifiers reject default and empty values" and
a type family where `default` is a language-guaranteed constructible value; a reader cannot tell
which is the decision. The `DefinitionId`-as-name question should be answered explicitly even if
the answer is "GUID, deliberately".

### P2

#### P2-6 — §17.2 and §17.3 contradict each other on whether `StepResult.AcquireResources` exists

`17:292-294`: "Portable `StepResult` remains limited to completed, expected failure, dynamic
`WaitForEvent`, and cooperative `Yield`." `17:241-246` shows `StepResult` with `WaitForEvent` only.

`17:379-383`: "Until the qualified portable member `StepResult.AcquireResources` is absent, the
compiler cannot see every dynamic acquisition and MUST NOT be treated as the sole enforcement
layer."

Within one normative document, §17.2 says the member does not exist and §17.3 says it does until
removed. The "Until …" phrasing additionally implies the runtime defense becomes optional once it
is gone, which contradicts `17:378-379` ("Runtime defense repeats … This protects manual, stale,
and legacy plans") and MG-063 (`09:253-256`), both of which make the defense permanent for
hand-built and stale plans. `docs/specs/09-requirements-management-operations.md:255-257` phrases
the same idea correctly: "Until portable `StepResult.AcquireResources` is removed, build-time
validation cannot claim complete coverage and this runtime defense is mandatory."

**Remediation.** In §17.3, replace the "Until …" sentence with the two separable claims: runtime
defense is permanent because hand-built/stale/legacy plans exist; and, additionally, build-time
coverage is incomplete until task 5.4 removes the transitional portable member. The transitional
clause belongs in the task graph, not in the normative baseline that also asserts the member's
absence.

#### P2-7 — `Dag.Plan()` and `Saga.*` are shaped inconsistently with `Workflow.*`, and durable DAG registration has no member

`17:127` — `WorkflowDagBuilder Dag.Plan();` takes no `DefinitionId`, no `DefinitionVersion`, and no
`WorkflowAuthoringOptions`. `17:113-125` — `Saga.Ephemeral`/`Saga.Durable` take identity but no
`WorkflowAuthoringOptions`, although `17:141` states "Workflow and saga `Build()` and `TryBuild()`
invoke the same internal definition compiler" and `17:148` states "DAG planning uses the same
completion and diagnostic contract". Three factories for three definition kinds, three different
parameter lists, no stated reason.

The DAG gap is the sharper one: `17:288-289` — "Durable DAG execution is an operation on the
durable runtime over a registered plan, not a caller-driven runner", and `design.md:343` — "The
durable runtime accepts a plan and root identity". Registration in the facade is
`runtime.Definitions.Register(definition)` returning `DurableDefinitionHandle<TState>`
(`design.md:246`, `durable-runtime/spec.md:45-50`) — typed by a state type a `WorkflowDagPlan` does
not have, and CR-004 (`04:32-34`) requires every definition to carry identity and version, which
`Dag.Plan()` cannot produce. Task 8.2 ("Add a durable DAG facade accepting a validated plan and
root identity") will therefore invent both the identity model and the registration member.

**Remediation.** Give `Dag.Plan(DefinitionId, DefinitionVersion, WorkflowAuthoringOptions?)` the
workflow factory's parameter list, add `WorkflowAuthoringOptions?` to both saga factories or record
why saga cannot configure serializers it demonstrably uses, and add the DAG registration/execution
member to §17.2 or reserve it in the RunExternalJob form (this is part of P0-1).

#### P2-8 — `EventId reportId` is the only cross-host matching contract in the external-job family without its own type

`17:468-471` and `06:191-196` establish three non-interchangeable external-job roles and give two
of them dedicated types (`ExternalJobKey`, `ExternalJobId`). The third — the worker's
retry-stable deduplication identity — reuses `EventId`, a `readonly record struct` over a GUID
shared with inbound engine events. §17.2's own rule (`design.md:387`) is that "strings that must
match across call sites, hosts, or authoring/configuration become strong matching values". A
worker's `reportId` matches across hosts, is authored by the worker, must be stable across
redelivery, and is passed positionally next to an `ExternalJobId` — `Complete(jobId, reportId, …)`
with two opaque scalars adjacent is exactly the swap the family exists to prevent (`EventId` and
`ExternalJobId` are different types, so the swap will not compile — but `EventId reportId` versus
any other `EventId` in scope will).

**Remediation.** Either introduce `ExternalJobReportId` with the family's rules at task 5.0, or
record in §17.5 that `EventId` is intentionally reused because the report *is* an engine event
identity in the protocol, and that no other `EventId` is in application scope at that call. The
second is a defensible answer; the absence of either is the finding.

#### P2-9 — `WaitId` is exposed "for targeting" but nothing accepts it

`17:488-491`: "`WaitId` remains application-visible as an opaque handle for targeting and
diagnostic correlation; it carries no fiber, scope, ordering, or checkpoint semantics."
`management-and-querying/spec.md:54` repeats "SHALL support application targeting and diagnostic
correlation". No approved member takes a `WaitId`: `IWorkflowInstanceHandle` is `GetAsync`,
`GetStateAsync<TState>`, `CancelAsync`, `TerminateAsync` (`design.md:299-304`); event delivery is
by `EventName` + routing identity (`durable-runtime/spec.md:90-91`). "Targeting" therefore names a
capability the surface does not have. Either a `RaiseEventAsync(WaitId, …)`-style member is
intended and missing, or `WaitId` is diagnostic-only and the word "targeting" is ceremony in a
document that is otherwise precise about this distinction. This is question §9.3's clearest hit.

**Remediation.** Delete "targeting and" from 17:488 and `management-and-querying/spec.md:54`, or
add the targeting member to §17.2.

#### P2-10 — `params ResourceLeaseRequirement[]` makes the empty request compile

`17:311-314` — `AcquireResources(params ResourceLeaseRequirement[] requirements)`; `17:322-323` — "A
static node copies its input and rejects a null/empty array, any null requirement element, or
duplicate pool at the fluent call." So `.AcquireResources()` compiles and throws. Against the
review goal ("unsupported, impossible, or obviously wrong API call chains should be
unrepresentable at compile time") and against this document's own standard elsewhere —
`ResourceLeaseRequirement` got a private constructor and a factory specifically to make its invalid
states unrepresentable — the empty request is the one invariant left to a runtime check when the
type system would take it.

**Remediation.** `AcquireResources(ResourceLeaseRequirement first, params ResourceLeaseRequirement[] rest)`.
Duplicate-pool and null-element checks stay at the fluent call; empty becomes a compile error. The
dynamic selector overload keeps its runtime validation, correctly.

#### P2-11 — There is no outcome for "no host has this definition version registered" during continuation

`DU-054` (`06:174-179`) and `durable-runtime/spec.md:60-62` specify `DefinitionNotRegistered` for
**start**. `DU-055` (`06:181-186`) makes every accepted operation commit "with an at-least-once
continuation handoff" and drive inline "only when the bound definition is registered locally",
returning `AppliedPendingContinuation` otherwise, which "requires a definition-owning host pump to
progress the instance".

Nothing specifies what happens when no host owns it. Concretely: v1 instances are in flight
(`06:116` — "Every durable instance SHALL be permanently bound to `DefinitionId` +
`DefinitionVersion`"); a deploy registers only v2 (registration is explicit and host-scoped, and
nothing requires retaining v1); a worker reports a v1 external-job completion. The callback host
commits, returns `AppliedPendingContinuation` — a *success* result — and the continuation record
sits in the outbox with no pump that can drive it. The application has a green result and a stalled
instance. `DU-052` (`06:145-147`) makes "outbox backlog" observable, which surfaces the symptom as
a queue depth, not as "definition version 1 is unowned".

**Remediation.** One requirement in `durable-runtime`: the continuation pump SHALL expose an
unowned-definition-version condition as an operational diagnostic (and management SHALL surface
in-flight instance counts by `DefinitionId`+`DefinitionVersion`), so an operator can see that a
version must remain registered before retiring it. This is cheap and it is the difference between
a documented deploy rule and a silent stall.

#### P2-12 — `TargetPaused` does not say what happens to the event

`durable-runtime/spec.md:101-103`: "**WHEN** an event resolves to a paused target **THEN** delivery
returns `TargetPaused` and does not silently discard or apply the event." EV-030 (`05:100-105`)
says "An event is consumed only when (1) it matched a wait AND (2) the resulting transition is
committed. Until both hold, the event SHALL remain available." Between them: is the event held for
delivery on resume, or is `TargetPaused` a rejection the caller must retry? "Does not silently
discard" excludes only *silent* discard. For a `WaitLong` on an at-most-once source (the EKS
watcher's `JobSucceeded`, `14:80-83`), the difference is a lost job outcome versus a queued one,
and the caller cannot tell which contract it is programming against from the result name.

**Remediation.** State the disposition in the scenario: either "the event is retained and delivered
when the instance resumes" (then `TargetPaused` is informational and the name should say so) or
"the event is not accepted and the caller must redeliver after resume" (then the pause/resume
section owes a statement about sources that cannot redeliver).

#### P2-13 — `ForEachItemOutcome<TResult>` and `BranchResult<TResult>` are two shapes for one concept

`17:248-251` — `BranchResult<TResult>(AuthoredBranchId BranchId, int Ordinal, TResult Value)`.
`17:278-281` — "Ephemeral `ForEach<TItem, TItemState, TResult>` uses the same item-private builder
and return contract. Its merge receives `IReadOnlyList<ForEachItemOutcome<TResult>>` in item-index
order." "The same … return contract" but a different result type, whose name asserts a different
concept: a `Result` carries a value, an `Outcome` implies success-or-failure. `BranchResult` has no
failure representation — consistent with `17:23` ("failed winner fails without merge") and with
`Parallel` failing its scope — but if `ForEachItemOutcome` is *not* discriminated, the name is
wrong; if it *is*, then per-item failure tolerance is a semantic difference between `Parallel` and
`ForEach` that no requirement states, and the merge author's obligations differ between two
"analogous members" in the sense §17 uses that phrase.

**Remediation.** Either rename to `ForEachItemResult<TResult>` with `AuthoredItemIndex`/`Ordinal`
symmetry with `BranchResult`, or give `ForEachItemOutcome` an approved signature in §17.2 (it has
none — this is inside P0-1) and state the failure-tolerance difference in §17.1's `ForEach` row.

#### P2-14 — Ephemeral best-effort `StartOrGet` reuses `StartIdempotencyKey` with a weaker guarantee

`06:171-172`: "Ephemeral mode MAY offer a best-effort variant using the same application type
without claiming durable deduplication." The same type, the same method name, and a guarantee that
silently degrades from "retrying a lost start returns the existing instance" to best-effort — in a
document whose central thesis is that `TransientPoolName` and `ResourcePoolName` must be different
types precisely because host-local and persisted capacity are different guarantees (`17:406-410`).
The same argument applies here and reaches the opposite conclusion. A developer moving code from
ephemeral to durable is served by the shared type; a developer moving *from durable to ephemeral*
loses deduplication with no signal — and `17:16-17` says mode-first exists so that "a static
mode-first builder exposes only capabilities guaranteed by every supported host for that mode".

**Remediation.** Either drop the ephemeral variant (an ephemeral engine that loses state on restart
has little use for restart-idempotent start), or record in §17.5 why the type is shared here while
the pool names are split — a two-sentence exception, not a redesign. I lean toward dropping it.

#### P2-15 — MG-062's pre-fiber "the instance suspends" survives against §17.2's "parks only its requesting fiber"

`09:207-209`: "acquisition behaves like a wait: with no ticket available, the instance suspends
(`Waiting`, cold-capable) and resumes when granted". `17:333-335`: "an unavailable request parks
only its requesting fiber until the request is granted or that exact owner is canceled, fails, or
terminates", and `workflow-authoring/spec.md:140-142` requires "saturation may park either sibling
without blocking the other's runnable quantum". The canonical requirement says instance; the matrix
and the delta say fiber. Since `17:8-9` makes conflicts with the matrix non-conforming, MG-062 is
the stale one — but MG-062 is a *canonical* requirement and `design.md:449-464` requires canonical
amendment before each source section, so this one should be fixed at task 5.0 rather than left for
a reader to adjudicate by precedence.

**Remediation.** Amend MG-062's clause to fiber-level parking with instance-level `Waiting` status
only when no sibling is runnable.

#### P2-16 — The application active-wait projection is event-shaped, but leases and delays park too

`17:481-484`: "Application active-wait projections expose stable authored facts such as wait kind,
authored node/path, `EventName`, correlation, residency, and relevant logical timing."
`management-and-querying/spec.md:53-58` repeats it. But MG-062 (`09:207-208`) makes an unavailable
acquisition a wait — the instance is `Waiting` — and `Delay` (`17:26`) parks with no event at all.
For those wait kinds `EventName` and correlation have no value, so either the model carries two
`null`-able fields that are meaningless for half its instances, or `wait kind` discriminates and
the projection needs a shape per kind. The document's own standard ("one shape per concept, no
surprises between analogous members") is not met by a single flat record here, and this is the
model that task 3.5/3.6 will guard.

**Remediation.** Specify the projection as a discriminated family (`EventWait`, `TimerWait`,
`ResourceWait`) in §17.6, or state that `EventName`/correlation are optional and that
`ResourceWait` additionally projects `ResourcePoolName` and queue position — the observability of
parked acquisitions is otherwise only available through pool administration
(`management-and-querying/spec.md:98-105`), i.e. per-pool, not per-instance. An operator asking
"why is instance X stuck" should not have to enumerate pools.

### P3

#### P3-17 — `sealed record` for the strong names buys a `with` that the prose must then forbid

`17:59-105` declares all eight names as `public sealed record`; `17:130-133` then requires "they are
not positional records and expose no `init` setter or invariant-bypassing `with` target." A sealed
class with a validating constructor, `Value`, and equality members needs no such sentence — the
`record` keyword is being chosen for equality and `ToString`, then partially disclaimed. The
residual `x with { }` (a legal clone expression on any record) is harmless but exists solely
because of the keyword. Minor; noted because §17.2 is a document whose value is that it says
exactly what it means.

#### P3-18 — `AcquireResources` is the only member in the lease vocabulary that does not say "lease"

The concept is a "durable resource lease" (`17:34`, `17:408-410`); the requirement type is
`ResourceLeaseRequirement`; the identity is `LeaseObligationId`; the failure is `LeaseLost`; the
diagnostics are `SFE-AUTH-LEASE-001/002/003` and `SFE-RUN-002 LeaseAncestryViolation`; management
calls it "lease administration"; MG-062 calls it "ticket (lease) semantics". The one thing a
developer types is `AcquireResources`. The recorded rationale is
`docs/review/developer-facing-interface-phase-00-lease-contract-amendment-draft-2026-07-16.md:33`:
"`AcquireResources`, matching the portable member it replaces; no third `AcquireLease` name".

I disagree with the reasoning, though not strongly enough to move the finding above P3. Continuity
with `StepResult.AcquireResources` is worth nothing: task 5.4 deletes that member, there are no
external consumers, and no developer will ever have typed it. The decision optimizes for a name
that will not exist against a vocabulary that will. `AcquireLease(...)` is not a "third name" once
the portable member is gone — it is the same name as everything else in the family. The one real
argument for `AcquireResources` is that the author requests *resources* and the lease is the
runtime's representation of the grant — which is a good argument, and if that is the reason it
should be the recorded reason, because it also predicts that the compiler diagnostics facing the
author (`SFE-AUTH-LEASE-*`) name a concept the author never typed. Verdict: keep the name, fix the
rationale, and note that `LeaseLost` is the one *author-visible* outcome in the lease vocabulary
whose noun appears nowhere in the authoring surface.

---

## 3. Dimension scores

**Consistency — 7/10.** One vocabulary is genuinely maintained where it was designed: the three
concurrency lifetimes never blur (`17:406-446`), `TransientPoolName`/`ResourcePoolName` are
structurally impossible to swap, ephemeral/durable pairs differ exactly where §17.1 says they must,
and the ephemeral/durable `Parallel`/`WhenFirst`/`Branch`/`Return`/`Wait` families are line-for-line
identical modulo the mode token. The losses are at the edges the matrix did not reach: three
definition factories with three parameter lists (P2-7), two result shapes for one branch-result
concept (P2-13), an identity family that validates differently from the matching-name family it
sits next to (P1-5), and one authoring verb outside its own vocabulary (P3-18). None is deep; all
are visible to the first developer who reads two adjacent members.

**Comprehensiveness — 5/10.** This is the weak dimension, and it is weak in a specific,
fixable way: the *judgment* is comprehensive and the *baseline* is not. Every hard question I went
looking for has an answer somewhere — capacity accounting, resize debt, grant/cancel races,
split-host continuation, provider collation, quarantine — and the gaps that remain are named
(`RunExternalJob`, durable transient pools, `Saga.Durable`). But eight authoring members that Phase
4 will implement have no approved signature (P0-1), the DAG execution journey has no entry point
(P2-7), the driving scenario's own capacity requirement needs a reserved shape (P1-2), and durable
cold fan-out has no expression in a branch (P1-3). "Gaps are named, not silent" is the standard;
these four are silent.

**Developer orientation — 7/10.** Discoverability is the best-executed idea here: choosing a mode
before IntelliSense opens (`design.md:129-152`) is worth more than any amount of documentation, and
preserving it through nested builders (`17:256-260`) is the part most libraries get wrong. Error
quality is high where specified — stable codes, primary *and related* locations for every lease
diagnostic (`17:386-393`), aggregate `TryBuild`, ordered diagnostics. Implementation vocabulary is
held out of the application tier with unusual discipline (`17:474-496`). Deductions: a callback host
cannot construct the ID its own API demands (P1-4); `WaitId` advertises targeting that does not
exist (P2-9); a developer cannot learn *why* durable `ForEach` or branch `WaitLong` are absent from
anything but the spec repository (§9.5); and the two-name residency model (`Wait`/`WaitLong`) has a
permanent, resolved rationale (`13:95-100`) but no author-facing bound — a `Wait` that waits a month
is legal.

**Misuse resistance — 8/10.** The strongest dimension. Of the 22 wrong programs I attempted (§7),
14 are compile-impossible, 4 are rejected at the fluent call or `Build()`, 3 are rejected at
registration/startup or by fenced runtime defense, and exactly 1 is silently accepted — and that
one (P1-2, holding a lease past the work it protects) is a capacity leak rather than a correctness
failure. Defaults are safe: `DestructiveOperationConfirmation.None = 0`, no acquisition timeout to
mis-set, no TTL to guess wrong, `End()` unnamed rather than empty-string-named, debt that blocks
grants rather than admitting them. Defense in depth is real rather than rhetorical — the compiler's
ancestry analysis is repeated at runtime over "the union of persistent pending lease records and
held active tickets" (`17:378-379`) before any pool mutation, which is what catches the hand-built
plan. Two deductions: the empty `params` request (P2-10), and the identity family's
default-representable invalid values that force "reject again at every seam" (P1-5).

---

## 4. Journeys

Written as a developer would, against §17.2 exactly. Members with no approved signature are marked
`// INVENTED` with the friction note — that annotation *is* finding P0-1 in practice: I could not
author any of the four journeys without inventing at least `Init` and `Then`.

### (a) Ephemeral parallel enrichment with a transient pool

```csharp
using OrcaCore.Core.Building;

static class Pools { public static readonly TransientPoolName Crm = new("crm-api"); }
static class Outcomes { public static readonly WorkflowOutcomeName Enriched = new("enriched"); }

var definition = Workflow.Ephemeral<CustomerState>(DefinitionIds.Enrich, new DefinitionVersion(1))
    .Init<CustomerInput>(CustomerState.From)                       // INVENTED — no signature in §17.2
    .Parallel<EnrichmentFragment>(
        branches: b => b
            .Branch<CrmLookupState>(
                new AuthoredBranchId("crm"),
                parent => CrmLookupState.From(parent.Value),        // FRICTION 1
                build: br => br
                    .WithTransientPool(Pools.Crm)                   // FRICTION 2
                    .Then<FetchCrmProfile>()                        // INVENTED
                    .Return(s => EnrichmentFragment.FromCrm(s.Value)))
            .Branch<GeoLookupState>(
                new AuthoredBranchId("geo"),
                parent => GeoLookupState.From(parent.Value),
                build: br => br
                    .Then<FetchGeo>()
                    .Return(s => EnrichmentFragment.FromGeo(s.Value))),
        merge: (parent, results) => parent.Value with               // FRICTION 3
        {
            Crm = results[0].Value,                                 // FRICTION 4
            Geo = results[1].Value,
        })
    .End(Outcomes.Enriched)
    .Build();
```

**Friction 1.** `Func<ReadOnlyParentSnapshot<TParentState>, TBranchState>` — the projector receives
a snapshot wrapper, not `TParentState`. `.Value` is my guess at the accessor; §17.2 approves the
type name `ReadOnlyParentSnapshot<TParentState>` (17:177) and never approves a member on it. Same
for `ReadOnlyBranchSnapshot<TBranchState>` in `Return`. Two types in every branch signature, zero
approved members. (Inside P0-1; worth calling out separately because these are *parameters of
approved signatures*.)

**Friction 2.** `WithTransientPool` sits on the branch builder before the step it guards. Nothing in
the signature says which step body the slot brackets — MG-061 (`09:174-191`) says hosts enforce
pools around step bodies, and `add-runtime-concurrency-limits`'s `spec.md:39-42` says "the granted
slot remains counted until the body returns/stops". With two `Then`s in a branch, does the pool
bracket both? `17:32` says a per-step throttle is "held only around one step body"; the transient
pool row (`17:33`) never says what it brackets. Real ambiguity, not a signature gap.

**Friction 3.** The merge returns "the complete replacement parent state" (`17:264-265`). With
`with` on a record that is fine; with a class it means the author hand-copies every field or mutates
a snapshot they were handed as read-only. The contract is right; a `ReadOnlyParentSnapshot` that
cannot be `with`-ed is the friction.

**Friction 4.** `results[0]` / `results[1]` — the merge receives `IReadOnlyList<BranchResult<TResult>>`
"in authored order" (`17:265-266`), and `BranchResult` carries the `AuthoredBranchId` I named, but
the ergonomic path is a positional index that silently re-binds if someone reorders two `Branch`
calls. `AuthoredBranchId` exists precisely so merge code "does not fall back to a raw string"
(`workflow-authoring/spec.md:149-150`) — but there is no lookup member, so the raw fallback is an
`int`. A `BranchResults<TResult>` collection with `this[AuthoredBranchId]` would close it. **New
observation, P3-class, folded into P2-13.**

### (b) Durable order flow: `WaitLong`, `AcquireResources`, reserved external job

```csharp
static class Pools { public static readonly ResourcePoolName RiskDb = new("risk-db"); }
static class Events { public static readonly EventName Approved = new("order.approved"); }

var definition = Workflow.Durable<OrderState>(DefinitionIds.Order, new DefinitionVersion(3))
    .Init<OrderInput>(OrderState.From)                              // INVENTED
    .Then<ValidateOrder>()                                          // INVENTED
    .WaitLong(Events.Approved, s => s.CorrelationId)
    .If(s => s.RequiresRiskCheck,                                   // INVENTED — arms unknown
        then: t => t
            .AcquireResources(ResourceLeaseRequirement.Require(Pools.RiskDb, 2))
            // .RunExternalJob(...)  RESERVED (task 5.0) — FRICTION 5
            .Then<RecordRiskOutcome>(),
        otherwise: e => e.Then<SkipRisk>())
    .End(Outcomes.Placed)
    .Build();
```

**Friction 5 — the journey cannot be completed.** The reserved block is the whole point of the risk
arm, and its absence is legitimate (§17.2:342). But stubbing it exposes P1-2: with the job in place,
the two `risk-db` units are held from `AcquireResources` until the *root fiber exits* — through
`RecordRiskOutcome` and `End`. JS-007 (`14:112-114`) requires release "on the job's terminal
outcome". The two are not the same instant, and nothing warns me. If instead I move the acquisition
after the job to shorten the hold, the job starts without its budget, violating JS-007's first
clause ("a job never starts without its budget"). The only shape that satisfies JS-007 in the
approved surface is one job per instance, where fiber exit ≈ job end — which is what open question
15 (`13:155-160`) resolved for DAG nodes, and which is therefore *load-bearing and unstated* for
anyone writing a non-DAG durable workflow.

**Friction 6.** `If` arms: `SFE-AUTH-LEASE-001`'s analysis depends on arms being separable
(`17:365-366`, "`If` arms are analyzed separately and merged"), so the shape of `If` is part of the
lease contract — and `If` has no approved signature. I invented `then:`/`otherwise:` sub-builders;
a `.If(pred).Then<X>().EndIf()` shape would compile the same author intent into a different
analysis problem. This is the clearest case for why P0-1 is P0: the lease diagnostics are specified
against a control-flow member whose shape is not.

### (c) Durable `WhenFirst` timeout-vs-acquisition race

The baseline's sanctioned idiom for an acquisition timeout (`17:334`, `13:136`):

```csharp
var definition = Workflow.Durable<PaymentState>(DefinitionIds.Pay, new DefinitionVersion(1))
    .Init<PaymentInput>(PaymentState.From)                          // INVENTED
    .WhenFirst<CaptureAttempt>(
        branches: b => b
            .Branch<CaptureState>(
                new AuthoredBranchId("capture"),
                p => CaptureState.From(p.Value),
                br => br
                    .AcquireResources(ResourceLeaseRequirement.Require(Pools.Gateway))
                    .Then<CapturePayment>()                          // INVENTED
                    .Return(s => CaptureAttempt.Captured(s.Value.Receipt)))
            .Branch<TimeoutState>(
                new AuthoredBranchId("timeout"),
                p => TimeoutState.From(p.Value),
                br => br
                    .Delay(TimeSpan.FromMinutes(15))                 // INVENTED
                    .Return(_ => CaptureAttempt.TimedOut())),
        merge: (parent, winner) => winner.BranchId == new AuthoredBranchId("capture")
            ? parent.Value with { Receipt = winner.Value.Receipt }
            : parent.Value with { Abandoned = true })                // FRICTION 7
    .End(s => s.Abandoned ? Outcomes.Abandoned : Outcomes.Captured)
    .Build();
```

**This journey works, and it is the strongest evidence for Decision 3's no-timeout-overload call.**
The parked acquisition is cancelled by loser cancellation (`17:23`: "all losers are cancelled";
`17:333-335`: parked until "that exact owner is canceled"), the queued request reserves zero units
(`17:437`), and no ticket can leak because the request was never granted. A timeout parameter would
have needed its own expiry semantics and would have collided with the review-deadline model. Correct
call. See §8.

**Friction 7.** Discriminating the winner means comparing `winner.BranchId` against a re-constructed
`AuthoredBranchId`, or switching on `winner.Ordinal`. Both are stringly/positionally typed inside a
merge that is otherwise fully typed — the same gap as Friction 4. A `WhenFirst` merge would read
better if `BranchResult<TResult>` were matched by the branch declaration itself, but at minimum
`AuthoredBranchId` constants (as in Journey (a)'s `Pools`) make it tolerable. Not a finding beyond
P2-13.

**Friction 8.** The race's two branches must agree on `TResult` (`CaptureAttempt`) — one common
serializable result type per scope (`17:174`). For a timeout branch that has nothing to say, the
author invents `CaptureAttempt.TimedOut()`. That is the correct tradeoff (typed merge over a
discriminated union of unrelated types) and I note it only because it is the friction a developer
will complain about first, and the documentation should meet it head-on.

### (d) Split-host external-job completion through the facade

```csharp
// ---- definition-owning host ----
var runtime = provider.GetRequiredService<DurableWorkflowRuntime>();
DurableDefinitionHandle<OrderState> orders = runtime.Definitions.Register(orderDefinition);

var started = await orders.StartOrGetAsync(
    new StartIdempotencyKey($"orders/{input.OrderNumber}"), input, ct);
// started.InstanceId; result is Created or the existing instance, or StartIdempotencyConflict

// ---- definition-less callback host (no Register call) ----
// Watcher gives us: string rawJobId (from a K8s Job annotation), a typed result, a report id.
ExternalJobId jobId = /* FRICTION 9 — cannot be constructed */;
var report = new EventId(Guid.Parse(message.Headers["report-id"]));   // FRICTION 10

var outcome = await runtime.ExternalJobs.CompleteAsync(               // RESERVED (task 5.0)
    jobId, report, new RiskResult(score: 0.82), ct);

// outcome is AppliedPendingContinuation on this host — correct and well-named.

// ---- either host ----
var instance = runtime.Management.Instance(started.InstanceId);
var snapshot = await instance.GetAsync(ct);
var state = await instance.GetStateAsync<OrderState>(ct);
var waits = await instance.GetActiveWaitsAsync(ct);                   // FRICTION 11
```

**Friction 9 — blocking.** See P1-4. `ExternalJobId`'s only constructor is private and the only
reconstruction seam is the JSON converter (`17:136-139`). This host has a string. There is no
supported way to turn it into the parameter the completion API requires. Task 3.9's two-host fixture
cannot be written without either the JSON-literal hack or reflection.

**Friction 10.** `new EventId(Guid...)` accepts `Guid.Empty` (`Ids/EventId.cs:11-17`) — the report
identity, the thing that makes redelivery idempotent, is the least-validated value in the call. See
P1-5, P2-8.

**Friction 11.** `GetActiveWaitsAsync` on an instance parked on an acquisition: per P2-16 I expect a
wait record whose `EventName` is null and whose useful content (`ResourcePoolName`, queue position)
is only available from pool administration, on the other management module, keyed by pool rather
than by instance. "Why is this order stuck" is the single most common operator question and it needs
two queries and a join.

**What works.** The registration→typed-handle→`StartOrGetAsync(key, input, ct)` chain is genuinely
good: no phantom `TState`, no implicit registration, the key is unmistakable, and
`AppliedPendingContinuation` tells the callback host exactly what it did and did not do. This is the
best-designed journey of the four and it validates Decisions 2, 4, and 5 together.

---

## 5. Lifecycle-model stress (§17.4, MG-062/064)

I attacked the no-author-TTL model on its own terms. It holds. Details, then the two soft spots.

**Deterministic release + review deadline.** The separation is clean and correctly load-bearing: the
review-at value is "a reconciliation deadline, not validity or an author TTL" (`17:421-422`), a due
review "is marked/audited while capacity stays held" (`17:422-423`), and marking "does not advance
waiters" (`09:268-269`). No path lets elapsed time free a unit. `17:428-431` closes the obvious
back door explicitly: "Any automatic elapsed-time reclaim of a logically active holder requires a
separately approved, provider-certified renewal protocol and end-to-end fencing at the protected
resource." That is the right rule and it is stated in the one place an implementer would look for
permission to cut the corner.

**Causal-proof recovery.** The four reconciliation outcomes (`09:271-282`) partition owner state
correctly: active/reconstructable → held; queued/pending-commit → decided only from causal owner
evidence; proven released/never-committed → released once with an audit; terminal → released only
when cleanup *and* protected-work stop are also proven. "Bare `Terminated` status is not that proof"
(`09:279`) is the sentence most engines get wrong. `LeaseLost` (`09:281-282`) covers the remaining
case — active owner, missing ticket — and forbids silent reacquisition, which is what prevents a
double-grant after a provider generation rolls.

**Fencing.** Every mutation compare-and-acts on "exact ticket ID, `LeaseObligationId`, and provider
ownership generation" (`09:284-286`, `17:431-432`). I could not construct a stale-cleanup-releases-a-successor
scenario against that.

**Capacity accounting.** State-defined (`17:433-437`), debt is `max(0, reserved - configured)`
recomputed after every release or resize, no new grant until debt is zero *and* the next request
fits (`17:437-439`). The refusal to promise `AvailableCapacity` restoration (`17:440-442`) is
honest and prevents a whole class of flaky assertions. Atomic all-or-nothing grants (`09:239-241`)
plus one-per-ancestry means no hold-and-wait, so the classic deadlock is structurally absent —
this is the model's best property and it is achieved by the ancestry rule, not by detection.

**Unstated state — soft spot 1.** `17:436-437` enumerates reserved states as "pending-commit, held,
expiry-marked, ambiguous, and forced-stop quarantine", and zero-reserving as "queued/released/cancelled".
MG-062 (`09:210-214`) enumerates the record's phases as "queued, pending-commit, held, marked,
ambiguous, quarantine, released, and cancelled" — eight. `management-and-querying/spec.md:101-104`
enumerates reserved-by-state as "(pending-commit, held, expiry-marked, ambiguous, or forced-stop
quarantine)" — five. These agree, and that is good. But `LeaseLost` (`09:281-282`,
`durable-runtime/spec.md:169-171`) has no state in the enumeration: when an active owner is told its
ticket is gone, what does the obligation become? The owner "cannot continue or silently reacquire" —
so the fiber fails — and failure releases (`durable-runtime/spec.md:121-123`, "scope failure"). But
the release path is defined to release *exact tickets*, and the premise of `LeaseLost` is that the
ticket is absent. The transition is therefore either a no-op release (fine, but unstated) or an
attempt to compare-and-act on a ticket that is not there (which fails, unstated). **Remediation:**
state in MG-064 that a `LeaseLost` obligation transitions to `cancelled`/`released` reserving zero,
with the audit fact, since the provider has already lost the reservation.

**Operator dead end — soft spot 2 (the tradeoff is accepted; the *exit* is what I am reviewing).**
`09:299-303`:

> "An operator force-release SHALL make units grantable only after the runtime fences the exact
> owner from resuming and protected work is causally confirmed stopped, or an end-to-end fence at
> the protected resource rejects the old ownership generation. Otherwise the obligation remains
> capacity-reserving quarantine and the attempted action is audited. An unsafe override that merely
> deletes provider metadata is outside the baseline contract."

`management-and-querying/spec.md:111-113` restates it: force release without stop proof "is audited
but remains capacity-reserving quarantine and does not grant a waiter; deleting provider metadata
alone is not a successful baseline operation."

I accept the tradeoff (the prompt is explicit, and I agree with it: a false release double-grants a
protected resource, which is worse than a stall). What I am reviewing is whether "the surrounding
contract makes it safe and observable" — safe, yes; **observable and exitable, not quite.** Consider
the realistic terminal case: an EKS cluster is deleted with jobs running; there is no watcher left
to report; the protected database has no fencing token, because "end-to-end fence at the protected
resource" is a property of the *resource*, and a plain PostgreSQL connection budget has none. The
owner instance is `Terminated`. Causal evidence will never arrive. Per the contract, those units are
quarantined **forever**, and the only sanctioned action ("request exact-obligation force release",
`09:293-294`) is defined to not work in exactly this case. The pool is permanently smaller. The
operator's real recovery is to raise configured capacity to compensate for dead units
(`17:438-439` allows it, and debt accounting makes it visible) — which works, but it is a workaround
the documents never name, and it silently redefines the pool's capacity as "budget + accumulated
tombstones".

The gap is small and the fix is smaller: the contract needs a *named* terminal action for
"the protected resource is gone, not just unreachable", distinct from the unsafe metadata delete it
correctly forbids.

**Remediation.** Add to MG-064: an audited operator **capacity write-off** (or
`ForceReleaseWithAttestation`) that requires an explicit operator attestation naming the destroyed
resource, records it as a first-class fact distinguishable from a proven release in the audit trail
and from a recovered-release in statistics, and makes the units grantable. This is not the forbidden
override: the forbidden one asserts nothing and deletes metadata; this one records a human's
statement of fact about the world, which is the only evidence that can exist when the world has
lost the ability to report. Without it the baseline's answer to "the cluster is gone" is "resize the
pool", which is the same effect with none of the audit.

**Livelocks, leaks.** I looked for and did not find: no renewal means no renewal-storm livelock; the
serialized pool lane plus one request identity (`09:305-311`) makes grant-vs-cancel choose one
outcome; debt cannot admit a grant, so a downward resize cannot cascade; FIFO default with no
priority (`13:164-167`, open) means no starvation by construction. The one capacity leak in the
model is authored, not operational: P1-2.

---

## 6. Vocabulary and projection audit

**Family completeness.** `EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `ResourcePoolName`,
`TransientPoolName`, `StartIdempotencyKey`, `ExternalJobKey`, `ExternalJobId`, `LeaseObligationId`
(protocol-side, `17:337`) — the set covers every string that matches across call sites or hosts,
with two exceptions I flag: `EventId reportId` (P2-8) and the identity family (P1-5). Specification
is consistent across all eight: validating construction, ordinal case-sensitive equality, scalar
serialization, no implicit conversion, no raw-string overload (`17:130-139`), restated identically
in `workflow-contracts/spec.md:42-49`, `developer-facing-surface/spec.md:66-71`, and
`workflow-authoring/spec.md:144-154`. Provider collation certification is required
(`workflow-authoring/spec.md:152-154`; task 8.14 including the 512/513 boundary). I found no
requirement that mentions one of these concepts and uses a raw string instead.

**Usage everywhere its concept appears — one gap.** `EventName` is used in `Wait`, `WaitLong`,
`StepResult.WaitForEvent`, event routing, and wait projections. `WorkflowOutcomeName` in `End` ×3,
completion snapshots, statistics filters, query predicates (`management-and-querying/spec.md:89-96`).
`ResourcePoolName` in `Require`, pool administration, reconciliation state. `AuthoredBranchId` in
`Branch` and `BranchResult` — but **not** in `ForEachItemOutcome`, which orders by item index
(`17:279-281`) with no approved identity type for an item. Minor; inside P2-13.

**Projection cleanliness — verified.** `17:481-484` and `management-and-querying/spec.md:53-58` both
exclude `FiberId`, `ScopeId`, `WaitSequence` from application wait projections and route them to an
advanced observation contract; `17:476-479` classifies them as runtime-protocol concepts alongside
format-2 envelopes, raw park reasons, and obligation ownership; `developer-facing-surface/spec.md:81-90`
guards it; `design.md:110-118`'s tier table places them consistently; task 3.5 makes it a failing
guard. `17:474-475` and `workflow-authoring/spec.md:64-69` keep the compiled plan out of both
definition types and behind an internal accessor. `FiberId` exists today in the application-tier
`src/OrcaCore.Abstractions/Ids/FiberId.cs` — which is exactly what task 6.5 moves, so this is a
planned violation, not a design leak. `AuthoredLocation` is correctly shared between diagnostics and
projections (`17:486-488`) and correctly disclaimed as an execution identity ("never suffices as a
lease holder/occurrence ID"). `GetStateAsync<TState>` is correctly scoped to root state and
explicitly refuses branch/item payloads (`17:493-496`, `management-and-querying/spec.md:38-41`) —
that scenario ("Structured branches contain private state") is the kind of thing that only gets
written down after someone has been burned, and it is right.

The one projection I would not ship as specified is the wait record itself (P2-16).

---

## 7. Misuse classification

| # | Attempted wrong program | Where the surface stops it | Evidence |
|---|---|---|---|
| M-1 | Durable `ForEach` | **Compile-impossible** (absent from `DurableWorkflowBuilder`); manual node → compiler `SFE-CAP-*` | `17:27`, `17:281-282` |
| M-2 | Ephemeral `WaitLong` | **Compile-impossible** | `17:28`, `05:113-115` |
| M-3 | Ephemeral `ContinueAsNew` | **Compile-impossible** | `17:29` |
| M-4 | Ephemeral `AcquireResources` | **Compile-impossible** | `17:34`, `workflow-authoring/spec.md:119-120` |
| M-5 | `WithTransientPool` on a durable builder | **Compile-impossible** (root and branch) | `17:253-254`, `17:256-259` |
| M-6 | `WithTransientPool` inside a durable `Parallel` branch | **Compile-impossible** (mode-preserved branch types) | `17:256-259`, `workflow-authoring/spec.md:57-62` |
| M-7 | `Init`/`End`/`ContinueAsNew` inside a branch | **Compile-impossible** | `17:261-263`, `17:272-274` |
| M-8 | External job inside a branch | **Compile-impossible** — *but see P1-3: this is a capability gap, not only a guard* | `17:272-274` |
| M-9 | `TransientPoolName` → `ResourceLeaseRequirement.Require` | **Compile-impossible** | `17:83-87`, `17:305-308` |
| M-10 | `EventName` → `End` | **Compile-impossible** | `17:233-239`, `workflow-authoring/spec.md:89-92` |
| M-11 | Raw string → any typed seam | **Compile-impossible** (no implicit conversion, no raw overload) | `17:133-136` |
| M-12 | `ExternalJobKey` as completion target | **Compile-impossible** | `17:468-471` |
| M-13 | **Hold a lease across work it does not protect** (acquire → job → 3-day `WaitLong` → end) | **SILENTLY ACCEPTED — finding P1-2** | `17:34`, `14:112-114`, no release member |
| M-14 | Same-fiber reacquisition | **Build** — `SFE-AUTH-LEASE-001` | `17:335-336`, `17:390` |
| M-15 | Acquisition in a repeatable `While` body | **Build** — `SFE-AUTH-LEASE-002` (+ related loop-header location) | `17:366`, `17:391` |
| M-16 | Descendant acquires under a pending/held ancestor | **Build** — `SFE-AUTH-LEASE-001`; hand-built plan → **runtime** `SFE-RUN-002`, fails before pool mutation, no park/retry/poison | `17:365-367`, `17:381-383`, `durable-runtime/spec.md:149-151` |
| M-17 | `ContinueAsNew` with a held lease | **Build** — `SFE-AUTH-LEASE-003`; stale plan → **runtime** `SFE-RUN-001`, no rollover fact, no lease clearing | `17:367-369`, `workflow-authoring/spec.md:111-113` |
| M-18 | Duplicate pool in one request | **Fluent call** (static) / selector validation (dynamic) | `17:322-327` |
| M-19 | `AcquireResources()` with no requirements | **Fluent call** — *could be compile-impossible; finding P2-10* | `17:311-312`, `17:322-323` |
| M-20 | `Require(pool, 0)` / null pool | **Fluent call** | `17:307-308`, `17:322` |
| M-21 | Empty/whitespace/padded name value | **Constructor** (author-constructible names) | `17:130-133` |
| M-22 | `default(DefinitionId)` / `Guid.Empty` id / version 0 | **Nearest seam at runtime** — *should be construction-time; finding P1-5* | `workflow-contracts/spec.md:42-53`, `Ids/DefinitionId.cs:9-17` |
| M-23 | Selection-wide terminate/purge with `default` confirmation | **Runtime, before any mutation** (`None = 0`) | `design.md:329-337`, `management-and-querying/spec.md:78-84` |
| M-24 | Start an unregistered definition | **Registration/runtime** — typed `DefinitionNotRegistered`, no registration mutation | `06:174-179`, `durable-runtime/spec.md:60-62` |
| M-25 | Register an ephemeral definition with the durable runtime | **Compile-impossible** | `durable-runtime/spec.md:56-58`, `17:284-287` |
| M-26 | Reuse a `StartIdempotencyKey` across definitions/versions | **Runtime** — `StartIdempotencyConflict`, no instance returned through the incompatible handle | `06:168-172`, `durable-runtime/spec.md:52-54` |
| M-27 | Unconfigured `ResourcePoolName` in a request | **Registration/startup** | `17:323-324` |
| M-28 | Continue a workflow after `LeaseLost` | **Runtime** — typed outcome, no silent reacquisition | `09:281-282`, `durable-runtime/spec.md:169-171` |

22 distinct wrong programs beyond the trivial-argument cases; 14 compile-impossible, 5 fluent/build,
1 registration, 5 runtime-typed (M-16/M-17 counted at both layers), **1 silently accepted (M-13)**.

---

## 8. Explicit non-findings

Decisions I examined, doubted, and now endorse.

**No acquisition-timeout overload (`17:334`, `13:136`).** I expected this to be the baseline's
mistake — every queue API grows a timeout — and journey (c) changed my mind. The `WhenFirst` race
expresses the same intent with *better* semantics: the timeout is authored where the author can see
what else races it, the parked request reserves zero units (`17:437`), loser cancellation is already
the specified release trigger (`17:333-335`), and no new expiry concept collides with the
review-deadline model. A timeout parameter would have required a second time semantic inside the
one contract that just spent 40 lines establishing that time never decides ownership. Correct, and
the reason it is correct is not obvious enough to leave undocumented — phase 9 should show journey
(c) as *the* timeout idiom.

**No holder renewal in the baseline (`17:427-431`, `09:286-289`).** I doubted this hardest: without
renewal, a crashed holder's units are held until reconciliation proves something, and the proof may
never come (see §5 soft spot 2). But renewal only *appears* to solve it — a renewal lapse is elapsed
time, and reclaiming on elapsed time double-grants a resource whose worker is alive and partitioned
unless the resource itself fences. The documents say exactly this ("renewal alone is insufficient",
`09:287-289`) and refuse to ship the appearance of safety. That is the correct call and it is rarer
than it should be. My §5 remediation adds an operator attestation, not renewal.

**Mode-first split with duplicated builder families (Decision 2).** The duplication is real —
`Parallel`, `WhenFirst`, `Branch`, `Return`, `Wait` are each declared twice in §17.2, and the branch
families double it again. I tried to design it away (one builder + a mode phantom type; one builder +
`ForMode()`) and both are worse: a phantom type parameter puts `Workflow<TState, Durable>` in every
signature and error message, and `ForMode()` leaves wrong methods discoverable through authoring
(`design.md:200-202`). The cost is paid once by the library in declarations that share one internal
implementation (`17:256-257`); the benefit is paid back on every developer's first IntelliSense
press. Carrying its weight. See §9.1 for the caveat.

**Hiding the compiled plan behind an internal accessor (`17:145-148`, Decision 1).** Doubted because
plan inspection is a genuinely useful tooling seam. Endorsed because `design.md:119-123` answers it
correctly: a future inspection tool gets "a separate read-only inspection model; it does not expose
executable delegates or make the runtime plan an application contract". The right shape, deferred
for the right reason.

**`Build()` + `TryBuild()` (Decision 9).** Two methods where one would do — but the pair splits two
real audiences (the golden path that should throw with everything wrong at once; tooling that must
not throw) without splitting semantics, since both "invoke the same internal definition compiler"
(`17:141`). Removing `BuildValidated` (task 4.10) is a strict improvement. Endorse.

**`DestructiveOperationConfirmation { None = 0, Confirmed = 1 }` (Decision 6).** A two-value enum is
a bool wearing a hat — but it is a bool that cannot be produced by `default`, cannot be swapped with
another bool parameter, and names itself at the call site. Deleting the always-throwing overloads
(`design.md:327`) and making single-instance terminate confirmation-free (selection is already the
guard) are both right. Endorse without reservation.

**`AppliedAndProgressed` vs `AppliedPendingContinuation` (DU-055).** I expected this to be an
implementation detail leaking into the application result. It is the opposite: it is the *only*
honest way to describe a split-host commit, it names the difference in the caller's vocabulary, and
it stops the callback host from believing it drove the workflow. Journey (d) confirms it reads
correctly at the call site.

**Runtime ancestry defense duplicating the compiler (`17:378-383`).** Looks like belt-and-braces;
is not. The compiler cannot see hand-built or stale plans, and the check runs "before queue/pool
mutation" over "the union of persistent pending lease records and held active tickets" — i.e., it is
the layer that actually protects the provider. `SFE-RUN-002` failing the workflow rather than
parking/retrying/poisoning is the right disposition for a violation that means the plan lied.

**Ephemeral saga shipping while `Saga.Durable` waits (`17:283-287`, `13:145-152`).** Asymmetric and
correct: ephemeral saga is implemented and tested, durable saga is not, and shipping the factory
early would put a name in IntelliSense that cannot keep its promise. The matrix says so in the
capability row rather than in a footnote. Endorse.

---

## 9. Focused questions

### 9.1 Is the mode-first builder split carrying its weight versus the duplication it creates?

**Yes — at the root. At the branch level it is carrying weight it should not have to.**

The root split is unambiguously worth it (§8). The branch split is where I want to be careful: it
doubles `EphemeralBranchScopeBuilder`/`DurableBranchScopeBuilder` ×
`EphemeralBranchBuilder`/`DurableBranchBuilder` to guard a delta of exactly **two members** —
ephemeral branches add `WithTransientPool`, durable branches add `AcquireResources` (`17:270-274`).
Four public types plus four signature duplications for two methods is a poor ratio *on its own
terms*.

It is still the right call, for a reason the documents state well: "A method cannot reappear inside
`Parallel` merely because the implementation reused a common branch class" (`design.md:164-166`) —
this is not hypothetical, it is the exact bug being fixed (`design.md:29`: nested
`BranchBuilder.WithPoolKey` today makes an ephemeral-only policy discoverable inside durable
`Parallel`). And the ratio improves the moment P1-3 is resolved: if durable branches gain `WaitLong`
(and, later, external jobs), the delta grows to where the split obviously pays. The ratio is bad
today partly *because* the durable branch surface is too thin, which is P1-3, not an argument against
the split.

The duplication that is *not* carrying weight is in §17.2 itself: `Parallel`, `WhenFirst`, `Branch`,
`Return`, and `Wait` are written out twice, verbatim modulo one token, and every future edit must be
made twice in a document whose entire authority rests on being exactly right. A single parameterized
block ("for `Mode` in {Ephemeral, Durable}") would remove a whole class of amendment drift.
Editorial, but this document is the contract.

### 9.2 Does the lease contract compose safely with `WaitLong`, `WhenFirst` cancellation, `ContinueAsNew`, and forced termination?

**Three of four: yes, and well. `WaitLong`: safely but not usefully.**

- **`WhenFirst` cancellation — yes, and it is the best composition in the design.** Journey (c): the
  loser's parked request is cancelled by owner cancellation, reserves zero, and cannot ghost-grant
  because "grant and exact-owner cancellation/failure/terminal cleanup race" is serialized to one
  outcome (`durable-runtime/spec.md:185-187`, MG-065). A granted loser releases exact tickets before
  parent resume (`durable-runtime/spec.md:141-143`). Clean.
- **`ContinueAsNew` — yes, conservatively, at a cost worth naming.** `SFE-AUTH-LEASE-003` on "every
  path where a pending or held lease may remain in the generation" (`17:367-369`) is sound. But
  compose it with P1-2: a root-fiber `AcquireResources` releases only at fiber exit, and
  `ContinueAsNew` *is* the root fiber not exiting. Therefore **any root-level acquisition makes
  `ContinueAsNew` unreachable for that definition, permanently, on every path** — not because of a
  timing conflict but because the two features' lifetime models are mutually exclusive by
  construction. A long-running instance that needs both (the natural shape: a per-tenant supervisor
  that periodically acquires a budgeted resource and rolls over history) must push the acquisition
  into a child fiber. That is *sound* — nothing leaks — but it is a composition failure that the
  scoped form in P1-2 removes entirely: a lease scope that has exited is quiescent, so
  `ContinueAsNew` after it is trivially legal, and `SFE-AUTH-LEASE-003` shrinks to "not inside a
  resource scope".
- **Forced termination — yes.** Fence the owner, quarantine unproven work, never treat `Terminated`
  as proof (`17:423-426`, `09:218-219`). Correct, with the one exit gap in §5.
- **`WaitLong` — safe, but the composition is a capacity trap.** Nothing breaks: the lease is held,
  accounted, reviewable, and reconcilable across the cold wait — which is exactly MG-062's canonical
  case ("an external job holding database connections while its owning instance is cold-waiting",
  `09:194-196`). The trap is that the surface cannot distinguish "cold-waiting *for the protected
  work*" (correct, intended) from "cold-waiting for something else while still holding"
  (M-13/P1-2, a leak), and both are authored identically. `WaitLong` is where the missing release
  point hurts most, because it is where holds are longest.

### 9.3 Is the strong-type family the right size?

**Nearly. One missing, one over-claimed, one half-built.**

*Missing:* `EventId reportId` (P2-8) — the external-job family's third role, and the only one
without its own type.

*Over-claimed:* `WaitId` (P2-9) — public "for targeting" with nothing to target. Either a member is
missing or the word is.

*Half-built:* the identity family (P1-5) — `DefinitionId`/`InstanceId`/`EventId`/`DefinitionVersion`
are the same *kind* of value as the eight names and get none of their guarantees, which is what
forces "reject default again at every seam" into eight separate requirements.

*Not ceremony, despite appearances:* I pressure-tested three. `AuthoredBranchId` looked like ceremony
until journey (a) showed the merge indexing positionally without it — the type is what makes
`BranchResult` self-describing, and it should be doing *more* work, not less (P2-13/Friction 4).
`TransientPoolName` vs `ResourcePoolName` looked like two names for a string until I tried M-9: the
whole point is that host-local-and-resettable and persisted-and-fenced must never be typo-compatible,
and the compiler is the only enforcement that scales. `StartIdempotencyKey` earns its 512-character
limit and its distinctness from `InstanceId` (`06:154-157`) — those are two different lifetimes with
two different owners. All three are contracts, not decoration.

`LeaseObligationId` is correctly protocol-side, never caller-supplied (`17:337`), and correctly
absent from the application tier — the right call, since its only application use would be to let a
caller invent one.

### 9.4 Are `Wait` / `WaitLong` / `AcquireResources` the right names?

**`Wait` / `WaitLong`: the split is right, `WaitLong` is the wrong word, and the decision to keep
both is resolved so I will not re-litigate it — but the names should carry the distinction they
exist for.** Open question 1 (`13:95-100`) resolved permanently that they stay separate concepts,
and I agree with the *separation*: durable residency is a real, author-visible, cost-bearing
property. But `Long` names a *duration*, and duration is not what differs — EV-040/EV-041 (`05:108-118`)
differ on **residency** ("the instance may remain hot" vs "immediately evictable"). Nothing bounds a
`Wait`'s duration; a 30-day `Wait` compiles and pins memory (§9.5, P1-3). `WaitCold` — or
`Wait`/`WaitEvictable` — names the actual contract, matches the `Cold`/`Resident` vocabulary the
wait records already use (`05:109`, `05:114`), and stops teaching authors to choose by guessing how
long a business process takes. This is a rename inside a resolved decision, not a reopening of it.

**`AcquireResources`: defensible, but the vocabulary around it is not.** See P3-18. The verb is
right (authors request resources; the lease is how the runtime grants them) — but then every
diagnostic the author reads says LEASE, the only failure they can catch is `LeaseLost`, and the
management page they open says lease administration. Keep the name; make §17.2 record *this*
rationale rather than "matching the portable member it replaces", and consider whether the
author-facing diagnostics should say `SFE-AUTH-RESOURCE-*`.

The one name I would change outright is the missing one: with a scoped form (P1-2), the pair becomes
`AcquireResources(...)` (fiber-lifetime) and `UsingResources(..., body)` (scope-lifetime), and the
author's choice of hold duration becomes visible in the shape of their code instead of implicit in
where their fiber happens to end.

### 9.5 Can a developer discover why something is absent without reading the spec repository?

**No, and this is the gap between a good surface and a good product.**

A developer types `.` on a `DurableWorkflowBuilder<T>`, does not see `ForEach`, and has three
hypotheses: it does not exist, it is named something else, or it is somewhere else. Nothing in the
IDE distinguishes them. If they force it — hand-build the node — they get `SFE-CAP-*` with a stable
code (`17:398-399`), which is good but arrives only for the developer who was already determined to
be wrong.

What exists today: the capability matrix (a spec-repository document), XML docs (task 9.4/9.6
mention publishing the matrix and correcting stale claims), and `SFE-CAP-*` for the forced path.
What does not exist: any mechanism reachable from the editor. The absences that most need this are
exactly the ones a competent developer will assume are bugs — durable `ForEach` (present in the
other mode!), branch `WaitLong` (present at the root!), branch external jobs (present at the root!),
durable `WithTransientPool` (present in the other mode!), and `Saga.Durable` (present as a *name* in
§17.2 and not shipped, `17:120-125` vs `17:286-287` — a developer who reads the matrix will look for
a factory that is not there).

Cheap, high-leverage remediation, none of which needs a design change:
1. Ship `[Obsolete(error: true)]`-style **tombstone members** on the wrong builder for the highest-traffic
   absences, whose message is the reason and the alternative: `.ForEach(...)` on
   `DurableWorkflowBuilder` → *"In-instance ForEach is ephemeral-only: durable fan-out uses child
   instances so each item has its own identity, retry, and lineage (matrix §17.1)."* This is a
   compile error with a *teaching* message, it is discoverable by typing, and it costs one method
   per absence. It preserves every property the mode-first split exists for — the member cannot be
   called — while replacing silence with an explanation. This is the single highest
   effort-to-value item in the review after P1-2.
2. Put the reason in XML docs on the *mode factory* (`Workflow.Durable` → "durable mode adds …,
   omits …; see §17.1"), which IntelliSense shows at the moment of mode selection — the exact moment
   the developer is making the choice that determines everything downstream.
3. Make task 9.4's published matrix the target of every `SFE-CAP-*` message.

### 9.6 What single change would most improve the surface?

**Give `AcquireResources` a scoped form and make it the documented idiom (P1-2).**

It is the only change that improves all four dimensions at once:

- *Misuse resistance:* closes the only silently-accepted misuse in the pass (M-13), and the leak it
  closes is a shared, cross-instance, cross-host resource — the most expensive thing in the system
  to leak.
- *Comprehensiveness:* makes JS-007's "release on the job's terminal outcome" expressible without
  the reserved job-bracket and without forcing child-instance decomposition; makes lease +
  `ContinueAsNew` composable (§9.2).
- *Consistency:* aligns the lease's lifetime model with the *scope-entry occurrence* the persistence
  contract already records (`09:212-213`) and with how every other resource in .NET is held.
- *Developer orientation:* the hold duration becomes visible in the shape of the code. Today an
  author cannot tell, by reading, how long they hold — they must trace where their fiber ends.
- *Simplification:* `SFE-AUTH-LEASE-002` (repeatable loop) disappears, `SFE-AUTH-LEASE-003`
  (blocks-`ContinueAsNew`) shrinks to a lexical check, and the ancestry rule becomes nesting the
  compiler can see directly instead of path-sensitive dataflow — one *fewer* analysis, not one more.

If only an editorial change were possible, it would be P0-1: approve or reserve the eight missing
signatures before Phase 4, because that one costs a page and prevents the cycle that produced the
superseded `AcquireLease` shape this baseline just finished amending away.

---

## 10. Summary of remediations by phase gate

| Finding | Gate | Remediation |
|---|---|---|
| P0-1 | Before Phase 4 | Approve or reserve `Init`, `Then`, `If`, `While`, `Delay`, `ForEach`, `ContinueAsNew`, child workflows, `ReadOnly*Snapshot` members in §17.2 |
| P1-5 | Before Phase 4 (task 4.12) | Validating identity types, or a recorded exception |
| P2-7, P2-13, P2-10, P3-17 | Before Phase 4 | Factory parameter symmetry; `ForEachItem*` shape; non-empty `params`; record→class |
| P1-2 | Task 5.0 | Scoped `AcquireResources`; MG-064 `LeaseLost` state; §5 operator attestation |
| P1-3 | Task 5.0 | Branch `WaitLong`, or a normative absence note |
| P1-4, P2-8 | Task 5.0 | `ExternalJobId.Parse`; `ExternalJobReportId` or a recorded exception |
| P2-6, P2-15 | Task 5.0 | §17.3 wording; MG-062 fiber-level parking |
| P2-11, P2-12, P2-16, P2-9 | Before Phase 7 | Unowned-version diagnostic; `TargetPaused` disposition; wait-projection family; `WaitId` targeting |
| P2-14 | Before Phase 7 | Drop ephemeral `StartOrGet` or record the exception |
| §9.5 | Phase 9 (design now) | Tombstone members + factory XML docs + matrix links from `SFE-CAP-*` |
