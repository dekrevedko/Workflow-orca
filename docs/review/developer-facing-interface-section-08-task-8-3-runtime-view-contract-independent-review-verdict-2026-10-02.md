# DAG hosting runtime-view contract independent review verdict

**Date:** 2026-10-02
**Reviewer:** independent review
**Scope reviewed:** the twenty-six-entry freeze on base `2a09b452af067fbd501215a572712ff08cf2bbc9`,
named by
`developer-facing-interface-section-08-task-8-3-runtime-view-contract-dirty-manifest-2026-10-02.txt`.
This is `admit-dag-hosting-runtime-view` task 1.1, the proposed runtime-view friend contract with
its document and ledger dispositions.
**Authorization requested:** checkpoint of this contract target (task 1.2). This verdict
authorizes nothing.

## Summary

Most of the contract is sound, and its bookkeeping reproduces exactly:

- **Freeze and chain.** All anchors and the simulated tree reproduce. The process chain
  `dc8095c` -> `0f4fafa` -> `2a09b45` is correct.
- **Signatures.** The three-type, fifteen-member signature block is identical in design section 6
  and the disposition artifact, and is source-pinned. I checked it against the current source:
  - `DagNodeRef` uses reference identity, so plan-local references work as dictionary keys;
  - `PlanToken` validation already exists;
  - the current `output is not TDependencyOutput` check is what rejects every null, and the
    documents disclose this honestly.
- **Codec ownership.** It is consistent everywhere: the durable bridge decodes before evaluation
  and owns normalization, fingerprints, commit, and child start.
- **Documents.** Every active document keeps the current eight friends and calls the ninth
  proposed. The Task 7.3 refresh, provenance, catalogs, and validation are all correct.

Two defects block approval:

- **RV-1:** the planned activation fails the must-green lane.
- **RV-2:** neither normative tree states the exact allowlist this contract makes exhaustive.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
twenty-six entries. Every script asserted its disposable location and that the main `HEAD` was
still `2a09b45`. Main was never modified, and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 26 lines, 1,926 B, `604d1d3d…286f` | identical; the union of diff and untracked paths is exactly 26; nothing staged |
| NUL-form porcelain | 1,926 B, `c6b5d148…6377` | identical |
| Semantic record: 22 rows, ordinal sort | 3,011 B, `ee4d5437…64e0` | identical with the stated recipe |
| Active record | 25 rows, 3,612 B, `64a4b50a…2862` | identical, equal to the fixture |
| All-file content record, 26 rows (reviewer) | — | 3,767 B, `e5399803d1830f38488ca32c9645c6d6bbc57eb4566e57acad9aea832579cf19` |
| Simulated checkpoint | tree `b77b09d5aae6e23e4225b897fa351ddc9463eb4b` | identical from a copied index and from a committed disposable copy; parent `2a09b45`; 4 A / 22 M; all blobs equal the raw bytes |

- **Process chain:**
  - `dc8095c` has tree `d8f7d84f`;
  - evidence `0f4fafa` is its only child: it adds the process verdict byte-exact (`bc72cd0e…`),
    catalogs it, and clears the freeze;
  - `2a09b45` checks only task 0.2.
- **Provenance (my own reproduction):**
  - record: 180 rows, 47,327 B, `e7c608ad…` (173 synchronized, 2 superseded, 2 pending, 3
    new-capability);
  - proposed block hashes: `48217f56…` and `7f637ce3…`;
  - canonical files, both older deltas, and `src/**` are unchanged.
- **Catalogs and pins:**
  - superseded catalog: 5 entries, `6991bed8…`;
  - current artifact: `524053f5…`;
  - contract artifact: `55271895…`;
  - signature block: `93e6490d…`, identical in the design and the artifact;
  - Task 8.0 map: `e610e218…`;
  - all 22 Task 7.3 rows match current bytes, with rows 1/6/7/12/14/18 refreshed, and the digest
    is `029a6e0c…`;
  - 427 baseline and 82 append-only history records are byte-exact.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 0 stale.

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
| **Planned activation:** task 1.2 checked on the committed state, freeze cleared as the evidence commit leaves it | **239/240: `CanonicalSynchronizationGate…` red**. The same state with 1.2 open is 240/240 |

## 3. Blocking findings

### RV-1 (P2): the instructed activation breaks the must-green lane

The new guard pins `admit-dag-hosting-runtime-view` task 1.2 as `Open`, in the same list as tasks
1.3–3.2. The request's sequencing then says: after approval, activate by checking task 1.2 only,
with no other refresh. That commit fails Infrastructure, as the last row of section 2 shows:
"post-gate task-state transitions require an explicit reviewed fixture refreeze". CI runs that lane
on every push. The author's control C5 ("close 1.2 early") passes for this reason: the guard
cannot tell an early close from the legitimate post-approval activation.

The process precedent avoided this: that target left its approval task 0.2 unpinned, so its
checkbox-only activation stayed green. **Fix:** leave task 1.2 unpinned in this target, or accept it
only together with real contract-approval evidence. Then pin 1.2 `Complete` in the 1.3/1.4 target,
bound to the contract verdict, checkpoint, and evidence.

### RV-2 (P2): the exact allowlist is not in either normative tree

