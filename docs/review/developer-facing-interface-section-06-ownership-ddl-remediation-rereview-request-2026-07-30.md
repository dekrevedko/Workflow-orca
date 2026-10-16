# Section 6 ownership-DDL remediation: independent exit re-review request

**Date:** 2026-07-30  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** Section 6 exit and the later start of task `7.0`

This request supersedes the target rejected by
`developer-facing-interface-section-06-greenfield-schema-remediation-compatibility-ddl-superseding-verdict-2026-07-30.md`.
It does not modify that verdict, the concurrent approval it superseded, or any earlier request,
manifest, approval, or rejection.

The rejection found one release blocker: relational resource ownership still arrived through
compatibility `ALTER` scripts even though OrcaCore is greenfield and preserves no provisional
durable schema. This target moves `fiber_id` and `scope_id` into every first-create resource ticket
and waiter definition, deletes both ownership upgrade migrations, removes PostgreSQL's redundant
initializer `ALTER`s, and adds executable protections against their return.

Task `7.0` remains open and blocked until an independent reviewer approves this exact refrozen
target without a release blocker.

## 1. Immutable review history

Preserve and read:

- `developer-facing-interface-section-06-greenfield-schema-remediation-rereview-request-2026-07-30.md`;
- `developer-facing-interface-section-06-greenfield-schema-remediation-rereview-dirty-manifest-2026-07-30.txt`;
- `developer-facing-interface-section-06-greenfield-schema-remediation-independent-rereview-verdict-2026-07-30.md`;
- `developer-facing-interface-section-06-greenfield-schema-remediation-compatibility-ddl-superseding-verdict-2026-07-30.md`;
- `developer-facing-interface-section-06-relational-provider-remediation-rereview-request-2026-07-30.md`;
- `developer-facing-interface-section-06-docker-provider-gate-superseding-verdict-2026-07-29.md`; and
- all earlier Section 6 requests, manifests, and verdicts named by those artifacts.

The concurrent approval is historical evidence only. The compatibility-DDL superseding rejection
remains authoritative until this new target receives an independent approval.

