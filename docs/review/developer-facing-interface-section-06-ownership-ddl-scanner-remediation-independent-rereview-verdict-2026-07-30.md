# Section 6 ownership-DDL scanner remediation: independent exit re-review verdict

**Date:** 2026-07-30  
**Verdict:** `APPROVE`  
**Gate effect:** Section 6 exit is approved for this exact frozen target. Task `7.0` may be
started in a later implementation turn, but remains unchecked and was not started by this review.

## 1. Decision

No release blocker was found.

The target closes the scanner defect recorded in
`developer-facing-interface-section-06-ownership-ddl-remediation-independent-rereview-verdict-2026-07-30.md`.
The live relational schemas remain greenfield first-create schemas, the rejected ownership upgrade
migrations remain deleted, and the filename-independent scanner now detects the exact former
PostgreSQL migration content when supplied under a different migration name.

No reviewed source, test, task, specification, plan, request, manifest, or earlier verdict was
edited during this review. This file is the only review artifact added.

## 2. Frozen target and provenance

| Item | Independently observed |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Frozen manifest entries | 503 |
| Entry classes | 351 modified / 11 deleted / 141 untracked |
| Raw manifest SHA-256 | `F9BE53C6EB3E730704EAEA11FCD1008378CF3456D0FC46A5F3EE31222F1AF6C3` |
| Sorted LF status SHA-256 | `7269113E31C1F093162AC3A2633A1F172707216827566DBE8BA63242CE03EEFD` |
| Docker engine | 29.6.1, `linux/x86_64` |

Before reading the request's conclusions and again after all validation, default
`git status --porcelain=v1` produced the same 503 ordered entries as
`developer-facing-interface-section-06-ownership-ddl-scanner-remediation-rereview-dirty-manifest-2026-07-30.txt`.
Both comparisons had zero ordered differences and reproduced the declared entry classes and
sorted LF hash.

The manifest intentionally uses Git's default collapsed representation for untracked directories.
An `--untracked-files=all` expansion is not the frozen representation and was not used to decide
target equality.

Compared with the previous 500-entry ownership-DDL target, the three new status entries are the
preserved rejection, this rereview request, and this rereview manifest. Earlier evidence remains
immutable.

## 3. Independent scanner derivation

### 3.1 Grammar and live scope

The scanner at `InfrastructureGuards.cs:13-40` is case-insensitive and recognizes:

- optional PostgreSQL `IF EXISTS` and `ONLY`;
- one optional quoted, bracketed, or unquoted schema qualifier;
- exact ticket or waiter table names in quoted, bracketed, or unquoted form;
- PostgreSQL's optional post-table `*`;
- optional `COLUMN` and `IF NOT EXISTS`; and
- exact `fiber_id` or `scope_id` columns in quoted, bracketed, or unquoted form.

The production-tree guard at `InfrastructureGuards.cs:68-112` applies the scanner to
`PostgreSqlResourcePoolStore.cs` and every `*.sql` file in both relational migration directories.
The filename is used only in the diagnostic returned by
`FindOwnershipCompatibilityAlters`; detection does not depend on it.

A separate case-insensitive source scan covered all 33 non-build PostgreSQL and SQL Server provider
files and found zero rejected migration IDs or ownership compatibility `ALTER` patterns.

### 3.2 Exact former migration regression

The regression at `InfrastructureGuards.cs:115-152`:

- labels its input `Migrations/999_renamed_resource_ownership.sql`;
- contains the same four statements as deleted
  `src/OrcaCore.Providers.PostgreSql/Migrations/007_resource_ownership.sql`;
- requires exactly four scanner findings;
- requires every finding to carry the renamed source path; and
- separately exercises schema-qualified, quoted, `ONLY`, bracketed, optional-`COLUMN`, and
  optional-`IF NOT EXISTS` forms.

An independent whitespace-normalized comparison against the deleted file obtained from `HEAD`
confirmed the checked-in four-statement test body is the exact former migration content.

### 3.3 Independent in-memory adversarial check

Without editing the frozen tree, the scanner pattern was exercised against:

| Input group | Result |
|---|---:|
| Exact deleted PostgreSQL migration | 4 findings |
| Five supported positive variants | 5 recognized / 0 missed |
| Five direct-create or unrelated near-misses | 0 false findings |

