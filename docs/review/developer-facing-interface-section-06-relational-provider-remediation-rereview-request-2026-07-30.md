# Section 6 relational-provider remediation: independent exit re-review request

**Date:** 2026-07-30  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** Section 6 exit and the later start of task `7.0`

This request supersedes the rejected Docker-provider target without modifying any prior request,
manifest, approval, rejection, or superseding verdict. The authoritative rejection is
`developer-facing-interface-section-06-docker-provider-gate-superseding-verdict-2026-07-29.md`.
Task `7.0` remains open and blocked until an independent reviewer approves this exact refrozen
target without a release blocker.

## 1. Immutable review history

Preserve and read:

- `developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-request-2026-07-29.md`;
- `developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-dirty-manifest-2026-07-29.txt`;
- `developer-facing-interface-section-06-ancestor-terminal-remediation-independent-rereview-verdict-2026-07-29.md`;
- `developer-facing-interface-section-06-docker-provider-gate-superseding-verdict-2026-07-29.md`; and
- the earlier Section 6 request, manifest, and two independent rejection verdicts named by the
  ancestor-terminal request.

The ancestor-terminal remediation remains part of this target. The superseding Docker verdict
reopened Section 6 because PostgreSQL and SQL Server each failed the same three shared
`ResourcePoolStoreCertificationTests`; it also required explicit disposition of four stale
integration failures.

## 2. Refrozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Review target | `HEAD` plus every entry in the self-inclusive remediation manifest |
| Self-inclusive porcelain entries | 493 |
| Entry classes | 350 modified / 9 deleted / 134 untracked |
| Raw-manifest SHA-256 | `77303C4AE9D4999B2B04B999F2A7FDD280AFB1103FB62AF2CA20FB52FF469C73` |
| LF-normalized sorted-status SHA-256 | `5B3F83DEF7F0A18FB45786A56D796B07F952194DC880D1F55245068511859272` |
| Reshape tasks | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Runtime-governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |
| Docker engine | 29.6.1, `linux/x86_64` |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-06-relational-provider-remediation-rereview-dirty-manifest-2026-07-30.txt`.
Reproduce it before reading conclusions and again after validation. Any path/status drift
invalidates the review.

## 3. Remediation claims to re-derive

### 3.1 Relational resource-pool contract

1. PostgreSQL and SQL Server reject resize capacity less than one before database mutation.
2. An update that names no configured pool throws `ResourcePoolNotConfiguredException` rather than
   silently succeeding.
3. Relational schemas now persist immutable `creation_capacity` separately from mutable current
   `capacity`.
4. PostgreSQL migration `008_resource_pool_creation_capacity` and SQL Server migration
   `009_resource_pool_creation_capacity` add/backfill the immutable value for a legacy one-column
   row without changing current capacity; existing migration tests execute that upgrade path.
5. Re-upsert with the original creation definition is an idempotent no-op that preserves current
   capacity. A changed creation capacity or review duration throws `InvalidOperationException`.
   PostgreSQL enforces this atomically in its conflict predicate; SQL Server validates under its
   serialized provider lock.
6. `ReleaseAsync` deletes the matching queued waiter in the same serialized transaction before
   ticket release and FIFO grant evaluation, so zero-ticket cancellation cannot allocate capacity.

### 3.2 Integration-test disposition

The prior unexplained 4-fail / 110-pass / 1-skip gate is now 0-fail / 110-pass / 5-skip.
Each new skip records its owner and remains executable source for its later task:

- `INT_JS_004` and `INT_JS_014`: generic external jobs and pause/resume are outside v1; task 9.6
  owns any future replacement;
- `INT_MN_009`: provider-backed two-host `StartOrGet` idempotency is blocked on task 7.5;
- `INT_HO_011`: task 10.5 must replace the raw-command fixture with a supported public hosting
  journey;
- `INT_JS_018`: unchanged one-hour soak deferred to the nightly slow suite.

`CLAUDE.md` records this exact baseline. No deferred task is marked complete.

### 3.3 Section boundary

The earlier ancestor-terminal driver and ownership derivation remain unchanged. No Section 7
package, facade, event-client, management, provider-authoring, or hosting implementation is added or
credited. Task `7.0` is still unchecked.

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
| Relational resource-pool fixtures | PostgreSQL 19/19; SQL Server 22/22 |
| Legacy-schema upgrade regressions | PostgreSQL 1/1; SQL Server 1/1 |
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

The final complete PostgreSQL, SQL Server, and integration runs were executed sequentially after the
last no-incremental build and after the migration regressions were added.

## 5. Minimum independent commands

Run from `X:\Projects\GitHub\Workflow-orca`:

```powershell
docker info --format '{{.ServerVersion}} {{.OSType}}/{{.Architecture}}'
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

The ExpectedRed guard and package commands must return nonzero with exactly the named intentional
failures. Run the infrastructure lane three consecutive times. Run the Docker-backed suites
sequentially, not concurrently. Inspect the migration tests and execute the three originally failing
shared certification methods directly if any count or behavior differs.

## 6. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record provenance, exact
manifest comparison before and after, every command/result, independent source and test derivation,
and `APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to begin task `7.0` in a
later turn; it does not mark task `7.0` complete. Rejection keeps Section 7 blocked.
