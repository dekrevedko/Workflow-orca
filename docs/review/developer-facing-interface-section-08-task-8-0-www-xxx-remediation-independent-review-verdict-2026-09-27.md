# Reshape Task 8.0 WWW-1/XXX-1 remediation independent review verdict

**Date:** 2026-09-27
**Reviewer:** independent review
**Scope reviewed:** the nineteen-entry freeze on base
`4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`, named by
`developer-facing-interface-section-08-task-8-0-www-xxx-remediation-dirty-manifest-2026-09-26.txt`.
**Authorization requested:** approval and checkpoint of the Task 8.0 Section 8 requirement gate.
This verdict authorizes that checkpoint for the exact frozen target only. It does not authorize
any Task 8.1–8.10 source work before the checkpoint exists.

## Summary

Both blocking findings from the second review are fixed, and the fixes hold under mutation:
- **WWW-1:** the map now names an owner and a compiled exit for four more criteria:
  - DR-AC-008, through DR-041: 8.4–8.6 implementation, 8.10 compiled exit;
  - AC-528's DAG clause: 8.7, then 8.10;
  - AC-321: 8.9, then 8.10.

  AC-317 moves to an explicit open 9.1 obligation. Its stale 8.4 attribution is removed, and
  DU-001 confirms the diagnostic really is still owed. Every waiver that names an 8.x task is
  enumerated, and the guard pins that set exactly.
- **XXX-1:**
  - **Checkbox forms:** the 8.10 closure check recognizes every ledger-equivalent form. Any form
    it cannot parse fails closed.
  - **Diagnostic:** a failure names all 34 missing criteria.
  - **Decoys:** traits in comments or string literals no longer count, and an in-process negative
    control keeps that property load-bearing.
  - **JS-AC-007:** the unrelated pool-test trait is removed.
- **Baseline:** the new 34-criterion baseline is exact. My independent count gives 0 active,
  24 compile-excluded only, and 10 with no trait.
- **Earlier notes:** the P3 carry-forward items are fixed, and the 8.5 row now cites DU-031 and
  DR-037.
- **Unchanged decisions:** the rest of the target is as I reviewed it before, including all 25
  canonical headings.

No P0–P2 findings. The P3 observations in section 5 do not block this checkpoint.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the
same nineteen entries. Every script first asserted that it was inside its disposable copy and that
the main `HEAD` was still `4ef6253e`. Main was never modified, and the review created no ref in
the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 19 lines, 1,798 B, `1f131746…039d` | identical |
| Content record, excluding the request and history catalog | 17 rows, 2,793 B, `a233956f…88ac` | identical |
| Entries | 10 modified, 9 untracked, 0 staged | identical |
| Simulated checkpoint | tree `62f9cb861f0de68bd71da2482ada708d43d86ed2` | identical from a copy of the live index and from a committed disposable copy; path set equals the manifest (9 A / 10 M) |

- **Retained records:** both earlier freezes are byte-exact and cataloged in `appendOnlyRecords`,
  together with this freeze's manifest and request (4,111 B, `a729a39a…`, LF):
  - first freeze: manifest `7e97b158…`, request `11308cec…`, REJECT verdict `27a58e66…`;
  - second freeze: manifest `762e8e68…`, request `f8570ab6…`, REJECT verdict `bffa6db8…`.
- **Registry:** the history catalog names this manifest as its active freeze. The harmonization
  `activeFreeze` stays `null`, with 22 archived freezes and 16 entries.
- **Pins:** map `fc9693f8…` equals its guard constant. Task 7.3 digest `0fa63c70…`, row 9
  `bab0317a…`, and the documentation bytes are unchanged from the reviewed freezes.
- **Scope:**
  - the only non-guard test edit is the removal of the misapplied `JS-AC-007` trait from
    `ResourcePoolStoreCertificationTests`;
  - there is no `src/**`, canonical `openspec/specs/**`, or behavior change.

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

## 3. Semantic checks

- **Numbered handoff.**
  - Document 16 is mapped to `durable-runtime` in the crosswalk. DR-041 and DR-AC-008 now sit in
    the 8.5 and 8.10 rows, and the waiver names the DR-P4 phase.
  - AC-528's parked-child clause is 8.7's.
  - AC-321 is the companion's JS-004 create-or-observe identity, owned by 8.9.
- **Waiver sweep.** No waiver in the catalog names an 8.x task outside the ten the guard pins:
  AC-321, AC-528, AC-617, AC-618, DR-AC-008, and JS-AC-014–JS-AC-018.
