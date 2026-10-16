# VERDICT: REJECT — Section 4 final codec closure, independent exit re-review

**Review date:** 2026-07-27
**Change:** `reshape-developer-facing-interfaces`
**Request under review:** [final codec-closure re-review request 2026-07-27](developer-facing-interface-section-04-final-codec-closure-rereview-request-2026-07-27.md)
**Decision:** Section 4 exit is rejected on one release blocker. Section 5 and task 5.0 remain blocked.

The three blockers from the [2026-07-27 independent rejection](developer-facing-interface-section-04-codec-remediation-independent-rereview-2026-07-27.md)
are genuinely closed, and the composite-scenario and name-based-guard weaknesses are genuinely
repaired. Every lane in the request reproduced exactly.

Approval is nevertheless withheld. Independent review question 1 asks whether exactly one
implementation chooses bytes for every workflow value **including the detached snapshot in both
engines**. It does not. The ephemeral engine still ships a public, documented, consumer-settable
snapshot codec that never routes through `FixedWorkflowValueCodec`, and its default implementation
accepts graphs the fixed codec rejects. This is the same defect class as B1/B2 — a second
byte-producing codec reachable by an ordinary consumer — in a path this packet did not examine.

## 0. Reviewer independence disclosure

This reviewer authored three artifacts inside the review target during a prior session:

- `tests/OrcaCore.DeveloperSurface.Guards/FixedCodecClosureContractGuards.cs` (created; subsequently
  edited by the implementer, who changed the converter guard to assert build-time `SFE-TYPE-002`)
- `tests/OrcaCore.Engine.Durable.Tests/Execution/WorkflowPayloadGraphPositionTests.cs` (created)
- `tests/OrcaCore.Engine.Durable.Tests/Versioning/DurableVersioningTests.cs` (one-line edit to the
  start-input helper content type)

This reviewer is therefore **not independent** with respect to the evidence in request rows
"Fixed-codec closure executable guards", "Independent graph-position durable regressions", and the
Section-4 composite-scenario repair, because those partly rest on tests written here. Those rows
were re-executed and reproduce, but a genuinely independent reviewer should re-derive them. The
finding below rests on neither: it was found by reading product source this reviewer did not write
and proved with an out-of-tree harness.

## 1. Provenance and immutable boundary

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`; `git merge-base --is-ancestor` confirms it is
  an ancestor of `HEAD`.
- Reviewed `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`.
- Reviewed tree: `2264e670493ecc76359d42ee5273028eb287a566` (verified by `git rev-parse HEAD^{tree}`).
- Frozen manifest:
  `docs/review/developer-facing-interface-section-04-final-codec-closure-dirty-manifest-2026-07-27.txt`.
- Frozen manifest SHA-256, independently recomputed:
  `1370D3462E5B555C33BEAFE32847D504785FFE77F6FFA89F96C7BAD28CE60604` — matches the request.
- The manifest contains exactly 347 entries.
- `git status --porcelain=v1 --untracked-files=all` reproduced the manifest with **zero**
  differences in either direction, before this verdict was written.
- OpenSpec task state reproduced: 45 checked, 67 unchecked, 112 total; 4.0–4.14 checked, 5.0 open.
- Source, tests, tasks, specs, documentation, manifests, requests, and prior review artifacts were
  treated as immutable. This review authored exactly one path: this file.
- Method: findings were proved with an **out-of-tree** console harness in the session scratchpad,
  referencing built product assemblies by `HintPath`. No repository file was created or modified.

## 2. Release-blocking finding

### B4 — The ephemeral detached-snapshot codec is public, replaceable, and unvalidated

The frozen contract scopes the fixed codec to detached snapshots and returned query values, and
forbids a replacement hook:

- `openspec/specs/workflow-contracts/spec.md:239` — "Selector and query snapshots SHALL be
  codec-detached."
- `openspec/changes/reshape-developer-facing-interfaces/design.md:138` — "Author selectors,
  workflow/DAG input, state, item snapshots, results, output, idempotency bytes, and **returned
  query values** are codec-detached."
- `docs/specs/12-acceptance-criteria.md:118-120` (AC-024) — "**No serializer replacement hook
  exists.**"

The path:

- `IEphemeralStateSnapshotter` is a **public** interface —
  `src/OrcaCore.Engine.Ephemeral/Management/IEphemeralStateSnapshotter.cs:9-15`.
- `SystemTextJsonEphemeralStateSnapshotter.Snapshot<TState>` calls plain
  `JsonSerializer.Serialize(state)` / `Deserialize<TState>` with no fixed-codec involvement, no
  graph validation, and no `orcacore-json-v1` binding — same file, lines 29-46.
- Its own XML documentation advertises the replacement: "Replace this through
  `EphemeralWorkflowEngineOptions.StateSnapshotter` when state uses types or constructors that are
  not supported by System.Text.Json" (lines 17-21).
- `EphemeralWorkflowEngineOptions.StateSnapshotter` is a **public `init` property** typed as that
  public interface — `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngineOptions.cs:30-31`.
- It is live on the application query path:
  `EphemeralManagement.GetState<TState>()` → `instance.CopyState(engine.StateSnapshotter)` at
  `src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs:408`, and
  `PublishState(options.StateSnapshotter)` at
  `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs:786`.

**Executable proof, public API only.**

```
=== PROBE G: public IEphemeralStateSnapshotter replacement hook ===
  IEphemeralStateSnapshotter exported : True
  Options.StateSnapshotter settable   : True
  authored Marker                     : authored
  Marker returned to the application  : REPLACED-BY-APPLICATION
