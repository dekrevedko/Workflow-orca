# Section 6 ownership-DDL remediation: independent exit re-review verdict

**Date:** 2026-07-30  
**Verdict:** `REJECT`  
**Gate effect:** Section 6 exit is not approved. Task `7.0` remains open and blocked.

## 1. Decision

The live relational schema remediation is correct: PostgreSQL and SQL Server now create resource
ticket and waiter ownership columns directly, the two ownership upgrade migrations are deleted,
the empty-database provider suites are green, and the frozen target contains no ownership upgrade
DDL.

Approval is nevertheless rejected because the new regression guard does not detect the exact
PostgreSQL compatibility DDL that caused the superseding rejection when that DDL is moved to a
different migration filename. That contradicts the rereview request's specific claim that the
guard protects every relational SQL resource and prevents the rejected DDL from returning under
another migration ID.

No reviewed source, test, task, specification, request, manifest, or earlier verdict was edited
during this review. This file is the only review artifact added.

## 2. Frozen target and provenance

| Item | Independently observed |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Frozen manifest entries | 500 |
| Entry classes | 351 modified / 11 deleted / 138 untracked |
| Raw manifest SHA-256 | `69B8D848B26A063770355DB358A15201F0F0115C3609C1CB1D50A2E3818E84FC` |
| Sorted LF status SHA-256 | `5DF7FC42F74E5767A5CDBC6DD80B21F6D595FBBF25AC8CCC3E47B29ACCFA0D1E` |
| Docker engine | 29.6.1, `linux/x86_64` |

Before source conclusions and again after all validation, default
`git status --porcelain=v1` produced the same 500 ordered entries as
`developer-facing-interface-section-06-ownership-ddl-remediation-rereview-dirty-manifest-2026-07-30.txt`.
The post-validation comparison had zero ordered differences and reproduced the declared entry
classes and sorted LF hash.

The manifest intentionally uses Git's default collapsed representation for untracked directories.
An `--untracked-files=all` expansion is not the frozen representation and was not used to decide
target equality.

## 3. Release blocker

### R1 — the all-resource PostgreSQL guard misses the exact rejected `ALTER` syntax

**Severity:** release blocker

The new guard has two different protections:

1. `InfrastructureGuards.cs:44-47` rejects only the two exact deleted paths,
   `007_resource_ownership.sql` and `008_resource_ownership.sql`.
2. `InfrastructureGuards.cs:67-96` enumerates every relational `*.sql` resource, but its
   PostgreSQL needles at lines 78-83 require the contiguous text
   `alter table orcacore_resource_... add column`.

The deleted PostgreSQL migration used this form:

```sql
alter table if exists orcacore_resource_tickets
    add column if not exists fiber_id text null;
```

After applying the guard's own whitespace normalization to the exact deleted file obtained from
`HEAD`, the following independent checks result:

| Check | Result |
|---|---:|
| Exact prior `ALTER TABLE IF EXISTS ... ADD COLUMN IF NOT EXISTS fiber_id` is present | true |
| Current ticket needle `alter table orcacore_resource_tickets add column` matches | false |
| Current waiter needle `alter table orcacore_resource_waiters add column` matches | false |

`IF EXISTS` appears between `ALTER TABLE` and the table name, so both all-resource PostgreSQL
assertions miss the precise content they are intended to prevent. The exact-path assertion catches
the old filename only. Renaming the same rejected content to another migration ID would therefore
leave the focused guard green.

This is not a claim that compatibility DDL remains in the frozen source: a case-insensitive scan of
all 33 non-build PostgreSQL and SQL Server provider files found zero matches for the rejected
ownership migration IDs or ownership `ALTER` patterns. It is a closure failure in the executable
protection specifically offered to discharge the previous blocker.

**Required remediation:** make the all-resource PostgreSQL check recognize the actual grammar,
including optional `IF EXISTS`, optional schema qualification, and optional `IF NOT EXISTS`, for
both ownership tables and both ownership columns. Add a regression that feeds the exact deleted
`007_resource_ownership.sql` content to the scanner under a different filename and proves it is
rejected. Then rerun the gates and refreeze a new immutable target.

