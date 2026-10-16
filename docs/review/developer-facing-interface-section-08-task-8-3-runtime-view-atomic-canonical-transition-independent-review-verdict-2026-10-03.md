# DAG hosting runtime-view atomic canonical transition independent review verdict

**Date:** 2026-10-03
**Reviewer:** independent review
**Scope reviewed:** the twenty-five-entry freeze on base `64d97c644475f6dfbe183540bf546c7ab48eab46`,
named by
`developer-facing-interface-section-08-task-8-3-runtime-view-atomic-canonical-transition-dirty-manifest-2026-10-03.txt`.
This is `admit-dag-hosting-runtime-view` tasks 1.3/1.4: the canonical synchronization of the
approved runtime-view contract, together with its `ApprovedPending` registry transition.
**Authorization requested:** checkpoint of this atomic target only (task 1.4). This verdict does
not authorize:
- the ninth friend attribute or runtime-view source;
- codec access for `OrcaCore.Dag`, child-start authority, or public API growth;
- Task 8.3 source, 8.4/8.5 bridge approval, archival, or withdrawal.

## Summary

The transition is exact and preserves history:

- **Canonical.** Exactly two canonical blocks change, under their verbatim headings, and each is
  byte-equal to its approved, unchanged delta (`3a848931…` and `f0dbc156…`). Every preamble, the
  requirement order, all text outside the two blocks, and every other canonical file are
  byte-identical.
- **Registry.** Schema 7 keeps both `Proposed` arrays and all authoring `ApprovedPending`/`Complete`
  evidence as history. It adds two runtime-view `ApprovedPending` rows bound to:
  - the real contract checkpoint `43d869f` and its tree `5c9d63fe`;
  - the single-parent evidence commit `1ea7f44`, which actually adds the verdict;
  - the byte-exact terminal APPROVE (`5ff6b332…`);
  - completed tasks 1.2 and 1.3.
- **Supersession.** Four predecessor identities (reshape and authoring, for both headings) are
  superseded only at their old hashes and the newest canonical hashes, all pinned in guard source.
  Owners resolve through exactly one active or dated archived record, and a duplicate record fails
  closed.
- **Round 88's notes.**
  - P3-1 is resolved: both dated contract artifacts are permanently pinned.
  - P3-2 is resolved: the unchanged durable-runtime and workflow-authoring owner blocks are
    hash-pinned, and eager decoding of every successful direct resultful output, unused ones
    included, is stated with its handoff to 8.4 and 8.5.
- **Documents.** They now say the contract is approved and canonical, not compiled.
- **Scope.** No `src/**`, friend, package edge, codec, public API, delta, or test-declaration change.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
twenty-five entries. Every script asserted its disposable location and that the main `HEAD` was
still `64d97c6`. Main was never modified, and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 25 lines, 1,842 B, `dc96f1d0…2c9b` | identical; the union of diff and untracked paths is exactly 25; nothing staged |
| Semantic record: 21 rows, ordinal sort | 2,823 B, `e84da5f6…3493` | identical with the stated recipe |
| Active record: 24 rows | 3,459 B, `dd3bec1d…a18e` (handoff) | identical, equal to the fixture |
| All-file content record, 25 rows (reviewer) | — | 3,614 B, `995841fd9895cb65a14393b6e29c81d6c12e238a61cf80046b13c7b6a6093026` |
| Simulated checkpoint | tree `572d86f6801e36a0611227386939e4dcbb1ab885` (handoff) | identical from a copied index and from a committed disposable copy; parent `64d97c6`; 4 A / 21 M; all blobs equal the raw bytes |

- **Contract chain:**
  - `43d869f` has tree `5c9d63fe` and parent `2a09b45`;
  - `1ea7f44` is its only child and adds the round-88 verdict byte-exact (10,608 B, `5ff6b332…`);
  - `64d97c6` checks only task 1.2.
- **Provenance (my own reproduction, with the four registered supersessions):**
  - record: 180 rows, 47,347 B, `40d4d8c0…` (173 synchronized, 4 superseded, 0 pending, 3
    new-capability); semantic approval is eligible;
  - canonical inventory: 14 / 547 B / `7165dac4…`;
  - active directories: 20 / 1,645 B / `dfabdc30…`.
  - Every active delta, the whole authoring change, and `src/**` are unchanged. The fixture's
    preamble list was only re-serialized, with its values unchanged.
