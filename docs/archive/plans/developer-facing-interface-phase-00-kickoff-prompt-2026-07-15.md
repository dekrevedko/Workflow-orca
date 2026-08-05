# Developer-facing interface refactor: Phase 0 kickoff prompt

> **Superseded historical prompt (2026-07-19):** do not copy or execute the prompt below.
> It records the pre-remediation Phase 0 scope, task count, package assumptions, and lease/
> fan-out expectations. Current authority is
> [spec 17](../../specs/17-selected-mode-capability-matrix.md), its
> [exact authoring companion](../../specs/17-public-authoring-contract.cs), the active
> [`reshape-developer-facing-interfaces` tasks](../../../openspec/changes/reshape-developer-facing-interfaces/tasks.md),
> and the current
> [remediation status](../../review/developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md).
> The independent planning re-review subsequently authorized guard retargeting. Tasks 3.1 through
> 3.12 are now implemented and independently approved. Phase 0 is complete; task 4.0 remains
> blocked at the requested next-phase review boundary and by its explicit prerequisites. This body
> is preserved unchanged as historical evidence and must still not be executed.

Copy and paste the text below into the implementation agent. Run it from
`X:\Projects\GitHub\Workflow-orca`.

```text
You are implementing Phase 0 of OrcaCore's developer-facing interface refactor.

Repository root:
X:\Projects\GitHub\Workflow-orca

OpenSpec change:
reshape-developer-facing-interfaces

Phase:
Phase 0 - Capture public and consumer guards

OpenSpec task scope:
3.1 through 3.11 only

Expected starting progress when this prompt was written:
15/111 tasks complete, with task 3.1 as the first pending task. Refresh this state before
acting; do not assume it is unchanged.

Terminal condition for this implementation session:
Complete and verify Phase 0, publish its detailed implementation report and reviewer prompt,
request independent review, and STOP. Do not begin task 4.0 or any Phase 1 source work even
though later OpenSpec tasks remain pending.

## Product stance

This repository is greenfield. There are no external API clients, released package contracts,
or retained durable-data contracts to preserve. Existing source and package shapes are
provisional. Build the best coherent final API constraints directly. Do not introduce or
recommend compatibility shims, obsolete aliases, dual execution paths, parallel public
surfaces, or preservation-only adapters.

The structured-fiber compiler, scheduler, typed structured scopes/results, and durable
format-2 driver are accepted substrate. Do not create a second builder, compiler, scheduler,
driver, or execution model.

## Mandatory startup procedure

1. Inspect `git status --short` before editing. The worktree may already contain approved
   planning/review changes. Preserve unrelated edits and record the starting state. Never
   clean, reset, revert, or sweep unrelated changes into Phase 0.
2. Use the OpenSpec apply workflow for `reshape-developer-facing-interfaces` and run:

   openspec status --change "reshape-developer-facing-interfaces" --json
   openspec instructions apply --change "reshape-developer-facing-interfaces" --json

3. Read every context file returned by the apply instructions. Also read these files fully:

   - docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md
   - docs/implementation/README.md
   - docs/implementation/02-engineering-conventions.md
   - docs/implementation/03-tdd-workflow.md
   - docs/implementation/04-task-protocol.md
   - CLAUDE.md
   - .github/workflows/ci.yml
   - docs/specs/17-selected-mode-capability-matrix.md
   - docs/review/developer-facing-interface-post-fiber-openspec-review-2026-07-14.md
   - docs/review/developer-facing-interface-post-fiber-review-remediation-2026-07-14.md
   - docs/review/developer-facing-interface-phase-review-template.md

4. Confirm that the OpenSpec change is ready, tasks 3.1-3.11 are pending, both coordinated
   changes strict-validate, and no newer review or implementation has superseded this prompt.
   If the state has advanced, reconcile the prompt with the live task file and report the
   adjustment before proceeding.
5. Inventory the existing test/fixture infrastructure and public assemblies only as needed to
   place the guards correctly. Prefer existing test support and project conventions. Do not
   explore or modify `archive/`.

## Phase 0 objective

Translate the approved external-developer contracts into executable guards before changing
production behavior. Implement all of the following OpenSpec tasks:

- 3.1: tier-aware public-signature inspection for application, provider-authoring,
  runtime-protocol, and internal types;
- 3.2: definitions for clean package-consumer fixtures covering minimal ephemeral,
  in-memory durable, provider-backed durable, and the small `OrcaCore` meta-package without
  referencing packages that do not yet exist;
- 3.3: definitions for the provider-author fixture proving the declared
  `Provider.Abstractions -> Runtime.Protocol` edge without adding an unrestorable project;
- 3.4: positive and negative compile fixtures for root and nested ephemeral/durable authoring,
  mode-specific registration, durable `ForEach` absence, nested transient-pool absence, and
  compiler defense in depth;
- 3.5: public-baseline guards for compiled-IR absence and application wait projections with
  opaque `WaitId` plus `AuthoredLocation`, excluding fiber/scope/wait-sequence routing IDs;
- 3.6: behavior guards for detached committed root state through the configured
  serializer/copier and stable authored active-wait matching metadata;
- 3.7: declaration guards requiring exactly one canonical `WorkflowInstanceQueryModel`,
  `WorkflowStatistics`, `WorkflowStatisticsGroup`, and
  `DestructiveOperationConfirmation`;
- 3.8: a durable-example regression that rejects `Poisoned`, direct
  `DurableCommandProcessor` use, terminal-then-job ordering, and raw protocol types in the
  application journey;
- 3.9: a two-host acceptance fixture where a definition-less callback host reports an
  external-job outcome and a definition-owning continuation pump progresses it exactly once;
- 3.10: facade guards for no-match, ambiguous-match, live-unmatched, paused-target,
  definition-not-registered, stale-remediation, completion, timeout, and worker-failure
  outcomes;
- 3.11: durable-lease lifecycle guards for normal scope exit, canceled branch, failed scope,
  terminal transition, and crash/restart expiry recovery.

## Phase 0 implementation rules

1. Phase 0 is guard and fixture infrastructure only. Do not implement Phase 1 production API
   changes to make these guards green. Production behavior and public signatures remain
   unchanged in this phase unless a minimal non-behavioral testability correction is strictly
   necessary; any such correction is a design deviation and must be justified in the report.
2. Use bounded TDD packets. Keep each packet focused, with explicit task IDs and an external
   contract it proves. Split large work rather than producing one oversized test module.
3. Each guard must fail for the intended missing contract or current leak—not because of a
   typo, bad path, missing restore source, unsupported SDK, fixture setup failure, or an
   invented signature inconsistent with the normative capability matrix.
4. Treat Phase 0's intentional red results honestly. Record the exact command, test/fixture,
   observed failure, expected contract, and future phase that will turn it green. Do not use
   vacuous assertions, blanket skips, ignored exit codes, over-broad snapshots, or assertions
   that merely freeze the current provisional surface.
5. Keep intentional-red guards separately and explicitly invocable so the normal build and
   unaffected baseline suites remain useful and green. Their isolation must be visible and
   documented; do not silently omit them from verification.
6. Positive compile fixtures and all fixture infrastructure must build successfully. Negative
   compile fixtures should be verified by a harness that expects the precise compiler failure;
   the harness itself should pass when the intended code does not compile.
7. Public-signature and declaration guards must use deterministic, reviewable baselines. Every
   classified public type needs an audience or an explicit finding. Do not optimize for a type
   count.
8. Acceptance fixtures must exercise public application entry points. Kernel-only access,
   `InternalsVisibleTo`, or raw protocol construction cannot establish that an application
   journey is supported.
9. Add one or more explicit regression guards for every actionable contract in tasks 3.1-3.11.
   Cover adjacent variants when the same leak or failure mode could occur at root and nested,
   ephemeral and durable, or single-host and split-host seams.
10. Use deterministic time and concurrency coordination. Do not add sleeps or probabilistic
    race loops.
11. Mark an OpenSpec task complete only after its entire guard/fixture scope exists, restores
    and builds as applicable, fails or passes for the intended reason, and has recorded
    evidence. For Phase 0, an intentional-red guard satisfies this rule when the harness builds
    and the guard fails for the approved contract reason recorded in the red ledger; checking
    the task does not claim the product behavior is green. Update checkboxes immediately after
    verification.
12. Do not perform durable transient-governance source work from
    `add-runtime-concurrency-limits`; that belongs to Phase 3. In this phase, use the coordinated
    change only to verify vocabulary and fixture expectations.

## Required verification

Run the narrow affected test/fixture command after each packet, then the adjacent guard suites,
then the normal solution build. At phase completion, record exact passed, failed, skipped, and
environment-gated counts for at least:

- every Phase 0 guard and compile/consumer/provider fixture command;
- every existing test project changed to host the new infrastructure;
- the unaffected baseline suites needed to prove no regression;
- a live baseline record that refreshes the planning references of 107 integration tests
  passed / 0 failed / 1 intentionally skipped (`INT_JS_018`) and 1,218 post-fiber verification
  tests passed / 0 failed / 1 skipped, explaining any current difference;
- confirmation that CI still enforces at least 0.80 aggregate line coverage for
  `OrcaCore.Engine.*`; run the CI-equivalent coverage command in Phase 0 only if production
  engine code is changed, and otherwise record that the gate was inspected but not remeasured;
- `dotnet build OrcaCore.slnx` with zero warnings;
- `openspec validate reshape-developer-facing-interfaces --strict`;
- `openspec validate add-runtime-concurrency-limits --strict`;
- Markdown relative-link validation for changed documentation;
- `git diff --check`;
- a final task-count check from `openspec instructions apply` or `openspec list`.

Do not report an intentional-red guard as a passed test. Report it in a separate expected-red
ledger with exact counts and reasons. Unexpected failures must be fixed before Phase 0 can be
submitted for review.

If container-backed or external-infrastructure verification is genuinely required but cannot
run, record the exact command, prerequisite, and reason. Do not represent it as passed and do
not use an environment limitation to hide a fixture setup defect.

## Required review handoff

After tasks 3.1-3.11 are complete:

1. Create the detailed implementation report using the actual completion date:

   docs/review/developer-facing-interface-phase-00-public-consumer-guards-implementation-status-<YYYY-MM-DD>.md

2. The report must include:

   - objective and task disposition for every task 3.1-3.11;
   - external-developer contract represented by each guard;
   - every file/project changed and why;
   - fixture architecture and how future phases turn the guards green;
   - exact expected-red ledger and exact green suite/build counts;
   - live integration/post-fiber baseline counts and the engine-coverage gate disposition;
   - positive/negative compile evidence;
   - public-signature classification and baseline approach;
   - package-consumer/provider-author fixture design;
   - deviations, unresolved concerns, and cross-phase dependencies;
   - OpenSpec checkbox/progress state;
   - diff/commit scope and pre-existing changes kept out of the phase;
   - focused reviewer questions and Phase 0 exit-criteria assessment.

3. Create a filled, copy-ready reviewer prompt using the actual completion date:

   docs/review/developer-facing-interface-phase-00-public-consumer-guards-reviewer-prompt-<YYYY-MM-DD>.md

   Base it on:
   docs/review/developer-facing-interface-phase-review-template.md

   Require the reviewer to verify source/test claims, run the fixtures, distinguish expected
   red from broken infrastructure, judge desired signatures from an external developer's
   viewpoint, inspect task completion, and return APPROVE / APPROVE WITH CHANGES / REJECT with
   P0/P1/P2/P3 findings.

4. Finish with a user-facing review request in this exact spirit:

   Phase 0 is complete, and implementation is paused for review.

   Implemented:
   - <specific guard and fixture outcomes>
   - <important external contracts now executable>

   Verification:
   - <green command and exact counts>
   - <expected-red command and exact counts/reasons>
   - strict validation/build/link/diff results

   Known deviations or remaining phase-local concerns:
   - <none or precise items>

   Review materials:
   - Implementation report: <absolute link>
   - Copy-ready reviewer prompt: <absolute link>

   Please review especially: consumer realism, signature expectations, negative capability
   coverage, split-host semantics, durable-lease lifecycle coverage, and expected-red
   isolation. I will not start Phase 1 until the verdict and required remediation are resolved.

5. STOP. Do not implement Phase 1, do not check task 4.0, and do not archive either active
   change.

## Blocker policy

If a task is ambiguous, the live artifacts conflict, or a correct fixture cannot be built
without making a public design decision not already approved, stop the affected packet. Record
the exact evidence and propose the smallest artifact correction. Do not invent a contract or
silently broaden the phase.

If commits are created, keep them phase-scoped and do not stage or commit unrelated existing
changes. Report every commit hash in the implementation report.
```