## 2. Refrozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Review target | `HEAD` plus every entry in the self-inclusive ownership-DDL manifest |
| Self-inclusive porcelain entries | 500 |
| Entry classes | 351 modified / 11 deleted / 138 untracked |
| Raw-manifest SHA-256 | `69B8D848B26A063770355DB358A15201F0F0115C3609C1CB1D50A2E3818E84FC` |
| LF-normalized sorted-status SHA-256 | `5DF7FC42F74E5767A5CDBC6DD80B21F6D595FBBF25AC8CCC3E47B29ACCFA0D1E` |
| Reshape tasks | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Runtime-governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |
| Docker engine | 29.6.1, `linux/x86_64` |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-06-ownership-ddl-remediation-rereview-dirty-manifest-2026-07-30.txt`.
Reproduce it before reading conclusions and again after validation. Any path/status drift invalidates
the review.

## 3. Remediation claims to re-derive

### 3.1 Current schema is created directly

1. `PostgreSqlResourcePoolStore.InitializeAsync` creates ticket and waiter `fiber_id`/`scope_id`
   columns directly and contains no resource ownership `ALTER`.
2. SQL Server `001_initial.sql`, which owns actual empty-database first creation, creates the two
   ownership columns on both resource tickets and waiters.
3. SQL Server's retained guarded first-create definition in `003_resource_pools.sql` has the same
   current shape.
4. PostgreSQL `007_resource_ownership.sql` and SQL Server `008_resource_ownership.sql` are deleted.
5. No relational SQL resource contains an ownership `ALTER` under another migration ID.
6. The earlier creation/current-capacity fix remains direct-first-create only; no capacity upgrade
   or backfill path has returned.

### 3.2 Executable closure

1. PostgreSQL's fresh resource-pool test queries `information_schema.columns` and proves all four
   ownership columns exist after direct initialization.
2. PostgreSQL's workflow migration test proves `007_resource_ownership` is absent from the journal.
3. SQL Server's fresh-schema test proves `creation_capacity` plus all four ownership columns exist
   and that `008_resource_ownership` is absent from the journal.
4. Infrastructure guard
   `RelationalResourcePoolOwnership_UsesGreenfieldFirstCreateSchemasOnly` checks deleted migration
   paths, PostgreSQL initializer source, both SQL Server first-create definitions, and every
   relational SQL resource for the forbidden ownership `ALTER` patterns.
5. Both complete provider suites start empty Docker containers and exercise the resulting schema.

### 3.3 Preserved Section 6 behavior and boundary

Resize validation, typed unknown-pool failure, immutable creation-definition agreement, current
capacity preservation, and transactional queued-waiter cancellation remain unchanged and green.
The integration lane remains 110 passed / 5 explicitly owned skips / 0 failed.

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
| PostgreSQL focused ownership/migration checks | 2 passed / 0 failed / 0 skipped |
| SQL Server focused fresh-schema check | 1 passed / 0 failed / 0 skipped |
| PostgreSQL provider suite | 78 passed / 0 failed / 0 skipped |
| SQL Server provider suite | 63 passed / 0 failed / 0 skipped |
| Integration | 110 passed / 0 failed / 5 explicitly owned skips |
| Focused no-compatibility-DDL guard | 1 passed / 0 failed / 0 skipped |
| Infrastructure guards | 105 passed / 0 failed / 0 skipped on three consecutive isolated runs |
| Section 6 current-physical drivers | 32 passed / 0 failed |
| Expected-red guards | 0 passed / 61 intentional named failures / 0 skipped |
| Green compile fixtures | passed; exact/product consumers compile; 26 source and 26 package forbidden-member diagnostics; incomplete control rejected |
| Product-authoring ExpectedRed compile set | passed with 0 remaining gaps |
| Package ExpectedRed compile set | nonzero as designed; exactly 8 named Section 7/8 gaps |
| Strict OpenSpec | both active changes valid; all 17 items passed |
| Task accounting | reshape 90/46/136, coordinated 16/0/16, no duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| Compatibility-DDL scan | 0 matches across non-build PostgreSQL and SQL Server provider files |
| Whitespace | `git diff --check` exit 0; line-ending notices only |

Provider suites and integration were executed sequentially after the schema changes and a complete
no-incremental build. The source guard was then strengthened to scan every SQL resource rather than
only the two rejected filenames; the focused guard and the complete 105-test infrastructure lane
passed again, followed by a final complete no-incremental solution build.

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
dotnet test tests/OrcaCore.Providers.PostgreSql.Tests/OrcaCore.Providers.PostgreSql.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Providers.SqlServer.Tests/OrcaCore.Providers.SqlServer.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --no-restore

.\tests\OrcaCore.Core.Tests\bin\Debug\net10.0\OrcaCore.Core.Tests.exe -noColor
.\tests\OrcaCore.Engine.Ephemeral.Tests\bin\Debug\net10.0\OrcaCore.Engine.Ephemeral.Tests.exe -noColor
.\tests\OrcaCore.Engine.Durable.Tests\bin\Debug\net10.0\OrcaCore.Engine.Durable.Tests.exe -noColor
.\tests\OrcaCore.Hosting.Tests\bin\Debug\net10.0\OrcaCore.Hosting.Tests.exe -noColor
.\tests\OrcaCore.Acceptance.Tests\bin\Debug\net10.0\OrcaCore.Acceptance.Tests.exe -noColor
.\tests\OrcaCore.ProviderCertification\bin\Debug\net10.0\OrcaCore.ProviderCertification.exe -noColor

.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -method "*RelationalResourcePoolOwnership_UsesGreenfieldFirstCreateSchemasOnly*" -noColor
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

The compatibility scan must return no matches. ExpectedRed guards and package commands must return
nonzero with exactly the named intentional failures. Run infrastructure three consecutive times and
Docker-backed suites sequentially. Inspect the actual first-create table definitions, not merely the
final empty-container schema or migration journal.

## 6. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record provenance, exact
manifest comparison before and after, every command/result, independent source and test derivation,
and `APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to begin task `7.0` in a
later turn; it does not mark task `7.0` complete. Rejection keeps Section 7 blocked.
