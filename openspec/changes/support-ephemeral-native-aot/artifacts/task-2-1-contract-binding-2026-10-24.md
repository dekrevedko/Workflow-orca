# Issue #13 contract binding and canonical transition — 2026-10-24

User approved checkpointing the exact R1 contract and starting task 2.
Checkpoint 6122025828c7e8e3525ad8f2e24048024cbbb2d5 has sole parent
3b7f98aa0748a4d9c3f54d4604e566ed61f21bad and tree
2b981a667a0c08aca7215118e73f0e40b91db8ac. Its sole direct verdict child
10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd adds only the R1 verdict, raw SHA-256
4389efe8e8d58366e6064ba6cbd6d5d55a5fcf77d111d21759cdbedf3b61a554.
Guards resolve those relationships and exact bytes through Git, failing closed.

Seven canonical blocks append the exact approved R1 deltas across four existing
capabilities. NF-034 and AC-563–574 own the twelve groups and nine R1 subcases.
Status is ContractApprovedCanonicalSynchronizedSourcePending: source/native
approval is null, and the coherent task-2 implementation review remains 2.5.
Contract tasks 1.3/1.4 are complete; task-2 boxes require their own completed work.
The public declaration companion and executable baselines are queued with
source task 2.2; no API/baseline advance occurs before the source exists.

Schema 20 adds only nativeSupportContractEvidence/nativeSupportPendingAmendments
to the existing registry. All prior records remain data-equal. The earlier
shutdown guard ignores only these explicitly owned extension keys when checking
its historical schema, and the new native-support guard validates their complete
scope. The earlier assessment guard checks its four blocks against its committed
approved canonical snapshot and requires that immutable prefix in the live
canonical spec; the new guard checks the exact appended successor. Neither is a
generic waiver for later changes. No source/behavior credit or publication occurs.
