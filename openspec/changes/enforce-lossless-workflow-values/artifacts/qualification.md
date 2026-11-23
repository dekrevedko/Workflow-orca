# Phase B qualification

The reviewed product source is `200d13a854677743b644e9bb1dbde2edb2318e05`. The change starts from merged master `499b9018`, including the DAG public facade. The [contract approval](contract-approval.md) covers the replacement semantics. The [canonical synchronization record](sync-openspec-provenance-2026-11-17.md) identifies the six requirement operations.

## What the tests establish

A separate process invoked the same fixed codec on the base and candidate assemblies. On master the private-setter input encoded as `{"Value":17}` and decoded to zero. On the candidate the encode raised `NotSupportedException`. The constructor-bound control returned 17 in both processes.

| Boundary | Observable result and executable coverage |
| --- | --- |
| Shared codec | `FixedWorkflowValueCodecCollectionTests` rejects a private setter losing 17, a transforming constructor, a constructor throwing during reconstruction, a changing getter and a nested lossy value. It preserves constructor-bound and computed members, ordered collections and declared null. The test mutates the original collection after capture. |
| Durable start and replay | `DurableStartIdempotencyFacadeTests` rejects lossy values before binding a new key and on replay. One capture supplies both the start fact and the fingerprint seen by local and replacement hosts. A typed null is recorded as the four JSON bytes `null`. |
| Ephemeral input and detached state | `EphemeralWorkflowFacadeTests` verifies that Init receives a detached input. When a step replaces state with a lossy value, the failure names the affected type and the last committed state remains queryable. The durable counterpart checks the failed step and its checkpoint. |
| Events and outputs | `EventEnvelopeContractTests` exercises typed payload and StartOrDeliver input rejection, fresh detached reads and present null versus absent bytes. `DurableEventDeduplicationFacadeTests` mutates the exposed payload and route input and verifies the accepted bytes and replay fingerprint, including base-typed delivery. `DurablePublishTests` delivers a present null through the public outbound accessor. Existing typed output and polymorphism tests exercise terminal output validation. |
| DAG | `DagFacadeTests` rejects a lossy run input on first start and replay. A lossy mapped input yields `DAG_INPUT_MAPPING_INVALID` before child admission. Tests cover reopening with an incompatible installed type, malformed persisted records and a compatible host finishing the run. |
| Structured execution | The item snapshot and branch/item result tests enforce byte limits on the first encoding, before invoking a reconstruction constructor. The size exception and exact-limit success remain observable. |

Shape validation retains its existing metadata rules. The new value test uses the actual serialized representation. A private-setter value of zero can pass when zero reconstructs faithfully. User-defined equality, unencoded CLR state and application side effects are outside this evidence. An invalid structural selector follows its existing engine failure path. The new step rollback tests concern business-step replacements.

## Independent review

Two reviewers independently checked contract ownership, conditional omission and the seven numbered document replacements. Source review established that separate captures could disagree across durable start and replay and that ephemeral Init could mutate the caller’s input. It also caught size checks running after reconstruction, typed events losing payload after an upcast, and explicit null becoming absent in the durable start fact. The revised tests exercise those cases. A second reviewer rebuilt the durable facade and DAG tests before the source checkpoint.

The clean solution run exposed a reflective test adapter that still selected a deleted internal overload, missing AC-328 discovery tags, an outdated test inventory total and an older guard’s whole-document canonical hashes. The reviewed fixture correction is `309450cbc2bf64c6526a9c2f66173e7183658f03`. It preserves the old hash chain and rejects an edit to a document preamble even when every requirement still matches.

## Release verification

The production source and package probes used the clean implementation commit above. The passing Core, Acceptance and Infrastructure results use the fixture corrections in `309450cb`. Both commits carry the same production source tree.

