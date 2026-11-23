# Task 8.6 follow-up self-review

Date: 2026-11-16. This is a fresh self-review of each major step, not a new independent verdict. No subagent or separate contract reviewer is claimed.

## 1. Reviewed checkpoint and evidence child

Verified the supplied 130-entry raw manifest, raw active-content digest and copied-index tree before staging exactly those paths. Checkpoint b9dcdef7e611da45d7dc4f75946ee192fc83c37e has reviewed tree 8ef5842bb8c7952f30f0f4184e97baf45ba986c0 and parent da1a4729ea2cdb9474eb219eb507760574f83dcb. Its direct child 598c6d4fb61f7883e25f79f03001aa620dda0bf7 adds only the byte-exact implementation verdict and two catalogue/freeze fixtures. No activation occurred. Re-read the verdict: its author expressly excluded independent contract approval. Tasks 0.2/1.2/2.7 record that scope without inventing separate verdicts.

## 2. F1 behavior and adversarial check

Re-read the framing and value-validation boundaries. The new independent typed serializer must reproduce the original checkpoint byte-for-byte before introducing either an own-graph contract-key mismatch or malformed JSON with a valid value fingerprint. Assertions require the exact new InvalidDataException message. A real Ready node, an observed scheduler rejection, zero mapper calls and an unchanged raw tail replace the previous empty-plan mapping claim. Restoring the bytes allows that same node to complete. Separate direct mapping-reader and poison-reconciliation checks cover those internal boundaries.

Removing only the run-store owner input-integrity call makes both cases fail in all three lanes: Hosting catches missing poison-reconciliation rejection; PostgreSQL and SQL Server catch corrupt owners replaying without an exception. One production mutation, killed in three lanes / six cases; no survived, timeout, uncovered, compile-error or ignored mutation. The original source bytes were restored before clean rebuilds. The initial real-provider test timeout was a harness defect: the standalone engine's null logger factory hid scheduler evidence. AddLogging before engine registration fixes observation without changing product code or using timing as an assertion.

## 3. F2 integration and guard boundaries

The merge has first parent 598c6d4 and named pending second parent ba2b057d7898f516845e90323b4288fb0c1744d8. It is deliberately uncommitted for review. Resolved nine conflicts, combining both changes rather than selecting one side wholesale. A separate reconstruction verifies all canonical files equal main with exactly the five reviewed facade blocks overlaid; all three delta files remain byte-identical to the checkpoint. All nine incoming Ephemeral source files are byte-identical to main. All 158 preexisting immutable records retain their bytes. No unresolved merge entries remain.

The source guard accepts only those nine exact mainline files, with a named integration-parent ancestry check; it retains the fifteen-file facade scope and exact hashes. Pending MERGE_HEAD is accepted only when equal to that parent. Source, API, document and whole-canonical pins remain enforced. Provenance retains both parent histories in a 33-entry superseded catalogue and measures 224 requirement rows / 40 capability directories. The physical test inventory is 392 files / 1,646 declarations; the logical retired ledger is unchanged.

The first guard pass found an accidental Windows text-decoding error and an incomplete human provenance record. Restored UTF-8 text, made subsequent edits use explicit UTF-8 and supplied the exact measured claims. No assertion was weakened to accommodate those failures.

## 4. F3-F5 and scope

F3: missing or incompatible installed child configuration throws a public InvalidOperationException before effects; registration still installs the plan. Both cases are tested and no start key binds. Self-review narrowed the translation to the internal definition-unavailable exception; a missing returned snapshot remains InvalidDataException, preserving true storage-failure classification. Public signatures and the 3-type/17-member boundary are unchanged.

F4: doc 17 and design now explicitly describe fail-closed input-integrity errors across public reads, waits, cancellation, notifications, scheduler visits and internal poison reconciliation. A valid fault checkpoint remains a separate case. Repair of corrupt stored representation remains outside poison reconciliation.

F5: CLAUDE now says no mapper delegates are exposed, and the obsolete certification reflection comment is removed.

## Review boundary

Implementation approval is checkpointed. This merge and remediation candidate still needs independent review. The five registry rows remain Proposed and Task 3.2 remains pending; this self-review does not promote them or activate reshape 8.6. After approval, checkpoint this exact merge, catalogue its verdict in the direct evidence child, then bind Complete activation to that real evidence. No commit IDs or verdicts for future work are fabricated.
