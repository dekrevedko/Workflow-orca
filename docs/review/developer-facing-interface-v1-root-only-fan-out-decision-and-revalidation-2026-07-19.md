# Owner decision and final delta revalidation: root-only v1 fan-out

**Date:** 2026-07-19

**Status:** current final planning review for fan-out placement. This document supersedes the
placement decision in section 3.1 and the affected finding/guard rows of the
[earlier consolidated review](developer-facing-interface-v1-simplification-consolidated-review-2026-07-19.md).
That review and reviews A-D remain immutable evidence for the contract snapshot they inspected.

**Nature:** planning-contract revalidation only. No product source or Phase 0 guard source is
claimed to implement this contract.

## 1. Final verdicts

| Gate | Verdict | Meaning |
|---|---|---|
| Planning contract | **APPROVE** | The root-only fan-out decision is coherent across the normative matrix, exact companion, canonical requirements, and both active OpenSpec changes. No current P0, P1, or P2 design contradiction remains. |
| Guard-retarget readiness | **READY** | All 15 mandatory section-3 tasks remain authorable without inventing a public signature or recursive fan-out rule. |

`READY` authorizes only the expected-red guard packet. Task 3.12 remains the run, evidence, and
independent re-review gate. Task 4.0 and product implementation remain blocked until that gate
passes.

## 2. Owner decision

For v1, the structural placement rule is:

- fixed `Parallel`, bounded `ForEach`, and `While` are root-sequence-only in both modes;
- `If` is the only structural control-flow member that remains nestable;
- nested, branch, item, and leased builders retain ordinary sequencing, nested `If`, `Wait`,
  `Delay`, and step decorators wherever their selected mode and parent kind already permit them;
- a durable root-nested `If`/`While` body, root-`Parallel` branch, or root-`ForEach` item may open one
  scoped `AcquireResources` body when no live ancestor lease exists;
- a dedicated leased body exposes no `Parallel`, `ForEach`, `While`, nested
  `AcquireResources`, or `ContinueAsNew`;
- retryable ambiguity keeps the same capacity-reserving lease obligation. Root-only fan-out does
  not change the approved `AmbiguousHeld` retry and quarantine-on-exit rules.

“Root-sequence-only” means the member is available only on the selected ephemeral or durable root
workflow builder. A root `Parallel` or root `ForEach` join returns that same selected root family,
so another root-sequence operation may follow. It does not mean that a branch or item cannot use
ordinary nested `If` or, in durable mode, its own scoped lease.

## 3. Why root-only is the better v1 boundary

The runtime could eventually support recursive fan-out, but publishing it now would multiply the
public and verification surface before the first release needs it. The exact companion reduction
is mechanical:

| Surface | Previous reviewed snapshot | Current root-only contract |
|---|---:|---:|
| `Parallel<TResult>` entry methods | 12 | 2 |
| Parallel branch-scope types | 12 | 2 |
| Parallel join types | 12 | 2 |
| Non-root `Parallel` entry methods | 10 | 0 |
| Non-root Parallel scope/join types | 20 | 0 |
| Root `ForEach` entry methods / join types | 2 / 2 | 2 / 2 |

The reduction removes recursive branch identity, path admission, nested merge, and lease ancestry
combinations from the first-release contract. It also removes the leased-join widening problem by
construction: no fan-out can begin while a lexical lease is live.

The cost is explicit. V1 cannot express conditional, item-local, or leased nested concurrency
inside one workflow definition. Consumers may compose sequential root fan-outs, put a nested `If`
inside a root branch/item, or use durable DAG node concurrency where the workload naturally spans
child workflows. These are not asserted to be semantics-preserving rewrites for every future use
case. Nested `Parallel` therefore remains a deferred capability that requires a new amendment
covering exact placement, recursive identity, admission, merge/failure, recovery, and lease rules.

The initial scheduler journeys remain covered by:

- root fixed `Parallel` for a statically authored branch set;
- bounded root `ForEach` for deterministic finite dynamic fan-out; and
- `OrcaCore.Dag` for durable dependency-driven child concurrency.

## 4. Reconciliation with the parallel reviews and owner comments

| Concern | Current disposition |
|---|---|
| Review D P1-1: a leased nested `Parallel` join widened to an unrestricted builder | **Eliminated by the owner decision.** Leased and all other non-root builders expose no `Parallel`; compiler defense rejects a hand-built nested graph with `SFE-AUTH-CAP-001`. |
| Review D P1-4: quarantine versus retry | **Owner decision retained.** A retryable ambiguous attempt keeps the same lease obligation, token, tickets, and capacity in `AmbiguousHeld`; quarantine is required before progress only when ambiguous exit, exhaustion, cancellation, deadline, termination, or abandonment wins. |
| Polling example for ephemeral output | **Resolved and unchanged by this delta.** The ordinary path is notification-driven: `var output = await start.WaitForOutputAsync(token);`. `GetOutputAsync` remains a one-shot snapshot query, not the prescribed wait loop. |
| Casts around DAG registration and start results | **Resolved and unchanged by this delta.** Closed result unions retain conflict data, while `GetHandleOrThrow()` supplies the cast-free success path for both registration and start. |
| Generic matrix join notation versus concrete companion types | **Resolved and further simplified.** Matrix fan-out notation now uses `TRootBuilder` only; the companion contains exactly two concrete root Parallel families. Compact notation remains a semantic index, never an extra public type. |

No other finding from reviews A-D is reopened by making fan-out root-only. The earlier consolidated
review remains the detailed disposition record for strong values, reactive waits, DAG mapping,
hosting, management reduction, diagnostics, package ownership, resource governance, and the
deterministic lease barriers.

## 5. Guard implications

The guard-only packet must use the new root-only baseline:

- task 3.4 proves exactly two root `Parallel` branch-scope/join families and proves that none of the
  ten non-root builder families exposes `Parallel` or a corresponding scope/join type;
- task 3.6 exercises ordered root-`Parallel` and root-`ForEach` joins and the unchanged path-token
  release/admit/reacquire model;
- task 3.11a proves approved independent lease placements and that every leased body omits all
  fan-out, nested acquisition, and `ContinueAsNew`;
- quality guards compile both positive root journeys and negative nested/branch/item/leased
  discovery, while compiler-defense fixtures reject stale or hand-built nested fan-out;
- task 3.12 runs and records the complete packet and obtains an independent final re-review.

No guard source was changed as part of this planning decision.

## 6. Validation

The revised planning artifacts pass:

- `openspec.cmd validate reshape-developer-facing-interfaces --strict` — **PASS**;
- `openspec.cmd validate add-runtime-concurrency-limits --strict` — **PASS**;
- task accounting — reshape **15 done / 97 pending / 112 total**, concurrency
  **7 done / 9 pending / 16 total**;
- exact companion structural checks — **2** `Parallel<TResult>` entries, **2** Parallel
  branch-scope types, **2** Parallel join types, **2** root `ForEach` entries, and **2** root
  `ForEach` join types; no non-root Parallel family remains;
- positive nested-`Parallel` stale-claim scan across the active matrix, companion, canonical docs,
  supporting plans, and OpenSpec artifacts — **PASS**;
- local Markdown link scan across all 29 changed planning documents — **PASS**;
- canonical requirement/acceptance heading identifier scan — **247 unique IDs / PASS**;
- `git diff --check` — **PASS** (working-copy line-ending warnings only).

No product build or runtime test suite was required because this change edits planning and
documentation only.
