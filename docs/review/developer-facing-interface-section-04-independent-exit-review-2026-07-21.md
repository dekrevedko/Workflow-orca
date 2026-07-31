# Section 4 Independent Exit Review — reshape-developer-facing-interfaces

# VERDICT: REJECT

**Reviewer role:** Independent Section 4 exit reviewer (audit-only; no source, spec, task, test, or
documentation files were edited, staged, committed, reverted, or cleaned during this review).
**Date:** 2026-07-21
**Change:** `reshape-developer-facing-interfaces`
**Scope:** Tasks 4.0–4.14 (typed mode-first authoring and the reduced surface).
**Authorization on APPROVE (not granted here):** would authorize only the later *start* of Section 5.

---

## 1. Baseline, provenance, and frozen manifest

| Item | Value |
|---|---|
| Stated baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| Actual `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` ("Complete Phase 0 developer surface guard packet") |
| HEAD tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Branch | `feature/v3-rebuild` |
| Baseline↔HEAD relation | Baseline **is an ancestor** of HEAD; HEAD is **exactly one commit ahead** of the stated baseline. |
| Dirty-manifest path count | **302** entries (276 `M`, 26 `??`) |
| Manifest sha256 (`git status --porcelain=v1`) | `7e4d089b1bd92dde50c48885a2b47306f149cac072807e77cd069f8b1b8039cb` |
| Sorted-paths sha256 | `842f4574a4ad663b8cf4c377bb6a70e16ee547c624a79926a8f28fc0348f91f0` |
| Manifest top-level split | `tests` 161, `src` 128, `openspec` 5, `samples` 4, `benchmarks` 3, root `Directory.Build.props` 1 |

**Provenance note (not a blocker):** HEAD is one commit past the stated baseline. That commit
("Complete Phase 0 developer surface guard packet") is a Phase-0 artifact commit, and the Section-4
product/test work sits in the uncommitted working tree on top of it. All results below were produced
against the frozen working tree described by the manifest above.

**Decision 17 confirmed intact.** `openspec/changes/reshape-developer-facing-interfaces/design.md`
§"### 17. Amend canonical requirements before each source section" is present and unmodified in
substance, including the canonical-area map (Section 4 → Workflow authoring, workflow contracts,
quality verification) and the statement that Section 4 remains additionally blocked by the complete
Phase 0 packet (Decision 16). The Section-4 canonical specs named by that row are all present in the
dirty manifest as amended: `openspec/specs/workflow-authoring/spec.md`,
`openspec/specs/workflow-contracts/spec.md`, `openspec/specs/quality-and-verification/spec.md`.

**Normative authority order applied:** (1) `docs/specs/17-selected-mode-capability-matrix.md`,
(2) `docs/specs/17-public-authoring-contract.cs`, (3) canonical specs under `docs/specs/`,
(4) active OpenSpec design/specs/tasks, (5) dated reviews/guides.

---

## 2. Exact commands run and reproduced results

| # | Command | Reproduced result | Prior claim | Match |
|---|---|---|---|---|
| 1 | `dotnet build OrcaCore.slnx --no-restore -v minimal` | **0 warnings, 0 errors**, Build succeeded | 0/0 | ✅ |
| 2 | `dotnet test tests/OrcaCore.Core.Tests/... --no-build` | **438 passed / 0 failed / 0 skipped** | 438/438 | ✅ |
| 3 | `dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/... --no-build` | **167 passed / 0 / 0** | 167/167 | ✅ |
| 4 | `dotnet test tests/OrcaCore.Engine.Durable.Tests/... --no-build` | **281 passed / 0 / 0** | 281/281 | ✅ |
| 5 | `dotnet test tests/OrcaCore.DeveloperSurface.Guards/... --filter "Disposition=Infrastructure"` | **43 passed / 0 / 0** | 43/43 | ✅ |
| 6 | `dotnet test tests/OrcaCore.DeveloperSurface.Guards/... --filter "Disposition=ExpectedRed"` | **7 passed / 22 failed / 0 skipped (29 total)** | 7 pass / 22 expected-red | ✅ |
| 7 | `powershell run-compile-fixtures.ps1 -Disposition Green` (exact consumer compile fixtures) | **FAILED (exit 1)** — CS0246 in `ExactAuthoring/PositiveUsage.cs(116)` | *(not in prior list)* | ❌ **P1** |
| 8 | `openspec validate reshape-developer-facing-interfaces --strict` | **"Change ... is valid"** | valid | ✅ |
| 9 | `openspec validate add-runtime-concurrency-limits --strict` | **"Change ... is valid"** | valid | ✅ |
| 10 | `git diff --check` | **clean, exit 0** (only benign "LF→CRLF" eol-normalization warnings; no whitespace errors) | clean | ✅ |

