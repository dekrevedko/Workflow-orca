# Section 6 ownership-DDL scanner remediation: independent exit re-review request

**Date:** 2026-07-30  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** Section 6 exit and the later start of task `7.0`

This request supersedes the target rejected by
`developer-facing-interface-section-06-ownership-ddl-remediation-independent-rereview-verdict-2026-07-30.md`.
It preserves that verdict and every earlier request, manifest, approval, and rejection as immutable
evidence.

The rejection confirmed the live PostgreSQL and SQL Server schemas were correctly remediated but
found that the all-resource guard missed the exact former PostgreSQL syntax
`ALTER TABLE IF EXISTS ... ADD COLUMN IF NOT EXISTS ...` when moved under a different filename.
This target replaces the substring needles with a filename-independent ownership-DDL scanner and
adds an executable regression containing the exact deleted PostgreSQL migration under a renamed
path.

Task `7.0` remains open and blocked until an independent reviewer approves this exact refrozen
target without a release blocker.

## 1. Immutable review history

Preserve and read:

- `developer-facing-interface-section-06-ownership-ddl-remediation-rereview-request-2026-07-30.md`;
- `developer-facing-interface-section-06-ownership-ddl-remediation-rereview-dirty-manifest-2026-07-30.txt`;
- `developer-facing-interface-section-06-ownership-ddl-remediation-independent-rereview-verdict-2026-07-30.md`;
- `developer-facing-interface-section-06-greenfield-schema-remediation-compatibility-ddl-superseding-verdict-2026-07-30.md`;
- `developer-facing-interface-section-06-greenfield-schema-remediation-independent-rereview-verdict-2026-07-30.md`;
- `developer-facing-interface-section-06-relational-provider-remediation-rereview-request-2026-07-30.md`;
- `developer-facing-interface-section-06-docker-provider-gate-superseding-verdict-2026-07-29.md`; and
- all earlier Section 6 requests, manifests, and verdicts named by those artifacts.

The latest scanner rejection remains authoritative until this new target receives an independent
approval.

## 2. Refrozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Review target | `HEAD` plus every entry in the self-inclusive scanner-remediation manifest |
| Self-inclusive porcelain entries | 503 |
| Entry classes | 351 modified / 11 deleted / 141 untracked |
| Raw-manifest SHA-256 | `F9BE53C6EB3E730704EAEA11FCD1008378CF3456D0FC46A5F3EE31222F1AF6C3` |
| LF-normalized sorted-status SHA-256 | `7269113E31C1F093162AC3A2633A1F172707216827566DBE8BA63242CE03EEFD` |
| Reshape tasks | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Runtime-governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |
| Docker engine | 29.6.1, `linux/x86_64` |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-06-ownership-ddl-scanner-remediation-rereview-dirty-manifest-2026-07-30.txt`.
Reproduce it before reading conclusions and again after validation. Any path/status drift invalidates
the review.

The manifest intentionally uses Git's default collapsed representation for untracked directories.
An `--untracked-files=all` expansion is not the frozen representation.

## 3. Remediation claims to re-derive

### 3.1 Scanner grammar and scope

1. `InfrastructureGuards.OwnershipCompatibilityAlter` recognizes ownership-column `ALTER TABLE`
   statements for both `orcacore_resource_tickets` and `orcacore_resource_waiters`.
2. Matching is case-insensitive and handles multiline whitespace, optional PostgreSQL `IF EXISTS`,
   `ONLY`, schema qualification, optional `COLUMN`, optional `IF NOT EXISTS`, quoted PostgreSQL
   identifiers, and bracketed SQL Server identifiers.
3. The live guard applies the same scanner to `PostgreSqlResourcePoolStore.cs` and every `*.sql`
   resource in both relational provider migration trees. Detection does not depend on a migration
   ID or filename.
4. Scanner findings include the source path so a future failure is directly actionable.
5. The scanner remains deliberately narrow to the two resource-ownership tables and the
   `fiber_id`/`scope_id` columns; unrelated schema evolution does not fail this greenfield guard.

### 3.2 Exact rejected-content regression

`RelationalResourcePoolOwnership_ScannerRejectsRenamedFormerPostgreSqlMigration` feeds the exact
deleted `007_resource_ownership.sql` body to the scanner as
`Migrations/999_renamed_resource_ownership.sql` and requires all four ownership statements to be
reported. The same regression proves schema-qualified, quoted, `ONLY`, and bracketed forms cannot
bypass the scanner.

The regression and the live repository scan are separate tests. Both must pass.

### 3.3 Preserved greenfield schema and Section 6 boundary

1. PostgreSQL and SQL Server still create ticket/waiter `fiber_id` and `scope_id` directly in
   first-create schema definitions.
2. PostgreSQL `007_resource_ownership.sql` and SQL Server `008_resource_ownership.sql` remain
   deleted.
3. A source scan over all 33 non-build PostgreSQL and SQL Server provider files reports zero
   compatibility-DDL matches.
4. Fresh-container PostgreSQL, SQL Server, and integration suites remain green.
5. No Section 7 package, facade, event-client, management, provider-authoring, or hosting work is
   added or credited. Task `7.0` remains unchecked.

## 4. Owner-run evidence

| Lane | Result |
|---|---:|
| Focused live scan plus renamed-content regression | 2 passed / 0 failed / 0 skipped |
| Solution build | succeeded; 0 warnings / 0 errors |
| Core | 464 passed / 0 failed / 0 skipped |
| Ephemeral | 173 passed / 0 failed / 0 skipped |
| Durable | 332 passed / 0 failed / 0 skipped |
| Hosting | 17 passed / 0 failed / 0 skipped |
| Acceptance | 70 passed / 0 failed / 0 skipped |
| In-memory provider certification | 78 passed / 0 failed / 0 skipped |
| PostgreSQL provider suite | 78 passed / 0 failed / 0 skipped |
| SQL Server provider suite | 63 passed / 0 failed / 0 skipped |
| Integration | 110 passed / 0 failed / 5 explicitly owned skips |
| Infrastructure guards | 106 passed / 0 failed / 0 skipped on three consecutive isolated runs |
| Section 6 current-physical drivers | 32 passed / 0 failed |
| Expected-red guards | 0 passed / 61 intentional named failures / 0 skipped |
| Green compile fixtures | passed; exact/product consumers compile; 26 source and 26 package forbidden-member diagnostics; incomplete control rejected |
| Product-authoring ExpectedRed compile set | passed with 0 remaining gaps |
| Package ExpectedRed compile set | nonzero as designed; exactly 8 named Section 7/8 gaps |
| Strict OpenSpec | both active changes valid; all 17 items passed |
| Task accounting | reshape 90/46/136, coordinated 16/0/16, no duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| Compatibility-DDL source scan | 0 matches across 33 non-build provider files |
| Whitespace | `git diff --check` exit 0; line-ending notices only |

The complete PostgreSQL and SQL Server suites were run sequentially through their built xUnit
runners to avoid container lifecycle interference. Their successful captured runs took 499.5 and
622.8 seconds respectively. Integration completed in 49.8 seconds. Earlier five-minute shell
capture attempts were discarded before these uninterrupted, fully captured green runs.

## 5. Minimum independent commands

Run from `X:\Projects\GitHub\Workflow-orca`:

```powershell
docker info --format '{{.ServerVersion}} {{.OSType}}/{{.Architecture}}'

