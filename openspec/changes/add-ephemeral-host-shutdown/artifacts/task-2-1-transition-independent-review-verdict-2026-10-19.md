# Issue #2 task 2.1 canonical transition — independent review verdict

**Date:** 2026-10-19
**Target:** base `4bd7c3e`, 46 entries, tree `0f4ad1fbb68b87fb3aee5af428b8da4d8dd60b18`, porcelain
SHA-256 `f2ffa5f4…`. The tree reproduced and nothing drifted during review.

## Verdict: APPROVE

There are no P1, P2 or P3 findings.

**Authorized now:**
1. Stage exactly the 46 manifest paths, excluding this verdict. Confirm tree `0f4ad1fb…` with parent
   `4bd7c3e`, then commit.
2. Add a verdict-only child commit.
3. Then build **one** green source-plus-consumer target covering all of Section 3 (3.1–3.5) and the
   registry/checkbox binding:
   - Move both shutdown records to canonical-approved, naming this checkpoint, its child and this
     verdict.
   - Tick 2.1 and 2.2.
   - Do not create a separate activation target, and do not commit an expected-red state.
4. Send lean evidence:
   - base, tree, porcelain and content hashes;
   - OpenSpec, Release `-warnaserror`, Infrastructure;
   - CI's six unfiltered unit projects;
   - 30 or more consecutive unfiltered ephemeral `dotnet test` runs under load;
   - engine coverage;
   - the 3.4 consumer results for all three drain recipes and Incomplete ownership.

That review also covers the PR text. Pushing and opening the PR follow its approval.

## What I verified

**Chain.**
- `3c8d33c` has sole parent `fa88986` and the approved tree `b1611b09…`.
- `4bd7c3e` adds only the R1 verdict (`fdac6ecd…`).
- Schema 17 pins both through git.

**F2.** The election now runs at Stopping. A start that was initialized before close proceeds and may
return `Accepted` while Closed. Queued pre-Init, new and replayed starts still fail as host-stopping.
Only starts still unaccepted at Stopping are fenced, and they keep their authoritative original
outcome and physical ownership. The scenarios cover both sides. The other four blocks and the seven
declarations are unchanged.

**Canonical.** All six blocks appear exactly once in `openspec/specs/` and are byte-equal to the
deltas. The sync is append-only: no existing canonical line changed.

**Guards and registry.**
- The DAG guard asserts an exact composition (prior + issue #1 + issue #2 blocks). It is not
  loosened.
- Two `ContractApprovedCanonicalReviewPending` records are added, with source and canonical approval
  still null. Older records are unchanged.

**Numbered owners.** MG-066–069, PR-041, NF-032, OB-027 and AC-543–554 are present. Doc 17 carries the
signatures. The guides keep the "implementation pending" status, the three drain recipes and the
pre-Stopping diagnostic guidance.

**Lanes.** I ran these in a disposable copy, `E:/ocv206`, as a simulated commit on `4bd7c3e`. The copy
has since been removed.

| Lane | Result |
|---|---|
| OpenSpec strict | 23/23 |
| Release `-warnaserror` | 0 warnings, 0 errors |
| Exact feed, then Infrastructure | 253/253 |
