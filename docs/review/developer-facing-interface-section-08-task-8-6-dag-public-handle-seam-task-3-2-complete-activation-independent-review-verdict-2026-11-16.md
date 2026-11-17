# Public DAG facade (Task 8.6 seam) Task 3.2 Complete activation independent review verdict

**Date:** 2026-11-16
**Reviewer:** independent review
**Scope reviewed:** the R1 34-entry freeze in `X:/ocv302` (branch `draft/admit-dag-public-handle-seam`) on
`94e9aca5632bd77d0c6ecde9d7fe689bcdee18d1`, named by
`developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-task-3-2-complete-activation-r1-reviewed-dirty-manifest-2026-11-16.txt`.

R1 is the author's revision of the original 29-entry target, which had projected tree `e959997f`. I reviewed the original
first, and the author revised it while my review was in progress. The R1 delta was then reviewed separately (see "R1
revision"). The combined target contains:
- the round-109 P1 fix (wall-clock polls) and the three P3 fixes;
- retirement of the live-byte candidate pins;
- promotion of the five facade registry rows to Complete;
- the Task 3.2 and reshape 8.6 checkboxes.

There are no product source changes.

**Authorization requested:** final review of the Complete activation.

**Post-hoc status.** While this review was running, the owner instructed the author's session to review, fix, checkpoint and
push. It committed R1 as `cd7d14906bba9d24cc878a2298bc265eaaf23061`, with parent `94e9aca5` and tree `4540a1de`, plus the
evidence child `6bd6d136588c50bdb410cf46727d06890820494a`. That child carries the author's own disposition, which discloses
that it is not independent. Both commits are pushed, and no PR exists yet.

I compared the checkpoint with my R1 replica, whose tree is `9156ca04`. It differs only in two files:
- `task-3-2-activation-review-validation-2026-11-16.json`, where the author attached the final infrastructure and strict
  OpenSpec counters;
- the active content digest in `review-manifest-provenance.json`.

Every code, guard, fixture, canonical and documentation byte I reviewed is the one committed.

This verdict is therefore the independent review of `cd7d1490` and its activation. It authorizes nothing further on its own.
It is not yet catalogued in history; that would take a follow-up commit (see "After this verdict"). It authorizes no other
Section 8 task, no DAG run (Task 3.1/3.2) completion and no archival.

**Reviewer disclosure (retained):** I drafted RV-1 to RV-4 of this contract. This verdict judges the fixes, the guard
retirement and the Complete evidence against the owner-approved RV-4 contract, whose five blocks and public signatures are
unchanged here.

## Summary

My first pass over the original 29-entry target found no blocking defect, but it missed a P2. That P2 is the author's R1
finding: two historical guards still compared the live working tree with old commits, so harmless future DAG source edits
would have failed CI. I reproduced it on the original target and confirmed the R1 fix (see "R1 revision"). The original
29-entry target should not be checkpointed.

With R1, the activation is faithful to the reviewed scope:
- Both real approval chains are bound by commit, tree, parent order, exact evidence-child paths and verdict bytes.
- The draft marker retires.
- The five canonical blocks are still required to equal the reviewed delta blocks.
- The strict current checks remain: canonical, API, runtime-view boundary and maintained documents.

Core.Tests is green: 390/390 on both the original and R1. On the original, the full CI unit list totals 1,149 tests. R1 changes only guard code. No blocking defect remains in the committed checkpoint. Two P3 items are recorded.

## What was checked

- **Chain.** Commit `1e960fc4` has parents `598c6d4` then `ba2b057d`, and tree `ff4711dc`. Its only child is `94e9aca5`, which
  changes exactly the round-109 verdict (`e9c5fedd`, byte-exact) and the two catalogue and freeze fixtures. Both are pushed,
  and HEAD is `94e9aca5`.
- **F1 (wall-clock polls).**
  - The process probe waits on a `Gate.Entered` barrier set by the child step.
  - The redundant snapshot poll before `childGate.Entered` is gone.
  - Lease contention in `DagFacadeWire` makes at most four acquires, three explicit `TimeProvider.System` waits of 5 s
    between them. That covers the provider's 5 s lease and 15 s maximum turn.
  - Any outcome other than `Queued` fails at once. A request still queued at the end is cancelled in `finally`, so it can't
    block later lessees.
  - Explicit `Task.Delay(…, TimeProvider.System, …)` is the repository's existing convention for deliberate waits. Ten
    earlier instances exist, including in the Durable and Hosting suites.
  - The Core guard is unchanged, and Core.Tests passes 390/390.
- **F2 (status text).** CLAUDE.md and both doc 17 status blocks name the real approvals. The runtime-view sentence again
  begins "The runtime-view contract…", and the Task 8.6 sentence follows it. The CLAUDE.md integration smoke filter now
  names the compiled `CurrentSurface` group.
- **F3 (child-binding rule).** Doc 17 now labels the child-binding exception as host-configuration behaviour outside the five
  seam requirements. No delta, canonical block or API changes.
- **F4 (guard retirement).**
  - `DagFacadeApprovedHistory` reads the retired candidate record, sources, documents and public API from the `1e960fc4`
    and `da1a4729` blobs instead of the working tree.
  - The nine integrated ephemeral files are verified as merge blobs equal to `ba2b057d`.
  - `ValidateShutdownStartReservationApprovals` now checks all three shutdown-synced documents and requires the live text to
    equal the merge text exactly. The early return for pre-sync text is gone, and ancestry is plain `HEAD`.
  - My probe Q1 reverse-applied only the shutdown hunk to doc 09 (see the table). The author's five probes also failed as
    intended (logs read).
- **Complete evidence.**
  - `dagFacadeCompleteEvidence` names both checkpoints, both evidence commits and the owner-authority artifact by SHA-256.
    The guard re-verifies both chains and requires tasks 0.2, 1.2, 1.3, 2.7 and 3.2, plus reshape 8.6, to be Complete.
  - The DAG run change's guard now leaves only 8.4 and 8.5 open. Task accounting moves from 132/27 to 133/26.
  - The owner-authority artifact quotes the instruction and repeats the contract-review exclusion.
  - The shape differs from the runtime-view precedent, which had a separate contract `ApprovedPending` record and a
    checkbox-only activation commit. That difference follows the owner's combined-delivery direction and is disclosed. It
    is not a defect.

## Validation (independent; replicas X:/ocv315 and X:/ocv316 hold the original target, X:/ocv317 holds R1)

| Lane | Result |
|---|---|
| Release build, `-warnaserror` | 0 warnings / 0 errors |
| Core · Ephemeral · Durable · Acceptance · Hosting · ProviderCertification | 390 · 414 · 113 · 37 · 79 · 116 = 1,149, all green |
| Integration.Tests · benchmark smoke | 11/11 · passed |
| PostgreSQL · SQL Server, every `DagFacade*` case (fault, restart, integrity, binding) | 5/5 · 5/5 |
| Infrastructure guards (absolute packed feed) | 319/319 |
| ExpectedRed | 14/14 still red; no credit |
| Probe Q1: doc 09 shutdown hunk reverse-applied | red in three independent guards: the strict shutdown check (live text differs from the merge text at line 597), the Task 7.3 row 17 pin, and the freeze manifest |

## Findings

## R1 revision

The R1 delta is two guard files, the two freeze fixtures and five new records:
- the R1 request and manifest;
- the findings-and-remediation note;
- its validation record;
- a binary patch preserving the original target.

There is no product, runtime-helper, API, canonical or delta change.

- `DagStreamIdentitySliceGuards.ValidateApprovedFingerprintAmendment` now diffs `baseCommit..1e960fc4` for both the canonical
  and the DAG source/API path checks. The untracked-file sweep is gone.
- `DagPumpSliceGuards` compares the merge blob, not the working file, with the Task 2.2 checkpoint bytes.
- Exact historical path sets, bytes and review chains are preserved.

My probes ran each of two harmless future edits on the original target and on R1, with the two affected guard classes
(7 tests) green on both at baseline:

| Probe | Original 29-entry target | R1 |
|---|---|---|
| New untracked file `src/OrcaCore.Dag/ReviewerProbeFile.cs` | red (`CanonicalTransitionApproval_BindsTheActualCheckpointAndVerdictAddition`) | green |
| Comment appended to `DagRunAppendV1.cs` | red (`Task22Candidate_BindsTheExactPumpSourceSetAndLeavesTheFullBridgeForTask23`) | green |

Whole-lane sweep on R1:
- **Clean run:** Release build 0/0, Core 390/390, Infrastructure 319/319, ExpectedRed 14 still red.
- **Harmless future edits across the DAG source tree:** I added new files in `OrcaCore.Dag`, `OrcaCore.Dag.Hosting` and
  `OrcaCore.Durable.Hosting/Dag`, and appended comments to `DurableDagFacade.cs`, `DagCoordinator.cs`, `DagRunAppendV1.cs`
  and `DurableDagValues.cs`.
  - Only two of 319 guards failed. One is the active-freeze manifest. The other is the repository-wide package-source record
    (`EveryManifestPackage_MatchesTheApprovedSourceRecord…`), which every source change must refresh in its own reviewed
    target.
  - No facade-specific live pin remains.

### F1 (P3): the Task 7.3 refresh departs from the letter of its refresh rule

`Task73PinRefreshDecision` allows a row refresh only "in the same frozen target that intentionally edits the source". This
target refreshes ten rows (1, 4, 6, 7, 10, 14, 17, 18, 19, 20), but edits only CLAUDE.md (row 1).

I verified that each of the other nine new hashes equals that document's bytes at the reviewed merge `1e960fc4`. The edits
were reviewed in rounds 108 and 109, and before this target they were reached through the retired live-byte adapters. The
refresh therefore consolidates reviewed bytes, which is what the rule's "no refresh for an earlier unreviewed edit" intent
protects.

Unlike every earlier section, the appended note doesn't list the rows or give that justification. Add one sentence at the
next touch.

### F2 (P3): dead helper

`EphemeralFailureSettlementReviewHistory.CurrentDocumentationHash` (`EphemeralFailureSettlementTransitionGuards.cs:135`) no
longer has any caller. Remove it at the next touch.

### Process note

The author's session committed R1 under the owner's instruction with only a self-review, while this independent review was
still pending. CLAUDE.md asks for no commit while an independent review is pending. The content turned out to be what I
approve, so nothing needs undoing, but the original 29-entry target would have shipped the R1 defect without that
self-review. Independent review and self-review each caught something here.

### Notes (no action)

- The history guards now require `b9dcdef7`, `598c6d4`, `1e960fc4` and `94e9aca5` to stay ancestors of HEAD. That matches the
  repository's merge-commit PR convention (#24, #25 and #26 are merges). The draft branch must therefore not be squashed or
  rebased into main.
- The main checkout still has uncommitted Task 3.1 certification edits that overlap `DagRunCertification.cs`, the provider
  test classes and `OpenSpecCorpusGuards.cs`. Expect a reviewed merge when either side lands.

## After this verdict

The checkpoint `cd7d1490` and evidence `6bd6d136` already exist and match the reviewed content. Nothing in them needs to
change.

If the owner wants this independent verdict in history before the PR to master, add it in one more commit on the draft
branch. That commit should add only this verdict and its immutable-history catalogue row, and the PR should then be opened
from that tip.

Merge the PR to master with a merge commit (not a squash or rebase) so the reviewed chains stay reachable for the history
guards. Archival of `admit-dag-public-handle-seam` and the DAG run change's open tasks remain separate.

**Verdict:** **APPROVE**
