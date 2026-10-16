# Independent review: SQL Server transitive PKCS advisory remediation

Date: 2026-10-14. Base/parent: 3e1fbdd4e8331fed3a799d90a9105dc64f63f2f4; tree 9327e0ce7a22284f3145fc2eb34640cff7d66a30.
This is a separate dependency-only maintenance target, not a DAG source slice.
The self-inclusive [raw Git-order manifest](sqlserver-pkcs-advisory-remediation-dirty-manifest-2026-10-14.txt) names exactly six commit-real paths (four modified, two new). Main is UNCOMMITTED with an empty index.

## Requested approval and sequencing

Review the one central transitive package pin, its recalculated package provenance, and the review/freeze metadata only. Do not infer approval of remaining Task 2.1, any new API, schema, protocol, pump, bridge or task checkbox. The approved identity chain is c93411e (tree ac61630f) -> direct-child evidence 3e1fbdd; its 16,156-byte verdict remains byte-exact at d786d7ca7c00af25c147079d68ccf74875b28878364738c9c06198453c23cb39.

This restores the existing build/security/dependency boundary; it introduces no new normative requirement and therefore no new OpenSpec change or delta. The owning rules remain repository-foundation's provider-native dependency isolation and clean package consumers, quality-and-verification's package smoke tests, and numbered docs 10/11. No canonical, delta, numbered/guide document, DAG ledger, C# file, project reference, friend, option or public API baseline changes. All earlier review records remain frozen.

Only after independent APPROVE: checkpoint exactly these six paths/tree on this base; add the byte-exact verdict and its append-only entry in the checkpoint's only direct-child evidence commit; clear activeFreeze and KEEP the history-file pointer. There is NO activation. Task 2.1 and all later tasks remain open. Resume its run-protocol/provider target only after this package fix is reviewed and checkpointed.

## Reproduced problem and exact fix

The unmodified base fails normal Release --no-incremental -warnaserror with NU1902 and NU1903 for System.Security.Cryptography.Pkcs 9.0.4. The two advisories were published after the 2026-10-07 run:

- [Microsoft moderate advisory GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).
- [Microsoft high advisory GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx).
- [Published Microsoft package System.Security.Cryptography.Pkcs 9.0.21](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/9.0.21).

Both advisories identify 9.0.21 as the patched 9.x version. Directory.Packages.props already enables CentralPackageTransitivePinningEnabled; the ONLY functional edit adds PackageVersion Include="System.Security.Cryptography.Pkcs" Version="9.0.21". Microsoft.Data.SqlClient stays at 6.1.3, with no direct PackageReference added and no major-line upgrade. Existing package ownership and exact twelve-package inventory stay intact. This is dependency removal of known advisories, not a claim that current SQL authentication directly invokes the vulnerable CMS routines.

An actual restored-graph comparison in the isolated copy has 35 library entries before and after: the only removed key is System.Security.Cryptography.Pkcs/9.0.4 and the only added key is System.Security.Cryptography.Pkcs/9.0.21. No other identity/version changes. Because Directory.Packages.props is a shared source input, all twelve package source hashes must change; the candidate was captured through the existing guard's deliberate fail-closed candidate mode, then compared and installed as an unapproved reviewed fixture update. All package IDs/projects, C# sources and fourteen API baselines are unchanged.

## Audit and package-consumer proof

On SDK 10.0.402, Debug and Release non-incremental -warnaserror builds each produce ZERO warnings/errors, without WarningsNotAsErrors, NoWarn or audit-disabling overrides. The SQL Server project's evaluated properties are NuGetAudit=true, NuGetAuditMode=all, NuGetAuditLevel=low, TreatWarningsAsErrors=true, WarningsNotAsErrors empty and only the SDK's default NoWarn 1701;1702. No repository audit policy is altered. Existing compile-fixture runner policies are unchanged.

dotnet list OrcaCore.slnx package --include-transitive --vulnerable --format json --no-restore reports 29 projects and zero vulnerable direct/transitive packages on 2026-10-14. This is a dated result from current NuGet advisory data, not a promise against future advisories. dotnet nuget why confirms SqlClient 6.1.3 -> Pkcs 9.0.21.

