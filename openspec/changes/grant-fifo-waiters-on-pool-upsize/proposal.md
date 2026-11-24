## Why

An oversized live waiter can head the partition FIFO while every pool is idle. Increasing its pool's capacity currently leaves both that waiter and younger requests parked until a later release. With no holder, a release may never happen.

## What Changes

On an upward management resize, apply the new capacity and grant the eligible FIFO prefix in one checkpoint-guarded governance append. Keep a live unsatisfied head in place, including when a younger request uses a different pool. The public management API and configured creation capacity stay the same.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `durable-runtime`: Grant the live FIFO prefix in the same governance commit as the capacity increase.

## Impact

The durable hosting aggregate owns promotion. SQL providers use the existing partition-version and owner-checkpoint append fences. Parked workflows can adopt their committed tickets on the next driver turn, including after restart. The separate retry investigation measures the existing conflict budget.

New resize receipts retain the applied pool snapshot from the successful commit. An exact replay returns that snapshot even if a later release, cancellation or resize changes the pool. Older v1 receipts retained only the pool, capacity and operation ID. The compatibility path returns those recorded operation facts with the latest pool snapshot and reads only the partition head. It cannot reconstruct historical accounting for these older receipts.

The workflow recovery scenario explicitly drives a parked owner on a replacement host after the resize. On that turn the existing driver adopts its tickets. Automatic scheduling of a parked owner after promotion is outside this change.
