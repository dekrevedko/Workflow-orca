# GitHub build and prerelease status

Status: RECORD, 2026-10-16. This is a build/publishing audit, not an independent implementation
approval or an OpenSpec phase exit.

## Local and remote source

The initial working tree was clean at `3c2cd9606db6e4380b148c5f8dd3503e367f255f`.
After fetching GitHub, `feature/v3-rebuild` was 17 commits ahead of its remote and had no remote-only
commits. Those commits contain the reviewed DAG contract/stream identity work and SQL Server
advisory remediation. Local `master` has two additional commits from the superseded lineage;
they are not the active implementation and should not be pushed as the release source.

The latest remote CI run was
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
or registry publications were created during this audit.

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
No separate stable-release threshold is introduced by this decision. A successful GitHub run
and publication have not yet been completed. The prepared distribution uses GitHub release
downloads and does not configure an external NuGet registry.
