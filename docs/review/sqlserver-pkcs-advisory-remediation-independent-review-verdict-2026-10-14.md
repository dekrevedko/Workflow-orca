# SQL Server transitive PKCS advisory remediation independent review verdict

**Date:** 2026-10-14
**Reviewer:** independent review
**Scope reviewed:** the six-entry freeze on base `3e1fbdd4e8331fed3a799d90a9105dc64f63f2f4`
(tree `9327e0ce…`), named by `sqlserver-pkcs-advisory-remediation-dirty-manifest-2026-10-14.txt`.
It contains:
- one central `PackageVersion` pin, `System.Security.Cryptography.Pkcs` 9.0.21, in
  `Directory.Packages.props`;
- the twelve recalculated rows of `package-source-provenance.json`;
- the review request, the manifest and the freeze/history fixtures.

**Authorization requested:** checkpoint of this dependency-only target and its direct-child
evidence. There is no activation.

This verdict authorizes that checkpoint and the evidence child, in the sequence the request states.
It authorizes nothing in Task 2.1 or any other task, and it does not resolve any of my round-100
observations.

## Summary

The fix is minimal, correct and verified end to end.

- **Problem.** NuGet now flags `System.Security.Cryptography.Pkcs` 9.0.4, which
  Microsoft.Data.SqlClient 6.1.3 pulls in, under GHSA-846f-f764-76mx (high, CVSS 7.5) and
  GHSA-2fmv-rv2c-59x7 (moderate, CVSS 5.9), both published 2026-10-13. With the repository's
  warnings-as-errors policy, every `-warnaserror` build failed, including the base.
- **Patched version.** Both advisory pages list 9.x as affected through 9.0.20 and patched in
  9.0.21, as the request states.
- **Fix.** One `PackageVersion` line. `CentralPackageTransitivePinningEnabled` was already on;
  SqlClient stays at 6.1.3; no project reference, audit setting, `NoWarn` or
  `WarningsNotAsErrors` changes.
- **Effect.** The restored graph keeps its 35 libraries, and the only change is Pkcs 9.0.4 to
  9.0.21. Builds pass plain `-warnaserror`, no project reports a vulnerable package, and the
  packed SQL Server package now declares Pkcs 9.0.21, so external consumers get the fix too.
- **Evidence.** Every anchor reproduces, every lane is green, and every targeted control is red.

There are no findings.

## Method

Validation ran in one disposable detached worktree at `3e1fbdd`, and probes in a second; a third
restored the base graph for comparison. Each held the same six entries where relevant.

- Every script asserted its disposable location and that the main `HEAD` was still `3e1fbdd`.
- Main was never modified, and the review created no ref in the reviewed repository.
- The external consumer was built outside every repository, at a short path.
- The advisories were read on GitHub's advisory pages.

## 1. Freeze anchors and the round-100 chain

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 6 lines, 449 B, `42fc54a5…` | identical (`42fc54a5fa4c5117f50783179e99ceeb97a9032122c92e34cc28030db8ff897a`); diff (4) plus untracked (2) paths are exactly the 6; nothing staged |
| Semantic record: 2 rows | 244 B, `e31639e0…` | identical with the stated recipe |
| Active record: 5 rows | 717 B, `02bd4cdc…` | identical (`02bd4cdcf4882ab41358f3a97238d1b10d68055771926ab54648b7794410fc92`), equal to the fixture |
| All-file record, with status (my form) | not embedded | 6 rows, 872 B, `805cc3cd94ac181888a959f60eac908be53109399ae7c62f73c94267bacf33e4` |
| Simulated checkpoint | tree `de81b66a1495fc43a5450345dbc551bacd243335` | identical from a copied index and from a committed disposable copy; parent `3e1fbdd`; 2 A / 4 M; all six blobs equal the raw bytes; committed `diff --check` clean |

- **Round-100 chain.** Checkpoint `c93411e` has parent `89e3b18` and tree `ac61630f`, the tree I
  approved. Its only direct child, `3e1fbdd`, adds my verdict byte-exact (16,156 B, `d786d7ca…`),
  catalogs it, and clears the freeze.
- **Scope.** No `src/`, canonical, delta, numbered or guide document, API baseline, C# or project
  file changes. Canonical provenance stays 184 / `fd6ad044…` with zero pending.