## 4. Independent source and test derivation

The implementation portion of the remediation was verified:

- `PostgreSqlResourcePoolStore.cs:59-68` creates ticket `fiber_id` and `scope_id` directly, and
  lines 77-85 do the same for waiters. The initializer contains no ownership `ALTER`.
- SQL Server `001_initial.sql:121-150` creates both columns on both tables in the actual
  empty-database first-create path.
- Retained guarded first-create script `003_resource_pools.sql:11-58` has the same current shape.
- PostgreSQL `007_resource_ownership.sql` and SQL Server `008_resource_ownership.sql` are absent
  from the filesystem and recorded as deletions in the target.
- PostgreSQL's fresh-schema assertion at
  `PostgreSqlResourcePoolStoreCertificationTests.cs:48-68` queries all four ownership columns.
- PostgreSQL's journal assertion at
  `PostgreSqlProviderCertificationTests.cs:62-69` proves migration `007_resource_ownership` was not
  applied.
- SQL Server's fresh-schema assertion at `SqlServerEventStoreTests.cs:108-138` proves
  `creation_capacity` plus all four ownership columns and absence of migration
  `008_resource_ownership`.
- The earlier creation-capacity direct-first-create protection remains present and green.

## 5. Independent validation

| Lane / command | Independent result |
|---|---:|
| Compatibility-DDL scan over provider non-build files | 33 files; 0 matches |
| `dotnet build OrcaCore.slnx --no-restore --no-incremental -p:NuGetAudit=false -v minimal` | succeeded; 0 warnings / 0 errors |
| Core direct runner | 464 passed / 0 failed / 0 skipped |
| Ephemeral direct runner | 173 passed / 0 failed / 0 skipped |
| Durable direct runner | 332 passed / 0 failed / 0 skipped |
| Hosting direct runner | 17 passed / 0 failed / 0 skipped |
| Acceptance direct runner | 70 passed / 0 failed / 0 skipped |
| In-memory provider certification direct runner | 78 passed / 0 failed / 0 skipped |
| PostgreSQL provider suite | 78 passed / 0 failed / 0 skipped |
| SQL Server provider suite | 63 passed / 0 failed / 0 skipped |
| Integration suite | 110 passed / 0 failed / 5 explicitly owned skips |
| Focused no-compatibility-DDL guard | 1 passed / 0 failed / 0 skipped |
| Infrastructure guards, isolated run 1 | 105 passed / 0 failed / 0 skipped |
| Infrastructure guards, isolated run 2 | 105 passed / 0 failed / 0 skipped |
| Infrastructure guards, isolated run 3 | 105 passed / 0 failed / 0 skipped |
| Section 6 current-physical drivers | 32 passed / 0 failed |
| ExpectedRed guards | exit 1 as designed; 0 passed / 61 intentional failures / 0 skipped |
| Green compile fixtures | exact and product consumers compiled; 26 source and 26 package forbidden-member diagnostics; incomplete control rejected |
| Product-authoring ExpectedRed compile set | exit 0; 0 remaining gaps |
| Package ExpectedRed compile set | exit 1 as designed; exactly 8 named Section 7/8 gaps |
| Strict OpenSpec, reshape | valid |
| Strict OpenSpec, runtime governance | valid |
| Strict OpenSpec, all | 17 passed / 0 failed |
| Task accounting, reshape | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Task accounting, runtime governance | 16 complete / 0 pending / 16 total; 0 duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| `git diff --check` | exit 0; line-ending notices only |

The SQL Server suite was run sequentially through the built xUnit executable with
`-parallel none`; it exercised the same 63-test assembly while avoiding parallel container
lifecycle interference. PostgreSQL and integration were also run sequentially. ExpectedRed and
package-red nonzero exits were classified separately and were not counted as passing tests.

## 6. Gate disposition

The current database schema defect is remediated, but the promised regression protection is not
closed. Section 6 therefore remains rejected and task `7.0` remains unchecked and blocked.

This verdict does not authorize Section 7 implementation and does not supersede or modify earlier
immutable review evidence.
