# DAG hosting runtime-view contract RV remediation independent review verdict

**Date:** 2026-10-02
**Reviewer:** independent review
**Scope reviewed:** the thirty-one-entry freeze on base `2a09b452af067fbd501215a572712ff08cf2bbc9`,
named by
`developer-facing-interface-section-08-task-8-3-runtime-view-contract-rv-remediation-dirty-manifest-2026-10-02.txt`.
This is the complete remediated `admit-dag-hosting-runtime-view` task 1.1 contract, including the
retained rejected packet, not only the repaired files.
**Authorization requested:** checkpoint of this contract target (task 1.2). This verdict does not
authorize:
- canonical synchronization or registry promotion;
- a `Dag -> Dag.Hosting` friend attribute or codec access for `OrcaCore.Dag`;
- Task 8.3 source or completion of reshape 8.4/8.5.

## Summary

Both blocking findings from the 2026-10-02 REJECT are fixed, and each fix holds up under adversarial
probing:

- **RV-1: activation.**
  - The guard no longer pins task 1.2. Task 1.1 is pinned complete, and tasks 1.3–3.2 stay pinned
    open.
  - I rehearsed the full chain on a committed copy: checkpoint, then the freeze cleared as the
    evidence commit leaves it (240/240), then task 1.2 checked with no guard edit (240/240).
  - Checking 1.3 together with 1.2 is still red.
- **RV-2: normative self-containment.**
  - Both proposed delta requirements now carry the identical exhaustive C# contract: three types
    and fifteen members.
  - Doc 17 carries the same block and the fifteen decoded signatures inside §17.2.6, between
    tagged markers. The misplaced §17.2.1 paragraph is gone.
  - §17.5 names the proposed ninth while keeping the current eight. CP-020 points at §17.2.6.
  - No delta or numbered document refers to "this change".
  - All five copies of the signature block hash to the same guard-pinned digest: the artifact,
    the design, both deltas, and doc 17.

The non-blocking observations are addressed:
- **Archival owner:** predecessor resolution is in 1.3/1.4, and actual archival is a separate target.
- **Failure policy:** mapper exceptions map to `DAG_INPUT_MAPPING_INVALID` except
  out-of-memory, stack-overflow, and access-violation exceptions. External cancellation keeps the
  runtime outcome. An ordinary bridge decode failure maps to `DAG_INPUT_MAPPING_INVALID` before the
  mapper runs or anything commits; protocol and storage failures stay runtime failures.
- **Null-rule ownership:** the rationale is in design §7.
- **Durable pins:** source pins now cover the doc 17 numbered block, §17.5, CP-020/CP-022, and the
  open reshape 8.3–8.5 text. Round 87's green status-flip probes are now red.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
thirty-one entries. Every script asserted its disposable location and that the main `HEAD` was
still `2a09b45`. Main was never modified, and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 31 lines, 2,560 B, `b365ebad…15ae` | identical; the union of diff and untracked paths is exactly 31; nothing staged |
| NUL-form porcelain | 2,560 B, `d412c446…95cb` | identical |
| Semantic record: 24 rows, ordinal sort | 3,386 B, `8e422828…0574` | identical with the stated recipe |
| Active record: 30 rows | pinned in the fixture | 4,599 B, `28857de5…31bc`, equal to the fixture |
| All-file content record, 31 rows (reviewer) | — | 4,754 B, `100a7f12389efca943d8e729f6fd0b6f008300be63e221b34f17a1de4166a5c2` |
| Simulated checkpoint | tree `5c9d63fe0b30ea4465054ac01c986a8b80707e62` | identical from a copied index and from a committed disposable copy; parent `2a09b45`; 9 A / 22 M; all blobs equal the raw bytes |

- **Rejected packet:** it is byte-exact and bound by a `rejectedFreezes` row:
  - request 11,267 B, `71b1999a…`;
  - manifest 1,926 B, `604d1d3d…`;
  - REJECT verdict 11,680 B, `1ddaa7a0…`.

  The original contract artifact (`55271895…`) and contract provenance artifact (`524053f5…`)
  are unchanged.
- **Provenance (my own reproduction):**
  - record: 180 rows, 47,327 B, `79f16383…` (173 synchronized, 2 superseded, 2 pending, 3
    new-capability);
  - superseded catalog: 6 entries, 1,041 B, `0eae8ac7…`, each artifact matching its pin;
  - current artifact: `515a968b…`;
  - canonical specs, both older deltas, and `src/**` are unchanged.
