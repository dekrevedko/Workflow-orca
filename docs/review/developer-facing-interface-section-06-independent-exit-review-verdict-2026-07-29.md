# Independent exit review verdict — Section 6 deadlines, operations, leasing, and governance

**Date:** 2026-07-29
**Repository:** `X:\Projects\GitHub\Workflow-orca`
**Branch:** `feature/v3-rebuild`
**Scope:** `HEAD` plus the exact 480-entry target frozen by
`developer-facing-interface-section-06-exit-review-dirty-manifest-2026-07-29.txt`
**Disposition:** **REJECT**

## 1. Verdict

Section 6 does not exit. Task `7.0` remains blocked.

The product suites, the 31 selected current-physical Section 6 drivers, provider certification,
compile fixtures, and strict OpenSpec validation are green. However, the ExpectedRed lane still
contains one scenario whose own frozen completion mapping ends in Section 6:
`ancestor-terminal-suppresses-merge`. It fails because no executable product driver exists.

That is the exact deadline-versus-active-fan-out race retained for Section 6 by the prior
independent approval. Treating it as one of 52 later-section reds makes the red count look correct
while leaving a required Section 6 proof red. This is a release-blocking evidence gap under the
request's own guard-disposition and exit rules. The review found no basis to authorize task `7.0`
until this scenario is implemented as a runtime-recorded current-physical driver and passes on a
new frozen target.

## 2. Frozen target and review integrity

| Item | Independently reproduced value |
|---|---|
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Frozen entries | 480 |
| Entry classes | 347 modified / 9 deleted / 124 untracked / 0 other |
| Raw-manifest SHA-256 | `1F0667403678F12870A51DA2F5218B389959B8CCF00E0A2BD5DBA46574CEAAFC` |
| LF-normalized sorted-status SHA-256 | `C106305B04F733C5A9A2765D64074800026A3DBF847B20465BB28FFB5E6B21D7` |
| Before conclusions and validation | exact line-for-line match; zero missing or additional entries |
| After every validation command | exact match after each command; both hashes and all entry classes unchanged |
| After the complete validation matrix, before this verdict | exact line-for-line match; both hashes and all entry classes unchanged |

Creating this verdict intentionally adds exactly this one new untracked review artifact after the
frozen target was revalidated. The reviewer did not edit any reviewed source, test, task, spec,
plan, request, manifest, documentation, or existing review artifact.

## 3. Authority and boundary review

The review followed the requested authority order:

1. `docs/specs/17-selected-mode-capability-matrix.md`;
2. `docs/specs/17-public-authoring-contract.cs`;
3. the relevant canonical requirements under `openspec/specs/`;
4. `openspec/changes/reshape-developer-facing-interfaces/tasks.md`;
5. the complete `openspec/changes/add-runtime-concurrency-limits/` change;
6. Decision 17 in the reshape design; and
7. the phased plan and immutable review history.

The prerequisite
`developer-facing-interface-section-04-05-amendment-remediation-independent-rereview-verdict-2026-07-29.md`
is an `APPROVE` for its exact 414-entry target and authorized task `6.0`, not Section 6 exit.
`developer-facing-interface-section-06-canonical-entry-review-2026-07-29.md` completed the
Section 6 canonical-entry review without authorizing task `7.0`.

Decision 17 still maps Section 6 to deadlines, durable runtime, management, leasing, and quality.
The scoped-deadline decision keeps root-only `CompleteWithin`, one-step `WithStepTimeout`, and
structural wait timeout distinct and adds no branch/item/lease deadline surface. The current
physical hosting owner is an honest temporary Section 6 proof boundary. Final package ownership,
the common facade, provider packages, and deletion of inherited catch-all `AddOrcaCore` remain
open Section 7 work, including task `7.10`; none was credited as complete here.

Independent task accounting reproduced:

- reshape change: 90 complete / 46 pending / 136 total / 0 duplicate IDs;
- coordinated runtime-governance change: 16 complete / 0 pending / 16 total /
  0 duplicate IDs; and
