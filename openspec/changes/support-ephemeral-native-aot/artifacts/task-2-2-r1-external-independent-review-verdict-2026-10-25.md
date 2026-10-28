# Issue #13 task 2.2 R1 — external independent review verdict

**Date:** 2026-10-25
**Reviewer:** an independent reviewer (Claude), separate from the author's self-review in
`task-2-2-r1-independent-review-verdict-2026-10-25.md`, which this file neither replaces nor edits.

**Target:** base HEAD `10aec46`, the 94 paths in `task-2-2-r1-review-manifest-2026-10-25.txt`.
- Content SHA-256 `45057db2f403190259b54c30a66949a5c2f383d20c3c55b07f2beb96348ff27b`, recomputed with
  the request's rule (an exact match).
- Tree `2e276cbeddf908ff41d75c762582fec80e6737b8` (reproduced).
- The porcelain hash differs from the freeze value only by the later request and self-review files,
  which are outside the manifest.

## Verdict: APPROVE task 2.2 as a green slice

There are no P1 findings. Task 2.2 may stay ticked, and work may continue to task 2.3 inside the same
uncommitted task-2 target. None of the following is granted:
- a task-2 checkpoint;
- native qualification;
- source approval;
- publication.

Those stay with the later whole-target gate. Fix F1 (P2) before that gate.

## What I verified

**The converter amendment was applied exactly as the owner approved it.**
- The only change to `StrongStringValueJsonConverterFactory.cs` is the single `internal` → `public`
  token.
- `StrongValues.cs` is byte-equal to HEAD.
- `PublicFactory_ChangesOnlyTheApprovedAccessibilityToken` pins both facts against HEAD.
- The OrcaCore API baseline adds exactly the factory type, its constructor and the two overrides.

**F2 is resolved by conformance, not by relaxing a test.**
- **Defect.** The ephemeral adapter recorded scope child failures, then terminalized the instance from
  the last leaf as a flat string. Public snapshots therefore published `WF-LEGACY-LIFECYCLE` with zero
  causes.
- **Contract.** Canonical `workflow-contracts` already requires two things:
  - "Public failure exceptions carry stable codes": a committed `WorkflowFailure` retains the
    exception's code;
  - "Workflow failures carry authored and occurrence provenance": a single `WhenAll` failure
    propagates unchanged, and several branch failures become `SFE-JOIN-FAILED` with ordered causes.
- **Fix.**
  - A failed scope now routes through the pre-existing `FailScope` / `ScopeReducer.AggregateFailures`
    path, which already cancelled waits and was already used by other scope failures.
  - The immutable public failure graph travels through `WorkflowErrorDetails` and the snapshot, and
    the summary fallback is kept.
  - No fiber, state, provider or scope object is published.
  - The change touches only the seven Ephemeral publication paths. The codec, converter bodies and
    public failure types are unchanged.
- **Tests.** No pre-existing runtime test was modified; the only edit to a test file that predates
  the change is the AC mapping in `RepositoryGuardTests`. The six CI unit projects pass unchanged.
- **The `WF-LEGACY-LIFECYCLE` cause codes in the new golden are legitimate.** The fixture's failing
  steps deliberately return `WorkflowLifecycleException`.
- **Visible behavior change.** Applications reading `Failure.Code` will now see real codes instead of
  `WF-LEGACY-LIFECYCLE` (P3-1).

**The fixture setup corrections are not weakening.**
- `[2, 1]` became `new List<int> { 2, 1 }` for an `IReadOnlyList<int>` input. The fixed codec's
  runtime allowlist predates this change and is independent of mode. For an interface-declared
  sequence it accepts only `T[]` or `List<T>`, so the compiler-synthesized read-only type is rejected
  in legacy JIT too.
- The other corrections (`string[]` in the closure, value equality for the location, the version
  field in the golden JSON) relax no assertion.

**The supplemental controls are present.**
- **NS-06g.** All eleven types are tested:
  - as roots, nested members and list elements;
  - with the independent literal `"catalogue-value"`, an escaping sample, null, and blank or padded
    rejections;
  - in both Metadata and Default contexts.
