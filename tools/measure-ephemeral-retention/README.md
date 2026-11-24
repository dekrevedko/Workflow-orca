# Ephemeral terminal retention measurements

**Keep the eligibility index for the measured steady workload.** At 10,000 entries in the terminal-heavy mix, a complete admission, ownership acquisition and release averaged 654 µs on the baseline and 0.59 µs on the final index. The baseline selection lock alone would require about 32.65% of one second at 500 releases/s in the terminal-heavy mix, and 13.16% in the pin-heavy mix. The indexed whole cycle bounds modeled gate demand at 0.04% or less for every enabled 10,000-entry mix. Pin acquisition itself became slightly more expensive at this size, with P95 moving from 0.20 µs to 0.40–0.50 µs. Both disabled controls performed zero retention selections.

**Simultaneous expiry can still hold up admission.** Expiring 80,000 eligible terminals together held the shared gate for 64–71 ms on the final index. The baseline observations were 38 and 112 ms, so this comparison leaves the direction of the full-wave difference unresolved. With one younger terminal surviving, the matched 79,999-victim holds were 55–64 ms before and 28–41 ms after. In a separate independent two-process rerun, the full wave took 36.58–39.39 ms on the baseline and 69.77–70.53 ms on the final index. The final index can therefore hold admission longer than the scan on synchronized full expiry. That rerun measured the 79,999-victim wave at 40.17–54.53 ms before and 30.71–30.81 ms after. Both revisions retired the survivor on the following tick. Two observations of each synchronous wave cannot establish a production burst percentile or acceptable deployment pause.

The first index at `fa652685` deleted each victim separately. Its original full-wave selection holds of 101–108 ms motivated the dense-expiry change. A diagnostic replay of the final fixture on that intermediate revision recorded 95–103 ms for all 80,000 due and 61–74 ms for 79,999 due. That replay overlapped a verification process, so its timings provide directional evidence. The published comparison below uses the original scan and the final density-aware index.

## Workload

The comparison uses the production ephemeral host lifetime, instance lifetime, ownership leases, retention policy and shared admission lock. It repeats the workload that identified F10, with fresh measurements at both revisions. The active-heavy population contains 90% active, 5% eligible and 5% pinned settled terminals. The terminal-heavy population uses 10/80/10, and the pin-heavy population uses 10/10/80. The disabled-policy control has the same lifetimes as the terminal-heavy mix and tracks zero for retention.

Each caller admits and disposes a host start lease. It then acquires and releases either work on an active instance or the first query pin on an eligible terminal. Callers alternate between these cases and use separate instances. The pin round trip includes removing and restoring the terminal’s membership in the index. Each population runs 12 warm-up cycles, 48 measured cycles with one caller, then 96 with four dedicated callers. Each schedule runs twice in fresh Release processes. These component timings exclude full workflow execution, public query materialization, engine and facade map removal, and user callbacks. The bulk-expiry removal callback increments a counter.

The deployment assumed for F10 has 10,000 tracked instances and 500 ownership releases per second, with a 10% serialized-service budget and a 1 ms P95 selection hold. This is an illustrative budget for evaluating this workload. We have no observed customer arrival rates. At 500 cycles/s, 200 µs of mean gate service would consume the 10% budget.

## Measurement boundaries

The selection hold timer starts inside the main selection lock and stops before its release. First-pin removal, last-unpin restoration, initial stamping, other short lock acquisitions and physical cleanup can all occur outside that timed section. The single-caller whole cycle runs from before host admission through acquisition and release, so its elapsed time is an upper bound on **all** serialized service the cycle requires. Multiplying its mean by 500/s gives the modeled upper bound in the table. The displayed percentage is rounded upward to the next 0.01%. This calculation assumes the same steady mix, one release per cycle and comparable service times. It excludes expiry bursts. The four-caller test measures saturation with no think time. Its throughput includes the selection timer hooks, sample collection and worker dispatch. Stopwatch quantization, scheduling and GC affect short measurements.

Tables pool per-operation samples from two independent process runs using nearest-rank P95/P99. P95 of a solo pin acquisition has 48 observations; an all-operation four-caller percentile has 192. Throughput divides total completed cycles by total scheduled duration across the two processes. Durations use the frequency recorded in each file. The paired values and counts refer to baseline `070d2ffbc4fdb873661a856bbdd07b236c3eeed4` and indexed `770a2a8742b2c2d9abd23e7e3d35bf004ee5a8e7`. Each arrow goes from baseline to indexed. The two simultaneous-expiry values per population are reported as a range.

## Results

