# Measuring DAG worker retirement

The runner creates a disposable local clone at the chosen commit and compiles the probe into its Hosting test assembly. It only changes the fixture's class declaration and adds the probe inside that new clone. Supply an SDK compatible with the repository's global.json. Use a new output directory for each invocation. The runner prints the disposable clone path so the caller can keep or remove it.

```powershell
# From the repository root
$tool = Join-Path $PWD 'tools/measure-dag-worker-retirement'
$results = Join-Path ([IO.Path]::GetTempPath()) ("orca-dag-workers-" + [guid]::NewGuid().ToString('N'))
$base = Join-Path $results baseline
$candidate = Join-Path $results candidate
& "$tool/Measure.ps1" -Repository . -Revision 960c0383 -OutputDirectory $base
& "$tool/Measure.ps1" -Repository . -Revision HEAD -OutputDirectory $candidate
$runs = @(Get-ChildItem -LiteralPath $candidate -Filter 'runs-*.jsonl').FullName
$bursts = @(Get-ChildItem -LiteralPath $candidate -Filter 'burst-*.jsonl').FullName
python "$tool/Summarize.py" @runs
python "$tool/Summarize.py" @bursts
```

Before a commit exists, `-CoordinatorSource <DagCoordinator.cs>` replaces that one file in the disposable clone. The environment record captures the source revision, coordinator Git blob, SDK, processor count and operating system. `-ScratchParent <directory>` selects a short local path if needed.

Each round starts three fresh Release .NET test processes against the in-memory provider.

- **Idle.** Sixty-four live runs authorize one child each. Every mapper has returned, and every child waits on a gate through the last sample. The test reads the active and retired DAG worker gauges, total OS threads, private committed bytes, resident working set and virtual address space before creating runs, then after admission and five and ten seconds later. Ten seconds spans two nominal four-second discovery intervals. A full GC, pending finalizers and another full GC precede each sample. Delta from the empty host includes the complete workflow workload, JIT, native runtime and provider. These are whole-process memory observations.
- **Rapid.** Twenty-four immediately returning children form either a dependency chain or 24 independent ready roots. The host admits up to 24 children. Real hosted outbox and continuation workers use five-millisecond pump settings. Each shape runs one warm-up and three measured journeys per process. A store wrapper timestamps successful admission and join appends after the provider returns. Results include unique mapper thread IDs in each run, start to last admission, mapper entry to committed admission, gaps between admissions, and previous child join to dependent admission. Every run must reach Succeeded. For live roots, competing child joins can wait for the same admission turn; individual intervals can include the engine's 250 ms retry delay.
- **Uncontended burst.** The scheduler admits 48 ready roots on one run while child-start notices remain unclaimed. Only discovery/dispatch runs. Every node must be StartAuthorized with its input committed. Each process runs one warm-up then ten measured bursts. The recorded duration measures the first through final admission, excluding start-up and the first mapping. The probe also records mapper entry to admission and gaps between admissions for the subsequent 47 nodes.

The JSONL files retain every round, sample, timing and outlier. The summary excludes rapid/burst warm-up rounds and calculates nearest-rank p95. Multiple rounds in one process share conditions. End-to-end timings come from a shared workstation and include OS timer scheduling. Treat the results as a comparison for these workloads and machine, without an absolute latency or memory guarantee.


## Results on the 64-run workload

The baseline was `960c0383`. The final candidate used the same source with coordinator blob `63aa2aa81cc60fa9597ab6acf5cce0fa725973ce`. The probes ran on Windows 11 Pro build 26300 with a Ryzen 9 9950X (32 logical processors), .NET SDK 10.0.403 and runtime 10.0.14. Three fresh processes per scenario and build supplied the results. Other workstation activity and OS timer scheduling were uncontrolled.

