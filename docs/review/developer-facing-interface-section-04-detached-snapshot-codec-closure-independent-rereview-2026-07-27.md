# VERDICT: APPROVE — Section 4 detached-snapshot codec closure, independent exit re-review

**Review date:** 2026-07-27
**Change:** `reshape-developer-facing-interfaces`
**Request under review:** [detached-snapshot codec-closure re-review request 2026-07-27](developer-facing-interface-section-04-detached-snapshot-codec-closure-rereview-request-2026-07-27.md)
**Decision:** Section 4 exit is **approved**. No release blocker remains. Task 5.0 is authorized.

B4 is closed at the root rather than patched: the public replacement interface, its implementation,
and the options hook are deleted outright, and both ephemeral detached-state paths call
`FixedWorkflowValueCodec` directly. B1, B2, and B3 remain closed. Every lane in the request
reproduced exactly, and — per independent review question 9 — every evidence row that rested on
this reviewer's own in-tree tests was re-derived from scratch through an out-of-tree harness that
depends on none of them. All 21 independent checks passed.

Two non-blocking observations about guard strength are recorded in §5.

## 1. Provenance and immutable boundary

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`, confirmed an ancestor of `HEAD`.
- Reviewed `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`.
- Reviewed tree: `2264e670493ecc76359d42ee5273028eb287a566` (`git rev-parse HEAD^{tree}`).
- Frozen manifest:
  `docs/review/developer-facing-interface-section-04-detached-snapshot-codec-closure-dirty-manifest-2026-07-27.txt`.
- Frozen manifest SHA-256, independently recomputed:
  `EAE3888955083813C83561F6459AD81F257466AD1DD1D0C97B7AA5A4DC806EEB` — matches the request.
- The manifest contains exactly 352 entries.
- `git status --porcelain=v1 --untracked-files=all` reproduced the manifest with **zero** differences
  in either direction, before validation. After this verdict was added, excluding this one new path
  left the same 352 entries with zero differences.
- OpenSpec task state reproduced: 45 checked, 67 unchecked, 112 total; 4.0–4.14 checked, 5.0 open.
- Source, tests, tasks, specs, documentation, manifests, requests, and prior review artifacts were
  treated as immutable. This review authored exactly one path: this file.

## 2. Reviewer independence, and how it was mitigated

The controlling rejection disclosed that this reviewer authored
`FixedCodecClosureContractGuards.cs` and `WorkflowPayloadGraphPositionTests.cs` and edited
`DurableVersioningTests.cs`. That disclosure still stands and those files remain in the target.

Question 9 asks whether those rows were independently re-derived rather than accepted because their
tests execute. They were. An out-of-tree console harness (session scratchpad, product assemblies
referenced by `HintPath`; **no repository file created or modified**) re-established every claim
from the public product surface, sharing no code with any in-tree test:

```
--- Q2/Q3  exported replacement surface ---
  [PASS] IEphemeralStateSnapshotter absent
  [PASS] SystemTextJsonEphemeralStateSnapshotter absent
  [PASS] EphemeralWorkflowEngineOptions.StateSnapshotter absent
  [PASS] no ctor/member on options mentions Snapshotter
  [PASS] no exported ephemeral type mentions Snapshotter
  [PASS] no exported ephemeral member accepts a snapshot strategy

--- Q5  ephemeral detached state via public query ---
  [PASS] unapproved nested polymorphism rejected (not truncated)
  [PASS] approved polymorphism keeps derived runtime type
  [PASS] approved polymorphism keeps derived-only data

--- Q8/B1  durable typed End<TOutput> output ---
  [PASS] unapproved derived terminal output rejected

--- Q8/B2  application converter policy ---
  [PASS] application converter rejected at build
  [PASS] diagnostic is SFE-TYPE-002

--- Q8/B3  raw start command ---
  [PASS] foreign content type rejected before commit
  [PASS] incomplete pair (content type, no payload) rejected
  [PASS] incomplete pair (payload, no content type) rejected

--- Q8  six graph positions reject before any provider call ---
  [PASS] root
  [PASS] nested property
  [PASS] collection element
  [PASS] dictionary value
  [PASS] dictionary key
  [PASS] cycle

