# Section 4 Remediation — Independent Exit Re-Review

# VERDICT: REJECT

**Section 5 (task 5.0) remains blocked.** The six explicitly-reopened blockers (4.3, 4.5, 4.7, 4.8, 4.12,
4.14) and the described structural-fingerprint body-opacity defect are all genuinely resolved. However,
independent verification against the amended canonical contract surfaced a **separate, still-open release
blocker**: authored static resource requests are silently discarded, so the structural fingerprint does not
retain the "static resource requests" structure that the workflow-contracts spec makes a normative `SHALL`
and that this re-review's Question 6 explicitly requires. Approval cannot be granted while that holds.

> **Review-integrity note.** My first working draft of this file recorded APPROVE after confirming the six
> reopened blockers. Before finalizing, I completed Question 6 element-by-element and found the static-request
> gap below. This file is my own deliverable; correcting it to REJECT is the honest outcome of that deeper
> pass. A concurrent independent reviewer's REJECT file
> (`…remediation-independent-rereview-2026-07-22.md`) appeared in the tree during this pass; I read it only
> to stress-test my own conclusion and independently reproduced the finding — its conclusions are not used as
> my evidence.

**Reviewer role:** Independent Section-4 remediation re-reviewer (audit-only).
**Date:** 2026-07-21
**Inputs (immutable, read only):** the two prior Section-4 reviews and the
[remediation re-review request](developer-facing-interface-section-04-remediation-rereview-request-2026-07-21.md).
No reviewed source, test, task, spec, or existing review/documentation artifact was edited, staged, reverted,
or cleaned. This review authored exactly one new file — this one.

---

## 1. Provenance and frozen manifest (Question 7)

| Item | Value | Match to request |
|---|---|---|
| Baseline | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` (ancestor of HEAD) | ✅ |
| HEAD | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` | ✅ |
| HEAD tree | `2264e670493ecc76359d42ee5273028eb287a566` | ✅ |
| Commits since baseline | exactly one (`Complete Phase 0 developer surface guard packet`) | ✅ |
| Frozen manifest entries | **322** (284 `M`, 5 `D`, 33 `??`) | ✅ |
| Frozen manifest SHA-256 | `854903dce160af1e2d6cd3285ded6903851a4e9a8c4cde1d3965adf9d30e0ce7` | ✅ (matches `854903DC…`) |
| Current worktree vs frozen | **exact match** (line-ending-normalized diff empty, delta 0, before this verdict written) | ✅ |

**Question 7: YES** — the frozen 322-entry manifest reproduces exactly (SHA-256 `854903dc…`).
**Decision 17 intact** at `design.md:318` with its canonical-amendment map. **Task state:** 4.0–4.14 all
`[x]`, 5.0 `[ ]`, 45/67/112.

---

## 2. Exact commands and independently reproduced results

| # | Command | Result | Requested | Match |
|---|---|---|---|---|
| 1 | `dotnet build OrcaCore.slnx --no-restore -v minimal` | Exit 0; **0 warn, 0 err** | 0/0 | ✅ |
| 2 | Core `--no-restore --no-build` | **404 / 0 / 0** | 404 | ✅ |
| 3 | Ephemeral `--no-restore --no-build` | **148 / 0 / 0** | 148 | ✅ |
| 4 | Durable `--no-restore --no-build` | **277 / 0 / 0** | 277 | ✅ |
| 5 | Guards `--filter Disposition=Infrastructure` | **53 / 0 / 0** (see P2-2) | 53 | ✅ (2nd run) |
| 6 | Guards `--no-build --filter Disposition=ExpectedRed` | Exit 1 by design; **0 / 104 / 0** | 0 / 104 | ✅ |
| 7 | `run-compile-fixtures.ps1 -Disposition Green` | Exit 0; fresh-packed current source; 26 source + 26 package CS1061; incomplete package rejected | green | ✅ |
| 8 | `run-compile-fixtures.ps1 -Disposition ExpectedRed` | Exit 0; "0 remaining; Section 4 product authoring package proof is green" | 0 remaining | ✅ |
| 9 | `openspec validate reshape-developer-facing-interfaces --strict` | Exit 0; valid | valid | ✅ |
| 10 | `openspec validate add-runtime-concurrency-limits --strict` | Exit 0; valid | valid | ✅ |
| 11 | `git diff --check` | Exit 0; clean (LF→CRLF notices only) | clean | ✅ |

