# Section 6 greenfield-schema remediation: independent exit re-review request

**Date:** 2026-07-30  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** Section 6 exit and the later start of task `7.0`

This request supersedes the migration-oriented target in
`developer-facing-interface-section-06-relational-provider-remediation-rereview-request-2026-07-30.md`
without modifying that request, its manifest, or any earlier verdict. After that target was frozen,
the implementation owner clarified the governing product constraint: OrcaCore is greenfield, has no
released durable-data compatibility promise, and must not carry compatibility migrations or legacy
row backfills.

The target therefore keeps the corrected first-created schema and provider behavior, but removes the
new PostgreSQL `008_resource_pool_creation_capacity` and SQL Server
`009_resource_pool_creation_capacity` upgrade scripts, PostgreSQL compatibility DDL, and both
legacy-row upgrade assertions. Task `7.0` remains open and blocked until an independent reviewer
approves this exact refrozen target without a release blocker.

## 1. Immutable review history

Preserve and read:

- `developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-request-2026-07-29.md`;
- `developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-dirty-manifest-2026-07-29.txt`;
- `developer-facing-interface-section-06-ancestor-terminal-remediation-independent-rereview-verdict-2026-07-29.md`;
- `developer-facing-interface-section-06-docker-provider-gate-superseding-verdict-2026-07-29.md`;
- `developer-facing-interface-section-06-relational-provider-remediation-rereview-request-2026-07-30.md`;
- `developer-facing-interface-section-06-relational-provider-remediation-rereview-dirty-manifest-2026-07-30.txt`; and
- the earlier Section 6 request, manifest, and rejection verdicts named by those artifacts.

The ancestor-terminal remediation, Docker-provider fixes, and explicit integration-test disposition
remain part of this target. Only the unsupported compatibility/upgrade layer has been removed.

## 2. Refrozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Review target | `HEAD` plus every entry in the self-inclusive greenfield-schema manifest |
| Self-inclusive porcelain entries | 493 |
| Entry classes | 350 modified / 9 deleted / 134 untracked |
| Raw-manifest SHA-256 | `7D4D7BABF1A15AAA8484355A3ABFDFFAF08F3CDFC61527CB104A0D4022993153` |
| LF-normalized sorted-status SHA-256 | `16BA0410267480C5B1E785C5A2FE259F9F5C0B3F3417A3FDA352BB2B5A4D1531` |
| Reshape tasks | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Runtime-governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |
| Docker engine | 29.6.1, `linux/x86_64` |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-06-greenfield-schema-remediation-rereview-dirty-manifest-2026-07-30.txt`.
Reproduce it before reading conclusions and again after validation. Any path/status drift invalidates
the review.

## 3. Claims to re-derive

### 3.1 Greenfield schema contract

1. The OpenSpec proposal and design explicitly state that OrcaCore is greenfield, has no
   compatibility obligations, and must not add compatibility shims.
2. A fresh PostgreSQL resource-pool initialization creates separate non-null
   `creation_capacity` and mutable `capacity` columns directly.
3. SQL Server's actual first-create path, `001_initial.sql`, creates both columns directly.
   The redundant guarded definition in `003_resource_pools.sql` is consistent with it.
4. No `008_resource_pool_creation_capacity` or `009_resource_pool_creation_capacity` resource,
   migration-journal assertion, legacy-row fixture, `creation_capacity` backfill, or compatibility
   `ALTER` remains.
5. This is intentionally not an upgrade path. Development databases created with the provisional
   schema may be discarded and recreated.

### 3.2 Relational resource-pool behavior

1. PostgreSQL and SQL Server reject resize capacity less than one before database mutation.
2. An update that names no configured pool throws `ResourcePoolNotConfiguredException`.
3. Re-upsert with the original creation definition is an idempotent no-op that preserves current
   capacity. Changed creation capacity or lease duration throws `InvalidOperationException`.
4. `ReleaseAsync` removes a matching queued waiter in the same serialized transaction before ticket
   release and FIFO grant evaluation, so zero-ticket cancellation cannot allocate capacity.
5. Both full Docker-backed provider suites exercise the fresh schema from empty containers.

### 3.3 Integration and section boundary

The integration lane remains 110 passed / 5 explicitly owned skips / 0 failed. `INT_JS_004` and
`INT_JS_014` remain owned by task 9.6, `INT_MN_009` by task 7.5, `INT_HO_011` by task 10.5, and
`INT_JS_018` by the nightly slow suite. No deferred task is marked complete.

No Section 7 package, facade, event-client, management, provider-authoring, or hosting implementation
is added or credited. Task `7.0` remains unchecked.

## 4. Owner-run evidence

| Lane | Result |
|---|---:|
| Solution build | succeeded; 0 warnings / 0 errors |
| Core | 464 passed / 0 failed / 0 skipped |
| Ephemeral | 173 passed / 0 failed / 0 skipped |
| Durable | 332 passed / 0 failed / 0 skipped |
| Hosting | 17 passed / 0 failed / 0 skipped |
| Acceptance | 70 passed / 0 failed / 0 skipped |
| In-memory provider certification | 78 passed / 0 failed / 0 skipped |
| PostgreSQL provider suite | 77 passed / 0 failed / 0 skipped |
| SQL Server provider suite | 63 passed / 0 failed / 0 skipped |
| Focused SQL Server fresh-schema checks | 2 passed / 0 failed / 0 skipped |
| Integration | 110 passed / 0 failed / 5 explicitly owned skips |
| Infrastructure guards | 104 passed / 0 failed / 0 skipped on three consecutive isolated runs |
| Section 6 current-physical drivers | 32 passed / 0 failed |
| Expected-red guards | 0 passed / 61 intentional named failures / 0 skipped |
| Green compile fixtures | passed; exact/product consumers compile; 26 source and 26 package forbidden-member diagnostics; incomplete control rejected |
| Product-authoring ExpectedRed compile set | passed with 0 remaining gaps |
| Package ExpectedRed compile set | nonzero as designed; exactly 8 named Section 7/8 gaps |
| Strict OpenSpec | both active changes valid; all 17 items passed |
| Task accounting | reshape 90/46/136, coordinated 16/0/16, no duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| Whitespace | `git diff --check` exit 0; line-ending notices only |

The complete PostgreSQL, SQL Server, and integration runs were executed sequentially after the final
no-incremental build. A first SQL Server run exposed that `001_initial.sql`, not the later guarded
`003_resource_pools.sql`, owned first creation; the initial schema was corrected, a focused 2/2
regression run passed, and the final full 63/63 run passed. No compatibility migration was restored.

## 5. Minimum independent commands

Run from `X:\Projects\GitHub\Workflow-orca`:

```powershell
docker info --format '{{.ServerVersion}} {{.OSType}}/{{.Architecture}}'
git grep -n -I -E "008_resource_pool_creation_capacity|009_resource_pool_creation_capacity|legacy-db|add column if not exists creation_capacity|set creation_capacity = capacity" -- src tests openspec docs/implementation
dotnet build OrcaCore.slnx --no-restore --no-incremental -p:NuGetAudit=false -v minimal