| Local lane | Result |
| --- | --- |
| Release build with warnings as errors | 0 warnings, 0 errors |
| Core / Durable / Ephemeral / Hosting | 408 / 137 / 432 / 95 passed |
| Acceptance / in-memory provider certification | 37 / 116 passed |
| Infrastructure | 323 passed |
| PostgreSQL / experimental SQL Server / integration | 127 / 98 / 11 passed |
| Benchmark smoke and external package consumer | Passed. All 12 packages restored into a fresh cache. The public workflow returned 42. |

AC-328 selects 13 cases across Core (4), Durable (4), Ephemeral (2) and Hosting (3). One intermediate Infrastructure run failed in the 30 ms lease-retry fixture for retained bindings. The individual scenario passed on rerun, and the subsequent full Infrastructure run passed 323/323.

## Native package preflight

Windows 11 Pro x64 build 26300, .NET SDK 10.0.403. The protocol-4 runner used a clean checkout, the 12-package Release feed and fresh cache and work directories for each flavor. All 12 archived assemblies matched their corresponding Release output and all nuspecs named the source checkpoint. The three flavors performed nine external application publishes.

| Flavor | Cases passed | Diagnostics | Runtime checks |
| --- | ---: | ---: | --- |
| Self-contained JIT | 84/84 | 0 | Generated and reflection controls executed in fresh processes |
| Fully trimmed JIT | 56/56 | 4 | JSON reflection disabled |
| Native AOT | 56/56 | 8 | JSON reflection and dynamic code disabled |

Every diagnostic identity and occurrence count matched the existing Windows catalogue. The suite exercised private-setter loss, nested values, faithful immutable/constructor choices and unstable conditional omission. The runner recorded an unqualified host after saving each complete report. Formal support qualification still requires Windows Server 2025 and Ubuntu 24.04 under the approved native workflow.

The three report SHA-256 digests in table order are `550adcee230d5076565616658310d99292aeff212f00a55e5f43be014f5de9e2`, `53cc7b1f7a86b4ff122deadd85195126a86d968920f8020d1b5359e81da73786` and `0fedddfcec8ee544c352bd7c3dee933b00b48c5fa5df99d1952a66f9fba5e1b6`. The digest of the 12-package manifest is `7ae6f1b718e6c347c44dabc2896f2d4fc52f91ab418f531accb64e600deca654`, the 417-source-file manifest is `c861d0c8cd7fd1eedb2c45edf34c2321f8fa514933564751801ded2d213a4d5c` and the 23-file harness manifest is `7c00466c7cb3544b011e3673e5ea2ce1b760795c72806dacd320dedf1d29509a`. Each manifest hashes UTF-8 lines in ordinal path order. Each line ends in LF and has the form `<sha256>  <path>\n`. The local reports are machine-generated evidence. These digests identify that run.

To repeat, build the source checkpoint in Release and run `tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -NoBuild -OutputDirectory artifacts/phase0-packages`. For each of `jit`, `trimmed` and `aot`, run `tools/ephemeral-native-assessment/support_v2/run_support.py` with `--repository`, `--feed`, fresh `--work` and `--output` directories, `--package-version 0.0.0-phase0`, `--flavor` and `--preflight`.

## Detachment cost

A local Release microbenchmark isolated the reflection-mode operation used for attempt-local state. On master it called Serialize followed by Deserialize. On the candidate it called Detach and kept the returned object alive. Both processes used the same .NET 10 runtime with tiered compilation disabled. After 5,000 warmups per shape, each process measured five batches of 30,000 captures using Stopwatch and current-thread allocated bytes. Values below are median time per capture and allocated bytes per capture.

| Declared state | Master ns / bytes | Phase B ns / bytes |
| --- | ---: | ---: |
| Int32 (17) | 501 / 192 | 293 / 144 |
| Record with one Int32 | 1,071 / 520 | 1,604 / 840 |
| Record containing an ordered list of eight one-Int32 records | 6,227 / 4,176 | 11,157 / 6,480 |

For DTOs the cost includes re-encoding the object returned by the decoder. The integral fast path can reuse the immutable value. These observations describe the measured shapes on one host. Engine throughput, provider scale, other payload sizes and release-platform native throughput remain unmeasured.