The contract's purpose is an exhaustive, exact runtime-view allowlist, but the normative text never
states it:

- **Canonical deltas.** The proposed `developer-facing-surface` block names only
  `GetRuntimeView()`, `Nodes`, and `EvaluateMapping(...)`. It defers the other twelve members to
  "the get-only descriptor/result members specified by this change's exact contract". The
  `repository-foundation` block cites "the fifteen internal method/getter signatures in this
  change's contract".
  - Canonical sync copies these blocks verbatim into `openspec/specs/`, where "this change" has no
    referent.
  - After archival, the referent is a historical change artifact, not a normative source.
  - The authoring precedent named every allowed member in canonical and left only exact CLR
    signatures to guard pins.
- **Doc 17.** It says the contract "is specified in that change's design §6". It also puts that
  statement at the top of §17.2.1 (strong values and execution context), not in §17.2.6 (typed DAG
  planning), even though the disposition table claims §§17.2.6/17.3.
- **Doc 08.** CP-020 says the view is the "closed runtime view in doc 17", which doc 17 does not
  contain.

**Fix:**
1. Name all fifteen members in both delta blocks: the eight descriptor getters and four result
   getters, with their types where they matter.
2. Carry the proposed signature block in doc 17 §17.2.6, marked proposed, and move the §17.2.1
   paragraph there.
3. Point doc 08 at doc 17 §17.2.6.
4. Re-pin the proposed block hashes, the provenance record and artifact, and the signature and
   artifact pins.

## 4. Independent negative controls

| Control | Mutation | Result |
|---|---|---|
| P0 | unmodified: canonical gate / Infrastructure in the probe copy | green / the same 13 environment-only failures (no package feed in the probe copy); later rows compare against this set |
| P1 | task 1.2 checked (planned activation) | **red**: canonical gate; one new failure against control (RV-1) |
| P2 | mapper-delegate getter added to both signature blocks, artifact SHA repinned in source | **red**: independent signature-block pin |
| P6 | `CLAUDE.md` claims the ninth friend approved and compiled (freeze refreshed) | **red**: Task 7.3 row pin |
| P3 / P4 | doc 17 §17.2.1 / doc 08 CP-022 status flipped to approved (freeze refreshed) | green: unpinned once the freeze lifts (P3-4) |
| P5 | reshape 8.3 re-acquires fixed-codec round-trip (freeze refreshed) | green: unpinned (P3-4) |
| P7 | authoring task 3.2 prose names a wrong checkpoint (freeze refreshed) | green: prose is unpinned (informational) |
| R0 | restored | green; porcelain equals the manifest; the 26 target files equal main byte-for-byte |

## 5. Non-blocking observations (P3)

- **P3-1: the archival-resolution owner is contradictory.** Design §8 says active-or-archived
  predecessor resolution "requires its own reviewed target". Task 1.4, and request item 6, place it
  in the atomic 1.3/1.4 target. Choose one.
- **P3-2: two failure classes are undefined.**
  - "Ordinary" mapper exceptions are not defined: say which exceptions, if any, are not mapped to
    `DAG_INPUT_MAPPING_INVALID`, for example cancellation or fatal runtime exceptions.
  - The bridge's failure when it decodes a committed dependency output is unclassified. That
    belongs to the 8.4 contract, but name it there.
- **P3-3: the successful-null rule has no runtime owner.** The rule lands only in the
  package-boundary requirement. The canonical owner of `OutputOf` runtime semantics, durable-runtime
  "Durable DAG progression is runtime owned" ("otherwise invalid output"), and the acceptance
  requirement stay silent. This is not a contradiction. The disposition should state why no
  durable-runtime or quality delta is needed, or schedule one before source task 2.2.
- **P3-4: guard coverage is narrow after the checkpoint.** Once the freeze lifts, only these new
  statements are pinned: `CLAUDE.md` and the other Task 7.3 rows, three doc 17 substrings, the
  Task 8.0 map, and the signature and artifact pins. The doc 17 and doc 08 proposal-status
  sentences and the reshape 8.3–8.5 text can drift unnoticed (P3–P5). Doc 17 §17.5's
  "complete product-friend set" is also missing from the disposition table; record it as
  checked and unchanged until the friend is approved.
- **Recorded (no action):** `dag-contract-scenarios.json` still lists `fixed-codec-input-once` as
  `8.3,8.5`. This is disclosed, and harmless, because expected-red ownership uses only the final
  section number.

## 6. Reviewer hygiene and remediation path

`HEAD` is `2a09b452af067fbd501215a572712ff08cf2bbc9`, with the twenty-six frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

Do not checkpoint this freeze. Remediate RV-1 and RV-2, preferably with P3-1 to P3-3. Retain this
rejected manifest, request, and verdict byte-exact and catalog them. Then refreeze with a new dated
manifest and request, and state the simulated tree in the handoff again. Canonical sync, registry
promotion, the ninth friend, and Task 8.3 source remain blocked.

## Determination

The design is right: a narrow internal view, decoding on the bridge, no codec in Dag, and an
honestly disclosed null-policy gap. But the target as frozen would turn the must-green lane red at
its own approved activation. It would also synchronize a canonical requirement whose exact
allowlist points at "this change" instead of stating it.

**Verdict:** **REJECT**