Guards project total = 72 tests (43 Infrastructure + 29 ExpectedRed). All headline numbers previously
reported reproduce. The **one required validation that was not in the prior-results list — the green
"exact consumer compile fixtures" lane — is RED** (see P1-1).

---

## 3. Findings (ordered P1 → P2 → P3)

### P1-1 — The exact-consumer *positive* compile fixture does not compile (required green lane is red)

- **Where:** `tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/ExactAuthoring/PositiveUsage.cs:116`
  (fixture built by `tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1` `-Disposition Green`,
  first positive build `ExactAuthoring/ExactAuthoring.csproj`).
- **Symptom (reproduced deterministically, twice — via the script and via a direct `dotnet build … -c Release`):**
  `error CS0246: The type or namespace name 'StepResult' could not be found`.
- **Root cause:** The uncommitted Section-4 edit changed the probe from the compiling
  `private sealed class ProbeStep : IStep<object>;` (as at HEAD) to a body:
  ```csharp
  public ValueTask<StepResult> ExecuteAsync(StepContext<object> context, CancellationToken ct) =>
      ValueTask.FromResult<StepResult>(new StepResult.Completed());
  ```
  `StepResult` is defined **nowhere the self-contained ExactAuthoring fixture can see it** — not in the
  normative contract `docs/specs/17-public-authoring-contract.cs` (which references only `IStep<TState>`
  as a generic constraint and never `StepResult`), and not in the fixture's
  `CompileFixtures/ExactAuthoring/SupportTypes.cs` (which defines only `StepContext<T>` and an empty
  `IStep<T>`). The edit compiles in the *package* consumer (`ProductAuthoring`, where `using OrcaCore;`
  resolves the real `OrcaCore.StepResult`) but breaks the *contract-only* fixture.
- **Why it is release-blocking for Section 4:** The audit's required validations include "Exact consumer
  compile fixtures," and the test-quality bar states "Positive fixtures must actually compile and exercise
  exact consumer signatures." This positive fixture compiled at HEAD and was regressed by Section-4 edits.
  A required green lane cannot be red at a Section-4 exit. (Fix is small — add a `StepResult` stand-in to
  `SupportTypes.cs` or restore the empty `ProbeStep` — but the exit gate is not met until it is green.)

### P2-1 — Superseded mixed-mode builder not deleted (direct contradiction of checked task 4.12)

- **Where:** `src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs:15`
  (`internal abstract class SelectedWorkflowBuilder<TState, TSelf>`).
- **Task 4.12 (checked `[x]`)** requires: *"Delete the superseded mixed-mode builder, `RequiresDurableEngine`,
  legacy plan fallback, registration-time mode inference, and all duplicate mixed-mode execution paths."*
  `RequiresDurableEngine` and `FromLegacy` were removed (verified absent from `src/**/*.cs`), but the
  mixed-mode builder itself was **only made `internal`, not deleted**. It is still compiled into
  `OrcaCore.Core` and still carries the superseded/forbidden authoring surface:
  - `WhenFirst<TResult>(…)` — `:254` (task 4.10 defers `WhenFirst`; Decision 3 requires it "absent from … builders").
  - `End(string? outcomeName = null)` — `:274` (task 4.5 removes **dynamic outcome-name selectors** and optional/nullable parameters; 4.13 rejects them).
  - `While(Func<TState,bool>, Action<TSelf> body)` — `:220` (nested/portable `While`).
  - `WithRetry(int maxAttempts, TimeSpan? backoff = null)` — `:80` (definition retry with optional param; 4.10 defers definition retry).
- **Why the guard suite misses it:** it is `internal`, so the public-export reflection guard
  (`AuthoringContractExpectedRedGuards`, which lists `…SelectedWorkflowBuilder\`2` among forbidden
  *exported* types) passes. It is also **unreferenced dead code** — `grep` finds zero usages elsewhere in
  `src/`, and the live authoring path uses the new standalone `public sealed`
  `EphemeralWorkflowBuilder<TInput,TState>` / `DurableWorkflowBuilder<TInput,TState>` families in
  `PublicStagedAuthoring.cs`, which do **not** derive from `SelectedWorkflowBuilder`. Dead or not, task
  4.12 says "delete," and this is a residual "duplicate mixed-mode … path." Not release-affecting to the
  shipped public surface, but it fails a checked Section-4 task and must be removed (or explicitly re-scoped).

