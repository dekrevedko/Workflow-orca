## Context

Durable output queries read the provider projection. The commit event that currently wakes the workflow facade belongs to one runtime. A different host can commit success or terminal failure without notifying that facade. A DAG run has its own persisted notification and reconciliation protocol. Ephemeral instances live in the observing runtime.

## Decisions

The durable facade subscribes before its first authoritative output read. A delivered local hint wakes the waiter immediately. After each completed pending read, the waiter arms a one-shot one-second timer through the runtime clock. The earlier hint or timer requests the next read. Multiple accumulated signals coalesce and reads run serially. Time spent executing a provider read does not accrue missed ticks. The interval bounds when the next read is due. Scheduler delay, provider latency and synchronous application projection work can extend observed completion time.

An available projection supplies the detached output. A terminal instance without output gives the existing typed exception. A missing instance projection gives the existing not-found exception. A checkpoint read failure after a successful projection read, including a deletion between those reads, and other query failures end the call and propagate so the caller can retry. Caller cancellation, host application stopping and service-provider disposal stop scheduling reads and signal any outstanding read. The waiter releases its timer and subscription as it exits. A noncooperative provider can delay that exit. Host cancellation requires the registered host lifetime. Direct DI owner disposal also releases a wait outside a running generic host.

Ephemeral output retains its operation pin and notification-only waiting. DAG terminal waiting retains its durable notification, authoritative recheck and typed integrity-fault behavior. Physical settlement remains a separate ephemeral operation. This change adds no provider requests on commit, new public option, notification rows, or schema migrations.

## Cost and limits

For N pending callers, registration starts N logical authoritative reads. Without hints there are at most N further logical reads per second of continuous idle waiting, apart from provider or scheduling delays. Local hints may prompt further reads. Requests coalesce while a read is in flight. Count reads per waiter with a controlled clock, including slow reads and bursts of hints. This bound is per waiter. The change claims no global cap, fixed physical SQL statement count, or bound on the existing DAG notification backlog.

Each durable output wait registers one handler on the host's shared commit event and removes it on exit. A refresh replaces only that waiter's signal. A local commit wakes the current signal. Each one-shot timer retains the signal for the read that armed it, so a delayed callback cannot wake a later read. A tick for N idle waiters still schedules N reads and allocates fresh per-waiter signals and timers. Registration and removal can copy the shared delegate list, and dispatching a local commit still visits every handler on the host. The [waiter scale measurements](artifacts/waiter-subscription-cost.md) quantify steady-state ticks against the in-memory provider.

Persisted subscriptions would add transaction work on workflow commits and require distributed cleanup. The bounded read closes the lost-wake defect without choosing a distributed subscription design. Subscriber counts, backlog and append cost still require measurement before changing DAG observer fan-out.

## Qualification

The regression uses two runtime instances sharing a store, establishes that the observer read Pending, commits on the other runtime and advances a controlled clock. Cover local registration races, suppressed and repeated hints, slow reads, cancellation, both shutdown paths, deletion, terminal failure and provider errors. Exercise real provider paths as available. Record read counts separately from measured provider cost.

The contract manifest records five full requirement replacements, their owner chains, and exact future numbered-document edits. The proposed stage retains the current canonical documents. After independent review, synchronize both normative trees and record the reviewed proposal commit and digest before closing tasks 1.1 and 1.2.