$files = Get-ChildItem `
    src/OrcaCore.Providers.PostgreSql,src/OrcaCore.Providers.SqlServer `
    -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$files | Select-String -CaseSensitive:$false -Pattern `
    '007_resource_ownership|008_resource_ownership|add column if not exists (fiber_id|scope_id)|alter table.*orcacore_resource_(tickets|waiters).*add.*(fiber_id|scope_id)'

dotnet build OrcaCore.slnx --no-restore --no-incremental -p:NuGetAudit=false -v minimal

.\tests\OrcaCore.Core.Tests\bin\Debug\net10.0\OrcaCore.Core.Tests.exe -noColor
.\tests\OrcaCore.Engine.Ephemeral.Tests\bin\Debug\net10.0\OrcaCore.Engine.Ephemeral.Tests.exe -noColor
.\tests\OrcaCore.Engine.Durable.Tests\bin\Debug\net10.0\OrcaCore.Engine.Durable.Tests.exe -noColor
.\tests\OrcaCore.Hosting.Tests\bin\Debug\net10.0\OrcaCore.Hosting.Tests.exe -noColor
.\tests\OrcaCore.Acceptance.Tests\bin\Debug\net10.0\OrcaCore.Acceptance.Tests.exe -noColor
.\tests\OrcaCore.ProviderCertification\bin\Debug\net10.0\OrcaCore.ProviderCertification.exe -noColor

.\tests\OrcaCore.Providers.PostgreSql.Tests\bin\Debug\net10.0\OrcaCore.Providers.PostgreSql.Tests.exe -parallel none -noColor
.\tests\OrcaCore.Providers.SqlServer.Tests\bin\Debug\net10.0\OrcaCore.Providers.SqlServer.Tests.exe -parallel none -noColor
.\tests\OrcaCore.Integration.Tests\bin\Debug\net10.0\OrcaCore.Integration.Tests.exe -parallel none -noColor

.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -method "*RelationalResourcePoolOwnership*" -noColor
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

The compatibility scan must return no matches. ExpectedRed guards and the package-red command must
return nonzero with exactly the named intentional failures. Run infrastructure three consecutive
times and Docker-backed suites sequentially. Independently mutate the scanner input in memory; do
not edit the frozen tree to test grammar variants.

## 6. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record provenance, exact
manifest comparison before and after, every command/result, independent source and test derivation,
and `APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to begin task `7.0` in a
later turn; it does not mark task `7.0` complete. Rejection keeps Section 7 blocked.
