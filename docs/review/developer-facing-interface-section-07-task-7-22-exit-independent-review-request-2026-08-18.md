# Section 7 task 7.22 independent exit review request

**Date:** 2026-08-18  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** check task `7.22` and create its coherent checkpoint commit

This is an implementation-owner request, not an approval. `HEAD` remains the independently
approved task 7.12 checkpoint. Task `7.22` remains open because independent approval is one of its
own completion conditions. Task `8.0` remains blocked by the unfinished
`harmonize-downstream-capability-specs` exit work even if this review approves the reshape Section 7
checkpoint.

The exact `HEAD`, tree, porcelain SHA-256, and content-record SHA-256 are supplied with the review
handoff after this file and the self-inclusive manifest are written. They are not embedded here so
the content-record hash can cover this request without a self-reference cycle.

## Review target

The target is `HEAD` plus every entry in
`developer-facing-interface-section-07-task-7-22-exit-dirty-manifest-2026-08-18.txt`.
Reproduce `git status --porcelain=v1 --untracked-files=all` before reading conclusions and again
after validation. Any status, path, byte-length, or content-hash difference invalidates the review.

The porcelain anchor is SHA-256 over the raw porcelain lines in Git's emitted order, joined with LF
and a final LF, encoded as UTF-8 without BOM. The content record sorts those raw lines ordinally and
renders each as `<2-char status>\t<forward-slash path>\t<byte length>\t<lowercase file SHA-256>`,
again LF-joined with a final LF and UTF-8 without BOM; its anchor is the SHA-256 of those bytes.

The implementation delta is deliberately narrow. It closes the final task 7.22 gaps discovered by
the owner-run exit audit:

1. add every active sample project to `OrcaCore.slnx` and guard exact sample-project ownership;
2. replace the two broken legacy sample applications with current public-surface examples;
3. make the browser dashboard a host-owned process-local BCL telemetry consumer with live health
   and JSON-safe snapshot endpoints;
4. move the completed Section 7B source fixture from the expected-red lane into the green lane;
5. remove shared nested-build directories and synchronous redirected-process reads from the compile
   guards so concurrent guard executions are deterministic.

No product assembly, OpenSpec requirement, task checkbox, package manifest, public API baseline, or
provider implementation is changed by this target.

## Claims to re-derive

### Section and change gates

- Tasks 7.12, 7.16, 7.17, 7.17a-d, 7.19, 7.20, and 7.23-7.34 are complete. Task 7.22 is the only
  open Section 7 task and remains open solely for this approval.
- `reshape-developer-facing-interfaces` is at 128/159. The reduced
  `harmonize-downstream-capability-specs` plan has its independent planning approval and its two
  approved canonical synchronizations complete (tasks 1.1-2.3); it does not duplicate reshape's
  messaging, persistence/outbox, repository-friend, or durable-runtime delta ownership.
- All 18 active OpenSpec changes/specs strict-validate together. Treat this as structural evidence,
  not as a substitute for independently checking the cross-change ownership above.
- Section 8 remains blocked until harmonization completes its remaining reconciliation, provenance,
  final approval, and checkpoint tasks.

### Samples and compile fixtures

- `OrcaCore.slnx` contains exactly every non-build-output `samples/**/*.csproj`: Dashboard,
  Examples, SampleHost, and SampleHost.BrokerAdapters.
- The Examples application stages a typed ephemeral definition, resolves its exact reference,
  starts idempotently, awaits typed output, and prints result `42` through public APIs only.
- Dashboard builds and starts; `/health/live` and `/health/ready` return HTTP 200, and
  `/api/dashboard/snapshot` serializes successfully. It observes BCL telemetry and exposes no
  product management API.
- The Section 7B source-surface fixture builds green. The compile expected-red lane contains zero
  fixtures; the package expected-red lane remains exactly `dag-hosting`, owned by Section 8.
- Each compile-fixture invocation owns a GUID-scoped artifacts/feed/packages root and disables
  MSBuild node reuse. Two concurrent script runs and two concurrent xUnit invocations complete
  without file locks or EOF hangs.

### Existing Section 7 evidence that must remain intact

- The fresh local feed contains the exact 12-package manifest and its 12 exhaustive exported API
  baselines match current source/package provenance.
- The production deletion ledger and semantic recovery crosswalk remain exact with zero unresolved
  entries and zero credited deferred/legacy cases.
- InMemory, PostgreSQL, and SQL Server remain the exact selected durable-provider set and their
  shared/provider-native certification lanes remain green.
- The 14 `ExecutableBehaviorExpectedRedGuards` failures remain intentional Section 8 scenarios;
  no infrastructure guard is red.

## Owner-run validation

| Lane | Result |
|---|---:|
| Release solution build (`--no-incremental -m:1 -warnaserror`) | 0 warnings / 0 errors |
| Debug solution build (`--no-incremental -m:1 -warnaserror`) | 0 warnings / 0 errors |
| Core / Ephemeral / Durable | 350 / 79 / 98 passed |
| Acceptance / Hosting | 37 / 24 passed |
| Provider certification | 96 passed |
| PostgreSQL / SQL Server / Integration, Docker-backed | 101 / 72 / 11 passed |
| Infrastructure guards | 210/210; three consecutive complete runs before freeze plus one final run |
| Expected-red guards | exactly 14/14 named Section 8 failures |
| Fresh-package forbidden probes | 132/132 |
| Exact public API baseline guards | 14/14 |
| Package fixtures | 8 green; exactly `dag-hosting` expected red |
| Compile fixtures | green lane passed; expected-red lane contains 0 |
| Example runtime | typed result `42` |
| Dashboard smoke | live 200; ready 200; snapshot serialized |
| Strict OpenSpec | 18/18 |
| Task accounting | reshape 128/159; harmonize 9/33 |
| NuGet vulnerability audit | no vulnerable packages in the full solution |
| Active documentation links | 43 documents / 0 missing local links |
| Whitespace | `git diff --check` exit 0; CRLF advisories only |

The first sandboxed final build attempted to refresh NuGet signature/audit data and failed with
network-only `NU1301`/`NU1900`; that failed restore left incomplete local assets. An authorized
online Release build then restored all projects and completed 0/0, followed by the clean no-restore
Debug build and the final test/guard runs above.

## Minimum independent checks

Run from the repository root:

```powershell
git status --porcelain=v1 --untracked-files=all
git rev-parse HEAD
git rev-parse 'HEAD^{tree}'

dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror
dotnet build OrcaCore.slnx -c Debug --no-restore --no-incremental -m:1 -warnaserror

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=ExpectedRed"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed

dotnet run --project samples/OrcaCore.Examples/OrcaCore.Examples.csproj -c Release --no-build --no-restore
openspec.cmd validate --all --strict
git diff --check
```

Also independently run the PostgreSQL, SQL Server, and integration suites sequentially; inspect the
12-package baseline/provenance result; rederive the production deletion ledger and semantic
crosswalk; and repeat the infrastructure guard lane to verify determinism.

## Verdict instructions

Write one new dated immutable verdict under `docs/review/`. Record both freeze anchors before and
after validation, every command/result, independent source/test derivation, and `APPROVE` or
`REJECT`. Do not edit the reviewed target.

Approval authorizes the implementation owner to check 7.22 and create the exact coherent Section 7
checkpoint commit. It does not authorize task 8.0; the next work after that checkpoint is the
remaining harmonization change.
