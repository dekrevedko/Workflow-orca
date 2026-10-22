# GitHub build and prerelease status

Status: RECORD, 2026-10-16. This is a build/publishing audit, not an independent implementation
approval or an OpenSpec phase exit.

## Publication update

The owner-authorized push and publication completed on 2026-10-16:

- [v0.1.0-preview.1](https://github.com/dekrevedko/Workflow-orca/releases/tag/v0.1.0-preview.1)
  is published from `299d02dce92a8ee703ea0d89a42f05bb0a103055`, with twelve `.nupkg` assets
  and `SHA256SUMS`.
- The [release workflow](https://github.com/dekrevedko/Workflow-orca/actions/runs/38961860144)
  passed build, unit/acceptance and infrastructure checks, real provider/integration tests,
  the 75% coverage gate, packaging, and the clean external consumer check.
- The downloaded release assets passed exact inventory, SHA-256, version, and source-commit
  checks. A fresh external project restored all twelve and ran the typed ephemeral sample,
  returning `42`.
- [PR #5](https://github.com/dekrevedko/Workflow-orca/pull/5) added NuGet.org trusted publishing.
  After the owner configured the `Dekrevedko` policy, the
  [successful publication retry](https://github.com/dekrevedko/Workflow-orca/actions/runs/38979035794/attempts/2)
  uploaded the same twelve release files, verified every public version and its repository/source
  metadata, then restored all twelve into a fresh consumer using only NuGet.org and returned `42`.
  The published source remains `299d02dce92a8ee703ea0d89a42f05bb0a103055`.
- [PR #4](https://github.com/dekrevedko/Workflow-orca/pull/4) contains the implementation
  and publishing changes. The [branch CI](https://github.com/dekrevedko/Workflow-orca/actions/runs/38962283979)
  and [PR CI retry](https://github.com/dekrevedko/Workflow-orca/actions/runs/38962289819/attempts/2)
  passed for the package-guide follow-up at `138dd76ccf2e1eace7ca3b2e06ab2fa572f65b75`.

The first PR check of that documentation-only follow-up failed in the competing-owner
`definitionless-continuation-handoff` scenario. Thirty consecutive isolated repetitions,
all 247 infrastructure checks with the CI coverage settings, and the CI retry passed.
The cause remains unconfirmed; no assertion was weakened or product behavior changed.
The observation and evidence links are recorded in PR #4.

The first [NuGet installation-guide CI run](https://github.com/dekrevedko/Workflow-orca/actions/runs/38985196935)
failed in the timer retry test: its 10,000-iteration polling budget expired after 77 ms.
Local two-processor coverage repetitions also exposed a throttle test that checked terminal
completion immediately after event acceptance. The test follow-up bounds dispatcher observation
by elapsed time, awaits the retry's completion signal, and waits for both throttled instances'
typed terminal outputs before checking their completed snapshots. All waits have ten-second
bounds and use scheduler yielding or completion notifications, without wall-clock sleeps.
Runtime behavior and packages are unchanged.
The original timer failure did not reproduce in 100 isolated class runs or a temporary
1,000-scenario loop; these checks do not prove its precise scheduling cause. After the wait changes,
all 88 ephemeral tests passed twenty consecutive full-suite runs with coverage and two reported
processors; all 19 repository rule checks and 16 source/documentation guards also passed.
The temporary diagnostic loop was removed.

Issues [#1](https://github.com/dekrevedko/Workflow-orca/issues/1),
[#2](https://github.com/dekrevedko/Workflow-orca/issues/2), and
[#3](https://github.com/dekrevedko/Workflow-orca/issues/3) remain open. Durable DAG execution
is incomplete. Packages require .NET 10 and are distributed on NuGet.org and as GitHub release downloads.

The sections below retain the initial preparation audit, before pushing and publishing.

## Local and remote source

The initial working tree was clean at `3c2cd9606db6e4380b148c5f8dd3503e367f255f`.
After fetching GitHub, `feature/v3-rebuild` was 17 commits ahead of its remote and had no remote-only
commits. Those commits contain the reviewed DAG contract/stream identity work and SQL Server
advisory remediation. Local `master` has two additional commits from the superseded lineage;
they are not the active implementation and should not be pushed as the release source.

At the start of the audit, the latest remote CI run was
[37241078578](https://github.com/dekrevedko/Workflow-orca/actions/runs/37241078578).
Its solution build and provider/integration job passed. The unit job failed at the benchmark
build with `NETSDK1004`, because the benchmark project was outside the solution restore.
Once restored locally, the benchmark also exposed 29 errors against retired or internal APIs.

## Prepared fixes

- Include the benchmark project in solution restore/build and use current typed engine handles,
  DI registrations, provider ports, projection snapshots, and structured authoring. Run one
  correctness smoke pass of every benchmark in CI.
- Run the contract certification job on Windows because its compile fixtures invoke Windows
  PowerShell. Build the required twelve-package local certification feed before the guards.
- Collect coverage for engine assemblies during infrastructure guards. Restrict instrumentation
  to engines so assembly-metadata boundary guards inspect uninstrumented DAG/hosting inputs.
- Add delivery rollback, timer retry, terminal cleanup, detached-state, lifecycle-history and
  throttle-ownership regressions. Preserve excluded legacy tests and expected-red dispositions.
- Add exact-manifest prerelease packing, SHA-256 checksums, an external package-only consumer,
  CI package artifacts, and a GitHub prerelease workflow triggered by prerelease tags or manually.
- Refresh the historical current-worktree match list after editing CI. Historical evidence rows
  remain intact; the old CI bytes are no longer claimed to match the current file.
- Record the two new regression sources in the exact inventory (344 sources, 1,424 declarations;
  196 active files, 736 active declarations). Retired dispositions and their digest are unchanged.
  Refresh only the intentional coverage/distribution documentation pins in Task 7.3 rows 1 and 11,
  with the artifact digest and the maintained Task 7.20 inventory note in the same target.
- Preserve committed line endings on the Windows runner so raw historical-byte comparisons use
  the reviewed source bytes. Include open retention/shutdown/AOT limitations in release notes.

The runtime source under `src/`, public API baselines, canonical specs, active task checkboxes,
and implementation approval states were not changed. No commits, branches, tags, pushes, releases,
or registry publications were created during the initial preparation audit.

Issue #2's shutdown contract (3c8d33c / 4bd7c3e) and F2 canonical transition
(2e51fbd / 6253f6c) are approved and checkpointed. The Section 3 source/candidate-
package target implements the seven lifetime/result/exception declarations and
qualifies the three drain recipes. Independent source approval, a push, a PR and
release publication remain pending; no published shutdown API is claimed here.

## Validation

Windows and a clean Linux container with SDK `10.0.402` both built the complete solution in
Release with warnings as errors: zero warnings and zero errors. All benchmark smoke scenarios
passed on both systems. `actionlint` 1.7.12 accepted both workflows.

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Core | 354 | 0 | 0 |
| Ephemeral | 88 | 0 | 0 |
| Durable | 102 | 0 | 0 |
| Hosting | 24 | 0 | 0 |
| Acceptance | 37 | 0 | 0 |
| In-memory provider certification | 100 | 0 | 0 |
| Infrastructure contract guards | 247 | 0 | 0 |
| PostgreSQL | 103 | 0 | 0 |
| SQL Server | 74 | 0 | 0 |
| Integration | 11 | 0 | 0 |
| Total | 1140 | 0 | 0 |

The table combines the final passing runs of each suite; it is not a claim that all tests ran
in one invocation. Intentionally expected-red guards were not included.
The infrastructure guard run verified the existing provenance checkpoint: 184 rows,
48,504 bytes, SHA-256 `fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8`,
no pending canonical operations, `semanticApprovalEligible: true`. This structural/provenance
result supplies no new product implementation approval.

All twelve packages were packed on Windows as `0.1.0-preview.local.1` and in the clean Linux
container as `0.1.0-preview.linux.1`. On each system a separate consumer outside the repository
restored all twelve into a fresh cache and executed the typed ephemeral sample with output `42`.
The Linux test container was removed on completion. Local installable packages are in
`.artifacts/publishing-validation/packages`; logs and coverage are under the same ignored
validation directory. Installation steps are in [packages.md](packages.md).

## Coverage gate decision

The original CI gate required 80% engine line coverage. Unit tests alone measured about 69.6%.
After adding the existing infrastructure scenario coverage and the new regressions, the same
ReportGenerator calculation measured **77.36836484032895%**.

On 2026-10-16 the owner explicitly instructed: "Update gate to 75%". CI and the repository
instructions now require **75%** engine line coverage. The measured **77.37% passes** that gate.
No separate stable-release threshold is introduced by this decision. The publication update
above records the subsequent successful GitHub runs and release, followed by verified
NuGet.org publication. The initial preparation audit configured GitHub release downloads;
external registry publishing was added in PR #5.

## Native publishing assessment candidate (2026-10-21)

Issue #3 uses fresh 0.0.0-phase0 candidate packages with exact hashes; these do not
change the published release or add a native-compatibility badge. Native assessment
and independent review are pending. Record validation may be green for a demonstrated
unsupported profile, but cannot be presented as a passing native consumer. Preserve
SDK-default and labelled switch-enabled results separately. No issue #3 push, PR,
release, merge or closure is authorized by its contract checkpoint.
