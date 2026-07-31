# VERDICT: REJECT — Section 4 final remediation independent exit re-review

**Review date:** 2026-07-22
**Change:** `reshape-developer-facing-interfaces`
**Decision:** Section 4 does **not** meet the immutable exit bar. **Section 5 and task 5.0 remain
blocked.**

**Reviewer role:** Independent Section-4 final-remediation exit re-reviewer (audit-only).
**Inputs (immutable, read only):** the
[final remediation re-review request](developer-facing-interface-section-04-final-remediation-rereview-request-2026-07-22.md),
its [frozen dirty manifest](developer-facing-interface-section-04-final-remediation-dirty-manifest-2026-07-22.txt),
and the prior REJECT re-reviews
([2026-07-21](developer-facing-interface-section-04-remediation-exit-rereview-2026-07-21.md),
[2026-07-22 independent](developer-facing-interface-section-04-remediation-independent-rereview-2026-07-22.md)).
No reviewed source, test, task, spec, documentation, manifest, request, or existing review artifact was
edited, staged, reverted, or cleaned. This review authored exactly one new file — this one.

> **Review-integrity note.** My first working draft of this verdict recorded APPROVE after I confirmed
> the three prior release blockers (static-request discard, false-green drivers, infrastructure race)
> were resolved and reproduced the full validation packet green. Before finalizing I audited the codec
> remediation directly against the frozen non-replaceable-codec contract (`workflow-contracts/spec.md:239`,
> AC-024) rather than only against the prior reviewers' enumerated concerns, and found a **live, exercised
> serializer replacement hook** that the Section-4 green scenario falsely asserts is absent. Correcting my
> own draft to REJECT is the honest outcome of that deeper pass. A concurrent independent reviewer's REJECT
> file (`…final-remediation-independent-rereview-2026-07-22.md`) appeared in the tree during my pass; I read
> it only to stress-test my conclusion and independently reproduced every finding from source — its text is
> not used as my evidence. I authored and then removed one earlier draft file of my own; no other artifact
> was touched.

---

## 1. Provenance and frozen manifest (Question 8)

| Item | Value | Match to request |
|---|---|---|
| Baseline | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` (ancestor of HEAD) | ✅ |
| HEAD | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` | ✅ |
| HEAD tree | `2264e670493ecc76359d42ee5273028eb287a566` | ✅ |
| Commits since baseline | exactly one | ✅ |
| Frozen manifest entries | **332** (285 `M`, 8 `D`, 39 `??`) | ✅ |
| Frozen manifest SHA-256 | `cf3e1be1bac6c32a081940e3bcf5d30430bb7f2fe9870e06950767e03351b90b` | ✅ (matches `CF3E1BE1…`) |
| Current worktree vs frozen manifest | exact match; re-checked after all validation, zero drift | ✅ |
| OpenSpec task state | 45 done, 67 pending, 112 total; 4.0–4.14 `[x]`, 5.0 `[ ]` | ✅ |

**Question 8: YES** — the frozen 332-entry manifest reproduced exactly. The failure below is **not** a
provenance or lane failure; like the prior P1-1, it is a contract/coverage gap sitting behind the green
lanes, found by auditing the authored surface against the frozen codec contract.

---

## 2. Release-blocking findings

### P1-1 (RELEASE BLOCKER) — A public serializer replacement hook remains, so the fixed codec is replaceable and the Section-4 "no replacement seam" green is false

**Contract.** `openspec/specs/workflow-contracts/spec.md:239`: *"V1 SHALL use the **non-replaceable**
certified `System.Text.Json` format `orcacore-json-v1` …"*. `docs/specs/12-acceptance-criteria.md:118-120`
(**AC-024**): *"… **No serializer replacement hook exists.**"* The Section-4 `fixed-codec-determinism`
scenario driver additionally asserts, in the green Infrastructure lane, that *"no codec replacement seam
exists."*

