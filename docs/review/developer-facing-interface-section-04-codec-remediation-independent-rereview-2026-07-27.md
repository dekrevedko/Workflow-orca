# VERDICT: REJECT — Section 4 codec remediation, second independent exit re-review

**Review date:** 2026-07-27
**Change:** `reshape-developer-facing-interfaces`
**Request under review:** [codec-remediation re-review request 2026-07-22](developer-facing-interface-section-04-codec-remediation-rereview-request-2026-07-22.md)
**Decision:** Section 4 exit is rejected. Section 5 and task 5.0 remain blocked.

This is a second, independent verdict on the same frozen target already rejected by
[the 2026-07-22 independent re-review](developer-facing-interface-section-04-codec-remediation-independent-rereview-2026-07-22.md).
It reaches the same direction but on partially different grounds: two of that review's three
release blockers are confirmed and strengthened with executable proof, one is confirmed only in a
materially narrower scope than stated, and one is reclassified from product defect to evidence
defect because the implementation under it is in fact correct. Two new observations are recorded.

The controlling reason for rejection is unchanged and simple: the frozen contract says the fixed
codec governs **input/state/result/output/event/DAG** values and that unapproved polymorphic graphs
fail **before commit**. Today it governs input and state. It does not govern typed completion
output, and application `JsonConverter` declarations can select the persisted bytes under the
`orcacore-json-v1` label.

## 1. Provenance and immutable boundary

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Reviewed `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`.
- Reviewed tree: `2264e670493ecc76359d42ee5273028eb287a566` (verified via `git rev-parse HEAD^{tree}`).
- The baseline is an ancestor of `HEAD`; the stable commit is unchanged from all prior rejections.
- Frozen manifest:
  `docs/review/developer-facing-interface-section-04-codec-remediation-dirty-manifest-2026-07-22.txt`.
- Frozen manifest SHA-256, independently recomputed:
  `4A9BE59EF3171186EEAFDD2FFAB250B618303D8420F2EB5DA1225E32AD0D39A0` — matches the request.
- The manifest contains exactly 339 entries.
- `git status --porcelain=v1 --untracked-files=all` reproduced the manifest with **one** added path:
  `?? docs/review/developer-facing-interface-section-04-codec-remediation-independent-rereview-2026-07-22.md`,
  which is the prior reviewer's own verdict, authored after the manifest was frozen. Zero other
  differences, in either direction.
- OpenSpec task state reproduced: 45 checked, 67 unchecked, 112 total. Tasks 4.0–4.14 checked,
  task 5.0 unchecked.
- Source, tests, tasks, specs, documentation, manifests, requests, and every prior review artifact
  were treated as immutable. This review authored exactly one path in the repository: this file.

### Method note — out-of-tree executable probes

Prior rejections in this series rested largely on source reading, which the request rightly treats
as weaker than execution. To avoid that weakness without touching the frozen tree, findings below
were reproduced by an **out-of-tree** console harness built in the session scratchpad
(`.../scratchpad/codec-probe/`), referencing the already-built product assemblies by `HintPath`
only. No repository file, project, or `.slnx` entry was created or modified; the harness produces
no artifact inside the repository. Probe transcripts are quoted verbatim below.

## 2. Release-blocking findings

### B1 — The fixed codec does not govern typed completion output (CONFIRMED, scope corrected)

The frozen contract is explicit that the fixed codec covers output values and rejects unapproved
polymorphism before commit:

- `openspec/specs/workflow-contracts/spec.md:239` — "…for supported input/state/result/**output**/event/DAG
  values… Registration SHALL reject unsupported cyclic or unapproved polymorphic graphs before commit".
- `openspec/changes/reshape-developer-facing-interfaces/design.md:138` — same, naming output explicitly.
- `docs/specs/12-acceptance-criteria.md:118-120` (AC-024).

There are two independent serialization paths in the product:

1. **Fixed, validating codec** — `JsonWorkflowPayloadSerializer`
   (`src/OrcaCore.Engine.Durable/Execution/JsonWorkflowPayloadSerializer.cs:21`), whose `Serialize`
   calls `ValidateGraph` at lines 29-35 with a recursive walk at lines 66-191.
2. **Unvalidated registry codec** — the *default interface method*
   `IWorkflowTypeSerializerRegistry.Serialize` at
   `src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs:49-52`, which calls
   `JsonSerializer.SerializeToUtf8Bytes(value, declaredType)` directly, with no validation and no
   content-type binding.

Typed completion output is routed through path 2 in **both** engines:

- Durable: `DurableFiberDriverExecutor.Terminals.cs:88-100` → `DurableStructuredValueCodec`
  (`DurableFiberDriverExecutor.Models.cs:12-28`) → registry.
- Ephemeral: `InMemoryExecutionStateAdapter.cs:76` and `:266-283` →
  `StructuredEphemeralValueCodec` (`InMemoryExecutionStateAdapter.Helpers.cs:45-58`) → registry.
- Ephemeral scope merge and branch/item copies use the same codec via
  `src/OrcaCore.Core/Execution/ScopeMergeAdapter.cs:29,110,205`.

**Correction to the 2026-07-22 rejection.** That review stated the gap also covers "current
detached state" and cited the ephemeral state adapter as evidence that state bypasses the
validator. Durable state does **not** bypass it. Durable attempt-copy, checkpoint, and terminal
state all go through the fixed codec at
`DurableFiberDriverExecutor.Helpers.cs:74,85,166,486` and `Terminals.cs:59`. This is not a
pedantic distinction: it changes the size of the remediation and it changes which regressions are
missing. Probe A below demonstrates the difference.

**Executable proof.** Probe A used a state whose declared member type is `CodecBase` holding a
`CodecDerived`; it was rejected, because state is validated:

```
=== PROBE A: typed durable End<CodecBase> returning CodecDerived ===
  REJECTED: NotSupportedException: Unapproved polymorphic payload 'CodecProbe.CodecDerived' for
  declared type 'CodecProbe.CodecBase' is not supported by orcacore-json-v1.
```

Probe A2 isolates the output path by making the state graph fully approved (declared member type
== runtime type) and introducing the substitution only in the `End<CodecBase>` projection:

```
=== PROBE A2: approved state graph, unapproved substitution only in End<CodecBase> projection ===
  status            : Completed
  output type name  : CodecProbe.CodecBase
  persisted bytes   : {"Value":"hello"}
  VERDICT           : accepted without validation; SECRET-DERIVED-DATA silently dropped
```

The workflow committed successfully, the terminal checkpoint holds `{"Value":"hello"}`, and the
`Detail` member carrying `SECRET-DERIVED-DATA` was silently discarded. This is exactly the
"unapproved runtime-type substitution" the contract requires to fail before commit, and it is
a silent data-loss path on a completed durable instance.

The ephemeral equivalent is source-verified only, not executed: the public ephemeral snapshot
(`src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`) exposes no typed output yet,
so there is no public observation point before Section 7. The code path is the same shared
default interface method, so the same defect is present by construction.

**Required remediation:** one validating codec must govern every implemented Section-4
input/state/result/output path in both engines, with regressions for unapproved polymorphism and
cycles in typed output specifically, and pre-mutation proof for the durable case.

### B2 — Application `JsonConverter` declarations select the persisted bytes (CONFIRMED, strengthened)

AC-024 requires the codec to produce "identical bytes for the same normalized graph/order across
hosts" and states "No serializer replacement hook exists."

`ValidateGraph` inspects `JsonDerivedTypeAttribute` (line 151) and `JsonIgnoreAttribute` (line 175)
but neither rejects nor allowlists `JsonConverterAttribute`. After validation, line 35 hands the
value to default `System.Text.Json`, which honors application converters. The admission gate
`DefaultWorkflowTypeSerializerRegistry.TryGetSchemaIdentity`
(`DefinitionCompilerOptions.cs:67-89`) accepts such a type, because STJ can produce metadata for
it — so the type is "supported" by the product's own definition.

