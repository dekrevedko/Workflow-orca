# Phase 0 guard-packet remediation: final independent re-review request

**Date:** 2026-07-19

**Requested verdict:** approve or reject Phase 0 exit after verifying the complete remediation of
the four P1 findings in the immutable first
[independent review](developer-facing-interface-phase-00-guard-packet-independent-review-2026-07-19.md)
and the three follow-up P1 findings in the immutable
[remediation re-review](developer-facing-interface-phase-00-guard-packet-remediation-independent-rereview-2026-07-19.md).
Task 3.12 and task 4.0 remain open; no product source work is authorized by this request.

## Review snapshot

- Baseline and `HEAD`: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Review target: that commit plus the paths frozen in the
  [remediation dirty manifest](developer-facing-interface-phase-00-guard-packet-remediation-dirty-manifest-2026-07-19.txt).
- Frozen manifest: 120 paths; SHA-256
  `39B15216CE9BEC47EEDFDB07E512CC0BBE54C7A0A4202EBC109D232FE85A51C2`.
- Product-source scope: no changed path under `src/`.
- OpenSpec state before the verdict: 29 done, 83 pending, 112 total; tasks 3.12 and 4.0 are open.

## Final remediation disposition

| Finding | Remediation |
|---|---|
| Legacy assemblies and four-tier inspection | `PublicSurfaceCatalog` derives all eleven target assemblies and all nine audiences from the frozen v1 contract. Recursive type/signature edges, exact friend metadata, future assembly discovery, and full audience classification are checked against that inventory. |
| String-only green compile lane | The green lane packs a fixture-owned `OrcaCore 0.0.0-phase0` package, compiles the exact companion and positive usage against only that package, verifies 26 precise forbidden-member CS1061 diagnostics, and rejects a deliberately incomplete package. The product red compiles the same full positive usage against the absent real package. |
| Prose-only behavioral scenarios | The 95 scenario rows map to 72 frozen exact call signatures. A driver must supply one direct expression per required call. The guard-owned context resolves assembly, declaring type, member, generic arity, parameter types, and return type; invokes the expression; records runtime execution; and mints an observation that `Phase0Assert` must consume. Sync/`Task`/`ValueTask`, value/void, and throwing shapes are covered. Deterministic time/barrier credit requires an active observed call plus a frame from a frozen product assembly. Mutations reject uninvoked expressions, discarded results, wrong members/overloads, unrelated product activity, system time, and no-op barriers. The empty scenario host therefore leaves all 95 future product behaviors intentionally red. |
| Wrong DAG failure owner | Failure checks derive workflow and DAG owners from the frozen ownership catalog. `DAG-*` typed failures must live in namespace and assembly `OrcaCore.Dag`; workflow failures and shared bases remain in `OrcaCore`. |

## Reproduced evidence

| Lane | Result |
|---|---:|
| Guard infrastructure | 43 passed, 0 failed, 0 skipped |
| Expected product guards | 0 passed, 28 intentional failures, 0 skipped; ten theories report the absent drivers for all 95 behavior scenarios |
| Compile infrastructure | exact companion and positive usage compiled from the package-only surface; 26 precise forbidden-member diagnostics verified; incomplete-package mutation rejected |
| Compile product lane | 1 intentional red: the complete positive usage cannot compile because the real `OrcaCore 0.0.0-phase0` package is absent |
| Package product lane | 8 intentional reds: the eight exact local-feed journeys lack their `0.0.0-phase0` packages |
| `dotnet build OrcaCore.slnx -c Release --no-restore` | succeeded, 0 warnings, 0 errors |
| Strict OpenSpec validation | both coordinated changes valid |
| Scope/whitespace | `git diff --check` passed; no changed `src/` path |

## Commands

```powershell
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore --filter "Disposition=Infrastructure" --nologo -v minimal
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore --filter "Disposition=ExpectedRed" --nologo -v minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed
dotnet build OrcaCore.slnx -c Release --no-restore --nologo -v minimal
openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
git diff --name-only -- src
```

Expected-red commands return nonzero by design. Inspect every named failure and reject setup,
restore, compiler, driver-discovery, or assertion failures that are not the recorded product gap.

## Final review questions

1. Does the public-surface gate cover the full frozen package and tier graph, including exact
   ownership, compiled friend metadata, and recursive cross-tier leak checks?
2. Does the package-only compile gate mechanically prove the exact authoring surface and reject
   incomplete or forbidden surfaces?
3. Can any of the 95 behavior scenarios turn green without a runtime-recorded exact product call,
   a context-owned asserted observation, and the required deterministic seam use?
4. Are workflow and DAG failures checked against their distinct normative owners?
5. Are all 28 test reds, the one compile red, and the eight package reds intentional and
   actionable, with task 3.12 and task 4.0 still open?

Record the reviewed baseline, exact dirty manifest and checksum, commands/results, findings, and
verdict in a new immutable dated review. Only an approval without unresolved release-blocking
findings may close task 3.12. Task 4.0 remains blocked until that approval is recorded.
