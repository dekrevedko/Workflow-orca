# Developer-facing interface — Section 6 greenfield-schema remediation: compatibility-DDL superseding verdict

**Date:** 2026-07-30  
**Reviewer role:** independent exit re-reviewer (Section 6 exit and authorization of task `7.0`)  
**Request under review:** `developer-facing-interface-section-06-greenfield-schema-remediation-rereview-request-2026-07-30.md`  
**Manifest under review:** `developer-facing-interface-section-06-greenfield-schema-remediation-rereview-dirty-manifest-2026-07-30.txt`  
**Supersedes:** `developer-facing-interface-section-06-greenfield-schema-remediation-independent-rereview-verdict-2026-07-30.md` (`APPROVE`)

## Verdict

**REJECT — one release-blocking greenfield-schema finding.**

Section 6 does not exit. Task `7.0` remains open and blocked.

The target correctly removes the newly proposed `creation_capacity` upgrade scripts and makes the
new capacity column part of each provider's first-created resource-pool table. It does not,
however, satisfy the governing greenfield rule that the product must not carry compatibility DDL.
Section 6 ownership columns still depend on, or are accompanied by, compatibility `ALTER` paths in
both relational providers.

This verdict preserves the concurrent approval artifact as immutable evidence. It supersedes that
approval because the approval classified SQL Server migration `008_resource_ownership.sql` as
"unrelated" and did not inspect the ownership ALTER paths. Resource ownership is a Section 6
resource-pool concern and is part of the same greenfield schema target.

## 1. Review provenance and concurrent-artifact disclosure

The submitted target initially reproduced exactly:

| Item | Independently observed |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline `8c2dd712...` | confirmed ancestor of `HEAD` |
| Porcelain entries | 493 |
| Entry classes | 350 modified / 9 deleted / 134 untracked |
| Ordered manifest comparison | exact |
| Raw-manifest SHA-256 | `7D4D7BABF1A15AAA8484355A3ABFDFFAF08F3CDFC61527CB104A0D4022993153` |
| LF-normalized sorted-status SHA-256 | `16BA0410267480C5B1E785C5A2FE259F9F5C0B3F3417A3FDA352BB2B5A4D1531` |

After validation, the working tree contained one additional untracked path:

`docs/review/developer-facing-interface-section-06-greenfield-schema-remediation-independent-rereview-verdict-2026-07-30.md`

That file was created at 11:17 local while this review was running and records `APPROVE`. Excluding
only that review artifact, `git status --porcelain=v1` still matches all 493 frozen manifest lines
exactly and in order. No reviewed source, test, task, spec, plan, request, manifest, or prior verdict
drifted during this review. Creating this superseding verdict intentionally adds one more untracked
review artifact after that comparison.

The concurrent approval is not edited. Its section 3.1 explicitly calls
`008_resource_ownership.sql` an "unrelated pre-existing migration." The finding below explains why
that classification cannot support Section 6 exit under the newly declared greenfield constraint.

## 2. Release-blocking finding

### R1 — Section 6 still carries relational compatibility DDL instead of one current first-created schema

**Severity:** release blocker

The active OpenSpec change is unambiguous:

- `openspec/changes/reshape-developer-facing-interfaces/proposal.md:3` says OrcaCore has no
  compatibility obligations.
- `proposal.md:60` says implementation **SHALL NOT add compatibility shims**.
- `openspec/changes/reshape-developer-facing-interfaces/design.md:26` excludes preserving old
  durable data.
- `design.md:438` directs rollback by recreating development stores because no durable-data
  compatibility contract exists.

The request applies that rule to this refreeze and claims that no compatibility `ALTER` remains.
The source contradicts it:

1. **PostgreSQL retains two ownership upgrade paths.**

   - `src/OrcaCore.Providers.PostgreSql/Migrations/007_resource_ownership.sql:1-11` consists entirely
     of four `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` operations for `fiber_id` and `scope_id`.
   - `src/OrcaCore.Providers.PostgreSql/PostgreSqlResourcePoolStore.cs:59-92` already creates those
     four columns in fresh ticket/waiter tables, then redundantly executes the same four compatibility
     ALTERs at lines 89-92.

   The initializer ALTERs have no purpose for a fresh database. They exist only to retrofit an older
   provisional table shape.

2. **SQL Server's current first-create schema still requires an ownership upgrade migration.**

   - `src/OrcaCore.Providers.SqlServer/Migrations/001_initial.sql:121-150` creates resource ticket
     and waiter tables without `fiber_id` or `scope_id`.
   - `src/OrcaCore.Providers.SqlServer/Migrations/003_resource_pools.sql:11-66` repeats those
     first-create shapes without the ownership columns.
   - `src/OrcaCore.Providers.SqlServer/Migrations/008_resource_ownership.sql:1-18` then uses four
     guarded `ALTER TABLE` statements to add them.

   Therefore the empty-container suite reaches the current schema by executing compatibility-style
   DDL after first creation. Passing 63/63 proves the migration chain works; it does not prove that
   the greenfield first-created schema is current or that compatibility DDL is absent.

These are not unrelated historical provider details. `fiber_id` and `scope_id` correlate the exact
resource owner required by Section 6 lease lifecycle and recovery. The files are in the reviewed
target, and the clarification that triggered this refreeze explicitly rejects compatibility
migrations and old-row upgrades.

### Required remediation

At minimum:

