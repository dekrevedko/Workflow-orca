# Durable continuation lease expiry

When a worker loses a continuation lease, DR-034 allows a retry or lease expiry. Once a replacement worker commits progress and marks the signal dispatched, what can the original worker still change?

Start with one runnable stream position and one pending `continue` record. Worker A holds the first claim until its lease expires. Worker B reclaims the same record, commits the position, and marks the record dispatched in a separate operation. Interleave one delayed call by A to `MarkAsync(Retryable)` or `ReleaseAsync` on either side of B's disposition. Permit one more claim to measure the effect on polling. Keep claim attempt, record state, and committed workflow progress separate.

Compare the current ID-only provider operations with a hypothetical disposition guarded by the acquired claim attempt. Check whether a live replacement claim survives stale writes, whether dispatch is irreversible in these schedules, and whether a second append at the same stream version can commit. Exercise the assumptions with witnesses for each late call and a fault control for the append guard.

Use deterministic claim timestamps and the shared provider fixture to replay both orderings in memory and on PostgreSQL and SQL Server. Inspect record identity, dispatch attempt, state, and stream history. Distinguish a provider-level append experiment from execution of a business step. This increment characterizes the existing protocol. Changing the provider contract or adding a claim token is a separate decision.

## Adopt claim-attempt fencing

Every outbox pump must retain the positive dispatch attempt returned with each claimed record. Marking, releasing and recording poison detail must compare that attempt and the current `Claimed` state in the same provider write. A mismatch leaves the record, lease and diagnostics intact. Expiry makes a record eligible for reclaim. Successful reclaim increments the attempt and fences the earlier worker, even when its call arrives before the replacement has committed workflow progress.

Require the attempt at the provider port so a caller cannot accidentally select an ID-only write. Upgrade pumps and providers together and drain older writers before sharing the store. Keep the existing persisted attempt format and stream-version append check. Verify both replacement-claim and terminal-state protection, valid-owner dispositions and actual pump behavior across the provider fixtures. Use the previous unfenced behavior as a negative model control.

Return whether the conditional write applied. A DAG ready pump can acknowledge its accepted wake only when its own dispatch mark succeeds. Pump completion counts also distinguish a successful disposition from a superseded attempt.