**Defect (a live, exercised replacement hook).**
- `IWorkflowPayloadSerializer` is a **public** interface (`src/OrcaCore.Abstractions/Providers/ProviderPorts.cs:210-221`).
- `DurableWorkflowRuntime`'s **public** constructor accepts any implementation
  (`src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs:33-37`).
- Hosting registers the fixed codec only as a **replaceable default** —
  `services.TryAddSingleton<IWorkflowPayloadSerializer, JsonWorkflowPayloadSerializer>()`
  (`src/OrcaCore.Hosting/OrcaCoreServiceCollectionExtensions.cs:55`) — and then feeds whatever is
  registered into the runtime via `provider.GetRequiredService<IWorkflowPayloadSerializer>()`
  (`:79`). Because `TryAddSingleton` is a no-op when the service is already present, **an application
  registration made before `AddOrcaCore()` wins.**
- This is not hypothetical: the existing hosting fixture registers a custom `IWorkflowPayloadSerializer`
  (`RecordingWorkflowProvider`) at `tests/OrcaCore.Hosting.Tests/OrcaCoreHostingServiceCollectionTests.cs:269`
  **before** `AddOrcaCore()` at `:272`, so the durable runtime uses the caller-supplied serializer, not
  the certified `orcacore-json-v1` codec.

**False green.** The remediation deleted the three *named* seam types (`IWorkflowPayloadCodec`,
`ContentTypeWorkflowPayloadSerializer`, `WorkflowPayloadSerializationOptions`), and the guards check only
that those three names are gone:
- `Phase0ScenarioHost.cs:70-78` asserts seam absence by testing only that `IWorkflowPayloadCodec` and
  `ContentTypeWorkflowPayloadSerializer` are not exported — while `IWorkflowPayloadSerializer` (a real
  replacement seam) is exported.
- `WorkflowPayloadSerializationRegistrationTests.AddOrcaCore_RegistersTheNonReplaceableFixedJsonCodec`
  resolves the **default** and round-trips it; it never attempts a replacement, so it does not prove
  non-replaceability despite its name.
- `WorkflowPayloadSerializationRegistrationTests.ProductSurface_HasNoCodecPluginOrWriterSelectionType`
  rejects only the three deleted names and does not reject the public interface or the runtime
  constructor.

The Section-4 green scenario therefore asserts a property ("no codec replacement seam exists") that the
shipped system does not hold, and AC-024's flat requirement ("No serializer replacement hook exists") is
violated. This is the same class of defect the prior two reviews rejected — a green certification that
overstates what the product guarantees.

**Scope note.** The Section-7 scenario `programmatic-options-copy-validation` (`turnsGreenTask: 7.10`,
still expected-red) concerns the *options* surface not exposing serializer replacement — a different
vector. It does not license shipping a live DI-ordering replacement hook in Section 4 while the Section-4
codec scenario asserts no such seam exists, and AC-024 carries no section deferral.

**Required remediation.** Make the durable value codec genuinely non-replaceable for application code:
bind the fixed `JsonWorkflowPayloadSerializer` directly in the durable runtime/hosting (or make the
serializer port implementation-only), remove the DI-ordering replacement path, and add negative
regressions that attempt replacement **both** via pre-`AddOrcaCore()` DI registration **and** via direct
runtime construction and prove the certified codec still runs. Additionally bind the `orcacore-json-v1`
format identifier into plan identity so persisted-byte/content changes cannot occur without a fingerprint
change.

### P1-2 (RELEASE BLOCKER) — Unapproved-polymorphism rejection is root-only, not graph-wide

**Contract.** `workflow-contracts/spec.md:239` and AC-024: *"reject unsupported cyclic or unapproved
polymorphic **graphs** before commit."*

