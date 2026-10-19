# Fresh package-only resident-host experiment — 2026-10-18

This is a Windows/.NET 10 Release experiment against twelve freshly packed local
`0.0.0-phase0` packages. It does not publish or qualify a new NuGet.org release.
Exact package hashes, SDK/OS information, resolved dependency closure, source
hashes, input sizes and raw observations are in [results.json](results.json).

The consumer was copied to `E:/oc5-lab`, outside the repository and its build
properties. Its only direct references are `OrcaCore.Engine.Ephemeral` and
Microsoft dependency injection; the OrcaCore transitive closure is `OrcaCore`,
`OrcaCore.Core` and `OrcaCore.Engine.Ephemeral`. Source mapping permits OrcaCore
packages only from the fresh local feed. There are no project references,
internal engine references, reflection calls or friend grants in the consumer.
The separate observation script uses external heap diagnostics, not a product API.

Each process registers two definitions and runs 1,024 sequential finite jobs,
alternating definitions, through the public registration/start/output APIs and a
named typed `EffectStep`. Every job owns a 48 KiB state payload and 48 KiB output
payload; the output content is checked. The bound is 16, so the two measurement
points are 32 and 64 times the bound. Eight early handle objects remain held;
caller output copies are dropped and checked with weak references. The null/default
policy is compared with count 16, age one hour and sweep one second, keeping age
expiry out of the count experiment.

| Policy | Jobs | Runtime / facade / keys | State / output objects | Managed MiB | Heap MiB | Allocated GiB | Private MiB | Working set MiB |
|---|---:|---|---|---:|---:|---:|---:|---:|
| null/default | 0 | 0 / 0 / 0 | 0 / 0 | 0.43 | 0.45 | 0.00 | 10.57 | 36.90 |
| null/default | 512 | 512 / 512 / 512 | 1024 / 1024 | 132.40 | 137.93 | 10.91 | 201.29 | 241.43 |
| null/default | 1024 | 1024 / 1024 / 1024 | 2048 / 2048 | 262.16 | 267.21 | 21.88 | 394.97 | 434.35 |
| bounded | 0 | 0 / 0 / 0 | 0 / 0 | 0.44 | 0.46 | 0.00 | 10.29 | 36.80 |
| bounded | 512 | 16 / 16 / 16 | 32 / 32 | 7.09 | 8.83 | 10.89 | 56.03 | 96.70 |
| bounded | 1024 | 16 / 16 / 16 | 32 / 32 | 7.08 | 8.83 | 21.77 | 45.25 | 87.52 |

SOS confirms live runtime and payload object counts. It reads the actual facade
and start dictionaries' `_count - _freeCount`, not their high-water mark. Their
bounded backing-array capacities remain 17 at both points, as does the retention
dictionary; definition registrations stay at two. Null/default retention creates
no retention entries. Execution and published snapshots explain the two live
state/output objects per retained runtime. Held handles can retain small identity
bindings; those caller-owned identities are distinct from the measured facade
dictionary count and do not retain the retired payloads.

Compatible replay of the newest retained key preserves its ID and does not run
the step again in both modes. Reuse of the first key preserves the old ID and
effect count without the policy. With the policy it creates a fresh ID, increments
the effect count, and leaves the old handle not-found for its original ID.

A separate real-clock behavior run uses count 16, age 200 ms and sweep 20 ms. It
waits on lifecycle notifications, making no further workflow calls while awaiting
idle expiry of 64 finite jobs. An event-waiting instance and a logically timed-out
instance whose physical step is blocked survive both that age and count pressure;
the protected key still replays the same ID. Releasing the physical step permits
its later retirement; explicitly terminating the waiting instance permits its
retirement. All 66 retirement observations and original-ID not-found checks pass.

GC is forced **only in the measurement process** immediately before heap capture.
The runtime does not force GC. Managed live memory, GC heap size, cumulative
allocation, private bytes and working set are separate observations. Allocation
continues growing in both modes; this is retained-ownership qualification, not an
allocation optimization or a total-memory ceiling. Working-set decline is not an
acceptance oracle. This single-machine experiment does not establish Linux/AOT
qualification, arbitrary external-effect cleanup, shutdown guarantees, a
performance budget or restart durability. The separate concurrency tests cover
query pins, notification races and the start-to-observe gap.

To reproduce, pack with `tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1`,
copy `Program.cs` and `RetentionConsumer.csproj` outside the repository, replace
`@LOCAL_FEED@` in `NuGet.Config.template`, then restore into an empty package cache
and build Release with warnings as errors. Run `RetentionConsumer.exe behavior`.
Run `observe.py --help` for the heap controller arguments; use dotnet-dump
`10.0.750501`. It drives the public consumer's checkpoints, captures Heap dumps,
uses SOS `dumpheap`/`dumpobj` to obtain counts, and deletes each generated dump
after extracting compact results. No dump or package/build output belongs in the
review target.