Every listed lane reproduces. The failure is **not** in these lanes; it is a contract/coverage gap found by
inspecting the authored surface behind the green lanes (§3, P1-1).

---

## 3. Findings

### P1-1 (RELEASE BLOCKER) — Static resource requests are discarded, so the fingerprint violates its canonical `SHALL` and Question 6 fails

**Contract.** `openspec/specs/workflow-contracts/spec.md:90` (a Section-4 canonical amendment area per
Decision 17, modified in the frozen manifest) states: *"The fingerprint SHALL cover inspectable authored
node/member kinds, ordering, strong values, referenced step/workflow types, **static resource requests**, and
codec format only."* `docs/specs/12-acceptance-criteria.md:110` likewise treats changing "static request
values" as a fingerprint-affecting change. Re-review **Question 6** explicitly asks whether the fingerprint
retains "static-request" structure.

**Defect.** Every public `AcquireResources(request, body)` overload validates `request` for null and then
**discards it**, authoring the body directly onto the parent builder:
- root: `src/OrcaCore.Core/Building/PublicStagedAuthoring.cs:449` and `:460`
  (`ArgumentNullException.ThrowIfNull(request); … body(new DurableLeaseWorkflowBuilder<…>(builder)); return this;`)
- nested: `src/OrcaCore.Core/Building/PublicNestedAuthoring.cs:156,166`
- branch: `src/OrcaCore.Core/Building/PublicBranchAuthoring.cs:128,130`
- item: `src/OrcaCore.Core/Building/PublicBranchAuthoring.cs:159,160`

`ResourceLeaseRequest`/`ResourceLeaseRequirement` is consumed **nowhere** in `OrcaCore.Core` beyond those
null-checks (verified by grep). The lease builders wrap the **same** underlying builder and delegate straight
to `builder.Then/If/Wait/Delay`, so **no lease scope node** and **no request** enter the authored graph. The
fingerprint's `DescribeSequence` (`DefinitionCompiler.Fingerprint.cs`) therefore has no lease node to describe.

**Consequence.** `.AcquireResources(poolRequestA, b => b.Then<X>())` and
`.AcquireResources(poolRequestB, b => b.Then<X>())` — and even `.Then<X>()` with no lease — compile to the
**same structure and the same `DefinitionFingerprint`**. Two definitions differing only in their static
resource request collide, and a public authoring method silently drops a semantically critical argument (the
requested pools/tickets) *and* the lease scope, with no diagnostic. No test asserts a request affects the
fingerprint or plan (`grep` for any such test → none). The green `structural-fingerprint-opacity` driver
proves only a `Delay` graph change and so does not catch this.

**Phasing nuance (considered, does not clear the blocker).** Full lease *lifecycle* lowering is Section 6
(task 6.5 "lower directly to the structured plan"; 6.4–6.9), and 4.8 only requires the leased builders to
preserve mode and omit fan-out (both satisfied). But (a) the fingerprint's static-request coverage lives in
the workflow-contracts Section-4 area and is a normative `SHALL`; (b) Question 6 is a stated acceptance
criterion for *this* exit and is factually **NO** for static-request; and (c) exposing a public method that
silently accepts-and-discards a required argument is a shipped Section-4 correctness trap, strictly worse than
not exposing it or throwing "not yet implemented." Minimum required fix: record the static request (and the
lease scope) into the authored graph so the fingerprint covers it — even if lifecycle execution stays in
Section 6 — and add a regression proving two distinct requests yield distinct fingerprints; or do not expose
`AcquireResources` until Section 6.

