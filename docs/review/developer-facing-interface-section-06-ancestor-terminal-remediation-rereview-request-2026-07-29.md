# Section 6 ancestor-terminal remediation: independent exit re-review request

**Date:** 2026-07-29  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** Section 6 exit and the later start of task `7.0`

This request supersedes the rejected Section 6 exit target without modifying its immutable
request, manifest, or either independent verdict. Task `7.0` remains open and blocked until an
independent reviewer approves this exact refrozen target without a release blocker.

## 1. Immutable rejection evidence

Preserve and read:

- `developer-facing-interface-section-06-exit-review-request-2026-07-29.md`;
- `developer-facing-interface-section-06-exit-review-dirty-manifest-2026-07-29.txt`;
- `developer-facing-interface-section-06-independent-exit-review-verdict-2026-07-29.md`; and
- `developer-facing-interface-section-06-exit-review-independent-verdict-2026-07-29.md`.

Both reviewers rejected the 480-entry target for the same release blocker:
`ancestor-terminal-suppresses-merge` declared terminal ownership through task `6.2` but remained
ExpectedRed and had no executable driver. The other 51 scenario reds were independently mapped to
Sections 7-9.

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
| Self-inclusive porcelain entries | 484 |
| Entry classes | 347 modified / 9 deleted / 128 untracked |
| Raw-manifest SHA-256 | `86ED89722B12580487FD3124BE4753D99DCBDA5FCB91590C1671520A4803E487` |
| LF-normalized sorted-status SHA-256 | `F02F174124700547ADEF1785CBC4B6F4DBCA97EEB80D70A5DF3E1DF21C605DD1` |
| Reshape tasks | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Runtime-governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-dirty-manifest-2026-07-29.txt`.
Reproduce it before reading conclusions and again after validation. Any path/status drift
invalidates the review.

## 3. Remediation claims to re-derive

1. Executable behavior ownership is no longer encoded in three parallel scenario-ID lists.
   `FinalOwningSection` parses every task reference and range in `turnsGreenTask`; the highest
   referenced section is the terminal owner.
2. That rule reproduces 4 Section 4 drivers, 8 Section 5 drivers, and 32 Section 6 drivers. Exactly
   51 behavior scenarios remain ExpectedRed because their terminal owners are Sections 7-9.
3. `ancestor-terminal-suppresses-merge` has one exact-call-certified deterministic-time driver.
4. The driver executes eight schedules: ephemeral and durable; root `Parallel` and root
   `ForEach`; `WhenAll` and `WhenAllOutcomes`.
5. Each schedule first proves two child waits are active, advances the guard-owned clock to the
   `CompleteWithin` deadline, and then proves `TimedOut`, zero active waits, zero merge calls, and
   unchanged merge-visible state. Durable schedules additionally project the committed deadline
   on a replacement runtime.
6. No Section 7 source, package, facade, or hosting cleanup is implemented or credited.

## 4. Owner-run evidence

| Lane | Result |
|---|---:|
| Solution build | succeeded; 0 warnings / 0 errors |
| Core | 464 passed / 0 failed / 0 skipped |
| Ephemeral | 173 passed / 0 failed / 0 skipped |
| Durable | 332 passed / 0 failed / 0 skipped |
| Hosting | 17 passed / 0 failed / 0 skipped |
| Acceptance | 70 passed / 0 failed / 0 skipped |
| Provider certification | 78 passed / 0 failed / 0 skipped |
| Infrastructure guards | 104 passed / 0 failed / 0 skipped on three consecutive isolated runs |
| Section 6 current-physical drivers | 32 passed / 0 failed |
| Expected-red guards | 0 passed / 61 intentional named failures / 0 skipped |
| Green compile fixtures | passed; current source packed fresh; exact/product consumers compile; 26 source and 26 package forbidden-member diagnostics; incomplete control rejected |
| Product-authoring ExpectedRed compile set | passed with 0 remaining gaps |
| Package ExpectedRed compile set | nonzero as designed; exactly 8 named Section 7/8 gaps |
| Strict OpenSpec | both active changes valid; all 17 items passed |
| Task accounting | reshape 90/46/136, coordinated 16/0/16, no duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| Whitespace | `git diff --check` exit 0; line-ending notices only |

Docker-backed PostgreSQL and SQL Server suites remain blocked because the Docker Desktop Linux
engine named pipe does not exist. This is an environment limitation, not passing evidence.

## 5. Minimum independent commands

Run from `X:\Projects\GitHub\Workflow-orca`:

```powershell
dotnet build OrcaCore.slnx --no-restore --no-incremental -p:NuGetAudit=false -v minimal

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
failures. Run the infrastructure lane three consecutive times. Inspect the driver body rather
than accepting counts as proof: confirm all eight schedules execute and that the durable schedule
really reopens through a replacement runtime after the deadline timer is committed.

## 6. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record provenance, exact
manifest comparison before and after, every command/result, independent source and test
derivation, environment limitations, and `APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to begin task `7.0` in a
later turn; it does not mark task `7.0` complete. Rejection keeps Section 7 blocked.