1. Put `fiber_id` and `scope_id` directly into SQL Server's actual first-create resource ticket and
   waiter definitions, including any retained guarded first-create definition.
2. Remove SQL Server `008_resource_ownership.sql`.
3. Remove PostgreSQL `007_resource_ownership.sql`.
4. Remove the four redundant ownership ALTERs from
   `PostgreSqlResourcePoolStore.InitializeAsync`.
5. Add focused assertions that the current ownership and `creation_capacity` columns exist from
   fresh creation while no resource-pool compatibility migration/ALTER resource remains.
6. Re-run both full Docker-backed provider suites from empty containers, re-freeze, and request a
   new independent review.

If the intended product rule is narrower and permits historical incremental schema scripts, the
proposal/design/request must first state that narrower rule coherently. The current broad
no-compatibility wording cannot be approved against this source.

## 3. Claims independently confirmed

The rejection is narrow. The following submitted claims are sound:

- The new `008_resource_pool_creation_capacity` and
  `009_resource_pool_creation_capacity` files are absent.
- A case-insensitive scan of 835 non-build files under `src`, `tests`, `openspec`, and
  `docs/implementation` found zero instances of the request's five banned creation-capacity
  migration/backfill patterns.
- PostgreSQL directly creates non-null `creation_capacity` and mutable `capacity`.
- SQL Server `001_initial.sql` and `003_resource_pools.sql` directly create both capacity columns.
- Both providers reject resize below one before database access/mutation and throw
  `ResourcePoolNotConfiguredException` for an unknown pool.
- Both providers retain immutable creation capacity separately from current capacity and preserve
  current capacity on an idempotent matching re-upsert.
- Both providers delete the exact holder waiter inside the same serializable transaction before
  ticket deletion and FIFO grant evaluation.
- PostgreSQL 77/77 and SQL Server 63/63 pass against empty Docker containers.
- The integration lane is 110 passed / 5 explicitly owned skips / 0 failed. The five skips and
  ownership are exactly:
  `INT_JS_004` and `INT_JS_014` -> task 9.6;
  `INT_MN_009` -> task 7.5;
  `INT_HO_011` -> task 10.5;
  `INT_JS_018` -> nightly slow suite.
- Task `7.0` remains unchecked, and no Section 7 implementation is credited.

## 4. Independent validation record

All product and validation commands ran from the repository root after the initial exact manifest
comparison.

| Lane | Independent result |
|---|---:|
| Docker engine | 29.6.1, `linux/x86_64` |
| Request's banned-pattern `git grep` | 0 matches |
| Broader untracked-aware banned-pattern scan | 835 files / 0 matches |
| Solution build, no restore/no incremental | succeeded; 0 warnings / 0 errors |
| Core | 464 passed / 0 failed / 0 skipped |
| Ephemeral | 173 passed / 0 failed / 0 skipped |
| Durable | 332 passed / 0 failed / 0 skipped |
| Hosting | 17 passed / 0 failed / 0 skipped |
| Acceptance | 70 passed / 0 failed / 0 skipped |
| In-memory provider certification | 78 passed / 0 failed / 0 skipped |
| PostgreSQL provider suite | 77 passed / 0 failed / 0 skipped |
| SQL Server provider suite | 63 passed / 0 failed / 0 skipped |
| Integration | 110 passed / 0 failed / 5 explicitly owned skips |
| Infrastructure guards, run 1 | 104 passed / 0 failed / 0 skipped |
| Infrastructure guards, run 2 | 104 passed / 0 failed / 0 skipped |
| Infrastructure guards, run 3 | 104 passed / 0 failed / 0 skipped |
| Section 6 current-physical drivers | 32 passed / 0 failed |
| Expected-red guards | 0 passed / 61 intentional named failures / 0 skipped |
| Green compile fixtures | exact/product consumers compile; 26 source and 26 package forbidden-member diagnostics; incomplete control rejected |
| Product-authoring ExpectedRed compile set | 0 remaining gaps |
| Package ExpectedRed compile set | exactly 8 named Section 7/8 gaps; nonzero as designed |
| Strict OpenSpec, reshape | valid |
| Strict OpenSpec, runtime governance | valid |
| Strict OpenSpec, all | 17 passed / 0 failed |
| Task accounting, reshape | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Task accounting, runtime governance | 16 complete / 0 pending / 16 total; 0 duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| `git diff --check` | exit 0; line-ending notices only |

### Runner/environment disclosure

- The first sandboxed PostgreSQL attempt could not access the Docker named pipe and produced
  Testcontainers infrastructure failures. The authorized rerun against the same build passed
  77/77; only that rerun is product evidence.
- The first parallel SQL Server run reported 24 passing tests and then the test host crashed. A
  second parallel attempt exceeded the command timeout and left the exact launched test processes
  running; after verifying their paths/start times, only those processes were stopped. A direct
  xUnit run with `-parallel none` then completed all 63 tests with 0 errors/failures/skips. The
  complete serial run is the product evidence.
- Diagnostic `docker ps`/`docker stats` and process checks were used only to isolate the runner
  issue. They changed no reviewed file.

## 5. Authorization boundary

**REJECT Section 6 exit.**

The green functional and guard suites do not waive R1 because none of the request's narrow banned
patterns names the retained ownership compatibility DDL, and the empty-container suites execute the
incremental ALTER chain instead of proving its absence.

Task `7.0` and all Section 7 source work remain blocked. This verdict authorizes no source, test,
task, spec, plan, or status change.
