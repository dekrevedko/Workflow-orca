# Reshape Task 8.0 UUU-1/VVV-1 remediation independent review verdict

**Date:** 2026-09-26
**Reviewer:** independent review
**Scope reviewed:** the fifteen-entry freeze on base
`4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`, named by
`developer-facing-interface-section-08-task-8-0-uuu-vvv-remediation-dirty-manifest-2026-09-26.txt`.
**Authorization requested:** approval and checkpoint of the remediated Task 8.0 requirement gate
before any Section 8 source work. This verdict does not authorize it.

## Summary

The remediation fixes most of what the first verdict found:
- **Document 14:** every JS-001–JS-010 and JS-AC-001–JS-AC-018 is routed, and the guard proves
  that each row exists.
- **DU-033:** the 8.5 row now carries DU-033's one-outbox, disjoint-claim rule. I accept the
  interpretation that child-start/join handoffs are canonical internal continuations; section 4
  explains why.
- **Evidence baseline:** it is honest. It uses the guard-verified crosswalk classification, and my
  independent count agrees: JS-AC-007 has one active source, 23 criteria appear only in
  compile-removed files, and 7 have no trait.
- **AC-618:** the waiver now names 8.6 and 8.10.
- **Unchanged decisions:** the documentation, Task 7.3 row 9, the package decision, the scenario
  handoff, and all 25 canonical headings are still exact.
- **Validation:** every lane reproduces.

Two blocking defects remain:

- **WWW-1 (P2): the numbered handoff still leaves Section 8 criteria outside the gate.**
  - DR-AC-008 is document 16's typed-DAG driving criterion. Its phase gate is "DR-AC-008 and
    JS-AC-001…017". It remains waived as "DR-P3 … still open", with no owner task.
  - Existing waivers give AC-528's DAG clause to 8.6, although 8.7 owns `MaxConcurrentNodes`.
    They give AC-321 to 8.9, and AC-317 to 8.4, which is unrelated work.
  - None of these criteria appears in the map or in the 8.10 compiled-evidence set.
  - These waivers were already present in the first freeze, and I should have reported them then.
- **XXX-1 (P2): the 8.10 closure gate does not enforce what it claims.**
  - **Checkbox form:** the ledger's own parser counts `- [X] 8.10`, a double space, or
    indentation as complete, but the gate stays green for each form.
  - **Comments:** traits that exist only in `//` comments satisfy the gate.
  - **JS-AC-007:** its only "compiled" credit is on a resource-pool capacity test that exercises
    no watcher, ingress, or wait. The gate already counts it.

## Method

Validation and probes ran in two disposable detached worktrees holding the same fifteen entries.
Every probe script first asserted that it was inside its disposable copy and that the main `HEAD`
was still `4ef6253e`. Main was never modified, and the review created no ref in the reviewed
repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 15 lines, 1,361 B, `762e8e68…a4ee` | identical |
| Content record, excluding the request and history catalog | 13 rows, 2,074 B, `c0ef31d2…d5f0` | identical |
| Entries | 9 modified, 6 untracked, 0 staged | identical |
| Simulated checkpoint | tree `b865268787f3fc57d40cdffc4318ca9d59f915ee` | identical from a copy of the live index and from a committed disposable copy; path set equals the manifest (6 A / 9 M) |

- **Retained records:** the first freeze's manifest (953 B, `7e97b158…`), request (4,807 B,
  `11308cec…`), and REJECT verdict (11,528 B, `27a58e66…`) are byte-identical to what I reviewed and
  wrote. All three are cataloged in `appendOnlyRecords`, together with the new manifest and request
  (3,613 B, `f8570ab6…`, LF).
- **Registry:** 22 archived freezes and 16 entries are present, the harmonization `activeFreeze`
  stays `null`, and the history catalog names this manifest as its active freeze.
- **Pins:**
  - map `82c922e2…` equals its guard constant;
  - Task 7.3 digest `0fa63c70…` and row 9 `bab0317a…` are unchanged from the first freeze.
