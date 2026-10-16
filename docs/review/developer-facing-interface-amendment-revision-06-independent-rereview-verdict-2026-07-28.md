# Independent rereview verdict — amendment Revision 6 / task 4.15

**Date:** 2026-07-28  
**Verdict:** `REJECT`  
**Scope:** planning and documentation mapping only  
**Gate effect:** task `4.15` remains open; task `4.16` must not begin; task `6.0`
remains blocked.

## 1. Executive determination

Revision 6 repairs the substantive Revision 4 findings that prompted this rereview:

- L6 now resolves to the exact active durable-runtime requirement and explicitly defers canonical
  promotion and product conformance;
- the complete capability-matrix registry preamble and re-entry bar landed;
- the missing `state-driven-runtime` change delta has an explicit pending owner in task `10.9`;
- all fifteen deliberately excluded semantic claims are present;
- all current normative, task, and publication statements withhold L4 until task `4.16`;
- tasks `9.10`, `9.12`, and `10.13` are reopened, and task accounting is correct.

The eight Revision 4 mapped corrections and all eight amendment section 10 semantic checklist items
also rederive successfully. Strict OpenSpec validation passes.

The gate nevertheless cannot close. The active **section 9 Plan deltas** in the Revision 6 amendment
still says Revision 5 reopened only `4.15` and instructs `4.15` to approve Revision 4. That directly
conflicts with the same amendment's Revision 6 status/history, the live task, the phased plan, and the
rereview request. The request requires an unconflicted approval of Revision 6 and permits `APPROVE`
only when no gate-blocking finding remains.

There is one P2 gate-blocking finding and one P3 non-blocking documentation-fidelity finding. There
are no P0 or P1 findings.

## 2. Review provenance

| Item | Independently observed value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Review target | `HEAD` plus the current dirty worktree, including the rereview request |
| Before-review sorted porcelain entries | 387 |
| Before-review LF-normalized sorted-status SHA-256 | `fd8e1914ca6b00eec19fb974ae4abb5b608246a548302926e13bc4634702b97a` |
| After-verdict sorted porcelain entries | 388 |
| After-verdict LF-normalized sorted-status SHA-256 | `59c10366da0f5bcde0e9f7517278c3a5920b704756ec89d946b522c796026311` |
| Status-set delta caused by this review | exactly this one new verdict path; zero removed entries and zero changed status entries |

The authority order from the rereview request was followed:

1. `docs/specs/17-selected-mode-capability-matrix.md`
2. unchanged `docs/specs/17-public-authoring-contract.cs`
3. affected canonical requirements under `openspec/specs/`
4. active deltas under `openspec/changes/reshape-developer-facing-interfaces/specs/`
5. the Revision 6 amendment
6. reshape tasks
7. the semantic appendix
8. the phased plan

No product implementation was reviewed or edited. No existing file was edited, renamed, deleted, or
replaced by this rereview.

## 3. Findings

### F1 — P2, gate-blocking — the active plan delta still makes Revision 4 the approval target

The amendment correctly establishes the new state at lines 3-6 and 15-25:

- Revision 4 was independently rejected;
- Revision 6 supersedes Revision 4 as the live gate target;
- Revision 6 remains pending fresh independent approval under `4.15`.

The amendment's active plan section then contradicts that state:

- line 589: `Revision 5 reopens only approval gate 4.15`;
- lines 594-595: `4.15` must obtain independent approval of `revision 4's exact mapped
  corrections before 4.16 begins`.

This is under `## 9. Plan deltas`, not merely in the revision-history narrative. In the requested
authority order, the amendment also precedes `tasks.md`. The task file correctly asks for independent
approval of Revision 6, but it cannot cure a contradictory live instruction in the higher, earlier
reviewed artifact.

**Impact:** Revision 6 correction item 7 ("Review state") fails. An approval following section 9
would approve the already-rejected Revision 4, while an approval following the request and task would
approve Revision 6. The result would not be the unconflicted Revision 6 approval required before
`4.16`.