dotnet test tests/OrcaCore.Providers.PostgreSql.Tests/OrcaCore.Providers.PostgreSql.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Providers.SqlServer.Tests/OrcaCore.Providers.SqlServer.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --no-restore

.\tests\OrcaCore.Core.Tests\bin\Debug\net10.0\OrcaCore.Core.Tests.exe -noColor
.\tests\OrcaCore.Engine.Ephemeral.Tests\bin\Debug\net10.0\OrcaCore.Engine.Ephemeral.Tests.exe -noColor
.\tests\OrcaCore.Engine.Durable.Tests\bin\Debug\net10.0\OrcaCore.Engine.Durable.Tests.exe -noColor
.\tests\OrcaCore.Hosting.Tests\bin\Debug\net10.0\OrcaCore.Hosting.Tests.exe -noColor
.\tests\OrcaCore.Acceptance.Tests\bin\Debug\net10.0\OrcaCore.Acceptance.Tests.exe -noColor
.\tests\OrcaCore.ProviderCertification\bin\Debug\net10.0\OrcaCore.ProviderCertification.exe -noColor

.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -trait "Disposition=Infrastructure" -noColor
.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -trait "Disposition=ExpectedRed" -noColor
.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -method "*Section6Scenario_CurrentPhysicalOwnerHasOneRuntimeRecordedExactDriverAndPassingAssertion*" -noColor

powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed

openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
openspec.cmd validate --all --strict --no-interactive
dotnet list OrcaCore.slnx package --vulnerable --include-transitive --no-restore
git diff --check
```

The `git grep` command must return no matches. ExpectedRed guards and package commands must return
nonzero with exactly the named intentional failures. Run infrastructure three consecutive times and
Docker-backed suites sequentially. Inspect both SQL Server first-create schema resources and the
PostgreSQL initializer; reject any attempt to reintroduce legacy upgrade logic.

## 6. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record provenance, exact
manifest comparison before and after, every command/result, independent source/test derivation, and
`APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to begin task `7.0` in a
later turn; it does not mark task `7.0` complete. Rejection keeps Section 7 blocked.
