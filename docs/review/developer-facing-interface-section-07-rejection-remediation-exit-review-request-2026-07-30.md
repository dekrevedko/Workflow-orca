# Section 7 rejection-remediation exit review request

Date: 2026-07-30

Status: **REVIEW REQUIRED**. This request supersedes the earlier Section 7 exit/refreeze
requests as the live review target. It does not alter or supersede either immutable rejection
verdict. Task `8.0` remains unchecked and blocked pending a fresh independent `APPROVE`.

## 1. Frozen target and provenance

| Item | Value |
| --- | --- |
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| HEAD | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| HEAD tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline is an ancestor of HEAD | yes |
| Review target | HEAD plus every path in the self-inclusive remediation manifest |
| Expanded porcelain entries | 705 |
| Entry classes | 404 modified / 52 deleted / 249 untracked |
| Raw-manifest SHA-256 | `E42D9C8C686C64ED9372DD837B89D0EC412184C29F810726F3B6FF4260842C99` |
| LF-normalized sorted-status SHA-256 | `3856C150A07D3C5FD07167671E5552C8E2D66B0EE3B8DEA9D3E241DE74760BE0` |
| Reshape tasks | 106 complete / 30 pending / 136 total; 0 duplicate IDs |
| Governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-07-rejection-remediation-exit-review-dirty-manifest-2026-07-30.txt`.
It is a raw `git status --porcelain=v1 -uall` capture. Reproduce the exact ordered content and both
hashes before reading the claims and again after validation. The review target is invalid if any
path differs.

The normalized hash is SHA-256 over the ordinal-sorted status lines encoded as UTF-8 without BOM,
joined by LF, including one final LF.

No commit is requested during review. If and only if the target receives an independent approval,
the implementation owner must create the mandatory Section 7 checkpoint commit before beginning
task `8.0`.

## 2. Rejection findings that must be re-derived

### 2.1 Durable start binding and event deduplication

1. The durable start-idempotency record persists target identity, definition fingerprint, and
   deterministic input fingerprint atomically with the start. A replacement runtime accepts an
   identical replay and returns the closed conflict for changed input or definition fingerprint.
2. The durable inbox persists target identity and normalized envelope fingerprint. Classification
   is target-aware, survives process replacement, distinguishes `Duplicate` from `EventConflict`,
   and occurs before terminal/no-wait classification.
3. PostgreSQL and InMemory implement the same provider contract. The re-enabled PostgreSQL binding
   includes replacement-process start and event cases.

The direct regressions are `DurableStartIdempotencyFacadeTests`,
`DurableEventDeduplicationFacadeTests`, the shared provider certification, and
`PostgreSqlProviderCertificationTests`.

### 2.2 Cooperative cancellation and serialized lifecycle results

1. Both engines expose committed `CancellationRequested` while work remains in flight and return
   `AlreadyRequested` on a repeated request.
2. Durable replay and replacement-host continuation preserve the request and finalize cancellation.
3. Cancellation and termination results come from the serialized committed mutation, not a stale
   pre-read; concurrent terminal winners return their exact closed result.

The direct regressions are `DurableLifecycleFacadeTests`,
`EphemeralWorkflowFacadeTests`, the lifecycle machine suite, and executable lifecycle scenarios.

### 2.3 Guard and task-accounting corrections

1. Task accounting includes `3.11a` through `3.11d`: 106/30/136 with no duplicate IDs.
2. All 81 completed-section behavior scenarios execute their original frozen contract against the
   current physical assemblies: Section 4 = 4, Section 5 = 8, Section 6 = 32, Section 7 = 37.
   No Section-5/6 call substitution or `AssertExecutableAgainstCurrentPhysicalAssemblyAsync`
   relaxation remains.
3. Exception-origin certification requires the exact expected declaring type/member. The old
   assembly-wide `OrcaCore.Core` fallback is absent, and the throwing-argument mutation proves an
   uninvoked facade cannot mint an observation.
4. Deterministic TimeProvider/barrier consumption requires a product frame causally above the
   guard-runner boundary. Direct harness use and stale lower continuation frames cannot mint
   evidence. The `cancel-every-barrier` driver now reaches each gate through
   `DurableCommandProcessor`; it no longer credits a direct test-gate call as product execution.

### 2.4 Test retirement and inactive-project recovery

Read these live ledgers in full:

- `developer-facing-interface-section-07-remediation-test-recovery-record-2026-07-30.md`
- `developer-facing-interface-section-07-inactive-test-project-audit-2026-07-30.md`

Re-derive, rather than accepting, these claims:

1. The one-sentence provisional retirement rationale is superseded by a declaration-level ledger.
   The unsupported-retirement class is zero.
2. Active recovery includes 42 acceptance tests, public Core authoring/compiler/strong-value tests,
   64 Ephemeral tests including 34 structured-fiber public cases, 28 restored Durable cases, and
   every file formerly available only in the recovery worktree.
3. The 98 explicitly excluded files / 549 declarations are each classified as named public
   replacement evidence, Section 8, or an explicit non-v1/superseded contract. No test source was
   deleted as a substitute for porting.
4. The PostgreSQL provider binding is active: its 40 cases and the complete 79-test project pass
   against Docker.
5. The five formerly inactive projects and their original 146 declarations remain physically
   preserved. The integration project is active in `OrcaCore.slnx` with five public-only current
   journeys; SQL Server remains an explicit task-10.2 plan-reconciliation obligation.

Any unjustified exclusion, lost supported behavior, test-only contract weakening, or mismatch
between ledger and source is a release blocker.

## 3. Owner-run evidence

All commands were run from `X:\Projects\GitHub\Workflow-orca` against Release output unless noted.
Do not trust these counts; reproduce them.

| Lane | Result |
| --- | --- |
| Clean solution rebuild | 24 projects; 0 warnings / 0 errors |
| Core | 413 passed / 0 failed / 0 skipped |
| Ephemeral | 64 passed / 0 failed / 0 skipped |
| Durable | 72 passed / 0 failed / 0 skipped |
| Hosting smoke | 1 passed / 0 failed / 0 skipped |
| Acceptance | 42 passed / 0 failed / 0 skipped |
| Provider certification | 80 passed / 0 failed / 0 skipped |
| PostgreSQL Docker | 79 passed / 0 failed / 0 skipped; the re-enabled binding contributes 40 |
| Reactivated integration | 5 passed / 0 failed / 0 skipped |
| Active product/integration total | 756 passed / 0 failed / 0 skipped |
| Section 7 executable drivers | 37 passed / 0 failed / 0 skipped |
| Infrastructure guards | 162 passed / 0 failed / 0 skipped on three consecutive runs |
| Expected-red guards | 0 passed / 14 intentional named Section-8/9 failures / 0 skipped |
| Green compile fixtures | freshly packed; exact and product consumers compile; 26 + 26 forbidden CS1061 calls rejected |
| Expected-red compile fixtures | 0 gaps |
| Green package fixtures | six built from the exact repository-local feed through Section 7 |
| Package expected-red | exactly `dag-hosting` and `kubernetes-companion` fail |
| Strict OpenSpec | both active changes valid; all 17 items valid |
| Task accounting | reshape 106/30/136; governance 16/0/16; no duplicate IDs |
| Vulnerability audit | no vulnerable packages across all 24 active solution projects |
| Whitespace | `git diff --check` exit 0; line-ending notices only |

The first sandboxed PostgreSQL attempt could not open Docker's Windows named pipe and produced
75 access failures plus four non-container passes. The exact command was immediately rerun with
Docker-pipe permission and passed 79/79. This was an execution-permission limitation, not a product
failure; independently verify Docker access.

## 4. Minimum independent commands

```powershell
git status --porcelain=v1 -uall
git rev-parse HEAD
git rev-parse 'HEAD^{tree}'
git merge-base --is-ancestor 8c2dd712 HEAD

dotnet build OrcaCore.slnx --configuration Release --no-incremental --nologo --verbosity minimal

dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Acceptance.Tests/OrcaCore.Acceptance.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Providers.PostgreSql.Tests/OrcaCore.Providers.PostgreSql.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj -c Release --no-build --no-restore

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ExecutableBehaviorInfrastructureGuards.Section7Scenario"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=ExpectedRed"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition Green -CompletedSection 7
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed -CompletedSection 7

openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
openspec.cmd validate --all --strict
dotnet list OrcaCore.slnx package --vulnerable --include-transitive
git diff --check
```

Run the infrastructure command three consecutive times. Run Docker-backed suites sequentially and
separately from fixture builds. Expected-red guard and package commands must return nonzero with
exactly the named intentional failures.

## 5. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record the exact manifest
comparison before and after, every command/result, independent source/test derivation, the
retirement and inactive-project judgment, and `APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to create the mandatory
Section 7 checkpoint commit and begin task `8.0` only in a later implementation turn. Rejection
keeps Section 8 blocked.
