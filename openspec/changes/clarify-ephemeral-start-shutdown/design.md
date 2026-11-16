## Shutdown admission

Admission closes independently of in-flight reservations and execution lanes.
Each start acquires host ownership before fingerprinting. A caller waiting for
the same key rechecks admission before replay, conflict, or a new attempt.
An initialized owner remains eligible to publish Accepted during Closed.

## Synchronization

Preserve the original shutdown and issue 16 checkpoints. Bind the one proposed
successor to the current issue 16 block and exact numbered replacement text.
Review registration first, then review the wording and synchronize the
capability, MG-066 and AC-546 as one documentation change. The merged runtime
and its regression tests supply the implementation evidence.
