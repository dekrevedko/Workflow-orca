## Why

An oversized live waiter can head the partition FIFO while every pool is idle. Increasing its pool's capacity currently leaves both that waiter and younger requests parked until a later release. With no holder, a release may never happen.

## What changes

On an upward management resize, apply the new capacity and grant the eligible FIFO prefix in one checkpoint-guarded governance append. Keep a live unsatisfied head in place, including when a younger request uses a different pool. The public management API and configured creation capacity stay the same.

## Impact

The durable hosting aggregate owns promotion. SQL providers use the existing partition-version and owner-checkpoint append fences. Parked workflows can adopt their committed tickets on the next driver turn, including after restart. The separate retry investigation measures the existing conflict budget.