**Required remediation:** update the active section 9 gate prose so it says Revision 6 is the corrected
live target and `4.15` requires fresh independent approval of Revision 6. Preserve the historical
Revision 4 verdicts and revision-history facts.

**Owner:** task `10.13` documentation-mapping validation, with `4.15` remaining open for a fresh
verdict.  
**Blocks task `4.15`:** yes.

### F2 — P3, non-blocking — the historical Revision 3 table retains the old L4 gate shorthand

Revision 6 history says every L4 publication gate was aligned on implementation task `4.16`.
Amendment line 60, inside the historical Revision 3 finding-disposition table, still says L4
publication is `gated on §2.3`.

The current law row, the current exclusion note, task `9.12`, and the published appendix all use task
`4.16`; therefore the live rule is unambiguous and Revision 6 correction item 5 passes. The historical
table wording does not independently block `4.15`, but it preserves the old ambiguous shorthand that
Revision 6 claims to have aligned everywhere.

**Suggested remediation:** qualify the row as historical/superseded or state the current `4.16`
publication gate without rewriting the historical decision.

**Owner:** tasks `9.12` and `10.13`.  
**Blocks task `4.15`:** no.

## 4. Revision 6 corrections rederived

| # | Requested correction | Determination |
|---|---|---|
| 1 | L6 citation | **PASS.** Appendix L6 links to `openspec/changes/reshape-developer-facing-interfaces/specs/durable-runtime/spec.md`, whose exact heading is `### Requirement: Durable leases are lexical occurrence-owned obligations`. The appendix says canonical promotion remains Section 6 work and does not assert current product conformance. |
| 2 | Matrix registry preamble | **PASS.** Section 17.6 contains the complete authoring-shape scoring rule, dated v1 residue, recovery distinction, and re-entry bar. The bar requires evidence for every applicable root-only encoding, admits authored/fixed-budget acceptance evidence, and disclaims general equivalence. |
| 3 | `state-driven-runtime` traceability | **PASS under the accepted disposition.** The canonical rewrite is present; the reshape change package has no delta; pending task `10.9` explicitly owns adding and verifying that missing delta. |
| 4 | Semantic exclusions | **PASS.** Exactly fifteen exclusion bullets are present, including reachable `TryBuild` after eager diagnostics and rejection of "fan-out rank one" as a computational-complexity claim. |
| 5 | L4 gate | **PASS for every live rule.** The law row, current amendment note, task `9.12`, and appendix withhold L4 until task `4.16`. F2 records stale wording only in the historical Revision 3 table. |
| 6 | Task state | **PASS.** Tasks `9.10`, `9.12`, `10.9`, and `10.13` are pending. Counts are 60 complete / 75 pending / 135 total; duplicate task IDs: 0. |
| 7 | Review state | **FAIL — F1.** The phased plan, task, amendment status, and amendment section 11 identify Revision 6 correctly, but the active amendment section 9 plan delta still instructs approval of Revision 4. Tasks `4.15`, `4.16`, and `6.0` remain unchecked. |
| 8 | Scope | **PASS.** `git diff --exit-code -- docs/specs/17-public-authoring-contract.cs` is empty. The 17 status entries added after the frozen Section 5 packet are all under `docs/` or `openspec/`; none is a product-source path. |

## 5. Eight Revision 4 mapped corrections

