# Public DAG facade (Task 8.6 seam) integration and remediation independent review verdict

**Date:** 2026-11-16
**Reviewer:** independent review
**Scope reviewed:** the 89-entry freeze in `X:/ocv302` (branch `draft/admit-dag-public-handle-seam`), named by
`developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-integration-remediation-dirty-manifest-2026-11-16.txt`.
It is an uncommitted two-parent merge. HEAD is `598c6d4fb61f7883e25f79f03001aa620dda0bf7` (the verdict child of the approved
implementation checkpoint `b9dcdef7`). MERGE_HEAD is `ba2b057d7898f516845e90323b4288fb0c1744d8` (main). The target adds:
- the resolved main merge (F2);
- canonical-frame corruption tests and a real-node scheduler leg (F1);
- a public child-binding error (F3), fail-closed wording (F4) and two wording fixes (F5);
- the Task 0.2, 1.2 and 2.7 records.

**Authorization requested:** review of the merge and the round-108 remediations before Complete promotion.

This verdict authorizes the two-parent merge checkpoint of this exact target and one direct-child evidence commit that adds
and catalogues this verdict and clears the freeze. It authorizes no Complete promotion, no Task 3.2 checkbox, no registry
transition and no reshape 8.6 activation.

**Reviewer disclosure (retained from round 108):** I drafted RV-1 to RV-4 of this contract in an earlier session. This verdict
judges the merge, the remediations and the evidence against the owner-approved RV-4 contract. The five delta blocks and all
public signatures are byte-unchanged in this target, so no contract text is re-opened.

## Summary

The merge is correct and complete, and the round-108 findings are resolved:
- The new integrity checks are now reached, including the replay owner.
- The real-node leg proves no mapping and no effect.
- The scope guards survive the merge without a broad exclusion.

No defect is introduced by this target.

One finding blocks Complete promotion, but not this checkpoint:
- **F1 (P1):** the Core.Tests repository guard fails. The cause is three wall-clock polling loops added by the approved
  implementation (`b9dcdef7`). Neither the author's lanes nor my round-108 validation ran that project.

Three P3 findings are recorded for the activation target.

## What was checked

- **Merge ancestry and completeness.**
  - The replica's raw porcelain is byte-identical to the target (89 rows), with MERGE_HEAD `ba2b057d`, merge base
    `da1a4729` and no unmerged entries.
  - All 65 paths that main changed since the merge base are present. 48 are byte-equal to main. The other 17 were also
    changed by this branch.
  - Of those 17, six equal a clean three-way merge exactly. Two differ only by remediation: the CLAUDE.md F5 line and the
    Durable.Hosting package-source hash.
  - The nine conflicted files combine both sides rather than picking one:
    - reshape `tasks.md` (task 7.20);
    - `DagPumpSliceGuards` (the integrated-source scope);
    - `DagStreamIdentitySliceGuards` (the recomputed canonical hash);
    - `EphemeralFailureSettlementTransitionGuards` and `OpenSpecCorpusGuards`:
      - the historical adapters chain the facade projection with main's shutdown-sync projection;
      - the provenance and superseded-catalogue pins grow from 30 to 33 entries, keeping both parents' rows;
      - owner filtering excludes the shutdown successor;
    - `openspec-provenance-checkpoint.json`;
    - `immutable-document-history.json`;
    - the declaration crosswalk and `RecoveryCrosswalkGuards`.
  - The accounting adds up:
    - files: 383 at the base, plus 8 from this branch, plus 1 from main, gives 392;
    - declarations: 1,615 plus 20 from this branch, 7 from main and 4 from the remediation gives 1,646;
    - the active counts 236 and 958 follow the same way.
  - `openspec/specs` differs from HEAD only by main's shutdown-sync block. The five delta files, `src/OrcaCore.Dag`,
    `src/OrcaCore.Dag.Hosting`, `src/OrcaCore.Abstractions`, the public-API fixtures and the runtime-view boundary guard are
    unchanged.
