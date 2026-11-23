# Durable output wait subscription cost

The scale review found that each pending caller removed and added its handler on the shared commit event every second. At 8,000 waiters the cost of changing the delegate list dominated an idle refresh. The fix in `0af8961042a79db78b80e920403d10f977672fcc` retains one handler for the life of each call. The deterministic regression was committed first at `74e2512f`.

## Steady-state measurements

An independent reviewer ran the same Release probe against `8706a3ca` and the corrected durable facade. Each run started N public output waits against one pending workflow on the in-memory provider. After all timers armed, the probe advanced the clock by one second and waited for all N timers to re-arm. It recorded elapsed wall time and process-wide allocated bytes for four ticks at each N. Setup and cancellation fell outside the timed region.

| Pending callers | Median time before | Median time after | Median MiB before | Median MiB after |
| ---: | ---: | ---: | ---: | ---: |
| 500 | 16 ms | 15.5 ms | 41.6 | 0.4 |
| 2,000 | 107.5 ms | 16 ms | 535.9 | 1.7 |
| 8,000 | 899 ms | 224 ms | 8,619.7 | 6.8 |

At 8,000 callers the four corrected ticks took 110–236 ms and allocated 6.8–7.5 MiB each. An isolated control that removed and added the same 8,000 multicast handlers once per round, without a workflow or provider, allocated a median 8,808.7 MiB over three rounds. The runs used Windows 11, a 32-logical-processor Ryzen 9 9950X and .NET 10.0.15. These measurements describe a synchronized in-memory workload. Database throughput, local commit fan-out, and the cost of admitting or releasing a burst of waiters require separate measurements.

## Regression coverage

The active `IdleRechecksKeepTheHostSubscriptionListStableWithThousandsOfWaiters` test registers 2,048 callers. Across two fully re-armed ticks it checks the multicast delegate's identity and counts one new projection read per caller per tick. It verifies cancellation removes all the handlers. The identity check fails on the first tick with the original implementation and avoids machine-dependent time or allocation thresholds. A second test delivers a local commit during a stale read on both initial registration and a timer-driven recheck.

An independent race probe held the first timer callback until a local hint had woken the wait and a second timer had armed. Delivering that old callback left the second signal pending. A new local commit completed the waiter immediately. The same probe failed when modified code let the old callback signal the current read.