| Correction | Determination |
|---|---|
| C1 — omit `MaxParallelBranchesPerScope` | **CONFIRMED.** No replacement branch-width semantic is introduced; allocation exhaustion remains infrastructure failure rather than authored workflow outcome. |
| C2 — scope the two-quantity model to one structured root fan-out scope | **CONFIRMED.** Path tokens and admitted root-`ForEach` items are scheduler-local quantities; step throttles, transient pools, durable leases, and DAG-node admission remain distinct. |
| C3 — use the real authored/value-envelope budget boundary | **CONFIRMED.** The mapped evidence uses authored `MaxItems` and fixed codec/payload/snapshot/envelope budgets, not a nonexistent fixed platform `MaxItems` ceiling. Revision 6 also lands the complete matrix re-entry bar. |
| C4 — phase- and scope-bound builder ownership | **CONFIRMED.** `Open`/`JoinPending`/`Frozen`, epoch/scope lifetime, successor-epoch join facades, callback expiry, root-terminal freeze, repeat-build equivalence, and session-owned configuration are canonical. The unchanged-graph guarantee remains restricted to the five governed lifecycle failures, with five matching `SFE-AUTH-LIFECYCLE-*` codes. |
| C5 — close `FailureOccurrence` construction without changing `WorkflowFailure` equality | **CONFIRMED.** Runtime-created closed occurrence variants, validation, creation-time attachment, aggregate provenance, copying, codec allowlist, occurrence value equality, and `WorkflowFailure` reference equality all remain mapped. |
| C6 — resize-debt capacity safety and token-only progress | **CONFIRMED.** Downward resize may create debt without revocation and blocks new grants until cleared; path-token capacity alone cannot deadlock, without claiming item-admission or global progress. |
| C7 — fingerprint contributors and compiler-format ownership | **CONFIRMED.** Fingerprint coverage is closed to authorized authored contributors and excludes compiler format/options, identity/version, fairness limits, opaque behavior, and author-supplied contributors. `MaxInternalInstructionsPerQuantum` remains compiler-format/runtime-compatibility owned. |
| C8 — append-only task numbering | **CONFIRMED.** Pre-amendment maxima at `HEAD` remain 4.14 / 5.9 / 6.11 / 7.13; current IDs are unique and append-only. Reopened tasks change marks, not identifiers. |

`MaxActiveFibers` is removed from all four normative roles: it is not an authored field, a scheduler
admission resource, a fingerprint contributor, or an authoring diagnostic. No
`MaxParallelBranchesPerScope` replacement exists. `SFE-LIMIT-003` and `SFE-LIMIT-008` are not
diagnostic-catalog entries.

## 6. Amendment section 10 checklist

| # | Checklist item | Independent answer |
|---|---|---|
| 1 | Scoped observational simulation and honest residue | **Accepted.** The budget, dependency, and observation preconditions are explicit; tagged flattening and sequential staging are not represented as general equivalences. |
| 2 | Reference model as normative v1 concurrency | **Accepted.** Fixed root `Parallel`, root `ForEach` admission, token release while parked, and non-overlapping sequential root scopes agree across the normative artifacts. |
| 3 | Remove `MaxActiveFibers` in all four roles, with no replacement | **Approved.** The item is posed as a question, not pre-answered. The four-role removal and retired diagnostic codes rederive. |
| 4 | Authoring session, clarifications, restricted atomicity | **Correct.** The three-state lifecycle, scope/epoch ownership, successor facades, freeze/build behavior, five governed failures, and restricted unchanged-graph guarantee agree. |
| 5 | Exact `FailureOccurrence` contract | **Approved.** Declaration closure, validation, failure-creation attachment, aggregate origin, equality asymmetry, detachment copying, codec allowlist, and defensive matching agree. |
| 6 | Nine semantic laws, including L8/L9 and gated L4 | **Accepted.** All 15 citations resolve to exact headings; all 15 exclusions are present; L8/L9 replace the withdrawn live-fiber law; the current L4 publication gate is task `4.16`. F2 is historical wording drift only. |
| 7 | `state-driven-runtime` reconciliation | **Agreed.** The unreachable nested-fan-out scenario is gone; supported nested `If` uses a linear continuation; the parent requirement no longer authorizes fan-out in a child body. Missing delta traceability remains explicitly pending under `10.9`. |
| 8 | Compiler-format compatibility | **Agreed.** Nonterminal durable instances retain referenced formats until terminalization or explicit migration, with the bounded pre-v1 hard-cutover allowance. |

