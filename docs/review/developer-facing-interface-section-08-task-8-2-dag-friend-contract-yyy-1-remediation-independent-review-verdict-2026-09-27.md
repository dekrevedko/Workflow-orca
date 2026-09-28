# DAG authoring friend contract YYY-1 remediation independent review verdict

**Date:** 2026-09-27
**Reviewer:** independent review
**Scope reviewed:** the twenty-entry freeze on base `5adddc3ba0ca7e0f70ee1f3b7317e69c1df7e76a`,
named by
`developer-facing-interface-section-08-task-8-2-dag-friend-contract-yyy-1-remediation-dirty-manifest-2026-09-27.txt`.
This includes, as semantic inputs, the two committed `MODIFIED` successor blocks of
`admit-dag-authoring-friend-boundary`, which are unchanged from the base.
**Authorization requested:** approval of the `OrcaCore -> OrcaCore.Dag` authoring-friend contract
(task 1.2) and a checkpoint of this exact target.

## What this verdict approves

- **The contract:** the authoring-only friend `OrcaCore -> OrcaCore.Dag`, limited to:
  - the internal constructors of `Validation<T>`, `WorkflowDiagnostic`, `AuthoredLocation`,
    `DefinitionFingerprint`, and `WorkflowDefinitionException`;
  - one future internal canonical-hash operation on `DefinitionFingerprint`, whose exact signature
    must be pinned and reviewed with the Task 2.4 metadata guard.
- **The rest of the contract:**
  - the unchanged one-way package graph and the sole DAG-to-durable runtime bridge
    `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting`;
  - the document disposition in this target;
  - the atomic 1.3/1.4 plan.
- **Explicit exclusions:**
  - no public factory, `Core -> Dag` reference, or DAG-owned parallel value family;
  - no other internal DAG member, runtime friend, or child-start access;
  - no non-atomic canonical sync or registry transition.
- **Not approved:** it does not add the friend attribute, synchronize canonical specs, move the
  registry, or authorize Task 8.2 product source. Tasks 1.3/1.4 still need their own atomic review
  and checkpoint.

## Summary

YYY-1 is fixed. Every numbered, binding, and guide document that stated a public-only DAG
dependency or an incomplete exact friend graph is now reconciled, or explicitly excluded with a
correct reason:

| Document | Status |
|---|---|
| 03 §glossary | amended |
| 08 CP-020 | amended |
| 10 PR-005 | amended |
| 11 NF-002 | explicitly excluded |
| Decision 22 (`00-stack-decisions.md`) | amended |
| `01-solution-architecture.md` | amended |
| `project-technical-overview.md` | amended |

- **Friend lists:** all of them now enumerate the current seven product friends. They name the
  eighth only as proposed and not compiled, and they keep the runtime bridge distinct.
- **Pins:** the five refreshed Task 7.3 rows, all 22 rows, the artifact digest, and the Task 8.0 map
  pin reproduce independently. Reverting a row or digest, or editing a pinned source, turns the
  guard red.
- **Validation:** every lane reproduces.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
twenty entries. Every script asserted its disposable location and that the main `HEAD` was still
`5adddc3b`. Main was never modified, and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 20 lines, `5d3633b7…` | 1,563 B, `5d3633b7436c67dd42d5def5ee17d4c3893c079619e7165c7f9b8927fa2d97ad` |
| Content record, all 20 rows (reviewer-computed) | `2d073074…` (not stated in the request) | 2,975 B, `50f57274b37fd00375e0b3fd9d89d487fdfcda3f0f571d0499407f2f6888eecf`; the claimed value was not reproducible (P3-3) |
| Simulated checkpoint | tree `3cc2b8eb…` | `3cc2b8eb8286e4e5c9f1691774d08dc15451f127` from a copy of the live index and from a committed disposable copy; path set equals the manifest (5 A / 15 M) |

- **Byte binding:** the repository sets `core.autocrlf=false`. All 20 blobs in the claimed tree
  equal the raw file bytes (`git hash-object --no-filters`), so the tree binds the exact frozen
  bytes.
- **Retained records:** the rejected round-80 packet is byte-exact:
  - manifest `56629a6d…`;
  - request `0307d43e…`;
  - REJECT verdict 9,160 B, `55762351…`.

  All three are in `appendOnlyRecords`, together with this manifest and request (4,262 B,
  `5a788884…`, LF). The active-freeze pointer names this manifest.
- **Unchanged from round 80:**
  - `CLAUDE.md`, document 17, and the Task 8.0 map are byte-identical;
  - both successor blocks are byte-identical to the base;
  - there is no `src/**` or canonical `openspec/specs/**` change, and no compiled friend.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 226/226 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 failures |
