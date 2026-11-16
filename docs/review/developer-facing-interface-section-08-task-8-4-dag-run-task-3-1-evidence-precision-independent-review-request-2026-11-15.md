# Independent review: DAG Task 3.1 evidence precision

Date: 2026-11-15. Main checkout, branch fix/dag-task-3-1-evidence-precision. Parent: 9de147ce6a430a0ffd581790f05785a82b917979.
Review the [eight-path manifest](developer-facing-interface-section-08-task-8-4-dag-run-task-3-1-evidence-precision-dirty-manifest-2026-11-15.txt) from the approved closeout evidence commit.

This narrow follow-up addresses F1, F2, and the decision-log note from the
[Task 2.4/3.1 closeout APPROVE](developer-facing-interface-section-08-task-8-4-dag-run-task-2-4-3-1-source-evidence-closeout-independent-review-verdict-2026-11-15.md).
The old checkpoint and verdict are unchanged. No new tests or implementation are claimed.

- F1: the [evidence inventory](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-2-4-3-1-source-evidence-closeout-2026-11-15.md) now credits expiry pruning but not unsubscribe
  (DG-P17), cancellation observed while the mapper is blocked but not post-release refusal
  (DG-P19), and a dependency-ordered chain on all providers but simultaneous-ready selection
  only on InMemory (DG-P22). Each production gap remains listed.
- F2: Task 2.4 credits five slices to date. Later slices require their own independent
  approval, checkpoint, and guard row.
- The stack decision log no longer claims the maintained historical status block was verbatim.
  The Task 7.3 row and artifact pin, the scoped status-block pin, and the evidence inventory
  pin are refreshed without changing guard behavior.

Corroborate the three claims from the cited tests and verify the scope/guard pin maintenance.
Do not repeat the full freeze/tree rehearsal or owner test lanes for this independent review.
Targeted guard validation and the final checkpoint drift check are owner responsibilities.

Task 3.1 and reshape Task 8.6 stay open. The registry remains ApprovedPending. No task
checkboxes, product source, canonical specs, deltas, older review records, or API baselines change.
Do not commit, activate, publish, or start Task 8.6 as part of this review.