### P2-2 — The positive contract-compile fixture is not asserted by any in-suite guard (coverage/scoping gap)

- **Observation:** P1-1's regression sailed through the 43/43 Infrastructure lane because **no xUnit guard
  actually builds `ExactAuthoring.csproj`.** The only in-suite `dotnet build` invocation
  (`AuthoringContractGuards.cs:194 Build(...)`) builds the *package* `ProductAuthoring`/`ProductForbiddenAuthoring`
  fixtures, and that test lives in the **ExpectedRed** class (`AuthoringContractExpectedRedGuards`,
  `AuthoringContractGuards.cs:131`). The infrastructure authoring guards
  (`Companion_HasExactRootEndParallelCompletionAndLambdaShape`,
  `ProductPositiveFixture_ConsumesOnlyThePackedProduct…`) only read *source text* of the companion and the
  csproj. So the exact-consumer positive compile is validated **only** by the out-of-band
  `run-compile-fixtures.ps1` script, not by the suite Section 4 points to as "green."
- **Why it matters (audit area 5):** "Guard assertions must be scoped … and must not pass from unrelated
  matches" and "positive fixtures must actually compile." The green compile lane should be asserted by a
  guard in the suite (or the script must be a gated step whose result is recorded), so a broken positive
  fixture cannot pass silently again.

### P3-1 — Residual superseded internal builders `SagaBuilder` / `WorkflowDagBuilder` (task 4.10 / area 4 judgment)

- **Where:** `src/OrcaCore.Core/Building/SagaBuilder.cs:12` (`internal sealed class SagaBuilder<TState>`) and
  `src/OrcaCore.Core/Building/WorkflowDagBuilder.cs:10` (`internal sealed class WorkflowDagBuilder`).
- **Determination (area 4 asks explicitly):** Both are `internal` and **unreferenced dead code** (zero
  usages in `src/`), and the public-export guard already proves neither `SagaBuilder\`1` nor
  `WorkflowDagBuilder` is exported. The forbidden-surface intent of task 4.10 ("delete **public** …")
  is therefore satisfied at the public boundary. `WorkflowDagBuilder` legitimately overlaps Section 8
  (`OrcaCore.Dag`) and Saga is a Decision-13 future-registry item, so their *physical deletion* is
  reasonably deferrable to the Section-7 package-tier / Section-8 DAG cleanup. Recorded as **P3 (tolerable,
  noted)** rather than a blocker, but they should be deleted or annotated as an explicit deferral so a
  future audit does not read them as a live surface.

---

## 4. Expected-red inventory and why each remaining red belongs to a later section

The ExpectedRed lane (`--filter "Disposition=ExpectedRed"`) = **7 passed, 22 failed (29 total)**. The 22
failures decompose into **12 contract/manifest/assembly facts + 10 executable-behavior fixtures**:

**Executable-behavior theory** `ExecutableBehaviorExpectedRedGuards.EveryScenario_…` — one failing case per
fixture (10). Each fixture is a **whole-fixture gate** requiring a runtime `Phase0Scenario` driver for
*every* scenario it contains; the union of each fixture's `turnsGreenTask` values spans beyond Section 4,
so none can be green at a Section-4 exit:

| Fixture | `turnsGreenTask` union → owning section(s) |
|---|---|
| `application-journey-scenarios.json` | 7.5–7.8, 7.6, 7.10, 9.1, 6.4–6.9, 8.8–9.4 → **§7/§8/§9** |
| `state-and-codec-scenarios.json` | 4.2, 4.3, 4.4, 4.5, **4.5,7.2**, **4.7,7.5–7.8**, **4.8,7.7–7.8** → **§7** (fixture also needs 7.2/7.5–7.8) |
| `structured-fanout-scenarios.json` | 5.1–5.9 → **§5** |
| `deadline-retry-scenarios.json` | 6.1–6.3, 8.9 → **§6/§8** |
| `dag-contract-scenarios.json` | 8.1–8.10 → **§8** |
| `facade-hosting-scenarios.json` | 7.5–7.10, 5.9, **4.6,7.10** → **§7** |
| `lease-authoring-admission-scenarios.json` | 6.4–6.7, 7.5 → **§6/§7** |
| `lease-discovery-confirmation-scenarios.json` | 6.8–6.9, 7.9, 7.11 → **§6/§7** |
| `lease-retry-exit-scenarios.json` | 6.6–6.9, 8.9 → **§6/§8** |
| `governance-accounting-scenarios.json` | 7.9, 7.11, 6.6 → **§7** |

