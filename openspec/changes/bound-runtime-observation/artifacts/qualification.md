# Durable output wait qualification

The independent source reviewer accepted commit `3e96c8cfe652f03d4f482b2515f59354033d22f3`, tree `31bb381b5a13e0d789f21e442cba7c5774515b69`. Its parent `c3e260b2` synchronized the five completion-wait requirements. The two-host regression was committed first at `5ceb13c8`.

## Cross-host and lifecycle results

The independent provider comparison used PostgreSQL 17-alpine and SQL Server 2022-latest. Each probe ran two provider roles in one OS process with independent connections and notification hubs. The observing runtime held a stale Pending read while the writer committed. An independent query from the observer confirmed output 42 or terminal status `Terminated` before the stale read was released. With no pump or local hint, all four baseline waits stayed asleep. On the reviewed source all four observed the expected output or typed terminal exception after one virtual second.

The controlled-clock tests cover a commit during a stale read, two local event deliveries during one read, remote success and termination without a local hint, purged projection, caller cancellation, and an injected provider exception. The waiter returns detached output, throws the existing typed terminal or missing-instance exception, or propagates the original provider error so the caller can retry. Generic host stopping and standalone service-provider disposal each left a pending wait asleep before the change and cancelled it afterward. The caller's own token remained uncancelled in both cases.

The eight observation cases passed in each of 20 fresh processes (160/160). The independent reviewer’s 18/18 scratch run repeated those eight cases and added four same-host/remote success and termination controls and six notification-delegate cleanup controls. Two separate independent controls held a noncooperative provider read open during caller cancellation or owner disposal. Cancellation reached the provider, 100 virtual seconds caused no further read or timer fire, and the wait settled when the provider returned.

## Read cost and latency

- Two pending callers made two initial projection reads. One clock interval added two reads. After one caller cancelled, the remaining caller made one read at the next interval and returned 42. Three further intervals added zero reads.
- Two local event deliveries during a blocked pending read caused one early recheck with zero timer fires. The independent reviewer counted four matching commit hints on this build. A later remote completion required one timer-driven recheck.
- A read held open for eight clock intervals incurred one in-flight read and no catch-up reads. The next refresh became due one second after that read returned Pending. Eight further intervals after cancellation added zero reads.

These counts measure logical projection reads. Reading a successful output additionally loads its checkpoint. Each pending waiter can schedule one timer-driven refresh per second of pending time. Local hints may cause earlier reads. Scheduler delay and provider latency can extend time to caller completion. Cancellation signals outstanding I/O, and an unresponsive provider can delay waiter cleanup.

## Verification

| Lane | Result |
| --- | --- |
| Release build, warnings as errors | 0 warnings, 0 errors |
| Core / Ephemeral / Durable / Hosting | 408 / 433 / 155 / 97 passed |
| Acceptance / in-memory provider / Integration | 37 / 116 / 11 passed |
| Benchmark smoke / external package consumer | Passed. All 12 packages stamped with the reviewed source restored into a fresh cache. The ephemeral typed workflow sample returned 42. |
| OpenSpec strict validation | 31 passed |
| Infrastructure / PostgreSQL / experimental SQL Server | 323 / 127 / 98 passed |

This qualification covers the missed remote workflow-output wake (F02). DAG observers still use their persisted versioned subscriptions. Append fan-out, notification backlog and cleanup under repeated commits remain separate measurement work.