| Strict OpenSpec | 19/19 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. Document review

- **Doc 03:** "depends directly only on the `OrcaCore` package and consumes public workflow
  references". The authoring friend is named as proposed, not compiled.
- **CP-020 and PR-005:** state the same dependency. They also correct the old "no OrcaCore package
  depends on `OrcaCore.Dag`" wording, which already conflicted with the canonical
  `OrcaCore.Dag.Hosting -> OrcaCore.Dag` edge. `OrcaCore.Dag.Hosting` is now named as the approved
  outward adapter (see P3-2).
- **Doc 11:** the exclusion is correct. NF-002 governs the `OrcaCore` package dependency closure, and
  a friend attribute on `OrcaCore` adds no package edge to it.
- **Decision 22** (table row and a new dated entry), **`01-solution-architecture.md`** (lines 77,
  80, and 135), and **`project-technical-overview.md`:**
  - all seven current product friends are listed exactly;
  - the eighth is marked proposed and not in metadata;
  - "only friend bridge to the child start/join seam" is kept.
- **Corpus sweep:** outside `docs/archive/` and `docs/review/`, no remaining public-only DAG claim
  and no incomplete exact friend list in any documentation class (numbered, BINDING, or GUIDE). The
  one remaining statement is change-local design history (P3-1).
- **Task 7.3 pins:**
  - rows 1, 6, 7, 12, 14, and 18 (`CLAUDE.md`, the two implementation documents, the overview,
    doc 03, and doc 10) recompute from LF-normalized bytes, and all 22 rows match;
  - the artifact digest `6733b968…` equals its guard constant;
  - document 08 is outside the 22-source list.

## 4. Guard probes

Each probe refreshed the mutable current-match pins after its edits.

| Probe | Mutation | `Task73_` result |
|---|---|---|
| P0 | pins refreshed only (positive control) | green |
| D1 | row 6 reverted to its pre-remediation hash | **red**: artifact digest |
| D2 | guard artifact digest reverted to the round-80 value (rebuilt) | **red**: artifact digest |
| D3 | Decision 22 reworded, row not refreshed | **red**: row 6 source hash |
| D4 | doc 03 friend sentence reworded, row not refreshed | **red**: row 14 source hash |
| R0 | restored | green; porcelain equals the manifest |

## 5. Non-blocking observations (P3)

- **P3-1: reshape's own design keeps an outdated friend list.**
  - `openspec/changes/reshape-developer-facing-interfaces/design.md` decision 22 still says
    "Product friends are exact". It lists four edges, which has been out of date since the
    2026-08-20 canonical sync added the `OrcaCore -> Core/engines` friends.
  - Change-local design rationale was not reconciled on that occasion either, and the source map
    does not classify it as documentation.
  - The new design's "every active document" sentence should name change-local design artifacts as
    excluded, or reshape's design should carry a one-line pointer to the amendment.
- **P3-2: the reworded negative clause is less exact than before.**
  - CP-020 and PR-005 now forbid a DAG dependency from "foundational application, Core, engine,
    provider, or durable-hosting" packages. That list does not clearly cover
    `OrcaCore.Runtime.Protocol` (or `OrcaCore.Provider.Abstractions`).
  - The canonical exhaustive edge list still forbids those edges, so there is no behavior gap.
  - "No OrcaCore package other than `OrcaCore.Dag.Hosting`" would be exact. This can be tightened
    with the 1.3 reconciliation.
- **P3-3: the content-record claim is unreproducible.**
  - The review message cited a content record `2d073074…`. The request states neither that value
    nor its convention.
  - No subset or format I tried reproduced it. The raw-byte-bound tree is what binds this approval.
  - Future requests should state the record's value and exact convention.

## 6. Reviewer hygiene and checkpoint instructions

`HEAD` is `5adddc3ba0ca7e0f70ee1f3b7317e69c1df7e76a`, with the twenty frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 20 manifest paths from the live index;
- confirm the tree is `3cc2b8eb8286e4e5c9f1691774d08dc15451f127` with parent `5adddc3b`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it in `appendOnlyRecords`.

The activation should then change only the review state; this marks task 1.2 complete. Tasks
1.3/1.4 (atomic canonical sync and approved-pending registry) are the next reviewed target. No
friend attribute or Task 8.2 product source may land before they are checkpointed.

## Determination

The narrow authoring friend contract is now approved, and the whole active normative, binding,
and guide corpus agrees with it. That corpus names the current seven-friend graph exactly and
presents the eighth edge as approved-but-not-yet-compiled authoring access. The edge grants no
runtime or child-start privilege.

**Verdict:** **APPROVE**