The exact local feed packs all twelve packages. SQL Server's nuspec explicitly carries System.Security.Cryptography.Pkcs 9.0.21: central transitive pinning promotes it into the published dependency contract, rather than only fixing this repo's restore. An external temporary consumer has NO repository parent/build settings, its own empty package cache, copied global.json, ManagePackageVersionsCentrally=false and source mapping (OrcaCore* to the local feed; third parties to nuget.org). Its only reference is OrcaCore.Providers.SqlServer 0.0.0-phase0. Normal warnings-as-errors restore/build succeeds with 0/0 and its assets resolve Pkcs 9.0.21. No project/source/binary reference or consumer-side pin is used.

## Validation

Debug/Release builds 0/0; twelve packages; Core 354, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 100; PostgreSQL 103, SQL Server 74, Integration 11, zero skipped. Infrastructure 247/247; exactly fourteen intentional expected-red guards, total 261; OpenSpec strict 21/21. Eight green package fixtures pass and dag-hosting is the sole expected-red package fixture. Existing infrastructure checks cover the fourteen exact API baselines, current-source package comparator, package ownership, compile fixtures and canonical provenance. There is no new C# to inspect or clean; no ReSharper/CleanupCode or unrelated refactoring was run.

Initial PostgreSQL validation hit Docker API 500 during container startup (two initialization failures). It was explicitly stopped after identifying only its owned process tree; Docker and unrelated workloads were not restarted or altered. Full bounded PostgreSQL, SQL Server and Integration retries passed afterward, with no source change or skipped test. The failed/aborted attempt is not represented as green. Logs, graph/audit JSON and candidate provenance are retained in ignored .codex-run, not smuggled into checkpoint scope.

### Isolated negative controls

Every mutation was in the owned disposable clone, never main. Parent/root assertions precede mutations and all Git commands name the clone explicitly.

| Control | Result |
|---|---|
| Remove the Pkcs central pin | RED: both NU1902 and NU1903 on 9.0.4 |
| Keep the pin but disable CentralPackageTransitivePinningEnabled | RED: both advisories return, proving the switch is load-bearing |
| Restore the old OrcaCore package-source row | RED: existing independent comparator names OrcaCore source SHA drift |
| Restore pin/switch/provenance | GREEN: ordinary provider build 0/0 and provenance comparator 1/1 |

The baseline removal probe was replayed to measure the 35-entry graph differential, with the same two expected errors. No audit suppression is used to manufacture a green probe. Frozen committed-copy and freeze-cleared-state rehearsals are reported with the tree/counts in the final handoff; there is no activation rehearsal because there is no checkbox for this maintenance target.

## Preserved state and next target

Canonical provenance remains 184 rows / 48,504 B / fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8: 173 synchronized, eight superseded, three bootstrap, zero pending, semanticApprovalEligible=true for synchronization only. The fourteen-file canonical content inventory, preambles, superseded catalog, Task 7.3 rows and all task states are byte-unchanged. No DAG non-blocking finding is laundered into this fix: the next reviewed Task 2.1 target must lock task 1.4 complete, strengthen before-storage effects/access regression tests, and correct the stale CLAUDE.md status sentence. Those are not claimed fixed here.

## Freeze recipes (LF, UTF-8 without BOM, one final LF)

1. Base HEAD/tree above, empty live index.
2. Manifest = byte-exact git status --porcelain=v1 --untracked-files=all --no-renames. Compare its set with git diff --name-only HEAD plus untracked, and rehearse using a COPIED index; do not stage main. Manifest bytes/SHA and exact staged tree are in the handoff to avoid recursive self-reference.
3. Semantic record excludes docs/review/** and only immutable-document-history.json and review-manifest-provenance.json. Each row is path<TAB>RAW byte length<TAB>RAW lowercase SHA-256; ordinal-sort full rows. 2 rows / 244 B / e31639e030169504e8a871e907d7835b490f3c7385accdb8776af63f3caf447b.
4. Active record excludes ONLY review-manifest-provenance.json. Each row is XY<TAB>path<TAB>RAW length<TAB>RAW SHA-256; ordinal-sort full rows. Its byte count/digest are pinned in activeFreeze and reported in the handoff. It includes this request, so neither its digest nor the self-inclusive tree is embedded recursively here.

Write one dated immutable APPROVE or REJECT for this exact maintenance freeze. Do not edit the request, manifest or prior verdicts. A new verdict is an EXTRA path, not part of these six approved checkpoint paths; catalog it only in the subsequent evidence child. Main must remain at the base until that independent verdict.
