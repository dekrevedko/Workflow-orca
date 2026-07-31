# Section 4 codec remediation: independent exit re-review request

**Date:** 2026-07-22  
**Requested verdict:** approve or reject Section 4 exit after independently verifying the two
release blockers in both immutable final-remediation rejection reviews:

- [Independent rejection](developer-facing-interface-section-04-final-remediation-independent-rereview-2026-07-22.md)
- [Exit re-review rejection](developer-facing-interface-section-04-final-remediation-exit-rereview-2026-07-22.md)

Those two rejections control this target. A conflicting approval was reported during the prior
concurrent review, but it is not present in this frozen tree and did not identify either codec
blocker. Section 5 remains blocked. This request does not authorize task 5.0 or any Section-5 work.

## Review snapshot

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`; tree
  `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`; the stable commit is unchanged from the rejected reviews.
- Review target: `HEAD` plus every entry in the
  [codec-remediation dirty manifest](developer-facing-interface-section-04-codec-remediation-dirty-manifest-2026-07-22.txt).
- Frozen manifest: 339 entries (288 modified, 8 deleted, 43 untracked); SHA-256
  `4A9BE59EF3171186EEAFDD2FFAB250B618303D8420F2EB5DA1225E32AD0D39A0`.
- OpenSpec task state: 45 done, 67 pending, 112 total. Tasks 4.0 through 4.14 are checked;
  task 5.0 remains open and independently gated.
- All prior review files, requests, and manifests are immutable inputs. None was edited.

## Release-blocker remediation

| Finding | Remediation to verify |
|---|---|
| Public serializer replacement seam | `IWorkflowPayloadSerializer` and `JsonWorkflowPayloadSerializer` are internal implementation details in the durable engine. The public `DurableWorkflowRuntime` constructor has no serializer/codec parameter and always creates the fixed codec. Hosting registers and resolves no serializer/codec service, so DI registration order cannot replace it. The internal constructor remains only as an implementation/test seam and is not visible to consumers. |
| Root-only polymorphism validation | The fixed codec walks the complete value graph before serialization: public readable object properties, generic collection elements, and generic dictionary keys/values are checked recursively. Cycles and undeclared runtime-type substitutions are rejected before provider mutation. Exact derived types declared by the static contract with `JsonDerivedTypeAttribute` remain valid and round-trip. |
| Codec identity absent from plan identity | `orcacore-json-v1` is included in the compiled-plan fingerprint seed, with a regression that independently recomputes the expected hash. |
| Green evidence overstated codec closure | The Section-4 executable fixed-codec scenario now starts a real durable workflow twice and proves deterministic persisted bytes/content type, detachment, null fidelity, cyclic rejection, nested/collection/dictionary polymorphic rejection, and absence of exported codec types or public runtime codec parameters. Infrastructure and hosting guards independently enforce the same public-surface closure. |

## Reproduced evidence

| Lane | Result |
|---|---:|
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | succeeded, 0 warnings, 0 errors |
| Core | 410 passed, 0 failed, 0 skipped |
| Ephemeral | 148 passed, 0 failed, 0 skipped |
| Durable | 282 passed, 0 failed, 0 skipped |
| Hosting | 15 passed, 0 failed, 0 skipped |
| Guard infrastructure | 53 passed, 0 failed, 0 skipped; repeated three consecutive times with the same result and no `CS2012` |
| Expected-red guard lane | 0 passed, 104 intentional individually named failures, 0 skipped |
| Four Section-4 scenario guards | 4 passed, 0 failed, 0 skipped |
| Durable fixed-codec regressions | 9 passed, 0 failed, 0 skipped |
| Provider pre-mutation polymorphism regression | passed; no event-store method was called |
| Hosting/public replacement-surface regressions | 2 passed, 0 failed, 0 skipped |
| Codec fingerprint-identity regression | passed |
| Green compile fixtures | fresh current-source package compiled; exact consumer compiled; 26 source and 26 package forbidden-member diagnostics verified; incomplete package rejected |
| Expected-red compile fixtures | 0 remaining; Section-4 authoring package proof reports green |
| Strict OpenSpec validation | `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits` valid |
| Whitespace | `git diff --check` exited 0 with only benign LF-to-CRLF notices |

The expected-red test command returns nonzero by design. Review every named failure and reject any
setup, discovery, restore, assertion, or newly-green result that is not a frozen later-section gap.

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

1. Can an ordinary consumer replace the durable value codec through a public type, runtime
   constructor, hosting registration, DI ordering, options object, router, writer selector, or any
   equivalent surface?
2. Does the public durable runtime always use the fixed `orcacore-json-v1` implementation while the
   internal test seam remains inaccessible outside friend assemblies?
3. Before any provider mutation, does serialization reject cycles and unapproved runtime-type
   substitutions in the root, nested properties, collection elements, and dictionary keys/values?
4. Does a statically approved polymorphic contract using `JsonDerivedTypeAttribute` still serialize
   and round-trip correctly?
5. Is `orcacore-json-v1` bound into plan identity, and does the regression independently prove the
   exact fingerprint seed rather than merely compare two product outputs?
6. Does the executable fixed-codec scenario prove persisted bytes/content type, detached round-trip,
   null fidelity, all required rejection paths, and public-surface closure?
7. Are all earlier Section-4 blockers still resolved, all 104 expected-red failures owned by later
   sections, and all Section-6 lease lifecycle scenarios still intentionally red?
8. Does the frozen manifest reproduce exactly before the reviewer adds one new immutable verdict?

Record provenance, the exact frozen manifest/checksum, every command/result, findings, and verdict
in one new immutable dated review file. Only an approval with no release blocker may authorize task
5.0. Do not edit reviewed source, tests, tasks, specs, documentation, manifests, requests, or
existing review artifacts.