ALL 21 INDEPENDENT CHECKS PASSED
```

The two incomplete-pair cases were not in any prior packet; they were added here to test the
`ValidateStartInput` boundary beyond the foreign-content-type case. Both reject correctly.

## 3. B4 remediation, verified

- **Deletion, not deprecation.** `IEphemeralStateSnapshotter`,
  `SystemTextJsonEphemeralStateSnapshotter`, and `EphemeralWorkflowEngineOptions.StateSnapshotter`
  are absent from source and from the exported surface. The only surviving occurrences of the name
  anywhere in the repository are the three negative assertions in
  `tests/OrcaCore.DeveloperSurface.Guards/StateAndCodecContractGuards.cs:56-61`, which now fail if
  any of them returns. `IWorkflowInstance.CopyState()` / `PublishState()` no longer take a strategy
  parameter at all, so there is no seam left to inject through.
- **Both paths use the fixed codec with the declared type.**
  `WorkflowInstance<TState>.CopyStateWithFixedCodec` calls
  `FixedWorkflowValueCodec.Serialize(state, typeof(TState))` then `Deserialize`, and both
  `CopyState()` and `PublishState()` route through it
  (`src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs:831-873`). There is no raw-STJ
  fallback path on either.
- **Behavior at the public query boundary.** The truncation reproduced in the controlling rejection
  is gone: the same public definition and `Management.Instance(id).GetState<TState>()` call now
  throws `NotSupportedException`, and the approved `[JsonDerivedType]` complement returns the
  derived runtime type with `derived-only-data` intact (§2).
- **Structured value codecs completed too.** Both `IStructuredValueCodec` implementations —
  `StructuredEphemeralValueCodec` and `DurableStructuredValueCodec` — now delegate to
  `FixedWorkflowValueCodec` and no longer take an `IWorkflowTypeSerializerRegistry`. This closes the
  ephemeral scope-merge and branch/item copy paths that shared the original B1 defect.
- **The concurrency-test correction does not weaken the product.** `CoordinatedStep` now receives
  the `RaceCoordinator` through its step factory
  (`tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ExecutionLaneTests.cs:81,140`) instead of the
  collaborator living inside workflow business state. The test was adapted to the codec rather than
  the codec relaxed for the test — the correct direction.

## 4. Capability sweep, independently recomputed

The new guard
(`StateAndCodecContractGuards.cs:101-142`) freezes raw JSON call sites by exact path and count. An
independent sweep of `src/` with an equivalent pattern, excluding `bin`/`obj`, reproduces it
exactly — **30 matches across the same 12 files, with identical per-file counts**:

| File | Count |
|---|---:|
| `OrcaCore.Engine.Durable/Execution/DurableCommitMaterializer.cs` | 5 |
| `OrcaCore.Providers.PostgreSql/PostgreSqlResourcePoolStore.cs` | 4 |
| `OrcaCore.Providers.SqlServer/SqlServerResourcePoolStore.cs` | 4 |
| `OrcaCore.Providers.Redis/RedisProjectionStore.cs` | 3 |
| `OrcaCore.Abstractions/Durable/DurableContinuationSignal.cs` | 2 |
| `OrcaCore.Abstractions/Durable/DurableExecutionEnvelopeV2.cs` | 2 |
| `OrcaCore.Abstractions/Serialization/WorkflowEventCodec.cs` | 2 |
| `OrcaCore.Core/Compilation/FixedWorkflowValueCodec.cs` | 2 |
| `OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs` | 2 |
| `OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs` | 2 |
| `OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.Projections.cs` | 1 |
| `OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.Projections.cs` | 1 |

Each non-fixed-codec entry was classified by reading it, not by trusting the label:

- `DurableExecutionEnvelopeV2`, `DurableContinuationSignal`, `WorkflowEventCodec`,
  `DurableCommitMaterializer` — checkpoint/protocol/outbox **framing**. The author-value bytes they
  carry are already codec-produced; these serialize the record around them.
- The six provider files — persistence of projection/resource-pool **records** into database
  columns, not workflow value graphs.

A broader sweep for other byte-producing mechanisms (`Utf8JsonWriter`, `JsonNode`, `JsonDocument`,
`SerializeAsync`/`DeserializeAsync`, `BinaryFormatter`, `XmlSerializer`, `DataContractSerializer`,
MessagePack, protobuf) found only two further classes, both benign:

- Product-owned `JsonConverter<T>` implementations for strong identifiers
  (`InstanceIdJsonConverter.cs`, `StrongStringValueJsonConverterFactory.cs`). These run *inside* the
  fixed codec and are exactly what `HasOnlyProductOwnedConverter` admits; they are not an
  alternative codec.
- `DurableFiberDriverExecutor.Helpers.cs:849` — `JsonDocument.Parse` in `ToResumedPayload`, a
  read-side clone of an inbound event payload. It produces no persisted bytes.

No byte-producing path for workflow values exists outside `FixedWorkflowValueCodec`.

## 5. Non-blocking observations

- **O-1 — the sweep counts matching lines, not calls.** `StateAndCodecContractGuards.cs:132-136`
  emits one entry per matching *line*, so two raw calls placed on a single line inside an
  already-allowlisted file would not change the count. Low risk given the codebase's formatting, but
  the guard is slightly weaker than "freezes all 30 call sites" implies.
- **O-2 — the sweep is pattern-scoped, not capability-scoped.** It matches `JsonSerializer.` member
  calls. A future bypass written with `Utf8JsonWriter` directly, a different serializer package, or
  a local alias would not trip it. It is a real and worthwhile improvement over name-shaped checks,
  and it is accurate today, but it is not a proof of the general property. The public API surface
  snapshot recommended in the prior review (`PublicAPI.Shipped.txt`) remains the complement worth
  adding, since the B4 hook was a *public member* and would have been caught there on the day it
  shipped.
- **Method limit, stated plainly.** Question 7 asks whether adding another raw JSON call would fail
  the lane. This could not be tested destructively without editing the reviewed tree, which the
  request forbids. It is answered by construction: the guard compares a path→count dictionary with
  `BeEquivalentTo`, so any new path, removed path, or changed count fails — and the baseline was
  independently verified correct above. The same reasoning covers a new exported replacement
  interface or options hook, which the reflection assertions at lines 49-61 reject by name.

## 6. Reproduced commands and results

| Command | Independent result | Matches request |
|---|---|---|
| Manifest SHA-256 | `EAE38889…806EEB` | yes |
| `git status --porcelain=v1 -uall` vs manifest, before and after | 352 entries, 0 differences both times | yes |
| `dotnet build OrcaCore.slnx --no-restore -m:1 -v minimal` | succeeded; 0 warnings, 0 errors | yes |
| Core | 414 passed, 0 failed, 0 skipped | yes |
| Ephemeral | 151 passed, 0 failed, 0 skipped | yes |
| Durable | 291 passed, 0 failed, 0 skipped | yes |
| Hosting | 15 passed, 0 failed, 0 skipped | yes |
| Guards `Disposition=Infrastructure` ×3 | 59/59, 59/59, 59/59; no `CS2012` | yes |
| Guards `Disposition=ExpectedRed` | exit 1 by design; 0 passed, 104 named failures, 0 skipped | yes |
| `run-compile-fixtures.ps1 -Disposition Green` | exit 0; fresh pack; 26 source + 26 package diagnostics; incomplete package rejected | yes |
| `run-compile-fixtures.ps1 -Disposition ExpectedRed` | exit 0; 0 remaining; Section-4 package proof green | yes |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | valid | yes |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | valid | yes |
| `git diff --check` | exit 0; benign LF→CRLF notices only | yes |
| Independent raw-JSON sweep | 30 calls / 12 files, per-file counts identical to the guard allowlist | yes |
| Out-of-tree closure harness | 21/21 independent checks passed | not in request |

The 104 expected-red failures were reviewed: no restore, discovery, setup, or newly-green result;
all remain owned by Sections 5–9; the Section-6 lease lifecycle scenarios are still intentionally
red. The count is unchanged from the prior two packets, confirming no Section-4 work leaked into
the later-owned inventory.

## 7. Answers to the independent review questions

1. **Manifest reproduces before and after?** **Yes** — 352 entries and the stated SHA-256 both
   times; after this verdict, one added path and nothing else.
2. **Snapshotter types and every `StateSnapshotter` path absent from exported and source surface?**
   **Yes** — verified by reflection over the exported surface and by source search; the only
   surviving occurrences are the guard's own negative assertions.
3. **Can application code influence detached ephemeral state bytes?** **No** — no options property,
   no public interface, no constructor parameter, no DI seam. `CopyState`/`PublishState` take no
   strategy argument.
4. **Do both use `FixedWorkflowValueCodec` with no raw STJ fallback?** **Yes**, through a single
   shared helper using the declared `TState`.
5. **Does the public query path reject unapproved polymorphism and preserve approved?** **Yes** —
   independently reproduced: `NotSupportedException` for unapproved; derived runtime type and
   derived-only data intact for approved.
6. **Does a capability sweep find every raw JSON call, and are the non-codec sites genuine framing?**
   **Yes** — 30/12 reproduced exactly, and each non-codec site was read and classified as
   envelope/protocol framing or provider-record persistence. See O-1/O-2 for the sweep's limits.
7. **Would a new raw JSON call, snapshot implementation, exported interface, or options hook fail
   the lane?** **Yes** for everything the guards match, by the dictionary-equivalence and reflection
   assertions; see the method limit in §5.
8. **B1–B3 still closed, 104 reds later-owned, lease scenarios still red?** **Yes**, all three,
   independently re-derived rather than carried forward.
9. **Reviewer-authored rows independently re-derived?** **Yes** — §2; all 21 out-of-tree checks pass
   without reference to any in-tree test.
10. **Is Section 4 free of release blockers?** **Yes.**

## 8. Exit decision

**Section 4 exit is APPROVED. No release blocker remains.**

**May Section 5 and task 5.0 begin? YES.** This approval authorizes task 5.0 and Section-5 source
work under the existing gate discipline. It does not pre-approve any Section-5 exit.

The observations in §5 are guard-strength improvements for a future packet, not conditions on this
approval. The recommendation carried forward is the public API surface snapshot: the B4 hook was a
public member that survived four review cycles aimed squarely at public replacement hooks, and a
checked-in surface file is the mechanism that makes that class of defect visible on the day it
ships rather than four packets later.