The positive variants included multiline uppercase tokens, `IF EXISTS`, `ONLY`, unquoted and
quoted schema qualification, bracketed SQL Server identifiers, optional `COLUMN`, optional
`IF NOT EXISTS`, and the PostgreSQL table `*`. Negative controls covered direct creation, another
table, another column, a table-name suffix, and a column-name suffix.

This independently closes the exact `ALTER TABLE IF EXISTS` blind spot that caused the prior
rejection.

## 4. Preserved schema and phase boundary

- PostgreSQL still creates ticket and waiter `fiber_id` and `scope_id` directly in
  `PostgreSqlResourcePoolStore.InitializeAsync`.
- SQL Server `001_initial.sql` and retained guarded first-create script
  `003_resource_pools.sql` still create all four ownership columns directly.
- PostgreSQL `007_resource_ownership.sql` and SQL Server `008_resource_ownership.sql` remain
  deleted.
- Fresh-container provider tests still prove direct ownership-column creation and absence of the
  compatibility migration journal entries.
- Task `7.0` remains exactly `- [ ] 7.0` in the active reshape task ledger.
- No Section 7 package, facade, event-client, management, provider-authoring, or hosting
  implementation was added or credited.

## 5. Independent validation

| Lane / command | Independent result |
|---|---:|
| Docker engine query | 29.6.1, `linux/x86_64` |
| Compatibility-DDL scan over provider non-build files | 33 files; 0 matches |
| `dotnet build OrcaCore.slnx --no-restore --no-incremental -p:NuGetAudit=false -v minimal` | succeeded; 0 warnings / 0 errors |
| Focused live scan and renamed-content guards | 2 passed / 0 failed / 0 skipped |
| Core direct runner | 464 passed / 0 failed / 0 skipped |
| Ephemeral direct runner | 173 passed / 0 failed / 0 skipped |
| Durable direct runner | 332 passed / 0 failed / 0 skipped |
| Hosting direct runner | 17 passed / 0 failed / 0 skipped |
| Acceptance direct runner | 70 passed / 0 failed / 0 skipped |
| In-memory provider certification direct runner | 78 passed / 0 failed / 0 skipped |
| PostgreSQL provider suite, authorized Docker rerun | 78 passed / 0 failed / 0 skipped |
| SQL Server provider suite | 63 passed / 0 failed / 0 skipped |
| Integration suite | 110 passed / 0 failed / 5 explicitly owned skips |
| Infrastructure guards, isolated run 1 | 106 passed / 0 failed / 0 skipped |
| Infrastructure guards, isolated run 2 | 106 passed / 0 failed / 0 skipped |
| Infrastructure guards, isolated run 3 | 106 passed / 0 failed / 0 skipped |
| Section 6 current-physical drivers | 32 passed / 0 failed |
| ExpectedRed guards | exit 1 as designed; 0 passed / 61 intentional failures / 0 skipped |
| Green compile fixtures | exact and product consumers compiled; 26 source and 26 package forbidden-member diagnostics; incomplete control rejected |
| Product-authoring ExpectedRed compile set | exit 0; 0 remaining gaps |
| Package ExpectedRed compile set | exit 1 as designed; exactly 8 named Section 7/8 gaps |
| Strict OpenSpec, reshape | valid |
| Strict OpenSpec, runtime governance | valid |
| Strict OpenSpec, all | 17 passed / 0 failed |
| Task accounting, reshape | 90 complete / 46 pending / 136 total; 0 duplicate IDs; task `7.0` unchecked |
| Task accounting, runtime governance | 16 complete / 0 pending / 16 total; 0 duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| `git diff --check` | exit 0; line-ending notices only |

The PostgreSQL and SQL Server suites were run sequentially with xUnit parallelism disabled.
PostgreSQL completed in 200.632 seconds and SQL Server in 492.855 seconds. Integration completed in
48 seconds.

The first sandboxed PostgreSQL attempt could not access Docker's Windows named pipe and produced
74 infrastructure failures before test execution. It was discarded as an environment result. The
same built 78-test assembly was immediately rerun with authorized Docker access and passed
78/78. Docker engine discovery, SQL Server, and integration were green.

ExpectedRed and package-red nonzero exits were classified separately and were not counted as
passing tests.

## 6. Gate disposition

The prior ownership-DDL scanner blocker is discharged. Section 6 exit is approved for the exact
503-entry frozen target.

This approval authorizes the implementation owner to begin task `7.0` in a later turn. It does not
mark task `7.0` complete, does not itself begin Section 7, and does not modify any historical
request, manifest, rejection, or approval.