- **Headings:** all 25 distinct `(capability, heading)` pairs still exist verbatim.
- **Scope:** no `src/**`, canonical `openspec/specs/**`, or behavior change.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 226/226 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 failures |
| Strict OpenSpec | 18/18 |
| Reshape ledger | 130 complete / 29 open / 159 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. Guard probes

Each probe refreshed the mutable current-match pins after all of its edits, so only the Task 8.0
checks could fail. Edits to the map also re-pinned the map digest and rebuilt the guard.

| Probe | Mutation | Result |
|---|---|---|
| Q0 / Q0b | pins refreshed only (positive controls) | green |
| Q1 | `- [x] 8.10` without new evidence | **red**, "Task 8.10 cannot close" (lists only the first missing ID, `AC-606`) |
| Q2a / Q2b / Q2c | `- [X] 8.10`, `- [x]  8.10`, `  - [x] 8.10` | **gate green** each time; the ledger parser counts 8.10 complete (131) each time |
| Q3 | JS-AC-007 trait removed from its active source | **red**, baseline |
| Q4 | crosswalk flips `DagAcceptanceTests.cs` to `active` | **red**, baseline, and red in the crosswalk inventory guard |
| Q5 | crosswalk flips the JS-AC-007 source to `compile-excluded` | **red**, baseline |
| Q6 | map row edited without re-pinning | **red**, map digest |
| Q7 / Q8 | JS-AC-012 row / JS-004 row deleted, digest re-pinned | **red**, exact row sequence |
| Q9 | DU-033 interpretation sentence reworded, digest re-pinned | **red**, sentence check |
| Q10 | ledger's 8.10 obligation sentence removed | **red**, ledger check |
| Q11 | one compiled `AC-606` trait added while 8.10 is open | **red**, baseline ratchet |
| Q12 | 8.10 closed and 30 traits added to one existing pool test | green (the closure path is satisfiable); the Core waiver test goes red, as it should |
| Q13 | 8.10 closed and the same 30 traits present only as `//` comments | **gate green** |

The probe copy was restored after every mutation, and its porcelain matched the manifest at the
end.

## 4. The DU-033 interpretation

The interpretation is consistent across the canonical and numbered trees, so no amendment is
needed before 8.5:
- **Canonical:** the first-class record list and the outbox requirement have only two outbox
  families: internal continuations on a runtime pump, and external application events.
- **DU-031** already names "internal DAG child-start commands" as durable outbox records in the
  same commit boundary. That makes child-start an approved record kind under DU-033.
- **DR-037 and PR-015** route every kind other than `continue` and `workflow-event` to the internal
  dispatcher pump. That pump never reaches `IWorkflowEventDispatcher`.

The artifact's refusal to reuse the `Continue` discriminator agrees with this. The 8.5 row should
also cite DU-031 and DR-037 (P3), because they settle which internal pump claims child-start
records.

## 5. WWW-1 (P2): Section 8 criteria still outside the gate

The first verdict gave existing waivers as evidence that the map was incomplete. The same test
applies to four more cataloged criteria:

- **DR-AC-008**
  - What it requires: a diamond including a resultless prerequisite, driven to completion with
    closed statuses, authored-order snapshots, and cancellation, with no public child node and no
    manual pumping (DR-041).
  - Document 16 is mapped to `durable-runtime` in the source-map crosswalk. Its DR-P4 phase gate is
    "DR-AC-008 and JS-AC-001…017".
  - Current waiver: "DR-P3 full DAG driving without manual pumping is still open." It names no
    owner, and the phase is wrong.
