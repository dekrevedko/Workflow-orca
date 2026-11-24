# Phase G qualification

The compiled instruction plan is the sole executable representation for supported workflows. The nested authoring graph remains the source for validation, fingerprints and lowering. The merged master base includes PR #40 at `f66ac0b8`. That integration changed verification and documentation. Its source tree under `src/` was identical to the earlier baseline `8c5e8998`. The final implementation and its regression tests are committed at `c9773c75`. A subsequent fixture correction at `c595c651` removes a single current-file match from the combined 7.5/7.6 historical review. Extending the deletion-ledger guard had changed that file’s bytes. Independent recomputation of all 16 lists confirmed this one change. [Reachability trace](./reachability.md) · [Test crosswalk](./coverage.md) · [Production disposition ledger](./removal.md)

## What the independent checks exercised

- On the baseline, a throwing sentinel in the zero-instruction dispatch did not fire on supported workflows. Reversing the predicate stopped the public parallel test at the sentinel.
- At `705f70fe`, a second sentinel threw on every read of the old tree. The ephemeral, durable, acceptance and hosting/DAG suites passed. The same getter stopped the typed Init test on the baseline. A fabricated empty executable plan was rejected before registration or Init. A compiler-bound dynamic End returned its chosen name or null and propagated the same exception object.
- Four public regression tests ran against unchanged engine code. The parked-sibling rejection test checked the index actually owned the wait before rejection and released it afterward. A reviewer left an orphan indexed and verified the assertion failed. The fifth port used the old and new scheduler signatures. Bypassing the lane, dropping the lifetime lease or capturing the snapshot before its lane turn each caused that test to fail.
- The removal replaced the graph lookups and recursive dispatch without changing the supported deadline, output, DAG binding or snapshot contracts. Independent review checked the remaining timer/resume callers, immutable per-instance options, package identities, recovery files and named executable witnesses. The numbered CR-003 clarification states how the already-required compiled plan executes. A one-word unrelated CR-004 edit failed the bounded documentation guard.

## Validation

| Lane | Result |
| --- | --- |
| Release build, warnings as errors | 0 warnings and 0 errors |
| Core / ephemeral / durable / hosting | 408 / 471 / 158 / 126 passed |
| Acceptance / in-memory provider / integration | 37 / 135 / 11 passed |
| PostgreSQL / experimental SQL Server | 165/165 and 136/136 passed, respectively. |
| Explicit process restart against both databases | PostgreSQL 2/2 and SQL Server 2/2 passed. Lost-acknowledgement replay joined on attempt two. Cancellation-first recovery joined on attempt one. Each recovered in a new process, retained the sibling and dispatched one notice. |
| Infrastructure and public API/package guards | 323/323 passed after the independently reviewed historical match refresh. |
| Engine line coverage across the six unit projects and Infrastructure | 85.39% (18,538 of 21,709 lines). The CI threshold is 75%. |
| Benchmark smoke and external package consumer | Smoke passed. All twelve packages restored in a fresh cache and the typed workflow returned 42. |
| Wait-ownership checks from #40 | Six runner classifications matched. The handover, mutation, witness and control selection produced 3 successful checks and 25 expected counterexamples. |
| OpenSpec | 34 items passed strict structural validation. The canonical owner guard passed on the merged #40 checkpoint of 243 rows / 65,048 bytes, SHA-256 `84625e3e0b80802c992b2ce60efe3c2ffb4720f2b8016b21d72db5626e82a5f6`. The unresolved-operation table was empty and the checkpoint was eligible for semantic approval. |

The first full SQL Server run passed 135 tests and hit the 90-second limit in `DagScheduler_CachedNoReadyRunRecoversRemoteJoinAfterLostWake` while waiting for the remote child join, before dropping the wake. The isolated test passed on both the candidate and unchanged `f66ac0b8`. The full 136-test SQL Server suite then passed in a clean checkout after PostgreSQL finished. The timeout’s cause is unconfirmed.

## Packaged runtime comparison

Independent clean clones built `8c5e8998` and `c9773c75` with SDK 10.0.403. All twelve packages on each side identified the correct source commit and contained the DLLs from that build. An external consumer restored the product assemblies from each local feed.

| Windows 11 x64 mode | Baseline and candidate, each | Assertions | Explicit pass markers | Approved warning occurrences |
| --- | ---: | ---: | ---: | ---: |
| JIT | 84/84 | 262 | 52 | 0 |
| Fully trimmed | 56/56 | 131 | 40 | 4 |
| Native AOT | 56/56 | 131 | 40 | 8 |

The case identities, outputs, exception types, negative controls and warning identities matched. Stack comparisons normalized scratch checkout paths and compiler-generated file-local identities in two observations. The trimmed and AOT consumers ran with JSON reflection disabled. The AOT consumer ran with dynamic code disabled.

The six Windows Server 2025 and Ubuntu 24.04 release runs remain a hosted qualification. This comparison is Windows 11 preflight. The wait-ownership model counts above cover the focused handover and control selection. [Packaged native support workflow](../../../.github/workflows/ephemeral-native-support.yml) · [Wait-ownership verification workflow](../../../.github/workflows/wait-ownership.yml)