| Entries | Mix | One-caller cycle mean / P95, µs | One-caller pin acquisition P95, µs | Cycles/s, one / four callers | Four-caller cycle P95, µs |
| ---: | --- | --- | --- | --- | --- |
| 1,000 | active | 10.74 / 13.70 → 0.66 / 3.30 | 0.10 → 0.40 | 82,368 / 77,878 → 427,427 / 257,269 | 171.00 → 67.90 |
| 1,000 | terminal | 74.98 / 122.90 → 0.61 / 2.60 | 0.30 → 0.40 | 13,255 / 16,572 → 1,158,022 / 472,906 | 106.60 → 17.50 |
| 1,000 | pinned | 23.07 / 26.00 → 0.66 / 2.80 | 0.10 → 0.40 | 42,943 / 43,067 → 1,163,636 / 526,027 | 50.70 → 18.10 |
| 1,000 | disabled | 0.32 / 1.30 → 0.37 / 1.30 | 0.10 → 0.10 | 2,046,908 / 1,167,883 → 1,675,393 / 1,302,578 | 5.20 → 5.70 |
| 10,000 | active | 103.16 / 117.70 → 0.64 / 3.20 | 0.20 → 0.40 | 9,654 / 9,885 → 1,198,502 / 471,744 | 140.80 → 20.70 |
| 10,000 | terminal | 653.86 / 679.90 → 0.59 / 1.10 | 0.20 → 0.50 | 1,528 / 1,459 → 1,144,219 / 465,906 | 4,108.30 → 16.70 |
| 10,000 | pinned | 264.06 / 381.30 → 0.56 / 1.00 | 0.20 → 0.50 | 3,780 / 4,373 → 1,175,031 / 529,655 | 306.60 → 14.50 |
| 10,000 | disabled | 0.28 / 0.60 → 0.26 / 0.60 | 0.10 → 0.10 | 1,955,193 / 1,092,150 → 2,082,430 / 1,060,773 | 5.80 → 6.70 |
| 100,000 | active | 2,006.48 / 2,862.10 → 1.01 / 4.20 | 0.50 → 0.90 | 498 / 669 → 804,694 / 583,942 | 3,007.40 → 15.30 |
| 100,000 | terminal | 10,563.64 / 15,467.50 → 0.66 / 2.70 | 0.90 → 0.40 | 95 / 77 → 1,097,143 / 513,644 | 166,085.90 → 14.90 |
| 100,000 | pinned | 10,665.26 / 16,799.90 → 0.66 / 2.90 | 0.90 → 0.50 | 94 / 110 → 1,107,266 / 382,394 | 160,644.90 → 18.70 |
| 100,000 | disabled | 0.32 / 0.80 → 0.29 / 0.90 | 0.10 → 0.10 | 1,509,434 / 1,043,478 → 1,787,709 / 1,009,995 | 4.50 → 5.90 |

| 10,000 entries | One-caller selection hold mean / P95, µs | Active work / unpin release P95, µs | Four-caller admission P99, µs | Modeled gate demand at 500 cycles/s |
| --- | --- | --- | --- | --- |
| active | 102.64 / 117.10 → 0.05 / 0.10 | 120.80 / 105.70 → 0.30 / 0.40 | 7,374.00 → 6.50 | ≤5.16% → ≤0.04% |
| terminal | 652.99 / 676.80 → 0.06 / 0.10 | 678.50 / 670.40 → 0.20 / 0.50 | 47,539.60 → 13.40 | ≤32.70% → ≤0.03% |
| pinned | 263.19 / 380.30 → 0.07 / 0.10 | 381.00 / 377.40 → 0.30 / 0.40 | 15,333.00 → 5.90 | ≤13.21% → ≤0.03% |
| disabled | 0.00 / 0.00 → 0.00 / 0.00 | 0.10 / 0.30 → 0.10 / 0.30 | 4.20 → 4.10 | ≤0.02% → ≤0.02% |

| 100,000 entries | Expired | Selection hold range, ms | Whole cycle range, ms |
| --- | ---: | --- | --- |
| active | 5,000 | 6.19–7.45 → 2.02–2.12 | 6.30–7.68 → 2.39–2.43 |
| terminal | 80,000 | 38.03–112.37 → 64.21–70.87 | 40.49–117.18 → 68.68–75.28 |
| pinned | 10,000 | 10.42–10.94 → 2.79–3.45 | 10.76–11.33 → 3.33–4.22 |
| disabled | 0 | 0.00–0.00 → 0.00–0.00 | 0.00–0.00 → 0.00–0.00 |
| expire-all-but-one | 79,999 | 55.10–63.74 → 28.39–41.36 | 58.41–67.54 → 32.65–45.57 |
| expire-last | 1 | 1.43–1.43 → 0.00–0.01 | 1.44–1.44 → 0.01–0.01 |