- **Scope guards (old F2).**
  - `DagFacadeReviewCandidate.IntegratedSourcePaths` requires `git diff da1a4729 ba2b057d -- src` to be exactly nine
    `OrcaCore.Engine.Ephemeral` files. Each must be byte-equal to main. That is the exact replacement for main's own
    `c81e16df` check in `DagPumpSliceGuards`.
  - The 15-file facade source scope and its hashes are unchanged.
  - Pending ancestry is accepted only when MERGE_HEAD equals `ba2b057d`. After the merge commit, `ba2b057d` must be an
    ancestor of HEAD.
  - `VerifyImplementationReview` pins the `b9dcdef7` tree and parent, the `598c6d4` child's exact three paths, and the
    verdict bytes.
  - The author's three scope probes failed for the stated reasons (log messages read).
- **Corruption tests reach the new code (old F1).**
  - `DagFacadeWire.CorruptRunInput` asserts that its re-serialized frame equals the stored checkpoint byte-for-byte before
    corrupting it. The older framing and value checks therefore pass, and the new checks are what fire.
  - The tests assert the exact messages from `VerifiedRunInput`. They cover reopen, replay, snapshot, wait and cancel, a
    direct mapping-reader call and poison reconciliation.
  - The real-node leg has a Ready node. A logger observer captures the scheduler's exact `InvalidDataException`. The
    mapper is called zero times and the raw tail is unchanged. Restoring the bytes lets the same node succeed with exactly
    one mapper call.
  - The author's mutation logs show the replay assertion is the one that fails on PostgreSQL and SQL Server when the
    store's owner check is removed. That is the RV-4 scenario "replay owner is corrupt".
  - My probes, both red:
    - **P1:** comparing the value key with its own recorded key fails the `key` case.
    - **P2:** removing the JSON parse fails the `json` case. The corrupt input then surfaces as
      `DurableDagDefinitionUnavailableException`, so the check is exactly what separates corruption from binding mismatch.
- **Child-binding error (old F3).**
  - A missing child, or a child with a different fingerprint or contract, now throws the internal unavailable exception
    before `FindExistingAsync`. The facade translates only that type into a public `InvalidOperationException`, so nothing
    is appended and no key is bound.
  - A missing returned snapshot stays `InvalidDataException`.
  - Doc 17 states that replay on such a host also throws, which matches the code order.
  - My probes, both red:
    - **P3:** removing the translation fails both cases.
    - **P4:** restoring `return Conflict()` fails the mismatch case with `InvalidDataException`.
- **Old F4 and F5, and task records.**
  - Doc 17 and the design state the fail-closed read scope and distinguish it from a valid `IntegrityFault` checkpoint.
  - CLAUDE.md now says no mapper delegates are exposed, and the reflection comment is gone.
  - Tasks 0.2, 1.2 and 2.7 record the real checkpoint and evidence. They state that the implementation verdict is not
    contract approval. Task 3.2 and the five Proposed rows stay pending.

## Validation (independent, disposable replicas X:/ocv313 and X:/ocv314)

The replicas were built from HEAD `598c6d4`, with the target's index, MERGE_HEAD and all 89 working files copied in.

| Lane | Result |
|---|---|
| Release build, `-warnaserror` | 0 warnings / 0 errors |
| Hosting · Durable · Ephemeral · Acceptance | 79 · 113 · 414 · 37, all green |
| ProviderCertification (InMemory) | 116/116 |
| PostgreSQL · SQL Server (Testcontainers) | 123/123 · 94/94 |
| Infrastructure guards (absolute packed feed) | 319/319 (53 min on a cold replica; the author recorded 3 min 49 s) |
| ExpectedRed | 14/14 still red; no credit |
| **Core.Tests** | **389/390: `RepositoryGuardTests.TestSources_DoNotUseWallClockTaskDelay` fails (F1)** |
| Benchmark smoke · Integration.Tests | passed · 11/11 (only the CurrentSurface group compiles in this tree, so the CLAUDE.md smoke filter matches nothing) |
| Package consumers | not re-run: the public API is byte-unchanged, and the author logged 9/9 |
| Probes P1–P4 | all red for the intended reason; baseline 4/4 green after restore |

## Findings

### F1 (P1, from `b9dcdef7`): Core.Tests is red because of three wall-clock polling loops

