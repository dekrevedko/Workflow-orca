# Resize qualification

The two-pool test failed against the Phase E runtime at `9465475d` on PostgreSQL 17 and SQL Server 2022. Increasing X from one to two returned zero reserved units on both. Its new capacity and operation ID had committed while both waiters remained queued.

At the implementation checkpoint `bc62560`, each provider passed four resize cases. The disjoint-pool case checks both tickets immediately after resize, exact replay on a replacement host, and a single entry into the protected workflow body on its next driver turn. The contested cases cover a live head that still cannot fit, an owner checkpoint that closes after the resize reads it, and a release that wins the governance append. They check the winning tickets, generations and committed version deltas. The same provider runs passed all ten previous grant ownership and recovery cases and the deterministic 32-conflict test.

The [TLA+ model](../../../verification/tla/resource-pool-resize/README.md) explored all 300 distinct states in its normal profile. Nine controls produced the specified traces, including a permanently stranded idle queue under the former capacity-only write and unsafe promotion when either version fence is removed. These bounds do not measure database contention or background scheduling.

## Conflict exhaustion

The baseline investigation used the existing bounded retry policy. A competing real append forced each of 32 stale writes to fail. Each database preserved the pre-existing ticket, waiter and capacity through exhaustion, then applied the same operation once on a fresh host. In an explicit 36-request burst across six independently constructed hosts, PostgreSQL applied 36 and SQL Server applied 33. The three exhausted SQL Server operations succeeded after contention subsided. These are local Docker Desktop observations from one burst per provider. The backoff comparison is a separate experiment.
