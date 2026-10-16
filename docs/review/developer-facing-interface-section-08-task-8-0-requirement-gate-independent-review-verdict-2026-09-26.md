# Reshape Task 8.0 Section 8 requirement gate independent review verdict

**Date:** 2026-09-26
**Reviewer:** independent review
**Scope reviewed:** the eleven-entry freeze on base
`4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`, named by
`developer-facing-interface-section-08-task-8-0-requirement-gate-dirty-manifest-2026-09-26.txt`.
**Authorization requested:** approval and checkpoint of the Task 8.0 requirement gate before any
Section 8 source work. This verdict does not authorize it.

## Summary

Much of the target is exact:
- **Canonical mapping:** all 25 quoted canonical requirement headings exist verbatim in their named
  capabilities. AC-606–AC-618 are each routed to the task that owns their behavior.
- **Agreement where checked:** runtime-owned progression, lineage, detached outputs,
  notification-driven terminal waiting, and `MaxConcurrentNodes` counting parked children agree
  across canonical OpenSpec, document 17 §17.2.6, and document 12.
- **Handoff:** the nine Task 3.10 scenarios are handed off with no disposition change.
- **8.1 decision:** reconciling the two existing six-file package roots in place is correct.
- **Pins and validation:** every pin, both anchors, and every validation lane reproduce.

Two blocking defects make the gate incomplete as a hand-off to Section 8:

- **UUU-1 (P2): the map omits numbered obligations that Section 8 must satisfy.**
  - Document 14 (JS-001–JS-010, JS-AC-001–JS-AC-018) is the normative DAG and Kubernetes-job
    scenario. It is cited only by name.
  - Repository waivers already assign JS-AC-016, JS-AC-017, and JS-AC-018 to tasks 8.1–8.3, 8.7,
    8.8, and 8.10.
  - AC-613's governing requirement DU-033, one logical outbox with partitioned claims, is not
    mapped. The only outbox requirement cited governs the external application-event outbox.
- **VVV-1 (P2): the acceptance evidence handed to Section 8 is vacuous.**
  - The acceptance catalog counts `[Trait("AC", …)]` in compile-removed files.
  - AC-606–AC-616 and JS-AC-001–006 and 008–013 are covered today only by traits in
    `<Compile Remove>`d legacy tests, several of which exercise removed public
    `RunChild`/`RunChildren`/`RunExternalJob` surfaces.
  - The gate's "must be executable before first-release exit" has no enforcing mechanism, and the
    AC-618 waiver names the wrong owner task.

## Method

Validation and probes ran in two disposable detached worktrees holding the same eleven entries.
Every script that could commit or reset first asserted its disposable location and that the main
`HEAD` was still `4ef6253e`. Main was never modified, and the review created no ref in the reviewed
repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 11 lines, 953 B, `7e97b158…31f4` | identical |
| Content record, excluding the request and history catalog | 9 rows, 1,386 B, `6e612bd6…9ce1` | identical |
| Entries | 8 modified, 3 untracked, 0 staged | identical |
| Simulated checkpoint | not stated | tree `9ea9b6647c287c39a3d89c516461a66aee2a3299`; path set equals the manifest (3 A / 8 M) |

- **Prior chain:** checkpoint `4f95a1ce` has tree `8c8fed45`, exactly the approved closeout. Evidence
  commit `457a862f` is its only child and adds the 2026-09-26 verdict byte-for-byte (6,322 B,
  `46b0458f…`). Activation `4ef6253e` changes only the two transition values.
- **Registry:** the 22 archived freezes and 16 entries reproduce, and every declared current-match
  pin is exact.
- **Review convention:** the harmonization active freeze stays `null`, so it is not reused for
  reshape. The history catalog names this manifest as its active freeze.
- **Pins:**
  - phased plan row 9 is `bab0317a…`;
  - all 22 Task 7.3 rows match, and the artifact digest `0fa63c70…` equals its guard constant;
  - the Task 8.0 map `4dd9138f…` equals its guard constant.
- **Scope:** no `src/**`, canonical `openspec/specs/**`, package-manifest, or behavior change.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 226/226 |
| `Disposition=ExpectedRed` | exactly 14 failures |
| Strict OpenSpec | 18/18 |
| Reshape ledger | 130 complete / 29 open / 159 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. What the gate gets right

- **Headings:** every one of the 25 distinct `(capability, heading)` pairs in the task table exists
  exactly in `openspec/specs/<capability>/spec.md`.
- **AC routing:** 606 diamond → 8.2; 607 build/mapping → 8.3; 608 restart identity → 8.3/8.5;
  609 node ceiling → 8.7; 610 racing completion → 8.5; 611 failure closure → 8.6; 612 no public
  child → 8.4; 613/614 → 8.4/8.5 (the canonical quality scenario assigns them to 8.4, 8.5, and
  8.10); 615 resultless → 8.2; 616–618 → 8.6. AC-601–605 concern workflow fanout, not DAG nodes.
- **Semantic agreement:**
  - canonical `durable-runtime` "Durable DAG progression is runtime owned", document 17 §17.2.6,
    and AC-609 all count a child parked in a wait until it is terminal;
  - `management-and-querying` and §17.2.6 both require subscribe-then-recheck waiting that never
    polls and cancels only the local wait;
  - the friend edge `OrcaCore.Durable.Hosting → OrcaCore.Dag.Hosting` matches the canonical
    friend allowlist and task 8.4.