`RepositoryGuardTests.TestSources_DoNotUseWallClockTaskDelay` bans `Task.Delay(` in test sources unless the call uses a
`TimeProvider` or `Timeout.Infinite`. The guard is unchanged since the merge base. AwesomeAssertions prints only the first
offender, but there are three, all added by the approved implementation commit:
- `tests/OrcaCore.Dag.ProcessProbe/Program.cs:52`: polls the snapshot until node 0 is Running.
- `tests/OrcaCore.ProviderCertification/DagFacadeCertification.cs:146`: the same poll, immediately followed by
  `childGate.Entered`, which already proves admission.
- `tests/OrcaCore.TestSupport/DagFacadeWire.cs:27`: retries lease acquisition every 25 ms.

CI's unit job runs Core.Tests first, so it is red for both `b9dcdef7` and this merge. The author's recorded lanes and my
round-108 validation omitted that project. My round-108 statement that the race tests use barriers rather than timing was
therefore incomplete.

This target neither introduces nor worsens the failure, and the defect is already in committed, approved history. It
therefore does not block this checkpoint. It must be fixed, and Core.Tests re-run green, before Complete promotion or any PR.

Suggested fix:
- Drop the redundant poll at `DagFacadeCertification.cs:146`.
- Signal admission from the child gate in the process probe.
- Make the lease retry event-driven or explicitly bounded by provider lease expiry.

Do not exempt the files from the guard. Add Core.Tests, and the rest of CI's unit list, to the recorded validation lanes.

### F2 (P3): stale status text and one misattributed sentence

- CLAUDE.md lines 113–114 and both copies of the doc 17 current-status block (around lines 1279 and 2303) still say the final
  independent implementation review is pending. It is approved and checkpointed (`b9dcdef7`, `598c6d4`). The F5 edit
  touched that exact CLAUDE.md sentence.
- In CLAUDE.md the Task 8.6 sentences now sit between the runtime-view sentence and "Its contract is independently approved
  at `43d869fc`…". That makes the later sentence read as if it describes Task 8.6.

Fix both in the activation target.

### F3 (P3): the new doc 17 child-binding rule has no OpenSpec counterpart

The F4 text clarifies existing delta scenarios ("Stored run input is corrupt", "Start-key owner examined during replay is
corrupt"). The F3 rule is new normative text in doc 17 only: a public `InvalidOperationException` for a missing or
incompatible child binding, at start and at replay. Under the two-tree rule, record it before Complete in one of two ways:
- as an owner-approved scenario in the developer-facing-surface block, through the normal amendment path;
- as host-configuration behaviour outside the requirement set, stated in doc 17.

### F4 (P3): the candidate scaffolding must retire at Complete

`DagFacadeReviewCandidate` still compares the working tree with `da1a4729`: the exact 15 plus 9 source paths and the pinned
document bytes. The next source or pinned-document change will break it again, the same way F2 broke it.

The main checkout already has uncommitted Task 3.1 certification edits to `DagRunCertification.cs`, the provider
certification tests, `OpenSpecCorpusGuards.cs` and shared fixtures. Those will conflict on the next merge.

`BeforeShutdownStartReservationSync` now returns early for pre-sync text. Today the facade document pins and the
`Synchronized` classification backstop that. When the pins retire, that backstop disappears.

The Complete promotion should therefore convert these checks to commit-anchored ones (`b9dcdef7` and this merge checkpoint),
or remove them, and restore a strict after-state check for the three shutdown-synced paths.

## Checkpoint sequence (authorized)

1. Confirm zero drift against the 89-entry manifest.
2. Commit the merge with first parent `598c6d4` and second parent `ba2b057d`, staging exactly the manifest paths. The tree
   must equal the author's projected tree `ff4711dca44f7a56abd53bf0854c10332831e814`.
3. In its only direct child, add and catalogue this verdict and clear the active freeze. Make no activation.

The next target should combine:
- the F1 fix with a green Core.Tests run;
- the F2 to F4 items;
- the Task 3.2 Complete promotion and the five registry transitions, bound to the real checkpoint and evidence of both
  reviewed chains.

**Verdict:** **APPROVE**