### P2-1 — Section-4 state/codec drivers under-assert relative to their own frozen scenarios
The four `Phase0ScenarioHost` drivers are executable, but three prove strictly less than their frozen
`state-and-codec-scenarios.json` assertions:
- `fixed-codec-determinism` (assertion: "Bytes and type fidelity are deterministic, detached, null-safe, and
  unsupported/cyclic/polymorphic shapes fail before commit; no codec replacement seam exists") only builds one
  workflow and checks `TryBuild()` yields a non-empty fingerprint — it exercises none of the round-trip
  determinism, null-safety, cyclic/polymorphic rejection, or codec-seam-absence it claims.
- `attempt-local-replace-state` (assertion/setup: "mutate…, fail/retry, and use ReplaceState on success;
  failed attempts leak no mutation") calls `ReplaceState` once and never simulates the fail/retry-no-leak path.
- `definition-id-nonempty` (assertion: "Parse rejects empty…") never exercises `DefinitionId.Parse(empty)`
  throwing (only `TryParse`).
The underlying product behaviors may be covered by the Core suite, but as scenario certifications these are
thin greens that overstate coverage. Recommend tightening the driver assertions (or the per-scenario required-
call contracts) to match the frozen scenario text before they are cited as Section-4 completion evidence.

### P2-2 — The required Infrastructure lane is nondeterministic on a cold/concurrent run
Command 5's first run reported **52 passed / 1 failed**, the single failure being
`ProductAuthoringGreenGuards.Product_CompilesEveryPositiveSignatureAndRejectsEveryForbiddenMember` aborting
with `CSC error CS2012: Cannot open '…/CompileFixtures/…'` — a build-server file lock from that guard spawning
child `dotnet pack`/`build` over the fixture feed while the parent rebuild holds handles. After
`dotnet build-server shutdown` + isolated `--no-build`, the lane is a clean **53 / 0**, and the standalone
green script passes the same flow. This is harness contention, not a product-assertion failure, but a required
gate that flaps on first independent run is a reproducibility weakness; recommend making the in-suite pack/
build guard robust to concurrent handles (serialize or `--no-build` the fixture feed).

### P3 (non-blocking, carried forward)
- Internal `WorkflowAuthoringSession` (renamed from `SelectedWorkflowBuilder`) retains a **dead** internal
  `WhenFirst`/`End(string?)`; the public reduced surface is proven clean and the source guard forbids the old
  name. Clean up in the Section-7 tier move.
- Broad `InternalsVisibleTo` and absent package split remain correctly-red Section-7 transitions.
- Internal `SagaBuilder`/`WorkflowDagBuilder` remain unexported Section-7/8 transition code, not final layout.

---

## 4. Six reopened blockers — all genuinely resolved

| Task | Verdict | Evidence (independently reproduced) |
|---|---|---|
| 4.3 | Resolved | `ProductAuthoring.csproj` restores from fixture-local `obj/product-feed` (not gitignored `artifacts/phase0-packages`); `run-compile-fixtures.ps1` fresh-packs current `src/OrcaCore.Core`; `PositiveUsage.cs` has no `dynamic`; green lane verifies 26 source + 26 package `CS1061`; now also asserted in-suite by `ProductAuthoringGreenGuards`. |
| 4.5 | Resolved | `DurableStructuredFiberDriverTests.cs:29` parks a public durable workflow on one runtime, resumes via a **separate replacement runtime/registry** over the same store, loads the persisted checkpoint, and asserts typed output + `Completed` + fixed outcome together. |
| 4.7 | Resolved | `…:82` registers a constructor-dependent `IStep` in a host service provider, authors `Then<ConstructorInjectedDurableStep>()`, runs durably, asserts `ExecutionCount==1` and `RequestedTypes==[the step]`. |
| 4.8 | Resolved | Branch `Parallel` removed; `DefinitionCompiler.cs:311,361` emits `AddRootOnlyParallelError` for hand-built nested scopes; `DefinitionCompilerTests.cs:200` hand-builds nested `Parallel` in **both** a root branch and a `ForEach` item and asserts exactly 2 root-only errors; obsolete executing nested-fan-out test removed. |
| 4.12 | Resolved | `WorkflowBuilder.cs`+tests deleted; `LegacyWorkflowBuilder`/`CreateLegacyTestPlan`/`ContainsDurableOnlyNodes`/`requiresDurableEngine` absent from all source; scoped source guard `ProductSource_HasNoSupersededMixedModeBuilder`; mixed-mode builder renamed to internal mode-fixed `WorkflowAuthoringSession`. |
| 4.14 | Resolved (mechanism) | Per-scenario enforcement via complementary `Section4Scenarios`/`RemainingScenarios` `[MemberData]` over `AllScenarios()`; 4 Section-4 drivers green in infrastructure; 91 remaining individually-named red; `duplicate-decorator-eager` (4.6) enforced by Core + infra ledger, absent from red. (Driver-strength caveat: P2-1.) |
| Fingerprint body-opacity | Resolved | `<opaque>` markers for condition/selector/projector/merge/output bodies + `Delegate=>"opaque"`; Core regressions `Fingerprint_*` prove graph/partitioner/compiler-option change → differs and opaque captured change → equal. **But static-request coverage is missing — P1-1.** |

---

## 5. Expected-red lane — all 104 are valid later-section gaps

**91** `ExecutableBehaviorExpectedRedGuards.Scenario_…` failures ("must have one reviewable executable
driver") — the 91 remaining executable scenarios (95 total across the 10 fixtures minus the 4 Section-4
drivers), owned by Sections 5–9 per their `turnsGreenTask`. **13** package/tier/manifest facts
(`File.Exists(…OrcaCore.Runtime.Protocol.csproj)`, `…OrcaCore.Dag.csproj`, `…{package}.0.0.0-phase0.nupkg`,
assembly/friend/tier-edge assertions) owned by Sections 7–8. No restore/discovery/NU1101/setup errors; no
Section-4-owned scenario is present. **No Section-4 scenario is stranded red and no later scenario turns green
via a whole-fixture/coarse shortcut** (Question 2 satisfied) — this part is clean; the blocker is P1-1.

---

## 6. Answers to the independent review questions

1. **All findings resolved without stale packages/dynamic?** The six named blockers: YES. But a distinct
   contract finding remains (P1-1), so not fully.
2. **Any Section-4 scenario red / later scenario green via shortcut?** NO — per-scenario complementary lanes;
   clean.
3. **Nested fan-out fails static + compiler for hand-built branch/item?** YES — two root-only errors proven.
4. **All mixed-mode fallback/inference and duplicate consumers gone?** YES — deleted and guarded.
5. **Replacement-host recovery + durable named-step host-DI proven?** YES — both behavioral regressions pass.
6. **Structural fingerprint excludes opaque code identity while retaining graph/strong-value/referenced-type/
   static-request/fixed-codec/compiler-option structure?** **NO** — graph, referenced step types, and
   compiler-option are retained and opaque identity is excluded, but **static resource requests are discarded
   and not retained** (P1-1), violating `workflow-contracts/spec.md:90`.
7. **Frozen manifest reproduces exactly before one new verdict?** YES.

---

## 7. Explicit statements

- **May Section 5 (task 5.0) begin?** **NO.** Resolve P1-1 (record static resource requests into the authored
  graph so the fingerprint covers them and two distinct requests yield distinct fingerprints — with a
  regression — or do not expose `AcquireResources` until Section 6; and stop silently discarding the argument),
  address P2-1/P2-2, freeze a new exact manifest, rerun the full validation packet, and obtain a new
  independent approval with no unresolved release blocker.
- **Files edited during review:** **NONE** except this single new immutable verdict file
  (`docs/review/developer-facing-interface-section-04-remediation-exit-rereview-2026-07-21.md`). Test/build
  writes went only to git-ignored `bin/`/`obj/`/`TestResults/`/package-cache paths and the session scratchpad.
  A concurrent actor's review file (`…independent-rereview-2026-07-22.md`) appeared in the tree during this
  pass; I did not author or edit it, and its conclusions are not used as my evidence.