- **Provenance rows.** All twelve source hashes change because `Directory.Packages.props` is a
  shared source input. The package-source comparator passes on the frozen target and fails on any
  stale row (see §3).

## 2. Validation

SDK 10.0.402, with no audit downgrade of any kind.

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / green fixtures pass, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 354 / 79 / 99 / 37 / 24 / 100 |
| PostgreSQL / SQL Server / Integration | 103 / 74 / 11 |
| `Disposition=Infrastructure`, Release | 247/247 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / Debug / strict OpenSpec | 16/16 / 16/16 / 21/21 |
| Guards on the committed simulation | 247 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 247/247 |
| `dotnet list package --include-transitive --vulnerable` | 29 projects, none with a vulnerable package |
| `dotnet nuget why` (SQL Server provider) | SqlClient 6.1.3 -> Pkcs 9.0.21 |
| Packed `OrcaCore.Providers.SqlServer` nuspec | depends on Pkcs 9.0.21 and SqlClient 6.1.3 |
| Restored graph, base vs target | 35 / 35 libraries; only Pkcs 9.0.4 -> 9.0.21 |

## 3. Independent negative controls

Builds use the SQL Server provider with plain `-warnaserror`; comparator cases run the
package-source guard.

| # | Mutation | Result |
|---|---|---|
| P0 / P4 | frozen target / restored | 0/0 / 0/0 |
| P1 | central Pkcs pin removed | **red**: NU1902 + NU1903 on 9.0.4 |
| P2 | pin set to 9.0.20, the last affected 9.x | **red**: NU1902 + NU1903 on 9.0.20 |
| P3 | pin kept, `CentralPackageTransitivePinningEnabled=false` | **red**: NU1902 + NU1903 on 9.0.4, so the switch is load-bearing |
| P5 / R0 | comparator on frozen target / restored | green / green |
| P6 | whole package-source fixture reverted to base, pin kept | **red**: OrcaCore source drift named |
| P7 | only the OrcaCore row reverted | **red** |
| P8 | pin removed, refreshed fixture kept | **red**: the comparator also notices the input change |

**External consumer.** Built outside every repository with its own empty package cache, empty
`Directory.Build.props`/`.targets`, central package management off, `TreatWarningsAsErrors` on,
and source mapping of `OrcaCore*` to the freshly packed feed and everything else to nuget.org. Its
only reference is `OrcaCore.Providers.SqlServer` 0.0.0-phase0. It restores and builds 0/0,
resolves Pkcs 9.0.21 and SqlClient 6.1.3, and runs. A negative control in the same setup that
references SqlClient 6.1.3 directly, without the OrcaCore package, fails with NU1902/NU1903 on
9.0.4, which shows the consumer's audit is live and the fix comes from the package's own
dependency.

## 4. Notes

No findings. Recorded for future maintenance:

- The pin stays on the 9.x line, the minimal change for SqlClient 6.1.3; the 10.x line (10.0.13)
  is also patched. If a later SqlClient requires a higher Pkcs, this pin must move with it, or
  restore will report a downgrade.
- The pin is now part of the published SQL Server package's dependency contract, by design.
- The clean audit is a dated result against current NuGet advisory data, not a guarantee against
  future advisories.
- My first external-consumer attempt failed only because my scratch path exceeded the Windows
  path limit; the rerun at a short path is the result reported above.

## 5. Reviewer hygiene and checkpoint sequence

`HEAD` is `3e1fbdd4e8331fed3a799d90a9105dc64f63f2f4`, with the six frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees and the external consumer are removed.

Checkpoint sequence, as the request states:
1. Confirm zero drift against the 6-entry manifest and the anchors above.
2. Stage exactly those 6 paths on `3e1fbdd`, expecting tree `de81b66a…`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation. Task 2.1 and every later task stay open; the next target is the remaining
Task 2.1 work, which should also address my round-100 observations.

## Determination

The advisory is real, the patched version is right, and one central pin fixes it for this
repository and for consumers of the published package, with no other dependency change and no
audit suppression. The controls show that removing or weakening the pin, or leaving its provenance
stale, fails visibly.

**Verdict:** **APPROVE**
