# Section 4 final remediation: independent exit re-review request

**Date:** 2026-07-22  
**Requested verdict:** approve or reject Section 4 exit after independently verifying the release
blockers in both immutable remediation re-reviews:

- [2026-07-21 re-review](developer-facing-interface-section-04-remediation-exit-rereview-2026-07-21.md)
- [2026-07-22 independent re-review](developer-facing-interface-section-04-remediation-independent-rereview-2026-07-22.md)

Section 5 remains blocked. This request does not authorize task 5.0 or any Section-5 source work.

## Review snapshot

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`; tree
  `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`; the stable commit is unchanged from both rejected reviews.
- Review target: `HEAD` plus every entry in the
  [final remediation dirty manifest](developer-facing-interface-section-04-final-remediation-dirty-manifest-2026-07-22.txt).
- Frozen manifest: 332 entries (285 modified, 8 deleted, 39 untracked); SHA-256
  `CF3E1BE1BAC6C32A081940E3BCF5D30430BB7F2FE9870E06950767E03351B90B`.
- OpenSpec task state: 45 done, 67 pending, 112 total. Tasks 4.0 through 4.14 are checked;
  task 5.0 remains open and independently gated.
- All prior review files and manifests are immutable inputs. None was edited.

## Release-blocker remediation

| Finding | Remediation to verify |
|---|---|
| Static request was discarded | Every approved root, nested, branch, and item `AcquireResources` overload now authors a lexical resource-scope node. Static `ResourceLeaseRequest` requirements and units are retained in structural fingerprint input, the scope body is recursively fingerprinted, and selector bodies/captures remain opaque. Regressions distinguish leased from unleased graphs and distinct static requests at all four locations while proving selector capture drift does not alter the fingerprint. |
| Fixed-codec driver under-proved behavior and replacement remained public | The fixed serializer now proves stable bytes, typed detached round-trip, deterministic null, cyclic rejection, unapproved polymorphic rejection, and foreign-content-type rejection. The public codec plugin, content-type router, and writer-selection options were deleted; hosting registers the fixed serializer directly; guards reject those exported replacement seams. |
| Fingerprint-version driver under-proved behavior | The executable Section-4 driver now proves opaque delegate equality, inspectable graph drift, explicit version drift, and static resource-request drift. Core regressions cover static lease requests at every approved authoring location. |
| Attempt-state driver under-proved behavior | The driver still observes `ReplaceState` directly and additionally executes a fail-first retry through the ephemeral engine, proving failed-attempt mutation is discarded and the successful replacement is committed. |
| Infrastructure lane was nondeterministic | Exact-authoring compile guards share a nonparallel xUnit collection. The exact infrastructure command passed 53/53 on three consecutive first attempts without a `CS2012` file-lock failure. |
| Task 4.14 was unresolved | The four Section-4 scenario drivers execute their frozen behaviors, the fixed codec surface is closed, and passing authoring/state/codec evidence is in infrastructure. Task 4.14 is checked only after the complete validation below; task 5.0 remains unchecked. |

### Section boundary for resource leasing

Section 4 now retains the lexical lease scope and static request as inspectable authored structure; it
does not silently discard either argument. The current compiler preserves the nested body through a
Section-4 internal marker. Actual permit acquisition, renewal, recovery, release, and lease-aware
runtime lowering remain owned by Section 6 and its still-red scenarios. Review must reject if this
boundary lets a Section-6 lifecycle scenario turn green, but must also reject if the Section-4 graph
or fingerprint loses the authored scope/request.

## Reproduced evidence

| Lane | Result |
|---|---:|
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | succeeded, 0 warnings, 0 errors |
| Core | 409 passed, 0 failed, 0 skipped |
| Ephemeral | 148 passed, 0 failed, 0 skipped |
| Durable | 277 passed, 0 failed, 0 skipped |
| Hosting | 15 passed, 0 failed, 0 skipped |
| Guard infrastructure | 53 passed, 0 failed, 0 skipped; repeated three consecutive times with the same result |
| Expected-red guard lane | 0 passed, 104 intentional individually named failures, 0 skipped |
| Four Section-4 scenario guards | 4 passed, 0 failed, 0 skipped |
| Static lease fingerprint regressions | 5 passed, 0 failed, 0 skipped |
| Durable fixed-codec regressions | 4 passed, 0 failed, 0 skipped |
| Hosting fixed-codec registration regressions | 2 passed, 0 failed, 0 skipped |
| Green compile fixtures | fresh current-source package compiled; exact consumer compiled; 26 source and 26 package forbidden-member diagnostics verified; incomplete package rejected |
| Expected-red compile fixtures | 0 remaining; Section-4 authoring package proof reports green |
| Strict OpenSpec validation | `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits` valid |
| Whitespace | `git diff --check` exited 0 with only benign LF-to-CRLF notices |

The expected-red test command returns nonzero by design. Review every named failure and reject any
setup, discovery, restore, assertion, or newly-green result that is not the frozen later-section gap.

## Commands

```powershell
dotnet build OrcaCore.slnx --no-restore -v minimal
dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --filter "Disposition=Infrastructure" -v minimal
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --no-build --filter "Disposition=ExpectedRed" -v minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
```

## Independent review questions

1. Does each approved `AcquireResources` placement retain a lexical authored scope, static request,
   ordered requirements, units, and nested body rather than discarding the request or mutating only
   the parent sequence?
2. Do distinct static requests and leased/unleased graphs produce distinct fingerprints at root,
   nested, branch, and item locations while selector body/capture identity stays opaque?
3. Does Section 6 remain the sole owner of executable lease acquisition, renewal, recovery, release,
   and protection-token behavior, with every corresponding scenario still intentionally red?
4. Does the fixed codec execute all frozen deterministic/detachment/rejection behavior, and is every
   public codec replacement/router/writer-selection seam gone?
5. Do the definition-id, fingerprint-version, and attempt-state drivers execute their frozen
   behavior rather than merely compile or count calls?
6. Does the infrastructure compile lane pass from a clean first attempt repeatedly without two test
   classes racing over the same fixture output?
7. Are all earlier Section-4 blockers still resolved, with no regression in exact-consumer compile,
   durable recovery, durable host-DI activation, root-only fan-out, or legacy fallback removal?
8. Does the frozen manifest reproduce exactly before the reviewer adds one new immutable verdict?

Record provenance, the exact frozen manifest/checksum, every command/result, findings, and verdict
in one new immutable dated review file. Only an approval with no release blocker may authorize task
5.0. Do not edit reviewed source, tests, tasks, specs, documentation, manifests, requests, or existing
review artifacts.