## Work counters and integrity controls

The separate count build instruments traversal and mutation counts. Traversal counters serialized in timing-only expiry rows have no instrumentation and carry zero. On the baseline, each enabled release visits every tracked entry. It checks eligibility for every stamped terminal and extracts a sort key, checks age and considers retirement once per eligible terminal. On the indexed build the counters cover age interval checks, yielded expired keys, retirement elections, eligible-membership adds, removal attempts and successful removals, wholesale ordering resets, eligible entries examined during a rebuild and comparisons made by the two ordered collections. Walking down the trees to get their extremes requires traversal, even when the default key comparison is never called. The counts do not measure CPU instructions, dictionary probes or those pointer traversals.

At 1,000, 10,000 and 100,000 entries in the terminal-heavy case, a stable indexed work release executed one age interval check, yielded zero keys and changed zero memberships. A first pin followed by its release removed and added one member, inspected one age interval, and made 36, 48 and 62 ordered-key comparisons, respectively. The baseline visited 1,000, 10,000 and 100,000 entries on both paths. These work bounds apply to this fixed-timestamp steady population. For a dense 79,999-victim expiry, the index stops enumerating the age range after 40,001 keys, checks the 80,000 eligible entries in ordinal order, and rebuilds one surviving membership. With all 80,000 entries due, it checks the same 80,000 eligible entries without enumerating any expired range keys. Each wave resets the ordering once and completes zero successful per-victim tree removals. Active and pinned entries stay outside both orderings.

The main matrix uses a fixed timestamp and a maximum count equal to population size. A separate 100,000-entry terminal-heavy cohort settles its last eligible terminal one tick later. The fixture checks the identity of the one survivor after 79,999 retirements, advances the clock one tick and requires the last terminal to retire. The main full-expiry wave follows the timing schedule; the partial-expiry cohort is separately seeded before its own two waves. Setup order and GC can differ between these cohorts. Compare matching rows across source revisions. The fixture clock returns an inert timer, so only the requested ownership transitions drive enforcement. The public runtime’s idle timer is covered separately by the engine tests. Setup runs before the stopwatch samples. On the baseline, the fixture defers selection during bulk setup so it can construct 100,000 entries in bounded time. For each mix a separate 100-entry run matches deferred and normal setup across the full stable and expiry counters and retired identities. The indexed build uses its normal setup at every size. In both builds, verification checks publication ownership, physical settlement with a surviving pin, the age boundary on last unpin, and physical cleanup outside the admission lock. A full expiry pass checks each eligible and active identity and the exact retirement count. Held pins keep the remaining settled terminals alive. Disabled controls perform zero selection and retire zero.

## Qualification

Independent source reviews covered the initial index at `fa652685` and dense-expiry change at `770a2a87`. The final Release build had zero warnings and errors. The ephemeral engine passed 466 tests, Core passed 408, Hosting passed its 126 ordinary tests, Acceptance passed 37 and Infrastructure passed all 323. The exact local feed contained 12 packages and strict OpenSpec validation passed all 34 items. An independent benchmark rerun reproduced every deterministic operation count across all 56 baseline and 47 indexed records and the steady-workload improvement in each enabled mix.

## Reproduce

Run these commands with PowerShell 7, Python 3.10 or newer, and a compatible .NET 10 SDK. Supply a path to the SDK executable via `-Dotnet` when needed. The runner clones into a fresh system temporary directory, instruments exact source anchors there, builds Release with warnings as errors and runs the fixture. The original checkout stays clean. File names and scratch paths are printed after each run. Use the two scratch directories as arguments to reproduce the tables from new measurements. Use the default two rounds for a comparison. With no arguments the formatter reads the six checked-in JSONL files.

```powershell
./tools/measure-ephemeral-retention/Measure.ps1 -Revision 070d2ffb
./tools/measure-ephemeral-retention/Measure.ps1 -Revision 770a2a87
python ./tools/measure-ephemeral-retention/Summarize.py
```

The recorded runs used Windows 11 Pro x64, an AMD Ryzen 9 9950X with 32 logical processors, SDK 10.0.403 and runtime 10.0.14. The two complete timing processes took 4.55/6.24 seconds on the baseline and 1.37/1.30 seconds on the indexed code, including setup and the final expiry waves. The counts processes took 1.22 and 1.31 seconds. Builds are excluded. Tiered compilation was disabled. Timed and counted processes used the same host without CPU affinity. Repeated samples capture some host variation; they do not constitute an OS or hardware qualification matrix. [Runner](./Measure.ps1) · [Probe](./RetentionProbe.cs) · [Formatter](./Summarize.py) · [Raw samples](./samples)
