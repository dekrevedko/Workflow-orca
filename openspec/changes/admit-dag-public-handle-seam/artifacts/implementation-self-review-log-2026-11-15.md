# Implementation self-review log

These are separate review passes by the implementing agent, not an independent external APPROVE. Final independent review remains pending. The owner requested a self-review after every major step and one combined uncommitted delivery.

## Step 1: process gate

Reviewed the final guard diff and registry independently of the editing scripts. Compared the registry against HEAD after projecting only the five exact new rows and schema version: every pre-existing field is identical. Verified that all original review packets and archived draft artifacts retain their initial raw SHA-256, and that no product or canonical file changed at this step. The staged index is empty.

The eight negative-probe classes (including removal of each of five rows) reject unknown owners, an extra settlement owner, altered predecessor/proposed bytes, an unauthorized stage and premature canonical completion. Positive probes allow preparation checkboxes to change. Full Infrastructure passed 319/319, Release build had zero warnings/errors, and the two packet checks passed after the last process refreeze. See the task-0.1 validation record for commands and the frozen process target.

Finding resolved: three older transition guards also project the registry. They now verify and remove precisely the five new rows before their existing historical projections; none accepts an unverified registry extension. The process plan's narrower source-file estimate was therefore corrected in the validation record.

## Step 2: facade and durable boundary implementation

Reviewed the public constructors, result discriminants, registration/install sequence, owner replay, checkpoint validation, mapping admission, cancellation and observer loops against RV-4 after the first implementation build. This is a source self-review, with provider certification still in progress at this checkpoint.

Resolved before proceeding:

- The prototype's shared normalizer widened the losslessness rule to mapped input. Production has a separate run-input normalizer; the existing mapped-input path is unchanged.
- The prototype swallowed `InvalidDataException` in both compatibility checking and cancellation retry. Production validates recorded input against its own graph before installed-contract comparison, preserves corrupt representation errors, and uses an explicit internal fault cancellation outcome observed both before and inside the fenced turn.
- Replay now lives in the run store, reads the authoritative key owner on both first lookup and losing append, and retains a separate explicit run-id collision check. Submitted input is normalized once.
- The richer definition mismatch compares fingerprint values with `Equals`; `DefinitionFingerprint` is a reference type without an equality operator. Version and identity mismatch take precedence over the input-contract flag.
- Unknown persisted status/failure codes are rejected rather than projected as cancellation, dependency blockage or a generic message.
- The old catalog test classified foreign installed bindings as corruption. Its expectation now matches local unavailability; corrupt recorded keys have separate facade/replay/read regression cases.

Evidence at this checkpoint: the complete Debug solution built with zero warnings/errors. The compiled policy observes exactly three non-public DAG types, seventeen members, no reflection and no new friend. All four boundary guards passed, including compiled evasion probes; the linked mapping-only harness retains its fifteen-member limit. Initial public facade/scheduler/create tests passed 12/12. The expanded facade run passed 18 cases with one test assertion corrected from record reference equality to structural equality; the corrected focused binding run passed 12/12. Later full-lane results belong to the final validation record.

The fault/reconciliation regression now uses a provider-port subscription barrier. It holds after provider registration, permits both real commits, releases the recheck, and waits for the next subscription before asserting that the waiter continues. No elapsed-time assumption establishes that race.

## Step 3: real-provider and process-restart review

The full Hosting lane passed 74/74 and Durable engine passed 113/113. Both databases passed binding/losslessness and the four-process restart sequence (create/fault, incompatible reopen, reconcile/cancel/fault, inspect cancellation status). The source task now includes a test-only executable using public application/provider APIs; each phase runs in a different OS process.

The first PostgreSQL fault certification timed out after the child had completed. Investigation recorded the authoritative run, child events, append failures and pump observations: the child had terminal events, the run remained StartAuthorized, no DAG append was attempted and no join signal was dispatched. Source review found that PostgreSQL checkpoint read/write omitted both ParentInstanceId and RootInstanceId. Loading the child checkpoint therefore erased the DAG lineage required to emit its terminal join notice. SQL Server and in-memory preserved those fields.

Fixed the PostgreSQL checkpoint load/upsert and unreleased initial schema to persist both nullable identities. The existing checkpoint round-trip test now covers distinct parent/root values and an upsert that clears both. The owner explicitly permits greenfield contract/schema changes; no compatibility migration or production-data claim is made. The provider fault test additionally checks the child's checkpoint lineage before fault injection.

Fresh review checked read ordinals, insert/update columns, typed nullable UUID parameters and unchanged event/checkpoint transaction boundaries. The five PostgreSQL checks then passed: checkpoint round-trip, facade bindings/losslessness, level-triggered fault/barrier, real process restart and shipped scheduler. This substrate correction belongs to this combined target and must not be credited retroactively to the earlier Task 2.3 packet. SQL Server's focused fault/barrier check also passed; final full provider lanes remain to be recorded.

## Step 4: contract, provenance and assembled-candidate review

Reviewed source again by boundary and outcome, independently of the editing scripts. Confirmed typed throws remain in Dag, opaque values originate on the durable side, definition mismatch precedes run-input mismatch, start validation occurs before persistence, and immutable stored input is validated before mapper invocation. Reviewed PostgreSQL SQL/reader ordinals and transactional placement. No additional product defect remained from this pass.

Found and corrected current-document/catalog omissions: DEPENDENCY_BLOCKED was specified and emitted but absent from the global failure catalog; both new exception codes and the DAG package consumer now reflect the actual public facade. Reconciled stale draft-only task language with the owner's combined delivery direction. Kept review/commit/activation claims pending rather than fabricating evidence. Removed solution-file line-ending churn.

Reviewed the candidate projection as an evidence boundary: exact raw source/document/API pins are checked before historical bytes are used; the five Proposed registry rows remain immutable process history; current canonical operations use a distinct owner-authorized state. Updated only the new candidate's whole-file canonical record. Prior Complete evidence, historical API records and dated approval packets remain unchanged.

Executed actual mutation probes for unnamed ownership, row removal, source and requirement drift and premature approval. The dedicated whole-file check rejects edits outside the five owned blocks. Independent reconstruction confirms all fourteen canonical files equal parent plus exactly five blocks. The first broad consistency run identified four stale inventory/catalog/historical-scope checks; after bounded updates the 318 non-freeze Infrastructure checks all pass. Package consumers pass 9/9; full Hosting 75/75, Durable 113/113, PostgreSQL 121/121 and SQL Server 92/92. Clean Debug/Release and the standalone proof compile with zero warnings/errors. Fourteen intentional ExpectedRed obligations receive no forward passing credit.

These are separate self-review passes by the implementing agent, not an external independent APPROVE. The owner will review the final uncommitted target.

Final handoff check: the active manifest/content guard passed 1/1, giving 319/319 Infrastructure in total. Packed-package public API and source provenance passed 2/2 and strict OpenSpec passed 27/27. The last review removed stale provider-exit wording in the live design and clarified that storage, integrity and caller cancellation can still throw from start. The final content guard is rerun after recording this evidence.