All eight semantic checklist answers are affirmative. They do not overcome F1, which is a separate
failure of the Revision 6 approval target and planning-gate mapping.

## 7. Validation evidence

All commands ran from `X:\Projects\GitHub\Workflow-orca`.

| Command or independent check | Exact result |
|---|---|
| `openspec.cmd validate --all --strict --no-interactive` | exit 0; `Totals: 16 passed, 0 failed (16 items)` |
| `git diff --exit-code -- docs/specs/17-public-authoring-contract.cs` | exit 0; no diff |
| `git diff --check` | exit 0; no whitespace errors; existing LF-to-CRLF working-copy warnings only |
| PowerShell exact-heading resolver over every semantic-appendix requirement link, using case-sensitive exact `### Requirement: ...` line membership in the resolved target | 15 citations; 15 resolved; 0 unresolved |
| PowerShell local-link resolver over the amendment, matrix, appendix, tasks, phased plan, and rereview request | 29 local links checked; 0 missing |
| PowerShell checkbox parser using `^\s*- \[(?<mark>[ xX])\]\s+(?<id>\d+\.\d+[a-z]?)\b`, followed by ID grouping | 135 tasks; 60 complete; 75 pending; 0 duplicate IDs |
| `Test-Path openspec/changes/reshape-developer-facing-interfaces/specs/state-driven-runtime/spec.md` | `False`; task `10.9` explicitly owns the missing delta |
| Exact-heading search in the active durable-runtime delta | requirement found at line 199 |
| Semantic-exclusion bullet count under `## Deliberately excluded claims` | 15 |
| Sorted `git status --short`, joined with LF plus a final LF and hashed as UTF-8 without BOM | before: 387 entries and `fd8e1914ca6b00eec19fb974ae4abb5b608246a548302926e13bc4634702b97a`; after: 388 entries and `59c10366da0f5bcde0e9f7517278c3a5920b704756ec89d946b522c796026311` |
| Frozen-manifest/current-status set comparison | manifest: 370; current before verdict: 387; frozen entries missing: 0; post-packet extras: 17; extras outside `docs/` or `openspec/`: 0 |

The before/after sorted-status set difference is exactly:

```text
?? docs/review/developer-facing-interface-amendment-revision-06-independent-rereview-verdict-2026-07-28.md
```

No pre-existing status entry was added, removed, or changed by the rereview.

## 8. Immutable evidence

Raw SHA-256 hashes were reproduced with `Get-FileHash -Algorithm SHA256`:

| Immutable artifact | Lines | SHA-256 |
|---|---:|---|
| `docs/review/developer-facing-interface-section-05-exit-review-dirty-manifest-2026-07-27.txt` | 370 | `F001016F92CF056AA1CF6E99203353112504500C1C5E582625AE3B607A41BBAD` |
| `docs/review/developer-facing-interface-section-05-exit-review-request-2026-07-27.md` | 159 | `53FCB5818FAE78F725B1676A2EBAF6FAB00C1595AC83FC7BD1BCF3DCE17BFA2E` |
| `docs/review/developer-facing-interface-amendment-rev04-task-4-15-independent-approval-verdict-2026-07-28.md` | 532 | `055D4CA879EC7B431F7D92F14074DCD7BA2E61CBD66DD2F38028D188E79B758C` |

All 370 frozen manifest entries remain present. The historical request, manifest, and prior verdict
were not edited.

## 9. Final gate disposition

`REJECT`.

- Task `4.15` remains open because F1 is gate-blocking.
- Task `4.16` remains blocked and must not begin.
- Task `6.0` remains blocked regardless of any later Revision 6 approval.
- Tasks `9.10`, `9.12`, `10.9`, and `10.13` remain pending.
- This verdict does not authorize product implementation, assert product conformance, approve
  Section 5 exit, or begin Section 6.