- **Handoff:** `dag-contract-scenarios.json` holds exactly the nine Task 3.10 scenario IDs listed,
  and no disposition changes.
- **8.1:** `src/OrcaCore.Dag/` and `src/OrcaCore.Dag.Hosting/` hold six files, and both package IDs
  are in the exact manifest, so reconcile-in-place is correct.

## 4. UUU-1 (P2): numbered obligations missing from the map

- **Document 14 is not mapped.**
  - `docs/specs/14-driving-scenario-eks-job-scheduler.md` is part of the normative package. The
    source map says its JS series intentionally has no canonical counterpart, so the numbered side
    is its only home.
  - It defines JS-001–JS-010 and JS-AC-001–JS-AC-018 for typed DAG execution (JS-AC-001–005, 017)
    and the companion Kubernetes Job journey (JS-AC-006–016, 018).
  - The map cites document 14 only by name for 8.8/8.9 and assigns no JS requirement or criterion.
  - `RepositoryGuardTests.AcceptanceCriterionWaivers` already assign JS-AC-016 to 8.8/8.10,
    JS-AC-017 to 8.1–8.3/8.10, and JS-AC-018 to 6.7/8.7/8.10.
  - So the gate's claim to map "all tasks 8.1–8.10" to their numbered contract, and its statement
    that "the selected canonical and numbered obligations above agree at this gate", do not hold
    for these rows.
- **DU-033 is not mapped.**
  - AC-613 cites DU-033: "One logical provider outbox SHALL retain every approved record kind,
    while selector-aware claims partition public workflow events from internal continuations and
    other host/provider work. The public dispatcher never receives internal records."
  - The 8.5 row instead cites canonical "Outbox delivery is adapter-driven and at-least-once".
    That requirement governs outbound *application* events through `IWorkflowEventDispatcher`, and
    says only that internal *continuation* records use their own runtime pump.
  - Canonical "Workflow store persists first-class durable records" names internal continuations
    and external outbox records, but not DAG child-start/join records.
  - The gate should cite DU-033 and state the partition obligation: internal child-start records
    live in the one logical outbox, are claimed only by the internal runtime pump, and never reach
    the application dispatcher.
  - It should also record whether those records count as canonical "internal continuations". If
    they don't, CLAUDE.md requires routing a reviewed amendment before task 8.5.

## 5. VVV-1 (P2): the acceptance evidence being handed off is vacuous

- **What the catalog counts:** `AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver`
  enumerates every `tests/**/*.cs` outside `bin`/`obj`, including files the test projects
  `<Compile Remove>`. A per-criterion classification of the traits shows:
  - **AC-606–AC-616:** zero compiled traited tests. All coverage comes from compile-removed files
    such as `ChildWorkflowAcceptanceTests.cs`, `RunChildTests.cs`, `RunChildrenTests.cs`,
    `ChildLineageTests.cs`, `ChildResidualPolicyTests.cs`, and `ChildCompensationTests.cs`.
  - **JS-AC-001–006 and 008–013:** zero compiled traited tests. Coverage comes from
    compile-removed `DagAcceptanceTests.cs`, `DurableDagRunnerTests.cs`, `DagBuilderTests.cs`,
    `RunExternalJobTests.cs`, `ExternalJobAcceptanceTests.cs`, `JobSchedulerStackIntegrationTests.cs`,
    and others. Only JS-AC-007 has one compiled test.
  - **AC-617 and AC-618** are waived.
- **Consequence:** the Section 8 criteria already look satisfied, several through tests of removed
  public surfaces. A Section 8 slice could finish with no compiled test carrying these criteria and
  the must-green lane would stay green. The gate's statement that AC-613 and AC-614 "must be
  executable before first-release exit" therefore has no enforcing check. This is the same class
  as EEE-1 (2026-09-20): credit from a non-behavioral source.
- **Owner mismatch:** the AC-618 waiver names "tasks 8.7 and 8.10". Task 8.6 owns the
  notification-driven `WaitForTerminalAsync`, and the map correctly says 8.6.

## 6. What a superseding freeze needs

1. Add document 14 to the map: JS-001–JS-010 and every JS-AC-001–JS-AC-018 with its owning Section 8
   task(s), consistent with the existing waivers or with corrected waivers.
2. Add DU-033 to the 8.5 row with the partition obligation. Record whether DAG child-start/join
   records are canonical "internal continuations" or need an approved amendment before 8.5.
3. Make acceptance evidence honest before Section 8 starts. Either:
   - have the catalog ignore compile-removed test sources, which the Section 7 crosswalk already
     classifies, and convert the affected criteria to explicit owner-task waivers like JS-AC-014–018;
     or
   - record this evidence baseline in the gate with a named task and point by which it will be
     fixed, before any slice claims those criteria.
4. Correct the AC-618 waiver owner to 8.6 (and 8.10).
5. Refresh the Task 8.0 map pin and ledger wording in the same target.

The documentation refreshes, Task 7.3 row 9, package decision, and scenario handoff can be
refrozen unchanged. The new freeze must also record this `REJECT` under the reshape review
convention.

## 7. Reviewer hygiene

`HEAD` is `4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`, with the eleven frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

## Determination

The canonical side of the map is exact and the documentation updates are correct. The gate must
also enumerate the numbered obligations Section 8 has to meet, including document 14 and DU-033.
It must not hand those criteria forward as already covered by compile-removed legacy tests.

**Verdict:** **REJECT**