**Defect.** `JsonWorkflowPayloadSerializer.Serialize<TPayload>` checks only the **root** runtime type
(`src/OrcaCore.Engine.Durable/Execution/JsonWorkflowPayloadSerializer.cs:19-24`) and then hands the entire
object graph to default `System.Text.Json` (`:26-28`). An exact-type outer object whose member or
collection element is declared as a base type but holds a derived instance (e.g. `List<Base>` containing a
`Derived`, or a `Base`-typed property) passes the root check and is serialized against the **declared**
type, silently dropping derived data instead of being rejected before commit. (Cyclic detection, by
contrast, is graph-wide via the default cycle guard — the asymmetry confirms the gap.)

**Coverage.** The durable regression
(`tests/OrcaCore.Engine.Durable.Tests/Execution/WorkflowPayloadSerializationTests.cs:41-53`) and the
scenario driver (`Phase0ScenarioHost.cs:62-68`) exercise only a **root** polymorphic value, so they cannot
detect nested/collection/dictionary polymorphism.

**Required remediation.** Validate the complete declared value graph against the approved contract before
commit, and add nested-property, collection-element, and dictionary-value polymorphism regressions proving
pre-commit rejection.

---

## 3. What is genuinely resolved (reproduced green)

The three prior release blockers and the resource-scope remediation are sound; approval is blocked solely
by §2.