- **Pins:**
  - contract artifact `2dec007d…`;
  - doc 17 numbered block `b5924c44…`;
  - §17.5 boundary `e1c85582…`;
  - CP-020/CP-022 `e370dc75…`;
  - reshape 8.3–8.5 handoff `18fa9858…`;
  - signature digest `93e6490d…` in all five copies;
  - Task 8.0 map `e610e218…`;
  - all 22 Task 7.3 rows, with digest `029a6e0c…`;
  - 427 baseline and 85 append-only history records are byte-exact.
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
| Planned activation: task 1.2 checked on that state, no guard edit | Infrastructure 240/240 |

## 3. Independent negative controls

| Control | Mutation | Result |
|---|---|---|
| Q0 | unmodified: canonical gate | green |
| Q1 | task 1.2 checked only (the planned activation) | green, as intended |
| Q2 | tasks 1.2 and 1.3 checked | **red**: 1.3 must stay open |
| Q3 | getter type changed only in the delta's C# block, block hash repinned in fixture and source | **red**: independent signature digest |
| Q4 | doc 17 numbered block moved back into §17.2.1, bytes unchanged | **red**: §17.2.6 location check |
| Q5 | §17.5 proposed-ninth sentence cut (freeze refreshed) | **red**: §17.5 boundary pin |
| Q6 | CP-022 says the null clarification is approved and implemented (freeze refreshed) | **red**: CP-020/CP-022 pin (green in round 87) |
| Q7 | reshape 8.3 re-acquires fixed-codec round-trip (freeze refreshed) | **red**: handoff pin (green in round 87) |
| Q8 | doc 17 numbered status flipped to approved and compiled (freeze refreshed) | **red**: numbered-block pin (green in round 87) |
| Q10 | retained rejected request edited by one byte (freeze refreshed) | **red**: rejected-freeze row and immutable-record catalog |
| Q9 (informational) | superseded original contract artifact appended to (freeze refreshed) | green: unpinned (P3-1) |
| R0 | restored | green; porcelain equals the manifest; the 31 target files equal main byte-for-byte |

The probe copy's Infrastructure lane has 13 environment-only failures because it has no package
feed. Infrastructure rows count only new failures against that control set.

## 4. Non-blocking observations (P3)

- **P3-1: the superseded original contract artifact is unpinned.**
  `task-1-1-runtime-view-contract-2026-10-02.md` stays in the active change directory. It still
  says "State: Proposed; awaiting independent contract approval" and carries the rejected wording.
  The remediation artifact says it is kept byte-exact, but no guard or catalog binds it (Q9).
  Add it to a permanent superseded catalog, like the provenance artifacts, in the 1.3/1.4 target.
- **P3-2: runtime semantics live in the package-boundary requirement.** The successful-null rule,
  the mapper-exception classes, and the bridge decode-failure classification sit in
  `developer-facing-surface` and docs 17/08. The canonical runtime owner, durable-runtime
  "Durable DAG progression is runtime owned", stays unchanged.
  - Eager bridge decoding means a node can fail with `DAG_INPUT_MAPPING_INVALID` for an output its
    mapper never reads. That refines "mapper access to an otherwise invalid output".
  - Design §7's rationale is acceptable for a contract. The 1.3/1.4 consistency check and the 8.4
    bridge contract must confirm it, or schedule an owning delta, as design §7 already requires.
  - Stack-overflow and access-violation exceptions cannot be caught on .NET 10 anyway, so only the
    out-of-memory exclusion is testable in source task 2.2.
- **Recorded coupling (expected):** the new pins tie the guard to the current proposal wording and
  to reshape 8.3–8.5 staying open. The 1.3/1.4 target must refresh the doc 17 and doc 08
  status pins when the status changes, and the 8.3 source slice must refresh the handoff pin.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `2a09b452af067fbd501215a572712ff08cf2bbc9`, with the thirty-one frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 31 manifest paths from the live index;
- confirm the tree is `5c9d63fe0b30ea4465054ac01c986a8b80707e62` with parent `2a09b45`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze.

The activation should then check task 1.2 only; that rehearses green. The next target is the
atomic 1.3/1.4 sync and `ApprovedPending` transition. It must bind 1.2 to this verdict, its
checkpoint, and its evidence, and should include P3-1. No ninth friend, codec access, or Task 8.3
source may land before that target is approved and checkpointed.

## Determination

The runtime-view contract is now exact and self-contained in both normative trees. Its failure and
null policies are explicit and honestly marked as unimplemented, and its status statements survive
the end of the freeze. Its own approved activation keeps the must-green lane green.

**Verdict:** **APPROVE**
