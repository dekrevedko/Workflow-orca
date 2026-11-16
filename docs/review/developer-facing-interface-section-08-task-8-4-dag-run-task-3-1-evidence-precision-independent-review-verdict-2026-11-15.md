# DAG Task 3.1 evidence precision independent review verdict

**Date:** 2026-11-15
**Reviewer:** independent evidence-review agent (read-only)
**Scope:** the eight paths in
[the reviewed manifest](developer-facing-interface-section-08-task-8-4-dag-run-task-3-1-evidence-precision-dirty-manifest-2026-11-15.txt)
on parent `9de147ce6a430a0ffd581790f05785a82b917979`.
**Checkpoint binding:** `ff7e80e59590d5b8e1ed94e92d162e1cfb2875b1`, tree
`b11d3f7458240105c97078bc09e82a9dfc68438b`.

## Review agent’s returned verdict

**APPROVE** the final eight-path Task 3.1 evidence-precision target for checkpointing and recording the review verdict. No blocking findings remain.

- DG-P17 now credits only what the test checks: expiry pruning, retention of live claims and poisoned observer notices, and retention of retryable or poisoned child-start work. Unsubscribe and process-death recovery remain unqualified.
- DG-P19 distinguishes cancellation observed while the mapper is still blocked from late admission refusal after release, which the tests do not assert.
- DG-P22 distinguishes the shared dependency-ordered chain from the InMemory-only authored-order and receipt-invalidation tests. The one- and two-host SQL qualification gaps remain open.

F2 now limits Task 2.4 to the five slices reviewed to date and requires a separate approval, checkpoint, and guard row for later slices. The decision log accurately identifies its historical wording as refreshed. The scoped pins match; Git status matches the manifest, and nothing is staged.

This was a read-only source review. I did not run tests or repeat the freeze/tree rehearsal. Owner validation of the final bytes remains required. Task 3.1 and Task 8.6 stay open, and the registry stays `ApprovedPending`. This approval does not cover activation, publication, or follow-on implementation.

**Verdict:** **APPROVE**