- **AC-317.**
  - DU-001 requires a startup diagnostic outside development/test use.
  - Nothing in `src/**` consumes `DurableProviderRole.IsDevelopmentOnly`, so that diagnostic is
    genuinely unimplemented.
  - The artifact states this accurately, and 9.1 now owns it explicitly.
- **DU-033 interpretation.** It is unchanged and sound:
  - DU-031 names internal DAG child-start commands as outbox records;
  - DR-037 gives non-`continue` internal kinds to the internal dispatcher pump.
- **Previous carry-forward notes.** JS-AC-018 now lists 8.9 and drops 8.7. JS-003 and JS-AC-005
  include 8.6.

## 4. Guard probes

Each probe refreshed the mutable current-match pins after all of its edits. Map edits also
re-pointed the map digest and rebuilt the guard. Where a probe closed 8.10, the ledger parser was
checked separately: it counted 8.10 as complete (131) every time.

| Probe | Mutation | Result |
|---|---|---|
| Q0 | pins refreshed only (positive control) | green |
| Q1 | `- [x] 8.10` without new evidence | **red**; names all 34 missing IDs |
| Q2a–Q2d | `[X]`, double space, indentation, tabs | **red**, same diagnostic |
| Q2e | a non-breaking space after the dash | **red**: no parseable 8.10 row, so it fails closed |
| Q2f | a second 8.10 row | **red**: exactly one row required |
| Q3a / Q3b / Q3c | 8.10 closed with all 34 traits only in `//` comments / one `/* */` block / regular, verbatim, and raw strings | **red** each time |
| Q3d | 8.10 closed with all 34 traits inside `#if NEVER_DEFINED … #endif` | green (P3-1) |
| Q3e | 8.10 closed with traits only in combined `[Fact, Trait(…)]` lists | red: not recognized, so the error is conservative (P3-2) |
| Q4 | the JS-AC-007 pool trait restored while 8.10 is open | **red**, baseline |
| Q5 | 8.10 closed with 34 real attributes on an active test (closure positive control) | green; the Core waiver test goes red, as it should |
| Q6a–Q6c | AC-528, DR-AC-008, and AC-317 waivers reverted to their old owners or text | **red**, owner fragment |
| Q6d | an unrelated AC-531 waiver made to name 8.7 | **red**, pinned waiver set |
| Q6e | the DR-AC-008 waiver deleted | **red** |
| Q7 | map edited without re-pinning | **red**, map digest |
| Q8a–Q8c | DR-AC-008 / AC-321 / AC-317 owner row removed, digest re-pinned | **red**, one-row check |
| Q8d / Q8e | JS-AC-018 row reverted / DR-037 sentence reworded, digest re-pinned | **red** |
| Q9 | the lexer's code-position filter disabled in the guard | **red**: the in-process negative control fires |

The probe copy was restored after every mutation. Both restored lanes were green, and its
porcelain matched the manifest at the end.

## 5. Non-blocking observations (P3)

- **P3-1: preprocessor-disabled regions still count.**
  - Traits inside an inactive `#if` block satisfy the closure check (Q3d), although that code is
    not compiled.
  - No trait-bearing test source uses `#if` today; the only user is an out-of-band compile
    fixture. The check is also explicitly necessary-not-sufficient.
  - Task 8.10 already owns the compile-aware catalog. That work should treat inactive preprocessor
    regions as non-code, or reject `#if` in trait-bearing sources.
- **P3-2: combined attribute lists are not recognized** (Q3e).
  - This fails closed, not open.
  - Section 8 tests should use standalone `[Trait("AC", …)]` attributes, or 8.10 should widen the
    extractor.
- **P3-3: AC-317 puts product-source work in a samples task.** Task 9.1 now carries a DU-001
  runtime diagnostic, and its review should treat it as source work.

## 6. Reviewer hygiene and checkpoint instructions

`HEAD` is `4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`, with the nineteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 19 manifest paths from the live index;
- confirm the tree is `62f9cb861f0de68bd71da2482ada708d43d86ed2` with parent `4ef6253e`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact;
- catalog it in `appendOnlyRecords`;
- follow reshape's own evidence and activation convention for Task 8.0.

Task 8.1 may begin only after that checkpoint exists.

## Determination

The Task 8.0 gate now hands Section 8 a complete, owned set of 34 numbered exit criteria. It records
honestly that none has genuine compiled evidence yet. It cannot be closed through the ledger forms,
comments, strings, or misapplied credit that the previous reviews found.

**Verdict:** **APPROVE**