| Prior blocker / remediation | Disposition | Evidence (independently reproduced) |
|---|---|---|
| Static resource request discarded | **Resolved** | `SelectedResourceLeaseAuthoringNode` / `BranchResourceLeaseAuthoringInstruction` retain the static request + body; `DescribeResourceLease` (`DefinitionCompiler.Fingerprint.cs:178-203`) emits `static:pool=units` and `selector:opaque`, recursively fingerprinting the body at root and branch/item. Regressions (Core, green): 1 fact (leased≠unleased, distinct request≠, selector-capture opacity=) + 4 theory (root/nested/branch/item drift). |
| Fingerprint-version driver | **Resolved** | `Phase0ScenarioHost.cs:81-125` proves opaque-delegate equality, `Delay` graph drift, explicit version bump, and static lease request A≠B drift. |
| Attempt-state driver | **Resolved** | `Phase0ScenarioHost.cs:127-188` observes `ReplaceState`, then runs a fail-first `WithRetry(2)` execution through a real `EphemeralWorkflowEngine`, proving the failed attempt's mutation is discarded and the successful replacement commits (`Completed`, attempts=2, committed=2). |
| Infrastructure nondeterminism | **Resolved** | `CompileFixtureCollection` disables parallelization; both process-spawning guard classes share it. My two independent Infrastructure runs were 53/53 with no `CS2012` (plus the request's three). |
| Fixed-codec *behavior* (determinism/detachment/null/cyclic/content-type) | **Resolved for those cases** | Deterministic bytes, detachment, null round-trip, `JsonException` on cyclic, foreign content-type rejection all pass. **But** the replacement hook (P1-1) and graph-wide polymorphism (P1-2) are not met. |
| Task 4.14 / Section-4 certification | **Not resolved** | The `fixed-codec-determinism` green asserts "no codec replacement seam exists" while a live seam remains; a checked task cannot override the release blocker. |

Earlier exact-consumer/package compile, replacement-host recovery, durable host-DI activation, root-only
fan-out, and legacy fallback removal remain present with no regression (Green compile fixtures; Durable 277;
Core 409; `WorkflowBuilder.cs` deleted). The Section-4/Section-6 boundary holds:
`LeaseAuthoringAndExitExpectedRedGuards.Product_ContainsScopedAcquireResourcesAndFinalLifecycle` and all
lease/governance scenarios remain red; no Section-6 lifecycle scenario turned green.

---

## 4. Commands and exact reproduced results

| # | Command | Result | Requested | Match |
|---|---|---|---|---|
| 1 | `dotnet build OrcaCore.slnx --no-restore -v minimal` | Exit 0; 0 warn / 0 err | 0/0 | ✅ |
| 2 | Core `--no-restore --no-build` | 409 / 0 / 0 | 409 | ✅ |
| 3 | Ephemeral `--no-restore --no-build` | 148 / 0 / 0 | 148 | ✅ |
| 4 | Durable `--no-restore --no-build` | 277 / 0 / 0 | 277 | ✅ |
| 5 | Hosting `--no-restore --no-build` | 15 / 0 / 0 | 15 | ✅ |
| 6 | Guards `Disposition=Infrastructure` | 53 / 0 / 0; **twice, no `CS2012`** | 53 (×3) | ✅ |
| 7 | Guards `--no-build Disposition=ExpectedRed` | Exit 1; 0 / 104 / 0 (91 scenario + 13 fact) | 0 / 104 | ✅ |
| 8 | `run-compile-fixtures.ps1 -Disposition Green` | Exit 0; fresh pack; 26+26 CS1061; incomplete rejected | green | ✅ |
| 9 | `run-compile-fixtures.ps1 -Disposition ExpectedRed` | Exit 0; 0 remaining; Section-4 package proof green | 0 remaining | ✅ |
| 10 | `openspec validate reshape-developer-facing-interfaces --strict` | Exit 0; valid | valid | ✅ |
| 11 | `openspec validate add-runtime-concurrency-limits --strict` | Exit 0; valid | valid | ✅ |
| 12 | `git diff --check` | Exit 0; benign LF→CRLF only | clean | ✅ |

Every lane reproduces exactly. The blockers are contract/coverage gaps behind these green lanes, not lane
failures.

---

## 5. Answers to the eight independent review questions

1. **Each approved `AcquireResources` retains scope + static request + body?** **YES.**
2. **Distinct static requests / leased-vs-unleased differ at all four locations while selectors stay
   opaque?** **YES.**
3. **Section 6 sole owner of executable lease lifecycle, scenarios still red?** **YES** — all lease/
   governance scenarios and `…FinalLifecycle` remain red.
4. **Fixed codec executes all frozen rejection behavior, and is every public codec replacement seam gone?**
   **NO.** The certified codec is replaceable through the public `IWorkflowPayloadSerializer` interface and
   `TryAddSingleton` DI ordering (exercised live in the hosting fixture), violating the non-replaceable
   `SHALL` and AC-024's "No serializer replacement hook exists" (P1-1); polymorphism rejection is root-only,
   not graph-wide (P1-2).
5. **definition-id / fingerprint-version / attempt-state drivers execute frozen behavior?** **YES** for the
   reviewed driver cases.
6. **Infrastructure passes repeatedly without the two classes racing?** **YES** — 53/53 on both independent
   runs, no `CS2012`.
7. **All earlier Section-4 blockers resolved without regression?** **Not overall.** Consumer/recovery/DI/
   fan-out/legacy and the lease-fingerprint remediations are resolved, but the codec blocker (P1-1/P1-2) is
   not.
8. **Frozen manifest reproduces exactly before one new verdict?** **YES** — SHA-256 `cf3e1be1…`, 332
   entries, zero drift before and after validation.

---

## 6. Exit decision

**Section 4 exit is REJECTED.**

**May Section 5 or task 5.0 begin? NO.** Task 4.14 must not be treated as independently certified while
P1-1 or P1-2 remains: the frozen non-replaceable-codec contract (`workflow-contracts/spec.md:239`, AC-024)
is violated by a live, test-exercised serializer replacement hook and by root-only polymorphism rejection,
and the Section-4 "no codec replacement seam exists" green is false against the shipped surface. Resolve
both blockers, add regressions that would fail against this reviewed target (replacement via DI ordering
and direct runtime construction; nested/collection/dictionary polymorphism), freeze a new exact manifest,
rerun the complete validation packet, and obtain a new independent approval with no unresolved release
blocker.

**Files edited during review:** **NONE** except this single new immutable verdict file. Build/test writes
went only to git-ignored `bin/`/`obj/`/`TestResults/`/package-cache paths and the session scratchpad. The
frozen manifest, the request, both prior reviews, the concurrent reviewer's file, and all reviewed
source/tests/tasks/specs/docs were treated as immutable and left unchanged.
