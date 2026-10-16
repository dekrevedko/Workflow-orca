# Section 4 final codec closure: independent exit re-review request

**Date:** 2026-07-27  
**Requested verdict:** approve or reject Section 4 exit after independently reproducing the
executable findings in the latest immutable rejection:

- [2026-07-27 independent rejection](developer-facing-interface-section-04-codec-remediation-independent-rereview-2026-07-27.md)

That rejection controls this target. Section 5 remains blocked: this request does not authorize
task 5.0 or any Section-5 source work.

## Review snapshot

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`; tree
  `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`; the stable commit is unchanged from the rejected reviews.
- Review target: `HEAD` plus every entry in the
  [final codec-closure dirty manifest](developer-facing-interface-section-04-final-codec-closure-dirty-manifest-2026-07-27.txt).
- Frozen manifest: 347 entries (289 modified, 8 deleted, 50 untracked); SHA-256
  `1370D3462E5B555C33BEAFE32847D504785FFE77F6FFA89F96C7BAD28CE60604`.
- OpenSpec task state: 45 done, 67 pending, 112 total.
  Tasks 4.0 through 4.14 are checked; task 5.0 remains open and independently gated.
- All prior review files, requests, and manifests are immutable inputs. None was edited.

## Release-blocker remediation

| Finding | Remediation to verify |
|---|---|
| B1: resultful `End<TOutput>` used a second unvalidated codec | The default methods that serialized bytes were removed from `IWorkflowTypeSerializerRegistry`; it now resolves schema identity only. Durable and ephemeral structured execution, durable boundary payloads, state, branch/item results, and terminal output all call the one `FixedWorkflowValueCodec`. Public-engine regressions reject an unapproved derived terminal output before completion/terminal commit, and cyclic terminal output is rejected before completion. |
| B2: application converters could choose nondeterministic bytes labelled `orcacore-json-v1` | Definition compilation recursively rejects application-owned type and member `JsonConverterAttribute` use with public diagnostic `SFE-TYPE-002`, including a converter on the external input when state has no converter. Runtime serialization and deserialization repeat the declared-type policy for dynamic boundary values. Product-owned converters on product-owned strong values remain supported. The plan codec constant now directly aliases the fixed codec format. |
| B3: raw `StartWorkflowCommand` could commit a foreign content type and payload | `DurableLifecycleCommandHandler` accepts either no input or an input payload labelled exactly `orcacore-json-v1` using ordinal comparison; a foreign or incomplete pair throws before a start event is created. Both aggregate and public `DurableCommandProcessor` regressions prove no poison start fact reaches the provider. Public raw-command surface removal remains deferred to the package split in Section 7. |
| Composite graph scenario falsely proved unvisited branches | Root, nested-property, collection-element, dictionary-key, dictionary-value, and cycle cases now execute independently. Each public-runtime case uses a fresh mutation-tracking event store and asserts zero provider calls. The Section-4 `fixed-codec-determinism` driver mirrors the independent cases and verifies no idempotency/start mapping appears. |
| Name-based guards missed the second codec | A semantic infrastructure guard reflects the internal type registry and requires its only method to be `TryGetSchemaIdentity`; it also proves the compiled plan and fixed codec share exactly `orcacore-json-v1`. Separate executable guards cover terminal output, approved polymorphism without data loss, converter rejection, and raw start-content rejection. |

## Reproduced evidence

| Lane | Result |
|---|---:|
| `dotnet build OrcaCore.slnx --no-restore -m:1 -v minimal` | succeeded, 0 warnings, 0 errors |
| Core | 414 passed, 0 failed, 0 skipped |
| Ephemeral | 150 passed, 0 failed, 0 skipped |
| Durable | 291 passed, 0 failed, 0 skipped |
| Hosting | 15 passed, 0 failed, 0 skipped |
| Guard infrastructure | 58 passed, 0 failed, 0 skipped; repeated three consecutive times with the same result and no `CS2012` |
| Expected-red guard lane | 0 passed, 104 intentional individually named failures, 0 skipped |
| Four Section-4 executable scenario guards | 4 passed, 0 failed, 0 skipped |
| Fixed-codec closure executable guards | 4 passed, 0 failed, 0 skipped |
| Independent graph-position durable regressions | root/member/collection/dictionary key/dictionary value/cycle all pass with zero provider calls |
| Green compile fixtures | fresh current-source package compiled; exact consumer compiled; 26 source and 26 package forbidden-member diagnostics verified; incomplete package rejected |
| Expected-red compile fixtures | 0 remaining; Section-4 product authoring package proof reports green |
| Strict OpenSpec validation | `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits` valid |
| Whitespace | `git diff --check` exited 0 with only benign LF-to-CRLF notices |

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

1. Is there exactly one implementation that chooses bytes for every workflow input, state,
   branch/item result, terminal output, event payload, and detached snapshot in both engines?
2. Does `IWorkflowTypeSerializerRegistry` resolve schema identity only, with no default or concrete
   serialization method and no execution-time path that consumes it for bytes?
3. Do public resultful durable and ephemeral workflows reject unapproved derived output and cycles
   before a completed status, terminal fact, output checkpoint, or equivalent commit?
4. Are application-owned converter attributes rejected recursively at definition build for state,
   external input, output, and serializable members with `SFE-TYPE-002`, while product-owned strong
   value converters and explicitly approved `JsonDerivedTypeAttribute` contracts still round-trip?
5. Before any provider call, do six independent cases reject root, nested-property,
   collection-element, dictionary-key, dictionary-value, and cyclic invalid graphs?
6. Can a raw start command commit neither a foreign content type nor an incomplete content/payload
   pair, and does rejection occur before `WorkflowStartedEvent` or provider mutation?
7. Is `orcacore-json-v1` the one exact ordinal content type and directly bound into compiled-plan
   fingerprint identity?
8. Do the semantic and executable guards fail if a second byte-producing codec, public replacement
   hook, converter bypass, output bypass, or raw-content bypass returns?
9. Are all earlier Section-4 blockers still resolved, all 104 expected-red failures owned by later
   sections, and all Section-6 lease lifecycle scenarios still intentionally red?
10. Does the frozen manifest reproduce exactly before the reviewer adds one new immutable verdict?

Record provenance, the exact frozen manifest/checksum, every command/result, findings, and verdict
in one new immutable dated review file. Only an approval with no release blocker may authorize task
5.0. Do not edit reviewed source, tests, tasks, specs, documentation, manifests, requests, or
existing review artifacts.
