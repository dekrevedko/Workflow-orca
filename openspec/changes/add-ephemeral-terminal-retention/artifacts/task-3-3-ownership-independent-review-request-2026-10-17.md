# Issue #1 task 3.3 — combined green ownership target review

Review tasks 3.1 and 3.2 together, uncommitted on issue/1-ephemeral-retention,
parent `ef88957af4c41bf18def689ba81c71e1a1d18696`. The adjacent self-inclusive
`task-3-3-ownership-dirty-manifest-2026-10-17.txt` defines the entire target.
The external freeze-before/after receipts bind its exact raw bytes and candidate
tree. The ordinary index remains empty and no task checkbox has been ticked.

Start with the carried 3.1 verdict and preserved fourteen-file archive/inventory.
The approved red tree was `aed8ba3fea6a66e59f0a1cc92e90291385ac5333` and must never
be checkpointed alone. Compare the complete candidate against that tree, using
the saved external diff or reconstructing its blobs from the archive if needed.
Read the implementation evidence, complete PhysicalOwnershipTests diff, added
callback/telemetry tests and mutation/validation records.

Check explicitly:

1. Every edit to PhysicalOwnershipTests beyond trait promotion is enumerated and
   justified. Every original assertion, case and control remains intact. The
   settlement barrier resolves the post-user-task races without hiding missing fences.
2. Actual physical root/branch/item and capacity-release work stays protected
   across timeout, cancellation, termination, retry and late completion, including
   unthrottled work. Pending retry, resource and timer callbacks own their dispatch
   and settlement. No callback executes under the lifetime lock.
3. Acceptance-versus-abort election, owning-lane caller-independent fencing,
   immutable prior terminals, original exceptions, publication and eager aborted
   cleanup work in both internal settings modes. Accepted instances cannot abort.
4. Runtime, facade/start, router accepted entries and timers release together;
   stale saves/completions/continuations cannot resurrect a retired unit. There
   is no count/age policy, sweeper, new public API or friend in this target.
5. Review the replacement DAG-owned scope explicitly. It rejects independent
   DAG edits while preserving all fingerprint/API/canonical and approval checks.
   Review the exact new source inventory, reasoned pending acceptance-coverage
   entries and sole changed package-source digest. No future baselines move early.
6. The original 52 expected-red cases now pass, all controls stay green, CI's
   unfiltered owning project and all remaining unit projects pass, and the full
   Infrastructure lane passes on the exact staged short-path target. Distinguish
   behavior-assertion mutations from the separately labeled slot timeout control.
7. Verify the binding chain, durable 3.1 archive/verdict, exact manifest and no
   drift. Both registry records and source approval remain unchanged. Check every
   carried P3 disposition rather than assuming completion from a template.

This requests independent ownership-slice approval before the green combined
checkpoint at 3.3. It does not authorize source activation, checkbox edits, master
integration, task 4.1, a push or issue changes. Leave the new review verdict out
of this frozen manifest for the later authorized checkpoint/evidence sequence.