**Executable proof.** Probe C declares `[JsonConverter(typeof(HostileConverter))]` on the start
input type and starts the same logical value twice:

```
=== PROBE C: application [JsonConverter] on the start input type ===
  ctype             : orcacore-json-v1
  bytes run 1       : "ATTACKER-CHOSEN-cc06b52a-d665-47e1-8d9e-6a8406a0d1e0"
  bytes run 2       : "ATTACKER-CHOSEN-34917a35-3423-4b8f-a051-fde6ff376cec"
  deterministic     : False
```

Two identical input graphs produced different persisted bytes under the `orcacore-json-v1`
content type. This directly falsifies the frozen `fixed-codec-determinism` assertion
(`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/state-and-codec-scenarios.json:5`: "Bytes and
type fidelity are deterministic…; no codec replacement seam exists") for a type the product
admits as supported. It is a stronger result than the prior review's source-level argument: it is
not a theoretical hook, it is observed nondeterminism in committed durable bytes.

**Required remediation:** define the converter policy explicitly and enforce it. See §7.2 for the
recommended enforcement point.

### B3 — A public durable command surface commits bytes the fixed codec never produced (CONFIRMED)

- `DurableCommandProcessor` is public with public constructors —
  `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:13,29-43`.
- Its `ProcessAsync(StartWorkflowCommand, …)` overload is public — lines 79-89.
- `StartWorkflowCommand.InputContentType` / `InputPayload` are public `init` members —
  `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs:63-70`.
- `DurableLifecycleCommandHandler.Handle` copies both into `WorkflowStartedEvent` with no
  validation — `src/OrcaCore.Engine.Durable/Aggregates/DurableLifecycleCommandHandler.cs:18-34`.
- Hosting registers the processor as a resolvable concrete service —
  `src/OrcaCore.Hosting/OrcaCoreServiceCollectionExtensions.cs:68-71`.

**Executable proof.** Probe B, using only public API:

```
=== PROBE B: public DurableCommandProcessor raw start ===
  commit outcome    : Committed
  persisted ctype   : application/x-attacker-protobuf
  persisted bytes   : DEADBEEF
```

The durable stream now holds a start fact the fixed codec never produced. On read, the driver
rejects it (`DurableFiberDriverExecutor.Helpers.cs:25-31` →
`JsonWorkflowPayloadSerializer.ValidateContentType`), so the practical outcome is a permanently
unrunnable instance rather than a codec substitution — a poison-pill durability defect rather than
a confidentiality/integrity one. Labelling non-codec bytes `orcacore-json-v1` instead avoids even
that trip-wire at the persistence boundary.

**Scope caveat, stated fairly.** A reasonable reading is that `DurableCommandProcessor` is an
advanced runtime-protocol surface whose *removal from the application package* is Section-7 work
(`openspec/specs/workflow-contracts/spec.md:230-234` forbids exactly this kind of durable-command
type in application-facing public signatures, and that guard is expected-red today). Under that
reading B3 is later-owned. It is nonetheless reported here because independent review question 1
asks specifically about "any equivalent surface", and the request's remediation table asserts a
closure — "Hosting registers and resolves no serializer/codec service" — that is true as written
but is used to support a broader claim that is not true today. A one-line aggregate-side content-type
check (§7.4) closes the durability half immediately and is independent of package reduction.

## 3. Reclassified: the executable scenario is false-green, but the implementation under it is correct

The 2026-07-22 rejection listed the scenario's proof weakness as a third release blocker, and
included "there is no dictionary-key regression" among its reasons — phrasing that reads as a
possible product gap. It is not one.

The scenario weakness is real. `Phase0ScenarioHost.cs:114-137` makes **one** start call with
invalid derived values in `Member`, `Items`, and `Map` simultaneously. `ValidateGraph` throws at
the first invalid value it reaches, so that single execution cannot traverse the collection or
dictionary branches, and the scenario asserts nothing about provider calls before and after the
failure — so the frozen "fail before commit" claim is not executed either. The four scenario
guards pass because observing one expected exception satisfies the driver.

But the branches themselves work. Probes D and E each present a graph in which **only** the
collection element, or **only** the dictionary key, is unapproved:

```
=== PROBE D: only a collection element is unapproved ===
  REJECTED          : NotSupportedException: Unapproved polymorphic payload 'CodecProbe.CodecDerived' …

=== PROBE E: only a dictionary key is unapproved ===
  REJECTED          : NotSupportedException: Unapproved polymorphic payload 'CodecProbe.CodecDerived' …
```

Both are rejected. `ValidateGraph`'s dictionary (lines 78-116), collection (lines 118-148), and
property (lines 150-191) traversals are correct, including dictionary keys.

**Classification:** evidence defect, not product defect. It must still be fixed before exit,
because the request makes truthful green evidence part of the bar, but it requires only test
work and it should not be scoped or estimated as a codec fix.

## 4. Additional observations (not exit-blocking)

- **O-1 — Plan identity binds the codec name but not the codec.** `CompiledWorkflowPlan` folds the
  literal `orcacore-json-v1` into the fingerprint seed (`CompiledWorkflowPlan.cs:30,68`) while
  `SerializerRegistry` is an injectable constructor parameter excluded from that seed
  (`CompiledWorkflowPlan.cs:45,65`). Two plans with different registries — therefore different
  bytes — carry identical fingerprints. The seam is internal today, so this is not a consumer
  defect, but it means the fingerprint binds a *string*, not the codec, which is not what
  "codec identity in plan identity" implies.
- **O-2 — `ValidateGraph` is a full reflection walk on every serialize.** It runs per checkpoint,
  per attempt copy (`Helpers.cs:74,85`), and per commit (`Helpers.cs:166`), doing
  `GetProperties`/`GetCustomAttributes`/`GetValue` over the entire object graph each time, with no
  per-type caching. On durable workloads that checkpoint every step this is a hot path. No
  benchmark in `benchmarks/OrcaCore.Benchmarks/` covers it. Recommend measuring before Section 5
  rather than after.

## 5. Remediations confirmed sound

Independently verified as claimed, and accepted:

- `IWorkflowPayloadSerializer` and `JsonWorkflowPayloadSerializer` are `internal`
  (`JsonWorkflowPayloadSerializer.cs:9,21`); no exported type carries either name.
- The public `DurableWorkflowRuntime` constructor takes no serializer parameter and constructs the
  fixed implementation (`DurableWorkflowRuntime.cs:33-45`); the injection seam at line 57 is
  internal.
- Hosting neither registers nor resolves any payload serializer or codec service
  (`OrcaCoreServiceCollectionExtensions.cs:55-95`); DI ordering cannot replace it.
- Durable **state** — attempt copies, checkpoints, terminal state — is fully governed by the fixed
  validating codec (Probe A).
- Root, nested-property, collection-element, dictionary-key, and dictionary-value unapproved
  polymorphism are all correctly rejected by the validator (Probes A, D, E).
- Cyclic graphs are rejected via reference tracking (`JsonWorkflowPayloadSerializer.cs:86,126,163`).
- Statically approved exact `JsonDerivedTypeAttribute` contracts remain valid (lines 150-155) and
  round-trip.
- The fingerprint regression is genuinely independent: `DefinitionModelTests.cs:15-28` recomputes
  SHA-256 with `System.Security.Cryptography` over a hard-coded `orcacore-json-v1` literal rather
  than comparing two product outputs. This answers question 5 affirmatively.
- All earlier Section-4 remediations (strong values, reduced surface, consumer compile fixtures,
  root-only fan-out, legacy removal, infrastructure race) reproduce green with no regression.

## 6. Reproduced commands and results

| Command | Result |
|---|---|
| `git rev-parse HEAD^{tree}` | `2264e670493ecc76359d42ee5273028eb287a566` — matches request |
| `Get-FileHash -Algorithm SHA256 <frozen manifest>` | `4A9BE5…D0A3A0` — matches request |
| `git status --porcelain=v1 --untracked-files=all` vs manifest | 339 entries reproduced; +1 path (prior reviewer's verdict); 0 other differences |
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | Build succeeded; 0 warnings, 0 errors |
| `dotnet test tests/OrcaCore.Core.Tests/…` | 410 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/…` | 148 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Engine.Durable.Tests/…` | 282 passed, 0 failed, 0 skipped |
| `dotnet test tests/OrcaCore.Hosting.Tests/…` | 15 passed, 0 failed, 0 skipped |
| Guards `--filter "Disposition=Infrastructure"` | 53 passed, 0 failed, 0 skipped; no `CS2012` |
| Guards `--filter "Disposition=ExpectedRed"` | Exit 1 by design; 0 passed, 104 named failures, 0 skipped |
| `dotnet test … --filter "FullyQualifiedName~WorkflowPayloadSerializationTests"` | 9 passed, 0 failed, 0 skipped |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | Exit 0; valid |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | Exit 0; valid |
| `git diff --check` | Exit 0; benign LF→CRLF notices only |
| Out-of-tree codec probe harness | 6 probes; results quoted in §2 and §3 |

Every reproduced lane matched the request's stated evidence exactly. The green lanes are green
because their assertions do not reach the paths in B1–B3; the 104 expected-red failures were
reviewed and remain owned by Sections 5–9, with no restore, discovery, setup, or newly-green
signal, and no red covering B1–B3.

## 7. Recommended decisions

These are recommendations, not exit conditions. Where they differ from the remediation the prior
review demanded, the difference is deliberate.

**7.1 — Delete the second codec instead of duplicating validation into it.** The root cause of B1
is that `IWorkflowTypeSerializerRegistry` carries a *default interface method* implementation of
`Serialize`/`Deserialize` (`DefinitionCompilerOptions.cs:49-60`). A default interface method is an
invisible fork of a contract that the codebase claims has exactly one implementation. Make those
members abstract (or drop serialization from the registry entirely, leaving it a schema-identity
resolver), and route `DurableStructuredValueCodec` and `StructuredEphemeralValueCodec` through the
same fixed implementation as `JsonWorkflowPayloadSerializer`. Adding a second `ValidateGraph` call
site would close the symptom and leave the fork.

**7.2 — Enforce the converter policy at compile time, not serialize time.**
`DefaultWorkflowTypeSerializerRegistry.TryGetSchemaIdentity` already exists as the per-type
admission gate and already runs at definition compile. Rejecting non-product `JsonConverterAttribute`
declarations there gives three things the runtime check cannot: failure at `Build()` with a
catalogued `SFE-…` diagnostic instead of at first start, "before commit" for free, and a far better
developer experience than a `NotSupportedException` from inside a durable start. The same argument
applies to most of `ValidateGraph`: declared-type shape is a property of the *type*, not the value,
so it can be resolved once per type and cached (addressing O-2). Only genuine value-dependent
checks — runtime substitution and cycles — need to stay on the serialize path.

**7.3 — Decide the converter question explicitly rather than by omission.** Either converters are
banned (simple, matches "one certified codec" literally, breaks common domain types like custom
value objects), or a product-owned allowlist is defined and the contract amended. Silence is the
one option that cannot be defended, because the guard fixture currently *asserts* determinism that
Probe C disproves.

**7.4 — Validate the content type where it is committed, not only where it is read.** One check in
`DurableLifecycleCommandHandler.Handle` rejecting any `InputContentType` other than
`orcacore-json-v1` converts B3 from "commits a permanently unrunnable instance" to "rejects a bad
command", costs one line, and is independent of whether `DurableCommandProcessor` stays public.
Package reduction (Section 7) then handles the surface question separately.

**7.5 — Make the scenario guard fail when a path is not observed.** Split the polymorphism scenario
into five independently-executed cases (root, nested property, collection element, dictionary key,
dictionary value), have each record the site it exercised, and assert the recorded set — rather
than asserting that *an* exception was seen. Add a provider call-count delta around each durable
rejection to execute the "before commit" claim. This is the general fix for the false-green class
that has now caused four rejection cycles.

**7.6 — Replace name-matching surface guards with an API-surface snapshot.**
`StateAndCodecContractGuards.cs:44-70` and `Phase0ScenarioHost.cs:149-155` test for *type-name
strings* and *constructor parameter-name substrings*. That is why they stayed green across every
finding in this series: they can only detect a seam someone remembered to name. A checked-in public
API surface file (`Microsoft.CodeAnalysis.PublicApiAnalyzers`, `PublicAPI.Shipped.txt`) makes every
public-surface change a reviewable diff that cannot be gamed by renaming, and would have caught
`DurableCommandProcessor` on the day it went public.

**7.7 — Process: encode the exit bar before remediating against it.** Four consecutive rejections
on one section, each on grounds the previous packet's green evidence did not cover, is a signal
about the loop rather than the code. Writing each release blocker as a named executable guard that
is red *before* remediation and green *after* makes exit mechanically decidable and makes a
false-green packet structurally impossible to submit.

## 8. Answers to the independent review questions

1. **Can an ordinary consumer replace the codec through any equivalent surface?** **No closure is
   proven.** The serializer interface / runtime constructor / hosting / DI-ordering seams are
   genuinely closed. The public `DurableCommandProcessor` raw-start route (B3, Probe B) and the
   application `JsonConverter` route (B2, Probe C) remain.
2. **Does the public durable runtime always use the fixed implementation, with the seam internal?**
   **Yes**, for that constructor boundary.
3. **Are cycles and unapproved substitutions rejected in root, nested, collection, and dictionary
   key/value positions before provider mutation?** **Partly.** For durable start input and durable
   state: yes, in every position, verified executably (Probes A, D, E). For typed completion
   output: no, in any position (Probe A2). Pre-mutation proof exists for one nested case only.
4. **Does an approved `JsonDerivedTypeAttribute` contract still round-trip?** **Yes.**
5. **Is `orcacore-json-v1` bound into plan identity, proven by independent recomputation?** **Yes** —
   `DefinitionModelTests.cs:15-28` recomputes the hash with BCL primitives and a hard-coded literal.
   See O-1 for what that binding does and does not cover.
6. **Does the executable scenario prove persisted bytes, detachment, null fidelity, every rejection
   path, and public-surface closure?** **No.** One call cannot traverse three branches; no
   pre-commit provider assertion exists; the surface check is name-based.
7. **Are all earlier Section-4 blockers still resolved, all 104 expected-reds later-owned, and the
   Section-6 lease scenarios still red?** **Yes**, all three.
8. **Does the frozen manifest reproduce exactly?** **Yes** — 339 entries, stated SHA-256, zero
   differences other than the prior reviewer's own verdict file.

## 9. Exit decision

**Section 4 exit is REJECTED.**

**May Section 5 or task 5.0 begin? NO.**

Blockers B1 and B2 are product defects on the frozen fixed-codec contract, both reproduced
executably. B3 is confirmed but may reasonably be split: the aggregate-side content-type check
(§7.4) belongs to Section 4; removing the durable command surface from the application package is
Section-7 work already tracked as expected-red. The scenario false-green (§3) must be fixed before
exit as an evidence defect, and should not be scoped as a codec fix — the codec branches under it
are correct.

Resolve B1, B2, and the Section-4 half of B3, repair the scenario evidence, freeze a new exact
manifest, rerun the complete packet, and obtain a new independent approval carrying no unresolved
release blocker.