Note on `state-and-codec`: several scenarios map to pure Section-4 tasks (`definition-id-nonempty`→4.2,
`fixed-codec-determinism`→4.3, `structural-fingerprint-opacity`→4.4, `attempt-local-replace-state`→4.5).
Their remaining red is **not** a Section-4 product gap — the underlying product contracts are implemented
(e.g. `DefinitionId.New()` verified nonempty; portable `StepResult` reduced) — it is the whole-fixture
executable-driver gate that is deferred, because sibling scenarios in the same fixture (`…,7.2`,
`4.7,7.5–7.8`, `4.8,7.7–7.8`) require Section-7 registration/handles/projections/package tiers. Task 4.14
turns the **infrastructure/reflection/contract** guards green (the 43/43 lane), not the executable
runtime-driver lane, which sections 5–8 turn green (5.9, 6.11, 7.13, 8.10).

**Twelve single-fact contract/manifest failures**, each a later-section deliverable:

| Failing guard | Owning section |
|---|---|
| `DagContractExpectedRedGuards.ExactDagAssembliesAndTypesExist` | §8 (OrcaCore.Dag package/types) |
| `FacadeHostingExpectedRedGuards.Product_ExportsTheCommonRegistryAndExactEventClient` | §7 (facades/hosting) |
| `LeaseAuthoringAndExitExpectedRedGuards.Product_ContainsScopedAcquireResourcesAndFinalLifecycle` | §6 (leasing) |
| `LeaseDiscoveryAndGovernanceExpectedRedGuards.Product_ContainsExactFriendBarrierFacts` | §6/§7 |
| `LeaseDiscoveryAndGovernanceExpectedRedGuards.Product_ContainsFinalDiagnosticsRecoveryAndGovernanceStore` | §6/§7 |
| `NormativeContractExpectedRedGuards.ProductAssemblies_MatchExactV1Manifest` | §7 (package manifest) |
| `NormativeContractExpectedRedGuards.ProductBuiltInFailures_HaveOneCanonicalDeclarationInTheirNormativeOwner` | §7 (tier ownership) |
| `NormativeContractExpectedRedGuards.ProductFriendAssemblies_AreExactlyApproved` | §7 (friend reduction) |
| `NormativeContractExpectedRedGuards.ProductProjects_DeclareExactPackageIdAssemblyNameAndRowScopedEdges` | §7 (package projects) |
| `NormativeContractExpectedRedGuards.ProductPublicSignatures_HaveNoForbiddenRecursiveTierEdges` | §7 (tier edges) |
| `PackageConsumerExpectedRedGuards.LocalFeed_ContainsEveryExactManifestPackageWithDeclaredDependencies` | §5/§7 (package extraction/pack) |
| `ProviderAuthorExpectedRedGuards.ExactProviderAuthoringProjects_ExistForTheFixtureToCompileAgainst` | §7 (provider-abstractions split) |

