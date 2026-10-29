# Issue #13 task 2.5 — full validation evidence (2026-10-27)

Base: 10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd on
codex/issue-13-native-aot-trimming. The 315-path task-2.4 input tree is
30777062d26e7a8542eaffbdfd08bbd9fbe29652 (content SHA256
d5fd2b0effb2bfbf630d33928c8c6693963969e0e566ce0fdae1e49fb5d03b26).
Task 2 remains uncommitted and the real index stays empty. Task 2.6 and publication
need separate explicit authorization. No product source or executable public
baseline changed in task 2.5.

## Executed lanes

| Lane | Result |
|---|---:|
| OpenSpec all/strict | 25/25 |
| Release, warnings as errors | 0 warnings / 0 errors |
| Core / Ephemeral / Durable unit suites | 354 / 352 / 102 passed |
| Acceptance / Hosting / ProviderCertification | 37 / 24 / 100 passed |
| Infrastructure, with CI engine coverage include | 302/302 |
| PostgreSQL / SQL Server / Integration | 103 / 74 / 11 passed |
| CI lifecycle/failure/acceptance selectors | 60 passed, repeated evidence |
| Unfiltered ephemeral CI-path stress | 50/50 runs; 352 each; 17,600 case executions |
| Benchmark smoke | pass |
| Exact feed and clean twelve-package consumer | pass; result 42 |
| Package publication negative/identity checks | 8 passed; no publication |
| Native evidence gate negative controls | 4 unittest methods passed |

All runtime TRX files show zero failed, skipped, aborted or unexecuted cases.
The fifty consecutive dotnet test runs used two concurrent processes, while
ReSharper, Infrastructure and container validation supplied additional load.
The unit/provider/integration lanes ran on Windows 11 x64 against actual Linux
Docker databases. This does not claim that CI's Linux provider job ran locally.
The existing integration exclusions were preserved: no disabled provisional or
DAG source is counted as active test coverage.

ReportGenerator 5.5.11 used CI's +OrcaCore.Engine.* filter and union of the six unit
and Infrastructure Cobertura inputs: **15,952 / 20,112 lines = 79.31583134447097%**,
above 75%. Durable is 81.69329398595706%; Ephemeral is 77.04423918327662%.
The combined Cobertura and Markdown reports are preserved in
validation-task-2-5-2026-10-27/. Provider tests were not substituted into CI's
coverage numerator. Exact arguments, per-run counts and raw-file hashes are in
task-2-5-validation-record-2026-10-27.json; local raw TRX, logs and original coverage
inputs remain in ignored publishing-validation receipts after scratch removal.

## Native and contract evidence

Native run 40629471843 is reverified from its original downloaded records, raw
publish/case logs and twelve archives, not rerun or reclassified. All six tested
source/harness maps equal current checkout bytes; its 356 cases, twelve NS groups,
thirteen mandatory subcases per cell and 24 reviewed diagnostic records remain
complete. No new native/source approval is inferred. Live event delivery and
outgoing publishing stay outside the explicitly approved public recipe.

Canonical append-only identity, approved converter/throttle/public-boundary layers,
exact public catalogue and compile/package baselines pass Infrastructure. The 206
provenance rows have 195 synchronized, 8 superseded, 3 outside and zero pending;
all 199 master rows and 26 predecessor artifacts remain untouched. No dependency,
friend grant, broad compatibility attribute or released-version claim changed.

## ReSharper and review

Built InspectCode 2026.2.3.1, Release/net10.0, Any CPU, suggestions included,
solution analysis enabled, 52 changed source/guard files: **0 errors, 583 warnings,
343 notes**. Of the warnings, 508 lie on retained lines and 75 on changed/added
lines. Every finding has a retained location/rule/disposition in the TSV beside
the raw SARIF. New reports concern prescribed namespaces, naming/qualification,
nullable redundancy, metadata record use and an unused adapter argument. Callback
lifetime reports were separately checked against CancellationScope leases,
governance ownership transfer and the unchanged physical-ownership tests. No
behavior defect or suppression/automatic cleanup was introduced. This is scoped
CLI analysis, not a clean repository-wide inspection claim.

The separate whole-target self-review is included in the checkpoint manifest: the
guard requires its bytes, so the checkpoint is independently green. A later
external approval verdict is excluded and becomes a verdict-only child.
The completion tail may bind this record/review, tick only 2.5 and verify that
small guard change; whole-target approval fields remain null. Its final checks,
freeze hashes and safe cleanup receipts are recorded separately. No advance to
2.6, push, PR, issue update or publication is authorized by this execution.