- task `7.0` remains open.

## 4. Release-blocking finding

### R1 — a Section 6-only merge-fencing scenario remains ExpectedRed with no driver

**Severity:** release blocker
**Disposition:** open

The frozen structured-fan-out scenario ledger declares:

```json
{"id":"ancestor-terminal-suppresses-merge", ...,
 "turnsGreenTask":"5.4-6.2"}
```

That mapping is at
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/structured-fanout-scenarios.json:5`.
It contains no Section 7 or Section 8 owner. The preceding independent Section 4/5 verdict also
states at line 162 that this scenario retained its Section 6 deadline race.

The live task contract independently requires the same proof:

- task `3.6` requires ancestor-terminal merge suppression;
- task `5.9` says ancestor merge suppression turns green in both engines;
- tasks `6.1` and `6.2` complete the workflow-deadline and terminal-race behavior; and
- task `6.11` says the deadline and related suites are green.

The phased plan's Section 6 exit criterion additionally requires the Phase 0
deadline/identity/lease guards to be green.

The guard implementation does not satisfy that contract:

- `ExecutableBehaviorInfrastructureGuards.Section6ScenarioIds` at
  `tests/OrcaCore.DeveloperSurface.Guards/ExecutableBehaviorContractGuards.cs:34` omits
  `ancestor-terminal-suppresses-merge`;
- `ExecutableBehaviorExpectedRedGuards.RemainingScenarios` at line 314 places every scenario not
  listed for Sections 4, 5, or 6 into ExpectedRed;
- the ExpectedRed execution fails this scenario with
  `must have one reviewable executable driver ... but the collection is empty`; and
- an independent source search found no matching `Phase0Scenario` driver in
  `OrcaCore.DeveloperSurface.BehaviorScenarios`.

Existing product tests cover deadline persistence and deadline terminalization, and separate
fan-out tests cover cancellation/termination merge suppression. They do not replace the frozen
scenario's exact `CompleteWithin` race against active branches/items and both `WhenAll` and
`WhenAllOutcomes` merge paths.

The 52-scenario ExpectedRed classification was independently enumerated from the fixture catalog.
Exactly 51 entries name at least one Section 7 or Section 8 completion task. This is the only entry
that does not. Therefore the claimed “52 scenarios remain ExpectedRed for Sections 7 and 8”
classification is false even though the aggregate count is 52.

**Required remediation:** add one runtime-recorded exact current-physical driver satisfying the
frozen `ancestor-terminal-suppresses-merge` call contract; exercise the workflow deadline winning
against active branch and item work, prove the ancestor commits `TimedOut`, prove both success and
outcome merges are suppressed, and prove losing obligations are fenced. Classify it in the
Section 6 current-physical lane, update the intentional-red accounting, freeze a new
self-inclusive manifest, and request a fresh independent re-review. Reclassification without a
passing executable driver does not close the finding.

## 5. Independent validation results

The manifest reproduced after each command below.

| Command or lane | Independent result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore -p:NuGetAudit=false -v minimal` | passed; 0 warnings / 0 errors |
| Core executable | 464 passed / 0 failed / 0 skipped |
| Ephemeral executable | 173 passed / 0 failed / 0 skipped |
| Durable executable | 332 passed / 0 failed / 0 skipped |
| Hosting executable | 17 passed / 0 failed / 0 skipped |
| Acceptance executable | 70 passed / 0 failed / 0 skipped |
| Provider-certification executable | 78 passed / 0 failed / 0 skipped |
| Infrastructure guard run 1 | 103 passed / 0 failed / 0 skipped |
| Infrastructure guard run 2 | 103 passed / 0 failed / 0 skipped |
| Infrastructure guard run 3 | 103 passed / 0 failed / 0 skipped |
| Section 6 current-physical driver lane | 31 passed / 0 failed |
| ExpectedRed guard lane | exit 1 as designed; 62 failed / 0 passed / 0 skipped |
| ExpectedRed scenario classification | 52 scenario reds; 51 have a Section 7/8 completion task, 1 is Section 6-only and is the blocker above |
| ExpectedRed non-scenario facts | 10 named failures / 0 unexpected passes |
| Green compile fixtures | passed; fresh source pack, positive consumers, 26 source and 26 package forbidden-member diagnostics, incomplete-package rejection |
| Product-authoring ExpectedRed compile set | passed; 0 remaining gaps |
| Package ExpectedRed compile set | exit 1 as designed; exactly 8 named gaps: `primary-package`, `minimal-ephemeral`, `postgresql-durable`, `callback-ingress`, `in-memory-durable`, `dag-hosting`, `provider-custom-host`, `kubernetes-companion` |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | passed |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | passed |
| `openspec.cmd validate --all --strict --no-interactive` | 17 passed / 0 failed |
| `dotnet list OrcaCore.slnx package --vulnerable --include-transitive --no-restore` | not independently completed; restricted run could not reach `api.nuget.org` |
| `git diff --check` | exit 0; line-ending notices only |