- **NS-02d.** An explicit `Converters = { factory }` context is rejected with `sealed=False`, and a
  valid context then registers.
- **NS-06h.** The factory is sealed and has 11 `CanConvert` matches. `string` is false, and
  `CreateConverter(string)` throws `NotSupportedException`. After direct use, registration still
  succeeds.

**Metadata registry.** I read `WorkflowJsonMetadataRegistry` (this is my first external review of
it).
- Reservation happens under a short lock, and construction and validation happen outside it.
- Waiting for the same type and conflicting on a different type both work.
- Reentrancy is detected through an async-local scope, so it is rejected rather than deadlocking.
- If first use seals the registry during construction, the registration fails cleanly.
- Any context-level converter is rejected.

**Independent package-only proof.** I copied the in-repo fixture outside the repository and switched
it to `PackageReference` against my own freshly packed 12-package feed. The restore used an isolated
cache, cleared fallback folders and a source mapping that allows only OrcaCore.
- It resolved only `OrcaCore`, `OrcaCore.Core` and `OrcaCore.Engine.Ephemeral`.
- It built with **0 warnings and 0 errors** under warnings-as-errors, including the external
  `StrongMetadataContext` and `StrongDefaultContext`. SYSLIB1220 and SYSLIB1030 are gone.
- All 28 fresh-process scenarios pass.
- The `strong-legacy`, `strong-metadata` and `strong-default` fingerprint rows are identical
  (`2ea7b8cb…`).

**Lanes,** in a disposable copy at `E:/ocv209` (a simulated commit of tree `2e276cbe…` on `10aec46`):

| Lane | Result |
|---|---|
| OpenSpec strict | 25/25 |
| Release `-warnaserror` | 0 warnings, 0 errors |
| Exact feed | 12 packages |
| Infrastructure | 296/296 |
| Six CI unit projects | 969/969: 354 + 352 + 102 + 37 + 24 + 100 |

I did not re-run ReSharper, coverage, SQL integration or any native host. No native credit is given.

## F1 — P2 — The approval binding still cites self-reviews as the review evidence

**Problem.** `task-2-2-catalogue-approval-binding-2026-10-25.json` sets `verdictPath` to the author's
self-review, `contract-converter-catalogue-amendment-independent-review-verdict-2026-10-25.md`
(`11b91e94…`). `NativeSupportTransitionGuards` checks that path. My amendment verdict required the
binding to cite the external verdict, and it does not:
`contract-converter-catalogue-amendment-external-independent-review-verdict-2026-10-25.md`.

**Required before the task-2 checkpoint.**
- Add the external verdict path and SHA-256 to the binding, as the independent review evidence, and
  pin them in the guard. The self-review may stay as transparency evidence.
- When task 2.2 approval is bound, bind **this** verdict rather than the R1 self-review.
- Do not rename the dated self-review files.

## P3 (optional)

- **P3-1 — Release notes.** Ephemeral `Failure.Code`/`Causes` now publish structured codes and trees
  instead of `WF-LEGACY-LIFECYCLE` with no causes. This conforms to the contract but is visible to
  consumers of `0.1.0-preview.2`, so record it in the prerelease notes and migration guidance.
- **P3-2 — NS-06h completeness.** `public-factory-no-seal` checks a direct `CreateConverter` round
  trip only for `EventName`. The amendment asks for positive conversion of all eleven types in that
  process; the other ten are covered only through the generated contexts, in separate processes. Add
  the remaining ten direct round trips.
- **P3-3 — Native watch item.** `WorkflowJsonMetadataRegistry.Register` checks the context's
  provenance through `GetCustomAttribute<GeneratedCodeAttribute>` and
  `GetCustomAttributesData()`. Confirm in the task-2.4 native cells that this attribute metadata
  survives trimming and AOT. Otherwise every valid registration would be rejected as having
  unsupported provenance.

## Cleanup

My scratch folders `E:/ocv209-pkg` and `E:/ocv209-logs` (and the earlier `E:/ocv208probe`) need
manual removal. I remove the disposable worktree `E:/ocv209` through `git worktree remove`.