**Friend-assembly question (area 4):** the broad `InternalsVisibleTo` list on `OrcaCore.Core` (engines +
test/sample assemblies) is **not** the exact-approved set of task 3.1 ("only the DAG product friend and
the test-only `OrcaCore.ProviderCertification` barrier friend"). This is correctly encoded as
`NormativeContractExpectedRedGuards.ProductFriendAssemblies_AreExactlyApproved`, which is **still red and
owned by Section 7**. Remaining friend assemblies are therefore a **valid, explicitly-deferred Section-7
transition, not a Section-4 violation.**

**No accidental Section-5 (package-extraction) work:** the package-manifest/local-feed guards remain red
(no manifest packages packed), all §5 tasks are unchecked, and no new package projects appear in the
manifest. Confirmed clean.

---

## 5. Task-by-task disposition (4.0–4.14)

Legend: ✅ met with reproduced evidence · ⚠️ met-with-defect / see finding · ❌ not met.

| Task | Disposition | Evidence / note |
|---|---|---|
| **4.0** Section 3 complete + Phase-0 approved + canonical amendments applied first | ✅ | Phase-0 exit **independently approved** (`…review-e-remediation-and-phase-00-status-2026-07-19.md`: "tasks 3.1–3.12 complete and independently approved; Phase 0 exit approved by the final immutable independent re-review"). Section-4 canonical specs (workflow-authoring/contracts, quality-and-verification) present in manifest as amended per Decision 17. |
| **4.1** Reference `DefinitionId`(New/Parse/TryParse) + positive `DefinitionVersion`(Initial); reject null/`Guid.Empty` | ✅ | `DefinitionId.cs:10` `sealed class`; `New()`/`Parse`/`TryParse`; `Guid.Empty` rejected in both parsers. `DefinitionVersion.cs:10` `sealed class`, `Initial => new(1)`, positive-validating ctor (`ArgumentOutOfRangeException`). Core suite green. |
| **4.2** Strong values: caller-created `Create(string)` only; runtime-created `Parse`/`TryParse`, no `Create` | ✅ | Consistent with infra guards + `state-and-codec` ledger (`caller-created-text-values`, `runtime-created-identities`). Product build clean. Executable driver deferred (see §4). |
| **4.3** Exact factory/init/root/nested/branch/item/leased/scope/join/completion signatures; four `End`; `Build`/`TryBuild`; one root `Parallel` family | ⚠️ | New `public sealed` staged builders exist (`PublicStagedAuthoring.cs`, `PublicNestedAuthoring.cs`, `StructuredBranchBuilders.cs`); companion-shape infra guard green. **But the exact-consumer positive compile fixture that exercises these signatures does not compile — P1-1.** |
| **4.4** Resultless/resultful ephemeral+durable definitions + `DurableWorkflowRef<…>`; no private-state exposure | ✅ | `PublicWorkflowDefinitions.cs`; consumed by `PositiveUsage.ReadMetadata`; IR-opacity guards green. |
| **4.5** Exactly the four `End` overloads per mode; reject null selectors/outcomes; remove optional/nullable + dynamic outcome-name selectors | ⚠️ | New builders honor this and infra ledger (`ephemeral/durable-resultless/resultful-end-*`) is exact. **However a dynamic `End(string? outcomeName = null)` still exists on the undeleted `SelectedWorkflowBuilder` — P2-1** (internal/dead, but a checked-task contradiction). |
| **4.6** `StepExecutionContext`/`StepOperationId`, attempt-local `State`+`ReplaceState`; reduce portable `StepResult` to `Completed`/`Failed`/`WaitForEvent` | ✅ | `StepResult.cs`: public `Completed`(:17)/`Failed`(:22)/`WaitForEvent`(:36); `Yield`/`ContinueAsNew`/`RunExternalJob`/`AcquireResources` are `internal`. |
| **4.7** Ephemeral lambda steps `ValueTask`/cancellation-aware only; no `Action` overload/no lambda `StepResult`; named DI `IStep<TState>` for durable | ✅ | Companion guard asserts `Func<StepContext<TState>,ValueTask>` (+CT) present and `Action<StepContext<` absent; ephemeral suite green. |
| **4.8** Preserve selected mode in nested/branch/item/leased; nested `If`, root-only `Parallel`/`While`/`ForEach`; static+compiler fan-out absence | ✅ | Companion `Companion_RestrictsParallelAndLeasedCapabilities…` green; nested/branch/item/leased builders expose no `Parallel`/fan-out; ephemeral+durable suites green. |
| **4.9** Delete `WaitLong`, author `Yield`; durable `Wait` cold-capable; quantum/checkpoint yielding internal | ✅ | `WaitLong` absent from all `src/**/*.cs` (only stale compiled DLLs match). `Yield` is `internal record` in `StepResult`. |
| **4.10** Delete public jobs, Saga, `WhenFirst`, public children, nested `While`/`ForEach`, durable lambdas, definition/management retry, pause/resume/archive/purge, publish/cancel, event fanout, aliases, tombstones, placeholders | ⚠️ | Public surface clean (forbidden-export guard green; `DurableManagement` lacks Pause/Resume/Archive/Purge). **Residual internal dead `SagaBuilder`/`WorkflowDagBuilder` remain — P3-1** (deferrable). `WhenFirst` still present on internal dead `SelectedWorkflowBuilder` — folded into P2-1. |
| **4.11** Hide compiled plans/instructions/scopes; expose only immutable identity/version/mode/contract/fingerprint/metadata | ✅ | `Compilation/*` types are `internal sealed record` (`CompiledWorkflowPlan`, `CompiledInstruction`, `CompiledScopePlan`, …); no public compiled IR found. |
| **4.12** Delete superseded mixed-mode builder, `RequiresDurableEngine`, legacy plan fallback, mode inference, duplicate mixed-mode paths | ❌ | `RequiresDurableEngine`/`FromLegacy` removed, **but the mixed-mode builder `SelectedWorkflowBuilder<TState,TSelf>` is not deleted (only `internal`) — P2-1.** Checked task not satisfied. |
| **4.13** Local arg rejection; eager single-diagnostic `WorkflowDefinitionException` (`SFE-AUTH-DEADLINE-001`); aggregate graph diagnostics via `Build`/`TryBuild` incl. empty root `Parallel` (`SFE-AUTH-BRANCH-004`); complete catalog, severity, `AuthoredLocation` grammar | ✅ | Codes present (`WorkflowDiagnosticCatalog.cs`, `PublicStagedAuthoring.cs`); `WorkflowDefinitionException` is `public sealed : OrcaCoreException` with internal ctor + `IReadOnlyList<WorkflowDiagnostic> Diagnostics`. Core suite green (incl. the ledger parity scenarios). |
| **4.14** Turn section-3 typed-authoring/reduced-surface/strong-value/stage/definition/registration/projection/IR-opacity guards green; review every baseline change | ⚠️ | Infrastructure lane 43/43 green and reproduced. **But (a) the exact-consumer positive compile fixture is red — P1-1; (b) "review every baseline change" did not catch the undeleted mixed-mode builder — P2-1; (c) the positive compile is not asserted by any in-suite guard — P2-2.** |

---

## 6. Explicit statements

- **May Section 5 begin?** **NO.** Section 4 is not certifiably complete. A required validation (the exact
  consumer positive compile fixture / green compile-fixtures lane) is red (P1-1), and a checked Section-4
  task (4.12, delete the superseded mixed-mode builder) is not satisfied (P2-1). Per the verdict rules a
  compile-fixture gap and an ownership/surface gap each independently withhold approval, and partial
  credit is not Section-4 completion.
- **Files edited during review:** **NONE.** This review created exactly one new file
  (`docs/review/developer-facing-interface-section-04-independent-exit-review-2026-07-21.md`). No source,
  test, spec, task, OpenSpec, or documentation file was edited, staged, committed, reverted, or cleaned.
  The pre-existing `docs/review/developer-facing-interface-section-04-exit-review-dirty-manifest-2026-07-21.txt`
  (a raw 303-line `git status` dump, not a verdict) was left untouched.

---

## 7. Blocker summary (what must be fixed before a re-review can approve Section 4)

1. **P1-1** Restore the ExactAuthoring positive compile fixture to green — add a `StepResult` stand-in to
   `CompileFixtures/ExactAuthoring/SupportTypes.cs` (or revert `ProbeStep` to the empty form) so
   `run-compile-fixtures.ps1 -Disposition Green` and a direct `dotnet build ExactAuthoring.csproj` succeed.
2. **P2-1** Delete `src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs` (the superseded mixed-mode builder,
   with its `WhenFirst`, dynamic `End(string?)`, nested `While`, optional-param `WithRetry`) to satisfy task 4.12.
3. **P2-2** Add an in-suite guard that actually compiles the ExactAuthoring positive fixture (or gate the PS
   script and record its result), so a broken positive fixture cannot pass the "43/43" lane silently.
4. **P3-1** (recommended, not strictly blocking) Delete or explicitly annotate the residual internal dead
   `SagaBuilder<TState>` / `WorkflowDagBuilder` per task 4.10 / their Section-7/8 re-scoping.

## 8. Manifest comparison

Frozen at review start at **302** entries (sha256 `7e4d089b…`). Nothing in the tracked, pre-existing
manifest was modified by this audit. Test/build writes went only to git-ignored `bin/`, `obj/`,
`TestResults/`, and package-cache paths, and to the session scratchpad — none appear in the manifest.

Two paths appeared *after* the freeze:
1. `docs/review/developer-facing-interface-section-04-independent-exit-review-2026-07-21.md` — **this
   review** (the sole file I authored).
2. `docs/review/developer-facing-interface-section-04-exit-review-dirty-manifest-2026-07-21.txt` — a raw
   `git status` dump (mtime 2026-07-21 18:02:51, not git-ignored) that was **created by another process,
   not by this review** (my commands only read it via `head`/`wc`/`stat`). It is flagged here purely for
   transparency, as evidence of concurrent activity in the working tree during the audit window.

Excluding those two post-freeze additions, `git status --porcelain=v1` reproduces the frozen 302-entry
manifest exactly. No tracked baseline path was edited, staged, reverted, or cleaned.