- **Catalogs and pins:**
  - superseded provenance catalog: 7 entries, 1,222 B, `a4c8f08e…`;
  - current artifact `612d4b9e…`; transition artifact `a2a261de…`;
  - both contract artifacts (`55271895…`, `2dec007d…`);
  - owner blocks: durable-runtime `11741eda…` and workflow-authoring `d1a5dc99…`;
  - doc 17 numbered block `31142f0f…`; §17.5 `1788d2e2…`; CP-020/CP-022 `468a2572…`;
  - reshape handoff `ec0492e7…`; Task 8.0 map `a41d3296…`;
  - signature digest `93e6490d…`;
  - all 22 Task 7.3 rows, with digest `281a9166…`;
  - 427 baseline and 88 append-only history records are byte-exact.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 106 matches, 0 stale.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / 8 green, exactly `dag-hosting` red |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 240/240 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / strict OpenSpec | 15/15 / 20/20 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 240 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 240/240 |
| Planned activation: task 1.4 checked on that state, no guard edit | Infrastructure 240/240 |

## 3. Independent negative controls

| Control | Mutation | Result |
|---|---|---|
| Q0 | unmodified: canonical gate | green |
| Q1 | task 1.4 checked only (the planned activation) | green, as intended |
| Q2 | source task 2.1 checked | **red**: source tasks stay open |
| Q3 | fixture-only fifth supersession for the durable-runtime owner | **red**: exactly four source-pinned predecessors |
| Q4 | synced canonical runtime-view block hand-edited by one word | **red**: supersession is bound to the newest approved canonical hash |
| Q5 | fixture and source evidence repointed to the retained REJECT verdict | **red**: an explicit terminal APPROVE is required |
| Q6 | authoring change duplicated as a dated archived record | **red**: "must have exactly one active or dated archived record" |
| Q7 | unchanged durable-runtime owner block edited by one word | **red**: owner-block pin |
| Q8 | superseded original contract artifact appended to | **red**: permanent contract catalog (round 88's P3-1 resolved) |
| Q9 | doc 17 numbered block says the ninth friend is compiled | **red**: numbered-block pin |
| R0 | restored | green; porcelain equals the manifest; the 25 target files equal main byte-for-byte |

## 4. Non-blocking observations (P3)

- **P3-1: status wording goes stale at checkpoint.** These status statements stop being accurate
  once this target is committed:
  - "canonical in the prepared 1.3/1.4 target", in `CLAUDE.md`, docs 03/08/10/17, Decision 22,
    the solution architecture, and the overview;
  - doc 17's "the atomic transition still requires independent review/checkpoint";
  - task 1.3's note that 1.4 "remains open".

  All of them are pinned, so refresh them in the next reviewed (source) target, not in the
  activation commit.
- **P3-2: misleading name.** The `supersededRuntimeViewContractArtifacts` list and its source
  twin include the approved RV-remediation contract (`2dec007d…`). The design and transition
  artifact also call both artifacts "permanent superseded decisions". The approved contract is a
  permanent historical record, not a superseded one. Rename the list, or note the distinction,
  in a later target.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `64d97c644475f6dfbe183540bf546c7ab48eab46`, with the twenty-five frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 25 manifest paths from the live index;
- confirm the tree is `572d86f6801e36a0611227386939e4dcbb1ab885` with parent `64d97c6`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze.

The activation should then check task 1.4 only; that rehearses green. The next target is runtime
source tasks 2.1–2.4. It covers the ninth friend attribute, the exact runtime view, the Hosting
metadata allowlist, and Hosting-level behavior, and it may also carry P3-1. It needs its own
independent review and checkpoint. The 8.4/8.5 bridge stays with its owning reshape slices.

## Determination

The approved runtime-view contract is now canonical in exactly the two reviewed blocks. Its
predecessors are superseded only by exact hashes, and its approval evidence is bound to the real
checkpoint, evidence, and verdict. Its unchanged runtime owners are pinned, and its own activation
keeps the must-green lane green. It compiles nothing and authorizes nothing beyond its checkpoint.

**Verdict:** **APPROVE**