| At ten seconds with 64 children still held | Baseline (three rounds) | Final (three rounds) |
|---|---:|---:|
| Active mapper workers | 64, 64, 64 | 0, 0, 0 |
| Retired mapper workers still physically running | 0, 0, 0 | 0, 0, 0 |
| Total process threads | 96, 96, 93 | 29, 32, 29 |
| Private committed growth, MiB from empty host | 21.80, 21.54, 21.56 | 17.62, 16.89, 20.78 |
| Working set growth, MiB from empty host | 35.93, 36.21, 35.14 | 32.25, 31.81, 35.67 |
| Virtual address-space growth, MiB from empty host | 232.93, 232.99, 223.94 | 31.98, 40.98, 31.93 |

Every process started with 23 threads. All 64 mapper callbacks had returned before the first held-state sample. The candidate had zero active and retired workers by the five-second sample in all rounds. The baseline still held all 64 workers at ten seconds. The largest memory difference was reserved virtual address space; the working-set and private-commit differences were a few MiB.

| Hot workload, measured samples | Baseline median / p95 | Final median / p95 |
|---|---:|---:|
| Chain of 24, preceding child join to dependent admission, 207 transitions | 17.50 / 19.17 ms | 17.42 / 19.32 ms |
| Chain of 24, start to last admission, 9 runs | 1341 / 1388 ms | 1356 / 1401 ms |
| 24 live independent roots, consecutive admission gap, 207 transitions | 264.47 / 528.96 ms | 264.55 / 528.92 ms |
| 24 live independent roots, start to last admission, 9 runs | 5327 / 7873 ms | 6155 / 7115 ms |
| 48 uncontended ready roots, consecutive admission gap, 1410 transitions | 7.58 / 23.28 ms | 7.42 / 20.23 ms |
| 48 uncontended ready roots, first to last admission, 30 runs | 350.58 / 1011.28 ms | 368.14 / 934.82 ms |

Every live independent-root run and uncontended burst used one mapper thread on both builds. Every baseline chain used one thread. Final chains used one or two. The main live-roots series saw a 15.5% higher median run time and a lower p95 in the candidate. Per-admission gaps were similar. For all 9-run comparisons nearest-rank p95 is the maximum. The tables include all warm measured runs. [Baseline samples](./samples/baseline) · [Final samples](./samples/final)


## Balanced repeat of live-root contention

The higher main-sample median prompted a repeat with the two builds interleaved in baseline/final/final/baseline/final/baseline/baseline/final order. Each of the eight fresh processes used the same 24-root shape, five-millisecond pump settings, one warm-up and three timed runs. Disposable source hooks counted each scheduled 250 ms admission retry and child-join heartbeat before the last admission. Both copies came from `960c0383`, with original coordinator blobs `02e733f4` and `63aa2aa8`. The instrumentation was identical. [Repeat runner](./Compare-Contention.ps1) · [All eight raw samples](./samples/contention)

| Metric | Baseline | Final |
|---|---:|---:|
| Start to final admission, median / p95 over 12 runs | 5971.95 / 7709.01 ms | 6118.64 / 6932.83 ms |
| Consecutive admission gap, median / p95 over 276 intervals | 263.962 / 529.153 ms | 264.136 / 529.144 ms |
| Admission retries with outcome Queued | 266 | 264 |
| Child-join heartbeats with outcome Queued before last admission | 272 | 273 |
| Distinct mapper threads per run | 1 in every run | 1 in every run |

The repeat measured a 2.46% higher end-to-end median and a lower p95 in the candidate. The candidate's process-pair median differences were +885, -22, +502 and -258 ms, each relative to its paired baseline. Each additional queued admission schedules a 250 ms delay, and observed run times followed the number of those delays closely. The initial 15.5% median increase did not repeat at that magnitude. Both comparisons retain variability from real FIFO contention and shared-machine timing.

```powershell
# Using the paths in the primary reproduction example
$repeat = Join-Path $results contention
& "$tool/Compare-Contention.ps1" -Repository . -BaselineRevision 960c0383 -CandidateRevision HEAD -OutputDirectory $repeat
$baseRuns = @(Get-ChildItem -LiteralPath $repeat -Filter '*-baseline.jsonl').FullName
$finalRuns = @(Get-ChildItem -LiteralPath $repeat -Filter '*-final.jsonl').FullName
python "$tool/Summarize.py" @baseRuns
python "$tool/Summarize.py" @finalRuns
```