- **AC-528**
  - Its DAG clause ("DAG `MaxConcurrentNodes` remains separate and counts a started nonterminal
    child even while that child is parked") is task 8.7 work.
  - The waiver names "tasks 5.7 and 8.6". This is the same wrong-owner error that AC-618 had.
- **AC-321**
  - Create-or-observe identity is the companion's JS-004 obligation.
  - The waiver names task 8.9, but the map omits it.
- **AC-317**
  - It covers in-memory durable hosting diagnostics.
  - The waiver names task 8.4, which is the internal child-start protocol and not that work. The
    waiver is stale, or the map is missing an obligation.

**Consequence:**
- None of these four IDs is in the 31-criterion compiled set.
- Section 8 could complete, and the closure gate could pass, while all four stay waived with
  "still open" or wrong-owner text.
- The waiver test forces removal only when a trait appears, so nothing else forces them.

## 6. XXX-1 (P2): the 8.10 closure gate is bypassable and textual

- **Checkbox form.**
  - The gate tests the exact pattern `^- \[x\] 8\.10\b`.
  - `TaskAccountingGuards` counts `^\s*-\s+\[[ xX]\]\s+<id>` and treats `[X]` as complete.
  - Q2a–Q2c close 8.10 in the ledger's own terms while the gate stays green.
  - The request's statement that "marking 8.10 complete now must turn the infrastructure gate red"
    is therefore false for ledger-equivalent forms.
- **Comments.**
  - The trait regex matches raw file text, so commented-out attributes count as "compiled
    trait-bearing" evidence (Q13).
  - The artifact says the gate "blocks an 8.10 completion with any missing compiled trait". A
    comment is not a compiled trait.
- **JS-AC-007.**
  - Its sole active credit is `ResourcePoolStoreCertificationTests.AcquireAsync_WhenPoolHasCapacity_GrantsTicketAndReducesAvailableCapacity`.
    That is a pool-capacity grant test with no Job watcher, durable ingress, `EventId` retention,
    or wait.
  - The artifact says this trait "does not by itself prove the complete Job watcher journey". In
    fact it proves none of it, and the closure gate already accepts it.
  - This is the same non-behavioral-credit class as VVV-1, now inside the new baseline.

## 7. Non-blocking observations (P3)

- **JS-AC-018:** the criterion table omits 8.9, although the 8.9 row claims JS-AC-018 and its
  token-correlated reconciliation is companion work. The table's 8.7 has no evident role.
- **JS-003 / JS-AC-005:** the structural-fingerprint conflict is returned by registration. That is
  8.6's cast-free registration helpers and its `typed-registration-start-reopen` scenario, but the
  table lists 8.2/8.3/8.5 and 8.3.
- **Failure message:** Q1's message names only the first missing ID. Joining the full list into the
  message would match the request's description.

## 8. What a superseding freeze needs

1. Route DR-AC-008 (with DR-041), AC-528's DAG clause (8.7), and AC-321 (8.9) in the map.
   - Rewrite their waivers as owner-task "pending" waivers, and add them to the 8.10 compiled set.
   - Correct AC-317's waiver to its real owner, or map the Section 8 obligation it claims.
   - Enumerate every waiver naming an 8.x task, so that none remains outside the gate.
2. Harden the closure gate:
   - detect 8.10 completion with the ledger parser's own semantics;
   - credit only trait attributes outside comments;
   - add probes for `[X]`, spacing, indentation, and commented traits.
3. Stop the gate from counting the misapplied JS-AC-007 trait as closure credit. Either:
   - remove or retag it, adjusting the baseline; or
   - pin that coordinate as non-credit.

   Then state plainly that it proves no part of JS-AC-007.
4. Refresh the map pin, ledger wording, and fixtures, and record this `REJECT` under the reshape
   convention.

The document 14 mapping, the DU-033 interpretation, the AC-618 waiver, the compile-aware baseline,
the documentation, and the package and scenario decisions can be refrozen unchanged. Only the
additions above need review.

## 9. Reviewer hygiene

`HEAD` is `4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`, with the fifteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

## Determination

The remediation makes the map honest about document 14, DU-033, and the compile-removed evidence
debt, and its interpretation of internal child-start records is sound. The gate still misses four
cataloged Section 8 criteria. Its new closure check can also be satisfied without compiled
evidence, so it cannot yet be the enforcing mechanism the target describes.

**Verdict:** **REJECT**