The green product and certification lanes support the deadline, operation-coordinate, scoped lease
lifecycle, provider accounting, host-governance, and observability claims selected by those
tests. They do not convert an explicitly required red behavior scenario into passing evidence.

## 6. Environment limitations

- The local Docker Desktop Linux engine named pipe
  `npipe:////./pipe/dockerDesktopLinuxEngine` does not exist. `docker version` could not obtain a
  server, so the PostgreSQL- and SQL Server-backed Docker suites were not run. Their absence is
  neither passing nor failing product evidence; the provider-certification executable is green
  78/78.
- The independent NuGet vulnerability command could not reach `https://api.nuget.org/v3/index.json`
  in the restricted environment. A request to rerun with external network access was denied by
  the execution policy because it would disclose private solution dependency metadata. The
  owner-run “no vulnerable packages” claim is therefore not independently confirmed in this
  review.

Neither environment limitation changes R1: the release blocker reproduces locally in the required
ExpectedRed guard lane.

## 7. Explicit answers

1. **Does the 480-entry manifest reproduce before and after validation?** Yes, exactly, including
   both hashes and all entry classes.
2. **Are the selected deadline and structural-wait proofs green?** The selected tests are green,
   but the required ancestor-deadline merge-fencing scenario is not.
3. **Are the selected retry, attempt-copy, cancellation, and overlap proofs green?** Yes.
4. **Is the selected operation coordinate committed and reused as claimed?** The selected product
   and 31-driver lanes are green.
5. **Are lease values, placements, leased-builder omissions, fingerprinting, and ancestry
   defenses covered?** Yes in the selected green lanes.
6. **Are the selected lease lifecycle, identity, release, quarantine, and reconciliation proofs
   green?** Yes in the selected green lanes and provider certification.
7. **Do the forged current-physical runtime defenses execute?** Yes in the selected 31-driver
   lane.
8. **Does provider certification pass?** Yes, 78/78; Docker-backed provider suites were
   unavailable.
9. **Do the selected execution-path and restart-admission proofs pass?** Yes.
10. **Do exact step throttles and transient-pool proofs pass?** Yes in the selected product and
    infrastructure lanes.
11. **Are the selected metrics/debug/operator-documentation checks green without extra policy
    claims?** Yes.
12. **Are all 62 ExpectedRed failures genuinely owned by Sections 7/8?** No. One of the 52
    scenario reds is mapped only through task `6.2` and lacks an executable driver.
13. **Is the Section 7 package/facade boundary otherwise honest?** Yes; task `7.0` and task `7.10`
    remain open and inherited catch-all deletion was not credited.
14. **Is Section 6 free of release blockers?** No.

## 8. Authorization boundary

**REJECT Section 6 exit.**

Task `7.0` and all Section 7 source work remain blocked. This verdict authorizes no source,
guard, task, spec, package, facade, management, provider, or documentation implementation. A
remediated target requires a new frozen manifest and a fresh independent review.
