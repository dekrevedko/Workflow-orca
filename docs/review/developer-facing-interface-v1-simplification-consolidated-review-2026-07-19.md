# Consolidated final review: OrcaCore v1 planning contract and Phase 0 guard readiness

**Date:** 2026-07-19

**Purpose:** reconcile the four independent reviews produced from
[the bounded reviewer prompt](developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md),
apply the owner's decisions, and issue one current verdict against the remediated live artifacts.

**Nature:** planning-contract review only. This document does not claim that product source or
Phase 0 guards implement the approved contract.

## 1. Authority and reviewed state

The current authority, in order, is:

1. [document 17](../specs/17-selected-mode-capability-matrix.md), including its exact package,
   namespace, diagnostics, lifecycle, hosting, and deferred-surface catalogs;
2. [the exact C# authoring companion](../specs/17-public-authoring-contract.cs);
3. the affected canonical requirements under `docs/specs/`;
4. the live
   [`reshape-developer-facing-interfaces` change](../../openspec/changes/reshape-developer-facing-interfaces/)
   and coordinated
   [`add-runtime-concurrency-limits` change](../../openspec/changes/add-runtime-concurrency-limits/).

The dated amendments, Phase 0 status reports, reviewer prompt, and reviews A-D are immutable
historical evidence. They were not rewritten. Their recommendations do not override the live
authority after this reconciliation.

The four independent inputs reached these snapshot verdicts:

| Review | Planning | Guard retarget | Consolidated disposition |
|---|---|---|---|
| [A / unsuffixed](developer-facing-interface-v1-simplification-review-2026-07-19.md) | APPROVE WITH CHANGES | NOT READY | Its three P1 blockers and P2 clarifications are resolved or explicitly deferred. |
| [B](developer-facing-interface-v1-simplification-review-2026-07-19-b.md) | APPROVE WITH CHANGES | READY | Its five P2 and two P3 clarifications were incorporated; no blocker remained. |
| [C](developer-facing-interface-v1-simplification-review-2026-07-19-c.md) | APPROVE WITH CHANGES | NOT READY | Its five P1 and four P2 inconsistencies are resolved in live authority. |
| [D](developer-facing-interface-v1-simplification-review-2026-07-19-d.md) | REJECT | NOT READY | All eleven P1 and eight P2 findings were remediated; its P3 visualization claim was removed. |

## 2. Final verdicts

| Gate | Final verdict | Meaning |
|---|---|---|
| **Planning contract** | **APPROVE** | No current P0, P1, or P2 contradiction requires another design decision before guards. |
| **Guard-retarget readiness** | **READY** | All 15 mandatory section-3 tasks—3.1-3.10, 3.11a-3.11d, and 3.12—are authorable without inventing a public signature, lifecycle transition, package edge, or test seam. |

`READY` authorizes a **guard-only** packet. It does not approve Phase 0 exit, mark any guard green,
or authorize product implementation. Task 3.12 remains the run/evidence/independent-re-review gate,
and task 4.0 remains blocked until that gate passes.

## 3. Owner decisions and developer-facing interpretation

### 3.1 `Parallel` and `ForEach` placement

The approved rule is:

- fixed `Parallel` is available at the root **and** in supported nested, branch, item, and leased
  bodies;
- `ForEach` itself is **root-only** in both modes;
- a root `ForEach` item body may contain a fixed nested `Parallel`, but no nested, branch, item, or
  leased builder exposes another `ForEach`.

This is explicit in document 17's capability rows (lines 29-32), availability table
(lines 568-571), and workflow-authoring delta (lines 137 and 166-174).

Review D P1-1 did not argue that nested `Parallel` should be banned. It found that one
`Parallel` join inside a lease widened back to an unrestricted builder. Both joins now return
`DurableLeaseNestedBuilder<TInput,TState>`
([companion lines 1044-1056](../specs/17-public-authoring-contract.cs)), so nested `Parallel`
remains legal while descendant `AcquireResources` remains statically unavailable. The guard task
requires a negative compile fixture for the complete join-then-reacquire chain.

### 3.2 Lease ownership during retry

The lease remains held while retrying. A retryable timeout, ambiguous response, process loss, or
recovered in-flight operation moves or retains the same obligation in `AmbiguousHeld` with the
same `StepOperationId`, protection token, tickets, and reserved capacity. A second in-process
leased attempt cannot overlap a still-running first body; host-loss recovery may retry the same
operation. Quarantine occurs only when ambiguous scope exit, retry exhaustion, cancellation,
workflow deadline, termination, or abandonment wins before safe progress
(document 17 lines 960-976; reshape task 3.11b).

### 3.3 Reactive output waiting

Periodic polling is not the ordinary API. The exact resultful handle and start-result convenience
both expose notification-driven `WaitForOutputAsync`, using subscribe-then-authoritative-recheck
to avoid missed wakeups. Cancelling the token cancels only the local wait.

The consumer path is:

    var registration = registry.Register(definition);
    var definitionHandle = registration.GetHandleOrThrow();
    var start = await definitionHandle.StartOrGetAsync(input, key, token);
    var output = await start.WaitForOutputAsync(token);

`GetOutputAsync` remains a nonblocking snapshot query for callers that intentionally want one;
it is not the prescribed wait loop. See document 17 lines 2048-2067 and 2214-2227, plus the
management delta lines 32-54 and quality delta lines 227-230.

### 3.4 Cast-free closed-result ergonomics

Registration/start results remain closed unions because conflict callers need the existing and
attempted facts. The ordinary success path uses typed `GetHandleOrThrow()` helpers, so consumers
do not cast:

    var dagDefinition = dagRegistry.Register(plan).GetHandleOrThrow();
    var start = await dagDefinition.StartOrGetAsync(
        new PipelineInput("source", "destination", "audit-42"),
        StartIdempotencyKey.Create("pipeline/audit-42"),
        token);
    var run = start.GetHandleOrThrow();
    var terminal = await run.WaitForTerminalAsync(token);

The exact helpers are declared at document 17 lines 1202-1220 and the canonical example is at
lines 1334-1337. Pattern matching remains available when the caller wants to inspect a conflict
instead of throwing the typed conflict exception.

## 4. Finding disposition

The table groups overlapping reports while retaining every substantive A-D finding.

| Source finding(s) | Final disposition | Current evidence |
|---|---|---|
| A P1-1; D P2-2 — `ContinueAsNew` shape and finite rollover | **Resolved / scope explicit.** It returns a terminal durable completion builder. V1's form is unconditional, perpetual, root-only, and quiescent; conditional finite rollover is explicitly deferred. | Matrix lines 40, 494, 610-616, 1950; companion lines 282-284; task 3.4. |
| A P1-2 and A/B disagreement — generic join notation versus concrete companion types | **Resolved.** Matrix sketches are compact semantic indexes, never extra public types; the concrete companion declarations are exact and normative. | Matrix authoring-notation rule and package/signature guards; workflow-authoring delta's exact-companion rule; task 3.1/3.4. |
| C P1-1 — lease placement contradiction | **Resolved.** Leasing is allowed at durable root, root-nested `If`/`While`, and independent branch/item bodies when no live ancestor lease exists. | Matrix line 43 and lines 941-957; workflow-authoring delta lines 219-238. |
| D P1-1 — leased nested `Parallel` re-enabled acquisition | **Resolved.** Both joins retain `DurableLeaseNestedBuilder`; a chained descendant acquisition is compile-absent and runtime ancestry remains a defense. | Companion lines 1044-1056; matrix lines 956-959; tasks 3.4 and 3.11a. |
| D P1-2 — one-parameter async lambda could bind `async void` | **Resolved.** All ephemeral lambda forms are `Func<...,ValueTask>`; no public `Action<StepContext<...>>` overload exists. | Matrix lines 23 and 464-467; companion lines 157-160, 302-305, 398-401, 451-454; task 3.4. |
| B F4; D P1-3; C P2-4 — DAG `MapInput` and failure timing | **Resolved.** Missing/duplicate `MapInput` is a build diagnostic. Opaque `OutputOf` access is validated during mapping and fails with `DAG_INPUT_MAPPING_INVALID` before input commit or child start. Rejection after `WhenAllOutcomes` routes through a named failing step; no fluent `Fail` member is implied. | Matrix lines 1278-1284, 1426, 1457; quality delta lines 130 and 208-212; task 3.10. |
| D P1-4 — retry versus immediate quarantine | **Resolved per owner decision.** Retryable ambiguity remains `AmbiguousHeld` and capacity-held; quarantine is an exit/terminal transfer, not every timeout. | Matrix lines 960-976; quality delta lines 158 and 173-187; task 3.11b. |
| A P1-3; D P1-5 — blind quarantined capacity | **Resolved.** Advanced diagnostics enumerate and retrieve immutable obligation/ticket/owner projections by protection token, separate from ordinary handles and from trusted recovery. | Matrix lines 882-927 and 997-1013; management delta lines 109-210; task 3.11c. |
| D P1-8 — resize operation conflict unrepresentable | **Resolved.** `DurableResourcePoolResizeResult` is a closed `Applied`/`Conflict` result with recorded and attempted facts and idempotent replay. | Matrix lease-management contract; management delta lines 151-200; tasks 3.11c/3.11d. |
| C P1-3; D P1-7 — task 3.11d had no executable accounting/crash seams | **Resolved.** Four exact friend-only post-commit barriers expose immutable correlated facts to `OrcaCore.ProviderCertification`. No sleep race or future protocol invention is permitted. | Matrix lines 1629-1661 and 1753-1761; quality delta lines 45-52; task 3.11d. |
| D P1-6; C P2-3 — package manifest and namespace owners absent | **Resolved.** The manifest, direct edges, CLR namespaces, assembly owners, local package version/feed, and only two friend edges are exhaustive. | Matrix lines 1781-1835; task 3.1-3.3. |
| C P1-4; B F1; D P1-10; D P2-6 — catch-all hosting, event owner, and binder claim | **Resolved.** Engine, ingress, in-memory, PostgreSQL, and DAG registrations have exact assembly owners. Ephemeral registration owns its in-process event client. Options are constructed programmatically, copied, and validated; no binder facade or catch-all exists. | Matrix lines 1837-1916; tasks 3.7-3.8; active hosting summaries carry the same split. |
| D P1-9; B F3; D P2-3 — undeclared exception/diagnostic surface | **Resolved.** `OrcaCoreException`, fixed-code built-in failures, `WorkflowDefinitionException`, complete workflow/DAG diagnostic catalogs, location grammar, eager decorator throw, and `TryBuild`/`Build` parity are exact. | Matrix diagnostic/error sections around lines 1369-1458 and 1968-2046; quality delta lines 96-136; tasks 3.1/3.4. |
| D P1-11; B F5 — opaque integration misuse cannot all be runtime-detected | **Disposed as an explicit trust/certification boundary.** `AttemptNumber` is diagnostic only; effect adapters certify `StepOperationId` discipline; elapsed time/delete acknowledgement/terminal status cannot certify stop. Core does not falsely claim to inspect opaque adapter behavior. | Matrix lines 369-378; quality delta lines 238-246; tasks 3.5/3.9/3.11c. |
| D P2-1 — pre-wait buffering contradicted `NoActiveWait` | **Resolved.** Pre-wait delivery is non-consuming, writes no mailbox/inbox/dedup state, and allows same-`EventId` first acceptance after registration. Duplicate/conflict begins only after acceptance. | Canonical event requirements and acceptance criteria; matrix event contract; task 3.8. |
| C P1-2; D P2-5 — opaque fingerprint and stale summaries | **Resolved in live authority and routing.** Opaque behavior requires a new `DefinitionVersion` and is not a fingerprint conflict. Frozen amendments/reviews/status reports are explicitly historical. | Matrix fingerprint rules; engineering conventions; active index/implementation routing. |
| C P1-5; A P2-6 — bulk retrieval versus reduced management | **Resolved.** Public instance enumeration/filter/count/statistics/bulk management is absent and explicitly deferred. Internal keyed/projection queries do not create an application facade. | Canonical management requirements; matrix deferred table; task 3.7. |
| B F2 — deterministic Phase 0 seams | **Resolved.** Guard work is bounded to declared public/package seams plus the four exact friend-only governance barriers. | Quality delta lines 31-52; tasks 3.1-3.12. |
| B F3; A P2-4 — decorator rejection classification | **Resolved.** Misplacement/duplication is an eager fluent-call `WorkflowDefinitionException`, not a compile error or delayed graph diagnostic. Transient decoration binds the preceding eligible ephemeral business step, named or lambda. | Workflow-authoring delta lines 119 and 177; task 3.7. |
| A P2-5 — lambda result/throttle asymmetry | **Explicit and guardable.** Lambdas return `ValueTask` and do not return `StepResult`; exact-type throttles target named steps only, while an ephemeral transient-pool decorator may bind a lambda. | Workflow-authoring delta lines 177 and 242; task 3.7. |
| A P2-7; B F6 — `ReadOnlyStateSnapshot` mutability/author construction | **Resolved.** It is a runtime-created non-positional projection with no public constructor, `with`, or `Deconstruct` surface. | Matrix state-context declarations and task 3.5. |
| A P2-8 — `ForEach` projector lacks hidden parent state | **Clarified as deliberate.** The projector sees detached item plus index; shared run data must be carried explicitly in `TItem`. | Matrix lines 778-792; workflow-authoring delta lines 137-160. |
| B F7 — `Init` availability-table ambiguity | **Clarified.** Staged factory/`Init` transitions are distinct from root sequence members. | Matrix staged-authoring section and exact companion factories. |
| C P2-1; D P3-1 — promised DAG visualization without an edge projection | **Resolved by removal.** Visualization is not a first-release public capability. | Matrix DAG/package descriptions and deferred/removal policy. |
| C P2-2 — parked DAG child did not consume `MaxConcurrentNodes` | **Resolved.** Every started nonterminal child, including one parked on wait, delay, or lease admission, counts until terminal. | Canonical composition requirement lines 167-174; concurrency delta lines 117-126 and scenario 159-163; task 3.10. |
| D P2-4 — empty GUID parsing unspecified | **Resolved.** Runtime-created GUID identities reject `Guid.Empty` at parse/try-parse boundaries; `DefinitionId.New()` never returns empty. | Matrix strong-value construction rules; task 3.5. |
| D P2-7 — stream continuity assigned to per-record factory | **Resolved.** `ResourceGovernanceRecord.FromPersisted` validates one copied record; `ResourceGovernanceStream.Create` copies and validates complete `1..Version` continuity. | Matrix lines 1595-1627 and 1767-1773; durable/quality deltas; task 3.3. |
| D P2-8 — canonical management omitted `ListAsync` | **Resolved.** `ListAsync`, `GetAsync`, and closed-result `ResizeAsync` agree across matrix, canonical management, and delta. | Management delta lines 151-200 and canonical MG-065. |
| C P3-1 — `StepExecutionContext` flattened into `StepContext` | **Resolved.** Execution identity remains a distinct nested value exposed through `StepContext` rather than duplicated scalar ownership. | Matrix context declarations and exact authoring companion references. |

The only surviving review nits are optional P3 ergonomics: the chain position of
`CompleteWithin` and the parallel payloadless/generic event carrier families. Both have exact
semantics and guard targets; neither is an ambiguity, safety defect, or Phase 0 blocker.

## 5. Guard-retarget readiness matrix

| Task | Required guard lane | Readiness |
|---|---|---|
| 3.1 | Exact public signatures, packages, namespaces, diagnostics, strong-value families, friends | **Authorable** |
| 3.2 | Clean local-package consumers for every manifest tier and role | **Authorable** |
| 3.3 | Provider-author/custom-host governance store, copies, continuity, append conflict | **Authorable** |
| 3.4 | Exact staged builders, leased joins, `ValueTask` lambdas, terminal rollover, decorator throw | **Authorable** |
| 3.5 | Construction families, codec/state isolation, structural fingerprint, opaque version rule | **Authorable** |
| 3.6 | Branch/item outcomes, ordering, merge suppression, path-token accounting | **Authorable** |
| 3.7 | Facades, reactive handles, events, reduced management, exact hosting/options | **Authorable** |
| 3.8 | Cast-free application-only ephemeral/durable consumer journeys and event redelivery | **Authorable** |
| 3.9 | Retry/deadline/operation identity, late fencing, occurrence identity | **Authorable** |
| 3.10 | DAG build/runtime mapping, statuses, waiting, parked-child admission, friend bridge | **Authorable** |
| 3.11a | Lease authoring, placement, admission, ancestry, leased nested `Parallel` | **Authorable** |
| 3.11b | `AmbiguousHeld` retry, non-overlap, exit, quarantine, release ordering | **Authorable** |
| 3.11c | Diagnostics/recovery/confirmation/reconciliation; no invented remote auth seam | **Authorable** |
| 3.11d | Exact store/accounting facts, four deterministic barriers, conservation, resize | **Authorable** |
| 3.12 | Run every lane, record exact expected-red/pass evidence, submit whole packet for re-review | **Ready as the execution gate** |

No unresolved product decision blocks guard implementation.

## 6. Validation

Final planning validation after remediation:

- `openspec.cmd validate reshape-developer-facing-interfaces --strict` — **PASS**;
- `openspec.cmd validate add-runtime-concurrency-limits --strict` — **PASS**;
- `git diff --check` — **PASS** (working-copy line-ending warnings only);
- task accounting — reshape **15 done / 97 pending / 112 total**, concurrency
  **7 done / 9 pending / 16 total**, with exactly **15 pending mandatory section-3 tasks**;
- exact companion structural checks — all ephemeral lambda overloads are `ValueTask`-returning,
  leased nested joins retain leased parents, and no forbidden nested `ForEach`/`While` surface is
  introduced;
- changed-document local-link scan — **PASS**;
- canonical requirement/acceptance identifier uniqueness scan — **PASS**.

No product build or runtime suite was required for this planning-only reconciliation. The next
authorized work is the guard-only section-3 packet; expected-red results must remain distinct from
passing guards until product implementation is separately authorized.