```

An ordinary consumer supplied its own `IEphemeralStateSnapshotter` through the public options
record; the detached state returned by `Management.Instance(id).GetState<T>()` was produced by
application code rather than by the fixed codec. This is a serializer replacement hook, still
present and still documented as supported.

```
=== PROBE H: default snapshotter vs unapproved polymorphism ===
  returned Item runtime type       : SnapBase
  round-tripped JSON               : {"Value":"hello"}
```

The **default** snapshotter is equally outside the closure: a state whose declared member is
`SnapBase` and whose runtime value is `SnapDerived` was accepted, and the copy handed to the
application silently degraded to `SnapBase` with `DERIVED-ONLY-DATA` lost. `FixedWorkflowValueCodec`
rejects exactly this shape at every other boundary, and this packet's own guards assert that
rejection for terminal output. The same author graph is therefore rejected on one path and silently
truncated on another.

**Scope assessment, stated fairly.** This is not a later-section gap:

- Nothing schedules `IEphemeralStateSnapshotter` for removal or replacement — the string does not
  appear in `openspec/changes/reshape-developer-facing-interfaces/tasks.md`, `design.md`, or
  `docs/specs/17-selected-mode-capability-matrix.md`.
- No expected-red guard covers it; the string does not appear anywhere in
  `tests/OrcaCore.DeveloperSurface.Guards/` or `tests/OrcaCore.DeveloperSurface.BehaviorScenarios/`
  source. It is not in the 104 named reds.
- The request's own remediation table asserts closure that this path contradicts: "Durable and
  ephemeral structured execution, durable boundary payloads, state, branch/item results, and
  terminal output all call the one `FixedWorkflowValueCodec`."
- Question 1 names "detached snapshot" and "both engines" explicitly; question 8 asks whether the
  guards fail if "a public replacement hook" returns. This one never left, and nothing fails.

**Required remediation.** Route ephemeral detached snapshots through `FixedWorkflowValueCodec`,
make `IEphemeralStateSnapshotter` and `SystemTextJsonEphemeralStateSnapshotter` internal (or delete
them and the `StateSnapshotter` option), and add a guard that fails when either the public hook or a
second byte-producing snapshot implementation returns. If an escape hatch for STJ-hostile state is
genuinely wanted, it contradicts AC-024 as written and requires an explicit contract amendment
rather than an unremarked public property.

## 3. Confirmed closed

Verified independently; all accepted.

- **B1 — second unvalidated codec.** `IWorkflowTypeSerializerRegistry` now declares exactly one
  member, `TryGetSchemaIdentity`, with no default or concrete byte-producing method
  (`src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs:39-45`). The default-interface
  `Serialize`/`Deserialize` fork is gone. A repository-wide sweep of `JsonSerializer.Serialize` /
  `SerializeToUtf8Bytes` outside `FixedWorkflowValueCodec.cs` leaves only envelope/protocol framing
  (`DurableExecutionEnvelopeV2`, `DurableContinuationSignal`, `WorkflowEventCodec`,
  `DurableCommitMaterializer`) and provider column serialization, whose author-value bytes are
  already codec-produced — plus the ephemeral snapshotter in §2.
- **B2 — application converters.** `FixedWorkflowValueCodec.HasOnlyProductOwnedConverter` rejects
  application-owned type and member `JsonConverterAttribute` recursively, surfaced as public
  diagnostic `SFE-TYPE-002` (`WorkflowDiagnosticCatalog.cs:26`,
  `PublicStagedAuthoring.cs:592`). `IsSupportedDeclaredType` is `ConcurrentDictionary`-cached,
  which also addresses the per-serialize reflection cost raised as O-2 in the prior review.
- **B3 — raw start command.** `DurableLifecycleCommandHandler.ValidateStartInput` accepts only "no
  input" or an exactly-ordinal `orcacore-json-v1` pair and throws before any `WorkflowStartedEvent`
  is constructed. Independently re-proved: the raw-start probe that previously committed
  `application/x-foreign` / `DEADBEEF` now throws `ArgumentException` from the aggregate.
- **Composite scenario false-green.** Root, nested-property, collection-element, dictionary-key,
  dictionary-value, and cycle now execute as six independent cases, each with a fresh
  mutation-tracking store asserting zero provider calls. *(Reviewer wrote these; see §0.)*
- **Fingerprint identity.** `CompiledWorkflowPlan.CodecFormat` is now a direct alias of
  `FixedWorkflowValueCodec.Format` (`CompiledWorkflowPlan.cs:30`), so plan identity cannot drift
  from the codec constant, and the seed at line 68 is unchanged.
- **Regression from the remediation, fixed.** Three durable versioning tests had a start-input
  helper hard-coding `"application/json"`; they now use the codec content type and the lane is green.

## 4. Reproduced commands and results

| Command | Independent result | Matches request |
|---|---|---|
| Manifest SHA-256 | `1370D346…E60604` | yes |
| `git status --porcelain=v1 -uall` vs manifest | 347 entries, 0 differences | yes |
| `dotnet build OrcaCore.slnx --no-restore -m:1 -v minimal` | succeeded; 0 warnings, 0 errors | yes |
| Core | 414 passed, 0 failed, 0 skipped | yes |
| Ephemeral | 150 passed, 0 failed, 0 skipped | yes |
| Durable | 291 passed, 0 failed, 0 skipped | yes |
| Hosting | 15 passed, 0 failed, 0 skipped | yes |
| Guards `Disposition=Infrastructure` ×3 | 58/58, 58/58, 58/58; no `CS2012` | yes |
| Guards `Disposition=ExpectedRed` | exit 1 by design; 0 passed, 104 named failures, 0 skipped | yes |
| `run-compile-fixtures.ps1 -Disposition Green` | exit 0; fresh pack; 26 source + 26 package diagnostics; incomplete package rejected | yes |
| `run-compile-fixtures.ps1 -Disposition ExpectedRed` | exit 0; 0 remaining; Section-4 package proof green | yes |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | valid | yes |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | valid | yes |
| `git diff --check` | exit 0; benign LF→CRLF notices only | yes |
| Out-of-tree probes G and H | reproduced as quoted in §2 | not in request |

Every stated lane reproduced exactly. The 104 expected-red failures were reviewed: no restore,
discovery, setup, or newly-green result, and all remain owned by Sections 5–9, with the Section-6
lease lifecycle scenarios still intentionally red. No expected-red covers B4.

## 5. Answers to the independent review questions

1. **Exactly one implementation chooses bytes for every value including detached snapshots in both
   engines?** **No.** Every path examined routes through `FixedWorkflowValueCodec` except the
   ephemeral detached snapshot, which uses a separate, public, replaceable implementation (§2).
2. **Does the registry resolve schema identity only?** **Yes.** One member,
   `TryGetSchemaIdentity`; no byte-producing method and no execution-time consumer for bytes.
3. **Do public resultful workflows reject unapproved derived output and cycles before commit?**
   **Yes** for terminal output in both engines. **No** for the value the ephemeral engine hands
   back through `GetState<TState>()`, which truncates instead of rejecting.
4. **Are application converters rejected recursively at build with `SFE-TYPE-002`, while
   product-owned converters and approved `JsonDerivedTypeAttribute` still round-trip?** **Yes**, on
   all four counts.
5. **Do six independent cases reject before any provider call?** **Yes** — each with a fresh
   mutation-tracking store asserting zero calls. *(Reviewer-authored; see §0.)*
6. **Can a raw start command commit a foreign or incomplete content/payload pair?** **No.**
   Rejection precedes `WorkflowStartedEvent` construction and any provider mutation.
7. **Is `orcacore-json-v1` the one exact ordinal content type, bound into plan identity?** **Yes**,
   and the plan constant now aliases the codec constant directly.
8. **Do the guards fail if a second codec, public replacement hook, or bypass returns?** **Partly.**
   They now cover the registry shape, terminal output, approved polymorphism, converter rejection,
   and raw start content. They do **not** cover the snapshot path: no guard or scenario references
   the snapshotter at all, which is why B4 survived a packet explicitly aimed at this defect class.
9. **Are earlier blockers resolved, the 104 reds later-owned, and lease scenarios still red?**
   **Yes**, all three.
10. **Does the frozen manifest reproduce exactly?** **Yes** — 347 entries, stated SHA-256, zero
    differences.

## 6. Recommendations

1. **Fix B4 by deletion, not by adding validation.** The cheapest correct change is to make the
   snapshotter types internal, drop the `StateSnapshotter` option, and call
   `FixedWorkflowValueCodec` from `WorkflowInstance.CopyState`/`PublishState`. Adding graph
   validation to `SystemTextJsonEphemeralStateSnapshotter` would leave the public replacement hook,
   which is the part AC-024 names.
2. **Search by capability, not by name, before the next packet.** B1 and B4 are the same defect and
   were both invisible to name-shaped guards. A sweep for byte-producing calls
   (`JsonSerializer.Serialize`, `SerializeToUtf8Bytes`, `JsonSerializer.Deserialize`) outside the
   fixed codec takes one command and would have surfaced B4 during remediation rather than at
   review. Consider encoding that sweep as an infrastructure guard with an explicit allowlist of
   framing sites, so a new byte-producing call fails the lane by default.
3. **Adopt the public API surface snapshot.** `EphemeralWorkflowEngineOptions.StateSnapshotter` is
   a public replacement hook that survived four review cycles aimed at replacement hooks. A checked
   in `PublicAPI.Shipped.txt` makes every public member a reviewable diff and would have made this
   property visible on the day it shipped.
4. **Re-verify the reviewer-authored rows independently** before the next approval, per §0.

## 7. Exit decision

**Section 4 exit is REJECTED.**

**May Section 5 or task 5.0 begin? NO.**

One release blocker (B4) remains, in the same defect class the packet set out to close, on a path
the packet did not examine. It is small and self-contained: the remediation is a visibility change
plus one call-site redirect, and the guard gap that hid it is a single sweep.

Resolve B4, add the guard that fails when a public snapshot replacement hook or a second
byte-producing snapshot implementation returns, freeze a new exact manifest, rerun the complete
packet, and obtain a new independent approval — ideally from a reviewer who authored none of the
tests in the target.
