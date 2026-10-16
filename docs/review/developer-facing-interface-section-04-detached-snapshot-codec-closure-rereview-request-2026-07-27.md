# Section 4 detached-snapshot codec closure: independent exit re-review request

**Date:** 2026-07-27  
**Requested verdict:** approve or reject Section 4 exit after independently reproducing the
release blocker and evidence in:

- [final codec-closure independent rejection](developer-facing-interface-section-04-final-codec-closure-independent-rereview-2026-07-27.md)

That rejection controls this target. Section 5 remains blocked: this request does not authorize
task 5.0 or any Section-5 source work.

## Review snapshot

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`; tree
  `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`; the stable commit is unchanged.
- Review target: `HEAD` plus every entry in the
  [detached-snapshot codec-closure dirty manifest](developer-facing-interface-section-04-detached-snapshot-codec-closure-dirty-manifest-2026-07-27.txt).
- Frozen manifest: 352 entries (290 modified, 9 deleted, 53 untracked); SHA-256
  `EAE3888955083813C83561F6459AD81F257466AD1DD1D0C97B7AA5A4DC806EEB`.
- OpenSpec task state: 45 done, 67 pending, 112 total.
  Tasks 4.0 through 4.14 are checked; task 5.0 remains open and independently gated.
- All prior review files, requests, and manifests are immutable inputs. None was edited.

## Release-blocker remediation

| Finding | Remediation to verify |
|---|---|
| B4: `IEphemeralStateSnapshotter` was a public, consumer-replaceable second codec | `IEphemeralStateSnapshotter`, `SystemTextJsonEphemeralStateSnapshotter`, and `EphemeralWorkflowEngineOptions.StateSnapshotter` were deleted. The engine no longer accepts, stores, or resolves a snapshot strategy. |
| B4: the default snapshotter used raw STJ and silently truncated unapproved nested polymorphism | `WorkflowInstance<TState>.CopyState()` and `PublishState()` now serialize and deserialize through `FixedWorkflowValueCodec` using the declared `TState`. A public engine/query regression proves an unapproved derived member throws instead of returning a degraded base object. |
| Positive codec complement | A second public engine/query regression proves an explicitly approved `[JsonDerivedType]` state member round-trips as its derived runtime type with derived-only data intact. |
| Guard gap: name-shaped checks did not find the snapshot hook or a second byte-producing implementation | The public-surface guard now rejects both snapshotter type names and the options property. A capability sweep freezes all raw `JsonSerializer.Serialize`, `SerializeToUtf8Bytes`, and `Deserialize` product call sites by exact source path and count: the one fixed codec plus explicitly reviewed envelope/protocol/provider-record framing only. Any additional call site fails the infrastructure lane pending explicit review. |
| Adjacent test regression | One concurrency test had put a live `RaceCoordinator` synchronization collaborator inside workflow business state. The test now captures it in the ephemeral step factory, leaving business state codec-safe without weakening the production codec. |

No serializer customization or replacement seam was introduced. No prior review artifact was
rewritten. B1, B2, and B3 from the earlier codec review remain unchanged and green.

## Red/green evidence for B4

- **Public replacement surface, red:** the augmented reflection guard failed because both
  `OrcaCore.Engine.Ephemeral.IEphemeralStateSnapshotter` and
  `SystemTextJsonEphemeralStateSnapshotter` were exported and the options property existed.
- **Public replacement surface, green:** the same guard passes after deleting the types and option.
- **Detached-state behavior, red:** a public ephemeral definition initialized a base-declared member
  with an unapproved derived value; start plus `Management.Instance(id).GetState<TState>()`
  returned without an exception, reproducing silent truncation.
- **Detached-state behavior, green:** the same public path throws `NotSupportedException`; an
  approved-polymorphism complement preserves the derived runtime type and `derived-only-data`.
- **Capability guard:** the source sweep finds exactly 30 raw JSON calls across 12 explicitly
  approved files. No ephemeral snapshot implementation appears in that allowlist.

## Reproduced evidence

| Lane | Result |
|---|---:|
| `dotnet build OrcaCore.slnx --no-restore -m:1 -v minimal` | succeeded, 0 warnings, 0 errors |
| Core | 414 passed, 0 failed, 0 skipped |
| Ephemeral | 151 passed, 0 failed, 0 skipped |
| Durable | 291 passed, 0 failed, 0 skipped |
| Hosting | 15 passed, 0 failed, 0 skipped |
| Guard infrastructure | 59 passed, 0 failed, 0 skipped; repeated three consecutive times with the same result and no `CS2012` |
| Expected-red guard lane | 0 passed, 104 intentional individually named failures, 0 skipped |
| Detached-state regressions | 2 passed: unapproved rejected, approved round-tripped without loss |
| Raw JSON capability sweep | 30 calls across the exact 12-file allowlist; no extra path/count |
| Green compile fixtures | fresh current-source package compiled; exact consumer compiled; 26 source and 26 package forbidden-member diagnostics verified; incomplete package rejected |
| Expected-red compile fixtures | 0 remaining; Section-4 product authoring package proof reports green |
| Strict OpenSpec validation | `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits` valid |
| Whitespace | `git diff --check` exited 0 with only benign LF-to-CRLF notices when stderr was shown |

The expected-red test command returns nonzero by design. Review every named failure and reject any
setup, discovery, restore, assertion, or newly-green result that is not a frozen later-section gap.

## Commands

```powershell
dotnet build OrcaCore.slnx --no-restore -m:1 -v minimal
dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --no-build --filter "Disposition=Infrastructure" -v minimal
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --no-build --filter "Disposition=ExpectedRed" -v minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
```

## Independent review questions

1. Does the frozen manifest reproduce exactly before and after validation?
2. Are `IEphemeralStateSnapshotter`, `SystemTextJsonEphemeralStateSnapshotter`, and every
   `StateSnapshotter` options/property/constructor path absent from the exported and source surface?
3. Can ordinary application code influence detached ephemeral state bytes through options, DI,
   direct construction, a public interface, or another replacement seam?
4. Do both `WorkflowInstance<TState>.PublishState()` and `CopyState()` use
   `FixedWorkflowValueCodec` directly, with no raw STJ fallback?
5. Does the public ephemeral engine/query path reject unapproved nested polymorphism instead of
   returning a truncated value, while approved polymorphism preserves derived-only data?
6. Does a capability-oriented sweep find every raw product JSON byte-producing call, and are all
   non-fixed-codec sites genuinely envelope/protocol/provider-record framing rather than workflow
   value codecs or replacement hooks?
7. Would adding another raw JSON call, snapshot implementation, exported replacement interface, or
   options hook fail the infrastructure lane?
8. Are B1, B2, and B3 still closed, all 104 expected-red failures later-owned, and all Section-6
   lease lifecycle scenarios still intentionally red?
9. Are the reviewer-authored evidence rows disclosed in the controlling rejection independently
   re-derived rather than accepted solely because their tests execute?
10. Is Section 4 now free of release blockers, so and only so may task 5.0 be authorized?

Record provenance, the exact frozen manifest/checksum, every command/result, findings, and verdict
in one new immutable dated review file. Only an approval with no release blocker may authorize task
5.0. Do not edit reviewed source, tests, tasks, specs, documentation, manifests, requests, or
existing review artifacts.
