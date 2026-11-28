## Context

The approved [RC-3 run contract, section 6](../admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-3-remediation-2026-10-05.md) requires an addressed notice per crossed append. This amendment supersedes that creation and per-version deduplication rule for provider-generated addressed notices. An observer waits for change and reads the committed head on delivery.

## Decisions

Serialize subscription registration and append on the run. Use the cursor and expiry actually persisted when the append takes ownership. Store a trusted wake version outside the payload only for generated addressed records. The natural key of a live wake is the run, boot recipient and subscription. Pending, Retryable and Claimed are live, including a claim eligible for expiry and reclaim.

| Situation at append | Durable result |
| --- | --- |
| Eligible subscription, no live generated row | Insert a pending notice for the committed version. Its record ID is derived from run, subscription and initial version. |
| Live wake version exceeds accepted cursor | Preserve the entire row, including dispatch attempts and claim expiry. |
| Live wake version is at or below accepted cursor | Keep the ID and last attempt, replace the payload/version, set Pending and clear the lease. The next claim increments the attempt. |
| Subscription expired or absent | Generate no addressed notice for this subscription. Apply existing observer maintenance and poison retention rules. |

Validate candidate identities even when coalescing would skip an insert. Exact replay of an already committed operation returns its receipt. Caller writes retain ordinary identity and payload checks. An explicit identity colliding with a generated candidate or retained generated row aborts the transaction. A replacement's ID continues to identify the slot through delivery.

PostgreSQL locks live outbox rows and prospective IDs in stable order before deciding whether to retain a claim. A partial unique index enforces one live generated wake per registration. SQL Server serializes its state document with the run transaction. The in-memory store prepares the whole outbox update under its provider lock before publishing the new run state. SQL Server and PostgreSQL use fresh first-create schemas so older unmarked rows cannot masquerade as caller records after upgrade.

If delivery settles a useful claim first, a new append inserts a fresh notice. If append retains that claim first, settlement still succeeds. Old boot notices can be settled without consuming a new registration's notice. The sink re-reads the head even if the payload refers to an earlier version.

## Alternatives and cost

Keeping only the latest version invalidates useful claims on busy runs. A separate watermark table adds delivery and recovery machinery. Reusing the current outbox keeps the existing pump, subscribe-before-recheck and trusted routing boundaries. Append work still scales with eligible observers. The bound applies to live generated records per registration, while terminal evidence and explicit records follow their existing retention policies.
